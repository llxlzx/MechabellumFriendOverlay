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
    public static WindowResizeEdge HitTest(float localX, float localY, float w, float h, float thickness) =>
        HitTest(localX, localY, w, h, edgeThickness: thickness, cornerThickness: thickness);

    public static WindowResizeEdge HitTest(
        float localX,
        float localY,
        float w,
        float h,
        float edgeThickness,
        float cornerThickness)
    {
        if (w <= 0f || h <= 0f || edgeThickness <= 0f)
            return WindowResizeEdge.None;

        if (cornerThickness < edgeThickness)
            cornerThickness = edgeThickness;

        // Corners first (enlarged pad), then edges.
        var onLCorner = localX >= 0f && localX <= cornerThickness;
        var onRCorner = localX >= w - cornerThickness && localX <= w;
        var onTCorner = localY >= 0f && localY <= cornerThickness;
        var onBCorner = localY >= h - cornerThickness && localY <= h;

        if (onTCorner && onLCorner) return WindowResizeEdge.NW;
        if (onTCorner && onRCorner) return WindowResizeEdge.NE;
        if (onBCorner && onLCorner) return WindowResizeEdge.SW;
        if (onBCorner && onRCorner) return WindowResizeEdge.SE;

        var onL = localX >= 0f && localX <= edgeThickness;
        var onR = localX >= w - edgeThickness && localX <= w;
        var onT = localY >= 0f && localY <= edgeThickness;
        var onB = localY >= h - edgeThickness && localY <= h;

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
