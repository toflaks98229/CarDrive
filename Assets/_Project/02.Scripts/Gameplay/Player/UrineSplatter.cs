using UnityEngine;

namespace CarDrive.Gameplay
{
    /// <summary>
    /// 오줌이 닿은 자리에 <b>젖은 자국을 찍고, 키우고, 말립니다.</b>
    ///
    /// <b>왜 파티클 충돌이 아닌가.</b> 튀는 물방울은 서브이미터가 맡지만, 자국은 지속되는
    /// 표시라 입자로는 만들 수 없습니다. 그리고 입자마다 충돌 콜백을 받으면 그 수만큼
    /// 비용이 붙는데 이 프로젝트는 렌더 스레드 드로우 제출이 이미 병목입니다.
    /// 여기서는 <b>프레임당 레이캐스트 한 줄기</b>로 닿는 자리를 찾습니다.
    ///
    /// <b>한자리에 오래 누면 고입니다.</b> 닿는 자리가 거의 그대로면 지금 자국을 계속
    /// 키웁니다. 번짐이 자라고 판도 함께 커져 물이 고이듯 넓어집니다.
    ///
    /// <b>이리저리 누면 넓게 퍼집니다.</b> 닿는 자리가 <see cref="_stampDistance"/> 넘게
    /// 움직이면 그 자리에 새 자국을 찍습니다. 훑고 지나간 길마다 자국이 남습니다.
    ///
    /// <b>벽에서는 흘러내립니다.</b> 닿은 면이 서 있을수록 셰이더의 흘러내림을 올립니다.
    /// 판은 언제나 월드 위쪽을 위로 두고 눕히므로, 셰이더가 아래로만 뻗는 줄기가
    /// 중력 방향과 맞습니다.
    ///
    /// <b>자국 판은 이 컴포넌트의 자식이 아닙니다.</b> 처음에는 자식으로 만들었는데,
    /// 이 컴포넌트는 플레이어 몸통(Player_OnFoot)에 붙어 있고 그 몸통은 마우스 좌우에 따라
    /// 통째로 돕니다(PlayerCameraController 가 playerBody.localRotation 을 씁니다).
    /// <c>Transform.position</c>·<c>rotation</c> 은 <b>그 순간의</b> 월드 자세를 로컬로 환산해
    /// 저장할 뿐이라, 부모가 움직이면 자식은 그대로 끌려갑니다. 그래서 벽에 붙어야 할 자국이
    /// <b>카메라를 따라다녔습니다.</b> 지금은 씬 루트에 움직이지 않는 통을 하나 만들어
    /// 거기에 담습니다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class UrineSplatter : MonoBehaviour
    {
        // --- Public Member Variables ---

        /// <summary>자국을 그릴 재질입니다. CarDrive/Splat 을 쓰는 것을 넣으세요.</summary>
        [Header("자국")]
        [Tooltip("CarDrive/Splat 셰이더를 쓰는 재질")]
        public Material splatMaterial;

        /// <summary>동시에 남아 있을 수 있는 자국 수입니다.</summary>
        [Tooltip("동시에 남는 자국 수. 넘으면 가장 오래된 것부터 덮어씁니다.")]
        [Range(4, 64)]
        public int maxSplats = 24;

        /// <summary>자국 하나가 다 마르는 데 걸리는 시간(초)입니다.</summary>
        [Tooltip("자국이 다 마르는 데 걸리는 시간(초)")]
        public float dryDuration = 26f;

        /// <summary>
        /// 갓 찍힌 자국의 <b>몸통</b> 지름(m)입니다.
        ///
        /// 예전 필드 이름은 startDiameter 였는데 그것은 <b>판</b> 지름이었습니다 —
        /// 실제 얼룩은 그 절반도 안 됐습니다. 이름이 거짓말을 하고 있어서
        /// [FormerlySerializedAs] 로 옛 값을 이어받지 <b>않습니다.</b> 이어받으면
        /// 자국이 두 배 넘게 커집니다.
        /// </summary>
        [Header("몸통 크기")]
        [Tooltip("갓 찍힌 자국의 몸통 지름(m)")]
        public float startBodyDiameter = 0.14f;

        /// <summary>한자리에 계속 누었을 때 자랄 수 있는 <b>몸통</b> 지름(m)입니다.</summary>
        [Tooltip("한자리에 계속 누었을 때 자랄 수 있는 몸통 지름(m)")]
        public float maxBodyDiameter = 1.4f;

        /// <summary>초당 자라는 몸통 지름(m)입니다. 출력이 셀수록 빨리 자랍니다.</summary>
        [Tooltip("초당 자라는 몸통 지름(m). 출력이 셀수록 빨라집니다.")]
        public float bodyGrowPerSecond = 0.58f;

        /// <summary>갓 찍힌 벽 자국의 줄기 길이(m)입니다.</summary>
        [Header("흘러내림")]
        [Tooltip("갓 찍힌 벽 자국의 줄기 길이(m)")]
        public float startDripReach = 0.05f;

        /// <summary>
        /// 줄기가 자랄 수 있는 최대 길이(m)입니다.
        /// <b>판 높이를 정하는 값</b>이라 오버드로에 직접 영향을 줍니다.
        /// </summary>
        [Tooltip("줄기가 자랄 수 있는 최대 길이(m). 판 높이를 정하므로 오버드로에 직접 영향")]
        public float maxDripReach = 1.2f;

        /// <summary>젖은 1초당 자라는 줄기 길이(m)입니다.</summary>
        [Tooltip("젖은 1초당 자라는 줄기 길이(m)")]
        public float dripReachPerSecond = 0.4f;

        // --- Private Member Variables ---

        /// <summary>닿는 자리가 이보다 멀리 움직이면 새 자국을 찍습니다.</summary>
        [Header("번짐")]
        [SerializeField]
        [Tooltip("닿는 자리가 이만큼 움직이면 새 자국을 찍습니다(m)")]
        private float _stampDistance = 0.24f;

        /// <summary>면에서 띄우는 거리(m)입니다. 0 이면 z 다툼으로 지글거립니다.</summary>
        [SerializeField]
        [Tooltip("면에서 띄우는 거리(m). 너무 크면 자국이 떠 보입니다.")]
        private float _surfaceOffset = 0.012f;

        /// <summary>자국이 붙을 수 있는 면입니다.</summary>
        [SerializeField]
        [Tooltip("자국이 붙을 수 있는 레이어")]
        private LayerMask _surfaceMask = ~0;

        /// <summary>
        /// 자국 판들을 담아 두는 <b>움직이지 않는 통</b>입니다. 씬 루트에 있습니다.
        ///
        /// 이 컴포넌트를 부모로 쓰면 안 됩니다 — 플레이어 몸통에 붙어 있어 마우스를 돌릴 때마다
        /// 함께 돌고, 차에 타서 몸통이 꺼지면 자국까지 통째로 사라집니다.
        /// </summary>
        private Transform _world;

        /// <summary>돌려 쓰는 자국 판들입니다.</summary>
        private Splat[] _pool;

        /// <summary>다음에 꺼내 쓸 자리입니다.</summary>
        private int _next;

        /// <summary>지금 키우고 있는 자국입니다. 없으면 -1 입니다.</summary>
        private int _active = -1;

        /// <summary>
        /// 재질에서 읽어 둔 테두리 갉기 값입니다. 판이 몸통을 통째로 담으려면
        /// 갉기가 얼마나 밖으로 밀어내는지 알아야 합니다.
        /// C# 이 이 숫자를 따로 들고 있으면 재질만 고쳤을 때 조용히 어긋납니다.
        /// </summary>
        private float _edgeBite = 0.55f;

        /// <summary>프로퍼티 이름은 매 프레임 문자열로 찾지 않습니다.</summary>
        private static readonly int BodyRadiusId = Shader.PropertyToID("_BodyRadius");
        private static readonly int AgeId = Shader.PropertyToID("_Age");
        private static readonly int DripId = Shader.PropertyToID("_DripAmount");
        private static readonly int DripStartId = Shader.PropertyToID("_DripStart");
        private static readonly int DripReachId = Shader.PropertyToID("_DripReach");
        private static readonly int SeedId = Shader.PropertyToID("_Seed");
        private static readonly int BodyOffsetId = Shader.PropertyToID("_BodyOffsetY");

        /// <summary>자국 한 장이 들고 있는 것들입니다.</summary>
        private struct Splat
        {
            public Transform Root;
            public MeshRenderer Renderer;
            public MaterialPropertyBlock Block;
            public float Age;

            /// <summary>몸통 반지름(m). <b>자라기만 합니다.</b></summary>
            public float BodyRadius;

            /// <summary>흘러내림 정도(0~1). 스탬프 때 정하고 안 바꿉니다.</summary>
            public float Drip;

            /// <summary>
            /// 줄기 머리의 깊이(m). <b>스탬프 때 못 박습니다.</b>
            ///
            /// 예전에는 셰이더가 매 프레임 몸통 반지름의 40% 로 다시 셈했습니다. 그래서
            /// 웅덩이가 자랄 때마다 <b>이미 흘러내린 줄기의 머리가 25cm 씩 내려갔습니다.</b>
            /// (EsProgram/InkPainter 의 줄기가 텍셀에 기록되어 안 움직이는 성질을,
            ///  버퍼 없이 스칼라 하나로 얻습니다.)
            /// </summary>
            public float DripStart;

            /// <summary>줄기 길이(m). <b>젖은 시간으로만</b> 자랍니다 — 몸통과 무관합니다.</summary>
            public float DripReach;

            /// <summary>
            /// 누적 젖은 시간(초)입니다. Age 와 달리 <b>절대 되감지 않습니다.</b>
            /// 되감으면 이미 흘러내린 꼬리가 뒤로 빨려 들어갑니다.
            /// </summary>
            public float WetSeconds;

            /// <summary>자국마다 다른 잡음 씨앗입니다.</summary>
            public float Seed;

            /// <summary>판 좌표에서 앵커가 놓인 y 입니다. Place 가 셉니다.</summary>
            public float BodyOffsetY;

            /// <summary>물줄기가 실제로 닿은 자리입니다. 판이 커져도 여기는 안 움직입니다.</summary>
            public Vector3 Anchor;

            /// <summary>
            /// 닿은 면의 법선입니다. <b>자세를 다시 잡을 때마다 여기서 새로 계산합니다.</b>
            /// 트랜스폼에서 되읽으면 그 트랜스폼이 이미 틀어져 있을 때 틀어진 채로 굳습니다.
            /// </summary>
            public Vector3 Normal;

            public bool Alive;
        }

        // --- Unity Event Functions ---

        void Awake()
        {
            // 판이 몸통을 통째로 담으려면 갉기가 얼마나 밖으로 밀어내는지 알아야 합니다.
            if (splatMaterial != null && splatMaterial.HasFloat("_EdgeBite"))
                _edgeBite = splatMaterial.GetFloat("_EdgeBite");

            BuildPool();
        }

        void LateUpdate()
        {
            AgeAll(Time.deltaTime);
        }

        void OnDestroy()
        {
            // 통은 씬 루트에 따로 서 있으므로 이 컴포넌트가 사라져도 저절로 없어지지 않습니다.
            if (_world != null) Destroy(_world.gameObject);
        }

        // --- Public Properties ---

        /// <summary>
        /// 자국 판들이 담긴 <b>움직이지 않는 통</b>입니다. 씬 루트에 있습니다.
        ///
        /// 밖으로 내주는 것은 테스트가 <b>이름으로 찾지 않게</b> 하기 위해서입니다.
        /// GameObject.Find 로 찾으면 앞 테스트의 통이 아직 안 지워졌을 때 빈 통을
        /// 들여다보고 조용히 틀린 답을 냅니다.
        /// </summary>
        public Transform SplatRoot { get { return _world; } }

        // --- Public Methods ---

        /// <summary>
        /// 이번 프레임에 오줌이 닿는 자리를 찍습니다. 흐르는 동안 매 프레임 부르세요.
        /// </summary>
        /// <param name="origin">노즐 위치</param>
        /// <param name="direction">노즐이 향한 방향</param>
        /// <param name="speed">입자 초기 속도(m/s)</param>
        /// <param name="gravityScale">입자에 걸린 중력 배율. 파티클의 gravityModifier 와 같아야 합니다.</param>
        /// <param name="flow">출력 비율(0~1)</param>
        /// <param name="deltaTime">이번 프레임의 시간</param>
        public void Mark(Vector3 origin, Vector3 direction, float speed, float gravityScale,
                         float flow, float deltaTime)
        {
            if (_pool == null || splatMaterial == null) return;
            if (flow <= 0.001f) { _active = -1; return; }

            RaycastHit hit;
            if (!TraceArc(origin, direction, speed, gravityScale, out hit)) { _active = -1; return; }

            Vector3 at = hit.point + hit.normal * _surfaceOffset;

            // 서 있는 면일수록 흘러내립니다. 바닥(법선이 위)이면 0 입니다.
            float drip = 1f - Mathf.Clamp01(Mathf.Abs(Vector3.Dot(hit.normal, Vector3.up)));

            if (_active >= 0 && _pool[_active].Alive &&
                Vector3.Distance(_pool[_active].Anchor, at) <= _stampDistance)
            {
                Grow(_active, flow, deltaTime);
                return;
            }

            _active = Stamp(at, hit.normal, drip);
        }

        /// <summary>줄기가 멈췄음을 알립니다. 다음에 다시 누면 새 자국부터 시작합니다.</summary>
        public void StopMarking()
        {
            _active = -1;
        }

        // --- Private Methods ---

        /// <summary>
        /// 포물선을 토막으로 끊어 레이캐스트합니다.
        ///
        /// <b>직선 하나로는 안 됩니다.</b> 줄기는 중력을 받아 휘므로 직선으로 재면
        /// 실제로 떨어지는 자리보다 훨씬 멀리 찍힙니다.
        ///
        /// <b>시간이 아니라 거리로 끊습니다.</b> 예전에는 0.09초씩 여섯 토막, 곧 0.54초만
        /// 훑었습니다. 그런데 이 씬의 입자는 최대 15m/s 로 2~3초를 날아갑니다. 그래서
        /// 조금만 위로 겨누거나 먼 벽에 쏘면 여섯 번째 레이가 아직 <b>허공</b>에서 끝나
        /// 물줄기는 눈에 보이게 벽에 부딪히는데 자국은 하나도 안 남았습니다.
        /// 토막 길이를 미터로 고정하면 빠르든 느리든 같은 정밀도로 같은 사거리를 훑습니다.
        ///
        /// <b>중력은 파티클과 같아야 합니다.</b> 이 씬의 물줄기는 gravityModifier 가 1.1 이라
        /// 10.79m/s^2 로 떨어집니다. 여기서 9.81 을 쓰면 레이가 10% 덜 처져 자국이
        /// 물이 실제로 떨어지는 자리보다 <b>앞쪽</b>에 찍힙니다.
        /// </summary>
        private bool TraceArc(Vector3 origin, Vector3 direction, float speed, float gravityScale,
                              out RaycastHit hit)
        {
            const int MaxSteps = 32;

            /// 한 토막의 목표 길이(m). 짧을수록 곡선을 잘 따라가지만 레이가 늘어납니다.
            const float SegmentLength = 0.75f;

            /// 노즐보다 이만큼 아래로 떨어지면 포기합니다. 절벽 아래로 무한히 쫓지 않습니다.
            const float MaxDrop = 20f;

            Vector3 p = origin;
            Vector3 v = direction.normalized * Mathf.Max(speed, 0.1f);
            Vector3 g = Physics.gravity * Mathf.Max(gravityScale, 0.01f);

            float stepTime = SegmentLength / Mathf.Max(speed, 0.5f);

            for (int i = 0; i < MaxSteps; i++)
            {
                Vector3 next = p + v * stepTime + 0.5f * g * stepTime * stepTime;

                Vector3 seg = next - p;
                float len = seg.magnitude;
                if (len > 0.0001f &&
                    Physics.Raycast(p, seg / len, out hit, len, _surfaceMask, QueryTriggerInteraction.Ignore))
                    return true;

                v += g * stepTime;
                p = next;

                if (origin.y - p.y > MaxDrop) break;
            }

            hit = default(RaycastHit);
            return false;
        }

        /// <summary>새 자국을 찍고 그 자리를 돌려줍니다.</summary>
        private int Stamp(Vector3 at, Vector3 normal, float drip)
        {
            int i = _next;
            _next = (_next + 1) % _pool.Length;

            Splat s = _pool[i];
            s.Alive = true;
            s.Age = 0f;
            s.WetSeconds = 0f;
            s.Drip = drip;
            s.Seed = Random.Range(0f, 64f);

            s.BodyRadius = startBodyDiameter * 0.5f;

            // <b>줄기 머리를 여기서 못 박습니다.</b> 이 뒤로 다시 계산하지 않습니다.
            s.DripStart = s.BodyRadius * 0.4f;

            s.DripReach = drip > 0.001f ? startDripReach : 0f;

            s.Renderer.enabled = true;
            s.Anchor = at;
            s.Normal = normal;

            Push(ref s);

            _pool[i] = s;
            return i;
        }

        /// <summary>
        /// 한자리에 계속 누고 있는 자국을 키웁니다.
        ///
        /// <b>몸통과 줄기는 서로를 참조하지 않습니다.</b> 각자 시간만 봅니다.
        /// 예전에는 줄기 길이가 몸통 반지름의 배수였기 때문에, 웅덩이가 넓어질 때마다
        /// 이미 흘러내린 줄기가 함께 길어지고 굵어지고 자리까지 옮겼습니다.
        /// </summary>
        private void Grow(int i, float flow, float deltaTime)
        {
            Splat s = _pool[i];

            s.BodyRadius = Mathf.Min(maxBodyDiameter * 0.5f,
                                     s.BodyRadius + bodyGrowPerSecond * 0.5f * flow * deltaTime);

            // <b>젖은 시간은 되감지 않습니다.</b> 나이는 되감아도(계속 적셔지므로)
            // 줄기 길이는 절대 줄면 안 됩니다 — 줄면 이미 흘러내린 꼬리가 뒤로 빨려 들어갑니다.
            s.WetSeconds += flow * deltaTime;
            s.DripReach = s.Drip > 0.001f
                ? Mathf.Min(maxDripReach, startDripReach + dripReachPerSecond * s.WetSeconds)
                : 0f;

            // 키우는 동안은 오히려 되젖습니다. 계속 적셔지고 있으니까요.
            s.Age = Mathf.Max(0f, s.Age - deltaTime / Mathf.Max(dryDuration, 0.01f));

            Push(ref s);

            _pool[i] = s;
        }

        /// <summary>
        /// 자세를 잡고, <b>그 자세에서 나온 값까지</b> 함께 셰이더로 밀어 넣습니다.
        ///
        /// 순서가 중요합니다. _BodyOffsetY 는 판 크기에서 나오므로 Place 가 먼저 돌아야
        /// 하고, 그 값을 같은 프레임에 넘겨야 합니다. 블록을 먼저 채우고 나중에 Place 를
        /// 부르면 앵커가 <b>한 프레임 어긋납니다.</b>
        /// </summary>
        private void Push(ref Splat s)
        {
            Place(ref s);

            s.Block.SetFloat(BodyRadiusId, s.BodyRadius);
            s.Block.SetFloat(AgeId, s.Age);
            s.Block.SetFloat(DripId, s.Drip);
            s.Block.SetFloat(DripStartId, s.DripStart);
            s.Block.SetFloat(DripReachId, s.DripReach);
            s.Block.SetFloat(SeedId, s.Seed);
            s.Block.SetFloat(BodyOffsetId, s.BodyOffsetY);
            s.Renderer.SetPropertyBlock(s.Block);
        }

        /// <summary>
        /// 판의 자세와 크기를 통째로 다시 잡습니다.
        ///
        /// <b>판은 종이일 뿐입니다.</b> 여기서 정하는 것은 그릴 자리가 얼마나 필요한가 뿐이고,
        /// 무엇을 어떻게 그릴지는 셰이더가 앵커 기준 미터로 정합니다. 그래서 판이 커져도
        /// 이미 그려진 것은 한 픽셀도 안 움직입니다.
        ///
        /// <b>크기 계산은 SplatQuadLayout 이 합니다.</b> 예전에는 이 클래스와 SplatCapture 가
        /// 늘이는 비율을 각자 적어 두고 주석으로만 묶여 있었습니다.
        ///
        /// <b>트랜스폼에서 되읽지 않습니다.</b> 위쪽 축을 Root.up 으로 읽으면 이미 틀어진
        /// 트랜스폼의 값을 그대로 믿게 됩니다. 보관해 둔 법선에서 매번 새로 셉니다.
        /// </summary>
        private void Place(ref Splat s)
        {
            // 판의 앞면이 면을 보게 눕히되 <b>위쪽은 언제나 월드 위</b>로 둡니다.
            // 그래야 셰이더가 아래로 뻗는 줄기가 중력 방향과 맞습니다.
            // 바닥처럼 법선이 위와 나란하면 LookRotation 이 풀 수 없으므로 다른 축을 줍니다.
            Vector3 up = Mathf.Abs(Vector3.Dot(s.Normal, Vector3.up)) > 0.98f ? Vector3.forward : Vector3.up;
            Quaternion rotation = Quaternion.LookRotation(-s.Normal, up);

            Vector2 size;
            float centerLift;
            float bodyOffsetY;
            SplatQuadLayout.Resolve(s.BodyRadius, _edgeBite, s.DripStart, s.DripReach, s.Drip,
                                    out size, out centerLift, out bodyOffsetY);

            s.BodyOffsetY = bodyOffsetY;

            Vector3 quadUp = rotation * Vector3.up;
            s.Root.SetPositionAndRotation(s.Anchor + quadUp * centerLift, rotation);
            s.Root.localScale = new Vector3(size.x, size.y, 1f);
        }

        /// <summary>모든 자국을 조금씩 말립니다.</summary>
        private void AgeAll(float deltaTime)
        {
            if (_pool == null) return;

            float step = deltaTime / Mathf.Max(dryDuration, 0.01f);

            for (int i = 0; i < _pool.Length; i++)
            {
                if (!_pool[i].Alive) continue;
                if (i == _active) continue;   // 지금 적셔지고 있는 자국은 건너뜁니다.

                Splat s = _pool[i];
                s.Age += step;

                if (s.Age >= 1f)
                {
                    s.Alive = false;
                    s.Renderer.enabled = false;
                }
                else
                {
                    s.Block.SetFloat(AgeId, s.Age);
                    s.Renderer.SetPropertyBlock(s.Block);
                }

                _pool[i] = s;
            }
        }

        /// <summary>
        /// 자국 판들을 미리 만들어 둡니다.
        ///
        /// <b>판은 이 컴포넌트의 자식이 아닙니다.</b> 이 컴포넌트는 플레이어 몸통에 붙어 있고
        /// 그 몸통은 걸을 때 움직이고 마우스 좌우에 따라 돕니다. 자식으로 두면 월드 자세를
        /// 아무리 정확히 넣어도 부모를 따라 끌려가, 자국이 <b>카메라를 따라다닙니다.</b>
        /// 씬 루트에 움직이지 않는 통을 하나 세우고 거기에 담습니다.
        /// </summary>
        private void BuildPool()
        {
            _world = new GameObject("UrineSplats (World)").transform;

            Mesh quad = BuildQuad();
            _pool = new Splat[Mathf.Max(4, maxSplats)];

            for (int i = 0; i < _pool.Length; i++)
            {
                GameObject go = new GameObject("Splat_" + i);
                go.transform.SetParent(_world, false);

                MeshFilter mf = go.AddComponent<MeshFilter>();
                mf.sharedMesh = quad;

                MeshRenderer mr = go.AddComponent<MeshRenderer>();
                mr.sharedMaterial = splatMaterial;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                mr.receiveShadows = false;
                mr.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
                mr.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
                mr.enabled = false;

                _pool[i] = new Splat
                {
                    Root = go.transform,
                    Renderer = mr,
                    Block = new MaterialPropertyBlock(),
                    Alive = false,
                };
            }
        }

        /// <summary>가운데가 원점인 1x1 판입니다. 유니티 기본 Quad 와 같은 모양입니다.</summary>
        private static Mesh BuildQuad()
        {
            Mesh m = new Mesh();
            m.name = "SplatQuad";
            m.vertices = new[]
            {
                new Vector3(-0.5f, -0.5f, 0f), new Vector3(0.5f, -0.5f, 0f),
                new Vector3(-0.5f,  0.5f, 0f), new Vector3(0.5f,  0.5f, 0f),
            };
            m.uv = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 1), new Vector2(1, 1) };
            m.triangles = new[] { 0, 2, 1, 2, 3, 1 };
            m.RecalculateNormals();
            m.RecalculateBounds();
            return m;
        }
    }
}
