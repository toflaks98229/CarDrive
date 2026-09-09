# -*- coding: utf-8 -*-
"""
하늘을 <b>건축물로 갈아 끼웁니다.</b>

이 세계의 대지는 자연 지형이 아니라 <b>거대 건축물의 인공 지반 한 조각</b>이라는
설정입니다. 그러면 하늘에 있어야 할 것은 구름이 아니라 <b>위층의 밑면</b>이고,
지반 가장자리 너머에 있어야 할 것은 땅이 아니라 <b>바닥이 안 보이는 구름</b>입니다.

<b>왜 스카이맵인가.</b> 진짜 기하로 지으면 원거리 클립 482 m 에 잘립니다. 천장을
620 m 위에 두는 순간 통째로 사라지고, 살리려면 레이어별 컬링 거리와 파클립 예외를
깔아야 합니다. 스카이박스는 <b>언제나 무한대</b>에 그려지므로 그 문제가 없어집니다.
시차가 없다는 것도 여기서는 오히려 맞습니다 - 수 km 밖의 천장은 차로 몇백 m
움직인다고 달라 보이지 않습니다.

<b>아래쪽 반구도 함께 굽습니다.</b> 지반 가장자리에 서서 내려다볼 때 보이는 것이
스카이박스의 아래쪽 반구입니다. 거기에 구름을 깔면 <b>바닥이 안 보이는 지평선</b>이
따라옵니다 - 따로 만들 물건이 아닙니다.

    blender -b -noaudio --python Art/Blender/build_canopy.py

⚠ 이것은 FBX 가 아니라 <b>이미지 한 장</b>을 냅니다. 등장방형(가로:세로 = 2:1)이고,
유니티 쪽에서 <c>generateCubemap: 6</c> 으로 큐브맵이 됩니다 - 기존 하늘 두 장이
쓰는 것과 같은 길입니다.

환경변수 <c>CANOPY_FAST=1</c> 이면 작게 빨리 굽습니다. 구도만 볼 때 씁니다.

<b>여기 적는 값이 곧 화면의 값입니다.</b> 유니티의 <c>Skybox/Cubemap</c> 은
<c>unity_ColorSpaceDouble</c> 로 두 배를 곱하므로, 하늘 노출을 0.5 로 두면 둘이
지워져 <b>구운 선형값이 그대로 화면의 선형값</b>이 됩니다. 그래서 씬의 안개색
0.745 에 맞추고 싶으면 여기 연무를 0.745 로 적으면 됩니다 - 이 뻔한 관계를
얻으려고 노출을 0.5 로 잡았습니다. <c>CanopySkySetup.DayExposure</c> 와 짝입니다.
"""

import math
import os
import random
import sys

import bpy

HERE = os.path.dirname(os.path.abspath(__file__))
if HERE not in sys.path:
    sys.path.insert(0, HERE)

import hardsurface  # noqa: E402

OUT_DIR = r"E:\GamePJ\CarDrive\Assets\_Project\04.Art\01.Images\Skybox"
OUT_NAME = "CanopySky_Baked"

FAST = os.environ.get("CANOPY_FAST") == "1"

WIDTH, HEIGHT, SAMPLES = (1024, 512, 16) if FAST else (4096, 2048, 40)

# --- 재질 -------------------------------------------------------------------
#
# 목록의 차례가 그대로 <c>Mass</c> 의 머티리얼 인덱스입니다.

CONCRETE, DEEP, LAMP = 0, 1, 2

MATS = [
    # 이름,             기본색,              거칠기, 보이는밝기, 비추는밝기
    ("Canopy_Concrete", (0.26, 0.26, 0.25), 0.95, 0.0, 0.0),
    ("Canopy_Deep",     (0.09, 0.09, 0.10), 0.95, 0.0, 0.0),
    ("Canopy_Lamp",     (1.00, 0.78, 0.52), 0.50, 4.5, 30.0),
]

# <b>등은 두 개의 밝기를 가집니다.</b> 하나로 두면 어느 쪽이든 망합니다 - 밝게 하면
# 유니티의 블룸(문턱 0.95)이 하늘을 크림색으로 덮어 버리고, 어둡게 하면 천장이
# 평평한 검정이 되어 <b>격자가 안 보입니다.</b> 카메라에 보이는 값은 문턱을 조금만
# 넘기고, 비추는 값은 크게 두면, 천장이 어두운 채로 <b>등 밑마다 빛 웅덩이</b>가
# 생겨 구조가 읽힙니다. 구운 그림이라 둘이 달라도 됩니다.

# ⚠ <b>빛우물의 밝기는 유니티의 블룸이 정합니다.</b> 처음에는 이 값이 52 였습니다 -
# 천장을 조명하려면 그만큼 필요했으니까요. 그런데 씬의 볼륨이 문턱 0.95, 세기 0.45,
# 따뜻한 색조(1, 0.886, 0.769)로 블룸을 걸어 두어서, 문턱의 55 배짜리 발광면이
# 하늘 전체에 흩뿌려지자 <b>화면이 통째로 크림색 안개</b>가 되었습니다 - 블렌더에서는
# 어두운 회색 천장에 밝은 등이었던 것이, 게임에서는 대비가 사라진 세피아 한 장이
# 되었습니다.
#
# 그래서 <b>조명과 밝기를 갈라 놓습니다.</b> 천장을 밝히는 일은 월드의 채움광이
# 맡고(구워 낼 그림이므로 물리적으로 맞을 필요가 없습니다), 빛우물은 문턱을
# 조금만 넘겨 <b>등으로 보이는 데까지만</b> 밝습니다.

# --- 치수 -------------------------------------------------------------------
#
# 카메라는 원점에 섭니다. 곧 Z = 0 이 <b>플레이어가 서 있는 인공 지반</b>입니다.

CEILING = 2400.0    # 위층 밑면까지의 높이
BAY = 340.0         # 1차 보의 간격. 지평선 쪽 리듬을 만듭니다
BEAM = 96.0         # 1차 보의 깊이
RIB = 68.0          # 2차 보의 간격. 머리 바로 위를 채웁니다
RIBDEEP = 17.0

# ⚠ <b>보가 깊으면 먼 천장이 스스로 닫힙니다.</b> 깊이 46 m 를 간격 68 m 에 걸었더니
# <c>atan(46/68) = 34</c> 도 아래로는 소핏이 안 보이고 보의 <b>옆면</b>만 보여, 천장이
# 통째로 검은 띠가 되었습니다(화면의 14%). 얕게 눕히면 12 도까지 열립니다.
REACH = 60000.0     # 천장이 뻗는 거리
WELLS = 22000.0     # 빛우물을 놓는 거리. 그 밖은 어차피 화소 아래입니다
CLOUD_Z = -2600.0   # 구름 바다의 높이. 지반 아래입니다

# <b>천장은 620 m 로는 창고였습니다.</b> 첫 시험에서 기둥이 지평선 위 29 도에서
# 천장에 막혀 <b>짧은 기둥 몇 개가 선 실내</b>로 보였습니다. 이 그림이 팔아야 하는
# 것은 "위층이 있다" 가 아니라 <b>"위층이 얼마나 위인지 모르겠다"</b> 라, 높이가
# 곧 이 그림의 전부입니다.
#
# 간격은 높이를 따라갑니다. 천장 꼭대기에서 한 칸이 <c>atan(BAY/2 / CEILING)</c>
# 만큼 벌어지므로, 340 m / 2400 m 면 8 도 - 60 도 화면에 일곱 칸입니다. 더 벌리면
# 머리 위가 다시 비고, 좁히면 화소 아래로 내려가 무늬가 어른거립니다.


# 지평선의 색. 천장도 구름도 여기로 수렴합니다.
#
# ⚠ <b>고른 색이 아니라 받아 적은 색입니다.</b> 씬의 <c>SkyController.dayFogColor</c>
# 가 sRGB (0.878, 0.812, 0.765) 라, 먼 지형은 저 따뜻한 크림색으로 사라집니다.
# 처음에 하늘의 연무를 푸른 회색으로 두었더니 <b>지평선에 색의 이음매</b>가
# 그어졌습니다 - 땅은 크림색으로 흐려지는데 그 위 하늘만 파랬습니다. 같은 색으로
# 맞추면 둘이 만나는 자리가 사라집니다.
#
# 따뜻한 연무가 이 세계에서 앞뒤가 맞기도 합니다. 하늘이 막혀 있으니 빛의 출처가
# 빛우물뿐이고, 그 빛이 (1.00, 0.78, 0.52) 로 따뜻합니다.
HAZE = (0.745, 0.624, 0.546)   # 위 sRGB 를 선형으로 옮긴 값
HAZE_NEAR = 600.0              # 이 거리까지는 맑습니다
HAZE_FAR = 40000.0             # 이 거리 밖은 연무뿐입니다


def fade(tree, surface):
    """
    카메라와의 거리에 따라 <c>surface</c> 를 연무색으로 덮습니다.

    <b>거리는 흐려짐으로만 읽힙니다.</b> 이것이 없으면 60 km 밖의 천장이 머리 위
    2.4 km 와 똑같이 새카매서, 멀다는 것이 아니라 <b>비었다</b>는 뜻이 됩니다.
    지평선에서 천장과 구름이 같은 색으로 만나므로, 둘 사이에 남던 빈 띠도 함께
    사라집니다.

    ⚠ 볼륨 산란이 물리적으로는 맞지만 60 km 짜리 장면에서는 감당이 안 되고,
    합성기의 미스트 패스는 블렌더 5 에서 <c>scene.node_tree</c> 가 없어져
    노드 그룹으로 바뀌었습니다. 재질 안에 넣으면 판본을 안 탑니다.
    """
    nodes, links = tree.nodes, tree.links

    eye = nodes.new("ShaderNodeCameraData")

    ramp = nodes.new("ShaderNodeMapRange")
    ramp.clamp = True
    ramp.inputs["From Min"].default_value = HAZE_NEAR
    ramp.inputs["From Max"].default_value = HAZE_FAR

    veil = nodes.new("ShaderNodeEmission")
    veil.inputs["Color"].default_value = (HAZE[0], HAZE[1], HAZE[2], 1.0)
    veil.inputs["Strength"].default_value = 1.0

    mix = nodes.new("ShaderNodeMixShader")

    links.new(eye.outputs["View Distance"], ramp.inputs["Value"])
    links.new(ramp.outputs["Result"], mix.inputs[0])
    links.new(surface.outputs[0], mix.inputs[1])
    links.new(veil.outputs[0], mix.inputs[2])

    return mix


def split(tree, color, seen, lit):
    """카메라에 보이는 밝기와 <b>비추는 밝기</b>가 다른 발광면입니다."""
    nodes, links = tree.nodes, tree.links
    rgba = (color[0], color[1], color[2], 1.0)

    shown = nodes.new("ShaderNodeEmission")
    shown.inputs["Color"].default_value = rgba
    shown.inputs["Strength"].default_value = seen

    source = nodes.new("ShaderNodeEmission")
    source.inputs["Color"].default_value = rgba
    source.inputs["Strength"].default_value = lit

    path = nodes.new("ShaderNodeLightPath")
    mix = nodes.new("ShaderNodeMixShader")

    links.new(path.outputs["Is Camera Ray"], mix.inputs[0])
    links.new(source.outputs[0], mix.inputs[1])
    links.new(shown.outputs[0], mix.inputs[2])

    return mix


def materials():
    """Cycles 로 <b>구울</b> 재질입니다. 내보내기용 툰 재질과는 쓰임이 다릅니다."""
    for name, color, rough, seen, lit in MATS:
        mat = bpy.data.materials.get(name) or bpy.data.materials.new(name)
        mat.use_nodes = True

        tree = mat.node_tree
        bsdf = tree.nodes.get("Principled BSDF")
        out = tree.nodes.get("Material Output")

        bsdf.inputs["Base Color"].default_value = (color[0], color[1], color[2], 1.0)
        bsdf.inputs["Roughness"].default_value = rough

        surface = bsdf if lit <= 0.0 else split(tree, color, seen, lit)

        tree.links.new(fade(tree, surface).outputs[0], out.inputs["Surface"])


def build():
    hardsurface.wipe()
    materials()

    rng = random.Random(20260909)
    mass = hardsurface.Mass([(m[0], None, None, None) for m in MATS])

    span = REACH * 2.0

    # ---- 천장 -------------------------------------------------------------
    mass.box((0.0, 0.0, CEILING + 60.0), (span, span, 120.0), CONCRETE)

    for pitch, deep, wide, slot in ((RIB, RIBDEEP, 13.0, CONCRETE),
                                    (BAY, BEAM, 34.0, DEEP)):
        for i in range(-int(REACH / pitch), int(REACH / pitch) + 1):
            at = i * pitch
            mass.box((at, 0.0, CEILING - deep * 0.5), (wide, span, deep), slot)
            mass.box((0.0, at, CEILING - deep * 0.5), (span, wide, deep), slot)

    # 빛우물. <b>이 세계의 해</b>입니다 - 하늘이 막혔으니 빛은 천장에서 옵니다.
    #
    # 천장 전체에 깔면 발광면이 12 만 개가 되어 광원 표집이 무너집니다. 22 km
    # 밖은 화소보다 작아 어차피 <b>하나의 띠</b>로 뭉개지므로 거기서 끊습니다.
    cells = int(WELLS / BAY)

    for i in range(-cells, cells + 1):
        for j in range(-cells, cells + 1):
            if rng.random() > 0.22:
                continue

            mass.box((i * BAY + BAY * 0.5, j * BAY + BAY * 0.5, CEILING - 7.0),
                     (BAY * 0.34, BAY * 0.34, 14.0), LAMP)

    # ---- 대기권을 뚫는 기둥 -----------------------------------------------
    #
    # 지반 아래에서 올라와 천장으로 들어갑니다. 시작도 끝도 화면 밖이어야
    # "어디까지 가는지 모르겠다" 가 됩니다.
    for k in range(16):
        angle = k * (math.pi * 2.0 / 16.0) + rng.random() * 0.35
        far = 1100.0 + rng.random() * 9000.0
        wide = 110.0 + far * 0.05

        x = math.cos(angle) * far
        y = math.sin(angle) * far

        mass.box((x, y, 0.0), (wide, wide, CEILING * 4.0), CONCRETE)

        # 세로 골. 민짜 기둥은 굵기만 있고 <b>크기가 없습니다.</b>
        for s in (-1.0, 1.0):
            mass.box((x + s * wide * 0.5, y, 0.0),
                     (wide * 0.14, wide * 0.60, CEILING * 4.0), DEEP)
            mass.box((x, y + s * wide * 0.5, 0.0),
                     (wide * 0.60, wide * 0.14, CEILING * 4.0), DEEP)

        # 층띠. 되풀이가 있어야 <b>얼마나 먼지</b>가 읽힙니다.
        for f in range(1, int(CEILING / 190.0)):
            mass.box((x, y, f * 190.0), (wide * 1.12, wide * 1.12, 22.0), DEEP)

        # 천장에 닿는 자리의 받침.
        mass.box((x, y, CEILING - BEAM), (wide * 1.6, wide * 1.6, BEAM * 1.5), CONCRETE)

    obj = mass.to_object("Canopy")

    print("###CANOPY-FACES###" + str(len(obj.data.polygons)))
    return obj


def clouds():
    """
    구름 바다입니다. 지반 가장자리에서 <b>내려다볼 때</b> 보이는 것이고, 바닥이
    보이면 이 대지가 한 조각이라는 감각이 통째로 사라집니다.

    ⚠ <b>상자로 만들면 안 됩니다.</b> 처음에는 이 파일의 다른 것들처럼 상자를
    흩뿌렸는데, 400~1800 m 짜리 발광 정육면체가 <b>흰 상자 무더기</b>로 보였습니다 -
    구름은 이 언어에 속하지 않는 유일한 물건입니다.

    대신 <b>평평한 판 둘에 잡음을 칠합니다.</b> 스치는 각도로 보이므로 판이라는
    것이 드러나지 않고, 잡음이 늘어나 오히려 구름결이 됩니다. 아래 판을 어둡게
    깔면 사이가 벌어진 곳이 깊어 보여 <b>두께</b>가 생깁니다.
    """
    span = REACH * 3.0

    for name, z, bright, seed in (("Cloud_Low", CLOUD_Z - 900.0, 0.19, 3.0),
                                  ("Cloud_Top", CLOUD_Z, 0.60, 0.0)):
        mesh = bpy.data.meshes.new(name)
        mesh.from_pydata([(-span, -span, z), (span, -span, z),
                          (span, span, z), (-span, span, z)], [], [(0, 1, 2, 3)])
        mesh.update()

        mat = bpy.data.materials.new(name)
        mat.use_nodes = True

        nodes = mat.node_tree.nodes
        links = mat.node_tree.links
        nodes.clear()

        out = nodes.new("ShaderNodeOutputMaterial")

        # ⚠ <b>구름은 보이는 것보다 훨씬 덜 비춰야 합니다.</b> 채움광을 2.6 에서
        # 0.75 로 3 분의 1 넘게 줄였는데 천장은 0.183 → 0.158 밖에 안 어두워졌습니다 -
        # 천장을 밝히던 것은 채움광이 아니라 <b>바로 아래 깔린 구름 바다</b>였습니다.
        # 반구를 통째로 덮은 발광면이라 채움광보다 훨씬 셉니다.
        #
        # 구름은 밝게 보여야 하고 천장은 어두워야 하므로, 월드에 쓴 것과 같은
        # 수를 씁니다 - 카메라에 보이는 밝기와 <b>광원으로서의 밝기</b>를 가릅니다.
        emit = nodes.new("ShaderNodeEmission")
        emit.inputs[1].default_value = bright

        lamp = nodes.new("ShaderNodeEmission")
        lamp.inputs[1].default_value = bright * 0.035

        path = nodes.new("ShaderNodeLightPath")
        pick = nodes.new("ShaderNodeMixShader")

        links.new(path.outputs["Is Camera Ray"], pick.inputs[0])
        links.new(lamp.outputs[0], pick.inputs[1])
        links.new(emit.outputs[0], pick.inputs[2])

        ramp = nodes.new("ShaderNodeValToRGB")
        ramp.color_ramp.elements[0].position = 0.36
        ramp.color_ramp.elements[0].color = (0.30, 0.33, 0.42, 1.0)
        ramp.color_ramp.elements[1].position = 0.66
        ramp.color_ramp.elements[1].color = (1.00, 0.99, 0.97, 1.0)

        noise = nodes.new("ShaderNodeTexNoise")
        noise.inputs["Scale"].default_value = 1.0
        noise.inputs["Detail"].default_value = 12.0
        noise.inputs["Roughness"].default_value = 0.62

        # <c>Generated</c> 는 바운딩 박스를 0~1 로 눌러 놓으므로 판이 넓을수록
        # 무늬가 늘어납니다 - 여기서는 360 km 라 아무것도 안 보입니다. 오브젝트
        # 좌표는 미터 그대로라, 6 km 를 잡음 한 칸으로 놓으면 구름 덩이가
        # 2~4 km 가 됩니다. 층마다 위치를 밀어 두 판이 <b>다른 구름</b>이 됩니다.
        place = nodes.new("ShaderNodeMapping")
        place.inputs["Scale"].default_value = (1.0 / 6000.0, 1.0 / 6000.0, 1.0)
        place.inputs["Location"].default_value = (seed, seed * 1.7, 0.0)

        coord = nodes.new("ShaderNodeTexCoord")

        links.new(coord.outputs["Object"], place.inputs["Vector"])
        links.new(place.outputs["Vector"], noise.inputs["Vector"])
        links.new(noise.outputs["Fac"], ramp.inputs["Fac"])
        links.new(ramp.outputs["Color"], emit.inputs["Color"])
        links.new(ramp.outputs["Color"], lamp.inputs["Color"])
        links.new(fade(mat.node_tree, pick).outputs[0], out.inputs["Surface"])

        mesh.materials.append(mat)
        bpy.context.scene.collection.objects.link(bpy.data.objects.new(name, mesh))


def world():
    """
    <b>하늘은 없습니다.</b> 이 그림의 요점이 그것이라, 배경은 하늘이 아니라
    지평선의 <b>연무</b>입니다. 천장 끝(+1.8도)과 구름 끝(-1.7도) 사이에 남는
    좁은 띠를 메우는 것이 이 색의 일입니다.

    <b>카메라에 보이는 색과 비추는 색을 가릅니다.</b> 연무를 그대로 광원으로 쓰면
    사방에서 같은 세기로 비쳐 천장의 골이 전부 뭉개집니다 - 빛은 빛우물에서
    와야 합니다. <c>Light Path</c> 의 카메라 광선 여부로 둘을 갈라 놓습니다.
    """
    w = bpy.data.worlds.new("CanopyWorld")
    w.use_nodes = True

    nodes = w.node_tree.nodes
    links = w.node_tree.links
    nodes.clear()

    out = nodes.new("ShaderNodeOutputWorld")

    fill = nodes.new("ShaderNodeBackground")
    fill.inputs[0].default_value = (0.34, 0.32, 0.31, 1.0)
    # ⚠ <b>채움광의 세기가 곧 천장의 값입니다.</b> 2.6 으로 두었더니 머리 위
    # 천장이 선형 0.183 - sRGB 로 0.47, 곧 <b>중간 회색</b>이었습니다. 블렌더
    # 파노라마 안에서는 밝은 구름 바다 옆이라 어두워 보였을 뿐, 실제로는
    # 지붕이 아니라 밝은 천장이었습니다. 이 세계의 하늘은 <b>덮여 있는 것</b>이라
    # 값이 낮아야 하고, 등이 그 위에서 터져야 합니다.
    fill.inputs[1].default_value = 0.14

    # ⚠ <b>연무색과 같아야 합니다.</b> 천장의 끝(고도 2.3도)과 구름의 끝(-0.8도)
    # 사이에는 아무것도 없어서 이 배경이 그대로 보입니다. 푸른 회색으로 두었더니
    # 지평선에 <b>파란 띠</b>가 한 줄 그어졌습니다 - 3 도짜리 틈이지만 눈이 가장
    # 오래 머무는 자리라 대번에 보입니다.
    haze = nodes.new("ShaderNodeBackground")
    haze.inputs[0].default_value = (HAZE[0], HAZE[1], HAZE[2], 1.0)
    haze.inputs[1].default_value = 1.0

    mix = nodes.new("ShaderNodeMixShader")
    path = nodes.new("ShaderNodeLightPath")

    links.new(path.outputs["Is Camera Ray"], mix.inputs[0])
    links.new(fill.outputs[0], mix.inputs[1])
    links.new(haze.outputs[0], mix.inputs[2])
    links.new(mix.outputs[0], out.inputs[0])

    bpy.context.scene.world = w


def camera():
    """
    <b>등장방형 파노라마.</b> 유니티가 큐브맵으로 구워 읽는 형식입니다.

    카메라를 X 로 90 도 세웁니다. 등장방형은 카메라의 로컬 +Y 가 이미지 위쪽으로
    가므로, 세워야 <b>월드의 위</b>가 이미지 위쪽이 됩니다. 눕힌 채로 구우면
    천장과 구름이 뒤바뀝니다.
    """
    data = bpy.data.cameras.new("CanopyCam")
    data.type = "PANO"

    if hasattr(data, "panorama_type"):
        data.panorama_type = "EQUIRECTANGULAR"
    else:
        data.cycles.panorama_type = "EQUIRECTANGULAR"

    cam = bpy.data.objects.new("CanopyCam", data)
    bpy.context.scene.collection.objects.link(cam)

    cam.location = (0.0, 0.0, 0.0)
    cam.rotation_euler = (math.radians(90.0), 0.0, 0.0)

    # ⚠ <b>먼 클립을 늘려야 합니다.</b> 새 카메라의 기본값은 100 m 라, 그대로 두면
    # 천장이 1 km 에서 잘려 <b>고도 38 도 아래로는 아무것도 없는 회색 띠</b>가
    # 됩니다 - 첫 시험에서 그 띠를 연무로 착각할 뻔했습니다. 구름 바다의 모서리가
    # 40 km 쯤이므로 그보다 넉넉히 둡니다.
    data.clip_start = 1.0
    data.clip_end = 120000.0

    bpy.context.scene.camera = cam


def device(scene):
    """
    배치 환경에서는 계산 장치가 <b>꺼져 있는 것이 기본</b>입니다. 켜지 않으면
    <c>device = "GPU"</c> 라고 적어 두어도 조용히 CPU 로 돕니다 - 8 백만 화소를
    CPU 로 구우면 몇십 분입니다.
    """
    try:
        prefs = bpy.context.preferences.addons["cycles"].preferences
    except Exception as e:
        print("###CANOPY-DEVICE###CPU " + str(e))
        return

    for kind in ("OPTIX", "CUDA", "HIP", "ONEAPI"):
        try:
            prefs.compute_device_type = kind
        except TypeError:
            continue

        prefs.get_devices()
        picked = [d for d in prefs.devices if d.type != "CPU"]

        if picked:
            for d in prefs.devices:
                d.use = True

            scene.cycles.device = "GPU"
            print("###CANOPY-DEVICE###" + kind + " " + picked[0].name)
            return

    print("###CANOPY-DEVICE###CPU none")


def bake():
    scene = bpy.context.scene

    scene.render.engine = "CYCLES"
    scene.cycles.samples = SAMPLES
    scene.cycles.use_denoising = True

    device(scene)

    scene.render.resolution_x = WIDTH
    scene.render.resolution_y = HEIGHT
    scene.render.resolution_percentage = 100

    os.makedirs(OUT_DIR, exist_ok=True)

    scene.render.image_settings.file_format = "HDR"
    scene.render.filepath = os.path.join(OUT_DIR, OUT_NAME)

    bpy.ops.render.render(write_still=True)

    path = scene.render.filepath + ".hdr"
    size = os.path.getsize(path) if os.path.exists(path) else 0

    print("###CANOPY###" + str(size) + " " + str(WIDTH) + "x" + str(HEIGHT) + " " + path)


if __name__ == "__main__":
    build()
    clouds()
    world()
    camera()
    bake()
