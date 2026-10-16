param(
    [string]$Destination = (Join-Path $env:LOCALAPPDATA 'Programs/SyubadubinStudios/NusantaraFishingMadness'),
    [switch]$NoIntegration
)
$ErrorActionPreference = 'Stop'
$source = [IO.Path]::GetFullPath($PSScriptRoot)
$target = [IO.Path]::GetFullPath($Destination).TrimEnd('\','/')
$manifest = Get-Content -LiteralPath (Join-Path $source 'package-manifest.json') -Raw | ConvertFrom-Json
if ($manifest.Product -ne 'NusantaraFishingMadness' -or $manifest.Runtime -ne 'win-x64') { throw 'Invalid Windows package.' }
if ($target -eq $source -or $target -eq [IO.Path]::GetPathRoot($target).TrimEnd('\','/')) { throw 'Choose a separate application directory.' }
if (Test-Path -LiteralPath $target) {
    if (Get-ChildItem -LiteralPath $target -Force | Select-Object -First 1) { throw 'Destination must be empty. Uninstall the previous app first; saves are preserved.' }
}
function Resolve-Child([string]$base, [string]$relative) {
    $resolved = [IO.Path]::GetFullPath((Join-Path $base $relative))
    if (!$resolved.StartsWith($base + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) { throw 'Package path escapes its directory.' }
    return $resolved
}
foreach ($file in $manifest.Files) {
    $inputFile = Resolve-Child $source $file.Path
    if ((Get-FileHash -LiteralPath $inputFile -Algorithm SHA256).Hash -ne $file.Sha256) { throw "Checksum mismatch: $($file.Path)" }
    $null = Resolve-Child $target $file.Path
}
New-Item -ItemType Directory -Path $target -Force | Out-Null
foreach ($file in $manifest.Files) {
    $outputFile = Resolve-Child $target $file.Path
    New-Item -ItemType Directory -Path (Split-Path $outputFile -Parent) -Force | Out-Null
    Copy-Item -LiteralPath (Resolve-Child $source $file.Path) -Destination $outputFile
}
Copy-Item -LiteralPath (Join-Path $source 'package-manifest.json') -Destination $target
$registration = @{ Product='NusantaraFishingMadness'; Directory=$target; Integrated=(!$NoIntegration) }
if (!$NoIntegration) {
    $shortcut = Join-Path ([Environment]::GetFolderPath('Programs')) 'Nusantara Fishing Madness.lnk'
    $shell = New-Object -ComObject WScript.Shell
    $link = $shell.CreateShortcut($shortcut)
    $link.TargetPath = Join-Path $target 'NusantaraFishing.Desktop.exe'; $link.WorkingDirectory = $target; $link.Save()
    $registration.Shortcut = $shortcut
    $key = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\NusantaraFishingMadness'
    New-Item -Path $key -Force | Out-Null
    New-ItemProperty -Path $key -Name DisplayName -Value 'Nusantara Fishing Madness 3D' -Force | Out-Null
    New-ItemProperty -Path $key -Name Publisher -Value 'Syubadubin Studios' -Force | Out-Null
    New-ItemProperty -Path $key -Name UninstallString -Value "powershell.exe -NoProfile -File `"$target\uninstall.ps1`"" -Force | Out-Null
}
$registration | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $target 'installation.json') -Encoding UTF8
Write-Output "INSTALLED: $target (user saves are stored separately)"
