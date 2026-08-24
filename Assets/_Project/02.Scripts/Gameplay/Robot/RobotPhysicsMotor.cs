using UnityEngine;

namespace CarDrive.Gameplay
{
    /// <summary>
    /// 보행 로봇의 루트를 <b>리지드바디로</b> 움직입니다. 붙이면 로봇이 물리의 영향을 받습니다.
    ///
    /// <b>무엇이 달라지는가.</b> 넷입니다.
    ///  1. <b>부딪힙니다.</b> 바위·나무·차량을 통과하지 못하고 밀려납니다.
    ///  2. <b>밀립니다.</b> 차에 받히거나 폭발에 맞으면 <see cref="AddImpulse"/> 로 날아갑니다.
    ///  3. <b>떨어집니다.</b> 발이 닿을 땅이 없으면 중력이 데려갑니다.
    ///  4. <b>눌립니다.</b> 다리가 몸을 떠받치는 힘이 용수철이라, 착지 충격에 살짝 주저앉았다 펴집니다.
    ///
    /// <b>다리를 하나씩 물리로 세우지 않는 이유.</b> 관절마다 조인트를 걸어 진짜로 걷게 만들면
    /// 균형 제어기가 필요하고, 그 순간 이것은 애니메이션이 아니라 로봇 공학 과제가 됩니다.
    /// 게임에서 필요한 것은 <b>보이는 물리</b>입니다. 그래서 걸음은 그대로 계산으로 두고,
    /// <b>몸 하나만</b> 리지드바디로 만들어 세계와 주고받게 했습니다.
    ///
    /// <b>다리는 여전히 몸을 떠받칩니다.</b> <see cref="WalkerRobot.SupportHeight"/> 가
    /// "지금 발들이 딛고 있는 높이"를 알려 주면, 그 높이로 몸을 끌어올리는 <b>용수철 + 감쇠</b>를 겁니다.
    /// 자동차의 서스펜션과 같은 구조입니다. 중력을 상쇄해 두므로 평지에서는 가라앉지 않고,
    /// 충격을 받으면 눌렸다가 돌아옵니다.
    ///
    /// <b>발밑이 너무 멀면 지지를 포기합니다.</b> 낭떠러지 밖으로 나가면 발은 저 아래 지면을 찾아내는데,
    /// 그 높이로 몸을 끌어내리면 <b>떨어지는 게 아니라 빨려 들어갑니다.</b> 그래서 차이가
    /// <see cref="fallThreshold"/> 를 넘으면 용수철을 끊고 중력에 맡깁니다.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(WalkerRobot))]
    [DisallowMultipleComponent]
    public class RobotPhysicsMotor : MonoBehaviour
    {
        // --- Public Member Variables : 지지 ---

        /// <summary>다리가 몸을 딛고 있는 높이로 끌어올리는 세기입니다. 클수록 딱딱한 다리가 됩니다.</summary>
        [Header("지지 (다리가 몸을 떠받치는 힘)")]
        [Tooltip("몸을 발 높이로 끌어올리는 세기. 클수록 딱딱한 다리입니다.")]
        public float supportStiffness = 60f;

        /// <summary>위아래 출렁임을 잡는 감쇠입니다. 작으면 착지할 때마다 통통 튑니다.</summary>
        [Tooltip("위아래 출렁임을 잡는 감쇠. 작으면 착지할 때마다 통통 튑니다.")]
        public float supportDamping = 12f;

        /// <summary>발밑이 이보다 깊으면 지지를 포기하고 떨어집니다.</summary>
        [Tooltip("발밑이 이보다(m) 깊으면 지지를 포기하고 떨어집니다")]
        public float fallThreshold = 0.6f;

        /// <summary>발자리가 몸보다 이보다 높으면 오르지 않습니다. 벽을 타고 오르는 것을 막습니다.</summary>
        [Tooltip("발자리가 몸보다 이보다(m) 높으면 오르지 않습니다")]
        public float stepUpTolerance = 0.9f;

        // --- Public Member Variables : 이동 ---

        /// <summary>땅에 있을 때 목표 속도로 다가가는 세기입니다. 클수록 즉각적이고 덜 밀립니다.</summary>
        [Header("이동")]
        [Tooltip("땅에 있을 때 목표 속도로 다가가는 세기. 클수록 즉각적이고 덜 밀립니다.")]
        public float groundControl = 12f;

        /// <summary>공중에서 목표 속도로 다가가는 세기입니다. 떨어지는 동안에는 거의 못 움직여야 합니다.</summary>
        [Tooltip("공중에서 목표 속도로 다가가는 세기. 작을수록 떨어질 때 손을 못 씁니다.")]
        public float airControl = 1.2f;

        /// <summary>목표 회전 속도로 다가가는 세기입니다.</summary>
        [Tooltip("목표 회전 속도로 다가가는 세기")]
        public float turnControl = 10f;

        /// <summary>부딪혀 밀려날 수 있는 최대 속도입니다. 폭발에 우주로 날아가는 것을 막습니다.</summary>
        [Tooltip("밀려날 수 있는 최대 속도(m/s)")]
        public float maxPushSpeed = 12f;

        // --- Public Member Variables : 충격 ---

        /// <summary>부딪혔을 때 몸통이 휘청이게 할지 여부입니다.</summary>
        [Header("충격")]
        [Tooltip("부딪혔을 때 몸통이 휘청이게 합니다")]
        public bool staggerOnCollision = true;

        /// <summary>
        /// 충돌이 만든 <b>속도 변화</b>가 이보다 작으면 휘청이지 않습니다.
        /// 상대 속도가 아니라 속도 변화(충격량÷질량)를 보므로 <b>상대의 무게가 셈에 들어갑니다.</b>
        /// </summary>
        [Tooltip("충돌이 만든 속도 변화(m/s)가 이보다 작으면 무시합니다. 상대의 무게가 반영됩니다.")]
        public float staggerThreshold = 1.2f;

        /// <summary>충돌 속도를 휘청임으로 바꾸는 배율입니다.</summary>
        [Tooltip("충돌 속도를 휘청임으로 바꾸는 배율")]
        [Range(0f, 2f)]
        public float staggerScale = 0.6f;

        // --- Private Member Variables ---

        /// <summary>움직일 리지드바디입니다.</summary>
        private Rigidbody body;

        /// <summary>발이 어디를 딛고 있는지 알려 주는 쪽입니다.</summary>
        private WalkerRobot robot;

        /// <summary>드라이버가 지정한 목표 속도입니다. (수평)</summary>
        private Vector3 desiredVelocity;

        /// <summary>드라이버가 지정한 목표 회전 속도(도/초)입니다.</summary>
        private float desiredYawRate;

        /// <summary>지금 다리가 몸을 떠받치고 있는지입니다.</summary>
        private bool supported;

        // --- Public Properties ---

        /// <summary>지금 다리가 몸을 떠받치고 있는지입니다. 아니면 떨어지는 중입니다.</summary>
        public bool IsSupported { get { return supported; } }

        /// <summary>리지드바디의 질량입니다.</summary>
        public float Mass { get { return body != null ? body.mass : 1f; } }

        /// <summary>
        /// 세게 부딪혔을 때 알립니다. 인자는 <b>밀린 방향</b>과 <b>그 충격이 만든 속도 변화(m/s)</b>입니다.
        ///
        /// <see cref="RobotKnockdown"/> 이 이것을 듣고 넘어뜨릴지 정합니다. 사건으로 둔 이유는
        /// 모터가 넘어짐 규칙을 몰라도 되게 하려는 것입니다. 소리·피해도 같은 자리에 붙일 수 있습니다.
        /// </summary>
        public event System.Action<Vector3, float> Struck;

        /// <summary>
        /// 힘을 걸지 말지입니다. 쓰러져 구르는 동안에는 꺼서 <b>중력과 충돌에만</b> 맡깁니다.
        /// </summary>
        public bool Suspended { get; set; }

        /// <summary>지금 실제로 나아가는 수평 속도입니다.</summary>
        public Vector3 HorizontalVelocity
        {
            get
            {
                if (body == null) return Vector3.zero;

                Vector3 velocity = body.linearVelocity;
                return new Vector3(velocity.x, 0f, velocity.z);
            }
        }

        // --- Unity Methods ---

        /// <summary>리지드바디를 보행에 맞게 세웁니다.</summary>
        private void Awake()
        {
            body = GetComponent<Rigidbody>();
            robot = GetComponent<WalkerRobot>();

            // 넘어지지는 않습니다. 경사에 맞춰 기우는 것은 몸통이 발 평면에서 뽑아냅니다.
            // 루트까지 기울이면 두 번 기울어집니다.
            body.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.useGravity = true;
        }

        /// <summary>물리 프레임마다 지지와 이동을 겁니다.</summary>
        private void FixedUpdate()
        {
            float dt = Time.fixedDeltaTime;
            if (dt <= 0f) return;

            if (Suspended)
            {
                supported = false;
                return;
            }

            ApplySupport();
            ApplyMove();
            ApplyTurn();
            ClampSpeed();
        }

        /// <summary>
        /// 세게 부딪히면 몸통을 휘청이게 합니다. 배선 없이도 차에 받히면 비틀거립니다.
        ///
        /// 밀려나는 것 자체는 물리가 이미 해 줍니다. 여기서 더하는 것은 <b>자세</b>뿐입니다.
        /// 문턱을 두는 이유는 경사에서 몸통 상자가 지형을 스칠 때마다 흔들리지 않게 하려는 것입니다.
        /// </summary>
        /// <param name="collision">부딪힌 내용</param>
        private void OnCollisionEnter(Collision collision)
        {
            if (robot == null || body == null) return;

            // <b>상대 속도가 아니라 충격량</b>을 봅니다. 그래야 상대의 질량이 셈에 들어갑니다.
            // 조약돌이 아무리 빨라도 2톤짜리를 휘청이게 하지 못하고, 차가 천천히 밀어도 넘어뜨립니다.
            float velocityChange = collision.impulse.magnitude / Mathf.Max(body.mass, 0.001f);
            if (velocityChange < 0.01f) return;

            // 접촉면의 법선 반대쪽이 우리가 밀려나는 방향입니다.
            Vector3 direction = -collision.GetContact(0).normal;

            if (staggerOnCollision && velocityChange >= staggerThreshold)
            {
                robot.AddImpact(direction * (velocityChange * staggerScale));
            }

            Struck?.Invoke(direction, velocityChange);
        }

        // --- Public Methods ---

        /// <summary>
        /// 이번 물리 프레임에 <b>이렇게 움직이고 싶다</b>고 알려 줍니다. <see cref="RobotDriver"/> 가 부릅니다.
        /// </summary>
        /// <param name="velocity">목표 수평 속도(m/s)</param>
        /// <param name="yawRate">목표 회전 속도(도/초)</param>
        public void Drive(Vector3 velocity, float yawRate)
        {
            desiredVelocity = new Vector3(velocity.x, 0f, velocity.z);
            desiredYawRate = yawRate;
        }

        /// <summary>
        /// 로봇을 밀칩니다. 차량 충돌·폭발·타격이 여기로 들어옵니다.
        /// </summary>
        /// <param name="impulse">충격량(N·s). 질량으로 나뉘어 속도가 됩니다.</param>
        public void AddImpulse(Vector3 impulse)
        {
            if (body == null) return;

            body.AddForce(impulse, ForceMode.Impulse);

            // 밀기만 하면 몸이 꼿꼿한 채로 옮겨 갑니다. 자세도 함께 흔들어야 맞은 것으로 보입니다.
            if (robot != null) robot.AddImpact(impulse / Mathf.Max(body.mass, 0.001f));
        }

        /// <summary>
        /// 로봇을 다른 자리로 옮깁니다. 속도를 지우고 발도 다시 심습니다.
        /// 리지드바디가 있는 로봇은 <see cref="WalkerRobot.Teleport"/> 대신 이쪽을 불러야 합니다.
        /// </summary>
        /// <param name="position">옮길 자리</param>
        /// <param name="rotation">옮길 자세</param>
        public void Teleport(Vector3 position, Quaternion rotation)
        {
            if (body != null)
            {
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
                body.position = position;
                body.rotation = rotation;
            }

            if (robot != null) robot.Teleport(position, rotation);
        }

        // --- Private Methods ---

        /// <summary>
        /// 다리가 몸을 떠받치는 힘을 겁니다. 자동차 서스펜션과 같은 용수철 + 감쇠입니다.
        /// 중력을 함께 상쇄하므로 평지에서는 가라앉지 않고 <b>제 높이에서 균형</b>을 이룹니다.
        /// </summary>
        private void ApplySupport()
        {
            supported = false;

            if (robot == null || !robot.HasGround) return;

            float error = robot.SupportHeight - body.position.y;

            // 발밑이 너무 깊으면(낭떠러지) 끌어내리지 않고 떨어지게 둡니다.
            // 반대로 너무 높으면(벽) 타고 오르지 않습니다.
            if (error < -fallThreshold || error > stepUpTolerance) return;

            supported = true;

            float verticalSpeed = body.linearVelocity.y;
            float acceleration = error * supportStiffness - verticalSpeed * supportDamping - Physics.gravity.y;

            body.AddForce(Vector3.up * acceleration, ForceMode.Acceleration);
        }

        /// <summary>목표 수평 속도로 다가갑니다. 공중에서는 거의 손을 쓰지 못합니다.</summary>
        private void ApplyMove()
        {
            Vector3 error = desiredVelocity - HorizontalVelocity;
            float control = supported ? groundControl : airControl;

            body.AddForce(error * control, ForceMode.Acceleration);
        }

        /// <summary>목표 회전 속도로 다가갑니다.</summary>
        private void ApplyTurn()
        {
            float current = body.angularVelocity.y * Mathf.Rad2Deg;
            float error = desiredYawRate - current;

            body.AddTorque(Vector3.up * (error * turnControl * Mathf.Deg2Rad), ForceMode.Acceleration);
        }

        /// <summary>밀려나는 속도에 상한을 둡니다. 폭발 한 번에 우주로 나가지 않게 합니다.</summary>
        private void ClampSpeed()
        {
            Vector3 velocity = body.linearVelocity;
            Vector3 horizontal = new Vector3(velocity.x, 0f, velocity.z);

            if (horizontal.sqrMagnitude <= maxPushSpeed * maxPushSpeed) return;

            horizontal = horizontal.normalized * maxPushSpeed;
            body.linearVelocity = new Vector3(horizontal.x, velocity.y, horizontal.z);
        }
    }
}
