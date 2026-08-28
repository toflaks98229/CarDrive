using UnityEngine;
using UnityEngine.Experimental.Rendering;
using CarDrive.Common;

namespace CarDrive.Systems
{
    /// <summary>
    /// 세계를 위에서 내려다본 <b>젖음 지도</b> 한 장을 들고, 거기에 칠하고 서서히 지웁니다.
    ///
    /// <b>무엇을 대신하는가.</b> 지금까지 오줌 자국은 닿는 자리마다 판(quad)을 눕혀 그렸습니다.
    /// 그 방식은 (1) 자국 수만큼 드로우가 늘고, (2) 고정 크기 풀을 돌려 써서 한 바퀴가 돌면
    /// 살아 있는 자국이 그대로 사라지며, (3) 판은 평평한데 터레인은 굽어 있어 비탈에서
    /// 모서리가 땅을 파고듭니다. 지도 한 장에 값을 더하는 방식은 셋 다 없습니다.
    ///
    /// <b>왜 전역 텍스처인가.</b> Unity Terrain 은 MaterialPropertyBlock 을 받지 않습니다.
    /// 타일이 103장이라 타일마다 다른 값을 물리려면 머티리얼 인스턴스를 103벌 들어야 합니다.
    /// 전역으로 두면 지형·도로·소품이 셰이더 한 줄로 같은 지도를 읽습니다.
    ///
    /// <b>커버 범위는 정하기 나름이고, 기본값은 창(Window)입니다.</b> 지형 전체는
    /// 1100m x 1200m 라, 4096제곱(17MB)을 써도 27cm/텍셀입니다. 갓 찍힌 자국이 14cm 이니
    /// <b>반 텍셀</b>입니다 — 형체가 남지 않습니다. 같은 4MB 로 플레이어 둘레 128m 를
    /// 2048제곱으로 덮으면 6.2cm/텍셀이라 자국이 2.2텍셀입니다. 전체를 덮고 싶으면
    /// <see cref="coverage"/> 를 Fixed 로 두면 되지만, 그때는 해상도를 올리고 자국 크기 자체를
    /// 키워야 합니다.
    ///
    /// <b>DI.</b> 싱글톤 프로퍼티를 만들지 않고 <see cref="GameContext"/> 에 등록합니다.
    /// 쓰는 쪽은 <c>GameContext.Get&lt;SplatManager&gt;()</c> 로 <b>느슨하게</b> 찾고,
    /// 없으면 없는 대로 돌아가야 합니다 — 이 시스템은 연출이지 규칙이 아닙니다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SplatManager : MonoBehaviour
    {
        // --- Constants ---

        /// <summary>컴퓨트 셰이더 파일 이름입니다. Resources 아래에 있어야 합니다.</summary>
        private const string ShaderName = "SplatPainter";

        /// <summary>.compute 의 numthreads 와 같아야 합니다.</summary>
        private const int ThreadGroupSize = 8;

        /// <summary>요청 하나의 바이트 수입니다. float2 + float + float.</summary>
        private const int RequestStride = 16;

        // --- Types ---

        /// <summary>지도가 세계의 어디를 덮는가.</summary>
        public enum CoverageMode
        {
            /// <summary>고정된 월드 사각형을 덮습니다. 넓게 덮는 대신 텍셀이 거칠어집니다.</summary>
            Fixed = 0,

            /// <summary>대상을 따라다니는 창을 덮습니다. 좁게 덮는 대신 텍셀이 촘촘합니다.</summary>
            FollowTarget = 1,
        }

        /// <summary>
        /// 칠할 자리 하나입니다.
        /// <b>SplatPainter.compute 의 SplatRequest 와 같은 순서·같은 크기(16바이트)여야 합니다.</b>
        /// </summary>
        private struct SplatRequest
        {
            public Vector2 Uv;
            public float RadiusUV;
            public float Amount;
        }

        // --- Serialized Fields ---

        /// <summary>지도가 세계의 어디를 덮는지.</summary>
        [Header("커버 범위")]
        [Tooltip("Fixed = 고정된 월드 사각형, FollowTarget = 대상을 따라다니는 창")]
        [SerializeField]
        private CoverageMode coverage = CoverageMode.FollowTarget;

        /// <summary>따라다닐 대상입니다. 비워두면 메인 카메라를 씁니다.</summary>
        [Tooltip("FollowTarget 일 때 따라다닐 대상. 비워두면 메인 카메라")]
        [SerializeField]
        private Transform followTarget;

        /// <summary>Fixed 일 때 덮을 월드 사각형의 구석입니다.</summary>
        [Tooltip("Fixed 일 때 덮을 사각형의 구석 (월드 x, z)")]
        [SerializeField]
        private Vector2 fixedOrigin = new Vector2(-300f, -300f);

        /// <summary>Fixed 일 때 덮을 월드 사각형의 크기(m)입니다.</summary>
        [Tooltip("Fixed 일 때 덮을 사각형의 크기(m). 지형 전체는 1100 x 1200")]
        [SerializeField]
        private Vector2 fixedSize = new Vector2(1100f, 1200f);

        /// <summary>FollowTarget 일 때 창 한 변의 길이(m)입니다.</summary>
        [Tooltip("FollowTarget 일 때 창 한 변의 길이(m)")]
        [Range(32f, 512f)]
        [SerializeField]
        private float windowMeters = 128f;

        /// <summary>
        /// 창을 다시 놓기까지 대상이 움직여야 하는 거리(m)입니다.
        /// 매 프레임 옮기면 <b>매 프레임 지도가 비워집니다.</b>
        /// </summary>
        [Tooltip("창을 다시 놓기까지 대상이 움직여야 하는 거리(m)")]
        [SerializeField]
        private float windowRecenterDistance = 40f;

        /// <summary>지도 한 변의 텍셀 수입니다.</summary>
        [Header("지도")]
        [Tooltip("지도 한 변의 텍셀 수. 2048 이면 R8 로 4MB")]
        [SerializeField]
        private int resolution = 2048;

        /// <summary>흠뻑 젖은 자리가 다 마르는 데 걸리는 시간(초)입니다.</summary>
        [Tooltip("흠뻑 젖은 자리가 다 마르는 데 걸리는 시간(초)")]
        [SerializeField]
        private float dryDuration = 26f;

        /// <summary>마름을 몇 초마다 처리할지입니다.</summary>
        [Tooltip("마름을 몇 초마다 처리할지. 짧을수록 부드럽고 비쌉니다.")]
        [Range(0.02f, 1f)]
        [SerializeField]
        private float fadeInterval = 0.1f;

        /// <summary>한 프레임에 받을 수 있는 요청 수입니다.</summary>
        [Tooltip("한 프레임에 받을 수 있는 요청 수. 넘으면 버립니다.")]
        [Range(8, 256)]
        [SerializeField]
        private int maxRequestsPerFrame = 64;

        // --- Private Member Variables ---

        private ComputeShader _shader;
        private int _paintKernel = -1;
        private int _fadeKernel = -1;

        private RenderTexture _map;
        private ComputeBuffer _requestBuffer;

        /// <summary>이번 프레임에 모은 요청입니다. <b>한 번만 잡고 계속 씁니다.</b></summary>
        private SplatRequest[] _requests;
        private int _requestCount;

        /// <summary>이번 프레임 요청들이 걸친 텍셀 범위입니다.</summary>
        private RectInt _paintBounds;

        /// <summary>지금까지 칠한 적 있는 텍셀 범위입니다. 마름은 여기만 돕니다.</summary>
        private RectInt _wetBounds;
        private bool _hasWet;

        /// <summary>마지막으로 칠한 시각입니다. 다 마를 때까지 조용하면 범위를 비웁니다.</summary>
        private float _lastPaintTime;

        private float _nextFadeTime;

        /// <summary>마지막으로 말린 시각입니다. <b>실제 경과</b>를 재려고 들고 있습니다.</summary>
        private float _lastFadeTime;

        /// <summary>
        /// 마지막으로 칠한 뒤로 깎아 낸 총량입니다.
        /// 1 을 넘으면 아무리 진했어도 다 마른 것이므로 범위를 놓습니다.
        /// </summary>
        private float _fadedSincePaint;

        /// <summary>지금 지도가 덮고 있는 월드 사각형입니다.</summary>
        private Vector2 _origin;
        private Vector2 _size;

        /// <summary>요청을 버렸음을 프레임당 한 번만 알리기 위한 표시입니다.</summary>
        private bool _warnedOverflowThisFrame;

        private bool _ready;

        private static readonly int RequestsId = Shader.PropertyToID("_Requests");
        private static readonly int RequestCountId = Shader.PropertyToID("_RequestCount");
        private static readonly int SplatMapId = Shader.PropertyToID("_SplatMap");
        private static readonly int MapSizeId = Shader.PropertyToID("_MapSize");
        private static readonly int PaintRectId = Shader.PropertyToID("_PaintRect");
        private static readonly int FadeRectId = Shader.PropertyToID("_FadeRect");
        private static readonly int FadeAmountId = Shader.PropertyToID("_FadeAmount");

        private static readonly int GlobalMapId = Shader.PropertyToID("_GlobalSplatMap");
        private static readonly int GlobalRectId = Shader.PropertyToID("_GlobalSplatMapRect");
        private static readonly int GlobalOnId = Shader.PropertyToID("_GlobalSplatMapOn");

        // --- Public Properties ---

        /// <summary>지도를 실제로 쓸 수 있는지입니다. 못 쓰면 <see cref="Paint"/> 는 아무 일도 안 합니다.</summary>
        public bool IsReady { get { return _ready; } }

        // --- Unity Event Functions ---

        void Awake()
        {
            GameContext.Register(this);
            Setup();
        }

        void OnDestroy()
        {
            GameContext.Unregister(this);
            Teardown();
        }

        void LateUpdate()
        {
            _warnedOverflowThisFrame = false;

            if (!_ready) return;

            UpdateWindow();
            FlushRequests();
            FadeIfDue();
        }

        // --- Public Methods ---

        /// <summary>
        /// 이 월드 자리를 적십니다. 같은 프레임에 여러 번 불러도 됩니다 —
        /// 모아 두었다가 <c>LateUpdate</c> 에서 <b>한 번만</b> GPU 로 보냅니다.
        /// </summary>
        /// <param name="worldPos">적실 자리. y 는 쓰지 않습니다(위에서 내려다본 투영).</param>
        /// <param name="radiusMeters">반경(m)</param>
        /// <param name="amount">이번에 더할 젖음(0~1)</param>
        public void Paint(Vector3 worldPos, float radiusMeters, float amount)
        {
            if (!_ready) return;
            if (amount <= 0f || radiusMeters <= 0f) return;

            Vector2 uv = WorldToUv(worldPos, _origin, _size);

            // 창 밖은 담지 않습니다. 담아 봐야 컴퓨트가 버립니다.
            if (uv.x < 0f || uv.x > 1f || uv.y < 0f || uv.y > 1f) return;

            if (_requestCount >= _requests.Length)
            {
                // <b>매 프레임 문자열을 만들지 않습니다.</b> 넘치는 상황은 대개 매 프레임
                // 이어지므로, 여기서 로그를 조립하면 그것 자체가 프레임 예산을 먹습니다.
                if (!_warnedOverflowThisFrame)
                {
                    _warnedOverflowThisFrame = true;
                    GameLog.Warn(GameLog.Channel.World,
                        "SplatManager: 한 프레임 요청이 상한을 넘어 버렸습니다.", this);
                }
                return;
            }

            // 반경은 <b>가로 기준</b> UV 로 넘깁니다. 컴퓨트가 세로만 비율로 되돌립니다.
            float radiusUV = radiusMeters / Mathf.Max(_size.x, 0.001f);

            _requests[_requestCount].Uv = uv;
            _requests[_requestCount].RadiusUV = radiusUV;
            _requests[_requestCount].Amount = Mathf.Clamp01(amount);
            _requestCount++;

            AccumulateBounds(uv, radiusUV);
        }

        // --- Public Static Methods : 순수 계산 ---

        /// <summary>
        /// 월드 좌표를 지도 UV 로 바꿉니다.
        ///
        /// <b>이 식은 CarDriveSplatMap.hlsl 과 글자 그대로 같아야 합니다.</b>
        /// 한쪽만 고치면 컴파일도 되고 화면도 그럴듯한데 자국이 엉뚱한 자리에 찍힙니다.
        /// 그래서 static 으로 떼어 두었습니다 — 씬도 그래픽 장치도 없이 검사할 수 있습니다.
        /// </summary>
        public static Vector2 WorldToUv(Vector3 worldPos, Vector2 origin, Vector2 size)
        {
            return new Vector2(
                (worldPos.x - origin.x) / Mathf.Max(size.x, 0.001f),
                (worldPos.z - origin.y) / Mathf.Max(size.y, 0.001f));
        }

        // --- Private Methods ---

        /// <summary>지도와 컴퓨트를 준비합니다. 하나라도 안 되면 조용히 물러섭니다.</summary>
        private void Setup()
        {
            if (!SystemInfo.supportsComputeShaders)
            {
                Fallback("이 기기는 컴퓨트 셰이더를 못 씁니다.");
                return;
            }

            _shader = Resources.Load<ComputeShader>(ShaderName);
            if (_shader == null)
            {
                Fallback(ShaderName + ".compute 를 Resources 에서 찾지 못했습니다.");
                return;
            }

            _paintKernel = _shader.FindKernel("PaintSplat");
            _fadeKernel = _shader.FindKernel("FadeSplat");

            GraphicsFormat format = PickFormat();
            if (format == GraphicsFormat.None)
            {
                Fallback("이 기기는 자국 지도에 쓸 수 있는 단일 채널 포맷이 없습니다.");
                return;
            }

            resolution = Mathf.Clamp(Mathf.ClosestPowerOfTwo(resolution), 256, 8192);

            _map = new RenderTexture(resolution, resolution, 0, format);
            _map.name = "GlobalSplatMap";
            _map.enableRandomWrite = true;

            // 밉을 두지 않습니다. 매 프레임 덧칠하는 대상이라 사슬을 다시 만드는 값이 아깝고,
            // 읽는 쪽(CarDriveSplatMap.hlsl)도 LOD 0 으로 못박아 두었습니다.
            _map.useMipMap = false;
            _map.autoGenerateMips = false;

            // 맵 밖은 읽는 쪽에서 자르지만, 가장자리가 번지지 않도록 여기서도 막아 둡니다.
            _map.wrapMode = TextureWrapMode.Clamp;
            _map.filterMode = FilterMode.Bilinear;

            if (!_map.Create())
            {
                Fallback("자국 지도를 만들지 못했습니다.");
                return;
            }

            Clear();

            _requests = new SplatRequest[Mathf.Max(8, maxRequestsPerFrame)];
            _requestBuffer = new ComputeBuffer(_requests.Length, RequestStride);

            RecenterWindow(true);
            _lastFadeTime = Time.time;
            _ready = true;

            Shader.SetGlobalTexture(GlobalMapId, _map);
            PushRect();
            Shader.SetGlobalFloat(GlobalOnId, 1f);
        }

        /// <summary>못 쓰게 됐음을 알리고 셰이더가 건너뛰게 합니다.</summary>
        private void Fallback(string reason)
        {
            _ready = false;
            Shader.SetGlobalFloat(GlobalOnId, 0f);

            GameLog.Warn(GameLog.Channel.World,
                "SplatManager: " + reason + " 젖은 자국 지도 없이 돌아갑니다.", this);
        }

        private void Teardown()
        {
            Shader.SetGlobalFloat(GlobalOnId, 0f);
            _ready = false;

            if (_requestBuffer != null) { _requestBuffer.Release(); _requestBuffer = null; }
            if (_map != null) { _map.Release(); Destroy(_map); _map = null; }
        }

        /// <summary>LoadStore(랜덤 쓰기)를 지원하는 단일 채널 포맷을 고릅니다.</summary>
        private static GraphicsFormat PickFormat()
        {
            // 값은 0~1 하나뿐이라 R8 이면 충분합니다. 안 되면 정밀도를 올려 물러섭니다.
            if (SystemInfo.IsFormatSupported(GraphicsFormat.R8_UNorm, GraphicsFormatUsage.LoadStore))
                return GraphicsFormat.R8_UNorm;
            if (SystemInfo.IsFormatSupported(GraphicsFormat.R16_SFloat, GraphicsFormatUsage.LoadStore))
                return GraphicsFormat.R16_SFloat;
            if (SystemInfo.IsFormatSupported(GraphicsFormat.R32_SFloat, GraphicsFormatUsage.LoadStore))
                return GraphicsFormat.R32_SFloat;

            return GraphicsFormat.None;
        }

        /// <summary>지도를 통째로 비웁니다.</summary>
        private void Clear()
        {
            RenderTexture prev = RenderTexture.active;
            RenderTexture.active = _map;
            GL.Clear(false, true, Color.clear);
            RenderTexture.active = prev;

            _hasWet = false;
        }

        /// <summary>지금 창이 덮는 월드 사각형을 전역으로 올립니다.</summary>
        private void PushRect()
        {
            // <b>역수로 넘깁니다.</b> 픽셀마다 나누지 않으려는 것입니다.
            Shader.SetGlobalVector(GlobalRectId, new Vector4(
                _origin.x, _origin.y,
                1f / Mathf.Max(_size.x, 0.001f),
                1f / Mathf.Max(_size.y, 0.001f)));
        }

        /// <summary>대상이 충분히 움직였으면 창을 다시 놓습니다.</summary>
        private void UpdateWindow()
        {
            if (coverage != CoverageMode.FollowTarget) return;

            Vector3 at = CurrentTargetPosition();
            Vector2 center = _origin + _size * 0.5f;

            float dx = at.x - center.x;
            float dz = at.z - center.y;

            if (dx * dx + dz * dz < windowRecenterDistance * windowRecenterDistance) return;

            RecenterWindow(false);
        }

        /// <summary>
        /// 창을 대상 자리로 옮깁니다.
        ///
        /// <b>옮기면 칠한 것이 사라집니다.</b> 지도는 창 기준 UV 로 저장돼 있어서, 창이 움직이면
        /// 같은 텍셀이 다른 월드 자리를 가리킵니다. 옮긴 만큼 내용을 밀어 주는(토러스) 방식도
        /// 있지만 지도를 한 장 더 들고 매번 복사해야 합니다.
        /// 자국은 <see cref="dryDuration"/> 이면 어차피 마르고, 창 밖은 64m 넘게 떨어진 곳이라
        /// <b>그 값을 치를 만큼 자주 보이지 않습니다.</b> 그래서 통째로 비웁니다.
        /// <see cref="windowRecenterDistance"/> 가 이 일이 잦아지지 않게 막습니다.
        /// </summary>
        private void RecenterWindow(bool initial)
        {
            if (coverage == CoverageMode.Fixed)
            {
                _origin = fixedOrigin;
                _size = fixedSize;
            }
            else
            {
                Vector3 at = CurrentTargetPosition();
                _size = new Vector2(windowMeters, windowMeters);
                _origin = new Vector2(at.x - windowMeters * 0.5f, at.z - windowMeters * 0.5f);
            }

            PushRect();

            if (!initial && _map != null) Clear();
        }

        private Vector3 CurrentTargetPosition()
        {
            if (followTarget != null) return followTarget.position;

            Camera cam = GameContext.MainCamera;
            return cam != null ? cam.transform.position : Vector3.zero;
        }

        /// <summary>이번 요청이 걸치는 텍셀 범위를 모읍니다.</summary>
        private void AccumulateBounds(Vector2 uv, float radiusUV)
        {
            // 세로는 가로 기준 반경을 비율로 되돌려야 합니다(컴퓨트와 같은 셈).
            float rx = radiusUV;
            float ry = radiusUV;   // 지도가 정사각이라 같습니다. 정사각이 아니면 여기도 갈라야 합니다.

            int x0 = Mathf.FloorToInt((uv.x - rx) * resolution) - 1;
            int y0 = Mathf.FloorToInt((uv.y - ry) * resolution) - 1;
            int x1 = Mathf.CeilToInt((uv.x + rx) * resolution) + 1;
            int y1 = Mathf.CeilToInt((uv.y + ry) * resolution) + 1;

            x0 = Mathf.Clamp(x0, 0, resolution - 1);
            y0 = Mathf.Clamp(y0, 0, resolution - 1);
            x1 = Mathf.Clamp(x1, 1, resolution);
            y1 = Mathf.Clamp(y1, 1, resolution);

            RectInt r = new RectInt(x0, y0, Mathf.Max(1, x1 - x0), Mathf.Max(1, y1 - y0));

            _paintBounds = _requestCount == 1 ? r : Union(_paintBounds, r);
            _wetBounds = _hasWet ? Union(_wetBounds, r) : r;
            _hasWet = true;
            _lastPaintTime = Time.time;
            _fadedSincePaint = 0f;
        }

        private static RectInt Union(RectInt a, RectInt b)
        {
            int x0 = Mathf.Min(a.xMin, b.xMin);
            int y0 = Mathf.Min(a.yMin, b.yMin);
            int x1 = Mathf.Max(a.xMax, b.xMax);
            int y1 = Mathf.Max(a.yMax, b.yMax);
            return new RectInt(x0, y0, x1 - x0, y1 - y0);
        }

        /// <summary>모인 요청을 한 번만 디스패치합니다.</summary>
        private void FlushRequests()
        {
            if (_requestCount == 0) return;

            // 앞부분만 올립니다. 버퍼 전체를 올리면 안 쓰는 자리까지 매 프레임 실어 나릅니다.
            _requestBuffer.SetData(_requests, 0, 0, _requestCount);

            _shader.SetBuffer(_paintKernel, RequestsId, _requestBuffer);
            _shader.SetTexture(_paintKernel, SplatMapId, _map);
            _shader.SetInt(RequestCountId, _requestCount);
            _shader.SetInts(MapSizeId, resolution, resolution);
            _shader.SetInts(PaintRectId,
                _paintBounds.xMin, _paintBounds.yMin, _paintBounds.width, _paintBounds.height);

            Dispatch(_paintKernel, _paintBounds);

            _requestCount = 0;
        }

        /// <summary>때가 되면 젖은 범위만 조금 말립니다.</summary>
        private void FadeIfDue()
        {
            if (!_hasWet) return;
            if (Time.time < _nextFadeTime) return;

            // <b>예정된 주기가 아니라 실제 경과로 깎습니다.</b> 처음에는 fadeInterval 을
            // 그대로 썼는데, 프레임이 그보다 느리면 예정보다 적게 돌아 <b>영영 안 마릅니다.</b>
            // 실제로 시험에서 다 말라야 할 시간이 지나도 젖어 있었습니다.
            float elapsed = Mathf.Min(Time.time - _lastFadeTime, dryDuration);
            _lastFadeTime = Time.time;
            _nextFadeTime = Time.time + fadeInterval;

            if (elapsed <= 0f) return;

            // 마르는 시간이 주기와 무관하게 같도록 경과에 비례해 깎습니다.
            float amount = elapsed / Mathf.Max(dryDuration, 0.01f);

            _shader.SetTexture(_fadeKernel, SplatMapId, _map);
            _shader.SetInts(MapSizeId, resolution, resolution);
            _shader.SetFloat(FadeAmountId, amount);
            _shader.SetInts(FadeRectId,
                _wetBounds.xMin, _wetBounds.yMin, _wetBounds.width, _wetBounds.height);

            Dispatch(_fadeKernel, _wetBounds);

            // <b>깎아 낸 총량으로 판정합니다.</b> "칠한 지 dryDuration 지났으면" 으로 두면,
            // 마름이 조금이라도 뒤처졌을 때 <b>덜 마른 채로 멈춰</b> 그 얼룩이 영원히 남습니다.
            // 총량이 1 을 넘었으면 아무리 진했어도(값의 상한이 1) 다 지워진 것이 확실합니다.
            _fadedSincePaint += amount;
            if (_fadedSincePaint >= 1f) _hasWet = false;
        }

        private void Dispatch(int kernel, RectInt rect)
        {
            int gx = (rect.width + ThreadGroupSize - 1) / ThreadGroupSize;
            int gy = (rect.height + ThreadGroupSize - 1) / ThreadGroupSize;

            if (gx <= 0 || gy <= 0) return;

            _shader.Dispatch(kernel, gx, gy, 1);
        }
    }
}
