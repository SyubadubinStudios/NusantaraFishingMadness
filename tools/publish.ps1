param(
    [ValidateSet('win-x64','linux-x64','osx-arm64','osx-x64')][string]$Runtime = 'win-x64',
    [string]$CertificateThumbprint = ''
)
$ErrorActionPreference = 'Stop'
if ($CertificateThumbprint -and $Runtime -ne 'win-x64') { throw 'The signing hook supports Windows executables only.' }
$repo = Split-Path $PSScriptRoot -Parent
$stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
$destination = Join-Path $repo "artifacts/packages/$Runtime-$stamp"
New-Item -ItemType Directory -Path $destination -Force | Out-Null
foreach ($project in @('Desktop','Server')) {
    $output = if ($project -eq 'Server') { Join-Path $destination 'server' } else { $destination }
    & dotnet publish (Join-Path $repo "src/NusantaraFishing.$project") -c Release -r $Runtime --self-contained true -p:PublishTrimmed=false "-p:NuGetLockFilePath=obj/packaging-$Runtime.packages.lock.json" -o $output
    if ($LASTEXITCODE -ne 0) { throw "Publish failed: $project" }
}
Copy-Item -LiteralPath (Join-Path $repo 'README.md') -Destination $destination
if (Test-Path -LiteralPath (Join-Path $repo 'LICENSE')) { Copy-Item -LiteralPath (Join-Path $repo 'LICENSE') -Destination $destination }
Copy-Item -LiteralPath (Join-Path $repo 'PLAN.md'),(Join-Path $repo 'Progress.md'),(Join-Path $repo 'solution-design.md') -Destination $destination
Copy-Item -LiteralPath (Join-Path $repo 'docs') -Destination $destination -Recurse
Copy-Item -LiteralPath (Join-Path $repo 'assets/README.md') -Destination (Join-Path $destination 'assets/README.md')
Copy-Item -LiteralPath (Join-Path $repo 'docs/THIRD-PARTY-NOTICES.md') -Destination $destination
Copy-Item -LiteralPath (Join-Path $repo 'docs/licenses') -Destination $destination -Recurse
if ($Runtime -eq 'win-x64') {
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'install.ps1'),(Join-Path $PSScriptRoot 'uninstall.ps1') -Destination $destination
    if ($CertificateThumbprint) {
        $signTool = Get-ChildItem "${env:ProgramFiles(x86)}/Windows Kits/10/bin/*/x64/signtool.exe" | Sort-Object FullName -Descending | Select-Object -First 1
        if (!$signTool) { throw 'Install the Windows SDK signing tools.' }
        foreach ($executable in @('NusantaraFishing.Desktop.exe','server/NusantaraFishing.Server.exe')) {
            & $signTool.FullName sign /sha1 $CertificateThumbprint /fd SHA256 /tr http://timestamp.digicert.com /td SHA256 (Join-Path $destination $executable)
            if ($LASTEXITCODE -ne 0) { throw "Signing failed: $executable" }
            & $signTool.FullName verify /pa (Join-Path $destination $executable)
            if ($LASTEXITCODE -ne 0) { throw "Signature verification failed: $executable" }
        }
    }
}
$files = @(Get-ChildItem -LiteralPath $destination -Recurse -File | ForEach-Object {
    @{ Path = $_.FullName.Substring($destination.Length + 1).Replace('\','/'); Sha256 = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash; Bytes = $_.Length }
})
@{ Product = 'NusantaraFishingMadness'; Runtime = $Runtime; Signed = [bool]$CertificateThumbprint; Files = $files } |
    ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $destination 'package-manifest.json') -Encoding UTF8
Compress-Archive -LiteralPath $destination -DestinationPath "$destination.zip" -CompressionLevel Optimal
Write-Output "PACKAGE_READY: $destination"
