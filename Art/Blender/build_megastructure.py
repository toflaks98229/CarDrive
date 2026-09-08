# -*- coding: utf-8 -*-
"""
Megastructure generator for CarDrive - INTERLOCKING PRESETS on a shared socket.

WHAT A MEGASTRUCTURE IS (Maki 1964, Banham 1976, Wilcoxon's four points)
  1. built of MODULAR UNITS
  2. capable of great or even UNLIMITED EXTENSION
  3. a FRAMEWORK into which smaller units are plugged or clipped, having been
     prefabricated elsewhere
  4. the framework outlives the units it carries, by a long way

WHY PRESETS AND NOT SEEDS
  The first working version drew every number from a range with a seed. That
  gave four bays that differed, but it could not give a megastructure a
  PROGRAMME: a stretch of bare viaduct, then habitation, then a junction, then
  something enormous, then viaduct again. Random values inside one range can
  only ever produce one kind of thing at slightly different sizes.

  Presets are named, coherent parameter blocks - Viaduct, Habitat, Industry,
  Junction, Citadel. A spine is then a SEQUENCE of presets, and the variety
  comes from the sequence rather than from noise. That is also how the real
  ones were drawn: Tange's Tokyo Bay is a spine with distinct programme pieces
  hung off it, not one bay repeated with jitter.

WHAT MAKES THEM INTERLOCK
  A contract, not a convention. Every preset builds the SAME core - legs,
  transfer truss, deck, road, parapets, service ducts - from <c>SOCKET</c>, and
  <b>only the core crosses the seam</b>. Everything a preset adds is inset from
  both ends. So any preset can follow any other, and <c>verify()</c> checks it
  by comparing the actual vertex profile at the two end planes.

  A preset may be several bays long. Its length is always a whole number of
  bays, so the grid never breaks.

Run:
    blender -b --python build_megastructure.py
"""
import json
import math
import os
import random
import sys

import bpy

HERE = os.path.dirname(os.path.abspath(__file__))
if HERE not in sys.path:
    sys.path.insert(0, HERE)

import hardsurface  # noqa: E402
import uv_worldscale  # noqa: E402

OUT_DIR = r"E:\GamePJ\CarDrive\Assets\_Project\04.Art\02.Models\Megastructure"
MANIFEST = r"E:\GamePJ\CarDrive\Assets\_Project\04.Art\02.Models\Megastructure\presets.json"
BLEND_PATH = r"E:\GamePJ\CarDrive\Art\Blender\Megastructure.blend"
UV_TILE = 1.0

# --- The interlock contract -------------------------------------------------
# <b>이 값들은 프리셋이 흔들 수 없습니다.</b> 하나라도 프리셋마다 다르면 이음매가
# 어긋나고, 어긋나면 "무한히 연장 가능"이 거짓말이 됩니다. 이음매를 지나는 부재는
# 전부 이 표에서 나오고, 프리셋이 더하는 것은 <b>양 끝에서 안으로 물러나 있습니다.</b>

SOCKET = dict(
    bay=42.0,        # 한 베이의 길이
    width=34.0,      # 스파인의 폭
    gate=22.0,       # 지면에서 트러스 밑까지. 이 밑으로 차가 지나갑니다
    leg=(9.0, 11.0),
    truss=8.0,
    deck=2.0,
    parapet=1.4,
    duct=2.4,
    burial=14.0,     # 다리가 원점 아래로 내려가는 깊이. 지형 기복을 파묻습니다
    inset=1.2,       # 프리셋 부재가 이음매에서 물러나는 거리
)

DECK_Z = SOCKET["gate"] + SOCKET["truss"]
DECK_TOP = DECK_Z + SOCKET["deck"]

MATS = [
    ("M_Mega_Concrete", (0.465, 0.452, 0.430, 1.0), 0.95, 0.00),
    ("M_Mega_Steel", (0.185, 0.192, 0.205, 1.0), 0.45, 1.00),
    ("M_Mega_Dark", (0.048, 0.050, 0.054, 1.0), 0.65, 1.00),
]

CONCRETE, STEEL, DARK = 0, 1, 2

# --- Presets ----------------------------------------------------------------
# 각각이 <b>한 가지 용도</b>입니다. 값을 조금씩 흔든 같은 물건이 아닙니다.

PRESETS = {
    # 연결 조직. 아무것도 얹지 않은 맨 골조입니다. 사이사이에 이것이 있어야
    # 나머지가 <b>얹힌 것</b>으로 보입니다 — 전부 채우면 그냥 긴 건물입니다.
    "Viaduct": dict(bays=1, tiers=0, upper=0.0, weight=5),

    # 사람이 사는 칸. 골조 슬롯에 캡슐이 꽂히고 몇 자리는 비어 있습니다.
    "Habitat": dict(bays=1, tiers=4, upper=22.0, fill=0.62, weight=6),

    # 설비. 탱크와 굴뚝, 데크 위를 건너는 컨베이어 갠트리.
    "Industry": dict(bays=1, tiers=2, upper=15.0, fill=0.35, tanks=True, weight=3),

    # 분기. 스파인에서 직각으로 갈라지는 두 번째 데크와 큰 코어.
    "Junction": dict(bays=2, tiers=3, upper=30.0, fill=0.5, branch=True, weight=2),

    # <b>초거대.</b> 스파인이 뚫고 지나가는 덩어리. 세 베이에 걸치고 170 m 를 올라갑니다.
    "Citadel": dict(bays=3, tiers=22, upper=118.0, fill=0.62, block=True, weight=1),
}


# --- Shared core ------------------------------------------------------------


def core(m, length, bays):
    """
    모든 프리셋이 똑같이 세우는 뼈대입니다. <b>이음매를 지나는 것은 전부 여기 있습니다.</b>

    다리는 베이마다 한 쌍씩 한가운데에 섭니다. 트러스·데크·노면·난간·덕트는 프리셋
    전체 길이를 지나 양 끝에서 정확히 끊깁니다. 그래서 어떤 프리셋 뒤에 어떤 프리셋을
    붙여도 이 단면끼리 맞닿습니다.
    """
    W = SOCKET["width"]
    lx, ly = SOCKET["leg"]
    gate = SOCKET["gate"]
    truss = SOCKET["truss"]
    duct = SOCKET["duct"]

    # ---- 다리 --------------------------------------------------------------
    for b in range(bays):
        x = (b - bays * 0.5 + 0.5) * SOCKET["bay"]

        for sign in (-1.0, 1.0):
            m.box((x, sign * (W * 0.5 - ly * 0.5),
                   (gate + 4.0 - SOCKET["burial"]) * 0.5),
                  (lx, ly, gate + 4.0 + SOCKET["burial"]), CONCRETE, taper=0.30)

            m.box((x, sign * (W * 0.5 - ly * 0.5), gate - 1.4),
                  (lx + 3.4, ly + 2.6, 2.8), CONCRETE)

        # 다리를 잇는 인방. 차가 지나가는 문의 위쪽입니다.
        m.box((x, 0.0, gate + truss * 0.34),
              (lx + 1.2, W - ly * 2.0 + 2.4, truss * 0.68), CONCRETE)

    # ---- 이송 트러스 -------------------------------------------------------
    for sign in (-1.0, 1.0):
        m.pierced((0.0, sign * (W * 0.5 - ly * 0.42), gate + truss * 0.5),
                  (length, ly * 0.84, truss), bays * 3,
                  SOCKET["bay"] / 3.0 * 0.56, truss * 0.5,
                  CONCRETE, clip=length * 0.5)

    # ---- 설비 덕트 ---------------------------------------------------------
    for sign in (-1.0, 1.0):
        m.box((0.0, sign * (W * 0.5 - ly * 1.05), gate - duct * 0.6),
              (length, duct, duct), STEEL)

    m.box((0.0, 0.0, gate - duct * 0.45), (length, duct * 1.8, duct * 0.9), STEEL)

    # ---- 데크 --------------------------------------------------------------
    m.box((0.0, 0.0, DECK_Z + SOCKET["deck"] * 0.5), (length, W, SOCKET["deck"]), CONCRETE)
    m.box((0.0, 0.0, DECK_TOP + 0.08), (length, W - 4.0, 0.16), DARK)

    for sign in (-1.0, 1.0):
        m.box((0.0, sign * (W * 0.5 - 0.6), DECK_TOP + SOCKET["parapet"] * 0.5),
              (length, 1.2, SOCKET["parapet"]), CONCRETE)


# --- Preset parts -----------------------------------------------------------


def capsules(m, s, length, rng, y_half=None, base=None, fade=False):
    """
    골조 슬롯에 꽂힌 거주 캡슐입니다. <b>빈 슬롯이 핵심</b>입니다.

    캡슐은 다른 데서 만들어 와 꽂는 것이므로 아직 안 찼거나 이미 빠진 자리가 있어야
    합니다. 빈 슬롯 하나가 Wilcoxon 의 3·4 번(골조가 영구, 캡슐이 임시)을 눈으로
    증명합니다. 슬롯 기둥은 캡슐보다 <b>바깥</b>에 서서 골조가 앞에 오게 합니다.
    """
    if s["tiers"] <= 0:
        return

    W = y_half if y_half is not None else SOCKET["width"] * 0.5
    z0 = base if base is not None else DECK_TOP

    cw, cd, ch = 6.4, 5.0, 4.4
    inner = length - SOCKET["inset"] * 2.0
    cols = max(2, int(inner / (cw + 1.2)))
    pitch = inner / cols
    tier = 5.6

    for sign in (-1.0, 1.0):
        for i in range(cols + 1):
            m.box((-inner * 0.5 + i * pitch, sign * (W + 0.4),
                   z0 + s["tiers"] * tier * 0.5),
                  (1.6, cd * 0.55, s["tiers"] * tier), CONCRETE)

        for t in range(s["tiers"] + 1):
            m.box((0.0, sign * (W + 0.2), z0 + t * tier), (inner, cd * 0.75, 0.7), CONCRETE)

        for t in range(s["tiers"]):
            # 위로 갈수록 덜 찹니다. 아직 못 올라간 것이지 지어진 적 없는 것이 아닙니다.
            chance = s.get("fill", 0.6)
            if fade:
                chance *= 1.0 - 0.75 * (t / max(1, s["tiers"] - 1))

            for col in range(cols):
                if rng.random() > chance:
                    continue

                x = -inner * 0.5 + (col + 0.5) * pitch
                m.box((x, sign * (W + cd * 0.5), z0 + (t + 0.5) * tier),
                      (cw, cd * 1.5, ch), DARK)
                m.box((x, sign * (W + cd), z0 + (t + 0.62) * tier),
                      (cw * 0.5, 0.4, ch * 0.28), STEEL)


def portal(m, s, length, rng):
    """
    데크 위로 이어지는 열린 골조입니다.

    <b>열려 있어야 합니다.</b> 여기를 막으면 데크가 지붕이 되고 전체가 건물이 됩니다.
    하늘이 비쳐야 "위로도 계속된다"가 읽힙니다.
    """
    if s["upper"] <= 0.0:
        return

    # 덩어리 프리셋은 위가 막혀 있으므로 열린 포털이 설 자리가 없습니다.
    if s.get("block"):
        return

    W = SOCKET["width"]
    post = 3.4
    inner = length - SOCKET["inset"] * 2.0
    top = DECK_TOP + s["upper"]

    for sign in (-1.0, 1.0):
        for i in (-1, 1):
            m.box((i * inner * 0.34, sign * (W * 0.5 - post * 0.8), (DECK_TOP + top) * 0.5),
                  (post, post, top - DECK_TOP), CONCRETE)

    # <b>끝에서 잘라야 합니다.</b> 안 자르면 마지막 살이 반쪽만큼 밖으로 나가
    # 프리셋이 제 길이보다 길어집니다 - 실측에서 42 m 가 44.6 m 로 나왔습니다.
    holes = max(3, int(inner / 14.0))
    m.pierced((0.0, 0.0, top + 1.6), (inner, W - post * 0.8, 3.2), holes,
              inner / holes * 0.62, 1.8, CONCRETE, clip=inner * 0.5)

    for sign in (-1.0, 1.0):
        m.box((0.0, sign * (W * 0.5 - post * 0.8), top - 1.4), (inner, post * 0.7, 1.4), STEEL)


def industry(m, s, length, rng):
    """탱크와 굴뚝, 데크를 건너는 갠트리. 설비가 여기 있다고 말하는 어휘입니다."""
    if not s.get("tanks"):
        return

    W = SOCKET["width"]
    inner = length - SOCKET["inset"] * 2.0

    for i in (-1, 1):
        m.box((i * inner * 0.26, -W * 0.16, DECK_TOP + 5.0), (9.0, 9.0, 10.0), STEEL)
        m.box((i * inner * 0.26, -W * 0.16, DECK_TOP + 10.4), (10.2, 10.2, 0.8), CONCRETE)

    m.box((inner * 0.32, W * 0.28, DECK_TOP + 13.0), (4.0, 4.0, 26.0), CONCRETE)
    m.box((inner * 0.32, W * 0.28, DECK_TOP + 26.2), (5.0, 5.0, 0.8), STEEL)

    # 갠트리. 데크를 가로질러 양쪽 캡슐 열을 잇습니다.
    m.box((0.0, 0.0, DECK_TOP + 16.0), (5.0, W + 10.0, 2.6), STEEL)


def branch(m, s, length, rng):
    """
    직각으로 갈라지는 두 번째 데크입니다.

    <b>분기가 있어야 체계로 보입니다.</b> 한 줄만 있으면 다리이고, 갈라지는 곳이
    있어야 이것이 <b>망</b>의 일부라는 것이 읽힙니다.
    """
    if not s.get("branch"):
        return

    W = SOCKET["width"]
    reach = 46.0
    side = 1.0

    for i in (-1, 1):
        m.box((i * 9.0, side * (W * 0.5 + reach * 0.5), DECK_Z + SOCKET["deck"] * 0.5),
              (7.0, reach, SOCKET["deck"] + 1.4), CONCRETE)

    m.box((0.0, side * (W * 0.5 + reach), (DECK_Z - SOCKET["burial"]) * 0.5),
          (14.0, 12.0, DECK_Z + SOCKET["burial"]), CONCRETE, taper=0.22)

    m.box((0.0, side * (W * 0.5 + reach * 0.5), DECK_TOP + 0.1),
          (14.0, reach, 0.2), DARK)


def citadel(m, s, length, rng):
    """
    <b>초거대 덩어리.</b> 스파인이 뚫고 지나갑니다.

    <b>왜 뚫고 지나가야 하는가.</b> 덩어리를 스파인 옆에 세우면 그냥 큰 건물이고,
    스파인이 그 안을 지나가면 <b>골조가 먼저 있고 덩어리가 거기 걸린 것</b>이 됩니다.
    그것이 메가스트럭처와 마천루를 가르는 자리입니다.

    구멍은 뚫지 않고 <b>구멍 둘레</b>를 세웁니다 — 통로 좌우의 살과 그 위의 인방.
    """
    if not s.get("block"):
        return

    W = SOCKET["width"]
    inner = length - SOCKET["inset"] * 2.0
    depth = 74.0
    top = DECK_TOP + s["upper"]

    # 통로 좌우의 살. 스파인이 지나갈 폭은 비웁니다.
    flank = (depth - W - 6.0) * 0.5

    for sign in (-1.0, 1.0):
        y = sign * (W * 0.5 + 3.0 + flank * 0.5)
        m.box((0.0, y, (top - SOCKET["burial"]) * 0.5),
              (inner, flank, top + SOCKET["burial"]), CONCRETE)

        # 옆구리에 꽂힌 캡슐. <b>덩어리도 골조라는 것</b>을 말합니다.
        #
        # 슬롯은 꼭대기까지 올라가고 위로 갈수록 덜 찹니다. 처음에는 열 층만 세웠더니
        # 위쪽 77 m 가 민짜 콘크리트로 남아 그 부분만 마천루로 보였습니다. 빈 슬롯이
        # 끝까지 이어져야 <b>아직 안 채운 골조</b>로 읽힙니다.
        capsules(m, dict(tiers=s["tiers"], fill=s["fill"] * 0.55), inner, rng,
                 y_half=abs(y) + flank * 0.5, base=DECK_TOP + 8.0, fade=True)

    # 통로 위의 인방. 여기부터 위가 덩어리로 이어집니다.
    lid = DECK_TOP + SOCKET["parapet"] + 12.0
    m.box((0.0, 0.0, (lid + top) * 0.5), (inner, W + 6.0, top - lid), CONCRETE)
    m.box((0.0, 0.0, lid + 0.9), (inner, W + 7.4, 1.8), DARK)

    # 설비 띠. 층수를 끊어 세게 만들고, 민짜 벽이 남지 않게 합니다.
    for i in range(1, 5):
        m.box((0.0, 0.0, lid + (top - lid) * i / 5.0), (inner + 0.6, W + 7.0, 2.2), DARK)

    # 꼭대기의 코어와 테두리
    for i in (-1, 1):
        m.box((i * inner * 0.3, i * depth * 0.24, top + 11.0), (12.0, 12.0, 22.0), CONCRETE)
        m.box((i * inner * 0.3, i * depth * 0.24, top + 22.4), (13.6, 13.6, 1.2), STEEL)

    m.box((0.0, 0.0, top + 0.9), (inner + 2.0, depth + 2.0, 1.8), DARK)


# --- Build ------------------------------------------------------------------


def build(name):
    """프리셋 하나를 세웁니다. 원점은 <b>지면이자 프리셋의 한가운데</b>입니다."""
    s = PRESETS[name]
    bays = s["bays"]
    length = bays * SOCKET["bay"]

    m = hardsurface.Mass(MATS)
    rng = random.Random(abs(hash(name)) % 100000)

    core(m, length, bays)
    capsules(m, s, length, rng)
    portal(m, s, length, rng)
    industry(m, s, length, rng)
    branch(m, s, length, rng)
    citadel(m, s, length, rng)

    return m.to_object("SM_Mega_" + name)


def seam(obj, x):
    """
    끝면에 닿는 꼭짓점의 (y, z) 목록입니다. <b>연동 규약의 증거</b>입니다.

    두 프리셋의 이 목록이 같으면 어떤 순서로 붙여도 단면이 맞습니다. 말로 "맞춰
    두었다" 고 적는 것과 <b>재서 같다는 것</b>은 다릅니다.
    """
    out = set()

    for v in obj.data.vertices:
        if abs(v.co.x - x) < 1e-4:
            out.add((round(v.co.y, 3), round(v.co.z, 3)))

    return out


def verify(made):
    """모든 프리셋이 양 끝에서 같은 단면을 내미는지 확인합니다."""
    profiles = {}

    for name, obj in made.items():
        half = PRESETS[name]["bays"] * SOCKET["bay"] * 0.5
        lo = seam(obj, -half)
        hi = seam(obj, half)

        assert lo == hi, "%s 의 양 끝 단면이 서로 다릅니다" % name
        profiles[name] = lo

    first = next(iter(profiles))

    for name, profile in profiles.items():
        if profile == profiles[first]:
            continue

        missing = sorted(profiles[first] - profile)[:4]
        extra = sorted(profile - profiles[first])[:4]
        raise AssertionError(
            "%s 의 이음매가 %s 과 다릅니다 — 없는 것 %s · 남는 것 %s"
            % (name, first, missing, extra))

    return len(profiles[first])


def run():
    made = {}
    report = []

    for name in PRESETS:
        hardsurface.wipe()
        hardsurface.ensure_materials(MATS)

        obj = build(name)
        made[name] = obj
        uv_worldscale.box_uv(obj, UV_TILE)

        path = os.path.join(OUT_DIR, obj.name + ".fbx")
        written = hardsurface.export_fbx(path)

        lo, hi = hardsurface.bounds(obj)
        want = PRESETS[name]["bays"] * SOCKET["bay"]

        report.append(dict(
            name=obj.name, preset=name, bays=PRESETS[name]["bays"],
            length=round(hi[0] - lo[0], 3), want=want,
            tiles=abs((hi[0] - lo[0]) - want) < 0.01,
            size=[round(hi[a] - lo[a], 2) for a in range(3)],
            top=round(hi[2], 1),
            tris=sum(len(p.vertices) - 2 for p in obj.data.polygons),
            weight=PRESETS[name]["weight"], fbx=written))

    # 이음매 검사는 <b>전부 만든 뒤</b>에 합니다. 하나만 보고는 알 수 없습니다.
    hardsurface.wipe()
    hardsurface.ensure_materials(MATS)
    fresh = {name: build(name) for name in PRESETS}
    points = verify(fresh)

    with open(MANIFEST, "w", encoding="utf-8") as f:
        json.dump(dict(
            bay=SOCKET["bay"], width=SOCKET["width"], burial=SOCKET["burial"],
            seamPoints=points,
            presets=[dict(name=r["preset"], mesh=r["name"], bays=r["bays"],
                          length=r["length"], weight=r["weight"], top=r["top"])
                     for r in report]), f, indent=1)

    return report, points


def lay_out():
    """프리셋을 <b>실제로 이어 붙여</b> blend 에 남깁니다. 늘어놓아야 이음매가 보입니다."""
    hardsurface.wipe()
    hardsurface.ensure_materials(MATS)

    made = {}
    for name in PRESETS:
        obj = build(name)
        uv_worldscale.box_uv(obj, UV_TILE)
        obj.location.y = 6000.0
        made[name] = obj

    order = ["Viaduct", "Viaduct", "Habitat", "Habitat", "Industry", "Viaduct",
             "Citadel", "Viaduct", "Habitat", "Junction", "Habitat", "Viaduct",
             "Industry", "Viaduct", "Viaduct"]

    x = 0.0
    for name in order:
        span = PRESETS[name]["bays"] * SOCKET["bay"]
        source = made[name]

        copy = source.copy()
        copy.data = source.data
        copy.location = (x + span * 0.5, 0.0, 0.0)
        bpy.context.scene.collection.objects.link(copy)

        x += span

    bpy.ops.wm.save_as_mainfile(filepath=BLEND_PATH)


if __name__ == "__main__":
    out, points = run()
    lay_out()
    print("###JSON###" + json.dumps(dict(presets=out, seamPoints=points)))
