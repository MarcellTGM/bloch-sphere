# Bloch-gömb szimulátor – publikáló szkript
#
# Használat (a Bloch-gomb-2026 mappában):
#   .\tools\publish.ps1
#   .\tools\publish.ps1 -OutDir docs -BaseHref "/bloch-gomb/"
#
# A kimenet (alapértelmezés: docs\) tartalmazza a futtatható szimulátort,
# valamint az Inditas.bat / szerver.ps1 / MiniServer.cs indítót.
# Ez a mappa közvetlenül feltölthető GitHub Pages-re is.

param(
    [string]$OutDir = "docs",
    [string]$BaseHref = "./"
)

$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
$project = Join-Path $root 'BlochGomb\BlochSimulator.csproj'
$out = Join-Path $root $OutDir
$tools = Join-Path $root 'tools'

$dotnetCandidates = @()
$dotnetCommand = Get-Command dotnet -ErrorAction SilentlyContinue
if ($null -ne $dotnetCommand) { $dotnetCandidates += $dotnetCommand.Source }
$dotnetCandidates += (Join-Path $env:USERPROFILE '.dotnet8\dotnet.exe')
$dotnetCandidates += (Join-Path $env:USERPROFILE '.dotnet\dotnet.exe')

function Test-DotnetSdk([string]$exe) {
    if (-not (Test-Path $exe)) { return $false }
    try {
        $sdks = & $exe --list-sdks 2>$null
        return ($LASTEXITCODE -eq 0 -and $sdks)
    }
    catch { return $false }
}

$dotnetExe = $null
foreach ($candidate in $dotnetCandidates) {
    if (Test-DotnetSdk $candidate) { $dotnetExe = $candidate; break }
}

if ($null -eq $dotnetExe) {
    throw 'Nem talalhato .NET SDK. Telepitsd a .NET 8 SDK-t (https://dot.net), majd futtasd ujra.'
}

Write-Host "dotnet: $dotnetExe" -ForegroundColor DarkGray
Write-Host "publish -> $out" -ForegroundColor Cyan

if (Test-Path $out) { Remove-Item $out -Recurse -Force }

& $dotnetExe publish $project -c Release -o $out --nologo
if ($LASTEXITCODE -ne 0) { throw 'A publish sikertelen.' }

# A Blazor WebAssembly publish a statikus fajlokat a wwwroot almappaba teszi;
# ezeket felhozzuk a kimeneti mappa gyokerebe, hogy a docs\ kozvetlenul
# szolgalhassa ki (es GitHub Pages-re is így toltheto fel).
$wwwroot = Join-Path $out 'wwwroot'
if (Test-Path $wwwroot) {
    Get-ChildItem $wwwroot -Force | Move-Item -Destination $out -Force
    Remove-Item $wwwroot -Recurse -Force
}
Remove-Item (Join-Path $out 'web.config') -ErrorAction SilentlyContinue

# A base href beallitasa (a "./" minden alkoenyvtarban mukodik: helyi szerver,
# alkoenyvtar, GitHub Pages projekt- es felhasznaloi oldal).
$indexPath = Join-Path $out 'index.html'
$html = Get-Content $indexPath -Raw
$html = [regex]::Replace($html, '<base href="[^"]*"\s*/?>', ('<base href="' + $BaseHref + '" />'))
Set-Content -Path $indexPath -Value $html -Encoding UTF8 -NoNewline

# Indito fajlok a publikalt mappa melle
Copy-Item (Join-Path $tools 'Inditas.bat') $out -Force
Copy-Item (Join-Path $tools 'szerver.ps1') $out -Force
Copy-Item (Join-Path $tools 'MiniServer.cs') $out -Force
Copy-Item (Join-Path $tools 'OLVASSEL.txt') $out -Force
New-Item -ItemType File -Path (Join-Path $out '.nojekyll') -Force | Out-Null

$size = [math]::Round(((Get-ChildItem $out -Recurse -File | Measure-Object Length -Sum).Sum / 1MB), 1)
Write-Host "Kesz. Meret: $size MB · base href: $BaseHref" -ForegroundColor Green
