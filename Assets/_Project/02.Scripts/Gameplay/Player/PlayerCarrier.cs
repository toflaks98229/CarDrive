using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 마우스 좌클릭으로 물건을 들고 내려놓습니다.
///
/// 조준점에 Carryable이 걸려 있을 때만 집을 수 있고, 들고 있는 동안에는
/// 물체가 속도로 손 위치를 따라오므로 벽이나 문틀에 걸리면 그대로 막힙니다.
/// 이 컴포넌트는 PlayerInteractor와 같은 카메라 오브젝트에 붙입니다.
/// </summary>
public class PlayerCarrier : MonoBehaviour
{
    // --- Public Member Variables ---

    [Header("연동")]
    [Tooltip("물건이 따라올 손 위치. 비워두면 카메라 앞에 자동으로 만듭니다.")]
    public Transform holdPoint;

    [Tooltip("들고 있는 물건과 충돌을 무시할 플레이어 콜라이더. 비워두면 자동으로 찾습니다.")]
    public Collider playerCollider;

    [Header("입력")]
    [Tooltip("들기 / 내려놓기 버튼 (0 = 좌클릭)")]
    public int carryMouseButton = 0;

    [Header("조준")]
    [Tooltip("조준 광선을 쏠 기준. 비워두면 이 오브젝트가 카메라인지 확인하고, 아니면 Camera.main을 씁니다.")]
    public Transform aimSource;

    [Tooltip("이 거리 안에서 화면 중앙에 걸린 물건만 집을 수 있습니다.")]
    public float pickupDistance = 2.5f;

    [Tooltip("집을 물건을 찾을 레이어. 물건이 물리로 굴러다녀야 하므로 상호작용 레이어와 따로 둡니다.")]
    public LayerMask carryableLayers = ~0;

    [Header("손 위치")]
    [Tooltip("holdPoint를 자동 생성할 때 카메라로부터의 거리")]
    public float holdDistance = 1.1f;

    [Tooltip("holdPoint를 자동 생성할 때의 높이 오프셋")]
    public float holdHeightOffset = -0.15f;

    [Header("따라오는 방식")]
    [Tooltip("손 위치로 끌려오는 속도. 클수록 딱딱 붙습니다.")]
    public float followSpeed = 12f;

    [Tooltip("따라오는 최대 속도. 너무 크면 벽을 뚫을 수 있습니다.")]
    public float maxFollowSpeed = 8f;

    [Tooltip("회전을 맞추는 속도")]
    public float rotationSpeed = 12f;

    [Header("놓치는 조건")]
    [Tooltip("손에서 이 거리보다 멀어지면 자동으로 놓습니다. (벽에 끼었을 때 대비)")]
    public float breakDistance = 2.2f;

    [Tooltip("이 질량보다 무거우면 들 수 없습니다.")]
    public float maxCarryMass = 40f;

    [Header("던지기")]
    [Tooltip("내려놓을 때 앞으로 밀어내는 힘. 0이면 그 자리에 놓습니다.")]
    public float throwImpulse = 2.5f;

    [Header("문구 (다국어 대응)")]
    [Tooltip("{0}에는 물건 이름이 들어갑니다.")]
    public string pickupFormat = "좌클릭: {0} 들기";

    [Tooltip("들고 있을 때 표시할 문구")]
    public string dropLabel = "좌클릭: 내려놓기";

    // --- Public Properties ---

    /// <summary>지금 들고 있는 물건입니다. (없으면 null)</summary>
    public Carryable Held { get; private set; }

    /// <summary>무언가 들고 있는지 여부입니다.</summary>
    public bool IsCarrying { get { return Held != null; } }

    /// <summary>조준점에 걸린 집을 수 있는 물건입니다. (없으면 null)</summary>
    public Carryable Target { get; private set; }

    /// <summary>
    /// 좌클릭이 들기/내려놓기에 쓰이는 상황인지 여부입니다.
    /// PlayerAttacker가 이 값을 보고 앙크를 꺼낼지 판단합니다.
    /// </summary>
    public bool UsesLeftClick { get { return IsCarrying || Target != null; } }

    /// <summary>화면에 표시할 안내 문구입니다. 없으면 빈 문자열입니다.</summary>
    public string GetPrompt()
    {
        if (IsCarrying) return dropLabel;
        if (Target != null) return string.Format(pickupFormat, Target.displayName);
        return "";
    }

    // --- Private Member Variables ---

    /// <summary>
    /// 들고 있는 동안 플레이어와의 충돌을 꺼 둔 콜라이더들입니다.
    /// 내려놓을 때 이 목록을 보고 충돌을 되살립니다.
    /// </summary>
    private readonly List<Collider> ignoredColliders = new List<Collider>();

    /// <summary>조준·손 위치·던지는 방향의 기준입니다. 보통 메인 카메라입니다.</summary>
    private Transform aimTransform;

    /// <summary>
    /// 손 기준으로 본 물건의 회전입니다. 집어 든 순간의 각도를 기억해 두었다가
    /// 들고 있는 동안 그대로 유지합니다. 그래서 <b>들어 올릴 때 물건이 홱 돌아가지 않습니다.</b>
    /// </summary>
    private Quaternion heldRotationOffset = Quaternion.identity;

    // --- Unity Event Functions ---

    /// <summary>
    /// 물건을 들 손 위치와 플레이어 콜라이더 참조를 준비합니다.
    /// </summary>
    void Start()
    {
        // "카메라에 붙어 있다"는 가정을 주석이 아니라 코드로 확인합니다.
        // 손 위치를 이 기준의 자식으로 만들기 때문에 반드시 먼저 정해야 합니다.
        aimTransform = PlayerAim.Resolve(aimSource, this);

        if (holdPoint == null) CreateHoldPoint();
        if (playerCollider == null) playerCollider = FindAnyObjectByType<CharacterController>();
    }

    /// <summary>
    /// 조준 대상을 갱신하고 좌클릭으로 들기·내려놓기를 처리합니다.
    /// 벽에 끼어 손에서 너무 멀어지면 놓칩니다.
    /// </summary>
    void Update()
    {
        // 오버레이 버튼을 누르는 클릭이 들기/내려놓기로 들어가지 않게 합니다.
        // (들고 있던 것은 그대로 유지합니다)
        if (GameInputGate.Suspended) return;

        UpdateTarget();

        if (Input.GetMouseButtonDown(carryMouseButton))
        {
            if (IsCarrying) Drop(true);
            else if (Target != null) PickUp(Target);
        }

        // 벽에 끼어 손에서 너무 멀어지면 놓칩니다.
        if (IsCarrying && holdPoint != null)
        {
            float distance = Vector3.Distance(Held.Body.position, holdPoint.position);
            if (distance > breakDistance) Drop(false);
        }
    }

    /// <summary>
    /// 들고 있는 물건을 손 위치로 끌어당깁니다.
    /// 위치를 직접 대입하지 않고 속도로 움직이므로 벽을 뚫지 않고 문틀에 걸립니다.
    /// </summary>
    void FixedUpdate()
    {
        if (!IsCarrying || holdPoint == null) return;

        Rigidbody body = Held.Body;

        // 위치: 손 위치로 향하는 속도를 직접 넣습니다. (질량과 무관하게 같은 느낌)
        Vector3 delta = holdPoint.position - body.position;
        Vector3 velocity = delta * followSpeed;
        if (velocity.magnitude > maxFollowSpeed) velocity = velocity.normalized * maxFollowSpeed;
        body.linearVelocity = velocity;

        // 회전: 집어 들 때의 자세를 손을 따라 그대로 유지합니다.
        if (Held.lockRotationWhileHeld)
        {
            Quaternion target = holdPoint.rotation * heldRotationOffset;
            body.MoveRotation(Quaternion.Slerp(body.rotation, target, rotationSpeed * Time.fixedDeltaTime));
            body.angularVelocity = Vector3.zero;
        }
    }

    /// <summary>
    /// 들고 있던 물건을 놓아 물리 설정이 복원되지 않은 채 남지 않게 합니다.
    /// </summary>
    void OnDisable()
    {
        // 하차나 모드 전환으로 꺼질 때 들고 있던 것을 놓습니다.
        if (IsCarrying) Drop(false);
    }

    // --- Public Methods ---

    /// <summary>
    /// 물건을 듭니다.
    /// </summary>
    public void PickUp(Carryable target)
    {
        if (target == null || target.IsHeld) return;
        if (target.Body == null) return;

        if (target.Body.mass > maxCarryMass)
        {
            Debug.Log("PlayerCarrier: " + target.displayName + "은(는) 너무 무겁습니다.");
            return;
        }

        Held = target;

        // 집어 든 순간의 각도를 손 기준으로 기억해 둡니다.
        // 이렇게 해야 물건이 들리면서 제멋대로 정렬되지 않고, 놓여 있던 자세 그대로 딸려옵니다.
        heldRotationOffset = ResolveHeldRotation(target);

        target.OnPickedUp();
        IgnorePlayerCollision(target, true);

        Debug.Log("PlayerCarrier: " + target.displayName + "을(를) 들었습니다.");
    }

    /// <summary>
    /// 들고 있던 물건을 내려놓습니다.
    /// </summary>
    /// <param name="throwForward">앞으로 밀어낼지 여부. 자동으로 놓칠 때는 false입니다.</param>
    public void Drop(bool throwForward)
    {
        if (!IsCarrying) return;

        Carryable dropped = Held;
        Held = null;

        IgnorePlayerCollision(dropped, false);
        dropped.OnDropped();

        if (throwForward && throwImpulse > 0f && dropped.Body != null)
        {
            Transform aim = aimTransform != null ? aimTransform : transform;
            dropped.Body.AddForce(aim.forward * throwImpulse, ForceMode.VelocityChange);
        }

        Debug.Log("PlayerCarrier: " + dropped.displayName + "을(를) 내려놓았습니다.");
    }

    // --- Private Methods ---

    /// <summary>
    /// 들고 있는 동안 유지할 회전을 손 기준으로 계산합니다.
    /// </summary>
    /// <param name="target">집어 든 물건</param>
    /// <returns>
    /// 기본적으로 집어 든 순간의 각도(손 기준)입니다.
    /// alignToHoldPose를 켠 물건만 지정된 자세로 맞춥니다.
    /// </returns>
    private Quaternion ResolveHeldRotation(Carryable target)
    {
        if (target.alignToHoldPose) return Quaternion.Euler(target.holdEuler);
        if (holdPoint == null) return Quaternion.identity;

        // 손 기준으로 본 현재 각도. 손이 움직여도 이 관계가 유지됩니다.
        return Quaternion.Inverse(holdPoint.rotation) * target.Body.rotation;
    }

    /// <summary>
    /// 조준점에 걸린 물건을 갱신합니다. 들고 있는 동안에는 새 대상을 찾지 않습니다.
    /// </summary>
    private void UpdateTarget()
    {
        if (IsCarrying) { Target = null; return; }

        // 원근 카메라에서 카메라 정면 = 화면 중앙이므로 이 레이가 곧 조준점입니다.
        RaycastHit hit;
        bool didHit = Physics.Raycast(
            aimTransform.position,
            aimTransform.forward,
            out hit,
            pickupDistance,
            carryableLayers,
            QueryTriggerInteraction.Ignore
        );

        Target = didHit ? hit.collider.GetComponentInParent<Carryable>() : null;
    }

    /// <summary>
    /// 카메라 앞에 손 위치를 만듭니다.
    /// 카메라의 자식이므로 탑승·하차로 카메라가 옮겨가도 함께 따라갑니다.
    /// </summary>
    private void CreateHoldPoint()
    {
        GameObject go = new GameObject("HoldPoint");
        go.transform.SetParent(aimTransform, false);
        go.transform.localPosition = new Vector3(0f, holdHeightOffset, holdDistance);
        go.transform.localRotation = Quaternion.identity;
        holdPoint = go.transform;
    }

    /// <summary>
    /// 들고 있는 동안 플레이어 콜라이더와 부딪히지 않게 합니다.
    /// 그러지 않으면 물건이 몸에 끼어 덜덜 떨립니다.
    /// </summary>
    private void IgnorePlayerCollision(Carryable target, bool ignore)
    {
        if (playerCollider == null || target == null) return;

        if (ignore)
        {
            ignoredColliders.Clear();
            target.GetComponentsInChildren<Collider>(true, ignoredColliders);
        }

        for (int i = 0; i < ignoredColliders.Count; i++)
        {
            if (ignoredColliders[i] == null) continue;
            Physics.IgnoreCollision(ignoredColliders[i], playerCollider, ignore);
        }

        if (!ignore) ignoredColliders.Clear();
    }
}
