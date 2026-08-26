# 하늘 텍스처 출처

두 장 모두 [Poly Haven](https://polyhaven.com) 에서 받았고 **CC0 (Public Domain)** 입니다.
상업적 사용·수정·재배포가 자유롭고 출처 표기 의무도 없습니다 — 이 문서는 의무가 아니라
나중에 "이 파일 어디서 났지" 를 다시 묻지 않으려고 남깁니다. 라이선스 원문: https://polyhaven.com/license

| 파일 | 원본 | 쓰이는 곳 |
|---|---|---|
| `ClearBlueSky_Kloofendal.hdr` | [Kloofendal 43d Clear (Pure Sky)](https://polyhaven.com/a/kloofendal_43d_clear_puresky) | 낮 하늘 (`ClearBlueSky.mat`) |
| `StarNightSky_Qwantani.hdr` | [Qwantani Night (Pure Sky)](https://polyhaven.com/a/qwantani_night_puresky) — Greg Zaal, Jarod Guest | 밤 하늘 (`StarNightSky.mat`) |

## 왜 Pure Sky 판인가

Poly Haven 의 다른 밤 HDRI(`dikhololo_night`, `satara_night`, `rogland_clear_night`)도 전부 CC0 이지만
**지면과 나무가 함께 찍혀 있어** 스카이박스로 쓰면 지평선에 남의 풍경이 띠로 박힙니다.
Pure Sky 판은 하늘만 있습니다.

## 임포트 설정 (두 장 동일)

`textureShape: Cube` / `generateCubemap: Latitude-Longitude` / `maxTextureSize: 2048` / `sRGB` / 밉맵 켬.
4K 원본을 2048 로 줄여 씁니다 — 원본이 4K 여야 줄인 뒤에도 별이 점으로 남습니다.

## 별이 나오는 원리

별을 그려 넣은 것이 아니라 **사진에 이미 HDR 값으로 들어 있는 별을 노출로 끌어올립니다.**
`SkyController.nightSkyExposure` 가 그 손잡이입니다. 어두우면 값을 올리십시오.
