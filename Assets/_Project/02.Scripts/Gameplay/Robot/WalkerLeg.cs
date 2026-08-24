using UnityEngine;
using CarDrive.Common;

namespace CarDrive.Gameplay
{
    /// <summary>
    /// 다리 하나입니다. <b>발이 지금 어디에 있는지</b>를 소유하고, 그 자리에 마디를 맞춥니다.
    ///
    /// <b>다리는 세 마디입니다.</b> 넓적마디(femur)가 몸통에서 <b>바깥 위로</b> 뻗고,
    /// 종아리마디(tibia)가 거기서 <b>바깥 아래로</b> 꺾여 내려오며, 발마디(tarsus)가 짧게
    /// 안쪽 아래로 굽어 땅을 짚습니다. 그래서 <b>무릎이 몸통보다 높습니다.</b>
    /// 다리가 몸 아래로 곧게 내려오는 골격과 실루엣이 완전히 다릅니다.
    ///
    /// <b>세 마디인데 왜 2뼈 IK 인가.</b> 발마디는 길이가 짧고 <b>땅과의 각도가 정해져 있어</b>
    /// 자유도로 칠 것이 없습니다. 그래서 발끝에서 발마디 길이만큼 되짚어 올라간 자리
    /// (발목)를 목표로 삼으면, 남는 것은 넓적마디와 종아리마디 둘뿐입니다.
    /// 셋을 한꺼번에 반복법으로 푸는 것보다 <b>정확하고 떨림이 없습니다.</b>
    ///
    /// <b>무릎이 어디로 접히는가</b>는 <see cref="kneePole"/> 하나가 정합니다.
    /// 옆으로 벌어진 다리는 바깥 위, 몸 아래로 내려오는 다리는 앞 또는 뒤입니다.
    /// 비워 두면 발이 서고 싶은 자리에서 <b>바깥 방향을 스스로 뽑아</b> 벌어진 쪽으로 맞춥니다.
    ///
    /// <b>이 컴포넌트는 스스로 갱신되지 않습니다.</b> <see cref="Update"/> 도 <see cref="LateUpdate"/> 도
    /// 없고, <see cref="WalkerRobot"/> 이 매 프레임 <see cref="Advance"/> 와 <see cref="Solve"/> 를
    /// 순서대로 불러 줍니다. 유니티는 같은 종류의 콜백 사이의 순서를 보장하지 않으므로,
    /// 다리가 각자 돌면 <b>어떤 프레임에는 몸통이 먼저, 어떤 프레임에는 다리가 먼저</b> 갱신됩니다.
    /// 발 위치에서 몸통 자세를 뽑아내는 구조에서 그 한 프레임 차이는 그대로 떨림으로 보입니다.
    ///
    /// <b>마디는 회전만 씁니다.</b> 위치를 직접 쓰면 부모의 스케일이나 리그의 오프셋과 싸우게 됩니다.
    /// 대신 마디 길이를 <see cref="Initialize"/> 에서 <b>리그 자체에서 재기</b> 때문에
    /// 계산한 관절 위치와 계층 구조가 만들어 내는 위치가 정확히 같습니다.
    /// </summary>
    [DisallowMultipleComponent]
    public class WalkerLeg : MonoBehaviour
    {
        // --- Public Member Variables : 마디 ---

        /// <summary>넓적마디(femur)입니다. 몸통에서 무릎까지이고, 로컬 +Z 가 무릎을 향합니다.</summary>
        [Header("마디")]
        [Tooltip("넓적마디 (몸통 → 무릎). 로컬 +Z 가 무릎을 향합니다.")]
        public Transform upperBone;

        /// <summary>종아리마디(tibia)입니다. 무릎에서 발목까지이고, 로컬 +Z 가 발목을 향합니다.</summary>
        [Tooltip("종아리마디 (무릎 → 발목). 로컬 +Z 가 발목을 향합니다.")]
        public Transform lowerBone;

        /// <summary>발마디(tarsus)입니다. 발목에서 발끝까지입니다. 없으면 종아리마디 끝이 곧 발끝입니다.</summary>
        [Tooltip("발마디 (발목 → 발끝). 없으면 종아리마디 끝이 곧 발끝입니다.")]
        public Transform ankleBone;

        // --- Public Member Variables : 치수 ---

        /// <summary>넓적마디의 길이입니다. 0이면 <see cref="Initialize"/>에서 리그를 재서 채웁니다.</summary>
        [Header("치수 (0이면 리그에서 잽니다)")]
        [Tooltip("넓적마디의 길이. 0이면 리그를 재서 채웁니다.")]
        public float upperLength;

        /// <summary>종아리마디의 길이입니다. 0이면 <see cref="Initialize"/>에서 리그를 재서 채웁니다.</summary>
        [Tooltip("종아리마디의 길이. 0이면 리그를 재서 채웁니다.")]
        public float lowerLength;

        /// <summary>발마디의 길이입니다. 0이면 발마디가 없는 것으로 봅니다.</summary>
        [Tooltip("발마디의 길이. 0이면 발마디가 없는 것으로 봅니다.")]
        public float ankleLength;

        /// <summary>
        /// 발마디가 <b>얼마나 바깥으로 눕는지</b>입니다. 0이면 지면에 수직으로 서고, 1이면 완전히 눕습니다.
        /// 발끝이 바깥에서 안쪽으로 굽어 들어오는 모양이 이 값에서 나옵니다.
        /// </summary>
        [Tooltip("발마디가 바깥으로 눕는 정도 (0=수직, 1=완전히 누움)")]
        [Range(0f, 1f)]
        public float ankleOutward = 0.45f;

        // --- Public Member Variables : 자리 ---

        /// <summary>
        /// 이 발이 서 있고 싶은 자리입니다. <b>로봇 루트의 로컬 좌표</b>이고, Y는 0(지면 높이)입니다.
        /// 옆으로 벌어진 다리는 이 값이 몸통 폭보다 훨씬 바깥에 있습니다.
        /// </summary>
        [Header("자리")]
        [Tooltip("이 발이 서 있고 싶은 자리 (로봇 루트 로컬, Y=0). 몸통보다 훨씬 바깥입니다.")]
        public Vector3 homeOffset;

        /// <summary>
        /// 무릎이 접히는 방향입니다. <b>로봇 루트의 로컬 좌표</b>입니다.
        ///
        /// 비워 두면(0,0,0) <see cref="homeOffset"/>에서 바깥 방향을 뽑아
        /// <b>바깥 위</b>로 맞춥니다. 옆으로 벌어진 다리의 기본 자세입니다.
        /// 몸 아래로 내려오는 골격으로 쓰려면 앞다리는 (0,0,-1), 뒷다리는 (0,0,1)을 넣으면 됩니다.
        /// </summary>
        [Tooltip("무릎이 접히는 방향 (루트 로컬). 비워두면 바깥 위로 자동 계산합니다")]
        public Vector3 kneePole;

        // --- Private Member Variables ---

        /// <summary>로봇 루트입니다. 무릎과 발마디의 방향을 여기서 뽑습니다.</summary>
        private Transform root;

        /// <summary>몸통 중심에서 이 발 쪽으로 향하는 수평 방향입니다. (루트 로컬)</summary>
        private Vector3 outwardLocal = Vector3.right;

        /// <summary>실제로 쓸 무릎 방향입니다. (루트 로컬)</summary>
        private Vector3 resolvedPole = Vector3.up;

        /// <summary>
        /// 방향을 뽑을 때 쓴 <see cref="homeOffset"/> 입니다.
        /// 실행 중에 발 자리를 인스펙터에서 옮길 수 있으므로, 값이 바뀌면 방향도 다시 뽑습니다.
        /// </summary>
        private Vector3 resolvedFrom;

        /// <summary>발의 현재 월드 위치입니다. 이 클래스가 소유하는 유일한 상태입니다.</summary>
        private Vector3 footPosition;

        /// <summary>발이 딛고 있는 면의 법선입니다.</summary>
        private Vector3 footNormal = Vector3.up;

        /// <summary>걸음이 시작된 자리입니다.</summary>
        private Vector3 stepFrom;

        /// <summary>걸음이 끝날 자리입니다.</summary>
        private Vector3 stepTo;

        /// <summary>떠날 때 딛고 있던 면의 법선입니다.</summary>
        private Vector3 stepFromNormal = Vector3.up;

        /// <summary>착지할 면의 법선입니다.</summary>
        private Vector3 stepToNormal = Vector3.up;

        /// <summary>이 걸음에 걸리는 시간(초)입니다.</summary>
        private float stepDuration;

        /// <summary>이 걸음에서 발이 들리는 높이입니다.</summary>
        private float stepHeight;

        /// <summary>걸음이 시작된 뒤 흐른 시간입니다.</summary>
        private float stepTime;

        /// <summary>지금 발을 떼고 있는지 여부입니다.</summary>
        private bool stepping;

        /// <summary>지금 발이 들려 있는 높이입니다.</summary>
        private float lift;

        /// <summary>발이 제자리에서 벗어날 수 있는 최대 수평 거리입니다. 리그 기하에서 계산합니다.</summary>
        private float strideRadius = 0.3f;

        // --- Constants ---

        /// <summary>마디 길이를 잴 수 없을 때 쓰는 최소값입니다.</summary>
        private const float MinBoneLength = 0.01f;

        /// <summary>0으로 보는 제곱 길이입니다.</summary>
        private const float Epsilon = 1e-8f;

        /// <summary>무릎 방향을 자동으로 뽑을 때 바깥으로 기우는 비율입니다.</summary>
        private const float AutoPoleOutward = 0.55f;

        /// <summary>무릎 방향을 자동으로 뽑을 때 위로 기우는 비율입니다.</summary>
        private const float AutoPoleUp = 0.83f;

        /// <summary>작업 반경의 하한입니다. 리그가 이상해도 0이 되지 않게 합니다.</summary>
        private const float MinStrideRadius = 0.05f;

        // --- Public Properties ---

        /// <summary>발의 현재 월드 위치입니다.</summary>
        public Vector3 FootPosition { get { return footPosition; } }

        /// <summary>발이 딛고 있는 면의 법선입니다.</summary>
        public Vector3 FootNormal { get { return footNormal; } }

        /// <summary>몸통에 붙은 자리(넓적마디의 밑동) 위치입니다.</summary>
        public Vector3 HipPosition { get { return upperBone != null ? upperBone.position : transform.position; } }

        /// <summary>지금 발을 떼고 있는지 여부입니다.</summary>
        public bool IsStepping { get { return stepping; } }

        /// <summary>걸음의 진행도(0~1)입니다. 딛고 있으면 0입니다.</summary>
        public float StepProgress { get { return stepping && stepDuration > 0f ? Mathf.Clamp01(stepTime / stepDuration) : 0f; } }

        /// <summary>지금 발이 들려 있는 높이입니다. 몸통이 이만큼을 되받아 흔들립니다.</summary>
        public float Lift { get { return lift; } }

        /// <summary>이 다리가 뻗을 수 있는 최대 거리입니다.</summary>
        public float MaxReach { get { return upperLength + lowerLength + ankleLength; } }

        /// <summary>
        /// 발이 제자리에서 벗어날 수 있는 <b>최대 수평 거리</b>입니다. 리그 기하에서 계산합니다.
        ///
        /// <b>이 값이 걸음의 모든 것을 정합니다.</b> 발은 이 원 안에서만 놓일 수 있고,
        /// 그 원을 벗어나는 순간 IK 가 한계에서 잘려 <b>다리가 뻗은 채 미끄러집니다.</b>
        /// 그래서 걸음 시간도, 낼 수 있는 속도도 전부 이 값에서 거꾸로 나옵니다.
        /// (<see cref="WalkerRobot"/> 참고)
        /// </summary>
        public float StrideRadius { get { return strideRadius; } }

        /// <summary>넓적마디와 종아리마디가 배선되어 쓸 수 있는 상태인지입니다.</summary>
        public bool IsWired { get { return upperBone != null && lowerBone != null; } }

        // --- Public Methods ---

        /// <summary>
        /// 관절을 끌어 옮긴 결과를 <b>마디 길이로 받아들입니다.</b> 에디터에서만 부릅니다.
        ///
        /// 씬 뷰에서 무릎을 잡아 끌면 유니티는 그것을 <c>localPosition</c> 으로 적습니다.
        /// 여기서는 그 거리를 마디 길이로 읽고, 자식 마디를 <b>다시 로컬 +Z 축 위에</b> 올려놓습니다.
        /// 이 프로젝트의 마디 규약이 "+Z 가 다음 관절을 향한다"이기 때문입니다. 축을 벗어난 채로 두면
        /// IK 가 돌리는 방향과 실제 마디가 향하는 방향이 어긋나 다리가 비틀립니다.
        ///
        /// 그래서 <b>끌어 옮기는 것이 곧 길이를 고치는 것</b>이 되고, 옆으로 새는 성분은 버려집니다.
        /// </summary>
        public void MeasureFromRig()
        {
            if (!IsWired) return;

            FoldHipOffset();

            upperLength = Mathf.Max(lowerBone.localPosition.magnitude, MinBoneLength);
            lowerBone.localPosition = new Vector3(0f, 0f, upperLength);

            if (ankleBone == null) return;

            lowerLength = Mathf.Max(ankleBone.localPosition.magnitude, MinBoneLength);
            ankleBone.localPosition = new Vector3(0f, 0f, lowerLength);
        }

        /// <summary>
        /// 다른 다리의 <b>마디 길이를 그대로 가져옵니다.</b> 에디터에서만 부릅니다.
        ///
        /// 길이는 계층 구조가 갖고 있으므로 숫자만 베끼면 소용이 없습니다. 자식 마디를
        /// <b>그 길이의 자리로 옮겨야</b> 실제로 같은 다리가 됩니다.
        /// </summary>
        /// <param name="source">본이 될 다리</param>
        public void CopyShapeFrom(WalkerLeg source)
        {
            if (source == null || source == this || !IsWired || !source.IsWired) return;

            upperLength = source.upperLength;
            lowerLength = source.lowerLength;
            ankleLength = source.ankleLength;

            lowerBone.localPosition = new Vector3(0f, 0f, upperLength);

            if (ankleBone != null) ankleBone.localPosition = new Vector3(0f, 0f, lowerLength);
        }

        /// <summary>
        /// 넓적마디가 다리 뿌리에서 떨어져 있으면 <b>그 어긋남을 뿌리로 넘깁니다.</b>
        ///
        /// 고관절은 다리 뿌리(이 컴포넌트가 붙은 오브젝트)에 있고 넓적마디는 그 자리에서 시작합니다.
        /// 그런데 씬 뷰에서는 <b>둘 중 아무거나 잡힙니다.</b> 넓적마디를 끌어 옮기면 겉보기에는
        /// 고관절이 옮겨진 것처럼 보이지만, 다리 뿌리는 제자리에 남아 두 가지가 어긋납니다.
        ///  1. 좌우 대칭이 뿌리 자리를 보므로 <b>짝이 따라오지 않습니다</b>
        ///  2. 발자리(<see cref="homeOffset"/>)는 루트 기준이라 <b>다리만 혼자 밀려납니다</b>
        ///
        /// 그래서 넓적마디의 어긋남을 뿌리로 옮기고 넓적마디를 제자리에 돌려놓습니다.
        /// <b>무엇을 끌어도 같은 결과</b>가 되고, 규약도 지켜집니다.
        /// </summary>
        private void FoldHipOffset()
        {
            if (upperBone.parent != transform) return;

            Vector3 offset = upperBone.localPosition;
            if (offset.sqrMagnitude < Epsilon) return;

            transform.localPosition += transform.localRotation * offset;
            upperBone.localPosition = Vector3.zero;
        }

        /// <summary>
        /// 리그를 재고 로봇 루트를 기억합니다. <see cref="WalkerRobot"/> 이 한 번 부릅니다.
        /// </summary>
        /// <param name="robotRoot">로봇의 루트 트랜스폼</param>
        /// <param name="strideScale">다리가 뻗을 수 있는 범위의 몇 배까지 쓸지 (0~1)</param>
        /// <returns>배선이 온전해서 쓸 수 있으면 true</returns>
        public bool Initialize(Transform robotRoot, float strideScale)
        {
            root = robotRoot;

            if (upperBone == null || lowerBone == null)
            {
                GameLog.Error(GameLog.Channel.Enemy, name + ": 넓적마디와 종아리마디가 필요합니다. 이 다리는 쉬어 갑니다.", this);
                return false;
            }

            // 길이를 <b>언제나</b> 리그에서 잽니다. 적어 둔 값을 믿지 않습니다.
            //
            // 계산이 만드는 관절 위치와 계층 구조가 만드는 위치는 반드시 같아야 합니다. 둘이 어긋나면
            // IK 는 닿았다고 여기는데 화면의 마디는 다른 곳에 있습니다. 그래서 <b>계층 구조를 유일한
            // 진실</b>로 삼습니다. 관절을 끌어 옮기면 길이가 저절로 따라오고, 인스펙터의 숫자는
            // 그 결과를 보여 주는 창일 뿐입니다.
            upperLength = Vector3.Distance(upperBone.position, lowerBone.position);

            lowerLength = ankleBone != null
                ? Vector3.Distance(lowerBone.position, ankleBone.position)
                : upperLength;

            upperLength = Mathf.Max(upperLength, MinBoneLength);
            lowerLength = Mathf.Max(lowerLength, MinBoneLength);
            ankleLength = Mathf.Max(ankleLength, 0f);

            ResolveDirections();
            MeasureStrideRadius(strideScale);

            footPosition = ankleBone != null ? ankleBone.position : lowerBone.position;

            return true;
        }

        /// <summary>발을 즉시 그 자리에 세웁니다. 스폰·순간이동에 씁니다.</summary>
        /// <param name="position">발을 둘 자리</param>
        /// <param name="normal">그 자리의 지면 법선</param>
        public void PlaceAt(Vector3 position, Vector3 normal)
        {
            footPosition = position;
            footNormal = normal;
            stepping = false;
            stepTime = 0f;
            lift = 0f;
        }

        /// <summary>
        /// 걸음을 시작합니다. 시작할지 말지는 <see cref="WalkerRobot"/> 이 이미 정했습니다.
        /// </summary>
        /// <param name="target">발이 착지할 자리</param>
        /// <param name="normal">착지할 면의 법선</param>
        /// <param name="duration">걸음에 걸리는 시간(초)</param>
        /// <param name="height">발이 들리는 높이</param>
        public void BeginStep(Vector3 target, Vector3 normal, float duration, float height)
        {
            stepFrom = footPosition;
            stepTo = target;
            stepFromNormal = footNormal;
            stepToNormal = normal;
            stepDuration = Mathf.Max(duration, 0.01f);
            stepHeight = height;
            stepTime = 0f;
            stepping = true;
        }

        /// <summary>
        /// 걸음을 한 프레임 진행합니다. <b>마디는 아직 건드리지 않습니다.</b>
        ///
        /// 마디를 세우는 것(<see cref="Solve"/>)과 나눈 이유는 <see cref="WalkerRobot"/> 의
        /// 설명에 있습니다. 몸통 자세가 이 결과에서 나오고 다리가 붙은 자리는 그 몸통에 있으므로
        /// <b>발 → 몸통 → 마디</b> 순서를 지켜야 합니다.
        /// </summary>
        /// <param name="dt">시간 간격(초)</param>
        public void Advance(float dt)
        {
            if (stepping) AdvanceStep(dt);
            else lift = 0f;
        }

        /// <summary>
        /// 발이 있는 자리로 마디를 세웁니다. 몸통 자세가 정해진 <b>뒤에</b> 불러야 합니다.
        /// </summary>
        public void Solve()
        {
            SolveBones();
        }

        // --- Private Methods ---

        /// <summary>
        /// 바깥 방향과 무릎 방향을 정합니다. <see cref="kneePole"/> 이 비어 있으면 거미형으로 자동 계산합니다.
        /// </summary>
        private void ResolveDirections()
        {
            resolvedFrom = homeOffset;

            Vector3 flat = new Vector3(homeOffset.x, 0f, homeOffset.z);
            outwardLocal = flat.sqrMagnitude > Epsilon ? flat.normalized : Vector3.right;

            if (kneePole.sqrMagnitude > Epsilon)
            {
                resolvedPole = kneePole.normalized;
                return;
            }

            // 바깥 위 — 무릎이 몸통보다 높이 솟는 거미형 자세입니다.
            resolvedPole = (outwardLocal * AutoPoleOutward + Vector3.up * AutoPoleUp).normalized;
        }

        /// <summary>
        /// 발이 제자리에서 벗어날 수 있는 <b>최대 수평 거리</b>를 리그에서 잽니다.
        ///
        /// 제자리에 서 있을 때 다리 밑동에서 발목까지의 벡터를 <b>수직 성분과 수평 성분</b>으로 나눕니다.
        /// 수직 성분은 몸통 높이라 걸음과 무관하게 고정이므로, 남은 길이를 전부 수평에 쓸 수 있습니다.
        /// <code>
        /// 쓸 수 있는 수평 거리 = √(뻗을 수 있는 길이² − 수직 성분²)
        /// 작업 반경         = 그 거리 − 지금 수평 거리
        /// </code>
        /// 예를 들어 넓적마디 0.60 · 종아리마디 0.70 인 다리가 0.29 만큼 내려와 0.77 바깥을 짚고 있으면
        /// 작업 반경은 약 0.44m 입니다. 발은 제자리에서 그 이상 벗어날 수 없습니다.
        ///
        /// <b>왜 손으로 정하지 않는가.</b> 예전에는 이 값을 0.9 로 적어 두었는데, 실제 다리가
        /// 감당할 수 있는 것은 0.33 이었습니다. 그 차이만큼 발이 매 걸음 뒤로 밀려나
        /// 다리가 뻗은 채 끌려다녔습니다. 리그를 조금만 고쳐도 다시 어긋나므로 <b>기하에서 뽑습니다.</b>
        /// </summary>
        /// <param name="strideScale">뻗을 수 있는 길이의 몇 배까지 쓸지 (0~1). 1에 가까우면 다리가 완전히 펴집니다.</param>
        private void MeasureStrideRadius(float strideScale)
        {
            Vector3 up = root != null ? root.up : Vector3.up;
            Vector3 home = root != null ? root.TransformPoint(homeOffset) : transform.position + homeOffset;

            Vector3 ankleDirection = (up * (1f - ankleOutward) + ToWorld(outwardLocal) * ankleOutward).normalized;
            Vector3 ankle = home + ankleDirection * ankleLength;

            Vector3 delta = ankle - upperBone.position;

            float vertical = Vector3.Dot(delta, up);
            float horizontal = (delta - up * vertical).magnitude;

            float reach = (upperLength + lowerLength) * Mathf.Clamp(strideScale, 0.3f, 1f);
            float usableHorizontal = Mathf.Sqrt(Mathf.Max(reach * reach - vertical * vertical, 0f));

            strideRadius = Mathf.Max(usableHorizontal - horizontal, MinStrideRadius);
        }

        /// <summary>
        /// 호(弧)를 따라 발을 옮깁니다.
        ///
        /// 수평은 <b>부드러운 계단 함수</b>(3t² − 2t³)로 갑니다. 선형으로 가면 발이 떠나는 순간과
        /// 닿는 순간에 속도가 뚝 끊겨 기계적으로 보이고, 실제로 착지 순간 발이 미끄러집니다.
        ///
        /// 수직은 <b>사인 반주기</b>입니다. 시작과 끝에서 높이도 0이고 <b>속도도 0</b>이라
        /// 땅을 찍거나 뽑아내는 느낌이 나지 않습니다.
        ///
        /// 들어 올리는 축은 월드 위쪽이 아니라 <b>섞인 지면 법선</b>입니다. 경사에서 월드 위쪽으로
        /// 들면 발이 비탈면을 파고듭니다.
        /// </summary>
        /// <param name="dt">시간 간격(초)</param>
        private void AdvanceStep(float dt)
        {
            stepTime += dt;

            float t = Mathf.Clamp01(stepTime / stepDuration);
            float ease = t * t * (3f - 2f * t);

            Vector3 normal = Vector3.Slerp(stepFromNormal, stepToNormal, ease);
            if (normal.sqrMagnitude < Epsilon) normal = Vector3.up;

            lift = Mathf.Sin(t * Mathf.PI) * stepHeight;

            footPosition = Vector3.Lerp(stepFrom, stepTo, ease) + normal * lift;
            footNormal = normal;

            if (t < 1f) return;

            footPosition = stepTo;
            footNormal = stepToNormal;
            lift = 0f;
            stepping = false;
        }

        /// <summary>
        /// 발이 있는 자리로 세 마디를 맞춥니다.
        ///
        /// 발마디가 있으면 <b>발끝에서 되짚어 올라간 발목</b>이 2뼈 IK 의 목표가 되고,
        /// 발마디는 그 발목에서 발끝을 향해 세워집니다.
        /// </summary>
        private void SolveBones()
        {
            if (upperBone == null || lowerBone == null) return;

            // 실행 중에 발 자리를 옮겼다면 바깥 방향과 무릎 방향을 다시 뽑습니다.
            if (resolvedFrom != homeOffset) ResolveDirections();

            Vector3 hip = upperBone.position;
            Vector3 pole = ToWorld(resolvedPole);

            bool hasAnkle = ankleBone != null && ankleLength > 0f;
            Vector3 target = hasAnkle ? footPosition + AnkleDirection() * ankleLength : footPosition;

            TwoBoneIK.Solution solution = TwoBoneIK.Solve(hip, target, upperLength, lowerLength, pole);

            upperBone.rotation = TwoBoneIK.BoneRotation(hip, solution.Joint, pole, upperBone.rotation);
            lowerBone.rotation = TwoBoneIK.BoneRotation(solution.Joint, solution.End, pole, lowerBone.rotation);

            if (!hasAnkle) return;

            // 발마디는 무릎 방향과 거의 <b>반대로</b> 굽습니다. 그래서 무릎 방향을 위쪽 힌트로 주면
            // 두 벡터가 나란해져 비틀림이 흔들립니다. 다리가 놓인 평면의 법선을 주면 항상 수직입니다.
            Vector3 legPlane = Vector3.Cross(solution.End - solution.Joint, pole);
            if (legPlane.sqrMagnitude < Epsilon) legPlane = pole;

            ankleBone.rotation = TwoBoneIK.BoneRotation(solution.End, footPosition, legPlane, ankleBone.rotation);
        }

        /// <summary>
        /// 발끝에서 발목으로 되짚어 올라가는 방향입니다.
        /// 지면 법선과 바깥 방향을 섞으므로, 경사에서도 발끝이 비탈을 따라 눕습니다.
        /// </summary>
        /// <returns>월드 방향</returns>
        private Vector3 AnkleDirection()
        {
            Vector3 direction = footNormal * (1f - ankleOutward) + ToWorld(outwardLocal) * ankleOutward;

            return direction.sqrMagnitude < Epsilon ? Vector3.up : direction.normalized;
        }

        /// <summary>루트 로컬 방향을 월드로 바꿉니다.</summary>
        /// <param name="local">루트 로컬 방향</param>
        /// <returns>월드 방향</returns>
        private Vector3 ToWorld(Vector3 local)
        {
            return root != null ? root.rotation * local : local;
        }
    }
}
