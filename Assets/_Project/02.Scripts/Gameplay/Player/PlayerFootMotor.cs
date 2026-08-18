using UnityEngine;

/// <summary>
/// 하차 상태에서 WASD로 걸어 다니는 이동을 담당합니다.
/// 회전은 기존 PlayerCameraController가 처리하므로 여기서는 이동만 다룹니다.
/// (이 컴포넌트가 붙은 오브젝트가 PlayerCameraController의 playerBody가 됩니다)
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class PlayerFootMotor : MonoBehaviour
{
    // --- Public Member Variables ---

    [Header("이동 속도")]
    [Tooltip("걷기 속도 (m/s)")]
    public float walkSpeed = 3.2f;

    [Tooltip("달리기 속도 (m/s)")]
    public float sprintSpeed = 6.0f;

    [Tooltip("목표 속도에 도달하는 가속도. 클수록 즉각적으로 반응합니다.")]
    public float acceleration = 14f;

    [Header("점프 / 중력")]
    [Tooltip("점프 높이 (m). 0이면 점프하지 않습니다.")]
    public float jumpHeight = 1.0f;

    [Tooltip("중력 가속도 (음수)")]
    public float gravity = -20f;

    [Tooltip("접지 상태에서 유지할 아래 방향 속도. 경사면에서 뜨는 것을 막습니다.")]
    public float groundedStickVelocity = -2f;

    [Header("입력 키")]
    [Tooltip("달리기 키")]
    public KeyCode sprintKey = KeyCode.LeftShift;

    [Tooltip("점프 키")]
    public KeyCode jumpKey = KeyCode.Space;

    [Tooltip("앉기 키")]
    public KeyCode crouchKey = KeyCode.LeftControl;

    [Header("앉기")]
    [Tooltip("체크하면 누를 때마다 앉기/서기가 토글됩니다. 끄면 누르고 있는 동안만 앉습니다.")]
    public bool toggleCrouch = false;

    [Tooltip("앉았을 때의 CharacterController 높이")]
    public float crouchHeight = 1.05f;

    [Tooltip("앉았을 때의 눈높이 (Head의 로컬 Y)")]
    public float crouchEyeHeight = 0.85f;

    [Tooltip("앉아서 걷는 속도")]
    public float crouchSpeed = 1.5f;

    [Tooltip("앉고 서는 전환 속도. 클수록 빠릿합니다.")]
    public float crouchTransitionSpeed = 9f;

    [Tooltip("눈높이를 조절할 Head. 비워두면 자식에서 이름으로 찾습니다.")]
    public Transform headTransform;

    [Tooltip("흔들림 기준 위치를 함께 갱신할 컴포넌트. 비워두면 Head에서 찾습니다.")]
    public PlayerCameraShake cameraShake;

    [Tooltip("천장 판정에 쓸 레이어. 여기 걸리면 일어서지 못합니다.")]
    public LayerMask ceilingMask = ~0;

    [Header("니즈 연동")]
    [Tooltip("달리는 동안 초당 오르는 피로 (NeedsSystem이 없으면 무시됩니다)")]
    public float sprintFatiguePerSecond = 0.005f;

    [Tooltip("달리는 동안 초당 오르는 더러움")]
    public float sprintHygienePerSecond = 0.0015f;

    [Tooltip("피로가 이 값을 넘으면 달릴 수 없습니다. 1보다 크면 제한 없음")]
    public float sprintFatigueLimit = 0.9f;

    // --- Public Properties ---

    /// <summary>현재 수평 이동 속력입니다. (발소리·흔들림 연출에 쓸 수 있습니다)</summary>
    public float CurrentSpeed { get { return horizontalVelocity.magnitude; } }

    /// <summary>지금 달리고 있는지 여부입니다.</summary>
    public bool IsSprinting { get; private set; }

    /// <summary>지금 앉아 있는지 여부입니다.</summary>
    public bool IsCrouching { get; private set; }

    /// <summary>천장에 막혀 일어서지 못하는 상태인지 여부입니다.</summary>
    public bool BlockedByCeiling { get; private set; }

    /// <summary>땅에 닿아 있는지 여부입니다.</summary>
    public bool IsGrounded { get { return controller != null && controller.isGrounded; } }

    // --- Private Member Variables ---

    /// <summary>실제 이동을 수행하는 CharacterController입니다.</summary>
    private CharacterController controller;

    /// <summary>지금 적용 중인 수평 이동 속도입니다. 가감속으로 목표 속도를 따라갑니다.</summary>
    private Vector3 horizontalVelocity;

    /// <summary>지금 적용 중인 수직 속도입니다. 중력과 점프에 쓰입니다.</summary>
    private float verticalVelocity;

    // 서 있을 때의 자세는 씬에 설정된 값을 그대로 기준으로 삼습니다.
    private float standHeight;
    private Vector3 standCenter;
    private float standEyeHeight;
    private bool crouchInput;

    // --- Unity Event Functions ---

    /// <summary>
    /// CharacterController와 머리 Transform을 찾아 서 있을 때의 자세(높이·중심·눈높이)를 기억해 둡니다.
    /// </summary>
    void Awake()
    {
        controller = GetComponent<CharacterController>();

        standHeight = controller.height;
        standCenter = controller.center;

        if (headTransform == null)
        {
            Transform head = transform.Find("Head");
            if (head != null) headTransform = head;
        }
        if (headTransform != null)
        {
            standEyeHeight = headTransform.localPosition.y;
            if (cameraShake == null) cameraShake = headTransform.GetComponent<PlayerCameraShake>();
        }
    }

    /// <summary>
    /// 남아 있던 속도와 달리기 상태를 비웁니다. (차에 탈 때 이 리그가 통째로 꺼집니다)
    /// </summary>
    void OnDisable()
    {
        // 다시 켰을 때 이전 속도가 남아 튀어나가지 않도록 정리합니다.
        horizontalVelocity = Vector3.zero;
        verticalVelocity = 0f;
        IsSprinting = false;
    }

    /// <summary>
    /// 한 프레임의 도보 이동을 처리합니다. 앉기 자세를 먼저 반영한 뒤 이동을 계산합니다.
    /// </summary>
    void Update()
    {
        if (controller == null || !controller.enabled) return;

        HandleCrouch();
        HandleMovement();
        HandleGravityAndJump();

        Vector3 motion = horizontalVelocity + Vector3.up * verticalVelocity;
        controller.Move(motion * Time.deltaTime);

        ApplyNeedsCost();
    }

    // --- Private Methods ---

    /// <summary>
    /// WASD 입력을 받아 수평 속도를 갱신합니다.
    /// 방향은 이 오브젝트의 정면 기준이므로, 마우스로 몸이 돌면 이동 방향도 함께 돕니다.
    /// </summary>
    private void HandleMovement()
    {
        // 오버레이가 떠 있으면 걷지 않고 그 자리에 멈춥니다.
        float inputX = GameInputGate.Suspended ? 0f : Input.GetAxisRaw("Horizontal");
        float inputZ = GameInputGate.Suspended ? 0f : Input.GetAxisRaw("Vertical");

        Vector3 wish = transform.right * inputX + transform.forward * inputZ;
        if (wish.sqrMagnitude > 1f) wish.Normalize();

        // 앉은 상태에서는 달릴 수 없습니다.
        IsSprinting = !IsCrouching && Input.GetKey(sprintKey) && wish.sqrMagnitude > 0.01f && CanSprint();

        float targetSpeed = IsCrouching ? crouchSpeed : (IsSprinting ? sprintSpeed : walkSpeed);
        Vector3 targetVelocity = wish * targetSpeed;

        horizontalVelocity = Vector3.MoveTowards(
            horizontalVelocity,
            targetVelocity,
            acceleration * Time.deltaTime
        );
    }

    /// <summary>
    /// 앉기 입력을 읽고 실제 자세를 서서히 바꿉니다.
    /// 천장에 막혀 있으면 키를 놓아도 일어서지 않습니다.
    /// </summary>
    private void HandleCrouch()
    {
        // 1. 입력 (오버레이가 떠 있으면 지금 자세를 그대로 유지합니다)
        if (!GameInputGate.Suspended)
        {
            if (toggleCrouch)
            {
                if (Input.GetKeyDown(crouchKey)) crouchInput = !crouchInput;
            }
            else
            {
                crouchInput = Input.GetKey(crouchKey);
            }
        }

        // 2. 일어설 공간이 있는지 확인
        BlockedByCeiling = !crouchInput && IsCrouching && HasCeiling();
        IsCrouching = crouchInput || BlockedByCeiling;

        // 3. 콜라이더 높이를 목표까지 서서히 바꿉니다.
        float targetHeight = IsCrouching ? crouchHeight : standHeight;
        float height = Mathf.MoveTowards(controller.height, targetHeight,
            Mathf.Abs(standHeight - crouchHeight) * crouchTransitionSpeed * Time.deltaTime);

        // 발이 바닥에 붙어 있도록 중심을 함께 내립니다.
        controller.height = height;
        controller.center = standCenter + Vector3.up * ((height - standHeight) * 0.5f);

        // 4. 눈높이도 같은 비율로 내립니다.
        if (headTransform == null) return;

        float t = Mathf.InverseLerp(crouchHeight, standHeight, height);
        float eye = Mathf.Lerp(crouchEyeHeight, standEyeHeight, t);

        Vector3 local = headTransform.localPosition;
        local.y = eye;
        headTransform.localPosition = local;

        // 흔들림이 이전 높이로 되돌리지 않도록 기준을 함께 옮겨 줍니다.
        if (cameraShake != null)
        {
            Vector3 rest = cameraShake.RestLocalPosition;
            rest.y = eye;
            cameraShake.RestLocalPosition = rest;
        }
    }

    /// <summary>
    /// 서 있는 높이만큼의 공간이 비어 있는지 확인합니다.
    /// 자기 자신의 콜라이더는 제외합니다.
    /// </summary>
    private bool HasCeiling()
    {
        float radius = Mathf.Max(0.05f, controller.radius * 0.95f);

        // 서 있을 때 차지하게 될 캡슐
        Vector3 bottom = transform.position + Vector3.up * (radius + 0.05f);
        Vector3 top = transform.position + Vector3.up * Mathf.Max(standHeight - radius, radius + 0.06f);

        Collider[] hits = Physics.OverlapCapsule(bottom, top, radius, ceilingMask, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < hits.Length; i++)
        {
            if (hits[i] == null) continue;
            if (hits[i] == controller) continue;
            if (hits[i].transform.IsChildOf(transform)) continue;

            return true;
        }

        return false;
    }

    /// <summary>
    /// 피로가 한계를 넘으면 달릴 수 없습니다.
    /// </summary>
    private bool CanSprint()
    {
        if (sprintFatigueLimit > 1f) return true;
        if (NeedsSystem.Instance == null) return true;

        return NeedsSystem.Instance.GetValue(NeedType.Fatigue) < sprintFatigueLimit;
    }

    /// <summary>
    /// 중력과 점프를 처리합니다.
    /// </summary>
    private void HandleGravityAndJump()
    {
        if (controller.isGrounded)
        {
            // 접지 상태에서는 아래로 살짝 눌러 두어야 경사면에서 덜컹거리지 않습니다.
            if (verticalVelocity < 0f) verticalVelocity = groundedStickVelocity;

            if (jumpHeight > 0f && !GameInputGate.Suspended && Input.GetKeyDown(jumpKey))
            {
                // v = sqrt(2 * g * h)
                verticalVelocity = Mathf.Sqrt(2f * Mathf.Abs(gravity) * jumpHeight);
            }
        }
        else
        {
            verticalVelocity += gravity * Time.deltaTime;
        }
    }

    /// <summary>
    /// 달리는 동안 피로와 더러움이 오릅니다.
    /// </summary>
    private void ApplyNeedsCost()
    {
        if (!IsSprinting) return;

        NeedsSystem.Report(NeedType.Fatigue, sprintFatiguePerSecond * Time.deltaTime);
        NeedsSystem.Report(NeedType.Hygiene, sprintHygienePerSecond * Time.deltaTime);
    }
}
