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

  2026-09-10 added a second layer the code does not carry: the mass pass. Every
  part was rescaled and re-placed in world space against Castraferrum front art
  (Dreadnought_MassAnalysis.md). The SKELETON constants below were updated to
  match what the armature now is - BODY_Z, LEGS, ARM_PIVOT, MUZZLE, SHOULDER and
  the part centres - because emit_rig_json() reads them and Unity would otherwise
  get a rig that disagrees with the mesh. The SECTION SIZES inside build_torso()
  and build_leg() were NOT updated; they still describe the slim first build.

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

        # 파츠마다의 <b>기준점과 프레임</b>입니다. fit() 이 여기서 잽니다.
        # 프레임이 None 이면 월드 축이고, 팔다리는 뼈의 프레임을 넣습니다.
        self.frames = {}

        # 손으로 걷어냈던 것을 다시 붙일지 여부입니다. build() 가 패턴에서 넣습니다.
        self.ornament = False

        # 발판 두께입니다. fit() 이 발 전체를 다시 재므로 여기 값은 <b>맞춰지기 전</b>입니다.
        self.foot_plate = 0.30

    def frame_for(self, piece, anchor, basis=None):
        """이 파츠를 어느 점 · 어느 축으로 잴지 적어 둡니다."""
        self.frames[piece] = (anchor.copy(), basis)

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
# 2026-09-10: the hip axle came down from 2.10 to 1.30 and the leg segments were
# halved. Measured against Castraferrum front art scaled to our height, the old
# skeleton put the hip at 59 % of total height where the pattern puts it at 33 %,
# so half the machine was open air between two posts. See Dreadnought_MassAnalysis.md.
BODY_Z = 1.30

# The knee folds BACKWARD (+Y). Digitigrade, like the pattern's plastic kit.
LEGS = {}
for _key, _sx in (("L", 1.0), ("R", -1.0)):
    LEGS[_key] = dict(
        root=V(_sx * 0.60, -0.02, BODY_Z),      # inboard end of the hip pin
        hip=V(_sx * 0.80, -0.02, BODY_Z),       # femur head == the Unity hip
        knee=V(_sx * 0.80, 0.412, 0.856),
        ankle=V(_sx * 0.80, 0.00, 0.34),
        tip=V(_sx * 0.80, 0.00, 0.00),
        axis=V(1, 0, 0),                        # both legs hinge about world X
        sx=_sx,
    )

TORSO_C = V(0.0, 0.05, 2.43)                    # armoured torso centre
SARC_C = V(0.0, -0.70, 2.345)                   # sarcophagus front plate
# The sensor head sits on the FRONT of the torso, resting on the sarcophagus
# rim and overhanging it like a brow - not perched on the roof.
# 머리 피벗의 z 는 <b>손으로 다듬은 머리의 수직 중심</b>입니다(2.839~3.29 의 가운데).
# 예전 값 3.34 는 그 머리의 꼭대기보다 위라, 부앙이 머리를 끄덕이는 것이 아니라
# 진자처럼 통째로 흔들었습니다.
HEAD_C = V(0.0, -0.84, 3.0645)
REACTOR_C = V(0.0, 0.86, 2.58)                  # thermic reactor, rear
SHOULDER = {"L": V(0.98, 0.05, 2.55), "R": V(-0.98, 0.05, 2.55)}
ARM_PIVOT = {"L": V(1.18, 0.05, 2.55), "R": V(-1.18, 0.05, 2.55)}
MUZZLE = {"L": V(1.18, -1.30, 2.17), "R": V(-1.18, -1.62, 2.55)}

ARMS = {"L": "SM_Dread_Arm_Fist", "R": "SM_Dread_Arm_Autocannon"}

SPLIT_PARTS = True


def fit(b, piece, target):
    """레시피가 낸 파츠를 재서 <c>target</c> 치수로 옮깁니다.

    파츠 안의 <b>비례는 건드리지 않습니다.</b> 전체를 기준점에서 늘리거나 줄일 뿐이라,
    상자와 핀과 칼라의 관계가 그대로 따라옵니다. 이것이 형태와 크기를 갈라 두는 방법입니다.

    <c>None</c> 인 축은 그대로 둡니다. 팔다리의 뼈 방향이 항상 None 인 이유가 여기 있습니다 -
    마디 길이는 골격(LEGS)이 정하지 치수 표가 정하지 않습니다. 표가 길이까지 정하면
    같은 값이 두 군데 적히고, 둘이 어긋나는 날 무릎이 뼈에서 떨어집니다.
    """
    pts = [pt for pt in b.parts if pt["piece"] == piece]
    if not pts:
        return None

    origin, basis = b.frames.get(piece, (V(0, 0, 0), None))
    inv = basis.inverted() if basis is not None else None

    def local(v):
        d = v - origin
        return inv @ d if inv is not None else d

    lo = [1e9, 1e9, 1e9]
    hi = [-1e9, -1e9, -1e9]
    for pt in pts:
        for v in pt["verts"]:
            l = local(v)
            for i in range(3):
                if l[i] < lo[i]: lo[i] = l[i]
                if l[i] > hi[i]: hi[i] = l[i]

    k = [1.0, 1.0, 1.0]
    for i in range(3):
        if target[i] is None:
            continue
        span = hi[i] - lo[i]
        if span > 1e-6:
            k[i] = target[i] / span

    if all(abs(x - 1.0) < 1e-9 for x in k):
        return tuple(round(hi[i] - lo[i], 4) for i in range(3))

    for pt in pts:
        for j, v in enumerate(pt["verts"]):
            l = local(v)
            l = V(l[0] * k[0], l[1] * k[1], l[2] * k[2])
            pt["verts"][j] = origin + (basis @ l if basis is not None else l)

    return tuple(round((hi[i] - lo[i]) * k[i], 4) for i in range(3))


# ----------------------------------------------------------------------------
# PATTERN - 레시피는 형태를 정하고 이 표는 치수를 정합니다
#
# <b>왜 레시피의 숫자를 직접 고치지 않는가.</b> 한 파츠는 상자 여럿과 핀 여럿의
# <b>비례</b>로 형태가 정해집니다. 숫자를 하나씩 고치면 그 비례가 흐트러지고, 변형마다
# 비례를 다시 맞춰야 합니다. 그러면 변형이 새 모델링이 됩니다.
#
# 그래서 형태와 크기를 갈라 둡니다. 아래 표는 <b>파츠의 목표 바운딩 박스</b>이고,
# fit() 이 레시피가 낸 것을 재서 그 치수로 옮깁니다. 변형 하나는 <b>표 한 줄</b>입니다.
#
# 값은 (가로, 앞뒤, 세로)이며 <c>None</c> 인 축은 건드리지 않습니다. 팔다리는
# <b>뼈의 프레임</b>에서 잽니다 - (좌우, 앞뒤, 뼈 방향)이고 뼈 방향은 항상 None 입니다.
# 마디 길이는 골격이 정하지 치수 표가 정하지 않습니다.
#
# 계보가 무엇으로 갈라지는지는 09.Docs/드레드노트_변형_기획.md 에 실측으로 있습니다.
# ----------------------------------------------------------------------------
PATTERNS = {
    # 출하 중인 그것입니다. 육중화(2026-09-10) 뒤의 실측값입니다.
    "Castraferrum": dict(
        hull=(1.85, 1.30, 2.06),
        sarc=(1.42, 0.25, 1.59),
        waist=(1.55, 1.15, 0.75),
        reactor=(1.10, 0.46, 1.36),
        shoulder=(0.47, 0.90, 0.86),
        head=(0.77, 1.00, 0.45),

        hip=(0.46, 0.98, 0.93),

        # ⚠ 팔다리는 <b>뼈 프레임</b>에서 잽니다. 월드 바운딩 박스가 아닙니다 —
        # 마디가 기울어 있어서 월드로 재면 굵기와 길이가 섞입니다. 여기 값은
        # (좌우, 앞뒤)이고 셋째는 항상 None(마디 길이는 골격이 정합니다).
        femur=(1.00, 0.90, None),
        tibia=(0.66, 0.72, None),
        # 발의 세로는 <b>맞추지 않습니다.</b> 발판 두께와 발 전체 높이를 둘 다
        # 표에 적으면 fit 이 서로를 밀어내 밑창이 땅에서 떠 버립니다. 세로는
        # 레시피(발목 블록 + 발판 두께)가 정하고, 표는 발자국 크기만 정합니다.
        foot=(1.22, 1.35, None),

        # 발판의 두께입니다. 발 전체 크기와 따로 두는 이유 - 1.22 x 1.35 짜리 판이
        # 얇으면 노가 되지 슬래브가 아닙니다. 육중화에서 손으로 두껍게 한 값입니다.
        footPlate=0.30,

        # 손으로 걷어낸 것들입니다. 켜면 레시피가 처음에 붙였던 것이 돌아옵니다 —
        # 반응로 배기 굴뚝 둘, 고관절 강철판과 핀, 주먹 너클. 출하본은 없는 상태입니다.
        ornament=False,

        # (왼팔, 오른팔). ARM_KINDS 에서 고릅니다.
        arms=("Fist", "Twin"),
        # 상부 무기. None 이면 안 답니다. TOP_KINDS 에서 고릅니다.
        top=None,
    ),

    # ---- 변형 ----------------------------------------------------------
    # 아래 셋은 <b>골격이 같고 얹은 것만 다릅니다.</b> 카스트라페룸 계열 안에서
    # 도는 것이 가장 싸고, 게임에서는 그것으로 충분한지부터 보십시오.

    # 근접 돌격. 주먹과 공성 드릴, 상부 없음. 실루엣이 가장 낮고 앞으로 길어집니다.
    "Ironclad": dict(
        hull=(1.85, 1.30, 2.06), sarc=(1.42, 0.25, 1.59), waist=(1.55, 1.15, 0.75),
        reactor=(1.10, 0.46, 1.36), shoulder=(0.52, 0.94, 0.90), head=(0.77, 1.00, 0.45),
        hip=(0.46, 0.98, 0.93), femur=(1.06, 0.96, None), tibia=(0.70, 0.76, None),
        foot=(1.28, 1.42, None), footPlate=0.32,
        ornament=True, arms=("Fist", "Drill"), top=None,
    ),

    # 사격 지원. 연장포 둘에 어깨 포대. <b>위에서 보면 이것이 가장 넓습니다.</b>
    "Mortis": dict(
        hull=(1.85, 1.30, 2.06), sarc=(1.42, 0.25, 1.59), waist=(1.55, 1.15, 0.75),
        reactor=(1.10, 0.46, 1.36), shoulder=(0.47, 0.90, 0.86), head=(0.77, 1.00, 0.45),
        hip=(0.46, 0.98, 0.93), femur=(1.00, 0.90, None), tibia=(0.66, 0.72, None),
        foot=(1.22, 1.35, None), footPlate=0.30,
        ornament=False, arms=("Twin", "Twin"), top="Battery",
    ),

    # 포위 사격. 회전포와 미사일 상자에 상부 미사일 팩. 위가 뒤로 무겁습니다.
    "Ballistus": dict(
        hull=(1.85, 1.30, 2.06), sarc=(1.42, 0.25, 1.59), waist=(1.55, 1.15, 0.75),
        reactor=(1.10, 0.46, 1.36), shoulder=(0.47, 0.90, 0.86), head=(0.77, 1.00, 0.45),
        hip=(0.46, 0.98, 0.93), femur=(1.00, 0.90, None), tibia=(0.66, 0.72, None),
        foot=(1.22, 1.35, None), footPlate=0.30,
        ornament=False, arms=("Cannon", "Missile"), top="Missiles",
    ),
}

PATTERN = "Castraferrum"


# ----------------------------------------------------------------------------
# torso - the sarcophagus is the face of the machine, so it gets the mass
# ----------------------------------------------------------------------------
def build_torso(b):
    b.piece = "Hull"
    b.frame_for("Hull", TORSO_C)
    # main armoured box - it reaches from the hip skirt to the roof seam
    b.add(*cbox(TORSO_C + V(0, 0, -0.23), (1.62, 1.30, 1.60), 0.05),
          group="B_Body", mat=MAT_CONCRETE)
    # the Mk.V roof taper. The hand pass had flattened this off, which left the
    # CROWN as the widest row of the whole machine - see Dreadnought_MassAnalysis.md.
    # Tapering rather than curving is the pattern's read.
    Mt = frame(V(0, 0, 1), V(1, 0, 0), TORSO_C + V(0, 0, 0.57))
    b.add(*solid(Mt, [(0.00, prof_rect(0.81, 0.65, 0.05)),
                      (0.46, prof_rect(0.47, 0.51, 0.04))]),
          group="B_Body", mat=MAT_CONCRETE)

    b.piece = "Sarcophagus"
    b.frame_for("Sarcophagus", SARC_C)
    # The lid is a slab of the same poured concrete, stood proud of the torso.
    # Only the retaining rim is steel - a dark border round the face would put
    # the value contrast in the wrong place.
    b.add(*cbox(SARC_C + V(0, 0.05, 0), (1.24, 0.10, 1.04), 0.03),
          group="B_Body", mat=MAT_STEEL)
    b.add(*cbox(SARC_C + V(0, -0.06, 0), (1.10, 0.18, 0.92), 0.04),
          group="B_Body", mat=MAT_CONCRETE)

    b.piece = "Waist"
    b.frame_for("Waist", V(0, 0.02, BODY_Z))
    # waist block and the hip axle it carries
    b.add(*cbox(V(0, 0.02, BODY_Z + 0.10), (1.26, 0.96, 0.46), 0.04),
          group="B_Body", mat=MAT_CONCRETE)
    b.add(*cbox(V(0, 0.02, BODY_Z - 0.10), (1.34, 1.00, 0.14), 0.03),
          group="B_Body", mat=MAT_STEEL)
    b.add(*prism(V(-0.84, -0.02, BODY_Z), V(0.84, -0.02, BODY_Z), 0.12, 0.12, 12,
                 xaxis=V(0, 1, 0)), group="B_Body", mat=MAT_DARK)

    b.piece = "Reactor"
    b.frame_for("Reactor", REACTOR_C)
    # thermic reactor and its two stacks - the pattern's power plant
    b.add(*cbox(REACTOR_C, (1.00, 0.46, 0.92), 0.04), group="B_Body", mat=MAT_CONCRETE)
    if b.ornament:
        for sx in (1.0, -1.0):
            b.add(*prism(REACTOR_C + V(sx * 0.30, 0.02, 0.52),
                         REACTOR_C + V(sx * 0.30, 0.08, 1.08), 0.09, 0.08, 10,
                         xaxis=V(1, 0, 0)), group="B_Body", mat=MAT_DARK)

    b.piece = None


def build_head(b):
    b.piece = "Head"
    b.frame_for("Head", HEAD_C)
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
        b.frame_for("Shoulder_" + key, S)
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
    b.frame_for("Hip_" + key, hip)
    b.add(*cbox(hip, (0.46, 0.76, 0.72), 0.04, zdir=V(0, 0, 1), xhint=ax),
          group=g_hip, mat=MAT_CONCRETE)
    if b.ornament:
        b.add(*cbox(hip + V(Lg["sx"] * 0.24, 0, 0), (0.10, 0.80, 0.76), 0.03,
                    zdir=V(0, 0, 1), xhint=ax), group=g_hip, mat=MAT_STEEL)
        b.add(*prism(hip - ax * Lg["sx"] * 0.26, hip + ax * Lg["sx"] * 0.30,
                     0.11, 0.11, 12, xaxis=V(0, 1, 0)), group=g_hip, mat=MAT_DARK)

    b.piece = "Femur_" + key
    # concrete box beam carries the mass; steel only as two flush collars
    fa, fb = hip + fd * 0.14, knee - fd * 0.26
    M, FL = beam(fa, fb, ax)
    # 뼈의 프레임에서 잽니다 - 로컬 X 가 좌우, Y 가 앞뒤, Z 가 뼈 방향입니다.
    b.frame_for("Femur_" + key, fa, M.to_3x3())
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
    b.frame_for("Tibia_" + key, ta, M.to_3x3())
    b.add(*solid(M, [(0.00, prof_rect(0.29, 0.28)),
                     (TL * 0.38, prof_rect(0.26, 0.25)),
                     (TL, prof_rect(0.23, 0.22))]), group=g_tib, mat=MAT_CONCRETE)
    b.add(*collar(M, 0.02, 0.14, 0.32, 0.31), group=g_tib, mat=MAT_STEEL)
    b.add(*collar(M, TL - 0.12, TL - 0.02, 0.26, 0.25), group=g_tib, mat=MAT_STEEL)

    b.piece = "Foot_" + key
    b.frame_for("Foot_" + key, ankle)
    fod = (tip - ankle).normalized()
    b.add(*cbox(ankle, (0.42, 0.42, 0.38), 0.03, zdir=td, xhint=ax),
          group=g_ft, mat=MAT_STEEL)
    b.add(*prism(ankle - ax * 0.24, ankle + ax * 0.24, 0.09, 0.09, 12, xaxis=td),
          group=g_ft, mat=MAT_DARK)
    # broad splayed footplate - the pattern stands on slabs, not toes
    b.add(*cbox(ankle + fod * 0.17, (0.78, 0.94, b.foot_plate), 0.03, zdir=fod, xhint=ax),
          group=g_ft, mat=MAT_STEEL)
    b.piece = None


# ----------------------------------------------------------------------------
# arms - separate objects, built around their shoulder pivot then localised
# ----------------------------------------------------------------------------
# ----------------------------------------------------------------------------
# arms - one recipe, five business ends
#
# <b>어깨 소켓과 위팔은 공통입니다.</b> 팔꿈치까지는 어느 무기든 같고, 그 앞만
# 갈립니다. 레퍼런스에서 반복되는 실루엣이 정확히 다섯이라 그것을 그대로 씁니다
# (09.Docs/드레드노트_변형_기획.md 의 무기 표).
#
# 팔은 <b>별개 오브젝트</b>이고 원점이 어깨 피벗에 맞춰져 있으므로, 프리팹에서
# 갈아 끼우면 그대로 정렬됩니다. 총구 자리는 무기마다 다르므로 <b>상수로 두지 않고</b>
# 만든 쪽이 돌려줍니다 - 예전에는 MUZZLE 상수와 실제 총열이 따로 놀 수 있었습니다.
# ----------------------------------------------------------------------------
ARM_KINDS = ("Fist", "Cannon", "Twin", "Missile", "Drill")


def _arm_root(b, P, sx):
    """어깨 소켓과 위팔. 다섯 무기가 공유하는 부분입니다."""
    elbow = P + V(sx * 0.02, -0.62, -0.14)
    b.add(*cbox(P + V(sx * 0.08, 0, 0), (0.30, 0.60, 0.66), 0.04), mat=MAT_CONCRETE)
    b.add(*bbeam(P + V(sx * 0.02, -0.16, 0), elbow, (0.27, 0.29), (0.24, 0.26),
                 V(1, 0, 0)), mat=MAT_CONCRETE)
    b.add(*prism(elbow - V(0.24, 0, 0), elbow + V(0.24, 0, 0), 0.10, 0.10, 12,
                 xaxis=V(0, 1, 0)), mat=MAT_DARK)
    return elbow


def _arm_fist(b, P, sx, elbow, ornament):
    """<b>주먹.</b> 때리는 면이 있어야 주먹입니다.

    처음 판은 손목 앞이 민짜 상자여서 <b>가방처럼</b> 보였습니다. 너클 막대를 장식으로
    돌려 두었던 것이 원인입니다 - 그것이 때리는 면이고, 없으면 이 무기의 실루엣이
    통째로 사라집니다. 이제 항상 붙습니다.
    """
    wrist = P + V(sx * 0.02, -0.96, -0.28)
    b.add(*bbeam(elbow, wrist, (0.25, 0.27), (0.29, 0.31), V(1, 0, 0)),
          mat=MAT_CONCRETE)
    # 손목 칼라. 관절이 있다는 것을 말해 주는 유일한 부재입니다.
    b.add(*collar(frame(V(0, -1, 0), V(1, 0, 0), wrist + V(0, 0.10, 0)),
                  0.0, 0.14, 0.33, 0.35), mat=MAT_STEEL)

    fist = wrist + V(0, -0.34, -0.02)
    b.add(*cbox(fist, (0.62, 0.66, 0.68), 0.04), mat=MAT_CONCRETE)
    b.add(*cbox(fist + V(0, 0.32, 0), (0.66, 0.10, 0.72), 0.02), mat=MAT_STEEL)
    # 때리는 면과 너클 넷. 굵기를 0.09 -> 0.14 로 올렸습니다 - 0.09 는 40 m 에서
    # 화소 아래로 내려가 판이 민짜로 보였습니다.
    b.add(*cbox(fist + V(0, -0.36, 0.04), (0.60, 0.14, 0.52), 0.02), mat=MAT_STEEL)
    for i in range(4):
        b.add(*cbox(fist + V(-0.195 + i * 0.13, -0.47, 0.04), (0.14, 0.12, 0.42), 0.02),
              mat=MAT_DARK)
    # 엄지. 바깥쪽에 있어야 손으로 읽히고 램으로 안 읽힙니다.
    b.add(*cbox(fist + V(sx * 0.36, -0.20, -0.18), (0.16, 0.34, 0.26), 0.02),
          mat=MAT_DARK)

    # 하부 총열. 주먹보다 앞으로 나가면 주먹이 안 보이므로 짧게 둡니다.
    muzzle = P + V(0, -1.62, -0.42)
    b.add(*cbox(fist + V(0, -0.12, -0.42), (0.30, 0.52, 0.24), 0.02), mat=MAT_STEEL)
    b.add(*prism(fist + V(0, -0.36, -0.42), muzzle, 0.09, 0.08, 10, xaxis=V(1, 0, 0)),
          mat=MAT_DARK)
    return muzzle


def _arm_cannon(b, P, sx, elbow, ornament):
    """<b>통.</b> 굵은 원통 하나. 실루엣의 전부가 그 통입니다.

    처음 판은 통이 어깨 뒤에 숨고 총열만 조금 나와 있었습니다. 통을 <b>앞으로 내보내고</b>
    급탄 드럼을 바깥에 답니다 - 드럼은 장식이 아니라 이 총을 먹이는 것입니다.
    """
    barrel_r = 0.36
    body = elbow + V(0, -0.30, 0.02)
    b.add(*prism(elbow + V(0, -0.02, 0.02), body + V(0, -0.62, 0),
                 0.33, barrel_r, 14, xaxis=V(1, 0, 0)), mat=MAT_CONCRETE)
    # 급탄 드럼. 바깥쪽에 붙어 실루엣을 한쪽으로 부풀립니다.
    b.add(*prism(body + V(sx * 0.30, -0.18, 0.0), body + V(sx * 0.52, -0.18, 0.0),
                 0.28, 0.26, 12, xaxis=V(0, 1, 0)), mat=MAT_CONCRETE)
    b.add(*collar(frame(V(0, -1, 0), V(1, 0, 0), body + V(0, -0.62, 0)),
                  0.0, 0.12, barrel_r + 0.04, barrel_r + 0.04), mat=MAT_STEEL)

    muzzle = P + V(0, -2.05, 0.02)
    ring = muzzle + V(0, 0.18, 0)
    for i in range(6):
        a = TAU * i / 6.0
        off = V(math.cos(a) * 0.17, 0.0, math.sin(a) * 0.17)
        b.add(*prism(body + V(0, -0.66, 0) + off, muzzle + off, 0.065, 0.06, 8,
                     xaxis=V(1, 0, 0)), mat=MAT_DARK)
    # 총구 슈라우드. 총열 여섯이 <b>하나의 어두운 원반</b>으로 읽히게 묶습니다 -
    # 낱개로는 거리에서 흩어져 사라집니다.
    b.add(*prism(ring, muzzle + V(0, -0.04, 0), 0.30, 0.29, 14, xaxis=V(1, 0, 0)),
          mat=MAT_STEEL)
    return muzzle


def _arm_twin(b, P, sx, elbow, ornament):
    """<b>연장.</b> 관 둘이 나란히.

    처음 판의 총열은 지름 0.12 였습니다. 3.5 m 짜리 기계가 40 m 앞에 있으면 그 굵기는
    화면에서 <b>1 화소 아래</b>입니다. 뒤 절반에 재킷을 씌워 굵은 관 둘로 읽히게 합니다.
    """
    breech = P + V(sx * -0.02, -0.54, 0.0)
    b.add(*cbox(breech, (0.56, 0.92, 0.68), 0.04), mat=MAT_CONCRETE)
    b.add(*cbox(breech + V(0, 0.48, 0), (0.60, 0.10, 0.72), 0.02), mat=MAT_STEEL)
    # 반동 블록. 총열이 어디에 물려 있는지 말합니다.
    b.add(*cbox(breech + V(0, -0.50, 0), (0.52, 0.18, 0.44), 0.02), mat=MAT_STEEL)

    muzzle = P + V(0, -1.86, 0.0)
    for s2 in (1.0, -1.0):
        off = V(s2 * 0.16, 0, 0)
        b.add(*prism(breech + V(0, -0.56, 0) + off, muzzle + off,
                     0.085, 0.075, 10, xaxis=V(1, 0, 0)), mat=MAT_DARK)
        # 재킷 - 뒤 절반만 굵게. 앞은 가늘어야 총열로 읽힙니다.
        b.add(*prism(breech + V(0, -0.58, 0) + off, breech + V(0, -1.02, 0) + off,
                     0.15, 0.13, 12, xaxis=V(1, 0, 0)), mat=MAT_STEEL)
        # 총구 제동기
        b.add(*prism(muzzle + V(0, 0.16, 0) + off, muzzle + V(0, -0.02, 0) + off,
                     0.13, 0.12, 10, xaxis=V(1, 0, 0)), mat=MAT_STEEL)
    return muzzle


def _arm_missile(b, P, sx, elbow, ornament):
    """<b>상자.</b> 각진 발사관 격자. 다섯 중 실루엣이 가장 또렷합니다."""
    block = P + V(sx * -0.02, -0.88, 0.08)
    b.add(*cbox(block + V(0, 0.36, 0), (0.50, 0.40, 0.58), 0.03), mat=MAT_CONCRETE)
    b.add(*cbox(block, (0.74, 0.72, 0.84), 0.03), mat=MAT_CONCRETE)
    b.add(*cbox(block + V(0, -0.38, 0), (0.78, 0.10, 0.88), 0.02), mat=MAT_STEEL)
    # 발사관 아홉. 깊게 파야 거리에서도 격자로 보입니다.
    for iy in range(3):
        for iz in range(3):
            b.add(*cbox(block + V(-0.24 + iy * 0.24, -0.36, -0.28 + iz * 0.28),
                        (0.17, 0.16, 0.21), 0.01), mat=MAT_DARK)
    # 위로 젖히는 방폭 덮개. 경첩이 보이는 것이 이 무기가 열린다는 표시입니다.
    b.add(*cbox(block + V(0, -0.04, 0.46), (0.78, 0.62, 0.10), 0.02), mat=MAT_STEEL)
    b.add(*prism(block + V(-0.36, 0.28, 0.46), block + V(0.36, 0.28, 0.46),
                 0.06, 0.06, 10, xaxis=V(0, 1, 0)), mat=MAT_DARK)
    muzzle = P + V(0, -1.30, 0.08)
    return muzzle


def _arm_drill(b, P, sx, elbow, ornament):
    """<b>드릴.</b> 계단진 원뿔.

    두 번 고쳤습니다. 처음은 바늘이었고, 다음은 날을 굵혔더니 <b>날 뭉치</b>가 되어
    원뿔이 그 뒤에 숨었습니다. 매끈한 원뿔은 거리에서 그냥 쐐기로 보이므로,
    <b>단을 지어</b> 굵기가 줄어드는 것이 보이게 합니다. 날은 얇게 세 줄만 남깁니다 -
    나선은 가까이서 읽히는 것이고, 실루엣을 정하는 것은 단입니다.
    """
    hub = elbow + V(0, -0.30, -0.02)
    b.add(*prism(elbow + V(0, -0.02, 0), hub + V(0, -0.20, 0), 0.28, 0.32, 12,
                 xaxis=V(1, 0, 0)), mat=MAT_CONCRETE)
    # 구동 하우징. 통이 굵어야 힘을 쓰는 것으로 보입니다.
    b.add(*cbox(hub + V(0, -0.44, 0.0), (0.62, 0.56, 0.60), 0.03), mat=MAT_CONCRETE)

    shaft = hub + V(0, -0.72, 0)
    # 단 넷. 굵기가 0.34 -> 0.05 로 줄어드는 것이 실루엣이고, 단 사이의 강철 띠가
    # 그 줄어듦을 읽히게 합니다.
    steps = ((0.34, 0.27, 0.00, 0.34), (0.27, 0.20, 0.34, 0.66),
             (0.20, 0.13, 0.66, 0.96), (0.13, 0.05, 0.96, 1.26))
    for r0, r1, z0, z1 in steps:
        a = shaft + V(0, -z0, 0)
        c = shaft + V(0, -z1, 0)
        b.add(*prism(a, c, r0, r1, 14, xaxis=V(1, 0, 0)), mat=MAT_STEEL)
        b.add(*collar(frame(V(0, -1, 0), V(1, 0, 0), a), 0.0, 0.06, r0 + 0.03, r0 + 0.03),
              mat=MAT_DARK)

    # 나선 날 셋. 얇게 - 굵히면 원뿔을 가립니다.
    for i in range(3):
        a0 = TAU * i / 3.0
        for k in range(4):
            t0, t1 = k * 0.30, (k + 1) * 0.30
            r0 = 0.34 - 0.23 * (t0 / 1.26)
            ang = a0 + 1.1 * (t0 / 1.26)
            b.add(*bbeam(shaft + V(math.cos(ang) * r0, -t0, math.sin(ang) * r0),
                         shaft + V(math.cos(ang + 0.33) * (r0 - 0.05), -t1,
                                   math.sin(ang + 0.33) * (r0 - 0.05)),
                         (0.045, 0.075), (0.035, 0.06), V(0, 1, 0)), mat=MAT_DARK)

    tip = shaft + V(0, -1.30, 0)
    return tip


# ----------------------------------------------------------------------------
# top mount - 동체 상부의 무기 자리
#
# <b>왜 상부인가.</b> 팔은 정면 실루엣만 바꿉니다. 위에서 본 모양을 정하는 것은
# <b>어깨 위에 얹힌 것뿐</b>이라는 규칙이 레퍼런스에서 분명했습니다 (데레데오·발리스투스의
# 어깨 포대, 리뎀터의 미사일 팩). 상부가 없으면 변형이 전부 같은 위 모양입니다.
#
# 팔과 같은 규약입니다 - 선회 마디에 회전판, 부앙 마디에 총. 회전판은 부앙하지 않습니다.
# ----------------------------------------------------------------------------
TOP_YAW = V(0.0, 0.30, 3.40)
TOP_PITCH = TOP_YAW + V(0.0, 0.0, 0.34)

TOP_KINDS = ("Battery", "Missiles")


def _top_mount(b):
    """회전판. 어느 무기를 얹든 같습니다."""
    b.piece = "TopMount"
    b.frame_for("TopMount", TOP_YAW)
    b.add(*prism(TOP_YAW + V(0, 0, -0.10), TOP_YAW + V(0, 0, 0.16), 0.34, 0.31, 14,
                 xaxis=V(1, 0, 0)), group="B_TopYaw", mat=MAT_DARK)
    b.add(*cbox(TOP_YAW + V(0, 0, 0.24), (0.66, 0.72, 0.30), 0.03),
          group="B_TopYaw", mat=MAT_CONCRETE)
    for sx in (1.0, -1.0):
        b.add(*cbox(TOP_PITCH + V(sx * 0.30, 0.02, 0.02), (0.12, 0.38, 0.34), 0.02),
              group="B_TopYaw", mat=MAT_STEEL)


def _top_battery(b):
    """<b>어깨 포대.</b> 동체보다 넓은 가로 막대.

    처음 판은 깊이가 0.44 여서 <b>정면에서 선 하나</b>였습니다. 위에서는 막대로,
    앞에서는 덩어리로 읽혀야 하므로 깊이를 올리고 가운데를 한 단 세웁니다.
    포신도 가늘면 거리에서 사라지므로 재킷과 제동기를 답니다.
    """
    b.add(*cbox(TOP_PITCH + V(0, 0.10, 0.08), (2.20, 0.66, 0.60), 0.03),
          group="B_TopPitch", mat=MAT_CONCRETE)
    b.add(*cbox(TOP_PITCH + V(0, 0.14, 0.44), (0.86, 0.58, 0.26), 0.03),
          group="B_TopPitch", mat=MAT_CONCRETE)
    b.add(*cbox(TOP_PITCH + V(0, -0.22, 0.08), (2.24, 0.10, 0.66), 0.02),
          group="B_TopPitch", mat=MAT_STEEL)
    muzzle = TOP_PITCH + V(0, -1.44, 0.08)
    for sx in (1.0, -1.0):
        off = V(sx * 0.74, 0, 0)
        b.add(*prism(TOP_PITCH + V(0, -0.26, 0.08) + off, muzzle + off,
                     0.14, 0.12, 12, xaxis=V(1, 0, 0)),
              group="B_TopPitch", mat=MAT_DARK)
        b.add(*prism(TOP_PITCH + V(0, -0.28, 0.08) + off,
                     TOP_PITCH + V(0, -0.78, 0.08) + off, 0.21, 0.19, 12,
                     xaxis=V(1, 0, 0)), group="B_TopPitch", mat=MAT_STEEL)
        b.add(*cbox(muzzle + V(0, 0.10, 0) + off, (0.30, 0.22, 0.30), 0.02),
              group="B_TopPitch", mat=MAT_STEEL)
    return muzzle


def _top_missiles(b):
    """<b>미사일 팩.</b> 뒤쪽으로 얹히는 네모 덩어리. 발사관이 위를 봅니다."""
    block = TOP_PITCH + V(0, 0.34, 0.22)
    b.add(*cbox(block, (1.18, 0.90, 0.64), 0.03), group="B_TopPitch", mat=MAT_CONCRETE)
    b.add(*cbox(block + V(0, -0.48, 0), (1.22, 0.10, 0.68), 0.02),
          group="B_TopPitch", mat=MAT_STEEL)
    # 발사관 넷. 위를 보므로 <b>상부 실루엣</b>이 여기서 나옵니다.
    for ix in range(4):
        b.add(*cbox(block + V(-0.39 + ix * 0.26, 0.0, 0.36), (0.20, 0.74, 0.16), 0.01),
              group="B_TopPitch", mat=MAT_DARK)
    # 뒤로 젖혀지는 받침. 발사관이 어디에 물려 있는지 말합니다.
    b.add(*cbox(block + V(0, 0.50, -0.10), (1.02, 0.16, 0.34), 0.02),
          group="B_TopPitch", mat=MAT_STEEL)
    return block + V(0, -0.54, 0.36)


TOP_BUILDERS = {"Battery": _top_battery, "Missiles": _top_missiles}


def build_top(b, kind):
    """상부 무기를 짓고 <b>총구의 월드 좌표</b>를 돌려줍니다. kind 가 None 이면 아무것도 안 합니다."""
    if not kind:
        return None
    assert kind in TOP_BUILDERS, "모르는 상부 무기: %r (있는 것: %s)" % (kind, list(TOP_KINDS))
    _top_mount(b)
    b.piece = "TopGun"
    b.frame_for("TopGun", TOP_PITCH)
    muzzle = TOP_BUILDERS[kind](b)
    b.piece = None
    return muzzle


ARM_BUILDERS = {"Fist": _arm_fist, "Cannon": _arm_cannon, "Twin": _arm_twin,
                "Missile": _arm_missile, "Drill": _arm_drill}


def build_arm(b, kind, side, ornament=False):
    """팔 하나를 짓고 <b>총구의 월드 좌표</b>를 돌려줍니다."""
    assert kind in ARM_BUILDERS, "모르는 무기: %r (있는 것: %s)" % (kind, list(ARM_KINDS))
    P = ARM_PIVOT[side]
    sx = 1.0 if side == "L" else -1.0
    elbow = _arm_root(b, P, sx)
    return ARM_BUILDERS[kind](b, P, sx, elbow, ornament)


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
def _mode(obj, mode):
    """mode_set through an explicit override.

    Headless the plain operator is fine, but called from the MCP add-on the
    context has no active object and the poll fails. Overriding costs nothing
    and makes this script runnable from either side, which is what lets a
    variant be built and looked at without restarting Blender.
    """
    bpy.context.view_layer.objects.active = obj
    obj.select_set(True)
    with bpy.context.temp_override(object=obj, active_object=obj,
                                   selected_objects=[obj],
                                   selected_editable_objects=[obj]):
        bpy.ops.object.mode_set(mode=mode)


def build_rig(muzzles=None):
    """<c>muzzles</c> 는 {"L": 점, "R": 점, "Top": 점 또는 None} 입니다.

    총구를 상수로 두지 않는 이유. 무기를 갈아 끼우면 총열 끝이 따라 움직이는데,
    상수는 안 움직입니다. 그러면 <b>총구 마커와 실제 총열이 따로 놉니다.</b>
    만든 쪽이 돌려준 자리를 그대로 씁니다.
    """
    muzzles = muzzles or {}
    arm = bpy.data.armatures.new("ARM_Dreadnought")
    rig = bpy.data.objects.new("Dreadnought_Rig", arm)
    bpy.context.scene.collection.objects.link(rig)
    _mode(rig, 'EDIT')
    eb = arm.edit_bones

    specs = [
        ("Root",   V(0, 0, 0),              V(0, -0.9, 0),           None,     False, False, V(0, 0, 1)),
        ("B_Body", V(0, 0.55, BODY_Z),      V(0, -0.55, BODY_Z),     "Root",   False, True,  V(0, 0, 1)),
        ("B_Head", HEAD_C + V(0, 0.34, 0),  HEAD_C + V(0, -0.36, 0), "B_Body", False, True,  V(0, 0, 1)),
    ]
    for key in ("L", "R"):
        m = muzzles.get(key) or MUZZLE[key]
        specs += [
            ("B_Arm_" + key, ARM_PIVOT[key], ARM_PIVOT[key] + V(0, -0.9, 0),
             "B_Body", False, True, V(0, 0, 1)),
            ("B_Muzzle_" + key, m, m + V(0, -0.35, 0),
             "B_Arm_" + key, False, False, V(0, 0, 1)),
        ]

    # 상부 무기가 있을 때만 마디를 세웁니다. 없으면 본도 없습니다.
    if muzzles.get("Top") is not None:
        mt = muzzles["Top"]
        specs += [
            ("B_TopYaw", TOP_YAW, TOP_PITCH, "B_Body", False, True, V(0, -1, 0)),
            ("B_TopPitch", TOP_PITCH, TOP_PITCH + V(0, -0.7, 0), "B_TopYaw",
             True, True, V(0, 0, 1)),
            ("B_Muzzle_Top", mt, mt + V(0, -0.35, 0), "B_TopPitch", False, False,
             V(0, 0, 1)),
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


def build(pattern=None):
    """레시피로 형태를 만들고, 치수 표로 크기를 맞춥니다.

    <c>pattern</c> 은 <see cref="PATTERNS"/> 의 키입니다. 비우면 모듈의 기본값
    (<see cref="PATTERN"/>)을 씁니다. 변형을 만들려면 표에 줄을 하나 더하고
    그 이름으로 부르십시오 - 이 함수는 고칠 것이 없습니다.
    """
    wipe()
    mats = ensure_materials()

    name = pattern or PATTERN
    P = PATTERNS.get(name)
    assert P is not None, "모르는 패턴: %r (있는 것: %s)" % (name, sorted(PATTERNS))

    body = Builder()
    body.ornament = bool(P.get("ornament", False))
    body.foot_plate = float(P.get("footPlate", 0.30))
    build_torso(body)
    build_head(body)
    build_shoulders(body)
    for key, Lg in LEGS.items():
        build_leg(body, key, Lg)

    # 레시피가 낸 것을 재서 표의 치수로 옮깁니다. 여기가 형태와 크기의 이음매입니다.
    fitted = {}
    for piece, spec in (("Hull", "hull"), ("Sarcophagus", "sarc"), ("Waist", "waist"),
                        ("Reactor", "reactor"), ("Head", "head")):
        fitted[piece] = fit(body, piece, P[spec])
    for key in LEGS:
        for piece, spec in (("Shoulder_", "shoulder"), ("Hip_", "hip"),
                            ("Femur_", "femur"), ("Tibia_", "tibia"), ("Foot_", "foot")):
            fitted[piece + key] = fit(body, piece + key, P[spec])

    kinds = P.get("arms", ("Fist", "Twin"))
    top_kind = P.get("top")

    # 상부 무기는 <b>동체 빌더</b>에 넣습니다. 파츠 하나에 본 하나 규약을 그대로 타고,
    # 팔처럼 따로 오브젝트를 만들 이유가 없습니다.
    top_muzzle = build_top(body, top_kind)

    a1 = Builder(); m1 = build_arm(a1, kinds[0], "L", body.ornament)
    a2 = Builder(); m2 = build_arm(a2, kinds[1], "R", body.ornament)
    arm_l = make_object("SM_Dread_Arm_L", a1.parts, mats, offset=ARM_PIVOT["L"], vgroups=False)
    arm_r = make_object("SM_Dread_Arm_R", a2.parts, mats, offset=ARM_PIVOT["R"], vgroups=False)

    rig = build_rig({"L": m1, "R": m2, "Top": top_muzzle})

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

    if top_muzzle is not None:
        gun = next(o for o in made if o.name == "SM_Dread_TopGun")
        e = bpy.data.objects.new("SKT_Muzzle_Top", None)
        e.empty_display_type = 'SINGLE_ARROW'
        e.empty_display_size = 0.3
        bpy.context.scene.collection.objects.link(e)
        e.parent = gun
        bpy.context.view_layer.update()
        e.matrix_world = Matrix.Translation(top_muzzle)

    bpy.context.view_layer.update()

    def tris(o):
        return sum(len(pl.vertices) - 2 for pl in o.data.polygons)

    return dict(
        pattern=name,
        arms=list(kinds),
        top=top_kind,
        fitted=dict((k, v) for k, v in fitted.items() if v is not None),
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
# 경로는 <b>슬래시로만</b> 적습니다. 블렌더도 윈도우도 이쪽을 받고, 역슬래시는
# 스크립트를 고칠 때마다 이스케이프로 조용히 망가집니다(_Project 가 제어문자가 된 적이 있습니다).
BLEND_DIR = "E:/GamePJ/CarDrive/Art/Blender"
MODEL_DIR = "E:/GamePJ/CarDrive/Assets/_Project/04.Art/02.Models/Robot"

# <b>기본 패턴만 옛 이름을 지킵니다.</b> 프리팹·유니티 설정·씬이 전부 SM_Dreadnought 를
# 가리키고 있어서, 여기서 이름을 바꾸면 형태와 무관한 곳이 줄줄이 깨집니다.
# 변형은 SM_Dread_<패턴> 으로 나갑니다.
BASE_PATTERN = "Castraferrum"

BLEND_PATH = BLEND_DIR + "/Dreadnought.blend"
FBX_PATH = MODEL_DIR + "/SM_Dreadnought.fbx"
RIG_JSON = MODEL_DIR + "/SM_Dreadnought.rig.json"


def paths_for(pattern):
    """패턴 하나가 나갈 세 자리입니다. (blend, fbx, rig json)"""
    if pattern == BASE_PATTERN:
        return BLEND_PATH, FBX_PATH, RIG_JSON
    stem = "SM_Dread_" + pattern
    return (BLEND_DIR + "/Dreadnought_" + pattern + ".blend",
            MODEL_DIR + "/" + stem + ".fbx",
            MODEL_DIR + "/" + stem + ".rig.json")

# One texture repeat per metre. Both machines use the same number so a single
# concrete map reads at the same grain on either of them.
UV_TILE = 1.0

# Blender -> Unity:  u = (-bx, bz, -by)
CM = Matrix(((-1, 0, 0, 0), (0, 0, 1, 0), (0, -1, 0, 0), (0, 0, 0, 1)))

LEG_NODES = {"L": "Leg_Left", "R": "Leg_Right"}
# node -> 그 파츠를 매다는 부모. 조준 노드 밑으로 가는 것들이 여기서 갈립니다.
# 고정된 목록이 아니라 <b>씬에 있는 것</b>을 씁니다. 상부 무기는 패턴에 따라
# 있기도 없기도 하므로, 목록을 손으로 적어 두면 변형마다 이 파일을 고쳐야 합니다.
BODY_PARTS_FIXED = [
    ("Chassis", "SM_Dread_Hull"),
    ("Sarcophagus", "SM_Dread_Sarcophagus"),
    ("Waist", "SM_Dread_Waist"),
    ("Reactor", "SM_Dread_Reactor"),
    ("Shoulder_L", "SM_Dread_Shoulder_L"),
    ("Shoulder_R", "SM_Dread_Shoulder_R"),
    ("Head", "SM_Dread_Head"),
    ("Arm_L", "SM_Dread_Arm_L"),
    ("Arm_R", "SM_Dread_Arm_R"),
]

TOP_PARTS = [("TopMount", "SM_Dread_TopMount"), ("TopGun", "SM_Dread_TopGun")]


def _body_parts():
    """씬에 실제로 있는 몸통 파츠만 돌려줍니다."""
    out = []
    for node, mesh in BODY_PARTS_FIXED + TOP_PARTS:
        if mesh in bpy.data.objects:
            out.append((node, mesh, True))
    return out


def _muzzle_world(key):
    """총구 자리를 <b>씬의 엠프티에서</b> 읽습니다.

    상수를 읽지 않는 이유. 무기를 갈아 끼우면 총열 끝이 움직이는데 상수는 안 움직입니다.
    엠프티는 만든 쪽이 총열 끝에 꽂아 둔 것이라 언제나 맞습니다.
    """
    e = bpy.data.objects.get("SKT_Muzzle_" + key)
    if e is not None:
        return e.matrix_world.translation.copy()
    return MUZZLE.get(key)


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
    has_top = "B_TopYaw" in bones
    head_pivot = Vector(bones["B_Head"].head_local)
    aim.append(dict(node="Head_Yaw", parent="Body",
                    pos=[round(v, 5) for v in
                         (body_world.inverted() @ Matrix.Translation(P(head_pivot))).translation],
                    axis=[0.0, 1.0, 0.0], range=[-80.0, 80.0]))
    aim.append(dict(node="Head_Pitch", parent="Head_Yaw",
                    pos=[0.0, 0.0, 0.0], axis=[1.0, 0.0, 0.0], range=[-25.0, 30.0]))
    turrets = [
        dict(name="Arm_L", yaw="Arm_L_Yaw", pitch="Arm_L_Pitch", muzzle="Muzzle_L"),
        dict(name="Arm_R", yaw="Arm_R_Yaw", pitch="Arm_R_Pitch", muzzle="Muzzle_R"),
        dict(name="Head", yaw="Head_Yaw", pitch="Head_Pitch", muzzle=""),
    ]
    if has_top:
        # 상부는 <b>한 바퀴 돕니다.</b> 팔이 ±35°인 것과 달리 회전판이라 막을 것이 없습니다.
        aim.append(dict(node="Top_Yaw", parent="Body",
                        pos=[round(v, 5) for v in
                             (body_world.inverted() @ Matrix.Translation(P(TOP_YAW))).translation],
                        axis=[0.0, 1.0, 0.0], range=[-180.0, 180.0]))
        aim.append(dict(node="Top_Pitch", parent="Top_Yaw",
                        pos=[round(v, 5) for v in
                             (Matrix.Translation(P(TOP_YAW)).inverted()
                              @ Matrix.Translation(P(TOP_PITCH))).translation],
                        axis=[1.0, 0.0, 0.0], range=[-10.0, 70.0]))
        turrets.append(dict(name="Top", yaw="Top_Yaw", pitch="Top_Pitch", muzzle="Muzzle_Top"))
    out["aim"] = aim
    out["turrets"] = turrets

    # 총구는 팔이 돌면 따라가야 하므로 그 팔 밑에 답니다.
    muzzles = []
    for key in ("L", "R"):
        m = _muzzle_world(key)
        muzzles.append(dict(
            node="Muzzle_%s" % key, parent="Arm_%s_Pitch" % key,
            pos=[round(v, 5) for v in
                 (Matrix.Translation(P(ARM_PIVOT[key])).inverted()
                  @ Matrix.Translation(P(m))).translation]))
    if has_top:
        m = _muzzle_world("Top")
        muzzles.append(dict(
            node="Muzzle_Top", parent="Top_Pitch",
            pos=[round(v, 5) for v in
                 (Matrix.Translation(P(TOP_PITCH)).inverted()
                  @ Matrix.Translation(P(m))).translation]))
    out["muzzles"] = muzzles

    ATTACH = {"Arm_L": "Arm_L_Pitch", "Arm_R": "Arm_R_Pitch", "Head": "Head_Pitch",
              "TopMount": "Top_Yaw", "TopGun": "Top_Pitch"}
    AIM_WORLD = {"Arm_L_Pitch": Matrix.Translation(P(ARM_PIVOT["L"])),
                 "Arm_R_Pitch": Matrix.Translation(P(ARM_PIVOT["R"])),
                 "Head_Pitch": Matrix.Translation(P(head_pivot)),
                 "Top_Yaw": Matrix.Translation(P(TOP_YAW)),
                 "Top_Pitch": Matrix.Translation(P(TOP_PITCH))}

    for node, obj_name, active in _body_parts():
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


def finalize(pattern=None):
    """지금 씬에 있는 것을 <c>pattern</c> 의 자리로 내보냅니다.

    <b>패턴 이름을 받는 이유.</b> 예전에는 경로가 상수 셋이라 변형을 구워도 갈 곳이
    한 자리뿐이었습니다. 그러면 마지막에 구운 것이 앞의 것을 덮습니다.
    """
    import os

    global RIG_JSON
    name = pattern or PATTERN
    blend_path, fbx_path, rig_path = paths_for(name)

    for o in bpy.data.objects:
        for m in [m for m in o.modifiers if m.type == 'BEVEL']:
            o.modifiers.remove(m)
    assert not any(m.type == 'BEVEL' for o in bpy.data.objects for m in o.modifiers)

    uv_stats = uv_worldscale()

    os.makedirs(os.path.dirname(blend_path), exist_ok=True)
    os.makedirs(os.path.dirname(fbx_path), exist_ok=True)
    bpy.ops.wm.save_as_mainfile(filepath=blend_path)

    bpy.ops.export_scene.fbx(
        filepath=fbx_path, use_selection=False, use_visible=False,
        object_types={'MESH'}, use_mesh_modifiers=True, mesh_smooth_type='FACE',
        bake_space_transform=True, add_leaf_bones=False, bake_anim=False,
        apply_scale_options='FBX_SCALE_NONE', global_scale=1.0,
        axis_forward='-Z', axis_up='Y', path_mode='AUTO')

    # emit_rig_json 은 모듈 상수에 씁니다. 패턴마다 자리가 다르므로 잠깐 바꿔 끼웁니다.
    keep = RIG_JSON
    RIG_JSON = rig_path
    try:
        rig_json = emit_rig_json()
    finally:
        RIG_JSON = keep

    return {"pattern": name, "blend": blend_path, "fbx": fbx_path, "rig": rig_json,
            "uv": uv_stats, "size": os.path.getsize(fbx_path),
            "actions": len(bpy.data.actions)}


if __name__ == "__main__":
    import sys

    # build() 는 이제 출하 모델을 지웁니다(맨 위 경고 참조). 실수로 부르지 못하게 막습니다.
    # 골격을 정말로 다시 뽑아야 할 때만 -- --rebuild 를 붙입니다.
    if "--rebuild" in sys.argv:
        print(build())
        print(finalize())
    else:
        print("build() 는 손으로 다듬은 형상을 지웁니다.")
        print("형상은 그대로 두고 다시 내보내려면 finalize() 만 부르십시오:")
        print('  blender -b Dreadnought.blend --python-expr '
              '"import build_dreadnought as b; b.finalize()"')
        print("골격까지 처음부터 다시 만들려면 -- --rebuild 를 붙이십시오.")
