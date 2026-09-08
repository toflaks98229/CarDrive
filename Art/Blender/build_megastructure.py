# -*- coding: utf-8 -*-
"""
Seeded brutalist megastructures for CarDrive.

WHAT THIS IS FOR
  The world is 103 baked terrain tiles of 100 m, and everything standing on it
  is house-sized. Nothing in it says "megastructure" - the art direction asks
  for concrete mass that dwarfs the car, and mass is the one thing a texture
  cannot fake. So this builds the mass.

WHY A GENERATOR AND NOT THREE MODELS
  Megastructures read as a SYSTEM - the same formwork logic repeated at
  different sizes. Hand-authoring three loses that; a seeded generator keeps
  one vocabulary and varies only the numbers, which is how the real ones were
  built: one contractor, one set of moulds, one budget.

DESIGN RULES (Strider / Dreadnought language, scaled up)
  * Rectangular masses. No ornament. Value contrast carries the read.
  * NO CHAMFER. On the mechs a 0.04 m chamfer catches a highlight; the same
    proportional chamfer here would be 0.3 m and still sub-pixel at the
    distance these are seen from. Beton brut is formwork-sharp anyway, so the
    polygons go into RIBS and SILHOUETTE instead, which do survive distance.
  * NO BOOLEANS. A passage is built as boxes framing the gap, not as a box
    minus a box. Deterministic, all quads, nothing to clean up.
  * World-scale UV at 1 m per repeat - the same tile as the mechs, so concrete
    is the same concrete whether it is a robot's hip or a hundred-metre wall.
    A 100 m face therefore reaches uv 100; that is intended, and mipmaps carry
    the far end.

Run:
    blender -b --python build_megastructure.py
    blender -b --python build_megastructure.py -- --seeds 7,11,23
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

# --- Materials --------------------------------------------------------------
# 로봇과 같은 세 층입니다. 콘크리트가 덩어리, 강철이 띠, 어두운 것이 그림자 홈.
# 이름만 다르고 역할은 같으므로 유니티에서 같은 지도를 물립니다.

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

    def box(self, center, size, mat=MAT_CONCRETE, taper=0.0):
        """
        축 정렬 상자입니다. <c>taper</c> 는 윗면을 좁히는 비율입니다.

        기단에 조금 주면 아래가 벌어져 땅을 누르는 모양이 됩니다. 브루탈리즘
        건물이 바닥에서 두꺼워지는 그 처리이고, 면 수는 상자와 똑같습니다.
        """
        cx, cy, cz = center
        hx, hy, hz = size[0] * 0.5, size[1] * 0.5, size[2] * 0.5
        tx, ty = hx * (1.0 - taper), hy * (1.0 - taper)

        i = len(self.verts)
        self.verts += [
            (cx - hx, cy - hy, cz - hz), (cx + hx, cy - hy, cz - hz),
            (cx + hx, cy + hy, cz - hz), (cx - hx, cy + hy, cz - hz),
            (cx - tx, cy - ty, cz + hz), (cx + tx, cy - ty, cz + hz),
            (cx + tx, cy + ty, cz + hz), (cx - tx, cy + ty, cz + hz),
        ]
        self.faces += [
            (i + 0, i + 3, i + 2, i + 1), (i + 4, i + 5, i + 6, i + 7),
            (i + 0, i + 1, i + 5, i + 4), (i + 1, i + 2, i + 6, i + 5),
            (i + 2, i + 3, i + 7, i + 6), (i + 3, i + 0, i + 4, i + 7),
        ]
        self.mats += [mat] * 6

    def framed(self, center, size, gap_w, gap_h, mat=MAT_CONCRETE, lintel=MAT_DARK):
        """
        가운데가 뚫린 덩어리입니다. 상자 셋으로 구멍을 두릅니다.

        <b>왜 불리언을 쓰지 않는가.</b> 결과가 매번 같아야 하기 때문입니다. 불리언은
        접평면에서 얇은 조각을 남기고, 그 조각이 상자 투영 UV 에서 늘어난 텍셀로
        드러납니다. 여기서는 애초에 구멍이 아니라 <b>구멍 둘레</b>를 만듭니다.
        """
        cx, cy, cz = center
        w, d, h = size
        side = (w - gap_w) * 0.5
        head = h - gap_h

        self.box((cx - (gap_w + side) * 0.5, cy, cz), (side, d, h), mat)
        self.box((cx + (gap_w + side) * 0.5, cy, cz), (side, d, h), mat)
        self.box((cx, cy, cz + (h - head) * 0.5), (gap_w, d, head), lintel)

    def facade(self, axis, face, span, height, cols, rows, relief, rng=None):
        """
        면 하나에 <b>셀 수 있는 격자</b>를 붙입니다. 어두운 판 위에 밝은 기둥과 인방입니다.

        <b>왜 이것이 필요한가.</b> 덩어리만으로는 크기를 알 수 없습니다. 100 m 벽과
        10 m 벽은 실루엣이 같습니다. 크기를 말해 주는 것은 <b>셀 수 있는 반복 단위</b>이고,
        그 단위가 사람 몸에 묶여 있어야 합니다(층 4 m, 베이 5 m). 눈이 그것을 세는
        순간 벽의 높이가 정해집니다.

        <b>왜 리브가 아니라 격자인가.</b> 처음에는 세로 지느러미만 세웠는데 렌더에서
        거의 보이지 않았습니다. 얇은 판은 자기 그림자를 못 만듭니다. 어두운 판을
        먼저 깔고 그 앞에 밝은 부재를 세우면, 그림자에 기대지 않고 <b>값 대비</b>로
        읽힙니다 — 해가 어디 있든 무너지지 않습니다.

        axis 는 면의 법선 축(1=Y, 0=X), face 는 그 축 위의 좌표와 부호입니다.
        """
        nrm, sign, other, z0 = face
        w = span[1] - span[0]
        h = height[1] - height[0]

        def put(cx, co, sx, so, sz, cz, mat):
            # 면의 축에 맞춰 x/y 를 바꿔 끼웁니다. 격자 논리는 한 번만 씁니다.
            if axis == 1:
                self.box((co, nrm + sign * cx, cz), (so, sx, sz), mat)
            else:
                self.box((nrm + sign * cx, co, cz), (sx, so, sz), mat)

        # 어두운 판. 격자 뒤에 깔려 그림자 노릇을 합니다.
        put(relief * 0.5, (span[0] + span[1]) * 0.5, relief, w, h,
            (height[0] + height[1]) * 0.5, MAT_DARK)

        # 세로 기둥
        pitch = w / cols
        for i in range(cols + 1):
            put(relief * 1.2, span[0] + i * pitch, relief * 1.4, pitch * 0.34, h,
                (height[0] + height[1]) * 0.5, MAT_CONCRETE)

        # 가로 인방
        step = h / rows
        for j in range(rows + 1):
            put(relief * 0.9, (span[0] + span[1]) * 0.5, relief * 1.0, w,
                step * 0.26, height[0] + j * step, MAT_CONCRETE)

        if rng is None:
            return

        # <b>결번.</b> 칸이 전부 뚫려 있으면 사무실 건물로 읽힙니다. 브루탈리즘
        # 메가스트럭처는 대부분이 막힌 벽이고 뚫린 곳이 예외입니다. 몇 칸을 통판으로
        # 메워 그 비율을 뒤집습니다.
        for i in range(cols):
            for j in range(rows):
                if rng.random() > 0.30:
                    continue
                # 앞면 깊이가 어두운 판과 <b>정확히 같으면 z-파이팅</b>이 납니다. 실제로
                # 첫 렌더에서 메운 칸마다 대각선 이음매가 보였습니다. 0.3·relief 만큼
                # 앞으로 내밀어 띄우되, 기둥·인방보다는 뒤에 둡니다 - 틀 뒤의 채움판입니다.
                put(relief * 0.95, span[0] + (i + 0.5) * pitch, relief * 0.7,
                    pitch * 0.72, step * 0.68, height[0] + (j + 0.5) * step,
                    MAT_CONCRETE)

        # <b>설비층.</b> 대여섯 층마다 통째로 막힌 띠가 지나갑니다. 층수를 끊어 세게
        # 만들고, 무엇보다 이것이 <b>건물이 아니라 설비</b>라고 말합니다.
        for j in range(3, rows, rng.randint(5, 8)):
            put(relief * 1.45, (span[0] + span[1]) * 0.5, relief * 1.7, w,
                step * 0.95, height[0] + (j + 0.5) * step, MAT_CONCRETE)

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
    씨앗 하나에서 한 구조물의 치수를 뽑습니다.

    <b>범위가 곧 양식입니다.</b> 값이 무엇이 되든 브루탈리즘 밖으로 나가지 못하게
    묶어 둡니다 - 얇은 탑도, 유리 상자도, 계단식 피라미드도 나오지 않습니다.

    <b>베이와 층은 미터가 아니라 사람으로 정합니다.</b> 층 3.6~4.4 m, 베이 5~7 m 는
    실제로 지어진 치수이고, 이것이 보는 사람에게 자 노릇을 합니다. 높이는 그 자의
    눈금 수로 정해집니다 - 90 m 는 "높다"가 아니라 "22 층"으로 읽힙니다.
    """
    rng = random.Random(seed)

    width = rng.uniform(26.0, 44.0)
    height = rng.uniform(55.0, 110.0)

    return dict(
        seed=seed,
        width=width,
        depth=rng.uniform(18.0, 30.0),
        height=height,
        # 지형에 파묻힐 몫입니다. 비탈에 놓아도 밑이 뜨지 않습니다.
        buried=8.0,
        apron=rng.uniform(10.0, 18.0),
        apron_reach=rng.uniform(12.0, 30.0),
        apron_side=rng.choice((-1.0, 1.0)),
        bay=rng.uniform(5.0, 7.0),
        floor=rng.uniform(3.6, 4.4),
        relief=rng.uniform(1.2, 2.0),
        cantilever=rng.random() < 0.75,
        cant_at=rng.uniform(0.62, 0.82),
        cant_reach=rng.uniform(0.5, 1.1),
        cant_thick=rng.uniform(8.0, 14.0),
        cores=rng.randint(1, 2),
        core_side=rng.uniform(0.30, 0.46),
        core_rise=rng.uniform(7.0, 17.0),
        passage=rng.random() < 0.75,
        passage_w=rng.uniform(11.0, 18.0),
        passage_h=rng.uniform(7.0, 10.0),
        buttress=rng.randint(2, 3),
    )


def build(s):
    """
    치수 하나를 덩어리로 세웁니다. 원점은 <b>지면</b>이고, 기단은 그 아래로 더 갑니다.

    <b>구성이 하나의 기둥에서 시작합니다.</b> 처음에는 널찍한 기단 위에 판을 쌓았는데,
    렌더에서 기단과 본체가 <b>따로 노는 두 건물</b>로 보였습니다. 케이크 받침 같았습니다.
    지금은 땅에서 꼭대기까지 이어지는 덩어리가 먼저 있고, 앞치마·캔틸레버·코어가
    거기에 <b>겹쳐 붙습니다.</b> 떠 있는 것이 하나도 없습니다.
    """
    m = Mass()

    w, d, h = s["width"], s["depth"], s["height"]
    apron = s["apron"]
    buried = s["buried"]

    # ---- 기둥 --------------------------------------------------------------
    m.box((0.0, 0.0, (h - buried) * 0.5), (w, d, h + buried), MAT_CONCRETE)

    # ---- 앞치마 ------------------------------------------------------------
    # 한쪽으로만 뻗습니다. 좌우 대칭이면 받침대로 보입니다.
    reach = s["apron_reach"]
    ax = s["apron_side"] * (w * 0.5 + reach * 0.5)

    if s["passage"]:
        # 차가 지나갈 굴입니다. 운전 게임이므로 <b>지나갈 수 있는 구멍</b>이 있는 편이
        # 구조물을 배경이 아니라 장소로 만듭니다.
        m.framed((ax, 0.0, (apron - buried) * 0.5), (reach, d * 1.8, apron + buried),
                 s["passage_w"], s["passage_h"] + buried)
    else:
        m.box((ax, 0.0, (apron - buried) * 0.5), (reach, d * 1.8, apron + buried),
              MAT_CONCRETE, taper=0.08)

    m.box((ax, 0.0, apron + 0.5), (reach + 1.4, d * 1.8 + 1.4, 1.0), MAT_DARK)

    # ---- 버팀벽 ------------------------------------------------------------
    # 앞치마에서 기둥으로 기대는 큰 쐐기입니다. 작게 여러 개면 톱니로 보입니다.
    for i in range(s["buttress"]):
        t = (i + 0.5) / s["buttress"]
        y = (t - 0.5) * d * 1.5
        m.box((s["apron_side"] * (w * 0.5 + 3.0), y, apron * 0.9),
              (7.0, d * 0.30, apron * 2.4), MAT_CONCRETE, taper=0.62)

    # ---- 격자 --------------------------------------------------------------
    grain = random.Random(s["seed"] * 7919 + 13)
    cols = max(3, int(round(w / s["bay"])))
    rows = min(24, max(4, int(round((h - apron - 6.0) / s["floor"]))))
    top = h - 4.0

    for sign in (-1.0, 1.0):
        m.facade(1, (sign * d * 0.5, sign, 0.0, 0.0),
                 (-w * 0.46, w * 0.46), (apron + 3.0, top),
                 cols, rows, s["relief"], grain)

    # ---- 캔틸레버 ----------------------------------------------------------
    # 하나의 몸짓입니다. 이것이 없으면 그냥 탑이고, 있으면 구조물이 됩니다.
    if s["cantilever"]:
        z = h * s["cant_at"]
        out = w * s["cant_reach"]
        cx = -s["apron_side"] * (w * 0.5 + out * 0.5)

        m.box((cx, 0.0, z + s["cant_thick"] * 0.5), (out, d * 1.15, s["cant_thick"]),
              MAT_CONCRETE)
        m.box((cx, 0.0, z - 0.5), (out + 0.8, d * 1.15 + 0.8, 1.0), MAT_DARK)

        # 뿌리를 기둥 안까지 물립니다. 끝에서 딱 끊기면 붙여 놓은 것으로 보입니다.
        m.box((-s["apron_side"] * w * 0.25, 0.0, z + s["cant_thick"] * 0.75),
              (w * 0.5, d * 0.55, s["cant_thick"] * 1.5), MAT_CONCRETE)

    # ---- 코어 --------------------------------------------------------------
    # <b>탑 폭에 대한 비율</b>로 잡습니다. 고정 미터로 두었더니 100 m 탑 옆에서
    # 안테나처럼 가늘어 보였습니다 - 계단실은 굵어야 계단실로 보입니다.
    side = max(6.0, w * s["core_side"])
    for i in range(s["cores"]):
        sx = 1.0 if i == 0 else -1.0
        x = sx * (w * 0.5 - side * 0.5)
        y = -d * 0.5 + side * 0.5
        peak = h + s["core_rise"] * (1.0 if i == 0 else 0.6)

        m.box((x, y, (apron + peak) * 0.5), (side, side, peak - apron), MAT_CONCRETE)
        m.box((x, y, peak + 0.4), (side + 0.9, side + 0.9, 0.8), MAT_STEEL)

    # 꼭대기 테두리
    m.box((0.0, 0.0, h + 0.6), (w + 1.6, d + 1.6, 1.2), MAT_DARK)

    return m.to_object("SM_Mega_%d" % s["seed"])


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
            verts=len(obj.data.vertices),
            size=[round(hi[i] - lo[i], 2) for i in range(3)],
            top=round(hi[2], 2), buried=round(-lo[2], 2),
            passage=s["passage"],
            uv=uv_worldscale.density_report([obj]),
            fbx=os.path.getsize(path)))

    return report


def lay_out(seeds):
    """전부 한 줄로 세워 blend 에 남깁니다. 눈으로 나란히 보려는 용도입니다."""
    wipe()
    ensure_materials()

    x = 0.0
    for seed in seeds:
        s = spec(seed)
        obj = build(s)
        uv_worldscale.box_uv(obj, UV_TILE)
        obj.location.x = x
        x += s["width"] + 60.0

    bpy.ops.wm.save_as_mainfile(filepath=BLEND_PATH)


if __name__ == "__main__":
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []

    seeds = [3, 17, 41, 58]
    if "--seeds" in argv:
        seeds = [int(v) for v in argv[argv.index("--seeds") + 1].split(",")]

    out = run(seeds)
    lay_out(seeds)
    print("###JSON###" + json.dumps(out))
