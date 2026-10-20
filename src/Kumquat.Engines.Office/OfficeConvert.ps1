# SPDX-License-Identifier: GPL-3.0-or-later
# Copyright (C) 2026 Kumquat contributors
#
# Convierte con Microsoft Office por automatización COM. Lo ejecuta Kumquat (no es para usar a mano).
# Seguridad: macros desactivadas antes de abrir, solo lectura, sin diálogos ni recientes; una
# contraseña falsa hace que un documento protegido falle en vez de pedirla.
# Salida (una línea por evento): "pid:<n>" si se abrió una instancia nueva de Office (Kumquat la
# mata si se cuelga), "ok" al terminar, o "error:<mensaje>".
param(
    [Parameter(Mandatory = $true)][ValidateSet('Word', 'Excel', 'PowerPoint')][string]$App,
    [Parameter(Mandatory = $true)][string]$InputPath,
    [Parameter(Mandatory = $true)][string]$OutputPath,
    [Parameter(Mandatory = $true)][ValidateSet('pdf', 'docx')][string]$Format
)

$ErrorActionPreference = 'Stop'
[Console]::OutputEncoding = [Text.Encoding]::UTF8
$missing = [Type]::Missing
$noPassword = 'kq-sin-clave' # Excel admite como mucho 15 caracteres

# Word avisa con un diálogo al abrir un PDF ("convertirá el PDF en un documento editable") aunque
# las alertas estén apagadas. Se apaga ese aviso solo durante la conversión y luego se deja como estaba.
function Set-PdfWarning([string]$Version, $Value) {
    $key = "HKCU:\Software\Microsoft\Office\$Version\Word\Options"
    if ($null -eq $Value) { Remove-ItemProperty -Path $key -Name DisableConvertPdfWarning -ErrorAction SilentlyContinue }
    else {
        if (-not (Test-Path $key)) { New-Item -Path $key -Force | Out-Null }
        Set-ItemProperty -Path $key -Name DisableConvertPdfWarning -Value $Value -Type DWord
    }
}

function Get-PdfWarning([string]$Version) {
    (Get-ItemProperty -Path "HKCU:\Software\Microsoft\Office\$Version\Word\Options" -Name DisableConvertPdfWarning -ErrorAction SilentlyContinue).DisableConvertPdfWarning
}

$processName = @{ Word = 'WINWORD'; Excel = 'EXCEL'; PowerPoint = 'POWERPNT' }[$App]
$before = @(Get-Process $processName -ErrorAction SilentlyContinue | ForEach-Object { $_.Id })
$office = New-Object -ComObject "$App.Application"
$created = @(Get-Process $processName -ErrorAction SilentlyContinue | Where-Object { $before -notcontains $_.Id } | ForEach-Object { $_.Id })
$ours = $created.Count -ge 1
if ($created.Count -eq 1) { [Console]::Out.WriteLine("pid:$($created[0])") }

try {
    $office.AutomationSecurity = 3 # msoAutomationSecurityForceDisable: nunca ejecutar macros
    switch ($App) {
        'Word' {
            $office.Visible = $false
            $office.DisplayAlerts = 0 # wdAlertsNone
            # FileName, ConfirmConversions, ReadOnly, AddToRecentFiles, PasswordDocument, PasswordTemplate,
            # Revert, WritePasswordDocument, WritePasswordTemplate, Format, Encoding, Visible, OpenAndRepair,
            # DocumentDirection, NoEncodingDialog
            $fromPdf = [IO.Path]::GetExtension($InputPath) -eq '.pdf'
            if ($fromPdf) {
                $previousWarning = Get-PdfWarning $office.Version
                Set-PdfWarning $office.Version 1
            }
            try {
                $doc = $office.Documents.Open($InputPath, $false, $true, $false, $noPassword, $noPassword, $false, $noPassword, $noPassword, 0, $missing, $false, $false, 0, $true)
            }
            finally {
                if ($fromPdf) { Set-PdfWarning $office.Version $previousWarning }
            }
            try {
                if ($Format -eq 'pdf') { $doc.ExportAsFixedFormat($OutputPath, 17) } # wdExportFormatPDF
                else { $doc.SaveAs2($OutputPath, 16) } # wdFormatDocumentDefault (.docx)
            }
            finally { $doc.Close(0) } # wdDoNotSaveChanges
        }
        'Excel' {
            $office.Visible = $false
            $office.DisplayAlerts = $false
            $office.EnableEvents = $false
            # FileName, UpdateLinks, ReadOnly, Format, Password, WriteResPassword, IgnoreReadOnlyRecommended
            $book = $office.Workbooks.Open($InputPath, 0, $true, $missing, $noPassword, $noPassword, $true)
            try { $book.ExportAsFixedFormat(0, $OutputPath) } # xlTypePDF: todo el libro
            finally { $book.Close($false) }
        }
        'PowerPoint' {
            $office.DisplayAlerts = 1 # ppAlertsNone
            # FileName, ReadOnly (msoTrue), Untitled (msoFalse), WithWindow (msoFalse)
            $deck = $office.Presentations.Open($InputPath, -1, 0, 0)
            try { $deck.SaveAs($OutputPath, 32) } # ppSaveAsPDF
            finally { $deck.Close() }
        }
    }

    [Console]::Out.WriteLine('ok')
}
catch {
    [Console]::Out.WriteLine('error:' + ($_.Exception.Message -replace '[\r\n]+', ' '))
    exit 1
}
finally {
    # Si Office ya estaba abierto (PowerPoint reutiliza la instancia), no se cierra: es de la persona.
    if ($ours) { $office.Quit() }
    [void][Runtime.InteropServices.Marshal]::FinalReleaseComObject($office)
}
