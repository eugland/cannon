using UnityEngine;

namespace Cannon.Game
{
    /// <summary>
    /// Generates simple procedural audio clips at runtime (no audio assets needed),
    /// used for fire / hit / win / lose feedback.
    /// </summary>
    public static class SoundFx
    {
        private const int Rate = 44100;

        /// <summary>A decaying tone at the given frequency and duration.</summary>
        public static AudioClip Tone(float freq, float dur, float volume = 0.4f)
        {
            int n = Mathf.Max(1, (int)(Rate * dur));
            var data = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / Rate;
                float env = Mathf.Exp(-4f * i / n); // quick decay
                data[i] = Mathf.Sin(2f * Mathf.PI * freq * t) * env * volume;
            }
            var clip = AudioClip.Create("tone", n, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        /// <summary>A short upward two-note chime (for a win).</summary>
        public static AudioClip Chime()
        {
            int n = (int)(Rate * 0.35f);
            var data = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / Rate;
                float freq = i < n / 2 ? 660f : 990f;
                float env = Mathf.Exp(-3f * i / n);
                data[i] = Mathf.Sin(2f * Mathf.PI * freq * t) * env * 0.4f;
            }
            var clip = AudioClip.Create("chime", n, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
