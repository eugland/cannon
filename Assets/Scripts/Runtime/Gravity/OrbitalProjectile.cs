using System.Collections.Generic;
using UnityEngine;

namespace Cannon.Gravity
{
    /// <summary>
    /// The in-flight projectile. Moved directly by the shared gravity integrator in
    /// FixedUpdate (not Unity's rigid-body gravity) so flight matches the preview exactly.
    /// Includes the plan's safety clamps: max speed and a max-lifetime timeout so an
    /// escaped or endlessly-orbiting shot ends cleanly. See docs/PLAN.md sections 5 and 11.
    /// </summary>
    public class OrbitalProjectile : MonoBehaviour
    {
        [Tooltip("Global gravity constant (shared with preview).")]
        public float G = 1f;

        [Tooltip("Hard cap on speed to prevent slingshot blow-ups.")]
        public float MaxSpeed = 40f;

        [Tooltip("Seconds before an unresolved shot times out.")]
        public float MaxLifetime = 12f;

        public Vector3 Velocity;

        /// <summary>Raised when the shot ends (timeout, out-of-bounds, or lethal contact).</summary>
        public event System.Action<OrbitalProjectile> Ended;

        private readonly List<GravityWell> _wells = new List<GravityWell>();
        private float _age;
        private bool _ended;

        public bool HasEnded => _ended;

        public void Launch(Vector3 velocity)
        {
            Velocity = velocity;
            _age = 0f;
            _ended = false;
        }

        private void FixedUpdate()
        {
            if (_ended)
                return;

            float dt = Time.fixedDeltaTime;
            _age += dt;

            GravityRegistry.CollectWells(_wells);

            Vector3 pos = transform.position;
            GravityField.Step(ref pos, ref Velocity, G, _wells, dt);

            if (Velocity.sqrMagnitude > MaxSpeed * MaxSpeed)
                Velocity = Velocity.normalized * MaxSpeed;

            // Bounce off solid bodies (kinematic vs static gives no collision callback,
            // so the surface is handled here against each body's Radius).
            var bodies = GravityRegistry.ActiveBodies;
            for (int i = 0; i < bodies.Count; i++)
            {
                var b = bodies[i];
                if (b == null || b.Radius <= 0f) continue;

                Vector3 d = pos - b.transform.position;
                float surf = b.Radius + 0.25f;
                if (d.sqrMagnitude < surf * surf)
                {
                    if (b.IsLethal) { transform.position = pos; End(); return; }
                    Vector3 n = d.sqrMagnitude > 1e-6f ? d.normalized : Vector3.up;
                    Velocity = Vector3.Reflect(Velocity, n) * 0.75f;
                    pos = b.transform.position + n * surf;
                    break;
                }
            }

            transform.position = pos;

            if (_age >= MaxLifetime)
                End();
        }

        public void End()
        {
            if (_ended)
                return;
            _ended = true;
            Ended?.Invoke(this);
        }
    }
}
