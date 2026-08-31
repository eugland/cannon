using UnityEngine;
using Cannon.Gravity;

namespace Cannon.Game
{
    /// <summary>
    /// Pulls a dynamic rigid body toward the nearest planet's center, so structures and
    /// pigs rest on (or orbit) the planet instead of falling in a flat world. Replaces
    /// global down-gravity for gameplay objects. See docs/PLAN.md section 5 (surface gravity).
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class SurfaceGravity : MonoBehaviour
    {
        public float Strength = 14f;

        private Rigidbody _rb;

        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            _rb.useGravity = false;
        }

        private void FixedUpdate()
        {
            var bodies = GravityRegistry.ActiveBodies;
            if (bodies.Count == 0) return;

            // Pull toward the nearest planet center.
            Vector3 pos = transform.position;
            float bestSqr = float.MaxValue;
            Vector3 center = pos;
            for (int i = 0; i < bodies.Count; i++)
            {
                if (bodies[i] == null) continue;
                float d = (bodies[i].transform.position - pos).sqrMagnitude;
                if (d < bestSqr) { bestSqr = d; center = bodies[i].transform.position; }
            }

            Vector3 dir = (center - pos);
            if (dir.sqrMagnitude > 0.0001f)
                _rb.AddForce(dir.normalized * Strength, ForceMode.Acceleration);
        }
    }
}
