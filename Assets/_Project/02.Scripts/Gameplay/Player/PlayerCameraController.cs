using UnityEngine;

/// <summary>
/// [리팩토링됨]
/// 오직 마우스 입력을 받아 플레이어 카메라(상하)와 몸체(좌우)의
/// 회전을 처리하는 역할만 전담하는 클래스입니다.
/// 상호작용과 공격 로직은 PlayerInteractor와 PlayerAttacker로 분리되었습니다.
/// </summary>
public class PlayerCameraController : MonoBehaviour
{
    // --- Public Member Variables ---

    [Header("회전 설정")]
    [Tooltip("마우스 감도")]
    public float mouseSensitivity = 100f;

    [Tooltip("플레이어 몸체 Transform. 좌우 회전에 사용됩니다.")]
    public Transform playerBody;

    [Header("상하 회전 제한")]
    [Tooltip("카메라의 최소 상하 회전 각도 (아래쪽)")]
    public float minVerticalAngle = -90f;

    [Tooltip("카메라의 최대 상하 회전 각도 (위쪽)")]
    public float maxVerticalAngle = 90f;

    [Header("좌우 회전 제한")]
    [Tooltip("좌우 회전 제한 사용 여부")]
    public bool useHorizontalRotationLimit = false;

    [Tooltip("플레이어의 최소 좌우 회전 각도")]
    public float minHorizontalAngle = -90f;

    [Tooltip("플레이어의 최대 좌우 회전 각도")]
    public float maxHorizontalAngle = 90f;


    [Header("시작 처리")]
    [Tooltip("시작 직후 이 시간(초) 동안 마우스 입력을 무시합니다. " +
             "로딩이 끝나는 프레임에 쌓여 있던 마우스 이동이 한꺼번에 들어와 시점이 홱 돌아가는 것을 막습니다.")]
    public float startupIgnoreSeconds = 0.4f;

    [Tooltip("시간과 별개로 이 프레임 수만큼 더 무시합니다. " +
             "로딩 직후 첫 몇 프레임은 deltaTime이 비정상적으로 큽니다.")]
    public int startupIgnoreFrames = 5;

    [Tooltip("체크하면 시작할 때 시점을 수평 정면으로 맞춥니다.")]
    public bool faceForwardOnStart = true;

    [Tooltip("한 프레임에 돌 수 있는 최대 각도. 프레임이 크게 끊겼을 때 시점이 튀는 것을 막습니다. " +
             "0이면 제한하지 않습니다.")]
    public float maxDegreesPerFrame = 25f;

    // --- Public Properties ---

    /// <summary>지금 시작 안정화 중이라 마우스를 받지 않는 상태인지 여부입니다.</summary>
    public bool IsSettling { get { return settleSeconds > 0f || settleFrames > 0; } }

    // --- Private Member Variables ---

    /// <summary>위아래 시선 각도(도)입니다. 상하 제한 범위 안으로 유지됩니다.</summary>
    private float xRotation = 0f;

    /// <summary>좌우 시선 각도(도)입니다.</summary>
    private float currentYRotation = 0f;

    /// <summary>시작 안정화가 끝나기까지 남은 시간(초)입니다. 이 동안에는 마우스를 받지 않습니다.</summary>
    private float settleSeconds;

    /// <summary>시작 안정화가 끝나기까지 남은 프레임 수입니다.</summary>
    private int settleFrames;

    /// <summary>
    /// 스크립트가 처음 활성화될 때 마우스 커서 및 초기 회전 값을 설정합니다.
    /// </summary>
    void Start()
    {
        // 씬을 다시 불러왔을 때 이전 판의 막힘 상태가 남아 있을 수 있습니다.
        GameInputGate.Reset();
        GameInputGate.Changed += OnInputGateChanged;

        SetCursorLocked(true);

        if (playerBody != null)
        {
            currentYRotation = playerBody.eulerAngles.y;
        }
        xRotation = transform.localEulerAngles.x;

        // 시작할 때는 위아래로 기울지 않은 정면을 봅니다.
        if (faceForwardOnStart)
        {
            xRotation = 0f;
            transform.localRotation = Quaternion.Euler(0f, 0f, 0f);
        }

        BeginSettle(startupIgnoreSeconds, startupIgnoreFrames);
    }

    /// <summary>
    /// 지정한 시간·프레임 동안 마우스 입력을 무시합니다.
    /// 로딩 화면이나 컷신이 끝난 직후처럼 시점이 튀면 곤란한 순간에 호출하세요.
    /// </summary>
    public void BeginSettle(float seconds, int frames)
    {
        settleSeconds = Mathf.Max(settleSeconds, seconds);
        settleFrames = Mathf.Max(settleFrames, frames);
    }

    /// <summary>
    /// 입력 막기 알림 구독을 해제합니다. 두지 않으면 파괴된 뒤에도 호출되어 오류가 납니다.
    /// </summary>
    void OnDestroy()
    {
        GameInputGate.Changed -= OnInputGateChanged;
    }

    /// <summary>
    /// 오버레이 등이 입력을 막으면 커서를 풀어 버튼을 누를 수 있게 하고,
    /// 풀리면 다시 잠급니다.
    /// </summary>
    private void OnInputGateChanged(bool suspended)
    {
        SetCursorLocked(!suspended);
    }

    /// <summary>커서 잠금과 표시를 함께 바꿉니다.</summary>
    public void SetCursorLocked(bool locked)
    {
        Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !locked;
    }

    /// <summary>
    /// 매 프레임마다 호출되어 마우스 회전을 처리합니다.
    /// </summary>
    void Update()
    {
        // 오버레이가 떠 있으면 마우스로 시점이 돌지 않게 합니다.
        if (GameInputGate.Suspended) return;

        // 시작(또는 로딩) 직후에는 마우스를 받지 않고 정면을 유지합니다.
        if (IsSettling)
        {
            TickSettle();
            return;
        }

        HandleMouseLook();
    }

    /// <summary>
    /// 안정화 시간을 줄이면서, 그동안 쌓인 마우스 이동을 읽어서 버립니다.
    /// 읽지 않으면 안정화가 끝나는 순간 누적분이 한꺼번에 적용되어 시점이 홱 돌아갑니다.
    /// </summary>
    private void TickSettle()
    {
        Input.GetAxis("Mouse X");
        Input.GetAxis("Mouse Y");

        // Time.timeScale이 0이어도 진행되도록 unscaled를 씁니다.
        if (settleSeconds > 0f) settleSeconds -= Time.unscaledDeltaTime;
        if (settleFrames > 0) settleFrames--;
    }

    // --- Public Methods ---

    /// <summary>
    /// 좌우 회전에 쓸 몸체를 바꿉니다.
    /// 탑승/하차로 카메라가 옮겨갈 때 호출하며, 현재 각도를 다시 읽어 시점이 튀지 않게 합니다.
    /// </summary>
    public void SetPlayerBody(Transform body)
    {
        playerBody = body;

        if (body != null)
        {
            currentYRotation = body.localEulerAngles.y;
            // eulerAngles는 0~360으로 나오므로 회전 제한과 비교하기 좋게 -180~180으로 맞춥니다.
            if (currentYRotation > 180f) currentYRotation -= 360f;
        }
        else
        {
            currentYRotation = 0f;
        }

        // 카메라가 다른 부모로 옮겨간 프레임에는 마우스를 받지 않습니다.
        // 재부모화가 일어난 그 한 프레임의 입력이 시점을 튀게 만들 수 있습니다.
        BeginSettle(0f, 2);
    }

    /// <summary>
    /// 마우스 입력을 받아 카메라(상하) 및 플레이어 몸체(좌우) 회전을 처리합니다.
    /// (원본 PlayerCameraController의 메서드)
    /// </summary>
    private void HandleMouseLook()
    {
        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity * Time.deltaTime;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity * Time.deltaTime;

        // 프레임이 크게 끊기면 deltaTime이 커져 한 번에 크게 돌아갑니다.
        // 로딩 직후나 순간적인 끊김에서 시점이 튀지 않도록 회전량을 잘라 둡니다.
        if (maxDegreesPerFrame > 0f)
        {
            mouseX = Mathf.Clamp(mouseX, -maxDegreesPerFrame, maxDegreesPerFrame);
            mouseY = Mathf.Clamp(mouseY, -maxDegreesPerFrame, maxDegreesPerFrame);
        }

        // 상하 회전 (Pitch)
        xRotation -= mouseY;
        xRotation = Mathf.Clamp(xRotation, minVerticalAngle, maxVerticalAngle);
        transform.localRotation = Quaternion.Euler(xRotation, 0f, 0f);

        // 좌우 회전 (Yaw)
        if (playerBody != null)
        {
            currentYRotation += mouseX;
            if (useHorizontalRotationLimit)
            {
                currentYRotation = Mathf.Clamp(currentYRotation, minHorizontalAngle, maxHorizontalAngle);
            }
            playerBody.localRotation = Quaternion.Euler(0f, currentYRotation, 0f);
        }
    }
}
