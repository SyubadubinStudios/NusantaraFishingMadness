"""Island life exports from Rodin originals: wild undergrowth, banana plant and a rigged macaque.
Run through Blender MCP (Blender 5.2) with runpy; each asset is built in its own scene and the
user's active scene is restored. Rodin originals stay untouched in sources/rodin.
"""
from pathlib import Path
import contextlib
import math
import bpy
from mathutils import Vector

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / 'assets/models'


def import_rodin(name):
    bpy.ops.import_scene.gltf(filepath=str(ROOT / 'assets/sources/rodin' / (name + '.glb')))
    meshes = [o for o in bpy.context.scene.objects if o.type == 'MESH']
    bpy.ops.object.select_all(action='DESELECT')
    for obj in meshes:
        obj.select_set(True)
    bpy.context.view_layer.objects.active = meshes[0]
    if len(meshes) > 1:
        bpy.ops.object.join()
    obj = bpy.context.object
    # Drop the importer's empty parents but keep the world transform.
    world = obj.matrix_world.copy()
    obj.parent = None
    obj.matrix_world = world
    for other in [o for o in bpy.context.scene.objects if o != obj]:
        bpy.data.objects.remove(other)
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    obj.name = name
    return obj


def normalize(obj, height, turn=0.0):
    """Centre on the ground at the origin, face the game's forward (+Y here, -Z in glTF)."""
    vs = obj.data.vertices
    low = Vector([min(v.co[i] for v in vs) for i in range(3)])
    high = Vector([max(v.co[i] for v in vs) for i in range(3)])
    center = Vector(((low.x + high.x) / 2, (low.y + high.y) / 2, low.z))
    scale = height / (high.z - low.z)
    c, s = math.cos(turn), math.sin(turn)
    for v in vs:
        p = (v.co - center) * scale
        v.co = Vector((p.x * c - p.y * s, p.x * s + p.y * c, p.z))
    obj.data.update()


def budget(obj, triangles, texture=1024):
    count = sum(len(p.vertices) - 2 for p in obj.data.polygons)
    if count > triangles:
        mod = obj.modifiers.new('Game budget', 'DECIMATE')
        mod.ratio = triangles / count
        bpy.context.view_layer.objects.active = obj
        bpy.ops.object.modifier_apply(modifier=mod.name)
    for mat in obj.data.materials:
        for node in mat.node_tree.nodes:
            if node.type == 'TEX_IMAGE' and node.image and max(node.image.size) > texture:
                node.image.scale(texture, texture)
        if 'leaf' in obj.name or 'plant' in obj.name or 'undergrowth' in obj.name:
            # Thin leaves must render from both sides.
            mat.use_backface_culling = False
    return sum(len(p.vertices) - 2 for p in obj.data.polygons)


def export(name, animated=False):
    bpy.ops.object.select_all(action='SELECT')
    bpy.ops.export_scene.gltf(filepath=str(OUT / (name + '.glb')), use_selection=True, use_active_scene=True,
                              export_animations=animated, export_animation_mode='NLA_TRACKS', export_force_sampling=True,
                              export_optimize_animation_size=True, export_image_format='JPEG', export_jpeg_quality=88)
    print('EXPORTED', name)


def plant(source, name, height, triangles):
    obj = import_rodin(source)
    normalize(obj, height)
    obj.name = name
    print(name, 'triangles', budget(obj, triangles))


def segment_distance(p, a, b):
    ab = b - a
    t = max(0.0, min(1.0, (p - a).dot(ab) / max(ab.length_squared, 1e-9)))
    return (p - (a + ab * t)).length


def macaque():
    obj = import_rodin('macaque')
    # The original faces -Y; turn it so the head leads along +Y (glTF -Z, the game's forward).
    normalize(obj, .5, math.pi)
    print('macaque triangles', budget(obj, 4200))
    vs = obj.data.vertices
    low = Vector([min(v.co[i] for v in vs) for i in range(3)])
    high = Vector([max(v.co[i] for v in vs) for i in range(3)])
    length, height = high.y - low.y, high.z
    def at(x, y, z):  # fractions of width, length (0 = tail tip, 1 = nose) and height
        return Vector((x * (high.x - low.x) / 2, low.y + y * length, z * height))
    # Landmarks measured on the Rodin original by slicing and k-means clustering the feet:
    # narrow front legs, splayed hind legs, and a tail that loops down, back and over the rump.
    bones = {
        'Spine': (at(0, .425, .58), at(0, .815, .62), None),
        'Head': (at(0, .82, .64), at(0, 1, .68), 'Spine'),
        'Tail1': (at(0, .4, .58), at(0, .148, .32), 'Spine'),
        'Tail2': (at(0, .148, .32), at(0, .02, .7), 'Tail1'),
        'Tail3': (at(0, .02, .7), at(0, .12, .97), 'Tail2'),
        'Tail4': (at(0, .12, .97), at(0, .25, .97), 'Tail3'),
        'FrontLeft': (at(.05, .888, .62), at(.05, .9, 0), 'Spine'),
        'FrontRight': (at(-.05, .8, .62), at(-.05, .79, 0), 'Spine'),
        'HindLeft': (at(.68, .4, .5), at(.68, .388, 0), 'Spine'),
        'HindRight': (at(-.68, .5, .5), at(-.68, .495, 0), 'Spine'),
    }
    arm = bpy.data.armatures.new('macaque_rig')
    rig = bpy.data.objects.new('macaque_rig', arm)
    bpy.context.scene.collection.objects.link(rig)
    bpy.context.view_layer.objects.active = rig
    bpy.ops.object.mode_set(mode='EDIT')
    for name, (head, tail, parent) in bones.items():
        bone = arm.edit_bones.new(name)
        bone.head, bone.tail = head, tail
        if parent:
            bone.parent = arm.edit_bones[parent]
    bpy.ops.object.mode_set(mode='OBJECT')
    # Deterministic distance skinning: Rodin meshes are rarely manifold enough for bone heat.
    groups = {name: obj.vertex_groups.new(name=name) for name in bones}
    for v in vs:
        # Below the belly only leg bones may claim a vertex, so neighbouring legs never smear together.
        candidates = [n for n in bones if v.co.z > height * .33 or n.startswith(('Front', 'Hind', 'Tail'))]
        distances = sorted((segment_distance(v.co, bones[n][0], bones[n][1]), n) for n in candidates)
        nearest = distances[:2]
        legs = [name for _, name in nearest if name.startswith(('Front', 'Hind'))]
        if len(legs) == 2:
            nearest = distances[:1]  # two different legs never share a vertex
        raw = [(1 / max(d, .004) ** 6, name) for d, name in nearest]
        total = sum(w for w, _ in raw)
        for w, name in raw:
            groups[name].add([v.index], w / total, 'REPLACE')
    mod = obj.modifiers.new('Skin', 'ARMATURE')
    mod.object = rig
    obj.parent = rig
    bpy.context.scene.render.fps = 30
    rig.animation_data_create()
    pose = rig.pose.bones
    for bone in pose:
        bone.rotation_mode = 'XYZ'

    def clip(name, frames, keyer):
        action = bpy.data.actions.new('macaque_' + name)
        rig.animation_data.action = action
        for frame in range(1, frames + 2, 2):
            phase = (frame - 1) / frames * math.tau
            keyer(phase)
            for bone in pose:
                bone.keyframe_insert('rotation_euler', frame=frame)
        track = rig.animation_data.nla_tracks.new()
        track.name = name
        track.strips.new(name, 1, action)
        rig.animation_data.action = None

    def walk(phase):
        # Diagonal gait: front-left with hind-right, then the other pair.
        # Long fingers flatten if the arms swing as far as the legs.
        swing = math.sin(phase)
        pose['FrontLeft'].rotation_euler = (swing * .17, 0, 0)
        pose['HindRight'].rotation_euler = (swing * .32, 0, 0)
        pose['FrontRight'].rotation_euler = (-swing * .17, 0, 0)
        pose['HindLeft'].rotation_euler = (-swing * .32, 0, 0)
        pose['Spine'].rotation_euler = (0, math.sin(phase * 2) * .03, math.sin(phase) * .05)
        pose['Head'].rotation_euler = (math.sin(phase * 2) * .06, 0, -math.sin(phase) * .05)
        for i, name in enumerate(('Tail1', 'Tail2', 'Tail3', 'Tail4')):
            pose[name].rotation_euler = (math.sin(phase * 2 - i * .8) * .08, 0, math.sin(phase - i * .9) * (.08 + i * .04))

    def idle(phase):
        for name in ('FrontLeft', 'FrontRight', 'HindLeft', 'HindRight'):
            pose[name].rotation_euler = (0, 0, 0)
        pose['Spine'].rotation_euler = (math.sin(phase) * .02, 0, 0)
        # Looks around, then back, over the loop.
        pose['Head'].rotation_euler = (math.sin(phase * 2) * .05, 0, math.sin(phase) * .5)
        for i, name in enumerate(('Tail1', 'Tail2', 'Tail3', 'Tail4')):
            pose[name].rotation_euler = (math.sin(phase - i * .6) * .06, 0, math.sin(phase - i * .7) * (.06 + i * .04))

    clip('Walk', 24, walk)
    clip('Idle', 120, idle)
    bpy.context.scene.frame_set(1)


ASSETS = [
    ('wild-undergrowth', lambda: plant('island-undergrowth', 'wild-undergrowth', 1.15, 2600), False),
    ('banana-plant', lambda: plant('banana-plant', 'banana-plant', 3.1, 3000), False),
    ('macaque', macaque, True),
]

if __name__ == '__main__':
    previous = bpy.context.window.scene
    scenes = []
    try:
        log_path = ROOT / 'artifacts' / 'blender-island-life.log'
        log_path.parent.mkdir(exist_ok=True)
        with log_path.open('w') as log, contextlib.redirect_stdout(log):
            for name, build, animated in ASSETS:
                scene = bpy.data.scenes.new('NFM_Island_' + name)
                scenes.append(scene)
                bpy.context.window.scene = scene
                build()
                export(name, animated)
        print('ISLAND_LIFE_EXPORTED', [name for name, _, _ in ASSETS])
    finally:
        bpy.context.window.scene = previous
    bpy.data.libraries.write(str(ROOT / 'assets/sources/island-life.blend'), set(scenes), fake_user=True)
