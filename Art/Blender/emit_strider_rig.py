# -*- coding: utf-8 -*-
"""
Read Strider.blend's armature and write the rig numbers Unity rebuilds from.

WHY THIS EXISTS SEPARATELY
  The Dreadnought's numbers come out of its own build script, so the model and
  the rig cannot drift. The Strider has no such script here - its blend lives
  in another project and its build script would regenerate a DIFFERENT model
  than the one shipped (16 parts against the script's 19). So the model must be
  read, not rebuilt, and this file is the reproducible way to do that.

  Same maths as build_dreadnought.emit_rig_json. Kept in step by hand; if one
  changes, change both.

Run:
    blender -b <Strider.blend> --python emit_strider_rig.py
"""
import json
import math

import bpy
from mathutils import Matrix, Vector

OUT = "E:/GamePJ/CarDrive/Assets/_Project/04.Art/02.Models/Robot/SM_Strider.rig.json"

# Blender -> Unity:  u = (-bx, bz, -by)
CM = Matrix(((-1, 0, 0, 0), (0, 0, 1, 0), (0, -1, 0, 0), (0, 0, 0, 1)))

RIG = "Strider_Rig"
BODY_Z = 5.60

LEG_NODES = {"L": "Leg_FrontLeft", "R": "Leg_FrontRight", "B": "Leg_Rear"}

BODY_PARTS = [
    # node, object, active, parent
    ("Chassis", "SM_Strider_Chassis", True, "Body"),
    ("HullCore", "SM_Strider_HullCore", True, "Body"),
    ("Neck", "SM_Strider_Neck", True, "Head_Yaw"),
    ("Head", "SM_Strider_Head", True, "Head_Pitch"),
    ("GunMount", "SM_Strider_GunMount", True, "Gun_Yaw"),
]

# 무장은 <b>박아 두지 않고 씬에서 찾습니다.</b> 여기 두 줄로 적어 두었더니
# 무장이 둘에서 여섯이 되는 순간 넷이 조용히 빠졌습니다 - FBX 에는 들어 있는데
# json 에 없으면 유니티가 오브젝트를 안 만들고, 아무 오류도 안 납니다.
# <c>build_strider_guns.py</c> 가 무엇을 내든 여기가 따라옵니다.
GUN_PREFIX = "SM_Gun_"

# 기본 무장입니다. 이것만 <c>active</c> 로 나갑니다.
DEFAULT_GUN = "SM_Gun_HeavyCannon"


def gun_node(obj_name):
    """오브젝트 이름에서 유니티 노드 이름을 뽑습니다."""
    return "Gun_" + obj_name[len(GUN_PREFIX):]


def gun_parts():
    """
    씬의 무장을 <b>부모가 먼저 오도록</b> 늘어놓습니다.

    이름이 규약입니다 - 토막 셋(<c>SM_Gun_Rotary</c>)이면 무장 본체이고,
    넷(<c>SM_Gun_Rotary_Rotor</c>)이면 그 무장의 <b>움직이는 조각</b>입니다.
    조각은 본체 노드 밑으로 들어가므로 <b>본체가 먼저 나와야</b> 합니다 -
    유니티 쪽은 목록 순서대로 만들면서 부모를 찾습니다.

    기본 무장만 <c>active</c> 이고, 그 조각들도 같이 켜집니다.
    """
    bases, kids = [], {}

    for o in bpy.data.objects:
        if o.type != 'MESH' or not o.name.startswith(GUN_PREFIX):
            continue

        bits = o.name.split("_")
        assert len(bits) in (3, 4), "무장 이름은 토막 셋 또는 넷입니다: " + o.name

        if len(bits) == 3:
            bases.append(o.name)
        else:
            kids.setdefault("_".join(bits[:3]), []).append(o.name)

    assert DEFAULT_GUN in bases, "기본 무장 %s 가 씬에 없습니다" % DEFAULT_GUN
    bases.sort(key=lambda n: (n != DEFAULT_GUN, n))

    out = []
    for base in bases:
        active = base == DEFAULT_GUN
        out.append((gun_node(base), base, active, "Gun_Pitch"))
        # ⚠ 조각은 <b>늘 켜 둡니다.</b> 무장이 꺼져 있으면 그 밑도 안 보이므로
        # 따로 끌 이유가 없는데, 껐더니 유니티가 그 오브젝트의 <c>Awake</c> 를
        # 부르지 않아 <b>반동 부품이 깨어나지 못했습니다</b> - 나중에 무장을 켜도
        # 포신이 안 밀립니다. 조각을 끄고 켜는 것은 부모의 몫입니다.
        for kid in sorted(kids.pop(base, ())):
            out.append((gun_node(kid), kid, True, gun_node(base)))

    assert not kids, "본체 없는 조각이 남았습니다: %s" % sorted(kids)

    return out


def look(fwd, up):
    """Unity Quaternion.LookRotation, rebuilt so the numbers match exactly."""
    z = Vector(fwd).normalized()
    x = Vector(up).cross(z)
    if x.length < 1e-6:
        alt = Vector((0, 1, 0)) if abs(z.y) < 0.99 else Vector((1, 0, 0))
        x = alt.cross(z)
    x.normalize()
    y = z.cross(x)
    return Matrix(((x.x, y.x, z.x), (x.y, y.y, z.y), (x.z, y.z, z.z))).to_4x4()


def trs(m):
    loc, rot, _ = m.decompose()
    return dict(pos=[round(v, 5) for v in loc],
                rot=[round(rot.x, 6), round(rot.y, 6), round(rot.z, 6), round(rot.w, 6)])


def main():
    rig = bpy.data.objects[RIG]
    bones = rig.data.bones
    cmi = CM.inverted()

    toes = [Vector(bones["B_Foot_" + k].tail_local) for k in LEG_NODES]
    root_b = Vector((sum(t.x for t in toes) / len(toes),
                     sum(t.y for t in toes) / len(toes), 0.0))
    body_b = Vector((0.0, root_b.y, BODY_Z))

    def P(v):
        d = Vector(v) - root_b
        return Vector((-d.x, d.z, -d.y))

    def M2U(m):
        return CM @ (Matrix.Translation(-root_b) @ m) @ cmi

    body_world = Matrix.Translation(P(body_b))

    out = {"standHeight": round(P(body_b).y, 5),
           "rootOriginBlender": [round(v, 5) for v in root_b],
           "meshPrefix": "SM_Strider_",
           "bodyLocal": trs(body_world), "bodyParts": [], "legs": []}

    # ---- aim joints ------------------------------------------------------
    # The gun bone points straight down, so its own axis is the traverse axis;
    # the pitch bone hinges about local X. The neck carries the sensor cap.
    gun_yaw = P(bones["B_GunYaw"].head_local)
    gun_pitch = P(bones["B_GunPitch"].head_local)
    neck = P(bones["B_Neck"].head_local)
    head = P(bones["B_Head"].head_local)

    yaw_world = Matrix.Translation(gun_yaw)
    pitch_world = Matrix.Translation(gun_pitch)
    neck_world = Matrix.Translation(neck)
    head_world = Matrix.Translation(head)

    out["aim"] = [
        dict(node="Gun_Yaw", parent="Body",
             pos=[round(v, 5) for v in (body_world.inverted() @ yaw_world).translation],
             axis=[0.0, 1.0, 0.0], range=[-120.0, 120.0]),
        dict(node="Gun_Pitch", parent="Gun_Yaw",
             pos=[round(v, 5) for v in (yaw_world.inverted() @ pitch_world).translation],
             # ⚠ 드는 각이 <c>-25</c> 였는데 <b>형상이 허락하지 않습니다.</b>
             # build_strider_guns.pitch_limit() 로 재면 무장 여섯이 10.5~18°
             # 에서 선회 링에 막힙니다. 내리는 각은 60° 가 넉넉히 됩니다.
             # 이 값이 형상보다 크면 게임에서 포신이 제 몸을 뚫습니다.
             axis=[1.0, 0.0, 0.0], range=[-10.0, 60.0]),
        dict(node="Head_Yaw", parent="Body",
             pos=[round(v, 5) for v in (body_world.inverted() @ neck_world).translation],
             axis=[0.0, 1.0, 0.0], range=[-75.0, 75.0]),
        dict(node="Head_Pitch", parent="Head_Yaw",
             pos=[round(v, 5) for v in (neck_world.inverted() @ head_world).translation],
             axis=[1.0, 0.0, 0.0], range=[-30.0, 35.0]),
    ]

    out["turrets"] = [
        dict(name="Gun", yaw="Gun_Yaw", pitch="Gun_Pitch", muzzle="Muzzle"),
        dict(name="Head", yaw="Head_Yaw", pitch="Head_Pitch", muzzle=""),
    ]

    # ⚠ 총구는 <b>본이 아니라 엠프티에서</b> 읽습니다. <c>B_GunMuzzle</c> 본은
    # 예전 중포에 맞춰 박아 둔 자리라, 포신 길이를 고치면 조용히 어긋납니다 -
    # 머즐 플래시가 포신 속이나 허공에서 납니다. 엠프티는 무장을 지을 때
    # 그 무장의 총구에 놓이므로 어긋날 수가 없습니다.
    # 총구를 <b>무장 노드 밑</b>에 답니다. <c>Gun_Pitch</c> 밑에 두면 무장을 갈아
    # 끼웠을 때 총구만 옛 자리에 남습니다 - 머즐 플래시가 허공에서 납니다.
    #
    # 무장 본체의 트랜스폼이 <c>Gun_Pitch</c> 기준으로 항등이므로 좌표는 같습니다.
    out["muzzles"] = []
    for node, obj_name, active, parent in gun_parts():
        if parent != "Gun_Pitch":
            continue

        suffix = obj_name[len(GUN_PREFIX):]
        emp = bpy.data.objects.get("SKT_Muzzle_" + suffix)
        if emp is None:
            continue

        at = P(emp.matrix_world.translation)
        out["muzzles"].append(dict(
            node="Muzzle" if active else "Muzzle_" + suffix,
            parent=node,
            pos=[round(v, 5) for v in
                 (pitch_world.inverted() @ Matrix.Translation(at)).translation]))

    assert out["muzzles"], "SKT_Muzzle_* 엠프티가 하나도 없습니다"

    AIM_WORLD = {"Gun_Yaw": yaw_world, "Gun_Pitch": pitch_world,
                 "Head_Yaw": neck_world, "Head_Pitch": head_world}

    # 무장 본체는 <c>Gun_Pitch</c> 자리에 항등으로 앉습니다. 그 밑의 조각들이
    # <b>본체 기준</b> 좌표로 나와야 하므로 여기에 같이 넣어 둡니다.
    for node, obj_name, active, parent in gun_parts():
        if parent == "Gun_Pitch":
            AIM_WORLD[node] = pitch_world

    for node, obj_name, active, parent in BODY_PARTS + gun_parts():
        base = AIM_WORLD.get(parent, body_world)
        d = trs(base.inverted() @ M2U(bpy.data.objects[obj_name].matrix_world))
        d.update(node=node, mesh=obj_name, active=active, parent=parent)
        out["bodyParts"].append(d)

    # ---- legs ------------------------------------------------------------
    for key, node in LEG_NODES.items():
        hip = P(bones["B_Femur_" + key].head_local)
        knee = P(bones["B_Femur_" + key].tail_local)
        ank = P(bones["B_Foot_" + key].head_local)
        toe = P(bones["B_Foot_" + key].tail_local)

        upper, lower, ankle = (knee - hip).length, (ank - knee).length, (toe - ank).length
        home = Vector((toe.x, 0.0, toe.z))

        axis = (ank - hip).normalized()
        pole = (knee - hip) - axis * (knee - hip).dot(axis)
        pole.normalize()

        femur_w = Matrix.Translation(hip) @ look(knee - hip, pole)
        tibia_w = Matrix.Translation(knee) @ look(ank - knee, pole)
        tarsus_w = Matrix.Translation(ank) @ look(toe - ank, (ank - knee).cross(pole))
        leg_w = Matrix.Translation(hip)

        leg = dict(node=node,
                   legLocal=trs(body_world.inverted() @ leg_w),
                   upperLength=round(upper, 5), lowerLength=round(lower, 5),
                   ankleLength=round(ankle, 5),
                   homeOffset=[round(v, 5) for v in home],
                   kneePole=[round(v, 6) for v in pole],
                   ankleOutward=0.0,
                   femurLocal=trs(leg_w.inverted() @ femur_w),
                   tibiaLocal=trs(femur_w.inverted() @ tibia_w),
                   tarsusLocal=trs(tibia_w.inverted() @ tarsus_w),
                   parts=[])
        for mesh_node, obj_name, parent in (
                ("FemurMesh", "SM_Strider_Femur_" + key, femur_w),
                ("TibiaMesh", "SM_Strider_Tibia_" + key, tibia_w),
                ("TarsusMesh", "SM_Strider_Foot_" + key, tarsus_w)):
            d = trs(parent.inverted() @ M2U(bpy.data.objects[obj_name].matrix_world))
            d.update(node=mesh_node, mesh=obj_name)
            leg["parts"].append(d)
        out["legs"].append(leg)

    hull = bpy.data.objects["SM_Strider_HullCore"]
    bb = [hull.matrix_world @ Vector(c) for c in hull.bound_box]
    mn = Vector([min(v[i] for v in bb) for i in range(3)])
    mx = Vector([max(v[i] for v in bb) for i in range(3)])
    span = mx - mn
    out["bodyBox"] = {"center": [round(v, 5) for v in P((mn + mx) * 0.5)],
                      "size": [round(abs(span.x), 5), round(abs(span.z), 5), round(abs(span.y), 5)]}

    with open(OUT, "w", encoding="utf-8") as f:
        json.dump(out, f, indent=1)

    print("standHeight", out["standHeight"], "legs", len(out["legs"]),
          "aim", [a["node"] for a in out["aim"]])
    print("written", OUT)


if __name__ == "__main__":
    main()
