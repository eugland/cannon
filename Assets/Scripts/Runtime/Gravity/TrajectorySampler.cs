using System.Collections.Generic;
using UnityEngine;

namespace Cannon.Gravity
{
    /// <summary>
    /// Forward-simulates the shared gravity integrator to produce a preview path
    /// (docs/PLAN.md section 7). Uses the identical GravityField.Step as the live
    /// projectile so the dotted preview cannot diverge from actual flight.
    /// Deliberately truncated so it hints the curve without solving the puzzle.
    /// </summary>
    public static class TrajectorySampler
    {
        /// <summary>
        /// Fill <paramref name="output"/> with sampled world points along the predicted path.
        /// </summary>
        /// <param name="stride">Emit one point every this many integration steps (spaced dots).</param>
        /// <param name="maxSteps">Hard cap on simulated steps (preview length / difficulty knob).</param>
        /// <param name="stopAtFieldEntry">Stop once the path first enters any well's field radius.</param>
        public static void Sample(
            Vector3 startPos, Vector3 startVel, float g,
            IReadOnlyList<GravityWell> wells, float dt,
            int maxSteps, int stride, List<Vector3> output,
            bool stopAtFieldEntry = false)
        {
            output.Clear();
            if (stride < 1) stride = 1;

            Vector3 pos = startPos;
            Vector3 vel = startVel;
            output.Add(pos);

            for (int step = 1; step <= maxSteps; step++)
            {
                GravityField.Step(ref pos, ref vel, g, wells, dt);

                if (stopAtFieldEntry && IsInsideAnyField(pos, wells))
                {
                    output.Add(pos);
                    break;
                }

                if (step % stride == 0)
                    output.Add(pos);
            }
        }

        private static bool IsInsideAnyField(Vector3 point, IReadOnlyList<GravityWell> wells)
        {
            if (wells == null)
                return false;

            for (int i = 0; i < wells.Count; i++)
            {
                Vector3 delta = wells[i].Position - point;
                if (delta.sqrMagnitude <= wells[i].FieldRadius * wells[i].FieldRadius)
                    return true;
            }
            return false;
        }
    }
}
