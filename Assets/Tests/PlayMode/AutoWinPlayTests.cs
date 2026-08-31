using System.Collections;
using Cannon.Game;
using Cannon.Targets;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Cannon.Tests.PlayMode
{
    /// <summary>
    /// Proves every level is winnable by an automated player and that playing through
    /// raises no exceptions/errors (no crash). This is the loop's verification gate.
    /// </summary>
    public class AutoWinPlayTests
    {
        [UnityTest]
        public IEnumerator EveryLevel_IsWinnable_WithoutErrors()
        {
            var go = new GameObject("GameManager");
            var gm = go.AddComponent<GameManager>();

            yield return null; // GameManager.Start builds level 0
            Assert.Greater(gm.LevelCount, 0, "There must be at least one level.");
            int levelCount = gm.LevelCount;

            for (int li = 0; li < levelCount; li++)
            {
                gm.LoadLevelPublic(li);
                yield return new WaitForFixedUpdate();

                int guard = 0;
                while (gm.State != GameState.Won && guard < 80)
                {
                    if (gm.State == GameState.Aiming)
                    {
                        Pig target = FirstAlivePig(gm);
                        if (target != null)
                            gm.FireAt(target.transform.position);
                    }

                    // Wait out the shot flight + resolve delay.
                    float t = 0f;
                    while (gm.State == GameState.Fired && t < 12f)
                    {
                        t += Time.deltaTime;
                        yield return null;
                    }
                    yield return new WaitForSeconds(0.15f);

                    if (gm.State == GameState.Lost)
                        break;

                    guard++;
                }

                Assert.AreEqual(GameState.Won, gm.State,
                    $"Level {li + 1} should be winnable by the auto-player (pigs left: {gm.PigsAlive}).");
            }

            Object.Destroy(go);
            yield return null;

            // No Debug.LogError / exceptions were emitted during the whole playthrough.
            LogAssert.NoUnexpectedReceived();
        }

        private static Pig FirstAlivePig(GameManager gm)
        {
            foreach (var p in gm.Pigs)
                if (p != null && !p.IsDead)
                    return p;
            return null;
        }
    }
}
