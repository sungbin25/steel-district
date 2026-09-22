param(
    [string]$Command,
    [string]$RequestFile,
    [string]$ProjectRoot = (Join-Path $PSScriptRoot '../../../../..'),
    [ValidateRange(1,660)][int]$TimeoutSeconds = 60
)
$ErrorActionPreference = 'Stop'
$bridgeRoot = Join-Path ([System.IO.Path]::GetFullPath($ProjectRoot)) 'Library/SteelDistrictBridge'
function Read-BridgeText([string]$Path) {
    $bridgeReadStream = [System.IO.File]::Open($Path, [System.IO.FileMode]::Open, [System.IO.FileAccess]::Read, ([System.IO.FileShare]::ReadWrite -bor [System.IO.FileShare]::Delete))
    $bridgeReader = New-Object System.IO.StreamReader($bridgeReadStream, [System.Text.Encoding]::UTF8)
    try { return $bridgeReader.ReadToEnd() } finally { $bridgeReader.Dispose() }
}
function Write-BridgeFailure([string]$Code, [string]$Message, [string]$Id) {
    [pscustomobject]@{ version=1; id=$Id; ok=$false; issues=@(@{code=$Code;message=$Message}) } | ConvertTo-Json -Depth 8
}
try {
    if ($RequestFile) { $bridgeRequest = Get-Content -LiteralPath $RequestFile -Raw | ConvertFrom-Json }
    elseif ($Command) { $bridgeRequest = [pscustomobject]@{command=$Command} }
    else { throw 'Command 또는 RequestFile을 지정하세요.' }
    if (-not $bridgeRequest.command) { throw 'command가 없습니다.' }
    if (-not $bridgeRequest.id) { $bridgeRequest | Add-Member id ([guid]::NewGuid().ToString('N')) -Force }
    if ($bridgeRequest.id -notmatch '^[a-zA-Z0-9_-]{1,64}$') { throw '잘못된 요청 id입니다.' }
    if (-not $bridgeRequest.version) { $bridgeRequest | Add-Member version 1 -Force }
    $bridgeJson = $bridgeRequest | ConvertTo-Json -Depth 20 -Compress
    $bridgeUtf8 = New-Object System.Text.UTF8Encoding($false)
    $bridgeBytes = $bridgeUtf8.GetBytes($bridgeJson)
    if ($bridgeBytes.Length -gt 65536) { throw '요청은 64 KiB 이하로 제한됩니다.' }
    $bridgeSha = [System.Security.Cryptography.SHA256]::Create()
    try { $bridgeHash = ([System.BitConverter]::ToString($bridgeSha.ComputeHash($bridgeBytes))).Replace('-','').ToLowerInvariant() }
    finally { $bridgeSha.Dispose() }
    $bridgeResponsePath = Join-Path $bridgeRoot ('responses/' + $bridgeRequest.id + '.json')
    $bridgeIncomingPath = Join-Path $bridgeRoot ('requests/' + $bridgeRequest.id + '.json')
    $bridgeProcessingPath = Join-Path $bridgeRoot ('processing/' + $bridgeRequest.id + '.json')
    if (-not (Test-Path -LiteralPath $bridgeResponsePath)) {
        $bridgeHeartbeatPath = Join-Path $bridgeRoot 'heartbeat.json'
        if (-not (Test-Path -LiteralPath $bridgeHeartbeatPath)) { throw '브리지가 준비되지 않았습니다. 해당 Unity 프로젝트를 열고 컴파일 완료를 기다리세요.' }
        $bridgeHeartbeat = Read-BridgeText $bridgeHeartbeatPath | ConvertFrom-Json
        # PowerShell 7은 JSON 날짜를 DateTime으로 바꿀 수 있습니다. 문자열로 재파싱하면 UTC Kind를 잃습니다.
        $bridgeHeartbeatUtc = if ($bridgeHeartbeat.utc -is [datetime]) { $bridgeHeartbeat.utc.ToUniversalTime() } else { [datetime]::Parse([string]$bridgeHeartbeat.utc, [System.Globalization.CultureInfo]::InvariantCulture, [System.Globalization.DateTimeStyles]::RoundtripKind).ToUniversalTime() }
        if (([datetime]::UtcNow - $bridgeHeartbeatUtc).TotalSeconds -gt 30) { throw 'Editor 응답이 오래되었습니다. 컴파일/실행 상태를 확인하세요.' }
        $null = Get-Process -Id $bridgeHeartbeat.editorPid
        if (-not (Test-Path -LiteralPath $bridgeIncomingPath) -and -not (Test-Path -LiteralPath $bridgeProcessingPath)) {
            $bridgeTemporaryPath = $bridgeIncomingPath + '.tmp'
            [System.IO.File]::WriteAllText($bridgeTemporaryPath, $bridgeJson, $bridgeUtf8)
            Move-Item -LiteralPath $bridgeTemporaryPath -Destination $bridgeIncomingPath
        }
    }
    $bridgeTimer = [System.Diagnostics.Stopwatch]::StartNew()
    while ($bridgeTimer.Elapsed.TotalSeconds -lt $TimeoutSeconds) {
        if (Test-Path -LiteralPath $bridgeResponsePath) {
            $bridgeResultJson = Read-BridgeText $bridgeResponsePath
            $bridgeResult = $bridgeResultJson | ConvertFrom-Json
            if ($bridgeResult.requestHash -ne $bridgeHash) { throw '같은 id의 요청 내용이 다릅니다. 기존 응답을 확인하세요.' }
            Write-Output $bridgeResultJson
            if ($bridgeResult.ok) { exit 0 } else { exit 1 }
        }
        Start-Sleep -Milliseconds 200
    }
    # 아직 처리 전인 요청만 취소합니다. 이미 시작한 변경을 새 id로 자동 재시도하지 않습니다.
    if (Test-Path -LiteralPath $bridgeIncomingPath) {
        try { Move-Item -LiteralPath $bridgeIncomingPath -Destination ($bridgeIncomingPath + '.cancelled') -ErrorAction Stop } catch { }
    }
    Write-BridgeFailure 'TIMEOUT' ('대기 시간이 지났습니다. 처리 중 여부: ' + (Test-Path -LiteralPath $bridgeProcessingPath) + '. 같은 id와 내용으로 결과를 다시 조회하세요.') $bridgeRequest.id
    exit 2
}
catch {
    Write-BridgeFailure 'CLIENT_FAILED' $_.Exception.Message $bridgeRequest.id
    exit 1
}
