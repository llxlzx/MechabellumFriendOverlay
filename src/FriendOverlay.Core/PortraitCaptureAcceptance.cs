namespace FriendOverlay.Core
{
    /// <summary>
    /// Pixel gate for CaptureRoot readbacks. Face rejects hollow/black plates; Outline allows
    /// empty centers (avatar frames) and dark chrome.
    /// </summary>
    public static class PortraitCaptureAcceptance
    {
        /// <summary>
        /// Evaluates RGBA8 pixels (length must be w*h*4: R,G,B,A per pixel, row-major).
        /// </summary>
        public static bool Evaluate(
            int w,
            int h,
            byte[]? rgba,
            PortraitBakeKind kind,
            out PortraitCaptureReject reject)
        {
            reject = PortraitCaptureReject.NullOrTiny;
            if (rgba == null || w < 2 || h < 2)
                return false;

            var expected = w * h * 4;
            if (rgba.Length < expected)
                return false;

            var pixelCount = w * h;
            var step = pixelCount / 4096;
            if (step < 1)
                step = 1;

            var sampled = 0;
            var opaque = 0;
            long lumSum = 0;
            var centerOpaque = 0;
            var centerSampled = 0;
            var x0 = w / 4;
            var x1 = (3 * w) / 4;
            var y0 = h / 4;
            var y1 = (3 * h) / 4;

            for (var i = 0; i < pixelCount; i += step)
            {
                sampled++;
                var x = i % w;
                var y = i / w;
                var inCenter = x >= x0 && x < x1 && y >= y0 && y < y1;
                if (inCenter)
                    centerSampled++;

                var o = i * 4;
                var a = rgba[o + 3];
                if (a < 16)
                    continue;

                opaque++;
                lumSum += (rgba[o] + rgba[o + 1] + rgba[o + 2]) / 3;
                if (inCenter)
                    centerOpaque++;
            }

            if (sampled == 0)
            {
                reject = PortraitCaptureReject.Sparse;
                return false;
            }

            var coverage = opaque / (float)sampled;
            // Faces need a solid plate; outlines can be thin corner ornaments (~1–2% coverage).
            var minCoverage = kind == PortraitBakeKind.Outline ? 0.012f : 0.05f;
            if (coverage < minCoverage)
            {
                reject = PortraitCaptureReject.Sparse;
                return false;
            }

            if (kind == PortraitBakeKind.Face)
            {
                if (centerSampled > 0 && centerOpaque / (float)centerSampled < 0.02f)
                {
                    reject = PortraitCaptureReject.Wireframe;
                    return false;
                }

                if (opaque > 0)
                {
                    var avgLum = lumSum / (double)opaque;
                    if (avgLum < 18.0)
                    {
                        reject = PortraitCaptureReject.Black;
                        return false;
                    }
                }
            }

            reject = PortraitCaptureReject.None;
            return true;
        }
    }
}
