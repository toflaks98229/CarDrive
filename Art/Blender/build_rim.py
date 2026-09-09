# -*- coding: utf-8 -*-
"""
<b>세계가 끝나는 자리</b>의 테두리 골조입니다.

대지가 자연 지형이 아니라 거대 건축물의 인공 지반 한 조각이라면, 끝까지 갔을 때
나와야 하는 것은 낭떠러지가 아니라 <b>잘린 단면</b>입니다 - 몇 겹의 슬래브와
그 아래 매달린 강재, 그리고 그 너머로는 바닥이 안 보이는 구름. 구름은 스카이맵의
아래쪽 반구가 이미 맡고 있으므로, 여기서 지을 것은 <b>잘린 자리</b>뿐입니다.

<b>왜 모듈인가.</b> 세계는 100 m 타일 103 장이고 격자가 꽉 차 있지 않아, 바깥면이
46 개인 들쭉날쭉한 계단입니다. 게다가 한 면(100 m) 안에서 지형이 중앙값 4.6 m,
최대 18.4 m 오르내립니다 - 통짜로 두르면 어딘가는 뜨고 어딘가는 파묻힙니다.
25 m 조각으로 끊어 <b>조각마다 제 높이</b>에 앉히면 높이차가 넷으로 나뉘어,
남는 차이는 연석 높이 안에 들어옵니다. 조각 사이의 단차는 계단으로 읽힙니다.

    blender -b -noaudio --python Art/Blender/build_rim.py

축: X 는 테두리를 따라, <b>-Y 가 바깥</b>, Z 가 위입니다. 내보내기가 블렌더 -Y 를
유니티 +Z 로 옮기므로 유니티에서 <c>LookRotation(바깥방향)</c> 하나면 놓입니다.
<b>X 로 좌우 대칭</b>이라 길이 방향이 뒤집혀도 같습니다 - 축 하나 헷갈려 뒤집히는
사고를 아예 없애려고 그렇게 뒀습니다.

⚠ 재질 이름은 메가스트럭처와 같아야 합니다. 이름으로 짝을 찾아 물립니다.
"""

import os
import sys

import bpy

HERE = os.path.dirname(os.path.abspath(__file__))
if HERE not in sys.path:
    sys.path.insert(0, HERE)

import hardsurface  # noqa: E402

OUT = r"E:\GamePJ\CarDrive\Assets\_Project\04.Art\02.Models\World\M_WorldRim.fbx"

MATS = [
    ("M_Mega_Concrete", (0.66, 0.65, 0.61, 1.0), 0.92, 0.0),
    ("M_Mega_Steel",    (0.22, 0.235, 0.26, 1.0), 0.55, 0.85),
    ("M_Mega_Dark",     (0.05, 0.058, 0.064, 1.0), 0.85, 0.1),
    ("M_Mega_Signal",   (0.86, 0.36, 0.09, 1.0), 0.7, 0.0),
]

CONCRETE, STEEL, DARK, SIGNAL = 0, 1, 2, 3

# --- 치수 -------------------------------------------------------------------
#
# 원점은 <b>바깥면의 지면 높이</b>입니다. 유니티가 조각마다 그 자리 지형의
# 가장 높은 점에 얹습니다.

LONG = 26.0         # 25 m 자리에 26 m. 1 m 는 이웃과 겹쳐 모서리 틈을 막습니다
KERB = 1.5          # 지면 위로 올라오는 연석. 차가 굴러떨어지지 않을 만큼
DEEP = 21.0         # 잘린 단면의 깊이. 조각 안의 높이차를 삼킬 만큼
HANG = 30.0         # 단면 아래로 매달린 강재
INWARD = 4.0        # 지형 아래로 파고드는 깊이. 틈이 비치지 않게


def build():
    hardsurface.wipe()
    hardsurface.ensure_materials(MATS)

    mass = hardsurface.Mass(MATS)

    # ---- 연석 -------------------------------------------------------------
    #
    # 지면 위로 나오는 유일한 부분입니다. <b>난간이 아니라 보</b>입니다 - 난간을
    # 세우면 밖이 안 보여, 바닥이 없다는 사실을 감추게 됩니다.
    mass.group("Kerb")
    mass.box((0.0, 1.3, KERB * 0.5 - 0.2), (LONG, 2.6, KERB), CONCRETE)
    mass.box((0.0, 0.2, KERB - 0.05), (LONG, 0.6, 0.34), SIGNAL)

    # ---- 잘린 단면 ---------------------------------------------------------
    #
    # 지반의 <b>두께</b>입니다. 여기가 이 물건의 요점이라 켜를 나눠 둡니다 -
    # 통짜 콘크리트 한 장이면 절벽이지 건축물이 아닙니다.
    mass.group("Slab")

    layers = (
        (3.4, CONCRETE, 0.0),      # 포장 아래 슬래브
        (5.2, DARK, 0.9),          # 설비층. 안으로 물려 그림자가 생깁니다
        (3.6, CONCRETE, 0.15),
        (4.4, DARK, 1.1),
        (4.4, CONCRETE, 0.3),
    )

    top = -0.2

    for thick, slot, bite in layers:
        mass.box((0.0, INWARD * 0.5 + bite * 0.5, top - thick * 0.5),
                 (LONG, INWARD + bite, thick), slot)
        top -= thick

    # 단면을 세로로 가르는 리브. 없으면 켜만 있는 판이 됩니다.
    for i in range(-2, 3):
        mass.box((i * 5.0, 0.35, -DEEP * 0.5), (1.6, 0.9, DEEP - 1.0), CONCRETE)

    # ---- 매달린 강재 -------------------------------------------------------
    #
    # 단면 아래가 그냥 끊기면 <b>부러진 것</b>으로 보입니다. 아래로 이어지는
    # 구조가 있어야 "여기가 끝" 이 아니라 "여기부터는 안 보인다" 가 됩니다.
    mass.group("Truss")

    mass.box((0.0, 1.2, -DEEP - 1.4), (LONG, 2.4, 2.8), STEEL)

    for i in range(-2, 3):
        x = i * 5.6

        mass.strut((x, 1.2, -DEEP - 1.4), (x * 0.55, INWARD, -DEEP - HANG), 1.5, STEEL)
        mass.box((x, INWARD * 0.6, -DEEP - HANG * 0.55), (1.1, 1.1, HANG * 0.9), STEEL)

    for k in range(1, 4):
        z = -DEEP - HANG * k / 4.0
        mass.box((0.0, INWARD * 0.6, z), (LONG * 0.86, 1.3, 1.1), STEEL)

    # ---- 내민 받침 ---------------------------------------------------------
    #
    # 바깥으로 튀어나온 것 몇. 테두리가 <b>자로 그은 직선</b>이면 잘린 자리가
    # 아니라 벽으로 보입니다.
    mass.group("Bracket")

    for i in (-1, 1):
        x = i * 7.0

        mass.box((x, -2.2, -3.0), (3.4, 5.0, 2.2), STEEL)
        mass.strut((x, -4.2, -3.6), (x, 1.0, -9.0), 1.2, STEEL)

    made = mass.to_parts("M_WorldRim", min_faces=2500)
    size = hardsurface.export_fbx(OUT)

    lo, hi = hardsurface.bounds(made[0])
    print("###RIM###%d parts=%d faces=%d %.1fx%.1fx%.1f" %
          (size, len(made), len(mass.faces),
           hi[0] - lo[0], hi[1] - lo[1], hi[2] - lo[2]))


if __name__ == "__main__":
    build()
