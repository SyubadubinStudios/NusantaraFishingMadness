"""Original regional motifs, percussion and adaptive combat music; no sampled recordings.
Deterministic PCM loops, with wrapped note tails to avoid clicks at the loop seam.
"""
from pathlib import Path
import math
import random
import struct
import wave

ROOT = Path(__file__).resolve().parents[2]
RATE = 22050
REGIONS = ['selatan','ampat','toba','kelimutu','kalimantan','jakarta','kubangan',
           'terjun','gua','tambang','awan','gunung','kristal','dimensi']


def compose(name, index, bpm, combat=False, dance=False):
    beat = 60 / bpm
    count = int(32 * beat * RATE)
    audio = [0.] * count
    rng = random.Random(431 + index)
    scale = [0, 3, 5, 7, 10] if combat else [0, 2, 4, 7, 9]
    motif = [rng.randrange(5) for _ in range(8)]
    root = 130.8128 * 2 ** ((index % 7) / 12)

    def note(start, frequency, duration, volume, voice):
        begin = int(start * RATE)
        for j in range(int(duration * RATE)):
            t = j / RATE
            attack = min(1, t * 90)
            if voice == 'bell':
                sound = math.sin(math.tau * frequency * t) * math.exp(-t * 3)
                sound += .24 * math.sin(math.tau * frequency * 2.76 * t) * math.exp(-t * 6)
            elif voice == 'flute':
                sound = (math.sin(math.tau * frequency * t + .018 * math.sin(t * 31)) + .12 * math.sin(math.tau * frequency * 2 * t)) * math.sin(math.pi * t / duration) ** 2
            elif voice == 'kick':
                sound = math.sin(math.tau * (48 * t + 3 * (1-math.exp(-t*28)))) * math.exp(-t*16)
            else:
                sound = math.sin(math.tau * frequency * t) * math.exp(-t * 2)
            end_fade = min(1, (duration-t) * 60)
            audio[(begin+j) % count] += sound * volume * attack * end_fade

    for step in range(32):
        degree = motif[step % 8] if step < 24 else (step + index) % 5
        frequency = root * 2 ** (scale[degree]/12)
        note(step * beat, frequency * 2, beat * 2.5, .12, 'bell')
        if step % 4 == 0:
            note(step * beat, root / 2 * (1 if step % 8 == 0 else 1.5), beat * 4, .13, 'bass')
            note(step * beat, frequency, beat * 3, .07, 'flute')
        if dance or combat or step % 4 == index % 4:
            note(step * beat, 65, .3, .2 if dance else .11, 'kick')
        if dance:
            note((step + .5)*beat, frequency*4, beat*.3, .055, 'bell')
    maximum = max(abs(x) for x in audio)
    gain = min(1, .8 / max(.001, maximum))
    path = ROOT/'assets/audio'/(name+'.wav')
    with wave.open(str(path), 'wb') as output:
        output.setparams((1, 2, RATE, 0, 'NONE', 'not compressed'))
        output.writeframes(b''.join(struct.pack('<h', round(x*gain*32767)) for x in audio))
    print(name, round(count/RATE, 2), 'seconds')


if __name__ == '__main__':
    for index, region in enumerate(REGIONS):
        compose('region-'+region, index, [84,92,72,78,86,100,104,80,64,76,68,108,70,96][index])
    compose('boss-theme', 15, 128, combat=True)
    compose('dj-theme', 16, 120, dance=True)
