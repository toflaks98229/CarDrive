using System.Collections.Generic;
using UnityEngine;
using CarDrive.UI;
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

        /// <summary>플레이어가 지금 타고 있는 차량입니다. 걸어 다니는 중이면 null입니다.</summary>
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

        /// <summary>
        /// 이 차량의 계기판 중 충돌 시 흔들 것들입니다. 비워두면 자식에서 모두 찾습니다.
        /// 예전에는 충돌 처리가 씬 전체의 계기판을 긁어모아, 한 차가 부딪히면 다른 차 UI도 흔들렸습니다.
        /// </summary>
        [Tooltip("이 차량의 계기판 중 충돌 시 흔들 것들. 비워두면 자식에서 모두 찾습니다. " +
                 "예전에는 충돌 처리가 씬 전체의 계기판을 긁어모아, 한 차가 부딪히면 다른 차 UI도 흔들렸습니다.")]
        public List<UIElementShaker> dashboardShakers = new List<UIElementShaker>();

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

        // --- Public Methods ---

        /// <summary>
        /// 지금 조종 중인 차량을 지정합니다. PlayerModeController가 탑승·하차할 때 호출합니다.
        /// null을 넘기면 "아무 차도 타고 있지 않음"이 됩니다.
        /// </summary>
        /// <param name="vehicle">지금 조종 중인 차량. 하차했다면 null을 넘깁니다.</param>
        public static void SetCurrent(Vehicle vehicle)
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

            if (dashboardShakers == null) dashboardShakers = new List<UIElementShaker>();
            if (dashboardShakers.Count == 0)
            {
                GetComponentsInChildren(true, dashboardShakers);
            }

            BuildShakables();

            if (seat == null)
            {
                Debug.LogWarning("Vehicle: VehicleSeat을 찾지 못해 승하차 지점을 계산할 수 없습니다.", this);
            }
        }

        /// <summary>
        /// 흔들 대상을 한 목록으로 모읍니다.
        ///
        /// 인스펙터 참조(<see cref="impactShake"/>·<see cref="dashboardShakers"/>)는 그대로 둡니다.
        /// 씬에 이미 이어 둔 배선을 잃지 않으면서, 부딪히는 쪽에는 목록 하나만 보여 주기 위해서입니다.
        /// </summary>
        private void BuildShakables()
        {
            shakables.Clear();

            if (impactShake != null) shakables.Add(impactShake);

            for (int i = 0; i < dashboardShakers.Count; i++)
            {
                if (dashboardShakers[i] != null) shakables.Add(dashboardShakers[i]);
            }
        }
    }
}
