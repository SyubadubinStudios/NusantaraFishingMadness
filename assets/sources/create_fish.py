"""Run in Blender 5.x. Creates an isolated, rigged stylized tuna and exports GLB."""
import bpy
import math
from pathlib import Path

root = Path(__file__).resolve().parents[2]
target = root / "assets" / "models"
target.mkdir(parents=True, exist_ok=True)
original_scene = bpy.context.window.scene
scene = bpy.data.scenes.new("Nusantara_Tuna_Asset")
bpy.context.window.scene = scene

def material(name, color, metallic=0):
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    shader = next(n for n in mat.node_tree.nodes if n.type == 'BSDF_PRINCIPLED')
    shader.inputs['Base Color'].default_value = (*color, 1)
    shader.inputs['Metallic'].default_value = metallic
    shader.inputs['Roughness'].default_value = .4
    return mat

blue = material('Tuna_ocean_blue', (.025, .28, .40), .3)
gold = material('Tuna_yellow_fins', (.95, .62, .11))
eye = material('Tuna_eyes', (.008, .016, .025))
meshes = []

def ellipsoid(name, pos, scale, mat):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=16, ring_count=8, location=pos)
    obj = bpy.context.object
    obj.name = name
    obj.scale = scale
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    obj.data.materials.append(mat)
    meshes.append(obj)
    return obj

ellipsoid('Tuna_body', (0, 0, 0), (1.15, .32, .43), blue)
for side in [-1, 1]:
    ellipsoid('Tuna_eye', (.77, side * .265, .13), (.055, .035, .055), eye)

def fin(name, verts):
    mesh = bpy.data.meshes.new(name)
    mesh.from_pydata(verts, [], [(0, 1, 2)])
    mesh.materials.append(gold)
    obj = bpy.data.objects.new(name, mesh)
    scene.collection.objects.link(obj)
    solid = obj.modifiers.new('Fin_thickness', 'SOLIDIFY')
    solid.thickness = .035
    meshes.append(obj)

fin('Tail_upper', [(-.95,0,0),(-1.65,0,.65),(-1.45,0,0)])
fin('Tail_lower', [(-.95,0,0),(-1.45,0,0),(-1.65,0,-.65)])
fin('Dorsal', [(-.45,0,.3),(-.15,0,.85),(.3,0,.35)])
for side in [-1,1]:
    fin('Pectoral', [(.3,side*.2,0),(-.5,side*.7,-.1),(-.1,side*.2,-.2)])

bpy.ops.object.select_all(action='DESELECT')
bpy.ops.object.armature_add()
rig = bpy.context.object
rig.name = 'Tuna_rig'
bpy.ops.object.mode_set(mode='EDIT')
body = rig.data.edit_bones[0]
body.name = 'Body'
body.head = (.8,0,0)
body.tail = (-.8,0,0)
tail = rig.data.edit_bones.new('Tail')
tail.head = (-.8,0,0)
tail.tail = (-1.65,0,0)
tail.parent = body
bpy.ops.object.mode_set(mode='OBJECT')
for obj in meshes:
    obj.parent = rig
    deform = obj.modifiers.new('Swim_deform', 'ARMATURE')
    deform.object = rig
    groups = {name: obj.vertex_groups.new(name=name) for name in ['Body','Tail']}
    for vertex in obj.data.vertices:
        world_x = vertex.co.x + obj.location.x
        tail_weight = max(0, min(1, (-world_x - .6) / .65))
        groups['Body'].add([vertex.index], 1-tail_weight, 'REPLACE')
        groups['Tail'].add([vertex.index], tail_weight, 'REPLACE')

bone = rig.pose.bones['Tail']
bone.rotation_mode = 'XYZ'
for frame, angle in [(1,0),(9,.35),(17,0),(25,-.35),(33,0)]:
    bone.rotation_euler.z = angle
    bone.keyframe_insert(data_path='rotation_euler', frame=frame)
scene.frame_start = 1
scene.frame_end = 33
scene.render.fps = 24
scene.frame_set(1)
for obj in bpy.data.objects:
    obj.select_set(False)
for obj in [rig, *meshes]:
    obj.select_set(True)
bpy.context.view_layer.objects.active = rig
bpy.ops.export_scene.gltf(filepath=str(target / 'tuna.glb'), export_format='GLB', use_selection=True, use_active_scene=True, export_animations=True)
# Preserve the user's original open scene; asset data remains available in its own scene.
bpy.context.window.scene = original_scene
print('Exported rigged tuna:', target / 'tuna.glb')
