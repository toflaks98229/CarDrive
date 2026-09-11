using UnityEngine;
using CarDrive.Common;

namespace CarDrive.Gameplay
{
    /// <summary>
    /// 견인 갈고리입니다. 줄을 <b>풀고 감고</b>, 매달린 갈고리를 <b>흔듭니다.</b>
    ///
    /// <b>여섯 중 유일하게 걷는 내내 움직입니다.</b> 다른 다섯은 쏠 때만 움직이므로
    /// 대충 해도 티가 안 나지만, 이것은 로봇이 한 걸음 뗄 때마다 보입니다.
    /// <b>진자를 대충 하면 이 무장이 죽습니다.</b>
    ///
    /// <b>매달린 것은 늘 수직입니다.</b> 붐이 부앙으로 기울고 몸통이 흔들려도
    /// 갈고리는 세상의 아래를 봅니다. 그것을 그대로 넣지 않고 <b>2차 시스템으로
    /// 따라가게</b> 하면, 늦게 따라오는 그 지연이 곧 흔들림이 됩니다 — 흔들림을
    /// 따로 만들 필요가 없습니다.
    ///
    /// <b>드럼은 줄 길이에서 뽑습니다.</b> 따로 돌리면 <b>줄은 그대로인데 드럼만
    /// 도는</b> 순간이 반드시 생기고, 그것이 보이면 기계가 가짜가 됩니다.
    /// </summary>
    [DisallowMultipleComponent]
    public class WeaponWinch : MonoBehaviour
    {
        // --- Public Member Variables : 배선 ---

        /// <summary>줄을 감는 드럼입니다.</summary>
        [Header("배선")]
        [Tooltip("줄을 감는 드럼")]
        public Transform drum;

        /// <summary>매달린 갈고리입니다. 원점이 <b>줄이 걸리는 자리</b>여야 합니다.</summary>
        [Tooltip("매달린 갈고리. 원점이 줄이 걸리는 자리여야 합니다")]
        public Transform hook;

        // --- Public Member Variables : 줄 ---

        /// <summary>다 풀었을 때의 길이입니다(m).</summary>
        [Header("줄")]
        [Tooltip("다 풀었을 때의 길이(m)")]
        public float maxPayout = 1.8f;

        /// <summary>다 푸는 데 걸리는 시간입니다(초).</summary>
        [Tooltip("다 푸는 데 걸리는 시간(초)")]
        public float payoutTime = 2.5f;

        /// <summary>드럼의 반지름입니다(m). 회전각이 여기서 나옵니다.</summary>
        [Tooltip("드럼 반지름(m). 회전각이 여기서 나옵니다")]
        public float drumRadius = 0.54f;

        /// <summary>드럼의 회전축입니다. 조각의 로컬 좌표입니다.</summary>
        [Tooltip("드럼의 회전축 (조각의 로컬 좌표)")]
        public Vector3 drumAxis = Vector3.right;

        /// <summary>줄이 늘어나는 방향입니다. 갈고리의 부모 기준이고 기본은 아래입니다.</summary>
        [Tooltip("줄이 늘어나는 방향 (갈고리의 부모 기준)")]
        public Vector3 payoutAxis = Vector3.down;

        // --- Public Member Variables : 흔들림 ---

        /// <summary>
        /// 수직을 <b>얼마나 늦게</b> 따라가는지입니다. 늦는 만큼 흔들립니다.
        ///
        /// 진동수를 낮추면 크게 천천히, 감쇠비를 낮추면 오래 흔들립니다.
        /// 무거운 갈고리는 둘 다 낮습니다.
        /// </summary>
        [Header("흔들림")]
        [Tooltip("수직을 따라가는 성격. 늦는 만큼 흔들립니다")]
        public SecondOrderSettings sway = new SecondOrderSettings(0.9f, 0.22f, 0f);

        /// <summary>수직에서 벗어날 수 있는 최대 각입니다(도).</summary>
        [Tooltip("수직에서 벗어날 수 있는 최대 각(도)")]
        public float maxSwing = 25f;

        // --- Public Properties ---

        /// <summary>지금 풀린 줄 길이입니다(m).</summary>
        public float Payout { get { return payout; } }

        /// <summary>갈고리가 수직에서 벗어난 각입니다(도). 실측이 봅니다.</summary>
        public float SwingAngle { get; private set; }

        // --- Private Member Variables ---

        /// <summary>지금 풀린 길이입니다.</summary>
        private float payout;

        /// <summary>풀라는 요구가 들어와 있는지입니다.</summary>
        private bool lowering;

        /// <summary>갈고리가 매달린 자리입니다.</summary>
        private Vector3 hookHome;

        /// <summary>드럼이 지금까지 돈 각입니다.</summary>
        private float drumAngle;

        /// <summary>수직을 늦게 따라가는 방향입니다. 부모 기준입니다.</summary>
        private SecondOrderDynamics3 hang;

        /// <summary>배선이 온전한지입니다.</summary>
        private bool ready;

        // --- Public Methods ---

        /// <summary>줄을 풉니다.</summary>
        public void Lower()
        {
            lowering = true;
        }

        /// <summary>줄을 감습니다.</summary>
        public void Raise()
        {
            lowering = false;
        }

        /// <summary>한 프레임분을 진행합니다.</summary>
        /// <param name="dt">시간 간격(초)</param>
        public void Tick(float dt)
        {
            if (!ready || dt <= 0f) return;

            Spool(dt);
            Swing(dt);
        }

        // --- Private Methods ---

        /// <summary>줄을 풀거나 감고, 드럼을 <b>그 길이에서</b> 돌립니다.</summary>
        /// <param name="dt">시간 간격(초)</param>
        private void Spool(float dt)
        {
            float rate = maxPayout / Mathf.Max(payoutTime, 0.01f);
            float before = payout;

            payout = Mathf.Clamp(payout + (lowering ? rate : -rate) * dt, 0f, maxPayout);

            hook.localPosition = hookHome + payoutAxis.normalized * payout;

            if (drum == null || drumRadius <= 0.001f) return;

            // 줄 1 m 는 드럼 한 바퀴가 아니라 <c>1 / (2πr)</c> 바퀴입니다.
            drumAngle += (payout - before) * Mathf.Rad2Deg / drumRadius;
            drum.localRotation = Quaternion.AngleAxis(drumAngle, drumAxis.normalized);
        }

        /// <summary>갈고리를 수직 쪽으로 <b>늦게</b> 돌립니다.</summary>
        /// <param name="dt">시간 간격(초)</param>
        private void Swing(float dt)
        {
            // 세상의 아래를 갈고리의 부모 좌표로 옮깁니다. 붐이 기울수록 이 값이
            // 부모의 아래에서 멀어지고, 그 차이가 곧 갈고리가 돌아야 할 각입니다.
            Vector3 down = hook.parent != null
                ? hook.parent.InverseTransformDirection(Vector3.down)
                : Vector3.down;

            Vector3 aim = hang.Update(dt, down);
            if (aim.sqrMagnitude < 1e-6f) return;

            aim.Normalize();

            Vector3 rest = payoutAxis.normalized;
            float angle = Vector3.Angle(rest, aim);

            // ⚠ 각을 제한하지 않으면 부앙을 크게 내렸을 때 갈고리가 <b>붐을 뚫고</b>
            // 뒤로 넘어갑니다. 실제 줄은 그렇게 못 갑니다.
            if (angle > maxSwing)
            {
                aim = Vector3.RotateTowards(rest, aim, maxSwing * Mathf.Deg2Rad, 0f);
                angle = maxSwing;
            }

            SwingAngle = angle;
            hook.localRotation = Quaternion.FromToRotation(rest, aim);
        }

        // --- Unity Event Functions ---

        private void Awake()
        {
            ready = hook != null;

            if (!ready)
            {
                GameLog.Error(GameLog.Channel.Enemy,
                    name + ": 갈고리가 있어야 합니다. 이 윈치는 쉬어 갑니다.", this);
                return;
            }

            hookHome = hook.localPosition;
            hang = new SecondOrderDynamics3(sway, payoutAxis.normalized);
        }

        private void OnValidate()
        {
            maxPayout = Mathf.Max(0f, maxPayout);
            payoutTime = Mathf.Max(0.01f, payoutTime);
            drumRadius = Mathf.Max(0.01f, drumRadius);
            maxSwing = Mathf.Clamp(maxSwing, 0f, 89f);
        }
    }
}
