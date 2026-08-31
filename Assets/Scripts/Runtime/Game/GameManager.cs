using System.Collections.Generic;
using UnityEngine;
using Cannon.Gravity;
using Cannon.Targets;
using Cannon.CannonControl;
using Cannon.Flow;

namespace Cannon.Game
{
    public enum GameState { Menu, Aiming, Charging, Fired, Won, Lost }

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
            public bool ExplosiveShields; // shields are chain-reaction explosive crates
            public int Par;             // shots for a 3-star clear

            // Optional hazard (sun / black hole) — lethal on contact.
            public bool HasHazard;
            public BodyKind HazardKind;
            public Vector3 HazardPos;
            public float HazardMass;
            public float HazardRadius;
            public float HazardField;

            // Optional orbiting moon (moving gravity well).
            public bool HasMoon;
            public float MoonOrbitRadius;
            public float MoonSpeed;
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
        private LineRenderer _aimLine;
        private Transform _cannon;
        private Vector3 _muzzle;

        private GameState _state;
        private int _levelIndex;
        private int _shotsFired;
        private float _holdTime;
        private OrbitalProjectile _activeShot;
        private float _resolveTimer;
        private float _levelTime;
        private const float LevelTimeLimit = 180f;
        private int _currentPar = 3;
        private int _lastStars;

        private AudioSource _audio;
        private AudioClip _fireClip, _hitClip, _winClip, _loseClip;

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

            new GameObject("Nebula").AddComponent<Nebula>();
            var starfield = new GameObject("Starfield").AddComponent<Starfield>();

            _audio = gameObject.AddComponent<AudioSource>();
            _fireClip = SoundFx.Tone(180f, 0.18f);
            _hitClip = SoundFx.Tone(320f, 0.15f);
            _winClip = SoundFx.Chime();
            _loseClip = SoundFx.Tone(110f, 0.4f);

            _levels = BuildLevels();
            _state = GameState.Menu; // start at the level-select menu
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
                    PigAngles = new[] { 150f }, ShieldsPerPig = 2, Par = 2
                },
                new LevelData
                {
                    PlanetRadius = 4.5f, PlanetMass = 40f, FieldRadius = 46f,
                    PigAngles = new[] { 120f, 160f }, ShieldsPerPig = 2, Par = 3
                },
                new LevelData
                {
                    PlanetRadius = 5f, PlanetMass = 48f, FieldRadius = 52f,
                    PigAngles = new[] { 120f, 150f }, ShieldsPerPig = 3, Par = 4
                },
                new LevelData
                {
                    PlanetRadius = 4.5f, PlanetMass = 40f, FieldRadius = 46f,
                    PigAngles = new[] { 120f, 150f }, ShieldsPerPig = 2, Par = 4,
                    HasHazard = true, HazardKind = BodyKind.Sun,
                    HazardPos = new Vector3(-4f, -3f, 0f), HazardMass = 22f, HazardRadius = 1.6f, HazardField = 28f
                },
                new LevelData
                {
                    PlanetRadius = 4f, PlanetMass = 34f, FieldRadius = 42f,
                    PigAngles = new[] { 130f, 155f }, ShieldsPerPig = 2, Par = 4,
                    HasHazard = true, HazardKind = BodyKind.BlackHole,
                    HazardPos = new Vector3(2f, -4f, 0f), HazardMass = 42f, HazardRadius = 1f, HazardField = 34f
                },
                new LevelData
                {
                    PlanetRadius = 3.5f, PlanetMass = 30f, FieldRadius = 40f,
                    PigAngles = new[] { 120f, 150f }, ShieldsPerPig = 2, Par = 4,
                    HasHazard = true, HazardKind = BodyKind.Planet, // second planet to slingshot around
                    HazardPos = new Vector3(-4f, -5f, 0f), HazardMass = 26f, HazardRadius = 2f, HazardField = 30f
                },
                new LevelData
                {
                    PlanetRadius = 4.5f, PlanetMass = 40f, FieldRadius = 46f,
                    PigAngles = new[] { 125f, 145f }, ShieldsPerPig = 2, ExplosiveShields = true, Par = 3
                },
                new LevelData
                {
                    PlanetRadius = 4f, PlanetMass = 34f, FieldRadius = 44f,
                    PigAngles = new[] { 120f, 150f }, ShieldsPerPig = 2, Par = 4,
                    HasMoon = true, MoonOrbitRadius = 9f, MoonSpeed = 45f
                },
                new LevelData
                {
                    PlanetRadius = 4.5f, PlanetMass = 40f, FieldRadius = 46f,
                    PigAngles = new[] { 120f, 150f }, ShieldsPerPig = 2, ExplosiveShields = true, Par = 4,
                    HasHazard = true, HazardKind = BodyKind.Sun,
                    HazardPos = new Vector3(-5f, -3f, 0f), HazardMass = 22f, HazardRadius = 1.6f, HazardField = 28f
                },
                new LevelData
                {
                    PlanetRadius = 4f, PlanetMass = 34f, FieldRadius = 44f,
                    PigAngles = new[] { 125f, 150f }, ShieldsPerPig = 2, Par = 5,
                    HasHazard = true, HazardKind = BodyKind.Sun,
                    HazardPos = new Vector3(-4f, -4f, 0f), HazardMass = 20f, HazardRadius = 1.5f, HazardField = 26f,
                    HasMoon = true, MoonOrbitRadius = 9f, MoonSpeed = 40f
                }
            };
        }

        private static readonly Vector3 PlanetCenter = Vector3.zero;

        private static readonly Color[] PlanetHues =
        {
            new Color(0.62f, 0.68f, 0.72f), // icy teal-grey
            new Color(0.70f, 0.62f, 0.52f), // sandy tan
            new Color(0.55f, 0.65f, 0.72f), // slate blue
            new Color(0.68f, 0.60f, 0.68f), // dusty mauve
            new Color(0.58f, 0.70f, 0.62f), // sage green
            new Color(0.74f, 0.66f, 0.55f), // warm clay
        };

        /// <summary>True if a point is inside a lethal body's kill radius (used by the aim solver).</summary>
        private static bool EntersLethalBody(Vector3 p)
        {
            var bodies = GravityRegistry.ActiveBodies;
            for (int i = 0; i < bodies.Count; i++)
            {
                var b = bodies[i];
                if (b == null || !b.IsLethal || b.Radius <= 0f) continue;
                float kill = b.Radius + 0.4f;
                if ((p - b.transform.position).sqrMagnitude < kill * kill) return true;
            }
            return false;
        }

        // ---- Level lifecycle ---------------------------------------------------

        private void LoadLevel(int index)
        {
            ClearLevel();
            _levelIndex = index;
            LevelData lvl = _levels[index];
            _shotsFired = 0;
            _currentPar = Mathf.Max(1, lvl.Par);
            _lastStars = 0;
            float r = lvl.PlanetRadius;

            // Planet at center (comet-toned, solid — the shot bounces off it).
            var planet = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            planet.name = "Planet";
            planet.transform.position = PlanetCenter;
            planet.transform.localScale = Vector3.one * (r * 2f);
            Paint(planet, PlanetHues[index % PlanetHues.Length]); // varied comfortable hue per level
            var body = planet.AddComponent<GravityBody>();
            body.Kind = BodyKind.Planet; body.Mass = lvl.PlanetMass;
            body.Radius = r;
            body.FieldRadius = lvl.FieldRadius; body.Softening = 0.5f;
            Track(planet);

            // Optional hazard (sun / black hole) — lethal on contact.
            if (lvl.HasHazard)
            {
                var hazard = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                hazard.name = lvl.HazardKind.ToString();
                hazard.transform.position = lvl.HazardPos;
                hazard.transform.localScale = Vector3.one * (lvl.HazardRadius * 2f);
                Color hazColor;
                switch (lvl.HazardKind)
                {
                    case BodyKind.Sun: hazColor = new Color(1f, 0.7f, 0.2f); break;       // warm sun
                    case BodyKind.BlackHole: hazColor = new Color(0.05f, 0.02f, 0.1f); break; // dark
                    default: hazColor = new Color(0.7f, 0.55f, 0.45f); break;             // second planet
                }
                Paint(hazard, hazColor);
                var hb = hazard.AddComponent<GravityBody>();
                hb.Kind = lvl.HazardKind; hb.Mass = lvl.HazardMass;
                hb.Radius = lvl.HazardRadius; hb.FieldRadius = lvl.HazardField; hb.Softening = 0.4f;
                Track(hazard);
            }

            // Optional orbiting moon (moving gravity well).
            if (lvl.HasMoon)
            {
                var moon = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                moon.name = "Moon";
                moon.transform.localScale = Vector3.one * 1.6f;
                Paint(moon, new Color(0.75f, 0.75f, 0.7f));
                var mb = moon.AddComponent<GravityBody>();
                mb.Kind = BodyKind.Planet; mb.Mass = 10f; mb.Radius = 0.8f;
                mb.FieldRadius = 18f; mb.Softening = 0.4f;
                var orbit = moon.AddComponent<OrbitingBody>();
                orbit.Center = PlanetCenter; orbit.OrbitRadius = lvl.MoonOrbitRadius;
                orbit.DegreesPerSecond = lvl.MoonSpeed;
                Track(moon);
            }

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
                var pigObj = pigGo;
                pig.Died += _ => { Play(_hitClip); Object.Destroy(pigObj); }; // vanish on death
                _pigs.Add(pig);
                Track(pigGo);

                for (int k = 0; k < lvl.ShieldsPerPig; k++)
                {
                    var block = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    block.transform.position = PlanetCenter + dir * (r + 1.7f + k * 1.1f);
                    block.transform.rotation = Quaternion.FromToRotation(Vector3.up, dir);
                    if (lvl.ExplosiveShields)
                    {
                        Paint(block, new Color(0.9f, 0.4f, 0.15f)); // explosive orange
                        block.AddComponent<ExplosiveBlock>();
                    }
                    else
                    {
                        Paint(block, new Color(0.72f, 0.6f, 0.42f)); // warm sandstone
                        block.AddComponent<DestructibleBlock>();
                    }
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

        private void Play(AudioClip clip)
        {
            if (_audio != null && clip != null) _audio.PlayOneShot(clip);
        }

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

            // Hotkeys: R restarts the level, Esc returns to the menu.
            if (_state != GameState.Menu)
            {
                if (Input.GetKeyDown(KeyCode.R)) { LoadLevel(_levelIndex); return; }
                if (Input.GetKeyDown(KeyCode.Escape)) { ToMenu(); return; }
            }

            if (_state == GameState.Aiming || _state == GameState.Charging)
            {
                _levelTime += Time.deltaTime;

                // Pigs knocked off the world count as destroyed.
                foreach (var pig in _pigs)
                    if (pig != null && !pig.IsDead && pig.transform.position.y < -14f)
                        pig.Kill();

                if (PigsAlive == 0) { WinLevel(); return; }
                if (_levelTime >= LevelTimeLimit) { _state = GameState.Lost; Play(_loseClip); return; }

                HandleAimAndCharge(); // rapid fire: always ready to shoot
            }
        }

        private void WinLevel()
        {
            _lastStars = ScoreModel.Stars(_shotsFired, _currentPar);
            string key = "stars_" + _levelIndex;
            if (_lastStars > PlayerPrefs.GetInt(key, 0))
                PlayerPrefs.SetInt(key, _lastStars);
            _state = GameState.Won;
            Play(_winClip);
        }

        private void UpdateAimIndicator(Vector3 aimDir)
        {
            Vector3 dir = aimDir.normalized;
            if (_cannon != null)
                _cannon.rotation = Quaternion.FromToRotation(Vector3.up, dir);
            if (_aimLine != null)
            {
                _aimLine.positionCount = 2;
                _aimLine.SetPosition(0, _muzzle + Vector3.back * 0.1f);
                _aimLine.SetPosition(1, _muzzle + dir * 2.6f + Vector3.back * 0.1f);
            }
        }

        private void HandleAimAndCharge()
        {
            Vector3 aimDir = (MouseWorld() - _muzzle);
            aimDir.z = 0f;
            if (aimDir.sqrMagnitude < 0.001f) aimDir = Vector3.right;

            UpdateAimIndicator(aimDir);

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

                    float pathBest = float.MaxValue;
                    bool hitsLethal = false;
                    for (int i = 0; i < path.Count; i++)
                    {
                        if (EntersLethalBody(path[i])) { hitsLethal = true; break; }
                        float d = (path[i] - target).sqrMagnitude;
                        if (d < pathBest) pathBest = d;
                    }
                    if (!hitsLethal && pathBest < best) { best = pathBest; bestDir = dir; bestHold = hold; }
                }
            }
        }

        private void Fire(Vector3 aimDir, float hold)
        {
            // Unlimited cannon balls, rapid fire: stay ready to shoot again immediately.
            _shotsFired++;
            _holdTime = 0f;
            _state = GameState.Aiming;
            if (_line != null) _line.positionCount = 0;
            if (_aimLine != null) _aimLine.positionCount = 0;

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
            hit.BurstRadius = 3.8f;
            hit.ProximityRadius = 3.5f;
            proj.Launch(ChargeModel.LaunchVelocity(aimDir, hold, Charge));
            var shotGo = shot;
            proj.Ended += _ => Object.Destroy(shotGo, 0.15f); // clean up promptly (no frozen ball)

            _activeShot = proj;
            Track(shot);
            Play(_fireClip);
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
            if (_cam.GetComponent<AudioListener>() == null)
                _cam.gameObject.AddComponent<AudioListener>();

            var lineGo = new GameObject("Preview");
            _line = lineGo.AddComponent<LineRenderer>();
            _line.widthMultiplier = 0.12f;
            _line.material = MaterialFactory.Unlit(Color.white);
            _line.startColor = _line.endColor = new Color(1f, 1f, 1f, 0.5f);
            _line.positionCount = 0;

            var aimGo = new GameObject("AimLine");
            _aimLine = aimGo.AddComponent<LineRenderer>();
            _aimLine.widthMultiplier = 0.18f;
            _aimLine.material = MaterialFactory.Unlit(new Color(1f, 0.85f, 0.2f));
            _aimLine.startColor = _aimLine.endColor = new Color(1f, 0.85f, 0.2f);
            _aimLine.positionCount = 0;
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

            if (_state == GameState.Menu)
            {
                DrawMenu();
                return;
            }

            GUI.Label(new Rect(20, 15, 620, 30), $"Level {_levelIndex + 1}   Shots: {_shotsFired}   Par: {_currentPar} (3 stars)");

            GUI.Label(new Rect(20, 45, 400, 30), $"Pigs left: {PigsAlive}");
            GUI.Label(new Rect(20, 75, 400, 30), $"Time: {Mathf.Max(0f, LevelTimeLimit - _levelTime):0}s");

            if (_state == GameState.Aiming)
                GUI.Label(new Rect(20, 105, 700, 30), "Aim with mouse. Hold left button to charge, release to fire.");

            if (_state == GameState.Won)
            {
                string stars = new string('★', _lastStars) + new string('☆', 3 - _lastStars);
                GUI.Label(new Rect(Screen.width / 2 - 100, Screen.height / 2 - 90, 320, 40), "LEVEL CLEARED!");
                GUI.Label(new Rect(Screen.width / 2 - 100, Screen.height / 2 - 55, 320, 40), $"{stars}   ({_shotsFired} shots, par {_currentPar})");
                bool last = _levelIndex + 1 >= _levels.Length;
                if (!last && GUI.Button(new Rect(Screen.width / 2 - 100, Screen.height / 2, 200, 50), "Next Level"))
                    LoadLevel(_levelIndex + 1);
                if (GUI.Button(new Rect(Screen.width / 2 - 100, Screen.height / 2 + 60, 200, 45), "Level Select"))
                    ToMenu();
            }

            if (_state == GameState.Lost)
            {
                GUI.Label(new Rect(Screen.width / 2 - 120, Screen.height / 2 - 60, 400, 40), "TIME'S UP — level lost");
                if (GUI.Button(new Rect(Screen.width / 2 - 100, Screen.height / 2, 200, 50), "Retry"))
                    LoadLevel(_levelIndex);
                if (GUI.Button(new Rect(Screen.width / 2 - 100, Screen.height / 2 + 60, 200, 45), "Level Select"))
                    ToMenu();
            }
        }

        private void ToMenu()
        {
            ClearLevel();
            _state = GameState.Menu;
        }

        private void DrawMenu()
        {
            float cx = Screen.width / 2f;
            int total = 0;
            for (int i = 0; i < _levels.Length; i++) total += PlayerPrefs.GetInt("stars_" + i, 0);
            GUI.Label(new Rect(cx - 160, 55, 360, 40), "CANNON — Orbital");
            GUI.Label(new Rect(cx - 160, 92, 400, 30), $"Stars: {total} / {_levels.Length * 3}   —  select a level:");

            for (int i = 0; i < _levels.Length; i++)
            {
                int best = PlayerPrefs.GetInt("stars_" + i, 0);
                string stars = new string('★', best) + new string('☆', 3 - best);
                if (GUI.Button(new Rect(cx - 160, 140 + i * 60, 320, 50), $"Level {i + 1}    {stars}"))
                    LoadLevel(i);
            }

            GUI.Label(new Rect(cx - 160, 160 + _levels.Length * 60, 420, 30), "Scroll to zoom • hold mouse to charge, release to fire");
        }
    }
}
