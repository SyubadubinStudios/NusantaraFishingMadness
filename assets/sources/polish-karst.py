"""Prepare a lighter, weathered derivative of the existing Rodin karst through Blender MCP.
Preserve the user's scene and the original asset. Runtime world-space shading supplies moss.
"""
from pathlib import Path
import math
import bpy

ROOT = Path(__file__).resolve().parents[2]
original = bpy.context.window.scene
scene = bpy.data.scenes.new('Weathered karst export')
try:
    bpy.context.window.scene = scene
    bpy.ops.import_scene.gltf(filepath=str(ROOT / 'assets/models/karst.glb'))
    meshes = [o for o in scene.objects if o.type == 'MESH']
    bpy.ops.object.select_all(action='DESELECT')
    for obj in meshes:
        obj.select_set(True)
    bpy.context.view_layer.objects.active = meshes[0]
    if len(meshes) > 1:
        bpy.ops.object.join()
    obj = bpy.context.object
    obj.name = 'Weathered limestone'
    world = obj.matrix_world.copy()
    obj.parent = None
    obj.matrix_world = world
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    smooth = obj.modifiers.new('Eroded edges', 'SMOOTH')
    smooth.factor = .7
    smooth.iterations = 3
    bpy.ops.object.modifier_apply(modifier=smooth.name)
    obj.data.calc_loop_triangles()
    decimate = obj.modifiers.new('Runtime budget', 'DECIMATE')
    decimate.ratio = min(1, 1800 / max(1, len(obj.data.loop_triangles)))
    bpy.ops.object.modifier_apply(modifier=decimate.name)
    # Keep the established normalized bounds so the Core waterline remains conservative.
    low = [min(v.co[i] for v in obj.data.vertices) for i in range(3)]
    high = [max(v.co[i] for v in obj.data.vertices) for i in range(3)]
    for v in obj.data.vertices:
        z = (v.co.z-low[2])/(high[2]-low[2])
        for i in (0, 1):
            v.co[i] = (v.co[i]-(low[i]+high[i])*.5)/(high[i]-low[i])
        twist = .15 * math.sin(z*4)
        x, y = v.co.x, v.co.y
        v.co.x = (x*math.cos(twist)-y*math.sin(twist)) * .9
        v.co.y = (x*math.sin(twist)+y*math.cos(twist)) * .9
        v.co.z = z
    mat = bpy.data.materials.new('Warm weathered limestone')
    mat.use_nodes = True
    bsdf = next(n for n in mat.node_tree.nodes if n.type == 'BSDF_PRINCIPLED')
    bsdf.inputs['Base Color'].default_value = (.32, .34, .28, 1)
    bsdf.inputs['Roughness'].default_value = .88
    obj.data.materials.clear()
    obj.data.materials.append(mat)
    for p in obj.data.polygons:
        p.use_smooth = True
    bpy.ops.export_scene.gltf(filepath=str(ROOT/'assets/models/karst-weathered.glb'),
        export_format='GLB', use_selection=True, use_active_scene=True, export_animations=False)
    bpy.data.libraries.write(str(ROOT/'assets/sources/weathered-karst.blend'), {scene})
    obj.data.calc_loop_triangles()
    print(f'WEATHERED_KARST_OK: {len(obj.data.loop_triangles)} triangles')
finally:
    bpy.context.window.scene = original
    for obj in list(scene.objects):
        bpy.data.objects.remove(obj, do_unlink=True)
    bpy.data.scenes.remove(scene)
