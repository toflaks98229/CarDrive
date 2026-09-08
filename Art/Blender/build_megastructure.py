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
    # 한쪽 난간을 끊어 <b>데크에서 뛰어내릴 수 있게</b> 합니다. 거주 구간마다 있으면
    # 흔해지므로 폭을 좁게 둡니다 - 노려서 맞춰야 하는 자리입니다.
    "Habitat": dict(bays=1, tiers=4, upper=22.0, fill=0.62, weight=6,
                    gaps=((-1.0, 0.0, 9.0),)),

    # 설비. 탱크와 굴뚝, 데크 위를 건너는 컨베이어 갠트리.
    "Industry": dict(bays=1, tiers=2, upper=15.0, fill=0.35, tanks=True, weight=3),

    # 분기. 스파인에서 직각으로 갈라지는 두 번째 데크와 큰 코어.
    "Junction": dict(bays=2, tiers=3, upper=30.0, fill=0.5, branch=True, weight=2),

    # <b>초거대.</b> 스파인이 뚫고 지나가는 덩어리. 세 베이에 걸치고 170 m 를 올라갑니다.
    "Citadel": dict(bays=3, tiers=22, upper=118.0, fill=0.62, block=True, weight=1),

    # 지상과 데크를 잇는 되돌이 경사로. <b>인공 지반을 실제로 쓸 수 있게 하는</b>
    # 조각이고, 운전 게임에서는 이것이 있어야 데크가 배경이 아니라 갈 수 있는 길입니다.
    # 경사로가 데크로 들어오는 자리. <b>여기가 안 뚫리면 경사로가 아무 소용이 없습니다</b> -
    # 실측에서 경사로는 데크 높이까지 올라왔는데 난간이 가로막고 있었습니다.
    "Ramp": dict(bays=2, tiers=0, upper=0.0, ramp=True, weight=2,
                 gaps=((1.0, -37.3, 15.0),)),

    # 크레인길. Archigram 의 Plug-In City 가 캡슐을 꽂고 빼던 그 장치입니다.
    # 캡슐이 <b>어떻게</b> 꽂히는지를 보여 주므로 3번 항목을 가장 직접 말합니다.
    "Crane": dict(bays=1, tiers=1, upper=0.0, fill=0.3, crane=True, weight=3),

    # 무너진 구간. 캡슐은 뜯겨 나갔는데 <b>골조는 서 있습니다</b> — 4번 항목
    # (골조가 담는 것보다 오래 산다)을 그림 하나로 말하는 자리입니다.
    # 무너진 구간이므로 난간도 뜯겨 나갔습니다. 두 군데가 크게 비어 있습니다.
    "Breach": dict(bays=1, tiers=4, upper=8.0, fill=0.16, breach=True, weight=2,
                   gaps=((1.0, -9.0, 13.0), (-1.0, 8.0, 11.0))),

    # 위를 직각으로 건너가는 두 번째 스파인. 한 줄이면 다리이고, 교차가 있어야 <b>망</b>입니다.
    "Overpass": dict(bays=2, tiers=0, upper=0.0, over=True, weight=2),

    # 뻗다 만 가지. 허공에서 끊겨 트러스 단면이 드러납니다. "연장 가능"을
    # 가장 크게 말하는 방법은 <b>연장하다 만 것</b>을 보여 주는 것입니다.
    # 가지가 갈라져 나가는 자리는 난간이 있을 수 없습니다.
    "Spur": dict(bays=1, tiers=0, upper=12.0, spur=True, weight=2,
                 gaps=((-1.0, 0.0, 19.0),)),
}


# --- Shared core ------------------------------------------------------------


def core(m, length, bays):
    """
    모든 프리셋이 똑같이 세우는 뼈대입니다. <b>이음매를 지나는 것은 전부 여기 있습니다.</b>

    다리는 베이마다 한 쌍씩 한가운데에 섭니다. 트러스·데크·노면·난간·덕트는 프리셋
    전체 길이를 지나 양 끝에서 정확히 끊깁니다. 그래서 어떤 프리셋 뒤에 어떤 프리셋을
    붙여도 이 단면끼리 맞닿습니다.

    <b>난간은 여기 없습니다.</b> 프리셋마다 끊는 자리가 달라 공용 부품이 될 수
    없으므로 프리셋 쪽 메시가 갖습니다.
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
    # <b>트러스는 다리보다 안으로 들어갑니다.</b> 폭을 다리와 같게 두었더니 바깥면이
    # 같은 평면에서 같은 쪽을 봐 깜빡였습니다. 보가 교각보다 좁은 것은 실제 구조에서도
    # 맞고, 그림자 선이 하나 더 생겨 두께가 읽힙니다.
    for sign in (-1.0, 1.0):
        m.pierced((0.0, sign * (W * 0.5 - ly * 0.44), gate + truss * 0.5),
                  (length, ly * 0.78, truss), bays * 3,
                  SOCKET["bay"] / 3.0 * 0.56, truss * 0.5,
                  CONCRETE, clip=length * 0.5)

    # ---- 설비 덕트 ---------------------------------------------------------
    for sign in (-1.0, 1.0):
        m.box((0.0, sign * (W * 0.5 - ly * 1.05), gate - duct * 0.6),
              (length, duct, duct), STEEL)

    m.box((0.0, 0.0, gate - duct * 0.45), (length, duct * 1.8, duct * 0.9), STEEL)

    # ---- 데크 --------------------------------------------------------------
    # <b>데크가 다리보다 조금 넓습니다.</b> 폭을 같게 두었더니 다리의 바깥면과
    # 데크의 바깥면이 같은 평면에서 같은 쪽을 봐 깜빡였습니다(프리셋마다 18 m2).
    # 상판이 교각보다 살짝 내미는 것은 실제 고가도로가 하는 일이기도 합니다.
    m.box((0.0, 0.0, DECK_Z + SOCKET["deck"] * 0.5),
          (length, W + 0.5, SOCKET["deck"]), CONCRETE)
    # 노면 띠를 데크 <b>속으로</b> 조금 묻습니다. 밑면을 데크 윗면과 같은 높이에
    # 두면 데크 위에 서는 다른 부재들의 밑면과도 같은 평면이 되어, 안 보이는
    # 자리에서 깊이 버퍼가 계속 다툽니다.
    m.box((0.0, 0.0, DECK_TOP + 0.03), (length, W - 4.0, 0.26), DARK)




def parapet(m, length, y, gaps):
    """
    난간을 <b>토막으로</b> 세웁니다. <c>gaps</c> 의 구간은 비웁니다.

    구멍의 <b>끝을 두껍게</b> 막습니다. 그냥 끊으면 판이 허공에서 잘린 것으로
    보이는데, 마구리가 있으면 <b>거기까지가 난간</b>이라고 읽힙니다.
    """
    # <b>구멍은 이음매에서 물러납니다.</b> 끝까지 뚫으면 옆 프리셋의 난간과 반쪽씩
    # 만나 단면이 어긋나고, 이음매 검사가 그 자리에서 멈춥니다 - 실제로 경사로의
    # 구멍이 프리셋 끝을 2.8 m 넘어가 걸렸습니다. 그래서 안으로 잘라 둡니다.
    limit = length * 0.5 - SOCKET["inset"]

    edge = -length * 0.5
    parts = []

    for center, width in gaps:
        lo = max(center - width * 0.5, -limit)
        hi = min(center + width * 0.5, limit)

        if hi - lo <= 0.2:
            continue

        parts.append((edge, lo))
        edge = hi

    parts.append((edge, length * 0.5))

    for lo, hi in parts:
        if hi - lo <= 0.2:
            continue

        m.box(((lo + hi) * 0.5, y, DECK_TOP + SOCKET["parapet"] * 0.5),
              (hi - lo, 1.2, SOCKET["parapet"]), CONCRETE)

    for center, width in gaps:
        for side in (-1.0, 1.0):
            x = min(max(center + side * width * 0.5, -limit), limit)
            m.box((x, y, DECK_TOP + SOCKET["parapet"] * 0.5),
                  (0.6, 1.6, SOCKET["parapet"] + 0.3), DARK)


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


def ramp(m, s, length, rng):
    """
    지상에서 데크로 올라가는 <b>되돌이 경사로</b>입니다.

    데크가 32 m 이므로 10% 로 곧게 올리려면 320 m 가 필요합니다 - 여덟 베이입니다.
    그래서 실제 주차 구조물이 하는 대로 <b>접습니다</b>: 네 번 되돌아 오르면 두
    베이 안에 듭니다. 경사는 약 20% 로 가파르지만 차가 올라갈 수 있습니다.
    """
    if not s.get("ramp"):
        return

    W = SOCKET["width"]
    inner = length - SOCKET["inset"] * 2.0
    run = inner - 14.0
    flights = 4
    lane = 9.0

    # <b>지면 아래에서 시작합니다.</b> 스파인은 선 위에서 가장 높은 지면에 맞춰
    # 놓이므로, 낮은 곳에 선 경사로는 발밑이 뜹니다 - 실측에서 2.12 m 떠 있었고
    # 그 단차로는 차가 못 올라갑니다. 다리와 같은 방법으로, 시작을 파묻어 두면
    # 지면이 어디에 있든 그 자리에서 경사면이 땅을 뚫고 나옵니다.
    #
    # 덤으로 기울기가 완만해집니다. 32 m 를 네 번에 나누면 11.4% 인데, 42 m 를
    # 같은 길이에 나누면 15% 이 아니라 - 층당 상승은 늘지만 길이도 그대로이므로
    # 층당 15% 입니다. 파묻힌 첫 층이 그 몫을 대신 먹습니다.
    low = -10.0
    step = (DECK_TOP - low) / flights

    # <b>두 갈래 차선을 오르내립니다.</b> 처음에는 층을 전부 같은 y 에 두었더니
    # 옆에서 보면 판때기가 겹친 지그재그로만 보였습니다. 실제 되돌이 경사로가
    # 그렇듯 두 줄을 나란히 놓고 양 끝에서 갈아타야 구조가 읽힙니다.
    lanes = (W * 0.5 + lane * 0.6, W * 0.5 + lane * 1.7)

    for i in range(flights):
        turn = 1.0 if i % 2 == 0 else -1.0
        y = lanes[i % 2]
        z = low + step * (i + 0.5)

        m.slope((0.0, y, z), (run, lane, 1.2), step * turn, CONCRETE)

        for edge in (-1.0, 1.0):
            # 난간은 본 데크와 같이 콘크리트입니다. 어둡게 두었더니 밑에서 올려다볼 때
            # 두 줄의 검은 띠만 보이고 정작 경사판이 가려졌습니다.
            m.slope((0.0, y + edge * lane * 0.5, z + 1.0),
                    (run, 0.7, 0.9), step * turn, CONCRETE)

        # 받치는 다리 둘. <b>층마다 x 를 어긋냅니다</b> - 같은 차선의 위아래 층이
        # 같은 자리에 서면 옆면이 같은 평면에서 겹쳐 깜빡입니다.
        for k in (-1, 1):
            offset = run * (0.22 + 0.07 * i)
            top = z + step * turn * k * 0.25
            m.box((k * offset, y, (top - SOCKET["burial"]) * 0.5),
                  (2.6, 2.6, top + SOCKET["burial"]), CONCRETE)

    # 갈아타는 참. 양 끝에서 두 차선을 잇습니다.
    for i in range(flights + 1):
        end = -1.0 if i % 2 == 0 else 1.0
        m.box((end * (run * 0.5 + 3.5), (lanes[0] + lanes[1]) * 0.5, low + step * i - 0.6),
              (7.0, lane * 2.2, 1.6), CONCRETE)

    # <b>데크로 건너가는 다리.</b> 마지막 참과 데크 가장자리 사이의 0.9 m 를 메웁니다.
    # 처음에는 이것을 반대쪽 끝에 두어 아무 데도 닿지 않았습니다.
    # <b>물러나야 합니다. 자르면 안 됩니다.</b> 처음에는 이 다리를 이음매에서 잘랐는데,
    # 그러면 잘린 면이 <b>이음매 평면 위에</b> 놓여 양 끝 단면이 서로 달라집니다.
    # 규약은 "끝을 넘지 않는다" 가 아니라 "끝에서 물러나 있다" 입니다.
    top_x = -(run * 0.5 + 3.5) if flights % 2 == 0 else (run * 0.5 + 3.5)
    span = (length * 0.5 - SOCKET["inset"] - abs(top_x)) * 2.0

    m.box((top_x, (W * 0.5 + lanes[0] - lane * 0.5) * 0.5, DECK_TOP - 0.6),
          (span, lanes[0] - lane * 0.5 - W * 0.5 + 2.0, 1.6), CONCRETE)


def crane(m, s, length, rng):
    """
    데크를 걸터앉은 갠트리 크레인과 쌓아 둔 캡슐입니다.

    캡슐이 <b>어떻게</b> 꽂히는지를 보여 줍니다. 크레인이 있으면 빈 슬롯이 "아직
    못 채운 것"으로 읽히고, 없으면 그냥 "구멍"입니다. Archigram 의 Plug-In City 가
    craneway 를 그림 한가운데 둔 이유가 그것입니다.
    """
    if not s.get("crane"):
        return

    W = SOCKET["width"]
    inner = length - SOCKET["inset"] * 2.0
    rail = DECK_TOP + 26.0

    for sign in (-1.0, 1.0):
        m.box((0.0, sign * (W * 0.5 + 5.0), DECK_TOP + 0.6), (inner, 1.6, 1.2), STEEL)

        for i in (-1, 1):
            m.box((i * inner * 0.3, sign * (W * 0.5 + 5.0), (DECK_TOP + rail) * 0.5),
                  (2.2, 2.2, rail - DECK_TOP), STEEL)

    # 가로보와 트롤리
    m.box((0.0, 0.0, rail + 1.4), (4.4, W + 14.0, 2.8), STEEL)
    m.box((inner * 0.14, W * 0.22, rail - 1.6), (5.0, 5.0, 2.4), DARK)

    # 매달린 캡슐
    m.box((inner * 0.14, W * 0.22, rail - 5.4), (1.0, 1.0, 5.2), STEEL)
    m.box((inner * 0.14, W * 0.22, rail - 10.0), (6.4, 5.0, 4.4), DARK)

    # 데크에 쌓아 둔 캡슐
    for i in range(3):
        m.box((-inner * 0.28 + i * 7.2, -W * 0.2, DECK_TOP + 2.3), (6.4, 5.0, 4.4), DARK)
    for i in range(2):
        m.box((-inner * 0.28 + i * 7.2, -W * 0.2, DECK_TOP + 6.8), (6.4, 5.0, 4.4), DARK)


def breach(m, s, length, rng):
    """
    무너진 구간입니다. <b>캡슐은 뜯겨 나갔는데 골조는 서 있습니다.</b>

    Wilcoxon 의 4번(골조가 담는 것보다 오래 산다)을 그림 하나로 말하는 자리입니다.
    골조를 부수면 그 말이 사라지므로 <b>뼈대는 손대지 않습니다</b> - 사라진 것은
    꽂혀 있던 것뿐이고, 남은 것은 슬롯과 잘린 철근입니다.
    """
    if not s.get("breach"):
        return

    W = SOCKET["width"]
    inner = length - SOCKET["inset"] * 2.0

    # 잘린 철근. 캡슐이 있던 자리에서 삐져나옵니다.
    for i in range(9):
        x = -inner * 0.42 + i * (inner * 0.84 / 8.0)
        for sign in (-1.0, 1.0):
            if rng.random() < 0.45:
                continue
            m.box((x, sign * (W * 0.5 + 1.6), DECK_TOP + 3.0 + rng.random() * 12.0),
                  (0.3, 2.4, 0.3), STEEL)

    # 떨어진 판. 지면에 비스듬히 박혀 있습니다.
    m.slope((-inner * 0.2, W * 0.5 + 12.0, 3.4), (14.0, 9.0, 1.6), 5.0, CONCRETE)
    m.slope((-inner * 0.2, W * 0.5 + 12.0, 4.4), (14.0, 9.0, 0.4), 5.0, DARK)

    # 임시 가림막. 사람이 아직 쓰고 있다는 표시입니다.
    m.box((inner * 0.3, -(W * 0.5 + 2.2), DECK_TOP + 6.0), (7.0, 0.4, 9.0), STEEL)


def overpass(m, s, length, rng):
    """
    위를 <b>직각으로</b> 건너가는 두 번째 스파인입니다.

    한 줄만 있으면 다리이고, 교차하는 곳이 있어야 이것이 <b>망</b>의 일부라는 것이
    읽힙니다. 자기 다리로 서므로 아래 스파인에 기대지 않습니다 - 두 번에 걸쳐 지어진
    두 구조물이 만난 자리로 보입니다.
    """
    if not s.get("over"):
        return

    W = SOCKET["width"]
    reach = 78.0
    z = DECK_TOP + 24.0
    deep = 5.0

    m.box((0.0, 0.0, z + 1.0), (24.0, reach * 2.0, 2.0), CONCRETE)
    m.pierced((0.0, 0.0, z - deep * 0.5), (10.0, reach * 2.0, deep), 7,
              reach * 2.0 / 7.0 * 0.6, deep * 0.5, CONCRETE, axis=1)

    for sign in (-1.0, 1.0):
        m.box((sign * 11.0, 0.0, z + 2.8), (2.0, reach * 2.0, 1.6), CONCRETE)

        # 다리는 스파인 폭 <b>밖에</b> 섭니다. 아래 구조물에 기대지 않습니다.
        m.box((0.0, sign * (W * 0.5 + 16.0), (z - deep - SOCKET["burial"]) * 0.5),
              (10.0, 12.0, z - deep + SOCKET["burial"]), CONCRETE, taper=0.26)


def spur(m, s, length, rng):
    """
    뻗다 만 가지입니다. 허공에서 끊겨 <b>트러스 단면</b>이 드러납니다.

    "무한히 연장 가능"을 가장 크게 말하는 방법은 완성된 것을 보여 주는 것이 아니라
    <b>연장하다 만 것</b>을 보여 주는 것입니다. 끝이 잘려 있으면 눈이 그 다음을
    스스로 그립니다.
    """
    if not s.get("spur"):
        return

    W = SOCKET["width"]
    reach = 34.0
    side = -1.0
    y = side * (W * 0.5 + reach * 0.5)

    m.box((0.0, y, DECK_Z + SOCKET["deck"] * 0.5), (18.0, reach, SOCKET["deck"]), CONCRETE)

    for sign in (-1.0, 1.0):
        m.pierced((sign * 8.0, y, DECK_Z - 2.4), (4.0, reach, 5.6), 3,
                  reach / 3.0 * 0.55, 2.8, CONCRETE, axis=1)

    # 잘린 끝. 철근이 삐져나옵니다.
    for i in range(5):
        m.box((-7.0 + i * 3.5, side * (W * 0.5 + reach + 0.8),
               DECK_Z + SOCKET["deck"] + 0.3), (0.3, 1.8, 0.3), STEEL)

    m.box((0.0, side * (W * 0.5 + reach - 0.4), DECK_Z + SOCKET["deck"] + 0.9),
          (18.0, 0.8, 1.8), DARK)


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

    if True:
        y = W * 0.5 + 3.0 + flank * 0.5

        # 옆구리에 꽂힌 캡슐. <b>덩어리도 골조라는 것</b>을 말합니다.
        #
        # <b>한 번만 부릅니다.</b> capsules() 자체가 좌우 양쪽을 세우는데 이것을 좌우
        # 반복 안에서 불렀더니 <b>같은 자리에 두 벌</b>이 겹쳐 그려졌습니다. 삼각형만
        # 배로 늘고 면이 전부 깜빡였습니다.
        #
        # 슬롯은 꼭대기까지 올라가고 위로 갈수록 덜 찹니다. 처음에는 열 층만 세웠더니
        # 위쪽 77 m 가 민짜 콘크리트로 남아 그 부분만 마천루로 보였습니다. 빈 슬롯이
        # 끝까지 이어져야 <b>아직 안 채운 골조</b>로 읽힙니다.
        capsules(m, dict(tiers=s["tiers"], fill=s["fill"] * 0.55), inner, rng,
                 y_half=abs(y) + flank * 0.5, base=DECK_TOP + 8.0, fade=True)

    # 통로 위의 인방. 여기부터 위가 덩어리로 이어집니다.
    lid = DECK_TOP + SOCKET["parapet"] + 12.0
    m.box((0.0, 0.0, (lid + top) * 0.5), (inner, W + 6.0, top - lid), CONCRETE)
    # 띠를 인방보다 <b>조금 내려</b> 답니다. 같은 높이에서 시작하면 덩어리의 밑면과
    # 띠의 밑면이 같은 평면에서 같은 쪽을 봐 4,944 m2 가 깜빡였습니다.
    m.box((0.0, 0.0, lid + 0.5), (inner, W + 7.4, 1.8), DARK)

    # 설비 띠. 층수를 끊어 세게 만들고, 민짜 벽이 남지 않게 합니다.
    for i in range(1, 5):
        m.box((0.0, 0.0, lid + (top - lid) * i / 5.0), (inner + 0.6, W + 7.0, 2.2), DARK)

    # 꼭대기의 코어와 테두리
    for i in (-1, 1):
        m.box((i * inner * 0.3, i * depth * 0.24, top + 11.4), (12.0, 12.0, 22.8), CONCRETE)
        m.box((i * inner * 0.3, i * depth * 0.24, top + 22.4), (13.6, 13.6, 1.2), STEEL)

    m.box((0.0, 0.0, top + 0.9), (inner + 2.0, depth + 2.0, 1.8), DARK)


# --- Build ------------------------------------------------------------------


def build_core():
    """
    <b>한 베이짜리 뼈대.</b> 프리셋이 몇 베이든 이것을 그만큼 늘어놓습니다.

    프리셋마다 구워 넣으면 같은 288 삼각형이 열 벌 저장되고 열 개의 다른 메시로
    그려집니다. 하나만 두면 스파인 전체가 이것을 인스턴싱합니다.
    """
    m = hardsurface.Mass(MATS)
    core(m, SOCKET["bay"], 1)
    return m.to_object("SM_Mega_Core")


def build(name):
    """
    프리셋이 <b>더하는 것</b>만 세웁니다. 뼈대는 build_core() 가 따로 냅니다.
    원점은 지면이자 프리셋의 한가운데입니다.

    난간은 여기 있습니다 — 프리셋마다 끊는 자리가 달라 공용 뼈대에 넣을 수 없습니다.
    대신 <b>양 끝의 단면은 모든 프리셋이 같아야</b> 하고, verify() 가 그것을 봅니다.
    """
    s = PRESETS[name]
    bays = s["bays"]
    length = bays * SOCKET["bay"]

    m = hardsurface.Mass(MATS)
    rng = random.Random(abs(hash(name)) % 100000)

    W = SOCKET["width"]
    for sign in (-1.0, 1.0):
        mine = sorted((g[1], g[2]) for g in s.get("gaps", ()) if g[0] == sign)
        parapet(m, length, sign * (W * 0.5 - 0.6), mine)

    capsules(m, s, length, rng)
    portal(m, s, length, rng)
    industry(m, s, length, rng)
    branch(m, s, length, rng)
    citadel(m, s, length, rng)
    ramp(m, s, length, rng)
    crane(m, s, length, rng)
    breach(m, s, length, rng)
    overpass(m, s, length, rng)
    spur(m, s, length, rng)

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
    """
    모든 프리셋이 양 끝에서 같은 단면을 내미는지 확인합니다.

    뼈대는 <b>글자 그대로 같은 메시</b>가 되었으므로 더 볼 것이 없습니다. 남은 것은
    프리셋마다 다른 난간인데, 끊는 자리가 이음매에 걸치면 옆 조각과 반쪽씩 만나
    어긋납니다. 그것을 여기서 잡습니다.
    """
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


def emit(obj, report_extra=None):
    """UV 를 깔고 FBX 로 내보냅니다. 부품마다 파일 하나입니다."""
    uv_worldscale.box_uv(obj, UV_TILE)

    path = os.path.join(OUT_DIR, obj.name + ".fbx")
    written = hardsurface.export_fbx(path)

    lo, hi = hardsurface.bounds(obj)

    out = dict(mesh=obj.name,
               size=[round(hi[a] - lo[a], 2) for a in range(3)],
               top=round(hi[2], 1),
               tris=sum(len(p.vertices) - 2 for p in obj.data.polygons),
               fbx=written)

    if report_extra:
        out.update(report_extra)

    return out


def run():
    report = []

    # ---- 뼈대 한 벌 --------------------------------------------------------
    hardsurface.wipe()
    hardsurface.ensure_materials(MATS)
    core_report = emit(build_core(), dict(bay=SOCKET["bay"]))

    # ---- 프리셋이 더하는 것 ------------------------------------------------
    for name in PRESETS:
        hardsurface.wipe()
        hardsurface.ensure_materials(MATS)

        obj = build(name)
        want = PRESETS[name]["bays"] * SOCKET["bay"]

        lo, hi = hardsurface.bounds(obj)
        span = hi[0] - lo[0]

        report.append(emit(obj, dict(
            preset=name, bays=PRESETS[name]["bays"], length=want,
            span=round(span, 3),
            # 부품이 프리셋 길이를 넘으면 이웃을 파고듭니다.
            fits=span <= want + 0.01,
            weight=PRESETS[name]["weight"])))

    # 이음매 검사는 <b>전부 만든 뒤</b>에 합니다. 하나만 보고는 알 수 없습니다.
    hardsurface.wipe()
    hardsurface.ensure_materials(MATS)
    fresh = {name: build(name) for name in PRESETS}
    points = verify(fresh)

    with open(MANIFEST, "w", encoding="utf-8") as f:
        json.dump(dict(
            bay=SOCKET["bay"], width=SOCKET["width"], burial=SOCKET["burial"],
            deckTop=DECK_TOP, gate=SOCKET["gate"],
            core=core_report["mesh"], coreTris=core_report["tris"],
            seamPoints=points,
            presets=[dict(name=r["preset"], mesh=r["mesh"], bays=r["bays"],
                          length=r["length"], weight=r["weight"], top=r["top"],
                          tris=r["tris"])
                     for r in report]), f, indent=1)

    return core_report, report, points


def lay_out():
    """
    부품을 <b>실제로 이어 붙여</b> blend 에 남깁니다.

    뼈대는 베이마다 한 번씩, 프리셋 부품은 그 가운데에 한 번씩 놓습니다 - 유니티가
    할 일과 똑같습니다. 늘어놓아야 이음매가 맞는지, 그리고 무엇보다 <b>끝이 안
    보이는지</b>가 보입니다.
    """
    hardsurface.wipe()
    hardsurface.ensure_materials(MATS)

    core_obj = build_core()
    uv_worldscale.box_uv(core_obj, UV_TILE)
    core_obj.location.y = 6000.0

    made = {}
    for name in PRESETS:
        obj = build(name)
        uv_worldscale.box_uv(obj, UV_TILE)
        obj.location.y = 6000.0
        made[name] = obj

    order = ["Viaduct", "Ramp", "Habitat", "Crane", "Viaduct", "Breach",
             "Viaduct", "Citadel", "Viaduct", "Spur", "Habitat", "Overpass",
             "Habitat", "Junction", "Industry", "Viaduct"]

    x = 0.0
    for name in order:
        bays = PRESETS[name]["bays"]
        span = bays * SOCKET["bay"]

        for b in range(bays):
            piece = core_obj.copy()
            piece.data = core_obj.data
            piece.location = (x + (b + 0.5) * SOCKET["bay"], 0.0, 0.0)
            bpy.context.scene.collection.objects.link(piece)

        part = made[name].copy()
        part.data = made[name].data
        part.location = (x + span * 0.5, 0.0, 0.0)
        bpy.context.scene.collection.objects.link(part)

        x += span

    bpy.ops.wm.save_as_mainfile(filepath=BLEND_PATH)


if __name__ == "__main__":
    core_out, out, points = run()
    lay_out()
    print("###JSON###" + json.dumps(
        dict(core=core_out, presets=out, seamPoints=points)))
