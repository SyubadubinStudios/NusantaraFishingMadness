"""Original Blender props: floating river house, restricted outpost and tournament cup."""
from pathlib import Path
import bpy
import math
import runpy
import contextlib

ROOT=Path(__file__).resolve().parents[2]
art=runpy.run_path(str(ROOT/'assets/sources/build-visual-assets.py'))
mat,box,beam=art['material'],art['cube'],art['beam']

def house(government=False):
    wood=mat('Teak river planks',(.38,.22,.10))
    roof=mat('Corrugated roof',(.18,.28,.25) if government else (.35,.14,.07),.6,.35)
    trim=mat('Painted trim',(.73,.76,.61) if government else (.56,.36,.15))
    dark=mat('Window glass',(.06,.17,.19),.2,.25)
    for i in range(16):
        if government:box('Deck plank',(-3.75+i*.5,0,1.05),(.47,6,.13),wood,.02)
        else:beam('Raft bamboo',(-3.75+i*.5,-3,.2),(-3.75+i*.5,3,.2),.25,wood,10)
    if not government:box('House platform',(0,0,.65),(7.5,5.5,.22),wood,.03)
    ground=1.1 if government else .8
    for x in (-3,3):
        for y in (-2,2):beam('Structural post',(x,y,.1),(x,y,ground+2.8),.12,wood)
    for i in range(9):
        z=ground+.2+i*.25
        box('Back siding',(0,1.8,z),(6,.12,.22),trim,.01)
        for x in (-3,3):box('Side siding',(x,0,z),(.12,3.6,.22),trim,.01)
    for x in (-2,2):
        box('Front panel',(x,-1.8,ground+1.25),(2,.12,2.5),trim,.02)
        box('Window',(x,-1.88,ground+1.55),(1.2,.05,1),dark,.02)
        for sign in (-1,1):box('Window frame',(x+sign*.63,-1.94,ground+1.55),(.07,.07,1.12),wood,.01)
        box('Window cross',(x,-1.94,ground+1.55),(1.2,.06,.06),wood,.01)
    for sign in (-1,1):
        panel=box('Roof sheet',(sign*1.8,0,ground+3.05),(3.8,5.1,.12),roof,.015)
        panel.rotation_euler.y=sign*math.radians(20)
        for i in range(18):
            rib=box('Roof seam',(sign*(.1+i*.2),0,ground+3.7-(.1+i*.2)*.36),(.035,5.1,.035),roof,.01)
    for y in (-2.8,2.8):
        beam('Deck handrail',(-3.8,y,ground+1),(3.8,y,ground+1),.06,wood)
        for x in (-3.8,-2,0,2,3.8):beam('Railing post',(x,y,ground),(x,y,ground+1),.04,wood)
    if government:
        pole=mat('Flagpole',(.5,.52,.55),.25,.8)
        red=mat('Merah',(.7,.03,.025));white=mat('Putih',(.9,.9,.85))
        beam('Flagpole',(3.5,2.3,1.1),(3.5,2.3,6.1),.035,pole)
        box('Flag red',(2.9,2.3,5.8),(1.2,.03,.3),red,.002)
        box('Flag white',(2.9,2.3,5.5),(1.2,.03,.3),white,.002)
    else:
        beam('Mooring rope',(-3,2.5,.5),(-5,3,.8),.025,wood,6)
        box('Supply crate',(2,-2.3,1.1),(.7,.65,.6),wood,.025)

def trophy():
    gold=mat('Trophy gold',(.78,.48,.10),.22,.85)
    stone=mat('Trophy base',(.045,.075,.09),.4)
    box('Plinth',(0,0,.08),(.48,.4,.16),stone,.03)
    beam('Stem',(0,0,.16),(0,0,.42),.07,gold,16)
    bpy.ops.mesh.primitive_cone_add(vertices=24,radius1=.1,radius2=.27,depth=.36,location=(0,0,.58))
    bpy.context.object.data.materials.append(gold)
    for sign in (-1,1):
        for i in range(12):
            a=i/12*math.pi; b=(i+1)/12*math.pi
            beam('Cup handle',(sign*(.19+.17*math.sin(a)),0,.57+.16*math.cos(a)),(sign*(.19+.17*math.sin(b)),0,.57+.16*math.cos(b)),.025,gold,6)

if __name__=='__main__':
    previous=bpy.context.window.scene
    scenes=[]
    try:
        for name,callback in [('raft-house',lambda:house(False)),('government-outpost',lambda:house(True)),('tournament-trophy',trophy)]:
            scene=bpy.data.scenes.new('NFM_Prop_'+name);scenes.append(scene);bpy.context.window.scene=scene
            with (ROOT/'artifacts'/('blender-'+name+'.log')).open('w') as log,contextlib.redirect_stdout(log):
                callback();art['export'](name)
            print('PROP_EXPORTED',name)
    finally:bpy.context.window.scene=previous
    bpy.data.libraries.write(str(ROOT/'assets/sources/regional-props.blend'),set(scenes),fake_user=True)
