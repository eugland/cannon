using UnityEngine;

namespace Cannon.Flow
{
    /// <summary>
    /// Star rating for clearing a level, based on shots used vs the level's par
    /// (fewer shots = more stars). See docs/PLAN.md scoring.
    /// </summary>
    public static class ScoreModel
    {
        /// <summary>3 stars at or under par, 2 within par+2, otherwise 1.</summary>
        public static int Stars(int shotsUsed, int par)
        {
            par = Mathf.Max(1, par);
            if (shotsUsed <= par) return 3;
            if (shotsUsed <= par + 2) return 2;
            return 1;
        }
    }
}
