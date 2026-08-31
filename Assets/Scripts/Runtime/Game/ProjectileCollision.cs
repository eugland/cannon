using UnityEngine;
using Cannon.Gravity;
using Cannon.Targets;

namespace Cannon.Game
{
    /// <summary>
    /// On impact, applies an explosive burst that pushes nearby structure blocks and
    /// damages pigs in range (impulse scaled by the projectile's speed), then ends the
    /// shot. Attached to the projectile at spawn alongside <see cref="OrbitalProjectile"/>.
    /// </summary>
    [RequireComponent(typeof(OrbitalProjectile))]
    public class ProjectileCollision : MonoBehaviour
    {
        public float BurstRadius = 2.5f;
        public float ForceScale = 6f;
        public float DamageScale = 1.5f;

        private OrbitalProjectile _proj;

        private void Awake()
        {
            _proj = GetComponent<OrbitalProjectile>();
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (_proj.HasEnded)
                return;

            Vector3 point = collision.GetContact(0).point;
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

            _proj.End();
        }
    }
}
