using System.Collections;
using Cannon.Game;
using Cannon.Gravity;
using Cannon.Targets;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Cannon.Tests.PlayMode
{
    /// <summary>Temporary diagnostic: observe a solved shot on level 0. Always "passes"; logs data.</summary>
    public class DiagTests
    {
        [UnityTest]
        public IEnumerator Diag_Level0_ShotBehaviour()
        {
            var go = new GameObject("GM");
            var gm = go.AddComponent<GameManager>();
            yield return null;
            gm.LoadLevelPublic(0);
            yield return new WaitForFixedUpdate();

            Pig pig = null;
            foreach (var p in gm.Pigs) { pig = p; break; }
            Vector3 tgt = pig.transform.position;

            gm.SolveShot(tgt, out Vector3 dir, out float hold);
            Debug.Log($"DIAG solve dir={dir} hold={hold:0.00} pigPos={tgt}");

            var proj = gm.FireAt(tgt);
            float minDist = 999f;
            int frames = 0;
            while (proj != null && !proj.HasEnded && frames < 600)
            {
                float dd = Vector3.Distance(proj.transform.position, pig.transform.position);
                if (dd < minDist) minDist = dd;
                if (frames % 30 == 0)
                    Debug.Log($"DIAG t={frames} projPos={proj.transform.position} vel={proj.Velocity.magnitude:0.0}");
                frames++;
                yield return new WaitForFixedUpdate();
            }
            Debug.Log($"DIAG RESULT minDistToPig={minDist:0.00} pigDead={pig.IsDead} frames={frames} state={gm.State}");

            Object.Destroy(go);
            Assert.Pass();
        }
    }
}
