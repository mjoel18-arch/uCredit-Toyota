#requires -Version 5.1
[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$webRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\src\uCredit.Web')).Path
if ([string]::IsNullOrWhiteSpace($env:VITE_DEV_HTTPS_CERT) -or [string]::IsNullOrWhiteSpace($env:VITE_DEV_HTTPS_KEY)) {
  throw 'VITE_DEV_HTTPS_CERT y VITE_DEV_HTTPS_KEY deben estar configuradas.'
}

if (-not (Test-Path -LiteralPath $env:VITE_DEV_HTTPS_CERT -PathType Leaf) -or
    -not (Test-Path -LiteralPath $env:VITE_DEV_HTTPS_KEY -PathType Leaf)) {
  throw 'Los archivos de certificado HTTPS no están disponibles.'
}

$stdoutPath = [System.IO.Path]::GetTempFileName()
$stderrPath = [System.IO.Path]::GetTempFileName()
$viteCacheDir = Join-Path (Split-Path -Parent $stdoutPath) 'vite-cache'
New-Item -ItemType Directory -Path $viteCacheDir -Force | Out-Null
$viteProcess = $null

try {
  $env:UCREDIT_VITE_CACHE_DIR = $viteCacheDir
  $viteProcess = Start-Process -FilePath 'npm.cmd' -ArgumentList @('run', 'dev', '--', '--host', 'localhost', '--strictPort') -WorkingDirectory $webRoot -RedirectStandardOutput $stdoutPath -RedirectStandardError $stderrPath -PassThru -WindowStyle Hidden

  $deadline = [DateTime]::UtcNow.AddSeconds(30)
  $httpsReady = $false

  while ([DateTime]::UtcNow -lt $deadline) {
    $stdout = Get-Content -LiteralPath $stdoutPath -Raw -ErrorAction SilentlyContinue
    $stderr = Get-Content -LiteralPath $stderrPath -Raw -ErrorAction SilentlyContinue
    $output = $stdout + $stderr

    if ($output -match 'Local:\s+https://localhost:5173/') {
      $httpsReady = $true
      break
    }

    if ($output -match 'Local:\s+http://localhost:5173/') {
      throw 'Vite inició en HTTP; la configuración HTTPS no se aplicó.'
    }

    if ($viteProcess.HasExited) {
      throw 'Vite terminó antes de anunciar el servidor HTTPS.'
    }

    Start-Sleep -Milliseconds 250
  }

  if (-not $httpsReady) {
    throw 'Vite no anunció HTTPS dentro del tiempo esperado.'
  }

  Write-Output 'Local: https://localhost:5173/'
  Write-Output 'Vite HTTPS smoke test passed: https://localhost:5173/'
}
finally {
  if ($null -ne $viteProcess -and -not $viteProcess.HasExited) {
    & taskkill.exe /PID $viteProcess.Id /T /F 2>$null | Out-Null
  }

  Remove-Item Env:UCREDIT_VITE_CACHE_DIR -ErrorAction SilentlyContinue
  Remove-Item -LiteralPath $stdoutPath, $stderrPath, $viteCacheDir -Recurse -Force -ErrorAction SilentlyContinue
}