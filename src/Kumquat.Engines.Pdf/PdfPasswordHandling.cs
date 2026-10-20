// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Kumquat contributors

namespace Kumquat.Engines.Pdf;

/// <summary>Qué pasa con la contraseña de un PDF protegido al aplicarle una herramienta.</summary>
internal enum PdfPasswordHandling
{
    /// <summary>Cada PDF resultante queda protegido con la misma contraseña (comprimir, rotar…).</summary>
    Keep,

    /// <summary>El resultado no puede llevar contraseña (páginas a JPG).</summary>
    Drop,

    /// <summary>La herramienta pone su propia contraseña (proteger: cambiarla).</summary>
    Replace,
}
