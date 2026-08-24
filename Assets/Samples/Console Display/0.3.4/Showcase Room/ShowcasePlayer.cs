using UnityEngine;

namespace ConsoleDisplay.Showcase
{
    /// <summary>
    /// 데모 방을 걸어 다니는 1인칭 이동입니다. 보여 주기에 필요한 만큼만 있습니다.
    ///
    /// <b>중력을 직접 다룹니다.</b> <see cref="CharacterController"/>는 바닥에 붙어 있어도
    /// 아주 작은 아래 방향 속도를 계속 넣어 주지 않으면 <c>isGrounded</c>가 깜빡입니다.
    /// 그러면 걸을 때 화면이 미세하게 떨립니다.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public sealed class ShowcasePlayer : MonoBehaviour
    {
        // --- Public Member Variables ---

        [Header("이동")]
        [Tooltip("걷는 속도(m/s)입니다.")]
        [SerializeField, Min(0.5f)] private float moveSpeed = 4.5f;

        [Tooltip("마우스 감도입니다.")]
        [SerializeField, Min(0.1f)] private float lookSensitivity = 2.2f;

        [Tooltip("위아래로 볼 수 있는 최대 각도입니다.")]
        [SerializeField, Range(30f, 89f)] private float pitchLimit = 80f;

        [Header("참조")]
        [Tooltip("시점 카메라입니다. 비워 두면 자식에서 찾습니다.")]
        [SerializeField] private Camera view;

        [Header("진단")]
        [Tooltip("포커스와 마우스 잠금이 언제 어떻게 바뀌는지 콘솔에 남깁니다. 조작이 이상할 때 켭니다.")]
        [SerializeField] private bool logFocusChanges = true;

        // --- Private Member Variables ---

        private CharacterController body;
        private float pitch;
        private float verticalSpeed;
        private bool looking;

        /// <summary>같은 내용을 거듭 남기지 않으려고 마지막으로 남긴 줄을 들고 있습니다.</summary>
        private string lastReport;

        /// <summary>지난 프레임의 잠금 상태입니다. 바뀔 때만 남기려고 봅니다.</summary>
        private CursorLockMode lastLockState;

        // --- Unity Event Functions ---

        private void Awake()
        {
            body = GetComponent<CharacterController>();

            if (view == null)
            {
                view = GetComponentInChildren<Camera>();
            }
        }

        private void Start()
        {
            SetLooking(true);
        }

        private void OnDisable()
        {
            SetLooking(false);
        }

        /// <summary>
        /// 창을 오갈 때 마우스 잠금을 되살립니다.
        ///
        /// 콘솔 창이 뜨면 포커스가 그쪽으로 가고, <b>유니티는 포커스를 잃을 때 마우스 잠금을
        /// 스스로 풀어 버립니다.</b> 게임으로 돌아와도 잠금이 돌아오지 않아서, 화면을 클릭할
        /// 때까지 시점이 돌아가지 않습니다. 돌아온 순간 원래 상태로 되돌립니다.
        /// </summary>
        private void OnApplicationFocus(bool focused)
        {
            Report(focused ? "focus gained" : "focus lost");

            if (!focused || !looking)
            {
                return;
            }

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

            Report("relock on focus");
        }

        private void Update()
        {
            // Esc로 마우스를 풀어 줍니다. 이게 없으면 에디터에서 플레이를 멈추기가 성가십니다.
            if (ShowcaseInput.CancelPressed)
            {
                SetLooking(!looking);
            }

            // 유니티가 스스로 잠금을 풀거나 되잡는 순간도 잡아 둡니다.
            if (Cursor.lockState != lastLockState)
            {
                lastLockState = Cursor.lockState;
                Report("lockState changed");
            }

            RecoverCursorLock();

            // 잠금이 풀린 채로 시점을 돌리면, 바탕화면에서 마우스를 움직이는 것만으로
            // 화면이 빙빙 돕니다. 실제로 잡혀 있을 때만 돌립니다.
            if (looking && Cursor.lockState == CursorLockMode.Locked)
            {
                Look();
            }

            Move();
        }

        /// <summary>
        /// 풀려 버린 마우스 잠금을 클릭 한 번으로 되잡습니다.
        ///
        /// <b>왜 포커스 이벤트만으로는 부족한가.</b> <c>OnApplicationFocus</c>는 포커스가
        /// <b>바뀌는 순간에만</b> 불립니다. 이미 게임에 포커스가 있는 상태에서 잠금만 풀리면
        /// 아무리 클릭해도 그 이벤트가 오지 않아 영영 되돌아오지 못합니다.
        /// 그래서 <b>매 프레임 상태를 보고</b>, 어긋나 있으면 클릭으로 되잡게 합니다.
        ///
        /// 저절로 되잡지 않고 클릭을 기다리는 이유는, 에디터에서 인스펙터를 만지는 중에
        /// 마우스를 빼앗기면 곤란하기 때문입니다.
        /// </summary>
        private void RecoverCursorLock()
        {
            if (!looking || Cursor.lockState == CursorLockMode.Locked)
            {
                return;
            }

            if (ShowcaseInput.PointerPressed)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;

                Report("relock on click");
            }
        }

        /// <summary>
        /// 지금 포커스와 마우스 잠금이 어떤 상태인지 한 줄로 남깁니다.
        ///
        /// <b>왜 이게 필요한가.</b> 마우스 잠금은 유니티가 스스로 풀기도 하고 되잡기도 합니다.
        /// 그 시점을 밖에서 볼 방법이 없어서, 조작이 이상할 때 <b>누가 언제 풀었는지</b>
        /// 알아낼 수가 없습니다. 상태가 바뀔 때만 남기므로 콘솔을 덮지 않습니다.
        /// </summary>
        private void Report(string what)
        {
            if (!logFocusChanges)
            {
                return;
            }

            string state = what + "  |  looking=" + looking +
                           "  lockState=" + Cursor.lockState +
                           "  visible=" + Cursor.visible +
                           "  appFocused=" + Application.isFocused;

            if (state == lastReport)
            {
                return;
            }

            lastReport = state;
            Debug.Log("[Showcase] " + state);
        }

        // --- Private Methods ---

        /// <summary>마우스로 시점을 돌립니다.</summary>
        private void Look()
        {
            Vector2 delta = ShowcaseInput.Look * lookSensitivity;

            transform.Rotate(0f, delta.x, 0f, Space.Self);

            pitch = Mathf.Clamp(pitch - delta.y, -pitchLimit, pitchLimit);
            if (view != null)
            {
                view.transform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
            }
        }

        /// <summary>바닥을 따라 걷습니다.</summary>
        private void Move()
        {
            Vector2 input = Vector2.ClampMagnitude(ShowcaseInput.Move, 1f);
            Vector3 wish = (transform.right * input.x) + (transform.forward * input.y);

            // 바닥에 붙어 있어도 작은 아래 속도를 유지합니다. 0으로 두면 isGrounded가 깜빡입니다.
            verticalSpeed = body.isGrounded ? -2f : verticalSpeed + (Physics.gravity.y * Time.deltaTime);

            Vector3 velocity = (wish * moveSpeed) + (Vector3.up * verticalSpeed);
            body.Move(velocity * Time.deltaTime);
        }

        /// <summary>마우스를 잠그거나 풀어 줍니다.</summary>
        private void SetLooking(bool enable)
        {
            looking = enable;
            Cursor.lockState = enable ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !enable;
        }
    }
}
