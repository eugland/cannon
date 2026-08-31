using Cannon.Flow;
using NUnit.Framework;

namespace Cannon.Tests.EditMode
{
    public class ScoreModelTests
    {
        [Test]
        public void AtOrUnderPar_ThreeStars()
        {
            Assert.AreEqual(3, ScoreModel.Stars(shotsUsed: 1, par: 2));
            Assert.AreEqual(3, ScoreModel.Stars(shotsUsed: 2, par: 2));
        }

        [Test]
        public void WithinParPlusTwo_TwoStars()
        {
            Assert.AreEqual(2, ScoreModel.Stars(3, 2));
            Assert.AreEqual(2, ScoreModel.Stars(4, 2));
        }

        [Test]
        public void BeyondParPlusTwo_OneStar()
        {
            Assert.AreEqual(1, ScoreModel.Stars(5, 2));
            Assert.AreEqual(1, ScoreModel.Stars(20, 2));
        }

        [Test]
        public void ParClampedToAtLeastOne()
        {
            Assert.AreEqual(3, ScoreModel.Stars(1, 0));
        }
    }
}
