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

        /// <summary>갓 찍힌 자국의 지름(m)입니다.</summary>
        [Header("크기")]
        [Tooltip("갓 찍힌 자국의 지름(m)")]
        public float startDiameter = 0.3f;

        /// <summary>한자리에 계속 누었을 때 자랄 수 있는 최대 지름(m)입니다.</summary>
        [Tooltip("한자리에 계속 누었을 때 자랄 수 있는 최대 지름(m)")]
        public float maxDiameter = 1.4f;

        /// <summary>초당 자라는 지름(m)입니다. 출력이 셀수록 빨리 자랍니다.</summary>
        [Tooltip("초당 자라는 지름(m). 출력이 셀수록 빨라집니다.")]
        public float growPerSecond = 0.5f;

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

        /// <summary>돌려 쓰는 자국 판들입니다.</summary>
        private Splat[] _pool;

        /// <summary>다음에 꺼내 쓸 자리입니다.</summary>
        private int _next;

        /// <summary>지금 키우고 있는 자국입니다. 없으면 -1 입니다.</summary>
        private int _active = -1;

        /// <summary>
        /// 벽에서 판을 세로로 늘이는 배수입니다.
        ///
        /// <b>왜 늘이는가.</b> 흘러내리는 줄기는 몸통 <b>아래로</b> 뻗어야 보입니다.
        /// 정사각 판에 그리면 줄기가 자랄 자리가 반쪽밖에 없어 몸통에 묻혀 버립니다.
        /// 실제로 그렇게 나왔습니다 — 자국 아래가 조금 두꺼워졌을 뿐 줄기로 안 읽혔습니다.
        ///
        /// <b>SplatCapture 의 WallAspect 와 같아야 합니다.</b> 다르면 눈으로 골라 둔 값이
        /// 게임에서 다른 그림이 됩니다.
        /// </summary>
        private const float WallAspect = 2.2f;

        /// <summary>늘인 판 안에서 몸통을 위로 올리는 정도입니다(판 좌표 -1~1 기준).</summary>
        private const float WallBodyOffsetY = 0.5f;

        /// <summary>프로퍼티 이름은 매 프레임 문자열로 찾지 않습니다.</summary>
        private static readonly int GrowId = Shader.PropertyToID("_Grow");
        private static readonly int AgeId = Shader.PropertyToID("_Age");
        private static readonly int DripId = Shader.PropertyToID("_DripAmount");
        private static readonly int SeedId = Shader.PropertyToID("_Seed");
        private static readonly int AspectId = Shader.PropertyToID("_Aspect");
        private static readonly int BodyOffsetId = Shader.PropertyToID("_BodyOffsetY");

        /// <summary>자국 한 장이 들고 있는 것들입니다.</summary>
        private struct Splat
        {
            public Transform Root;
            public MeshRenderer Renderer;
            public MaterialPropertyBlock Block;
            public float Age;
            public float Diameter;
            public float Aspect;

            /// <summary>물줄기가 실제로 닿은 자리입니다. 판이 커져도 여기는 안 움직입니다.</summary>
            public Vector3 Anchor;

            public bool Alive;
        }

        // --- Unity Event Functions ---

        void Awake()
        {
            BuildPool();
        }

        void LateUpdate()
        {
            AgeAll(Time.deltaTime);
        }

        // --- Public Methods ---

        /// <summary>
        /// 이번 프레임에 오줌이 닿는 자리를 찍습니다. 흐르는 동안 매 프레임 부르세요.
        /// </summary>
        /// <param name="origin">노즐 위치</param>
        /// <param name="direction">노즐이 향한 방향</param>
        /// <param name="speed">입자 초기 속도(m/s)</param>
        /// <param name="flow">출력 비율(0~1)</param>
        /// <param name="deltaTime">이번 프레임의 시간</param>
        public void Mark(Vector3 origin, Vector3 direction, float speed, float flow, float deltaTime)
        {
            if (_pool == null || splatMaterial == null) return;
            if (flow <= 0.001f) { _active = -1; return; }

            RaycastHit hit;
            if (!TraceArc(origin, direction, speed, out hit)) { _active = -1; return; }

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
        /// 포물선을 몇 토막으로 끊어 레이캐스트합니다.
        ///
        /// <b>직선 하나로는 안 됩니다.</b> 줄기는 중력을 받아 휘므로 직선으로 재면
        /// 실제로 떨어지는 자리보다 훨씬 멀리 찍힙니다.
        /// </summary>
        private bool TraceArc(Vector3 origin, Vector3 direction, float speed, out RaycastHit hit)
        {
            const int Steps = 6;
            const float StepTime = 0.09f;

            Vector3 p = origin;
            Vector3 v = direction.normalized * Mathf.Max(speed, 0.1f);
            Vector3 g = Physics.gravity;

            for (int i = 0; i < Steps; i++)
            {
                Vector3 next = p + v * StepTime + 0.5f * g * StepTime * StepTime;

                Vector3 seg = next - p;
                float len = seg.magnitude;
                if (len > 0.0001f &&
                    Physics.Raycast(p, seg / len, out hit, len, _surfaceMask, QueryTriggerInteraction.Ignore))
                    return true;

                v += g * StepTime;
                p = next;
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
            s.Diameter = startDiameter;

            // 서 있는 면일수록 판을 세로로 늘여 줄기가 자랄 자리를 만듭니다.
            // 바닥이면 1 이라 정사각 그대로입니다.
            s.Aspect = Mathf.Lerp(1f, WallAspect, drip);

            // 판의 앞면이 면을 보게 눕히되 <b>위쪽은 언제나 월드 위</b>로 둡니다.
            // 그래야 셰이더가 아래로 뻗는 줄기가 중력 방향과 맞습니다.
            // 바닥처럼 법선이 위와 나란하면 LookRotation 이 풀 수 없으므로 다른 축을 줍니다.
            Vector3 up = Mathf.Abs(Vector3.Dot(normal, Vector3.up)) > 0.98f ? Vector3.forward : Vector3.up;
            s.Root.rotation = Quaternion.LookRotation(-normal, up);

            s.Renderer.enabled = true;

            s.Block.SetFloat(GrowId, 0.45f);
            s.Block.SetFloat(AgeId, 0f);
            s.Block.SetFloat(DripId, drip);
            s.Block.SetFloat(SeedId, Random.Range(0f, 64f));
            s.Block.SetFloat(AspectId, s.Aspect);
            s.Block.SetFloat(BodyOffsetId, Mathf.Lerp(0f, WallBodyOffsetY, drip));
            s.Renderer.SetPropertyBlock(s.Block);

            s.Anchor = at;
            Place(ref s);

            _pool[i] = s;
            return i;
        }

        /// <summary>한자리에 계속 누고 있는 자국을 키웁니다.</summary>
        private void Grow(int i, float flow, float deltaTime)
        {
            Splat s = _pool[i];

            s.Diameter = Mathf.Min(maxDiameter, s.Diameter + growPerSecond * flow * deltaTime);

            // 키우는 동안은 오히려 되젖습니다. 계속 적셔지고 있으니까요.
            s.Age = Mathf.Max(0f, s.Age - deltaTime / Mathf.Max(dryDuration, 0.01f));

            Place(ref s);

            // 판이 커지는 것만으로는 부족합니다. 셰이더의 반지름도 함께 열어야
            // 자국이 판 안에서도 차오릅니다.
            s.Block.SetFloat(GrowId,
                Mathf.Lerp(0.45f, 1f, Mathf.InverseLerp(startDiameter, maxDiameter, s.Diameter)));
            s.Block.SetFloat(AgeId, s.Age);
            s.Renderer.SetPropertyBlock(s.Block);

            _pool[i] = s;
        }

        /// <summary>
        /// 판의 크기와 자리를 다시 잡습니다.
        ///
        /// <b>얼룩의 한가운데가 늘 앵커에 있어야 합니다.</b> 벽에서는 판을 세로로 늘이고
        /// 그 안에서 몸통을 위로 올리므로, 판 한가운데는 앵커보다 아래에 놓입니다.
        /// 그만큼 내려 두지 않으면 자국이 실제로 닿은 자리보다 위에 찍힙니다.
        /// </summary>
        private void Place(ref Splat s)
        {
            float height = s.Diameter * s.Aspect;
            s.Root.localScale = new Vector3(s.Diameter, height, 1f);

            // 판 좌표 -1~1 이 높이 전체를 덮으므로, 올린 정도에 반높이를 곱한 만큼 내립니다.
            float lift = Mathf.Lerp(0f, WallBodyOffsetY, Mathf.InverseLerp(1f, WallAspect, s.Aspect));
            s.Root.position = s.Anchor - s.Root.up * (lift * height * 0.5f);
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

        /// <summary>자국 판들을 미리 만들어 둡니다.</summary>
        private void BuildPool()
        {
            Mesh quad = BuildQuad();
            _pool = new Splat[Mathf.Max(4, maxSplats)];

            for (int i = 0; i < _pool.Length; i++)
            {
                GameObject go = new GameObject("Splat_" + i);
                go.transform.SetParent(transform, false);

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
