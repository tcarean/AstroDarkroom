$Version = '1.1.0'   # Versiunea scriptului - se incrementeaza la fiecare modificare (major.minor.patch)

# SPDX-License-Identifier: GPL-3.0-or-later
#
# Copyright (C) 2026 Tudor Carean
#
# This program is free software: you can redistribute it and/or modify
# it under the terms of the GNU General Public License as published by
# the Free Software Foundation, either version 3 of the License, or
# (at your option) any later version.
#
# This program is distributed in the hope that it will be useful,
# but WITHOUT ANY WARRANTY; without even the implied warranty of
# MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
# GNU General Public License for more details.
#
# You should have received a copy of the GNU General Public License
# along with this program.  If not, see <https://www.gnu.org/licenses/>.

<#
    SirilCleanup.ps1

    Muta (NU sterge) fisierele ne-necesare rezultate din procesarea cu Siril
    intr-un folder "safe-to-delete", pastrand exact structura de foldere
    originala, ca sa poata fi mutate inapoi daca a fost o greseala.

    Se considera ne-necesare:
      1. tot ce este intr-un folder numit  process/
      2. tot ce este intr-un folder numit  masters/
      3. fisierele result.fit  - doar daca utilizatorul raspunde Y la intrebarea de la inceput

    Cautarea porneste din folderul in care se afla scriptul si este recursiva
    (pot fi multe sesiuni Siril in subfoldere). Folderul safe-to-delete este
    ignorat la cautare, deci scriptul poate fi rulat de mai multe ori.

    Se ruleaza cu dublu click pe SirilCleanup.bat (nu are parametri de consola).
#>

# ---------------------------------------------------------------------------
# Configurare
# ---------------------------------------------------------------------------

# Folderul de unde incepe cautarea = folderul in care se afla scriptul.
$Root = $PSScriptRoot

# Folderul in care se muta fisierele (creat sub $Root).
$SafeFolderName = 'safe-to-delete'
$SafeRoot       = Join-Path $Root $SafeFolderName

# Numele folderelor al caror continut este considerat ne-necesar (case-insensitive).
$JunkFolderNames = @('process', 'masters')

# Numele fisierului rezultat (case-insensitive), mutat doar la cerere.
$ResultFileName = 'result.fit'

# ---------------------------------------------------------------------------
# Intrebare: se face cleanup si la result.fit?
# ---------------------------------------------------------------------------

Write-Host "Siril cleanup v$Version"
Write-Host "Copyright (C) 2026 Tudor Carean"
Write-Host "Software liber, licenta GPLv3 sau ulterioara. FARA NICIO GARANTIE - vezi LICENSE."
Write-Host ""
Write-Host "Folder de pornire : $Root"
Write-Host "Destinatie        : $SafeRoot"
Write-Host ""

$CleanResult = $null
while ($null -eq $CleanResult) {
    $answer = (Read-Host "Sa fac cleanup si la result.fit? [y/N]").Trim()

    if ($answer -eq '')       { $CleanResult = $false }   # Enter = valoarea implicita N
    elseif ($answer -match '^[yY]$') { $CleanResult = $true  }
    elseif ($answer -match '^[nN]$') { $CleanResult = $false }
    else { Write-Host "Raspunde cu Y sau N (sau Enter pentru N)." }
}

Write-Host ""
if ($CleanResult) { Write-Host "-> result.fit VA FI mutat." }
else              { Write-Host "-> result.fit NU va fi mutat." }
Write-Host ""

# ---------------------------------------------------------------------------
# Functii ajutatoare
#   Wait-ForKey  - asteapta o tasta inainte de inchidere (ca sa se vada ce s-a mutat)
#   Test-IsJunk  - decide daca un fisier este ne-necesar
#   $RelativePath = calea fisierului relativ la $Root, de ex. "M31\process\pp_light_00001.fit"
# ---------------------------------------------------------------------------

function Wait-ForKey {
    Write-Host "Apasa orice tasta pentru a inchide..."
    if ([Console]::IsInputRedirected) { [void](Read-Host) }      # rulare din alt program / teste
    else                              { [void][Console]::ReadKey($true) }
}

function Test-IsJunk([string] $RelativePath) {
    # Separam folderele de numele fisierului.
    $folders  = Split-Path $RelativePath -Parent      # "M31\process"
    $fileName = Split-Path $RelativePath -Leaf        # "pp_light_00001.fit"

    # Regula 1 + 2: fisierul se afla (oriunde) sub un folder process/ sau masters/.
    # Verificam fiecare segment al caii, nu doar parintele direct, ca sa prindem
    # si subfolderele din process/ (de ex. process\seq\...).
    foreach ($segment in $folders.Split([char[]]@('\', '/'))) {
        if ($JunkFolderNames -contains $segment.ToLowerInvariant()) { return $true }
    }

    # Regula 3: result.fit, doar daca utilizatorul a cerut.
    if ($CleanResult -and $fileName -ieq $ResultFileName) { return $true }

    return $false
}

# ---------------------------------------------------------------------------
# Cautare recursiva a fisierelor ne-necesare
# ---------------------------------------------------------------------------

$RootPrefix = $Root.TrimEnd('\') + '\'
$SafePrefix = $SafeRoot.TrimEnd('\') + '\'

$FilesToMove = Get-ChildItem -LiteralPath $Root -Recurse -File -Force |
    Where-Object {
        # Ignoram tot ce este deja in safe-to-delete si scriptul insusi.
        -not $_.FullName.StartsWith($SafePrefix, [System.StringComparison]::OrdinalIgnoreCase) -and
        $_.FullName -ne $PSCommandPath
    } |
    Where-Object {
        $relative = $_.FullName.Substring($RootPrefix.Length)
        Test-IsJunk $relative
    } |
    Sort-Object FullName

if ($FilesToMove.Count -eq 0) {
    Write-Host "Nu am gasit nimic de mutat."
    Write-Host ""
    Wait-ForKey
    exit
}

# ---------------------------------------------------------------------------
# Mutare, pastrand structura de foldere sub safe-to-delete
# ---------------------------------------------------------------------------

$Moved   = 0
$Skipped = 0
$Bytes   = 0

foreach ($file in $FilesToMove) {
    $relative    = $file.FullName.Substring($RootPrefix.Length)   # "M31\process\x.fit"
    $destination = Join-Path $SafeRoot $relative                  # "...\safe-to-delete\M31\process\x.fit"
    $destFolder  = Split-Path $destination -Parent

    # Nu suprascriem niciodata: daca exista deja un fisier cu acelasi nume
    # in destinatie (rulare anterioara), il lasam pe loc si raportam.
    if (Test-Path -LiteralPath $destination) {
        Write-Host "SARIT  (exista deja in destinatie): $relative"
        $Skipped++
        continue
    }

    try {
        if (-not (Test-Path -LiteralPath $destFolder)) {
            New-Item -ItemType Directory -Path $destFolder -Force | Out-Null
        }
        Move-Item -LiteralPath $file.FullName -Destination $destination -ErrorAction Stop

        Write-Host "MUTAT  $relative"
        $Moved++
        $Bytes += $file.Length
    }
    catch {
        Write-Host "EROARE $relative  ->  $($_.Exception.Message)"
        $Skipped++
    }
}

# ---------------------------------------------------------------------------
# Sumar
# ---------------------------------------------------------------------------

$MB = [math]::Round($Bytes / 1MB, 1)

Write-Host ""
Write-Host "Gata."
Write-Host "  Fisiere mutate : $Moved  ($MB MB)"
Write-Host "  Sarite / erori : $Skipped"
Write-Host "  Destinatie     : $SafeRoot"
Write-Host ""
Write-Host "Nimic nu a fost sters. Folderele process/ si masters/ originale raman pe loc (goale)."
Write-Host "Ca sa anulezi, muta continutul din safe-to-delete inapoi peste structura originala."
Write-Host ""
Wait-ForKey
