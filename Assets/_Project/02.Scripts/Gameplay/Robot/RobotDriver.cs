using UnityEngine;
using CarDrive.Common;

namespace CarDrive.Gameplay
{
    /// <summary>
    /// 로봇의 <b>루트</b>를 <b>목표 위치</b>로 걸어가게 합니다. 다리와 몸통은 건드리지 않습니다.
    ///
    /// <b>왜 이동 명령이 아니라 목표 위치인가.</b> 예전에는 키 입력을 그대로 속도로 바꿔 루트를 밀었습니다.
    /// 그러면 <b>다리가 따라올 수 있는지와 무관하게</b> 몸이 나아갑니다. 발은 제자리에서
    /// 다리가 닿는 만큼만 벗어날 수 있으므로, 몸이 그보다 빨리 가면 발이 매 걸음 뒤로 밀려나
    /// 결국 다리가 뻗은 채 미끄러집니다. 이동키를 꾹 누르고 있을 때 걸음이 이어지지 않던 이유입니다.
    ///
    /// 목표 위치로 바꾸면 <b>얼마나 빨리 갈지를 이쪽이 정할 수 있습니다.</b>
    /// <see cref="WalkerRobot.MaxTravelSpeed"/> 가 "다리가 따라올 수 있는 최고 속도"를 알려 주고,
    /// 여기서 그 값으로 속도를 묶습니다. 그래서 <b>다리가 몸을 놓치는 일이 구조적으로 없습니다.</b>
    /// 목표에 가까워지면 스스로 감속해 멈추는 것도 덤으로 따라옵니다.
    ///
    /// <b>목표는 둘 중 하나로 줍니다.</b>
    ///  1. <see cref="destinationTarget"/> 에 트랜스폼을 꽂으면 그것을 계속 쫓습니다.
    ///     (실행 중에 씬 뷰에서 마커를 끌어 옮기면 로봇이 따라옵니다)
    ///  2. 코드에서 <see cref="SetDestination"/> 을 부릅니다.
    ///
    /// 둘 다 없으면 서 있습니다.
    ///
    /// <b>속도와 회전에 2차 시스템을 씌운 이유.</b> 목표를 그대로 속도로 바꾸면 로봇이
    /// 정지에서 최고 속도로 <b>한 프레임 만에</b> 갑니다. 그러면 몸통이 기울 일이 없습니다
    /// (기울기는 가속도에서 나오므로) 여기에 관성을 넣으면 <b>출발할 때 몸을 숙이고
    /// 멈출 때 앞으로 쏠리는</b> 모습이 따로 만들지 않아도 나옵니다.
    ///
    /// <b>물리는 선택입니다.</b> <see cref="RobotPhysicsMotor"/> 가 같은 오브젝트에 붙어 있으면
    /// 루트를 옮기는 일을 통째로 그쪽에 넘깁니다. 부딪히고 밀리고 떨어지는 로봇이 됩니다.
    /// 없으면 트랜스폼을 직접 옮깁니다. <b>어느 쪽이든 목표를 정하는 방식은 같습니다.</b>
    ///
    /// 루트는 <b>기울지 않습니다.</b> 항상 세워 둔 채 방위각만 돌립니다. 경사에 맞춰 기우는 것은
    /// 몸통의 일이고, 몸통은 네 발이 만드는 평면에서 그것을 뽑아냅니다.
    /// 루트까지 기울이면 두 번 기울어집니다.
    /// </summary>
    [RequireComponent(typeof(WalkerRobot))]
    [DisallowMultipleComponent]
    public class RobotDriver : MonoBehaviour
    {
        // --- Public Member Variables : 목표 ---

        /// <summary>쫓아갈 대상입니다. 꽂아 두면 매 프레임 그 위치를 목표로 삼습니다.</summary>
        [Header("목표")]
        [Tooltip("쫓아갈 대상. 실행 중에 씬 뷰에서 끌어 옮기면 로봇이 따라옵니다.")]
        public Transform destinationTarget;

        /// <summary>이 거리 안으로 들어오면 도착으로 봅니다.</summary>
        [Tooltip("이 거리(m) 안으로 들어오면 도착으로 봅니다")]
        public float arriveRadius = 1.2f;

        /// <summary>도착한 뒤 다시 출발하기까지 필요한 여유 거리입니다. 문턱에서 덜덜거리는 것을 막습니다.</summary>
        [Tooltip("다시 출발하기까지 필요한 여유 거리(m)")]
        public float restartMargin = 0.6f;

        /// <summary>이 거리부터 감속을 시작합니다. 도착 반경에 부드럽게 붙습니다.</summary>
        [Tooltip("이 거리(m)부터 감속을 시작합니다")]
        public float slowRadius = 3f;

        // --- Public Member Variables : 이동 ---

        /// <summary>
        /// 내고 싶은 속도입니다. <b>실제 속도는 다리가 따라올 수 있는 만큼으로 묶입니다.</b>
        /// (<see cref="WalkerRobot.MaxTravelSpeed"/>)
        /// </summary>
        [Header("이동")]
        [Tooltip("내고 싶은 속도(m/s). 실제로는 다리가 따라올 수 있는 만큼으로 묶입니다.")]
        public float cruiseSpeed = 1.6f;

        /// <summary>제자리에서 도는 최대 속도입니다.</summary>
        [Tooltip("도는 최대 속도(도/초)")]
        public float turnRate = 130f;

        /// <summary>속도가 목표에 다가가는 방식입니다. 이것이 곧 가속의 무게감입니다.</summary>
        [Tooltip("속도의 2차 시스템. 느릴수록 무거운 로봇이 됩니다.")]
        public SecondOrderSettings speedSpring = new SecondOrderSettings(1f, 1f, 0f);

        /// <summary>회전 속도가 목표에 다가가는 방식입니다.</summary>
        [Tooltip("회전 속도의 2차 시스템")]
        public SecondOrderSettings turnSpring = new SecondOrderSettings(2.5f, 1f, 0f);

        // --- Public Member Variables : 지면 ---

        /// <summary>땅으로 볼 레이어입니다. <b>로봇 자신은 빼야 합니다.</b></summary>
        [Header("지면")]
        [Tooltip("땅으로 볼 레이어. 로봇 자신의 콜라이더가 들어가면 안 됩니다. (이 월드의 지형은 Ground 레이어입니다)")]
        public LayerMask groundMask = ~0;

        /// <summary>땅을 찾는 레이가 루트보다 위에서 출발하는 거리입니다.</summary>
        [Tooltip("땅을 찾는 레이가 루트보다 위에서 출발하는 거리(m)")]
        public float groundProbeUp = 2f;

        /// <summary>땅을 찾는 레이가 아래로 보는 거리입니다.</summary>
        [Tooltip("땅을 찾는 레이가 아래로 보는 거리(m)")]
        public float groundProbeDown = 6f;

        /// <summary>
        /// 루트 높이가 지면을 따라가는 방식입니다.
        ///
        /// 계단이나 바위에서 지면 높이는 <b>계단식으로 뜁니다.</b> 그대로 넣으면 발이 서고 싶은
        /// 자리도 함께 뛰어 예측이 흔들립니다. 여기에 한 번 통과시키면 루트가 매끄럽게 오르고,
        /// 발은 각자 지면을 찾으므로 <b>정확도는 잃지 않습니다.</b>
        /// </summary>
        [Tooltip("루트 높이의 2차 시스템. 계단에서 루트가 튀는 것을 막습니다.")]
        public SecondOrderSettings heightSpring = new SecondOrderSettings(6f, 1f, 0f);

        // --- Public Member Variables : 디버그 ---

        /// <summary>씬 뷰에 목표와 도착 반경을 그릴지 여부입니다.</summary>
        [Header("디버그")]
        [Tooltip("씬 뷰에 목표와 도착 반경을 그립니다")]
        public bool drawGizmos = true;

        // --- Private Member Variables ---

        /// <summary>자세를 잡는 쪽입니다. 다리가 따라올 수 있는 속도를 여기서 물어봅니다.</summary>
        private WalkerRobot robot;

        /// <summary>
        /// 물리 모터입니다. 붙어 있으면 루트를 옮기는 일을 <b>전부 그쪽에 넘깁니다.</b>
        /// 없으면 이 클래스가 트랜스폼을 직접 옮깁니다. (물리가 필요 없는 연출용 로봇)
        /// </summary>
        private RobotPhysicsMotor motor;

        /// <summary>현재 전진 속도입니다.</summary>
        private SecondOrderDynamics speedMotion;

        /// <summary>현재 회전 속도(도/초)입니다.</summary>
        private SecondOrderDynamics turnMotion;

        /// <summary>현재 루트 높이입니다.</summary>
        private SecondOrderDynamics heightMotion;

        /// <summary>코드로 지정한 목표입니다. <see cref="destinationTarget"/> 이 비어 있을 때 씁니다.</summary>
        private Vector3 destination;

        /// <summary>지금 갈 곳이 있는지입니다.</summary>
        private bool hasDestination;

        /// <summary>지금 걸어가는 중인지입니다. 도착 반경에 이력을 주려고 들고 있습니다.</summary>
        private bool travelling;

        // --- Constants ---

        /// <summary>이 각도만큼 틀어져 있으면 조향을 최대로 씁니다.</summary>
        /// <summary>이만큼까지는 넘어도 넘어갑니다. 조금 넘는 것은 가감속 여유로 흡수됩니다.</summary>
        private const float SpeedWarnMargin = 1.2f;

        private const float FullSteerAngle = 45f;

        /// <summary>이 각도보다 크게 틀어져 있으면 전진을 멈추고 <b>돌기부터</b> 합니다.</summary>
        private const float TurnFirstAngle = 120f;

        /// <summary>루트가 이만큼 순간이동했다면 높이 시스템을 다시 세웁니다.</summary>
        private const float TeleportThreshold = 2f;

        // --- Public Properties ---

        /// <summary>지금 내고 있는 전진 속도입니다. 물리 모터가 있으면 실제로 나아가는 속도입니다.</summary>
        public float CurrentSpeed
        {
            get
            {
                if (motor != null) return motor.HorizontalVelocity.magnitude;

                return speedMotion != null ? speedMotion.Value : 0f;
            }
        }

        /// <summary>지금 내고 있는 회전 속도(도/초)입니다.</summary>
        public float CurrentTurnRate { get { return turnMotion != null ? turnMotion.Value : 0f; } }

        /// <summary>지금 갈 곳이 있는지입니다.</summary>
        public bool HasDestination { get { return destinationTarget != null || hasDestination; } }

        /// <summary>지금 가고 있는 목표입니다. 갈 곳이 없으면 제자리를 돌려줍니다.</summary>
        public Vector3 Destination
        {
            get
            {
                if (destinationTarget != null) return destinationTarget.position;

                return hasDestination ? destination : transform.position;
            }
        }

        /// <summary>
        /// 다리가 따라올 수 있는 만큼으로 묶은, 실제로 낼 수 있는 속도입니다.
        /// </summary>
        public float TravelSpeedLimit
        {
            get { return robot != null ? Mathf.Min(cruiseSpeed, robot.MaxTravelSpeed) : cruiseSpeed; }
        }

        // --- Unity Methods ---

        /// <summary>2차 시스템 셋을 세웁니다.</summary>
        private void Awake()
        {
            robot = GetComponent<WalkerRobot>();
            motor = GetComponent<RobotPhysicsMotor>();

            speedMotion = new SecondOrderDynamics(speedSpring, 0f);
            turnMotion = new SecondOrderDynamics(turnSpring, 0f);
            heightMotion = new SecondOrderDynamics(heightSpring, transform.position.y);
        }

        /// <summary>
        /// <b>다리가 따라올 수 없는 순항 속도</b>인지 한 번 확인합니다.
        ///
        /// 이것은 조용히 망가지는 종류의 설정 실수입니다. 속도는 자동으로 묶이므로 오류도 경고도
        /// 나지 않고, 로봇은 그저 <b>느리게 기어가며 제자리에서 통통 튑니다.</b> 보폭이 너무 짧아
        /// 몇 센티만 움직여도 걸음이 새로 시작되고, 그때마다 발이 <c>stepHeight</c> 만큼 들리기
        /// 때문입니다.
        ///
        /// 가장 흔한 원인은 <b>몸통이 너무 높은 것</b>입니다. 서 있는 높이가 다리가 뻗을 수 있는
        /// 길이에 가까워지면 다리가 거의 곧게 서고, 남는 <b>수평</b> 여유가 급격히 사라집니다.
        /// 다리를 길게 하거나 <c>standHeight</c> 를 낮추면 됩니다.
        /// </summary>
        private void Start()
        {
            if (robot == null) return;

            float reachable = robot.MaxTravelSpeed;

            if (cruiseSpeed <= reachable * SpeedWarnMargin) return;

            GameLog.Warn(GameLog.Channel.Enemy, name + ": 순항 속도 " + cruiseSpeed.ToString("0.00") +
                "m/s 는 다리가 따라올 수 있는 " + reachable.ToString("0.00") + "m/s 를 넘습니다. " +
                "보폭이 " + robot.PlannedStride.ToString("0.00") + "m 밖에 안 됩니다 — " +
                "standHeight 를 낮추거나 다리를 길게 하세요. 지금은 느리게 기며 통통 튑니다.", this);
        }

        /// <summary>목표를 향해 루트를 옮깁니다.</summary>
        private void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            ComputeCommand(out float throttle, out float steer);

            float targetSpeed = throttle * TravelSpeedLimit;
            float targetTurn = steer * turnRate;

            // 걸음은 <b>내려는 속도</b>에 맞춰 계획해야 합니다. 실측으로 계획하면 출발하는 동안
            // 첫 걸음이 느리게 잡혀 나머지 다리가 뒤로 끌려납니다. (WalkerRobot.PlanForMotion 참고)
            if (robot != null) robot.PlanForMotion(targetSpeed, targetTurn);

            // 물리 모터가 있으면 여기서 트랜스폼을 건드리지 않습니다. 리지드바디를 밀면
            // 부딪히고 밀리고 떨어지는 것이 전부 따라옵니다. 관성도 물리가 주므로
            // 아래의 2차 시스템 두 개는 <b>물리가 없을 때만</b> 쓰입니다.
            if (motor != null)
            {
                motor.Drive(transform.forward * targetSpeed, targetTurn);
                return;
            }

            float speed = speedMotion.Update(dt, targetSpeed);
            float turn = turnMotion.Update(dt, targetTurn);

            transform.Rotate(0f, turn * dt, 0f, Space.World);
            transform.position += transform.forward * (speed * dt);

            FollowGround(dt);
        }

        /// <summary>목표와 도착 반경을 그립니다.</summary>
        private void OnDrawGizmos()
        {
            if (!drawGizmos || !HasDestination) return;

            Vector3 goal = Destination;

            Gizmos.color = new Color(0.4f, 1f, 0.6f, 0.9f);
            Gizmos.DrawLine(transform.position, goal);
            Gizmos.DrawWireSphere(goal, 0.15f);

            Gizmos.color = new Color(0.4f, 1f, 0.6f, 0.35f);
            Gizmos.DrawWireSphere(goal, arriveRadius);
        }

        // --- Public Methods ---

        /// <summary>걸어갈 자리를 정합니다. <see cref="destinationTarget"/> 이 꽂혀 있으면 그쪽이 우선입니다.</summary>
        /// <param name="worldPosition">걸어갈 월드 위치</param>
        public void SetDestination(Vector3 worldPosition)
        {
            destination = worldPosition;
            hasDestination = true;
        }

        /// <summary>목표를 지웁니다. 로봇이 그 자리에 섭니다.</summary>
        public void ClearDestination()
        {
            hasDestination = false;
            travelling = false;
        }

        // --- Private Methods ---

        /// <summary>
        /// 목표를 향한 명령을 만듭니다.
        ///
        /// 크게 틀어져 있으면 <b>돌기부터</b> 합니다. 그러지 않으면 로봇이 원을 그리며
        /// 목표 주위를 도는데, 네발짐승이 그렇게 걷지는 않습니다.
        /// </summary>
        /// <param name="throttle">전진 정도 (0~1)</param>
        /// <param name="steer">조향 정도 (−1 ~ 1)</param>
        private void ComputeCommand(out float throttle, out float steer)
        {
            throttle = 0f;
            steer = 0f;

            if (!HasDestination)
            {
                travelling = false;
                return;
            }

            Vector3 toTarget = Destination - transform.position;
            toTarget.y = 0f;

            float distance = toTarget.magnitude;
            if (distance < 0.001f) return;

            float angle = Vector3.SignedAngle(transform.forward, toTarget / distance, Vector3.up);
            steer = Mathf.Clamp(angle / FullSteerAngle, -1f, 1f);

            // 도착 반경에 이력을 둡니다. 없으면 문턱에서 앞뒤로 덜덜거립니다.
            if (travelling && distance < arriveRadius) travelling = false;
            else if (!travelling && distance > arriveRadius + restartMargin) travelling = true;

            if (!travelling) return;

            // 남은 거리에 맞춰 감속합니다. 그래야 목표를 지나쳤다 되돌아오지 않습니다.
            float approach = Mathf.Clamp01((distance - arriveRadius) / Mathf.Max(slowRadius, 0.01f));

            throttle = approach * Mathf.Clamp01(1f - Mathf.Abs(angle) / TurnFirstAngle);
        }

        /// <summary>루트를 지면 높이에 올려 둡니다.</summary>
        /// <param name="dt">시간 간격(초)</param>
        private void FollowGround(float dt)
        {
            Vector3 position = transform.position;

            // 레이캐스트만 쓰면 지형을 놓칩니다. 이유는 GroundProbe 설명에 있습니다.
            if (!GroundProbe.Sample(position, groundProbeUp, groundProbeDown, groundMask,
                    out Vector3 point, out Vector3 _))
            {
                return;
            }

            // 누군가 루트를 통째로 옮겼다면(순간이동·지면 스냅) 높이를 따라가지 말고 그 자리에서 다시 시작합니다.
            if (Mathf.Abs(heightMotion.Value - position.y) > TeleportThreshold) heightMotion.Reset(position.y);

            position.y = heightMotion.Update(dt, point.y);
            transform.position = position;
        }
    }
}
