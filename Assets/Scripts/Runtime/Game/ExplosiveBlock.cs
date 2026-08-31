using UnityEngine;
using Cannon.Targets;

namespace Cannon.Game
{
    /// <summary>
    /// A crate that detonates when caught in a blast, dealing its own explosion that
    /// kills pigs, shoves debris, and chains into other explosive blocks nearby.
    /// </summary>
    public class ExplosiveBlock : MonoBehaviour
    {
        public float Radius = 3.5f;
        public float Force = 40f;

        private bool _done;

        public void Detonate()
        {
            if (_done) return;
            _done = true;

            Vector3 p = transform.position;
            ExplosionFx.Spawn(p, Radius);
            Collider[] hits = Physics.OverlapSphere(p, Radius);
            foreach (var col in hits)
            {
                if (col.gameObject == gameObject) continue;

                var pig = col.GetComponent<Pig>();
                if (pig != null && !pig.IsDead) pig.Kill();

                var rb = col.attachedRigidbody;
                if (rb != null && !rb.isKinematic)
                    rb.AddExplosionForce(Force, p, Radius, 0.3f, ForceMode.Impulse);

                var other = col.GetComponent<ExplosiveBlock>();
                if (other != null && other != this) other.Detonate(); // chain
            }

            Object.Destroy(gameObject);
        }
    }
}
