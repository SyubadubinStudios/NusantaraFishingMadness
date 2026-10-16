"""Check shipped glTF structure and skin weights without a GPU or Blender."""
import json
import math
import struct
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
results = []
for path in sorted((ROOT/'assets/models').glob('*.glb')):
    raw = path.read_bytes()
    magic, version, total = struct.unpack_from('<III', raw)
    assert magic == 0x46546C67 and version == 2 and total == len(raw), path
    length = struct.unpack_from('<I', raw, 12)[0]
    data = json.loads(raw[20:20+length])
    binary = raw[28+length:]
    assert len(data['scenes']) == 1, f'{path}: unrelated Blender scenes exported'
    assert all('uri' not in image for image in data.get('images', [])), f'{path}: external image'
    triangles = sum(data['accessors'][p['indices']]['count']//3 for m in data['meshes'] for p in m['primitives'])
    clips = [a['name'] for a in data.get('animations', [])]
    if path.stem in ('fisherman', 'rina', 'bambang', 'tono', 'ojan', 'ojol', 'satpam', 'bakso', 'kucing', 'robot', 'pendaki'):
        assert set(clips) == {'Idle', 'Walk', 'Fishing'}, clips
        assert triangles <= 7500 and len(data['skins']) == 1
        assert len(data['skins'][0]['joints']) == 16
    if path.stem == 'macaque':
        assert set(clips) == {'Idle', 'Walk'} and triangles <= 4500, (clips, triangles)
        assert len(data['skins']) == 1 and len(data['skins'][0]['joints']) == 10, path
    if path.stem in ('wild-undergrowth', 'banana-plant'):
        # Instanced and re-transformed every frame for wind sway: keep them light and unrigged.
        assert triangles <= 3200 and not data.get('skins') and not clips, (path, triangles)
    if path.stem.startswith('catch-') and data.get('skins'):
        assert clips == ['Swim'] and len(data['skins']) == 1, path
        assert len(data['skins'][0]['joints']) == 4, path
    if data.get('skins'):
        for mesh in data['meshes']:
            for primitive in mesh['primitives']:
                if 'WEIGHTS_0' not in primitive['attributes']:
                    continue  # Unskinned accessories may accompany a rigged body.
                weights = data['accessors'][primitive['attributes']['WEIGHTS_0']]
                view = data['bufferViews'][weights['bufferView']]
                assert weights['componentType'] == 5126 and weights['type'] == 'VEC4'
                offset = view.get('byteOffset', 0) + weights.get('byteOffset', 0)
                for i in range(weights['count']):
                    w = struct.unpack_from('<4f', binary, offset + i*view.get('byteStride', 16))
                    assert all(math.isfinite(x) and x >= 0 for x in w) and abs(sum(w)-1) < .001
    results.append({'asset': path.name, 'triangles': triangles, 'animations': clips, 'bytes': total})
report = ROOT/'docs/validation/visual-assets.json'
report.write_text(json.dumps({'checks': 'GLB structure, isolated scenes, embedded images, character budgets, creature Swim clips and normalized skin weights', 'assets': results}, indent=2)+'\n')
print(f'VISUAL_ASSETS_OK: {len(results)} models; character and creature rigs, clips and weights validated')
