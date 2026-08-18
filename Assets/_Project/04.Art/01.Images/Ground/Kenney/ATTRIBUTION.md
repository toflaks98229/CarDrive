# 지면 텍스처 출처

이 폴더의 PNG는 **Kenney Voxel Pack**의 원본입니다. 직접 쓰지 않고,
`PixelGroundTextures` 에디터 도구가 밤 팔레트로 그레이딩한 결과를 터레인 레이어로 씁니다.

| 파일 | 원본 | 쓰임 |
|---|---|---|
| `grass_top.png` | Voxel Pack / Tiles | 풀밭 (`Ground_Grass`) |
| `dirt.png` | Voxel Pack / Tiles | 흙·갓길·마을 부지 (`Ground_Dirt`) |
| `greystone.png` | Voxel Pack / Tiles | 노면 (`Ground_Road`) |

## 라이선스

**CC0 1.0 Universal (퍼블릭 도메인)** — `License.txt` 참고.

> You may use these graphics in personal and commercial projects.

- 개인·교육·상업 프로젝트에서 자유롭게 사용, 수정, 재배포, 판매 가능
- 출처 표기 **의무 없음** (Kenney 측은 자발적 크레딧을 환영함)
- 개작이 허용되므로 색 보정 후 사용하는 것에 문제가 없습니다

## 받은 곳

- 제작: Kenney — <https://kenney.nl>
- 원본 팩: Voxel Pack — <https://kenney.nl/assets/voxel-pack>
- 내려받은 미러: <https://github.com/ETdoFresh/kenney.nl> (`voxel-pack/PNG/Tiles`)

## 왜 그대로 쓰지 않는가

원본은 밝고 채도가 높은 카툰 팔레트입니다. (`grass_top` 평균색 rgb(45, 202, 112))
이 게임은 밤 주행이 기본이고 화면을 세로 215픽셀로 줄여 출력하므로,
그대로 쓰면 지면이 화면에서 튀어 헤드라이트와 귀신이 묻힙니다.

그래서 픽셀 구조는 그대로 두고 밝기와 채도만 낮춰서 씁니다.
보정값은 `PixelGroundTextures.cs`의 `Sources` 배열에서 조정할 수 있습니다.
