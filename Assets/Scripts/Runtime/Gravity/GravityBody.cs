using UnityEngine;

namespace Cannon.Gravity
{
    /// <summary>
    /// A gravity source in the scene (planet, sun, black hole). Exposes tuning for the
    /// integrator and its contact rule. Registers itself with <see cref="GravityRegistry"/>
    /// while enabled. See docs/PLAN.md sections 5 and 6.
    /// </summary>
    public enum BodyKind
    {
        Planet,     // solid; carries pigs and structures
        Sun,        // lethal on contact
        BlackHole   // shot lost on contact (event horizon)
    }

    public class GravityBody : MonoBehaviour
    {
        [Tooltip("What happens when the projectile touches this body.")]
        public BodyKind Kind = BodyKind.Planet;

        [Tooltip("Abstract game-scale mass (planet ~1, sun ~8-15, black hole ~30+).")]
        public float Mass = 1f;

        [Tooltip("Hard cutoff radius of the gravity field (default ~3-5x body radius).")]
        public float FieldRadius = 5f;

        [Tooltip("Distance softening; small fraction of body radius.")]
        public float Softening = 0.2f;

        /// <summary>True if touching this body ends the shot (sun burns, black hole swallows).</summary>
        public bool IsLethal => Kind == BodyKind.Sun || Kind == BodyKind.BlackHole;

        public GravityWell ToWell()
        {
            return new GravityWell(transform.position, Mass, FieldRadius, Softening);
        }

        private void OnEnable()
        {
            GravityRegistry.Register(this);
        }

        private void OnDisable()
        {
            GravityRegistry.Unregister(this);
        }
    }
}
