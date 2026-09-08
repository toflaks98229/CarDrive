# -*- coding: utf-8 -*-
"""
Build the mechs' surface maps from this project's own palette. Procedural,
seamless, and already in the target style.

WHY GENERATE INSTEAD OF DOWNLOADING
  A photo has to be neutralised, normalised and squeezed before it fits, and
  even a purpose-made pixel pack arrives with SOMEBODY ELSE'S palette. This
  game already fixed its own values (concrete 0.60, steel 0.26, dark 0.11), its
  own 4x4 Bayer matrix, and a screen-wide quantisation on top. A foreign
  palette gets quantised twice and its hues argue with the colour grade.

  Generating from those same numbers means the map lands correct with no
  correction step at all, and the source is a script rather than a binary.

WHY EVERY SURFACE IS ISOTROPIC
  The UVs are a world-scale box projection, so a face takes its mapping from
  whichever world axis it points down. Anything directional - formwork seams,
  brushed metal - therefore lies flat across horizontal faces instead of
  running as courses. A surface with no orientation has nothing to get wrong.
  `board_seams` is kept as a switch but ships off.

WHY BOLDER THAN IT SHOULD LOOK
  The map is read through `_BaseMapStrength` (0.45) and then through the
  screen-wide quantisation, so both halve its contrast again. The first pass
  was tuned by eye on the tile alone and vanished completely in game.

Run:  python make_surfaces.py
"""
import math
import os
import random

from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
OUT_DIR = os.path.join(HERE, "..", "..", "Assets", "_Project", "04.Art", "02.Models", "Robot")

SIZE = 256               # 256 px over one metre -> 3.9 mm per texel
TILE_METRES = 1.0

BAYER4 = [
    0, 8, 2, 10,
    12, 4, 14, 6,
    3, 11, 1, 9,
    15, 7, 13, 5,
]


class Surface(object):
    """One map. Everything that makes a material read differently lives here."""

    def __init__(self, name, seed, levels, mean, blotch, grain, speck,
                 pit_count, pit_radius, pit_depth,
                 board_seams=False, tie_holes=False):
        self.name = name
        self.seed = seed
        self.levels = levels
        self.mean = mean
        self.blotch = blotch      # (cells, amplitude) - the broad stain
        self.grain = grain        # (cells, amplitude) - the material's own tooth
        self.speck = speck        # (cells, amplitude) - the finest rattle
        self.pit_count = pit_count
        self.pit_radius = pit_radius
        self.pit_depth = pit_depth
        self.board_seams = board_seams
        self.tie_holes = tie_holes


SURFACES = [
    # Poured concrete: broad staining, coarse tooth, and the blowholes a poured
    # face is covered in. The blotches are what make it read as cast material.
    Surface("Concrete_Brutalist", seed=20260908, levels=8, mean=0.52,
            blotch=(8, 0.215), grain=(32, 0.145), speck=(64, 0.060),
            pit_count=220, pit_radius=(0.9, 3.2), pit_depth=(0.10, 0.34)),

    # Rolled steel plate: almost flat. Metal has no aggregate, so the broad
    # stain nearly disappears and the tooth tightens. What is left is a fine
    # rattle and sparse corrosion pits - deeper than concrete's blowholes but
    # far fewer, so the plate still reads as a hard smooth surface.
    Surface("Steel_Plate", seed=20260909, levels=8, mean=0.52,
            blotch=(12, 0.075), grain=(64, 0.070), speck=(128, 0.055),
            pit_count=70, pit_radius=(0.7, 1.9), pit_depth=(0.16, 0.42)),
]

BOARD_PITCH = 0.15       # 150 mm formwork boards, when board_seams is on
TIE_PITCH = 0.50         # tie-holes every half metre, when tie_holes is on


def to_srgb(v):
    v = max(0.0, min(1.0, v))
    v = v * 12.92 if v <= 0.0031308 else 1.055 * (v ** (1 / 2.4)) - 0.055
    return int(round(v * 255.0))


def to_linear(c):
    return c / 12.92 if c <= 0.04045 else ((c + 0.055) / 1.055) ** 2.4


def lattice(cells, rng):
    return [[rng.random() for _ in range(cells)] for _ in range(cells)]


def sample(grid, x, y):
    """Bilinear sample of a wrapping lattice. x, y in tile units (0..1)."""
    n = len(grid)
    fx, fy = x * n, y * n
    x0, y0 = int(math.floor(fx)) % n, int(math.floor(fy)) % n
    x1, y1 = (x0 + 1) % n, (y0 + 1) % n
    tx, ty = fx - math.floor(fx), fy - math.floor(fy)
    tx = tx * tx * (3 - 2 * tx)
    ty = ty * ty * (3 - 2 * ty)
    a = grid[y0][x0] * (1 - tx) + grid[y0][x1] * tx
    b = grid[y1][x0] * (1 - tx) + grid[y1][x1] * tx
    return a * (1 - ty) + b * ty


def build(surface):
    rng = random.Random(surface.seed)

    # Every lattice size divides the tile, so the noise wraps.
    blotch = lattice(surface.blotch[0], rng)
    grain = lattice(surface.grain[0], rng)
    speck = lattice(surface.speck[0], rng)

    boards = int(round(1.0 / BOARD_PITCH))
    board_tone = [rng.uniform(-0.105, 0.105) for _ in range(boards)]

    # Scatter the pits once, then look them up per pixel. Wrapping the distance
    # test keeps them seamless across the tile edge.
    pits = [(rng.random() * SIZE, rng.random() * SIZE,
             rng.uniform(*surface.pit_radius), rng.uniform(*surface.pit_depth))
            for _ in range(surface.pit_count)]

    steps = surface.levels - 1
    pixels = []

    for py in range(SIZE):
        v = py / SIZE
        for px in range(SIZE):
            u = px / SIZE

            value = surface.mean
            value += (sample(blotch, u, v) - 0.5) * surface.blotch[1]
            value += (sample(grain, u, v) - 0.5) * surface.grain[1]
            value += (sample(speck, u, v) - 0.5) * surface.speck[1]

            if surface.board_seams:
                value += board_tone[int(v * boards) % boards]
                seam = (v * boards) % 1.0
                if seam < 0.045 or seam > 0.955:
                    value -= 0.30
                elif seam < 0.10:
                    value += 0.085

            if surface.tie_holes:
                ties = int(round(1.0 / TIE_PITCH))
                tu = ((u * ties) % 1.0 - 0.5)
                tv = ((v * ties) % 1.0 - 0.5)
                d = math.sqrt(tu * tu + tv * tv) / ties * SIZE * TILE_METRES
                if d < 3.4:
                    value -= 0.46 * (1.0 - d / 3.4)

            for (cx, cy, r, depth) in pits:
                dx = abs(px - cx)
                dy = abs(py - cy)
                if dx > SIZE * 0.5:
                    dx = SIZE - dx          # wrap, so pits survive the seam
                if dy > SIZE * 0.5:
                    dy = SIZE - dy
                if dx > r or dy > r:
                    continue
                dist = math.sqrt(dx * dx + dy * dy)
                if dist < r:
                    value -= depth * (1.0 - dist / r)

            # Quantise with the game's own Bayer matrix, in gamma so the steps
            # land evenly to the eye instead of piling up in the highlights.
            encoded = math.sqrt(max(value, 0.0))
            threshold = (BAYER4[(py % 4) * 4 + (px % 4)] / 16.0) - 0.5
            q = math.floor(encoded * steps + 0.5 + threshold) / steps
            q = min(max(q, 0.0), 1.0) ** 2

            s = to_srgb(q)
            pixels.append((s, s, s))

    image = Image.new("RGB", (SIZE, SIZE))
    image.putdata(pixels)

    os.makedirs(os.path.abspath(OUT_DIR), exist_ok=True)
    path = os.path.abspath(os.path.join(OUT_DIR, surface.name + ".png"))
    image.save(path)

    greys = sorted(set(p[0] for p in pixels))
    mean = sum(to_linear(p[1] / 255.0) for p in pixels) / len(pixels)

    print("%-20s %d greys %-28s linear mean %.4f  ->  %s"
          % (surface.name, len(greys), str(greys), mean, os.path.basename(path)))
    return path


if __name__ == "__main__":
    print("%dx%d, %.1f mm per texel over %.2f m"
          % (SIZE, SIZE, TILE_METRES * 1000.0 / SIZE, TILE_METRES))
    for s in SURFACES:
        build(s)
