// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Kumquat contributors

namespace Kumquat.Core.Conversion;

/// <summary>Crea carpetas temporales de trabajo.</summary>
public interface ITempWorkspaceFactory
{
    /// <summary>Crea una carpeta temporal nueva.</summary>
    ITempWorkspace Create();
}
