using System.Collections.Generic;

namespace Cannon.Gravity
{
    /// <summary>
    /// Tracks the active gravity bodies in a level so the projectile and preview can
    /// query current wells without scene-wide searches each step. Plain static state
    /// (a level has one gravity world); bodies register on enable, unregister on disable.
    /// </summary>
    public static class GravityRegistry
    {
        private static readonly List<GravityBody> Bodies = new List<GravityBody>();

        public static IReadOnlyList<GravityBody> ActiveBodies => Bodies;

        public static void Register(GravityBody body)
        {
            if (body != null && !Bodies.Contains(body))
                Bodies.Add(body);
        }

        public static void Unregister(GravityBody body)
        {
            Bodies.Remove(body);
        }

        /// <summary>Clear all bodies (call on level teardown / test setup).</summary>
        public static void Clear()
        {
            Bodies.Clear();
        }

        /// <summary>Fill <paramref name="buffer"/> with the current wells (no allocation per call).</summary>
        public static void CollectWells(List<GravityWell> buffer)
        {
            buffer.Clear();
            for (int i = 0; i < Bodies.Count; i++)
            {
                if (Bodies[i] != null)
                    buffer.Add(Bodies[i].ToWell());
            }
        }
    }
}
