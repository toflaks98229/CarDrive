# -*- coding: utf-8 -*-
"""
스트라이더의 <b>건포트와 무장</b>만 다시 짓습니다. 몸은 손대지 않습니다.

WHY THIS EXISTS SEPARATELY
  스트라이더의 몸은 다른 프로젝트(`Sandland`)의 `build_strider.py` 가 만들었지만,
  <b>출하된 blend 는 그 스크립트가 지금 내는 것과 다릅니다</b> - 스크립트는 19 파츠를
  내고 blend 에는 16 파츠가 있으며, 파츠마다 삼각형 수도 다릅니다(Chassis 300 대 60).
  그 스크립트를 다시 돌리면 <b>지금 게임에 있는 것이 아닌 다른 로봇</b>이 나옵니다.

  그래서 이 파일은 <b>기존 blend 를 열어 총 관련 오브젝트만 갈아 끼웁니다.</b>
  몸 15 개는 읽지도 만지지도 않습니다.

측정으로 시작한 문제 넷 (2026-09-10, 출하본 실측)
  1. 무기 둘의 바운딩 박스가 <b>세 축 모두 10% 안에서 같습니다</b> -
     1.54~1.60 x 4.03~4.40 x 1.56~1.84. 화면에서 두 무장은 같은 실루엣입니다.
  2. 둘 다 피벗 앞으로 3.8~4.2 m 를 내밀고 <b>뒤로는 0.22 m</b> 뿐입니다.
     저울추가 없어 무겁게 읽히지 않습니다.
  3. 포드 240 삼각형에 <b>콘크리트가 한 조각도 없습니다.</b> "밝은 콘크리트 /
     어두운 강철" 이 이 로봇의 재질 규칙인데 포드만 통째로 어둡습니다.
  4. <b>탄약이 어디서 오고 반동이 어디로 가는지</b> 형상에 없습니다. 요크가 핀
     하나를 잡고 있을 뿐입니다.

형태 언어 (Strider_README.md 의 규칙 - 어기지 마십시오)
  * 부재는 직사각 단면 박스 빔. 챔퍼 0.03~0.05 - 하이라이트만 잡습니다.
  * Bevel 모디파이어 금지.
  * 쌓아 올린 모놀리스이지 깎아낸 곡면이 아닙니다.
  * 명도 대비로 재질을 읽힙니다: 밝은 콘크리트 슬래브 / 어두운 강철 구조.
  * <b>장식 금지.</b> 돌출된 것은 전부 구조여야 합니다.

Run:
    blender -b <Strider.blend> --python build_strider_guns.py
  또는 애드온에서:
    import build_strider_guns as G; G.run()
"""
import math
import os
import sys

import bpy
import bmesh
from mathutils import Matrix, Vector


def V(x, y, z):
    return Vector((float(x), float(y), float(z)))


TAU = math.pi * 2.0

BLEND_PATH = "E:/GamePJ/Sandland/Art/Blender/Strider.blend"

# FBX 는 <b>두 군데로, 서로 다르게</b> 나갑니다.
#
# ⚠ 카드라이브의 임포터는 FBX 계층을 안 씁니다 - <b>메시만 꺼내</b> 자기 리그 노드
# 밑에 새로 답니다(`WalkerModelSetup.Attach`). 그러면 메시의 정점이 <b>오브젝트
# 기준으로 바로 놓여 있어야</b> 하므로 <c>bake_space_transform=True</c> 가
# 필요합니다. 이것 없이 구웠더니 다리 메시가 제자리를 못 찾아 로봇이 <b>흩어진
# 덩어리</b>로 반입되었습니다 - 임포트는 오류 없이 끝나고 그림에서만 보였습니다.
#
# 샌드랜드는 계층을 그대로 쓰므로 굽지 않습니다. 두 사본이 이미 크기부터
# 달랐던(192 KB 대 135 KB) 이유가 이것입니다.
FBX_DIRS = (("E:/GamePJ/CarDrive/Assets/_Project/04.Art/02.Models/Robot", True),
            ("E:/GamePJ/Sandland/Assets/3DModel/Strider", False))

# ----------------------------------------------------------------------------
# 재질 - 몸이 쓰는 것 그대로입니다. 새로 만들지 않습니다.
# ----------------------------------------------------------------------------
MAT_CONCRETE, MAT_STEEL, MAT_DARK, MAT_LAMP = 0, 1, 2, 3
MAT_NAMES = ("M_Strider_Concrete", "M_Strider_Steel",
             "M_Strider_Dark", "M_Strider_Lamp")

RIG_NAME = "Strider_Rig"

# ----------------------------------------------------------------------------
# 마운트 좌표 - <b>출하된 blend 에서 잰 값</b>이지 스크립트의 상수가 아닙니다.
# ----------------------------------------------------------------------------
GUN_PIVOT = V(0.0, -1.35, 3.78)     # SKT_GunPort · 부앙 축의 중심
GUN_DECK = V(0.0, -1.35, 4.63)      # 선회 링이 섀시에 물리는 면

# 부앙 범위입니다. 리그 json 의 Gun_Pitch range 와 <b>같아야</b> 합니다 -
# 여기 값으로 간섭을 검사하므로, 엔진이 더 벌리면 검사가 거짓이 됩니다.
#
# ⚠ <b>-25 를 -10 으로 줄였습니다.</b> 앞선 값은 형상이 허락하지 않습니다 -
# <c>pitch_limit()</c> 로 재면 무장 여섯의 <b>드는 각</b>이 10.5~18° 이고,
# 그 위로는 약실이 선회 링을 뚫습니다. 반대로 <b>내리는 각은 60° 가 넉넉히
# 됩니다</b>(재면 80° 까지 아무것도 안 막습니다). 키가 5.6 m 인 기계가 땅 위의
# 것을 겨누므로, 필요한 것은 원래 내리는 각입니다.
#
# 음수가 드는 쪽입니다. 블렌더의 +X 회전은 -y 를 보는 포신을 <b>아래로</b>
# 돌리고, 유니티 쪽 규약도 같습니다.
PITCH_RANGE = (-10.0, 60.0)

# 가로 배치. 안에서 밖으로: 방패판 · 트러니언 보스 · 요크 치크 · 섹터 기어.
# <b>넷이 겹치면 안 됩니다.</b> 방패판은 치크 <b>사이를</b> 지나가야 하고 - 부앙할 때
# 치크를 지나므로 - 섹터는 치크보다 밖이어야 피니언이 물립니다.
MANTLET_HX = 0.51
BOSS_HX = 0.52
CHEEK_X, CHEEK_HX = 0.66, 0.13
SECTOR_X, SECTOR_HX = 0.92, 0.06

# 트러니언 위로 쓸 수 있는 높이입니다. 섀시 밑면이 4.692 이고 선회 링이 그 밑에
# 붙으므로, 부앙하는 것은 전부 이 밑을 지나야 합니다.
YOKE_FLOOR = 4.44


# ----------------------------------------------------------------------------
# 기하 도구 - build_strider.py 의 것과 같습니다. 저쪽을 import 하지 않는 이유는
# 그 모듈이 읽히기만 해도 상수를 들고 오고, 우리가 쓰는 좌표는 <b>실측값</b>이라
# 그 상수와 섞이면 어느 쪽이 진짜인지 다시 헷갈리기 때문입니다.
# ----------------------------------------------------------------------------
def loft(sections):
    """sections: 훑는 방향으로 늘어선, 길이가 같은 정점 고리들."""
    n = len(sections[0])
    verts = [v.copy() for s in sections for v in s]
    faces = []
    for i in range(len(sections) - 1):
        a, b = i * n, (i + 1) * n
        for j in range(n):
            k = (j + 1) % n
            faces.append((a + j, a + k, b + k, b + j))
    faces.append(tuple(reversed(range(n))))
    last = (len(sections) - 1) * n
    faces.append(tuple(range(last, last + n)))
    return verts, faces


def prof_cham(hx, hy, c):
    """모서리를 깎은 직사각형(8점). c 가 0 에 가까우면 각진 상자입니다."""
    c = max(1e-4, min(c, hx * 0.85, hy * 0.85))
    return [(hx, hy - c), (hx - c, hy), (-hx + c, hy), (-hx, hy - c),
            (-hx, -hy + c), (-hx + c, -hy), (hx - c, -hy), (hx, -hy + c)]


def prof_rect(hx, hy, c=0.03):
    return prof_cham(hx, hy, c)


def prof_ngon(n, r, phase=0.0):
    return [(math.cos(phase + TAU * i / n) * r,
             math.sin(phase + TAU * i / n) * r) for i in range(n)]


def solid(M, stops):
    secs = [[M @ Vector((a, b, z)) for (a, b) in prof] for z, prof in stops]
    return loft(secs)


def frame(zdir, xhint, origin=V(0, 0, 0)):
    """로컬 +Z 가 zdir, 로컬 +X 가 xhint 에 가장 가까운 정규직교 4x4."""
    z = Vector(zdir).normalized()
    x = Vector(xhint) - z * Vector(xhint).dot(z)
    if x.length < 1e-6:
        x = Vector((0, 0, 1)) - z * z.z
    if x.length < 1e-6:
        x = Vector((1, 0, 0))
    x.normalize()
    y = z.cross(x)
    return Matrix(((x.x, y.x, z.x, origin.x),
                   (x.y, y.y, z.y, origin.y),
                   (x.z, y.z, z.z, origin.z),
                   (0.0, 0.0, 0.0, 1.0)))


def beam(p0, p1, xaxis):
    d = p1 - p0
    return frame(d, xaxis, p0), d.length


def cbox(center, size, cham=0.04, zdir=V(0, 0, 1), xhint=V(1, 0, 0)):
    sx, sy, sz = size
    hx, hy, hz = sx * 0.5, sy * 0.5, sz * 0.5
    c = max(1e-4, min(cham, hx * 0.45, hy * 0.45, hz * 0.45))
    M = frame(zdir, xhint, center)
    return solid(M, [(-hz, prof_cham(hx - c, hy - c, c * 0.7)),
                     (-hz + c, prof_cham(hx, hy, c)),
                     (hz - c, prof_cham(hx, hy, c)),
                     (hz, prof_cham(hx - c, hy - c, c * 0.7))])


def bbeam(p0, p1, s0, s1, xaxis=V(1, 0, 0), c=0.03):
    M, L = beam(p0, p1, xaxis)
    return solid(M, [(0.0, prof_rect(s0[0], s0[1], c)),
                     (L, prof_rect(s1[0], s1[1], c))])


def prism(p0, p1, r0, r1, n=12, xaxis=V(1, 0, 0), phase=0.0):
    M, L = beam(p0, p1, xaxis)
    return solid(M, [(0.0, prof_ngon(n, r0, phase)),
                     (L, prof_ngon(n, r1, phase))])


def sector(centre, x, hx, r_in, r_out, a0, a1, steps=9, tooth=0.0):
    """
    트러니언을 도는 <b>부채꼴 판</b>입니다. y-z 평면에서 도는 호이고, 각도는
    <b>총구 방향(-y)에서 위(+z)</b>로 잽니다.

    이것이 이 작업에서 가장 중요한 한 조각입니다. 유압 램을 달면 <b>실린더는 요크에,
    로드는 포에</b> 붙어야 하는데 둘이 다른 본이라 부앙할 때 반드시 어긋납니다.
    부채꼴 기어는 <b>통째로 포 쪽</b>이고 트러니언을 중심으로 돌므로, 어느 각도에서도
    거짓말이 없습니다. 브루탈리즘에도 유압보다 <b>깎아 놓은 호</b>가 맞습니다.

    <c>tooth</c> 를 주면 바깥 반지름이 단마다 오르내려 <b>이가 공짜로 생깁니다</b> -
    상자를 따로 얹으면 이 하나에 60 삼각형인데 여기서는 0 입니다.
    """
    secs = []
    for i in range(steps + 1):
        a = math.radians(a0 + (a1 - a0) * i / steps)
        ro = r_out + (tooth if i % 2 else 0.0)
        d = V(0.0, -math.cos(a), math.sin(a))
        p_in, p_out = centre + d * r_in, centre + d * ro
        secs.append([V(x - hx, p_in.y, p_in.z), V(x + hx, p_in.y, p_in.z),
                     V(x + hx, p_out.y, p_out.z), V(x - hx, p_out.y, p_out.z)])
    return loft(secs)


class Builder(object):
    """
    <c>tag</c> 가 간섭 검사를, <c>piece</c> 가 <b>오브젝트 나누기</b>를 가릅니다.

    <b>왜 나누는가.</b> 무장 하나를 통째로 용접하면 포신을 뒤로 밀 수도, 회전포를
    돌릴 수도 없습니다. 동적 애니메이션은 <b>움직일 조각</b>이 있어야 시작됩니다.
    몸이 이미 그렇게 나뉘어 있으므로 같은 방식을 씁니다.

    <c>tag</c> 는 <b>요크 속에 들어가 있어야 정상인 것</b>(트러니언 보스 · 약실 칼라 ·
    부채꼴 기어)을 "root" 로, 축 그 자체를 "axis" 로 두어 간섭 검사에서 뺍니다.
    그것까지 세면 레스트 포즈에서도 늘 겹쳤다고 나옵니다.
    """
    def __init__(self):
        self.parts = []
        self.tag = "body"
        self.piece = None
        self.pivots = {}

    def add(self, verts, faces, mat=MAT_CONCRETE, tag=None, piece=None):
        self.parts.append(dict(verts=verts, faces=faces, mat=mat,
                               tag=tag or self.tag,
                               piece=piece if piece is not None else self.piece))

    def pivot(self, piece, point):
        """
        조각이 <b>돌 자리</b>를 못 박습니다. 안 주면 제 바운딩 박스 중심입니다.

        매달린 갈고리처럼 <b>한쪽 끝에서 도는</b> 것은 중심이 답이 아닙니다 -
        무게중심을 축으로 삼으면 갈고리가 제자리에서 빙글 돕니다.
        """
        self.pivots[piece] = point

    def tagged(self, want):
        return [pt for pt in self.parts if pt["tag"] == want]

    def pieces(self):
        """조각 이름 -> 파츠 목록. <b>기본 조각(None)이 맨 앞</b>입니다."""
        order, out = [], {}
        for pt in self.parts:
            k = pt["piece"]
            if k not in out:
                out[k] = []
                order.append(k)
            out[k].append(pt)
        order.sort(key=lambda k: (k is not None, k or ""))
        return [(k, out[k]) for k in order]

    def bounds(self):
        vs = [v for pt in self.parts for v in pt["verts"]]
        lo = Vector([min(v[i] for v in vs) for i in range(3)])
        hi = Vector([max(v[i] for v in vs) for i in range(3)])
        return lo, hi


# ----------------------------------------------------------------------------
# 건포트 - 요크 쪽. 부앙하지 않고 <b>선회만</b> 하는 것들입니다.
# ----------------------------------------------------------------------------
def build_pod(b):
    """
    <b>탄약과 반동이 어디로 가는지</b>를 형상으로 말합니다.

    앞선 포드는 선회 링 · 트래버스 블록 · 요크 치크 · 핀, 이렇게 넷이었습니다.
    포를 <b>매달아 놓기만</b> 하고 먹이지도 붙잡지도 않는 모양이라, 4.2 m 짜리
    포신이 핀 하나에 걸린 것으로 읽혔습니다.

    ⚠ <b>트러니언 위로는 0.66 m 밖에 없습니다.</b> 섀시 밑면이 4.692 이고 축이
    3.78 입니다. 그래서 앞선 판의 <b>두꺼운 트래버스 블록은 있을 수 없습니다</b> -
    실측하면 포 약실이 그 블록을 레스트 포즈에서 이미 <b>0.56 m</b> 파고들고
    있었습니다. 여기서는 블록 대신 <b>얇은 요크 상판</b>을 쓰고, 덩치는 전부
    축 뒤 · 축 아래로 보냅니다.

    더한 것 셋은 전부 구조입니다.
      * <b>탄약 드럼</b> - 피벗 <b>뒤</b>에 놓아 포신의 저울추가 됩니다. 이 로봇에서
        유일하게 밝은 재질이 콘크리트이므로 드럼을 콘크리트로 싸면 어두운 포드
        한가운데에 밝은 덩어리가 생겨 <b>포드가 처음으로 눈에 걸립니다.</b>
      * <b>피니언 하우징</b> - 포 쪽의 부채꼴 기어와 물립니다. 짝이 있어야 기어가
        기어로 읽힙니다.
      * <b>드럼 행어</b> - 상판에서 드럼 마구리로 내려갑니다. 반동이 드럼을 지나
        선회 링으로 가는 길입니다.
    """
    D, P = GUN_DECK, GUN_PIVOT

    # 선회 링. 섀시 밑면에 물리고 <b>아래로는 요크 바닥까지만</b> 내려옵니다.
    b.add(*prism(D + V(0, 0, -0.02), V(0, D.y, YOKE_FLOOR), 0.62, 0.62, 16),
          mat=MAT_DARK)

    # 요크 상판. 링에서 뒤로 뻗어 치크와 행어를 함께 받습니다.
    b.add(*cbox(V(0, -0.86, YOKE_FLOOR + 0.08), (1.66, 1.94, 0.16)),
          mat=MAT_STEEL)

    # 탄약 드럼. 축이 x 라 옆에서 보면 <b>원</b>, 앞에서 보면 <b>가로 덩어리</b>입니다.
    b.add(*prism(V(-0.54, -0.10, 3.70), V(0.54, -0.10, 3.70), 0.62, 0.62, 12),
          mat=MAT_CONCRETE)
    for sx in (-1.0, 1.0):
        b.add(*prism(V(sx * 0.30, -0.10, 3.70), V(sx * 0.46, -0.10, 3.70),
                     0.65, 0.65, 12), mat=MAT_STEEL)           # 조임 밴드
        # ⚠ 마구리를 <b>드럼보다 크게</b> 만들면 안 됩니다. 1.14 각 상자로 덮었더니
        # 옆에서 볼 때 드럼이 통째로 가려져 <b>네모난 판</b>으로 보였습니다 -
        # 드럼을 넣은 이유(어두운 포드 한가운데의 밝은 원기둥)가 사라집니다.
        # 드럼보다 작은 어두운 원판이라야 축으로 읽히고 드럼이 드러납니다.
        b.add(*prism(V(sx * 0.54, -0.10, 3.70), V(sx * 0.66, -0.10, 3.70),
                     0.44, 0.40, 12), mat=MAT_DARK)             # 드럼 마구리
        b.add(*bbeam(V(sx * 0.62, -0.10, 4.34), V(sx * 0.62, -0.32, YOKE_FLOOR),
                     (0.09, 0.24), (0.09, 0.20)), mat=MAT_STEEL)  # 행어

    # 요크 치크. 상판에서 내려와 핀을 잡습니다.
    for sx in (-1.0, 1.0):
        b.add(*cbox(V(sx * CHEEK_X, -1.18, 4.04), (CHEEK_HX * 2.0, 0.84, 0.94)),
              mat=MAT_STEEL)
        # 피니언 하우징. 부채꼴 기어 바로 밑에서 물립니다.
        b.add(*cbox(V(sx * SECTOR_X, -0.98, 3.28), (0.28, 0.54, 0.54)),
              mat=MAT_STEEL)

    # 트러니언 핀. 치크 바깥까지 꿰고 나옵니다 - 꿰고 나와야 핀입니다.
    #
    # ⚠ <c>tag="axis"</c> 입니다. 이것은 <b>포가 도는 축 자체</b>라 방패판이 늘
    # 그 둘레를 지납니다. 검사에 넣으면 상자끼리 스치는 것을 간섭이라고 잡아
    # 부앙 한계가 실제보다 <b>훨씬 작게</b> 나옵니다.
    b.add(*prism(V(-0.86, P.y, P.z), V(0.86, P.y, P.z), 0.19, 0.19, 12,
                 xaxis=V(0, 1, 0)), mat=MAT_DARK, tag="axis")


# ----------------------------------------------------------------------------
# 무장 - 포 쪽. 여섯 개가 <b>같은 뿌리</b>를 씁니다.
# ----------------------------------------------------------------------------
def gun_root(b):
    """
    무장 여섯이 공유하는 <b>마운트 물림부</b>입니다. 늘 기본 조각에 들어갑니다.

    앞선 두 무장은 각자 다른 모양으로 마운트에 붙어 있었습니다. 그러면 무장을
    갈아 끼웠을 때 <b>물리는 방식까지 달라져</b>, 같은 기계가 아니라 다른 기계로
    보입니다. 뿌리를 공유하면 "이 기계는 이 자리에 무엇이든 문다" 가 됩니다.

    ⚠ 방패판은 <b>작아야</b> 합니다. 축 위로 0.66 m 밖에 없는데 판을 앞으로 0.7 m
    내밀고 세로로 키우면, 부앙하는 순간 그 모서리가 요크 상판을 뚫습니다.
    치크 <b>사이를</b> 지나가도록 폭도 잘라 둡니다.

    :returns: 무장 본체가 시작해야 하는 y (방패판 앞면)
    """
    P = GUN_PIVOT
    b.piece = None

    # 트러니언 보스. 치크 사이에 꼭 맞습니다.
    b.add(*prism(V(-BOSS_HX, P.y, P.z), V(BOSS_HX, P.y, P.z), 0.30, 0.30, 12,
                 xaxis=V(0, 1, 0)), mat=MAT_DARK, tag="root")

    # 약실 칼라. 보스와 방패판을 잇는 강철 토막입니다.
    b.add(*cbox(V(0, P.y - 0.28, P.z), (1.00, 0.42, 0.96)), mat=MAT_STEEL,
          tag="root")

    # 방패판. <b>이 로봇에서 콘크리트는 구조입니다</b> - 얇은 강판이 아니라
    # 두꺼운 슬래브라야 트러니언을 가리는 값을 합니다.
    b.add(*cbox(V(0, P.y - 0.56, P.z + 0.02), (MANTLET_HX * 2.0, 0.32, 0.86)),
          mat=MAT_CONCRETE)

    # 부채꼴 기어 둘. 치크 바깥, 피니언 하우징 위입니다.
    for sx in (-1.0, 1.0):
        b.add(*sector(P, sx * SECTOR_X, SECTOR_HX, 0.50, 0.70,
                      PITCH_RANGE[0] - 15.0, PITCH_RANGE[1] + 15.0,
                      steps=9, tooth=0.05), mat=MAT_STEEL, tag="root")

    return P.y - 0.72


def gun_heavy_cannon(b):
    """
    <b>한 줄.</b> 가장 길고 가장 단순한 실루엣입니다.

    조각 둘: 약실은 서 있고 <b>포신만 0.38 m 뒤로</b> 밀립니다.
    """
    y = gun_root(b)
    Z = GUN_PIVOT.z

    # ⚠ 약실 높이가 <b>고개를 드는 각을 정합니다.</b> 뒷면 위 모서리가 축에서
    # 0.72 m 앞 · h 위에 있으면 θ 만큼 들 때 0.72 sinθ + h cosθ 만큼 올라가는데,
    # 선회 링 밑까지 0.66 m 뿐입니다. h = 0.48 이 15° 의 값입니다.
    b.add(*cbox(V(0, y - 0.70, Z + 0.02), (1.30, 1.40, 0.96)), mat=MAT_CONCRETE)
    b.add(*cbox(V(0, y - 0.70, Z + 0.02), (1.38, 0.26, 1.04)), mat=MAT_STEEL)
    b.add(*cbox(V(0, y - 1.52, Z + 0.04), (0.86, 0.32, 0.86)), mat=MAT_STEEL)

    b.piece = "Barrel"
    b.add(*bbeam(V(0, y - 1.64, Z + 0.04), V(0, y - 3.26, Z + 0.04),
                 (0.33, 0.33), (0.26, 0.26)), mat=MAT_STEEL)
    b.add(*cbox(V(0, y - 2.40, Z + 0.04), (0.46, 0.22, 0.46)), mat=MAT_DARK)
    b.add(*cbox(V(0, y - 3.40, Z + 0.04), (0.68, 0.44, 0.68)), mat=MAT_DARK)
    b.piece = None

    return V(0, y - 3.68, Z + 0.04)


def gun_autocannon(b):
    """
    <b>두 줄.</b> 배 밑에 탄통이 붙어 아래로 두꺼워집니다.

    조각 셋: 두 포신이 <b>번갈아</b> 0.11 m 밀립니다. 그래서 좌우를 따로 냅니다.
    가운데 재킷은 서 있고 포신이 그 속을 지납니다.
    """
    y = gun_root(b)
    Z = GUN_PIVOT.z

    b.add(*cbox(V(0, y - 0.62, Z - 0.10), (1.26, 1.22, 0.86)), mat=MAT_CONCRETE)
    b.add(*cbox(V(0, y - 0.62, Z - 0.10), (1.34, 0.24, 0.94)), mat=MAT_STEEL)
    b.add(*cbox(V(0, y - 0.50, Z - 0.82), (1.06, 1.14, 0.56)), mat=MAT_STEEL)

    # 재킷. 포신이 이 속을 미끄러지므로 <b>기본 조각</b>에 둡니다.
    b.add(*cbox(V(0, y - 2.02, Z - 0.02), (0.90, 0.28, 0.44)), mat=MAT_DARK)

    # ⚠ 총구 마개를 <b>하나로 두면 안 됩니다.</b> 두 포신이 번갈아 밀리는데
    # 마개가 한 장이면 어느 쪽이 물러났는지 안 보입니다. 포신마다 답니다.
    for sx, side in ((-1.0, "BarrelL"), (1.0, "BarrelR")):
        b.piece = side
        b.add(*bbeam(V(sx * 0.27, y - 1.24, Z - 0.02),
                     V(sx * 0.27, y - 2.88, Z - 0.02),
                     (0.17, 0.17), (0.15, 0.15)), mat=MAT_STEEL)
        b.add(*cbox(V(sx * 0.27, y - 3.00, Z - 0.02), (0.40, 0.30, 0.40)),
              mat=MAT_DARK)
    b.piece = None

    return V(0, y - 3.20, Z - 0.02)


def gun_rotary(b):
    """
    <b>앞이 무거운 뭉치.</b> 짧은데 코가 굵어 다른 다섯과 안 헷갈립니다.

    조각 둘: 탄창은 서 있고 <b>로터가 돕니다.</b> 로터의 원점은 반드시
    <b>회전축 위</b>여야 합니다 - 어긋나면 포신이 원뿔을 그립니다. 하우징 · 포신 ·
    총구 링이 모두 축에 대해 대칭이므로 바운딩 박스 중심이 곧 축입니다.
    """
    y = gun_root(b)
    Z = GUN_PIVOT.z
    n = 6

    b.add(*prism(V(0, y - 0.10, Z - 0.06), V(0, y - 1.14, Z - 0.06),
                 0.54, 0.54, 12, xaxis=V(1, 0, 0)), mat=MAT_CONCRETE)
    b.add(*prism(V(0, y - 0.44, Z - 0.06), V(0, y - 0.62, Z - 0.06),
                 0.58, 0.58, 12, xaxis=V(1, 0, 0)), mat=MAT_STEEL)

    b.piece = "Rotor"
    b.add(*prism(V(0, y - 1.14, Z), V(0, y - 1.52, Z), 0.52, 0.48, 12,
                 xaxis=V(1, 0, 0)), mat=MAT_STEEL)

    for i in range(n):
        a = TAU * i / n
        off = V(math.cos(a) * 0.27, 0.0, math.sin(a) * 0.27)
        b.add(*bbeam(V(0, y - 1.50, Z) + off, V(0, y - 2.66, Z) + off,
                     (0.085, 0.085), (0.075, 0.075)), mat=MAT_STEEL)

    b.add(*prism(V(0, y - 2.62, Z), V(0, y - 2.80, Z), 0.40, 0.40, 12,
                 xaxis=V(1, 0, 0)), mat=MAT_DARK)
    b.piece = None

    return V(0, y - 2.96, Z)


def gun_missile_rack(b):
    """
    <b>가장 넓고 포신이 없습니다.</b> 다섯 중 유일하게 가로로 읽힙니다.

    칸을 여섯 개 따로 짜면 상자 하나에 60 삼각형이라 360 이 듭니다. 대신
    <b>콘크리트 한 덩어리에 어두운 판을 붙이고 강철 살로 나눕니다</b> - 같은
    "칸이 여섯" 을 3 분의 1 로 말합니다.

    조각 둘: 되밀리지 않는 유일한 무장이라 <b>덮개가 열리는 것이 곧 예고</b>입니다.
    """
    y = gun_root(b)
    Z = GUN_PIVOT.z

    b.add(*cbox(V(0, y - 0.24, Z - 0.08), (1.44, 0.48, 0.92)), mat=MAT_STEEL)
    b.add(*cbox(V(0, y - 1.16, Z - 0.10), (2.52, 1.44, 1.06)), mat=MAT_CONCRETE)

    # ⚠ 살이 어두운 판보다 <b>확실히 앞으로 나와야</b> 합니다. 0.10 m 만 내밀었더니
    # 그림자가 안 생겨 격자가 안 읽히고 <b>판때기 한 장</b>으로 보였습니다.
    # 칸이 여섯이라는 것이 이 무장의 전부이므로, 안 읽히면 미사일 랙이 아닙니다.
    b.add(*cbox(V(0, y - 1.74, Z - 0.10), (2.44, 0.34, 0.15)), mat=MAT_STEEL)
    for sx in (-1.0, 1.0):
        b.add(*cbox(V(sx * 0.41, y - 1.74, Z - 0.10), (0.15, 0.34, 0.98)),
              mat=MAT_STEEL)
        b.add(*cbox(V(sx * 1.24, y - 1.16, Z - 0.10), (0.18, 1.50, 1.10)),
              mat=MAT_STEEL)

    # 덮개. <b>위로 미끄러져</b> 여섯 칸이 드러납니다.
    b.piece = "Doors"
    b.add(*cbox(V(0, y - 1.84, Z - 0.10), (2.36, 0.16, 0.92)), mat=MAT_DARK)
    b.piece = None

    return V(0, y - 2.02, Z - 0.10)


def gun_siege(b):
    """
    <b>가장 굵고 가장 짧습니다.</b> 구멍 하나가 실루엣 전부입니다.

    처음에는 이 자리를 <b>박격포</b>로 잡았습니다 - 짧고 굵은 통이 35° 위를 보는
    것이라 다섯 중 유일하게 세로로 읽혔습니다. <b>그런데 이 마운트는 고개를 못
    듭니다.</b> 재 보니 위로 15° 가 한계이고(선회 링이 막습니다), 애초에 키가
    5.6 m 인 기계가 땅 위의 것을 겨누므로 필요한 것은 <b>내리는 각</b>입니다.

    대신 <b>구경</b>으로 다르게 만듭니다.

    조각 셋: 포신이 0.52 m 밀릴 때 <b>실린더는 그 절반만</b> 압축됩니다.
    같이 밀리면 실린더가 아니라 그냥 막대입니다.
    """
    y = gun_root(b)
    Z = GUN_PIVOT.z

    b.add(*cbox(V(0, y - 0.60, Z - 0.02), (1.34, 1.20, 0.94)), mat=MAT_CONCRETE)
    b.add(*cbox(V(0, y - 0.60, Z - 0.02), (1.42, 0.26, 1.02)), mat=MAT_STEEL)

    # 반동 실린더 둘. 짧은 포신에 큰 구경이면 <b>반동이 문제</b>이고, 그것을
    # 형상으로 말하는 것이 이 무장의 성격입니다.
    b.piece = "Cylinders"
    for sx in (-1.0, 1.0):
        b.add(*bbeam(V(sx * 0.52, y - 1.18, Z + 0.26),
                     V(sx * 0.52, y - 2.10, Z + 0.26),
                     (0.13, 0.13), (0.11, 0.11)), mat=MAT_STEEL)

    b.piece = "Barrel"
    b.add(*prism(V(0, y - 1.16, Z - 0.02), V(0, y - 2.22, Z - 0.02),
                 0.52, 0.50, 12), mat=MAT_STEEL)
    b.add(*prism(V(0, y - 1.52, Z - 0.02), V(0, y - 1.70, Z - 0.02),
                 0.56, 0.56, 12), mat=MAT_DARK)
    b.add(*prism(V(0, y - 2.18, Z - 0.02), V(0, y - 2.40, Z - 0.02),
                 0.58, 0.58, 12), mat=MAT_DARK)
    b.piece = None

    return V(0, y - 2.54, Z - 0.02)


def gun_tow_hook(b):
    """
    <b>무장이 아닙니다.</b> 죽은 차를 가지러 오는 기계의 팔입니다.

    로봇_기획.md 가 견인기를 "스트라이더의 총 자리에 갈고리를 답니다" 로 잡아
    두었습니다. 마운트가 이미 교체식이므로 여기서 같이 냅니다.

    실루엣은 <b>아래로 늘어집니다.</b> 다섯 무장이 전부 앞으로 뻗는 것과 다릅니다.

    조각 셋. 여섯 중 <b>유일하게 걷는 내내 움직입니다</b> - 다른 다섯은 쏠 때만
    움직입니다. 그래서 갈고리의 원점은 <b>붐 끝의 줄이 걸리는 자리</b>여야 합니다.
    무게중심을 축으로 삼으면 매달린 것이 제자리에서 빙글 돕니다.
    """
    y = gun_root(b)
    Z = GUN_PIVOT.z

    b.piece = "Drum"
    b.add(*prism(V(-0.52, y - 0.46, Z + 0.02), V(0.52, y - 0.46, Z + 0.02),
                 0.54, 0.54, 12), mat=MAT_CONCRETE)
    b.piece = None

    for sx in (-1.0, 1.0):
        b.add(*cbox(V(sx * 0.58, y - 0.46, Z + 0.02), (0.14, 1.02, 1.02)),
              mat=MAT_STEEL)
    b.add(*bbeam(V(0, y - 0.86, Z - 0.06), V(0, y - 2.44, Z - 0.66),
                 (0.24, 0.30), (0.19, 0.24)), mat=MAT_STEEL)
    b.add(*cbox(V(0, y - 1.60, Z - 0.42), (0.44, 0.22, 0.44)), mat=MAT_DARK)
    b.add(*cbox(V(0, y - 2.52, Z - 0.72), (0.50, 0.36, 0.50)), mat=MAT_STEEL)

    # 갈고리. 붐 끝에서 <b>곧게 내려옵니다</b> - 매달린 것은 늘 수직입니다.
    hang = V(0, y - 2.52, Z - 0.86)
    b.piece = "Hook"
    b.add(*bbeam(V(0, y - 2.52, Z - 0.92), V(0, y - 2.52, Z - 1.62),
                 (0.11, 0.11), (0.13, 0.13)), mat=MAT_DARK)
    b.add(*cbox(V(0, y - 2.44, Z - 1.72), (0.30, 0.46, 0.24)), mat=MAT_DARK)
    b.piece = None
    b.pivot("Hook", hang)

    return V(0, y - 2.52, Z - 1.86)


# 이름 · 빌더 · 기본 장착 여부. <b>첫 줄이 기본 무장</b>입니다.
LOADOUTS = (
    ("SM_Gun_HeavyCannon", gun_heavy_cannon, True),
    ("SM_Gun_Autocannon", gun_autocannon, False),
    ("SM_Gun_Rotary", gun_rotary, False),
    ("SM_Gun_MissileRack", gun_missile_rack, False),
    ("SM_Gun_Siege", gun_siege, False),
    ("SM_Gun_TowHook", gun_tow_hook, False),
)



# ----------------------------------------------------------------------------
# 간섭 검사 - <b>부앙 끝까지 돌려 보고</b> 겹치는지 잽니다.
# ----------------------------------------------------------------------------
def _box(verts):
    return (Vector([min(v[i] for v in verts) for i in range(3)]),
            Vector([max(v[i] for v in verts) for i in range(3)]))


def _hull(pts):
    """2D 볼록 껍질(Andrew monotone chain)입니다."""
    pts = sorted(set((round(a, 6), round(b, 6)) for a, b in pts))
    if len(pts) < 3:
        return pts

    def half(seq):
        out = []
        for q in seq:
            while len(out) >= 2:
                (x1, y1), (x2, y2) = out[-2], out[-1]
                if (x2 - x1) * (q[1] - y1) - (y2 - y1) * (q[0] - x1) > 0:
                    break
                out.pop()
            out.append(q)
        return out[:-1]

    return half(pts) + half(list(reversed(pts)))


def _sat(a, b):
    """
    두 볼록 다각형이 겹치는 <b>가장 얕은 깊이</b>입니다. 안 겹치면 0 입니다.

    ⚠ <b>축 정렬 상자로 재면 안 됩니다.</b> 처음에 그렇게 쟀더니 4 m 짜리 포신을
    11° 만 기울여도 그 상자가 세로로 0.9 m 커져서, <b>닿지도 않은 선회 링에
    닿았다</b>고 나왔습니다. 부앙 한계가 실제의 절반으로 나왔고 그 거짓 숫자에
    맞춰 형상을 깎을 뻔했습니다. 재는 법이 틀리면 <b>고칠 곳도 틀립니다.</b>

    회전이 x 축 하나뿐이라 x 는 그대로 비교하고 여기서는 y-z 평면만 봅니다.
    """
    depth = None
    for poly in (a, b):
        n = len(poly)
        for i in range(n):
            (x1, y1), (x2, y2) = poly[i], poly[(i + 1) % n]
            ax, ay = -(y2 - y1), (x2 - x1)
            L = math.hypot(ax, ay)
            if L < 1e-9:
                continue
            ax, ay = ax / L, ay / L
            pa = [ax * x + ay * y for x, y in a]
            pb = [ax * x + ay * y for x, y in b]
            gap = min(max(pa), max(pb)) - max(min(pa), min(pb))
            if gap <= 0.0:
                return 0.0
            if depth is None or gap < depth:
                depth = gap
    return depth or 0.0


def _pen(verts, blo, bhi):
    """돌아간 부재 하나가 상자 하나를 <b>얼마나 파고들었는지</b>입니다."""
    lo = min(v[0] for v in verts)
    hi = max(v[0] for v in verts)
    x = min(hi, bhi[0]) - max(lo, blo[0])
    if x <= 0.0:
        return 0.0
    return min(x, _sat(_hull([(v[1], v[2]) for v in verts]),
                       [(blo[1], blo[2]), (bhi[1], blo[2]),
                        (bhi[1], bhi[2]), (blo[1], bhi[2])]))


def swing_clash(swing, fixed, steps=24):
    """
    포 쪽을 부앙 범위 끝까지 돌려 가며 요크 쪽을 <b>얼마나 파고드는지</b> 잽니다.

    이 결함은 레스트 포즈에서 안 보입니다 - 고개를 든 순간에만 파고들기 때문에
    눈으로는 못 잡습니다. 그래서 <b>돌려 보고</b> 잽니다.

    맞물려 있어야 정상인 것(<c>tag="root"</c>)과 축 그 자체(<c>"axis"</c>)는
    부르는 쪽이 미리 빼고 넘깁니다.

    :returns: (최악 각도, 파고든 깊이 m)
    """
    P = GUN_PIVOT
    boxes = [_box(pt["verts"]) for pt in fixed]
    worst = (0.0, 0.0)

    for i in range(steps + 1):
        a = math.radians(PITCH_RANGE[0]
                         + (PITCH_RANGE[1] - PITCH_RANGE[0]) * i / steps)
        R = Matrix.Rotation(a, 4, 'X')
        for pt in swing:
            vs = [P + R @ (v - P) for v in pt["verts"]]
            for blo, bhi in boxes:
                deep = _pen(vs, blo, bhi)
                if deep > worst[1]:
                    worst = (round(math.degrees(a), 1), round(deep, 3))

    return worst


def yaw_clash(parts, blockers, span=120.0, steps=48):
    """
    포드를 <b>선회 끝까지 돌려 보고</b> 몸에 닿는지 봅니다.

    탄약 드럼을 축 <b>뒤로</b> 1.9 m 내밀었으므로 이 검사가 필요해졌습니다 -
    앞선 포드는 축 뒤로 0.59 m 뿐이라 어디로 돌려도 몸 밑을 못 벗어났습니다.
    선회는 ±120° 이고, 90° 부근에서 드럼이 <b>옆구리로</b> 나갑니다.

    :returns: (최악 각도, 파고든 깊이 m)
    """
    C = GUN_DECK
    worst = (0.0, 0.0)

    for i in range(steps + 1):
        a = math.radians(-span + 2.0 * span * i / steps)
        R = Matrix.Rotation(a, 4, 'Z')
        for pt in parts:
            vs = [C + R @ (v - C) for v in pt["verts"]]
            lo, hi = _box(vs)
            for b_lo, b_hi in blockers:
                deep = min(min(hi[k], b_hi[k]) - max(lo[k], b_lo[k])
                           for k in range(3))
                if deep > worst[1]:
                    worst = (round(math.degrees(a), 1), round(deep, 3))

    return worst


def body_boxes(names=("SM_Strider_Head", "SM_Strider_Neck",
                      "SM_Strider_HullCore", "SM_Strider_Chassis")):
    """포가 올라갈 때 <b>앞을 막고 있는</b> 몸 파츠의 상자입니다."""
    out = []
    for n in names:
        o = bpy.data.objects.get(n)
        if o is None:
            continue
        out.append(_box([o.matrix_world @ v.co for v in o.data.vertices]))
    return out


def pitch_limit(swing, blockers, span=(-45.0, 80.0), step=0.5):
    """
    포를 올리고 내리다 <b>어디서 제 몸에 닿는지</b> 찾습니다.

    ⚠ 리그 json 은 부앙을 <c>-25 ~ +60</c> 으로 적어 두었는데, 재 보면 그 값을
    <b>형상이 허락하지 않습니다</b> - 센서 머리가 포신 바로 앞 위(z 4.99~6.61,
    y -2.64~-4.71)에 있어서 조금만 들면 포신이 그 속을 지납니다. 조준 코드는
    json 을 믿으므로, 적힌 숫자가 형상보다 크면 게임에서 <b>포신이 제 머리를
    뚫습니다.</b> 그래서 숫자를 형상에서 뽑습니다.

    :returns: (내릴 수 있는 각, 올릴 수 있는 각)
    """
    P = GUN_PIVOT

    def hits(deg):
        R = Matrix.Rotation(math.radians(deg), 4, 'X')
        for pt in swing:
            vs = [P + R @ (v - P) for v in pt["verts"]]
            for b_lo, b_hi in blockers:
                if _pen(vs, b_lo, b_hi) > 0.0:
                    return True
        return False

    out = []
    for sign in (-1.0, 1.0):
        a = 0.0
        while span[0] <= a + sign * step <= span[1]:
            if hits(a + sign * step):
                break
            a += sign * step
        out.append(round(a, 1))

    return tuple(out)


# ----------------------------------------------------------------------------
# 씬에 올리기
# ----------------------------------------------------------------------------
def materials():
    out = []
    for name in MAT_NAMES:
        m = bpy.data.materials.get(name)
        assert m is not None, "%s 가 없습니다 - Strider.blend 를 연 것이 맞습니까" % name
        out.append(m)
    return out


def make_object(name, parts, mats, offset=None, center=False):
    verts, faces, fmat = [], [], []
    for pt in parts:
        off = len(verts)
        verts.extend(pt["verts"])
        for f in pt["faces"]:
            faces.append(tuple(i + off for i in f))
            fmat.append(pt["mat"])

    if center:
        lo = Vector([min(v[i] for v in verts) for i in range(3)])
        hi = Vector([max(v[i] for v in verts) for i in range(3)])
        offset = (lo + hi) * 0.5
    loc = offset if offset is not None else Vector((0.0, 0.0, 0.0))

    me = bpy.data.meshes.new(name)
    me.from_pydata([tuple(v - loc) for v in verts], [], faces)
    for i, mi in enumerate(fmat):
        if i < len(me.polygons):
            me.polygons[i].material_index = mi
    me.validate(verbose=False)
    for m in mats:
        me.materials.append(m)

    bm = bmesh.new()
    bm.from_mesh(me)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    bm.to_mesh(me)
    bm.free()

    obj = bpy.data.objects.new(name, me)
    bpy.context.scene.collection.objects.link(obj)
    obj.matrix_world = Matrix.Translation(loc)
    return obj


def bone_parent(obj, rig, bone_name, world=None):
    keep = Matrix.Translation(world) if world else obj.matrix_world.copy()
    obj.parent = rig
    obj.parent_type = 'BONE'
    obj.parent_bone = bone_name
    bpy.context.view_layer.update()
    obj.matrix_world = keep


def uv_unwrap(obj):
    """
    ⚠ <c>bpy.ops.object.mode_set</c> 는 <b>MCP 애드온에서 그냥 부르면 실패합니다</b> -
    애드온의 컨텍스트에는 활성 오브젝트가 없어 poll 이 떨어집니다. 배치모드에서는
    되기 때문에 애드온으로 돌릴 때만 조용히 깨집니다. <c>temp_override</c> 로
    컨텍스트를 직접 만들어 넘깁니다.
    """
    if not obj.data.uv_layers:
        obj.data.uv_layers.new(name="UVMap")

    view = bpy.context.view_layer
    bpy.ops.object.select_all(action='DESELECT')
    view.objects.active = obj
    obj.select_set(True)

    ctx = dict(active_object=obj, object=obj, selected_objects=[obj],
               selected_editable_objects=[obj], view_layer=view)

    # <c>bpy.ops.mesh.*</c> 는 오브젝트만으로는 안 되고 <b>3D 뷰포트</b>가 있어야
    # poll 을 통과합니다. 애드온에서 부를 때 화면이 어디를 보고 있느냐에 따라
    # 되기도 안 되기도 해서, 창을 뒤져 하나 찾아 붙입니다.
    for win in bpy.context.window_manager.windows:
        area = next((a for a in win.screen.areas if a.type == 'VIEW_3D'), None)
        if area is None:
            continue
        region = next((r for r in area.regions if r.type == 'WINDOW'), None)
        ctx.update(window=win, screen=win.screen, area=area, region=region,
                   space_data=area.spaces.active)
        break

    with bpy.context.temp_override(**ctx):
        bpy.ops.object.mode_set(mode='EDIT')
        bpy.ops.mesh.select_all(action='SELECT')
        bpy.ops.uv.smart_project(angle_limit=math.radians(66.0),
                                 island_margin=0.02)
        bpy.ops.object.mode_set(mode='OBJECT')

    obj.select_set(False)


def clear_old():
    """
    <b>총 관련 오브젝트만</b> 지웁니다. 몸 15 개는 이름으로 걸러 남깁니다.

    지울 것을 이름으로 고르는 대신 <b>남길 것을 이름으로 고르면</b> 실수했을 때
    쪽이 안전합니다 - 못 지우고 남는 것은 눈에 띄지만, 잘못 지운 몸은 blend 를
    다시 받기 전에는 안 돌아옵니다.
    """
    gone = []
    for o in list(bpy.data.objects):
        keep = (o.type == 'ARMATURE'
                or (o.name.startswith("SM_Strider_")
                    and o.name != "SM_Strider_GunMount"))
        if keep:
            continue
        gone.append(o.name)
        bpy.data.objects.remove(o, do_unlink=True)
    for me in list(bpy.data.meshes):
        if me.users == 0:
            bpy.data.meshes.remove(me)
    return gone


def build():
    """포드와 무장 여섯을 짓고 리그에 물립니다."""
    rig = bpy.data.objects[RIG_NAME]
    mats = materials()
    removed = clear_old()

    pod = Builder()
    build_pod(pod)
    mount = make_object("SM_Strider_GunMount", pod.parts, mats, center=True)
    bone_parent(mount, rig, "B_GunYaw")

    blockers = body_boxes()
    pod_boxes = [_box(pt["verts"]) for pt in pod.tagged("body")]

    made, report = [], []
    for name, fn, default in LOADOUTS:
        g = Builder()
        muzzle = fn(g)

        # <b>조각마다 오브젝트 하나</b>입니다. 기본 조각(None)이 무장의 이름을 갖고,
        # 나머지는 <c>이름_조각</c> 입니다. 유니티가 이 이름으로 찾습니다.
        #
        # ⚠ 원점이 곧 <b>도는 자리</b>입니다. 안 정하면 제 바운딩 박스 중심인데,
        # 매달린 갈고리는 그러면 제자리에서 빙글 돕니다. <c>b.pivot()</c> 이
        # 그런 조각의 자리를 못 박습니다.
        cut = []
        for piece, parts in g.pieces():
            obj_name = name if piece is None else name + "_" + piece
            if piece is None:
                obj = make_object(obj_name, parts, mats, offset=GUN_PIVOT)
            elif piece in g.pivots:
                obj = make_object(obj_name, parts, mats, offset=g.pivots[piece])
            else:
                obj = make_object(obj_name, parts, mats, center=True)

            bone_parent(obj, rig, "B_GunPitch")
            obj.hide_set(not default)
            obj.hide_render = not default
            cut.append(obj)
            made.append(obj)

        e = bpy.data.objects.new("SKT_Muzzle_" + name.split("_")[-1], None)
        e.empty_display_type = 'SINGLE_ARROW'
        e.empty_display_size = 0.5
        bpy.context.scene.collection.objects.link(e)
        e.parent = cut[0]
        bpy.context.view_layer.update()
        e.matrix_world = Matrix.Translation(muzzle)

        lo, hi = g.bounds()
        angle, deep = swing_clash(g.tagged("body"), pod.tagged("body"))
        limit = pitch_limit(g.tagged("body"), blockers + pod_boxes)
        report.append(dict(
            name=name, default=default,
            size=[round(hi[i] - lo[i], 2) for i in range(3)],
            reach=round(GUN_PIVOT.y - lo.y, 2),
            behind=round(hi.y - GUN_PIVOT.y, 2),
            drop=round(GUN_PIVOT.z - lo.z, 2),
            rise=round(hi.z - GUN_PIVOT.z, 2),
            tris=sum(len(f) - 2 for pt in g.parts for f in pt["faces"]),
            pieces=[o.name for o in cut],
            clash=[angle, deep], limit=list(limit)))

    skt = bpy.data.objects.new("SKT_GunPort", None)
    skt.empty_display_type = 'ARROWS'
    skt.empty_display_size = 0.6
    bpy.context.scene.collection.objects.link(skt)
    bone_parent(skt, rig, "B_GunPitch", world=GUN_PIVOT)

    bpy.context.view_layer.update()

    lo, hi = pod.bounds()
    swept = yaw_clash(pod.parts, body_boxes(
        ("SM_Strider_Chassis", "SM_Strider_HullCore",
         "SM_Strider_Femur_L", "SM_Strider_Femur_R", "SM_Strider_Femur_B",
         "SM_Strider_Tibia_L", "SM_Strider_Tibia_R", "SM_Strider_Tibia_B")))

    return dict(
        removed=removed,
        pod=dict(size=[round(hi[i] - lo[i], 2) for i in range(3)],
                 lo=[round(v, 2) for v in lo], hi=[round(v, 2) for v in hi],
                 behind=round(hi.y - GUN_PIVOT.y, 2),
                 yaw_clash=list(swept),
                 tris=sum(len(f) - 2 for pt in pod.parts for f in pt["faces"])),
        guns=report)


def export():
    """UV 를 깔고 blend 를 저장한 뒤 FBX 를 두 프로젝트로 내보냅니다."""
    for o in bpy.data.objects:
        if o.type == 'MESH':
            for m in [m for m in o.modifiers if m.type == 'BEVEL']:
                o.modifiers.remove(m)
    assert not any(m.type == 'BEVEL' for o in bpy.data.objects for m in o.modifiers)

    hidden = [o for o in bpy.data.objects if o.hide_get()]
    for o in hidden:
        o.hide_set(False)

    for o in [m for m in bpy.data.objects if m.type == 'MESH']:
        uv_unwrap(o)

    bpy.ops.wm.save_as_mainfile(filepath=BLEND_PATH)

    # ⚠ <c>use_selection=True</c> 는 <b>애드온에서 터집니다</b> - FBX 익스포터가
    # <c>context.selected_objects</c> 를 읽는데 애드온 컨텍스트에는 그 항목이
    # 없습니다. 배치모드에서는 되기 때문에 애드온으로 돌릴 때만 깨집니다.
    #
    # 어차피 <b>씬에 있는 것을 전부</b> 내보내므로 선택을 쓸 이유가 없습니다.
    # <c>object_types</c> 가 카메라와 라이트를 걸러 줍니다.
    written = []
    for d, bake in FBX_DIRS:
        os.makedirs(d, exist_ok=True)
        path = os.path.join(d, "SM_Strider.fbx").replace("\\", "/")
        bpy.ops.export_scene.fbx(
            filepath=path, use_selection=False, bake_space_transform=bake,
            apply_unit_scale=True,
            apply_scale_options='FBX_SCALE_ALL',
            object_types={'ARMATURE', 'MESH', 'EMPTY'},
            use_mesh_modifiers=True, mesh_smooth_type='FACE',
            add_leaf_bones=False, primary_bone_axis='Y', secondary_bone_axis='X',
            armature_nodetype='NULL', bake_anim=False, path_mode='COPY',
            axis_forward='-Z', axis_up='Y')
        written.append((path, os.path.getsize(path)))

    for o in hidden:
        o.hide_set(True)
    bpy.ops.wm.save_as_mainfile(filepath=BLEND_PATH)
    return written


def run():
    if not bpy.data.objects.get(RIG_NAME):
        bpy.ops.wm.open_mainfile(filepath=BLEND_PATH)
    out = build()
    out["fbx"] = export()
    return out


if __name__ == "__main__":
    import json
    print("###JSON###" + json.dumps(run()))
