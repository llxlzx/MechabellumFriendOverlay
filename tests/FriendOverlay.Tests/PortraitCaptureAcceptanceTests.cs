using FriendOverlay.Core;
using Xunit;

namespace FriendOverlay.Tests;

public class PortraitCaptureAcceptanceTests
{
    private static byte[] Solid(int w, int h, byte r, byte g, byte b, byte a)
    {
        var px = new byte[w * h * 4];
        for (var i = 0; i < w * h; i++)
        {
            var o = i * 4;
            px[o] = r;
            px[o + 1] = g;
            px[o + 2] = b;
            px[o + 3] = a;
        }

        return px;
    }

    /// <summary>Opaque border ring, transparent center — typical avatar frame.</summary>
    private static byte[] HollowRing(int w, int h)
    {
        var px = Solid(w, h, 0, 0, 0, 0);
        var border = Math.Max(2, w / 8);
        for (var y = 0; y < h; y++)
        for (var x = 0; x < w; x++)
        {
            var edge = x < border || y < border || x >= w - border || y >= h - border;
            if (!edge)
                continue;
            var o = (y * w + x) * 4;
            px[o] = 200;
            px[o + 1] = 200;
            px[o + 2] = 220;
            px[o + 3] = 255;
        }

        return px;
    }

    [Fact]
    public void Opaque_face_passes_Face()
    {
        var px = Solid(64, 64, 120, 90, 70, 255);
        Assert.True(PortraitCaptureAcceptance.Evaluate(64, 64, px, PortraitBakeKind.Face, out var reject));
        Assert.Equal(PortraitCaptureReject.None, reject);
    }

    [Fact]
    public void Hollow_ring_fails_Face_passes_Outline()
    {
        var px = HollowRing(64, 64);
        Assert.False(PortraitCaptureAcceptance.Evaluate(64, 64, px, PortraitBakeKind.Face, out var faceReject));
        Assert.Equal(PortraitCaptureReject.Wireframe, faceReject);

        Assert.True(PortraitCaptureAcceptance.Evaluate(64, 64, px, PortraitBakeKind.Outline, out var outlineReject));
        Assert.Equal(PortraitCaptureReject.None, outlineReject);
    }

    [Fact]
    public void Near_black_plate_fails_Face_passes_Outline()
    {
        var px = Solid(64, 64, 8, 8, 8, 255);
        Assert.False(PortraitCaptureAcceptance.Evaluate(64, 64, px, PortraitBakeKind.Face, out var faceReject));
        Assert.Equal(PortraitCaptureReject.Black, faceReject);

        Assert.True(PortraitCaptureAcceptance.Evaluate(64, 64, px, PortraitBakeKind.Outline, out var outlineReject));
        Assert.Equal(PortraitCaptureReject.None, outlineReject);
    }

    [Fact]
    public void Tiny_or_null_fails_both()
    {
        Assert.False(PortraitCaptureAcceptance.Evaluate(1, 1, Solid(1, 1, 255, 0, 0, 255), PortraitBakeKind.Face, out var a));
        Assert.Equal(PortraitCaptureReject.NullOrTiny, a);

        Assert.False(PortraitCaptureAcceptance.Evaluate(64, 64, null!, PortraitBakeKind.Outline, out var b));
        Assert.Equal(PortraitCaptureReject.NullOrTiny, b);

        Assert.False(PortraitCaptureAcceptance.Evaluate(64, 64, new byte[10], PortraitBakeKind.Face, out var c));
        Assert.Equal(PortraitCaptureReject.NullOrTiny, c);
    }

    [Fact]
    public void Nearly_transparent_fails_both_as_Sparse()
    {
        var px = Solid(64, 64, 255, 255, 255, 0);
        // One opaque pixel → coverage well under 5%
        px[0] = 255;
        px[1] = 255;
        px[2] = 255;
        px[3] = 255;

        Assert.False(PortraitCaptureAcceptance.Evaluate(64, 64, px, PortraitBakeKind.Face, out var faceReject));
        Assert.Equal(PortraitCaptureReject.Sparse, faceReject);

        Assert.False(PortraitCaptureAcceptance.Evaluate(64, 64, px, PortraitBakeKind.Outline, out var outlineReject));
        Assert.Equal(PortraitCaptureReject.Sparse, outlineReject);
    }
}
