# -*- coding: utf-8 -*-
"""
Megastructure generator for CarDrive - a SPINE of tileable megaframe bays.

WHAT A MEGASTRUCTURE ACTUALLY IS
  Not a big brutalist building. The term is from 1960s urbanism (Maki 1964,
  Banham 1976). Ralph Wilcoxon's definition, the one Banham works from, is
  four points:

    1. built of MODULAR UNITS
    2. capable of great or even UNLIMITED EXTENSION
    3. a structural FRAMEWORK into which smaller units are built, plugged in
       or clipped on, having been prefabricated elsewhere
    4. that framework outlives the units it carries, by a long way

  Maki: "a large frame in which all the functions of a city are housed... it is
  a man-made feature of the landscape."

  The first version of this file got all four wrong. It made four finished
  towers - one scale, one material, a composed silhouette with a top, standing
  as objects on the ground. Brutalist, yes. Megastructure, no.

WHAT THAT MEANS FOR GEOMETRY
  * TWO SCALES, VISIBLY DIFFERENT. A coarse permanent MEGAFRAME (pylons,
    transfer trusses, decks, service ducts) and a fine transient INFILL
    (dwelling cells clipped into slots). If both read at one scale it is a
    building. The value split does the work: pale frame, dark cells.
  * IT MUST RUN OFF-FRAME. So the unit of production is a BAY that tiles end
    to end, not a structure with a footprint. Everything crossing the seam is
    exactly one bay long and butts. That is points 1 and 2, built in.
  * EMPTY SLOTS. Cells are prefabricated and clipped on, so some slots are not
    filled yet, or not any more. An empty slot is the clearest possible
    statement of point 3 - you can see the frame is the permanent thing.
  * CIRCULATION IS THE FORM. The deck is artificial ground carrying a road
    (Park Hill's "streets in the sky" were wide enough for a milk float;
    Tange's Boston Harbor put roads and monorail inside the structure). In a
    driving game this is the whole point: you drive UNDER it through the pylon
    bays, and the deck above carries a road you can see.

DESIGN RULES kept from the mechs
  * Rectangular masses, no ornament, value contrast carries the read.
  * NO CHAMFER, NO BOOLEANS. Openings are built as boxes framing the gap.
  * World-scale UV at 1 m per repeat - the same tile as the mechs, so concrete
    is the same concrete everywhere.

Run:
    blender -b --python build_megastructure.py
    blender -b --python build_megastructure.py -- --seeds 3,17
"""
import json
import os
import random
import sys

import bpy
from mathutils import Vector

HERE = os.path.dirname(os.path.abspath(__file__))
if HERE not in sys.path:
    sys.path.insert(0, HERE)

import uv_worldscale  # noqa: E402

# --- Paths ------------------------------------------------------------------

OUT_DIR = r"E:\GamePJ\CarDrive\Assets\_Project\04.Art\02.Models\Megastructure"
BLEND_PATH = r"E:\GamePJ\CarDrive\Art\Blender\Megastructure.blend"
UV_TILE = 1.0

# --- The one dimension everything else hangs off ----------------------------
# 한 베이의 길이입니다. <b>이 숫자만은 씨앗이 흔들지 않습니다.</b> 베이가 서로 다른
# 길이면 이어 붙지 않고, 이어 붙지 않으면 "무한히 연장 가능"이 거짓말이 됩니다.
BAY = 42.0

# 데크 밑을 차가 지나갑니다.
CLEARANCE = 9.0

# --- Materials --------------------------------------------------------------
# 로봇과 같은 세 층입니다. 여기서 역할이 하나 더 붙습니다 - <b>콘크리트는 골조,
# 어두운 것은 꽂아 넣은 캡슐</b>. 값이 수명이 다른 두 층을 갈라 줍니다.

MATS = [
    ("M_Mega_Concrete", (0.465, 0.452, 0.430, 1.0), 0.95, 0.00),
    ("M_Mega_Steel", (0.185, 0.192, 0.205, 1.0), 0.45, 1.00),
    ("M_Mega_Dark", (0.048, 0.050, 0.054, 1.0), 0.65, 1.00),
]

MAT_CONCRETE, MAT_STEEL, MAT_DARK = 0, 1, 2


def ensure_materials():
    for name, base, rough, metal in MATS:
        mat = bpy.data.materials.get(name)
        if mat is None:
            mat = bpy.data.materials.new(name)
        mat.use_nodes = True
        bsdf = mat.node_tree.nodes.get("Principled BSDF")
        if bsdf is not None:
            bsdf.inputs["Base Color"].default_value = base
            bsdf.inputs["Roughness"].default_value = rough
            bsdf.inputs["Metallic"].default_value = metal


# --- Geometry ---------------------------------------------------------------


class Mass:
    """상자만 모읍니다. 상자 하나가 면 여섯 개이고, 그것이 이 언어의 전부입니다."""

    def __init__(self):
        self.verts = []
        self.faces = []
        self.mats = []

    def box(self, center, size, mat=MAT_CONCRETE, taper=0.0, taper_axis=2):
        """
        축 정렬 상자입니다. <c>taper</c> 는 <c>taper_axis</c> 의 양(+) 쪽 끝을 좁힙니다.

        기둥 밑을 벌리는 데 씁니다 - 발이 벌어진 A 자 다리가 메가프레임의 기본
        문법입니다(Sant'Elia 1914 -> Tange). 면 수는 상자와 똑같습니다.
        """
        c = list(center)
        h = [size[0] * 0.5, size[1] * 0.5, size[2] * 0.5]

        lo = [c[i] - h[i] for i in range(3)]
        hi = [c[i] + h[i] for i in range(3)]

        k = 1.0 - taper
        other = [i for i in range(3) if i != taper_axis]

        def corner(u, v, top):
            p = [0.0, 0.0, 0.0]
            p[taper_axis] = hi[taper_axis] if top else lo[taper_axis]
            for axis, sign in zip(other, (u, v)):
                half = h[axis] * (k if top else 1.0)
                p[axis] = c[axis] + sign * half
            return tuple(p)

        i = len(self.verts)
        self.verts += [corner(-1, -1, False), corner(1, -1, False),
                       corner(1, 1, False), corner(-1, 1, False),
                       corner(-1, -1, True), corner(1, -1, True),
                       corner(1, 1, True), corner(-1, 1, True)]
        self.faces += [
            (i + 0, i + 3, i + 2, i + 1), (i + 4, i + 5, i + 6, i + 7),
            (i + 0, i + 1, i + 5, i + 4), (i + 1, i + 2, i + 6, i + 5),
            (i + 2, i + 3, i + 7, i + 6), (i + 3, i + 0, i + 4, i + 7),
        ]
        self.mats += [mat] * 6

    def bay_box(self, center, size, half, mat=MAT_CONCRETE):
        """
        베이 경계에서 <b>잘라 낸</b> 상자입니다. x 로 ±<c>half</c> 밖은 버립니다.

        <b>왜 잘라야 하는가.</b> 이음매를 지나는 부재를 안 자르면 베이가 제 길이보다
        길어지고, 이어 붙일 때 이웃과 겹칩니다. 실측에서 42 m 베이가 47.88 m 로
        나왔습니다 - 트러스의 끝 살과 슬롯 살이 각각 밖으로 나가 있었습니다.
        잘린 반쪽은 <b>옆 베이의 반쪽과 만나 온전한 하나</b>가 됩니다.
        """
        lo = max(center[0] - size[0] * 0.5, -half)
        hi = min(center[0] + size[0] * 0.5, half)

        if hi - lo <= 1e-6:
            return

        self.box(((lo + hi) * 0.5, center[1], center[2]),
                 (hi - lo, size[1], size[2]), mat)

    def pierced(self, center, size, holes, hole_w, hole_h, mat=MAT_CONCRETE):
        """
        긴 보에 <b>경량화 구멍</b>을 냅니다. 구멍이 아니라 구멍 사이의 살을 세웁니다.

        <b>왜 구멍이 필요한가.</b> 통짜 벽으로 두면 두께가 안 읽혀 그냥 벽이 됩니다.
        구멍이 뚫려 있어야 <b>깊은 보</b>로, 즉 구조로 보입니다.

        불리언을 쓰지 않는 이유는 결과가 매번 같아야 하기 때문입니다. 불리언은
        접평면에 얇은 조각을 남기고, 그것이 상자 투영 UV 에서 늘어난 텍셀이 됩니다.
        """
        cx, cy, cz = center
        w, d, h = size
        pitch = w / holes
        web = pitch - hole_w
        rim = (h - hole_h) * 0.5

        for i in range(holes + 1):
            self.bay_box((cx - w * 0.5 + i * pitch, cy, cz), (web, d, h), w * 0.5, mat)

        for sign in (-1.0, 1.0):
            self.box((cx, cy, cz + sign * (h - rim) * 0.5), (w, d, rim), mat)

    def to_object(self, name):
        mesh = bpy.data.meshes.new(name)
        mesh.from_pydata(self.verts, [], self.faces)
        mesh.update()

        for mat_name, _, _, _ in MATS:
            mesh.materials.append(bpy.data.materials[mat_name])

        for poly, index in zip(mesh.polygons, self.mats):
            poly.material_index = index

        obj = bpy.data.objects.new(name, mesh)
        bpy.context.scene.collection.objects.link(obj)
        return obj


# --- Dimensions -------------------------------------------------------------


def spec(seed):
    """
    씨앗 하나에서 한 <b>베이</b>의 치수를 뽑습니다.

    <b>골조 치수는 흔들지 않습니다.</b> 베이 길이·데크 높이·기둥 간격이 베이마다
    다르면 이어 붙지 않고, 이어 붙지 않으면 연장이 불가능합니다. 흔드는 것은
    <b>꽂혀 있는 것</b>입니다 - 어느 슬롯이 찼는지, 캡슐이 얼마나 튀어나왔는지,
    이 베이에 계단탑이 있는지. 그 자체가 "골조는 영구, 캡슐은 임시"라는 뜻입니다.
    """
    rng = random.Random(seed)

    return dict(
        seed=seed,
        # ---- 골조(영구) ----
        bay=BAY,
        width=34.0,
        gate=22.0,          # 다리 구간. 이 밑으로 차가 지나갑니다.
        leg=(9.0, 11.0),
        truss=8.0,
        deck=2.0,
        parapet=1.4,
        duct=2.4,
        tiers=4,            # 캡슐 층수
        tier=5.6,
        upper=22.0,         # 상부 포털 높이
        post=3.4,
        # ---- 채움(가변) ----
        cell=(6.4, 5.0, 4.4),
        fill=rng.uniform(0.42, 0.74),
        stair=rng.random() < 0.5,
        tank=rng.random() < 0.45,
    )


def build(s):
    """
    베이 하나를 세웁니다. 원점은 <b>지면이자 베이의 한가운데</b>입니다.

    x 가 스파인 방향이고 베이는 x 로 <c>bay</c> 만큼 차지합니다. 같은 것을
    <c>bay</c> 간격으로 늘어놓으면 이어집니다.

    <b>단면이 골조와 채움을 번갈아 갑니다.</b> 첫 판에서는 캡슐이 골조를 덮어 버려
    긴 선반 위의 상자 더미로 보였습니다 - Wilcoxon 의 3·4 번(골조가 주인이고 더
    오래 산다)이 그림에서 뒤집힌 것입니다. 지금은 이렇게 쌓입니다:

        0  ~ 22   다리 구간 - 골조만. 차가 지나갑니다.
       22 ~ 30    이송 트러스 - 골조. 구멍이 뚫린 깊은 보.
       30 ~ 32    주 데크 - 골조이자 <b>인공 지반</b>. 도로가 놓입니다.
       32 ~ 54    캡슐 구간 - 채움. 슬롯 기둥이 캡슐보다 <b>바깥</b>에 섭니다.
       54 ~ 56    상부 데크 - 두 번째 인공 지반.
       56 ~ 78    상부 포털 - 골조만. 하늘이 비쳐 "위로도 계속된다"가 됩니다.
    """
    m = Mass()
    rng = random.Random(s["seed"] * 104729 + 7)

    L, W = s["bay"], s["width"]
    lx, ly = s["leg"]
    gate = s["gate"]
    truss = s["truss"]
    deck_z = gate + truss
    deck_top = deck_z + s["deck"]

    # ---- 다리 --------------------------------------------------------------
    # 베이 한가운데에 한 쌍. 밑이 벌어진 A 자입니다(Sant'Elia -> Tange).
    for sign in (-1.0, 1.0):
        m.box((0.0, sign * (W * 0.5 - ly * 0.5), (gate + 8.0) * 0.5 - 4.0),
              (lx, ly, gate + 8.0), MAT_CONCRETE, taper=0.30)

        # 기둥머리
        m.box((0.0, sign * (W * 0.5 - ly * 0.5), gate - 1.4),
              (lx + 3.4, ly + 2.6, 2.8), MAT_CONCRETE)

    # 다리를 잇는 인방. 차가 지나가는 문의 위쪽입니다.
    m.box((0.0, 0.0, gate + truss * 0.34), (lx + 1.2, W - ly * 2.0 + 2.4, truss * 0.68),
          MAT_CONCRETE)

    # ---- 이송 트러스 -------------------------------------------------------
    # 스파인 방향으로 흐르는 깊은 보. 이음매를 지나므로 정확히 한 베이 길이입니다.
    for sign in (-1.0, 1.0):
        m.pierced((0.0, sign * (W * 0.5 - ly * 0.42), gate + truss * 0.5),
                  (L, ly * 0.84, truss), 3, L / 3.0 * 0.56, truss * 0.5)

    # ---- 설비 덕트 ---------------------------------------------------------
    # 데크 밑을 따라 끝없이 흐릅니다. 이음매에서 끊기면 안 됩니다.
    duct = s["duct"]
    for sign in (-1.0, 1.0):
        m.box((0.0, sign * (W * 0.5 - ly * 1.05), gate + duct * 0.4),
              (L, duct, duct), MAT_STEEL)
    m.box((0.0, 0.0, gate - duct * 0.5), (L, duct * 1.8, duct * 0.9), MAT_STEEL)

    # ---- 주 데크 -----------------------------------------------------------
    m.box((0.0, 0.0, deck_z + s["deck"] * 0.5), (L, W, s["deck"]), MAT_CONCRETE)
    m.box((0.0, 0.0, deck_top + 0.08), (L, W - 4.0, 0.16), MAT_DARK)

    for sign in (-1.0, 1.0):
        m.box((0.0, sign * (W * 0.5 - 0.6), deck_top + s["parapet"] * 0.5),
              (L, 1.2, s["parapet"]), MAT_CONCRETE)

    # ---- 캡슐 구간 ---------------------------------------------------------
    # <b>슬롯 기둥이 캡슐보다 바깥에 섭니다.</b> 그래야 골조가 앞에 서고 캡슐이
    # 그 뒤에 꽂힌 것으로 읽힙니다. 반대로 두면 캡슐이 골조를 덮습니다.
    cw, cd, ch = s["cell"]
    cols = max(2, int(L / (cw + 1.2)))
    pitch = L / cols
    tier = s["tier"]
    zone = s["tiers"] * tier

    for sign in (-1.0, 1.0):
        yc = sign * (W * 0.5 - cd * 0.5)

        # 슬롯 기둥
        for i in range(cols + 1):
            m.bay_box((-L * 0.5 + i * pitch, sign * (W * 0.5 + 0.4),
                       deck_top + zone * 0.5),
                      (1.6, cd * 0.55, zone), L * 0.5, MAT_CONCRETE)

        # 층 바닥판. 이것이 없으면 기둥만 서 있어 캡슐이 떠 보입니다.
        for t in range(s["tiers"] + 1):
            m.box((0.0, sign * (W * 0.5 + 0.2), deck_top + t * tier),
                  (L, cd * 0.75, 0.7), MAT_CONCRETE)

        # 캡슐
        for t in range(s["tiers"]):
            for col in range(cols):
                if rng.random() > s["fill"]:
                    continue
                x = -L * 0.5 + (col + 0.5) * pitch
                m.box((x, yc, deck_top + (t + 0.5) * tier),
                      (cw, cd * 1.5, ch), MAT_DARK)
                m.box((x, yc - sign * cd * 0.72, deck_top + (t + 0.62) * tier),
                      (cw * 0.5, 0.4, ch * 0.28), MAT_STEEL)

    # ---- 상부 데크 ---------------------------------------------------------
    up_z = deck_top + zone
    m.box((0.0, 0.0, up_z + 1.0), (L, W - 3.0, 2.0), MAT_CONCRETE)
    m.box((0.0, 0.0, up_z + 2.08), (L, W - 9.0, 0.16), MAT_DARK)

    # ---- 상부 포털 ---------------------------------------------------------
    # <b>열려 있어야 합니다.</b> 여기를 막으면 데크가 지붕이 되고 전체가 건물이 됩니다.
    post = s["post"]
    top = up_z + 2.0 + s["upper"]

    for sign in (-1.0, 1.0):
        for i in (-1, 1):
            m.box((i * L * 0.30, sign * (W * 0.5 - post * 0.8), (up_z + 2.0 + top) * 0.5),
                  (post, post, top - up_z - 2.0), MAT_CONCRETE)

    m.pierced((0.0, 0.0, top + 1.6), (L, W - post * 0.8, 3.2), 3, L / 3.0 * 0.62, 1.8)

    for sign in (-1.0, 1.0):
        m.box((0.0, sign * (W * 0.5 - post * 0.8), top - 1.4), (L, post * 0.7, 1.4),
              MAT_STEEL)

    # ---- 이 베이에만 있는 것 -----------------------------------------------
    # 전부 같으면 압출한 것으로 보입니다. 베이마다 다른 것이 하나쯤 있어야
    # <b>덧붙여 자란 것</b>으로 읽힙니다.
    if s["stair"]:
        side = rng.choice((-1.0, 1.0))
        m.box((L * 0.30, side * (W * 0.5 + 5.0), (top + 3.0) * 0.5),
              (7.0, 7.0, top + 3.0), MAT_CONCRETE)
        m.box((L * 0.30, side * (W * 0.5 + 5.0), top + 3.6), (8.4, 8.4, 1.4), MAT_STEEL)

    if s["tank"]:
        side = rng.choice((-1.0, 1.0))
        m.box((-L * 0.28, side * (W * 0.5 - post * 2.4), up_z + 2.0 + 4.0),
              (10.0, 9.0, 8.0), MAT_STEEL)

    return m.to_object("SM_Mega_Bay_%d" % s["seed"])


# --- Run --------------------------------------------------------------------


def wipe():
    for obj in list(bpy.data.objects):
        bpy.data.objects.remove(obj, do_unlink=True)
    for block in (bpy.data.meshes, bpy.data.cameras, bpy.data.lights):
        for item in list(block):
            if item.users == 0:
                block.remove(item)


def bounds(obj):
    pts = [obj.matrix_world @ Vector(c) for c in obj.bound_box]
    lo = [min(p[i] for p in pts) for i in range(3)]
    hi = [max(p[i] for p in pts) for i in range(3)]
    return lo, hi


def run(seeds):
    os.makedirs(OUT_DIR, exist_ok=True)

    report = []

    for seed in seeds:
        wipe()
        ensure_materials()

        s = spec(seed)
        obj = build(s)
        uv_worldscale.box_uv(obj, UV_TILE)

        path = os.path.join(OUT_DIR, obj.name + ".fbx")
        bpy.ops.export_scene.fbx(
            filepath=path, use_selection=False, use_visible=False,
            object_types={'MESH'}, use_mesh_modifiers=True, mesh_smooth_type='FACE',
            bake_space_transform=True, add_leaf_bones=False, bake_anim=False,
            apply_scale_options='FBX_SCALE_NONE', global_scale=1.0,
            axis_forward='-Z', axis_up='Y', path_mode='AUTO')

        lo, hi = bounds(obj)
        report.append(dict(
            name=obj.name, seed=seed,
            tris=sum(len(p.vertices) - 2 for p in obj.data.polygons),
            size=[round(hi[i] - lo[i], 2) for i in range(3)],
            # 이음매가 맞는지의 증거입니다. x 길이가 베이 길이와 같아야 합니다.
            span_x=round(hi[0] - lo[0], 3), bay=s["bay"],
            tiles=abs((hi[0] - lo[0]) - s["bay"]) < 0.01,
            clearance=round(s["gate"], 2),
            uv=uv_worldscale.density_report([obj])[1],
            fbx=os.path.getsize(path)))

    return report


def lay_out(seeds, length=11):
    """
    베이를 <b>실제로 이어 붙여</b> blend 에 남깁니다.

    한 베이만 보면 이어지는지 알 수 없습니다. 늘어놓아야 이음매가 맞는지, 덕트가
    끊기지 않는지, 그리고 무엇보다 <b>끝이 안 보이는지</b>가 보입니다.
    """
    wipe()
    ensure_materials()

    made = {}
    for seed in seeds:
        obj = build(spec(seed))
        uv_worldscale.box_uv(obj, UV_TILE)
        obj.location.y = 4000.0
        made[seed] = obj

    rng = random.Random(20260908)
    for i in range(length):
        source = made[rng.choice(seeds)]
        copy = source.copy()
        copy.data = source.data
        copy.location = (i - length * 0.5) * BAY, 0.0, 0.0
        bpy.context.scene.collection.objects.link(copy)

    bpy.ops.wm.save_as_mainfile(filepath=BLEND_PATH)


if __name__ == "__main__":
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []

    seeds = [3, 17, 41, 58]
    if "--seeds" in argv:
        seeds = [int(v) for v in argv[argv.index("--seeds") + 1].split(",")]

    out = run(seeds)
    lay_out(seeds)
    print("###JSON###" + json.dumps(out))
