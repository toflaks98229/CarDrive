# -*- coding: utf-8 -*-
"""
Shared box-language for CarDrive's procedural hard-surface generators.

WHY THIS EXISTS
  The megastructure and the buildings are the same language at different sizes:
  axis-aligned masses, no chamfer, no booleans, world-scale UVs, three values
  (concrete / steel / dark). When there was one generator the helpers lived in
  it. With two, a copy would drift - and a drift here means the two stop looking
  like the same world, which is the one thing the art direction cannot afford.

WHAT IS DELIBERATELY NOT HERE
  Anything that decides a SHAPE. This module knows how to put a box down and
  how to write the mesh out; what to build and at what size belongs to each
  generator, because that is where the design argument lives.
"""
import os

import bpy
from mathutils import Vector


# --- Materials --------------------------------------------------------------


def ensure_materials(mats):
    """
    <c>(name, base_rgba, roughness, metallic)</c> 목록을 블렌더 머티리얼로 만듭니다.

    블렌더 쪽 색은 <b>작업용</b>입니다. 게임에서 쓰는 값은 유니티의 셋업 스크립트가
    정하고, 결은 <c>BrutalistTextureSetup</c> 이 물립니다. 여기 색은 블렌더에서
    형태를 볼 때 명도 대비가 맞는지 확인하는 용도입니다.
    """
    for name, base, rough, metal in mats:
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
    """
    상자만 모읍니다. 상자 하나가 면 여섯 개이고, 그것이 이 언어의 전부입니다.

    <b>모따기 없음.</b> 메크에서는 0.04 m 모따기가 하이라이트를 잡지만, 건물 크기에서
    같은 비율이면 화소 아래입니다. 노출 콘크리트는 원래 거푸집처럼 날카롭기도 합니다.

    <b>불리언 없음.</b> 구멍은 상자를 빼서가 아니라 <b>구멍 둘레</b>를 세워 만듭니다.
    불리언은 접평면에 얇은 조각을 남기고, 그것이 상자 투영 UV 에서 늘어난 텍셀이 됩니다.
    """

    def __init__(self, mats):
        self.mats = mats
        self.verts = []
        self.faces = []
        self.slots = []

        # 면마다의 <b>파츠 이름</b>입니다. to_parts() 가 이걸로 갈라 냅니다.
        self.part = "Frame"
        self.parts = []

    def group(self, name):
        """
        이 뒤로 쌓는 상자가 속할 <b>파츠</b>입니다.

        <b>왜 나누는가.</b> 한 프리셋을 메시 하나로 구우면 시타델이 454 m 짜리
        바운딩 박스 하나가 됩니다. 그 상자는 데크 위 어디에서나 화면에 걸리므로
        <b>꼭대기의 6 만 삼각형이 발밑에 서 있을 때도 통째로 그려집니다.</b> 컬링은
        렌더러 단위라, 나누지 않으면 나눌 방법이 없습니다.

        <b>잘게 나누면 오히려 손해입니다.</b> 이 프로젝트의 병목은 삼각형이 아니라
        드로우 제출(렌더 스레드 87%)이라, 렌더러를 늘리는 것 자체가 비용입니다.
        그래서 <b>공간적으로 뭉치는 덩어리</b>로만 나눕니다 - 층, 단, 방 하나.

        면을 늘리는 곳이 여럿(box·slope·pierced·reveal)이라 <b>여기서만</b> 밀린
        만큼을 채웁니다. 생성기마다 태그를 달게 하면 새 생성기를 더할 때 조용히
        빠뜨립니다.
        """
        self._tag()
        self.part = name

    def _tag(self):
        while len(self.parts) < len(self.faces):
            self.parts.append(self.part)

    def box(self, center, size, mat=0, taper=0.0, taper_axis=2):
        """축 정렬 상자입니다. <c>taper</c> 는 <c>taper_axis</c> 의 양(+) 쪽 끝을 좁힙니다."""
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
        self.slots += [mat] * 6

    def slope(self, center, size, rise, mat=0, axis=0):
        """
        한쪽 끝이 들린 상자입니다. <c>rise</c> 만큼 <c>axis</c> 방향으로 올라갑니다.

        <b>이 언어에서 경사가 허용되는 유일한 자리입니다.</b> 나머지는 전부 축 정렬인데,
        그것들이 붓거나 용접한 것이기 때문입니다. 경사로도 부어 만들지만 <b>경사인
        것이 그 물건의 정의</b>라 계단으로 흉내 내면 차가 올라갈 수 없습니다.

        면은 여섯 개 그대로입니다. 윗면·아랫면이 함께 기울 뿐입니다.
        """
        c = list(center)
        h = [size[0] * 0.5, size[1] * 0.5, size[2] * 0.5]

        def corner(u, v, top):
            p = [c[0] + u * h[0], c[1] + v * h[1], 0.0]
            along = (u, v)[axis]
            p[2] = c[2] + (h[2] if top else -h[2]) + rise * along * 0.5
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
        self.slots += [mat] * 6

    def clipped(self, center, size, axis, half, mat=0):
        """
        경계에서 <b>잘라 낸</b> 상자입니다. <c>axis</c> 로 ±<c>half</c> 밖은 버립니다.

        이어 붙는 모듈에 씁니다. 이음매를 지나는 부재를 안 자르면 모듈이 제 길이보다
        길어져 이웃과 겹칩니다 - 잘린 반쪽은 옆 모듈의 반쪽과 만나 온전한 하나가 됩니다.
        """
        lo = max(center[axis] - size[axis] * 0.5, -half)
        hi = min(center[axis] + size[axis] * 0.5, half)

        if hi - lo <= 1e-6:
            return

        c = list(center)
        s = list(size)
        c[axis] = (lo + hi) * 0.5
        s[axis] = hi - lo

        self.box(c, s, mat)

    def pierced(self, center, size, holes, hole_w, hole_h, mat=0, axis=0, clip=None):
        """
        긴 보에 <b>경량화 구멍</b>을 냅니다. 구멍이 아니라 구멍 사이의 살을 세웁니다.

        통짜 벽으로 두면 두께가 안 읽혀 그냥 벽이 됩니다. 구멍이 뚫려 있어야
        <b>깊은 보</b>로, 즉 구조로 보입니다.

        <b>살은 테두리 사이만 채웁니다.</b> 처음에는 살을 보 전체 높이로 두었는데,
        그러면 살의 윗면·밑면이 테두리의 윗면·밑면과 <b>같은 평면에서 같은 쪽을
        봅니다</b> - 깊이 버퍼가 고르지 못해 밑에서 올려다볼 때 깜빡입니다.
        감사에서 프리셋마다 수십 쌍씩 잡혔습니다. 지금은 살이 구멍 높이만 차지하고
        위아래는 테두리가 맡으므로, 맞닿는 면들이 서로 <b>반대를 봅니다</b>.
        """
        span = size[axis]
        pitch = span / holes
        web = pitch - hole_w
        rim = (size[2] - hole_h) * 0.5

        for i in range(holes + 1):
            c = list(center)
            c[axis] = center[axis] - span * 0.5 + i * pitch
            s = list(size)
            s[axis] = web
            s[2] = hole_h

            if clip is None:
                self.box(c, s, mat)
            else:
                self.clipped(c, s, axis, clip, mat)

        for sign in (-1.0, 1.0):
            c = list(center)
            c[2] = center[2] + sign * (size[2] - rim) * 0.5
            s = list(size)
            s[2] = rim
            self.box(c, s, mat)

    def reveal(self, center, opening, axis, out, mat=0, sill=0,
               jamb=0.22, depth=0.35, wall=0.24):
        """
        <b>깊은 창 구멍</b>입니다. 벽을 뚫지 않고 구멍의 <b>테두리</b>를 앞으로 세웁니다.

        브루탈리즘에서 창은 유리가 아니라 <b>그림자</b>로 읽힙니다. 벽면에 어두운
        사각형을 칠하면 스티커로 보이고, 테두리를 두껍게 앞으로 내밀면 안쪽이
        그늘에 들어가 진짜 구멍처럼 보입니다. 해가 어디 있든 무너지지 않습니다.

        <c>axis</c> 는 벽의 법선 축(0=x, 1=y), <c>out</c> 은 그 축에서 <b>바깥쪽 부호</b>
        입니다. 처음에는 언제나 +y 로만 내밀게 짰는데, 그러면 반대쪽 벽에서는
        테두리가 벽 <b>안으로</b> 들어가 아무것도 안 보입니다.
        """
        w, h = opening
        plane = 1 - axis  # 벽면 안에서 가로 방향

        def put(off_plane, off_axis, off_z, size_plane, size_axis, size_z, slot):
            c = [center[0], center[1], center[2] + off_z]
            c[plane] += off_plane
            c[axis] += off_axis

            s = [0.0, 0.0, size_z]
            s[plane] = size_plane
            s[axis] = size_axis

            self.box(c, s, slot)

        # 안쪽의 어두운 판. 구멍 바닥입니다.
        put(0.0, 0.0, 0.0, w, wall, h, sill)

        # 테두리 넷. 벽면보다 <c>depth</c> 만큼 바깥으로 나옵니다.
        nose = out * depth * 0.5
        thick = wall + depth

        for sign in (-1.0, 1.0):
            put(sign * (w + jamb) * 0.5, nose, 0.0, jamb, thick, h + jamb * 2.0, mat)
            put(0.0, nose, sign * (h + jamb) * 0.5, w + jamb * 2.0, thick, jamb, mat)

    def to_object(self, name):
        self._tag()

        mesh = bpy.data.meshes.new(name)
        mesh.from_pydata(self.verts, [], self.faces)
        mesh.update()

        for mat_name, _, _, _ in self.mats:
            mesh.materials.append(bpy.data.materials[mat_name])

        for poly, index in zip(mesh.polygons, self.slots):
            poly.material_index = index

        obj = bpy.data.objects.new(name, mesh)
        bpy.context.scene.collection.objects.link(obj)
        return obj

    def to_parts(self, name, min_faces=1500):
        """
        파츠마다 오브젝트 하나입니다. <b>같은 FBX 안에 나란히</b> 나갑니다.

        <b>작으면 도로 합칩니다.</b> 200 삼각형짜리를 셋으로 나누면 걸러서 아끼는
        것은 거의 없는데 드로우만 셋이 됩니다. 실측에서 전부 나누니 드로우가
        48 → 70 으로 늘고 삼각형은 12% 밖에 안 줄어, 드로우 제출이 병목인 이
        프로젝트에서는 작은 것까지 나누는 것이 <b>순손해</b>였습니다.

        FBX 내보내기가 <c>object_types={'MESH'}</c> 라 빈 부모는 나가지 않습니다.
        전부 원점에 있으므로 부모 없이 나란히 두면 유니티에서 FBX 루트의 자식으로
        들어오고, MegastructureSetup.BuildBay 가 자식마다 콜라이더를 답니다 —
        그쪽은 이미 자식 여럿을 전제로 쓰여 있습니다.
        """
        self._tag()

        if len(self.faces) < min_faces:
            return [self.to_object(name)]

        order = []
        for g in self.parts:
            if g not in order:
                order.append(g)

        made = []
        for g in order:
            keep = [f for f, p in zip(self.faces, self.parts) if p == g]
            slots = [m for m, p in zip(self.slots, self.parts) if p == g]

            index = {}
            used = []
            for face in keep:
                for v in face:
                    if v not in index:
                        index[v] = len(used)
                        used.append(v)

            mesh = bpy.data.meshes.new(name + "_" + g)
            mesh.from_pydata([self.verts[v] for v in used], [],
                             [tuple(index[v] for v in f) for f in keep])
            mesh.update()

            for mat_name, _, _, _ in self.mats:
                mesh.materials.append(bpy.data.materials[mat_name])

            for poly, slot in zip(mesh.polygons, slots):
                poly.material_index = slot

            obj = bpy.data.objects.new(name + "_" + g, mesh)
            bpy.context.scene.collection.objects.link(obj)
            made.append(obj)

        return made


# --- Scene ------------------------------------------------------------------


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


def export_fbx(path):
    """
    ⚠ <c>bake_space_transform=True</c> 와 <c>global_scale=1</c> 이 함께여야 합니다.

    끄면 축 변환이 메시가 아니라 오브젝트 트랜스폼에 실려, 유니티가 꺼내 쓰는 Mesh 가
    블렌더의 Z-up 인 채로 옵니다. 켜면 단위까지 제대로 실리므로 배율은 1 입니다.
    """
    os.makedirs(os.path.dirname(path), exist_ok=True)

    bpy.ops.export_scene.fbx(
        filepath=path, use_selection=False, use_visible=False,
        object_types={'MESH'}, use_mesh_modifiers=True, mesh_smooth_type='FACE',
        bake_space_transform=True, add_leaf_bones=False, bake_anim=False,
        apply_scale_options='FBX_SCALE_NONE', global_scale=1.0,
        axis_forward='-Z', axis_up='Y', path_mode='AUTO')

    return os.path.getsize(path)
