# -*- coding: utf-8 -*-
"""
<b>대기권을 뚫는 기둥</b>을 짓습니다.

플레이어가 선 대지는 거대 건축물의 인공 지반 한 조각이고, 그 지반에는 <b>위로
붙어 있는 것</b>이 있습니다. 하늘을 덮은 천장은 스카이맵이 맡지만, 스카이맵은
시차가 없어 <b>다가갈 수 없습니다.</b> 다가갈 수 있는 것이 하나는 있어야
"그림"이 아니라 "구조물"이 되므로, 기둥만 진짜 기하로 세웁니다.

<b>꼭대기를 안 만듭니다.</b> 460 m 까지 올리고 그냥 끝냅니다. 끝을 보여 주지
않는 것이 "어디까지 가는지 모르겠다" 를 만드는 유일한 방법입니다.

⚠ <b>안개가 지워 줄 거라고 믿으면 안 됩니다.</b> 처음에 그렇게 적어 뒀는데
틀렸습니다 - 기둥 재질의 <c>_FogScale</c> 이 1 이 아니라서, 아무리 멀어도 안개가
다 먹지 못하고 <b>파클립에 잘린 단면</b>이 그대로 보입니다. 게다가 파클립은
평면이라 잘리는 자리가 <b>시선의 상하 각도에 따라 움직입니다</b> - 고개를 들면
기둥 끝이 잘려 나가고 내리면 돌아옵니다. <c>ViewRangeScaler</c> 의 랜드마크
사거리가 그것을 맡습니다.

    blender -b -noaudio --python Art/Blender/build_column.py

⚠ 재질 이름이 <c>M_Column_*</c> 여야 합니다. 유니티 쪽에서 이름으로 짝을 찾아
<c>ColumnConcrete</c> 등을 물립니다 - 다르면 URP Lit 이 붙습니다.
"""

import math
import os
import random
import sys

import bpy

HERE = os.path.dirname(os.path.abspath(__file__))
if HERE not in sys.path:
    sys.path.insert(0, HERE)

import hardsurface  # noqa: E402

OUT = r"E:\GamePJ\CarDrive\Assets\_Project\04.Art\02.Models\World\M_SkyColumn.fbx"

# <b>메가스트럭처 재질을 쓰지 않습니다.</b> 기둥은 스카이맵에 그려 둔 기둥들과
# 나란히 서므로, 값과 안개 거동을 그쪽에 맞춰야 합니다 - 메가스트럭처의
# 콘크리트(0.66)를 쓰면 안개를 먹기도 전에 이미 밝아 다른 물건으로 보입니다.
MATS = [
    ("M_Column_Concrete", (0.30, 0.30, 0.29, 1.0), 0.92, 0.0),
    ("M_Column_Dark",     (0.105, 0.108, 0.115, 1.0), 0.80, 0.2),
    ("M_Column_Signal",   (0.86, 0.36, 0.09, 1.0), 0.7, 0.0),
]

# 강재 띠도 어두운 쪽으로 보냅니다. 밝은 띠가 층마다 있으면 멀리서
# <b>줄무늬</b>로 뭉쳐 기둥이 아니라 무늬가 됩니다.
CONCRETE, DARK, SIGNAL = 0, 1, 2
STEEL = DARK

# --- 치수 -------------------------------------------------------------------
#
# 원점은 <b>밑동</b>입니다. 유니티가 지형 높이에 그대로 얹습니다.

TALL = 780.0        # 밑동에서 올려다보면 꼭대기가 천정 가까이 갑니다
WIDE = 96.0         # 밑동의 한 변
NARROW = 0.62       # 꼭대기에서 이 비율까지 좁아집니다
FLOOR = 34.0        # 층띠 간격
FOOT = 46.0         # 밑동 확장부의 높이


def at(z):
    """높이 <c>z</c> 에서의 한 변. 위로 갈수록 좁아집니다."""
    k = min(max(z / TALL, 0.0), 1.0)
    return WIDE * (1.0 - (1.0 - NARROW) * k)


def build():
    hardsurface.wipe()
    hardsurface.ensure_materials(MATS)

    rng = random.Random(20260909)
    mass = hardsurface.Mass(MATS)

    # ---- 밑동 -------------------------------------------------------------
    #
    # <b>땅에 꽂힌 것이 아니라 땅에서 자란 것</b>으로 보여야 합니다. 기둥을 그냥
    # 지면에 세우면 무대 소품처럼 보이므로, 아래로 갈수록 벌어지는 받침과
    # 사방으로 뻗은 부벽을 답니다.
    mass.group("Foot")

    for i in range(5):
        k = i / 4.0
        wide = WIDE * (1.42 - 0.42 * k)
        mass.box((0.0, 0.0, FOOT * k * 0.5 + 1.0), (wide, wide, FOOT * 0.34), CONCRETE)

    # 부벽. 네 귀퉁이에서 밑동으로 비스듬히 붙습니다.
    for k in range(4):
        angle = math.pi * 0.5 * k + math.pi * 0.25
        out = WIDE * 0.92

        x = math.cos(angle) * out
        y = math.sin(angle) * out

        mass.strut((x, y, 0.0), (x * 0.34, y * 0.34, FOOT * 2.1), 13.0, CONCRETE)
        mass.box((x, y, 7.0), (22.0, 22.0, 14.0), CONCRETE)

    # ---- 몸통 -------------------------------------------------------------
    #
    # 층띠는 <b>자</b>입니다. 되풀이되는 것이 없으면 얼마나 높은지 알 수 없고,
    # 그러면 굵기만 있고 크기가 없는 기둥이 됩니다.
    for level in range(int(TALL / FLOOR)):
        low = level * FLOOR
        high = low + FLOOR

        mass.group("Shaft%d" % (level // 4))

        wide = at((low + high) * 0.5)

        mass.box((0.0, 0.0, (low + high) * 0.5), (wide, wide, FLOOR), CONCRETE)

        # 세로 골. 민짜 면은 크기를 못 읽게 합니다.
        for s in (-1.0, 1.0):
            mass.box((s * wide * 0.5, 0.0, (low + high) * 0.5),
                     (wide * 0.10, wide * 0.56, FLOOR), DARK)
            mass.box((0.0, s * wide * 0.5, (low + high) * 0.5),
                     (wide * 0.56, wide * 0.10, FLOOR), DARK)

        # 층띠.
        mass.box((0.0, 0.0, high), (wide * 1.09, wide * 1.09, 4.6), STEEL)

        # 드문드문 튀어나온 것. 층마다 같으면 <b>기둥이 아니라 무늬</b>가 됩니다.
        if rng.random() < 0.3:
            side = rng.randrange(4)
            angle = math.pi * 0.5 * side
            reach = wide * 0.5 + 11.0

            mass.box((math.cos(angle) * reach, math.sin(angle) * reach, low + 12.0),
                     (24.0, 24.0, 9.0), STEEL)

        # 항공 표지. 안개 너머에서도 <b>거기 뭔가 있다</b>를 알립니다.
        if level % 4 == 3:
            for s in (-1.0, 1.0):
                mass.box((s * wide * 0.5, 0.0, high - 2.0), (2.4, 6.0, 2.4), SIGNAL)

    # ---- 머리 -------------------------------------------------------------
    #
    # <b>끝을 감출 수 없게 되었습니다.</b> 처음에는 안개가 꼭대기를 지워 줄 것으로
    # 보고 아무것도 안 얹었는데, 기둥을 하늘 쪽 거동에 맞추느라 안개를 4 분의 1 만
    # 먹이자 <b>공중에서 뚝 끊긴 단면</b>이 그대로 드러났습니다.
    #
    # 그래서 끝을 <b>감추는 대신 짓습니다.</b> 위로 무언가를 받치는 머리가 있으면
    # "잘렸다" 가 아니라 "여기서 위층이 시작한다" 로 읽히고, 그것이 스카이맵의
    # 기둥들이 천장에 닿는 방식과도 같습니다.
    mass.group("Head")

    crown = at(TALL)

    for i in range(4):
        k = i / 3.0
        mass.box((0.0, 0.0, TALL + 6.0 + k * 26.0),
                 (crown * (1.0 + k * 1.5), crown * (1.0 + k * 1.5), 20.0), CONCRETE)

    # 사방으로 뻗는 보. 머리가 <b>무언가를 받치고 있다</b>는 것이 이 부재입니다.
    for k in range(4):
        angle = math.pi * 0.5 * k
        reach = crown * 2.3

        mass.box((math.cos(angle) * reach * 0.5, math.sin(angle) * reach * 0.5,
                  TALL + 74.0),
                 (reach if k % 2 == 0 else crown * 0.5,
                  crown * 0.5 if k % 2 == 0 else reach, 15.0), DARK)

        mass.strut((math.cos(angle) * reach, math.sin(angle) * reach, TALL + 74.0),
                   (math.cos(angle) * crown * 0.4, math.sin(angle) * crown * 0.4,
                    TALL - 40.0), 6.0, DARK)

    made = mass.to_parts("M_SkyColumn", min_faces=2500)

    size = hardsurface.export_fbx(OUT)

    lo, hi = hardsurface.bounds(made[0])
    print("###COLUMN###%d parts=%d faces=%d %.0fx%.0fx%.0f" %
          (size, len(made), len(mass.faces),
           hi[0] - lo[0], hi[1] - lo[1], hi[2] - lo[2]))


if __name__ == "__main__":
    build()
