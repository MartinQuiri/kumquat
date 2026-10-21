// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Kumquat contributors

using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Kumquat.Dialogs.Shelf;

/// <summary>
/// Miniaturas del Explorador (la foto en sí, la primera página de un PDF) o, si no hay, el ícono
/// del tipo de archivo. Se pueden pedir desde cualquier hilo: el resultado viene congelado.
/// </summary>
internal static partial class ShellThumbnails
{
    private const int IconOnly = 0x4; // SIIGBF_ICONONLY
    private const int ThumbnailOnly = 0x8; // SIIGBF_THUMBNAILONLY

    private static readonly StrategyBasedComWrappers ComWrappers = new();

    /// <summary>
    /// Pide la imagen en un hilo STA propio, de fondo: los generadores de miniaturas del Explorador
    /// esperan un STA (desde el grupo de hilos, que es MTA, COM levanta hilos de apoyo propios) y
    /// uno de fondo nunca impide que Kumquat se cierre.
    /// </summary>
    public static Task<BitmapSource?> GetAsync(string path, int size, bool iconOnly)
    {
        var done = new TaskCompletionSource<BitmapSource?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            try
            {
                done.SetResult(Get(path, size, iconOnly));
            }
#pragma warning disable CA1031 // Límite de un hilo propio: el error viaja en la tarea.
            catch (Exception ex)
#pragma warning restore CA1031
            {
                done.SetException(ex);
            }
        })
        {
            IsBackground = true,
            Name = "Kumquat: miniatura",
        };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        return done.Task;
    }

    /// <summary>
    /// Imagen de lado <paramref name="size"/> píxeles, o <c>null</c> si Windows no da ninguna:
    /// la miniatura del contenido o, con <paramref name="iconOnly"/>, el ícono del tipo de archivo.
    /// </summary>
    private static BitmapSource? Get(string path, int size, bool iconOnly)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        var iid = typeof(IShellItemImageFactory).GUID;
        if (SHCreateItemFromParsingName(path, 0, in iid, out var unknown) < 0 || unknown == 0)
        {
            return null;
        }

        try
        {
            var factory = (IShellItemImageFactory)ComWrappers.GetOrCreateObjectForComInstance(unknown, CreateObjectFlags.None);
            factory.GetImage(new NativeSize(size, size), iconOnly ? IconOnly : ThumbnailOnly, out var bitmap);
            try
            {
                return ToBitmapSource(bitmap);
            }
            finally
            {
                _ = DeleteObject(bitmap);
            }
        }
        catch (Exception ex) when (ex is COMException or InvalidCastException or ArgumentException)
        {
            return null; // Archivo que ya no está, sin permiso o sin manejador de miniaturas.
        }
        finally
        {
            _ = Marshal.Release(unknown);
        }
    }

    /// <summary>
    /// HBITMAP de 32 bits a imagen WPF. Las miniaturas de fotos vienen con el canal alfa en cero
    /// (serían invisibles): en ese caso se vuelven opacas; los íconos traen su alfa real.
    /// </summary>
    private static unsafe BitmapSource? ToBitmapSource(nint bitmap)
    {
        if (GetObject(bitmap, sizeof(NativeBitmap), out var info) == 0 || info.Width <= 0 || info.Height <= 0)
        {
            return null;
        }

        var width = info.Width;
        var height = info.Height;
        var stride = width * 4;
        var pixels = new byte[stride * height];
        var header = new BitmapInfoHeader
        {
            Size = (uint)sizeof(BitmapInfoHeader),
            Width = width,
            Height = -height, // de arriba hacia abajo
            Planes = 1,
            BitCount = 32,
        };
        var screen = GetDC(0);
        try
        {
            fixed (byte* bits = pixels)
            {
                if (GetDIBits(screen, bitmap, 0, (uint)height, bits, ref header, usage: 0) == 0)
                {
                    return null;
                }
            }
        }
        finally
        {
            _ = ReleaseDC(0, screen);
        }

        var anyAlpha = false;
        for (var i = 3; i < pixels.Length && !anyAlpha; i += 4)
        {
            anyAlpha = pixels[i] != 0;
        }

        if (!anyAlpha)
        {
            for (var i = 3; i < pixels.Length; i += 4)
            {
                pixels[i] = 255;
            }
        }

        var source = BitmapSource.Create(width, height, 96, 96, PixelFormats.Pbgra32, null, pixels, stride);
        source.Freeze();
        return source;
    }

    [LibraryImport("shell32.dll", StringMarshalling = StringMarshalling.Utf16)]
    private static partial int SHCreateItemFromParsingName(string path, nint bindContext, in Guid iid, out nint item);

    [LibraryImport("gdi32.dll", EntryPoint = "GetObjectW")]
    private static partial int GetObject(nint handle, int size, out NativeBitmap bitmap);

    [LibraryImport("gdi32.dll")]
    private static unsafe partial int GetDIBits(nint dc, nint bitmap, uint start, uint lines, byte* bits, ref BitmapInfoHeader header, uint usage);

    [LibraryImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool DeleteObject(nint handle);

    [LibraryImport("user32.dll")]
    private static partial nint GetDC(nint window);

    [LibraryImport("user32.dll")]
    private static partial int ReleaseDC(nint window, nint dc);

    /// <summary>SIZE de Win32.</summary>
    [StructLayout(LayoutKind.Sequential)]
    internal readonly struct NativeSize(int width, int height)
    {
        public readonly int Width = width;
        public readonly int Height = height;
    }

    /// <summary>BITMAP de Win32.</summary>
    [StructLayout(LayoutKind.Sequential)]
    private struct NativeBitmap
    {
        public int Type;
        public int Width;
        public int Height;
        public int WidthBytes;
        public ushort Planes;
        public ushort BitsPixel;
        public nint Bits;
    }

    /// <summary>BITMAPINFOHEADER de Win32 (32 bits, sin tabla de colores).</summary>
    [StructLayout(LayoutKind.Sequential)]
    private struct BitmapInfoHeader
    {
        public uint Size;
        public int Width;
        public int Height;
        public ushort Planes;
        public ushort BitCount;
        public uint Compression;
        public uint SizeImage;
        public int XPelsPerMeter;
        public int YPelsPerMeter;
        public uint ClrUsed;
        public uint ClrImportant;
    }
}
