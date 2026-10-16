"""Request missing character originals through the configured Rodin MCP.
Completed responses are reused; never silently pay for the same generation again.
"""
from pathlib import Path
import concurrent.futures
import json
import subprocess
import sys
import shutil

ROOT = Path(__file__).resolve().parents[2]
CHARACTERS = {
    'tono': 'Pak Tono, middle aged Indonesian male civil servant, stocky build, neat short black hair, khaki short sleeve civil service uniform with collar and chest pockets, khaki trousers, black belt and polished black shoes, calm round face',
    'ojol': 'Indonesian male motorcycle delivery driver, young adult, bright green zipped riding jacket with black panels and reflective piping, black trousers and sneakers, green open face helmet with raised clear visor, friendly face, no logos',
    'satpam': 'Indonesian male security guard, middle aged broad build, light tan short sleeve security uniform with dark brown shoulder panels and trousers, black peaked cap, black utility belt and boots, serious friendly face, no weapons',
    'bakso': 'Indonesian male bakso street food vendor, middle aged round belly, maroon polo shirt under a cream cloth apron with food stains and pocket, black trousers, sandals, white folded cloth cap, friendly moustached face',
    'pendaki': 'Indonesian male mountain hiker, young adult lean athletic build, rust orange hiking jacket, olive cargo trousers, rugged hiking boots, dark green framed backpack with rolled sleeping mat attached, dark short hair and brimmed hiking hat',
    'kucing': 'Orange tabby cat fishing adventurer, upright anthropomorphic cat with clearly feline anatomy, triangular pointed ears, whiskers, short muzzle, striped orange fur, long curved tail, small green fishing vest, two legs and two arms with paws, no human face',
    'robot': 'Friendly Indonesian fishing robot, humanoid mechanical proportions, weathered ivory enamel body panels over articulated dark metal joints, cyan glowing eyes on rounded rectangular head, small chest fish insignia, visible finger joints, sturdy feet, copper accents, no weapons'
}

def generate(item):
    name, description = item
    target = ROOT/'assets/sources/rodin'/(name+'.glb')
    response = ROOT/'artifacts'/('rodin-'+name+'-result.json')
    prompt = ROOT/'assets/sources'/('rodin-'+name+'.json')
    if target.exists(): return name+' original exists'
    if not response.exists():
        prompt.write_text(json.dumps({'prompt': 'Single full body character: '+description+'. Detailed realistic game asset with PBR texture, clean anatomy and efficient topology. Symmetric relaxed A pose for rigging, arms angled away from torso and hands separate from hips, straight separated legs, front facing upright. One character only. No handheld props, no scene, no ground, no pedestal.'},indent=2)+'\n')
        subprocess.run([sys.executable,str(ROOT/'tools/rodin-client.py'),'Generate3DFromPrompt','--arguments',str(prompt),'--output',str(response)],check=True)
    result=json.loads(response.read_text())
    if result.get('result',{}).get('isError'): raise RuntimeError(name+' generation returned error; inspect response, do not auto-retry')
    text=result['result']['content'][0]['text']
    source=Path(text.split('download at: ',1)[1].strip())
    shutil.copy2(source,target)
    return name+' original saved'

if __name__=='__main__':
    with concurrent.futures.ThreadPoolExecutor(max_workers=3) as pool:
        for result in pool.map(generate, CHARACTERS.items()): print(result,flush=True)
