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
import hashlib
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

# 방의 치수와 생김새는 <b>저쪽이 주인</b>입니다. 여기서 6.0 x 4.6 이라고 다시 적으면
# 저쪽을 고치는 순간 캡슐 안이 조용히 어긋납니다.
import build_interiors  # noqa: E402
from mathutils import Matrix  # noqa: E402

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
    width=128.0,     # 스파인의 폭
    gate=24.0,       # 지면에서 첫 트러스 밑까지. 이 밑으로 차가 지나갑니다
    leg=(11.0, 13.0),
    truss=9.0,
    deck=2.2,
    parapet=1.4,
    duct=2.4,
    burial=16.0,     # 다리가 원점 아래로 내려가는 깊이. 지형 기복을 파묻습니다
    inset=1.4,       # 프리셋 부재가 이음매에서 물러나는 거리
    # 층 사이의 기둥 굵기와 층 높이.
    post=7.0,
    storey=72.0,
)

# 인공 지반이 <b>몇 겹</b>인가. 이것이 이 구조물을 다리가 아니라 생태계로 만듭니다.
#
# Maki 의 정의가 "도시의 모든 기능을 담는 커다란 틀" 인데, 기능이 여럿이려면 지반도
# 여럿이어야 합니다. 한 겹이면 아무리 길어도 고가도로입니다. 아래는 차가 지나가는
# 도로, 위로 갈수록 사람이 사는 곳 - 층마다 쓰임이 달라야 겹친 보람이 있습니다.
# 하늘을 스카이맵으로 갈아 끼우면서 <b>기준이 바뀌었습니다.</b> 저 위의 천장은
# 2.4 km 이고 그것을 받치는 기둥은 780 m 인데, 그 아래에 143 m 짜리가 서 있으면
# 같은 세계의 물건으로 안 보입니다 - 크기는 절대값이 아니라 <b>이웃과의 비</b>로
# 읽힙니다. 층을 하나 더하고 층높이를 54 → 72 m 로 올려 top deck 을 251 m 로,
# 그 위의 시타델까지 700 m 대로 끌어올립니다. 기둥과 같은 체급입니다.
LEVELS = 4

DECK_Z = SOCKET["gate"] + SOCKET["truss"]
DECK_TOP = DECK_Z + SOCKET["deck"]

# 층마다의 노면 높이입니다. 0 번이 도로이고 그 위가 인공 지반입니다.
DECKS = [DECK_TOP + i * SOCKET["storey"] for i in range(LEVELS)]
TOP_DECK = DECKS[-1]

# 갤러리 한 벌의 치수입니다. 캡슐 한 칸 · 통로 폭 · 한 층 높이.
CAPSULE = (6.4, 5.0, 4.4)
WALK = 2.6
TIER = 5.6

# 갤러리 한 벌이 <c>y_half</c> 를 기준으로 <b>y 로 차지하는 구간</b>입니다.
# 안쪽 끝은 층 슬래브, 바깥 끝은 난간 파이프입니다. 값을 여기 적지 않고
# <c>capsules()</c> 가 쓰는 것에서 뽑으므로, 거기를 고치면 이것이 따라옵니다.
GALLERY = (0.2 - CAPSULE[1] * 0.375, WALK + 0.62)


def clear_of(half, taken, gap=0.6):
    """
    갤러리를 <b>이미 갤러리가 있는 자리에서 비켜</b> 세웁니다.

    ⚠ <b>스파인도 자기 갤러리를 갖고 있습니다.</b> 시타델의 단이 물려 올라가다
    그 갤러리 높이에 들어서면 테라스 가장자리가 스파인 갤러리 <b>한가운데</b>에
    떨어져, 두 벌이 같은 부피를 차지합니다. 3 베이 시타델의 둘째 단이 정확히
    그랬고, 슬롯 기둥 옆면이 <c>x = ±0.80</c> 에서 같은 쪽을 보며 겹쳐
    <b>823 m2</b> 가 깜빡였습니다 - 이 프로젝트에서 가장 컸던 한 자리입니다.

    비켜 세우는 쪽은 <b>안쪽</b>입니다. 바깥으로 밀면 허공에 뜨지만, 안쪽에는
    아래 단의 테라스가 있어 실제로 설 자리가 됩니다. 난간에서 물러나 서는 것뿐이라
    멀리서 보는 실루엣은 그대로입니다.

    :param half: 세우고 싶은 자리(테라스 가장자리)
    :param taken: 이미 갤러리가 서 있는 자리
    :param gap: 두 벌 사이에 남길 틈
    :returns: 비켜 세울 자리. 겹치지 않으면 <paramref name="half"/> 그대로
    """
    lo, hi = half + GALLERY[0], half + GALLERY[1]
    t_lo, t_hi = taken + GALLERY[0], taken + GALLERY[1]
    if hi <= t_lo or lo >= t_hi:
        return half

    return t_lo - gap - GALLERY[1]

# <b>값을 끝까지 벌립니다.</b> 셋 다 중간 값대(0.465~0.048)에 몰려 있어, 폭을
# 96 m 로 넓히고 438 m 를 올려도 화면에서는 한 덩어리로 눌려 보였습니다. 텍스처도
# 색도 없는 NaissanceE 가 거대하게 읽히는 이유가 이것뿐이었습니다 — 밝은 면은
# 거의 하얗고 어두운 면은 거의 검습니다. 그늘은 재질이 아니라 빛이 만듭니다.
#
# 마지막 하나가 <b>이 세계에서 유일하게 채도가 있는 것</b>입니다. 아무 데나 칠하면
# 그 순간 특별하지 않게 되므로, 칠할 자리를 규칙으로 못 박습니다:
# <b>갈 수 있는 곳과 기계</b> — 등, 경사로 입구, 난간이 끊긴 자리, 방 문틀, 크레인.
MATS = [
    ("M_Mega_Concrete", (0.610, 0.596, 0.566, 1.0), 0.95, 0.00),
    ("M_Mega_Steel", (0.155, 0.163, 0.178, 1.0), 0.45, 1.00),
    ("M_Mega_Dark", (0.030, 0.031, 0.034, 1.0), 0.65, 1.00),
    ("M_Mega_Signal", (0.760, 0.290, 0.055, 1.0), 0.70, 0.00),
    ("M_Mega_Road", (0.088, 0.090, 0.096, 1.0), 0.88, 0.00),
]

# <b>노면은 캡슐이 아닙니다.</b> 처음에는 둘 다 DARK 로 칠했는데, 캡슐을 검게
# 떨어뜨리려고 DARK 를 0.030 까지 내리자 노면이 같이 내려가 <b>운전 화면의 아래
# 삼분의 이가 순검정</b>이 되었습니다. 값이 하나인데 쓰임이 둘이면 한쪽은 반드시
# 틀립니다. 실제 아스팔트도 반사율 0.10 안팎이지 0.03 이 아닙니다.
CONCRETE, STEEL, DARK, SIGNAL, ROAD = 0, 1, 2, 3, 4

# --- Presets ----------------------------------------------------------------
# 각각이 <b>한 가지 용도</b>입니다. 값을 조금씩 흔든 같은 물건이 아닙니다.

PRESETS = {
    # 연결 조직. 아무것도 얹지 않은 맨 골조입니다. 사이사이에 이것이 있어야
    # 나머지가 <b>얹힌 것</b>으로 보입니다 — 전부 채우면 그냥 긴 건물입니다.
    "Viaduct": dict(bays=1, tiers=0, upper=0.0, weight=5, busy=0.35),

    # 사람이 사는 칸. 골조 슬롯에 캡슐이 꽂히고 몇 자리는 비어 있습니다.
    # 한쪽 난간을 끊어 <b>데크에서 뛰어내릴 수 있게</b> 합니다. 거주 구간마다 있으면
    # 흔해지므로 폭을 좁게 둡니다 - 노려서 맞춰야 하는 자리입니다.
    # <b>들어갈 수 있는 방</b>이 여기 있습니다. (쪽, 층, 슬롯, 밖으로 이어지는 방들)
    #
    # 난간벽 끊긴 자리(x=0)와 <b>같은 슬롯</b>에 통로를 답니다 - 데크에서 걸어 나와
    # 통로를 지나 캡슐로 들어가는 한 줄이 됩니다. 문만 그려 두고 못 들어가면
    # 캡슐은 창고이지 집이 아닙니다.
    "Habitat": dict(bays=1, tiers=4, upper=22.0, fill=0.62, weight=6, levels=(0, 1, 2),
                    gaps=((-1.0, 0.0, 9.0),),
                    rooms=((-1.0, 0, 2, ("Corridor", "Cell")),
                           (-1.0, 0, 1, ("Cell",)),
                           (-1.0, 0, 3, ("Plant",)))),

    # 설비. 탱크와 굴뚝, 데크 위를 건너는 컨베이어 갠트리.
    "Industry": dict(bays=1, tiers=2, upper=15.0, fill=0.35, tanks=True, weight=3,
                    busy=1.6, hang=True,
                    levels=(0, 1)),

    # 분기. 스파인에서 직각으로 갈라지는 두 번째 데크와 큰 코어.
    "Junction": dict(bays=2, tiers=3, upper=30.0, fill=0.5, branch=True, weight=2,
                    levels=(1,), hang=True),

    # <b>초거대.</b> 스파인이 뚫고 지나가는 덩어리. 세 베이에 걸치고 170 m 를 올라갑니다.
    # <b>지표는 나머지보다 확실히 커야 합니다.</b> 층이 셋으로 늘고 수직 프리셋이
    # 260 m 를 넘은 뒤로 176 m 짜리 덩어리는 더 이상 지표가 아니었습니다.
    "Citadel": dict(bays=3, tiers=48, upper=650.0, fill=0.6, block=True, weight=1, lod=True,
                    levels=(0,)),

    # 지상과 데크를 잇는 되돌이 경사로. <b>인공 지반을 실제로 쓸 수 있게 하는</b>
    # 조각이고, 운전 게임에서는 이것이 있어야 데크가 배경이 아니라 갈 수 있는 길입니다.
    # 경사로가 데크로 들어오는 자리. <b>여기가 안 뚫리면 경사로가 아무 소용이 없습니다</b> -
    # 실측에서 경사로는 데크 높이까지 올라왔는데 난간이 가로막고 있었습니다.
    "Ramp": dict(bays=2, tiers=0, upper=0.0, ramp=True, weight=2,
                 gaps=((1.0, -37.3, 15.0),)),

    # 크레인길. Archigram 의 Plug-In City 가 캡슐을 꽂고 빼던 그 장치입니다.
    # 캡슐이 <b>어떻게</b> 꽂히는지를 보여 주므로 3번 항목을 가장 직접 말합니다.
    "Crane": dict(bays=1, tiers=1, upper=0.0, fill=0.3, crane=True, weight=3, busy=1.8,
                 levels=(2,)),

    # 위를 직각으로 건너가는 두 번째 스파인. 한 줄이면 다리이고, 교차가 있어야 <b>망</b>입니다.
    "Overpass": dict(bays=2, tiers=0, upper=0.0, over=True, weight=2),

    # 뻗다 만 가지. 허공에서 끊겨 트러스 단면이 드러납니다. "연장 가능"을
    # 가장 크게 말하는 방법은 <b>연장하다 만 것</b>을 보여 주는 것입니다.
    # 가지가 갈라져 나가는 자리는 난간이 있을 수 없습니다.
    "Spur": dict(bays=1, tiers=0, upper=12.0, spur=True, weight=2,
                 gaps=((-1.0, 0.0, 19.0),)),

    # <b>수직 하나 — 서비스 샤프트 다발.</b> Tange 의 야마나시(1966)가 원형입니다:
    # 원통 코어 열여섯이 승강기·계단·설비를 전부 나르고 바닥판이 그 사이를 건너며,
    # <b>일부러 비워 둔 바닥판</b>이 나중에 자랄 자리입니다. 빈 캡슐 슬롯이 수평에서
    # 하는 말을 수직에서 하는 것이 이 빈 층입니다.
    "Shaft": dict(bays=2, tiers=0, upper=0.0, shaft=True, weight=1,
                  gaps=((1.0, 0.0, 11.0),)),

    # <b>수직 둘 — 캡슐 탑.</b> Kurokawa 의 나카긴(1972)이 원형입니다: 코어 둘에
    # 캡슐이 하나씩 볼트로 붙고 갈아 끼울 수 있게 되어 있습니다. 여기서는 스파인
    # 옆구리에 꽂히는 것과 <b>같은 캡슐</b>을 씁니다 - 같은 공장에서 나온 물건이
    # 수평에도 수직에도 간다는 것이 이 세계의 규칙입니다.
    "CapsuleTower": dict(bays=2, tiers=0, upper=0.0, tower=True, weight=1,
                         gaps=((-1.0, 0.0, 11.0),)),
}


# --- Shared core ------------------------------------------------------------


def core(m, length, bays):
    """
    모든 프리셋이 똑같이 세우는 뼈대입니다. <b>이음매를 지나는 것은 전부 여기 있습니다.</b>

    <b>층이 여럿입니다.</b> 한 겹짜리 데크는 아무리 길어도 고가도로이지 생태계가
    아닙니다. 아래는 차가 지나가는 도로, 그 위로 사람이 쓰는 인공 지반이 두 겹 더
    올라가고, 층 사이는 기둥이 잇습니다. 위로 갈수록 하늘이 보이는 비율이 늘어
    <b>덮인 곳에서 열린 곳으로</b> 넘어갑니다.

    <b>폭도 넓습니다.</b> 34 m 는 다리이고 96 m 는 밑이 홀입니다 - 그 밑을 지날 때
    옆이 안 보이는 것이 규모의 증거입니다. 그래서 다리를 네 줄로 세웁니다.
    """
    W = SOCKET["width"]
    lx, ly = SOCKET["leg"]
    gate = SOCKET["gate"]
    truss = SOCKET["truss"]
    duct = SOCKET["duct"]
    post = SOCKET["post"]

    # 다리가 서는 네 줄. 바깥 둘이 가장자리를, 안쪽 둘이 가운데를 받칩니다.
    rows = [-(W * 0.5 - ly * 0.5), -W * 0.17, W * 0.17, W * 0.5 - ly * 0.5]

    for b in range(bays):
        x = (b - bays * 0.5 + 0.5) * SOCKET["bay"]

        for y in rows:
            m.box((x, y, (gate + 4.0 - SOCKET["burial"]) * 0.5),
                  (lx, ly, gate + 4.0 + SOCKET["burial"]), CONCRETE, taper=0.30)

            m.box((x, y, gate - 1.6), (lx + 3.6, ly + 3.0, 3.2), CONCRETE)

        # 다리를 잇는 인방. 차가 지나가는 문의 위쪽입니다.
        for i in range(len(rows) - 1):
            span = rows[i + 1] - rows[i]
            m.box((x, (rows[i] + rows[i + 1]) * 0.5, gate + truss * 0.34),
                  (lx + 1.4, span - ly + 2.4, truss * 0.68), CONCRETE)

        # <b>사재.</b> 다리가 그냥 서 있기만 하고 버티는 것으로 보이지 않았습니다 -
        # 하중은 세로 기둥이 아니라 <b>대각</b>이 보여 줍니다.
        #
        # <b>가운데 칸은 비웁니다.</b> 여기가 차가 지나가는 문이고, 폭 32.6 m 짜리
        # 그 칸에 사재를 걸면 통로가 막혀 "밑으로 지나간다" 는 뜻이 통째로 사라집니다.
        # 바깥 두 칸만 걸어도 다리는 버티는 것으로 읽힙니다.
        for i in (0, 2):
            lo, hi = rows[i], rows[i + 1]
            foot, head = 3.0, gate - 2.6

            # <b>두 부재를 앞뒤로 어긋냅니다.</b> 같은 x 에 겹쳐 놓았더니 각재의
            # 옆면 둘이 같은 평면에서 같은 쪽을 봐 2,204 m2 가 깜빡였습니다.
            # 실제 X 브레이스도 두 부재가 한 평면에 있지 않습니다 - 하나가 앞으로
            # 지나가고 하나가 뒤로 지나가며 가운데서 볼트로 물립니다.
            m.strut((x - 0.9, lo, foot), (x - 0.9, hi, head), 1.5, STEEL)
            m.strut((x + 0.9, hi, foot), (x + 0.9, lo, head), 1.5, STEEL)

            # 두 부재가 만나는 자리의 거싯.
            m.box((x, (lo + hi) * 0.5, (foot + head) * 0.5), (2.6, 2.2, 2.2), STEEL)

    # ---- 층마다 트러스·덕트·데크 ------------------------------------------
    for level in range(LEVELS):
        base = DECKS[level] - SOCKET["deck"] - truss
        top = DECKS[level]

        for y in rows:
            m.pierced((0.0, y, base + truss * 0.5),
                      (length, ly * 0.78, truss), bays * 3,
                      SOCKET["bay"] / 3.0 * 0.56, truss * 0.5,
                      CONCRETE, clip=length * 0.5)

        # 설비 덕트는 <b>맨 아래 층 밑</b>에만 흐릅니다. 층마다 달면 읽히지 않습니다.
        if level == 0:
            for y in (rows[0] + ly, rows[3] - ly):
                m.box((0.0, y, base - duct * 0.6), (length, duct, duct), STEEL)

            m.box((0.0, 0.0, base - duct * 0.45), (length, duct * 1.8, duct * 0.9), STEEL)

            # <b>밑을 지날 때 보라고</b> 다는 등입니다. 데크 밑은 54 m 짜리 뚜껑이
            # 덮인 곳이라 늘 그늘인데, 그늘이 <b>어두운 빈 곳</b>이 되면 지나갈 곳으로
            # 안 읽힙니다. 밝은 것이 있어야 어두운 것이 어둠이 됩니다.
            #
            # 간격은 노면의 등주와 같은 21 m 입니다. 위아래가 같은 자로 세어져야
            # 데크를 사이에 둔 두 공간이 <b>한 구조물</b>로 묶입니다.
            for x in (-length * 0.25, length * 0.25):
                m.box((x, 0.0, base - duct * 1.15), (1.6, W * 0.6, 0.5), STEEL)
                m.box((x, 0.0, base - duct * 1.36), (1.15, W * 0.58, 0.22), SIGNAL)

            # 가로보는 이제 <c>soffit</c> 이 격자로 깝니다. 여기 있던 6 개짜리
            # 가로 리브는 <b>같은 자리에 두 번</b> 놓여 겹쳤습니다.

            # 배관 두 줄. 리브를 가로질러 흐르며 <b>이 밑이 설비 공간</b>임을 말합니다.
            for y in (-W * 0.30, W * 0.30):
                m.box((0.0, y, base - 1.5), (length, 0.55, 0.55), STEEL)

        soffit(m, length, W, base, level)

        m.box((0.0, 0.0, top - SOCKET["deck"] * 0.5),
              (length, W + 0.5, SOCKET["deck"]), CONCRETE)

        if level == 0:
            road(m, length, W, top)
        else:
            # 위층은 광장입니다. 어두운 띠가 가장자리를 그어 <b>지반</b>으로 읽히게 합니다.
            for edge in (-1.0, 1.0):
                m.box((0.0, edge * (W * 0.5 - 3.0), top - 0.03), (length, 2.0, 0.26), ROAD)

            m.box((0.0, 0.0, top + 0.55), (length, W * 0.6, 0.3), ROAD)

        # ---- 층 사이의 기둥 ------------------------------------------------
        if level + 1 >= LEVELS:
            continue

        nxt = DECKS[level + 1] - SOCKET["deck"] - truss

        for bb in range(bays):
            x = (bb - bays * 0.5 + 0.5) * SOCKET["bay"]

            for y in rows:
                m.box((x, y, (top + nxt) * 0.5), (post, post, nxt - top), CONCRETE)

                # ⚠ 주두의 <b>윗면이 기둥 윗면과 같은 평면에 오면 안 됩니다.</b>
                # 둘 다 <c>nxt</c> 에 떨어져 같은 쪽을 보고 있었고, 기둥을 5 → 7 m 로
                # 굵히고 층을 하나 더하자 한 코어에 633 m2 가 됐습니다.
                # 0.12 m 내려 기둥 머리만 그 평면에 남깁니다.
                m.box((x, y, nxt - 1.06), (post + 1.6, post + 1.6, 1.88), CONCRETE)


def soffit(m, length, W, base, level):
    """
    데크 밑면의 <b>격자 천장</b>입니다. 하늘과 같은 말을 쓰게 하는 것이 목적입니다.

    이 세계의 하늘은 스카이맵으로 구운 <b>위층의 밑면</b>이고, 그것은 깊은 보가
    짜인 격자에 <b>따뜻한 빛우물</b>이 박힌 천장입니다. 그런데 정작 발밑의
    구조물은 매끈한 판에 리브 몇 개뿐이라, 올려다본 하늘과 데크 밑이 서로 다른
    세계의 물건으로 보였습니다.

    같은 격자를 여기에도 깝니다. 다른 것은 <b>간격뿐</b>입니다 — 하늘의 천장은
    2.4 km 위라 340 m 로 짜야 시야각 8 도가 되는데, 데크는 수십 미터 위라
    7 m 면 같은 각이 됩니다. 크기가 다른 같은 물건으로 읽히는 것이 목표입니다.

    <b>빛우물은 규칙적입니다.</b> 하늘의 것은 흩뿌려 두었지만(수 km 밖이라 무리로만
    보입니다) 여기서는 바로 위라 하나하나가 보이므로, 어긋나면 <b>고장난 것</b>으로
    읽힙니다. 건축은 규칙적이고, 규칙이 곧 자입니다.
    """
    pitch = 7.0
    rib = 0.9

    across = int(W / pitch)
    along = int(length / pitch)

    # ⚠ <b>두 방향을 같은 깊이에 걸면 안 됩니다.</b> 처음에 둘 다 밑면을 <c>base</c>
    # 에 맞췄더니, 교차점마다 두 리브의 윗면과 아랫면이 <b>같은 평면에서 같은 쪽을
    # 봤습니다</b> — 한 층에 324 개씩, 코어 하나에 2,308 m2 가 깜빡였습니다.
    #
    # 2 차 보를 <b>1 차 보 안으로</b> 넣습니다. 깊이가 1.2 와 0.7 이고 0.3 m 물려
    # 있어, 교차점에서 2 차 보의 여섯 면이 전부 1 차 보 속에 들어갑니다 - 같은
    # 평면에 놓이는 면이 아예 없습니다. 실제 보도 이렇게 걸립니다.
    deep, shallow = 1.2, 0.7

    # ⚠ <b>정확히 <c>base</c> 에 걸치지 않게 6 cm 내려 답니다.</b> 그 평면에는 이미
    # 층 사이 기둥의 <b>주두</b> 윗면이 와 있어서, 리브의 윗면과 같은 평면에서 같은
    # 쪽을 봤습니다 - 코어 하나에 1,092 m2 였습니다. 6 cm 는 눈에 안 보이지만
    # 깊이 버퍼에는 충분합니다.
    base -= 0.06

    for j in range(across + 1):
        y = (j - across * 0.5) * pitch

        if abs(y) > W * 0.5 - 1.0:
            continue

        m.box((0.0, y, base - deep * 0.5), (length, rib, deep), CONCRETE)

    for i in range(along + 1):
        x = (i - along * 0.5) * pitch

        if abs(x) > length * 0.5 - 1.0:
            continue

        m.box((x, 0.0, base - 0.3 - shallow * 0.5), (rib, W - 2.0, shallow), CONCRETE)

    # 빛우물. 격자 한 칸 걸러 하나씩, 층마다 어긋나게 둡니다.
    for i in range(along):
        if (i + level) % 3 != 0:
            continue

        x = (i - along * 0.5 + 0.5) * pitch

        if abs(x) > length * 0.5 - pitch:
            continue

        for j in range(across):
            if (j + level) % 2 != 0:
                continue

            y = (j - across * 0.5 + 0.5) * pitch

            if abs(y) > W * 0.5 - pitch:
                continue

            # 우물의 테두리를 먼저 내리고 그 안에 발광면을 답니다. 판만 붙이면
            # <b>스티커</b>로 보입니다 - 깊이가 있어야 등으로 읽힙니다.
            m.box((x, y, base - 0.45), (pitch * 0.62, pitch * 0.62, 0.9), CONCRETE)
            m.box((x, y, base - 0.95), (pitch * 0.46, pitch * 0.46, 0.16), SIGNAL)


def road(m, length, W, top):
    """
    차가 다니는 층의 노면입니다. <b>단색 판이면 안 됩니다.</b>

    폭 88 m 짜리 어두운 판 하나로 두었더니 시속 120 이나 40 이나 화면이 똑같았습니다.
    주행 게임에서 속도는 <b>흘러가는 무늬</b>로 느껴지는 것이지 숫자로 느껴지는 것이
    아닙니다. 그래서 파선을 깝니다 - 한 칸이 곧 자이고, 흐르는 속도가 곧 속도계입니다.

    등주는 <b>21 m 마다 좌우 번갈아</b> 섭니다. 베이가 42 m 이므로 한 베이에 둘이고,
    이것이 이 구조물에서 사람이 셀 수 있는 <b>유일한 반복</b>입니다 - 나머지 반복은
    베이 42 m 와 층 54 m 라 한눈에 세어지지 않습니다.
    """
    half = length * 0.5
    edge = W * 0.5 - 4.0          # 노면의 가장자리

    m.box((0.0, 0.0, top - 0.03), (length, edge * 2.0, 0.26), ROAD)

    # 연석. 노면과 갓길을 가르고, 밤에 헤드라이트를 받아 길의 폭을 보여 줍니다.
    for side in (-1.0, 1.0):
        m.box((0.0, side * (edge + 0.3), top + 0.12), (length, 0.6, 0.44), CONCRETE)

    # 가장자리 실선. 4 cm 만 두껍게 - 그 이상이면 차가 넘을 때 걸립니다.
    for side in (-1.0, 1.0):
        m.box((0.0, side * (edge - 2.0), top + 0.12), (length, 0.5, 0.04), CONCRETE)

    # 가운데와 그 사이의 파선. 4 m 칠하고 3 m 비웁니다.
    for lane in (0.0, -21.0, 21.0):
        for i in range(6):
            m.box((-half + 3.5 + i * 7.0, lane, top + 0.12),
                  (4.0, 0.45, 0.04), CONCRETE)

    # 등주. 좌우 번갈아 세워 21 m 간격을 만듭니다.
    for i, side in enumerate((1.0, -1.0)):
        x = -half * 0.5 + i * half
        y = side * (W * 0.5 - 2.6)

        m.box((x, y, top + 3.6), (0.36, 0.36, 7.2), STEEL)
        m.box((x, y - side * 1.2, top + 7.05), (0.34, 2.4, 0.34), STEEL)
        m.box((x, y - side * 2.2, top + 6.86), (0.5, 1.5, 0.22), SIGNAL)


def props(m, s, length, rng):
    """
    데크 어깨에 놓인 <b>사람 크기의 물건</b>들입니다.

    1,596 x 96 m 짜리 데크 위에 놓인 것이 <b>하나도 없었습니다.</b> 참조한 게임들의
    광장이 텅 비어 있는데도 넓게 읽히는 이유는 볼라드 몇 개와 상자 몇 개가 있어서
    였습니다 - 밀도가 아니라 <b>있느냐 없느냐</b>의 문제입니다. 잴 것이 하나도 없는
    바닥은 넓은 것이 아니라 아무것도 아닙니다.

    <b>코어가 아니라 프리셋에 답니다.</b> 코어는 42 m 마다 똑같이 되풀이되므로,
    거기 놓으면 물건이 자로 변해 버립니다. 자는 등주 하나로 충분하고, 물건은
    불규칙해야 물건입니다.

    노면(y ±44)과 난간벽(y ±47.4) 사이의 어깨에만 둡니다. 차선 위에 놓으면 그냥
    장애물입니다.
    """
    W = SOCKET["width"]
    inner = length - SOCKET["inset"] * 2.0
    span = inner - 4.0
    z = DECKS[0]

    for _ in range(max(2, int(span / 6.0 * s.get("busy", 1.0)))):
        x = -span * 0.5 + rng.random() * span
        side = 1.0 if rng.random() < 0.5 else -1.0
        y = side * (W * 0.5 - 3.6 + rng.random() * 1.9)
        kind = rng.random()

        if kind < 0.34:
            # 볼라드. 꼭대기만 칠해 <b>사람이 두는 것</b>임을 말합니다.
            m.box((x, y, z + 0.5), (0.32, 0.32, 1.0), CONCRETE)
            m.box((x, y, z + 1.04), (0.36, 0.36, 0.14), SIGNAL)

        elif kind < 0.62:
            # 자재 상자. 눕혀 쌓습니다.
            wide = 1.6 + rng.random() * 1.0
            m.box((x, y, z + 0.7), (wide, 1.5, 1.4), STEEL)

        elif kind < 0.82:
            # 표지판. 세로로 서서 <b>수평선을 끊습니다.</b>
            m.box((x, y, z + 1.3), (0.16, 0.16, 2.6), STEEL)
            m.box((x, y - side * 0.1, z + 2.3), (1.5, 0.1, 0.9), CONCRETE)

        else:
            # 배수구. 바닥에 붙어 <b>바닥이 만들어진 것</b>임을 말합니다.
            m.box((x, y, z + 0.05), (1.1, 0.9, 0.1), STEEL)


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

    # <b>판이 아니라 선이어야 합니다.</b> 1.4 m 짜리 콘크리트 판 한 장이었는데,
    # 그러면 사람 크기의 것이 화면에 하나도 안 걸립니다. 참조한 게임들이 100 m 짜리
    # 덩어리를 읽게 만드는 것은 전경에 걸리는 <b>5~15 cm 짜리 선재</b>였습니다.
    #
    # 다만 아래는 벽으로 남깁니다 - 차가 부딪히는 높이이고, 여기까지 파이프로 두면
    # 방호가 아니라 장식이 됩니다. 벽 0.80 m 위에 난간을 얹습니다.
    wall = 0.80
    rails = (0.30, 0.58)

    for lo, hi in parts:
        if hi - lo <= 0.2:
            continue

        m.box(((lo + hi) * 0.5, y, DECK_TOP + wall * 0.5),
              (hi - lo, 1.2, wall), CONCRETE)

        # 가로대는 <b>이음매를 지나갑니다.</b> 옆 베이의 난간과 이어져야 합니다.
        for up in rails:
            m.box(((lo + hi) * 0.5, y, DECK_TOP + wall + up),
                  (hi - lo, 0.12, 0.12), STEEL)

        # 기둥은 <b>토막 안쪽에만</b> 섭니다. 이음매에 걸리면 단면이 달라집니다.
        posts = max(1, int((hi - lo) / 3.5))
        pitch = (hi - lo) / posts

        for i in range(posts):
            m.box((lo + (i + 0.5) * pitch, y, DECK_TOP + wall + rails[1] * 0.5),
                  (0.14, 0.14, rails[1] + 0.12), STEEL)

    for center, width in gaps:
        for side in (-1.0, 1.0):
            x = min(max(center + side * width * 0.5, -limit), limit)
            # <b>마구리는 남기되 콘크리트로.</b> 지우면 난간이 허공에서 잘린
            # 판으로 보입니다 - 검은 상자로 보이던 것이 문제였지 마구리 자체가
            # 문제는 아니었습니다.
            m.box((x, y, DECK_TOP + SOCKET["parapet"] * 0.5),
                  (0.6, 1.6, SOCKET["parapet"] + 0.3), CONCRETE)

            # 마구리 <b>꼭대기만</b> 액센트입니다. 여기가 뛰어내릴 수 있는 자리라는
            # 표시이고, 이 세계에서 채도가 있는 것은 갈 수 있는 곳뿐입니다.
            m.box((x, y, DECK_TOP + SOCKET["parapet"] + 0.24),
                  (0.72, 1.72, 0.28), SIGNAL)


# --- Preset parts -----------------------------------------------------------


def capsules(m, s, length, rng, y_half=None, base=None, fade=False, deck=None,
             open_at=(), band=None, band_tiers=7):
    """
    골조 슬롯에 꽂힌 거주 캡슐입니다. <b>빈 슬롯이 핵심</b>입니다.

    <c>band</c> 를 주면 층을 <c>band_tiers</c> 개씩 묶어 <b>파츠를 갈아 가며</b>
    쌓습니다. 시타델처럼 한 번에 120 m 를 올리는 곳에서, 데크에 서면 눈에 들어오는
    것은 맨 아래 띠뿐인데도 통째로 그려지고 있었습니다.

    <b>따로 여러 번 부르면 안 됩니다.</b> 그렇게 나누면 띠가 끝나는 높이와 다음
    띠가 시작하는 높이에 <b>같은 슬래브가 두 벌</b> 생겨 그 면이 깜빡입니다. 한
    번에 훑으면서 이름표만 바꿔야 겹치는 것이 없습니다.

    캡슐은 다른 데서 만들어 와 꽂는 것이므로 아직 안 찼거나 이미 빠진 자리가 있어야
    합니다. 빈 슬롯 하나가 Wilcoxon 의 3·4 번(골조가 영구, 캡슐이 임시)을 눈으로
    증명합니다. 슬롯 기둥은 캡슐보다 <b>바깥</b>에 서서 골조가 앞에 오게 합니다.
    """
    if s["tiers"] <= 0:
        return

    W = y_half if y_half is not None else SOCKET["width"] * 0.5
    z0 = base if base is not None else (deck if deck is not None else DECKS[0])

    cw, cd, ch = CAPSULE
    inner = length - SOCKET["inset"] * 2.0
    cols = max(2, int(inner / (cw + 1.2)))
    pitch = inner / cols
    tier = TIER

    # <b>갤러리 — 하늘의 거리.</b> 캡슐이 꽂혀 있어도 들어갈 길이 없으면 창고입니다.
    # Park Hill(1961)의 데크 접근 복도가 그 답이었습니다: 층마다 바깥으로 통로를
    # 내고 문이 거기로 열립니다. 우유 트럭이 다닐 폭이었다는 그 통로입니다.
    #
    # 그래서 골조에서 밖으로 <b>기둥 → 통로 → 캡슐</b> 순서가 됩니다. 캡슐을 통로
    # 바깥으로 밀어 두지 않으면 문 앞이 허공입니다.
    walk = WALK
    reach = W + walk

    def holes(sign, t):
        """이 층 이 쪽에서 <b>난간을 끊을</b> 구간입니다. 방이 들어가는 자리입니다."""
        out = []
        for o_sign, o_tier, o_col in open_at:
            if o_sign == sign and o_tier == t:
                cx = -inner * 0.5 + (o_col + 0.5) * pitch
                out.append((cx - pitch * 0.5, cx + pitch * 0.5))

        return sorted(out)

    def segments(gaps):
        edge = -inner * 0.5
        for lo, hi in gaps:
            if lo > edge:
                yield edge, lo
            edge = max(edge, hi)

        if edge < inner * 0.5:
            yield edge, inner * 0.5

    def tag(t):
        # 슬롯 기둥은 층을 꿰고 서 있으므로 띠로 못 자릅니다. 나머지만 자릅니다.
        if band:
            m.group("%s%d" % (band, t // band_tiers))

    for sign in (-1.0, 1.0):
        for i in range(cols + 1):
            m.box((-inner * 0.5 + i * pitch, sign * (W + 0.4),
                   z0 + s["tiers"] * tier * 0.5),
                  (1.6, cd * 0.55, s["tiers"] * tier), CONCRETE)

        for t in range(s["tiers"] + 1):
            tag(min(t, s["tiers"] - 1))
            m.box((0.0, sign * (W + 0.2), z0 + t * tier), (inner, cd * 0.75, 0.7), CONCRETE)

        # 통로 바닥과 난간. 난간은 무릎이 아니라 <b>가슴</b> 높이입니다 - 30 m 위입니다.
        #
        # <b>난간은 토막으로 세웁니다.</b> 한 줄로 죽 세웠더니 캡슐 문 앞을 그대로
        # 가로막아, 문이 달려 있어도 들어갈 수 없는 <b>그림</b>이었습니다. 방이
        # 들어가는 자리에서는 끊어야 문이 문이 됩니다.
        for t in range(s["tiers"]):
            tag(t)
            # ⚠ <b>층 슬래브와 같은 자리에서 끝나면 안 됩니다.</b> 둘 다
            # <c>inner</c> 라 마구리가 <c>x = ±61.6</c> 에서 같은 평면에 놓였고,
            # 시타델에서만 <b>636 쌍</b>이 나왔습니다 - 낱장은 0.22 m2 인데
            # 층마다 양쪽 양끝이라 수가 불어납니다. 여기는 이음매 안쪽이라
            # 옆 프리셋이 가려 주지도 않습니다.
            #
            # 0.6 m 짧게 놓습니다. 구조인 슬래브가 <b>디딤판보다 조금 나온</b>
            # 것이 되어 오히려 맞습니다.
            m.box((0.0, sign * (W + walk * 0.5 + 0.6), z0 + t * tier + 0.35),
                  (inner - 0.6, walk, 0.3), CONCRETE)

            for lo, hi in segments(holes(sign, t)):
                # 아래는 얇은 판, 위는 파이프. 상자 수는 그대로인데 30 m 위에서
                # 내려다볼 때 <b>가장자리에 선이 걸립니다.</b>
                m.box(((lo + hi) * 0.5, sign * (W + walk + 0.5), z0 + t * tier + 0.85),
                      (hi - lo, 0.14, 0.62), CONCRETE)
                m.box(((lo + hi) * 0.5, sign * (W + walk + 0.56), z0 + t * tier + 1.36),
                      (hi - lo, 0.11, 0.11), STEEL)

        for t in range(s["tiers"]):
            # 위로 갈수록 덜 찹니다. 아직 못 올라간 것이지 지어진 적 없는 것이 아닙니다.
            tag(t)

            chance = s.get("fill", 0.6)
            if fade:
                chance *= 1.0 - 0.75 * (t / max(1, s["tiers"] - 1))

            for col in range(cols):
                # <b>주사위를 먼저 굴립니다.</b> 빈 슬롯에서 건너뛰며 굴리지 않으면
                # 그 뒤 슬롯들이 전부 다른 눈을 받아, 방 하나 넣었다고 프리셋 전체의
                # 캡슐 배치가 바뀝니다.
                roll = rng.random()

                if (sign, t, col) in open_at:
                    continue

                if roll > chance:
                    continue

                x = -inner * 0.5 + (col + 0.5) * pitch

                # <b>꽂힌 캡슐은 걷어 냈습니다.</b> 남는 것은 슬롯 기둥과 통로,
                # 곧 <b>아직 아무것도 안 꽂힌 골조</b>입니다. 문틀만 남깁니다 -
                # 슬롯이 무엇을 받는 자리인지는 여전히 문이 말합니다.
                m.box((x, sign * (reach - 0.05), z0 + (t + 0.5) * tier - 0.35),
                      (1.5, 0.4, 2.3), CONCRETE)


def access(m, s, length, rng, deck=None):
    """
    데크에서 갤러리로 올라가는 <b>계단 코어</b>입니다.

    통로를 층마다 냈어도 데크에서 거기까지 올라갈 길이 없으면 소용이 없습니다.
    Yamanashi 가 그랬듯 <b>코어가 먼저 있고 층은 거기 붙습니다</b> - 코어는 한 자리에
    서서 모든 층을 꿰고, 층마다 짧은 다리로 통로에 닿습니다.
    """
    if s["tiers"] <= 0:
        return

    W = SOCKET["width"]
    inner = length - SOCKET["inset"] * 2.0
    tier = TIER
    walk = WALK
    reach = W + walk

    side = 1.0
    x = inner * 0.5 - 3.6
    floor = deck if deck is not None else DECKS[0]
    top = floor + s["tiers"] * tier

    # 코어. 데크에서 맨 위 통로까지.
    m.box((x, side * (reach + 2.2), (floor + top) * 0.5),
          (5.2, 5.2, top - floor), CONCRETE)
    m.box((x, side * (reach + 2.2), top + 0.5), (6.0, 6.0, 1.0), STEEL)

    # 계단실 창. 층마다 하나씩 나면 <b>안에 계단이 있다</b>는 것이 밖에서 읽힙니다.
    for t in range(s["tiers"]):
        m.box((x, side * (reach + 4.7), floor + (t + 0.5) * tier),
              (1.2, 0.4, 2.0), STEEL)

    # 데크에서 코어로 건너가는 다리.
    m.box((x, side * (W * 0.5 + (reach + 2.2 - W * 0.5) * 0.5), floor - 0.15),
          (3.4, reach + 2.2 - W * 0.5, 0.3), CONCRETE)

    # 층마다 코어에서 통로로.
    for t in range(s["tiers"]):
        m.box((x, side * (reach + 0.9), floor + t * tier + 0.35),
              (3.0, 2.8, 0.3), CONCRETE)


def portal(m, s, length, rng, deck=None):
    """
    데크 위로 이어지는 열린 골조입니다.

    <b>열려 있어야 합니다.</b> 여기를 막으면 데크가 지붕이 되고 전체가 건물이 됩니다.
    하늘이 비쳐야 "위로도 계속된다"가 읽힙니다.
    """
    floor = deck if deck is not None else DECKS[0]

    if s["upper"] <= 0.0:
        return

    # 덩어리 프리셋은 위가 막혀 있으므로 열린 포털이 설 자리가 없습니다.
    if s.get("block"):
        return

    W = SOCKET["width"]
    post = 3.4
    inner = length - SOCKET["inset"] * 2.0
    top = floor + s["upper"]

    for sign in (-1.0, 1.0):
        for i in (-1, 1):
            m.box((i * inner * 0.34, sign * (W * 0.5 - post * 0.8), (floor + top) * 0.5),
                  (post, post, top - floor), CONCRETE)

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
        m.box((i * inner * 0.26, -W * 0.16, DECKS[s.get('levels', (0,))[-1]] + 5.0), (9.0, 9.0, 10.0), STEEL)
        m.box((i * inner * 0.26, -W * 0.16, DECKS[s.get('levels', (0,))[-1]] + 10.4), (10.2, 10.2, 0.8), CONCRETE)

    m.box((inner * 0.32, W * 0.28, DECKS[s.get('levels', (0,))[-1]] + 13.0), (4.0, 4.0, 26.0), CONCRETE)
    m.box((inner * 0.32, W * 0.28, DECKS[s.get('levels', (0,))[-1]] + 26.2), (5.0, 5.0, 0.8), STEEL)

    # 갠트리. 데크를 가로질러 양쪽 캡슐 열을 잇습니다.
    m.box((0.0, 0.0, DECKS[s.get('levels', (0,))[-1]] + 16.0), (5.0, W + 10.0, 2.6), STEEL)


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

          # 어두운 것을 강철로. <b>이 한 상자 때문에 슬롯이 하나 더 생기고</b>
          # 그 슬롯이 곧 드로우 하나입니다 - 12 삼각형이 파츠 전체 삼각형의
          # 0.5~1.4% 인데 제출 비용은 다른 슬롯과 똑같습니다.
    m.box((0.0, side * (W * 0.5 + reach * 0.5), DECK_TOP + 0.1),
          (14.0, reach, 0.2), STEEL)


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

            # 난간 꼭대기의 액센트 띠. 밑에서 올려다볼 때 <b>어디로 오르는 길인지</b>가
            # 이 두 줄로 읽힙니다. 지금까지 경사로는 회색 판이라 지형에 묻혔습니다.
            m.slope((0.0, y + edge * lane * 0.5, z + 1.52),
                    (run, 0.78, 0.16), step * turn, SIGNAL)

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

    # ⚠ <b>다리를 참 속으로 밀어 넣으면 안 됩니다.</b> 둘이 두께도 높이도 같아
    # 윗면과 밑면이 <c>z = 35.4 · 33.8</c> 에서 같은 평면을 쓰며 겹쳤고,
    # <b>20 m2</b> 가 깜빡였습니다 - 데크 밑에서 올려다보면 보이는 자리입니다.
    # 메워야 할 것은 데크와 참 사이의 틈뿐이므로 참 앞에서 멈춥니다.
    near = (lanes[0] + lanes[1]) * 0.5 - lane * 1.1
    far = W * 0.5 - 1.25

    m.box((top_x, (near + far) * 0.5, DECK_TOP - 0.6),
          (span, near - far, 1.6), CONCRETE)

    # 데크로 들어서는 문턱. 데크 위에서 <b>여기가 내려가는 길</b>임을 보여 줍니다.
    m.box((top_x, W * 0.5 - 1.0, DECK_TOP + 0.22),
          (span * 0.9, 1.2, 0.12), SIGNAL)


def hang(m, s, length, rng):
    """
    윗층 바닥에 <b>매달려 아래층을 가로지르는 것</b>입니다.

    층이 셋인데 데크에 서면 위층은 그냥 천장이었습니다. 층이 있다는 것은 위를
    올려다봐서 아는 것이 아니라 <b>위의 것이 내려와 지나갈 때</b> 압니다 - 참조한
    게임의 광장 화면 맨 위를 가로지르던 매달린 포드가 그 한 장으로 "위에 층이
    하나 더 있다" 를 말했습니다.

    <b>노면 위 높이를 지킵니다.</b> 데크가 35.2 이고 윗층 밑면이 78.0 이므로 그
    사이에 답니다. 너무 높이 달면 화면에 안 들어오고, 너무 내리면 차가 부딪힙니다.
    """
    if not s.get("hang"):
        return

    W = SOCKET["width"]
    inner = length - SOCKET["inset"] * 2.0
    roof = DECKS[1] - SOCKET["deck"] - 9.0      # 윗층 트러스 밑면
    rail = roof - 3.4

    for x in (-inner * 0.24, inner * 0.24):
        # 매다는 기둥 넷. 윗층 바닥에서 레일까지.
        for y in (-W * 0.34, W * 0.34):
            m.box((x, y, (roof + rail) * 0.5), (0.9, 0.9, roof - rail), STEEL)

        # 가로지르는 레일. <b>스파인을 가로질러</b> 놓입니다 - 나란히 놓으면
        # 데크와 같은 방향이라 층이 갈린 것이 안 보입니다.
        m.box((x, 0.0, rail), (2.2, W * 0.86, 1.4), STEEL)
        m.box((x, 0.0, rail - 0.9), (2.4, W * 0.88, 0.35), STEEL)

    # 레일 위를 달리는 <b>트롤리</b>. 매달려 있던 포드는 걷어 냈습니다.
    px = -inner * 0.24
    py = W * 0.16

    m.box((px, py, rail - 1.9), (2.6, 3.4, 1.6), STEEL)
    m.box((px, py, rail - 3.1), (1.4, 1.4, 1.0), SIGNAL)


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
    rail = DECKS[s.get('levels', (0,))[-1]] + 26.0

    # ⚠ <b>다리는 레일 위에서 시작합니다.</b> 데크 높이에서 시작했더니 레일과
    # 다리의 밑면이 같은 평면에서 같은 쪽(아래)을 보았습니다. 이 둘은 데크
    # <b>바깥으로</b> 5 m 나가 있어 밑에 데크가 없고, 그래서 밑에서 올려다보면
    # 정말 보이는 자리였습니다 - 14 m2.
    #
    # 갠트리는 원래 레일을 <b>타고</b> 다니는 것이므로 이것이 맞는 순서입니다.
    base = DECKS[s.get('levels', (0,))[-1]]
    ride = base + 1.2

    for sign in (-1.0, 1.0):
        m.box((0.0, sign * (W * 0.5 + 5.0), base + 0.6), (inner, 1.6, 1.2), STEEL)

        for i in (-1, 1):
            m.box((i * inner * 0.3, sign * (W * 0.5 + 5.0), (ride + rail) * 0.5),
                  (2.2, 2.2, rail - ride), STEEL)

            # 다리 밑동의 경고 띠. 갠트리가 <b>움직이는 것</b>임을 말합니다.
            m.box((i * inner * 0.3, sign * (W * 0.5 + 5.0),
                   DECKS[s.get('levels', (0,))[-1]] + 3.0), (2.4, 2.4, 1.2), SIGNAL)

    # 가로보와 트롤리. <b>기계는 칠해져 있습니다.</b> 골조와 같은 회색으로 두면
    # 크레인이 구조물의 일부로 읽혀, "이것이 캡슐을 꽂는 장치" 라는 문장이 사라집니다.
    m.box((0.0, 0.0, rail + 1.4), (4.4, W + 14.0, 2.8), SIGNAL)
    m.box((0.0, 0.0, rail + 2.9), (4.6, W + 14.4, 0.4), STEEL)
    m.box((inner * 0.14, W * 0.22, rail - 1.6), (5.0, 5.0, 2.4), SIGNAL)

    # 갈고리만. 매달려 있던 캡슐은 걷어 냈습니다.
    m.box((inner * 0.14, W * 0.22, rail - 4.2), (0.8, 0.8, 2.8), STEEL)
    m.box((inner * 0.14, W * 0.22, rail - 6.0), (2.2, 2.2, 0.9), STEEL)

    # 데크에 쌓아 둔 캡슐
    deck = DECKS[s.get('levels', (0,))[-1]]

    # 쌓아 둔 캡슐 대신 <b>자재</b>를 쌓습니다. 강철 각재 더미입니다.
    for i in range(3):
        m.box((-inner * 0.28 + i * 7.2, -W * 0.2, deck + 0.7), (6.4, 4.4, 1.4), STEEL)
    for i in range(2):
        m.box((-inner * 0.28 + i * 7.2 + 3.6, -W * 0.2, deck + 2.1),
              (6.4, 4.0, 1.4), STEEL)

    # <b>기계가 사건이어야 합니다.</b> 크레인만 서 있으면 배경이고, 그 발밑에
    # 일하던 흔적이 있어야 <b>짓다 만 것</b>으로 읽힙니다 - 자재 더미, 세워 둔
    # 운반차, 바닥에 그은 작업 구역.
    for i in range(4):
        m.box((inner * 0.30, -W * 0.32 + i * 2.6, deck + 0.55 + (i % 2) * 0.9),
              (5.6, 2.2, 1.1), STEEL)

    # 운반차. 사람이 아니라 <b>기계의 크기</b>로 스케일을 잽니다.
    hx = -inner * 0.34
    m.box((hx, W * 0.16, deck + 1.5), (8.4, 3.4, 2.2), SIGNAL)
    m.box((hx + 2.6, W * 0.16, deck + 3.3), (2.8, 3.0, 1.8), STEEL)

    for i in (-1, 1):
        for j in (-1, 1):
            m.box((hx + i * 3.0, W * 0.16 + j * 1.8, deck + 0.7),
                  (1.8, 0.8, 1.4), STEEL)

    # 작업 구역을 그은 바닥 띠.
    m.box((inner * 0.30, -W * 0.26, deck + 0.06), (9.0, 9.0, 0.1), SIGNAL)
    m.box((inner * 0.30, -W * 0.26, deck + 0.09), (8.0, 8.0, 0.1), ROAD)


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

          # 어두운 것을 강철로. <b>이 한 상자 때문에 슬롯이 하나 더 생기고</b>
          # 그 슬롯이 곧 드로우 하나입니다 - 12 삼각형이 파츠 전체 삼각형의
          # 0.5~1.4% 인데 제출 비용은 다른 슬롯과 똑같습니다.
    m.box((0.0, side * (W * 0.5 + reach - 0.4), DECK_Z + SOCKET["deck"] + 0.9),
          (18.0, 0.8, 1.8), STEEL)


def shaft(m, s, length, rng):
    """
    서비스 샤프트 다발과 그 사이를 건너는 바닥판입니다.

    <b>비어 있는 층이 요점입니다.</b> 야마나시는 바닥판 몇 장을 처음부터 넣지 않고
    비워 두었습니다 - 나중에 자랄 자리를 <b>지어 두지 않은 채로 보여 주는</b> 것이고,
    수평에서 빈 캡슐 슬롯이 하는 말과 똑같습니다. 다 채우면 그냥 탑입니다.
    """
    if not s.get("shaft"):
        return

    W = SOCKET["width"]
    inner = length - SOCKET["inset"] * 2.0
    side = 1.0

    floors = 18
    step = 5.0
    base = TOP_DECK
    core_r = 3.4

    # 코어. 여덟 개를 두 줄로 세우고 하나는 훨씬 높입니다.
    cores = []
    for i in range(4):
        for j in (-1, 1):
            x = (i - 1.5) * (inner * 0.22)
            y = side * (W * 0.5 + 9.0 + j * 7.5)
            tall = base + step * (floors + (6 if i == 1 and j == 1 else 0))
            cores.append((x, y, tall))

            m.box((x, y, (base - SOCKET["burial"] + tall) * 0.5),
                  (core_r * 2.0, core_r * 2.0, tall - base + SOCKET["burial"]), CONCRETE)
            m.box((x, y, tall + 0.6), (core_r * 2.4, core_r * 2.4, 1.2), DARK)

    # 바닥판. rng 가 정한 층은 <b>넣지 않습니다</b>.
    span_y = side * (W * 0.5 + 9.0)

    for f in range(1, floors + 1):
        if rng.random() < 0.28:
            continue

        z = base + f * step

        # 난간 대신 가장자리 보. 층이 <b>판</b>으로 읽히게 합니다.
        beam, edge_y = 0.4, 9.6

        m.box((0.0, span_y, z + 0.25), (inner * 0.82, 19.0, 0.5), CONCRETE)

        # ⚠ <b>덮개를 보와 같은 자리에서 끝내면 안 됩니다.</b> 둘 다 가장자리가
        # <c>span_y ± 9.8</c> 이라 바깥면이 같은 평면에서 같은 쪽을 보았고, 층마다
        # 두 쌍씩 열세 층이면 <b>277 m2</b> 가 깜빡였습니다 - 시타델을 고친 뒤
        # 스파인에서 가장 컸던 자리입니다.
        #
        # 덮개를 보 <b>사이</b>에서 끊습니다. 가장자리를 보 하나가 도맡으니
        # "판" 이라는 말이 오히려 또렷해집니다.
        m.box((0.0, span_y, z + 1.05),
              (inner * 0.82 + 0.5, (edge_y - beam * 0.5) * 2.0, 0.16), DARK)

        for edge in (-1, 1):
            m.box((0.0, span_y + edge * edge_y, z + 0.9),
                  (inner * 0.82, beam, 0.9), CONCRETE)

    # 데크에서 다발로 건너가는 다리.
    m.box((inner * 0.3, side * (W * 0.5 + 4.5), TOP_DECK - 0.15),
          (4.0, 9.0, 0.3), CONCRETE)


def tower(m, s, length, rng):
    """
    코어 둘에 캡슐을 꽂은 탑입니다.

    스파인 옆구리와 <b>같은 캡슐</b>을 씁니다. 크기를 달리하면 두 물건이 다른 공장에서
    나온 것으로 보이고, 그러면 "다른 데서 만들어 와 꽂는다" 는 말이 약해집니다.
    """
    if not s.get("tower"):
        return

    W = SOCKET["width"]
    inner = length - SOCKET["inset"] * 2.0
    side = -1.0

    cw, cd, ch = CAPSULE
    step = 5.2
    floors = 13
    base = TOP_DECK
    top = base + step * floors

    lanes = (-inner * 0.16, inner * 0.16)

    for x in lanes:
        y = side * (W * 0.5 + 8.0)

        m.box((x, y, (base - SOCKET["burial"] + top) * 0.5),
              (5.0, 5.0, top - base + SOCKET["burial"]), CONCRETE)
        m.box((x, y, top + 0.7), (5.8, 5.8, 1.4), DARK)

        # 계단실 창
        for f in range(floors):
            m.box((x, y + side * 2.7, base + (f + 0.5) * step), (1.1, 0.4, 1.8), DARK)

    # 캡슐. 코어마다 층마다 한 면씩, 빈 자리를 남깁니다.
    for x in lanes:
        y = side * (W * 0.5 + 8.0)

        for f in range(floors):
            for face in (-1, 1):
                if rng.random() < 0.34:
                    continue

                m.box((x + face * (2.5 + cd * 0.5), y, base + (f + 0.5) * step),
                      (cd, cw * 0.86, ch), CONCRETE)
                m.box((x + face * (2.5 + cd), y + side * cw * 0.3,
                       base + (f + 0.62) * step),
                      (0.4, cw * 0.3, ch * 0.3), STEEL)

    m.box((0.0, side * (W * 0.5 + 4.0), TOP_DECK - 0.15), (4.0, 8.0, 0.3), CONCRETE)


def endwall(m, half_x, half_y, z0, z1, rng, fill, band=None, band_rows=2):
    """
    덩어리의 <b>끝면</b>입니다. 스파인을 따라 달리면 정면으로 보게 되는 면입니다.

    처음에는 여기를 그냥 콘크리트로 두었습니다. 그랬더니 150 x 380 m 짜리 민무늬
    벽이 생겨, 규모는 커졌는데 <b>큰 상자</b>로 보였습니다. 메가스트럭처가 마천루와
    다른 이유는 크기가 아니라 <b>골조가 먼저 보이고 유닛이 거기 걸려 있다</b>는
    것인데, 민짜 벽에는 그 문장이 없습니다.

    그래서 끝면에도 같은 말을 새깁니다 - 세로 살과 가로 띠가 격자를 만들고, 그
    칸 일부에만 유닛이 꽂힙니다. <b>빈 칸이 남아야</b> 골조가 먼저라는 것이 보입니다.
    """
    cell_y, cell_z = 12.0, 21.0

    ny = max(2, int(half_y * 2.0 / cell_y))
    nz = max(2, int((z1 - z0) / cell_z))
    py = half_y * 2.0 / ny
    pz = (z1 - z0) / nz

    for sign in (-1.0, 1.0):
        x = sign * half_x

        # 살과 띠는 벽 <b>바깥으로</b> 나옵니다. 벽면과 같은 평면에 두면 같은 쪽을
        # 보는 면이 겹쳐 깜빡입니다. 안쪽 면은 벽을 등지므로 그려지지 않습니다.
        # 세로 살은 단 전체를 꿰고 서 있으므로 띠로 자를 수 없습니다. 26 개뿐이라
        # 통째로 두어도 잃는 것이 없습니다.
        for i in range(ny + 1):
            m.box((x + sign * 0.7, -half_y + i * py, (z0 + z1) * 0.5),
                  (1.4, 1.8, z1 - z0), CONCRETE)

        for j in range(nz + 1):
            if band:
                m.group("%s%d" % (band, min(j, nz - 1) // band_rows))

            m.box((x + sign * 0.5, 0.0, z0 + j * pz),
                  (1.0, half_y * 2.0, 1.6), CONCRETE)

        for j in range(nz):
            if band:
                m.group("%s%d" % (band, j // band_rows))

            for i in range(ny):
                if rng.random() > fill:
                    continue

                y = -half_y + (i + 0.5) * py
                z = z0 + (j + 0.5) * pz

                # 살보다 바깥에 꽂아 <b>골조가 뒤로 지나가게</b> 합니다.
                # 꽂힌 유닛 대신 <b>빈 칸의 안쪽 면</b>을 냅니다. 격자만 남으면
                # 밋밋하므로 칸마다 깊이를 주어 그림자가 지게 합니다.
                m.box((x + sign * 0.6, y, z), (0.6, py * 0.62, pz * 0.58), CONCRETE)


def rooms(m, s, length, prefix):
    """
    갤러리에 <b>실제로 들어갈 수 있는 방</b>을 답니다.

    지금까지 캡슐은 속이 없는 상자였습니다. 문틀이 그려져 있어도 열리지 않고, 그
    앞은 난간이 가로막고 있었습니다. 메가스트럭처의 3·4 번 항목(골조에 유닛이
    꽂힌다 / 골조가 유닛보다 오래 산다)은 <b>유닛 안에 들어가 봐야</b> 몸으로
    읽힙니다 - Nakagin 이 유명한 것은 겉모습이 아니라 그 안이 방이어서입니다.

    방은 <b>따로 선 오브젝트</b>입니다. 프리셋 메시에 구워 넣으면 캡슐 하나 고치는
    데 454 m 짜리 덩어리를 다시 구워야 하고, 컬링도 통째로만 됩니다.

    바깥으로 이어 답니다 - 통로 다음에 방, 각 방의 문은 스파인 쪽을 봅니다.
    """
    spec = s.get("rooms")
    if not spec:
        return []

    W = SOCKET["width"] * 0.5
    walk = WALK
    front = W + walk + 0.6      # 통로 바닥의 <b>바깥</b> 끝. 여기서부터 방입니다
    tier = TIER
    inner = length - SOCKET["inset"] * 2.0
    cols = max(2, int(inner / (6.4 + 1.2)))
    pitch = inner / cols

    made = []

    for sign, level, col, chain in spec:
        x = -inner * 0.5 + (col + 0.5) * pitch

        # 통로 바닥 윗면이 z0 + 0.5 입니다. 6 cm 만 올려 <b>같은 평면을 피합니다</b> -
        # 딱 맞추면 방 바닥과 통로 바닥이 같은 높이에서 같은 쪽을 봐 깜빡입니다.
        z = DECKS[level] + 0.56
        off = 0.0

        for name in chain:
            spec_room = build_interiors.ROOMS[name]
            depth = spec_room["inner"][1] + build_interiors.WALL * 2.0

            obj = build_interiors.shell(name, spec_room)
            obj.name = "%s_Room%d%s" % (prefix, col, name)

            # 트랜스폼을 <b>메시에 구워 넣습니다.</b> 오브젝트 위치로 두면 이음매
            # 검사가 로컬 좌표를 보므로 방이 어디 있는지 모릅니다.
            obj.data.transform(
                Matrix.Translation((x, sign * (front + off + depth * 0.5), z))
                @ Matrix.Rotation(math.pi if sign < 0.0 else 0.0, 4, "Z"))

            made.append(obj)

            # <b>외피는 걷어 냈습니다.</b> 어두운 클래딩을 둘렀던 것은 방이
            # 주변 캡슐과 같은 재질로 읽히게 하려던 것인데, 그 캡슐 자체가
            # 없어졌으므로 두를 이유가 사라졌습니다. 방은 콘크리트 상자입니다.
            off += depth

        # 데크에서 통로로 건너가는 <b>발판</b>. 데크 끝이 48.25, 통로 시작이 48.6 이라
        # 35 cm 가 뚫려 있습니다. 난간벽이 끊긴 자리에서만 답니다 - 벽이 서 있는
        # 자리에 놓으면 벽을 뚫고 지나가는 판이 됩니다.
        for g_sign, g_at, g_wide in s.get("gaps", ()):
            if g_sign != sign or abs(x - g_at) > g_wide * 0.5:
                continue

            m.box((x, sign * 47.75, DECKS[level] + 0.35), (pitch, 1.9, 0.3), CONCRETE)

    return made


def citadel(m, s, length, rng, coarse=False):
    """
    <b>초거대 덩어리.</b> 스파인이 뚫고 지나갑니다.

    <b>왜 뚫고 지나가야 하는가.</b> 덩어리를 스파인 옆에 세우면 그냥 큰 건물이고,
    스파인이 그 안을 지나가면 <b>골조가 먼저 있고 덩어리가 거기 걸린 것</b>이 됩니다.
    그것이 메가스트럭처와 마천루를 가르는 자리입니다.

    구멍은 뚫지 않고 <b>구멍 둘레</b>를 세웁니다 - 통로 좌우의 살과 그 위의 인방.

    <b>단을 물려 올립니다.</b> 한 덩어리로 380 m 를 올렸더니 옆에서 본 윤곽이 그냥
    직사각형이었습니다. 높이는 실루엣으로 읽히는데 직사각형에는 실루엣이 없습니다.
    물러난 자리마다 테라스가 생기고, 그 테라스 난간에 캡슐 갤러리가 붙습니다.

    단이 <b>베이 길이보다 짧은</b> 것은 끝면 격자가 밖으로 나오기 때문입니다. 처음에
    단을 이음매 코앞까지 채웠더니 살과 캡슐이 <b>이음매 평면 위에</b> 올라앉아, 옆
    베이와 맞물려야 할 단면이 달라졌습니다. 나오는 만큼 미리 물러나 있어야 합니다.
    """
    if not s.get("block"):
        return

    W = SOCKET["width"]
    inner = length - SOCKET["inset"] * 2.0
    depth = 150.0
    top = DECKS[0] + s["upper"]

    # 통로 좌우의 살. 스파인이 지나갈 폭은 비웁니다.
    flank = (depth - W - 6.0) * 0.5

    # 통로 위의 인방. 여기부터 위가 덩어리로 이어집니다.
    lid = DECKS[0] + SOCKET["parapet"] + 12.0

    m.group("Base")

    for sign in (-1.0, 1.0):
        y = sign * (W * 0.5 + 3.0 + flank * 0.5)
        m.box((0.0, y, (lid - SOCKET["burial"]) * 0.5),
              (inner, flank, lid + SOCKET["burial"]), CONCRETE)

    # ---- 물려 올라가는 단 --------------------------------------------------
    #
    # ⚠ <b>단의 개수를 높이에서 뽑습니다.</b> 3 으로 박아 두었더니, 하늘에 맞춰
    # 시타델을 380 m 에서 650 m 로 올리는 순간 한 단이 122 m 에서 212 m 가 되어
    # <b>48,753 m2</b> 가 깜빡였습니다 - 단 안의 것들(끝벽 격자, 캡슐 갤러리)이
    # 122 m 짜리 단에 맞춰 짜여 있었기 때문입니다. 특히 캡슐 층수는 이 아래에서
    # <c>step</c> 으로 다시 계산되므로, 단이 커지면 조용히 딴 물건이 됩니다.
    #
    # 단 높이를 그대로 두고 <b>개수를 늘리면</b> 안쪽 치수가 전부 검증된 자리에
    # 머무릅니다. 물러남이 늘어 오히려 더 기념비적으로 보이기도 합니다.
    stages = max(3, int(round((top - lid) / 122.0)))
    step = (top - lid) / stages

    for k in range(stages):
        # 단 하나가 오브젝트 하나입니다. 122 m 짜리 상자 넷이 454 m 짜리 상자
        # 하나보다 <b>훨씬 잘 걸러집니다</b> — 발밑에 서 있으면 위 두 단은 화면 밖입니다.
        m.group("Stage%d" % k)

        z0 = lid + step * k
        z1 = z0 + step
        d = depth - k * 24.0
        w = inner - 16.0 - k * 9.0

        m.box((0.0, 0.0, (z0 + z1) * 0.5), (w, d, step), CONCRETE)

        # 물러난 자리의 테두리. 띠의 <b>밑면</b>이 단의 밑면보다 위에 있어야 합니다.
        # 두께 2.0 짜리를 z0 + 1.0 에 두었더니 밑면이 정확히 z0 에 떨어져 단의 밑면과
        # 같은 평면에서 같은 쪽을 보았고, 세 단을 합쳐 <b>37,551 m2</b> 가 깜빡였습니다.
        # 0.4 m 올려 두 면을 갈라 놓습니다.
        m.box((0.0, 0.0, z0 + 1.4), (w + 1.4, d + 1.4, 2.0), DARK)

        # <b>껍질을 덩어리에서 뗍니다.</b> 단의 삼각형은 거의 전부가 이 껍질(격자와
        # 캡슐)이고, 그것은 가까이서만 읽힙니다. 나중에 먼 거리에서 껍질만 걷어낼 때
        # 실루엣을 그대로 두려면 <b>지금 갈라 두어야</b> 합니다.
        m.group("Stage%dSkin" % k)

        # <b>먼 데서는 이것이 안 보입니다.</b> 격자 한 칸이 12 x 21 m 인데
        # 300 m 밖에서는 화면 높이의 4% 라, 그리는 값에 비해 읽히는 것이 없습니다.
        if coarse:
            continue

        endwall(m, w * 0.5, d * 0.5, z0 + 2.6, z1, rng, 0.34 - k * 0.09)

        # 테라스 난간에 붙는 캡슐 갤러리. 단마다 <b>덜 찹니다</b> - 위층은 아직
        # 안 올라간 것이지 지어진 적 없는 것이 아닙니다.
        tiers = max(2, int(step / TIER) - 1)
        capsules(m, dict(tiers=tiers, fill=s["fill"] * (0.46 - k * 0.12)),
                 w, rng, y_half=clear_of(d * 0.5, W * 0.5), base=z0 + 4.0, fade=True)

    m.group("Peak")

    # 꼭대기의 코어와 테두리
    peak_w = inner - 16.0 - (stages - 1) * 9.0
    peak_d = depth - (stages - 1) * 24.0

    for i in (-1, 1):
        m.box((i * peak_w * 0.3, i * peak_d * 0.24, top + 11.4),
              (12.0, 12.0, 22.8), CONCRETE)
        m.box((i * peak_w * 0.3, i * peak_d * 0.24, top + 22.4),
              (13.6, 13.6, 1.2), STEEL)

    # 꼭대기 띠도 마찬가지입니다. top + 0.9 에 두면 밑면이 top 에 떨어져 그 위에
    # 선 코어들의 밑면과 겹칩니다.
    # 어두운 것을 강철로. <b>이 한 상자 때문에 슬롯이 하나 더 생기고</b>
    # 그 슬롯이 곧 드로우 하나입니다 - 12 삼각형이 파츠 전체 삼각형의
    # 0.5~1.4% 인데 제출 비용은 다른 슬롯과 똑같습니다.
    #
    # ⚠ 한 번 더 올려야 했습니다. <c>endwall</c> 의 <b>맨 위 가로 띠는 단 꼭대기를
    # 걸치고</b> 서서 <c>top + 0.8</c> 까지 올라오는데, 마지막 단의 띠가 x 로
    # <c>±36.6</c> 이라 이 띠와 <b>같은 평면</b>에 놓였습니다 - 32 m2 였습니다.
    # 끝면 띠 위로 넘겨 둡니다.
    m.box((0.0, 0.0, top + 2.0), (peak_w + 2.0, peak_d + 2.0, 1.8), STEEL)


# --- Build ------------------------------------------------------------------


def build_core():
    """
    <b>한 베이짜리 뼈대.</b> 프리셋이 몇 베이든 이것을 그만큼 늘어놓습니다.

    프리셋마다 구워 넣으면 같은 288 삼각형이 열 벌 저장되고 열 개의 다른 메시로
    그려집니다. 하나만 두면 스파인 전체가 이것을 인스턴싱합니다.
    """
    m = hardsurface.Mass(MATS)
    core(m, SOCKET["bay"], 1)

    # <b>뼈대는 안 나눕니다.</b> 1,344 삼각형이고 베이마다 놓이므로 38 벌입니다.
    # 셋으로 나누면 렌더러가 114 개가 되는데, 걸러서 아끼는 삼각형보다 드로우
    # 제출이 더 비쌉니다. 나누는 것은 <b>덩치가 큰 쪽만</b>입니다.
    return m.to_object("SM_Mega_Core")


def build(name, coarse=False):
    """
    프리셋이 <b>더하는 것</b>만 세웁니다. 뼈대는 build_core() 가 따로 냅니다.
    원점은 지면이자 프리셋의 한가운데입니다.

    <c>coarse</c> 는 <b>멀리서 볼 판본</b>입니다. 갤러리와 끝면 격자를 빼고 덩어리만
    남깁니다 - 시타델의 30,720 삼각형 중 29,304(95%)가 그 둘이고, 300 m 밖에서는
    갤러리 슬롯 하나가 화면에서 두 화소입니다. <b>깎아 만드는 것이 아니라 안 짓는
    것</b>입니다 - 상자로 만든 것을 데시메이션하면 각이 뭉개져 삼각형 죽이 됩니다.

    난간은 여기 있습니다 — 프리셋마다 끊는 자리가 달라 공용 뼈대에 넣을 수 없습니다.
    대신 <b>양 끝의 단면은 모든 프리셋이 같아야</b> 하고, verify() 가 그것을 봅니다.
    """
    s = PRESETS[name]
    bays = s["bays"]
    length = bays * SOCKET["bay"]

    m = hardsurface.Mass(MATS)
    # <b>hash() 를 쓰면 안 됩니다.</b> 파이썬 문자열 해시는 프로세스마다 뿌려지는
    # 값이 달라, 같은 스크립트를 두 번 돌리면 캡슐이 전부 다른 자리에 꽂혔습니다.
    # 삼각형 수가 빌드마다 흔들려 <b>고친 것과 흔들린 것을 가릴 수 없었습니다.</b>
    rng = random.Random(int(hashlib.md5(name.encode()).hexdigest()[:8], 16))
    prefix = "SM_Mega_" + name

    m.group("Frame")

    W = SOCKET["width"]
    for sign in (-1.0, 1.0):
        mine = sorted((g[1], g[2]) for g in s.get("gaps", ()) if g[0] == sign)
        parapet(m, length, sign * (W * 0.5 - 0.6), mine)

    # <b>층마다 세웁니다.</b> 프리셋이 한 층에만 붙으면 아무리 넓혀도 아래만 찬
    # 구조물이 됩니다. 층마다 쓰임이 다르게 채워야 <b>겹친 보람</b>이 생깁니다.
    # 위로 갈수록 덜 채워 하늘이 보이는 비율이 늘게 합니다.
    open_at = tuple((r[0], 0, r[2]) for r in s.get("rooms", ()))

    for i, level in enumerate(() if coarse else s.get("levels", (0,))):
        thin = dict(s)
        thin["fill"] = s.get("fill", 0.6) * (1.0 - 0.22 * i)

        # 층 하나가 파츠 하나입니다. 층은 <b>54 m 씩 떨어져</b> 있으므로 데크에 서면
        # 늘 한 층만 가깝고, 나머지는 시야 밖으로 나갈 수 있어야 합니다.
        m.group("L%d" % level)

        capsules(m, thin, length, rng, deck=DECKS[level],
                 open_at=open_at if level == 0 else ())
        access(m, thin, length, rng, deck=DECKS[level])

        if level == s.get("levels", (0,))[-1]:
            portal(m, thin, length, rng, deck=DECKS[level])

    # 나머지는 <b>함수 하나가 파츠 하나</b>입니다. 각각이 한 자리에 뭉친 물건이라
    # 그대로 공간 덩어리가 됩니다 - 탱크, 가지, 경사로, 크레인, 수직 코어.
    for tag, fn in (("Industry", industry), ("Branch", branch), ("Citadel", citadel),
                    ("Ramp", ramp), ("Crane", crane), ("Hang", hang),
                    ("Overpass", overpass), ("Spur", spur), ("Shaft", shaft),
                    ("Tower", tower)):
        m.group(tag)

        if fn is citadel:
            fn(m, s, length, rng, coarse)
        else:
            fn(m, s, length, rng)

    m.group("Deck")
    props(m, s, length, rng)

    m.group("Landing")
    inside = rooms(m, s, length, prefix)

    # 거친 판본은 <b>한 덩어리</b>입니다. 멀리서 쓸 것을 파츠로 나누면 걸러지는 것도
    # 없이 드로우만 늘어납니다.
    if coarse:
        return [m.to_object(prefix + "_LOD1")]

    return m.to_parts(prefix) + inside


def seam(objs, x):
    """
    끝면에 닿는 꼭짓점의 (y, z) 목록입니다. <b>연동 규약의 증거</b>입니다.

    두 프리셋의 이 목록이 같으면 어떤 순서로 붙여도 단면이 맞습니다. 말로 "맞춰
    두었다" 고 적는 것과 <b>재서 같다는 것</b>은 다릅니다.

    파츠 <b>전부</b>를 봅니다. 하나만 보면 이음매에 걸친 것이 다른 파츠에 있을 때
    통과해 버립니다.
    """
    out = set()

    for obj in objs:
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


def emit(name, objs, report_extra=None):
    """
    UV 를 깔고 FBX 로 내보냅니다. <b>프리셋마다 파일 하나, 그 안에 파츠 여럿.</b>

    내보내기가 씬 전체를 담으므로(<c>use_selection=False</c>) 파츠를 따로 지정할
    것이 없습니다. 대신 <b>치수는 합쳐서</b> 냅니다 - 유니티가 읽는 것은 프리셋
    하나의 크기이지 파츠 하나의 크기가 아닙니다.
    """
    for obj in objs:
        uv_worldscale.box_uv(obj, UV_TILE)

    path = os.path.join(OUT_DIR, name + ".fbx")
    written = hardsurface.export_fbx(path)

    edge = [hardsurface.bounds(obj) for obj in objs]
    lo = [min(e[0][a] for e in edge) for a in range(3)]
    hi = [max(e[1][a] for e in edge) for a in range(3)]

    out = dict(mesh=name,
               size=[round(hi[a] - lo[a], 2) for a in range(3)],
               top=round(hi[2], 1),
               tris=sum(sum(len(p.vertices) - 2 for p in o.data.polygons)
                        for o in objs),
               parts=len(objs),
               fbx=written)

    if report_extra:
        out.update(report_extra)

    return out


def run():
    report = []

    # ---- 뼈대 한 벌 --------------------------------------------------------
    hardsurface.wipe()
    hardsurface.ensure_materials(MATS)
    core_report = emit("SM_Mega_Core", [build_core()], dict(bay=SOCKET["bay"]))

    # ---- 프리셋이 더하는 것 ------------------------------------------------
    for name in PRESETS:
        hardsurface.wipe()
        hardsurface.ensure_materials(MATS)

        objs = build(name)
        want = PRESETS[name]["bays"] * SOCKET["bay"]

        edge = [hardsurface.bounds(o) for o in objs]
        span = max(e[1][0] for e in edge) - min(e[0][0] for e in edge)

        report.append(emit("SM_Mega_" + name, objs, dict(
            preset=name, bays=PRESETS[name]["bays"], length=want,
            span=round(span, 3),
            # 부품이 프리셋 길이를 넘으면 이웃을 파고듭니다.
            fits=span <= want + 0.01,
            weight=PRESETS[name]["weight"])))

        # <b>멀리서 볼 판본.</b> 덩치가 큰 것에만 답니다.
        if not PRESETS[name].get("lod"):
            continue

        hardsurface.wipe()
        hardsurface.ensure_materials(MATS)

        coarse = emit("SM_Mega_" + name + "_LOD1", build(name, coarse=True))
        report[-1]["lod1"] = coarse["mesh"]
        report[-1]["lod1Tris"] = coarse["tris"]

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
                          tris=r["tris"], parts=r["parts"],
                          lod1=r.get("lod1", ""), lod1Tris=r.get("lod1Tris", 0))
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
        objs = build(name)
        for obj in objs:
            uv_worldscale.box_uv(obj, UV_TILE)
            obj.location.y = 6000.0

        made[name] = objs

    order = ["Viaduct", "Habitat", "Viaduct", "Shaft", "Viaduct",
             "Habitat", "Viaduct", "CapsuleTower", "Viaduct", "Ramp",
             "Crane", "Viaduct", "Citadel", "Viaduct"]

    x = 0.0
    for name in order:
        bays = PRESETS[name]["bays"]
        span = bays * SOCKET["bay"]

        for b in range(bays):
            piece = core_obj.copy()
            piece.data = core_obj.data
            piece.location = (x + (b + 0.5) * SOCKET["bay"], 0.0, 0.0)
            bpy.context.scene.collection.objects.link(piece)

        for source in made[name]:
            part = source.copy()
            part.data = source.data
            part.location = (x + span * 0.5, 0.0, 0.0)
            bpy.context.scene.collection.objects.link(part)

        x += span

    bpy.ops.wm.save_as_mainfile(filepath=BLEND_PATH)


if __name__ == "__main__":
    core_out, out, points = run()
    lay_out()
    print("###JSON###" + json.dumps(
        dict(core=core_out, presets=out, seamPoints=points)))
