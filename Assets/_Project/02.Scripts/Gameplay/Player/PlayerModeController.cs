using UnityEngine;
using UnityEngine.Events;

/// <summary>플레이어가 지금 무엇을 조종하고 있는지 나타냅니다.</summary>
public enum PlayerMode
{
    Driving,    // 차량 탑승 중
    OnFoot      // 하차 상태
}

/// <summary>
/// 탑승/하차를 전환하는 컨트롤러입니다.
/// 카메라를 운전석 피벗과 도보 리그의 머리 위치 사이로 옮기고,
/// 그에 맞춰 차량 입력 / PlayerFootMotor / CarCameraFollow를 켜고 끕니다.
///
/// 차량 쪽 부품은 <see cref="Vehicle"/> 하나로 받습니다.
/// 예전에는 CarController·CarInput·VehicleSeat를 각각 들고 있어서
/// 차량이 두 대가 되면 전부 다시 연결해야 했습니다. 지금은 문이 자기 Vehicle을
/// 넘겨 주므로 <b>어느 차의 문을 열든 그 차에 탑니다.</b>
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

    // --- Unity Event Functions ---

    /// <summary>
    /// 필수 참조를 확인한 뒤 시작 모드를 적용합니다.
    /// 반대 상태로 두고 전환을 태워, 도보·주행 어느 쪽으로 시작하든 초기화 경로가 같아집니다.
    /// </summary>
    void Start()
    {
        if (!ValidateReferences()) return;

        // 시작 상태를 강제로 적용합니다. (반대 상태로 두고 전환해 초기화를 일관되게 태웁니다)
        if (startMode == PlayerMode.Driving)
        {
            Mode = PlayerMode.OnFoot;
            EnterVehicle(true);
        }
        else
        {
            Mode = PlayerMode.Driving;
            ExitVehicle(true);
        }
    }

    // 탑승·하차는 모두 문을 조준한 뒤 상호작용 키로 합니다. (VehicleDoorInteractable)
    // 그래서 이 컴포넌트는 입력을 직접 받지 않습니다.

    // --- Public Methods ---

    /// <summary>
    /// 지정한 차량에 탑승합니다. 문(VehicleDoorInteractable)이 자기 차량을 넘겨 호출합니다.
    /// 다른 차를 타고 있었다면 그 차에서 먼저 정리하고 옮겨 탑니다.
    /// </summary>
    public void EnterVehicle(Vehicle target)
    {
        if (target == null)
        {
            Debug.LogWarning("PlayerModeController: 탑승할 차량이 없습니다.", this);
            return;
        }

        // 다른 차에 타고 있었다면 그 차의 조작을 먼저 내려놓습니다.
        if (Mode == PlayerMode.Driving && vehicle != null && vehicle != target)
        {
            ReleaseVehicleControl(vehicle);
        }

        vehicle = target;
        EnterVehicle(false);
    }

    /// <summary>
    /// 지금 지정된 차량에 탑승합니다.
    /// </summary>
    public void EnterVehicle(bool immediate)
    {
        if (Mode == PlayerMode.Driving && !immediate) return;
        if (vehicle == null)
        {
            Debug.LogWarning("PlayerModeController: 탑승할 차량이 없습니다.", this);
            return;
        }

        Mode = PlayerMode.Driving;
        Vehicle.SetCurrent(vehicle);

        // 1. 도보 조작을 끕니다.
        if (footRig != null) footRig.SetActive(false);

        // 2. 카메라를 운전석 피벗으로 옮깁니다.
        Transform pivot = driverPivot != null ? driverPivot : (carCameraFollow != null ? carCameraFollow.transform : null);
        AttachCamera(pivot);

        // 3. 차량 조작을 켭니다.
        if (carCameraFollow != null)
        {
            carCameraFollow.target = vehicle.DriverAnchor;
            carCameraFollow.enabled = true;
        }
        if (vehicle.input != null) vehicle.input.enabled = true;

        // 이 차량의 계기판 체력 표시를 켭니다. (차마다 자기 것을 가집니다)
        if (vehicle.seat != null) vehicle.seat.SetHealthDisplayVisible(true);

        // 4. 주행 진동을 켜고, 어느 차의 진동인지 알려 줍니다.
        if (carCameraEffects != null)
        {
            carCameraEffects.SetVehicle(vehicle);
            carCameraEffects.enabled = true;
        }

        // 5. 마우스 좌우 회전은 운전석 피벗을 돌립니다. (차 안에서 두리번거리기)
        if (lookController != null) lookController.SetPlayerBody(pivot);

        Debug.Log("PlayerModeController: " + vehicle.displayName + "에 탑승했습니다.");
        if (!immediate && onEnteredVehicle != null) onEnteredVehicle.Invoke();
    }

    /// <summary>
    /// 차량에서 내립니다.
    /// </summary>
    public void ExitVehicle(bool immediate)
    {
        if (Mode == PlayerMode.OnFoot && !immediate) return;

        // 달리는 중에는 내릴 수 없습니다.
        if (!immediate && vehicle != null && vehicle.controller != null
            && vehicle.controller.GetCurrentSpeed() > maxExitSpeed)
        {
            Debug.Log("PlayerModeController: 속도가 너무 빨라 내릴 수 없습니다.");
            return;
        }

        Mode = PlayerMode.OnFoot;
        Vehicle.SetCurrent(null);

        // 1. 차량 조작을 끕니다. (입력 값도 반드시 비웁니다)
        ReleaseVehicleControl(vehicle);
        if (carCameraFollow != null) carCameraFollow.enabled = false;

        // 주행 진동을 끄고 카메라 위치를 원래대로 되돌려 둡니다.
        // (LateUpdate에서 localPosition을 계속 덮어쓰기 때문에 반드시 꺼야 합니다)
        if (carCameraEffects != null) carCameraEffects.enabled = false;

        // 2. 도보 리그를 하차 지점에 놓습니다.
        PlaceFootRig();

        // 3. 카메라를 머리 위치로 옮깁니다.
        AttachCamera(headMount);

        // 4. 마우스 좌우 회전은 도보 리그 본체를 돌립니다.
        if (lookController != null && footRig != null) lookController.SetPlayerBody(footRig.transform);

        Debug.Log("PlayerModeController: 차량에서 내렸습니다.");
        if (!immediate && onExitedVehicle != null) onExitedVehicle.Invoke();
    }

    /// <summary>현재 상태를 반대로 전환합니다. (UI 버튼 등에서 호출)</summary>
    public void Toggle()
    {
        if (Mode == PlayerMode.Driving) ExitVehicle(false);
        else EnterVehicle(false);
    }

    // --- Private Methods ---

    /// <summary>
    /// 차량의 조작을 내려놓습니다. 입력 값을 비우지 않으면 마지막 값이 남아 차가 계속 달립니다.
    /// </summary>
    private void ReleaseVehicleControl(Vehicle target)
    {
        if (target == null || target.input == null) return;

        target.input.ResetInput();
        target.input.enabled = false;

        // 차 밖에서는 계기판 체력 표시를 끕니다.
        if (target.seat != null) target.seat.SetHealthDisplayVisible(false);
    }

    /// <summary>
    /// 필수 참조가 빠졌는지 확인합니다.
    /// </summary>
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

    /// <summary>
    /// 카메라를 지정한 부모 아래로 옮기고 로컬 위치·회전을 초기화합니다.
    /// </summary>
    private void AttachCamera(Transform parent)
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
    private void PlaceFootRig()
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
}
