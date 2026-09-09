# -*- coding: utf-8 -*-
"""
<b>결 텍스처를 굽습니다.</b> 셰이더가 이 맵을 색이 아니라 <b>결</b>로만 쓰므로,
중요한 것은 밝기가 아니라 <b>대비</b>입니다.

⚠ <b>사진의 확산광(diffuse)을 쓰면 안 됩니다.</b> 콘크리트 벽 사진의 5~95% 폭을
재 보면 0.93~1.05 입니다 — 평평한 회색 벽은 평평한 회색 사진이니 당연합니다.
거기에 <c>_BaseMapStrength</c> 0.45 를 곱하면 ±2% 가 되어 <b>말 그대로 단색</b>이
됩니다. 실제로 그렇게 들어가 있었고, 화면에서 콘크리트가 단색으로 보였습니다.

결은 색이 아니라 <b>요철</b>에 있습니다. 같은 에셋의 변위(displacement) 맵은
0.67~1.30 으로 확산광의 여섯 배가 넘는 폭을 가집니다. 여기에 앰비언트 오클루전을
곱하면 파인 곳이 한 번 더 어두워져 깊이가 생깁니다.

⚠ <b>감마로 평균을 맞추지 마십시오.</b> 예전 판이 어두운 사진의 선형 평균을 0.505 로
끌어올리려고 감마를 걸었는데, 그러면 밝은 쪽이 눌려 <b>대비가 함께 죽습니다.</b>
평균은 런타임에 <c>_BaseMapGain</c> 이 나눠 없애므로 <b>맞출 필요가 아예 없습니다</b> —
여기서는 평균으로 나눈 뒤 1 을 중심으로 <b>비례</b>해서만 늘리고 줄입니다.

    blender -b -noaudio --python Art/Blender/build_surfaces.py
"""

import os
import sys

import bpy

SRC = r"C:\Users\jeje0\Downloads\concrete_wall_001_1k\textures"
OUT = r"E:\GamePJ\CarDrive\Assets\_Project\04.Art\01.Images\Surfaces"

# 이득으로 나눈 뒤 2~98% 가 앉을 자리입니다.
#
# <b>위쪽이 좁은 것은 일부러입니다.</b> 툰 램프가 명암을 계단으로 끊으므로 밝은
# 쪽을 넓히면 밝은 절반이 통째로 하얗게 탑니다. 어두운 쪽은 그늘로 읽혀 안전합니다.
LOW, HIGH = 0.58, 1.34

# 저장할 때의 선형 평균. 값 자체는 뜻이 없지만(런타임에 나뉩니다) 8 비트 계단이
# 보이지 않도록 폭이 넓게 앉는 자리를 고릅니다.
MEAN = 0.42


def linear(c):
    return c / 12.92 if c <= 0.04045 else ((c + 0.055) / 1.055) ** 2.4


def encode(c):
    c = max(0.0, min(1.0, c))
    return c * 12.92 if c <= 0.0031308 else 1.055 * (c ** (1.0 / 2.4)) - 0.055


def read(name):
    """이미지 한 장을 <b>평균이 1 인 선형 배열</b>로 읽습니다."""
    img = bpy.data.images.load(os.path.join(SRC, name))
    w, h = img.size
    px = list(img.pixels)

    vals = [linear(px[i]) for i in range(0, w * h * 4, 4)]
    mean = sum(vals) / len(vals)

    bpy.data.images.remove(img)

    return [v / mean for v in vals], w, h


def bake(disp, ao, out_name):
    relief, w, h = read(disp)
    shade, _, _ = read(ao)

    # 요철에 오클루전을 곱합니다. 파인 곳이 두 번 어두워져 깊이가 생깁니다.
    mix = [relief[i] * shade[i] for i in range(len(relief))]

    mean = sum(mix) / len(mix)
    mix = [v / mean for v in mix]

    # <b>1 을 중심으로 비례해 늘립니다.</b> 지금의 2~98% 폭을 재서, 목표 폭에
    # 닿도록 한 번만 곱합니다 - 감마처럼 한쪽을 누르지 않습니다.
    ranked = sorted(mix)
    n = len(ranked)
    low = ranked[n // 50]
    high = ranked[n - n // 50 - 1]

    spread = max((1.0 - LOW) / max(1.0 - low, 1e-6),
                 (HIGH - 1.0) / max(high - 1.0, 1e-6))

    mix = [1.0 + (v - 1.0) * spread for v in mix]
    mix = [max(0.06, min(2.2, v)) for v in mix]

    pixels = []
    for v in mix:
        c = encode(v * MEAN)
        pixels += [c, c, c, 1.0]

    made = bpy.data.images.new(out_name, w, h, alpha=False)
    made.pixels = pixels
    made.filepath_raw = os.path.join(OUT, out_name)
    made.file_format = "JPEG"
    made.save()

    ranked = sorted(mix)
    print("###SURFACE###%s %dx%d 2%% %.2f · 중앙 %.2f · 98%% %.2f · 늘림 x%.1f"
          % (out_name, w, h, ranked[n // 50], ranked[n // 2],
             ranked[n - n // 50 - 1], spread))


if __name__ == "__main__":
    bake("concrete_wall_001_disp_1k.png",
         "concrete_wall_001_ao_1k.png",
         "CC0_Concrete_1K.jpg")
