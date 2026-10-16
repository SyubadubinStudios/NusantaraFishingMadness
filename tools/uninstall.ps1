param([string]$Installation = $PSScriptRoot)
$ErrorActionPreference = 'Stop'
$target = [IO.Path]::GetFullPath($Installation).TrimEnd('\','/')
$record = Get-Content -LiteralPath (Join-Path $target 'installation.json') -Raw | ConvertFrom-Json
$manifest = Get-Content -LiteralPath (Join-Path $target 'package-manifest.json') -Raw | ConvertFrom-Json
if ($record.Product -ne 'NusantaraFishingMadness' -or $manifest.Product -ne $record.Product -or $record.Directory -ne $target) { throw 'Installation ownership cannot be verified.' }
$paths = @($manifest.Files | ForEach-Object { $_.Path }) + @('package-manifest.json','installation.json')
$resolvedFiles = @($paths | ForEach-Object {
    $resolved = [IO.Path]::GetFullPath((Join-Path $target $_))
    if (!$resolved.StartsWith($target + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) { throw 'Manifest path escapes the installation.' }
    $resolved
})
# Remove only recorded files. Never recursively delete an installation or a save directory.
foreach ($path in $resolvedFiles) { if (Test-Path -LiteralPath $path -PathType Leaf) { Remove-Item -LiteralPath $path -Force } }
Get-ChildItem -LiteralPath $target -Directory -Recurse | Sort-Object FullName -Descending | ForEach-Object {
    if (!(Get-ChildItem -LiteralPath $_.FullName -Force | Select-Object -First 1)) { Remove-Item -LiteralPath $_.FullName }
}
if ($record.Integrated) {
    $expected = Join-Path ([Environment]::GetFolderPath('Programs')) 'Nusantara Fishing Madness.lnk'
    if ($record.Shortcut -eq $expected -and (Test-Path -LiteralPath $expected)) { Remove-Item -LiteralPath $expected }
    $key = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\NusantaraFishingMadness'
    if (Test-Path $key) { Remove-Item -LiteralPath $key }
}
Write-Output 'UNINSTALLED: application files removed; user saves and unrecorded files preserved.'
