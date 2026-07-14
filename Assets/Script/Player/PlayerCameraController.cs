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


    // --- Private Member Variables ---
    private float xRotation = 0f;
    private float currentYRotation = 0f;

    /// <summary>
    /// 스크립트가 처음 활성화될 때 마우스 커서 및 초기 회전 값을 설정합니다.
    /// </summary>
    void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        if (playerBody != null)
        {
            currentYRotation = playerBody.eulerAngles.y;
        }
        xRotation = transform.localEulerAngles.x;
    }

    /// <summary>
    /// 매 프레임마다 호출되어 마우스 회전을 처리합니다.
    /// </summary>
    void Update()
    {
        HandleMouseLook();
    }

    /// <summary>
    /// 마우스 입력을 받아 카메라(상하) 및 플레이어 몸체(좌우) 회전을 처리합니다.
    /// (원본 PlayerCameraController의 메서드)
    /// </summary>
    private void HandleMouseLook()
    {
        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity * Time.deltaTime;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity * Time.deltaTime;

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
