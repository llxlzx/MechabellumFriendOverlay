using UnityEngine;

namespace FriendOverlay.UI
{
    /// <summary>Unscaled-time helpers so animation keeps running while the game is paused.</summary>
    public static class Anim
    {
        public static bool Enabled { get; set; } = true;

        public static float Now => Time.unscaledTime;

        /// <summary>Eased 0..1 progress since <paramref name="startTime"/>.</summary>
        public static float Progress(float startTime, float duration)
        {
            if (!Enabled || duration <= 0f)
                return 1f;

            var t = Mathf.Clamp01((Now - startTime) / duration);
            return t * t * (3f - 2f * t);
        }

        /// <summary>Triangle wave in 0..1.</summary>
        public static float Pulse(float period)
        {
            if (!Enabled || period <= 0f)
                return 1f;

            var t = Mathf.Repeat(Now, period) / period;
            return t < 0.5f ? t * 2f : (1f - t) * 2f;
        }

        /// <summary>Sawtooth wave in 0..1, used for the title bar light sweep.</summary>
        public static float Sweep(float period)
        {
            if (!Enabled || period <= 0f)
                return 0f;

            return Mathf.Repeat(Now, period) / period;
        }
    }
}
