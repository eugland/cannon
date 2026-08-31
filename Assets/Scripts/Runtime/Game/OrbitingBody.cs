using UnityEngine;

namespace Cannon.Game
{
    /// <summary>
    /// Moves its transform on a scripted circular orbit around a center point (a moon).
    /// Kinematic and deterministic — see docs/PLAN.md section 6 (orbiting bodies). Any
    /// GravityBody on the same object becomes a moving gravity well the shot must account for.
    /// </summary>
    public class OrbitingBody : MonoBehaviour
    {
        public Vector3 Center = Vector3.zero;
        public float OrbitRadius = 9f;
        public float DegreesPerSecond = 35f;
        public float StartAngle = 0f;

        private float _angle;

        private void Awake() => _angle = StartAngle;

        public void Configure(Vector3 center, float radius, float degreesPerSecond, float startAngle)
        {
            Center = center;
            OrbitRadius = radius;
            DegreesPerSecond = degreesPerSecond;
            StartAngle = startAngle;
            _angle = startAngle;

            float a = _angle * Mathf.Deg2Rad;
            transform.position = Center + new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * OrbitRadius;
        }

        private void FixedUpdate()
        {
            _angle += DegreesPerSecond * Time.fixedDeltaTime;
            float a = _angle * Mathf.Deg2Rad;
            transform.position = Center + new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * OrbitRadius;
        }
    }
}
