# Bloch-gömb szimulátor – helyi webszerver (Windows, telepítés nélkül)
#
# Miért kell webszerver? A Blazor WebAssembly a fájlokat fetch()-csel tölti be,
# amit a böngészők file:// módban biztonsági okból tiltanak. Ez a szkript egy
# parányi HTTP szervert indít a beépített .NET/PowerShell eszközökkel, majd
# megnyitja a böngészőt. Semmit nem kell telepíteni.
#
# Használat: kattints duplán az Inditas.bat fájlra.

param([int]$Port = 8765, [switch]$NoBrowser)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$app = $root

if (-not (Test-Path (Join-Path $app 'index.html'))) {
    Write-Host ''
    Write-Host 'HIBA: az index.html nem talalhato.' -ForegroundColor Red
    Write-Host 'Az Inditas.bat mellett ott kell lennie az index.html fajlnak (a szimulator fajljai).'
    Write-Host ''
    Read-Host 'Nyomj Entert a kilepeshez'
    exit 1
}

Add-Type -Path (Join-Path $root 'MiniServer.cs') 

$server = $null
$currentPort = $Port
for ($i = 0; $i -lt 30; $i++) {
    try {
        $server = New-Object MiniHttpServer -ArgumentList $app, $currentPort
        $server.Start()
        break
    }
    catch {
        $server = $null
        $currentPort++
    }
}

if ($null -eq $server) {
    Write-Host 'HIBA: nem sikerult szabad portot talalni.' -ForegroundColor Red
    Read-Host 'Nyomj Entert a kilepeshez'
    exit 1
}

$url = "http://localhost:$($server.Port)/"

Write-Host ''
Write-Host '======================================================' -ForegroundColor Cyan
Write-Host '  Bloch-gomb szimulator - helyi szerver elindult' -ForegroundColor Cyan
Write-Host '======================================================' -ForegroundColor Cyan
Write-Host ''
Write-Host "  Cim:  $url"
Write-Host ''
Write-Host '  A bongeszoben megnyilt az alkalmazas.'
Write-Host '  A szerver addig fut, amig ezt az ablakot be nem zarod.'
Write-Host ''
Write-Host '  Leallitas: ennek az ablaknak a bezarasa (vagy Ctrl+C).'
Write-Host ''

if (-not $NoBrowser) {
    Start-Process $url
}
try {
    while ($true) { Start-Sleep -Seconds 3600 }
}
finally {
    $server.Stop()
}


