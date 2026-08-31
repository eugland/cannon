using UnityEngine;

namespace Cannon.Gravity
{
    /// <summary>
    /// Plain data for one gravity source (planet, sun, black hole). Kept as a
    /// struct with no engine dependencies beyond Vector3 so the integrator is
    /// trivially unit-testable. See docs/PLAN.md section 5.
    /// </summary>
    public struct GravityWell
    {
        /// <summary>World-space center of the body.</summary>
        public Vector3 Position;

        /// <summary>Abstract game-scale mass (not kilograms).</summary>
        public float Mass;

        /// <summary>Hard cutoff radius; beyond this the well exerts no pull.</summary>
        public float FieldRadius;

        /// <summary>Distance softening; prevents near-infinite force near the center.</summary>
        public float Softening;

        public GravityWell(Vector3 position, float mass, float fieldRadius, float softening)
        {
            Position = position;
            Mass = mass;
            FieldRadius = fieldRadius;
            Softening = softening;
        }
    }
}
