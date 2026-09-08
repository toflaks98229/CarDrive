# -*- coding: utf-8 -*-
"""
Move B_Head down to the hand-made head's own centre, leaving the mesh where it is.

WHY
  The head was enlarged and moved forward-down by hand, but its bone stayed at
  z 3.34 - above the top of the new head (3.29). Pitching it therefore swung the
  whole head like a pendulum instead of nodding it. The pivot belongs at the
  head's vertical centre, 3.0645.

  The head mesh is bone-parented, so moving the bone drags the mesh with it. This
  records the mesh's world matrix first and puts it back afterwards, so only the
  pivot moves. emit_rig_json then picks up the compensating offset by itself -
  the Head body part is stored relative to Head_Pitch.

Run:
    blender -b Dreadnought.blend --python move_head_pivot.py
    blender -b Dreadnought.blend --python-expr "import build_dreadnought as b; b.finalize()"
"""
import json

import bpy

RIG = "Dreadnought_Rig"
BONE = "B_Head"
MESH = "SM_Dread_Head"
NEW_Z = 3.0645


def main():
    rig = bpy.data.objects[RIG]
    head = bpy.data.objects[MESH]

    before = head.matrix_world.copy()
    was = list(rig.data.bones[BONE].head_local)

    bpy.context.view_layer.objects.active = rig
    bpy.ops.object.mode_set(mode='EDIT')

    bone = rig.data.edit_bones[BONE]
    delta = NEW_Z - bone.head.z
    bone.head.z += delta
    bone.tail.z += delta

    bpy.ops.object.mode_set(mode='OBJECT')

    # 본이 움직이면 매달린 메시도 끌려갑니다. 세계 좌표를 그대로 되돌립니다.
    head.matrix_world = before
    bpy.context.view_layer.update()

    moved = max(abs(a - b) for a, b in zip(head.matrix_world.translation, before.translation))
    assert moved < 1e-5, "머리 메시가 %.6f m 움직였습니다" % moved

    bpy.ops.wm.save_mainfile()

    return dict(bone_was=[round(v, 4) for v in was],
                bone_now=[round(v, 4) for v in rig.data.bones[BONE].head_local],
                mesh_drift=round(moved, 8))


if __name__ == "__main__":
    print("###JSON###" + json.dumps(main()))
