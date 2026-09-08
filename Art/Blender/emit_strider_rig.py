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
    ("Gun", "SM_Gun_HeavyCannon", True, "Gun_Pitch"),
    ("Gun_Alt", "SM_Gun_Autocannon", False, "Gun_Pitch"),
]


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
    muzzle = P(bones["B_GunMuzzle"].head_local)

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
             axis=[1.0, 0.0, 0.0], range=[-25.0, 60.0]),
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

    out["muzzles"] = [dict(node="Muzzle", parent="Gun_Pitch",
                           pos=[round(v, 5) for v in (pitch_world.inverted() @ Matrix.Translation(muzzle)).translation])]

    AIM_WORLD = {"Gun_Yaw": yaw_world, "Gun_Pitch": pitch_world,
                 "Head_Yaw": neck_world, "Head_Pitch": head_world}

    for node, obj_name, active, parent in BODY_PARTS:
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
