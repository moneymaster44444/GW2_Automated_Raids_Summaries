[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'

try {
    [Console]::Out.Write((Get-Date).ToString('MM-dd-yy'))
}
catch {
    Write-Error $_
    exit 1
}
