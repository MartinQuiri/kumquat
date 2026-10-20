// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Kumquat contributors

namespace Kumquat.Dialogs.Install;

/// <summary>Qué se va a instalar, para mostrarlo antes de pedir permiso.</summary>
/// <param name="Name">Nombre del programa.</param>
/// <param name="Purpose">Completa "Kumquat lo usa para …".</param>
/// <param name="Publisher">Quién lo publica.</param>
/// <param name="License">Licencia que se acepta al instalar.</param>
public sealed record InstallRequest(string Name, string Purpose, string Publisher, string License);
