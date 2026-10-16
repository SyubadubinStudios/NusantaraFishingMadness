"""Rodin anatomical bases for the catch library; cache completed responses and originals."""
from pathlib import Path
import concurrent.futures
import json
import subprocess
import sys
import shutil

ROOT = Path(__file__).resolve().parents[2]
CREATURES = {
    'catfish': 'A single Indonesian walking catfish, Clarias, long smooth dark olive grey body, broad flattened head, small eyes, four pairs of distinct long barbels around the mouth, continuous long dorsal and anal fins, rounded tail, wet realistic skin, natural swimming pose, horizontal body',
    'dragon': 'A single mythical Indonesian aquatic dragon fish, elongated serpentine body with overlapping emerald scales, golden dorsal spines and fins, elegant Asian dragon head with antler-like horns and two whiskers, koi-like flowing tail, regal natural aquatic anatomy, horizontal swimming pose',
    'octopus': 'A single realistic giant tropical octopus, eight distinct organically curved arms with visible rows of suction cups, rounded mantle and expressive golden eyes, detailed mottled reddish brown wet skin, natural relaxed radial pose with separated arm tips, no rock',
    'squid': 'A single realistic tropical squid, streamlined elongated mantle, two triangular fins, eight short arms plus two longer feeding tentacles, distinct large eyes, iridescent reddish purple translucent wet skin, horizontal swimming pose',
    'crab': 'A single Indonesian mud crab, broad olive brown ridged carapace, two powerful open claws, eight separate walking legs including flat rear paddles, stalked eyes, detailed wet shell, natural symmetrical standing pose',
    'frog': 'A single golden Indonesian tree frog, anatomically realistic squat body, long folded hind legs, small webbed fingers and toes, prominent gold eyes with horizontal pupils, textured golden yellow wet skin, natural sitting pose',
    'turtle': 'A single ancient Indonesian freshwater turtle, worn olive brown domed shell with detailed scutes and faint moss patina, four broad clawed legs, small wrinkled head with dark eyes, short tail, natural swimming pose, no scenery',
    'crocodile': 'A single realistic adult Indonesian saltwater crocodile, long ridged olive green body, powerful tapering tail, four short splayed legs, long broad snout with closed mouth and visible small teeth, detailed irregular scales and dorsal scutes, horizontal resting pose',
    'dugong': 'A single realistic dugong, rounded grey brown marine mammal, broad downturned muzzle with bristles, small dark eyes, two short paddle flippers and horizontal crescent tail flukes, smooth naturally wrinkled skin, horizontal swimming pose',
    'whale': 'A single magnificent mythical Indonesian humpback whale, realistic whale proportions, long pectoral flippers, broad horizontal tail flukes, ventral throat grooves and knobbly head, deep indigo skin with subtle glowing turquoise constellation markings, majestic horizontal swimming pose'
}

def generate(item):
    name, description = item
    target = ROOT/'assets/sources/rodin'/('creature-'+name+'.glb')
    response = ROOT/'artifacts'/('rodin-creature-'+name+'-result.json')
    prompt = ROOT/'assets/sources'/('rodin-creature-'+name+'.json')
    if target.exists(): return name+' source exists'
    if not response.exists():
        prompt.write_text(json.dumps({'prompt':description+'. High quality realistic PBR game creature, efficient complete watertight anatomy, clear silhouette. One animal only, centered. No ground, no water, no scene, no pedestal, no text.'},indent=2)+'\n')
        subprocess.run([sys.executable,str(ROOT/'tools/rodin-client.py'),'Generate3DFromPrompt','--arguments',str(prompt),'--output',str(response)],check=True)
    result=json.loads(response.read_text())
    if result.get('result',{}).get('isError'):return name+' source error; retained response, needs inspection'
    text=result['result']['content'][0]['text']
    source=Path(text.split('download at: ',1)[1].strip())
    shutil.copy2(source,target)
    return name+' source saved'

if __name__=='__main__':
    with concurrent.futures.ThreadPoolExecutor(max_workers=3) as pool:
        futures=[pool.submit(generate,item) for item in CREATURES.items()]
        for future in concurrent.futures.as_completed(futures):
            try:print(future.result(),flush=True)
            except Exception as error: print(type(error).__name__+': '+str(error),flush=True)
