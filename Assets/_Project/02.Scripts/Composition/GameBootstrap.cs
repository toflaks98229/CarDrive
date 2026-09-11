using UnityEngine;
using VContainer;
using VContainer.Unity;
using CarDrive.Common;
using CarDrive.Gameplay;
using CarDrive.Systems;

namespace CarDrive.Composition
{
    /// <summary>
    /// 컨테이너가 조립된 <b>직후에 한 번</b> 돌면서 시스템끼리를 이어 줍니다.
    ///
    /// <b>왜 이런 것이 필요한가.</b> 이 프로젝트는 협력자를 <c>Start</c>에서 찾아 왔습니다.
    /// "등록은 Awake, 조회는 Start"라는 규칙이 있었고 실제로 잘 지켜졌지만, 규칙이
    /// <b>지켜지는지 확인할 방법이 없었습니다.</b> 어느 컴포넌트가 언제 무엇을 잡는지
    /// 알려면 열세 개 파일의 <c>Start</c>를 각각 읽어야 했습니다.
    ///
    /// 이제 <b>이어 붙이는 일이 이 파일 한 곳에 모여</b> 순서대로 적혀 있습니다.
    /// 새 연결이 생기면 여기 한 줄이 늘고, 그 줄의 위치가 곧 실행 순서입니다.
    ///
    /// <b>없는 것은 건너뜁니다.</b> 씬을 아직 다 옮기지 않았어도 게임이 돌아야 하므로,
    /// 해석에 실패한 연결은 경고만 남기고 지나갑니다. 오류로 멈추는 것은
    /// 설치자(<see cref="SimulationScope"/> 등)의 <c>DIValidation</c>이 맡습니다.
    /// </summary>
    public class GameBootstrap : IPostInitializable, IStartable
    {
        // --- Private Member Variables ---

        /// <summary>등록된 것을 꺼내 볼 컨테이너입니다.</summary>
        private readonly IObjectResolver _resolver;

        // --- Constructors ---

        /// <summary>
        /// 컨테이너를 주입받습니다.
        ///
        /// <b>개별 타입을 생성자로 받지 않는 이유가 있습니다.</b> 그렇게 하면 그중 하나라도
        /// 등록되지 않았을 때 컨테이너 조립 전체가 실패하고, 이 마이그레이션 도중에는
        /// 그런 상태가 정상입니다. 여기서는 <b>있는 것만 이어 붙입니다.</b>
        /// </summary>
        /// <param name="resolver">등록된 것을 꺼내 볼 컨테이너</param>
        [UnityEngine.Scripting.Preserve]
        public GameBootstrap(IObjectResolver resolver)
        {
            _resolver = resolver;
        }

        // --- Public Methods : 생명주기 ---

        /// <summary>
        /// 컨테이너 조립 직후에 시스템 사이의 참조를 연결합니다.
        /// </summary>
        public void PostInitialize()
        {
            GameLog.Info(GameLog.Channel.Core, "[GameBootstrap] 시스템을 연결합니다...");

            // 순서가 중요합니다. 풀 갈고리를 가장 먼저 겁니다 — 이 뒤의 어떤 단계가
            // 오브젝트를 스폰하더라도 그것이 이미 의존성을 갖춘 채로 나오게 하기 위해서입니다.
            WirePoolInjector();
            InjectSceneObjects();

            CaptureViewBase();
            WireNeedsDamageTarget();
            WireSpeedSource();
            EnsureSaveParticipants();

            GameLog.Info(GameLog.Channel.Core, "[GameBootstrap] 연결을 마쳤습니다.");
        }

        /// <summary>
        /// 첫 프레임에 한 번 돌면서 기동이 끝났음을 남깁니다.
        /// </summary>
        public void Start()
        {
            GameLog.Info(GameLog.Channel.Core, "[GameBootstrap] 게임을 시작합니다.");
        }

        // --- Private Methods : 연결 단계 ---

        /// <summary>
        /// 풀에서 나오는 오브젝트에 의존성을 넣어 줄 갈고리를 겁니다.
        ///
        /// <b>왜 이것이 필요했는가.</b> 귀신과 재화 덩어리는 씬에 미리 놓여 있지 않습니다.
        /// 인스펙터로 배선할 수 없으니 그것들은 <c>NeedsSystem.Report()</c>·<c>Wallet.Report()</c>
        /// 같은 정적 메서드로 협력자를 스스로 찾았습니다. <b>풀에서 나오는 오브젝트가 있는 한
        /// 정적 접근을 없앨 수 없었던 진짜 이유가 이것입니다.</b>
        ///
        /// 갈고리 한 줄이 그 이유를 없앱니다. 풀은 인스턴스를 <b>처음 만들 때 한 번</b>
        /// 이것을 부르고, 그 뒤의 재사용에서는 부르지 않습니다 — 파괴되지 않으므로
        /// 한 번 채운 필드가 그대로 남아 있기 때문입니다.
        /// </summary>
        private void WirePoolInjector()
        {
            PrefabPool.Injector = InjectPooledInstance;
        }

        /// <summary>
        /// 풀이 갓 만든 인스턴스에 의존성을 넣습니다.
        /// </summary>
        /// <param name="instance">풀이 방금 만든 인스턴스</param>
        private void InjectPooledInstance(GameObject instance)
        {
            if (instance == null) return;
            _resolver.InjectGameObject(instance);
        }

        /// <summary>
        /// 씬에 이미 놓여 있는 컴포넌트들에 의존성을 넣습니다.
        /// </summary>
        /// <remarks>
        /// <b>왜 한 번에 훑는가.</b> 주입이 필요한 타입을 여기 나열하면 새 소비자가
        /// 생길 때마다 이 파일을 고쳐야 하고, 빠뜨리면 <b>조용히 Null 객체로 도는</b>
        /// 가장 찾기 어려운 고장이 됩니다. 타입을 세지 않고 전부 훑으면 그 실수가 없습니다.
        ///
        /// <b>비용.</b> 기동 중 딱 한 번, 네이티브 탐색 한 번입니다.
        /// <c>[Inject]</c>가 없는 타입에 대해서는 VContainer가 빈 주입기를 캐시해 두므로
        /// 두 번째부터는 사실상 공짜입니다. 지형 타일의 나무·바위는 MonoBehaviour가 아니라
        /// 이 탐색에 잡히지 않습니다.
        ///
        /// <b>꺼져 있는 것도 넣습니다.</b> 도보 리그는 차에 타고 시작하면 꺼진 채로
        /// 있는데, 내리는 순간 이미 준비되어 있어야 합니다.
        /// </remarks>
        private void InjectSceneObjects()
        {
            // 정렬 없는 과부하를 씁니다. 넣는 순서는 결과에 영향을 주지 않습니다.
            MonoBehaviour[] components = Object.FindObjectsByType<MonoBehaviour>(
                FindObjectsInactive.Include);

            int injected = 0;
            int failed = 0;

            for (int i = 0; i < components.Length; i++)
            {
                MonoBehaviour component = components[i];
                if (component == null) continue;

                // 한 건이 실패해도 나머지는 넣어야 합니다.
                //
                // 대개 원인은 등록되지 않은 타입을 매개변수로 요구한 Construct 입니다.
                // 그대로 두면 예외가 이 반복을 끊어, <b>그 뒤 컴포넌트 전부가 조용히
                // Null 객체로 도는</b> 가장 찾기 어려운 고장이 됩니다.
                // 잡되 삼키지는 않습니다 — 무엇이 실패했는지 이름을 남깁니다.
                try
                {
                    _resolver.Inject(component);
                    injected++;
                }
                catch (System.Exception e)
                {
                    failed++;
                    GameLog.Error(GameLog.Channel.Core,
                        "[GameBootstrap] " + component.GetType().Name + " 에 의존성을 넣지 못했습니다. " +
                        "Construct 가 요구하는 타입이 등록되지 않았을 수 있습니다. " + e.Message, component);
                }
            }

            GameLog.InfoFormat(GameLog.Channel.Core,
                "[GameBootstrap] 씬 컴포넌트 {0}개에 의존성을 넣었습니다. (실패 {1}건)", injected, failed);
        }

        /// <summary>
        /// <b>씬이 적어 둔 시야 거리를 날씨가 건드리기 전에 못박습니다.</b>
        ///
        /// <see cref="ViewDistances.SetViewBase"/>는 <b>첫 값만 받고 이후를 무시합니다.</b>
        /// 배율이 곱해진 값을 다시 기준으로 삼으면 거리가 계속 깎여 나가기 때문입니다.
        ///
        /// 문제는 그 첫 값을 누가 주느냐였습니다. 예전에는 <see cref="ViewRangeScaler"/>가
        /// 자기 <c>LateUpdate</c>에서 <c>camera.farClipPlane</c>을 읽어 넘겼는데, 같은 프레임의
        /// <b>더 이른 LateUpdate</b>에서 <see cref="WeatherRig"/>가 이미 그 값을 날씨 배율만큼
        /// 줄여 둘 수 있었습니다. (실행 순서가 각각 200과 0입니다) 그러면 사다리 전체의 기준이
        /// <b>흐린 날의 값으로 영구히 굳고</b>, 날이 개어도 돌아오지 않습니다.
        ///
        /// 여기서 먼저 못박으면 그 경합 자체가 사라집니다. <c>PostInitialize</c>는
        /// 어떤 <c>LateUpdate</c>보다도 앞이라, 여기서 읽는 값은 <b>씬에 적힌 그대로</b>입니다.
        /// </summary>
        private void CaptureViewBase()
        {
            Camera camera = GameContext.MainCamera;
            if (camera == null)
            {
                GameLog.Warn(GameLog.Channel.World,
                    "[GameBootstrap] 메인 카메라를 찾지 못해 기준 시야 거리를 잡지 못했습니다. " +
                    "ViewRangeScaler 가 첫 LateUpdate 에서 대신 잡습니다.");
                return;
            }

            ViewDistances.SetViewBase(camera.farClipPlane);

            GameLog.InfoFormat(GameLog.Channel.World,
                "[GameBootstrap] 기준 시야 거리를 {0}m 로 고정했습니다.", camera.farClipPlane);
        }

        /// <summary>
        /// 니즈가 한계를 넘었을 때 깎을 대상을 연결합니다.
        ///
        /// <b>타입 안전성은 <see cref="PlayerScope"/>가 지킵니다.</b> 그쪽 필드가
        /// <see cref="PlayerHealth"/>로 좁혀져 있어, 차량 내구도가 여기까지 올 수 없습니다.
        /// </summary>
        private void WireNeedsDamageTarget()
        {
            NeedsSystem needs;
            if (!_resolver.TryResolve(out needs) || needs == null) return;

            PlayerHealth health;
            if (!_resolver.TryResolve(out health) || health == null)
            {
                GameLog.Warn(GameLog.Channel.Simulation,
                    "[GameBootstrap] PlayerHealth 가 없어 니즈 한계 초과 시 체력이 줄지 않습니다. " +
                    "게이지는 빨개지고 이벤트도 발생하지만 죽지는 않습니다.");
                return;
            }

            needs.SetDamageTarget(health);
        }

        /// <summary>
        /// 풀 거리 LOD가 볼 속도원을 연결합니다.
        ///
        /// 연결하지 않으면 속도를 0으로 보므로 <b>이 최적화만 쉬고</b> 게임은 그대로 돕니다.
        /// </summary>
        private void WireSpeedSource()
        {
            ISpeedSource speedSource;
            if (!_resolver.TryResolve(out speedSource) || speedSource == null)
            {
                GameLog.Warn(GameLog.Channel.World,
                    "[GameBootstrap] 속도원이 없어 속도 적응형 풀 LOD 가 동작하지 않습니다.");
                return;
            }

            TerrainDetailLod.SpeedSource = speedSource;
        }

        /// <summary>
        /// 플레이어·차량 세이브 참여자가 씬에 있는지 확인하고, 없으면 만듭니다.
        ///
        /// <b>왜 만들어 주는가.</b> 이 둘은 원래 <see cref="SaveSystem"/> 안에 박혀 있다가
        /// 이번에 참여자로 떨어져 나왔습니다. 씬에는 아직 그 컴포넌트가 없으므로,
        /// 여기서 확보하지 않으면 <b>세이브에서 플레이어와 차량이 조용히 빠집니다.</b>
        /// 저장은 성공하는데 위치만 복원되지 않는, 알아채기 가장 어려운 종류의 고장입니다.
        ///
        /// 씬에 직접 배치하면 그것을 쓰고 여기서는 아무것도 하지 않습니다.
        /// </summary>
        private void EnsureSaveParticipants()
        {
            bool hasVehicle = Object.FindAnyObjectByType<VehicleSaveParticipant>(FindObjectsInactive.Include) != null;
            bool hasPlayer = Object.FindAnyObjectByType<PlayerSaveParticipant>(FindObjectsInactive.Include) != null;
            bool hasItems = Object.FindAnyObjectByType<ItemSaveParticipant>(FindObjectsInactive.Include) != null;
            bool hasLamps = Object.FindAnyObjectByType<LampSaveParticipant>(FindObjectsInactive.Include) != null;

            // 물건 수명은 세이브 참여자가 아니지만 같은 사정입니다 —
            // 없으면 물건이 영원히 쌓이고, 그것을 알아챌 방법이 없습니다.
            bool hasDecay = Object.FindAnyObjectByType<ItemDecay>(FindObjectsInactive.Include) != null;

            if (hasVehicle && hasPlayer && hasItems && hasLamps && hasDecay) return;

            GameObject go = new GameObject("[Save Participants]");
            Object.DontDestroyOnLoad(go);

            if (!hasVehicle) go.AddComponent<VehicleSaveParticipant>();
            if (!hasPlayer) go.AddComponent<PlayerSaveParticipant>();
            if (!hasItems) go.AddComponent<ItemSaveParticipant>();
            if (!hasLamps) go.AddComponent<LampSaveParticipant>();
            if (!hasDecay) go.AddComponent<ItemDecay>();

            // 방금 만들었으므로 위의 씬 훑기에 잡히지 않았습니다. 여기서 따로 넣습니다.
            _resolver.InjectGameObject(go);

            GameLog.Info(GameLog.Channel.Core,
                "[GameBootstrap] 세이브 참여자(플레이어·차량·물건·등)와 물건 수명을 만들었습니다. " +
                "씬에 직접 배치해 두면 이 오브젝트는 생기지 않습니다.");
        }
    }
}
