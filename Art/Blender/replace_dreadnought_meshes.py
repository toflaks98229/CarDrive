# -*- coding: utf-8 -*-
"""
Replace the Dreadnought's mesh geometry from hand-edited meshes, keeping the rig.

WHY THIS EXISTS
  build_dreadnought.py builds the model procedurally, so re-running it wipes any
  hand edit. But the interesting edits ARE by hand - trimming ornament, moving the
  head - and they are worth keeping. Those edits come back from an FBX round trip:
  unparented objects at world positions with no armature.

  So the rig cannot come from the edited file (there is no armature in it) and the
  geometry cannot come from the build script. This script joins the two: it takes
  world-space geometry from a dump and writes it into the rigged blend's objects,
  converting through each object's own matrix. Object origins, bone parenting,
  material slots and the armature all stay as they were.

  After this, Dreadnought.blend is READ, not rebuilt - same as the Strider. Running
  build_dreadnought.build() again would throw the hand edits away.

  The dump is world-space geometry, made by running this in the edited file
  (leave Edit Mode first, or the mesh being edited comes out stale):

      out = {}
      for ob in bpy.data.objects:
          if ob.type != 'MESH':
              continue
          m = ob.matrix_world
          out[ob.name] = dict(
              verts=[list(m @ v.co) for v in ob.data.vertices],
              polys=[list(p.vertices) for p in ob.data.polygons],
              matidx=[p.material_index for p in ob.data.polygons],
              mats=[s.material.name for s in ob.material_slots])
      json.dump(out, open(path, "w"))

  World space is the point: the edited file has no armature and no parenting, so
  its object frames differ from the rigged file's. Going through world space means
  neither side has to agree about local frames.

Run:
    blender -b Dreadnought.blend --python replace_dreadnought_meshes.py -- <geo.json>
    blender -b Dreadnought.blend --python-expr "import build_dreadnought as b; b.finalize()"
"""
import json
import sys

import bpy
from mathutils import Vector


def main(path):
    with open(path, encoding="utf-8") as f:
        geo = json.load(f)

    missing = [n for n in geo if n not in bpy.data.objects]
    assert not missing, "이 blend 에 없는 오브젝트: %s" % missing

    extra = [o.name for o in bpy.data.objects if o.type == 'MESH' and o.name not in geo]
    assert not extra, "덤프에 없는 메시가 남습니다: %s" % extra

    report = {}

    for name, g in geo.items():
        ob = bpy.data.objects[name]
        inv = ob.matrix_world.inverted()

        me = bpy.data.meshes.new(name + "_new")
        me.from_pydata([tuple(inv @ Vector(v)) for v in g["verts"]], [],
                       [tuple(p) for p in g["polys"]])
        me.update()

        # 머티리얼 칸은 이름으로 다시 잇습니다. 순서가 곧 material_index 이므로
        # 덤프의 순서를 그대로 씁니다.
        for mat_name in g["mats"]:
            mat = bpy.data.materials.get(mat_name)
            assert mat is not None, "머티리얼 없음: %s" % mat_name
            me.materials.append(mat)

        for poly, index in zip(me.polygons, g["matidx"]):
            poly.material_index = index

        old = ob.data
        old_name = old.name
        before = (len(old.vertices), len(old.polygons))

        ob.data = me
        bpy.data.meshes.remove(old)
        me.name = old_name

        report[name] = dict(before=before, after=(len(me.vertices), len(me.polygons)))

    return report


if __name__ == "__main__":
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    print("###JSON###" + json.dumps(main(argv[0])))
