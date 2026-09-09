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

SLOT = 25.0         # 유니티가 조각을 놓는 간격

# ⚠ <b>이웃과 겹치면 안 됩니다.</b> 처음에 26 m 로 두어 1 m 를 겹쳐 모서리 틈을
# 막으려 했는데, 겹친 1 m 에서 두 모듈의 <b>바깥면이 같은 평면에서 같은 쪽을
# 봅니다</b> — 깊이 버퍼가 고르지 못해 깜빡입니다. 나란한 셋과 직각 하나로
# 재 보니 쌍 98 개 268.6 m2 였고, 전부 y = 0(바깥면)이었습니다.
#
# 딱 맞게 두면 맞닿는 면이 <b>서로 반대를 봅니다</b>(A 의 +X 면과 B 의 -X 면).
# 등을 맞댄 쌍은 어느 시점에서도 하나만 보이므로 깜빡이지 않습니다.
LONG = SLOT
KERB = 1.5          # 지면 위로 올라오는 연석. 차가 굴러떨어지지 않을 만큼
DEEP = 21.0         # 잘린 단면의 깊이. 조각 안의 높이차를 삼킬 만큼
HANG = 30.0         # 단면 아래로 매달린 강재
INWARD = 4.0        # 지형 아래로 파고드는 깊이. 틈이 비치지 않게


def shape(tag, put, bar):
    """
    모듈 하나의 기하입니다. <b>상자를 놓는 함수를 밖에서 받습니다.</b>

    그래야 같은 기하를 <b>옮기고 돌려서</b> 여러 벌 쌓을 수 있습니다 — 테두리의
    깜빡임은 모듈 <b>안</b>이 아니라 <b>이웃끼리</b> 생기므로, 한 벌만 검사하는
    <c>zfight_audit</c> 으로는 잡히지 않습니다.
    """


    # ---- 연석 -------------------------------------------------------------
    #
    # 지면 위로 나오는 유일한 부분입니다. <b>난간이 아니라 보</b>입니다 - 난간을
    # 세우면 밖이 안 보여, 바닥이 없다는 사실을 감추게 됩니다.
    tag("Kerb")
    put((0.0, 1.3, KERB * 0.5 - 0.2), (LONG, 2.6, KERB), CONCRETE)
    put((0.0, 0.2, KERB - 0.05), (LONG, 0.6, 0.34), SIGNAL)

    # ---- 잘린 단면 ---------------------------------------------------------
    #
    # 지반의 <b>두께</b>입니다. 여기가 이 물건의 요점이라 켜를 나눠 둡니다 -
    # 통짜 콘크리트 한 장이면 절벽이지 건축물이 아닙니다.
    tag("Slab")

    layers = (
        (3.4, CONCRETE, 0.0),      # 포장 아래 슬래브
        (5.2, DARK, 0.9),          # 설비층. 안으로 물려 그림자가 생깁니다
        (3.6, CONCRETE, 0.15),
        (4.4, DARK, 1.1),
        (4.4, CONCRETE, 0.3),
    )

    top = -0.2

    for thick, slot, bite in layers:
        put((0.0, INWARD * 0.5 + bite * 0.5, top - thick * 0.5),
                 (LONG, INWARD + bite, thick), slot)
        top -= thick

    floor = top

    # 단면을 세로로 가르는 리브. 없으면 켜만 있는 판이 됩니다.
    for i in range(-2, 3):
        put((i * 5.0, 0.35, -DEEP * 0.5), (1.6, 0.9, DEEP - 1.0), CONCRETE)

    # ---- 매달린 강재 -------------------------------------------------------
    #
    # 단면 아래가 그냥 끊기면 <b>부러진 것</b>으로 보입니다. 아래로 이어지는
    # 구조가 있어야 "여기가 끝" 이 아니라 "여기부터는 안 보인다" 가 됩니다.
    tag("Truss")

    # ⚠ <b>슬래브 <c>밑에</c> 놓아야 합니다.</b> 처음에 -DEEP 을 기준으로 잡았더니
    # 켜의 실제 바닥보다 0.2 m 높아, 이 보의 앞면과 맨 아래 켜의 앞면이 <b>같은
    # 평면에서 같은 쪽을 봤습니다</b> - 모듈 하나마다 5 m2 씩 깜빡였습니다.
    # 켜를 쌓고 남은 바닥값을 그대로 받아 씁니다. 앞면도 0.3 m 물려, 평면이
    # 겹칠 일 자체를 없앱니다.
    beam = floor - 1.4

    put((0.0, 1.5, beam), (LONG, 2.4, 2.8), STEEL)

    for i in range(-2, 3):
        x = i * 5.6

        bar((x, 1.5, beam), (x * 0.55, INWARD, -DEEP - HANG), 1.5, STEEL)
        put((x, INWARD * 0.6, -DEEP - HANG * 0.55), (1.1, 1.1, HANG * 0.9), STEEL)

    for k in range(1, 4):
        z = -DEEP - HANG * k / 4.0
        put((0.0, INWARD * 0.6, z), (LONG * 0.86, 1.3, 1.1), STEEL)

    # ---- 내민 받침 ---------------------------------------------------------
    #
    # 바깥으로 튀어나온 것 몇. 테두리가 <b>자로 그은 직선</b>이면 잘린 자리가
    # 아니라 벽으로 보입니다.
    tag("Bracket")

    for i in (-1, 1):
        x = i * 7.0

        put((x, -2.2, -3.0), (3.4, 5.0, 2.2), STEEL)
        bar((x, -4.2, -3.6), (x, 1.0, -9.0), 1.2, STEEL)



def build():
    hardsurface.wipe()
    hardsurface.ensure_materials(MATS)

    mass = hardsurface.Mass(MATS)
    shape(mass.group, mass.box, mass.strut)

    made = mass.to_parts("M_WorldRim", min_faces=2500)
    size = hardsurface.export_fbx(OUT)

    lo, hi = hardsurface.bounds(made[0])
    print("###RIM###%d parts=%d faces=%d %.1fx%.1fx%.1f" %
          (size, len(made), len(mass.faces),
           hi[0] - lo[0], hi[1] - lo[1], hi[2] - lo[2]))


def cluster():
    """
    <b>놓인 모습 그대로</b> 몇 벌을 한 메시에 쌓아 깜빡임을 잽니다.

    유니티가 25 m 간격으로 줄지어 놓고, 세계의 모서리에서는 직각으로 만납니다.
    그 두 상황을 그대로 만들어 봅니다 — 나란한 셋과, 직각으로 붙는 하나.
    """
    hardsurface.wipe()
    hardsurface.ensure_materials(MATS)

    mass = hardsurface.Mass(MATS)

    def stamp(dx, dy, dz, turn):
        def move(p):
            x, y, z = p
            if turn:
                x, y = -y, x
            return (x + dx, y + dy, z + dz)

        def size(sx, sy, sz):
            return (sy, sx, sz) if turn else (sx, sy, sz)

        def put(center, extent, mat=0, *rest):
            mass.box(move(center), size(*extent), mat, *rest)

        def bar(a, b, thick, mat=0):
            mass.strut(move(a), move(b), thick, mat)

        shape(lambda name: None, put, bar)

    for i in (-1, 0, 1):
        stamp(i * SLOT, 0.0, 0.0, False)

    # 세계의 모서리. 직각으로 만나는 이웃입니다.
    stamp(SLOT * 1.5, -SLOT * 0.5, 0.0, True)

    obj = mass.to_object("Cluster")

    import zfight_audit
    hits = zfight_audit.audit(zfight_audit.faces_of(obj))

    worst = sorted(hits, key=lambda h: -h["area"])[:5]

    print("###CLUSTER###쌍 %d · 넓이 %.1f m2" %
          (len(hits), sum(h["area"] for h in hits)))

    for h in worst:
        print("###WORST###%.2f m2 · %s 축 %+d 쪽 · 평면 %.2f"
              % (h["area"], h["axis"], h["facing"], h["at"]))


if __name__ == "__main__":
    if os.environ.get("RIM_AUDIT") == "1":
        cluster()
    else:
        build()
