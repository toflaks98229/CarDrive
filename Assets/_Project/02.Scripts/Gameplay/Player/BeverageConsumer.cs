using System.Collections;
using UnityEngine;

/// <summary>
/// 음료를 마시고 빈 병을 던지는 절차 전체를 소유합니다.
///
/// 예전에는 이 일이 PlayerInteractor 안에 들어 있었습니다. 그래서 범용 상호작용기가
/// 회복량·갈증·배뇨·마시기 연출 같은 <b>음료 전용 값</b>을 들고 있었고, 음료 종류를
/// 늘리려면 상호작용 시스템을 고쳐야 했습니다. 이제 상자와 낱개 병 양쪽이
/// 이 컴포넌트에 "마셔 달라"고 요청합니다.
///
/// 마시는 절차는 이렇게 흘러갑니다.
///  1. 병을 <b>즉시</b> 감춥니다. (입에 가져가는 순간 사라져야 자연스럽습니다)
///  2. 회복·갈증·배뇨를 적용하고 연출과 소리를 냅니다.
///  3. 연출이 끝나면 병을 <b>빈 병으로 바꿔</b> 다시 꺼내 던집니다.
///
/// 그 사이에는 <see cref="IsBusy"/>가 켜져 있어 E 상호작용이 막힙니다.
/// 연타로 여러 병을 한꺼번에 비우지 못하게 하는 것이 목적입니다.
/// </summary>
public class BeverageConsumer : MonoBehaviour
{
    // --- Public Member Variables ---

    /// <summary>마셨을 때 회복할 체력입니다. 비워두면 씬에서 PlayerHealth를 찾습니다.</summary>
    [Header("효과")]
    [Tooltip("음료를 마셨을 때 회복할 체력")]
    public float healAmount = 10f;

    /// <summary>마셨을 때 해소되는 갈증입니다.</summary>
    [Tooltip("음료를 마셨을 때 해소되는 갈증")]
    public float thirstRelief = 0.5f;

    /// <summary>마셨을 때 차오르는 배뇨입니다.</summary>
    [Tooltip("음료를 마셨을 때 차오르는 배뇨")]
    public float urineGain = 0.25f;

    /// <summary>회복할 대상입니다. 비워두면 실행 중에 찾습니다.</summary>
    [Header("연동 (비워두면 실행 중에 찾습니다)")]
    [Tooltip("회복할 플레이어 체력")]
    public PlayerHealth playerHealth;

    /// <summary>니즈를 반영할 시스템입니다.</summary>
    [Tooltip("갈증·배뇨를 반영할 니즈 시스템")]
    public NeedsSystem needsSystem;

    /// <summary>마시는 연출입니다. 이 연출이 끝나는 시점에 병을 던집니다.</summary>
    [Tooltip("마시는 연출. 이 연출이 끝나는 시점에 빈 병을 던집니다.")]
    public DrinkAnimation drinkAnimator;

    /// <summary>마시는 소리를 낼 컨트롤러입니다.</summary>
    [Tooltip("마시는 소리를 낼 컨트롤러")]
    public PlayerSoundController soundController;

    /// <summary>탑승 여부를 확인할 컨트롤러입니다. 던지는 곳이 달라집니다.</summary>
    [Tooltip("탑승 여부를 확인할 컨트롤러. 주행 중이면 창밖으로 던집니다.")]
    public PlayerModeController modeController;

    /// <summary>연출이 없을 때 쓸 마시는 시간(초)입니다.</summary>
    [Header("시간")]
    [Tooltip("마시는 연출이 없을 때 쓸 시간(초)")]
    public float fallbackDrinkSeconds = 1.6f;

    /// <summary>운전석 기준으로 병을 내보낼 위치입니다. 왼쪽 창밖입니다.</summary>
    [Header("던지기 - 주행 중 (운전석 왼쪽 창밖)")]
    [Tooltip("운전석 기준 로컬 좌표. X가 음수여야 왼쪽 창밖으로 나갑니다.")]
    public Vector3 windowLocalOffset = new Vector3(-0.95f, -0.1f, 0.15f);

    /// <summary>창밖으로 던지는 속도입니다.</summary>
    [Tooltip("창밖으로 던지는 속도(m/s)")]
    public float windowThrowSpeed = 3.5f;

    /// <summary>창밖으로 던질 때 위로 뜨는 정도입니다.</summary>
    [Tooltip("창밖으로 던질 때 위로 뜨는 비율")]
    [Range(0f, 1f)]
    public float windowThrowUp = 0.35f;

    /// <summary>도보에서 던지는 기준입니다. 비워두면 조준 기준을 씁니다.</summary>
    [Header("던지기 - 도보")]
    [Tooltip("도보에서 던질 기준. 비워두면 조준 기준(카메라)을 씁니다.")]
    public Transform footThrowOrigin;

    /// <summary>도보에서 던지는 속도입니다.</summary>
    [Tooltip("도보에서 던지는 속도(m/s)")]
    public float footThrowSpeed = 6.5f;

    /// <summary>도보에서 던질 때 위로 뜨는 정도입니다.</summary>
    [Tooltip("도보에서 던질 때 위로 뜨는 비율")]
    [Range(0f, 1f)]
    public float footThrowUp = 0.25f;

    /// <summary>병이 굴러갈 때 도는 정도입니다.</summary>
    [Tooltip("던질 때 병이 도는 정도")]
    public float throwSpin = 6f;

    // --- Public Properties ---

    /// <summary>
    /// 지금 마시는 중인지 여부입니다.
    /// 켜져 있는 동안 E 상호작용이 막혀 연타로 여러 병을 비울 수 없습니다.
    /// </summary>
    public bool IsBusy { get; private set; }

    // --- Private Member Variables ---

    /// <summary>조준 기준입니다. 도보에서 던질 방향으로 씁니다.</summary>
    private Transform aim;

    /// <summary>지금 마시는 중인 병입니다. 중간에 끊겼을 때 마무리하는 데 씁니다.</summary>
    private Beverage pending;

    /// <summary>마시기 시작할 때 타고 있던 차량입니다.</summary>
    private Vehicle pendingVehicle;

    // --- Unity Event Functions ---

    /// <summary>
    /// 비어 있는 참조를 채웁니다.
    /// </summary>
    void Start()
    {
        aim = PlayerAim.Resolve(footThrowOrigin, this);

        if (playerHealth == null) playerHealth = FindAnyObjectByType<PlayerHealth>();
        if (needsSystem == null) needsSystem = FindAnyObjectByType<NeedsSystem>();
        if (drinkAnimator == null) drinkAnimator = FindAnyObjectByType<DrinkAnimation>();
        if (modeController == null) modeController = FindAnyObjectByType<PlayerModeController>();
        if (soundController == null) soundController = GetComponent<PlayerSoundController>();

        if (playerHealth == null)
        {
            Debug.LogWarning("BeverageConsumer: PlayerHealth를 찾지 못해 음료로 회복할 수 없습니다.", this);
        }
    }

    /// <summary>
    /// 마시는 도중에 꺼졌을 때 뒤처리를 합니다.
    ///
    /// 탑승할 때 도보 리그가 통째로 꺼지는데, 카메라가 그 아래에 있으면 이 컴포넌트도
    /// 잠깐 함께 꺼집니다. Unity는 그 순간 코루틴을 <b>영구히</b> 중단하므로,
    /// 아무것도 하지 않으면 병은 감춰진 채로 사라지고 IsBusy가 켜진 채 남아
    /// E 상호작용이 영영 막힙니다.
    ///
    /// 그래서 남은 절차를 여기서 즉시 끝냅니다.
    /// </summary>
    void OnDisable()
    {
        if (!IsBusy) return;

        StopAllCoroutines();

        if (pending != null) ThrowEmpty(pending, pendingVehicle);

        pending = null;
        pendingVehicle = null;
        IsBusy = false;
    }

    // --- Public Methods ---

    /// <summary>
    /// 음료 하나를 마십니다. 이미 마시는 중이면 아무 일도 하지 않습니다.
    /// </summary>
    /// <param name="bottle">마실 음료</param>
    /// <returns>마시기 시작했으면 true를 반환합니다.</returns>
    public bool Drink(Beverage bottle)
    {
        if (IsBusy || bottle == null || !isActiveAndEnabled) return false;

        StartCoroutine(DrinkRoutine(bottle));
        return true;
    }

    // --- Private Methods ---

    /// <summary>
    /// 마시고, 연출이 끝나면 빈 병을 던지는 한 사이클입니다.
    /// </summary>
    /// <param name="bottle">마실 음료</param>
    private IEnumerator DrinkRoutine(Beverage bottle)
    {
        IsBusy = true;
        pending = bottle;

        // 어느 차에 타고 던질지는 마시기 <b>시작</b> 시점으로 정합니다.
        // 마시는 도중에 내리면 던질 곳이 사라지기 때문입니다.
        Vehicle vehicle = (modeController != null && modeController.Mode == PlayerMode.Driving)
            ? modeController.CurrentVehicle
            : null;
        pendingVehicle = vehicle;

        // 1. 병을 즉시 감춥니다. 상자에 들어 있었다면 목록에서도 뺍니다.
        bottle.LeaveBox();
        bottle.transform.SetParent(null, true);
        bottle.gameObject.SetActive(false);

        // 2. 효과와 연출.
        if (playerHealth != null) playerHealth.Heal(healAmount);
        if (needsSystem != null)
        {
            needsSystem.Satisfy(NeedType.Thirst, thirstRelief);
            needsSystem.Add(NeedType.Urine, urineGain);
        }

        if (soundController != null) soundController.PlayDrinkSound();

        float wait = fallbackDrinkSeconds;
        if (drinkAnimator != null)
        {
            drinkAnimator.PlayDrinkAnimation();
            wait = drinkAnimator.TotalDuration;
        }

        yield return new WaitForSeconds(wait);

        // 3. 다 마셨으니 빈 병으로 바꿔 던집니다.
        ThrowEmpty(bottle, vehicle);

        pending = null;
        pendingVehicle = null;
        IsBusy = false;
    }

    /// <summary>
    /// 병을 빈 병으로 바꿔 던집니다.
    ///
    /// 새 프리팹을 만들지 않고 <b>같은 오브젝트를 재사용</b>합니다.
    /// Beverage 컴포넌트를 떼면 더 이상 마실 수 없게 되므로, 그것이 곧 빈 병입니다.
    /// </summary>
    /// <param name="bottle">비워진 병</param>
    /// <param name="vehicle">주행 중이었다면 그 차량. 도보였다면 null입니다.</param>
    private void ThrowEmpty(Beverage bottle, Vehicle vehicle)
    {
        if (bottle == null) return;

        GameObject go = bottle.gameObject;

        // 마실 수 있는 표식을 떼어 냅니다. 이 순간부터 빈 병입니다.
        Destroy(bottle);

        Vector3 origin;
        Vector3 velocity;
        ResolveThrow(vehicle, out origin, out velocity);

        go.transform.SetPositionAndRotation(origin, Random.rotation);
        go.SetActive(true);

        Rigidbody body = go.GetComponent<Rigidbody>();
        if (body == null) return;

        body.isKinematic = false;
        body.linearVelocity = velocity;
        body.angularVelocity = Random.insideUnitSphere * throwSpin;
    }

    /// <summary>
    /// 던질 위치와 속도를 정합니다.
    ///
    /// 주행 중이면 운전석 왼쪽 창밖으로 내보내고 <b>차의 속도를 더해</b> 줍니다.
    /// 그러지 않으면 시속 80km로 달리는 차에서 병이 제자리에 떨어져 곧바로 뒤로 튕깁니다.
    /// </summary>
    /// <param name="vehicle">주행 중이면 그 차량, 아니면 null</param>
    /// <param name="origin">던질 위치가 담깁니다.</param>
    /// <param name="velocity">던질 속도가 담깁니다.</param>
    private void ResolveThrow(Vehicle vehicle, out Vector3 origin, out Vector3 velocity)
    {
        if (vehicle != null)
        {
            Transform seat = vehicle.DriverAnchor != null ? vehicle.DriverAnchor : vehicle.transform;

            origin = seat.TransformPoint(windowLocalOffset);

            // 차 기준 왼쪽으로 밀어냅니다.
            Vector3 outward = (-vehicle.transform.right + Vector3.up * windowThrowUp).normalized;
            velocity = outward * windowThrowSpeed;

            Rigidbody carBody = vehicle.GetComponent<Rigidbody>();
            if (carBody != null) velocity += carBody.linearVelocity;

            return;
        }

        Transform from = aim != null ? aim : transform;

        origin = from.position + from.forward * 0.5f;
        velocity = (from.forward + Vector3.up * footThrowUp).normalized * footThrowSpeed;
    }
}
