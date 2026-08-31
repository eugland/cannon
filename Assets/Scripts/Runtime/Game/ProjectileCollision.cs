using UnityEngine;
using Cannon.Gravity;
using Cannon.Targets;

namespace Cannon.Game
{
    /// <summary>
    /// Impact handling for a shot:
    ///  - Planet: bounce off the surface (reflect velocity, lose some energy), keep flying.
    ///  - Sun / black hole: shot is lost.
    ///  - Structure / pig: explosive burst (push blocks, damage nearby pigs), then end.
    /// </summary>
    [RequireComponent(typeof(OrbitalProjectile))]
    public class ProjectileCollision : MonoBehaviour
    {
        public float BurstRadius = 2.5f;
        public float ForceScale = 6f;
        public float DamageScale = 1.5f;
        public float BounceDamping = 0.75f;
        public int MaxBounces = 6;
        public float ProximityRadius = 2.2f;

        private OrbitalProjectile _proj;
        private int _bounces;

        private void Awake()
        {
            _proj = GetComponent<OrbitalProjectile>();
        }

        private void FixedUpdate()
        {
            if (_proj.HasEnded) return;

            // Detonate when the shell passes near a pig (forgiving near-miss = blast kill).
            var hits = Physics.OverlapSphere(transform.position, ProximityRadius);
            foreach (var col in hits)
            {
                var pig = col.GetComponent<Pig>();
                if (pig != null && !pig.IsDead)
                {
                    Burst(transform.position);
                    _proj.End();
                    return;
                }
            }
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (_proj.HasEnded)
                return;

            var body = collision.gameObject.GetComponent<GravityBody>();
            if (body != null)
            {
                if (body.IsLethal) { _proj.End(); return; }
                Bounce(collision);
                return;
            }

            Burst(collision.GetContact(0).point);
            _proj.End();
        }

        private void Bounce(Collision collision)
        {
            var contact = collision.GetContact(0);
            Vector3 reflected = Vector3.Reflect(_proj.Velocity, contact.normal) * BounceDamping;
            _proj.Velocity = reflected;
            // Nudge out of the surface so we don't immediately re-collide.
            transform.position = contact.point + contact.normal * 0.35f;

            if (++_bounces >= MaxBounces)
                _proj.End();
        }

        private void Burst(Vector3 point)
        {
            float speed = _proj.Velocity.magnitude;
            Collider[] hits = Physics.OverlapSphere(point, BurstRadius);
            foreach (var col in hits)
            {
                float dist = Vector3.Distance(point, col.transform.position);
                float falloff = Mathf.Clamp01(1f - dist / BurstRadius);

                var rb = col.attachedRigidbody;
                if (rb != null && !rb.isKinematic)
                    rb.AddExplosionForce(speed * ForceScale, point, BurstRadius, 0.3f, ForceMode.Impulse);

                var pig = col.GetComponent<Pig>();
                if (pig != null)
                    pig.ApplyImpact(speed * DamageScale * falloff);
            }
        }
    }
}
