# 사운드 출처 및 라이선스

이 폴더의 모든 오디오는 **CC0 1.0 (퍼블릭 도메인)** 입니다.
귀속 표기 의무가 없지만, **어디서 왔는지 기록해 두는 것이 의무보다 중요합니다** —
나중에 "이 파일 써도 되나"를 다시 물을 때 답할 수 있어야 하기 때문입니다.

라이선스는 검색 요약이 아니라 **각 배포 페이지 원문에서 직접 확인**했습니다.
(조사 중 어떤 자료는 요약이 "CC0"라고 했지만 실제로는 CC-BY/GPL이었습니다)

---

## Vehicle/ — 엔진 루프 6종

| 항목 | 내용 |
|---|---|
| 원제 | racing car engine sound loops |
| 제작 | domasx2 |
| 라이선스 | CC0 1.0 (퍼블릭 도메인) |
| 출처 | https://opengameart.org/content/racing-car-engine-sound-loops |
| 원본 | pdsounds.org 의 퍼블릭 도메인 차량 시동음을 잘라 루프로 편집 |
| 파일 | `engine_loop_0.wav` ~ `engine_loop_5.wav` (WAV, 각 54~76KB) |

여섯 개는 **같은 소리의 피치 변형**입니다. 원 제작자가 루프가 매끄럽도록 편집했습니다.
`CarSoundController` 가 RPM 에 따라 피치를 다시 조절하므로, 보통은 가운데 것 하나만 물리면 됩니다.
나머지는 차종을 늘릴 때 쓰거나, 기본 피치를 바꿔 보고 싶을 때 쓰세요.

## Ghost/ — 귀신 목소리 5종

| 항목 | 내용 |
|---|---|
| 원제 | Ghost Monster Voice Moaning & Growling |
| 제작 | qubodup |
| 라이선스 | CC0 1.0 (퍼블릭 도메인) |
| 출처 | https://opengameart.org/content/ghost-monster-voice-moaning-growling |
| 파일 | `qubodup-GhostMoan01.wav` ~ `05.wav` (WAV, 각 0.3~1.0MB) |

배포본에는 같은 소리의 MP3 사본도 들어 있었으나 **WAV 만 가져왔습니다.**
같은 소리가 두 벌 있으면 어느 것이 쓰이는지 헷갈리고, MP3 는 루프 이음매에 틱이 생깁니다.

## Impact/ — 충돌음 19종

| 항목 | 내용 |
|---|---|
| 원제 | 100 CC0 metal and wood SFX |
| 제작 | rubberduck |
| 라이선스 | CC0 1.0 (퍼블릭 도메인) |
| 출처 | https://opengameart.org/content/100-cc0-metal-and-wood-sfx |
| 파일 | `metal_hit_*`, `metal_falling_*`, `metal_slam_*`, `wood_hit_*`, `wood_breaking_*` (OGG) |

원 배포본은 100개지만 **19개만 가져왔습니다.** 나머지는 문 여닫기·열쇠·망치·연장 소리라
이 게임에 쓰일 자리가 없습니다. 필요해지면 위 출처에서 같은 팩을 다시 받으면 됩니다.

## Weapon/ — 무장 발사음 8종

| 항목 | 내용 |
|---|---|
| 원제 | 25 CC0 bang / firework SFX |
| 제작 | rubberduck |
| 라이선스 | CC0 1.0 (퍼블릭 도메인) |
| 출처 | https://opengameart.org/content/25-cc0-bang-firework-sfx |
| 원본 | 실제 불꽃놀이를 녹음해 자른 것 |
| 파일 | `cannon_01~03.ogg` · `shot_01~03.ogg` · `bang_06.ogg` · `bang_08.ogg` (OGG) |

**같은 제작자의 팩입니다** — 위 `Impact/` 의 금속·나무 소리를 만든 사람과 같습니다.
그래서 결이 이미 맞고, 라이선스도 같은 방식으로 확인되어 있습니다.

원 배포본은 25개지만 **8개만 가져왔습니다.** 불꽃놀이 7종(`fw_*`, 600KB 짜리 루프 포함)은
이 게임에 쓸 자리가 없고, 나머지 `bang_*` 은 위 여덟과 겹칩니다.

| 파일 | 어디에 |
|---|---|
| `cannon_02` (가장 김) | 공성포 — 한 발이 기계를 밀어내는 그 소리 |
| `cannon_01` · `cannon_03` | 중포 — 둘을 번갈아 써서 같은 소리가 반복되지 않게 |
| `shot_01` · `shot_02` | 연장포 — 좌우 포신이 번갈아 밀리므로 소리도 번갈아 |
| `shot_03` (가장 짧음) | 회전포 — 0.07 초마다 나가므로 가장 짧아야 합니다 |
| `bang_06` · `bang_08` | 미사일 랙 — 발사관을 떠나는 둔한 소리 |

**착탄음은 새로 받지 않았습니다.** `Impact/metal_hit_*` 가 이미 있고, 금속에 맞는
소리로는 그것이 맞습니다.

---

## 아직 없는 소리

아래는 CC0 범위에서 적당한 것을 찾지 못했습니다. 슬롯은 비어 있고,
`AudioUtility` 가 null 클립을 걸러내므로 예외는 나지 않습니다. 그냥 소리가 안 납니다.

| 필요한 것 | 쓰이는 곳 |
|---|---|
| 로터 회전 루프 | `RobotWeapon` 회전포 — 도는 동안 나는 소리. 지금은 발사음만 있습니다 |
| 윈치 · 줄 감김 | `WeaponWinch` — 견인 갈고리가 줄을 풀고 감을 때 |
| 엔진 시동 · 정지 | `CarSoundController.engineStartClip` / `engineStopClip` |
| 앙크 충전 · 발사 루프 · 해제 | `PlayerSoundController` 3종 |
| 음료 마시기 | `PlayerSoundController.drinkSound` |
| 상호작용 실패 | `PlayerSoundController.interactionFailSound` |
| 귀신 등장 | `EnemySoundController.spawnSound`, `AttachedGhostSoundController.spawnSound` |

앙크 소리는 어느 CC0 저장소에도 맞는 것이 없었습니다.
이 게임 고유의 연출이라 직접 만들거나 Freesound 의 CC0 필터에서 개별 선별하는 편이 낫습니다.

## 검토하다 쓰지 않은 것

| 후보 | 쓰지 않은 이유 |
|---|---|
| [pmndrs/racing-game](https://github.com/pmndrs/racing-game) | README 가 "CC0 assets only" 라고만 하고 **파일별 라이선스·크레딧 기록이 없음.** 게다가 MP3 라 엔진 루프에 부적합 |
| [OGA · Car Engine Loop 96kHz](https://opengameart.org/content/car-engine-loop-96khz-4s) | 음질은 가장 좋지만 **CC-BY 3.0 / GPL 이라 CC0 아님.** 귀속 표기 의무 발생 |
| [KenneyNL/Starter-Kit-Racing](https://github.com/KenneyNL/Starter-Kit-Racing) | CC0 이고 출처도 명확하지만 양식화된 아케이드 사운드라 실사 방향과 맞지 않음 |
| [lavenderdotpet/CC0-Public-Domain-Sounds](https://github.com/lavenderdotpet/CC0-Public-Domain-Sounds) | 여러 출처를 모아 재업로드한 것이라 **파일별 출처를 되짚을 수 없음.** 차량음도 없음 |
