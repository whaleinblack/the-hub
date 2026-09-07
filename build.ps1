$ErrorActionPreference = 'Stop'
$framework = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
$output = Join-Path $PSScriptRoot 'bin'
New-Item -ItemType Directory -Force -Path $output | Out-Null
& (Join-Path $PSScriptRoot 'make-icon.ps1') -OutputPath (Join-Path $output 'hub.ico')
$references = @('System.dll', 'System.Core.dll', 'System.Drawing.dll', 'System.Windows.Forms.dll')
$references += @('UIAutomationClient.dll', 'UIAutomationTypes.dll', 'WindowsBase.dll') | ForEach-Object { Join-Path "$framework\WPF" $_ }
$arguments = @('/nologo', '/target:winexe', '/optimize+', '/platform:x64', '/codepage:65001', "/win32icon:$output\hub.ico", "/out:$output\Hub.exe")
$arguments += $references | ForEach-Object { "/reference:$_" }
$arguments += Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot 'src') -Filter '*.cs' | ForEach-Object FullName
& "$framework\csc.exe" @arguments
if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
Write-Output "$output\Hub.exe"
