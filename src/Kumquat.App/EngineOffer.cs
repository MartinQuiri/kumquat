// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Kumquat contributors

using Kumquat.Core.Formats;

namespace Kumquat.App;

/// <summary>Programa gratuito que Kumquat usa si está instalado y que puede ofrecer instalar con winget.</summary>
/// <param name="ActionId">Id de la acción "Instalar …" en la rueda.</param>
/// <param name="Name">Nombre visible.</param>
/// <param name="WingetId">Id exacto del paquete en el repositorio de winget.</param>
/// <param name="Purpose">Para qué sirve, completa la frase "Kumquat lo usa para …".</param>
/// <param name="Publisher">Quién lo publica (de dónde sale el instalador).</param>
/// <param name="License">Licencia del programa (se acepta al instalar).</param>
/// <param name="Formats">Formatos que hace convertibles.</param>
internal sealed record EngineOffer(string ActionId, string Name, string WingetId, string Purpose, string Publisher, string License, IReadOnlyCollection<FileFormat> Formats);
