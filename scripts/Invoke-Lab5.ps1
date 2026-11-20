# Lab 5 evidence helper.
# Load it once per PowerShell window:   . .\scripts\Invoke-Lab5.ps1
# Use it:                               Invoke-Lab5 T01 GET /api/v1/students
#                                       Invoke-Lab5 T02 POST /api/v1/students '{"studentNumber":"ST-500","fullName":"Eli Tan"}'
# It prints the result and appends the request, status, Location header and body to docs\lab5\evidence\<Test>-requests.txt.
# It never prints or stores any password or connection string.

$script:BaseUrl = "http://localhost:5080"
$script:EvidenceDir = Join-Path (Split-Path $PSScriptRoot -Parent) "docs\lab5\evidence"

function Invoke-Lab5 {
    param(
        [Parameter(Mandatory)][string]$Test,
        [Parameter(Mandatory)][string]$Method,
        [Parameter(Mandatory)][string]$Path,
        [string]$Body
    )

    New-Item -ItemType Directory -Force $script:EvidenceDir | Out-Null
    $log = Join-Path $script:EvidenceDir "$Test-requests.txt"
    $outFile = [System.IO.Path]::GetTempFileName()
    $hdrFile = [System.IO.Path]::GetTempFileName()

    $curlArgs = @("-s", "-o", $outFile, "-D", $hdrFile, "-w", "%{http_code}", "-X", $Method.ToUpper(), "$script:BaseUrl$Path")
    if ($Body) {
        $inFile = [System.IO.Path]::GetTempFileName()
        [System.IO.File]::WriteAllText($inFile, $Body)
        $curlArgs += @("-H", "Content-Type: application/json", "--data-binary", "@$inFile")
    }

    $status = & curl.exe @curlArgs
    $curlExit = $LASTEXITCODE
    $responseBody = ""
    if (Test-Path $outFile) { $responseBody = [System.IO.File]::ReadAllText($outFile) }

    $location = ""
    if (Test-Path $hdrFile) {
        $loc = Select-String -Path $hdrFile -Pattern '^Location:' | Select-Object -First 1
        if ($loc) { $location = ($loc.Line -replace '^Location:\s*', '').Trim() }
    }

    if ($curlExit -ne 0 -or $status -eq "000") {
        $statusText = "NO RESPONSE (no HTTP response received, curl exit code $curlExit)"
    } else {
        $statusText = "HTTP $status"
    }

    $stamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
    $lines = @(
        "[$stamp] $Test",
        "REQUEST : $($Method.ToUpper()) $Path",
        $(if ($Body) { "BODY    : $Body" } else { "BODY    : (none)" }),
        "RESPONSE: $statusText",
        $(if ($location) { "LOCATION: $location" } else { "LOCATION: (none)" }),
        "RESULT  : $responseBody",
        ""
    )
    $lines | Add-Content -Path $log -Encoding UTF8
    $lines | ForEach-Object { Write-Host $_ }

    Remove-Item $outFile, $hdrFile -ErrorAction SilentlyContinue
    if ($inFile) { Remove-Item $inFile -ErrorAction SilentlyContinue }
}
