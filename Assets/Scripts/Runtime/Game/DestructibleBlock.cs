using UnityEngine;

namespace Cannon.Game
{
    /// <summary>
    /// A structure block with hit points. Hard impacts chip its HP; it is destroyed once
    /// HP runs out (so blocks break apart under fire instead of only bouncing forever).
    /// </summary>
    public class DestructibleBlock : MonoBehaviour
    {
        public float HitPoints = 6f;

        public void Damage(float amount)
        {
            HitPoints -= amount;
            if (HitPoints <= 0f)
                Object.Destroy(gameObject);
        }

        private void OnCollisionEnter(Collision collision)
        {
            // Chip HP on strong impacts (fast collisions), ignore gentle resting contacts.
            float impulse = collision.impulse.magnitude;
            if (impulse > 2f)
                Damage(impulse * 0.4f);
        }
    }
}
