# Renames the Starter placeholder to your project name, in file names, directory names and
# file contents. Run once, right after copying the kit, before the first commit.
#   powershell -NoProfile -ExecutionPolicy Bypass -File scripts/rename.ps1 -Name MyTool
[CmdletBinding()]
param([Parameter(Mandatory = $true)][string]$Name)

$ErrorActionPreference = 'Stop'
if ($Name -notmatch '^[A-Z][A-Za-z0-9]*$') { throw 'Name must be PascalCase letters and digits, e.g. FlatCheck.' }
$root = Split-Path -Parent $PSScriptRoot
$lower = $Name.ToLowerInvariant()
$upper = $Name.ToUpperInvariant()

$skip = '\\(bin|obj|\.git|\.vs)(\\|$)'
$files = Get-ChildItem $root -Recurse -File | Where-Object { $_.FullName -notmatch $skip -and $_.Name -ne 'rename.ps1' }

foreach ($f in $files) {
    $text = [System.IO.File]::ReadAllText($f.FullName)
    $new = $text -creplace 'Starter', $Name -creplace 'starter', $lower -creplace 'STARTER', $upper
    if ($new -ne $text) {
        [System.IO.File]::WriteAllText($f.FullName, $new, (New-Object System.Text.UTF8Encoding $false))
        Write-Host ("content  {0}" -f $f.FullName.Substring($root.Length + 1))
    }
}

# Deepest paths first so a renamed directory does not invalidate a child's path.
$items = Get-ChildItem $root -Recurse | Where-Object { $_.FullName -notmatch $skip -and $_.Name -cmatch 'Starter' } |
    Sort-Object { $_.FullName.Length } -Descending
foreach ($i in $items) {
    $newName = $i.Name -creplace 'Starter', $Name
    Rename-Item $i.FullName $newName
    Write-Host ("renamed  {0} -> {1}" -f $i.Name, $newName)
}

# The new name changes the alphabetical order of `using` lines; let the formatter re-sort them
# so the first verify run is green.
Write-Host 'formatting (using order depends on the project name)'
Push-Location $root
try {
    dotnet format (Join-Path $root ("{0}.sln" -f $Name)) -v q
    if ($LASTEXITCODE -ne 0) { throw 'dotnet format failed after the rename; run scripts/verify.ps1 to see why.' }
}
finally {
    Pop-Location
}

Write-Host ("Done. Review CLAUDE.md and README.md, then: git add -A; git commit -m 'Start {0} from ClaudeCodeStarterKit'" -f $Name)
