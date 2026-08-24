using UnityEngine;
using CarDrive.Common;

namespace CarDrive.Gameplay
{
    /// <summary>
    /// 일어날 때 다리를 <b>어떤 차례로</b> 딛을지입니다.
    /// </summary>
    public enum WalkerRiseOrder
    {
        /// <summary>배선 순서 그대로. 앞에서 뒤로, 왼쪽에서 오른쪽으로 하나씩 딛습니다.</summary>
        LegOrder,

        /// <summary>
        /// 걸을 때와 <b>같은 묶음</b>으로 딛습니다. 4족 속보면 대각선 둘씩, 2족이면 한 짝씩입니다.
        /// 일어나는 모습이 걷는 모습과 이어져 보입니다.
        /// </summary>
        GaitGroups,

        /// <summary>
        /// <b>땅에 가까운 발부터</b> 딛습니다. 옆으로 누우면 아래쪽 다리가 이미 땅에 닿아 있으므로,
        /// 그 발로 먼저 몸을 받치고 위쪽 다리를 나중에 넘기는 모습이 됩니다.
        /// </summary>
        LowestFirst
    }

    /// <summary>
    /// 편집 중에 다리 하나를 고쳤을 때 <b>나머지 다리가 얼마나 따라올지</b>입니다.
    /// </summary>
    public enum WalkerRigSync
    {
        /// <summary>따라오지 않습니다. <b>다리마다 완전히 다르게</b> 만들 수 있습니다.</summary>
        Off,

        /// <summary>
        /// <b>좌우로 짝이 되는 다리에만</b> 옮깁니다. (기본)
        ///
        /// 짝은 발자리가 Z 는 같고 X 는 부호만 다른 다리입니다. 길이와 함께 고관절·발자리·무릎
        /// 방향까지 X 대칭으로 맞춥니다. <b>앞다리와 뒷다리는 서로 다르게 둘 수 있습니다</b> —
        /// 앞이 짧고 뒤가 긴 짐승 같은 비례가 여기서 나옵니다.
        /// </summary>
        MirrorPair,

        /// <summary>
        /// <b>모든 다리</b>의 마디 길이를 하나로 맞춥니다. 좌우 짝은 자리까지 대칭으로 갑니다.
        /// 다리가 전부 같은 기계형 리그에 씁니다.
        /// </summary>
        AllLegs
    }

    /// <summary>
    /// 로봇이 지금 <b>어떤 상태의 몸</b>인지입니다. <see cref="RobotKnockdown"/> 이 바꿉니다.
    /// </summary>
    public enum WalkerPosture
    {
        /// <summary>평소. 걷고, 발에서 자세를 뽑습니다.</summary>
        Standing,

        /// <summary>쓰러지는 중. 다리가 늘어지고 몸통은 굴러가는 루트를 그대로 따릅니다.</summary>
        Limp,

        /// <summary>일어나는 중. 발을 제자리로 모으고 몸통이 그 위로 올라옵니다. 걸음은 멈춥니다.</summary>
        Rising
    }

    /// <summary>
    /// 애니메이션 클립 없이 <b>계산만으로</b> 걷는 보행 로봇입니다. <b>다리 수를 가리지 않습니다.</b>
    /// 2족(드레드노트) · 3족(스트라이더) · 4족 · 6족이 전부 이 하나로 돕니다.
    ///
    /// <b>무엇이 애니메이터를 대신하는가.</b> 둘뿐입니다.
    ///  1. <b>발</b> — 제자리에서 너무 멀어지면 새 자리로 호를 그리며 옮깁니다. (<see cref="WalkerLeg"/>)
    ///  2. <b>몸통</b> — 발들의 평균과 그 평면에서 자세를 뽑고, 2차 시스템으로 늦게 따라갑니다.
    ///
    /// <b>왜 시계를 두지 않는가.</b> 걸음을 주기로 만들면 "지금 몇 초"에 발이 어디 있어야 하는지가
    /// 정해집니다. 그러면 속도가 변하거나 경사를 만났을 때 <b>발이 땅을 긁습니다.</b>
    /// 여기서는 반대로 <b>거리</b>가 걸음을 부릅니다. 로봇이 멈추면 걸음도 멈춥니다.
    ///
    /// <b>걸음의 속도는 다리 기하가 정합니다.</b> 발은 제자리에서 <see cref="WalkerLeg.StrideRadius"/>
    /// 안에서만 놓일 수 있습니다. 그 원을 벗어나면 IK 가 한계에서 잘려 다리가 뻗은 채 미끄러집니다.
    /// 그래서 걸음 시간을 <b>거꾸로</b> 계산합니다.
    /// <code>
    /// 계획 보폭 = 2 × 작업 반경 × strideUsage
    /// 한 바퀴   = 계획 보폭 ÷ 속도
    /// 걸음 하나 = 한 바퀴 ÷ 묶음 수      (파도보면 다리 수만큼, 교대보면 둘)
    /// 최고 속도 = 계획 보폭 ÷ (걸음 시간 하한 × 묶음 수)
    /// </code>
    /// 못 따라가는 속도는 <see cref="MaxTravelSpeed"/> 로 알려 주고 <see cref="RobotDriver"/> 가 묶습니다.
    ///
    /// <b>순서가 곧 품질입니다.</b> 이 클래스가 다리를 직접 순서대로 불러 주는 이유가 그것입니다.
    /// <code>
    /// 1. 루트의 속도·회전율을 잰다
    /// 2. 발이 가고 싶은 자리를 찾는다 (예측 + 지면 탐침, 작업 반경으로 자름)
    /// 3. 어느 묶음이 나갈지 정한다   (모든 발이 땅에 있을 때만)
    /// 4. 발을 옮긴다        ← 마디는 아직 건드리지 않습니다
    /// 5. 몸통 자세를 잡는다  ← 4번의 결과를 씁니다
    /// 6. 마디를 세운다      ← 5번이 정한 자리에서 IK 를 풉니다
    /// </code>
    ///
    /// <b>물리는 <see cref="RobotPhysicsMotor"/> 가 맡습니다.</b> 붙어 있으면 루트가 리지드바디로 움직여
    /// 부딪히고 밀리고 떨어집니다. 없으면 <see cref="RobotDriver"/> 가 트랜스폼을 직접 옮깁니다.
    /// 어느 쪽이든 <b>이 클래스는 달라지지 않습니다.</b> 루트가 어디 있든 자세를 잡을 뿐입니다.
    ///
    /// 참고: t3ssel8r, "Giving Personality to Procedural Animations using Math" 와
    /// Wikipedia 의 Matched Z-transform · Semi-implicit Euler.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public class WalkerRobot : MonoBehaviour
    {
        // --- Public Member Variables : 배선 ---

        /// <summary>자세를 잡을 몸통입니다. 루트의 자식이어야 합니다.</summary>
        [Header("배선")]
        [Tooltip("자세를 잡을 몸통. 루트의 자식이어야 합니다.")]
        public Transform body;

        /// <summary>
        /// 다리들입니다. <b>앞에서 뒤로, 각 줄마다 왼쪽 · 오른쪽</b> 순서로 넣습니다.
        /// 교대보의 대각선 묶음이 이 순서를 전제로 나옵니다.
        /// </summary>
        [Tooltip("다리들. 앞→뒤, 각 줄마다 왼쪽→오른쪽 순서. 비워두면 자식에서 찾습니다.")]
        public WalkerLeg[] legs;

        // --- Public Member Variables : 지면 ---

        /// <summary>발이 딛을 수 있는 레이어입니다. <b>로봇 자신은 빼야 합니다.</b></summary>
        [Header("지면")]
        [Tooltip("발이 딛을 수 있는 레이어. 로봇 자신의 콜라이더가 들어가면 안 됩니다.")]
        public LayerMask groundMask = ~0;

        /// <summary>발 자리를 찾을 때 얼마나 위에서 아래로 쏘는지입니다. 오를 수 있는 턱의 높이입니다.</summary>
        [Tooltip("발 자리를 찾는 레이를 얼마나 위에서 쏘는가. 오를 수 있는 턱의 높이입니다.")]
        public float groundProbeUp = 1.2f;

        /// <summary>발 자리를 찾을 때 아래로 얼마나 멀리 보는지입니다.</summary>
        [Tooltip("발 자리를 찾는 레이가 아래로 보는 거리. 내려설 수 있는 깊이입니다.")]
        public float groundProbeDown = 4f;

        /// <summary>
        /// 시작할 때 루트를 지면 위로 올릴지 여부입니다.
        ///
        /// 이 월드의 평지는 이미 y≈19.6m 에 있습니다. 프리팹을 씬에 끌어다 놓으면 보통
        /// 원점 근처, 즉 <b>지면보다 20m 아래</b>에 떨어집니다. 그대로 두면 다리가 허공을 딛습니다.
        /// </summary>
        [Tooltip("시작할 때 루트를 지면 위로 올립니다. 프리팹을 아무 데나 놓아도 지형 위에 섭니다.")]
        public bool snapToGroundOnStart = true;

        // --- Public Member Variables : 자세 ---

        /// <summary>발 평면에서 몸통까지의 높이입니다.</summary>
        [Header("자세")]
        [Tooltip("발 평면에서 몸통까지의 높이")]
        public float standHeight = 0.42f;

        /// <summary>들린 발을 몸통이 얼마나 되받는지입니다. 0이면 몸통이 위아래로 흔들리지 않습니다.</summary>
        /// <summary>
        /// 서 있을 때 몸통이 <b>앞뒤로 기우는 각도</b>(도)입니다. 양수가 앞으로 숙인 자세입니다.
        ///
        /// 가감속 기울임과 달리 <b>가만히 있어도 남아 있는</b> 자세입니다. 앞으로 숙이면 맹수처럼,
        /// 뒤로 젖히면 버티고 선 중장비처럼 보입니다. 실루엣만 바꾸는 값이 아닙니다 —
        /// 몸통이 기울면 <b>고관절이 앞뒤로 옮겨져</b> 다리마다 뻗는 여유가 달라집니다.
        /// 앞으로 숙이면 앞다리는 좁아지고 뒷다리는 넓어집니다. 작업 반경은 리그에서 다시 재므로
        /// 그 변화가 걸음에 저절로 반영됩니다.
        /// </summary>
        [Tooltip("서 있을 때 몸통이 앞뒤로 기우는 각도(도). 양수가 앞으로 숙임")]
        [Range(-45f, 45f)]
        public float standPitch;

        [Tooltip("들린 발 때문에 생기는 위아래 흔들림을 몸통이 얼마나 따라가는가. 0=흔들리지 않음")]
        [Range(0f, 2f)]
        public float bodyBob = 0.5f;

        /// <summary>선회할 때 안쪽으로 기우는 최대 각도입니다.</summary>
        [Tooltip("선회할 때 안쪽으로 기우는 최대 각도(도)")]
        public float leanIntoTurn = 7f;

        /// <summary>가속·감속할 때 앞뒤로 기우는 최대 각도입니다.</summary>
        [Tooltip("가속·감속할 때 앞뒤로 기우는 최대 각도(도)")]
        public float pitchIntoAccel = 6f;

        /// <summary>몸통 위치가 목표를 따라가는 방식입니다.</summary>
        [Tooltip("몸통 위치의 2차 시스템")]
        public SecondOrderSettings bodyPositionSpring = new SecondOrderSettings(3.4f, 0.8f, 0.35f);

        /// <summary>몸통 회전이 목표를 따라가는 방식입니다.</summary>
        [Tooltip("몸통 회전의 2차 시스템")]
        public SecondOrderSettings bodyRotationSpring = new SecondOrderSettings(2.8f, 0.7f, 0.6f);

        /// <summary>
        /// 충격을 받았을 때 <b>휘청이는 방식</b>입니다. 감쇠비를 낮게 잡을수록 오래 흔들립니다.
        ///
        /// 자세를 따라가는 용수철과 <b>따로</b> 두는 이유가 있습니다. 몸통이 발 평면을 따라가는 것은
        /// 매끄러워야 하지만(감쇠 0.7), 얻어맞았을 때는 두어 번 넘나들며 흔들려야 맞은 것처럼 보입니다.
        /// 하나로 묶으면 둘 중 하나를 포기해야 합니다.
        /// </summary>
        [Tooltip("충격을 받았을 때 휘청이는 2차 시스템. 감쇠비가 낮을수록 오래 흔들립니다.")]
        public SecondOrderSettings impactSpring = new SecondOrderSettings(1.9f, 0.32f, 0f);

        /// <summary>충격 속도 1m/s 당 몸통이 <b>가장 많이</b> 기우는 각도입니다.</summary>
        [Tooltip("충격 속도 1m/s 당 몸통이 가장 많이 기우는 각도(도)")]
        public float impactTiltPerSpeed = 3f;

        /// <summary>아무리 세게 맞아도 이보다 기울지는 않습니다.</summary>
        [Tooltip("충격으로 기울 수 있는 최대 각도(도)")]
        public float impactMaxTilt = 28f;

        // --- Public Member Variables : 걸음 ---

        /// <summary>손으로 고른 보행입니다. <see cref="autoGait"/>가 꺼져 있을 때 씁니다.</summary>
        [Header("걸음")]
        [Tooltip("손으로 고른 보행. 자동 전환이 꺼져 있을 때 씁니다.")]
        public WalkerGaitType gait = WalkerGaitType.Alternate;

        /// <summary>속도에 따라 파도보 ↔ 교대보로 바꿀지 여부입니다.</summary>
        [Tooltip("느리면 파도보(한 발씩), 빠르면 교대보(절반씩)로 바꿉니다")]
        public bool autoGait = true;

        /// <summary>이 속도를 넘으면 교대보로 바꿉니다.</summary>
        [Tooltip("이 속도(m/s)를 넘으면 교대보")]
        public float alternateSpeed = 0.9f;

        /// <summary>
        /// 다리가 뻗을 수 있는 길이의 몇 배까지 쓸지입니다. 작업 반경이 여기서 나옵니다.
        /// 1에 가까우면 다리가 완전히 펴진 자세까지 쓰므로 보폭은 커지지만 실루엣이 뻣뻣해집니다.
        /// </summary>
        [Tooltip("다리가 뻗을 수 있는 길이의 몇 배까지 쓸지. 작업 반경이 여기서 나옵니다.")]
        [Range(0.3f, 1f)]
        public float strideScale = 0.9f;

        /// <summary>
        /// 작업 반경을 <b>얼마나 쓸지</b>입니다. 나머지는 가감속과 방향 전환에 남겨 두는 여유입니다.
        ///
        /// 1로 두면 정상 주행에서 발이 매 걸음 작업 반경의 끝까지 갔다 옵니다. 속도가 일정하면
        /// 그래도 되지만, <b>가속하는 동안에는 그 여유가 없어</b> 다리가 한계를 넘습니다.
        /// </summary>
        [Tooltip("작업 반경을 얼마나 쓸지. 나머지는 가감속·선회에 남겨 두는 여유입니다.")]
        [Range(0.4f, 1f)]
        public float strideUsage = 0.7f;

        /// <summary>발이 계획된 보폭의 이만큼을 벗어나면 걸음이 시작됩니다.</summary>
        [Tooltip("발이 계획된 보폭의 이만큼을 벗어나면 걸음이 시작됩니다. 작을수록 종종걸음입니다.")]
        [Range(0.2f, 0.95f)]
        public float stepTriggerFraction = 0.5f;

        /// <summary>걸음 하나에 걸리는 시간의 <b>상한</b>입니다. 느리게 갈 때 이 값이 쓰입니다.</summary>
        [Tooltip("걸음 하나에 걸리는 시간의 상한(초). 느리게 갈 때 쓰입니다.")]
        public float stepDuration = 0.28f;

        /// <summary>아무리 빨라도 걸음이 이보다 짧아지지는 않습니다. 낼 수 있는 최고 속도를 정합니다.</summary>
        [Tooltip("걸음 시간의 하한(초). 이 값이 낼 수 있는 최고 속도를 정합니다.")]
        public float minStepDuration = 0.1f;

        /// <summary>발이 들리는 높이입니다.</summary>
        [Tooltip("발이 들리는 높이(m)")]
        public float stepHeight = 0.3f;

        /// <summary>
        /// 힘이 풀린 다리가 <b>얼마나 뻗은 채</b> 매달리는지입니다. 뻗을 수 있는 길이에 대한 비율입니다.
        /// 1에 가까우면 다리를 쭉 편 채 흔들리고, 작으면 접힌 채 매달립니다.
        /// </summary>
        [Tooltip("힘이 풀린 다리가 매달리는 길이. 뻗을 수 있는 길이에 대한 비율")]
        [Range(0.3f, 0.98f)]
        public float limpHangLength = 0.85f;

        /// <summary>힘이 풀린 다리에 걸리는 중력입니다. 음수가 아래입니다.</summary>
        [Tooltip("힘이 풀린 다리에 걸리는 중력(m/s²). 음수가 아래입니다.")]
        public float limpGravity = -14f;

        /// <summary>힘이 풀린 다리의 흔들림을 깎는 비율입니다. 0이면 영원히 흔들립니다.</summary>
        [Tooltip("힘이 풀린 다리의 흔들림을 깎는 비율 (0~1)")]
        [Range(0f, 0.4f)]
        public float limpDamping = 0.05f;

        /// <summary>일어날 때 발이 그리는 호의 높이 배율입니다. 1보다 크면 발을 더 크게 들어 옮깁니다.</summary>
        [Tooltip("일어날 때 발이 그리는 호의 높이 배율")]
        public float riseLiftScale = 1.3f;

        /// <summary>일어날 때 다리를 딛는 <b>차례</b>입니다.</summary>
        [Tooltip("일어날 때 다리를 딛는 차례")]
        public WalkerRiseOrder riseOrder = WalkerRiseOrder.LegOrder;

        /// <summary>
        /// 다리 하나를 옮기는 시간이 <b>다리 구간 전체</b>에서 차지하는 비율입니다.
        ///
        /// 1이면 모든 다리가 <b>동시에</b> 출발합니다. (차례가 사라집니다)
        /// 작을수록 하나가 거의 끝난 뒤에 다음이 출발해 <b>또박또박</b> 차례가 보입니다.
        /// 겹치는 정도를 정하는 값이라, 순서를 얼마나 뚜렷하게 보여 줄지가 여기서 갈립니다.
        /// </summary>
        [Tooltip("다리 하나를 옮기는 시간이 다리 구간에서 차지하는 비율. 1=전부 동시, 작을수록 또박또박")]
        [Range(0.2f, 1f)]
        public float riseStepShare = 0.6f;

        /// <summary>
        /// 발을 <b>얼마나 앞에</b> 내려놓을지입니다. 한 바퀴의 절반에 곱하는 비율입니다.
        /// 1이면 발이 제자리를 중심으로 <b>앞뒤 대칭</b>으로 오갑니다.
        /// </summary>
        [Tooltip("발을 얼마나 앞에 내려놓는가. 1이면 제자리를 중심으로 앞뒤 대칭입니다.")]
        [Range(0f, 1.5f)]
        public float stepPrediction = 1f;

        // --- Public Member Variables : 디버그 ---

        /// <summary>
        /// 다리가 <b>자기 몸통·다른 다리와 부딪히게</b> 할지 여부입니다.
        ///
        /// 켜면 같은 다리 안의 마디끼리만 무시하고 나머지는 전부 살립니다. 그러려면 리그에서
        /// <b>고관절이 몸통 상자 밖에</b> 있어야 합니다. 안에 박혀 있으면 서 있기만 해도 파고든 상태라,
        /// 다리가 매 프레임 몸통을 밀어내 로봇이 제자리에서 떨거나 스스로 날아갑니다.
        ///
        /// 손으로 만든 리그가 겹쳐 있다면 이것을 끄세요. 예전처럼 자기 콜라이더 쌍을 전부 무시합니다.
        /// </summary>
        [Header("리그 편집")]
        [Tooltip("편집 중 다리 하나를 고치면 나머지 다리도 따라옵니다. 재생 중에는 아무 일도 하지 않습니다.")]
        public WalkerRigSync rigSync = WalkerRigSync.MirrorPair;

        [Header("자기 충돌")]
        [Tooltip("다리가 자기 몸통·다른 다리와 부딪히게 합니다. 리그가 겹쳐 있으면 끄세요.")]
        public bool selfCollision = true;

        /// <summary>씬 뷰에 발 자리와 지지 다각형을 그릴지 여부입니다.</summary>
        [Header("디버그")]
        [Tooltip("씬 뷰에 발 자리와 지지 다각형을 그립니다")]
        public bool drawGizmos = true;

        // --- Private Member Variables ---

        /// <summary>몸통 위치를 따라가는 2차 시스템입니다.</summary>
        private SecondOrderDynamics3 bodyPositionMotion;

        /// <summary>몸통 회전을 따라가는 2차 시스템입니다.</summary>
        private SecondOrderRotation bodyRotationMotion;

        /// <summary>
        /// 충격으로 생긴 기울임입니다. X가 앞뒤(피치), Z가 좌우(롤)이고 단위는 도입니다.
        /// 목표는 언제나 0이라, 때리면 흔들리다 제자리로 돌아옵니다.
        /// </summary>
        private SecondOrderDynamics3 impactTiltMotion;

        /// <summary>
        /// 루트 속도를 부드럽게 만드는 2차 시스템입니다.
        /// 덤으로 이 시스템의 속도가 곧 <b>가속도</b>가 되어, 따로 잴 필요가 없습니다.
        /// </summary>
        private SecondOrderDynamics3 velocityFilter;

        /// <summary>지난 프레임의 루트 위치입니다.</summary>
        private Vector3 previousRootPosition;

        /// <summary>지난 프레임의 루트 방위각입니다.</summary>
        private float previousYaw;

        /// <summary>부드럽게 만든 루트 속도입니다.</summary>
        private Vector3 smoothVelocity;

        /// <summary>부드럽게 만든 루트 가속도입니다.</summary>
        private Vector3 smoothAcceleration;

        /// <summary>루트의 방위각 변화율(도/초)입니다.</summary>
        private float yawRate;

        /// <summary>지금 쓰고 있는 보행입니다.</summary>
        private WalkerGaitType activeGait;

        /// <summary>다리마다의 묶음 번호입니다.</summary>
        private int[] gaitGroups;

        /// <summary>묶음의 개수입니다. 한 바퀴를 이루는 걸음 수이기도 합니다.</summary>
        private int groupCount = 1;

        /// <summary>다리를 중심 둘레로 정렬한 순서입니다. 지지 평면과 기즈모가 이 순서로 잇습니다.</summary>
        private int[] ringOrder;

        /// <summary>다리별로 지금 발이 가고 싶은 자리입니다.</summary>
        private Vector3[] stepTargets;

        /// <summary>다리별로 그 자리의 지면 법선입니다.</summary>
        private Vector3[] stepNormals;

        /// <summary>다리별로 발이 <b>가려는 자리</b>에서 얼마나 떨어져 있는지입니다.</summary>
        private float[] strideErrors;

        /// <summary>다리별로 발이 <b>제자리</b>에서 얼마나 벗어났는지입니다. 작업 반경과 견주는 값입니다.</summary>
        private float[] homeDistances;

        /// <summary>배선이 온전한지입니다. 하나라도 어긋나면 아무 일도 하지 않습니다.</summary>
        private bool ready;

        /// <summary>땅을 못 찾는다고 이미 알렸는지입니다.</summary>
        private bool warnedAboutGround;

        /// <summary>다리 중 <b>가장 좁은</b> 작업 반경입니다. 한 다리라도 끌려나가면 눈에 띄므로 좁은 쪽을 씁니다.</summary>
        private float strideRadius = 0.3f;

        /// <summary>발이 서는 자리가 루트에서 수평으로 얼마나 떨어져 있는지입니다. 제자리 선회 속도를 환산할 때 씁니다.</summary>
        private float homeRadius = 1f;

        /// <summary>드라이버가 알려 준 <b>내려는 속도</b>입니다. 실측이 아니라 이 값으로 걸음을 계획합니다.</summary>
        private float plannedSpeed;

        /// <summary>이번 프레임에 땅을 찾은 발의 개수입니다.</summary>
        private int groundedFeet;

        /// <summary>딛고 있는 발들의 평균 높이입니다. 물리 모터가 몸을 떠받칠 목표로 씁니다.</summary>
        private float supportHeight;

        /// <summary>지금 어떤 상태의 몸인지입니다.</summary>
        private WalkerPosture posture = WalkerPosture.Standing;

        /// <summary>일어나기 시작한 뒤 흐른 시간입니다.</summary>
        private float riseTimer;

        /// <summary>일어나는 데 걸리는 전체 시간입니다.</summary>
        private float riseDuration = 1f;

        /// <summary>다리 하나를 옮기는 데 걸리는 시간입니다.</summary>
        private float riseStepTime = 0.3f;

        /// <summary>다리마다 발을 떼기 시작하는 시각입니다. 차례로 딛게 하려고 어긋나게 둡니다.</summary>
        private float[] riseDelays;

        /// <summary>다리마다 일어선 뒤 발이 놓일 자리입니다.</summary>
        private Vector3[] riseTargets;

        /// <summary>그 자리의 지면 법선입니다.</summary>
        private Vector3[] riseNormals;

        /// <summary>이 다리가 이미 발을 떼기 시작했는지입니다.</summary>
        private bool[] riseStarted;

        /// <summary>인스펙터에서 무언가 바뀌어 리그를 다시 읽어야 하는지입니다. 편집 중에만 씁니다.</summary>
        private bool editorRigDirty;

        /// <summary>지난 그리기 때의 넓적마디 길이입니다. 무엇이 바뀌었는지 알아내는 데 씁니다.</summary>
        private float[] syncUpper;

        /// <summary>지난 그리기 때의 종아리마디 길이입니다.</summary>
        private float[] syncLower;

        /// <summary>지난 그리기 때의 고관절 자리(몸통 기준)입니다.</summary>
        private Vector3[] syncHip;

        /// <summary>지난 그리기 때의 발자리입니다. 좌우 짝을 찾는 기준이기도 합니다.</summary>
        private Vector3[] syncHome;

        /// <summary>기억해 둔 모양이 쓸 만한지입니다. 처음 불러왔을 때는 비교할 것이 없습니다.</summary>
        private bool syncReady;

        /// <summary>다리마다 <b>몇 번째</b>로 딛을지입니다. 같은 번호끼리는 함께 나갑니다.</summary>
        private int[] riseSlots;

        /// <summary>차례를 정할 때 쓰는 정렬용 배열입니다. 할당을 피하려고 들고 있습니다.</summary>
        private int[] riseSorted;

        /// <summary>
        /// 힘이 풀린 다리를 흔드는 진자입니다. 다리마다 점 둘 — <b>붙은 자리와 발</b> — 뿐입니다.
        ///
        /// 붙은 자리를 매 프레임 몸에 고정하고 발은 중력에 맡기면, 거리 제약이 둘을 이어 주므로
        /// <b>단단한 막대에 매달린 추</b>가 됩니다. 몸이 구르면 발이 관성으로 뒤따라 휘둘립니다.
        /// 관절 물리를 세우지 않고도 "힘이 풀린 다리"가 나오는 가장 싼 방법입니다.
        /// </summary>
        private VerletChain[] limpLegs;

        /// <summary>진자를 지금 발 자리에서 이어받아야 하는지입니다. 쓰러지는 첫 프레임에만 참입니다.</summary>
        private bool limpNeedsHandover;

        // --- Constants ---

        /// <summary>기울기를 각도로 바꿀 때 기준으로 삼는 중력 가속도입니다.</summary>
        private const float Gravity = 9.81f;

        /// <summary>보행을 되돌릴 때 쓰는 이력 비율입니다. 문턱에서 보행이 딱딱 바뀌는 것을 막습니다.</summary>
        private const float GaitHysteresis = 0.85f;

        /// <summary>0으로 보는 제곱 길이입니다.</summary>
        private const float Epsilon = 1e-8f;

        /// <summary>다리가 이보다 적으면 걸을 수 없습니다.</summary>
        private const int MinLegCount = 2;

        /// <summary>리그가 달라졌다고 볼 최소 차이(m)입니다. 부동소수 잡음보다 크고 손으로 옮긴 것보다 작습니다.</summary>
        private const float RigEpsilon = 1e-4f;

        /// <summary>좌우 짝으로 볼 발자리 차이(m)입니다.</summary>
        private const float MirrorTolerance = 0.05f;

        // --- Public Properties ---

        /// <summary>다리의 개수입니다.</summary>
        public int LegCount { get { return legs != null ? legs.Length : 0; } }

        /// <summary>지금 루트가 나아가는 속도(m/s)입니다. 수평 성분만 봅니다.</summary>
        public float Speed { get { return new Vector2(smoothVelocity.x, smoothVelocity.z).magnitude; } }

        /// <summary>지금 쓰고 있는 보행입니다.</summary>
        public WalkerGaitType ActiveGait { get { return activeGait; } }

        /// <summary>부드럽게 만든 루트 속도입니다.</summary>
        public Vector3 SmoothVelocity { get { return smoothVelocity; } }

        /// <summary>다리 중 가장 좁은 작업 반경입니다.</summary>
        public float StrideRadius { get { return strideRadius; } }

        /// <summary>한 바퀴에 발이 앞뒤로 오가도록 <b>계획한</b> 거리입니다.</summary>
        public float PlannedStride { get { return 2f * strideRadius * strideUsage; } }

        /// <summary>딛고 있는 발들의 평균 높이입니다. 발을 하나도 못 찾았으면 루트 높이입니다.</summary>
        public float SupportHeight { get { return supportHeight; } }

        /// <summary>발이 하나라도 땅을 찾았는지입니다.</summary>
        public bool HasGround { get { return groundedFeet > 0; } }

        /// <summary>
        /// <b>다리가 따라올 수 있는 최고 속도</b>입니다. 이보다 빨리 밀면 발이 끌립니다.
        ///
        /// 자동 보행이 켜져 있으면 속도가 붙는 순간 교대보로 올라가므로 <b>교대보 기준</b>으로 계산합니다.
        /// (파도보 기준으로 묶으면 속도가 낮게 잡혀 교대보로 올라가지 못하는 굴레에 빠집니다)
        /// </summary>
        public float MaxTravelSpeed
        {
            get
            {
                int phases = autoGait && WalkerGait.IsMeaningful(WalkerGaitType.Alternate, LegCount) ? 2 : groupCount;

                return PlannedStride / Mathf.Max(minStepDuration * Mathf.Max(phases, 1), 0.001f);
            }
        }

        /// <summary>지금 어떤 상태의 몸인지입니다.</summary>
        public WalkerPosture Posture { get { return posture; } }

        /// <summary>서 있는 상태인지입니다. 아니면 쓰러졌거나 일어나는 중입니다.</summary>
        public bool IsStanding { get { return posture == WalkerPosture.Standing; } }

        // --- Unity Methods ---

        /// <summary>배선을 확인하고 다리 수에 맞춰 자리를 잡습니다.</summary>
        private void Awake()
        {
            if (legs == null || legs.Length < MinLegCount) legs = GetComponentsInChildren<WalkerLeg>();

            // 편집 중에는 <b>아직 배선이 끝나지 않았을 수</b> 있습니다. 컴포넌트를 갓 붙였거나
            // 도구가 조립하는 도중이면 몸통도 다리도 비어 있습니다. 그건 잘못이 아니라 과정이므로
            // 조용히 물러납니다. 인스펙터가 바뀌면 <see cref="OnValidate"/> 가 다시 부릅니다.
            if (!Application.isPlaying && (body == null || legs == null || legs.Length < MinLegCount))
            {
                ready = false;
                return;
            }

            ready = ValidateRig();
            if (!ready) return;

            int count = legs.Length;

            stepTargets = new Vector3[count];
            stepNormals = new Vector3[count];
            strideErrors = new float[count];
            homeDistances = new float[count];
            gaitGroups = new int[count];
            ringOrder = new int[count];
            riseDelays = new float[count];
            riseTargets = new Vector3[count];
            riseNormals = new Vector3[count];
            riseStarted = new bool[count];
            riseSlots = new int[count];
            riseSorted = new int[count];
            limpLegs = new VerletChain[count];

            strideRadius = float.MaxValue;
            homeRadius = 0f;

            for (int i = 0; i < count; i++)
            {
                if (!legs[i].Initialize(transform, strideScale))
                {
                    ready = false;
                    continue;
                }

                strideRadius = Mathf.Min(strideRadius, legs[i].StrideRadius);

                Vector3 home = legs[i].homeOffset;
                homeRadius = Mathf.Max(homeRadius, new Vector2(home.x, home.z).magnitude);
            }

            if (!ready)
            {
                strideRadius = 0.3f;
                return;
            }

            homeRadius = Mathf.Max(homeRadius, 0.01f);

            for (int i = 0; i < count; i++)
            {
                float hang = Mathf.Max(legs[i].MaxReach * limpHangLength, 0.05f);

                limpLegs[i] = new VerletChain(2, legs[i].HipPosition, Vector3.down * hang);
            }

            BuildRingOrder();

            activeGait = gait;
            groupCount = WalkerGait.Assign(activeGait, count, gaitGroups);

            bodyPositionMotion = new SecondOrderDynamics3(bodyPositionSpring, body.position);
            bodyRotationMotion = new SecondOrderRotation(bodyRotationSpring, body.rotation);
            impactTiltMotion = new SecondOrderDynamics3(impactSpring, Vector3.zero);
            velocityFilter = new SecondOrderDynamics3(new SecondOrderSettings(4f, 1f, 0f), Vector3.zero);

            previousRootPosition = transform.position;
            previousYaw = transform.eulerAngles.y;
            supportHeight = transform.position.y;
        }

        /// <summary>인스펙터에서 값이 바뀌면 다음 그리기 때 리그를 다시 읽습니다.</summary>
        private void OnValidate()
        {
            if (Application.isPlaying) return;

            editorRigDirty = true;
        }

        /// <summary>루트를 땅 위로 올리고, 발을 내려놓고, 몸통을 그 위에 세웁니다.</summary>
        private void Start()
        {
            // 편집 중에는 물리도 지면 탐침도 건드리지 않습니다. 자세는 LateUpdate 가 잡습니다.
            if (!Application.isPlaying) return;

            if (!ready) return;

            ConfigureSelfCollisions();

            if (snapToGroundOnStart) SnapRootToGround();

            PlantAllFeet();
            SnapBody();
        }

        /// <summary>클래스 설명의 여섯 단계를 순서대로 밟습니다.</summary>
        private void LateUpdate()
        {
            if (!Application.isPlaying)
            {
                PoseInEditor();
                return;
            }

            if (!ready) return;

            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            MeasureRootMotion(dt);

            switch (posture)
            {
                case WalkerPosture.Limp:
                    PoseLimp(dt);
                    return;

                case WalkerPosture.Rising:
                    PoseRising(dt);
                    return;
            }

            UpdateGait();
            UpdateFootTargets();
            ScheduleSteps();

            for (int i = 0; i < legs.Length; i++) legs[i].Advance(dt);

            PoseBody(dt);

            for (int i = 0; i < legs.Length; i++) legs[i].Solve();
        }

        /// <summary>발 자리 · 작업 반경 · 지지 다각형을 그립니다.</summary>
        private void OnDrawGizmos()
        {
            if (!drawGizmos || legs == null) return;

            for (int i = 0; i < legs.Length; i++)
            {
                if (legs[i] == null) continue;

                Vector3 home = transform.TransformPoint(legs[i].homeOffset);

                // 발이 서 있고 싶은 자리 — 노랑
                Gizmos.color = new Color(1f, 0.9f, 0.2f, 0.6f);
                Gizmos.DrawWireSphere(home, 0.06f);

                if (!ready || !Application.isPlaying) continue;

                // 발이 놓일 수 있는 범위 — 주황 원. 이 밖으로 나가면 다리가 뻗은 채 끌립니다.
                Gizmos.color = new Color(1f, 0.6f, 0.1f, 0.5f);
                DrawCircle(home, legs[i].StrideRadius);

                // 발이 지금 있는 자리 — 딛고 있으면 초록, 떼고 있으면 빨강
                Gizmos.color = legs[i].IsStepping ? new Color(1f, 0.3f, 0.2f) : new Color(0.3f, 1f, 0.4f);
                Gizmos.DrawSphere(legs[i].FootPosition, 0.05f);
                Gizmos.DrawLine(legs[i].HipPosition, legs[i].FootPosition);

                Gizmos.color = Color.white;
                Gizmos.DrawLine(legs[i].FootPosition, stepTargets[i]);
            }

            if (!ready || !Application.isPlaying || legs.Length < 3) return;

            // 지지 다각형 — 발을 둘레 순서로 이은 도형
            Gizmos.color = new Color(0.3f, 0.8f, 1f, 0.8f);

            for (int i = 0; i < ringOrder.Length; i++)
            {
                Vector3 a = legs[ringOrder[i]].FootPosition;
                Vector3 b = legs[ringOrder[(i + 1) % ringOrder.Length]].FootPosition;

                Gizmos.DrawLine(a, b);
            }
        }

        // --- Public Methods ---

        /// <summary>
        /// 몸의 상태를 바꿉니다. <see cref="RobotKnockdown"/> 이 부릅니다.
        ///
        /// <b>왜 로봇이 상태를 갖는가.</b> 쓰러진 로봇에게 "발이 제자리에서 밀려났으니 걸음을 시작해라"는
        /// 규칙은 뜻이 없습니다. 옆으로 누워 있으면 제자리라는 것 자체가 공중에 떠 있으니까요.
        /// 그렇다고 걸음 계산을 통째로 꺼 버리면 다리가 마지막 자세에서 굳어 <b>뻣뻣한 인형</b>이 됩니다.
        /// 그래서 상태마다 <b>다른 방식으로</b> 다리와 몸통을 놓습니다.
        /// </summary>
        /// <param name="value">바꿀 상태</param>
        public void SetPosture(WalkerPosture value)
        {
            if (posture == value) return;

            posture = value;

            // 늘어지기 시작하는 프레임에는 진자를 <b>지금 발이 있는 자리에서</b> 이어받아야 합니다.
            // 그러지 않으면 첫 프레임에 발이 진자의 기본 자리로 순간이동합니다.
            if (value == WalkerPosture.Limp) limpNeedsHandover = true;

            if (!ready || value != WalkerPosture.Standing) return;

            // 일어나는 동안 루트를 손으로 옮겼으므로, 그 이동이 <b>걸어간 것으로 잡히면</b>
            // 다시 서는 순간 엄청난 속도로 읽혀 네 다리가 한꺼번에 튀어 나갑니다.
            // 그래서 계측과 발을 그 자리에서 다시 시작합니다.
            previousRootPosition = transform.position;
            previousYaw = transform.eulerAngles.y;
            smoothVelocity = Vector3.zero;
            smoothAcceleration = Vector3.zero;
            yawRate = 0f;
            plannedSpeed = 0f;

            velocityFilter.Reset(Vector3.zero);
            impactTiltMotion.Reset(Vector3.zero);

            // 여기서 발을 다시 심지 않습니다. 일어나기가 끝난 시점의 발은 이미 제자리에 놓여 있고,
            // 다시 심으면 <b>마지막 순간에 네 발이 한꺼번에 순간이동</b>합니다.
            // (일어나는 동안 발이 옮겨지지 않는 것처럼 보이던 원인이 이것이었습니다)
        }

        /// <summary>
        /// 일어나기를 시작합니다. 다 섰을 때의 <b>루트 자세를 미리 받아</b> 발이 놓일 자리를 계산해 둡니다.
        ///
        /// <b>왜 미리 계산하는가.</b> 예전에는 매 프레임 <c>루트 기준 제자리</c>를 목표로 삼았습니다.
        /// 그런데 일어나는 동안 루트가 눕힌 자세에서 선 자세로 돌아가므로, 그 제자리가
        /// <b>거대한 호를 그리며 쓸려 다닙니다.</b> 발은 그 자리를 쫓다가 결국 끌려가듯 미끄러졌습니다.
        ///
        /// 다 섰을 때의 자리를 미리 정해 두면 목표가 <b>세계에 고정</b>되고, 발은 그 자리로
        /// 평소의 걸음과 같은 호를 그리며 옮겨 갑니다. 게다가 다리마다 시작 시각을 어긋나게 두므로
        /// <b>하나씩 차례로 딛는</b> 모습이 나옵니다. 넷이 동시에 움직이면 그것 자체가 순간이동처럼 보입니다.
        /// </summary>
        /// <param name="rootPosition">다 섰을 때의 루트 위치</param>
        /// <param name="rootRotation">다 섰을 때의 루트 자세</param>
        /// <param name="duration">일어나는 데 걸리는 전체 시간(초)</param>
        /// <param name="legWindow">그중 다리를 옮기는 데 쓰는 시간(초)</param>
        public void BeginRise(Vector3 rootPosition, Quaternion rootRotation, float duration, float legWindow)
        {
            if (!ready) return;

            riseTimer = 0f;
            riseDuration = Mathf.Max(duration, 0.05f);

            float window = Mathf.Clamp(legWindow, 0.05f, riseDuration);

            // 한 다리가 옮겨지는 시간입니다. 구간에서 차지하는 비율이 곧 겹치는 정도입니다.
            riseStepTime = Mathf.Max(window * Mathf.Clamp(riseStepShare, 0.2f, 1f), 0.05f);

            for (int i = 0; i < legs.Length; i++)
            {
                Vector3 home = rootPosition + rootRotation * legs[i].homeOffset;

                SampleGround(home, out riseTargets[i], out riseNormals[i]);

                riseStarted[i] = false;
            }

            // 차례를 정한 뒤, 남는 시간을 그 차례 수로 나눠 출발 시각을 벌립니다.
            int slots = BuildRiseSlots();
            float gap = slots > 1 ? Mathf.Max(window - riseStepTime, 0f) / (slots - 1) : 0f;

            for (int i = 0; i < legs.Length; i++) riseDelays[i] = gap * riseSlots[i];

            SetPosture(WalkerPosture.Rising);
        }

        /// <summary>
        /// 드라이버가 <b>이만큼 낼 작정</b>이라고 알려 줍니다. 걸음 박자를 여기에 맞춥니다.
        ///
        /// <b>왜 실측이 아닌가.</b> 출발하는 순간의 실측 속도는 0입니다. 그 값으로 첫 걸음을 계획하면
        /// 아주 느린 걸음이 잡히는데, 그 걸음이 진행되는 동안 몸은 이미 순항 속도로 가속합니다.
        /// 그래서 기다리던 나머지 다리들이 작업 반경 밖으로 끌려나갑니다.
        ///
        /// 선회는 바깥 발이 그리는 호의 속도로 환산합니다. 제자리에서 돌 때도 발은 실제로 그만큼
        /// 움직여야 하므로, 이것을 빼먹으면 <b>돌 때만 다리가 끌립니다.</b>
        /// </summary>
        /// <param name="linearSpeed">내려는 전진 속도(m/s)</param>
        /// <param name="angularSpeed">내려는 회전 속도(도/초)</param>
        public void PlanForMotion(float linearSpeed, float angularSpeed)
        {
            float spin = Mathf.Abs(angularSpeed) * Mathf.Deg2Rad * homeRadius;

            plannedSpeed = Mathf.Max(Mathf.Abs(linearSpeed), spin);
        }

        /// <summary>
        /// 몸통을 <b>휘청이게</b> 합니다. 얻어맞았을 때 부르면 됩니다.
        ///
        /// <b>왜 밀리는 것만으로는 부족한가.</b> <see cref="RobotPhysicsMotor"/> 가 리지드바디를 밀면
        /// 몸이 통째로 옮겨 가고 발이 따라붙습니다. 그런데 <b>자세는 그대로</b>입니다.
        /// 실제로 얻어맞은 것은 몸통인데 몸통이 꼿꼿하면 밀려난 것이지 맞은 것으로 보이지 않습니다.
        ///
        /// <b>기울어지는 방향은 미는 쪽과 반대입니다.</b> 스스로 가속할 때는 다리가 땅을 밀어
        /// 몸이 진행 방향으로 기울지만(<see cref="pitchIntoAccel"/>), 밖에서 맞으면 발은 그 자리에 있고
        /// 몸통만 밀려나므로 <b>맞은 쪽으로 젖혀집니다.</b> 두 기울임의 부호가 반대인 이유입니다.
        /// </summary>
        /// <param name="velocityChange">이 충격이 만든 속도 변화(m/s). 충격량을 질량으로 나눈 값입니다.</param>
        public void AddImpact(Vector3 velocityChange)
        {
            if (!ready) return;

            // 로봇이 보는 방향 기준으로 앞뒤·좌우 성분을 나눕니다.
            Vector3 local = transform.InverseTransformDirection(velocityChange);

            // 뒤에서 밀면(local.z 양수) 뒤로 젖혀지므로 피치는 음수입니다.
            // 오른쪽에서 밀면(local.x 양수) 왼쪽으로 기울므로 롤은 양수입니다.
            float pitch = -local.z * impactTiltPerSpeed;
            float roll = local.x * impactTiltPerSpeed;

            // 기울기(도)를 그 기울기가 나오게 하는 속도 충격으로 바꿉니다.
            // 이 변환 덕분에 impactTiltPerSpeed 가 "1m/s 에 몇 도"라는 눈에 보이는 단위가 되고,
            // 용수철을 느리게 바꿔도 기울기는 그대로인 채 흔들리는 시간만 길어집니다.
            float scale = SecondOrderDynamics.ImpulseForPeak(impactSpring, 1f);

            impactTiltMotion.AddVelocity(new Vector3(pitch, 0f, roll) * scale);
        }

        /// <summary>
        /// 로봇을 다른 자리로 옮깁니다. 발을 다시 심고 2차 시스템을 그 자리에서 정지시킵니다.
        /// 그냥 위치를 바꾸면 한 프레임 만에 그 거리를 걸어간 것이 되어 다리가 찢어집니다.
        /// </summary>
        /// <param name="position">옮길 자리</param>
        /// <param name="rotation">옮길 자세</param>
        public void Teleport(Vector3 position, Quaternion rotation)
        {
            transform.SetPositionAndRotation(position, rotation);

            previousRootPosition = position;
            previousYaw = rotation.eulerAngles.y;
            smoothVelocity = Vector3.zero;
            smoothAcceleration = Vector3.zero;
            yawRate = 0f;

            if (!ready) return;

            velocityFilter.Reset(Vector3.zero);
            impactTiltMotion.Reset(Vector3.zero);

            if (snapToGroundOnStart) SnapRootToGround();

            PlantAllFeet();
            SnapBody();
        }

        // --- Private Methods : 준비 ---

        /// <summary>다리와 몸통이 제자리에 있는지 봅니다.</summary>
        /// <returns>쓸 수 있으면 true</returns>
        private bool ValidateRig()
        {
            if (body == null)
            {
                GameLog.Error(GameLog.Channel.Enemy, name + ": 몸통(body)이 비어 있습니다. 자세를 잡을 대상이 없습니다.", this);
                return false;
            }

            if (body == transform)
            {
                GameLog.Error(GameLog.Channel.Enemy, name + ": 몸통은 루트와 달라야 합니다. 루트는 드라이버가 움직입니다.", this);
                return false;
            }

            if (legs == null || legs.Length < MinLegCount)
            {
                GameLog.Error(GameLog.Channel.Enemy, name + ": 다리가 " + MinLegCount + "개 이상이어야 합니다. 지금 " +
                    (legs == null ? 0 : legs.Length) + "개입니다.", this);
                return false;
            }

            for (int i = 0; i < legs.Length; i++)
            {
                if (legs[i] != null) continue;

                GameLog.Error(GameLog.Channel.Enemy, name + ": " + i + "번 다리가 비어 있습니다.", this);
                return false;
            }

            return true;
        }

        /// <summary>
        /// 다리를 <b>중심 둘레 순서</b>로 정렬해 둡니다.
        ///
        /// 지지 평면을 뉴웰 방식으로 구하려면 발이 다각형의 둘레를 따라 이어져 있어야 합니다.
        /// 배선 순서(앞→뒤, 왼→오른)는 지그재그라 그대로 이으면 8자가 되어 법선이 뒤집힙니다.
        /// 제자리의 각도로 한 번 정렬해 두면 다리 수가 몇이든 올바른 둘레가 됩니다.
        /// </summary>
        private void BuildRingOrder()
        {
            for (int i = 0; i < ringOrder.Length; i++) ringOrder[i] = i;

            // 다리가 많아야 여섯 개라 삽입 정렬이면 충분하고 할당도 없습니다.
            for (int i = 1; i < ringOrder.Length; i++)
            {
                int current = ringOrder[i];
                float key = HomeAngle(current);
                int j = i - 1;

                while (j >= 0 && HomeAngle(ringOrder[j]) > key)
                {
                    ringOrder[j + 1] = ringOrder[j];
                    j--;
                }

                ringOrder[j + 1] = current;
            }
        }

        /// <summary>이 다리의 제자리가 루트 중심에서 이루는 각도입니다.</summary>
        /// <param name="leg">다리 번호</param>
        /// <returns>각도(라디안)</returns>
        private float HomeAngle(int leg)
        {
            Vector3 home = legs[leg].homeOffset;

            return Mathf.Atan2(home.x, home.z);
        }

        /// <summary>
        /// 자기 몸끼리의 충돌을 정리합니다.
        ///
        /// <b>같은 다리 안의 마디끼리는 반드시 끕니다.</b> 넓적마디와 종아리마디는 관절에서 <b>언제나</b>
        /// 맞닿아 있어, 켜 두면 영원히 서로를 밀어내며 떱니다. 구조적으로 겹치는 것은 끄는 것이 맞습니다.
        ///
        /// 나머지(다리 ↔ 몸통, 다리 ↔ 다른 다리)는 <b>살려 둡니다.</b> 고관절을 몸통 상자 밖에 달았기
        /// 때문에 서 있을 때는 닿지 않고, 쓰러져 다리가 접힐 때만 닿습니다. 그때 다리가 몸을 받치는
        /// 것은 없애야 할 버그가 아니라 <b>있어야 할 움직임</b>입니다.
        ///
        /// <see cref="selfCollision"/> 을 끄면 예전처럼 모든 쌍을 무시합니다.
        /// 레이어를 새로 파고 충돌 행렬을 고치는 방법도 있지만 그것은 프로젝트 전역 설정입니다.
        /// <see cref="Physics.IgnoreCollision"/> 는 이 로봇에만 걸립니다.
        /// </summary>
        private void ConfigureSelfCollisions()
        {
            if (!selfCollision)
            {
                IgnorePairs(GetComponentsInChildren<Collider>());
                return;
            }

            for (int i = 0; i < legs.Length; i++)
            {
                IgnorePairs(legs[i].GetComponentsInChildren<Collider>());
            }
        }

        /// <summary>주어진 콜라이더들이 서로 부딪히지 않게 합니다.</summary>
        /// <param name="colliders">끌 콜라이더들</param>
        private static void IgnorePairs(Collider[] colliders)
        {
            for (int i = 0; i < colliders.Length; i++)
            {
                for (int j = i + 1; j < colliders.Length; j++)
                {
                    Physics.IgnoreCollision(colliders[i], colliders[j], true);
                }
            }
        }

        /// <summary>루트를 지면 위로 올립니다.</summary>
        private void SnapRootToGround()
        {
            if (!GroundProbe.Sample(transform.position, groundProbeUp, groundProbeDown, groundMask,
                    out Vector3 point, out Vector3 _))
            {
                return;
            }

            transform.position = point;
            previousRootPosition = point;
        }

        /// <summary>
        /// 모든 발을 각자의 자리 아래 땅에 내려놓습니다.
        ///
        /// 지지 높이도 여기서 함께 채웁니다. 물리 모터는 <see cref="FixedUpdate"/> 에서 도는데
        /// 발 자리는 <see cref="LateUpdate"/> 에서 갱신되므로, <b>첫 물리 프레임에는 아직 값이 없습니다.</b>
        /// 비워 두면 로봇이 한두 프레임 동안 지지를 받지 못해 주저앉습니다.
        /// </summary>
        private void PlantAllFeet()
        {
            groundedFeet = 0;
            float plantedSum = 0f;

            for (int i = 0; i < legs.Length; i++)
            {
                Vector3 home = transform.TransformPoint(legs[i].homeOffset);

                if (SampleGround(home, out Vector3 point, out Vector3 normal)) groundedFeet++;

                stepTargets[i] = point;
                stepNormals[i] = normal;
                legs[i].PlaceAt(point, normal);

                plantedSum += point.y;
            }

            supportHeight = plantedSum / legs.Length;
        }

        /// <summary>몸통을 발 위에 즉시 세웁니다.</summary>
        private void SnapBody()
        {
            ComputeStance(out Vector3 planted, out Vector3 bob, out Vector3 normal);

            Vector3 position = planted + normal * standHeight + bob * bodyBob;
            Quaternion rotation = StanceRotation(normal, 0f, 0f);

            body.SetPositionAndRotation(position, rotation);

            bodyPositionMotion.Reset(position);
            bodyRotationMotion.Reset(rotation);

            for (int i = 0; i < legs.Length; i++) legs[i].Solve();
        }

        // --- Private Methods : 계측 ---

        /// <summary>루트가 이번 프레임에 얼마나 움직였는지 잽니다.</summary>
        /// <param name="dt">시간 간격(초)</param>
        private void MeasureRootMotion(float dt)
        {
            Vector3 rawVelocity = (transform.position - previousRootPosition) / dt;
            previousRootPosition = transform.position;

            smoothVelocity = velocityFilter.Update(dt, rawVelocity);
            smoothAcceleration = velocityFilter.Velocity;

            float yaw = transform.eulerAngles.y;
            yawRate = Mathf.DeltaAngle(previousYaw, yaw) / dt;
            previousYaw = yaw;
        }

        /// <summary>속도에 따라 보행을 고릅니다. 문턱마다 이력을 두어 딱딱 바뀌지 않게 합니다.</summary>
        private void UpdateGait()
        {
            WalkerGaitType desired = activeGait;

            if (!autoGait)
            {
                desired = gait;
            }
            else
            {
                float speed = PlanningSpeed;

                if (activeGait == WalkerGaitType.Alternate)
                {
                    if (speed < alternateSpeed * GaitHysteresis) desired = WalkerGaitType.Wave;
                }
                else if (speed > alternateSpeed)
                {
                    desired = WalkerGaitType.Alternate;
                }
            }

            if (desired == activeGait) return;

            activeGait = desired;
            groupCount = WalkerGait.Assign(activeGait, legs.Length, gaitGroups);
        }

        /// <summary>걸음을 계획할 때 쓰는 속도입니다. 실측과 <b>내려는 속도</b> 중 큰 쪽입니다.</summary>
        private float PlanningSpeed { get { return Mathf.Max(Speed, plannedSpeed); } }

        // --- Private Methods : 발 ---

        /// <summary>
        /// 발마다 지금 가고 싶은 자리를 찾습니다.
        ///
        /// <b>예측이 핵심입니다.</b> 발이 착지하는 것은 지금이 아니라 걸음 시간 뒤입니다.
        /// 지금 자리로 발을 보내면 착지하는 순간에는 이미 몸이 지나가 있어, 발이 항상
        /// <b>뒤에서 끌려오는</b> 모습이 됩니다. 회전을 빼먹으면 제자리에서 돌 때만 발이 끌립니다.
        /// </summary>
        private void UpdateFootTargets()
        {
            float cycle = CurrentStepDuration() * groupCount;
            float predict = cycle * 0.5f * stepPrediction;

            Quaternion turn = Quaternion.AngleAxis(yawRate * predict, Vector3.up);
            Vector3 drift = smoothVelocity * predict;

            groundedFeet = 0;
            float plantedSum = 0f;

            for (int i = 0; i < legs.Length; i++)
            {
                Vector3 home = transform.TransformPoint(legs[i].homeOffset);
                Vector3 predicted = transform.position + turn * (home - transform.position) + drift;

                // 이 다리가 실제로 닿을 수 있는 원 안으로 자릅니다.
                float limit = legs[i].StrideRadius;
                Vector3 offset = predicted - home;
                if (offset.sqrMagnitude > limit * limit) predicted = home + offset.normalized * limit;

                if (SampleGround(predicted, out Vector3 point, out Vector3 normal)) groundedFeet++;

                stepTargets[i] = point;
                stepNormals[i] = normal;
                strideErrors[i] = Vector3.Distance(legs[i].FootPosition, point);
                homeDistances[i] = Vector3.Distance(legs[i].FootPosition, home);

                plantedSum += FootGround(i).y;
            }

            supportHeight = plantedSum / legs.Length;
        }

        /// <summary>
        /// 어느 묶음이 나갈지 정합니다.
        ///
        /// <b>모든 발이 땅에 있을 때만</b> 새 걸음이 시작됩니다. 그래야 한 묶음이 통째로 함께
        /// 출발해 함께 착지하므로 <b>묶음이 어긋난 채 굳는 일</b>이 없습니다.
        /// 예전에는 "자기 짝을 뺀 나머지가 땅에 있으면" 떼게 했는데, 보행이 바뀌는 순간
        /// 짝이 서로 다른 위상에 놓이면 둘 중 하나가 언제나 공중에 있게 되어
        /// <b>나머지 다리가 한 걸음도 떼지 못했습니다.</b>
        /// (시뮬레이션에서 10초 동안 두 다리는 98걸음, 나머지 둘은 0걸음이었습니다)
        /// </summary>
        private void ScheduleSteps()
        {
            if (!AllFeetPlanted())
            {
                RescueStretchedLeg();
                return;
            }

            int bestGroup = -1;
            float bestScore = 1f;

            for (int i = 0; i < legs.Length; i++)
            {
                // 다리마다 작업 반경이 다를 수 있으므로 비율로 견줍니다.
                float score = strideErrors[i] / StepTrigger(i);
                if (score <= bestScore) continue;

                bestGroup = gaitGroups[i];
                bestScore = score;
            }

            if (bestGroup < 0) return;

            for (int i = 0; i < legs.Length; i++)
            {
                if (gaitGroups[i] == bestGroup) BeginStep(i);
            }
        }

        /// <summary>모든 발이 땅에 있는지 봅니다.</summary>
        /// <returns>모두 땅에 있으면 true</returns>
        private bool AllFeetPlanted()
        {
            for (int i = 0; i < legs.Length; i++)
            {
                if (legs[i].IsStepping) return false;
            }

            return true;
        }

        /// <summary>
        /// 보행 표가 막고 있는 사이에 <b>작업 반경 밖으로 끌려나간</b> 다리를 구합니다.
        ///
        /// 걸음 시간을 기하에서 뽑고 계획 속도로 미리 맞추므로 보통은 여기까지 오지 않습니다.
        /// 그래도 지형이 갑자기 꺼지거나 누군가 로봇을 밀면 생길 수 있고, 그때는
        /// <b>보행의 규칙보다 다리가 찢어지지 않는 것</b>이 우선입니다. 다만 두 발은 남깁니다.
        /// </summary>
        private void RescueStretchedLeg()
        {
            int planted = 0;
            for (int i = 0; i < legs.Length; i++)
            {
                if (!legs[i].IsStepping) planted++;
            }

            if (planted < 2) return;

            int worst = -1;
            float worstScore = 1f;

            for (int i = 0; i < legs.Length; i++)
            {
                if (legs[i].IsStepping) continue;

                // 목표까지의 거리가 아니라 <b>제자리에서 벗어난 거리</b>로 봅니다.
                float score = homeDistances[i] / Mathf.Max(legs[i].StrideRadius, 0.01f);
                if (score <= worstScore) continue;

                worst = i;
                worstScore = score;
            }

            if (worst < 0) return;

            BeginStep(worst);
        }

        /// <summary>이 다리가 걸음을 시작하는 문턱 거리입니다.</summary>
        /// <param name="leg">다리 번호</param>
        /// <returns>문턱 거리(m)</returns>
        private float StepTrigger(int leg)
        {
            return Mathf.Max(legs[leg].StrideRadius * strideUsage * stepTriggerFraction, 0.01f);
        }

        /// <summary>한 다리의 걸음을 시작합니다. 많이 밀려난 걸음일수록 발을 높이 듭니다.</summary>
        /// <param name="leg">다리 번호</param>
        private void BeginStep(int leg)
        {
            if (legs[leg].IsStepping) return;

            float reach = Mathf.Max(strideErrors[leg] / StepTrigger(leg), 0.5f);
            float height = stepHeight * Mathf.Min(reach, 1.8f);

            legs[leg].BeginStep(stepTargets[leg], stepNormals[leg], CurrentStepDuration(), height);
        }

        /// <summary>
        /// 지금 속도에서 걸음 하나에 걸릴 시간입니다. <b>다리 기하가 정합니다.</b>
        /// 발이 제자리 앞뒤로 오갈 수 있는 거리를 지금 속도로 지나가는 시간이 한 바퀴이고,
        /// 그것을 묶음 수로 나누면 걸음 하나입니다.
        /// </summary>
        /// <returns>걸음 시간(초)</returns>
        private float CurrentStepDuration()
        {
            float speed = Mathf.Max(PlanningSpeed, 0.01f);
            float cycle = PlannedStride / speed;

            return Mathf.Clamp(cycle / Mathf.Max(groupCount, 1), minStepDuration, stepDuration);
        }

        /// <summary>어떤 자리 아래의 땅을 찾습니다. 못 찾으면 한 번만 알리고 그 자리를 그대로 씁니다.</summary>
        /// <param name="around">찾을 자리</param>
        /// <param name="point">찾은 지면 위치</param>
        /// <param name="normal">찾은 지면 법선</param>
        /// <returns>땅을 찾았으면 true</returns>
        private bool SampleGround(Vector3 around, out Vector3 point, out Vector3 normal)
        {
            if (GroundProbe.Sample(around, groundProbeUp, groundProbeDown, groundMask, out point, out normal)) return true;

            if (warnedAboutGround) return false;

            warnedAboutGround = true;
            GameLog.Warn(GameLog.Channel.Enemy, name + ": 발 아래에서 땅을 찾지 못했습니다. " +
                "레이어 마스크에 지면이 들어 있는지, 로봇이 터레인 범위 안에 있는지 확인하세요.", this);

            return false;
        }

        // --- Private Methods : 자세 ---

        /// <summary>
        /// 발들에서 몸통이 있어야 할 자리와 기울기를 뽑습니다.
        ///
        /// <b>평면은 뉴웰 방식으로 구합니다.</b> 다리가 셋이면 평면이 하나로 정해지지만 넷 이상이면
        /// 네 점이 한 평면에 있지 않습니다. 뉴웰 방식은 <b>둘레를 한 바퀴 돌며</b> 법선을 누적하므로
        /// 점이 몇 개든, 어긋나 있어도 가장 그럴듯한 평면을 줍니다.
        /// (다리가 둘뿐이면 평면이 정해지지 않으므로 세계의 위쪽을 씁니다)
        /// </summary>
        /// <param name="planted">발이 딛고 있는 자리들의 평균 (들린 높이를 뺀 값)</param>
        /// <param name="bob">들린 발 때문에 생기는 위아래 치우침</param>
        /// <param name="normal">발들이 만드는 평면의 법선</param>
        private void ComputeStance(out Vector3 planted, out Vector3 bob, out Vector3 normal)
        {
            Vector3 plantedSum = Vector3.zero;
            Vector3 actualSum = Vector3.zero;

            for (int i = 0; i < legs.Length; i++)
            {
                plantedSum += FootGround(i);
                actualSum += legs[i].FootPosition;
            }

            planted = plantedSum / legs.Length;
            bob = actualSum / legs.Length - planted;

            normal = Vector3.up;

            if (legs.Length >= 3)
            {
                Vector3 accumulated = Vector3.zero;

                for (int i = 0; i < ringOrder.Length; i++)
                {
                    Vector3 a = FootGround(ringOrder[i]);
                    Vector3 b = FootGround(ringOrder[(i + 1) % ringOrder.Length]);

                    accumulated.x += (a.y - b.y) * (a.z + b.z);
                    accumulated.y += (a.z - b.z) * (a.x + b.x);
                    accumulated.z += (a.x - b.x) * (a.y + b.y);
                }

                if (accumulated.sqrMagnitude > Epsilon) normal = accumulated.normalized;
            }

            // 둘레를 도는 방향에 따라 법선이 뒤집힐 수 있습니다. 위쪽으로 맞춥니다.
            if (Vector3.Dot(normal, Vector3.up) < 0f) normal = -normal;
        }

        /// <summary>발이 들린 높이를 뺀, 그 발이 딛고 있던 자리입니다.</summary>
        /// <param name="leg">다리 번호</param>
        /// <returns>지면에 닿아 있는 것으로 본 위치</returns>
        private Vector3 FootGround(int leg)
        {
            return legs[leg].FootPosition - legs[leg].FootNormal * legs[leg].Lift;
        }

        /// <summary>발 평면과 기울임에서 몸통의 목표 회전을 만듭니다.</summary>
        /// <param name="normal">발 평면의 법선</param>
        /// <param name="pitch">앞뒤 기울임(도)</param>
        /// <param name="roll">좌우 기울임(도)</param>
        /// <returns>목표 회전</returns>
        private Quaternion StanceRotation(Vector3 normal, float pitch, float roll)
        {
            Vector3 forward = Vector3.ProjectOnPlane(transform.forward, normal);
            if (forward.sqrMagnitude < Epsilon) forward = transform.forward;

            return Quaternion.LookRotation(forward.normalized, normal) * Quaternion.Euler(pitch + standPitch, 0f, roll);
        }

        /// <summary>
        /// 몸통을 발 위에 올립니다.
        ///
        /// <b>기울임은 관성의 흔적입니다.</b> 선회하면 원심력의 반대쪽으로 몸을 기울이고,
        /// 가속하면 앞으로 숙입니다. 둘 다 가속도를 중력으로 나눈 값이라
        /// <b>1g 만큼 가속하면 45°</b> 라는 기준이 그대로 성립합니다.
        /// </summary>
        /// <param name="dt">시간 간격(초)</param>
        private void PoseBody(float dt)
        {
            ComputeStance(out Vector3 planted, out Vector3 bob, out Vector3 normal);

            Vector3 targetPosition = planted + normal * standHeight + bob * bodyBob;

            // 선회 가속도 = 각속도 × 속도. 오른쪽으로 돌면 양수입니다.
            float centripetal = yawRate * Mathf.Deg2Rad * Speed;
            float roll = -Mathf.Clamp(centripetal / Gravity, -1f, 1f) * leanIntoTurn;

            Vector3 forward = Vector3.ProjectOnPlane(transform.forward, normal).normalized;
            float pitch = Mathf.Clamp(Vector3.Dot(smoothAcceleration, forward) / Gravity, -1f, 1f) * pitchIntoAccel;

            // 충격으로 생긴 기울임을 얹습니다. 목표가 0이라 때리지 않으면 아무것도 더하지 않습니다.
            Vector3 tilt = impactTiltMotion.Update(dt, Vector3.zero);
            tilt = Vector3.ClampMagnitude(tilt, impactMaxTilt);

            Quaternion targetRotation = StanceRotation(normal, pitch + tilt.x, roll + tilt.z);

            body.SetPositionAndRotation(
                bodyPositionMotion.Update(dt, targetPosition),
                bodyRotationMotion.Update(dt, targetRotation));
        }

        /// <summary>
        /// <b>재생하지 않고도</b> 리그가 제 자세로 서 있게 합니다.
        ///
        /// 관절 하나를 끌어 옮기면 나머지가 따라오게 하려는 것입니다. 그러려면 두 가지가 필요합니다.
        ///  1. 옮긴 결과를 <b>마디 길이로 받아들이기</b> (<see cref="WalkerLeg.MeasureFromRig"/>)
        ///  2. 그 길이로 <b>IK 를 다시 풀어</b> 아래 마디들을 제자리에 놓기
        /// 그래서 무릎을 당기면 종아리마디가 따라 늘고, 고관절을 옮기면 다리 전체가 따라옵니다.
        ///
        /// <b>스프링은 돌리지 않습니다.</b> 2차 시스템은 시간이 지나야 목표에 닿는데 편집 중에는
        /// 시간이 흐르지 않습니다. 대신 몸통을 곧장 서 있는 높이에 놓습니다. 지면 탐침도 하지 않습니다 —
        /// 편집 중에는 발밑에 지형이 없을 수도 있고, 매 그리기마다 레이를 쏘면 씬이 무거워집니다.
        ///
        /// 계산이 언제나 같은 답을 내므로, 한 번 자리를 잡은 뒤에는 같은 값을 다시 쓸 뿐입니다.
        /// 그래서 가만히 두면 씬이 계속 더러워지지 않습니다.
        /// </summary>
        private void PoseInEditor()
        {
            if (editorRigDirty)
            {
                editorRigDirty = false;
                Awake();
            }

            if (!ready || body == null) return;

            for (int i = 0; i < legs.Length; i++)
            {
                if (legs[i] == null || !legs[i].IsWired) return;
            }

            body.SetPositionAndRotation(transform.TransformPoint(new Vector3(0f, standHeight, 0f)),
                transform.rotation * Quaternion.Euler(standPitch, 0f, 0f));

            for (int i = 0; i < legs.Length; i++) legs[i].MeasureFromRig();

            SyncRig();

            for (int i = 0; i < legs.Length; i++)
            {
                legs[i].Initialize(transform, strideScale);
                legs[i].PlaceAt(transform.TransformPoint(legs[i].homeOffset), transform.up);
                legs[i].Solve();
            }
        }

        /// <summary>
        /// <b>고친 다리 하나를 찾아 나머지에 옮깁니다.</b>
        ///
        /// 다리를 넷 만들면서 같은 값을 네 번 넣는 것은 실수가 나기 쉽고, 한 번 어긋나면
        /// 걸음이 미묘하게 절뚝이는데 눈으로는 어느 다리가 다른지 알기 어렵습니다.
        /// 그래서 <b>하나만 고치면 나머지가 따라오게</b> 합니다.
        ///
        /// <b>누가 본인지는 달라진 것으로 알아냅니다.</b> 지난 그리기 때의 모양을 기억해 두었다가
        /// 이번에 달라진 다리를 찾습니다. 그 다리가 방금 손댄 다리입니다. 어느 다리를 고쳐도
        /// 되므로 "0번 다리만 고치세요" 같은 규칙을 외울 필요가 없습니다.
        ///
        /// <b>여러 개가 한꺼번에 달라졌으면 손대지 않습니다.</b> 씬을 막 열었거나 프리팹을 다시
        /// 구운 직후가 그렇습니다. 그때 옮기면 일부러 만든 비대칭을 뭉개 버립니다.
        /// 기억만 새로 해 두고 넘어갑니다.
        /// </summary>
        private void SyncRig()
        {
            if (rigSync == WalkerRigSync.Off) return;

            int count = legs.Length;

            if (syncUpper == null || syncUpper.Length != count)
            {
                syncUpper = new float[count];
                syncLower = new float[count];
                syncHip = new Vector3[count];
                syncHome = new Vector3[count];
                syncReady = false;
            }

            if (!syncReady)
            {
                CacheRig();
                syncReady = true;
                return;
            }

            int master = -1;
            int changed = 0;

            for (int i = 0; i < count; i++)
            {
                if (!LegChanged(i)) continue;

                master = i;
                changed++;
            }

            if (changed != 1)
            {
                if (changed > 1) CacheRig();
                return;
            }

            if (rigSync == WalkerRigSync.AllLegs)
            {
                for (int i = 0; i < count; i++)
                {
                    if (i == master) continue;

                    legs[i].CopyShapeFrom(legs[master]);
                    MarkEdited(legs[i]);
                }
            }

            int partner = MirrorPartner(master);

            if (partner >= 0)
            {
                legs[partner].CopyShapeFrom(legs[master]);
                MirrorInto(master, partner);
            }

            CacheRig();
        }

        /// <summary>이 다리가 지난 그리기 때와 달라졌는지입니다.</summary>
        /// <param name="leg">다리 번호</param>
        /// <returns>달라졌으면 true</returns>
        private bool LegChanged(int leg)
        {
            if (Mathf.Abs(legs[leg].upperLength - syncUpper[leg]) > RigEpsilon) return true;
            if (Mathf.Abs(legs[leg].lowerLength - syncLower[leg]) > RigEpsilon) return true;
            if ((legs[leg].transform.localPosition - syncHip[leg]).sqrMagnitude > RigEpsilon * RigEpsilon) return true;

            return (legs[leg].homeOffset - syncHome[leg]).sqrMagnitude > RigEpsilon * RigEpsilon;
        }

        /// <summary>지금 모양을 기억해 둡니다.</summary>
        private void CacheRig()
        {
            for (int i = 0; i < legs.Length; i++)
            {
                syncUpper[i] = legs[i].upperLength;
                syncLower[i] = legs[i].lowerLength;
                syncHip[i] = legs[i].transform.localPosition;
                syncHome[i] = legs[i].homeOffset;
            }
        }

        /// <summary>
        /// 이 다리의 <b>좌우 짝</b>을 찾습니다. 발자리가 Z 는 같고 X 는 부호만 다른 다리입니다.
        ///
        /// <b>바뀌기 전</b>의 발자리로 찾습니다. 본은 방금 옮겨졌으므로 지금 값으로 찾으면
        /// 짝을 놓칩니다. 가운데에 있는 다리(스트라이더의 뒷다리)는 짝이 없습니다.
        /// </summary>
        /// <param name="master">본이 되는 다리 번호</param>
        /// <returns>짝의 번호. 없으면 -1</returns>
        private int MirrorPartner(int master)
        {
            Vector3 was = syncHome[master];

            if (Mathf.Abs(was.x) < MirrorTolerance) return -1;

            for (int i = 0; i < legs.Length; i++)
            {
                if (i == master) continue;
                if (Mathf.Abs(syncHome[i].x + was.x) > MirrorTolerance) continue;
                if (Mathf.Abs(syncHome[i].z - was.z) > MirrorTolerance) continue;

                return i;
            }

            return -1;
        }

        /// <summary>본이 되는 다리의 자리를 짝에게 X 대칭으로 옮깁니다.</summary>
        /// <param name="master">본이 되는 다리 번호</param>
        /// <param name="partner">짝의 번호</param>
        private void MirrorInto(int master, int partner)
        {
            Vector3 hip = legs[master].transform.localPosition;
            Vector3 home = legs[master].homeOffset;
            Vector3 pole = legs[master].kneePole;

            legs[partner].transform.localPosition = new Vector3(-hip.x, hip.y, hip.z);
            legs[partner].homeOffset = new Vector3(-home.x, home.y, home.z);
            legs[partner].kneePole = new Vector3(-pole.x, pole.y, pole.z);

            MarkEdited(legs[partner]);
        }

        /// <summary>편집 중에 고친 값이 저장되도록 표시합니다.</summary>
        /// <param name="leg">표시할 다리</param>
        private static void MarkEdited(WalkerLeg leg)
        {
#if UNITY_EDITOR
            if (leg != null) UnityEditor.EditorUtility.SetDirty(leg);
#endif
        }

        /// <summary>
        /// 쓰러진 몸입니다. <b>다리는 늘어지고 몸통은 루트를 그대로 따릅니다.</b>
        ///
        /// 발을 세계에 고정해 두지 않는 것이 핵심입니다. 굴러가는 몸에 발만 땅에 박혀 있으면
        /// 다리가 고무줄처럼 늘어납니다. 대신 <b>다리가 붙은 자리에서 중력 쪽으로 처지게</b> 두면
        /// 관절이 풀린 것처럼 보입니다.
        ///
        /// 몸통은 루트의 제자리(선 자세의 로컬 위치)를 목표로 두되 <b>같은 2차 시스템을 통과</b>시킵니다.
        /// 그래야 쓰러지기 시작하는 순간 자세가 뚝 끊기지 않고 흘러갑니다.
        /// </summary>
        /// <param name="dt">시간 간격(초)</param>
        private void PoseLimp(float dt)
        {
            for (int i = 0; i < legs.Length; i++)
            {
                VerletChain chain = limpLegs[i];

                // 첫 프레임에는 지금 발이 있는 자리를 속도 없이 이어받습니다.
                if (limpNeedsHandover) chain.Place(1, legs[i].FootPosition);

                // 붙은 자리를 몸에 고정합니다. 다리가 흔들리는 <b>힘의 원천은 이것 하나</b>입니다.
                chain.Pin(0, legs[i].HipPosition);

                chain.Step(dt, Vector3.up * limpGravity, limpDamping);
                chain.Solve(2, 180f);

                // 발이 땅을 파고들지 않게 합니다. 늘어진 동안에만 하므로 비용은 문제되지 않습니다.
                if (GroundProbe.Sample(chain.GetPosition(1), groundProbeUp, groundProbeDown, groundMask,
                        out Vector3 ground, out Vector3 groundNormal))
                {
                    chain.CollideWithPlane(ground, groundNormal, 0.6f);
                }

                legs[i].PlaceAt(chain.GetPosition(1), Vector3.up);
            }

            limpNeedsHandover = false;

            Vector3 target = transform.TransformPoint(new Vector3(0f, standHeight, 0f));

            body.SetPositionAndRotation(
                bodyPositionMotion.Update(dt, target),
                bodyRotationMotion.Update(dt, transform.rotation));

            for (int i = 0; i < legs.Length; i++) legs[i].Solve();
        }

        /// <summary>
        /// 다리마다 <b>몇 번째로 딛을지</b>를 정하고, 서로 다른 차례가 몇 개인지 돌려줍니다.
        ///
        /// 같은 번호를 받은 다리는 <b>함께</b> 나갑니다. 그래서 보행 묶음을 쓰면 4족이 대각선 둘씩
        /// 딛고, 차례 수는 넷이 아니라 둘이 됩니다. 남는 시간을 그 수로 나누므로
        /// <b>차례가 적을수록 한 번에 더 여유 있게</b> 딛습니다.
        /// </summary>
        /// <returns>서로 다른 차례의 개수</returns>
        private int BuildRiseSlots()
        {
            switch (riseOrder)
            {
                case WalkerRiseOrder.GaitGroups:
                    for (int i = 0; i < legs.Length; i++) riseSlots[i] = gaitGroups[i];
                    return Mathf.Max(groupCount, 1);

                case WalkerRiseOrder.LowestFirst:
                    SortByFootHeight();
                    for (int rank = 0; rank < riseSorted.Length; rank++) riseSlots[riseSorted[rank]] = rank;
                    return legs.Length;

                default:
                    for (int i = 0; i < legs.Length; i++) riseSlots[i] = i;
                    return legs.Length;
            }
        }

        /// <summary>발 높이가 낮은 다리부터 오도록 번호를 정렬합니다. (다리가 여섯을 넘지 않으므로 삽입 정렬)</summary>
        private void SortByFootHeight()
        {
            for (int i = 0; i < riseSorted.Length; i++) riseSorted[i] = i;

            for (int i = 1; i < riseSorted.Length; i++)
            {
                int current = riseSorted[i];
                float key = legs[current].FootPosition.y;
                int j = i - 1;

                while (j >= 0 && legs[riseSorted[j]].FootPosition.y > key)
                {
                    riseSorted[j + 1] = riseSorted[j];
                    j--;
                }

                riseSorted[j + 1] = current;
            }
        }

        /// <summary>
        /// 일어나는 몸입니다. <b>발을 제자리로 모으고</b> 몸통은 평소처럼 그 위에 올립니다.
        ///
        /// 걸음은 시작하지 않습니다. 아직 목표를 향해 가는 중이 아니라 <b>자세를 되찾는 중</b>이고,
        /// 이때 걸음 규칙이 돌면 발이 제자리에서 멀다는 이유로 우르르 걸음을 시작해 버립니다.
        ///
        /// 발이 다가가는 속도를 <c>1 − exp(−k·dt)</c> 로 쓰는 이유는 프레임률에 흔들리지 않기 위해서입니다.
        /// <c>Lerp(현재, 목표, k·dt)</c> 는 프레임이 촘촘할수록 느려집니다.
        /// </summary>
        /// <param name="dt">시간 간격(초)</param>
        private void PoseRising(float dt)
        {
            riseTimer += dt;

            groundedFeet = legs.Length;
            float plantedSum = 0f;

            for (int i = 0; i < legs.Length; i++)
            {
                // 제 차례가 오면 <b>평소의 걸음과 같은 호</b>를 그리며 발을 옮깁니다.
                if (!riseStarted[i] && riseTimer >= riseDelays[i])
                {
                    riseStarted[i] = true;
                    legs[i].BeginStep(riseTargets[i], riseNormals[i], riseStepTime, stepHeight * riseLiftScale);
                }

                legs[i].Advance(dt);

                stepTargets[i] = riseTargets[i];
                stepNormals[i] = riseNormals[i];
                plantedSum += riseTargets[i].y;
            }

            supportHeight = plantedSum / legs.Length;

            PoseRisingBody(dt);

            for (int i = 0; i < legs.Length; i++) legs[i].Solve();
        }

        /// <summary>
        /// 일어나는 동안의 몸통입니다. <b>루트를 따라가는 자세</b>에서 <b>발에서 뽑은 자세</b>로 넘어갑니다.
        ///
        /// 둘 중 하나만 쓰면 어색합니다. 발에서만 뽑으면 아직 공중에 있는 발 때문에 몸통이 엉뚱한 데 가고,
        /// 루트만 따르면 발이 땅을 딛는데도 몸이 그것을 <b>모른 척</b>합니다.
        /// 일어난 정도에 따라 섞으면 <b>발이 땅을 잡아 갈수록 몸이 그 위로 올라앉습니다.</b>
        /// </summary>
        /// <param name="dt">시간 간격(초)</param>
        private void PoseRisingBody(float dt)
        {
            ComputeStance(out Vector3 planted, out Vector3 bob, out Vector3 normal);

            Vector3 stancePosition = planted + normal * standHeight + bob * bodyBob;
            Quaternion stanceRotation = StanceRotation(normal, 0f, 0f);

            Vector3 ridePosition = transform.TransformPoint(new Vector3(0f, standHeight, 0f));
            Quaternion rideRotation = transform.rotation;

            float blend = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(riseTimer / riseDuration));

            body.SetPositionAndRotation(
                bodyPositionMotion.Update(dt, Vector3.Lerp(ridePosition, stancePosition, blend)),
                bodyRotationMotion.Update(dt, Quaternion.Slerp(rideRotation, stanceRotation, blend)));
        }

        /// <summary>수평 원을 그립니다. <see cref="Gizmos"/> 에는 원이 없어 선분으로 잇습니다.</summary>
        /// <param name="center">원의 중심</param>
        /// <param name="radius">반지름</param>
        private static void DrawCircle(Vector3 center, float radius)
        {
            const int Segments = 24;

            Vector3 previous = center + new Vector3(radius, 0f, 0f);

            for (int i = 1; i <= Segments; i++)
            {
                float angle = i * (2f * Mathf.PI / Segments);
                Vector3 point = center + new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);

                Gizmos.DrawLine(previous, point);
                previous = point;
            }
        }
    }
}
