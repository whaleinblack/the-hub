param([switch]$Uninstall, [string]$ThreadId, [string]$RulesPath)
$ErrorActionPreference = 'Stop'
if (-not ('HubPackageIdentity' -as [type])) {
    Add-Type 'using System; using System.Runtime.InteropServices; public static class HubPackageIdentity { [DllImport("kernel32.dll", CharSet=CharSet.Unicode)] public static extern int GetCurrentPackageFullName(ref int length, System.Text.StringBuilder name); }'
}
$packageLength = 0
if ([HubPackageIdentity]::GetCurrentPackageFullName([ref]$packageLength, $null) -ne 15700) {
    throw 'Run install.ps1 from an ordinary Windows PowerShell opened from Start. A packaged terminal can redirect app files and shortcuts into private storage.'
}
$installDirectory = Join-Path $env:LOCALAPPDATA 'TheHub'
$executable = Join-Path $installDirectory 'Hub.exe'
$startupShortcut = Join-Path ([Environment]::GetFolderPath('Startup')) 'The Hub.lnk'
$menuShortcut = Join-Path ([Environment]::GetFolderPath('Programs')) 'Hub.lnk'

function Stop-HubProgram([string]$TargetExe) {
    if (-not (Test-Path -LiteralPath $TargetExe)) { return }
    Start-Process -FilePath $TargetExe -ArgumentList '--stop' -WindowStyle Hidden -Wait
    $deadline = [DateTime]::UtcNow.AddSeconds(5)
    do {
        $running = @(Get-Process -ErrorAction SilentlyContinue | Where-Object { $_.Path -eq $TargetExe })
        if ($running.Count -eq 0) { return }
        Start-Sleep -Milliseconds 150
    } while ([DateTime]::UtcNow -lt $deadline)
    throw 'Existing program did not stop; installation cancelled.'
}

if ($Uninstall) {
    Stop-HubProgram $executable
    foreach ($shortcutPath in @($startupShortcut, $menuShortcut)) {
        if (Test-Path -LiteralPath $shortcutPath) { Remove-Item -LiteralPath $shortcutPath }
    }
    Write-Output 'Stopped and removed startup / Start menu shortcuts. Local settings retained.'
    return
}

if ($ThreadId) { $parsedId = [guid]::ParseExact($ThreadId, 'D'); if ($parsedId -eq [guid]::Empty) { throw 'Empty conversation ID.' } }
if ($RulesPath -and (-not (Test-Path -LiteralPath $RulesPath -PathType Leaf) -or [IO.Path]::GetExtension($RulesPath) -ne '.md')) { throw 'RulesPath must reference an existing Markdown file.' }
& (Join-Path $PSScriptRoot 'build.ps1')
Stop-HubProgram $executable
New-Item -ItemType Directory -Force -Path $installDirectory | Out-Null
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'bin\Hub.exe') -Destination $executable -Force

# Migrate the previous prototype's private settings without publishing them.
$legacyDirectory = Join-Path $env:LOCALAPPDATA 'WindowsCompanion'
foreach ($name in @('thread-id.txt', 'rules-path.txt')) {
    $oldFile = Join-Path $legacyDirectory $name
    $newFile = Join-Path $installDirectory $name
    if ((Test-Path -LiteralPath $oldFile) -and -not (Test-Path -LiteralPath $newFile)) { Copy-Item -LiteralPath $oldFile -Destination $newFile }
}
if ($ThreadId) { [IO.File]::WriteAllText((Join-Path $installDirectory 'thread-id.txt'), $parsedId.ToString('D')) }
if ($RulesPath) { [IO.File]::WriteAllText((Join-Path $installDirectory 'rules-path.txt'), (Resolve-Path -LiteralPath $RulesPath).Path) }

$shell = New-Object -ComObject WScript.Shell
foreach ($path in @($startupShortcut, $menuShortcut)) {
    $shortcut = $shell.CreateShortcut($path)
    $shortcut.TargetPath = $executable
    $shortcut.WorkingDirectory = $installDirectory
    $shortcut.IconLocation = "$executable,0"
    $shortcut.Description = 'The Hub - native Windows quick actions and AI assistant'
    $shortcut.Arguments = if ($path -eq $startupShortcut) { '--background' } else { '--show' }
    $shortcut.Save()
}

Stop-HubProgram (Join-Path $legacyDirectory 'WindowsCompanion.exe')
$legacyShortcut = Join-Path ([Environment]::GetFolderPath('Startup')) 'Windows Companion.lnk'
if (Test-Path -LiteralPath $legacyShortcut) { Remove-Item -LiteralPath $legacyShortcut }
Start-Process -FilePath $executable -ArgumentList '--background' -WindowStyle Hidden
Write-Output "Installed: $executable"
Write-Output "Start menu: $menuShortcut"
Write-Output "Startup: $startupShortcut"
