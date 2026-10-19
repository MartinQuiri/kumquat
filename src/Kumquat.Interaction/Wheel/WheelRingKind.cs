// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Kumquat contributors

namespace Kumquat.Interaction.Wheel;

/// <summary>Anillo de la rueda, de adentro hacia afuera.</summary>
public enum WheelRingKind
{
    /// <summary>Convertir a otro formato (anillo interior: lo más usado, lo más cerca).</summary>
    Convert,

    /// <summary>Herramientas: comprimir, rotar, quitar audio... (anillo medio).</summary>
    Tools,

    /// <summary>Acciones sobre el archivo: copiar ruta, terminal, mostrar en carpeta (anillo exterior).</summary>
    Actions,
}
