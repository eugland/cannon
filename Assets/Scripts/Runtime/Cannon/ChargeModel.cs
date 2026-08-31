using UnityEngine;

namespace Cannon.CannonControl
{
    /// <summary>
    /// Tunable data for the timed-charge space cannon (docs/PLAN.md section 8).
    /// Lives on the cannon so levels can override it.
    /// </summary>
    [System.Serializable]
    public struct ChargeSettings
    {
        [Tooltip("Seconds of holding to reach maximum force.")]
        public float ChargeTime;

        [Tooltip("Force applied on an instant tap (fraction floor of the range).")]
        public float MinForce;

        [Tooltip("Force applied at full charge / timer auto-fire.")]
        public float MaxForce;

        public static ChargeSettings Default => new ChargeSettings
        {
            ChargeTime = 1.2f,
            MinForce = 2.5f,
            MaxForce = 10f
        };
    }

    /// <summary>
    /// Pure functions mapping hold duration to launch force for the timed-charge cannon.
    /// Force grows linearly from MinForce to MaxForce over ChargeTime; holding to the cap
    /// auto-fires at MaxForce; releasing early fires with proportionally less force.
    /// </summary>
    public static class ChargeModel
    {
        /// <summary>Normalized charge in [0,1] for a given hold duration.</summary>
        public static float ChargeFraction(float holdTime, ChargeSettings settings)
        {
            if (settings.ChargeTime <= 0f)
                return 1f;
            return Mathf.Clamp01(holdTime / settings.ChargeTime);
        }

        /// <summary>Launch force for a given hold duration (linear MinForce→MaxForce).</summary>
        public static float ForceForHold(float holdTime, ChargeSettings settings)
        {
            return Mathf.Lerp(settings.MinForce, settings.MaxForce, ChargeFraction(holdTime, settings));
        }

        /// <summary>True once the hold reaches the charge cap (cannon auto-fires at max).</summary>
        public static bool ShouldAutoFire(float holdTime, ChargeSettings settings)
        {
            return holdTime >= settings.ChargeTime;
        }

        /// <summary>
        /// Launch velocity: aim direction (normalized) scaled by the charged force.
        /// A zero direction yields zero velocity.
        /// </summary>
        public static Vector3 LaunchVelocity(Vector3 aimDirection, float holdTime, ChargeSettings settings)
        {
            Vector3 dir = aimDirection.sqrMagnitude > 0f ? aimDirection.normalized : Vector3.zero;
            return dir * ForceForHold(holdTime, settings);
        }
    }
}
