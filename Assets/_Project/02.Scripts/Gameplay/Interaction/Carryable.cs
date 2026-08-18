using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 마우스 좌클릭으로 들 수 있는 물건에 붙입니다.
///
/// 들고 있는 동안에도 Rigidbody를 살려 두고 속도로 따라오게 하므로,
/// 벽을 뚫고 지나가지 않고 문틀에 걸리면 자연스럽게 막힙니다.
/// 실제 이동은 PlayerCarrier가 처리하고, 이 컴포넌트는 상태와 물리 설정 복원을 담당합니다.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class Carryable : MonoBehaviour
{
    // --- Public Member Variables ---

    /// <summary>조준했을 때 안내 문구에 쓸 이름입니다.</summary>
    [Header("표시")]
    [Tooltip("조준했을 때 안내에 쓸 이름")]
    public string displayName = "물건";

    /// <summary>
    /// 체크하면 들어 올릴 때 <see cref="holdEuler"/> 각도로 맞춥니다.
    /// 꺼 두면 집어 든 순간의 각도를 그대로 유지합니다. (기본값)
    /// </summary>
    [Header("들었을 때의 자세")]
    [Tooltip("체크하면 들어 올릴 때 아래 각도로 맞춥니다. " +
             "꺼 두면 집어 든 순간의 각도를 그대로 유지합니다.")]
    public bool alignToHoldPose = false;

    /// <summary>
    /// 들어 올릴 때 맞출 회전(도)입니다.
    /// <see cref="alignToHoldPose"/>를 켰을 때만 쓰입니다.
    /// </summary>
    [Tooltip("들어 올릴 때 맞출 회전(도). 위 항목을 켰을 때만 쓰입니다.")]
    public Vector3 holdEuler = Vector3.zero;

    /// <summary>들고 있는 동안 회전을 고정할지 여부입니다.</summary>
    [Tooltip("체크하면 들고 있는 동안 회전을 고정합니다.")]
    public bool lockRotationWhileHeld = true;

    /// <summary>들고 있을 때의 이동 감쇠입니다. 클수록 덜 흔들립니다.</summary>
    [Header("물리 (들고 있는 동안)")]
    [Tooltip("들고 있을 때의 이동 감쇠. 클수록 덜 흔들립니다.")]
    public float heldLinearDamping = 12f;

    /// <summary>들고 있을 때의 회전 감쇠입니다.</summary>
    [Tooltip("들고 있을 때의 회전 감쇠")]
    public float heldAngularDamping = 12f;

    /// <summary>물건을 집어 든 순간 한 번 호출됩니다.</summary>
    [Header("이벤트")]
    public UnityEvent onPickedUp;

    /// <summary>물건을 내려놓은 순간 한 번 호출됩니다.</summary>
    public UnityEvent onDropped;

    // --- Public Properties ---

    /// <summary>지금 들려 있는지 여부입니다.</summary>
    public bool IsHeld { get; private set; }

    /// <summary>이 물건의 Rigidbody입니다.</summary>
    public Rigidbody Body { get { return body; } }

    // --- Private Member Variables ---

    /// <summary>이 물건의 Rigidbody입니다. 들고 있는 동안에도 살려 두고 속도로 따라오게 합니다.</summary>
    private Rigidbody body;

    // 들기 전 물리 설정을 기억해 두었다가 내려놓을 때 되돌립니다.
    private bool cachedUseGravity;
    private float cachedLinearDamping;
    private float cachedAngularDamping;
    private RigidbodyInterpolation cachedInterpolation;
    private CollisionDetectionMode cachedCollisionMode;
    private bool cached;

    // --- Unity Event Functions ---

    /// <summary>
    /// 들고 다닐 때 조작할 Rigidbody 참조를 가져옵니다.
    /// </summary>
    void Awake()
    {
        body = GetComponent<Rigidbody>();
    }

    // --- Public Methods ---

    /// <summary>
    /// 들리기 시작할 때 PlayerCarrier가 호출합니다.
    /// </summary>
    public void OnPickedUp()
    {
        if (IsHeld) return;
        IsHeld = true;

        if (!cached)
        {
            cachedUseGravity = body.useGravity;
            cachedLinearDamping = body.linearDamping;
            cachedAngularDamping = body.angularDamping;
            cachedInterpolation = body.interpolation;
            cachedCollisionMode = body.collisionDetectionMode;
            cached = true;
        }

        body.useGravity = false;
        body.linearDamping = heldLinearDamping;
        body.angularDamping = heldAngularDamping;
        body.interpolation = RigidbodyInterpolation.Interpolate;

        // 빠르게 움직이는 동안 벽을 통과하지 않도록 합니다.
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

        if (onPickedUp != null) onPickedUp.Invoke();
    }

    /// <summary>
    /// 내려놓을 때 PlayerCarrier가 호출합니다.
    /// </summary>
    public void OnDropped()
    {
        if (!IsHeld) return;
        IsHeld = false;

        if (cached)
        {
            body.useGravity = cachedUseGravity;
            body.linearDamping = cachedLinearDamping;
            body.angularDamping = cachedAngularDamping;
            body.interpolation = cachedInterpolation;
            body.collisionDetectionMode = cachedCollisionMode;
        }

        if (onDropped != null) onDropped.Invoke();
    }
}
