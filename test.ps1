$ErrorActionPreference = 'Stop'
& (Join-Path $PSScriptRoot 'build.ps1')
$app = Join-Path $PSScriptRoot 'bin\Hub.exe'
$testProcess = Start-Process -FilePath $app -ArgumentList '--self-test' -WindowStyle Hidden -PassThru
if (-not $testProcess.WaitForExit(15000)) { $testProcess.Kill(); throw 'Self-test timed out.' }
Get-Content (Join-Path $PSScriptRoot 'bin\test-results.txt')
if ($testProcess.ExitCode -ne 0) { throw 'Self-test failed.' }
Start-Process -FilePath $app -ArgumentList '--render-preview' -WindowStyle Hidden -Wait
