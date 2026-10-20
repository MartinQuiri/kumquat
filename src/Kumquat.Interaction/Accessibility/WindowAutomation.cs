// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Kumquat contributors

using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;
using Kumquat.Interaction.Shell;

namespace Kumquat.Interaction.Accessibility;

/// <summary>
/// Presenta una ventana propia (sin controles de Windows) a UI Automation: le da nombre y tipo
/// para Narrador, NVDA o JAWS y le permite anunciar textos con <c>UiaRaiseNotificationEvent</c>.
/// La rueda nunca toma el foco, así que sin esto un lector de pantalla no sabe qué pasa en ella.
/// </summary>
[GeneratedComClass]
internal sealed partial class WindowAutomation : IRawElementProviderSimple, IDisposable
{
    private const int UiaRootObjectId = -25;
    private const int ProviderOptionsServerSide = 0x2;
    private const int ProviderOptionsUseComThreading = 0x20;
    private const int NamePropertyId = 30005;
    private const int ControlTypePropertyId = 30003;
    private const int IsKeyboardFocusablePropertyId = 30009;
    private const int MenuControlTypeId = 50009;
    private const int NotificationKindOther = 4;
    private const int NotificationProcessingMostRecent = 3;

    private static readonly StrategyBasedComWrappers ComWrappers = new();

    private readonly nint _hwnd;
    private readonly string _name;
    private nint _provider;

    /// <param name="hwnd">Ventana a la que representa.</param>
    /// <param name="name">Nombre que dice el lector ("Rueda de Kumquat").</param>
    public WindowAutomation(nint hwnd, string name)
    {
        _hwnd = hwnd;
        _name = name;
    }

    /// <summary>Atiende <c>WM_GETOBJECT</c>; <c>null</c> si el pedido no es para UI Automation.</summary>
    public nint? OnGetObject(nint wParam, nint lParam)
        => (int)lParam == UiaRootObjectId ? NativeMethods.UiaReturnRawElementProvider(_hwnd, wParam, lParam, Provider()) : null;

    /// <summary>Dice un texto (si hay un lector de pantalla escuchando); el último reemplaza a los pendientes.</summary>
    public void Announce(string text)
    {
        if (string.IsNullOrEmpty(text) || !NativeMethods.UiaClientsAreListening())
        {
            return;
        }

        var display = Marshal.StringToBSTR(text);
        var activity = Marshal.StringToBSTR("Kumquat.Wheel");
        try
        {
            _ = NativeMethods.UiaRaiseNotificationEvent(Provider(), NotificationKindOther, NotificationProcessingMostRecent, display, activity);
        }
        catch (EntryPointNotFoundException)
        {
            // Windows 10 anterior a 1709: sin anuncios, el resto funciona igual.
        }
        finally
        {
            Marshal.FreeBSTR(display);
            Marshal.FreeBSTR(activity);
        }
    }

    public void Dispose()
    {
        if (_provider != 0)
        {
            _ = NativeMethods.UiaDisconnectProvider(_provider);
            Marshal.Release(_provider);
            _provider = 0;
        }
    }

    int IRawElementProviderSimple.GetProviderOptions(out int options)
    {
        options = ProviderOptionsServerSide | ProviderOptionsUseComThreading;
        return 0;
    }

    int IRawElementProviderSimple.GetPatternProvider(int patternId, out nint provider)
    {
        provider = 0;
        return 0;
    }

    int IRawElementProviderSimple.GetPropertyValue(int propertyId, out Variant value)
    {
        value = propertyId switch
        {
            NamePropertyId => Variant.String(_name),
            ControlTypePropertyId => Variant.Int32(MenuControlTypeId),
            IsKeyboardFocusablePropertyId => Variant.Boolean(false),
            _ => default,
        };
        return 0;
    }

    int IRawElementProviderSimple.GetHostRawElementProvider(out nint provider) => NativeMethods.UiaHostProviderFromHwnd(_hwnd, out provider);

    /// <summary>Puntero COM a este objeto (se crea una vez y se suelta en <see cref="Dispose"/>).</summary>
    private nint Provider()
    {
        if (_provider == 0)
        {
            var unknown = ComWrappers.GetOrCreateComInterfaceForObject(this, CreateComInterfaceFlags.None);
            try
            {
                var iid = typeof(IRawElementProviderSimple).GUID;
                Marshal.ThrowExceptionForHR(Marshal.QueryInterface(unknown, in iid, out _provider));
            }
            finally
            {
                Marshal.Release(unknown);
            }
        }

        return _provider;
    }

    private static partial class NativeMethods
    {
        [LibraryImport("uiautomationcore.dll")]
        public static partial nint UiaReturnRawElementProvider(nint hwnd, nint wParam, nint lParam, nint provider);

        [LibraryImport("uiautomationcore.dll")]
        public static partial int UiaHostProviderFromHwnd(nint hwnd, out nint provider);

        [LibraryImport("uiautomationcore.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static partial bool UiaClientsAreListening();

        [LibraryImport("uiautomationcore.dll")]
        public static partial int UiaRaiseNotificationEvent(nint provider, int kind, int processing, nint displayString, nint activityId);

        [LibraryImport("uiautomationcore.dll")]
        public static partial int UiaDisconnectProvider(nint provider);
    }
}
