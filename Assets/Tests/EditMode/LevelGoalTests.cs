using Cannon.Flow;
using NUnit.Framework;

namespace Cannon.Tests.EditMode
{
    public class LevelGoalTests
    {
        [Test]
        public void NotWon_BeforeRequiredKills()
        {
            var goal = new LevelGoal(totalPigs: 3, requiredKills: 3);
            goal.NotifyPigKilled();
            goal.NotifyPigKilled();
            Assert.IsFalse(goal.IsWon);
            Assert.AreEqual(1, goal.RemainingToWin);
        }

        [Test]
        public void Won_WhenRequiredKillsReached()
        {
            var goal = new LevelGoal(3, 3);
            goal.NotifyPigKilled();
            goal.NotifyPigKilled();
            goal.NotifyPigKilled();
            Assert.IsTrue(goal.IsWon);
            Assert.AreEqual(0, goal.RemainingToWin);
        }

        [Test]
        public void PartialGoal_WinsWithoutAllPigs()
        {
            var goal = new LevelGoal(totalPigs: 5, requiredKills: 2);
            goal.NotifyPigKilled();
            goal.NotifyPigKilled();
            Assert.IsTrue(goal.IsWon);
        }

        [Test]
        public void FromPercentage_RoundsUp()
        {
            var goal = LevelGoal.FromPercentage(totalPigs: 5, fraction: 0.5f); // ceil(2.5)=3
            Assert.AreEqual(3, goal.RequiredKills);
        }

        [Test]
        public void RequiredKills_ClampedToPigCount()
        {
            var goal = new LevelGoal(totalPigs: 2, requiredKills: 10);
            Assert.AreEqual(2, goal.RequiredKills);
        }

        [Test]
        public void IsLost_OnlyWhenNotWonAndOutOfAmmo()
        {
            var goal = new LevelGoal(2, 2);
            goal.NotifyPigKilled(); // 1 of 2, not won
            Assert.IsTrue(goal.IsLost(ammoRemaining: 0));
            Assert.IsFalse(goal.IsLost(ammoRemaining: 1));
        }

        [Test]
        public void NotLost_WhenWonEvenWithNoAmmo()
        {
            var goal = new LevelGoal(1, 1);
            goal.NotifyPigKilled();
            Assert.IsFalse(goal.IsLost(ammoRemaining: 0));
        }
    }
}
