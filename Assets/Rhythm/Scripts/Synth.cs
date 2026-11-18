using UnityEngine;

namespace Echo
{
    /// <summary>
    /// Every sound in the game is synthesised at runtime, so the project ships with no audio assets.
    /// Holds get a warm pentatonic bell tone; rests are silence.
    /// </summary>
    public static class Synth
    {
        public const int Rate = 44100;

        // ---------------------------------------------------------------- track

        public static AudioClip BuildTrack(Rhythm rhythm, out int[] pitches)
        {
            float tail = 0.35f;
            int n = Mathf.CeilToInt((rhythm.length + tail) * Rate);
            float[] buf = new float[n];

            System.Random rng = new System.Random(rhythm.seed ^ 0x5f3759df);
            int[] pentatonic = { 0, 2, 4, 7, 9, 12, 14, 16, 19 };
            int degree = 4;

            var holds = rhythm.Holds();
            pitches = new int[holds.Count];

            for (int i = 0; i < holds.Count; i++)
            {
                degree = Mathf.Clamp(degree + rng.Next(-2, 3), 0, pentatonic.Length - 1);
                int midi = 60 + pentatonic[degree];
                pitches[i] = midi;
                AddBell(buf, holds[i].x, holds[i].y, MidiToHz(midi), 0.24f);
            }

            Normalize(buf, 0.72f);
            FadeEdges(buf, 220);
            return ToClip("Rhythm", buf);
        }

        static void AddBell(float[] buf, float start, float end, float hz, float gain)
        {
            int i0 = Mathf.Max(0, Mathf.CeilToInt(start * Rate));
            int i1 = Mathf.Min(buf.Length, Mathf.CeilToInt((end + 0.24f) * Rate));
            float dur = end - start;

            for (int i = i0; i < i1; i++)
            {
                float t = (i - i0) / (float)Rate;
                float attack = Mathf.Clamp01(t / 0.018f);
                float release = t <= dur ? 1f : Mathf.Clamp01(1f - (t - dur) / 0.22f);
                float env = attack * release * (0.82f + 0.18f * Mathf.Exp(-t * 1.6f));

                float vibPhase = 0.0032f * Mathf.Sin(2f * Mathf.PI * 5.1f * t);
                float phase = 2f * Mathf.PI * hz * t + vibPhase;

                float body = Mathf.Sin(phase)
                           + 0.26f * Mathf.Sin(phase * 2f)
                           + 0.09f * Mathf.Sin(phase * 3f);

                float pluck = Mathf.Exp(-t * 8f) * Mathf.Sin(phase * 2.01f + 0.6f) * 0.35f;

                buf[i] += (body + pluck) * env * gain;
            }
        }

        // ------------------------------------------------------------ sustained

        /// <summary>One sustained target-style bell note for the player's instrument.</summary>
        public static AudioClip BuildHoldNote(int midi, float seconds)
        {
            int n = Mathf.CeilToInt(seconds * Rate);
            float[] buf = new float[n];
            float sustain = Mathf.Max(0.15f, seconds - 0.24f);
            AddBell(buf, 0f, sustain, MidiToHz(midi), 0.3f);
            Normalize(buf, 0.6f);
            FadeEdges(buf, 128);
            return ToClip("Hold" + midi, buf);
        }

        // ------------------------------------------------------------------ sfx

        public static AudioClip Tick()
        {
            return MakeClip("Tick", 0.09f, delegate (float t)
            {
                return Mathf.Sin(2f * Mathf.PI * 1120f * t) * Mathf.Exp(-t * 55f);
            }, 0.7f);
        }

        public static AudioClip Go()
        {
            return MakeClip("Go", 0.24f, delegate (float t)
            {
                return Mathf.Sin(2f * Mathf.PI * 1560f * t) * Mathf.Exp(-t * 22f);
            }, 0.7f);
        }

        public static AudioClip Tap()
        {
            return MakeClip("Tap", 0.11f, delegate (float t)
            {
                float s = Mathf.Sin(2f * Mathf.PI * 720f * t) + 0.4f * Mathf.Sin(2f * Mathf.PI * 1440f * t);
                return s * Mathf.Exp(-t * 42f);
            }, 0.55f);
        }

        public static AudioClip Success()
        {
            float[] notes = { 523.25f, 659.25f, 783.99f, 1046.5f };
            float[] offsets = { 0f, 0.075f, 0.15f, 0.25f };
            return MakeClip("Success", 1.1f, delegate (float t)
            {
                float sum = 0f;
                for (int i = 0; i < notes.Length; i++)
                {
                    float tt = t - offsets[i];
                    if (tt < 0f) continue;
                    float env = Mathf.Clamp01(tt / 0.006f) * Mathf.Exp(-tt * 4.2f);
                    sum += (Mathf.Sin(2f * Mathf.PI * notes[i] * tt)
                          + 0.3f * Mathf.Sin(2f * Mathf.PI * notes[i] * 2f * tt)) * env;
                }
                return sum * 0.4f;
            }, 0.8f);
        }

        public static AudioClip Fail()
        {
            return MakeClip("Fail", 0.55f, delegate (float t)
            {
                float wobble = Mathf.Sin(2f * Mathf.PI * 9f * t) * 0.01f;
                float s = Mathf.Sin(2f * Mathf.PI * (168f + wobble * 400f) * t)
                        + 0.35f * Mathf.Sin(2f * Mathf.PI * 84f * t);
                return s * Mathf.Exp(-t * 4.5f) * Mathf.Clamp01(t * 160f);
            }, 0.7f);
        }

        public static AudioClip LostHeart()
        {
            return MakeClip("LostHeart", 0.35f, delegate (float t)
            {
                float f = Mathf.Lerp(240f, 150f, Mathf.Clamp01(t / 0.3f));
                return Mathf.Sin(2f * Mathf.PI * f * t) * Mathf.Exp(-t * 6f);
            }, 0.65f);
        }

        // --------------------------------------------------------------- helpers

        public static float MidiToHz(int midi)
        {
            return 440f * Mathf.Pow(2f, (midi - 69) / 12f);
        }

        static AudioClip MakeClip(string name, float seconds, System.Func<float, float> fn, float peak)
        {
            int n = Mathf.Max(1, Mathf.CeilToInt(seconds * Rate));
            float[] buf = new float[n];
            for (int i = 0; i < n; i++)
                buf[i] = fn(i / (float)Rate);
            Normalize(buf, peak);
            FadeEdges(buf, 64);
            return ToClip(name, buf);
        }

        static void Normalize(float[] buf, float peak)
        {
            float max = 0.0001f;
            for (int i = 0; i < buf.Length; i++)
            {
                float a = Mathf.Abs(buf[i]);
                if (a > max) max = a;
            }
            float k = peak / max;
            for (int i = 0; i < buf.Length; i++) buf[i] *= k;
        }

        static void FadeEdges(float[] buf, int samples)
        {
            int n = Mathf.Min(samples, buf.Length / 2);
            for (int i = 0; i < n; i++)
            {
                float k = i / (float)n;
                buf[i] *= k;
                buf[buf.Length - 1 - i] *= k;
            }
        }

        static AudioClip ToClip(string name, float[] buf)
        {
            AudioClip clip = AudioClip.Create(name, buf.Length, 1, Rate, false);
            clip.SetData(buf, 0);
            return clip;
        }
    }
}
