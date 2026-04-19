[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$WebhookUrl,
    [Parameter(Mandatory = $true)][string]$FilePath,
    [string]$ZipFallbackDir
)

$ErrorActionPreference = 'Stop'

function Write-Result {
    param([string]$Result, [string]$Name)
    Write-Output ("RESULT=" + $Result)
    if ($Name) { Write-Output ("NAME=" + $Name) }
}

function Get-SummaryMessage {
    param([string]$Path)
    $name = [System.IO.Path]::GetFileNameWithoutExtension($Path)
    if ($name -match '^INC_(\d{2})-(\d{2})-(\d{2})$') {
        $mm = $Matches[1]; $dd = $Matches[2]; $yy = $Matches[3]
        $year = 2000 + [int]$yy
        $d = Get-Date -Year $year -Month ([int]$mm) -Day ([int]$dd)
        return ("Summary for {0} {1}/{2} raid" -f $d.ToString('dddd'), $mm, $dd)
    }
    $d = Get-Date
    return ("Summary for {0} {1} raid" -f $d.ToString('dddd'), $d.ToString('MM/dd'))
}

function Send-Attachment {
    param([string]$Url, [string]$Path, [string]$Message)

    Add-Type -AssemblyName System.Net.Http

    $client = [System.Net.Http.HttpClient]::new()
    $content = [System.Net.Http.MultipartFormDataContent]::new()
    $fs = $null

    try {
        $payload = @{ content = $Message } | ConvertTo-Json -Compress
        $stringContent = [System.Net.Http.StringContent]::new(
            $payload, [System.Text.Encoding]::UTF8, 'application/json')
        $null = $content.Add($stringContent, 'payload_json')

        $fs = [System.IO.File]::OpenRead($Path)
        $fileContent = [System.Net.Http.StreamContent]::new($fs)
        if ($Path.ToLower().EndsWith('.html')) {
            $fileContent.Headers.ContentType =
                [System.Net.Http.Headers.MediaTypeHeaderValue]::Parse('text/html')
        }
        else {
            $fileContent.Headers.ContentType =
                [System.Net.Http.Headers.MediaTypeHeaderValue]::Parse('application/zip')
        }
        $null = $content.Add($fileContent, 'files[0]',
            [System.IO.Path]::GetFileName($Path))

        $response = $client.PostAsync($Url, $content).Result
        if (-not $response.IsSuccessStatusCode) {
            $code = [int]$response.StatusCode
            $reason = [string]$response.ReasonPhrase
            throw ("HTTP {0} {1}" -f $code, $reason)
        }
    }
    finally {
        if ($fs) { $fs.Dispose() }
        $client.Dispose()
    }
}

try {
    if (-not (Test-Path -LiteralPath $FilePath)) {
        Write-Host "[WARN] Discord notify: file missing: $FilePath"
        Write-Result -Result 'SKIP:MissingFile'
        exit 2
    }

    $msg = Get-SummaryMessage -Path $FilePath
    $name = [System.IO.Path]::GetFileName($FilePath)

    Write-Host "[INFO] Discord: uploading HTML attachment..."
    try {
        Send-Attachment -Url $WebhookUrl -Path $FilePath -Message $msg
        Write-Host "[OK] Posted Discord notification (HTML)."
        Write-Result -Result 'HTML' -Name $name
        exit 0
    }
    catch {
        Write-Host ("[INFO] HTML upload failed ({0}) - trying ZIP fallback..." -f $_.Exception.Message)
    }

    if (-not $ZipFallbackDir) {
        $ZipFallbackDir = [System.IO.Path]::GetDirectoryName($FilePath)
    }
    if (-not (Test-Path -LiteralPath $ZipFallbackDir)) {
        New-Item -ItemType Directory -Path $ZipFallbackDir -Force | Out-Null
    }

    $zipName = [System.IO.Path]::GetFileNameWithoutExtension($FilePath) + '.zip'
    $zipPath = Join-Path $ZipFallbackDir $zipName

    Write-Host "[INFO] Building ZIP file"
    if (Test-Path -LiteralPath $zipPath) { Remove-Item -LiteralPath $zipPath -Force }
    Compress-Archive -LiteralPath $FilePath -DestinationPath $zipPath -Force

    if (-not (Test-Path -LiteralPath $zipPath)) {
        Write-Host "[WARN] ZIP fallback failed - skipping Discord notification."
        Write-Result -Result 'SKIP:ZipCreateFailed'
        exit 2
    }

    Write-Host "[OK] ZIP file written to:"
    Write-Host "      $zipPath"
    Write-Host "[INFO] Discord: uploading ZIP fallback..."

    try {
        Send-Attachment -Url $WebhookUrl -Path $zipPath -Message $msg
        Write-Host "[OK] Posted Discord notification (ZIP)."
        Write-Result -Result 'ZIP' -Name ([System.IO.Path]::GetFileName($zipPath))
        exit 0
    }
    catch {
        Write-Host ("[WARN] Discord upload (ZIP) failed: {0}" -f $_.Exception.Message)
        Write-Result -Result 'SKIP:Error'
        exit 2
    }
}
catch {
    Write-Host ("[ERROR] " + $_.Exception.Message)
    Write-Result -Result 'SKIP:Error'
    exit 1
}
