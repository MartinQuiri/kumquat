// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Kumquat contributors

using System.Text.Json.Serialization;

namespace Kumquat.Engines.Pdf;

/// <summary>Serialización generada en compilación para los metadatos de PDF.</summary>
[JsonSerializable(typeof(Dictionary<string, string>))]
internal sealed partial class PdfJson : JsonSerializerContext;
