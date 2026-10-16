"""Blender MCP authoring source for species silhouettes and collectible objects.
All dimensions are metres. Separate scenes/exports preserve the user's active scene.
These are original game meshes, not claims of photorealistic finished creature art.
"""
import bpy
import math
import json
import re
from pathlib import Path
from mathutils import Vector

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT/'assets/models'
TAU = math.tau

def mat(name, rgb, alpha=1, metal=0, glow=0):
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    shader = next(n for n in m.node_tree.nodes if n.type == 'BSDF_PRINCIPLED')
    shader.inputs['Base Color'].default_value = (*rgb, alpha)
    shader.inputs['Roughness'].default_value = .38
    shader.inputs['Metallic'].default_value = metal
    shader.inputs['Alpha'].default_value = alpha
    shader.inputs['Emission Color'].default_value = (*rgb, 1)
    shader.inputs['Emission Strength'].default_value = glow
    if alpha < 1:
        m.surface_render_method = 'DITHERED'
    return m

def sphere(name, pos, size, material, segments=16):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=segments, ring_count=8, radius=1, location=pos)
    obj = bpy.context.object
    obj.name = name
    obj.scale = size
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    obj.data.materials.append(material)
    for p in obj.data.polygons: p.use_smooth = True
    return obj

def box(name, pos, size, material, bevel=.03):
    bpy.ops.mesh.primitive_cube_add(size=1, location=pos)
    obj = bpy.context.object
    obj.name = name
    obj.scale = size
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    obj.data.materials.append(material)
    if bevel:
        mod = obj.modifiers.new('Edge highlights', 'BEVEL')
        mod.width = bevel; mod.segments = 2
        bpy.ops.object.modifier_apply(modifier=mod.name)
    return obj

def mesh(name, points, faces, material):
    data = bpy.data.meshes.new(name)
    data.from_pydata(points, [], faces)
    data.materials.append(material)
    obj = bpy.data.objects.new(name, data)
    bpy.context.scene.collection.objects.link(obj)
    return obj

def tube(name, points, widths, material, sides=8):
    points = [Vector(p) for p in points]
    verts, faces = [], []
    for i, p in enumerate(points):
        tangent = points[min(i+1,len(points)-1)]-points[max(0,i-1)]
        q = tangent.to_track_quat('Z', 'Y')
        for j in range(sides):
            a = j/sides*TAU
            verts.append(p + q @ Vector((math.cos(a)*widths[i], math.sin(a)*widths[i], 0)))
        if i:
            for j in range(sides):
                a = (i-1)*sides+j; b = (i-1)*sides+(j+1)%sides
                faces.append((a,b,b+sides,a+sides))
    faces += [tuple(reversed(range(sides))), tuple(range(len(verts)-sides,len(verts)))]
    obj = mesh(name, verts, faces, material)
    for p in obj.data.polygons: p.use_smooth=True
    return obj

def fin(name, points, material):
    thickness=.016
    verts=[(x-thickness,y,z) for x,y,z in points]+[(x+thickness,y,z) for x,y,z in points]
    n=len(points)
    return mesh(name,verts,[tuple(reversed(range(n))),tuple(range(n,2*n))]+[(i,(i+1)%n,(i+1)%n+n,i+n) for i in range(n)],material)

def eyes(y, z, width, dark, ivory):
    for sign in (-1,1):
        sphere('eye', (sign*width,y,z), (.065,.085,.07), ivory,12)
        sphere('pupil',(sign*(width+.035),y-.025,z),(.035,.045,.043),dark,12)

def fish(identifier, body, accent, dark, ivory):
    whale = identifier in ('lumba','dugong','paus-kubangan','boss-awan')
    eel = identifier in ('belut','boss-gunung')
    dragon = identifier in ('patenggang','boss-toba','boss-gua','boss-kelimutu')
    cat = identifier in ('lele','ghost','radioaktif','penunggu','boss-kubangan','boss-tambang','boss-dimensi')
    deep = identifier in ('gurame','kerapu','raja','shadow')
    if eel or dragon:
        points=[(math.sin(i*.55)*.18, .9-i*.19, .1+math.sin(i*.38)*.08) for i in range(15)]
        tube('serpentine-body',points,[.025+.22*math.sin(math.pi*(i+1)/17) for i in range(15)],body,12)
        head_y=-1.6
    else:
        sphere('body',(0,0,0),(.32 if not deep else .23,.92,.29 if not deep else .53),body)
        sphere('belly',(0,-.08,-.14),(.29,.70,.18 if not deep else .29),ivory)
        head_y=-.7
    sphere('head',(0,head_y,.05),(.29,.34,.25),body)
    eyes(head_y-.12,.15,.255,dark,ivory)
    if not eel:
        for sign in (-1,1):
            tip = 1.05 if identifier=='terbang' else .65
            fin('pectoral-fin',[(sign*.2,-.24,0),(sign*tip,.2,-.07),(sign*.22,.4,-.03)],accent)
        if whale:
            fin('tail-flukes',[(0,.78,0),(-.6,1.1,0),(0,1.0,.06),(.6,1.1,0)],accent)
            sphere('muzzle',(0,-1.0,.01),(.17,.28,.13),body)
        else:
            fin('tail-fin',[(0,.7,0),(0,1.25,.42),(0,1.12,0),(0,1.25,-.42)],accent)
        fin('dorsal-fin',[(0,-.2,.23),(0,.05,.62 if identifier=='hiu' else .43),(0,.62,.22)],accent)
    if cat:
        for sign in (-1,1):
            for j in range(2): tube('whisker',[(sign*.13,-.95,.02),(sign*.4,-1.12-j*.07,-.02),(sign*.7,-.9-j*.12,-.12)],[.018,.013,.004],accent,6)
    if dragon or identifier=='raja':
        for sign in (-1,1): tube('crown-horn',[(sign*.15,head_y,.23),(sign*.22,head_y+.1,.55),(sign*.27,head_y+.23,.72)],[.06,.035,.002],accent)
        for i in range(7): sphere('gilded-scale',(0,i*.2-.5,.25),(.07,.10,.07),accent,8)
    if identifier=='gurame': sphere('third-eye',(0,-.82,.36),(.09,.07,.09),dark)
    if identifier=='tv':
        box('television',(0,-.45,.03),(.68,.6,.5),accent)
        box('screen',(0,-.765,.03),(.52,.025,.34),dark,.01)
        for sign in (-1,1):tube('antenna',[(0,-.4,.28),(sign*.25,-.4,.65)],[.02,.01],accent)
    if identifier in ('arwana','tenggiri','kerapu','hiu','patenggang'):
        for sign in (-1,1):
            for i in range(6): sphere('flank-mark',(sign*.30,-.35+i*.16,.07),(.012,.035,.12),accent,8)

def cephalopod(identifier, body, accent, dark, ivory):
    squid = identifier in ('disco','roket')
    sphere('mantle',(0,.1,.3),(.3,.72 if squid else .42,.32 if squid else .52),body)
    eyes(-.26,.24,.25,dark,ivory)
    for arm in range(8):
        a=arm/8*TAU
        points=[]
        for i in range(10):
            r=.22+i*.075
            angle=a+i*.055
            points.append((math.cos(angle)*r,math.sin(angle)*r-.15,-.1-i*.025+max(0,i-6)*.075))
        tube('tentacle-'+str(arm),points,[.095*(1-i/11) for i in range(10)],body)
        for i in (2,4,6):sphere('sucker',Vector(points[i])+Vector((0,0,.035)),(.035,.035,.019),ivory,8)
    if squid:
        for sign in (-1,1):fin('mantle-fin',[(0,.7,.2),(sign*.55,.7,.1),(sign*.2,0,.05)],accent)
    if identifier in ('batik','boss-kristal'):
        for i in range(12):
            a=i/12*TAU
            sphere('batik-motif',(math.cos(a)*.29,.1+math.sin(a)*.4,.42),(.06,.045,.03),accent,8)
    if identifier=='dj':
        for sign in (-1,1):sphere('headphone',(sign*.34,.02,.38),(.08,.13,.16),dark)
        tube('headband',[(-.34,0,.45),(-.22,0,.82),(.22,0,.82),(.34,0,.45)],[.04]*4,dark)
    if identifier=='profesor':
        for sign in (-1,1):box('spectacle',(sign*.21,-.33,.32),(.22,.025,.16),dark,.03)
        box('book',(0,-.8,-.12),(.4,.35,.09),accent)

def crustacean(identifier, body, accent, dark, ivory):
    crab=identifier=='kepiting'
    sphere('carapace',(0,0,.1),(.4 if crab else .22,.3 if crab else .6,.2),body)
    for sign in (-1,1):
        for i in range(4):
            y=-.2+i*.16
            tube('leg',[(sign*.15,y,.06),(sign*.48,y+.16,-.05),(sign*.65,y+.2,-.22)],[.04,.03,.008],body)
        tube('claw-arm',[(sign*.25,-.2,.1),(sign*.48,-.4,.1),(sign*.5,-.65,.18)],[.08,.065,.04],body)
        sphere('claw',(sign*.5,-.72,.18),(.17,.24,.11),accent)
        fin('pincer',[(sign*.5,-.75,.2),(sign*.37,-1,.2),(sign*.52,-.9,.2)],ivory)
        sphere('eye',(sign*.12,-.3,.3),(.045,.045,.045),dark,8)
    if not crab:
        for i in range(5):sphere('tail-segment',(0,.4+i*.15,.04-i*.02),(.22-i*.025,.12,.14),accent)
        for sign in (-1,1):tube('antenna',[(sign*.12,-.45,.15),(sign*.28,-.8,.4),(sign*.4,-1.3,.4)],[.018,.014,.003],ivory,6)

def reptile(identifier, body, accent, dark, ivory):
    frog=identifier in ('kodok','ninja')
    turtle=identifier=='kura'
    sphere('torso',(0,0,.05),(.37,.4 if frog or turtle else .7,.23),body)
    if turtle:
        sphere('shell',(0,.08,.22),(.44,.53,.25),accent)
        for i in range(5):box('shell-ridge',(0,-.25+i*.15,.46),(.15,.1,.035),ivory,.01)
    for sign in (-1,1):
        for y in (-.24,.3):
            tube('limb',[(sign*.2,y,0),(sign*.5,y+.12,-.08),(sign*.42,y-.15,-.18)],[.12,.095,.04],body)
            for toe in range(3):tube('toe',[(sign*.42,y-.15,-.18),(sign*(.36+toe*.06),y-.3,-.19)],[.025,.01],accent,6)
    sphere('head',(0,-.48,.12),(.3,.26,.17),body)
    eyes(-.56,.26,.23,dark,ivory)
    if not frog and not turtle:
        tube('tail',[(0,.4,0),(0,.95,-.02),(.18,1.45,-.06),(.3,1.75,-.06)],[.22,.16,.07,.005],body)
        sphere('snout',(0,-.85,.08),(.19,.4,.1),accent)
        for i in range(8):fin('back-scute',[(0,-.2+i*.18,.2),(0,-.15+i*.18,.36),(0,i*.18,.2)],accent)
    if identifier=='ninja':
        box('headband',(0,-.65,.21),(.57,.04,.08),dark,.01)
        fin('headband-tie',[(.25,-.5,.24),(.55,-.2,.2),(.6,-.4,.3)],dark)

def collectible(identifier, body, accent, dark, ivory):
    if identifier=='keris':
        tube('wavy-blade',[(math.sin(i*1.3)*.055,0,i*.12) for i in range(10)],[.055-i*.005 for i in range(10)],accent,4)
        box('guard',(0,0,0),(.4,.10,.09),body)
        tube('carved-handle',[(0,0,0),(0,0,-.25),(.10,0,-.35)],[.07,.065,.04],body)
    elif identifier=='mahkota':
        for i in range(12):
            a=i/12*TAU; b=(i+1)/12*TAU
            tube('crown-band',[(math.cos(a)*.45,math.sin(a)*.45,0),(math.cos(b)*.45,math.sin(b)*.45,0)],[.055,.055],accent)
            tube('crown-point',[(math.cos(a)*.45,math.sin(a)*.45,0),(math.cos(a)*.37,math.sin(a)*.37,.4 if i%2 else .55)],[.06,.005],accent)
            sphere('gem',(math.cos(a)*.46,math.sin(a)*.46,.06),(.05,.05,.08),body,8)
    elif identifier=='peta':
        box('parchment',(0,0,0),(.9,.7,.035),ivory,.01)
        for sign in (-1,1):tube('scroll',[(sign*.45,-.4,.03),(sign*.45,.4,.03)],[.07,.07],body)
        tube('route',[(-.3,-.2,.025),(-.1,.15,.025),(.1,-.1,.025),(.3,.2,.025)],[.015]*4,dark,6)
    elif identifier=='sandal':
        box('sole',(0,0,0),(.36,.8,.07),body,.08)
        for sign in (-1,1):tube('strap',[(0,-.25,.04),(sign*.15,.05,.16),(sign*.18,.2,.04)],[.03]*3,accent)
    elif identifier=='kasur':
        box('mattress',(0,0,0),(1,1.7,.22),body,.1)
        box('pillow',(0,-.5,.18),(.75,.36,.15),ivory,.06)
        for i in range(5):box('stitch',(0,-.3+i*.22,.115),(.9,.012,.012),accent,.004)
        for sign in (-1,1):fin('wing',[(sign*.4,0,0),(sign*1.1,.3,.3),(sign*.5,.6,0)],ivory)
    elif identifier=='motor':
        for y in (-.65,.65):
            bpy.ops.mesh.primitive_torus_add(major_segments=20,minor_segments=8,major_radius=.24,minor_radius=.06,location=(0,y,0),rotation=(0,math.pi/2,0))
            bpy.context.object.data.materials.append(dark)
        tube('frame',[(0,-.65,0),(0,-.3,.6),(0,.4,.2),(0,.65,0)],[.055]*4,accent)
        box('seat',(0,.15,.55),(.3,.6,.12),dark)
        box('tank',(0,-.25,.4),(.32,.4,.32),body)
        tube('handlebar',[(-.3,-.4,.7),(.3,-.4,.7)],[.04,.04],accent)
    elif identifier=='peti':
        box('chest',(0,0,0),(.9,.6,.55),body,.06)
        for x in (-.32,.32):box('iron-band',(x,0,.02),(.08,.64,.6),accent,.01)
        box('lock',(0,-.32,0),(.14,.045,.18),accent)
        for i in range(4):box('plank',(0,-.305,-.2+i*.14),(.86,.025,.012),dark,.002)
    else:
        box('cabinet',(0,0,.25),(.75,.65,1.4),body,.05)
        if identifier=='kulkas':
            box('door',(0,-.345,.25),(.7,.045,1.3),ivory)
            box('divider',(0,-.375,.55),(.7,.015,.025),dark)
            box('handle',(.23,-.4,.25),(.04,.06,.27),accent)
        else:
            box('screen',(0,-.34,.55),(.5,.03,.36),dark)
            box('keypad',(0,-.36,.15),(.4,.04,.2),accent)
            box('cash-slot',(0,-.36,-.1),(.43,.04,.04),dark)

COLORS={'lele':(.14,.22,.23),'gabus':(.28,.33,.15),'arwana':(.65,.25,.10),'tuna':(.05,.25,.39),
 'tenggiri':(.35,.48,.5),'kerapu':(.4,.29,.16),'gurita':(.43,.24,.16),'batik':(.42,.12,.2),'dj':(.25,.08,.55),
 'profesor':(.25,.4,.45),'disco':(.1,.7,.8),'roket':(.8,.15,.1),'kepiting':(.5,.16,.08),'lobster':(.6,.3,.08),
 'udang':(.45,.8,.9),'kodok':(.8,.58,.06),'ninja':(.08,.25,.10),'kura':(.2,.3,.13),'buaya':(.17,.26,.10),
 'biawak':(.78,.75,.66),'lumba':(.2,.43,.54),'dugong':(.37,.39,.37),'sandal':(.07,.4,.5),'kasur':(.35,.4,.65),
 'kulkas':(.6,.65,.62),'motor':(.5,.04,.05),'atm':(.1,.35,.5),'keris':(.22,.1,.04),'peti':(.25,.11,.04),
 'mahkota':(.5,.04,.05),'peta':(.5,.32,.13),'radioaktif':(.18,.62,.03),'gurame':(.4,.3,.5),'belut':(.1,.3,.6),
 'tv':(.28,.35,.17),'shadow':(.03,.03,.06),'ghost':(.55,.8,.83),'phantom':(.3,.12,.55),'raja':(.8,.53,.07),
 'patenggang':(.15,.58,.4),'penunggu':(.22,.18,.13),'paus-kubangan':(.3,.19,.45),'hiu':(.22,.32,.39),'terbang':(.3,.6,.8)}
COLORS.update({'boss-selatan':(.45,.12,.10),'boss-ampat':(.18,.12,.37),'boss-toba':(.12,.5,.32),
    'boss-kelimutu':(.12,.55,.65),'boss-kalimantan':(.18,.25,.08),'boss-jakarta':(.35,.22,.12),
    'boss-kubangan':(.16,.22,.23),'boss-terjun':(.23,.45,.58),'boss-gua':(.32,.19,.48),
    'boss-tambang':(.27,.64,.08),'boss-awan':(.34,.52,.72),'boss-gunung':(.6,.16,.08),
    'boss-kristal':(.42,.18,.64),'boss-dimensi':(.45,.30,.08)})

RODIN_FAMILIES = {
    **dict.fromkeys(['lele','ghost','radioaktif','penunggu','boss-kubangan','boss-tambang','boss-dimensi'],'catfish'),
    **dict.fromkeys(['patenggang','boss-toba','boss-kelimutu','boss-gua','raja'],'dragon'),
    **dict.fromkeys(['gurita','batik','dj','profesor','phantom','boss-selatan','boss-ampat','boss-kristal'],'octopus'),
    'disco':'squid','roket':'squid','kepiting':'crab','kodok':'frog','ninja':'frog','kura':'turtle',
    'buaya':'crocodile','biawak':'crocodile','boss-kalimantan':'crocodile','boss-jakarta':'crocodile',
    'dugong':'dugong','paus-kubangan':'whale','boss-awan':'whale'
}

def rodin_creature(identifier):
    family=RODIN_FAMILIES.get(identifier)
    if not family:return False
    path=ROOT/'assets/sources/rodin'/('creature-'+family+'.glb')
    if not path.exists():return False
    bpy.ops.import_scene.gltf(filepath=str(path))
    objects=[o for o in bpy.context.scene.objects if o.type=='MESH']
    bpy.ops.object.select_all(action='DESELECT')
    for obj in objects:obj.select_set(True)
    bpy.context.view_layer.objects.active=objects[0]
    bpy.ops.object.join();obj=bpy.context.object;obj.name='body'
    bpy.ops.object.transform_apply(location=True,rotation=True,scale=True)
    low=Vector([min(v.co[i] for v in obj.data.vertices) for i in range(3)])
    high=Vector([max(v.co[i] for v in obj.data.vertices) for i in range(3)])
    center=(low+high)/2;size=high-low
    if family in ('catfish','dragon','squid','crocodile','dugong','whale') and size.x>size.y:
        for v in obj.data.vertices:
            p=v.co-center;v.co=Vector((-p.y,p.x,p.z))*2/max(size)
    else:
        for v in obj.data.vertices:v.co=(v.co-center)*2/max(size)
    triangles=sum(len(p.vertices)-2 for p in obj.data.polygons)
    modifier=obj.modifiers.new('Creature game budget','DECIMATE');modifier.ratio=min(1,4200/triangles)
    bpy.ops.object.modifier_apply(modifier=modifier.name)
    tint=COLORS.get(identifier,(.35,.4,.5))
    for material in obj.data.materials:
        shader=next(n for n in material.node_tree.nodes if n.type=='BSDF_PRINCIPLED')
        shader.inputs['Base Color'].default_value=tuple(.65+.35*c for c in tint)+(1,)
        shader.inputs['Roughness'].default_value=.4
        if identifier in ('ghost','phantom'):
            shader.inputs['Alpha'].default_value=.4;material.surface_render_method='DITHERED'
        if identifier in ('disco','radioaktif','boss-kristal'):
            shader.inputs['Emission Color'].default_value=(*tint,1)
            shader.inputs['Emission Strength'].default_value=.35
        for node in material.node_tree.nodes:
            if node.type=='TEX_IMAGE' and node.image and max(node.image.size)>512:node.image.scale(512,512)
    # Sinuous species get a real four-bone swim loop; radial creatures retain their
    # original anatomy and use whole-body bobbing in the renderer.
    if family in ('catfish','dragon','crocodile','dugong','whale'):
        arm=bpy.data.armatures.new(identifier+'_swim')
        rig=bpy.data.objects.new(identifier+'_rig',arm);bpy.context.scene.collection.objects.link(rig)
        bpy.context.view_layer.objects.active=rig;obj.select_set(False);rig.select_set(True)
        bpy.ops.object.mode_set(mode='EDIT')
        for i in range(4):
            bone=arm.edit_bones.new('Spine'+str(i));bone.head=(0,-1+i*.5,0);bone.tail=(0,-.5+i*.5,0)
            if i:bone.parent=arm.edit_bones['Spine'+str(i-1)]
        bpy.ops.object.mode_set(mode='OBJECT')
        groups=[obj.vertex_groups.new(name='Spine'+str(i)) for i in range(4)]
        for v in obj.data.vertices:
            t=max(0,min(3,(v.co.y+.75)*2));a=int(t);b=min(3,a+1)
            groups[a].add([v.index],1-(t-a),'REPLACE')
            if b!=a:groups[b].add([v.index],t-a,'REPLACE')
        modifier=obj.modifiers.new('Swim skin','ARMATURE');modifier.object=rig;obj.parent=rig
        bpy.context.scene.render.fps=30
        for frame in range(1,62,3):
            phase=(frame-1)/60*TAU
            for i,bone in enumerate(rig.pose.bones):
                bone.rotation_mode='XYZ';bone.rotation_euler=(0,0,math.sin(phase-i*.7)*(.018+i*.018))
                bone.keyframe_insert('rotation_euler',frame=frame)
        action=rig.animation_data.action;action.name=identifier+'_Swim'
        track=rig.animation_data.nla_tracks.new();track.name='Swim';track.strips.new('Swim',1,action)
        rig.animation_data.action=None
        bpy.context.scene.frame_set(1)
    return True

def build(identifier, kind):
    rgb=COLORS.get(identifier,(.27,.35,.42))
    body=mat(identifier+'-body',rgb,.38 if identifier in ('udang','ghost','phantom') else 1,glow=.35 if identifier in ('radioaktif','disco') else 0)
    accent=mat(identifier+'-markings',(.74,.49,.15) if identifier not in ('ninja','hiu') else (.15,.2,.22),metal=.65 if kind=='Artifact' else .05)
    dark=mat(identifier+'-ink',(.018,.025,.028))
    ivory=mat(identifier+'-ivory',(.68,.65,.48))
    detailed=rodin_creature(identifier)
    if detailed:
        if identifier=='dj':
            for sign in (-1,1):sphere('headphone',(sign*.27,0,.32),(.065,.10,.12),dark)
            tube('headband',[(-.27,0,.35),(-.19,0,.63),(.19,0,.63),(.27,0,.35)],[.025]*4,dark)
        if identifier=='batik':
            for i in range(12):
                a=i/12*TAU;sphere('batik-motif',(math.cos(a)*.22,math.sin(a)*.2,.35),(.025,.025,.016),accent,8)
        if identifier=='profesor':box('book',(0,-.7,-.1),(.3,.24,.05),accent)
        bpy.ops.object.select_all(action='SELECT')
        animated=any(o.type=='ARMATURE' for o in bpy.context.scene.objects)
        bpy.ops.export_scene.gltf(filepath=str(OUT/('catch-'+identifier+'.glb')),use_selection=True,use_active_scene=True,
            export_animations=animated,export_animation_mode='NLA_TRACKS',export_force_sampling=True)
        return
    if kind in ('Artifact','Junk'):collectible(identifier,body,accent,dark,ivory)
    elif kind=='Cephalopod' or identifier in ('phantom','boss-selatan','boss-ampat','boss-kristal'):cephalopod(identifier,body,accent,dark,ivory)
    elif kind=='Crustacean':crustacean(identifier,body,accent,dark,ivory)
    elif kind in ('Amphibian','Reptile') or identifier in ('boss-kalimantan','boss-jakarta'):reptile(identifier,body,accent,dark,ivory)
    else:fish(identifier,body,accent,dark,ivory)
    # Preserve moving appendages as individually addressable node groups.
    objects=list(bpy.context.scene.objects)
    for obj in objects:
        if obj.type!='MESH':continue
        group='motion' if obj.name.startswith(('tentacle','tail-fin','tail-flukes','pectoral-fin')) else 'body'
        obj['part_group']=group
    for group in ('body','motion'):
        objects=[o for o in bpy.context.scene.objects if o.type=='MESH' and o.get('part_group')==group]
        if not objects:continue
        bpy.ops.object.select_all(action='DESELECT')
        for o in objects:o.select_set(True)
        bpy.context.view_layer.objects.active=objects[0]
        bpy.ops.object.join()
        bpy.context.object.name=group
    bpy.ops.object.select_all(action='SELECT')
    bpy.ops.export_scene.gltf(filepath=str(OUT/('catch-'+identifier+'.glb')),use_selection=True,use_active_scene=True,export_animations=False)

def main():
    text=(ROOT/'src/NusantaraFishing.Core/Catalog.cs').read_text(encoding='utf-8')
    species=re.findall(r'S\("([^"]+)", "([^"]+)", CatchKind\.(\w+)',text)
    region_ids=re.findall(r'new\("([^"]+)", "[^"]+", "[^"]+", \d+, \d+, "#[A-Fa-f0-9]+", "[^"]+"\)',text)
    entries=[(i,n,k) for i,n,k in species]+[('boss-'+i,'Regional boss '+i,'Boss') for i in region_ids]
    previous=bpy.context.window.scene
    manifest=[]
    scenes=[]
    try:
        for identifier,name,kind in entries:
            scene=bpy.data.scenes.new('NFM_Catch_'+identifier)
            scenes.append(scene)
            bpy.context.window.scene=scene
            build(identifier,kind)
            triangles=sum(len(p.vertices)-2 for o in scene.objects if o.type=='MESH' for p in o.data.polygons)
            family=RODIN_FAMILIES.get(identifier)
            original=('creature-'+family+'.glb') if family and (ROOT/'assets/sources/rodin'/('creature-'+family+'.glb')).exists() else None
            manifest.append({'id':identifier,'name':name,'kind':kind,'model':'catch-'+identifier+'.glb','triangles':triangles,'rodinSource':original})
    finally:bpy.context.window.scene=previous
    (ROOT/'assets/sources/catch-manifest.json').write_text(json.dumps(manifest,indent=2)+'\n')
    bpy.data.libraries.write(str(ROOT/'assets/sources/catch-library.blend'),set(scenes),fake_user=True)
    print('CATCH_LIBRARY_OK',len(manifest),'models')

if __name__=='__main__':main()
