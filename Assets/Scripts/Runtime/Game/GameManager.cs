using System.Collections.Generic;
using UnityEngine;
using Cannon.Gravity;
using Cannon.Targets;
using Cannon.CannonControl;
using Cannon.Flow;

namespace Cannon.Game
{
    public enum GameState { Aiming, Charging, Fired, Won, Lost }

    /// <summary>
    /// Drives the whole playable slice at runtime: builds each level in code, handles
    /// aim / hold-charge / fire input, shows the gravity-aware trajectory preview,
    /// resolves shots, and advances the win/lose/next-level flow. See docs/PLAN.md.
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        [System.Serializable]
        public struct LevelData
        {
            public float PlanetRadius;
            public float PlanetMass;
            public float FieldRadius;
            public float[] PigAngles;   // degrees around the planet where pigs sit on the surface
            public int ShieldsPerPig;   // blocks stacked outward in front of each pig
        }

        // Lower forces so the lowest charge no longer escapes the planet's pull.
        private static readonly ChargeSettings Charge = new ChargeSettings
        {
            ChargeTime = 1.2f, MinForce = 3f, MaxForce = 8f
        };

        private const float ZoomMin = 6f;
        private const float ZoomMax = 34f;
        private float _zoom = 16f;

        private readonly List<GameObject> _spawned = new List<GameObject>();
        private readonly List<Pig> _pigs = new List<Pig>();
        private readonly List<Vector3> _preview = new List<Vector3>();

        private Camera _cam;
        private LineRenderer _line;
        private Transform _cannon;
        private Vector3 _muzzle;

        private GameState _state;
        private int _levelIndex;
        private int _shotsFired;
        private float _holdTime;
        private OrbitalProjectile _activeShot;
        private float _resolveTimer;
        private float _levelTime;
        private const float LevelTimeLimit = 120f;

        private LevelData[] _levels;

        // ---- Public API (input, AI, and headless tests) ----
        public GameState State => _state;
        public int LevelIndex => _levelIndex;
        public int LevelCount => _levels?.Length ?? 0;
        public int ShotsFired => _shotsFired;

        public int PigsAlive
        {
            get { int n = 0; foreach (var p in _pigs) if (p != null && !p.IsDead) n++; return n; }
        }

        public IReadOnlyList<Pig> Pigs => _pigs;

        public void LoadLevelPublic(int index) => LoadLevel(index);

        private void Start()
        {
            Application.targetFrameRate = 60;
            Physics.gravity = Vector3.zero; // objects are held to the planet by SurfaceGravity

            _cam = Camera.main;
            SetupCameraAndLine();

            var starfield = new GameObject("Starfield").AddComponent<Starfield>();

            _levels = BuildLevels();
            LoadLevel(0);
        }

        // ---- Level definitions -------------------------------------------------
        // Pigs sit on the planet surface at given angles; shields stack outward in front.

        private LevelData[] BuildLevels()
        {
            return new[]
            {
                new LevelData
                {
                    PlanetRadius = 4f, PlanetMass = 34f, FieldRadius = 40f,
                    PigAngles = new[] { 150f }, ShieldsPerPig = 2
                },
                new LevelData
                {
                    PlanetRadius = 4.5f, PlanetMass = 40f, FieldRadius = 46f,
                    PigAngles = new[] { 120f, 160f }, ShieldsPerPig = 2
                },
                new LevelData
                {
                    PlanetRadius = 5f, PlanetMass = 48f, FieldRadius = 52f,
                    PigAngles = new[] { 110f, 140f, 170f }, ShieldsPerPig = 2
                }
            };
        }

        private static readonly Vector3 PlanetCenter = Vector3.zero;

        // ---- Level lifecycle ---------------------------------------------------

        private void LoadLevel(int index)
        {
            ClearLevel();
            _levelIndex = index;
            LevelData lvl = _levels[index];
            _shotsFired = 0;
            float r = lvl.PlanetRadius;

            // Planet at center (comet-toned, solid — the shot bounces off it).
            var planet = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            planet.name = "Planet";
            planet.transform.position = PlanetCenter;
            planet.transform.localScale = Vector3.one * (r * 2f);
            Paint(planet, new Color(0.62f, 0.68f, 0.72f)); // soft icy-comet grey-teal
            var body = planet.AddComponent<GravityBody>();
            body.Kind = BodyKind.Planet; body.Mass = lvl.PlanetMass;
            body.Radius = r;
            body.FieldRadius = lvl.FieldRadius; body.Softening = 0.5f;
            Track(planet);

            // Cannon floating in space up-left of the planet.
            var cannon = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cannon.name = "Cannon";
            cannon.transform.position = new Vector3(-(r + 11f), r + 7f, 0f);
            cannon.transform.localScale = Vector3.one * 1.4f;
            Paint(cannon, new Color(0.85f, 0.82f, 0.7f));
            Object.Destroy(cannon.GetComponent<Collider>());
            _cannon = cannon.transform;
            _muzzle = _cannon.position;
            Track(cannon);

            // Pigs on the surface at given angles, each with shields stacked outward.
            _pigs.Clear();
            foreach (float deg in lvl.PigAngles)
            {
                Vector3 dir = new Vector3(Mathf.Cos(deg * Mathf.Deg2Rad), Mathf.Sin(deg * Mathf.Deg2Rad), 0f);

                var pigGo = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                pigGo.transform.position = PlanetCenter + dir * (r + 0.6f);
                pigGo.transform.localScale = Vector3.one * 0.9f;
                Paint(pigGo, new Color(0.55f, 0.8f, 0.5f)); // soft green
                MakeDynamic(pigGo, 0.6f);
                var pig = pigGo.AddComponent<Pig>();
                pig.HitPoints = 1f; pig.DamageThreshold = 1f;
                _pigs.Add(pig);
                Track(pigGo);

                for (int k = 0; k < lvl.ShieldsPerPig; k++)
                {
                    var block = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    block.transform.position = PlanetCenter + dir * (r + 1.7f + k * 1.1f);
                    block.transform.rotation = Quaternion.FromToRotation(Vector3.up, dir);
                    Paint(block, new Color(0.72f, 0.6f, 0.42f)); // warm sandstone
                    MakeDynamic(block, 1f);
                    Track(block);
                }
            }

            _state = GameState.Aiming;
            _holdTime = 0f;
            _levelTime = 0f;
            _zoom = Mathf.Clamp(r * 2.6f + 8f, ZoomMin, ZoomMax);
        }

        private void ClearLevel()
        {
            GravityRegistry.Clear();
            foreach (var go in _spawned) if (go != null) Object.Destroy(go);
            _spawned.Clear();
            _pigs.Clear();
            _activeShot = null;
            if (_line != null) _line.positionCount = 0;
        }

        private void Track(GameObject go) => _spawned.Add(go);

        /// <summary>Add a dynamic rigidbody held to the planet by SurfaceGravity.</summary>
        private void MakeDynamic(GameObject go, float mass)
        {
            var rb = go.AddComponent<Rigidbody>();
            rb.mass = mass;
            rb.useGravity = false;
            rb.constraints = RigidbodyConstraints.FreezePositionZ;
            go.AddComponent<SurfaceGravity>();
        }

        // ---- Input & flow ------------------------------------------------------

        private void Update()
        {
            // Scroll to zoom in/out.
            float scroll = Input.mouseScrollDelta.y;
            if (Mathf.Abs(scroll) > 0.01f)
                _zoom = Mathf.Clamp(_zoom - scroll * 2f, ZoomMin, ZoomMax);
            if (_cam != null) _cam.orthographicSize = _zoom;

            if (_state == GameState.Aiming || _state == GameState.Charging || _state == GameState.Fired)
            {
                _levelTime += Time.deltaTime;
                if (_levelTime >= LevelTimeLimit && PigsAlive > 0)
                {
                    _state = GameState.Lost;
                    return;
                }
            }

            switch (_state)
            {
                case GameState.Aiming:
                case GameState.Charging:
                    HandleAimAndCharge();
                    break;
                case GameState.Fired:
                    break;
            }
        }

        private void HandleAimAndCharge()
        {
            Vector3 aimDir = (MouseWorld() - _muzzle);
            aimDir.z = 0f;
            if (aimDir.sqrMagnitude < 0.001f) aimDir = Vector3.right;

            if (Input.GetMouseButton(0))
            {
                _state = GameState.Charging;
                _holdTime += Time.deltaTime;
                ShowPreview(aimDir, _holdTime);

                if (ChargeModel.ShouldAutoFire(_holdTime, Charge))
                    Fire(aimDir, _holdTime);
            }
            else
            {
                if (_state == GameState.Charging)
                    Fire(aimDir, _holdTime);
                else
                    ShowPreview(aimDir, 0.4f); // faint aim hint at low power
            }
        }

        /// <summary>Test/AI hook: fire a solved shot (best angle + charge) aimed at a world point.</summary>
        public OrbitalProjectile FireAt(Vector3 worldTarget)
        {
            SolveShot(worldTarget, out Vector3 dir, out float hold);
            Fire(dir, hold);
            return _activeShot;
        }

        /// <summary>
        /// Search launch direction AND charge for the shot whose predicted gravity-aware
        /// path passes closest to the target. Used for auto-play and the auto-win test.
        /// </summary>
        public void SolveShot(Vector3 target, out Vector3 bestDir, out float bestHold)
        {
            var wells = new List<GravityWell>();
            GravityRegistry.CollectWells(wells);

            var path = new List<Vector3>();
            float best = float.MaxValue;
            bestDir = (target - _muzzle).normalized;
            bestHold = Charge.ChargeTime;

            float[] holds = { Charge.ChargeTime * 0.4f, Charge.ChargeTime * 0.6f, Charge.ChargeTime * 0.8f, Charge.ChargeTime };

            for (int deg = -30; deg <= 100; deg += 2)
            {
                Vector3 dir = Quaternion.Euler(0f, 0f, deg) * Vector3.right;
                foreach (float hold in holds)
                {
                    Vector3 vel = ChargeModel.LaunchVelocity(dir, hold, Charge);
                    TrajectorySampler.Sample(_muzzle, vel, 1f, wells, Time.fixedDeltaTime,
                        maxSteps: 260, stride: 1, path);

                    for (int i = 0; i < path.Count; i++)
                    {
                        float d = (path[i] - target).sqrMagnitude;
                        if (d < best) { best = d; bestDir = dir; bestHold = hold; }
                    }
                }
            }
        }

        private void Fire(Vector3 aimDir, float hold)
        {
            // Unlimited cannon balls: no ammo gate.
            _shotsFired++;
            _holdTime = 0f;
            _state = GameState.Fired;
            if (_line != null) _line.positionCount = 0;

            var shot = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            shot.name = "Shot";
            shot.transform.position = _muzzle;
            shot.transform.localScale = Vector3.one * 0.5f;
            Paint(shot, new Color(1f, 0.55f, 0.1f));

            var rb = shot.AddComponent<Rigidbody>();
            rb.isKinematic = true; rb.useGravity = false;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;

            var proj = shot.AddComponent<OrbitalProjectile>();
            proj.G = 1f; proj.MaxSpeed = 45f; proj.MaxLifetime = 10f;
            var hit = shot.AddComponent<ProjectileCollision>();
            hit.BurstRadius = 3.2f;
            proj.Launch(ChargeModel.LaunchVelocity(aimDir, hold, Charge));
            proj.Ended += OnShotEnded;

            _activeShot = proj;
            Track(shot);
        }

        private void OnShotEnded(OrbitalProjectile proj)
        {
            _resolveTimer = 0f;
            Invoke(nameof(ResolveTurn), 1.2f); // let debris settle briefly
        }

        private void ResolveTurn()
        {
            if (_activeShot != null && _activeShot.gameObject != null)
                Object.Destroy(_activeShot.gameObject);
            _activeShot = null;

            // A pig knocked off the world counts as destroyed (plan section 6).
            int alive = 0;
            foreach (var pig in _pigs)
            {
                if (pig == null || pig.IsDead) continue;
                if (pig.transform.position.y < -14f) { pig.Kill(); continue; }
                alive++;
            }

            if (alive == 0) { _state = GameState.Won; return; }
            _state = GameState.Aiming; // unlimited ammo: keep shooting until the timer runs out

        }

        // ---- Preview -----------------------------------------------------------

        private void ShowPreview(Vector3 aimDir, float hold)
        {
            var wells = new List<GravityWell>();
            GravityRegistry.CollectWells(wells);
            Vector3 vel = ChargeModel.LaunchVelocity(aimDir, hold, Charge);
            TrajectorySampler.Sample(_muzzle, vel, 1f, wells, Time.fixedDeltaTime,
                maxSteps: 120, stride: 4, _preview, stopAtFieldEntry: false);

            _line.positionCount = _preview.Count;
            for (int i = 0; i < _preview.Count; i++)
                _line.SetPosition(i, _preview[i] + Vector3.back * 0.1f);
        }

        // ---- Setup helpers -----------------------------------------------------

        private void SetupCameraAndLine()
        {
            if (_cam == null)
            {
                var camGo = new GameObject("Main Camera");
                camGo.tag = "MainCamera";
                _cam = camGo.AddComponent<Camera>();
            }
            _cam.orthographic = true;
            _cam.orthographicSize = _zoom;
            _cam.transform.position = new Vector3(0f, 2f, -30f);
            _cam.transform.rotation = Quaternion.identity;
            _cam.clearFlags = CameraClearFlags.SolidColor;
            _cam.backgroundColor = new Color(0.16f, 0.17f, 0.28f); // soft dusk-blue space, not black

            var lineGo = new GameObject("Preview");
            _line = lineGo.AddComponent<LineRenderer>();
            _line.widthMultiplier = 0.12f;
            _line.material = MaterialFactory.Unlit(Color.white);
            _line.startColor = _line.endColor = new Color(1f, 1f, 1f, 0.5f);
            _line.positionCount = 0;
        }

        private static void Paint(GameObject go, Color color)
        {
            go.GetComponent<Renderer>().sharedMaterial = MakeMaterial(color);
        }

        /// <summary>Create a colored lit material (never throws; see MaterialFactory).</summary>
        public static Material MakeMaterial(Color color) => MaterialFactory.Lit(color);

        private Vector3 MouseWorld()
        {
            Vector3 sp = Input.mousePosition;
            sp.z = -_cam.transform.position.z; // distance to z=0 plane
            Vector3 w = _cam.ScreenToWorldPoint(sp);
            w.z = 0f;
            return w;
        }

        // ---- UI ----------------------------------------------------------------

        private void OnGUI()
        {
            GUI.skin.label.fontSize = 22;
            GUI.skin.button.fontSize = 22;
            GUI.Label(new Rect(20, 15, 500, 30), $"Level {_levelIndex + 1}   Shots: {_shotsFired}   (unlimited)");

            GUI.Label(new Rect(20, 45, 400, 30), $"Pigs left: {PigsAlive}");
            GUI.Label(new Rect(20, 75, 400, 30), $"Time: {Mathf.Max(0f, LevelTimeLimit - _levelTime):0}s");

            if (_state == GameState.Aiming)
                GUI.Label(new Rect(20, 105, 700, 30), "Aim with mouse. Hold left button to charge, release to fire.");

            if (_state == GameState.Won)
            {
                GUI.Label(new Rect(Screen.width / 2 - 100, Screen.height / 2 - 60, 300, 40), "LEVEL CLEARED!");
                bool last = _levelIndex + 1 >= _levels.Length;
                string label = last ? "You Win! Play Again" : "Next Level";
                if (GUI.Button(new Rect(Screen.width / 2 - 100, Screen.height / 2, 200, 50), label))
                    LoadLevel(last ? 0 : _levelIndex + 1);
            }

            if (_state == GameState.Lost)
            {
                GUI.Label(new Rect(Screen.width / 2 - 120, Screen.height / 2 - 60, 400, 40), "TIME'S UP — level lost");
                if (GUI.Button(new Rect(Screen.width / 2 - 100, Screen.height / 2, 200, 50), "Retry"))
                    LoadLevel(_levelIndex);
            }
        }
    }
}
