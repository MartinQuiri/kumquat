// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Kumquat contributors

using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;

namespace Kumquat.Interaction.Native;

/// <summary>Selector de carpetas nativo de Windows (IFileOpenDialog con FOS_PICKFOLDERS).</summary>
public static unsafe partial class FolderPicker
{
    private const uint ClsctxInprocServer = 1;
    private const uint FosPickFolders = 0x20;
    private const uint FosForceFileSystem = 0x40;
    private const uint FosNoChangeDir = 0x8;
    private const uint SigdnFileSysPath = 0x80058000;
    private const int ErrorCancelledHResult = unchecked((int)0x800704C7);

    private static readonly Guid FileOpenDialogClsid = new("DC1C5A9C-E88A-4dde-A5A1-60F82A20AEF7");
    private static readonly Guid FileDialogIid = new("42f85136-db7e-439c-85f1-e4075d135fc8");

    /// <summary>
    /// Muestra el diálogo (bloquea el hilo de UI pero bombea mensajes). Debe llamarse en el hilo STA.
    /// </summary>
    /// <param name="title">Título del diálogo.</param>
    /// <param name="okLabel">Texto del botón de aceptar.</param>
    /// <returns>Carpeta elegida, o <c>null</c> si el usuario canceló.</returns>
    public static string? PickFolder(string title, string okLabel)
    {
        var hr = CoCreateInstance(FileOpenDialogClsid, 0, ClsctxInprocServer, FileDialogIid, out var pointer);
        Marshal.ThrowExceptionForHR(hr);
        var dialog = ComInterfaceMarshaller<IFileDialog>.ConvertToManaged((void*)pointer)!;
        ComInterfaceMarshaller<IFileDialog>.Free((void*)pointer);

        dialog.GetOptions(out var options);
        dialog.SetOptions(options | FosPickFolders | FosForceFileSystem | FosNoChangeDir);
        dialog.SetTitle(title);
        dialog.SetOkButtonLabel(okLabel);

        using var owner = new ForegroundOwner();
        hr = dialog.Show(owner.Handle);
        if (hr == ErrorCancelledHResult)
        {
            return null;
        }

        Marshal.ThrowExceptionForHR(hr);
        dialog.GetResult(out var item);
        item.GetDisplayName(SigdnFileSysPath, out var name);
        try
        {
            return Marshal.PtrToStringUni(name);
        }
        finally
        {
            Marshal.FreeCoTaskMem(name);
        }
    }

    [LibraryImport("ole32.dll")]
    private static partial int CoCreateInstance(in Guid clsid, nint outer, uint context, in Guid iid, out nint instance);

    [LibraryImport("user32.dll")]
    private static partial nint GetForegroundWindow();

    [LibraryImport("user32.dll")]
    private static partial uint GetWindowThreadProcessId(nint hwnd, nint processId);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool AttachThreadInput(uint attach, uint attachTo, [MarshalAs(UnmanagedType.Bool)] bool doAttach);

    /// <summary>
    /// Ventana dueña invisible traída al frente. Tras un drop, el que está en primer plano es
    /// Explorer: sin esto el diálogo se abriría detrás, parpadeando en la barra de tareas.
    /// </summary>
    private sealed class ForegroundOwner : NativeWindow
    {
        public ForegroundOwner()
        {
            Create("KumquatDialogOwner", Win32.WS_POPUP, Win32.WS_EX_TOOLWINDOW);
            Win32.GetCursorPos(out var cursor);
            Win32.SetWindowPos(Handle, 0, cursor.X, cursor.Y, 0, 0, Win32.SWP_NOACTIVATE);

            var current = Win32.GetCurrentThreadId();
            var foreground = GetWindowThreadProcessId(GetForegroundWindow(), 0);
            var attached = foreground != current && AttachThreadInput(current, foreground, doAttach: true);
            Win32.ShowWindow(Handle, 5 /* SW_SHOW */);
            Win32.SetForegroundWindow(Handle);
            if (attached)
            {
                AttachThreadInput(current, foreground, doAttach: false);
            }
        }
    }
}
