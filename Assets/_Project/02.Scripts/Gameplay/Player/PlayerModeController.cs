using UnityEngine;
using UnityEngine.Events;

/// <summary>플레이어가 지금 무엇을 조종하고 있는지 나타냅니다.</summary>
public enum PlayerMode
{
    Driving,    // 차량 탑승 중
    OnFoot      // 하차 상태
}

/// <summary>
/// 플레이어 상태를 보관하고 전환을 실행하는 컨트롤러입니다.
///
/// 어떤 상태가 되면 무엇을 켜고 무엇을 끄는지는 이 클래스가 아니라
/// <see cref="IPlayerState"/> 구현이 들고 있습니다. (DrivingState / OnFootState)
/// 이 클래스가 하는 일은 셋입니다. <b>참조 보관, 전환 실행, 전환 가능 여부 판단.</b>
///
/// 예전에는 탑승·하차 메서드가 여덟 개 참조의 enabled를 직접 토글했습니다.
/// 상태가 둘일 때는 성립하지만, 수면·정비·대화·사망이 붙을 때마다 켜고 끌 조합이
/// 배로 늘어납니다. 이제 새 상태는 IPlayerState 구현을 하나 추가하면 되고
/// 이 클래스와 기존 상태 클래스는 건드리지 않습니다.
///
/// 인스펙터 참조는 여기 그대로 두었습니다. 상태가 참조를 나눠 가지면
/// 씬에서 연결해야 할 곳이 상태 수만큼 늘어나기 때문입니다.
///
/// 주의: 차량 입력과 PlayerFootMotor는 둘 다 Horizontal/Vertical 축과 Space를 쓰므로
/// 반드시 한쪽만 켜져 있어야 합니다.
/// </summary>
public class PlayerModeController : MonoBehaviour
{
    // --- Public Member Variables ---

    [Header("시작 상태")]
    [Tooltip("게임을 시작할 때의 상태")]
    public PlayerMode startMode = PlayerMode.Driving;

    [Header("도보 리그")]
    [Tooltip("CharacterController와 PlayerFootMotor가 붙은 오브젝트")]
    public GameObject footRig;

    [Tooltip("도보 상태에서 카메라가 붙을 위치 (눈높이)")]
    public Transform headMount;

    [Header("차량")]
    [Tooltip("시작할 때 탈 차량. 비워두면 씬에서 찾습니다. " +
             "실제 탑승 대상은 문을 조준할 때 그 문이 정해 줍니다.")]
    public Vehicle vehicle;

    [Tooltip("운전석을 따라다니는 카메라 리그의 CarCameraFollow")]
    public CarCameraFollow carCameraFollow;

    [Tooltip("주행 중 카메라가 붙을 피벗. 카메라 리그의 자식으로 두면 차 안에서 두리번거릴 수 있습니다.")]
    public Transform driverPivot;

    [Header("카메라")]
    [Tooltip("옮겨 다닐 메인 카메라")]
    public Transform mainCamera;

    [Tooltip("마우스 시점 컨트롤러. 몸체 참조를 상태에 맞춰 바꿔 줍니다.")]
    public PlayerCameraController lookController;

    [Tooltip("주행 진동 효과. 도보 상태에서는 꺼야 걸어다닐 때 화면이 떨리지 않습니다.")]
    public CarCameraEffects carCameraEffects;

    [Header("하차 조건")]
    [Tooltip("이 속도(km/h)보다 빠르면 내릴 수 없습니다.")]
    public float maxExitSpeed = 5f;

    [Header("이벤트")]
    public UnityEvent onEnteredVehicle;
    public UnityEvent onExitedVehicle;

    // --- Public Properties ---

    /// <summary>현재 상태입니다.</summary>
    public PlayerMode Mode { get; private set; }

    /// <summary>지금 타고 있는(또는 마지막으로 탔던) 차량입니다.</summary>
    public Vehicle CurrentVehicle { get { return vehicle; } }

    // --- Private Member Variables ---

    /// <summary>주행 상태입니다. 참조를 들고 있지 않아 한 번만 만들어 재사용합니다.</summary>
    private readonly DrivingState drivingState = new DrivingState();

    /// <summary>도보 상태입니다.</summary>
    private readonly OnFootState onFootState = new OnFootState();

    /// <summary>지금 적용 중인 상태입니다.</summary>
    private IPlayerState current;

    // --- Unity Event Functions ---

    /// <summary>
    /// 필수 참조를 확인한 뒤 시작 모드를 적용합니다.
    /// </summary>
    void Start()
    {
        if (!ValidateReferences()) return;

        // 시작 상태의 반대편에서 출발해 전환을 태웁니다.
        // 그래야 도보로 시작하든 주행으로 시작하든 초기화 경로가 같아지고,
        // 반대편 정리(도보로 시작할 때 차량 입력을 꺼 두는 것 등)가 반드시 한 번 실행됩니다.
        //
        // 차량을 끝내 찾지 못했다면 주행으로 시작할 수 없으므로 도보로 떨어뜨립니다.
        // (그러지 않으면 상태만 주행이고 실제로는 아무것도 설정되지 않은 채 시작됩니다)
        bool startDriving = startMode == PlayerMode.Driving && vehicle != null;
        current = startDriving ? (IPlayerState)onFootState : drivingState;

        SetState(startDriving ? (IPlayerState)drivingState : onFootState, true);
    }

    // 탑승·하차는 모두 문을 조준한 뒤 상호작용 키로 합니다. (VehicleDoorInteractable)
    // 그래서 이 컴포넌트는 입력을 직접 받지 않습니다.

    // --- Public Methods ---

    /// <summary>
    /// 지정한 차량에 탑승합니다. 문(VehicleDoorInteractable)이 자기 차량을 넘겨 호출합니다.
    /// 다른 차를 타고 있었다면 그 차에서 정리하고 그대로 옮겨 탑니다.
    /// </summary>
    /// <param name="target">탑승할 차량</param>
    public void EnterVehicle(Vehicle target)
    {
        if (target == null)
        {
            Debug.LogWarning("PlayerModeController: 탑승할 차량이 없습니다.", this);
            return;
        }

        // 이미 이 차를 타고 있으면 아무 일도 하지 않습니다.
        if (Mode == PlayerMode.Driving && vehicle == target) return;

        // 다른 차를 타고 있었다면 그 차의 조작을 먼저 내려놓습니다.
        if (Mode == PlayerMode.Driving && vehicle != null) ReleaseVehicleControl(vehicle);

        vehicle = target;

        // 이미 주행 중이더라도 새 차량으로 다시 진입해야 합니다.
        // 예전에는 여기서 EnterVehicle(false)를 불렀는데, 그 안의 "이미 주행 중이면 반환"
        // 조건에 걸려 새 차의 설정이 실행되지 않았습니다. 그래서 옮겨 타면 옛 차의 조작은
        // 풀렸는데 새 차의 조작은 켜지지 않아 두 차 모두 조종할 수 없었습니다.
        SetState(drivingState, false);
    }

    /// <summary>
    /// 지금 지정된 차량에 탑승합니다.
    /// </summary>
    /// <param name="immediate">true면 전환 이벤트를 발생시키지 않습니다. (게임 시작·세이브 복원용)</param>
    public void EnterVehicle(bool immediate)
    {
        if (Mode == PlayerMode.Driving && !immediate) return;

        if (vehicle == null)
        {
            Debug.LogWarning("PlayerModeController: 탑승할 차량이 없습니다.", this);
            return;
        }

        SetState(drivingState, immediate);
    }

    /// <summary>
    /// 차량에서 내립니다.
    /// </summary>
    /// <param name="immediate">true면 속도 검사와 전환 이벤트를 건너뜁니다. (게임 시작·세이브 복원용)</param>
    public void ExitVehicle(bool immediate)
    {
        if (Mode == PlayerMode.OnFoot && !immediate) return;

        // 달리는 중에는 내릴 수 없습니다.
        if (!immediate && !CanExitVehicle())
        {
            Debug.Log("PlayerModeController: 속도가 너무 빨라 내릴 수 없습니다.");
            return;
        }

        SetState(onFootState, immediate);
    }

    /// <summary>
    /// 지금 내릴 수 있는 속도인지 확인합니다.
    /// 안내 문구(VehicleDoorInteractable)와 실제 하차 판정이 <b>같은 규칙</b>을 보도록 여기에 둡니다.
    /// </summary>
    /// <returns>차량이 없거나 충분히 느리면 true를 반환합니다.</returns>
    public bool CanExitVehicle()
    {
        if (vehicle == null || vehicle.controller == null) return true;
        return vehicle.controller.GetCurrentSpeed() <= maxExitSpeed;
    }

    /// <summary>현재 상태를 반대로 전환합니다. (UI 버튼 등에서 호출)</summary>
    public void Toggle()
    {
        if (Mode == PlayerMode.Driving) ExitVehicle(false);
        else EnterVehicle(false);
    }

    // --- Public Methods : 상태 구현이 호출하는 동작 ---
    // 아래 넷은 IPlayerState 구현이 쓰라고 열어 둔 것입니다. 바깥에서 직접 부르지 마세요.

    /// <summary>
    /// 차량의 조작을 내려놓습니다. 입력 값을 비우지 않으면 마지막 값이 남아 차가 계속 달립니다.
    /// </summary>
    /// <param name="target">조작을 내려놓을 차량. null이면 아무것도 하지 않습니다.</param>
    public void ReleaseVehicleControl(Vehicle target)
    {
        if (target == null || target.input == null) return;

        target.input.ResetInput();
        target.input.enabled = false;

        // 차 밖에서는 계기판 체력 표시를 끕니다.
        if (target.seat != null) target.seat.SetHealthDisplayVisible(false);
    }

    /// <summary>
    /// 주행 중 카메라가 붙을 피벗을 돌려줍니다.
    /// </summary>
    /// <returns>지정된 운전석 피벗. 없으면 카메라 리그, 그것도 없으면 null입니다.</returns>
    public Transform GetDriverPivot()
    {
        if (driverPivot != null) return driverPivot;
        return carCameraFollow != null ? carCameraFollow.transform : null;
    }

    /// <summary>
    /// 카메라를 지정한 부모 아래로 옮기고 로컬 위치·회전을 초기화합니다.
    /// </summary>
    /// <param name="parent">카메라를 붙일 부모. null이면 아무것도 하지 않습니다.</param>
    public void AttachCamera(Transform parent)
    {
        if (mainCamera == null || parent == null) return;

        mainCamera.SetParent(parent, false);
        mainCamera.localPosition = Vector3.zero;
        mainCamera.localRotation = Quaternion.identity;
    }

    /// <summary>
    /// 도보 리그를 하차 지점으로 옮겨 활성화합니다.
    /// CharacterController는 켜져 있으면 위치 대입을 무시하므로 잠시 꺼야 합니다.
    /// </summary>
    public void PlaceFootRig()
    {
        if (footRig == null) return;

        Vector3 exitPosition = (vehicle != null && vehicle.seat != null)
            ? vehicle.seat.GetExitPosition()
            : transform.position;

        // 차량의 좌우 기울기를 따라가지 않도록 Y축 회전만 가져옵니다.
        float yaw = vehicle != null
            ? vehicle.transform.eulerAngles.y
            : footRig.transform.eulerAngles.y;

        CharacterController cc = footRig.GetComponent<CharacterController>();
        if (cc != null) cc.enabled = false;

        footRig.SetActive(true);
        footRig.transform.SetPositionAndRotation(exitPosition, Quaternion.Euler(0f, yaw, 0f));

        if (cc != null) cc.enabled = true;
    }

    // --- Private Methods ---

    /// <summary>
    /// 상태를 전환합니다.
    ///
    /// 같은 상태로 다시 들어오는 경우(다른 차량으로 갈아타기)에는 Exit을 건너뛰고
    /// Enter만 다시 실행합니다. 그래야 옛 차량을 정리한 뒤 새 차량으로 설정이 다시 잡힙니다.
    /// </summary>
    /// <param name="next">전환할 상태</param>
    /// <param name="immediate">true면 전환 이벤트를 발생시키지 않습니다.</param>
    private void SetState(IPlayerState next, bool immediate)
    {
        if (next == null) return;

        if (current != null && current != next) current.Exit(this);

        current = next;
        Mode = next.Mode;
        current.Enter(this);

        if (immediate) return;

        UnityEvent raised = next.Mode == PlayerMode.Driving ? onEnteredVehicle : onExitedVehicle;
        if (raised != null) raised.Invoke();
    }

    /// <summary>
    /// 필수 참조가 빠졌는지 확인합니다.
    /// </summary>
    /// <returns>상태 전환에 필요한 참조가 모두 있으면 true를 반환합니다.</returns>
    private bool ValidateReferences()
    {
        bool ok = true;

        if (mainCamera == null) { Debug.LogError("PlayerModeController: mainCamera가 없습니다.", this); ok = false; }
        if (footRig == null) { Debug.LogError("PlayerModeController: footRig가 없습니다.", this); ok = false; }
        if (headMount == null) { Debug.LogError("PlayerModeController: headMount가 없습니다.", this); ok = false; }

        // 차량은 인스펙터로 연결하지 않아도 됩니다.
        // 차량 프리팹 안에 있는 것을 씬 오브젝트가 가리키기 번거롭기 때문입니다.
        if (vehicle == null)
        {
            vehicle = Vehicle.Current != null ? Vehicle.Current : FindAnyObjectByType<Vehicle>();
        }
        if (vehicle == null)
        {
            Debug.LogWarning("PlayerModeController: 씬에서 Vehicle을 찾지 못했습니다. 탑승할 수 없습니다.", this);
        }

        if (!ok) enabled = false;
        return ok;
    }
}
