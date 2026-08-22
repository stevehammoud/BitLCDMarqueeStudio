param(
    [string]$Configuration = 'Release'
)

$ErrorActionPreference = 'Stop'

$Root = Split-Path -Parent $PSScriptRoot
$OutDir = Join-Path $Root 'dist'
$Out = Join-Path $OutDir 'BitLCDMarqueeStudio.exe'

New-Item -ItemType Directory -Path $OutDir -Force | Out-Null

$sources = @(
    (Join-Path $Root 'src\Program.cs'),
    (Join-Path $Root 'src\Models.cs'),
    (Join-Path $Root 'src\CanvasPreviewControl.cs'),
    (Join-Path $Root 'src\MainForm.cs')
)

& "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe" `
    /nologo `
    /target:winexe `
    /platform:anycpu `
    "/out:$Out" `
    /reference:System.dll `
    /reference:System.Core.dll `
    /reference:System.Drawing.dll `
    /reference:System.Windows.Forms.dll `
    $sources

if ($LASTEXITCODE -ne 0) {
    throw "C# build failed with exit code $LASTEXITCODE"
}

Write-Host "Built $Out"
