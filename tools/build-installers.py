"""Wrap a self-contained publish directory in a native preview installer on its target OS."""
import argparse
import hashlib
import json
import os
from pathlib import Path
import plistlib
import re
import shutil
import subprocess
import tempfile

ROOT = Path(__file__).resolve().parents[1]


def run(*args):
    subprocess.run([str(x) for x in args], check=True)


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--package', required=True, type=Path)
    parser.add_argument('--runtime', required=True, choices=['win-x64', 'linux-x64', 'osx-arm64', 'osx-x64'])
    parser.add_argument('--version', required=True)
    parser.add_argument('--output', type=Path, default=ROOT / 'artifacts/installers')
    args = parser.parse_args()
    version = args.version.removeprefix('v')
    if not re.fullmatch(r'\d+\.\d+\.\d+(?:-[a-zA-Z0-9.]+)?', version):
        raise ValueError('Expected a numeric semantic version with an optional prerelease suffix.')
    source = args.package.resolve(strict=True)
    manifest = json.loads((source / 'package-manifest.json').read_text(encoding='utf-8-sig'))
    if manifest['Product'] != 'NusantaraFishingMadness' or manifest['Runtime'] != args.runtime:
        raise ValueError('Package/runtime mismatch.')
    for entry in manifest['Files']:
        path = (source / entry['Path']).resolve(strict=True)
        if not path.is_relative_to(source):
            raise ValueError('Manifest path escapes package.')
        with path.open('rb') as stream:
            if hashlib.file_digest(stream, 'sha256').hexdigest().lower() != entry['Sha256'].lower():
                raise ValueError('Package checksum mismatch: ' + entry['Path'])
    output = args.output.resolve()
    output.mkdir(parents=True, exist_ok=True)
    stage = Path(tempfile.mkdtemp(prefix=args.runtime + '-', dir=output))
    name = f'NusantaraFishingMadness-{version}-{args.runtime}'
    if args.runtime == 'win-x64':
        compiler = shutil.which('ISCC') or str(Path(os.environ.get('ProgramFiles(x86)', 'C:/Program Files (x86)')) / 'Inno Setup 6/ISCC.exe')
        run(compiler, f'/DSourceDir={source}', f'/DOutputDir={output}', f'/DAppVersion={version}', ROOT / 'tools/installers/windows.iss')
        artifact = output / (name + '-setup.exe')
    elif args.runtime == 'linux-x64':
        payload = stage / 'opt/nusantara-fishing-madness'
        shutil.copytree(source, payload)
        for executable in [payload / 'NusantaraFishing.Desktop', payload / 'server/NusantaraFishing.Server']:
            executable.chmod(0o755)
        control = stage / 'DEBIAN'
        control.mkdir()
        (control / 'control').write_text(
            f'Package: nusantara-fishing-madness\nVersion: {version.replace("-", "~")}\n'
            'Section: games\nPriority: optional\nArchitecture: amd64\n'
            'Maintainer: Syubadubin Studios\n'
            'Depends: libc6 (>= 2.34), libstdc++6, libgcc-s1, libssl3t64 | libssl3, libicu78 | libicu76 | libicu74 | libicu72 | libicu70, libx11-6, libice6, libsm6, libfontconfig1, libfreetype6, libvulkan1, libasound2t64 | libasound2\n'
            'Homepage: https://github.com/SyubadubinStudios/NusantaraFishingMadness\n'
            'Description: Indonesian fishing adventure with native 3D graphics\n'
            ' Unsigned preview by Ariana Mischa Fadhila, Syubadubin Studios.\n', encoding='utf-8')
        desktop = stage / 'usr/share/applications/nusantara-fishing-madness.desktop'
        desktop.parent.mkdir(parents=True)
        desktop.write_text('[Desktop Entry]\nType=Application\nName=Nusantara Fishing Madness\n'
                           'Exec=/opt/nusantara-fishing-madness/NusantaraFishing.Desktop\n'
                           'Icon=nusantara-fishing-madness\nCategories=Game;Simulation;\nTerminal=false\n', encoding='utf-8')
        icon = stage / 'usr/share/icons/hicolor/256x256/apps/nusantara-fishing-madness.png'
        icon.parent.mkdir(parents=True)
        shutil.copyfile(source / 'assets/art/game-icon-256.png', icon)
        artifact = output / (name + '.deb')
        run('dpkg-deb', '--build', '--root-owner-group', stage, artifact)
        run('dpkg-deb', '--info', artifact)
    else:
        app = stage / 'Nusantara Fishing Madness.app'
        contents = app / 'Contents'
        payload = contents / 'MacOS'
        # macOS treats directories with bundle-like extensions (for example
        # ReactiveUI.Binding.Avalonia) as nested code bundles during deep signing.
        # Keep documentation and license files out of the executable payload.
        shutil.copytree(source, payload, ignore=shutil.ignore_patterns(
            'docs', 'README.md', 'LICENSE', 'PLAN.md', 'Progress.md',
            'solution-design.md', 'THIRD-PARTY-NOTICES.md', 'package-manifest.json', 'licenses'))
        for executable in [payload / 'NusantaraFishing.Desktop', payload / 'server/NusantaraFishing.Server']:
            executable.chmod(0o755)
        resources = contents / 'Resources'
        resources.mkdir()
        shutil.copyfile(source / 'assets/art/game-icon.icns', resources / 'GameIcon.icns')
        license_dir = resources / 'ThirdPartyLicenses'
        license_dir.mkdir()
        shutil.copyfile(source / 'THIRD-PARTY-NOTICES.md', license_dir / 'THIRD-PARTY-NOTICES.md')
        for license_file in (source / 'licenses').rglob('*'):
            if license_file.is_file():
                flattened_name = '__'.join(license_file.relative_to(source / 'licenses').parts)
                shutil.copyfile(license_file, license_dir / flattened_name)
        with (contents / 'Info.plist').open('wb') as stream:
            plistlib.dump({'CFBundleIdentifier': 'com.syubadubinstudios.nusantarafishingmadness',
                          'CFBundleName': 'Nusantara Fishing Madness', 'CFBundleDisplayName': 'Nusantara Fishing Madness',
                          'CFBundleExecutable': 'NusantaraFishing.Desktop', 'CFBundleIconFile': 'GameIcon.icns',
                          'CFBundlePackageType': 'APPL', 'CFBundleShortVersionString': version.split('-')[0],
                          'CFBundleVersion': version.split('-')[0], 'LSMinimumSystemVersion': '15.0',
                          'NSHighResolutionCapable': True}, stream)
        # Ad-hoc signing supports local launch; it is not Developer ID signing or notarization.
        run('codesign', '--force', '--deep', '--sign', '-', app)
        run('codesign', '--verify', '--deep', '--strict', app)
        artifact = output / (name + '.pkg')
        run('pkgbuild', '--component', app, '--install-location', '/Applications', '--identifier',
            'com.syubadubinstudios.nusantarafishingmadness', '--version', version.split('-')[0], artifact)
        run('pkgutil', '--payload-files', artifact)
    with artifact.open('rb') as stream:
        checksum = hashlib.file_digest(stream, 'sha256').hexdigest()
    (output / (artifact.name + '.sha256')).write_text(f'{checksum}  {artifact.name}\n', encoding='utf-8')
    print('INSTALLER_READY:', artifact)


if __name__ == '__main__':
    main()
