// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Kumquat contributors

using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;
using Kumquat.Interaction.Shell;

namespace Kumquat.Interaction.Accessibility;

/// <summary>Elemento de UI Automation del lado del servidor (uiautomationcore.h), lo mínimo.</summary>
[GeneratedComInterface]
[Guid("D6DD68D1-86FD-4332-8666-9ABEDEA2D24C")]
internal partial interface IRawElementProviderSimple
{
    [PreserveSig]
    int GetProviderOptions(out int options);

    [PreserveSig]
    int GetPatternProvider(int patternId, out nint provider);

    [PreserveSig]
    int GetPropertyValue(int propertyId, out Variant value);

    [PreserveSig]
    int GetHostRawElementProvider(out nint provider);
}
