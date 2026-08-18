# CarDrive

밤길을 달리는 1인칭 차량 운전 + 귀신 퇴치 서바이벌 게임 (Unity 6 / URP).

## 개요

CarDrive는 플레이어가 1인칭 시점으로 차를 몰고 밤길을 달리는 Unity 게임입니다. 문을 조준해 차에 타고, 운전대를 조준해 시동을 걸고, 기어·RPM·연료를 관리하며 달립니다. 주행 중에는 차량의 뒤쪽과 옆쪽에 귀신이 달라붙어 지속적으로 내구도를 깎고, 플레이어는 앙크(Ankh)를 들어 충전한 뒤 조준해 퇴치합니다.

차에서 내려 걸어 다닐 수도 있습니다. 도보 상태에서는 허기·갈증·피로·스트레스·배뇨·청결 6종의 **니즈**가 시간에 따라 차오르고, 하늘이 뚫린 곳에 서 있으면 그날의 **날씨**가 몸에 영향을 줍니다. 비를 맞으면 몸이 씻기고, 하늘을 보고 있으면 빗물을 받아 마실 수 있습니다.

세계는 무한히 이어지지 않습니다. 마을을 중심으로 길이 뻗어 나가고 그 끝에 현장이 있는 **고정 배치**이며, 배치는 시드로 고정되어 실행할 때마다 같은 세계가 나옵니다. 화면은 URP 커스텀 렌더러 피처(픽셀화 + 색상 팔레트 양자화)를 통해 레트로 픽셀 룩으로 출력됩니다.

## 기술 스택

- **엔진**: Unity 6000.5.3f1 (Unity 6)
- **언어**: C# (Assembly-CSharp, 단일 어셈블리 · asmdef 미사용)
- **렌더 파이프라인**: Universal RP 17.5.0, Shader Graph 기반 스프라이트 셰이더
- **주요 패키지**: uGUI 2.5.0 (TextMeshPro 포함), Splines 2.9.0, Timeline 1.8.12, Visual Scripting 1.9.11, Test Framework 1.7.0
- **물리**: Unity 내장 Rigidbody + WheelCollider
- **오브젝트 풀**: `UnityEngine.Pool.ObjectPool<T>` (내장)
- **외부 에셋**: LowPolyRetroCars, Cartoon FX Remaster (JMO Assets), tree_pack, Crate/Barrels, Bottles
- **빌드 타깃**: Windows 스탠드얼론 (`BUILD/CarDrive.exe` 빌드 산출물 포함)

## 주요 기능 / 시스템

스크립트는 `Assets/_Project/02.Scripts/` 아래에 77개, 약 13,100줄입니다.

### 세계의 시계 — 시간 · 날씨 (`Systems/Time/`, `Systems/Weather/`)
- **TimeSystem**: 게임 시계·시간대(새벽/아침/낮/저녁/밤)·햇빛의 **단일 소스**. 니즈와 날씨가 각자 시간을 세지 않고 여기서 배율을 읽어 가므로, 수면으로 시간을 건너뛰면 모든 시스템이 함께 움직입니다.
- **WeatherSystem**: 날씨 7종(맑음·흐림·잔뜩 흐림·비·폭우·안개·이슬비)의 상태와 전환을 소유합니다. 같은 "비"라도 그날 뽑힌 강도가 다르고, 맑음에서 폭우로 직행하지 않고 중간 날씨를 거칩니다. 하늘이 먼저 흐려진 뒤에 비가 굵어집니다.
- **WeatherRig**: 계산된 수치를 비 파티클·안개·앰비언트·헤드라이트 사거리로 **표현**합니다. (계산과 표현이 분리되어 있습니다)
- 날씨는 노면 미끄러움·연료 소모·시야·귀신 활동량·수면 회복률에 영향을 줍니다.

### 니즈 (`Systems/Needs/`)
- **NeedsSystem**: 허기·갈증·피로·스트레스·배뇨·청결 6종을 관리합니다. 모두 0에서 1로 차오르며, 1을 넘어도 즉시 죽지 않고 한계(기본 1.5)까지 유예가 있습니다.
- **연쇄 규칙**: 스트레스가 높으면 갈증과 피로가 빨라지고, 다른 니즈가 한계에 가까우면 스트레스가 오릅니다.
- **처벌**: 한계 초과 시 체력이 지속적으로 깎이거나(치명), 기절해 시간이 건너뛰어집니다(피로).
- **NeedSatisfier**: 음식·물·침대·화장실·샤워·라디오를 컴포넌트 하나로 표현합니다. 인스펙터 우클릭 프리셋 제공.
- **NeedsProfile** (ScriptableObject)로 수치를 에셋으로 관리합니다.

### 월드 (`Gameplay/World/`)
- **WorldStreamer**: 마을을 중심으로 길이 뻗어 나가는 hub-and-spoke 배치를 시드로 고정해 **한 번만** 깔고, 멀리 있는 타일만 비활성화합니다. 무엇도 파괴하지 않으므로 **돌아갈 마을이 존재합니다**.
- **WorldLocation**: 이름을 가진 장소(마을/현장/지형지물)와 진입·이탈 판정. 앞으로의 의뢰·상점·대화의 전제입니다.

### 차량 (`Gameplay/Vehicle/`)
- **Vehicle**: 차량 부품(컨트롤러·입력·좌석·내구도·흔들림·계기판)의 **단일 진입점 + 전역 등록부**. 문이 자기 차량을 넘겨 주므로 차량이 여러 대여도 조준한 문의 차에 정확히 탑니다.
- **컴포넌트 분리**: `CarController`(조율자) + `CarInput`(입력) + `Powertrain`(동력계) + `CarVisuals`(휠 메시) + `CarCollisionHandler`(충돌).
- **CarData** (ScriptableObject): 토크 곡선, 기어비, RPM 임계, 연료, 조향각·조향 보조.
- **Powertrain**: 휠 RPM 기반 엔진 RPM, 자동 변속, RPM·스로틀 비례 연료 소모. 연료가 0이면 시동이 꺼집니다.
- **날씨 연동**: 미끄러운 노면에서 타이어 접지력이 떨어지고, 맞바람·젖은 노면에서 연료를 더 먹습니다.
- **시동**: 운전대(`SteeringWheelInteractable`)를 조준해야 걸 수 있고, **그 차에 타고 있어야만** 가능합니다.

### 플레이어 (`Gameplay/Player/`)
- **PlayerModeController + 상태 기계** (`Player/States/`): 주행(`DrivingState`)과 도보(`OnFootState`)를 `IPlayerState` 구현으로 분리했습니다. 상태 진입·이탈 동작을 상태 자신이 소유하므로, 새 상태를 추가할 때 기존 코드를 건드리지 않습니다.
- **PlayerFootMotor**: CharacterController 기반 도보 이동·달리기·웅크리기. 달리면 피로와 더러움이 누적됩니다.
- **PlayerAttacker**: 좌클릭으로 앙크를 들고 충전한 뒤, `SphereCast`로 잡힌 **`IDamageable`이면 무엇이든** 초당 피해를 줍니다.
- **PlayerInteractor**: 카메라 정면 레이캐스트로 조준한 대상을 찾아 `E`로 상호작용합니다. (문·운전대·니즈 해소 대상·음료 상자)
- **PlayerCarrier**: 좌클릭으로 물건을 듭니다. 위치가 아니라 **속도**로 손을 따라오므로 벽을 뚫지 않고 문틀에 걸립니다. 집어 든 순간의 각도를 그대로 유지합니다.
- **WeatherExposure / RainDrinking / UrineRelief**: 하늘 아래에서 날씨가 몸에 주는 영향, 빗물 받아 마시기, 배뇨(압력·조준·역류 시뮬레이션).

### 전투 규약 (`Gameplay/Combat/`)
- **IDamageable / IHostile / IInteractable**: 태그 문자열 분기를 인터페이스로 대체했습니다. 새 적은 인터페이스 두 개만 구현하면 앙크 공격·차량 충돌·도보 피격에 **자동으로 통합니다.**
- **Health → PlayerHealth / VehicleHealth**: 타입을 갈라 인스펙터에서 플레이어 체력 자리에 차량 내구도를 끼우는 일이 **컴파일 단계에서 불가능**합니다.

### 적 / 귀신 (`Gameplay/Enemy/`)
- **AttachedGhostController**: 차량의 자식으로 스폰되어 로컬 좌표로 접근한 뒤 주기적으로 내구도·스트레스·더러움을 가합니다.
- **GhostSpawner**: 시동이 켜져 있을 때만 스폰하며, 날씨와 시간대에 따라 간격이 좁아집니다. (폭우 ×1.9 × 밤 ×1.5)
- **EnemyController**: Rigidbody로 차량을 추적하는 일반 적.

### 세이브 (`Systems/Save/`)
- 시간·날씨·니즈·플레이어·차량 상태를 JSON으로 저장합니다 (`F5` / `F9`).
- 각 시스템이 `CaptureState`/`RestoreState`로 자기 상태를 내주므로 `SaveSystem`은 조립만 합니다.
- **지형은 저장하지 않습니다.** `WorldStreamer`가 시드로 배치를 고정하므로 같은 세계가 다시 깔립니다.

### 성능 (`Common/PrefabPool.cs`, `Systems/Sound/OneShotAudioPool.cs`)
- 귀신과 위치 기반 일회성 사운드를 오브젝트 풀로 재사용합니다. 전투 중 가장 잦은 할당(앙크 피격음 0.25초, 귀신 공격음 1초 간격)을 없앴습니다.

### UI / 연출 (`UI/`)
- **CarUIController**(아날로그 계기판), **NeedsUI**(니즈 게이지), **InteractionPromptUI**(조준 대상 안내), **TextHealthBar** / **HealthBarImage**(체력 표시), **AnkhAnimation**, **DrinkAnimation**, **UIElementShaker**.
- UI는 상태를 소유하지 않고 **읽기만** 합니다.

### 커스텀 렌더링 (`Assets/_Project/04.Art/03.Shaders/`)
- **PixelizeFeature / PixelizePass**: 화면을 저해상도로 다운샘플해 픽셀 아트 룩을 만듭니다.
- **PaletteFeature / PalettePass**: 휘도 양자화 / 팔레트 텍스처 매핑 / 디더링 기반 색상 감축.

## 프로젝트 구조

```
CarDrive/
├─ Assets/
│  ├─ _Project/                        # 자체 제작물 (외부 에셋과 분리)
│  │  ├─ 01.Scenes/                    # SampleScene.unity (메인 플레이 씬)
│  │  ├─ 02.Scripts/                   # 게임 로직 (C#, 77개)
│  │  │  ├─ Common/                    # GameInputGate, SkyCover, PlayerAim,
│  │  │  │                             # PrefabPool, PooledObject, AudioUtility,
│  │  │  │                             # Billboard, SpriteFlipper
│  │  │  ├─ Gameplay/
│  │  │  │  ├─ Combat/                 # IDamageable, IHostile, Health,
│  │  │  │  │                          # PlayerHealth, VehicleHealth, HitFlicker
│  │  │  │  ├─ Vehicle/                # Vehicle, CarController, CarInput, Powertrain,
│  │  │  │  │                          # CarVisuals, CarData(SO), CarCollisionHandler,
│  │  │  │  │                          # CarUIController, CarCameraEffects,
│  │  │  │  │                          # CarCameraFollow, CarImpactShake, VehicleSeat
│  │  │  │  ├─ Player/                 # PlayerModeController, PlayerFootMotor,
│  │  │  │  │  └─ States/              # IPlayerState, DrivingState, OnFootState
│  │  │  │  ├─ Interaction/            # IInteractable, Carryable,
│  │  │  │  │                          # VehicleDoorInteractable, SteeringWheelInteractable
│  │  │  │  ├─ Enemy/                  # EnemyController, AttachedGhostController, GhostSpawner
│  │  │  │  ├─ World/                  # WorldStreamer, WorldLocation
│  │  │  │  ├─ Road/                   # ObstacleController
│  │  │  │  └─ Item/                   # BeverageBox, Beverage
│  │  │  ├─ Systems/
│  │  │  │  ├─ Time/                   # TimeSystem, TimeDebugOverlay
│  │  │  │  ├─ Weather/                # WeatherSystem, WeatherRig, WeatherDefinitions
│  │  │  │  ├─ Needs/                  # NeedsSystem, NeedSatisfier, NeedsProfile(SO)
│  │  │  │  ├─ Save/                   # SaveSystem, SaveData
│  │  │  │  └─ Sound/                  # 차량/적/플레이어/환경 사운드 + OneShotAudioPool
│  │  │  └─ UI/                        # NeedsUI, InteractionPromptUI, TextHealthBar,
│  │  │                                # HealthBarImage, AnkhAnimation, DrinkAnimation,
│  │  │                                # UIElementShaker
│  │  ├─ 03.DataAssets/                # Vehicles(CarData), Terrain
│  │  ├─ 04.Art/                       # 01.Images, 02.Models, 03.Shaders, 04.Animations
│  │  │                                # (Pixelize / Palette 렌더러 피처 포함)
│  │  ├─ 05.Prefabs/                   # Player, Monster, Prop, Effects, Items, Map, UI
│  │  ├─ 07.Settings/                  # URP 에셋 및 렌더러
│  │  ├─ 09.Docs/                      # TODO.md
│  │  └─ 06.Sound, 08.Behavior, 10.Tests  # 예약된 빈 슬롯
│  ├─ Imports/                         # 외부 에셋 (LowPolyRetroCars, Cartoon FX Remaster 등)
│  └─ TerrainSampleAssets/             # 지형 샘플 에셋
├─ ProjectSettings/            # Unity 프로젝트 설정 (ProductName: CarDrive)
├─ Packages/manifest.json      # 패키지 의존성 (URP 17.5.0 등)
├─ BUILD/                      # Windows 빌드 산출물 (CarDrive.exe)
└─ CarDrive.sln, *.csproj      # Unity가 생성한 IDE 솔루션/프로젝트 파일
```

## 실행 방법

### 에디터에서 실행
1. Unity Hub에서 **Unity 6000.5.3f1** 을 설치합니다.
2. `CarDrive` 폴더를 프로젝트로 열고 `Assets/_Project/01.Scenes/SampleScene.unity` 씬을 엽니다.
3. Play 버튼을 눌러 실행합니다.

### 빌드 실행
- 저장소에 포함된 `BUILD/CarDrive.exe` (Windows 64bit)를 바로 실행할 수 있습니다.
- 새로 빌드하려면 `File > Build Settings`에서 Windows 플랫폼으로 SampleScene을 포함해 빌드합니다.

### 조작 (스크립트 기준 기본값)

**공통**

| 입력 | 동작 |
|---|---|
| 마우스 이동 | 시점 회전 |
| `E` | 조준한 대상과 상호작용 (문 = 탑승/하차, 운전대 = 시동, 음료 상자 = 마시기, 침대·화장실 등 = 니즈 해소) |
| 마우스 좌클릭 | 조준점에 물건이 있으면 들기/내려놓기, 없으면 앙크 들기(홀드 → 충전 후 지속 피해) |
| `F5` / `F9` | 저장 / 불러오기 |

**주행 중**

| 입력 | 동작 |
|---|---|
| `W`/`S` (Vertical) | 가속 / 후진 |
| `A`/`D` (Horizontal) | 조향 |
| `Space` | 브레이크 |

**도보 중**

| 입력 | 동작 |
|---|---|
| `W`/`A`/`S`/`D` | 이동 |
| `Left Shift` | 달리기 (피로·더러움 누적) |
| `Space` | 점프 |
| `Left Ctrl` | 웅크리기 |
| `P` (홀드) | 배뇨 — 연타하면 압력이 붙고, 위를 보고 쏘면 역류합니다 |
| 하늘 보기 (비 올 때) | 빗물 받아 마시기 |

**디버그**

| 입력 | 동작 |
|---|---|
| `F1` | 니즈 오버레이 (`1`~`6` 니즈 증가, `0` 초기화) |
| `F2` | 시간·날씨 오버레이 (`,` `.` 배율, `/` 일시정지) |

## 개발 현황

### 구현되어 동작하는 것
- 차량 시뮬레이션, 탑승/하차, 도보 이동, 앙크 전투, 귀신 스폰
- 시간·날씨·니즈 시스템과 그 상호작용 (날씨 → 접지력·연료·귀신 활동량·니즈)
- 고정 월드 배치와 거리 기반 스트리밍, 장소 판정
- JSON 세이브/로드, 오브젝트 풀링
- 픽셀화 + 팔레트 양자화 포스트 프로세싱

### 아직 없는 것 / 알려진 한계
- **메타 게임 루프가 없습니다.** 마을과 현장이 구분되어 있지만 의뢰·보상·화폐가 없어, 어디로 왜 가는지에 대한 동기가 아직 없습니다.
- **허기와 피로는 해소할 방법이 전혀 없습니다.** `NeedSatisfier`가 식사·수면 프리셋까지 갖추고 있지만 이를 사용하는 오브젝트가 씬에 배치되어 있지 않습니다. (갈증은 음료·빗물, 배뇨는 `P`, 청결·스트레스는 날씨로 일부 해소됩니다)
- **연료를 보충할 수단이 없습니다.** 연료는 유일한 하드 실패 조건인데 주유소가 없습니다.
- 게임 오버/승리 조건, 메뉴, 다중 세이브 슬롯이 없습니다.
- 어셈블리 정의(asmdef)와 네임스페이스를 쓰지 않아 전체가 `Assembly-CSharp` 하나입니다.
- `10.Tests` 폴더는 비어 있습니다. `Powertrain`·`NeedsSystem`·`WeatherSystem`의 계산은 이미 프레임과 무관한 순수 로직으로 분리되어 있어 테스트 작성 비용이 낮은 상태입니다.
- 귀신의 시인성이 낮아 개선이 필요합니다. (`Assets/_Project/09.Docs/TODO.md`)
