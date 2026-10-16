"""Generate inventory from restored dependency metadata and shipped assets; stdlib only."""
import hashlib
import json
from pathlib import Path
import struct
import shutil
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[1]
packages = {}
for project in [*ROOT.glob('src/*/obj/project.assets.json'), *ROOT.glob('tests/*/obj/project.assets.json')]:
    data = json.loads(project.read_text(encoding='utf-8-sig'))
    for identity, library in data['libraries'].items():
        if library['type'] != 'package':
            continue
        package_id, version = identity.rsplit('/', 1)
        entry = {'id': package_id, 'version': version, 'license': 'REVIEW REQUIRED', 'url': ''}
        for cache in data['packageFolders']:
            directory = Path(cache) / library['path']
            nuspec = next(directory.glob('*.nuspec'), None)
            if nuspec:
                tree = ET.parse(nuspec)
                for node in tree.iter():
                    tag = node.tag.rsplit('}', 1)[-1]
                    if tag == 'license':
                        entry['license'] = (node.text or '') + (' (license file)' if node.attrib.get('type') == 'file' else '')
                    if tag == 'projectUrl':
                        entry['url'] = node.text or ''
                notice_paths = [p for p in directory.rglob('*') if p.is_file() and (p.name.lower().startswith(('license', 'notice', 'copying', 'third-party')))]
                for notice in notice_paths:
                    output = ROOT / 'docs/licenses' / package_id / version / notice.relative_to(directory)
                    output.parent.mkdir(parents=True, exist_ok=True)
                    shutil.copyfile(notice, output)
                break
        packages[identity] = entry

assets = []
rodin_catches = {entry['model'] for entry in json.loads((ROOT / 'assets/sources/catch-manifest.json').read_text()) if entry.get('rodinSource')}
for folder in ('models', 'textures', 'audio', 'art'):
    for path in sorted((ROOT / 'assets' / folder).rglob('*')):
        if not path.is_file():
            continue
        raw = path.read_bytes()
        item = {'path': path.relative_to(ROOT).as_posix(), 'bytes': len(raw), 'sha256': hashlib.sha256(raw).hexdigest()}
        if path.suffix == '.glb':
            length, kind = struct.unpack_from('<II', raw, 12)
            document = json.loads(raw[20:20+length])
            accessors = document.get('accessors', [])
            item['triangles'] = sum(accessors[p['indices']]['count'] // 3 for m in document.get('meshes', []) for p in m['primitives'] if 'indices' in p and p.get('mode', 4) == 4)
            item['animations'] = len(document.get('animations', []))
        generated = {'jukung.glb', 'fisherman.glb', 'rina.glb', 'bambang.glb', 'tono.glb', 'ojan.glb', 'ojol.glb', 'satpam.glb', 'bakso.glb', 'kucing.glb', 'robot.glb', 'pendaki.glb', 'karst.glb', 'coral-sand.png', 'weathered-teak.png', 'harbor-keyart.png'}
        generated |= rodin_catches | {'launch-voyage.jpg', 'loading-reef.jpg', 'loading-river.jpg', 'loading-myth.jpg', 'game-icon.png', 'game-icon-256.png', 'game-icon.ico', 'game-icon.icns'}
        textured = {'fishing-pier.glb', 'harbor-pavilion.glb', 'raft-house.glb', 'government-outpost.glb'}
        item['provenance'] = ('Rodin generation with Blender preparation; provider rights confirmation required' if path.name in generated
            else 'Original Blender geometry with Rodin teak texture; provider texture rights confirmation required' if path.name in textured
            else 'Original project asset; editable source in assets/sources')
        assets.append(item)
report = {'assets': assets, 'totalBytes': sum(x['bytes'] for x in assets), 'packages': sorted(packages.values(), key=lambda x: x['id'].lower())}
(ROOT / 'docs/asset-audit.json').write_text(json.dumps(report, indent=2) + '\n', encoding='utf-8')
lines = ['# Third-party notices', '', 'Generated from restored NuGet metadata by `python tools/audit-assets.py`. License expressions identify declared package terms; retain upstream license notices when redistributing. This inventory does not grant rights to generated assets or establish a license for the game.', '', '| Package | Version | Declared license |', '|---|---|---|']
for entry in report['packages']:
    lines.append(f"| {entry['id']} | {entry['version']} | {entry['license']} |")
lines += ['', '## Asset provenance', '', 'Game: Ariana Mischa Fadhila, Syubadubin Studios. Original Blender geometry, rigs, animations, analytic sky and synthesized audio include editable sources. Rodin supplied the jukung, eleven character meshes, ten creature anatomy sources shared by 33 catch variants, karst, sand/teak textures harbor key art, four promotional illustrations and the application icon; commercial redistribution remains subject to the account/provider terms. Pier, pavilion, raft-house and government-outpost exports embed the Rodin teak texture. See `assets/README.md` and `docs/asset-audit.json` for provenance, sizes and SHA-256 hashes.', '', 'The destination repository supplies the MIT license for project code (LICENSE). Generated assets remain subject to the provider terms recorded above; dependency licenses remain separate.', '']
(ROOT / 'docs/THIRD-PARTY-NOTICES.md').write_text('\n'.join(lines), encoding='utf-8')
print(f"AUDIT_OK: {len(assets)} assets, {report['totalBytes']} bytes; {len(packages)} packages")
