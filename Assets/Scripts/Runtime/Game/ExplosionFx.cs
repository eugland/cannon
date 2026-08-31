using UnityEngine;

namespace Cannon.Game
{
    /// <summary>
    /// A brief expanding, fading flash at a blast point so explosions read clearly.
    /// Self-destroys when finished.
    /// </summary>
    public class ExplosionFx : MonoBehaviour
    {
        private float _t;
        private float _life = 0.35f;
        private float _maxScale = 3f;
        private Material _mat;

        public static void Spawn(Vector3 pos, float radius)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "Explosion";
            var col = go.GetComponent<Collider>();
            if (col != null) Object.Destroy(col);
            go.transform.position = pos;
            go.AddComponent<ExplosionFx>().Init(radius);
        }

        private void Init(float radius)
        {
            _maxScale = radius * 2f;
            transform.localScale = Vector3.one * 0.4f;
            _mat = MaterialFactory.Sprite(new Color(1f, 0.7f, 0.25f, 0.85f));
            GetComponent<Renderer>().sharedMaterial = _mat;
        }

        private void Update()
        {
            _t += Time.deltaTime;
            float k = _t / _life;
            if (k >= 1f) { Object.Destroy(gameObject); return; }

            transform.localScale = Vector3.one * Mathf.Lerp(0.4f, _maxScale, k);
            if (_mat != null)
            {
                Color c = _mat.color;
                c.a = 0.85f * (1f - k);
                _mat.color = c;
            }
        }
    }
}
