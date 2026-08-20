using UnityEngine;
using CarDrive.Common;

namespace CarDrive.Gameplay
{
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

            SeedYaw(playerBody);
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
            //
            // <b>여기는 Suspended를 직접 봅니다.</b> 축이 0이 되는 것만으로는 부족합니다.
            // 아래 안정화(TickSettle)까지 함께 멈춰야, 오버레이를 닫은 뒤에 남은 안정화 프레임이
            // 그대로 이어집니다.
            if (GameInput.Suspended) return;

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
            _ = GameInput.LookX;
            _ = GameInput.LookY;

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

            SeedYaw(body);

            // 카메라가 다른 부모로 옮겨간 프레임에는 마우스를 받지 않습니다.
            // 재부모화가 일어난 그 한 프레임의 입력이 시점을 튀게 만들 수 있습니다.
            BeginSettle(0f, 2);
        }

        /// <summary>
        /// 좌우 회전 값을 지금 몸체의 각도에서 다시 읽어 옵니다.
        ///
        /// <b>읽는 곳과 쓰는 곳이 같아야 합니다.</b>
        /// 회전은 아래에서 localRotation 으로 쓰는데, 예전에는 시작할 때만 전역 각도(eulerAngles)를
        /// 읽었습니다. 몸체의 부모가 돌아가 있으면 두 값이 달라서, 마우스를 처음 움직이는 순간
        /// 그 차이만큼 시점이 한 번에 튀었습니다.
        ///
        /// 0~360 을 -180~180 으로 맞추는 것도 여기서 함께 합니다.
        /// 이것을 빠뜨리면 좌우 제한이 켜져 있을 때(운전 중) 350도 같은 값이 들어오고,
        /// 제한이 그 값을 한 프레임에 잘라내며 시점이 홱 돌아갑니다.
        ///
        /// 같은 일을 두 곳에 따로 적어 두었던 것이 문제의 뿌리라, 한 곳으로 모았습니다.
        /// </summary>
        /// <param name="body">좌우 회전에 쓸 몸체. 없으면 0으로 둡니다.</param>
        private void SeedYaw(Transform body)
        {
            if (body == null)
            {
                currentYRotation = 0f;
                return;
            }

            currentYRotation = body.localEulerAngles.y;
            if (currentYRotation > 180f) currentYRotation -= 360f;
        }

        /// <summary>
        /// 마우스 입력을 받아 카메라(상하) 및 플레이어 몸체(좌우) 회전을 처리합니다.
        /// (원본 PlayerCameraController의 메서드)
        /// </summary>
        private void HandleMouseLook()
        {
            // <b>마우스 입력에 deltaTime 을 곱하면 안 됩니다.</b>
            //
            // GameInput.LookX 가 돌려주는 값은 속도가 아니라 <b>이번 프레임에 마우스가 움직인 양</b>입니다.
            // 이미 프레임당 값인데 거기에 프레임 시간을 또 곱하면, 화면이 끊긴 순간
            // 같은 손놀림이 몇 배로 커집니다. 100ms 끊긴 프레임은 16ms 프레임의 여섯 배가 됩니다.
            // 지형을 불러오느라 한 번 끊길 때마다 시점이 홱 도는 것이 이 때문이었습니다.
            //
            // ReferenceFrame 을 곱하는 것은 <b>지금까지 맞춰 둔 감도를 그대로 쓰기 위해서</b>입니다.
            // 그냥 빼 버리면 감도가 예순 배가 됩니다.
            const float ReferenceFrame = 1f / 60f;

            float mouseX = GameInput.LookX * mouseSensitivity * ReferenceFrame;
            float mouseY = GameInput.LookY * mouseSensitivity * ReferenceFrame;

            // 그래도 한 번에 크게 도는 것은 막아 둡니다.
            // 위 계산으로 원인은 사라졌지만, 마우스를 아주 빠르게 휘둘렀을 때를 위한 안전장치입니다.
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
}
