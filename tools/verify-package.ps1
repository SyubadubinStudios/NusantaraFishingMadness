param([Parameter(Mandatory=$true)][string]$Package)
$ErrorActionPreference='Stop'
$packageRoot=[IO.Path]::GetFullPath($Package).TrimEnd('\','/')
$manifest=Get-Content -LiteralPath (Join-Path $packageRoot 'package-manifest.json') -Raw | ConvertFrom-Json
foreach($file in $manifest.Files) {
    $path=[IO.Path]::GetFullPath((Join-Path $packageRoot $file.Path))
    if(!$path.StartsWith($packageRoot+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)) { throw 'Unsafe package path.' }
    if((Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash -ne $file.Sha256) { throw "Checksum mismatch: $($file.Path)" }
}
$temporary=[IO.Path]::GetFullPath((Join-Path ([IO.Path]::GetTempPath()) ('nfm-install-'+[Guid]::NewGuid().ToString('N'))))
New-Item -ItemType Directory -Path $temporary | Out-Null
$installation=Join-Path $temporary 'app'
& powershell -NoProfile -File (Join-Path $packageRoot 'install.ps1') -Destination $installation -NoIntegration
if($LASTEXITCODE -ne 0) { throw 'Installer failed.' }
Set-Content -LiteralPath (Join-Path $installation 'keep-user-note.txt') -Value 'Preserve unknown files.'
& powershell -NoProfile -File (Join-Path $installation 'uninstall.ps1')
if($LASTEXITCODE -ne 0) { throw 'Uninstaller failed.' }
if(!(Test-Path -LiteralPath (Join-Path $installation 'keep-user-note.txt'))) { throw 'Uninstall removed an unknown file.' }
if(Test-Path -LiteralPath (Join-Path $installation 'NusantaraFishing.Desktop.exe')) { throw 'App executable remains installed.' }
# Delete only the two fixture files/directories created by this validation.
Remove-Item -LiteralPath (Join-Path $installation 'keep-user-note.txt')
Remove-Item -LiteralPath $installation
Remove-Item -LiteralPath $temporary
Write-Output "PACKAGE_CHECK_OK: $($manifest.Files.Count) hashes, isolated install/uninstall, unknown-file preservation."
