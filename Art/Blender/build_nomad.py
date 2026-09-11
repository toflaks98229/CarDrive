# -*- coding: utf-8 -*-
"""
Nomad - a WALKING MEGASTRUCTURE for CarDrive.

WHAT IT IS
  The Strider's tripod at the spine's size. Archigram's Walking City (Ron
  Herron, 1964) is the reference: not a machine that happens to be large, but a
  piece of the same city that got up and left. The megastructure generator
  already argues that size is read as a RATIO TO NEIGHBOURS, never as an
  absolute - so this machine is dimensioned out of that generator's own
  contract rather than out of a scale factor.

  Every one of these is a number SOCKET already fixes in build_megastructure.py:

    hull axle      z = 24.0   = SOCKET.gate      the height of the opening the
                                                 spine leaves for traffic
    knee apex      z = 35.2   = DECK_TOP         the knees ride at deck level,
                                                 so from deck 0 they go past
                                                 you at eye height
    footprint      64.0 x 42.0 = width/2 x bay   it stands on exactly one bay
    members        truss 9.0 - deck 2.2 - parapet 1.4 - duct 2.4 - post 7.0

  It does NOT fit under the gate. The hull axle is at the gate height but the
  reverse knee rises 11 m past it, which is the Strider's whole silhouette and
  not worth giving up. It walks BESIDE the spine, knees breaking the deck line.

DESIGN LANGUAGE - the spine's, not the mech's
  A 34 m box beam is not a beam, it is a wall. At this size the members have to
  be built the way the spine builds them, or the machine reads as a small robot
  that was scaled up:

    femur   pierced deep beam       - the transfer truss, at leg size
    tibia   four chords + X-braces  - the spine's leg bracing, with the same
            + gusset at each cross    offset pair and gusset that core() uses
    hull    truss / deck / parapet  - the machine's chassis IS a bay of deck,
            / service ducts           with a broken parapet you can jump from
    flanks  capsule slots           - some plugged, some empty, same as Habitat

  NO CHAMFER. The mech scripts chamfer 0.03~0.05 to catch a highlight; at
  building size that is below a pixel, which is why hardsurface.Mass has none.
  This machine is at building size. Profiles here are 4-point rectangles.

  The pins, collars and rams stay - they are the only thing that says this is a
  machine and not a bridge, and they are load-bearing, so the no-ornament rule
  keeps them.

MATERIALS ARE THE SPINE'S OWN
  Not a robot palette tuned to look similar - the same four assets the
  megastructure uses (MegaConcrete / MegaSteel / MegaDark / MegaSignal). The
  spine's values are pushed further apart than the robots' on purpose, because
  that is what makes an untextured mass read as huge. Sharing them also means
  the Nomad batches with the spine.

WHY THE KIT IS COPIED AND NOT IMPORTED
  hardsurface.Mass is AXIS-ALIGNED boxes plus one strut. A walker's members
  point wherever the leg points, so it needs the mech kit's oriented lofts.
  The kit below is build_strider.py's, minus the chamfer. The FORMS are
  build_megastructure.py's.

Run:  blender -b --python build_nomad.py
"""
import math

import bpy
from mathutils import Matrix, Vector


def V(x, y, z):
    return Vector((x, y, z))


TAU = math.pi * 2.0

# ----------------------------------------------------------------------------
# materials - the megastructure's four, under the Blender names the FBX carries
# ----------------------------------------------------------------------------
MAT_CONCRETE, MAT_STEEL, MAT_DARK, MAT_SIGNAL = 0, 1, 2, 3
MAT_DEFS = [
    ("M_Nomad_Concrete", (0.610, 0.596, 0.566, 1.0), 0.95, 0.00, None),
    ("M_Nomad_Steel",    (0.155, 0.163, 0.178, 1.0), 0.45, 1.00, None),
    ("M_Nomad_Dark",     (0.030, 0.031, 0.034, 1.0), 0.65, 1.00, None),
    ("M_Nomad_Signal",   (0.760, 0.290, 0.055, 1.0), 0.70, 0.00,
     ((1.0, 0.42, 0.07, 1.0), 2.2)),
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
# kit - oriented lofts. build_strider.py's, with the chamfer taken out.
# ----------------------------------------------------------------------------
class Builder(object):
    """Loose parts, each tagged with the bone that drives it and the object it
    is baked into."""

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


def prof_rect(hx, hy):
    """Four points. No chamfer - see the header."""
    return [(hx, hy), (-hx, hy), (-hx, -hy), (hx, -hy)]


def prof_ngon(n, r, phase=0.0):
    return [(math.cos(phase + TAU * i / n) * r,
             math.sin(phase + TAU * i / n) * r) for i in range(n)]


def solid(M, stops):
    secs = [[M @ Vector((a, b, z)) for (a, b) in prof] for z, prof in stops]
    return loft(secs)


def frame(zdir, xhint, origin=V(0, 0, 0)):
    z = Vector(zdir).normalized()
    x = Vector(xhint) - z * Vector(xhint).dot(z)
    if x.length < 1e-6:
        alt = V(0, 0, 1) if abs(z.z) < 0.9 else V(1, 0, 0)
        x = alt - z * alt.dot(z)
    x.normalize()
    y = z.cross(x)
    M = Matrix(((x.x, y.x, z.x, origin.x),
                (x.y, y.y, z.y, origin.y),
                (x.z, y.z, z.z, origin.z),
                (0.0, 0.0, 0.0, 1.0)))
    return M


def beam(p0, p1, xaxis):
    d = p1 - p0
    return frame(d, xaxis, p0), d.length


def box(center, size, zdir=V(0, 0, 1), xhint=V(1, 0, 0)):
    """Oriented rectangular block. size is (across x, across y, along zdir)."""
    M = frame(zdir, xhint, center)
    hx, hy, hz = size[0] * 0.5, size[1] * 0.5, size[2] * 0.5
    return solid(M, [(-hz, prof_rect(hx, hy)), (hz, prof_rect(hx, hy))])


def bbeam(p0, p1, s0, s1, xaxis=V(1, 0, 0)):
    M, L = beam(p0, p1, xaxis)
    return solid(M, [(0.0, prof_rect(s0[0], s0[1])),
                     (L, prof_rect(s1[0], s1[1]))])


def collar(M, z0, z1, hx, hy):
    return solid(M, [(z0, prof_rect(hx, hy)), (z1, prof_rect(hx, hy))])


def prism(p0, p1, r0, r1, n=12, xaxis=V(1, 0, 0), phase=0.0):
    M, L = beam(p0, p1, xaxis)
    return solid(M, [(0.0, prof_ngon(n, r0, phase)),
                     (L, prof_ngon(n, r1, phase))])


# ----------------------------------------------------------------------------
# megastructure forms, in the oriented kit
# ----------------------------------------------------------------------------
def pierced(b, p0, p1, s0, s1, holes, xaxis, group, mat=MAT_CONCRETE,
            rim=0.30, hole=0.62):
    """
    A deep beam with lightening holes: the spine's transfer truss at leg size.

    Built the way Mass.pierced builds it - the webs BETWEEN the holes are put
    down, not the holes. The webs only occupy the hole's height and the rims
    take the rest, so the faces that meet look opposite ways instead of
    co-planar. That is what stopped the spine's trusses flickering from below.
    """
    M, L = beam(p0, p1, xaxis)
    pitch = L / holes
    web = pitch * (1.0 - hole)

    def sec(t):
        return (s0[0] + (s1[0] - s0[0]) * t, s0[1] + (s1[1] - s0[1]) * t)

    for i in range(holes + 1):
        z = i * pitch
        t = z / L
        hx, hy = sec(t)
        hh = hy * (1.0 - rim)
        b.add(*solid(M, [(z - web * 0.5, prof_rect(hx, hh)),
                         (z + web * 0.5, prof_rect(hx, hh))]),
              group=group, mat=mat)

    for sign in (-1.0, 1.0):
        secs = []
        for i in range(holes + 1):
            z = i * pitch
            hx, hy = sec(z / L)
            hh = hy * (1.0 - rim)
            c = sign * (hy + hh) * 0.5
            secs.append((z, [(hx, c + (hy - hh) * 0.5), (-hx, c + (hy - hh) * 0.5),
                             (-hx, c - (hy - hh) * 0.5), (hx, c - (hy - hh) * 0.5)]))
        b.add(*solid(M, secs), group=group, mat=mat)


def lattice(b, p0, p1, s0, s1, chord, bays, xaxis, group,
            mat_chord=MAT_CONCRETE, mat_brace=MAT_STEEL, mat_node=MAT_DARK):
    """
    Four chords, X-braces on the two long faces, a gusset where they cross.

    This is core()'s leg bracing, member for member. Two things are copied on
    purpose: the pair of braces is OFFSET along the member instead of sharing a
    plane (co-planar pairs were what made 2,204 m2 of the spine flicker), and
    the crossing carries a gusset block, because a diagonal that just passes
    through another diagonal does not read as a joint.
    """
    M, L = beam(p0, p1, xaxis)
    h = chord * 0.5

    def sec(t):
        return (s0[0] + (s1[0] - s0[0]) * t, s0[1] + (s1[1] - s0[1]) * t)

    # ---- the four chords, tapering along the member -------------------------
    for sx in (-1.0, 1.0):
        for sy in (-1.0, 1.0):
            secs = []
            for i in range(bays + 1):
                t = i / float(bays)
                hx, hy = sec(t)
                cx, cy = sx * (hx - h), sy * (hy - h)
                secs.append((t * L, [(cx + h, cy + h), (cx - h, cy + h),
                                     (cx - h, cy - h), (cx + h, cy - h)]))
            b.add(*solid(M, secs), group=group, mat=mat_chord)

    br = chord * 0.62
    for i in range(bays):
        z0, z1 = i * L / bays, (i + 1) * L / bays
        hx0, hy0 = sec(i / float(bays))
        hx1, hy1 = sec((i + 1) / float(bays))

        # ---- ties: a closed ring at every bay line --------------------------
        for z, hx, hy in ((z1, hx1, hy1),):
            for sy in (-1.0, 1.0):
                b.add(*bbeam(M @ V(-(hx - h), sy * (hy - h), z),
                             M @ V(hx - h, sy * (hy - h), z),
                             (br, br), (br, br), M.to_3x3() @ V(0, 0, 1)),
                      group=group, mat=mat_brace)

        # ---- X on the two long faces, the pair offset across the member -----
        for sy, off in ((-1.0, -1.0), (1.0, 1.0)):
            y0, y1 = sy * (hy0 - h), sy * (hy1 - h)
            d = off * chord * 0.55
            a1 = M @ V(-(hx0 - h), y0 + d, z0)
            b1 = M @ V(hx1 - h, y1 + d, z1)
            a2 = M @ V(hx0 - h, y0 - d, z0)
            b2 = M @ V(-(hx1 - h), y1 - d, z1)
            b.add(*bbeam(a1, b1, (br, br), (br, br), M.to_3x3() @ V(0, 1, 0)),
                  group=group, mat=mat_brace)
            b.add(*bbeam(a2, b2, (br, br), (br, br), M.to_3x3() @ V(0, 1, 0)),
                  group=group, mat=mat_brace)
            mid = (a1 + b1 + a2 + b2) * 0.25
            b.add(*box(mid, (chord * 1.05, chord * 1.05, chord * 1.05),
                       zdir=M.to_3x3() @ V(0, 0, 1), xhint=M.to_3x3() @ V(1, 0, 0)),
                  group=group, mat=mat_node)


def duct(b, p0, p1, size, group, mat=MAT_STEEL):
    """Service duct. Square section, banded - the spine runs these under the
    lowest deck only, so they read as one system instead of decoration."""
    ax = V(0, 0, 1) if abs((p1 - p0).normalized().z) < 0.9 else V(1, 0, 0)
    M, L = beam(p0, p1, ax)
    h = size * 0.5
    b.add(*solid(M, [(0.0, prof_rect(h, h)), (L, prof_rect(h, h))]),
          group=group, mat=mat)
    for i in range(1, 5):
        z = L * i / 5.0
        b.add(*collar(M, z - size * 0.10, z + size * 0.10, h * 1.16, h * 1.16),
              group=group, mat=MAT_DARK)


def capsule(b, centre, size, group, plugged=True, face=1.0):
    """
    A dwelling capsule in a socket. Kurokawa's Nakagin unit, the same one the
    spine plugs into its flanks - the rule of this world is that one factory
    makes a part and it goes wherever there is a socket.

    An EMPTY socket says more than a full one: it is the room that has not been
    delivered yet, which is the whole point of a framework that outlives its
    units.
    """
    sx, sy, sz = size
    for side in (-1.0, 1.0):
        b.add(*box(centre + V(0, 0, side * (sz * 0.5 + 0.35)),
                   (sx + 1.4, sy + 1.4, 0.7)), group=group, mat=MAT_STEEL)
    for side in (-1.0, 1.0):
        b.add(*box(centre + V(0, side * (sy * 0.5 + 0.35), 0),
                   (sx + 1.4, 0.7, sz)), group=group, mat=MAT_STEEL)
    if not plugged:
        # the back wall of an empty socket. An undelivered room says more about
        # a framework that outlives its units than a full one does.
        b.add(*box(centre + V(-face * (sx * 0.5 - 0.35), 0, 0), (0.7, sy, sz)),
              group=group, mat=MAT_DARK)
        return
    b.add(*box(centre, (sx, sy, sz)), group=group, mat=MAT_CONCRETE)
    # the round window is the one thing a capsule is allowed to have
    b.add(*prism(centre + V(face * (sx * 0.5 - 0.1), 0, sz * 0.12),
                 centre + V(face * (sx * 0.5 + 0.5), 0, sz * 0.12),
                 sz * 0.22, sz * 0.22, 14, xaxis=V(0, 1, 0)),
          group=group, mat=MAT_DARK)


# ----------------------------------------------------------------------------
# dimensions - every one of these comes out of SOCKET in build_megastructure.py
# ----------------------------------------------------------------------------
GATE = 24.0        # SOCKET["gate"]      - the opening the spine leaves
DECK_TOP = 35.2    # DECK_TOP            - top of deck 0
BAY = 42.0         # SOCKET["bay"]
WIDTH = 128.0      # SOCKET["width"]
TRUSS = 9.0        # SOCKET["truss"]
DECK = 2.2         # SOCKET["deck"]
PARAPET = 1.4      # SOCKET["parapet"]
DUCT = 2.4         # SOCKET["duct"]
POST = 7.0         # SOCKET["post"]

BODY_Z = GATE
KNEE_Z = DECK_TOP
ANKLE_Z = 3.0

# The tripod is the Strider's: two forward, one behind, knees above the hull.
# Footprint is one bay deep and half a spine wide.
LEGS = {
    "L": dict(hip=V(6.0, -2.2, BODY_Z), knee=V(22.4, -5.4, KNEE_Z),
              ankle=V(32.0, -9.0, ANKLE_Z), tip=V(32.0, -9.0, 0.0)),
    "R": dict(hip=V(-6.0, -2.2, BODY_Z), knee=V(-22.4, -5.4, KNEE_Z),
              ankle=V(-32.0, -9.0, ANKLE_Z), tip=V(-32.0, -9.0, 0.0)),
    "B": dict(hip=V(0.0, 7.6, BODY_Z), knee=V(0.0, 23.2, KNEE_Z),
              ankle=V(0.0, 33.0, ANKLE_Z), tip=V(0.0, 33.0, 0.0)),
}
for _L in LEGS.values():
    _fd = (_L["knee"] - _L["hip"]).normalized()
    _td = (_L["ankle"] - _L["knee"]).normalized()
    _L["fd"], _L["td"] = _fd, _td
    _L["axis"] = _fd.cross(_td).normalized()      # hinge axis of the whole leg

HULL_C = V(0.0, 2.0, BODY_Z)                      # transfer truss centre
HULL = (22.0, 30.0, TRUSS)
DECK_C = V(0.0, 2.0, BODY_Z + TRUSS * 0.5 + DECK * 0.5)
DECK_S = (24.0, 32.0, DECK)
DECK_Z = DECK_C.z + DECK * 0.5                    # walking surface

HEAD_C = V(0.0, -17.4, BODY_Z - 0.6)              # bridge cabin
NECK_A = V(0.0, -11.0, BODY_Z + 0.4)

CRANE_YAW = V(0.0, 7.0, DECK_Z)                   # slew ring on the deck
CRANE_PITCH = V(0.0, 7.0, DECK_Z + 3.6)           # luffing axle
JIB_LEN = 26.0
MUZZLE = CRANE_PITCH + V(0.0, -JIB_LEN, 1.2)      # the hook

SPLIT_PARTS = True


# ----------------------------------------------------------------------------
# hull - a bay of deck that walks
# ----------------------------------------------------------------------------
def build_hull(b):
    hx, hy, hz = HULL[0] * 0.5, HULL[1] * 0.5, HULL[2] * 0.5

    b.piece = "Truss"
    # the chassis is a transfer truss, pierced the long way like the spine's
    for sx in (-1.0, 1.0):
        pierced(b, HULL_C + V(sx * (hx - 1.8), -hy, 0.0),
                HULL_C + V(sx * (hx - 1.8), hy, 0.0),
                (3.6, hz), (3.6, hz), 5, V(1, 0, 0), "B_Body")
    # cross girders tying the two sides, one over each hip line
    for y in (HULL_C.y - hy + 3.0, -2.2, 7.6, HULL_C.y + hy - 3.0):
        b.add(*box(V(0.0, y, BODY_Z), (HULL[0] - 3.0, 3.2, TRUSS * 0.72)),
              group="B_Body", mat=MAT_CONCRETE)
    # hip pads - flush, no overhang
    for key, Lg in LEGS.items():
        h = Lg["hip"]
        b.add(*box(V(h.x, h.y, BODY_Z), (7.4, 6.6, TRUSS * 0.9)),
              group="B_Body", mat=MAT_CONCRETE)

    b.piece = "Deck"
    b.add(*box(DECK_C, DECK_S), group="B_Body", mat=MAT_CONCRETE)
    # parapet, broken on the left - the spine breaks one too, so the deck can
    # be jumped from instead of only looked at
    dx, dy = DECK_S[0] * 0.5, DECK_S[1] * 0.5
    pz = DECK_Z + PARAPET * 0.5
    b.add(*box(V(0.0, DECK_C.y + dy - 0.45, pz), (DECK_S[0], 0.9, PARAPET)),
          group="B_Body", mat=MAT_STEEL)
    b.add(*box(V(0.0, DECK_C.y - dy + 0.45, pz), (DECK_S[0], 0.9, PARAPET)),
          group="B_Body", mat=MAT_STEEL)
    b.add(*box(V(dx - 0.45, DECK_C.y, pz), (0.9, DECK_S[1] - 1.8, PARAPET)),
          group="B_Body", mat=MAT_STEEL)
    for sy in (-1.0, 1.0):
        b.add(*box(V(-dx + 0.45, DECK_C.y + sy * (dy * 0.5 + 2.2), pz),
                   (0.9, DECK_S[1] * 0.5 - 4.4, PARAPET)),
              group="B_Body", mat=MAT_STEEL)
    # the gap gets the only saturated thing on the machine
    for sy in (-1.0, 1.0):
        b.add(*box(V(-dx + 0.45, DECK_C.y + sy * 4.4, pz + 0.1),
                   (1.0, 0.5, PARAPET * 0.8)), group="B_Body", mat=MAT_SIGNAL)

    b.piece = "Duct"
    for x in (-8.0, 8.0):
        duct(b, V(x, HULL_C.y - hy, BODY_Z - hz - DUCT * 0.6),
             V(x, HULL_C.y + hy, BODY_Z - hz - DUCT * 0.6), DUCT, "B_Body")
    duct(b, V(0.0, HULL_C.y - hy, BODY_Z - hz - DUCT * 0.45),
         V(0.0, HULL_C.y + hy, BODY_Z - hz - DUCT * 0.45), DUCT * 1.5, "B_Body")
    # lamps under the belly, to be seen from below
    for y in (-8.0, 0.0, 8.0, 16.0):
        b.add(*box(V(0.0, y, BODY_Z - hz - DUCT * 1.15), (2.6, 0.9, 0.35)),
              group="B_Body", mat=MAT_SIGNAL)

    b.piece = "Capsule"
    # flanks. Two plugged, one socket left open on each side.
    for sx in (-1.0, 1.0):
        for i, y in enumerate((-9.0, 0.0, 9.0)):
            capsule(b, V(sx * (hx + 4.4), y + 2.0, BODY_Z - 0.4),
                    (5.6, 7.6, 5.4), "B_Body", face=sx,
                    plugged=not (i == 1 and sx > 0))

    b.piece = None


def build_bridge(b):
    """The head is the bridge: a capsule hung off the front of the truss, at
    hull height, not perched on the roof."""
    b.piece = "Truss"
    b.add(*box(V(0.0, NECK_A.y - 1.6, BODY_Z + 0.2), (7.0, 5.2, 5.6)),
          group="B_Body", mat=MAT_CONCRETE)
    b.piece = "Head"
    b.add(*box(HEAD_C, (8.6, 6.4, 5.6)), group="B_Head", mat=MAT_CONCRETE)
    b.add(*box(HEAD_C + V(0.0, -3.4, 0.0), (7.4, 1.0, 4.4)),
          group="B_Head", mat=MAT_STEEL)
    # sensor slit and one lamp beside it
    b.add(*box(HEAD_C + V(0.0, -3.9, 0.8), (5.8, 0.5, 1.1)),
          group="B_Head", mat=MAT_DARK)
    b.add(*box(HEAD_C + V(2.4, -3.9, -0.9), (0.8, 0.5, 0.8)),
          group="B_Head", mat=MAT_SIGNAL)
    # the stalk back to the truss
    b.add(*prism(HEAD_C + V(0, 2.8, 0), NECK_A + V(0, -1.0, -0.6),
                 1.5, 1.9, 12, xaxis=V(1, 0, 0)),
          group="B_Head", mat=MAT_DARK)
    b.piece = None


def build_crane(b):
    """
    The deck turret is a CRANE, not a gun.

    Archigram's Plug-In City drew the crane as the thing that made a framework
    a framework - it is what plugs a capsule in and pulls it out. A walking
    piece of that city carries its own. It is rigged exactly like the Strider's
    gun (slew, luff, muzzle at the hook), so a weapon can bolt to the same
    mount if this one ever needs to be armed.
    """
    b.piece = "Gantry"
    b.add(*prism(CRANE_YAW + V(0, 0, -0.6), CRANE_YAW + V(0, 0, 0.9),
                 4.6, 4.2, 16, xaxis=V(1, 0, 0)),
          group="B_CraneYaw", mat=MAT_DARK)
    b.add(*box(CRANE_YAW + V(0, 0, 2.0), (7.6, 8.6, 3.2)),
          group="B_CraneYaw", mat=MAT_CONCRETE)
    for sx in (-1.0, 1.0):
        b.add(*box(CRANE_PITCH + V(sx * 3.1, 0.4, 0.4), (1.4, 4.6, 4.4)),
              group="B_CraneYaw", mat=MAT_STEEL)
    # counter-jib and its block, so the reach is paid for
    b.add(*bbeam(CRANE_PITCH + V(0, 1.6, 0.4), CRANE_PITCH + V(0, 9.4, 1.0),
                 (1.5, 1.5), (1.3, 1.3)), group="B_CraneYaw", mat=MAT_STEEL)
    b.add(*box(CRANE_PITCH + V(0, 9.8, 0.2), (4.2, 3.4, 3.0)),
          group="B_CraneYaw", mat=MAT_DARK)

    b.piece = "Crane"
    tip = CRANE_PITCH + V(0, -JIB_LEN, 1.2)
    lattice(b, CRANE_PITCH + V(0, -2.0, 0.2), tip, (1.9, 1.7), (1.0, 0.9),
            0.7, 6, V(1, 0, 0), "B_CranePitch",
            mat_chord=MAT_STEEL, mat_brace=MAT_STEEL, mat_node=MAT_DARK)
    b.add(*prism(CRANE_PITCH + V(-3.4, 0, 0), CRANE_PITCH + V(3.4, 0, 0),
                 1.1, 1.1, 12, xaxis=V(0, 1, 0)),
          group="B_CranePitch", mat=MAT_DARK)
    # hoist rope and the block on it - the hook is the muzzle node
    b.add(*bbeam(tip, tip + V(0, 0, -6.0), (0.18, 0.18), (0.18, 0.18)),
          group="B_CranePitch", mat=MAT_DARK)
    b.add(*box(tip + V(0, 0, -6.6), (1.8, 1.6, 1.4)),
          group="B_CranePitch", mat=MAT_SIGNAL)
    b.piece = None


# ----------------------------------------------------------------------------
# leg - hip pin, pierced femur, latticed tibia, footplate
# ----------------------------------------------------------------------------
def build_leg(b, key, Lg):
    hip, knee, ankle, tip = Lg["hip"], Lg["knee"], Lg["ankle"], Lg["tip"]
    fd, td, ax = Lg["fd"], Lg["td"], Lg["axis"]
    up = ax.cross(fd).normalized()

    g_hip = "B_Hip_" + key
    g_fem = "B_Femur_" + key
    g_tib = "B_Tibia_" + key
    g_ft = "B_Foot_" + key

    b.piece = "Hip_" + key
    b.add(*box(hip + fd * 1.6, (6.2, 5.8, 6.6), zdir=fd, xhint=ax),
          group=g_hip, mat=MAT_CONCRETE)
    b.add(*prism(hip - ax * (POST * 0.5), hip + ax * (POST * 0.5),
                 1.6, 1.6, 14, xaxis=fd), group=g_hip, mat=MAT_DARK)
    b.add(*collar(*[frame(fd, ax, hip)], 0.4, 1.2, 3.5, 3.3),
          group=g_hip, mat=MAT_STEEL)

    b.piece = "Femur_" + key
    fa, fb = hip + fd * 3.2, knee - fd * 3.4
    pierced(b, fa, fb, (2.6, 2.3), (2.2, 1.9), 4, ax, g_fem)
    Mf, FL = beam(fa, fb, ax)
    b.add(*collar(Mf, 0.0, 1.3, 2.9, 2.6), group=g_fem, mat=MAT_STEEL)
    b.add(*collar(Mf, FL - 1.3, FL, 2.5, 2.2), group=g_fem, mat=MAT_STEEL)
    # one hydraulic ram down the front, the only mech part of the member
    b.add(*bbeam(fa + fd * 0.4 - up * 3.0, fa + fd * (FL * 0.62) - up * 2.6,
                 (0.75, 0.75), (0.75, 0.75), ax), group=g_fem, mat=MAT_STEEL)
    b.add(*bbeam(fa + fd * (FL * 0.58) - up * 2.6, knee - fd * 2.0 - up * 1.9,
                 (0.45, 0.45), (0.45, 0.45), ax), group=g_fem, mat=MAT_DARK)
    b.add(*prism(knee - ax * 2.9, knee + ax * 2.9, 1.15, 1.15, 14, xaxis=fd),
          group=g_fem, mat=MAT_DARK)

    b.piece = "Tibia_" + key
    b.add(*box(knee + td * 2.0, (5.0, 4.6, 5.0), zdir=td, xhint=ax),
          group=g_tib, mat=MAT_CONCRETE)
    ta, tb = knee + td * 4.4, ankle - td * 2.2
    lattice(b, ta, tb, (2.15, 1.95), (1.25, 1.15), 0.82, 6, ax, g_tib)
    Mt, TL = beam(ta, tb, ax)
    b.add(*collar(Mt, 0.0, 1.1, 2.5, 2.3), group=g_tib, mat=MAT_STEEL)
    b.add(*collar(Mt, TL - 1.1, TL, 1.6, 1.5), group=g_tib, mat=MAT_STEEL)

    b.piece = "Foot_" + key
    fod = (tip - ankle).normalized()
    perp = ax.cross(fod).normalized()
    b.add(*box(ankle, (4.4, 4.2, 3.8), zdir=td, xhint=ax),
          group=g_ft, mat=MAT_STEEL)
    b.add(*prism(ankle - ax * 2.2, ankle + ax * 2.2, 0.95, 0.95, 12, xaxis=td),
          group=g_ft, mat=MAT_DARK)
    b.add(*box(ankle + fod * 1.6, (9.4, 10.4, 1.7), zdir=fod, xhint=ax),
          group=g_ft, mat=MAT_CONCRETE)
    for sx in (1.0, -1.0):
        for sy in (1.0, -1.0):
            b.add(*box(ankle + fod * 2.5 + ax * (sx * 3.2) + perp * (sy * 3.6),
                       (2.3, 2.3, 1.2), zdir=fod, xhint=ax),
                  group=g_ft, mat=MAT_DARK)
    b.piece = None


# ----------------------------------------------------------------------------
# bake
# ----------------------------------------------------------------------------
def make_object(name, parts, mats):
    verts, faces, slots = [], [], []
    for p in parts:
        base = len(verts)
        verts += [v.copy() for v in p["verts"]]
        faces += [tuple(base + i for i in f) for f in p["faces"]]
        slots += [p["mat"]] * len(p["faces"])

    mesh = bpy.data.meshes.new(name)
    mesh.from_pydata([tuple(v) for v in verts], [], faces)
    mesh.update()
    for m in mats:
        mesh.materials.append(m)
    for poly, index in zip(mesh.polygons, slots):
        poly.material_index = index
    for poly in mesh.polygons:
        poly.use_smooth = False

    obj = bpy.data.objects.new(name, mesh)
    bpy.context.scene.collection.objects.link(obj)
    return obj


def _mode(obj, mode):
    """mode_set through an explicit override.

    Run headless the plain operator is fine, but called from the MCP add-on the
    context has no active object and the poll fails. Overriding costs nothing
    and makes this script runnable from either side, which is what lets the
    shape be iterated without a Blender restart per change.
    """
    bpy.context.view_layer.objects.active = obj
    obj.select_set(True)
    with bpy.context.temp_override(object=obj, active_object=obj,
                                   selected_objects=[obj],
                                   selected_editable_objects=[obj]):
        bpy.ops.object.mode_set(mode=mode)


def build_rig():
    arm = bpy.data.armatures.new("ARM_Nomad")
    rig = bpy.data.objects.new("Nomad_Rig", arm)
    bpy.context.scene.collection.objects.link(rig)
    _mode(rig, 'EDIT')
    eb = arm.edit_bones

    specs = [
        ("Root", V(0, 0, 0), V(0, -6.0, 0), None, False, False, V(0, 0, 1)),
        ("B_Body", V(0, 4.0, BODY_Z), V(0, -4.0, BODY_Z), "Root", False, True, V(0, 0, 1)),
        ("B_Head", HEAD_C + V(0, 3.2, 0), HEAD_C + V(0, -3.2, 0), "B_Body", False, True, V(0, 0, 1)),
        ("B_CraneYaw", CRANE_YAW, CRANE_PITCH, "B_Body", False, True, V(0, -1, 0)),
        ("B_CranePitch", CRANE_PITCH, CRANE_PITCH + V(0, -6.0, 0), "B_CraneYaw", True, True, V(0, 0, 1)),
        ("B_Muzzle", MUZZLE, MUZZLE + V(0, -3.0, 0), "B_CranePitch", False, False, V(0, 0, 1)),
    ]
    for key, Lg in LEGS.items():
        hip, knee, ankle, tip = Lg["hip"], Lg["knee"], Lg["ankle"], Lg["tip"]
        fd, td, ax = Lg["fd"], Lg["td"], Lg["axis"]
        up = ax.cross(fd).normalized()
        upt = ax.cross(td).normalized()
        upf = ax.cross((tip - ankle).normalized()).normalized()
        specs += [
            ("B_Hip_" + key, hip, hip + fd * 3.2, "B_Body", False, True, up),
            ("B_Femur_" + key, hip + fd * 3.2, knee, "B_Hip_" + key, True, True, up),
            ("B_Tibia_" + key, knee, ankle, "B_Femur_" + key, True, True, upt),
            ("B_Foot_" + key, ankle, tip, "B_Tibia_" + key, True, True, upf),
            ("B_IK_" + key, ankle, ankle + V(0, -4.0, 0), "Root", False, False, V(0, 0, 1)),
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

    _mode(rig, 'POSE')
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
    _mode(rig, 'OBJECT')
    return rig


def bone_parent(obj, rig, bone_name):
    keep = obj.matrix_world.copy()
    obj.parent = rig
    obj.parent_type = 'BONE'
    obj.parent_bone = bone_name
    bpy.context.view_layer.update()
    obj.matrix_world = keep


PIECE_BONE = {"Truss": "B_Body", "Deck": "B_Body", "Duct": "B_Body",
              "Capsule": "B_Body", "Head": "B_Head",
              "Gantry": "B_CraneYaw", "Crane": "B_CranePitch"}


def wipe():
    # bpy.context.object is missing when this runs from the add-on's context,
    # so ask the view layer instead of the context shortcut.
    active = bpy.context.view_layer.objects.active
    if active is not None and active.mode != 'OBJECT':
        bpy.ops.object.mode_set(mode='OBJECT')
    for o in list(bpy.data.objects):
        bpy.data.objects.remove(o, do_unlink=True)
    for block in (bpy.data.meshes, bpy.data.armatures):
        for d in list(block):
            if d.users == 0:
                block.remove(d)


def build():
    wipe()
    mats = ensure_materials()

    b = Builder()
    build_hull(b)
    build_bridge(b)
    build_crane(b)
    for key, Lg in LEGS.items():
        build_leg(b, key, Lg)

    rig = build_rig()

    order = []
    for p in b.parts:
        if p["piece"] not in order:
            order.append(p["piece"])

    made = {}
    for piece in order:
        parts = [p for p in b.parts if p["piece"] == piece]
        obj = make_object("SM_Nomad_" + piece, parts, mats)
        bone = PIECE_BONE.get(piece) or parts[0]["group"]
        bone_parent(obj, rig, bone)
        made[piece] = obj

    for key in ("Muzzle",):
        e = bpy.data.objects.new("SKT_" + key, None)
        bpy.context.scene.collection.objects.link(e)
        e.empty_display_size = 2.0
        e.matrix_world = Matrix.Translation(MUZZLE)
        bone_parent(e, rig, "B_CranePitch")

    return {"objects": len(made),
            "tris": sum(sum(len(p.vertices) - 2 for p in o.data.polygons)
                        for o in made.values())}


# ----------------------------------------------------------------------------
# export - same contract as the Dreadnought. bake_space_transform MUST be on:
# the Unity importer pulls meshes out on their own, so the Z-up -> Y-up
# conversion has to ride on the mesh data, not on the object transforms.
# ----------------------------------------------------------------------------
BLEND_PATH = r"E:\GamePJ\CarDrive\Art\Blender\Nomad.blend"
FBX_PATH = r"E:\GamePJ\CarDrive\Assets\_Project\04.Art\02.Models\Robot\SM_Nomad.fbx"
RIG_JSON = "E:/GamePJ/CarDrive/Assets/_Project/04.Art/02.Models/Robot/SM_Nomad.rig.json"
UV_TILE = 1.0

CM = Matrix(((-1, 0, 0, 0), (0, 0, 1, 0), (0, -1, 0, 0), (0, 0, 0, 1)))

LEG_NODES = {"L": "Leg_FrontLeft", "R": "Leg_FrontRight", "B": "Leg_Rear"}
BODY_PARTS = [
    ("Chassis", "SM_Nomad_Truss", True),
    ("Deck", "SM_Nomad_Deck", True),
    ("Duct", "SM_Nomad_Duct", True),
    ("Capsule", "SM_Nomad_Capsule", True),
    ("Gantry", "SM_Nomad_Gantry", True),
    ("Crane", "SM_Nomad_Crane", True),
    ("Head", "SM_Nomad_Head", True),
]


def _look(fwd, up):
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
    local +Y. Unity is handed numbers, not this armature, and this is the one
    place they are derived."""
    import json

    rig = bpy.data.objects["Nomad_Rig"]
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
           "meshPrefix": "SM_Nomad_",
           "bodyLocal": _trs(body_world), "bodyParts": [], "legs": []}

    head_pivot = Vector(bones["B_Head"].head_local)
    yaw_p = (body_world.inverted() @ Matrix.Translation(P(CRANE_YAW))).translation
    pitch_p = (Matrix.Translation(P(CRANE_YAW)).inverted()
               @ Matrix.Translation(P(CRANE_PITCH))).translation

    aim = [
        dict(node="Crane_Yaw", parent="Body",
             pos=[round(v, 5) for v in yaw_p],
             axis=[0.0, 1.0, 0.0], range=[-180.0, 180.0]),
        dict(node="Crane_Pitch", parent="Crane_Yaw",
             pos=[round(v, 5) for v in pitch_p],
             axis=[1.0, 0.0, 0.0], range=[-55.0, 20.0]),
        dict(node="Head_Yaw", parent="Body",
             pos=[round(v, 5) for v in
                  (body_world.inverted() @ Matrix.Translation(P(head_pivot))).translation],
             axis=[0.0, 1.0, 0.0], range=[-70.0, 70.0]),
        dict(node="Head_Pitch", parent="Head_Yaw",
             pos=[0.0, 0.0, 0.0], axis=[1.0, 0.0, 0.0], range=[-20.0, 25.0]),
    ]
    out["aim"] = aim
    out["turrets"] = [
        dict(name="Crane", yaw="Crane_Yaw", pitch="Crane_Pitch", muzzle="Muzzle_Main"),
        dict(name="Head", yaw="Head_Yaw", pitch="Head_Pitch", muzzle=""),
    ]
    out["muzzles"] = [
        dict(node="Muzzle_Main", parent="Crane_Pitch",
             pos=[round(v, 5) for v in
                  (Matrix.Translation(P(CRANE_PITCH)).inverted()
                   @ Matrix.Translation(P(MUZZLE))).translation])]

    ATTACH = {"Gantry": "Crane_Yaw", "Crane": "Crane_Pitch", "Head": "Head_Pitch"}
    AIM_WORLD = {"Crane_Yaw": Matrix.Translation(P(CRANE_YAW)),
                 "Crane_Pitch": Matrix.Translation(P(CRANE_PITCH)),
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
                ("HipMesh", "SM_Nomad_Hip_" + key, leg_w),
                ("FemurMesh", "SM_Nomad_Femur_" + key, femur_w),
                ("TibiaMesh", "SM_Nomad_Tibia_" + key, tibia_w),
                ("TarsusMesh", "SM_Nomad_Foot_" + key, tarsus_w)):
            d = _trs(parent.inverted() @ M2U(bpy.data.objects[obj_name].matrix_world))
            d.update(node=mesh_node, mesh=obj_name)
            leg["parts"].append(d)
        out["legs"].append(leg)

    hull = bpy.data.objects["SM_Nomad_Truss"]
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
    print(build())
    print(finalize())
