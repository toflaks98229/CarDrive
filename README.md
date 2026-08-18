# CarDrive

밤길을 달리는 1인칭 차량 운전 + 귀신 퇴치 서바이벌 게임 (Unity / URP).

## 개요

CarDrive는 플레이어가 차량 운전석에 앉아 1인칭 시점으로 무한히 이어지는 도로를 주행하는 Unity 게임입니다. 시동을 걸고(E) 기어·RPM·연료를 관리하며 달리는 동안, 차량의 뒤쪽과 옆쪽에 귀신이 달라붙어 지속적으로 차량 체력을 깎습니다. 플레이어는 마우스로 시점을 돌려 앙크(Ankh)를 들어(좌클릭) 일정 시간 충전한 뒤 귀신을 조준해 퇴치하고, 차 안의 음료 상자에서 음료를 꺼내 마셔 체력을 회복합니다. 화면은 URP 커스텀 렌더러 피처(픽셀화 + 색상 팔레트 양자화)를 통해 레트로 픽셀 룩으로 출력됩니다.

## 기술 스택

- **엔진**: Unity 2022.3.62f2 (LTS)
- **언어**: C# (Assembly-CSharp, .NET / Mono)
- **렌더 파이프라인**: Universal RP 14.0.12 (URP), Shader Graph 기반 스프라이트 셰이더
- **주요 패키지**: TextMesh Pro 3.0.7, Splines 2.8.2, Timeline 1.7.7, Visual Scripting 1.9.4, uGUI
- **물리**: Unity 내장 Rigidbody + WheelCollider (com.unity.modules.vehicles)
- **외부 에셋**: LowPolyRetroCars, Cartoon FX Remaster (JMO Assets), tree_pack, Crate/Barrels, Bottles
- **빌드 타깃**: Windows 스탠드얼론 (`BUILD/CarDrive.exe` 빌드 산출물 포함)

## 주요 기능 / 시스템

### 차량 시뮬레이션 (`Assets/_Project/02.Scripts/Gameplay/Vehicle/`)
- **컴포넌트 분리 구조**: `CarController`(조정자) + `CarInput`(입력) + `Powertrain`(동력계) + `CarVisuals`(휠 메시) + `CarCollisionHandler`(충돌) 로 역할이 나뉜 구조.
- **CarData (ScriptableObject)**: 모터 토크, 브레이크 토크, 토크 곡선(AnimationCurve), 아이들/최대 RPM, 변속 임계 RPM, 기어비 리스트, 최대 연료·연료 소모율, 최대 조향각·조향 보조값을 에셋으로 관리.
- **Powertrain**: 휠 RPM 기반 엔진 RPM 계산, 자동 변속(업/다운 시프트), 후진/중립 기어 처리, RPM·스로틀 비례 연료 소모. 연료가 0이 되면 시동이 꺼집니다.
- **구동/조향 방식**: FWD / RWD / AWD, 전륜 조향 / 4륜 조향 선택 가능. 속도가 높을수록 조향각을 줄이는 `steerHelper` 보정 적용.
- **시동 시스템**: E키로 토글, 시동이 꺼져 있으면 스로틀 입력 무시.
- **충돌 처리**: `Enemy` 태그 충돌 시 카메라 셰이크 + UI 셰이크 + 체력 감소. `ObstacleController`가 붙은 장애물은 충돌 속도에 비례한 힘으로 위로 튕겨 나감.

### 무한 도로 생성 (`RoadManager.cs`, `RoadSegment.cs`)
- 도로 프리팹 리스트에서 무작위 조각을 뽑아 `NextSpawnPoint` 앵커에 이어 붙이는 방식.
- 플레이어가 도로 조각의 트리거에 진입하면 다음 조각을 생성하고, 활성 도로 수가 상한을 넘으면 가장 오래된 조각을 파괴(성능 관리).

### 적 / 귀신 (`Assets/_Project/02.Scripts/Gameplay/Enemy/`)
- **AttachedGhostController**: 차량의 자식 오브젝트로 스폰되어 로컬 좌표 기준으로 차량에 접근하고, 도착 후 주기적으로 차량 체력을 깎습니다. 피격 시 렌더러/라이트 점멸, 히트 파티클, 사망 파티클 처리.
- **GhostSpawner**: 시동이 켜져 있을 때만 15~30초 랜덤 간격으로 뒤쪽/옆쪽(좌·우 앵커) 귀신을 스폰. 각 타입은 동시에 1마리로 제한.
- **EnemyController**: Rigidbody 기반으로 `Player` 태그 대상을 추적하는 일반 적. 체력, 피격 점멸, 사망 이펙트 보유.

### 플레이어 (`Assets/_Project/02.Scripts/Gameplay/Player/`)
- **PlayerCameraController**: 마우스 룩(상하 클램프, 선택적 좌우 각도 제한), 커서 잠금.
- **PlayerAttacker**: 좌클릭으로 앙크를 들고 충전(기본 1초), 충전 완료 후 `SphereCastAll`로 전방 적을 감지해 초당 데미지를 적용. `EnemyController`와 `AttachedGhostController` 양쪽 모두 타격 처리.
- **PlayerInteractor**: 카메라 전방 레이캐스트로 `BeverageBox` 감지 → E키로 음료 섭취(체력 회복 + 마시기 애니메이션), 대상이 없으면 차량 시동 토글.

### UI / 연출
- **CarUIController**: 속도계 / RPM / 연료 게이지 바늘을 Z축 회전으로 구동하는 아날로그 계기판.
- **TextHealthBar**: TextMesh Pro 문자열("I" 반복)로 체력을 표현하는 텍스트형 체력 바 (피해/회복 API 제공).
- **AnkhAnimation**: 앙크 UI의 등장/퇴장 슬라이드, 충전 진행도에 따른 HDR 머티리얼 인텐시티 보간, 명중 시 떨림 연출.
- **DrinkAnimation**, **UIElementShaker**, **Billboard**, **SpriteFlipper**: 음료 마시기 연출, 충격 시 UI 흔들림, 빌보드 스프라이트, 스프라이트 프레임 플립.
- **CarCameraEffects**: 엔진 가동/시동 시 진동, 조향 강도 반영 진동, 충돌 시 임팩트 셰이크(Perlin 노이즈 기반). **CarCameraFollow**: 차량 추종 카메라.

### 사운드 (`Assets/_Project/02.Scripts/Systems/Sound/`)
- `CarSoundController`: 엔진 시동/루프/정지 클립, RPM에 따른 루프 피치 보간, 충격 강도 기반 충돌 사운드.
- `EnemySoundController`, `AttachedGhostSoundController`, `PlayerSoundController`, `EnvironmentSoundController`, `AudioUtility`.

### 커스텀 렌더링 (`Assets/_Project/04.Art/03.Shaders/`)
- **PixelizeFeature / PixelizePass**: URP `ScriptableRendererFeature`로 화면을 저해상도로 다운샘플해 픽셀 아트 룩을 만듭니다 (URP-HighFidelity-Renderer에 등록됨).
- **PaletteFeature / PalettePass**: 휘도 양자화 / 팔레트 텍스처 매핑 / 디더링을 지원하는 색상 감축 포스트 이펙트.
- Shader Graph 셰이더: 스프라이트 라이트/디졸브/노이즈/애니메이션 서브그래프.

## 프로젝트 구조

```
CarDrive/
├─ Assets/
│  ├─ _Project/                        # 자체 제작물 (외부 에셋과 분리)
│  │  ├─ 01.Scenes/                    # SampleScene.unity (메인 플레이 씬)
│  │  ├─ 02.Scripts/                   # 게임 로직 (C#)
│  │  │  ├─ Common/                    # Billboard, ImageFlipper(SpriteFlipper), AudioUtility
│  │  │  ├─ Gameplay/
│  │  │  │  ├─ Vehicle/                # CarController, Powertrain, CarInput, CarVisuals,
│  │  │  │  │                          # CarData(SO), CarCollisionHandler, CarUIController,
│  │  │  │  │                          # CarCameraEffects, CarCameraFollow
│  │  │  │  ├─ Player/                 # PlayerCameraController, PlayerAttacker, PlayerInteractor
│  │  │  │  ├─ Enemy/                  # EnemyController, AttachedGhostController, GhostSpawner
│  │  │  │  ├─ Road/                   # RoadManager, RoadSegment, ObstacleController
│  │  │  │  └─ Item/                   # BeverageBox, Beverage
│  │  │  ├─ Systems/Sound/             # 차량/적/플레이어/환경 사운드 컨트롤러
│  │  │  └─ UI/                        # TextHealthBar, AnkhAnimation, DrinkAnimation, UIElementShaker
│  │  ├─ 03.DataAssets/                # Car Data(SO), New Terrain(TerrainData)
│  │  ├─ 04.Art/
│  │  │  ├─ 01.Images/                 # 스프라이트 + RenderTextures(미러) 및 전용 머티리얼
│  │  │  └─ 03.Shaders/                # Shader Graph, 머티리얼,
│  │  │                                # 커스텀 URP 렌더러 피처(Pixelize, Palette)
│  │  ├─ 05.Prefabs/                   # Player, Monster, Prop/Tree, Effects, Items, Map, UI
│  │  ├─ 07.Settings/                  # URP 에셋 (Performant / Balanced / HighFidelity) 및 렌더러
│  │  ├─ 09.Docs/                      # TODO.md 등 문서
│  │  └─ 06.Sound, 08.Behavior, 10.Tests  # 예약된 빈 슬롯 (.gitkeep)
│  ├─ Imports/                         # 외부 에셋 (LowPolyRetroCars, Cartoon FX Remaster, tree_pack, Bottles 등)
│  ├─ TerrainSampleAssets/             # 지형 샘플 에셋
│  └─ TextMesh Pro/, TutorialInfo/     # 패키지 기본 제공 에셋
├─ ProjectSettings/            # Unity 프로젝트 설정 (ProductName: CarDrive)
├─ Packages/manifest.json      # 패키지 의존성 (URP 14.0.12 등)
├─ BUILD/                      # Windows 빌드 산출물 (CarDrive.exe)
└─ CarDrive.sln, *.csproj      # Unity가 생성한 IDE 솔루션/프로젝트 파일
```

## 실행 방법

### 에디터에서 실행
1. Unity Hub에서 **Unity 2022.3.62f2** 를 설치합니다.
2. `CarDrive` 폴더를 프로젝트로 열고 `Assets/_Project/01.Scenes/SampleScene.unity` 씬을 엽니다.
3. Play 버튼을 눌러 실행합니다.

### 빌드 실행
- 저장소에 포함된 `BUILD/CarDrive.exe` (Windows 64bit)를 바로 실행할 수 있습니다.
- 새로 빌드하려면 `File > Build Settings`에서 Windows 플랫폼으로 SampleScene을 포함해 빌드합니다.

### 조작 (스크립트 기준)
| 입력 | 동작 |
|---|---|
| `W`/`S` (Vertical 축) | 가속 / 후진 |
| `A`/`D` (Horizontal 축) | 조향 |
| `Space` | 브레이크 |
| 마우스 이동 | 시점 회전 |
| `E` | 시동 토글 / 음료 상자 상호작용(체력 회복) |
| 마우스 좌클릭(홀드) | 앙크 들기 → 충전 후 전방 적에게 지속 데미지 |

## 개발 현황

- Git 히스토리는 초기 커밋 1건(`Initial commit`)으로, 프로젝트 전체가 한 번에 커밋된 상태입니다.
- 씬은 `SampleScene` 단일 씬이며, 게임 오버/승리 조건, 점수, 메뉴 등 메타 게임 루프 스크립트는 아직 존재하지 않습니다 (체력이 0이 되었을 때의 처리도 `TextHealthBar`에서 표시만 갱신).
- 차량·적·도로·전투·사운드·포스트 프로세싱 등 핵심 시스템은 구현되어 동작하며, 스크립트는 책임 단위로 분리 리팩터링된 상태입니다 (주석에 `[리팩터링됨]`, `[신규]` 표기 다수).
- Windows 빌드 산출물이 존재하므로 플레이 가능한 프로토타입 단계로 볼 수 있습니다.
