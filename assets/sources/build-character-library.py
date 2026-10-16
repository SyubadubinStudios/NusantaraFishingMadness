"""Rig each Rodin original in an isolated Blender scene. Run through Blender MCP."""
from pathlib import Path
import bpy
import contextlib
import runpy

ROOT = Path(__file__).resolve().parents[2]
CHARACTERS = ['bambang','tono','ojan','ojol','satpam','bakso','kucing','robot','pendaki']

def main():
    art=runpy.run_path(str(ROOT/'assets/sources/build-visual-assets.py'))
    previous=bpy.context.window.scene
    scenes=[]
    try:
        for name in CHARACTERS:
            if not (ROOT/'assets/sources/rodin'/(name+'.glb')).exists():
                print('SOURCE_PENDING',name)
                continue
            scene=bpy.data.scenes.new('NFM_Character_'+name)
            scenes.append(scene)
            bpy.context.window.scene=scene
            with (ROOT/'artifacts'/('blender-'+name+'.log')).open('w') as log, contextlib.redirect_stdout(log):
                art['character'](name)
                art['export'](name,True)
            print('CHARACTER_EXPORTED',name)
    finally: bpy.context.window.scene=previous
    bpy.data.libraries.write(str(ROOT/'assets/sources/character-library.blend'),set(scenes),fake_user=True)

if __name__=='__main__': main()
