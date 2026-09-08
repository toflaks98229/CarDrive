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
