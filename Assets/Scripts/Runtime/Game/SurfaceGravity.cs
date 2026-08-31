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
        public float EscapeRange = 16f; // beyond this the object has escaped and flies free
        public Vector3 Center = Vector3.zero; // the main planet; moons must not capture surface objects

        private Rigidbody _rb;

        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            _rb.useGravity = false;
        }

        private void FixedUpdate()
        {
            // Always pull toward the main planet, never a passing moon or second body.
            Vector3 dir = (Center - transform.position);
            float dist = dir.magnitude;
            if (dist > EscapeRange || dist < 0.0001f)
                return; // escaped (or at center): no pull, free flight
            _rb.AddForce(dir.normalized * Strength, ForceMode.Acceleration);
        }
    }
}
