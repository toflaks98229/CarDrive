# -*- coding: utf-8 -*-
"""
Seeded boulders for CarDrive, sized to replace the frozen placeholder rocks.

WHAT THIS REPLACES
  Five rock meshes, 774 instances between them, stored as code-generated
  .asset files whose tool was deleted in commit 1170605. Two of them are 80
  triangles and three are 320 - the counts of a subdivided icosphere, which is
  what they look like: smooth balls, not stone.

WHY NOT THE BOX LANGUAGE
  Everything else in this world is axis-aligned masses, because everything else
  was POURED or WELDED. A rock was not. Forcing boxes on it would read as
  rubble from the megastructure, which is a different object with a different
  meaning. So this is the one generator that leaves the box language.

  It uses a CONVEX HULL of jittered points instead. That gives large flat
  facets meeting at hard edges - which is what a fractured boulder is - and it
  is deterministic from a seed, needs no booleans, and produces a mesh that is
  already convex. That last part matters: the rock prefabs use a CONVEX mesh
  collider, so the collider is exactly the silhouette with nothing to cook.

  Fewer, larger facets also read better at distance than a subdivided sphere,
  and cost less: about 60 triangles against 320.

THE ENVELOPE
  Same rule as the buildings. The scene has 774 rock transforms baked in and
  the world is frozen, so each replacement takes the old bounds as INPUT -
  including how far it sank below the ground plane, which is what makes a rock
  look bedded rather than dropped.

Run:
    blender -b --python build_rocks.py
"""
import json
import math
import os
import random
import sys

import bmesh
import bpy

HERE = os.path.dirname(os.path.abspath(__file__))
if HERE not in sys.path:
    sys.path.insert(0, HERE)

import hardsurface  # noqa: E402
import uv_worldscale  # noqa: E402

OUT_DIR = r"E:\GamePJ\CarDrive\Assets\_Project\04.Art\02.Models\Rock"
BLEND_PATH = r"E:\GamePJ\CarDrive\Art\Blender\Rocks.blend"
UV_TILE = 1.0

MATS = [("M_Rock", (0.40, 0.39, 0.36, 1.0), 0.90, 0.00)]

# 옛 바위의 봉투입니다. <b>측량해서 얻은 값이고 바꾸면 안 됩니다.</b>
# (가로 x, 세로 y, 높이 z, 지면 아래로 묻힌 깊이) — PropMeshSetup.Survey 가 찍어 준 값.
ENVELOPE = {
    "Rock01": (1.64, 2.27, 0.89, 0.30),
    "Rock02": (1.98, 2.03, 0.76, 0.27),
    "Rock03": (2.34, 1.97, 1.60, 0.54),
    "Rock04": (1.64, 1.77, 1.33, 0.45),
    "Rock05": (1.85, 1.67, 1.16, 0.41),
}


def hull_points(seed, count, roughness):
    """
    구 위에 흩뿌린 점입니다. 반지름을 흔들어야 껍질이 <b>공이 아니라 돌</b>이 됩니다.

    황금각으로 고르게 뿌린 다음 반지름만 흔듭니다. 방향까지 무작위로 뿌리면 한쪽에
    점이 몰려 껍질의 한 면이 통째로 평평해지고, 그러면 돌이 아니라 깨진 접시가 됩니다.
    """
    rng = random.Random(seed)
    golden = math.pi * (3.0 - math.sqrt(5.0))
    points = []

    for i in range(count):
        # 고르게 뿌리기 (Fibonacci sphere)
        z = 1.0 - 2.0 * (i + 0.5) / count
        r = math.sqrt(max(0.0, 1.0 - z * z))
        a = golden * i

        radius = 1.0 - rng.uniform(0.0, roughness)
        points.append((math.cos(a) * r * radius, math.sin(a) * r * radius, z * radius))

    return points


def build(name, seed):
    """봉투 하나에서 바위 한 덩이를 세웁니다. 원점은 <b>지면</b>입니다."""
    w, d, h, buried = ENVELOPE[name]
    rng = random.Random(seed)

    bm = bmesh.new()

    for p in hull_points(seed, rng.randint(13, 20), rng.uniform(0.26, 0.44)):
        bm.verts.new(p)

    bm.verts.ensure_lookup_table()

    result = bmesh.ops.convex_hull(bm, input=bm.verts, use_existing_faces=False)

    # 껍질 안쪽에 남은 점과 면은 버립니다. 두면 콜라이더가 헛돌고 UV 도 늘어납니다.
    bmesh.ops.delete(bm, geom=result["geom_interior"], context='VERTS')
    bmesh.ops.delete(bm, geom=result["geom_unused"], context='VERTS')

    mesh = bpy.data.meshes.new("SM_" + name)
    bm.to_mesh(mesh)
    bm.free()

    for poly in mesh.polygons:
        poly.use_smooth = False

    mesh.materials.append(bpy.data.materials[MATS[0][0]])

    obj = bpy.data.objects.new("SM_" + name, mesh)
    bpy.context.scene.collection.objects.link(obj)

    # ---- 봉투에 맞추기 ----------------------------------------------------
    # 껍질은 씨앗마다 크기가 달라지므로 만든 뒤에 <b>재서</b> 맞춥니다. 미리 계산해
    # 두면 점 배치가 조금만 달라져도 봉투를 넘습니다.
    lo, hi = hardsurface.bounds(obj)
    span = [hi[i] - lo[i] for i in range(3)]
    want = (w, d, h)

    for v in mesh.vertices:
        for i in range(3):
            t = (v.co[i] - lo[i]) / span[i] if span[i] > 1e-6 else 0.5
            v.co[i] = (t - 0.5) * want[i]

        # 바닥을 지면 아래로 내립니다. 묻혀 있어야 <b>박힌 돌</b>로 보입니다 —
        # 지면에 딱 놓으면 굴러온 것처럼 보입니다.
        v.co[2] += h * 0.5 - buried

    mesh.update()
    return obj


def run():
    report = []

    for i, name in enumerate(sorted(ENVELOPE)):
        hardsurface.wipe()
        hardsurface.ensure_materials(MATS)

        obj = build(name, 4400 + i * 91)
        uv_worldscale.box_uv(obj, UV_TILE)

        path = os.path.join(OUT_DIR, obj.name + ".fbx")
        written = hardsurface.export_fbx(path)

        lo, hi = hardsurface.bounds(obj)
        w, d, h, buried = ENVELOPE[name]
        got = [round(hi[a] - lo[a], 2) for a in range(3)]

        report.append(dict(
            name=obj.name, want=[w, d, h], got=got,
            fits=all(abs(got[a] - [w, d, h][a]) < 0.02 for a in range(3)),
            floor=round(lo[2], 3), buried=buried,
            tris=sum(len(p.vertices) - 2 for p in obj.data.polygons),
            verts=len(obj.data.vertices), fbx=written))

    return report


def lay_out():
    """다섯을 한 줄로 세워 blend 에 남깁니다. 나란히 놓아야 서로 다른지 보입니다."""
    hardsurface.wipe()
    hardsurface.ensure_materials(MATS)

    x = 0.0
    for i, name in enumerate(sorted(ENVELOPE)):
        obj = build(name, 4400 + i * 91)
        uv_worldscale.box_uv(obj, UV_TILE)
        obj.location.x = x
        x += ENVELOPE[name][0] + 1.4

    bpy.ops.wm.save_as_mainfile(filepath=BLEND_PATH)


if __name__ == "__main__":
    out = run()
    lay_out()
    print("###JSON###" + json.dumps(out))
