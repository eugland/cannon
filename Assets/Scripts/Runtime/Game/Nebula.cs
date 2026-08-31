using UnityEngine;

namespace Cannon.Game
{
    /// <summary>
    /// Soft translucent color clouds behind the starfield for a pleasant space backdrop.
    /// Overlapping low-alpha quads read as nebula haze. Built once, cheap.
    /// </summary>
    public class Nebula : MonoBehaviour
    {
        public int Clouds = 9;
        public float Depth = 17f;

        private static readonly Color[] Hues =
        {
            new Color(0.35f, 0.25f, 0.55f), // violet
            new Color(0.2f, 0.35f, 0.6f),   // blue
            new Color(0.2f, 0.5f, 0.5f),    // teal
            new Color(0.5f, 0.3f, 0.45f)    // rose
        };

        private void Start()
        {
            if (transform.childCount == 0)
                Build(Clouds);
        }

        public int Build(int count)
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
                Object.DestroyImmediate(transform.GetChild(i).gameObject);

            var rng = new System.Random(777);
            for (int i = 0; i < count; i++)
            {
                var cloud = GameObject.CreatePrimitive(PrimitiveType.Quad);
                cloud.name = "Nebula";
                var col = cloud.GetComponent<Collider>();
                if (col != null) Object.DestroyImmediate(col);

                float x = (float)(rng.NextDouble() * 2 - 1) * 40f;
                float y = (float)(rng.NextDouble() * 2 - 1) * 24f;
                cloud.transform.position = new Vector3(x, y, Depth);
                float s = 12f + (float)rng.NextDouble() * 16f;
                cloud.transform.localScale = new Vector3(s, s, 1f);
                cloud.transform.SetParent(transform, true);

                Color hue = Hues[rng.Next(Hues.Length)];
                hue.a = 0.06f + (float)rng.NextDouble() * 0.06f; // faint
                cloud.GetComponent<Renderer>().sharedMaterial = MaterialFactory.Sprite(hue);
            }
            return transform.childCount;
        }
    }
}
