using UnityEngine;

namespace Cannon.Flow
{
    /// <summary>
    /// Evaluates the level objective (docs/PLAN.md sections 4 and 9): destroy at least a
    /// required number of pigs within the ammunition limit. Pure logic so it can be unit
    /// tested; GameFlow drives it with kill notifications and decides when to evaluate
    /// (only after physics has settled).
    /// </summary>
    public class LevelGoal
    {
        private readonly int _totalPigs;
        private readonly int _requiredKills;
        private int _killed;

        /// <param name="totalPigs">Total pigs placed in the level.</param>
        /// <param name="requiredKills">
        /// How many must be destroyed to win. Clamped to [1, totalPigs].
        /// </param>
        public LevelGoal(int totalPigs, int requiredKills)
        {
            _totalPigs = Mathf.Max(0, totalPigs);
            _requiredKills = Mathf.Clamp(requiredKills, 1, Mathf.Max(1, _totalPigs));
        }

        /// <summary>Build a goal requiring a percentage of pigs destroyed (rounded up).</summary>
        public static LevelGoal FromPercentage(int totalPigs, float fraction)
        {
            int required = Mathf.CeilToInt(totalPigs * Mathf.Clamp01(fraction));
            return new LevelGoal(totalPigs, required);
        }

        public int Killed => _killed;
        public int RequiredKills => _requiredKills;
        public int RemainingToWin => Mathf.Max(0, _requiredKills - _killed);
        public bool IsWon => _killed >= _requiredKills;

        public void NotifyPigKilled()
        {
            _killed++;
        }

        /// <summary>Lost when the objective is not met and no ammunition remains.</summary>
        public bool IsLost(int ammoRemaining)
        {
            return !IsWon && ammoRemaining <= 0;
        }
    }
}
