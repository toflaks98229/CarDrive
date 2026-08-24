using UnityEngine;
using CarDrive.Common;

namespace CarDrive.Gameplay
{
    /// <summary>로봇이 넘어지고 다시 일어나는 네 단계입니다.</summary>
    public enum RobotKnockdownState
    {
        /// <summary>서 있습니다. 평소대로 걷습니다.</summary>
        Standing,

        /// <summary>쓰러지는 중입니다. 힘을 걸지 않고 중력과 충돌에만 맡깁니다.</summary>
        Falling,

        /// <summary>바닥에 멈춰 있습니다. 잠깐 뜸을 들입니다.</summary>
        Downed,

        /// <summary>일어나는 중입니다. 몸을 세우고 발을 모읍니다.</summary>
        Rising
    }

    /// <summary>
    /// 세게 부딪히면 <b>넘어지고</b>, 멈추면 <b>다시 일어나는</b> 절차입니다.
    ///
    /// <b>넘어지는 것은 물리에 맡깁니다.</b> 루트의 회전 잠금을 풀고 넘어뜨리는 회전 충격을 한 번 준 뒤,
    /// 힘을 전부 끊고 손을 뗍니다. 그러면 몸통 상자가 지형과 부딪히며 <b>스스로 구르고 멈춥니다.</b>
    /// 넘어지는 자세를 미리 만들어 둘 필요가 없고, 비탈에서는 굴러 내려가기까지 합니다.
    ///
    /// <b>일어나는 것은 계산으로 합니다.</b> 물리로 일으켜 세우려면 관절 토크와 균형 제어기가 필요합니다.
    /// 그 대신 리지드바디를 잠시 <b>기구학 모드</b>로 바꿔 자세와 높이를 정해진 시간에 걸쳐 되돌리고,
    /// 그동안 다리는 제자리로 모입니다. 다 서면 다시 물리로 돌려줍니다.
    ///
    /// <b>왜 나눴는가.</b> 넘어지는 순간은 <b>예측할 수 없어야</b> 재미있고(어디로 구를지 모름),
    /// 일어나는 순간은 <b>예측할 수 있어야</b> 합니다(언제 다시 위협이 되는지 알아야 함).
    /// 물리와 계산의 경계를 그 두 성질에 맞춰 그었습니다.
    ///
    /// <code>
    /// 서 있음 ──(충격이 문턱을 넘음)──▶ 쓰러짐 ──(멈춤)──▶ 누움 ──(뜸)──▶ 일어남 ──▶ 서 있음
    ///                                   물리         판정        대기       계산
    /// </code>
    /// </summary>
    [RequireComponent(typeof(WalkerRobot))]
    [RequireComponent(typeof(RobotPhysicsMotor))]
    [DisallowMultipleComponent]
    public class RobotKnockdown : MonoBehaviour
    {
        // --- Public Member Variables : 넘어짐 ---

        /// <summary>
        /// 충격이 만든 <b>속도 변화</b>가 이보다 크면 넘어집니다.
        /// 상대 속도가 아니라 속도 변화라 <b>상대의 무게가 셈에 들어갑니다.</b>
        /// </summary>
        [Header("넘어짐")]
        [Tooltip("충격이 만든 속도 변화(m/s)가 이보다 크면 넘어집니다. 무거운 로봇일수록 크게 잡습니다.")]
        public float knockdownThreshold = 4.5f;

        /// <summary>넘어뜨릴 때 주는 회전 충격입니다. 클수록 확 자빠집니다.</summary>
        [Tooltip("넘어뜨릴 때 주는 회전 충격(rad/s). 클수록 확 자빠집니다.")]
        public float topplingSpin = 3.5f;

        /// <summary>회전 충격의 상한입니다. 아무리 세게 맞아도 팽이가 되지는 않습니다.</summary>
        [Tooltip("회전 충격의 상한(rad/s)")]
        public float maxTopplingSpin = 8f;

        // --- Public Member Variables : 멈춤 판정 ---

        /// <summary>이 속도보다 느려야 멈춘 것으로 봅니다.</summary>
        [Header("멈춤 판정")]
        [Tooltip("이 속도(m/s)보다 느려야 멈춘 것으로 봅니다")]
        public float settleSpeed = 0.7f;

        /// <summary>이 각속도보다 느려야 멈춘 것으로 봅니다.</summary>
        [Tooltip("이 각속도(rad/s)보다 느려야 멈춘 것으로 봅니다")]
        public float settleAngularSpeed = 0.9f;

        /// <summary>이만큼 계속 느려야 멈춘 것으로 인정합니다. 한순간 느려진 것에 속지 않게 합니다.</summary>
        [Tooltip("이만큼(초) 계속 느려야 멈춘 것으로 인정합니다")]
        public float settleTime = 0.5f;

        /// <summary>아무리 굴러도 이 시간이 지나면 멈춘 것으로 봅니다. 영영 못 일어나는 것을 막습니다.</summary>
        [Tooltip("이 시간(초)이 지나면 멈추지 않았어도 누운 것으로 봅니다")]
        public float maxFallTime = 6f;

        // --- Public Member Variables : 일어나기 ---

        /// <summary>누운 채로 뜸을 들이는 시간입니다. 0이면 닿자마자 일어납니다.</summary>
        [Header("일어나기")]
        [Tooltip("누운 채로 뜸을 들이는 시간(초)")]
        public float downedTime = 0.9f;

        /// <summary>몸을 세우는 데 걸리는 시간입니다. 무거운 로봇일수록 길게 잡습니다.</summary>
        [Tooltip("몸을 세우는 데 걸리는 시간(초)")]
        public float riseDuration = 1.6f;

        /// <summary>
        /// 그중 <b>다리를 먼저 모으는 데</b> 쓰는 비율입니다.
        /// 다리가 자리를 잡기 시작한 뒤에 몸이 올라와야 "일어난다"로 보입니다.
        /// </summary>
        [Tooltip("일어나는 시간 중 다리를 옮기는 데 쓰는 비율")]
        [Range(0.2f, 0.9f)]
        public float riseLegFraction = 0.55f;

        /// <summary>
        /// 몸이 <b>언제부터</b> 올라오기 시작할지입니다. 0이면 다리와 동시에 일어나고,
        /// 크면 다리가 먼저 자리를 잡은 뒤에 몸이 따라 올라옵니다.
        /// </summary>
        [Tooltip("몸이 올라오기 시작하는 시점(전체 시간에 대한 비율). 다리가 먼저 자리를 잡습니다.")]
        [Range(0f, 0.6f)]
        public float riseBodyDelay = 0.35f;

        /// <summary>스스로 일어날지 여부입니다. 끄면 쓰러진 채로 남습니다.</summary>
        [Tooltip("스스로 일어납니다. 끄면 쓰러진 채로 남습니다.")]
        public bool riseAutomatically = true;

        // --- Private Member Variables ---

        /// <summary>자세를 잡는 쪽입니다.</summary>
        private WalkerRobot robot;

        /// <summary>힘을 거는 쪽입니다.</summary>
        private RobotPhysicsMotor motor;

        /// <summary>움직일 리지드바디입니다.</summary>
        private Rigidbody body;

        /// <summary>지금 어느 단계인지입니다.</summary>
        private RobotKnockdownState state = RobotKnockdownState.Standing;

        /// <summary>이 단계에 들어온 뒤 흐른 시간입니다.</summary>
        private float stateTime;

        /// <summary>멈춘 상태가 이어진 시간입니다.</summary>
        private float settleTimer;

        /// <summary>일어나기 시작할 때의 자세와 자리입니다.</summary>
        private Quaternion riseFromRotation;

        /// <summary>다 일어섰을 때의 자세입니다. 방위각만 남긴 수직 자세입니다.</summary>
        private Quaternion riseToRotation;

        /// <summary>일어나기 시작할 때의 자리입니다.</summary>
        private Vector3 riseFromPosition;

        /// <summary>다 일어섰을 때의 자리입니다. 몸이 있던 자리 아래의 지면입니다.</summary>
        private Vector3 riseToPosition;

        /// <summary>서 있을 때의 회전 잠금입니다. 일어난 뒤 되돌립니다.</summary>
        private RigidbodyConstraints standingConstraints;

        // --- Constants ---

        /// <summary>0으로 보는 제곱 길이입니다.</summary>
        private const float Epsilon = 1e-6f;

        // --- Public Properties ---

        /// <summary>지금 어느 단계인지입니다.</summary>
        public RobotKnockdownState State { get { return state; } }

        /// <summary>서 있지 않은 상태(쓰러짐·누움·일어남)인지입니다.</summary>
        public bool IsDown { get { return state != RobotKnockdownState.Standing; } }

        // --- Unity Methods ---

        /// <summary>배선을 찾고 서 있을 때의 잠금을 기억해 둡니다.</summary>
        private void Awake()
        {
            robot = GetComponent<WalkerRobot>();
            motor = GetComponent<RobotPhysicsMotor>();
            body = GetComponent<Rigidbody>();

            standingConstraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
        }

        /// <summary>충돌 알림을 듣습니다.</summary>
        private void OnEnable()
        {
            if (motor != null) motor.Struck += OnStruck;
        }

        /// <summary>알림을 끊습니다.</summary>
        private void OnDisable()
        {
            if (motor != null) motor.Struck -= OnStruck;
        }

        /// <summary>단계를 진행합니다.</summary>
        private void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            stateTime += dt;

            switch (state)
            {
                case RobotKnockdownState.Falling:
                    UpdateFalling(dt);
                    break;

                case RobotKnockdownState.Downed:
                    UpdateDowned();
                    break;

                case RobotKnockdownState.Rising:
                    UpdateRising();
                    break;
            }
        }

        // --- Public Methods ---

        /// <summary>
        /// 로봇을 넘어뜨립니다. 밖에서 직접 부를 수도 있습니다. (처형 연출·스크립트 이벤트)
        /// </summary>
        /// <param name="direction">밀려나는 방향. 이 쪽으로 자빠집니다.</param>
        /// <param name="strength">충격이 만든 속도 변화(m/s). 회전 충격의 세기가 됩니다.</param>
        public void Knockdown(Vector3 direction, float strength)
        {
            if (state == RobotKnockdownState.Falling) return;
            if (body == null || robot == null || motor == null) return;

            EnterFalling(direction, strength);
        }

        /// <summary>쓰러져 있다면 지금 일어나게 합니다.</summary>
        public void StandUpNow()
        {
            if (state == RobotKnockdownState.Downed || state == RobotKnockdownState.Falling) EnterRising();
        }

        // --- Private Methods : 사건 ---

        /// <summary>모터가 알려 준 충격이 문턱을 넘으면 넘어뜨립니다.</summary>
        /// <param name="direction">밀려나는 방향</param>
        /// <param name="velocityChange">충격이 만든 속도 변화(m/s)</param>
        private void OnStruck(Vector3 direction, float velocityChange)
        {
            if (velocityChange < knockdownThreshold) return;

            Knockdown(direction, velocityChange);
        }

        // --- Private Methods : 단계 ---

        /// <summary>
        /// 쓰러지기 시작합니다. <b>회전 잠금을 풀고 손을 뗍니다.</b>
        ///
        /// 넘어뜨리는 축은 <b>미는 방향과 수직인 수평축</b>입니다. 그 축으로 돌려야 밀린 쪽으로 자빠집니다.
        /// 세기는 충격에 비례하되 상한을 둡니다. 그러지 않으면 차에 받힌 로봇이 팽이가 됩니다.
        /// </summary>
        /// <param name="direction">밀려나는 방향</param>
        /// <param name="strength">충격이 만든 속도 변화</param>
        private void EnterFalling(Vector3 direction, float strength)
        {
            SetState(RobotKnockdownState.Falling);

            body.isKinematic = false;
            body.constraints = RigidbodyConstraints.None;

            motor.Suspended = true;
            robot.SetPosture(WalkerPosture.Limp);

            Vector3 flat = new Vector3(direction.x, 0f, direction.z);
            if (flat.sqrMagnitude < Epsilon) flat = transform.forward;

            Vector3 axis = Vector3.Cross(Vector3.up, flat.normalized);
            if (axis.sqrMagnitude < Epsilon) return;

            float spin = Mathf.Min(strength * topplingSpin, maxTopplingSpin);

            body.AddTorque(axis.normalized * spin, ForceMode.VelocityChange);
        }

        /// <summary>구르다 멈추면 누운 것으로 봅니다.</summary>
        /// <param name="dt">시간 간격(초)</param>
        private void UpdateFalling(float dt)
        {
            bool slow = body.linearVelocity.magnitude < settleSpeed &&
                        body.angularVelocity.magnitude < settleAngularSpeed;

            settleTimer = slow ? settleTimer + dt : 0f;

            if (settleTimer < settleTime && stateTime < maxFallTime) return;

            SetState(RobotKnockdownState.Downed);
        }

        /// <summary>뜸을 들인 뒤 일어납니다.</summary>
        private void UpdateDowned()
        {
            if (!riseAutomatically) return;
            if (stateTime < downedTime) return;

            EnterRising();
        }

        /// <summary>
        /// 일어나기 시작합니다. 리지드바디를 <b>기구학 모드</b>로 바꿔 자세를 손으로 되돌립니다.
        ///
        /// 다 섰을 때의 자리는 루트 아래가 아니라 <b>몸통이 있던 자리 아래</b>입니다.
        /// 옆으로 누우면 루트(발 높이 기준점)는 몸에서 한참 옆으로 밀려나 있으므로,
        /// 루트 아래에서 일어서면 <b>누운 자리에서 몸 길이만큼 옆으로 순간이동</b>합니다.
        /// </summary>
        private void EnterRising()
        {
            SetState(RobotKnockdownState.Rising);

            body.isKinematic = true;
            body.constraints = standingConstraints;

            motor.Suspended = true;

            riseFromRotation = transform.rotation;
            riseFromPosition = transform.position;

            // 방위각만 남긴 수직 자세로 세웁니다. 누워 있으면 앞 방향이 위아래를 보고 있을 수 있어
            // 그때는 위 방향을 대신 씁니다.
            Vector3 facing = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
            if (facing.sqrMagnitude < Epsilon) facing = Vector3.ProjectOnPlane(transform.up, Vector3.up);
            if (facing.sqrMagnitude < Epsilon) facing = Vector3.forward;

            riseToRotation = Quaternion.LookRotation(facing.normalized, Vector3.up);

            Vector3 pivot = transform.TransformPoint(new Vector3(0f, robot.standHeight, 0f));

            riseToPosition = GroundProbe.Sample(pivot, robot.standHeight, robot.standHeight * 4f, robot.groundMask,
                out Vector3 point, out Vector3 _)
                ? point
                : new Vector3(pivot.x, riseFromPosition.y, pivot.z);

            // 다 섰을 때의 자세를 넘겨 주면, 로봇이 그 자리를 기준으로 발을 <b>하나씩 차례로</b> 옮깁니다.
            robot.BeginRise(riseToPosition, riseToRotation, riseDuration, riseDuration * riseLegFraction);
        }

        /// <summary>정해진 시간에 걸쳐 몸을 세웁니다. 다 서면 물리로 돌려줍니다.</summary>
        private void UpdateRising()
        {
            float t = Mathf.Clamp01(stateTime / Mathf.Max(riseDuration, 0.01f));

            // 다리가 자리를 잡는 동안 몸은 아직 누워 있습니다. 그 뒤부터 올라오기 시작합니다.
            float bodyT = Mathf.InverseLerp(riseBodyDelay, 1f, t);

            // 부드러운 계단 함수라 시작과 끝에서 속도가 0입니다. 뚝 서거나 뚝 멈추지 않습니다.
            float ease = bodyT * bodyT * (3f - 2f * bodyT);

            // 기구학 모드에서는 트랜스폼을 직접 씁니다. MovePosition 은 물리 프레임용이라
            // 여기(Update)에서 부르면 프레임마다 한 번씩 반영되지 않고 건너뛰거나 겹칩니다.
            transform.SetPositionAndRotation(
                Vector3.Lerp(riseFromPosition, riseToPosition, ease),
                Quaternion.Slerp(riseFromRotation, riseToRotation, ease));

            if (t < 1f) return;

            EnterStanding();
        }

        /// <summary>다시 걷습니다. 물리를 돌려주고 그 자리에서 발을 다시 심습니다.</summary>
        private void EnterStanding()
        {
            SetState(RobotKnockdownState.Standing);

            // 순서가 중요합니다. 기구학 상태에서 속도를 대입하면 먹지 않으므로,
            // 물리로 먼저 돌려놓은 뒤에 지웁니다.
            body.isKinematic = false;
            body.constraints = standingConstraints;

            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;

            motor.Suspended = false;
            robot.SetPosture(WalkerPosture.Standing);
        }

        /// <summary>단계를 바꾸고 시계를 되돌립니다.</summary>
        /// <param name="value">바꿀 단계</param>
        private void SetState(RobotKnockdownState value)
        {
            state = value;
            stateTime = 0f;
            settleTimer = 0f;
        }
    }
}
