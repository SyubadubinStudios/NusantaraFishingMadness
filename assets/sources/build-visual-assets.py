"""Run through Blender MCP. Preserve the user's scene; deterministic game exports.
Rodin originals stay in sources/rodin. Blender 5.2, glTF Y-up, meters.
"""
import bpy
import math
import random
from pathlib import Path
from mathutils import Vector

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / 'assets/models'
random.seed(431)

def material(name, color, rough=.75, metal=0):
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    shader = next(n for n in mat.node_tree.nodes if n.type == 'BSDF_PRINCIPLED')
    shader.inputs['Base Color'].default_value = (*color, 1)
    shader.inputs['Roughness'].default_value = rough
    shader.inputs['Metallic'].default_value = metal
    if any(word in name.lower() for word in ('teak','joinery')) and (ROOT/'assets/textures/weathered-teak.png').exists():
        texture=mat.node_tree.nodes.new('ShaderNodeTexImage')
        texture.image=bpy.data.images.load(str(ROOT/'assets/textures/weathered-teak.png'),check_existing=True)
        mat.node_tree.links.new(texture.outputs['Color'],shader.inputs['Base Color'])
    return mat

def mesh(name, verts, faces, mat):
    data = bpy.data.meshes.new(name)
    data.from_pydata(verts, [], faces)
    data.materials.append(mat)
    obj = bpy.data.objects.new(name, data)
    bpy.context.scene.collection.objects.link(obj)
    return obj

def beam(name, start, end, radius, mat, vertices=8):
    a, b = Vector(start), Vector(end)
    bpy.ops.mesh.primitive_cone_add(vertices=vertices, radius1=radius, radius2=radius*.9, depth=(b-a).length, location=(a+b)/2)
    obj = bpy.context.object
    obj.name = name
    obj.rotation_euler = (b-a).to_track_quat('Z','Y').to_euler()
    obj.data.materials.append(mat)
    return obj

def cube(name, loc, size, mat, bevel=0):
    bpy.ops.mesh.primitive_cube_add(size=1, location=loc)
    obj = bpy.context.object
    obj.name = name
    obj.scale = size
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    obj.data.materials.append(mat)
    if bevel:
        mod = obj.modifiers.new('Worn edges', 'BEVEL'); mod.width = bevel; mod.segments = 1
        bpy.ops.object.modifier_apply(modifier=mod.name)
    return obj

def export(name, animated=False):
    bpy.ops.object.select_all(action='SELECT')
    if name in ('seabird','dolphin'):
        parents={o.parent for o in bpy.context.scene.objects if o.type=='MESH'}
        for parent in parents:
            objects=[o for o in bpy.context.scene.objects if o.type=='MESH' and o.parent==parent]
            bpy.ops.object.select_all(action='DESELECT')
            for obj in objects: obj.select_set(True)
            bpy.context.view_layer.objects.active=objects[0]
            if len(objects)>1:bpy.ops.object.join()
        bpy.ops.object.select_all(action='SELECT')
    # Merge by material for a small draw count while preserving the character rig.
    if not animated and name not in ('seabird','dolphin'):
        objects = [o for o in bpy.context.scene.objects if o.type == 'MESH']
        bpy.context.view_layer.objects.active = objects[0]
        bpy.ops.object.join()
        bpy.context.object.name = name
    bpy.ops.export_scene.gltf(filepath=str(OUT / (name+'.glb')), use_selection=True, use_active_scene=True,
        export_animations=animated, export_animation_mode='NLA_TRACKS', export_force_sampling=True,
        export_optimize_animation_size=True, export_image_format='AUTO')
    print('EXPORTED', name)

def isolated(name, callback):
    previous = bpy.context.window.scene
    scene = bpy.data.scenes.new('NFM_'+name)
    bpy.context.window.scene = scene
    try:
        callback()
        export(name, name in ('fisherman','rina'))
    finally:
        bpy.context.window.scene = previous

def character(name):
    bpy.ops.import_scene.gltf(filepath=str(ROOT / 'assets/sources/rodin' / (name+'.glb')))
    objects = [o for o in bpy.context.scene.objects if o.type == 'MESH']
    bpy.ops.object.select_all(action='DESELECT')
    for obj in objects: obj.select_set(True)
    bpy.context.view_layer.objects.active = objects[0]
    bpy.ops.object.join()
    obj = bpy.context.object
    obj.name = name+'_body'
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    low = min(v.co.z for v in obj.data.vertices)
    factor = 1.78 / (max(v.co.z for v in obj.data.vertices)-low)
    for v in obj.data.vertices: v.co = Vector((v.co.x*factor,v.co.y*factor,(v.co.z-low)*factor))
    triangles = sum(len(p.vertices)-2 for p in obj.data.polygons)
    mod = obj.modifiers.new('Game mesh budget', 'DECIMATE'); mod.ratio=min(1,7000/triangles)
    bpy.ops.object.modifier_apply(modifier=mod.name)
    for mat in obj.data.materials:
        for node in mat.node_tree.nodes:
            if node.type == 'TEX_IMAGE' and node.image and max(node.image.size)>1024:
                node.image.scale(1024,1024)
    arm = bpy.data.armatures.new(name+'_skeleton')
    rig = bpy.data.objects.new(name+'_rig',arm)
    bpy.context.scene.collection.objects.link(rig)
    bpy.context.view_layer.objects.active=rig
    obj.select_set(False); rig.select_set(True)
    bpy.ops.object.mode_set(mode='EDIT')
    joints = {
        'Pelvis':((0,0,.82),(0,0,1.02),None),
        'Spine':((0,0,1.02),(0,0,1.28),'Pelvis'),
        'Chest':((0,0,1.28),(0,0,1.44),'Spine'),
        'Head':((0,0,1.44),(0,0,1.75),'Chest'),
    }
    for side,sign in [('Left',1),('Right',-1)]:
        joints.update({
            side+'Arm':((sign*.20,0,1.40),(sign*.29,0,1.12),'Chest'),
            side+'Forearm':((sign*.29,0,1.12),(sign*.34,-.025,.86),side+'Arm'),
            side+'Hand':((sign*.34,-.025,.86),(sign*.35,-.04,.75),side+'Forearm'),
            side+'Thigh':((sign*.12,0,.86),(sign*.14,0,.47),'Pelvis'),
            side+'Shin':((sign*.14,0,.47),(sign*.15,0,.12),side+'Thigh'),
            side+'Foot':((sign*.15,0,.12),(sign*.15,-.17,.07),side+'Shin')})
    if name=='rina':
        for key,(head,tail,parent) in list(joints.items()):
            if key.endswith(('Arm','Forearm','Hand')):
                joints[key]=((head[0]*.8,head[1],head[2]),(tail[0]*.8,tail[1],tail[2]),parent)
    if name=='ojan':
        for side,sign in [('Left',1),('Right',-1)]:
            joints[side+'Arm']=((sign*.30,0,1.34),(sign*.48,0,.88),'Chest')
            joints[side+'Forearm']=((sign*.48,0,.88),(sign*.60,-.04,.49),side+'Arm')
            joints[side+'Hand']=((sign*.60,-.04,.49),(sign*.62,-.05,.34),side+'Forearm')
            joints[side+'Thigh']=((sign*.24,0,.80),(sign*.29,0,.43),'Pelvis')
            joints[side+'Shin']=((sign*.29,0,.43),(sign*.25,0,.12),side+'Thigh')
            joints[side+'Foot']=((sign*.25,0,.12),(sign*.25,-.20,.07),side+'Shin')
    for key,(head,tail,parent) in joints.items():
        bone=arm.edit_bones.new(key); bone.head=head; bone.tail=tail
        if parent: bone.parent=arm.edit_bones[parent]
    bpy.ops.object.mode_set(mode='OBJECT')
    groups={key:obj.vertex_groups.new(name=key) for key in joints}
    for v in obj.data.vertices:
        p=v.co; side='Left' if p.x>0 else 'Right'
        if p.z>1.47: candidates=['Head']
        elif name=='ojan' and abs(p.x)>(.29 if p.z>1.08 else .40 if p.z>.70 else .48) and p.z>.25: candidates=[side+'Arm',side+'Forearm',side+'Hand']
        elif name!='ojan' and abs(p.x)>((.20 if p.z>1.08 else .215) if name=='rina' else (.235 if p.z>1.08 else .285)) and p.z>.60: candidates=[side+'Arm',side+'Forearm',side+'Hand']
        elif p.z<.84: candidates=[side+'Thigh',side+'Shin',side+'Foot','Pelvis']
        else: candidates=['Pelvis','Spine','Chest','Head']
        if name=='ojan' and p.z<.85:
            arms=[side+'Forearm',side+'Hand']
            legs=[side+'Thigh',side+'Shin',side+'Foot','Pelvis']
            def bone_distance(key):
                a,b=Vector(joints[key][0]),Vector(joints[key][1]); delta=b-a
                return (p-(a+delta*max(0,min(1,(p-a).dot(delta)/delta.length_squared)))).length
            candidates=arms if min(map(bone_distance,arms))<min(map(bone_distance,legs)) else legs
        distances=[]
        for key in candidates:
            a,b=Vector(joints[key][0]),Vector(joints[key][1]); delta=b-a
            closest=a+delta*max(0,min(1,(p-a).dot(delta)/delta.length_squared))
            distances.append((max(.012,(p-closest).length),key))
        distances.sort()
        weights=[(1/d**4,key) for d,key in distances[:2]]; total=sum(w for w,k in weights)
        for weight,key in weights: groups[key].add([v.index],weight/total,'REPLACE')
    modifier=obj.modifiers.new('Skin', 'ARMATURE'); modifier.object=rig
    obj.parent=rig
    if name not in ('fisherman','rina','ojan'):
        # Bone heat follows connected anatomy rather than slicing fingers and cuffs by X/Z.
        bpy.ops.object.select_all(action='DESELECT')
        obj.select_set(True); rig.select_set(True)
        bpy.context.view_layer.objects.active=rig
        bpy.ops.object.parent_set(type='ARMATURE_AUTO')
        # Loose coat/apron hems must follow the pelvis, not nearby hands.
        if name in ('bambang','bakso'):
            for vertex in obj.data.vertices:
                p=vertex.co
                if .58<p.z<.94 and abs(p.x)<.30 and (p.y>.08 or p.y<-.12):
                    for group in obj.vertex_groups: group.remove([vertex.index])
                    obj.vertex_groups['Pelvis'].add([vertex.index],1,'REPLACE')
        # Smooth only along actual mesh edges, preserving separated fingers and limbs.
        neighbors=[set() for v in obj.data.vertices]
        for edge in obj.data.edges:
            a,b=edge.vertices; neighbors[a].add(b); neighbors[b].add(a)
        weights=[{g.group:g.weight for g in v.groups if g.weight>0} for v in obj.data.vertices]
        for vertex in obj.data.vertices:
            if weights[vertex.index]:continue
            # Disconnected buttons/eyeglass islands cannot receive heat weights. Bind them
            # to the nearest anatomical segment instead of leaving them at the origin.
            distances=[]
            for key,(head,tail,parent) in joints.items():
                a,b=Vector(head),Vector(tail);delta=b-a
                nearest=a+delta*max(0,min(1,(vertex.co-a).dot(delta)/delta.length_squared))
                distances.append(((vertex.co-nearest).length,key))
            key=min(distances)[1]
            weights[vertex.index]={obj.vertex_groups[key].index:1}
        for iteration in range(3):
            smoothed=[]
            for i,current in enumerate(weights):
                if not neighbors[i]: smoothed.append(current); continue
                values={key:value*.6 for key,value in current.items()}
                for other in neighbors[i]:
                    for key,value in weights[other].items():values[key]=values.get(key,0)+value*.4/len(neighbors[i])
                strongest=sorted(values.items(),key=lambda p:p[1],reverse=True)[:4]
                total=sum(value for key,value in strongest)
                if total<=0:raise RuntimeError(name+' contains unweighted vertices')
                smoothed.append({key:value/total for key,value in strongest})
            weights=smoothed
        for v,current in zip(obj.data.vertices,weights):
            for group in obj.vertex_groups:group.remove([v.index])
            for key,value in current.items():obj.vertex_groups[key].add([v.index],value,'REPLACE')
    bpy.context.scene.render.fps=30
    for clip,frames in [('Idle',90),('Walk',30),('Fishing',90)]:
        rig.animation_data_create(); rig.animation_data.action=None
        for frame in range(1,frames+2,3):
            phase=(frame-1)/frames*math.tau
            for bone in rig.pose.bones:
                bone.rotation_mode='XYZ'; bone.rotation_euler=(0,0,0); bone.location=(0,0,0)
            rig.pose.bones['Chest'].rotation_euler.x=.012*math.sin(phase)
            rig.pose.bones['Head'].rotation_euler.z=.035*math.sin(phase)
            if clip=='Walk':
                for side,sign in [('Left',1),('Right',-1)]:
                    swing=math.sin(phase)*sign
                    rig.pose.bones[side+'Thigh'].rotation_euler.x=.36*swing
                    rig.pose.bones[side+'Shin'].rotation_euler.x=-.48*max(0,-swing)
                    rig.pose.bones[side+'Arm'].rotation_euler.x=-.23*swing
                    rig.pose.bones[side+'Forearm'].rotation_euler.x=-.12
                rig.pose.bones['Pelvis'].location.z=.018*(1-math.cos(phase*2))
            if clip=='Fishing':
                rig.pose.bones['Chest'].rotation_euler.x=.06
                for side in ('Left','Right'):
                    rig.pose.bones[side+'Arm'].rotation_euler.x=-.55+.04*math.sin(phase)
                    rig.pose.bones[side+'Forearm'].rotation_euler.x=-.55+.08*math.sin(phase)
            for bone in rig.pose.bones:
                bone.keyframe_insert('rotation_euler',frame=frame,group=bone.name)
                bone.keyframe_insert('location',frame=frame,group=bone.name)
        action=rig.animation_data.action
        action.name=name+'_'+clip
        action.use_fake_user=True
        track=rig.animation_data.nla_tracks.new(); track.name=clip
        track.strips.new(clip,1,action)
    rig.animation_data.action=None
    bpy.context.scene.frame_set(1)
    print(name,'triangles',sum(len(p.vertices)-2 for p in obj.data.polygons))

def karst():
    bpy.ops.import_scene.gltf(filepath=str(ROOT/'assets/sources/rodin/karst.glb'))
    obj=next(o for o in bpy.context.scene.objects if o.type=='MESH')
    bpy.context.view_layer.objects.active=obj
    bpy.ops.object.transform_apply(location=True,rotation=True,scale=True)
    low=min(v.co.z for v in obj.data.vertices)
    extent=Vector((max(v.co.x for v in obj.data.vertices)-min(v.co.x for v in obj.data.vertices),max(v.co.y for v in obj.data.vertices)-min(v.co.y for v in obj.data.vertices),max(v.co.z for v in obj.data.vertices)-low))
    for v in obj.data.vertices: v.co=Vector((v.co.x/extent.x,v.co.y/extent.y,(v.co.z-low)/extent.z))
    mod=obj.modifiers.new('Rock LOD','DECIMATE'); mod.ratio=min(1,1800/sum(len(p.vertices)-2 for p in obj.data.polygons))
    bpy.ops.object.modifier_apply(modifier=mod.name)
    for mat in obj.data.materials:
        for node in mat.node_tree.nodes:
            if node.type=='TEX_IMAGE' and node.image and max(node.image.size)>1024: node.image.scale(1024,1024)

def palm():
    bark=material('Coconut bark',(.21,.145,.08))
    rings=material('Leaf scars',(.32,.235,.13))
    greens=[material('Frond '+str(i),c) for i,c in enumerate([(.08,.19,.035),(.14,.26,.055),(.23,.31,.07)])]
    def trunk(t): return Vector((.4*t*t,.14*math.sin(t*2),4.6*t))
    for i in range(24):
        a,b=trunk(i/24),trunk((i+1)/24)
        beam('Trunk',a,b,.15-i*.002,bark,10)
        beam('Growth ring',a,a+(b-a)*.13,.157-i*.002,rings,10)
    crown=trunk(1)
    for i in range(11):
        angle=i*math.tau/11
        direction=Vector((math.cos(angle),math.sin(angle),0))
        sideways=Vector((-direction.y,direction.x,0))
        length=2.1+(i%3)*.25
        def spine(t): return crown+direction*(length*t)+Vector((0,0,.6*math.sin(t*math.pi)-.85*t*t))
        for j in range(7): beam('Frond stem',spine(j/7),spine((j+1)/7),.018,greens[0],5)
        for j in range(1,16):
            t=j/17; center=spine(t); width=.55*math.sin(t*math.pi)**.6
            for sign in [-1,1]:
                tip=center+sideways*sign*width+direction*.24+Vector((0,0,-.12))
                mid=(center+tip)/2+Vector((0,0,.045))
                verts=[center-direction*.045,mid-direction*.045,tip,mid+direction*.045,center+direction*.045]
                leaf=mesh('Coconut leaflet',verts,[(0,1,2,3,4)],greens[(i+j)%3])
    for i in range(4):
        bpy.ops.mesh.primitive_uv_sphere_add(segments=8,ring_count=5,radius=.13,location=crown+Vector((math.sin(i)*.18,math.cos(i)*.18,-.12)))
        bpy.context.object.data.materials.append(rings)

def dock():
    wood=[material('Weathered teak '+str(i),(.24+i*.025,.145+i*.018,.075+i*.012)) for i in range(4)]
    roof=material('Sun bleached palm thatch',(.32,.245,.125))
    dark=material('Rope and shadow',(.11,.09,.065))
    # Blender XY maps to runtime X,-Z; deck matches the existing walkable pier.
    for i in range(16):
        plank=cube('Pier plank',(-7,8-i*.65,.55),(3,.60,.18),wood[i%4],.025)
        plank.rotation_euler.z=(random.random()-.5)*.012
        for x in [-8.15,-5.85]:
            beam('Iron nail',(x,8-i*.65,.647),(x,8-i*.65,.656),.018,dark,6)
    for x in [-8.2,-5.8]:
        for y in [8,4,0,-2]: beam('Pier piling',(x,y,-1.2),(x,y,1.04),.115,wood[0],10)
    for x in [-8.4,-5.6]:
        for y in [4.5,7.5]: beam('Hut column',(x,y,.6),(x,y,3.25),.09,wood[1])
    for x in [-8.35,-5.65]:
        for j in range(13): cube('Hut side slat',(x,4.5+j*.24,1.8),(.075,.20,2.3),wood[j%4],.01)
    for j in range(13): cube('Hut rear slat',(-8.4+j*.235,7.5,1.8),(.22,.07,2.3),wood[j%4],.01)
    for sign in [-1,1]:
        for j in range(13):
            x=-7+sign*(.1+j*.14); z=4.2-j*.09
            panel=cube('Layered thatch',(x,6,z),(.23,3.8,.10),roof,.015)
            panel.rotation_euler.y=sign*.57
    cube('Counter',(-7,4.45,1.2),(2.8,.55,.15),wood[2],.03)
    for x,y in [(-8,2),(-6.4,6.3)]:
        cube('Fish crate',(x,y,.92),(.65,.5,.5),wood[1],.02)
        for j in range(3): cube('Crate binding',(x,y,.76+j*.15),(.69,.54,.035),dark)
    for i in range(4):
        bpy.ops.mesh.primitive_torus_add(major_radius=.27-i*.035,minor_radius=.025,major_segments=24,minor_segments=6,location=(-6.3,1,.67+i*.025))
        bpy.context.object.data.materials.append(dark)

def loft(name, rings, mat, segments=16):
    verts=[]
    for y,z,width,height in rings:
        for i in range(segments):
            a=i*math.tau/segments
            verts.append((math.cos(a)*width,y,z+math.sin(a)*height))
    faces=[]
    for j in range(len(rings)-1):
        for i in range(segments):
            k=j*segments+i; n=j*segments+(i+1)%segments
            faces.append((k,n,n+segments,k+segments))
    faces.extend([tuple(reversed(range(segments))),tuple(range((len(rings)-1)*segments,len(rings)*segments))])
    obj=mesh(name,verts,faces,mat)
    for p in obj.data.polygons:p.use_smooth=True
    return obj

def jetski():
    ivory=material('Marine ivory',(.72,.75,.70),.25)
    orange=material('Burnt orange enamel',(.65,.16,.045),.3)
    rubber=material('Marine rubber',(.025,.035,.04),.85)
    metal=material('Handlebar metal',(.2,.23,.24),.3,.7)
    loft('Hull',[(-1.5,.03,.07,.06),(-1.1,.05,.38,.16),(-.4,.07,.60,.24),(.5,.08,.55,.26),(1.2,.12,.36,.22),(1.5,.2,.03,.07)],ivory)
    loft('Upper fairing',[(-1.25,.18,.08,.04),(-.8,.27,.38,.14),(-.2,.35,.48,.23),(.55,.40,.40,.29),(1.25,.30,.18,.10)],orange)
    loft('Saddle',[(-.95,.43,.15,.05),(-.6,.48,.25,.09),(.1,.50,.22,.10),(.4,.53,.13,.08)],rubber)
    for sign in [-1,1]: cube('Foot deck',(sign*.43,-.4,.25),(.19,1.1,.065),rubber,.03)
    beam('Steering column',(0,.50,.5),(0,.32,.85),.04,metal)
    beam('Handlebar',(-.36,.32,.85),(.36,.32,.85),.025,metal)
    for sign in [-1,1]:beam('Handle grip',(sign*.2,.32,.85),(sign*.38,.32,.85),.04,rubber)

def pavilion():
    woods=[material('Pavilion teak '+str(i),(.20+i*.035,.115+i*.025,.055+i*.016)) for i in range(3)]
    roof=material('Terracotta roof',(.28,.105,.055),.9)
    dark=material('Pavilion dark joinery',(.09,.075,.05))
    for j in range(27):cube('Veranda floor',(0,-4.4+j*.33,.04),(9,.31,.12),woods[j%3],.014)
    for x in [-4.3,0,4.3]:
        for y in [-4.3,0,4.3]:
            beam('Stilt',(x,y,-2),(x,y,.1),.12,woods[0],8)
            if y!=0 or x!=0:beam('Pavilion post',(x,y,.08),(x,y,3.3),.10,woods[1])
    for x in [-4.3,4.3]:
        beam('Side eave',(x,-4.6,3.3),(x,4.6,3.3),.12,woods[0])
        for y in [-4.3,4.3]:
            beam('Diagonal brace',(x,y,2.3),(x,y-math.copysign(.8,y),3.25),.065,woods[1])
    for j in range(36):
        cube('Rear slat',(-4.3+j*.245,4.3,1.5),(.21,.08,2.8),woods[j%3],.012)
    for sign in [-1,1]:
        for j in range(16):
            x=sign*(.15+j*.31); z=5.0-j*.105
            obj=cube('Roof tile course',(x,0,z),(.4,9.7,.10),roof,.02);obj.rotation_euler.y=sign*.33
        beam('Roof ridge end',(0,sign*4.8,5),(sign*4.9,sign*4.8,3.35),.10,dark)
    beam('Roof ridge',(0,-5,5),(0,5,5),.10,dark)
    for x in [-4.3,4.3]:
        beam('Side rail',(x,-4.3,1),(x,4.3,1),.06,woods[1])
        for j in range(15):beam('Baluster',(x,-4+j*.56,.1),(x,-4+j*.56,1),.025,woods[0],6)

def surfboard():
    teal=material('Waxed teal board',(.04,.38,.34),.32)
    cream=material('Board center stripe',(.75,.66,.39),.4)
    dark=material('Surf fin',(.045,.065,.06),.55)
    loft('Surfboard',[(-1.55,0,.025,.025),(-1.2,0,.23,.07),(-.5,0,.37,.085),(.3,0,.38,.085),(1.1,.07,.24,.07),(1.6,.17,.01,.02)],teal)
    loft('Board stringer',[(-1.4,.06,.018,.012),(0,.087,.018,.012),(1.4,.15,.012,.01)],cream,8)
    for x in [-.15,0,.15]:mesh('Surf fin',[(x,-.9,0),(x,-1.25,0),(x,-1.1,-.3)],[(0,1,2)],dark)

def seabird():
    white=material('Gull warm white',(.74,.77,.73),.9)
    gray=material('Gull mantle',(.32,.37,.39),.85)
    black=material('Gull primary feathers',(.035,.045,.05))
    yellow=material('Gull bill',(.68,.40,.07))
    body=loft('Gull body',[(-.45,.03,.025,.04),(-.26,.04,.14,.12),(.05,0,.18,.14),(.35,.02,.08,.06),(.48,.03,.015,.015)],white,12)
    loft('Gull head',[(-.48,.15,.03,.035),(-.4,.16,.09,.09),(-.28,.13,.08,.08)],white,10)
    beam('Bill',(0,-.46,.14),(0,-.65,.13),.035,yellow,6)
    for side,sign in [('left',-1),('right',1)]:
        pivot=bpy.data.objects.new(side+'-wing',None); bpy.context.scene.collection.objects.link(pivot)
        pivot.location=(sign*.12,0,.07)
        wing=mesh(side+' wing surface',[(0,-.12,0),(sign*.48,-.16,.02),(sign*.92,.04,-.03),(sign*.83,.18,-.04),(sign*.4,.23,0),(0,.17,0)],[(0,1,2,3,4,5)],gray)
        wing.parent=pivot
        for j in range(6):
            feather=mesh('Primary feather',[(sign*(.55+j*.065),.09,0),(sign*(.98+j*.018),.21+j*.025,-.05),(sign*(.63+j*.06),.21,0)],[(0,1,2)],black)
            feather.parent=pivot
    mesh('Tail feathers',[(-.07,.3,0),(.07,.3,0),(.15,.6,.03),(-.15,.6,.03)],[(0,1,2,3)],white)

def dolphin():
    gray=material('Dolphin slate',(.16,.27,.30),.35)
    loft('Dolphin body',[(-1.42,0,.04,.03),(-1.12,0,.09,.08),(-.85,.07,.22,.23),(-.25,.08,.33,.36),(.35,.04,.25,.29),(.9,0,.12,.14),(1.3,0,.04,.05)],gray)
    mesh('Dorsal fin',[(0,-.1,.32),(0,.45,.3),(0,.27,.85),(.035,.2,.4)],[(0,1,2),(0,2,3),(1,3,2)],gray)
    for sign in [-1,1]:mesh('Pectoral fin',[(sign*.2,-.5,0),(sign*.75,.02,-.14),(sign*.3,.2,-.1)],[(0,1,2)],gray)
    pivot=bpy.data.objects.new('dolphin-tail',None);bpy.context.scene.collection.objects.link(pivot);pivot.location=(0,1.15,0)
    tail=mesh('Tail flukes',[(0,0,0),(-.62,.36,.04),(-.35,.43,.04),(0,.3,0),(.35,.43,.04),(.62,.36,.04)],[(0,1,2,3),(0,3,4,5)],gray);tail.parent=pivot

def coral():
    mat=material('Warm branching coral',(.40,.19,.12),.86)
    for i in range(11):
        angle=i*2.4; length=.35+(i%4)*.13
        start=Vector((0,0,0)); end=Vector((math.cos(angle)*.3,math.sin(angle)*.3,length))
        beam('Coral trunk',start,end,.07,mat,7)
        for j in range(3):
            tip=end+Vector((math.cos(angle+j)*.16,math.sin(angle+j)*.16,.18+j*.06))
            beam('Coral fingers',end,tip,.035,mat,6)

def reef_fish():
    gold=material('Butterflyfish gold',(.72,.46,.06),.4)
    cream=material('Butterflyfish ivory',(.70,.72,.57),.45)
    dark=material('Butterflyfish ink',(.025,.038,.04),.4)
    loft('Reef fish body',[(-.43,0,.015,.02),(-.27,0,.075,.13),(-.05,0,.10,.22),(.18,0,.07,.16),(.32,0,.025,.06)],gold,12)
    mesh('Tail fan',[(0,.28,0),(0,.53,.17),(0,.49,-.17)],[(0,1,2)],cream)
    mesh('Dorsal fin',[(0,-.2,.16),(0,.16,.28),(0,.23,.1)],[(0,1,2)],cream)
    for sign in [-1,1]:
        mesh('Eye stripe',[(sign*.075,-.26,.12),(sign*.09,-.16,.19),(sign*.09,-.16,-.19),(sign*.075,-.26,-.12)],[(0,1,2,3)],dark)
        bpy.ops.mesh.primitive_uv_sphere_add(segments=8,ring_count=4,radius=.022,location=(sign*.077,-.25,.045))
        bpy.context.object.data.materials.append(dark)

if __name__=='__main__':
    for name in ('fisherman','rina'): isolated(name,lambda n=name:character(n))
    isolated('karst',karst)
    isolated('coconut-palm',palm)
    isolated('fishing-pier',dock)
    isolated('jetski',jetski)
    isolated('harbor-pavilion',pavilion)
    isolated('surfboard',surfboard)
    isolated('seabird',seabird)
    isolated('dolphin',dolphin)
    isolated('branching-coral',coral)
    isolated('reef-fish',reef_fish)
