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
            public Vector3 PlanetPos;
            public float PlanetRadius;
            public float PlanetMass;
            public float FieldRadius;
            public Vector3[] Blocks;
            public Vector3[] Pigs;
            public int Ammo;
        }

        private static readonly ChargeSettings Charge = new ChargeSettings
        {
            ChargeTime = 1.2f, MinForce = 6f, MaxForce = 20f
        };

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
            Physics.gravity = new Vector3(0f, -12f, 0f);

            _cam = Camera.main;
            SetupCameraAndLine();

            var starfield = new GameObject("Starfield").AddComponent<Starfield>();

            _levels = BuildLevels();
            LoadLevel(0);
        }

        // ---- Level definitions -------------------------------------------------

        private LevelData[] BuildLevels()
        {
            return new[]
            {
                new LevelData
                {
                    PlanetPos = new Vector3(0f, 1f, 0f), PlanetRadius = 2f, PlanetMass = 20f, FieldRadius = 12f,
                    Blocks = Stack(new Vector3(9f, -4.2f, 0f), 1, 3),
                    Pigs = new[] { new Vector3(9f, -3f, 0f) },
                    Ammo = 4
                },
                new LevelData
                {
                    PlanetPos = new Vector3(1f, 2f, 0f), PlanetRadius = 2.5f, PlanetMass = 40f, FieldRadius = 16f,
                    Blocks = Concat(Stack(new Vector3(8f, -4.2f, 0f), 2, 3), Stack(new Vector3(12f, -4.2f, 0f), 1, 2)),
                    Pigs = new[] { new Vector3(8f, -2.8f, 0f), new Vector3(12f, -3.3f, 0f) },
                    Ammo = 5
                },
                new LevelData
                {
                    PlanetPos = new Vector3(0f, 0f, 0f), PlanetRadius = 3f, PlanetMass = 70f, FieldRadius = 20f,
                    Blocks = Concat(Stack(new Vector3(10f, -4.2f, 0f), 2, 4), Stack(new Vector3(13f, -4.2f, 0f), 2, 2)),
                    Pigs = new[] { new Vector3(10f, -2.5f, 0f), new Vector3(13f, -3.3f, 0f), new Vector3(11.5f, -1f, 0f) },
                    Ammo = 6
                }
            };
        }

        private static Vector3[] Stack(Vector3 basePos, int wide, int high)
        {
            var list = new List<Vector3>();
            for (int x = 0; x < wide; x++)
                for (int y = 0; y < high; y++)
                    list.Add(basePos + new Vector3(x * 1.05f, y * 1.05f, 0f));
            return list.ToArray();
        }

        private static Vector3[] Concat(Vector3[] a, Vector3[] b)
        {
            var list = new List<Vector3>(a); list.AddRange(b); return list.ToArray();
        }

        // ---- Level lifecycle ---------------------------------------------------

        private void LoadLevel(int index)
        {
            ClearLevel();
            _levelIndex = index;
            LevelData lvl = _levels[index];
            _shotsFired = 0;

            // Ground the structures rest on.
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.name = "Ground";
            ground.transform.position = new Vector3(3f, -6.5f, 0f);
            ground.transform.localScale = new Vector3(60f, 3f, 4f);
            Paint(ground, new Color(0.15f, 0.15f, 0.2f));
            Track(ground);

            // Planet (gravity well that bends the shot).
            var planet = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            planet.name = "Planet";
            planet.transform.position = lvl.PlanetPos;
            planet.transform.localScale = Vector3.one * (lvl.PlanetRadius * 2f);
            Paint(planet, new Color(0.25f, 0.5f, 0.95f));
            var body = planet.AddComponent<GravityBody>();
            body.Kind = BodyKind.Planet; body.Mass = lvl.PlanetMass;
            body.FieldRadius = lvl.FieldRadius; body.Softening = 0.5f;
            Track(planet);

            // Cannon.
            var cannon = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cannon.name = "Cannon";
            cannon.transform.position = new Vector3(-13f, -4f, 0f);
            cannon.transform.localScale = new Vector3(1.4f, 1.4f, 1.4f);
            Paint(cannon, new Color(0.8f, 0.8f, 0.85f));
            Object.Destroy(cannon.GetComponent<Collider>());
            _cannon = cannon.transform;
            _muzzle = _cannon.position;
            Track(cannon);

            // Structures.
            foreach (var p in lvl.Blocks)
            {
                var block = GameObject.CreatePrimitive(PrimitiveType.Cube);
                block.transform.position = p;
                Paint(block, new Color(0.55f, 0.4f, 0.25f));
                var rb = block.AddComponent<Rigidbody>();
                rb.mass = 1f; rb.constraints = RigidbodyConstraints.FreezePositionZ | RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationY;
                Track(block);
            }

            // Pigs.
            _pigs.Clear();
            foreach (var p in lvl.Pigs)
            {
                var pigGo = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                pigGo.transform.position = p;
                pigGo.transform.localScale = Vector3.one * 0.9f;
                Paint(pigGo, new Color(0.4f, 0.85f, 0.3f));
                var rb = pigGo.AddComponent<Rigidbody>();
                rb.mass = 0.6f; rb.constraints = RigidbodyConstraints.FreezePositionZ;
                var pig = pigGo.AddComponent<Pig>();
                pig.HitPoints = 3f; pig.DamageThreshold = 3f;
                _pigs.Add(pig);
                Track(pigGo);
            }

            _state = GameState.Aiming;
            _holdTime = 0f;
            _levelTime = 0f;
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

        // ---- Input & flow ------------------------------------------------------

        private void Update()
        {
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

        /// <summary>Test/AI hook: fire a solved full-power shot aimed to hit a world point.</summary>
        public OrbitalProjectile FireAt(Vector3 worldTarget)
        {
            Fire(SolveAim(worldTarget), Charge.ChargeTime);
            return _activeShot;
        }

        /// <summary>
        /// Scan launch directions at full charge and return the one whose predicted
        /// (gravity-aware) path passes closest to the target. Used for auto-play and tests.
        /// </summary>
        public Vector3 SolveAim(Vector3 target)
        {
            var wells = new List<GravityWell>();
            GravityRegistry.CollectWells(wells);

            var path = new List<Vector3>();
            float best = float.MaxValue;
            Vector3 bestDir = (target - _muzzle).normalized;

            for (int deg = -10; deg <= 85; deg += 2)
            {
                Vector3 dir = Quaternion.Euler(0f, 0f, deg) * Vector3.right;
                Vector3 vel = ChargeModel.LaunchVelocity(dir, Charge.ChargeTime, Charge);
                TrajectorySampler.Sample(_muzzle, vel, 1f, wells, Time.fixedDeltaTime,
                    maxSteps: 200, stride: 1, path);

                for (int i = 0; i < path.Count; i++)
                {
                    float d = (path[i] - target).sqrMagnitude;
                    if (d < best) { best = d; bestDir = dir; }
                }
            }
            return bestDir;
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
            proj.G = 1f; proj.MaxSpeed = 45f; proj.MaxLifetime = 8f;
            shot.AddComponent<ProjectileCollision>();
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
            _cam.orthographicSize = 9f;
            _cam.transform.position = new Vector3(0f, -1f, -20f);
            _cam.transform.rotation = Quaternion.identity;
            _cam.clearFlags = CameraClearFlags.SolidColor;
            _cam.backgroundColor = new Color(0.03f, 0.03f, 0.09f);

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
