# -*- coding: utf-8 -*-
"""
Dreadnought (biped walker) - procedural hard-surface build for CarDrive.

Skeleton / proportions: Castraferrum-pattern Dreadnought reference. Just under
four metres, deliberately LOW PROFILE (the pattern exists for boarding actions
and tunnel fights), a stocky torso carried close above short backward-folding
legs, and a flat sarcophagus front plate as the defining face.

Design language: BRUTALIST, identical to the Strider. Poured concrete slabs on
a steel chassis. Monolithic rectangular masses, cassette-futurism heft, no
external ornament - every protruding part is structure (pins, collars, rams,
footplates). Value contrast carries the read: pale concrete / dark steel.

RULES (same as Strider - see Strider_README.md)
  * Rectangular box sections everywhere. Chamfer 0.03~0.05, enough to catch a
    highlight and no more. It must not round the silhouette.
  * NO BEVEL MODIFIER on anything. The section chamfer already does that job;
    the modifier only multiplied the polygon count. finalize() strips and
    asserts against them.
  * No ornament. No stripes, fins, antennae or gloss panels. The only
    non-structural elements are the sensor slit and one indicator lamp.
  * Rig only. No actions / keyframes / NLA.
  * Arms are SEPARATE objects bone-parented to the shoulder bones => swappable,
    exactly like the Strider's gun.

⚠ THIS SCRIPT NO LONGER MATCHES THE SHIPPED MODEL - build() WOULD DESTROY IT.

  The shipped Dreadnought.blend carries HAND EDITS on top of what build() makes:
  ornament trimmed off every part, the reactor cut down, the head enlarged and
  moved forward. 1940v/1458p became 1436v/1123p. None of that is in the code
  below, so running build() again throws it away.

  The model is now READ, not rebuilt - the same standing the Strider has. What
  is still safe to run:

    finalize()                  strip bevels, world-scale UVs, save blend,
                                export FBX, emit rig json. Reads whatever is
                                in the scene; does not rebuild it.
    replace_dreadnought_meshes  push new hand-edited geometry into the rigged
                                blend without touching the armature.

  build() is kept because it is where the proportions, bone placement and aim
  pivots came from, and because the rig it makes is still the rig in use. Treat
  it as the record of how the skeleton was derived, not as the model source.

Run:  blender -b --python build_dreadnought.py   (or exec() from the MCP addon)
"""
import bpy, bmesh, math
from mathutils import Vector, Matrix


def V(x, y, z):
    return Vector((x, y, z))


TAU = math.pi * 2.0

# ----------------------------------------------------------------------------
# materials - concrete, steel, dark steel, and one functional indicator
# ----------------------------------------------------------------------------
MAT_CONCRETE, MAT_STEEL, MAT_DARK, MAT_LAMP = 0, 1, 2, 3
MAT_DEFS = [
    ("M_Dread_Concrete", (0.465, 0.452, 0.430, 1.0), 0.95, 0.00, None),
    ("M_Dread_Steel",    (0.185, 0.192, 0.205, 1.0), 0.45, 1.00, None),
    ("M_Dread_Dark",     (0.048, 0.050, 0.054, 1.0), 0.65, 1.00, None),
    ("M_Dread_Lamp",     (0.180, 0.070, 0.010, 1.0), 0.30, 0.00, ((1.0, 0.42, 0.07, 1.0), 3.0)),
]


def ensure_materials():
    mats = []
    for name, base, rough, metal, emit in MAT_DEFS:
        m = bpy.data.materials.get(name) or bpy.data.materials.new(name)
        m.use_nodes = True
        bsdf = m.node_tree.nodes.get("Principled BSDF")
        if bsdf:
            bsdf.inputs["Base Color"].default_value = base
            bsdf.inputs["Roughness"].default_value = rough
            bsdf.inputs["Metallic"].default_value = metal
            if emit:
                bsdf.inputs["Emission Color"].default_value = emit[0]
                bsdf.inputs["Emission Strength"].default_value = emit[1]
        m.diffuse_color = base
        mats.append(m)
    return mats


# ----------------------------------------------------------------------------
# geometry helpers  (same kit as build_strider.py - kept local so this script
# stays runnable on its own, which is how the Strider script is written too)
# ----------------------------------------------------------------------------
class Builder(object):
    """Collects loose parts, each tagged with the bone that drives it and the
    destructible piece it belongs to. Set `piece` before a run of add() calls."""

    def __init__(self):
        self.parts = []
        self.piece = None

    def add(self, verts, faces, group=None, mat=MAT_CONCRETE, piece=None):
        self.parts.append(dict(verts=verts, faces=faces, group=group, mat=mat,
                               piece=piece or self.piece or group))


def loft(sections):
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
    """Rectangle with a corner chamfer (8 pts). c ~= 0 gives a hard box."""
    c = max(1e-4, min(c, hx * 0.85, hy * 0.85))
    return [(hx, hy - c), (hx - c, hy), (-hx + c, hy), (-hx, hy - c),
            (-hx, -hy + c), (-hx + c, -hy), (hx - c, -hy), (hx, -hy + c)]


def prof_rect(hx, hy, c=0.03):
    return prof_cham(hx, hy, c)


def prof_ngon(n, r, phase=0.0, sx=1.0, sy=1.0):
    return [(math.cos(phase + TAU * i / n) * r * sx,
             math.sin(phase + TAU * i / n) * r * sy) for i in range(n)]


def solid(M, stops):
    secs = [[M @ Vector((a, b, z)) for (a, b) in prof] for z, prof in stops]
    return loft(secs)


def frame(zdir, xhint, origin=V(0, 0, 0)):
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


def collar(M, z0, z1, hx, hy):
    return solid(M, [(z0, prof_rect(hx, hy, 0.02)), (z1, prof_rect(hx, hy, 0.02))])


def prism(p0, p1, r0, r1, n=12, xaxis=V(1, 0, 0), phase=0.0):
    M, L = beam(p0, p1, xaxis)
    return solid(M, [(0.0, prof_ngon(n, r0, phase)),
                     (L, prof_ngon(n, r1, phase))])


# ----------------------------------------------------------------------------
# dimensions
#
# Ground is z = 0. Forward is -Y (same convention as the Strider, which is what
# the Unity import maths assumes). BODY_Z is the hip axle - the torso rides
# directly on it, which is what gives the pattern its squat stance.
# ----------------------------------------------------------------------------
BODY_Z = 2.10

# The knee folds BACKWARD (+Y). Digitigrade, like the pattern's plastic kit.
LEGS = {}
for _key, _sx in (("L", 1.0), ("R", -1.0)):
    LEGS[_key] = dict(
        root=V(_sx * 0.52, -0.02, BODY_Z),      # inboard end of the hip pin
        hip=V(_sx * 0.72, -0.02, BODY_Z),       # femur head == the Unity hip
        knee=V(_sx * 0.72, 0.62, 1.32),
        ankle=V(_sx * 0.70, -0.20, 0.46),
        tip=V(_sx * 0.70, -0.20, 0.00),
        axis=V(1, 0, 0),                        # both legs hinge about world X
        sx=_sx,
    )

TORSO_C = V(0.0, 0.05, 2.78)                    # armoured torso centre
SARC_C = V(0.0, -0.70, 2.62)                    # sarcophagus front plate
# The sensor head sits on the FRONT of the torso, resting on the sarcophagus
# rim and overhanging it like a brow - not perched on the roof.
HEAD_C = V(0.0, -0.84, 3.34)
REACTOR_C = V(0.0, 0.86, 2.80)                  # thermic reactor, rear
SHOULDER = {"L": V(0.98, 0.05, 3.10), "R": V(-0.98, 0.05, 3.10)}
ARM_PIVOT = {"L": V(1.18, 0.05, 3.10), "R": V(-1.18, 0.05, 3.10)}
MUZZLE = {"L": V(1.18, -1.30, 2.72), "R": V(-1.18, -1.62, 3.10)}

ARMS = {"L": "SM_Dread_Arm_Fist", "R": "SM_Dread_Arm_Autocannon"}

SPLIT_PARTS = True


# ----------------------------------------------------------------------------
# torso - the sarcophagus is the face of the machine, so it gets the mass
# ----------------------------------------------------------------------------
def build_torso(b):
    b.piece = "Hull"
    # main armoured box
    b.add(*cbox(TORSO_C, (1.62, 1.30, 1.36), 0.05), group="B_Body", mat=MAT_CONCRETE)
    # slightly pointed cap - the Mk.V read, done by tapering rather than curving
    Mt = frame(V(0, 0, 1), V(1, 0, 0), TORSO_C + V(0, 0, 0.68))
    b.add(*solid(Mt, [(0.00, prof_rect(0.81, 0.65, 0.05)),
                      (0.18, prof_rect(0.55, 0.44, 0.04))]),
          group="B_Body", mat=MAT_CONCRETE)

    b.piece = "Sarcophagus"
    # The lid is a slab of the same poured concrete, stood proud of the torso.
    # Only the retaining rim is steel - a dark border round the face would put
    # the value contrast in the wrong place.
    b.add(*cbox(SARC_C + V(0, 0.05, 0), (1.24, 0.10, 1.04), 0.03),
          group="B_Body", mat=MAT_STEEL)
    b.add(*cbox(SARC_C + V(0, -0.06, 0), (1.10, 0.18, 0.92), 0.04),
          group="B_Body", mat=MAT_CONCRETE)

    b.piece = "Waist"
    # waist block and the hip axle it carries
    b.add(*cbox(V(0, 0.02, BODY_Z + 0.10), (1.26, 0.96, 0.46), 0.04),
          group="B_Body", mat=MAT_CONCRETE)
    b.add(*cbox(V(0, 0.02, BODY_Z - 0.10), (1.34, 1.00, 0.14), 0.03),
          group="B_Body", mat=MAT_STEEL)
    b.add(*prism(V(-0.84, -0.02, BODY_Z), V(0.84, -0.02, BODY_Z), 0.12, 0.12, 12,
                 xaxis=V(0, 1, 0)), group="B_Body", mat=MAT_DARK)

    b.piece = "Reactor"
    # thermic reactor and its two stacks - the pattern's power plant
    b.add(*cbox(REACTOR_C, (1.00, 0.46, 0.92), 0.04), group="B_Body", mat=MAT_CONCRETE)
    for sx in (1.0, -1.0):
        b.add(*prism(REACTOR_C + V(sx * 0.30, 0.02, 0.52),
                     REACTOR_C + V(sx * 0.30, 0.08, 1.08), 0.09, 0.08, 10,
                     xaxis=V(1, 0, 0)), group="B_Body", mat=MAT_DARK)

    b.piece = None


def build_head(b):
    b.piece = "Head"
    b.add(*cbox(HEAD_C, (0.68, 0.64, 0.40), 0.04), group="B_Head", mat=MAT_CONCRETE)
    # the bracket that actually carries it back to the torso face
    b.add(*cbox(HEAD_C + V(0, 0.36, -0.06), (0.40, 0.34, 0.24), 0.03),
          group="B_Head", mat=MAT_STEEL)
    # the one slit and the one lamp. Nothing else on this machine is decoration.
    b.add(*cbox(HEAD_C + V(0, -0.33, 0.03), (0.48, 0.06, 0.10), 0.01),
          group="B_Head", mat=MAT_DARK)
    b.add(*cbox(HEAD_C + V(0.24, -0.33, -0.10), (0.09, 0.06, 0.09), 0.01),
          group="B_Head", mat=MAT_LAMP)
    b.piece = None


def build_shoulders(b):
    for key, S in SHOULDER.items():
        sx = 1.0 if key == "L" else -1.0
        b.piece = "Shoulder_" + key
        b.add(*cbox(S, (0.40, 0.86, 0.82), 0.04), group="B_Body", mat=MAT_CONCRETE)
        b.add(*cbox(S + V(sx * 0.22, 0, 0), (0.10, 0.90, 0.86), 0.03),
              group="B_Body", mat=MAT_STEEL)
        b.add(*prism(S + V(sx * 0.26, 0, 0), S + V(sx * 0.36, 0, 0), 0.11, 0.11, 12,
                     xaxis=V(0, 1, 0)), group="B_Body", mat=MAT_DARK)
    b.piece = None


# ----------------------------------------------------------------------------
# leg - same recipe as the Strider, at Dreadnought scale
# ----------------------------------------------------------------------------
def build_leg(b, key, Lg):
    root, hip, knee = Lg["root"], Lg["hip"], Lg["knee"]
    ankle, tip, ax = Lg["ankle"], Lg["tip"], Lg["axis"]

    fd = (knee - hip).normalized()
    td = (ankle - knee).normalized()
    up = ax.cross(fd).normalized()

    g_hip = "B_Hip_" + key
    g_fem = "B_Femur_" + key
    g_tib = "B_Tibia_" + key
    g_ft = "B_Foot_" + key

    b.piece = "Hip_" + key
    b.add(*cbox(hip, (0.46, 0.76, 0.72), 0.04, zdir=V(0, 0, 1), xhint=ax),
          group=g_hip, mat=MAT_CONCRETE)
    b.add(*cbox(hip + V(Lg["sx"] * 0.24, 0, 0), (0.10, 0.80, 0.76), 0.03,
                zdir=V(0, 0, 1), xhint=ax), group=g_hip, mat=MAT_STEEL)
    b.add(*prism(hip - ax * Lg["sx"] * 0.26, hip + ax * Lg["sx"] * 0.30,
                 0.11, 0.11, 12, xaxis=V(0, 1, 0)), group=g_hip, mat=MAT_DARK)

    b.piece = "Femur_" + key
    # concrete box beam carries the mass; steel only as two flush collars
    fa, fb = hip + fd * 0.14, knee - fd * 0.26
    M, FL = beam(fa, fb, ax)
    b.add(*solid(M, [(0.00, prof_rect(0.35, 0.33)),
                     (FL * 0.16, prof_rect(0.37, 0.35)),
                     (FL * 0.86, prof_rect(0.30, 0.28)),
                     (FL, prof_rect(0.28, 0.26))]), group=g_fem, mat=MAT_CONCRETE)
    b.add(*collar(M, 0.02, 0.16, 0.39, 0.37), group=g_fem, mat=MAT_STEEL)
    b.add(*collar(M, FL - 0.14, FL - 0.02, 0.31, 0.29), group=g_fem, mat=MAT_STEEL)
    # one hydraulic ram down the front of the thigh
    b.add(*bbeam(fa - up * 0.33, fb - up * 0.27, (0.06, 0.06), (0.06, 0.06), ax),
          group=g_fem, mat=MAT_DARK)
    b.add(*prism(knee - ax * 0.30, knee + ax * 0.30, 0.10, 0.10, 12, xaxis=fd),
          group=g_fem, mat=MAT_DARK)

    b.piece = "Tibia_" + key
    b.add(*cbox(knee + td * 0.20, (0.48, 0.46, 0.48), 0.04, zdir=td, xhint=ax),
          group=g_tib, mat=MAT_CONCRETE)
    ta, tb = knee + td * 0.40, ankle - td * 0.20
    M, TL = beam(ta, tb, ax)
    b.add(*solid(M, [(0.00, prof_rect(0.29, 0.28)),
                     (TL * 0.38, prof_rect(0.26, 0.25)),
                     (TL, prof_rect(0.23, 0.22))]), group=g_tib, mat=MAT_CONCRETE)
    b.add(*collar(M, 0.02, 0.14, 0.32, 0.31), group=g_tib, mat=MAT_STEEL)
    b.add(*collar(M, TL - 0.12, TL - 0.02, 0.26, 0.25), group=g_tib, mat=MAT_STEEL)

    b.piece = "Foot_" + key
    fod = (tip - ankle).normalized()
    b.add(*cbox(ankle, (0.42, 0.42, 0.36), 0.03, zdir=td, xhint=ax),
          group=g_ft, mat=MAT_STEEL)
    b.add(*prism(ankle - ax * 0.24, ankle + ax * 0.24, 0.09, 0.09, 12, xaxis=td),
          group=g_ft, mat=MAT_DARK)
    # broad splayed footplate - the pattern stands on slabs, not toes
    b.add(*cbox(ankle + fod * 0.32, (0.78, 0.94, 0.22), 0.03, zdir=fod, xhint=ax),
          group=g_ft, mat=MAT_STEEL)
    # broad splayed footplate - the pattern stands on slabs, not toes
    b.add(*cbox(ankle + fod * 0.32, (0.78, 0.94, 0.22), 0.03, zdir=fod, xhint=ax),
          group=g_ft, mat=MAT_STEEL)
    b.piece = None


# ----------------------------------------------------------------------------
# arms - separate objects, built around their shoulder pivot then localised
# ----------------------------------------------------------------------------
def build_fist_arm(b):
    """Close-combat arm: armoured upper arm, a blunt fist, underslung barrel."""
    P = ARM_PIVOT["L"]
    elbow = P + V(0.02, -0.62, -0.14)
    wrist = P + V(0.02, -1.02, -0.30)

    b.add(*cbox(P + V(0.08, 0, 0), (0.30, 0.60, 0.66), 0.04), mat=MAT_CONCRETE)
    b.add(*bbeam(P + V(0.02, -0.16, 0), elbow, (0.27, 0.29), (0.24, 0.26),
                 V(1, 0, 0)), mat=MAT_CONCRETE)
    b.add(*prism(elbow - V(0.24, 0, 0), elbow + V(0.24, 0, 0), 0.10, 0.10, 12,
                 xaxis=V(0, 1, 0)), mat=MAT_DARK)
    b.add(*bbeam(elbow, wrist, (0.23, 0.25), (0.25, 0.27), V(1, 0, 0)),
          mat=MAT_CONCRETE)
    # the gauntlet: a concrete block behind a steel cuff, and the striking face.
    # The knuckle bar and its ridges are the working surface of a close-combat
    # weapon - that is structure, not ornament, so it stays.
    fist = wrist + V(0, -0.26, -0.02)
    b.add(*cbox(fist, (0.50, 0.54, 0.56), 0.04), mat=MAT_CONCRETE)
    b.add(*cbox(fist + V(0, 0.26, 0), (0.54, 0.10, 0.60), 0.02), mat=MAT_STEEL)
    b.add(*cbox(fist + V(0, -0.29, 0.06), (0.50, 0.12, 0.40), 0.02), mat=MAT_STEEL)
    for i in range(4):
        b.add(*cbox(fist + V(-0.165 + i * 0.11, -0.38, 0.06), (0.09, 0.10, 0.34), 0.02),
              mat=MAT_DARK)
    # thumb block, outboard, so the gauntlet reads as a hand and not a ram
    b.add(*cbox(fist + V(0.29, -0.18, -0.14), (0.12, 0.26, 0.20), 0.02), mat=MAT_DARK)
    # underslung weapon
    b.add(*cbox(fist + V(0, -0.10, -0.36), (0.24, 0.42, 0.18), 0.02), mat=MAT_STEEL)
    b.add(*prism(fist + V(0, -0.30, -0.36), MUZZLE["L"], 0.06, 0.05, 10,
                 xaxis=V(1, 0, 0)), mat=MAT_DARK)
    return MUZZLE["L"]


def build_autocannon_arm(b):
    """Ranged arm: a concrete breech casing on the shoulder and twin barrels."""
    P = ARM_PIVOT["R"]
    breech = P + V(-0.02, -0.52, 0.0)

    b.add(*cbox(P + V(-0.08, 0, 0), (0.30, 0.60, 0.66), 0.04), mat=MAT_CONCRETE)
    b.add(*cbox(breech, (0.48, 0.84, 0.62), 0.04), mat=MAT_CONCRETE)
    b.add(*cbox(breech + V(0, 0.44, 0), (0.52, 0.10, 0.66), 0.02), mat=MAT_STEEL)
    for sx in (1.0, -1.0):
        b.add(*prism(breech + V(sx * 0.13, -0.42, 0.0),
                     MUZZLE["R"] + V(sx * 0.13, 0, 0), 0.06, 0.05, 10,
                     xaxis=V(1, 0, 0)), mat=MAT_DARK)
    b.add(*cbox(breech + V(0, -0.74, 0), (0.42, 0.14, 0.32), 0.02), mat=MAT_STEEL)
    return MUZZLE["R"]


# ----------------------------------------------------------------------------
# object assembly
# ----------------------------------------------------------------------------
def make_object(name, parts, mats, offset=None, vgroups=True, center=False):
    verts, faces, fmat, groups = [], [], [], {}
    for pt in parts:
        off = len(verts)
        verts.extend(pt["verts"])
        for f in pt["faces"]:
            faces.append(tuple(i + off for i in f))
            fmat.append(pt["mat"])
        if pt["group"]:
            groups.setdefault(pt["group"], []).extend(range(off, len(verts)))

    if center:
        lo = Vector([min(v[i] for v in verts) for i in range(3)])
        hi = Vector([max(v[i] for v in verts) for i in range(3)])
        offset = (lo + hi) * 0.5
    loc = offset if offset else Vector((0.0, 0.0, 0.0))

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
    if vgroups:
        for g, idx in groups.items():
            vg = obj.vertex_groups.new(name=g)
            vg.add(list(idx), 1.0, 'REPLACE')

    return obj


# ----------------------------------------------------------------------------
# armature
# ----------------------------------------------------------------------------
def build_rig():
    arm = bpy.data.armatures.new("ARM_Dreadnought")
    rig = bpy.data.objects.new("Dreadnought_Rig", arm)
    bpy.context.scene.collection.objects.link(rig)
    bpy.context.view_layer.objects.active = rig
    rig.select_set(True)
    bpy.ops.object.mode_set(mode='EDIT')
    eb = arm.edit_bones

    specs = [
        ("Root",   V(0, 0, 0),              V(0, -0.9, 0),           None,     False, False, V(0, 0, 1)),
        ("B_Body", V(0, 0.55, BODY_Z),      V(0, -0.55, BODY_Z),     "Root",   False, True,  V(0, 0, 1)),
        ("B_Head", HEAD_C + V(0, 0.34, 0),  HEAD_C + V(0, -0.36, 0), "B_Body", False, True,  V(0, 0, 1)),
    ]
    for key in ("L", "R"):
        specs += [
            ("B_Arm_" + key, ARM_PIVOT[key], ARM_PIVOT[key] + V(0, -0.9, 0),
             "B_Body", False, True, V(0, 0, 1)),
            ("B_Muzzle_" + key, MUZZLE[key], MUZZLE[key] + V(0, -0.35, 0),
             "B_Arm_" + key, False, False, V(0, 0, 1)),
        ]

    for key, Lg in LEGS.items():
        root, hip, knee = Lg["root"], Lg["hip"], Lg["knee"]
        ankle, tip, ax = Lg["ankle"], Lg["tip"], Lg["axis"]
        fd = (knee - hip).normalized()
        td = (ankle - knee).normalized()
        fod = (tip - ankle).normalized()
        specs += [
            ("B_Hip_" + key,   root, hip,   "B_Body",          False, True, ax.cross(fd).normalized()),
            ("B_Femur_" + key, hip,  knee,  "B_Hip_" + key,    True,  True, ax.cross(fd).normalized()),
            ("B_Tibia_" + key, knee, ankle, "B_Femur_" + key,  True,  True, ax.cross(td).normalized()),
            ("B_Foot_" + key,  ankle, tip,  "B_Tibia_" + key,  True,  True, ax.cross(fod).normalized()),
            ("B_IK_" + key,    ankle, ankle + V(0, -0.7, 0), "Root", False, False, V(0, 0, 1)),
        ]

    for name, head, tail, parent, conn, deform, roll_t in specs:
        bone = eb.new(name)
        bone.head, bone.tail = head, tail
        bone.use_deform = deform
        if parent:
            bone.parent = eb[parent]
            bone.use_connect = conn
        bone.align_roll(roll_t)

    col_def = arm.collections.new("DEF")
    col_ctl = arm.collections.new("CTRL")
    for bone in eb:
        (col_def if bone.use_deform else col_ctl).assign(bone)

    bpy.ops.object.mode_set(mode='POSE')
    for key in LEGS:
        pb_hip = rig.pose.bones["B_Hip_" + key]
        pb_fem = rig.pose.bones["B_Femur_" + key]
        pb_tib = rig.pose.bones["B_Tibia_" + key]
        pb_hip.lock_ik_x = True
        pb_hip.lock_ik_z = True
        for pb in (pb_fem, pb_tib):
            pb.lock_ik_y = True
            pb.lock_ik_z = True
        ik = pb_tib.constraints.new('IK')
        ik.target = rig
        ik.subtarget = "B_IK_" + key
        ik.chain_count = 3
        ik.use_tail = True
        ik.use_stretch = False
    bpy.ops.object.mode_set(mode='OBJECT')
    return rig


def bone_parent(obj, rig, bone_name, world=None):
    keep = Matrix.Translation(world) if world else obj.matrix_world.copy()
    obj.parent = rig
    obj.parent_type = 'BONE'
    obj.parent_bone = bone_name
    bpy.context.view_layer.update()
    obj.matrix_world = keep


# ----------------------------------------------------------------------------
# main
# ----------------------------------------------------------------------------
def wipe():
    # bpy.context.object does not exist in every execution context (the MCP
    # addon's exec() is one), so ask for it defensively rather than assume.
    active = getattr(bpy.context, "object", None)
    if active and active.mode != 'OBJECT':
        bpy.ops.object.mode_set(mode='OBJECT')
    for o in list(bpy.data.objects):
        bpy.data.objects.remove(o, do_unlink=True)
    for coll in (bpy.data.meshes, bpy.data.armatures, bpy.data.actions):
        for d in list(coll):
            coll.remove(d)
    for m in list(bpy.data.materials):
        if m.users == 0:
            bpy.data.materials.remove(m)


def build():
    wipe()
    mats = ensure_materials()

    body = Builder()
    build_torso(body)
    build_head(body)
    build_shoulders(body)
    for key, Lg in LEGS.items():
        build_leg(body, key, Lg)

    a1 = Builder(); m1 = build_fist_arm(a1)
    a2 = Builder(); m2 = build_autocannon_arm(a2)
    arm_l = make_object(ARMS["L"], a1.parts, mats, offset=ARM_PIVOT["L"], vgroups=False)
    arm_r = make_object(ARMS["R"], a2.parts, mats, offset=ARM_PIVOT["R"], vgroups=False)

    rig = build_rig()

    made = []
    order, buckets = [], {}
    for pt in body.parts:
        if pt["piece"] not in buckets:
            buckets[pt["piece"]] = []
            order.append(pt["piece"])
        buckets[pt["piece"]].append(pt)
    for piece in order:
        parts = buckets[piece]
        bones = set(pt["group"] for pt in parts)
        assert len(bones) == 1, "piece %r spans bones %s" % (piece, bones)
        obj = make_object("SM_Dread_" + piece, parts, mats, vgroups=False, center=True)
        before = obj.matrix_world.translation.copy()
        bone_parent(obj, rig, parts[0]["group"])
        assert (obj.matrix_world.translation - before).length < 1e-5
        made.append(obj)

    for key, obj, muz in (("L", arm_l, m1), ("R", arm_r, m2)):
        bone_parent(obj, rig, "B_Arm_" + key, world=ARM_PIVOT[key])
        e = bpy.data.objects.new("SKT_Muzzle_" + key, None)
        e.empty_display_type = 'SINGLE_ARROW'
        e.empty_display_size = 0.3
        bpy.context.scene.collection.objects.link(e)
        e.parent = obj
        bpy.context.view_layer.update()
        e.matrix_world = Matrix.Translation(muz)

    bpy.context.view_layer.update()

    def tris(o):
        return sum(len(pl.vertices) - 2 for pl in o.data.polygons)

    return dict(
        pieces=dict((o.name.replace("SM_Dread_", ""), [o.parent_bone, tris(o)])
                    for o in made),
        piece_count=len(made),
        bones=len(rig.data.bones),
        deform_bones=len([b for b in rig.data.bones if b.use_deform]),
        body_tris=sum(tris(o) for o in made),
        arm_tris=tris(arm_l) + tris(arm_r),
        actions=len(bpy.data.actions),
        height=max(v.z for o in made for v in
                   [o.matrix_world @ vv.co for vv in o.data.vertices]),
    )


# ----------------------------------------------------------------------------
# finalise: UVs, save, Unity FBX export
#
# The FBX settings are NOT the Strider's. bake_space_transform MUST be on: with
# it off the Z-up -> Y-up conversion rides on the object transforms instead of
# the mesh data, and the importer that pulls meshes out on their own gets them
# still Z-up. With it on the units come across correctly too, so scale is 1.
# ----------------------------------------------------------------------------
BLEND_PATH = r"E:\GamePJ\CarDrive\Art\Blender\Dreadnought.blend"
FBX_PATH = r"E:\GamePJ\CarDrive\Assets\_Project\04.Art\02.Models\Robot\SM_Dreadnought.fbx"
RIG_JSON = "E:/GamePJ/CarDrive/Assets/_Project/04.Art/02.Models/Robot/SM_Dreadnought.rig.json"

# One texture repeat per metre. Both machines use the same number so a single
# concrete map reads at the same grain on either of them.
UV_TILE = 1.0

# Blender -> Unity:  u = (-bx, bz, -by)
CM = Matrix(((-1, 0, 0, 0), (0, 0, 1, 0), (0, -1, 0, 0), (0, 0, 0, 1)))

LEG_NODES = {"L": "Leg_Left", "R": "Leg_Right"}
# node -> 그 파츠를 매다는 부모. 조준 노드 밑으로 가는 것들이 여기서 갈립니다.
BODY_PARTS = [
    ("Chassis", "SM_Dread_Hull", True),
    ("Sarcophagus", "SM_Dread_Sarcophagus", True),
    ("Waist", "SM_Dread_Waist", True),
    ("Reactor", "SM_Dread_Reactor", True),
    ("Shoulder_L", "SM_Dread_Shoulder_L", True),
    ("Shoulder_R", "SM_Dread_Shoulder_R", True),
    ("Head", "SM_Dread_Head", True),
    ("Arm_L", ARMS["L"], True),
    ("Arm_R", ARMS["R"], True),
]


def _look(fwd, up):
    """Unity Quaternion.LookRotation, rebuilt here so the numbers match exactly."""
    z = Vector(fwd).normalized()
    x = Vector(up).cross(z)
    if x.length < 1e-6:
        alt = Vector((0, 1, 0)) if abs(z.y) < 0.99 else Vector((1, 0, 0))
        x = alt.cross(z)
    x.normalize()
    y = z.cross(x)
    return Matrix(((x.x, y.x, z.x), (x.y, y.y, z.y), (x.z, y.z, z.z))).to_4x4()


def _trs(m):
    loc, rot, _ = m.decompose()
    return dict(pos=[round(v, 5) for v in loc],
                rot=[round(rot.x, 6), round(rot.y, 6), round(rot.z, 6), round(rot.w, 6)])


def emit_rig_json():
    """Unity's WalkerLeg wants local +Z at the next joint; a Blender bone wants
    local +Y. So Unity is handed numbers, not this armature, and this is the one
    place they are derived - the model and the rig cannot drift apart."""
    import json

    rig = bpy.data.objects["Dreadnought_Rig"]
    bones = rig.data.bones
    cmi = CM.inverted()

    # Root = the horizontal centre of the footprint, so the torso rides over the
    # support instead of leaning off it.
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
           "meshPrefix": "SM_Dread_",
           "bodyLocal": _trs(body_world), "bodyParts": [], "legs": []}

    # ---- 조준 관절 -------------------------------------------------------
    # 어깨는 부앙(로컬 X)만, 머리는 요(로컬 Y)와 부앙 둘 다. 모델에 본이 이미
    # 있으므로 그 자리를 그대로 씁니다 - 눈대중으로 다시 잡지 않습니다.
    aim = []
    for key in ("L", "R"):
        # 어깨에 선회를 조금 둡니다. 포탑 부품이 선회·부앙 두 마디를 전제하기도 하고,
        # 실제로 팔이 좌우로 조금 트는 것이 맞습니다.
        aim.append(dict(node="Arm_%s_Yaw" % key, parent="Body",
                        pos=[round(v, 5) for v in
                             (body_world.inverted() @ Matrix.Translation(P(ARM_PIVOT[key]))).translation],
                        axis=[0.0, 1.0, 0.0], range=[-35.0, 35.0]))
        aim.append(dict(node="Arm_%s_Pitch" % key, parent="Arm_%s_Yaw" % key,
                        pos=[0.0, 0.0, 0.0],
                        axis=[1.0, 0.0, 0.0], range=[-70.0, 35.0]))
    head_pivot = Vector(bones["B_Head"].head_local)
    aim.append(dict(node="Head_Yaw", parent="Body",
                    pos=[round(v, 5) for v in
                         (body_world.inverted() @ Matrix.Translation(P(head_pivot))).translation],
                    axis=[0.0, 1.0, 0.0], range=[-80.0, 80.0]))
    aim.append(dict(node="Head_Pitch", parent="Head_Yaw",
                    pos=[0.0, 0.0, 0.0], axis=[1.0, 0.0, 0.0], range=[-25.0, 30.0]))
    out["aim"] = aim
    out["turrets"] = [
        dict(name="Arm_L", yaw="Arm_L_Yaw", pitch="Arm_L_Pitch", muzzle="Muzzle_L"),
        dict(name="Arm_R", yaw="Arm_R_Yaw", pitch="Arm_R_Pitch", muzzle="Muzzle_R"),
        dict(name="Head", yaw="Head_Yaw", pitch="Head_Pitch", muzzle=""),
    ]

    # 총구는 팔이 돌면 따라가야 하므로 그 팔 밑에 답니다.
    out["muzzles"] = [
        dict(node="Muzzle_%s" % key, parent="Arm_%s_Pitch" % key,
             pos=[round(v, 5) for v in
                  (Matrix.Translation(P(ARM_PIVOT[key])).inverted() @ Matrix.Translation(P(MUZZLE[key]))).translation])
        for key in ("L", "R")]

    ATTACH = {"Arm_L": "Arm_L_Pitch", "Arm_R": "Arm_R_Pitch", "Head": "Head_Pitch"}
    AIM_WORLD = {"Arm_L_Pitch": Matrix.Translation(P(ARM_PIVOT["L"])),
                 "Arm_R_Pitch": Matrix.Translation(P(ARM_PIVOT["R"])),
                 "Head_Pitch": Matrix.Translation(P(head_pivot))}

    for node, obj_name, active in BODY_PARTS:
        parent = ATTACH.get(node, "Body")
        base = AIM_WORLD.get(parent, body_world)
        m = base.inverted() @ M2U(bpy.data.objects[obj_name].matrix_world)
        d = _trs(m)
        d.update(node=node, mesh=obj_name, active=active, parent=parent)
        out["bodyParts"].append(d)

    for key, node in LEG_NODES.items():
        hip = P(bones["B_Femur_" + key].head_local)
        knee = P(bones["B_Femur_" + key].tail_local)
        ank = P(bones["B_Foot_" + key].head_local)
        toe = P(bones["B_Foot_" + key].tail_local)

        upper, lower, ankle = (knee - hip).length, (ank - knee).length, (toe - ank).length
        home = Vector((toe.x, 0.0, toe.z))

        # Pole chosen so the IK's knee lands exactly on the modelled knee.
        axis = (ank - hip).normalized()
        pole = (knee - hip) - axis * (knee - hip).dot(axis)
        pole.normalize()

        femur_w = Matrix.Translation(hip) @ _look(knee - hip, pole)
        tibia_w = Matrix.Translation(knee) @ _look(ank - knee, pole)
        tarsus_w = Matrix.Translation(ank) @ _look(toe - ank, (ank - knee).cross(pole))
        leg_w = Matrix.Translation(hip)

        leg = dict(node=node,
                   legLocal=_trs(body_world.inverted() @ leg_w),
                   upperLength=round(upper, 5), lowerLength=round(lower, 5),
                   ankleLength=round(ankle, 5),
                   homeOffset=[round(v, 5) for v in home],
                   kneePole=[round(v, 6) for v in pole],
                   ankleOutward=0.0,
                   femurLocal=_trs(leg_w.inverted() @ femur_w),
                   tibiaLocal=_trs(femur_w.inverted() @ tibia_w),
                   tarsusLocal=_trs(tibia_w.inverted() @ tarsus_w),
                   parts=[])
        for mesh_node, obj_name, parent in (
                ("HipMesh", "SM_Dread_Hip_" + key, leg_w),
                ("FemurMesh", "SM_Dread_Femur_" + key, femur_w),
                ("TibiaMesh", "SM_Dread_Tibia_" + key, tibia_w),
                ("TarsusMesh", "SM_Dread_Foot_" + key, tarsus_w)):
            d = _trs(parent.inverted() @ M2U(bpy.data.objects[obj_name].matrix_world))
            d.update(node=mesh_node, mesh=obj_name)
            leg["parts"].append(d)
        out["legs"].append(leg)

    hull = bpy.data.objects["SM_Dread_Hull"]
    bb = [hull.matrix_world @ Vector(c) for c in hull.bound_box]
    mn = Vector([min(v[i] for v in bb) for i in range(3)])
    mx = Vector([max(v[i] for v in bb) for i in range(3)])
    span = mx - mn
    out["bodyBox"] = {"center": [round(v, 5) for v in P((mn + mx) * 0.5)],
                      "size": [round(abs(span.x), 5), round(abs(span.z), 5),
                               round(abs(span.y), 5)]}

    with open(RIG_JSON, "w", encoding="utf-8") as f:
        json.dump(out, f, indent=1)
    return RIG_JSON



def uv_worldscale():
    """World-scale box UVs. See uv_worldscale.py for why not smart_project."""
    import os, sys
    here = os.path.dirname(os.path.abspath(__file__))
    if here not in sys.path:
        sys.path.insert(0, here)
    import uv_worldscale
    return uv_worldscale.apply_all(UV_TILE)


def finalize():
    import os

    for o in bpy.data.objects:
        for m in [m for m in o.modifiers if m.type == 'BEVEL']:
            o.modifiers.remove(m)
    assert not any(m.type == 'BEVEL' for o in bpy.data.objects for m in o.modifiers)

    uv_stats = uv_worldscale()

    os.makedirs(os.path.dirname(BLEND_PATH), exist_ok=True)
    os.makedirs(os.path.dirname(FBX_PATH), exist_ok=True)
    bpy.ops.wm.save_as_mainfile(filepath=BLEND_PATH)

    bpy.ops.export_scene.fbx(
        filepath=FBX_PATH, use_selection=False, use_visible=False,
        object_types={'MESH'}, use_mesh_modifiers=True, mesh_smooth_type='FACE',
        bake_space_transform=True, add_leaf_bones=False, bake_anim=False,
        apply_scale_options='FBX_SCALE_NONE', global_scale=1.0,
        axis_forward='-Z', axis_up='Y', path_mode='AUTO')

    rig_json = emit_rig_json()

    return {"blend": BLEND_PATH, "fbx": FBX_PATH, "rig": rig_json, "uv": uv_stats,
            "size": os.path.getsize(FBX_PATH), "actions": len(bpy.data.actions)}


if __name__ == "__main__":
    import sys

    # build() 는 이제 출하 모델을 지웁니다(맨 위 경고 참조). 실수로 부르지 못하게 막습니다.
    # 골격을 정말로 다시 뽑아야 할 때만 -- --rebuild 를 붙입니다.
    if "--rebuild" in sys.argv:
        print(build())
        print(finalize())
    else:
        print("build() 는 손으로 다듬은 형상을 지웁니다. 형상은 그대로 두고 다시 내보내려면
"
              "  blender -b Dreadnought.blend --python-expr \"import build_dreadnought as b; b.finalize()\"
"
              "골격까지 처음부터 다시 만들려면 -- --rebuild 를 붙이십시오.")
