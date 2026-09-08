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

MODELS = [
    ("Megastructure", "build_megastructure"),
    ("Building", "build_buildings"),
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


def audit(faces):
    buckets = defaultdict(list)

    for axis, facing, offset, lo, hi in faces:
        buckets[(axis, facing, round(offset / EPS))].append((lo, hi))

    hits = []

    for (axis, facing, key), rects in buckets.items():
        if len(rects) < 2:
            continue

        for i in range(len(rects)):
            for j in range(i + 1, len(rects)):
                a_lo, a_hi = rects[i]
                b_lo, b_hi = rects[j]

                w = min(a_hi[0], b_hi[0]) - max(a_lo[0], b_lo[0])
                h = min(a_hi[1], b_hi[1]) - max(a_lo[1], b_lo[1])

                if w <= 0.0 or h <= 0.0 or w * h < MIN_AREA:
                    continue

                hits.append(dict(axis="xyz"[axis], facing=facing,
                                 at=round(key * EPS, 3), area=round(w * h, 2),
                                 # 어느 부재인지 알아보려면 두 사각형의 크기가
                                 # 이름보다 낫습니다. 상자 언어에서는 치수가 곧 정체입니다.
                                 a=[round(a_hi[0] - a_lo[0], 2), round(a_hi[1] - a_lo[1], 2)],
                                 b=[round(b_hi[0] - b_lo[0], 2), round(b_hi[1] - b_lo[1], 2)]))

    return hits


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

        for key, faces in groups.items():
            hits = audit(faces)

            report.append(dict(
                name=key, faces=len(faces),
                pairs=len(hits),
                area=round(sum(h["area"] for h in hits), 1),
                worst=sorted(hits, key=lambda h: -h["area"])[:8]))

    return report


if __name__ == "__main__":
    print("###JSON###" + json.dumps(main()))
