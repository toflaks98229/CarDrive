# -*- coding: utf-8 -*-
"""
Seeded buildings for CarDrive, sized to replace the frozen placeholder houses.

WHAT THIS REPLACES
  The six houses standing in the world are code-generated .asset meshes of
  EIGHTEEN TRIANGLES each - a box and a prism roof. They are placeholders, and
  the tool that made them was deleted in commit 1170605, so there is no way to
  regenerate or edit them. Meanwhile the art direction moved to brutalism and a
  96 m megastructure now crosses the sky above them.

  These are their replacements: the same concrete logic as the megastructure,
  at domestic scale. Service buildings at the foot of a viaduct.

THE ONE HARD CONSTRAINT
  The scene has the houses' positions and rotations baked in, and the world is
  frozen. So a replacement MUST fit the old envelope - same footprint, floor at
  z = 0 - or buildings sink, float, or turn their doors into hillsides. Each
  seed therefore takes its width, depth and height as INPUT and composes inside
  them. That is the opposite of the megastructure, which chose its own size.

DESIGN
  * Board-formed concrete walls on a plinth, one heavy roof slab with a deep
    overhang. The overhang is what makes it brutalist rather than a shed: it
    casts a hard band of shadow across the wall all day.
  * Windows are DEEP REVEALS, not painted rectangles - see hardsurface.reveal.
  * One service mass (stair, chimney or lean-to) breaks the box. Rural utility
    buildings always have one, and without it the silhouette is a carton.
  * No chamfer, no booleans, world-scale UV at 1 m - same as everything else.

Run:
    blender -b --python build_buildings.py
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

OUT_DIR = r"E:\GamePJ\CarDrive\Assets\_Project\04.Art\02.Models\Building"
BLEND_PATH = r"E:\GamePJ\CarDrive\Art\Blender\Buildings.blend"
UV_TILE = 1.0

MATS = [
    ("M_Mega_Concrete", (0.465, 0.452, 0.430, 1.0), 0.95, 0.00),
    ("M_Mega_Steel", (0.185, 0.192, 0.205, 1.0), 0.45, 1.00),
    ("M_Mega_Dark", (0.048, 0.050, 0.054, 1.0), 0.65, 1.00),
]

CONCRETE, STEEL, DARK = 0, 1, 2

# 옛 집의 봉투입니다. <b>측량해서 얻은 값이고 바꾸면 안 됩니다</b> — 씬에 위치가
# 박혀 있어서, 크기가 달라지면 집이 땅에 묻히거나 뜹니다.
# (BuildingSetup.Survey 가 찍어 준 값입니다.)
ENVELOPE = {
    "House01": (9.11, 6.62, 6.07),
    "House02": (9.45, 7.36, 6.69),
    "House03": (10.94, 7.58, 8.08),
    "House04": (7.38, 9.39, 7.07),
    "House05": (7.15, 8.52, 7.88),
    "House06": (11.77, 7.82, 6.46),
}


def spec(name, seed):
    """
    봉투 하나에서 한 채의 치수를 뽑습니다.

    <b>봉투는 입력이고 구성만 씨앗이 정합니다.</b> 창이 몇 칸인지, 지붕이 한쪽으로
    기우는지, 서비스 덩어리가 계단인지 굴뚝인지 — 그런 것만 흔듭니다. 크기를
    흔들면 씬에 박힌 자리와 어긋납니다.
    """
    rng = random.Random(seed)
    w, d, h = ENVELOPE[name]

    return dict(
        name=name, seed=seed, width=w, depth=d, height=h,
        plinth=rng.uniform(0.5, 0.9),
        roof=rng.uniform(0.42, 0.62),
        eave=rng.uniform(0.55, 1.0),
        # 한쪽으로 기운 지붕. 평지붕만 여섯 채면 창고 단지로 보입니다.
        mono=rng.random() < 0.55,
        rise=rng.uniform(0.5, 1.1),
        bays=rng.randint(2, 4),
        floors=2 if h > 7.0 else 1,
        win=(rng.uniform(1.0, 1.5), rng.uniform(1.1, 1.6)),
        service=rng.choice(("stair", "stack", "lean")),
        band=rng.random() < 0.6,
    )


def build(s):
    """
    봉투 안에서 한 채를 세웁니다. 원점은 <b>바닥 한가운데</b>입니다.

    <b>봉투에서 빼면서 짓습니다.</b> 처음에는 본체를 봉투 크기로 잡고 처마와 서비스
    덩어리를 <b>더했더니</b> 여섯 채가 전부 봉투를 넘었습니다(가로 최대 +1.5 m).
    씬에 자리가 박혀 있으므로 넘으면 이웃을 파고듭니다. 그래서 튀어나올 것들을
    먼저 빼고 남은 것이 본체입니다:

        가로 = 본체 + 처마 양쪽 + 서비스 한쪽
        세로 = 본체 + 처마 양쪽
        높이 = 본체 + 지붕 (+ 경사) (+ 서비스가 지붕 위로 솟는 몫)
    """
    m = hardsurface.Mass(MATS)
    rng = random.Random(s["seed"] * 7717 + 3)

    w, d, h = s["width"], s["depth"], s["height"]
    plinth = s["plinth"]
    roof = s["roof"]
    eave = s["eave"]

    side = 1.0 if rng.random() < 0.5 else -1.0
    take = 1.9 if s["service"] == "lean" else 1.2

    bw = w - take - eave * 2.0
    bd = d - eave * 2.0

    # 본체를 서비스 반대쪽으로 밀어, 본체+서비스가 봉투 한가운데에 놓이게 합니다.
    cx = -side * take * 0.5

    # 계단·굴뚝은 지붕 위로 솟습니다. 그 몫만큼 지붕을 낮춰야 봉투 안에 듭니다.
    over = 0.0 if s["service"] == "lean" else rng.uniform(0.6, 2.2)
    roof_top = h - over
    rise = s["rise"] if s["mono"] else 0.0
    body_top = roof_top - roof - rise

    # ---- 기단 -------------------------------------------------------------
    # 시골 콘크리트 건물은 바닥이 젖으므로 한 단 올려 짓습니다. 그 한 단이
    # 벽과 땅 사이에 그림자 선을 만들어 건물이 <b>놓인 것</b>으로 보이게 합니다.
    m.box((cx, 0.0, plinth * 0.5), (bw + 0.5, bd + 0.5, plinth), CONCRETE)

    # ---- 벽 ---------------------------------------------------------------
    m.box((cx, 0.0, (plinth + body_top) * 0.5), (bw, bd, body_top - plinth), CONCRETE)

    if s["band"]:
        # 층 사이의 띠. 층수를 세게 만들어 크기를 알려 줍니다.
        m.box((cx, 0.0, plinth + (body_top - plinth) * 0.5),
              (bw + 0.28, bd + 0.28, 0.34), DARK)

    # ---- 창 ---------------------------------------------------------------
    ww, wh = s["win"]
    pitch = bw / s["bays"]

    for floor in range(s["floors"]):
        z = plinth + (body_top - plinth) * (floor + 0.62) / s["floors"]
        if z + wh * 0.5 > body_top - 0.4:
            continue

        for i in range(s["bays"]):
            if rng.random() < 0.18:
                continue

            x = cx - bw * 0.5 + (i + 0.5) * pitch
            for sign in (-1.0, 1.0):
                m.reveal((x, sign * bd * 0.5, z), (ww, wh), 1, sign, CONCRETE, DARK)

    # 서비스 반대쪽 옆면에 좁고 긴 것 하나. 브루탈리즘의 계단실 창입니다.
    tall = max(1.0, body_top - plinth - 1.6)
    m.reveal((cx - side * bw * 0.5, bd * 0.18, plinth + (body_top - plinth) * 0.55),
             (0.55, tall), 0, -side, CONCRETE, DARK)

    # ---- 지붕 -------------------------------------------------------------
    # <b>깊은 처마가 이 건물을 브루탈리즘으로 만듭니다.</b> 처마가 없으면 창고입니다.
    if s["mono"]:
        # 한쪽으로 기운 판. 계단으로 흉내 냅니다 — 경사면 하나를 만들면 상자
        # 언어가 깨지고, 계단은 오히려 <b>부어 만든 단</b>으로 읽힙니다.
        steps = 3
        for i in range(steps):
            t = (i + 0.5) / steps
            m.box((cx, (t - 0.5) * (bd + eave * 2.0),
                   body_top + rise * t + roof * 0.5),
                  (bw + eave * 2.0, (bd + eave * 2.0) / steps + 0.02, roof), CONCRETE)
    else:
        m.box((cx, 0.0, body_top + roof * 0.5),
              (bw + eave * 2.0, bd + eave * 2.0, roof), CONCRETE)

    # 처마 밑의 어두운 선. 그림자를 그림자로 못 믿을 때를 위한 보험입니다.
    # 선을 지붕 <b>밑으로</b> 내립니다. 윗면을 지붕 밑면과 같은 높이에 두면 서로
    # 반대를 보아 괜찮지만, 경사 지붕에서는 계단마다 높이가 달라 한쪽이 같은 쪽을
    # 보게 됩니다. 조금 내려 두면 어느 경우에도 겹치지 않습니다.
    m.box((cx, 0.0, body_top - 0.22), (bw + eave * 1.6, bd + eave * 1.6, 0.18), DARK)

    # ---- 서비스 덩어리 -----------------------------------------------------
    edge = cx + side * (bw * 0.5 + take * 0.5)

    if s["service"] == "stair":
        m.box((edge, bd * 0.12, h * 0.5), (take, bd * 0.52, h), CONCRETE)
        # 뚜껑은 덩어리 <b>위에</b> 얹습니다. 같은 높이에서 끝나게 두었더니 두 윗면이
        # 같은 평면에서 같은 쪽을 봐 깜빡였습니다.
        m.box((edge, bd * 0.12, h + 0.16), (take + 0.4, bd * 0.52 + 0.4, 0.32), DARK)
        m.reveal((edge, bd * 0.12 - bd * 0.26, h * 0.55),
                 (take * 0.36, h * 0.5), 1, -1.0, CONCRETE, DARK)

    elif s["service"] == "stack":
        m.box((edge, -bd * 0.18, h * 0.5), (take * 0.62, take * 0.62, h), CONCRETE)
        m.box((edge, -bd * 0.18, h + 0.2), (take * 0.82, take * 0.82, 0.4), STEEL)

    else:  # lean
        top = plinth + (body_top - plinth) * rng.uniform(0.45, 0.62)
        m.box((edge, 0.0, (plinth + top) * 0.5), (take, bd * 0.72, top - plinth), CONCRETE)
        m.box((edge, 0.0, top + 0.16), (take, bd * 0.72 + eave, 0.32), CONCRETE)
        m.box((edge, 0.0, top - 0.08), (take * 0.9, bd * 0.72 + eave * 0.8, 0.16), DARK)

    return m.to_object("SM_" + s["name"])


def run():
    report = []

    for i, name in enumerate(sorted(ENVELOPE)):
        hardsurface.wipe()
        hardsurface.ensure_materials(MATS)

        s = spec(name, 1000 + i * 37)
        obj = build(s)
        uv_worldscale.box_uv(obj, UV_TILE)

        path = os.path.join(OUT_DIR, obj.name + ".fbx")
        written = hardsurface.export_fbx(path)

        lo, hi = hardsurface.bounds(obj)
        want = ENVELOPE[name]
        got = [round(hi[a] - lo[a], 2) for a in range(3)]

        report.append(dict(
            name=obj.name, seed=s["seed"],
            want=[round(v, 2) for v in want], got=got,
            # 봉투를 넘었는지가 이 생성기의 유일한 실패 조건입니다.
            fits=(got[0] <= want[0] + 0.02 and got[1] <= want[1] + 0.02
                  and got[2] <= want[2] + 0.02),
            floor=round(lo[2], 3),
            tris=sum(len(p.vertices) - 2 for p in obj.data.polygons),
            service=s["service"], mono=s["mono"], fbx=written))

    return report


def lay_out():
    """여섯 채를 한 줄로 세워 blend 에 남깁니다. 나란히 놓아야 서로 다른지 보입니다."""
    hardsurface.wipe()
    hardsurface.ensure_materials(MATS)

    x = 0.0
    for i, name in enumerate(sorted(ENVELOPE)):
        s = spec(name, 1000 + i * 37)
        obj = build(s)
        uv_worldscale.box_uv(obj, UV_TILE)
        obj.location.x = x
        x += s["width"] + 6.0

    bpy.ops.wm.save_as_mainfile(filepath=BLEND_PATH)


if __name__ == "__main__":
    out = run()
    lay_out()
    print("###JSON###" + json.dumps(out))
