using System.Collections.Generic;
using UnityEngine;

namespace Echo
{
    /// <summary>A single slice of the staff: either a held block or a silent rest.</summary>
    public struct RhythmSegment
    {
        public float start;
        public float end;
        public bool hold;

        public float Duration { get { return end - start; } }
    }

    /// <summary>
    /// A rhythm is a strictly alternating sequence of holds and rests that tiles [0, length].
    /// It always begins with a short silent lead-in so the player can settle in.
    /// </summary>
    public class Rhythm
    {
        public readonly List<RhythmSegment> segments = new List<RhythmSegment>();
        public float length;
        public int holdCount;
        public int seed;

        public static Rhythm Generate(int round)
        {
            int seed = unchecked(System.Environment.TickCount * 31 + round * 7919);
            return Generate(round, seed);
        }

        public static Rhythm Generate(int round, int seed)
        {
            System.Random rng = new System.Random(seed);
            Rhythm r = new Rhythm();
            r.seed = seed;

            // Round 1 gets two holds, then one more every two rounds, up to five.
            int pairs = Mathf.Clamp(2 + (round - 1) / 2, 2, 5);

            float[] holdPool;
            float[] restPool;
            if (round < 3)
            {
                holdPool = new float[] { 0.6f, 0.8f, 1.0f, 1.2f };
                restPool = new float[] { 0.5f, 0.8f, 1.0f };
            }
            else if (round < 5)
            {
                holdPool = new float[] { 0.5f, 0.6f, 0.8f, 1.0f, 1.2f };
                restPool = new float[] { 0.5f, 0.6f, 0.8f, 1.0f };
            }
            else
            {
                holdPool = new float[] { 0.4f, 0.5f, 0.6f, 0.8f, 1.0f, 1.2f };
                restPool = new float[] { 0.3f, 0.4f, 0.5f, 0.6f, 0.8f };
            }

            float t = 0f;
            float lead = 1.0f + (float)rng.NextDouble() * 0.25f;
            t = Add(r, t, lead, false);

            float prevHold = -1f;
            float prevRest = -1f;
            for (int i = 0; i < pairs; i++)
            {
                float hold = Pick(rng, holdPool, prevHold);
                t = Add(r, t, hold, true);
                r.holdCount++;
                prevHold = hold;

                float rest = Pick(rng, restPool, prevRest);
                t = Add(r, t, rest, false);
                prevRest = rest;
            }

            // Let the final rest breathe a little longer.
            float lastEnd = t + 0.4f;
            RhythmSegment last = r.segments[r.segments.Count - 1];
            last.end = lastEnd;
            r.segments[r.segments.Count - 1] = last;
            r.length = lastEnd;
            return r;
        }

        static float Pick(System.Random rng, float[] pool, float avoid)
        {
            float v = pool[rng.Next(pool.Length)];
            if (pool.Length > 1 && Mathf.Abs(v - avoid) < 0.001f)
                v = pool[rng.Next(pool.Length)];
            return v;
        }

        static float Add(Rhythm r, float t, float dur, bool hold)
        {
            RhythmSegment s = new RhythmSegment();
            s.start = t;
            s.end = t + dur;
            s.hold = hold;
            r.segments.Add(s);
            return t + dur;
        }

        public bool IsHoldAt(float t)
        {
            if (t < 0f || t >= length) return false;
            int lo = 0;
            int hi = segments.Count - 1;
            while (lo <= hi)
            {
                int mid = (lo + hi) >> 1;
                RhythmSegment s = segments[mid];
                if (t < s.start) hi = mid - 1;
                else if (t >= s.end) lo = mid + 1;
                else return s.hold;
            }
            return false;
        }

        public List<Vector2> Holds()
        {
            List<Vector2> list = new List<Vector2>();
            for (int i = 0; i < segments.Count; i++)
                if (segments[i].hold)
                    list.Add(new Vector2(segments[i].start, segments[i].end));
            return list;
        }

        public float HoldFraction()
        {
            if (length <= 0f) return 0f;
            float sum = 0f;
            for (int i = 0; i < segments.Count; i++)
                if (segments[i].hold) sum += segments[i].Duration;
            return sum / length;
        }
    }

    /// <summary>Compares the player's holds to the target holds only; gaps are not sampled.</summary>
    public static class RhythmScorer
    {
        public const float PassThreshold = 0.65f;
        const float MaxShift = 0.35f;
        const float EdgeTolerance = 0.15f;

        /// <summary>
        /// 0..1 hold score. Coverage = how much of the target's hold time you covered.
        /// Precision = how much of your own holding landed inside target holds. Score =
        /// coverage × precision, so silence is 0, a clean reproduction is 1, and holding
        /// through the gaps is penalised by precision.
        /// </summary>
        public static float Score(Rhythm target, List<Vector2> player, out float bestShift)
        {
            if (player == null || player.Count == 0)
            {
                bestShift = 0f;
                return 0f;
            }

            float best = HoldScore(target, player, 0f);
            bestShift = 0f;

            // Scan outward from zero so ties keep the smallest timing correction.
            int steps = Mathf.RoundToInt(MaxShift / 0.01f);
            for (int i = 1; i <= steps; i++)
            {
                float a = HoldScore(target, player, -i * 0.01f);
                if (a > best + 0.0005f)
                {
                    best = a;
                    bestShift = -i * 0.01f;
                }
                float b = HoldScore(target, player, i * 0.01f);
                if (b > best + 0.0005f)
                {
                    best = b;
                    bestShift = i * 0.01f;
                }
            }

            float center = bestShift;
            for (int i = -4; i <= 4; i++)
            {
                float shift = center + i * 0.0025f;
                float v = HoldScore(target, player, shift);
                if (v > best + 0.0002f)
                {
                    best = v;
                    bestShift = shift;
                }
            }
            return Mathf.Clamp01(best);
        }

        static float HoldScore(Rhythm target, List<Vector2> player, float shift)
        {
            const float dt = 0.01f;
            int n = Mathf.Max(1, Mathf.RoundToInt(target.length / dt));
            float targetHold = 0f;
            float matched = 0f;

            for (int i = 0; i < n; i++)
            {
                float t = (i + 0.5f) * dt;
                if (!target.IsHoldAt(t)) continue;
                targetHold += dt;
                if (IsHoldNear(player, t - shift, EdgeTolerance)) matched += dt;
            }
            if (targetHold <= 0f) return 0f;

            float playerHold = 0f;
            for (int i = 0; i < player.Count; i++)
                playerHold += Mathf.Max(0f, player[i].y - player[i].x);
            if (playerHold <= 0.0001f) return 0f;

            float coverage = matched / targetHold;
            float precision = Mathf.Clamp01(matched / playerHold);
            return coverage * precision;
        }

        static bool IsHoldNear(List<Vector2> holds, float t, float tolerance)
        {
            if (holds == null) return false;
            for (int i = 0; i < holds.Count; i++)
            {
                if (t >= holds[i].x - tolerance && t < holds[i].y + tolerance) return true;
            }
            return false;
        }

        public static bool IsHold(List<Vector2> holds, float t)
        {
            if (holds == null || t < 0f) return false;
            for (int i = 0; i < holds.Count; i++)
                if (t >= holds[i].x && t < holds[i].y) return true;
            return false;
        }

        public static int Stars(float similarity)
        {
            if (similarity >= 0.92f) return 3;
            if (similarity >= 0.80f) return 2;
            if (similarity >= PassThreshold) return 1;
            return 0;
        }
    }
}
