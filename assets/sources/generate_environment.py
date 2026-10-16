"""Original analytic HDR lighting and synthesized game audio. Python standard library only."""
from pathlib import Path
import math
import random
import struct
import wave
import zlib

ROOT = Path(__file__).resolve().parents[2] / "assets"
RATE = 22050


def wav(name, samples):
    target = ROOT / "audio" / (name + ".wav")
    target.parent.mkdir(parents=True, exist_ok=True)
    with wave.open(str(target), "wb") as output:
        output.setnchannels(1)
        output.setsampwidth(2)
        output.setframerate(RATE)
        output.writeframes(b"".join(struct.pack("<h", int(max(-1, min(1, x)) * 32760)) for x in samples))


def soundscape(name, seconds, mode):
    rng = random.Random(431)
    low = 0
    samples = []
    for i in range(int(RATE * seconds)):
        t = i / RATE
        low = .98 * low + .02 * rng.uniform(-1, 1)
        if mode == "water":
            value = low * (.8 + .35 * math.sin(t * math.tau / 4)) + .025 * math.sin(t * 110) * math.sin(t * 3)
        elif mode == "rain":
            value = .17 * rng.uniform(-1, 1) + low * .4
        elif mode == "birds":
            chirp = max(0, math.sin(t * math.tau / 3.7)) ** 45
            value = .09 * math.sin(t * 17000 + 90 * math.sin(t * 22)) * chirp
        else:
            chirp = max(0, math.sin(t * math.tau / 2.5)) ** 30
            value = .025 * math.sin(t * 5300 + 60 * math.sin(t * 30)) * chirp + low * .1
        # Seamless value at loop boundaries; long soundscape loops remain unobtrusive.
        fade = min(1, t / .15, (seconds - t) / .15)
        samples.append(value * fade)
    wav(name, samples)


def music():
    beat = 60 / 88
    length = 32 * beat
    samples = [0.0] * int(RATE * length)
    notes = [293.665, 349.228, 391.995, 440.0, 523.251]
    melody = [0, 2, 3, 2, 4, 3, 2, 1, 0, 1, 2, 4, 3, 2, 1, 0]
    for step in range(32):
        frequency = notes[melody[step % len(melody)]]
        start = int(step * beat * RATE)
        for j in range(min(int(beat * 3 * RATE), len(samples) - start)):
            t = j / RATE
            attack = min(1, t * 150)
            # Soft struck-metal partials; original pentatonic motif, no sampled recording.
            tone = math.sin(math.tau * frequency * t) * math.exp(-t * 3)
            tone += .25 * math.sin(math.tau * frequency * 2.76 * t) * math.exp(-t * 6)
            tone += .12 * math.sin(math.tau * frequency * 5.4 * t) * math.exp(-t * 10)
            bass = .3 * math.sin(math.tau * frequency / 4 * t) * math.exp(-t * 2) if step % 4 == 0 else 0
            samples[start + j] += (tone + bass) * attack * .16
    wav("island-theme", samples)


def effects():
    for name, duration, notes in [
        ("cast", .4, [420, 270]), ("bite", .45, [660, 880]),
        ("catch", 1.1, [392, 494, 587, 784]), ("escape", .6, [330, 220, 147]),
        ("ability", .8, [440, 554, 659]), ("ui", .12, [660])
    ]:
        samples = []
        for i in range(int(RATE * duration)):
            t = i / RATE
            segment = min(len(notes) - 1, int(t / duration * len(notes)))
            local = t % (duration / len(notes))
            envelope = min(1, local * 200) * math.exp(-local * 14) * min(1, (duration - t) * 30)
            samples.append(math.sin(math.tau * notes[segment] * t) * envelope * .3)
        wav(name, samples)


def hdr():
    width, height = 512, 256
    target = ROOT / "textures" / "nusantara-sky.hdr"
    target.parent.mkdir(parents=True, exist_ok=True)
    png_rows = bytearray()
    with target.open("wb") as output:
        output.write(f"#?RADIANCE\n# Original Syubadubin Studios analytic tropical lighting\nFORMAT=32-bit_rle_rgbe\n\n-Y {height} +X {width}\n".encode())
        for y in range(height):
            png_rows.append(0)
            row = []
            latitude = (y + .5) / height * math.pi
            elevation = math.cos(latitude)
            for x in range(width):
                longitude = (x + .5) / width * math.tau
                dx, dz = math.sin(latitude) * math.cos(longitude), math.sin(latitude) * math.sin(longitude)
                horizon = math.exp(-abs(elevation) * 5)
                sun = max(0, dx * .4 + elevation * .8 + dz * .4472) ** 700 * 12
                cloud = max(0, math.sin(longitude * 7 + elevation * 15) * math.sin(longitude * 3 - elevation * 9) - .25) * .3 if elevation > 0 else 0
                color = [.08 + .48 * horizon + sun + cloud, .19 + .43 * horizon + sun * .72 + cloud, .32 + .34 * horizon + sun * .4 + cloud]
                if elevation < 0:
                    color = [.035 + horizon * .15, .09 + horizon * .18, .1 + horizon * .19]
                # Fixed-exposure HDR encoding: the renderer decodes sRGB then restores x16 radiance.
                # This avoids unsupported filterable Rgba32Float textures without clipping the sun.
                for component in color:
                    linear = max(0, min(1, component / 16))
                    encoded = linear * 12.92 if linear <= .0031308 else 1.055 * linear ** (1 / 2.4) - .055
                    png_rows.append(round(encoded * 255))
                mantissa, exponent = math.frexp(max(color))
                scale = mantissa * 256 / max(color)
                row.append(bytes([min(255, int(c * scale)) for c in color] + [exponent + 128]))
            output.write(bytes([2, 2, width >> 8, width & 255]))
            for channel in range(4):
                data = bytes(pixel[channel] for pixel in row)
                for start in range(0, width, 127):
                    block = data[start:start + 127]
                    output.write(bytes([len(block)]) + block)
    def chunk(kind, data):
        return struct.pack(">I", len(data)) + kind + data + struct.pack(">I", zlib.crc32(kind + data) & 0xffffffff)
    png = b"\x89PNG\r\n\x1a\n"
    png += chunk(b"IHDR", struct.pack(">IIBBBBB", width, height, 8, 2, 0, 0, 0))
    png += chunk(b"IDAT", zlib.compress(png_rows, 9)) + chunk(b"IEND", b"")
    (ROOT / "textures" / "nusantara-sky-scaled.png").write_bytes(png)


if __name__ == "__main__":
    hdr()
    soundscape("water", 12, "water")
    soundscape("rain", 12, "rain")
    soundscape("night", 10, "night")
    soundscape("birds", 12, "birds")
    music()
    effects()
    print("Generated original HDR environment, theme, ambience, and effects.")
