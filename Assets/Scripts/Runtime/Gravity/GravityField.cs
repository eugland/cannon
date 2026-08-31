using System.Collections.Generic;
using UnityEngine;

namespace Cannon.Gravity
{
    /// <summary>
    /// Multi-well gravity integrator — the core of the game (docs/PLAN.md section 5).
    /// Dimension-agnostic (runs on 3D vectors), deterministic, and used by both the
    /// live projectile and the trajectory preview so they cannot diverge.
    /// </summary>
    public static class GravityField
    {
        /// <summary>
        /// Accumulated gravitational acceleration at <paramref name="point"/> from every
        /// well whose field contains the point:
        ///   a = Σ  G·m·(p_i − p) / (|p_i − p|² + ε²)^(3/2)
        /// Wells farther than their FieldRadius contribute nothing (empty space is zero-G).
        /// </summary>
        public static Vector3 ComputeAcceleration(Vector3 point, float g, IReadOnlyList<GravityWell> wells)
        {
            var accel = Vector3.zero;
            if (wells == null)
                return accel;

            for (int i = 0; i < wells.Count; i++)
            {
                GravityWell well = wells[i];

                Vector3 delta = well.Position - point;
                float distSqr = delta.sqrMagnitude;

                // Hard field cutoff: outside the radius the well exerts no pull.
                if (distSqr > well.FieldRadius * well.FieldRadius)
                    continue;

                float soft = well.Softening;
                float denom = Mathf.Pow(distSqr + soft * soft, 1.5f);
                if (denom <= Mathf.Epsilon)
                    continue;

                accel += (g * well.Mass / denom) * delta;
            }

            return accel;
        }

        /// <summary>
        /// Advance one fixed step with semi-implicit (symplectic) Euler:
        /// compute a(p), then v += a·dt, then p += v·dt. Conserves orbital energy
        /// far better than explicit Euler, keeping slingshots and orbits stable.
        /// </summary>
        public static void Step(ref Vector3 position, ref Vector3 velocity, float g,
            IReadOnlyList<GravityWell> wells, float dt)
        {
            Vector3 accel = ComputeAcceleration(position, g, wells);
            velocity += accel * dt;
            position += velocity * dt;
        }
    }
}
