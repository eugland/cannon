using UnityEngine;

namespace Cannon.Targets
{
    /// <summary>
    /// A destructible target (working name "pig", docs/PLAN.md section 6). Takes damage
    /// when an impact impulse exceeds a threshold; damage scales with impact strength
    /// above it. Also dies immediately on a lethal (sun / black hole) contact or when
    /// knocked out of the playfield — handled by callers via <see cref="Kill"/>.
    /// </summary>
    public class Pig : MonoBehaviour
    {
        [Tooltip("Hit points; pig dies at zero.")]
        public float HitPoints = 1f;

        [Tooltip("Minimum impact impulse that deals any damage; below this is a harmless nudge.")]
        public float DamageThreshold = 2f;

        public bool IsDead { get; private set; }

        public event System.Action<Pig> Died;

        /// <summary>
        /// Apply an impact of the given impulse magnitude. Below the threshold does nothing;
        /// above it, damage equal to the excess is subtracted from hit points.
        /// Returns true if this impact killed the pig.
        /// </summary>
        public bool ApplyImpact(float impulseMagnitude)
        {
            if (IsDead)
                return false;
            if (impulseMagnitude < DamageThreshold)
                return false;

            HitPoints -= impulseMagnitude - DamageThreshold;
            if (HitPoints <= 0f)
            {
                Kill();
                return true;
            }
            return false;
        }

        /// <summary>Destroy the pig outright (lethal-body contact, fell off the world, etc.).</summary>
        public void Kill()
        {
            if (IsDead)
                return;
            IsDead = true;
            HitPoints = 0f;
            Died?.Invoke(this);
        }
    }
}
