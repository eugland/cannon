using UnityEngine;

namespace Cannon.Game
{
    /// <summary>
    /// Simple space backdrop: scatters many small unlit quads behind the playfield.
    /// Built once and persists across levels. Star count is small enough for mobile.
    /// </summary>
    public class Starfield : MonoBehaviour
    {
        public int Count = 160;
        public float SpreadX = 28f;
        public float SpreadY = 15f;
        public float Depth = 8f;

        private void Start()
        {
            if (transform.childCount == 0)
                Build(Count);
        }

        /// <summary>Rebuild the starfield with the given number of stars. Returns the count created.</summary>
        public int Build(int count)
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
                Object.DestroyImmediate(transform.GetChild(i).gameObject);

            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Sprites/Default");

            var rng = new System.Random(12345);
            for (int i = 0; i < count; i++)
            {
                var star = GameObject.CreatePrimitive(PrimitiveType.Quad);
                star.name = "Star";
                var col = star.GetComponent<Collider>();
                if (col != null) Object.DestroyImmediate(col);

                float x = (float)(rng.NextDouble() * 2 - 1) * SpreadX;
                float y = (float)(rng.NextDouble() * 2 - 1) * SpreadY;
                star.transform.position = new Vector3(x, y, Depth);
                float s = 0.04f + (float)rng.NextDouble() * 0.09f;
                star.transform.localScale = Vector3.one * s;
                star.transform.SetParent(transform, true);

                float b = 0.6f + (float)rng.NextDouble() * 0.4f;
                star.GetComponent<Renderer>().sharedMaterial = new Material(shader) { color = new Color(b, b, b) };
            }
            return transform.childCount;
        }
    }
}
