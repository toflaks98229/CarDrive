# -*- coding: utf-8 -*-
"""
Find z-fighting risks in the generated meshes: coplanar faces that overlap.

WHY THIS EXISTS
  This whole art direction is made of boxes stacked on boxes. Two boxes whose
  faces land on the SAME plane and overlap will flicker - the depth buffer
  cannot decide which is in front, and the result shimmers as the camera moves.
  It is invisible in a still render and obvious in motion, which is the worst
  possible way for a defect to behave.

  It already happened once: the Citadel's infill panels sat exactly on the dark
  backing plane and showed a diagonal seam through every filled bay. That was
  caught by eye. This finds the rest by measurement.

HOW
  Two faces can only fight if they lie on the same plane. So bucket every face
  by (dominant axis, offset rounded to the tolerance) and only compare within a
  bucket - otherwise it is a million-pair problem on the Citadel alone.

  Inside a bucket, two faces fight if their footprints overlap. Every face here
  is an axis-aligned rectangle (the ramp's slopes are the one exception, and a
  sloped face is not axis-aligned so it lands in no bucket), so an AABB overlap
  test in the plane is exact.

Run:
    blender -b --python zfight_audit.py
"""
import json
import os
import sys
from collections import OrderedDict, defaultdict

import bpy

HERE = os.path.dirname(os.path.abspath(__file__))
if HERE not in sys.path:
    sys.path.insert(0, HERE)

# 같은 평면으로 볼 거리(m)입니다. 콘크리트 판 두께가 0.16 m 이므로 그보다 훨씬
# 작아야 하고, 실제로 깜빡이는 것은 밀리미터 단위입니다.
EPS = 0.004

# 겹침으로 볼 최소 넓이(㎡)입니다. 모서리끼리 스치는 것은 깜빡이지 않습니다.
MIN_AREA = 0.05

# <b>새 생성기를 만들면 여기에 넣으십시오.</b> 실내가 빠져 있어서 가게(SM_Room_Mart)의
# 벽 모서리 겹침이 오래 안 잡혔습니다 - 감사에 없는 것은 없는 것이 아니라 안 보는 것입니다.
MODELS = [
    ("Megastructure", "build_megastructure"),
    ("Building", "build_buildings"),
    ("Interior", "build_interiors"),
]


def faces_of(obj):
    """면마다 (축, 평면 위치, 2D 사각형)입니다. 축에 정렬되지 않은 면은 건너뜁니다."""
    mesh = obj.data
    out = []

    for poly in mesh.polygons:
        n = poly.normal
        axis = max(range(3), key=lambda i: abs(n[i]))

        # 축 정렬이 아니면 같은 평면에 겹칠 일이 거의 없고, 있어도 사각형이 아니라
        # AABB 로 판정할 수 없습니다. 경사로의 빗면이 여기 걸립니다.
        if abs(n[axis]) < 0.999:
            continue

        pts = [mesh.vertices[i].co for i in poly.vertices]
        other = [i for i in range(3) if i != axis]

        lo = [min(p[i] for p in pts) for i in other]
        hi = [max(p[i] for p in pts) for i in other]

        # <b>보는 방향까지 기억합니다.</b> 상자를 쌓으면 위 상자의 아랫면과 아래
        # 상자의 윗면이 같은 평면에 오는데, 서로 반대를 보므로 어느 쪽에서 보든
        # 하나는 뒷면이라 걷힙니다 - 깜빡이지 않습니다. 방향을 안 보면 그런 쌍이
        # 전부 결함으로 잡혀 실제로 143,732 m2 가 나왔습니다. 거의 전부 허위였습니다.
        out.append((axis, 1 if n[axis] > 0 else -1, pts[0][axis], lo, hi))

    return out


def preset_of(name):
    """
    파츠 이름에서 <b>프리셋 이름</b>을 뽑습니다.

    프리셋 하나가 오브젝트 여럿으로 쪼개진 뒤로, 오브젝트마다 따로 보면 <b>파츠
    사이에 걸친 겹침을 통째로 놓칩니다.</b> 게임에서는 같이 그려지므로 같이 깜빡이는데
    감사만 못 보는 것이라, 나누는 방식을 바꿀 때마다 수치가 이유 없이 오르내렸습니다.
    """
    bits = name.split("_")

    if len(bits) >= 4 and bits[1] == "Mega":
        return "_".join(bits[:3])

    return name


def audit(faces, keep_buried=False, seam=False, under=()):
    """
    같은 평면에서 <b>같은 쪽을 보며 겹치는</b> 면 쌍을 찾습니다.

    <b>파묻힌 쌍은 셉니다만 세지 않습니다.</b> 두 상자를 맞대면 맞댄 자리에 양쪽
    면이 다 생기는데, 그중 <b>덩어리 안쪽을 보는</b> 면들은 반대쪽을 보는 면에
    가려 절대 그려지지 않습니다. 시타델의 껍질이 정확히 이 모양입니다 - 세로 살과
    가로 띠가 벽면에서 시작해 바깥으로 나오므로, 안쪽 면 둘이 같은 평면에서 겹치지만
    그 앞을 벽이 막고 있습니다. 이 허위 때문에 1,217쌍이 잡혀 <b>진짜가 묻혔습니다.</b>

    판정은 간단합니다 - 겹친 사각형이 <b>반대쪽을 보는 면 하나에 통째로 들어가면</b>
    그 자리는 두 고체가 맞댄 속이므로 보이지 않습니다.

    <c>under</c> 는 <b>가려 주기만 하는 면</b>입니다 - 소켓의 데크처럼 프리셋
    밑에 늘 깔려 있지만 다른 메시라 이 무리에 안 들어오는 것들입니다. 결함으로는
    안 세고 파묻힘 판정에만 씁니다.

    <c>seam</c> 은 <b>이음매 평면</b>을 빼라는 뜻이고, 스파인 프리셋에만 씁니다.
    프리셋은 x 로 줄줄이 맞물리므로 양 끝의 단면은 <b>옆 프리셋 속</b>입니다 -
    데크 마구리와 노면 마구리가 거기서 같은 평면을 쓰지만 게임에서는 둘 다
    안 보입니다. 집과 실내는 혼자 서 있으므로 이 규칙을 주면 안 됩니다.
    """
    buckets = defaultdict(list)
    opposite = defaultdict(list)

    for axis, facing, offset, lo, hi in faces:
        key = round(offset / EPS)
        buckets[(axis, facing, key)].append((lo, hi))
        opposite[(axis, -facing, key)].append((lo, hi))

    for axis, facing, offset, lo, hi in under:
        opposite[(axis, -facing, round(offset / EPS))].append((lo, hi))

    ends = [f[2] for f in faces if f[0] == 0]
    joint = (min(ends), max(ends)) if seam and ends else None

    def buried(axis, facing, key, lo, hi):
        for o_lo, o_hi in opposite.get((axis, facing, key), ()):
            if (o_lo[0] <= lo[0] + EPS and o_hi[0] >= hi[0] - EPS and
                    o_lo[1] <= lo[1] + EPS and o_hi[1] >= hi[1] - EPS):
                return True
        return False

    hits = []

    for (axis, facing, key), rects in buckets.items():
        if len(rects) < 2:
            continue

        # 이음매 바깥을 보는 면입니다. 옆 프리셋이 거기 붙습니다.
        if joint is not None and axis == 0:
            if abs(key * EPS - joint[facing > 0]) < EPS:
                continue

        for i in range(len(rects)):
            for j in range(i + 1, len(rects)):
                a_lo, a_hi = rects[i]
                b_lo, b_hi = rects[j]

                w = min(a_hi[0], b_hi[0]) - max(a_lo[0], b_lo[0])
                h = min(a_hi[1], b_hi[1]) - max(a_lo[1], b_lo[1])

                if w <= 0.0 or h <= 0.0 or w * h < MIN_AREA:
                    continue

                o_lo = (max(a_lo[0], b_lo[0]), max(a_lo[1], b_lo[1]))
                o_hi = (min(a_hi[0], b_hi[0]), min(a_hi[1], b_hi[1]))

                if not keep_buried and buried(axis, facing, key, o_lo, o_hi):
                    continue

                hits.append(dict(axis="xyz"[axis], facing=facing,
                                 at=round(key * EPS, 3), area=round(w * h, 2),
                                 # 어느 부재인지 알아보려면 두 사각형의 크기가
                                 # 이름보다 낫습니다. 상자 언어에서는 치수가 곧 정체입니다.
                                 a=[round(a_hi[0] - a_lo[0], 2), round(a_hi[1] - a_lo[1], 2)],
                                 b=[round(b_hi[0] - b_lo[0], 2), round(b_hi[1] - b_lo[1], 2)]))

    return hits


def socket_floor(faces, bay):
    """
    소켓에서 <b>스파인 전체에 이어지는 면</b>만 남깁니다.

    소켓은 한 베이짜리 메시 하나를 베이마다 되풀이해 깔아 놓은 것입니다. 그래서
    한 베이를 <b>꽉 채우는</b> 면 - 데크 윗면, 노면, 밑판 - 은 스파인 어디에나
    있고, 프리셋의 부재는 그 위에 얹힙니다. 난간 밑동과 갤러리 기둥 밑면이
    <c>z = 35.2</c> 에서 같은 평면을 쓰지만 <b>둘 다 데크에 눌려</b> 안 보입니다.

    베이를 다 안 채우는 것(기둥, 다리)은 <b>버립니다.</b> 그것을 늘여 놓으면
    있지도 않은 자리를 가려 준다고 우겨 진짜를 놓칩니다.
    """
    out = []

    for axis, facing, offset, lo, hi in faces:
        # x 평면은 이음매 규칙이 봅니다. 여기서는 x 로 늘일 면만 봅니다.
        if axis == 0:
            continue

        if lo[0] > -bay * 0.5 + EPS or hi[0] < bay * 0.5 - EPS:
            continue

        out.append((axis, facing, offset, (-1e6, lo[1]), (1e6, hi[1])))

    return out


def main():
    report = []

    for folder, module in MODELS:
        mod = __import__(module)
        blend = mod.BLEND_PATH

        if not os.path.exists(blend):
            continue

        bpy.ops.wm.open_mainfile(filepath=blend)

        seen = set()
        groups = OrderedDict()

        for obj in bpy.data.objects:
            if obj.type != 'MESH' or obj.data.name in seen:
                continue

            seen.add(obj.data.name)
            key = preset_of(obj.name)
            groups.setdefault(key, []).extend(faces_of(obj))

        spine = folder == "Megastructure"
        socket = (socket_floor(groups.get("SM_Mega_Core", ()), mod.SOCKET["bay"])
                  if spine else ())

        for key, faces in groups.items():
            hits = audit(faces, seam=spine,
                         under=() if key == "SM_Mega_Core" else socket)

            report.append(dict(
                name=key, faces=len(faces),
                pairs=len(hits),
                area=round(sum(h["area"] for h in hits), 1),
                worst=sorted(hits, key=lambda h: -h["area"])[:8]))

    return report


if __name__ == "__main__":
    print("###JSON###" + json.dumps(main()))
