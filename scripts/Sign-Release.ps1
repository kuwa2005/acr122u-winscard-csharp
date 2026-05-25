[CmdletBinding()]
param(
    [Parameter(Mandatory = $true, Position = 0)]
    [ValidateNotNullOrEmpty()]
    [string[]]$Path
)

$ErrorActionPreference = 'Stop'

function Resolve-SignTool {
    if ($env:SIGNTOOL_PATH) {
        if (-not (Test-Path -LiteralPath $env:SIGNTOOL_PATH)) {
            throw "SIGNTOOL_PATH が存在しません: $env:SIGNTOOL_PATH"
        }
        return (Resolve-Path -LiteralPath $env:SIGNTOOL_PATH).Path
    }

    $command = Get-Command signtool.exe -ErrorAction SilentlyContinue
    if ($command) {
        return $command.Source
    }

    $kitRoots = @(
        "${env:ProgramFiles(x86)}\Windows Kits\10\bin",
        "${env:ProgramFiles}\Windows Kits\10\bin"
    ) | Where-Object { $_ -and (Test-Path -LiteralPath $_) }

    $candidate = $null
    if ($kitRoots.Count -gt 0) {
        $candidate = Get-ChildItem -Path $kitRoots -Filter signtool.exe -Recurse -ErrorAction SilentlyContinue |
            Where-Object { $_.FullName -match '\\x64\\signtool\.exe$' } |
            Sort-Object FullName -Descending |
            Select-Object -First 1
    }

    if ($candidate) {
        return $candidate.FullName
    }

    throw 'signtool.exe が見つかりません。Windows SDK をインストールするか、SIGNTOOL_PATH を指定してください。'
}

$signTool = Resolve-SignTool
$timestampUrl = if ($env:SIGN_TIMESTAMP_URL) { $env:SIGN_TIMESTAMP_URL } else { 'http://timestamp.digicert.com' }
$signArgs = @('sign', '/fd', 'SHA256', '/td', 'SHA256', '/tr', $timestampUrl)

if ($env:SIGN_CERT_PATH) {
    if (-not (Test-Path -LiteralPath $env:SIGN_CERT_PATH)) {
        throw "SIGN_CERT_PATH が存在しません: $env:SIGN_CERT_PATH"
    }

    $signArgs += @('/f', (Resolve-Path -LiteralPath $env:SIGN_CERT_PATH).Path)
    if ($env:SIGN_CERT_PASSWORD) {
        $signArgs += @('/p', $env:SIGN_CERT_PASSWORD)
    }
}
elseif ($env:SIGN_CERT_THUMBPRINT) {
    $storeName = if ($env:SIGN_CERT_STORE_NAME) { $env:SIGN_CERT_STORE_NAME } else { 'My' }
    $signArgs += @('/sha1', $env:SIGN_CERT_THUMBPRINT, '/s', $storeName)

    if ($env:SIGN_CERT_STORE_LOCATION -eq 'LocalMachine') {
        $signArgs += '/sm'
    }
}
else {
    throw 'SIGN_CERT_PATH または SIGN_CERT_THUMBPRINT を指定してください。証明書や秘密鍵はリポジトリにコミットしないでください。'
}

foreach ($item in $Path) {
    if (-not (Test-Path -LiteralPath $item)) {
        throw "署名対象が存在しません: $item"
    }

    $resolvedPath = (Resolve-Path -LiteralPath $item).Path
    Write-Host "Signing: $resolvedPath"
    & $signTool @signArgs $resolvedPath
    if ($LASTEXITCODE -ne 0) {
        throw "signtool.exe が終了コード $LASTEXITCODE を返しました: $resolvedPath"
    }
}