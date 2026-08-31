using NUnit.Framework;

namespace Cannon.Tests.EditMode
{
    /// <summary>
    /// Minimal test proving the headless EditMode test runner is wired up.
    /// Replaced by real gameplay tests as the game is built.
    /// </summary>
    public class SmokeTest
    {
        [Test]
        public void TestRunner_IsWorking()
        {
            Assert.AreEqual(4, 2 + 2);
        }
    }
}
