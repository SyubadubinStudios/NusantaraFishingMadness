"""Original tileable island ground textures: jungle floor and wet rock. Requires numpy and Pillow.

python assets/sources/generate_island_textures.py
"""
from pathlib import Path
import numpy as np
from PIL import Image

ROOT = Path(__file__).resolve().parents[2]
SIZE = 1024
rng = np.random.default_rng(431)


def noise(octave_scale, power=2.0):
    """Periodic fractal noise from filtered white noise in frequency space, so edges tile."""
    white = rng.standard_normal((SIZE, SIZE))
    fx = np.fft.fftfreq(SIZE)[:, None]
    fy = np.fft.fftfreq(SIZE)[None, :]
    radius = np.sqrt(fx * fx + fy * fy) * SIZE / octave_scale
    spectrum = np.fft.fft2(white) / np.maximum(radius, 1) ** power
    field = np.real(np.fft.ifft2(spectrum))
    return (field - field.mean()) / field.std()


def blades(count, length, width):
    """Short anisotropic strokes for grass and fallen leaf edges, wrapped around the tile."""
    canvas = np.zeros((SIZE, SIZE))
    ys, xs = rng.integers(0, SIZE, count), rng.integers(0, SIZE, count)
    angles = rng.normal(-np.pi / 2, .5, count)
    for y, x, a in zip(ys, xs, angles):
        for t in range(length):
            for w in range(-width, width + 1):
                py = int(y + np.sin(a) * t + np.cos(a) * w) % SIZE
                px = int(x + np.cos(a) * t - np.sin(a) * w) % SIZE
                canvas[py, px] = max(canvas[py, px], 1 - t / length)
    return canvas


def ramp(value, stops):
    positions = np.array([p for p, _ in stops])
    colors = np.array([c for _, c in stops], dtype=float)
    out = np.empty(value.shape + (3,))
    for channel in range(3):
        out[..., channel] = np.interp(value, positions, colors[:, channel])
    return out


def jungle_floor():
    large, medium, fine = noise(6, 1.6), noise(24, 1.4), noise(140, 1.0)
    moss = .55 * large + .3 * medium + .15 * fine
    litter = np.clip(noise(40, 1.2) - .35, 0, None)
    base = ramp(moss, [(-2.2, (34, 46, 22)), (-.6, (52, 74, 30)), (.4, (74, 102, 40)), (1.6, (104, 126, 52)), (2.6, (128, 140, 66))])
    leaf = ramp(noise(90, 1.0), [(-2, (78, 52, 26)), (0, (112, 78, 38)), (2, (146, 108, 52))])
    mix = np.clip(litter * 1.4, 0, .75)[..., None]
    color = base * (1 - mix) + leaf * mix
    grass = blades(5200, 22, 1)[..., None]
    color = color * (1 - grass * .5) + np.array([118, 152, 58]) * grass * .5
    soil = np.clip(-noise(18, 1.5) - 1.1, 0, 1)[..., None]
    color = color * (1 - soil * .6) + np.array([58, 42, 28]) * soil * .6
    shade = (1 + .12 * fine)[..., None]
    return np.clip(color * shade, 0, 255).astype(np.uint8)


def wet_rock():
    strata = np.sin(np.linspace(0, 12 * np.pi, SIZE))[:, None] * .35
    value = .5 * noise(8, 1.7) + .3 * noise(40, 1.3) + .2 * noise(200, .9) + strata
    color = ramp(value, [(-2.4, (46, 50, 48)), (-.8, (78, 82, 74)), (.6, (112, 112, 98)), (2.2, (150, 146, 126))])
    algae = np.clip(noise(16, 1.4) - .4, 0, 1)[..., None] * .55
    color = color * (1 - algae) + np.array([58, 86, 46]) * algae
    return np.clip(color, 0, 255).astype(np.uint8)


if __name__ == '__main__':
    out = ROOT / 'assets/textures'
    Image.fromarray(jungle_floor()).save(out / 'jungle-floor.jpg', quality=88, optimize=True)
    Image.fromarray(wet_rock()).save(out / 'wet-rock.jpg', quality=88, optimize=True)
    print('wrote jungle-floor.jpg and wet-rock.jpg')
