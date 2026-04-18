[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$SampleEi,
    [Parameter(Mandatory = $true)][string]$RealEi,
    [Parameter(Mandatory = $true)][string]$EiJsonDir,
    [Parameter(Mandatory = $true)][string]$SampleComb,
    [Parameter(Mandatory = $true)][string]$RealComb
)

$ErrorActionPreference = 'Stop'

try {
    $utf8NoBom = New-Object System.Text.UTF8Encoding($false)

    $ei = Get-Content -Raw -LiteralPath $SampleEi
    $ei = $ei.Replace('__OUTLOCATION__', $EiJsonDir)
    [System.IO.File]::WriteAllText($RealEi, $ei, $utf8NoBom)

    $comb = Get-Content -Raw -LiteralPath $SampleComb
    $comb = $comb.Replace('__INPUT_JSON_DIR__', $EiJsonDir)
    [System.IO.File]::WriteAllText($RealComb, $comb, $utf8NoBom)
}
catch {
    Write-Error $_
    exit 1
}
