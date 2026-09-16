using System;

namespace FriendOverlay.Core;

public enum WindowResizeEdge
{
    None = 0,
    N,
    S,
    E,
    W,
    NE,
    NW,
    SE,
    SW,
}

public readonly struct WindowRectF
{
    public WindowRectF(float x, float y, float w, float h)
    {
        X = x;
        Y = y;
        W = w;
        H = h;
    }

    public float X { get; }
    public float Y { get; }
    public float W { get; }
    public float H { get; }
}

/// <summary>
/// Pure hit-test and resize math for IMGUI overlay chrome (local coords, origin top-left).
/// Deltas: +dx grows/moves toward +X; +dy grows/moves toward +Y (down).
/// </summary>
public static class WindowResizeMath
{
    public static WindowResizeEdge HitTest(float localX, float localY, float w, float h, float thickness)
    {
        if (w <= 0f || h <= 0f || thickness <= 0f)
            return WindowResizeEdge.None;

        var onL = localX >= 0f && localX <= thickness;
        var onR = localX >= w - thickness && localX <= w;
        var onT = localY >= 0f && localY <= thickness;
        var onB = localY >= h - thickness && localY <= h;

        if (onT && onL) return WindowResizeEdge.NW;
        if (onT && onR) return WindowResizeEdge.NE;
        if (onB && onL) return WindowResizeEdge.SW;
        if (onB && onR) return WindowResizeEdge.SE;
        if (onT) return WindowResizeEdge.N;
        if (onB) return WindowResizeEdge.S;
        if (onL) return WindowResizeEdge.W;
        if (onR) return WindowResizeEdge.E;
        return WindowResizeEdge.None;
    }

    public static WindowRectF ApplyDelta(
        WindowResizeEdge edge,
        float x,
        float y,
        float w,
        float h,
        float dx,
        float dy,
        float minW,
        float minH)
    {
        if (edge == WindowResizeEdge.None)
            return new WindowRectF(x, y, w, h);

        var nx = x;
        var ny = y;
        var nw = w;
        var nh = h;

        var resizeW = edge is WindowResizeEdge.E or WindowResizeEdge.NE or WindowResizeEdge.SE
            or WindowResizeEdge.W or WindowResizeEdge.NW or WindowResizeEdge.SW;
        var resizeH = edge is WindowResizeEdge.N or WindowResizeEdge.NE or WindowResizeEdge.NW
            or WindowResizeEdge.S or WindowResizeEdge.SE or WindowResizeEdge.SW;
        var fromWest = edge is WindowResizeEdge.W or WindowResizeEdge.NW or WindowResizeEdge.SW;
        var fromNorth = edge is WindowResizeEdge.N or WindowResizeEdge.NE or WindowResizeEdge.NW;

        if (resizeW)
        {
            if (fromWest)
            {
                var newW = w - dx;
                if (newW < minW)
                {
                    dx = w - minW;
                    newW = minW;
                }

                nx = x + dx;
                nw = newW;
            }
            else
            {
                nw = Math.Max(minW, w + dx);
            }
        }

        if (resizeH)
        {
            if (fromNorth)
            {
                var newH = h - dy;
                if (newH < minH)
                {
                    dy = h - minH;
                    newH = minH;
                }

                ny = y + dy;
                nh = newH;
            }
            else
            {
                nh = Math.Max(minH, h + dy);
            }
        }

        return new WindowRectF(nx, ny, nw, nh);
    }
}
