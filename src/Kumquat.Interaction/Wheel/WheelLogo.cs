// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Kumquat contributors

using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using Kumquat.Design;

namespace Kumquat.Interaction.Wheel;

/// <summary>
/// Logo para el centro de la rueda, reescalado una sola vez por tamaño (el original es grande y
/// la rueda se redibuja en cada movimiento sobre un sector).
/// </summary>
internal static class WheelLogo
{
    private static readonly Lock Gate = new();
    private static Bitmap? _scaled;

    /// <summary>Logo cuadrado del lado pedido, o <c>null</c> si no se encuentra el archivo.</summary>
    public static Bitmap? At(int side)
    {
        lock (Gate)
        {
            if (_scaled?.Width == side)
            {
                return _scaled;
            }

            if (!File.Exists(DesignAssets.LogoPng))
            {
                return null;
            }

            using var source = new Bitmap(DesignAssets.LogoPng);
            var scaled = new Bitmap(side, side, PixelFormat.Format32bppPArgb);
            using (var g = Graphics.FromImage(scaled))
            {
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.CompositingQuality = CompositingQuality.HighQuality;
                var ratio = Math.Min((float)side / source.Width, (float)side / source.Height);
                var w = source.Width * ratio;
                var h = source.Height * ratio;
                g.DrawImage(source, (side - w) / 2, (side - h) / 2, w, h);
            }

            _scaled?.Dispose();
            _scaled = scaled;
            return scaled;
        }
    }
}
