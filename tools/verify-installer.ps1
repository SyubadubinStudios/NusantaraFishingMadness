param([Parameter(Mandatory=$true)][string]$Installer)
$ErrorActionPreference = 'Stop'
$workspace = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$target = [IO.Path]::GetFullPath((Join-Path $workspace ('artifacts/installer-smoke-' + [Guid]::NewGuid().ToString('N'))))
if (!$target.StartsWith($workspace + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) { throw 'Unsafe test directory.' }
$setup = Start-Process -FilePath ([IO.Path]::GetFullPath($Installer)) -ArgumentList @('/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART', "/DIR=`"$target`"") -WindowStyle Hidden -Wait -PassThru
if ($setup.ExitCode -ne 0 -or !(Test-Path -LiteralPath (Join-Path $target 'NusantaraFishing.Desktop.exe'))) { throw 'Installer failed.' }
Set-Content -LiteralPath (Join-Path $target 'keep-user-note.txt') -Value 'Preserve unknown files.'
$uninstall = Start-Process -FilePath (Join-Path $target 'unins000.exe') -ArgumentList @('/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART') -WindowStyle Hidden -Wait -PassThru
if ($uninstall.ExitCode -ne 0 -or (Test-Path -LiteralPath (Join-Path $target 'NusantaraFishing.Desktop.exe'))) { throw 'Uninstaller failed.' }
if (!(Test-Path -LiteralPath (Join-Path $target 'keep-user-note.txt'))) { throw 'Uninstaller removed an unknown file.' }
Remove-Item -LiteralPath (Join-Path $target 'keep-user-note.txt')
Write-Output 'INSTALLER_CHECK_OK: installed, uninstalled, unknown file preserved.'
