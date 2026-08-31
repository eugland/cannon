using UnityEngine;
using Cannon.Gravity;

namespace Cannon.Demo
{
    /// <summary>
    /// Non-interactive showcase: repeatedly launches the projectile so the gravity
    /// curve is visible without any input. Temporary — removed once the real cannon
    /// and level exist.
    /// </summary>
    [RequireComponent(typeof(OrbitalProjectile))]
    public class DemoLauncher : MonoBehaviour
    {
        public Vector3 StartPosition = new Vector3(-6f, 3f, 0f);
        public Vector3 LaunchVelocity = new Vector3(4f, 0f, 0f);
        public float ResetAfter = 5f;

        private OrbitalProjectile _proj;
        private float _timer;

        private void Awake()
        {
            _proj = GetComponent<OrbitalProjectile>();
        }

        private void OnEnable()
        {
            Fire();
        }

        private void Fire()
        {
            transform.position = StartPosition;
            _proj.Launch(LaunchVelocity);
            _timer = 0f;
        }

        private void Update()
        {
            _timer += Time.deltaTime;
            if (_proj.HasEnded || _timer >= ResetAfter)
                Fire();
        }
    }
}
