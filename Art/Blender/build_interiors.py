# -*- coding: utf-8 -*-
"""
Room shells for CarDrive - the village shop and home, and the cells inside the
megastructure.

WHY ONE GENERATOR FOR BOTH
  A shop you walk into and a capsule plugged into a megaframe are the same
  problem: a floor, four walls with holes in them, a ceiling, and a way in.
  What differs is the size and what stands inside. Writing them twice would
  guarantee they drift apart, and drifting apart is the one thing this art
  direction cannot survive.

THE SCENE ROOMS ARE MEASURED, NOT INVENTED
  The shop and the home are built directly in the scene, not as prefabs, and
  the counter, shelves, clerk and bed are positioned against their walls. So
  the shell is rebuilt to the SAME clear dimensions and the SAME openings:

    Mart  12.40 x 9.40 x 3.20 inner · door 1.4 m on -Z · window 2.0 x 1.1 on +X
    Home   8.40 x 6.40 x 2.80 inner · door 1.0 m on -Z · window 1.6 x 1.0 on +X

  Change those and the clerk ends up inside a wall.

THE CAPSULE ROOMS ARE THE INSIDE OF A MODULE
  The megastructure's capsules are 6.4 x 5.0 x 4.4 m. A room preset is what one
  looks like from within: the frame is concrete and outlives everything, the
  fit-out is steel and dark and clearly added later. That reading is the whole
  point of the style, so the interior has to say it too.

  <b>No booleans.</b> An opening is built as the boxes AROUND it, exactly as
  outside. A window is a deep reveal, not a painted rectangle.

Run:
    blender -b --python build_interiors.py
"""
import json
import os
import random
import sys

import bpy

HERE = os.path.dirname(os.path.abspath(__file__))
if HERE not in sys.path:
    sys.path.insert(0, HERE)

import hardsurface  # noqa: E402
import uv_worldscale  # noqa: E402

OUT_DIR = r"E:\GamePJ\CarDrive\Assets\_Project\04.Art\02.Models\Interior"
BLEND_PATH = r"E:\GamePJ\CarDrive\Art\Blender\Interiors.blend"
UV_TILE = 1.0

MATS = [
    ("M_Mega_Concrete", (0.465, 0.452, 0.430, 1.0), 0.95, 0.00),
    ("M_Mega_Steel", (0.185, 0.192, 0.205, 1.0), 0.45, 1.00),
    ("M_Mega_Dark", (0.048, 0.050, 0.054, 1.0), 0.65, 1.00),
]

CONCRETE, STEEL, DARK = 0, 1, 2

# 벽 두께. 옛 방과 같아야 안쪽 치수가 유지됩니다.
WALL = 0.20
SLAB = 0.20

# --- Presets ----------------------------------------------------------------
# openings: (벽, 벽 안에서의 위치, 폭, 아래 높이, 높이)
#   벽은 -z / +z / -x / +x. 아래 높이가 0 이면 문, 아니면 창입니다.

ROOMS = {
    # 마을 상점. <b>치수는 재서 얻은 것</b>이고 바꾸면 점원이 벽에 박힙니다.
    "Mart": dict(inner=(12.40, 9.40, 3.20), roof=1.40, over=0.20,
                 openings=(("-z", 0.0, 1.40, 0.0, 2.10),
                           ("+x", 0.0, 2.00, 1.00, 1.10)),
                 shopfront=True),

    # 플레이어의 집.
    "Home": dict(inner=(8.40, 6.40, 2.80), roof=1.40, over=0.20,
                 openings=(("-z", 0.0, 1.00, 0.0, 2.05),
                           ("+x", 0.0, 1.60, 1.00, 1.00),)),

    # 메가스트럭처의 거주 캡슐 안. 골조가 콘크리트, 살림이 강철입니다.
    "Cell": dict(inner=(6.00, 4.60, 3.60), roof=0.0, over=0.0,
                 openings=(("-z", 0.0, 0.95, 0.0, 2.05),
                           ("+x", 0.0, 2.40, 0.90, 1.40)),
                 bunk=True),

    # 설비실. 창이 없고 덕트와 반이 벽을 채웁니다.
    "Plant": dict(inner=(6.00, 4.60, 3.60), roof=0.0, over=0.0,
                  openings=(("-z", 0.0, 1.10, 0.0, 2.05),),
                  plant=True),

    # 통로. 양쪽이 뚫려 캡슐과 캡슐을 잇습니다.
    "Corridor": dict(inner=(4.00, 6.00, 3.20), roof=0.0, over=0.0,
                     openings=(("-z", 0.0, 2.20, 0.0, 2.40),
                               ("+z", 0.0, 2.20, 0.0, 2.40),
                               ("+x", 0.0, 1.20, 1.30, 1.00)),
                     corridor=True),
}


# --- Shell ------------------------------------------------------------------


def wall(m, side, inner, height, openings):
    """
    벽 하나입니다. <b>구멍은 뚫지 않고 구멍 둘레를 세웁니다.</b>

    문이든 창이든 같은 처리입니다 - 좌우의 살, 위의 인방, 창이면 아래의 하방.
    아래 높이가 0 이면 문이고, 그러면 하방이 없습니다.
    """
    w, d, _ = inner
    half = (w if side in ("-z", "+z") else d) * 0.5

    # 벽면의 바깥쪽 부호와 벽이 놓이는 축.
    axis = 0 if side in ("-x", "+x") else 1
    sign = -1.0 if side.startswith("-") else 1.0
    at = (d if axis else w) * 0.5 + WALL * 0.5

    def put(lo, hi, z0, z1, mat=CONCRETE):
        if hi - lo <= 1e-6 or z1 - z0 <= 1e-6:
            return

        c = [0.0, 0.0, (z0 + z1) * 0.5]
        s = [0.0, 0.0, z1 - z0]
        c[axis] = sign * at
        s[axis] = WALL
        c[1 - axis] = (lo + hi) * 0.5
        s[1 - axis] = hi - lo

        m.box(c, s, mat)

    mine = sorted((o[1], o[2], o[3], o[4]) for o in openings if o[0] == side)

    # <b>모서리는 한 쌍만 채웁니다.</b> 넷이 다 모서리까지 나가면 두 벽이 같은
    # WALL x WALL 기둥을 차지하고, 그러면 한쪽의 끝면과 다른 쪽의 바깥면이
    # <b>같은 평면에서 같은 쪽을</b> 봐 깜빡입니다. 방 다섯에서 잡힌 81쌍의
    # 절반이 이것이었습니다. 좌우 벽이 모서리까지 가고 앞뒤 벽은 안에서 멈춥니다.
    grow = WALL if axis == 0 else 0.0

    edge = -half - grow
    for pos, width, sill, tall in mine:
        put(edge, pos - width * 0.5, 0.0, height)
        edge = pos + width * 0.5

        if sill > 0.0:
            put(pos - width * 0.5, pos + width * 0.5, 0.0, sill)

        put(pos - width * 0.5, pos + width * 0.5, sill + tall, height)

        # 구멍의 <b>속</b>. 깊은 인방이 그늘을 만들어 구멍으로 읽히게 합니다.
        # 인방을 구멍 머리보다 <b>6 cm 아래로</b> 늘어뜨립니다. 예전에는 밑면이
        # 구멍 위 벽의 밑면과 정확히 같은 높이라 그 자리가 깜빡였습니다.
        # 실제 인방도 구멍보다 내려와 걸칩니다.
        c = [0.0, 0.0, sill + tall + 0.16 - 0.06]
        s = [0.0, 0.0, 0.32]
        # 인방을 <b>벽면보다 안으로</b> 들입니다. 예전에는 바깥면이 벽면과 정확히
        # 같은 평면이라 그 자리가 깜빡였습니다. 6 cm 들어가면 그늘이 한 겹 더 생겨
        # 구멍이 오히려 깊어 보입니다.
        c[axis] = sign * (at - WALL * 0.35 - 0.03)
        s[axis] = WALL * 1.7 - 0.06
        c[1 - axis] = pos
        s[1 - axis] = width + 0.36
        m.box(c, s, DARK)

    put(edge, half + grow, 0.0, height)


def shell(name, s):
    """바닥·벽·천장·지붕입니다. 원점은 <b>안쪽 바닥의 한가운데</b>입니다."""
    m = hardsurface.Mass(MATS)
    w, d, h = s["inner"]

    # 바닥과 천장. 벽 두께만큼 밖으로 나가 벽 밑을 받칩니다.
    m.box((0.0, 0.0, -SLAB * 0.5), (w + WALL * 2.0, d + WALL * 2.0, SLAB), CONCRETE)
    m.box((0.0, 0.0, h + SLAB * 0.5), (w + WALL * 2.0, d + WALL * 2.0, SLAB), CONCRETE)

    for side in ("-z", "+z", "-x", "+x"):
        wall(m, side, s["inner"], h, s["openings"])

    # 지붕. 캡슐 안쪽에는 없습니다 - 위가 이미 골조입니다.
    if s["roof"] > 0.0:
        over = s["over"]
        m.box((0.0, 0.0, h + SLAB + s["roof"] * 0.5),
              (w + WALL * 2.0 + over * 2.0, d + WALL * 2.0 + over * 2.0, s["roof"]),
              CONCRETE)

        # 처마 밑의 그늘 선. 밖에서 볼 때 지붕이 <b>얹힌 것</b>으로 보이게 합니다.
        m.box((0.0, 0.0, h + SLAB - 0.14),
              (w + WALL * 2.0 + over * 1.4, d + WALL * 2.0 + over * 1.4, 0.18), DARK)

    fittings(m, s, w, d, h)
    return m.to_object("SM_Room_" + name)


def fittings(m, s, w, d, h):
    """
    안에 서는 것들입니다. <b>골조는 콘크리트, 나중에 들인 것은 강철</b>입니다.

    이 구분이 메가스트럭처의 3·4 번을 방 안에서도 말합니다 - 벽은 영구, 살림은 임시.
    """
    rng = random.Random(abs(hash(s["inner"])) % 99991)

    if s.get("shopfront"):
        # 진열창 아래의 낮은 벽과 그 위의 차양. 상점이라는 것을 밖에서 알립니다.
        m.box((0.0, -d * 0.5 - WALL * 0.5, h + SLAB - 0.5), (w * 0.7, WALL * 3.0, 0.35), STEEL)

        # 안쪽 선반 띠. 진열대는 씬이 갖고 있으므로 <b>벽에 붙는 것</b>만 만듭니다.
        for i in (-1, 1):
            m.box((i * (w * 0.5 - 0.35), 0.0, 1.05), (0.6, d * 0.72, 0.08), STEEL)
            m.box((i * (w * 0.5 - 0.35), 0.0, 1.85), (0.6, d * 0.72, 0.08), STEEL)

    if s.get("bunk"):
        # 접이식 침상과 사물함. 캡슐 하나에 사람 하나가 산다는 치수입니다.
        m.box((-w * 0.5 + 1.05, d * 0.25, 0.62), (2.0, 0.9, 0.14), STEEL)
        m.box((-w * 0.5 + 0.12, d * 0.25, 1.10), (0.2, 0.9, 1.1), DARK)
        m.box((w * 0.5 - 0.42, -d * 0.5 + 0.7, 1.0), (0.8, 0.6, 2.0), STEEL)

        # 천장의 설비 트레이. 캡슐은 골조에서 물과 전기를 받습니다.
        m.box((0.0, d * 0.5 - 0.5, h - 0.22), (w * 0.9, 0.4, 0.3), DARK)

    if s.get("plant"):
        # 덕트와 반. 벽을 채우는 것이 설비실의 성격입니다.
        for i in range(3):
            m.box((-w * 0.5 + 0.45, -d * 0.4 + i * (d * 0.4), h * 0.5),
                  (0.8, 0.9, h - 0.4), STEEL)

        m.box((0.0, d * 0.5 - 0.35, h - 0.45), (w * 0.9, 0.6, 0.6), STEEL)
        m.box((w * 0.5 - 0.3, 0.0, 1.3), (0.5, d * 0.6, 1.9), DARK)

    if s.get("corridor"):
        # 손잡이 난간과 바닥의 격자판. 지나가는 곳이라는 표시입니다.
        for i in (-1, 1):
            m.box((i * (w * 0.5 - 0.18), 0.0, 1.0), (0.12, d * 0.86, 0.1), STEEL)

        m.box((0.0, 0.0, 0.03), (w * 0.62, d * 0.94, 0.06), DARK)


# --- Run --------------------------------------------------------------------


def run():
    report = []

    for name in ROOMS:
        hardsurface.wipe()
        hardsurface.ensure_materials(MATS)

        s = ROOMS[name]
        obj = shell(name, s)
        uv_worldscale.box_uv(obj, UV_TILE)

        path = os.path.join(OUT_DIR, obj.name + ".fbx")
        written = hardsurface.export_fbx(path)

        lo, hi = hardsurface.bounds(obj)
        w, d, h = s["inner"]

        report.append(dict(
            name=obj.name, room=name,
            inner=[w, d, h],
            outer=[round(hi[a] - lo[a], 2) for a in range(3)],
            floor=round(lo[2], 3),
            openings=len(s["openings"]),
            tris=sum(len(p.vertices) - 2 for p in obj.data.polygons),
            fbx=written))

    return report


def lay_out():
    """방을 한 줄로 세워 blend 에 남깁니다."""
    hardsurface.wipe()
    hardsurface.ensure_materials(MATS)

    x = 0.0
    for name in ROOMS:
        obj = shell(name, ROOMS[name])
        uv_worldscale.box_uv(obj, UV_TILE)
        obj.location.x = x
        x += ROOMS[name]["inner"][0] + 4.0

    bpy.ops.wm.save_as_mainfile(filepath=BLEND_PATH)


if __name__ == "__main__":
    out = run()
    lay_out()
    print("###JSON###" + json.dumps(out))
