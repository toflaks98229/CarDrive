# -*- coding: utf-8 -*-
"""
Build the brutalist concrete maps this project needs, from this project's own
palette. Procedural, seamless, and already in the target style.

WHY GENERATE INSTEAD OF DOWNLOADING
  A photo has to be neutralised, normalised and squeezed before it fits, and
  even a purpose-made pixel pack arrives with SOMEBODY ELSE'S palette. This
  game already fixed its own values (concrete 0.60, steel 0.26, dark 0.11), its
  own 4x4 Bayer matrix, and now a screen-wide quantisation on top. A foreign
  palette gets quantised twice and its hues argue with the colour grade.

  Generating from those same numbers means the map lands correct with no
  correction step at all, and the source is a script rather than a binary.

WHAT IT DRAWS
  Board-formed concrete - the brutalist finish where the formwork boards are
  left legible in the surface. Every feature is something the formwork actually
  does: board seams, the slight value step between adjacent boards, tie-holes
  on the tie grid, aggregate pitting, and streaking below the ties.

SEAMLESS
  The tile is exactly `TILE_METRES` of real surface, matching the world-scale
  UVs (uv_worldscale.py). Noise is lattice noise on a torus and every feature
  pitch divides the tile, so it repeats without a seam.

Run:  python make_concrete.py
"""
import math
import os
import random

from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
OUT_DIR = os.path.join(HERE, "..", "..", "Assets", "_Project", "04.Art", "02.Models", "Robot")

SIZE = 256               # 256 px over one metre -> 3.9 mm per texel
TILE_METRES = 1.0

# Few greys, dithered - the same bargain the rest of the game makes.
LEVELS = 8
MEAN = 0.52              # linear mean; the tint's gain normalises it anyway

# The map is read through _BaseMapStrength (0.45) and then through the screen-wide
# quantisation, so both halve its contrast again. It has to be bolder standalone
# than it should look in game - the first pass was too timid and the board seams
# vanished entirely.

# Board seams and tie-holes are DIRECTIONAL. The UVs are a box projection, so a
# face takes its mapping from whichever world axis it points down - which means
# the seams lie flat across horizontal faces instead of running as courses. An
# isotropic surface has no orientation to get wrong, so the default is off and
# the aggregate carries the read on its own.
BOARD_SEAMS = False
TIE_HOLES = False

BOARD_PITCH = 0.15       # 150 mm formwork boards
TIE_PITCH = 0.50         # tie-holes every half metre

# Blowholes: the little voids a poured face is covered in. Isotropic, and the
# thing that makes concrete read as concrete once the seams are gone.
PIT_COUNT = 220
PIT_RADIUS = (0.9, 3.2)  # texels
PIT_DEPTH = (0.10, 0.34)
SEED = 20260908

BAYER4 = [
    0, 8, 2, 10,
    12, 4, 14, 6,
    3, 11, 1, 9,
    15, 7, 13, 5,
]


def to_srgb(v):
    v = max(0.0, min(1.0, v))
    v = v * 12.92 if v <= 0.0031308 else 1.055 * (v ** (1 / 2.4)) - 0.055
    return int(round(v * 255.0))


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


def build():
    rng = random.Random(SEED)

    # Aggregate at three scales. Every lattice size divides the tile, so the
    # noise wraps.
    coarse = lattice(8, rng)
    medium = lattice(32, rng)
    fine = lattice(64, rng)

    # One value offset per board, so adjacent boards sit at slightly different
    # tones the way real formwork leaves them.
    boards = int(round(1.0 / BOARD_PITCH))
    board_tone = [rng.uniform(-0.105, 0.105) for _ in range(boards)]

    # Scatter the blowholes once, then look them up per pixel. Wrapping the
    # distance test keeps them seamless across the tile edge.
    pits = [(rng.random() * SIZE, rng.random() * SIZE,
             rng.uniform(*PIT_RADIUS), rng.uniform(*PIT_DEPTH))
            for _ in range(PIT_COUNT)] if not BOARD_SEAMS else []

    pixels = []
    for py in range(SIZE):
        v = py / SIZE
        for px in range(SIZE):
            u = px / SIZE

            value = MEAN

            # ---- aggregate / pitting -------------------------------------
            value += (sample(coarse, u, v) - 0.5) * (0.150 if BOARD_SEAMS else 0.215)
            value += (sample(medium, u, v) - 0.5) * (0.105 if BOARD_SEAMS else 0.145)
            value += (sample(fine, u, v) - 0.5) * 0.060

            # ---- board tone and seam (directional - off by default) -------
            if BOARD_SEAMS:
                board = int(v * boards) % boards
                value += board_tone[board]

                seam = abs(((v * boards) % 1.0) - 0.0)
                if seam < 0.045 or seam > 0.955:
                    value -= 0.30
                elif seam < 0.10:
                    value += 0.085

            if TIE_HOLES:
                ties = int(round(1.0 / TIE_PITCH))
                tu = ((u * ties) % 1.0 - 0.5)
                tv = ((v * ties) % 1.0 - 0.5)
                d = math.sqrt(tu * tu + tv * tv) / ties * SIZE * TILE_METRES

                if d < 3.4:
                    value -= 0.46 * (1.0 - d / 3.4)
                elif d < 5.0:
                    value += 0.09 * (1.0 - (d - 3.4) / 1.6)

                below = ((v * ties) % 1.0) - 0.5
                if 0.0 < below < 0.34 and abs(tu) < 0.06:
                    value -= 0.105 * (1.0 - below / 0.34)

            # ---- blowholes -----------------------------------------------
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

            # ---- quantise with the game's own Bayer matrix ---------------
            steps = LEVELS - 1
            encoded = math.sqrt(max(value, 0.0))
            threshold = (BAYER4[(py % 4) * 4 + (px % 4)] / 16.0) - 0.5
            q = math.floor(encoded * steps + 0.5 + threshold) / steps
            q = min(max(q, 0.0), 1.0) ** 2

            s = to_srgb(q)
            pixels.append((s, s, s))

    image = Image.new("RGB", (SIZE, SIZE))
    image.putdata(pixels)

    os.makedirs(os.path.abspath(OUT_DIR), exist_ok=True)
    path = os.path.abspath(os.path.join(OUT_DIR, "Concrete_Brutalist.png"))
    image.save(path)

    greys = sorted(set(p[0] for p in pixels))
    lin = [(g / 255.0) for g in (p[1] for p in pixels)]

    def to_linear(c):
        return c / 12.92 if c <= 0.04045 else ((c + 0.055) / 1.055) ** 2.4

    mean = sum(to_linear(x) for x in lin) / len(lin)

    print("size        %dx%d  (%0.1f mm per texel over %.2f m)"
          % (SIZE, SIZE, TILE_METRES * 1000.0 / SIZE, TILE_METRES))
    print("unique greys %d  %s" % (len(greys), greys))
    print("linear mean  %.4f  (target %.2f)" % (mean, MEAN))
    print("written", path)
    return path


if __name__ == "__main__":
    build()
