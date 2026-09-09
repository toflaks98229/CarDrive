# -*- coding: utf-8 -*-
"""
<b>대기권을 뚫는 기둥</b>을 짓습니다.

플레이어가 선 대지는 거대 건축물의 인공 지반 한 조각이고, 그 지반에는 <b>위로
붙어 있는 것</b>이 있습니다. 하늘을 덮은 천장은 스카이맵이 맡지만, 스카이맵은
시차가 없어 <b>다가갈 수 없습니다.</b> 다가갈 수 있는 것이 하나는 있어야
"그림"이 아니라 "구조물"이 되므로, 기둥만 진짜 기하로 세웁니다.

<b>꼭대기를 안 만듭니다.</b> 460 m 까지 올리고 그냥 끝냅니다 - 이 게임의 안개는
257 m 에서 완전히 닫히므로, 밑동에 서서 올려다보면 <b>안개가 먼저 기둥을 지웁니다.</b>
끝을 보여 주지 않는 것이 "어디까지 가는지 모르겠다" 를 만드는 유일한 방법이고,
공교롭게도 파클립(482 m)을 건드리지 않아도 되는 방법이기도 합니다.

    blender -b -noaudio --python Art/Blender/build_column.py

⚠ 재질 이름은 메가스트럭처와 <b>같아야 합니다</b>. 유니티 쪽에서 이름으로 짝을
찾아 <c>MegaConcrete</c> 등을 물립니다 - 다르면 URP Lit 이 붙습니다.
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

MATS = [
    ("M_Mega_Concrete", (0.66, 0.65, 0.61, 1.0), 0.92, 0.0),
    ("M_Mega_Steel",    (0.22, 0.235, 0.26, 1.0), 0.55, 0.85),
    ("M_Mega_Dark",     (0.05, 0.058, 0.064, 1.0), 0.85, 0.1),
    ("M_Mega_Signal",   (0.86, 0.36, 0.09, 1.0), 0.7, 0.0),
]

CONCRETE, STEEL, DARK, SIGNAL = 0, 1, 2, 3

# --- 치수 -------------------------------------------------------------------
#
# 원점은 <b>밑동</b>입니다. 유니티가 지형 높이에 그대로 얹습니다.

TALL = 460.0        # 안개가 닫히는 257 m 를 훌쩍 넘깁니다
WIDE = 78.0         # 밑동의 한 변
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

    made = mass.to_parts("M_SkyColumn", min_faces=2500)

    size = hardsurface.export_fbx(OUT)

    lo, hi = hardsurface.bounds(made[0])
    print("###COLUMN###%d parts=%d faces=%d %.0fx%.0fx%.0f" %
          (size, len(made), len(mass.faces),
           hi[0] - lo[0], hi[1] - lo[1], hi[2] - lo[2]))


if __name__ == "__main__":
    build()
