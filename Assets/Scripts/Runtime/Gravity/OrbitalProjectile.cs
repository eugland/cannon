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
