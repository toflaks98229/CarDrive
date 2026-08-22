using System.Collections.Generic;
using UnityEngine;
using CarDrive.Common;

namespace CarDrive.Gameplay
{
    /// <summary>
    /// 차량 한 대가 소유한 부품들의 단일 진입점입니다.
    ///
    /// 예전에는 PlayerModeController가 CarController·CarInput·VehicleSeat를 각각 따로 들고 있었고,
    /// CarCameraEffects와 UIElementShaker는 <c>FindObjectOfType&lt;CarController&gt;()</c>로
    /// "씬에 있는 아무 차"를 잡았습니다. 그래서 차량이 두 대가 되는 순간 여섯 군데가 동시에 깨졌습니다.
    ///
    /// 이제는 "이 차량"을 이 컴포넌트 하나로 가리킵니다.
    /// 문을 조준해 타면 그 문이 자기 Vehicle을 넘겨주므로 <b>차량 교체가 저절로 됩니다.</b>
    ///
    /// 부품은 인스펙터로 연결하지 않아도 됩니다. Awake에서 자기 계층을 뒤져 찾습니다.
    /// 차량 프리팹 안의 부품과 씬에서 추가한 부품(VehicleSeat 등)이 섞여 있어도
    /// 실행 중에 찾으면 그 경계를 신경 쓸 필요가 없기 때문입니다.
    /// </summary>
    [RequireComponent(typeof(CarController))]
    public class Vehicle : MonoBehaviour
    {
        // --- Static Registry ---

        /// <summary>
        /// 활성화된 모든 차량의 등록부입니다.
        /// OnEnable/OnDisable에서 스스로 넣고 빼므로 씬을 오가도 유령 항목이 남지 않습니다.
        /// </summary>
        private static readonly List<Vehicle> all = new List<Vehicle>();

        /// <summary>씬에 존재하는 모든 차량입니다.</summary>
        public static IReadOnlyList<Vehicle> All { get { return all; } }

        /// <summary>
        /// 플레이어가 지금 타고 있는 차량입니다. 걸어 다니는 중이면 null입니다.
        ///
        /// <b>이것은 차량의 속성이 아니라 플레이어 상태의 사본입니다.</b> 진짜 주인은
        /// <c>PlayerModeController</c>이고, 그 상태 기계(<c>DrivingState</c>/<c>OnFootState</c>)만
        /// 이 값을 씁니다. 여기 두는 이유는 읽는 쪽이 많고(계기판·흔들개·속도원·적의 추적)
        /// 그들 대부분이 플레이어를 알 이유가 없기 때문입니다.
        ///
        /// 쓰기는 <see cref="SetCurrent"/> 하나로 모여 있고 어셈블리 밖으로 열려 있지 않습니다.
        /// <b>새로 쓰고 싶어졌다면 그것은 대개 신호입니다</b> — 견인이나 차량 상점처럼
        /// "지금 차"의 뜻이 갈라지는 기능이라면, 여기에 덧쓰지 말고 그 개념에 이름을 따로 주세요.
        /// </summary>
        public static Vehicle Current { get; private set; }

        // --- Public Member Variables ---

        /// <summary>안내 문구와 세이브 식별에 쓸 차량 이름입니다.</summary>
        [Header("표시")]
        [Tooltip("안내 문구에 쓸 이름")]
        public string displayName = "차량";

        /// <summary>주행·시동을 담당하는 컨트롤러입니다.</summary>
        [Header("부품 (비워두면 실행할 때 자기 계층에서 찾습니다)")]
        public CarController controller;

        /// <summary>이 차량의 조향·스로틀 입력원입니다.</summary>
        public CarInput input;

        /// <summary>승하차 지점을 정의하는 좌석입니다.</summary>
        public VehicleSeat seat;

        /// <summary>이 차량의 내구도입니다.</summary>
        public VehicleHealth health;

        /// <summary>충돌할 때 차체를 흔드는 컴포넌트입니다.</summary>
        public CarImpactShake impactShake;

        // --- Public Properties ---

        /// <summary>
        /// 이 차량이 부딪혔을 때 흔들릴 것들을 <b>한 목록으로</b> 모아 둔 것입니다.
        /// 차체(<see cref="impactShake"/>)와 계기판(<see cref="dashboardShakers"/>)이 함께 들어 있습니다.
        ///
        /// 예전에는 충돌 처리가 이 둘을 각각 다른 코드 경로로 불렀습니다.
        /// 흔들 대상이 하나 늘 때마다 그 분기도 하나씩 늘어나던 자리라, 목록 하나로 모았습니다.
        /// 새로 흔들 것이 생기면 <see cref="IImpactShakable"/>만 구현하면 됩니다.
        /// </summary>
        public IReadOnlyList<IImpactShakable> Shakables { get { return shakables; } }

        /// <summary>주행 중 카메라가 따라갈 기준 위치입니다.</summary>
        public Transform DriverAnchor
        {
            get { return seat != null ? seat.GetDriverAnchor() : transform; }
        }

        /// <summary>플레이어가 지금 이 차를 타고 있는지 여부입니다.</summary>
        public bool IsOccupied { get { return Current == this; } }

        // --- Private Member Variables ---

        /// <summary>
        /// <see cref="Shakables"/>가 돌려줄 목록입니다. <see cref="ResolveParts"/>에서 한 번 채웁니다.
        /// 인터페이스라 직렬화되지 않으므로 실행 중에 만듭니다.
        /// </summary>
        private readonly List<IImpactShakable> shakables = new List<IImpactShakable>();

        // --- Unity Event Functions ---

        /// <summary>
        /// 비어 있는 부품 참조를 자기 계층에서 찾아 채웁니다.
        /// </summary>
        void Awake()
        {
            ResolveParts();
        }

        /// <summary>
        /// 이 차량을 전역 등록부에 넣습니다.
        /// </summary>
        void OnEnable()
        {
            if (!all.Contains(this)) all.Add(this);
        }

        /// <summary>
        /// 이 차량을 전역 등록부에서 빼고, 탑승 중이던 차량이었다면 그 참조도 지웁니다.
        /// </summary>
        void OnDisable()
        {
            all.Remove(this);
            if (Current == this) Current = null;
        }

        // --- Private Methods : 정적 상태 초기화 ---

        /// <summary>
        /// 플레이 모드에 들어갈 때 등록부와 탑승 상태를 비웁니다.
        ///
        /// <b>왜 필요한가.</b> 이 둘은 정적이라 <c>Enter Play Mode Options</c>로
        /// 도메인 리로드를 꺼 두면 <b>지난 실행의 값이 그대로 남습니다.</b>
        /// 그러면 등록부에 파괴된 차량이 유령으로 남고, 걸어서 시작했는데도
        /// 지난 판에 몰던 차가 <see cref="Current"/>에 들어 있게 됩니다.
        ///
        /// 이 프로젝트의 다른 정적 보유자(<c>GameContext</c>·<c>SaveRegistry</c>·
        /// <c>PrefabPool</c>·<c>ViewDistances</c>·<c>WorldProfiler</c> 등)는 모두 이것을
        /// 갖추고 있었는데 여기만 빠져 있었습니다.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            all.Clear();
            Current = null;
        }

        // --- Public Methods ---

        /// <summary>
        /// 지금 조종 중인 차량을 지정합니다. 플레이어 상태 기계가 탑승·하차할 때 부릅니다.
        /// null을 넘기면 "아무 차도 타고 있지 않음"이 됩니다.
        ///
        /// <b>왜 internal 인가.</b> 예전에는 <c>public</c>이었습니다. 바로 위 <see cref="Current"/>를
        /// <c>private set</c>으로 막아 두고도 그 옆에 공개 설정자를 두어, 보호가 사실상 없었습니다.
        /// 실제 호출자는 <c>DrivingState</c>와 <c>OnFootState</c> 둘뿐이고 둘 다 이 어셈블리 안에
        /// 있으므로, 밖에서는 부를 수 없게 좁혔습니다. 이제 <b>탑승 상태를 바꾸는 길은
        /// 상태 기계 하나</b>입니다.
        /// </summary>
        /// <param name="vehicle">지금 조종 중인 차량. 하차했다면 null을 넘깁니다.</param>
        internal static void SetCurrent(Vehicle vehicle)
        {
            Current = vehicle;
        }

        /// <summary>
        /// 지정한 위치에서 가장 가까운 차량을 찾습니다. (적의 추적 대상 선정 등에 씁니다)
        /// </summary>
        /// <param name="from">거리를 잴 기준 위치</param>
        /// <returns>가장 가까운 차량. 씬에 차량이 하나도 없으면 null입니다.</returns>
        public static Vehicle FindNearest(Vector3 from)
        {
            Vehicle best = null;
            float bestSqr = float.MaxValue;

            for (int i = 0; i < all.Count; i++)
            {
                if (all[i] == null) continue;

                float sqr = (all[i].transform.position - from).sqrMagnitude;
                if (sqr < bestSqr) { bestSqr = sqr; best = all[i]; }
            }

            return best;
        }

        /// <summary>
        /// 적이 노릴 만한 차량을 돌려줍니다.
        /// 타고 있는 차가 있으면 그 차를, 없으면 가장 가까운 차를 고릅니다.
        /// </summary>
        /// <param name="from">타고 있는 차가 없을 때 거리를 잴 기준 위치</param>
        /// <returns>적이 노릴 차량. 씬에 차량이 하나도 없으면 null입니다.</returns>
        public static Vehicle GetTargetVehicle(Vector3 from)
        {
            if (Current != null) return Current;
            return FindNearest(from);
        }

        // --- Private Methods ---

        /// <summary>
        /// 비어 있는 부품 참조를 자기 계층에서 찾아 채웁니다.
        /// 꺼져 있는 오브젝트(하차 중인 계기판 등)도 포함해서 찾습니다.
        /// </summary>
        private void ResolveParts()
        {
            if (controller == null) controller = GetComponent<CarController>();
            if (input == null) input = GetComponentInChildren<CarInput>(true);
            if (seat == null) seat = GetComponentInChildren<VehicleSeat>(true);
            if (health == null) health = GetComponentInChildren<VehicleHealth>(true);
            if (impactShake == null) impactShake = GetComponentInChildren<CarImpactShake>(true);

            BuildShakables();

            if (seat == null)
            {
                GameLog.Warn(GameLog.Channel.Player, "Vehicle: VehicleSeat을 찾지 못해 승하차 지점을 계산할 수 없습니다.", this);
            }
        }

        /// <summary>
        /// 흔들 대상을 <b>자기 자식 안에서</b> 한 목록으로 모읍니다.
        ///
        /// <b>왜 인스펙터 목록을 없앴는가.</b> 예전에는 계기판 흔들개들을
        /// <c>List&lt;UIElementShaker&gt;</c>로 들고 있었습니다. 그 한 줄 때문에
        /// Gameplay 가 UI 를 이름으로 알아야 했고, 계층 화살표가 거꾸로 났습니다.
        ///
        /// 그런데 그 목록은 원래도 <b>비어 있으면 자식에서 찾아 채우는</b> 것이었습니다.
        /// 찾는 조건을 "UIElementShaker" 에서 "흔들 수 있는 것"(<see cref="IImpactShakable"/>)
        /// 으로 바꾸면 목록 자체가 필요 없어집니다. 차체 흔들개까지 같은 탐색에 잡히므로
        /// 결과도 예전과 같습니다.
        ///
        /// <b>범위는 여전히 이 차량의 자식입니다.</b> 예전에 씬 전체의 계기판을 긁어모아
        /// 한 차가 부딪히면 다른 차 UI 도 흔들리던 문제가 다시 생기지 않습니다.
        /// </summary>
        private void BuildShakables()
        {
            shakables.Clear();
            GetComponentsInChildren(true, shakables);
        }
    }
}
