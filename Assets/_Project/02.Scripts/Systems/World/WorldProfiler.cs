using System.Diagnostics;
using UnityEngine;

namespace CarDrive.Systems
{
    /// <summary>
    /// 월드 시스템이 <b>실제로 얼마나 자주 무엇을 하는지</b> 세어 둡니다.
    ///
    /// <b>왜 필요한가.</b> 지금까지의 최적화는 "타일을 너무 자주 켜고 끈다"는 진단 위에서
    /// 이뤄졌습니다. 그 진단은 코드를 읽어서 나온 것이고, 고친 뒤에도 <b>실제로 줄었는지는
    /// 확인하지 않았습니다.</b> 추측으로 고치기를 계속하면 언젠가 아무 이득이 없는 곳을
    /// 정교하게 다듬게 됩니다.
    ///
    /// 유니티 프로파일러는 "어느 함수가 몇 ms"를 알려 주지만, 이 프로젝트에서 알고 싶은 것은
    /// <b>"초당 몇 번 토글되는가"</b>입니다. 그 숫자는 프로파일러에 나오지 않습니다.
    ///
    /// <b>릴리즈 빌드에는 남지 않습니다.</b> 세는 메서드에 <c>[Conditional]</c> 이 걸려 있어
    /// 호출 자체가 컴파일 단계에서 사라집니다. (<see cref="Common.GameLog"/> 와 같은 방식)
    /// </summary>
    public static class WorldProfiler
    {
        // --- Constants ---

        /// <summary>프레임 시간을 담아 둘 창의 크기입니다. 60fps 기준 약 2초입니다.</summary>
        private const int FrameWindow = 120;

        /// <summary>이보다 오래 걸린 프레임을 <b>끊김</b>으로 셉니다. (30fps 미만)</summary>
        public const float HitchMilliseconds = 33.3f;

        /// <summary>이보다 오래 걸린 프레임을 <b>심한 끊김</b>으로 셉니다.</summary>
        public const float BadHitchMilliseconds = 100f;

        /// <summary>
        /// 기동 구간으로 볼 시간(초)입니다. 이 동안의 프레임은 <b>따로 셉니다.</b>
        ///
        /// <b>왜 나누는가.</b> 씬을 불러오고 첫 프레임을 그리는 동안에는 수백 ms 짜리 프레임이
        /// 반드시 나옵니다. 에셋 로드·셰이더 컴파일·타일 첫 활성화가 거기 다 몰려 있습니다.
        /// 그 값을 세션 최악에 섞으면 <b>그 뒤로 어떤 숫자를 봐도 의미가 없습니다</b> —
        /// 플레이 중에 40ms 가 나오든 400ms 가 나오든 최악값은 로딩 프레임 그대로입니다.
        ///
        /// 그래서 기동 구간은 <see cref="StartupWorstMs"/> 로 따로 두고, 플레이 구간의
        /// 숫자는 이 시간이 지난 뒤부터 셉니다. 기동 비용을 버리는 것이 아니라
        /// <b>다른 질문의 답으로 옮기는</b> 것입니다.
        /// </summary>
        public const float WarmupSeconds = 5f;

        // --- Public Types ---

        /// <summary>세고 있는 항목 하나입니다.</summary>
        public enum Counter
        {
            /// <summary>타일을 통째로 켠 횟수입니다. (<c>SetActive(true)</c>)</summary>
            TileActivated = 0,

            /// <summary>타일을 통째로 끈 횟수입니다. 이 프로젝트에서 가장 비싼 토글입니다.</summary>
            TileDeactivated,

            /// <summary>지면 컴포넌트를 켜고 끈 횟수입니다. (<c>Terrain.enabled</c>)</summary>
            SurfaceToggled,

            /// <summary>나무·풀 접기를 켜고 끈 횟수입니다. (<c>drawTreesAndFoliage</c>)</summary>
            FoliageToggled,

            /// <summary>풀 그리기 거리를 다시 대입한 횟수입니다. 디테일 패치가 다시 짜입니다.</summary>
            DetailDistanceWritten,

            /// <summary>지형 목록을 다시 찾은 횟수입니다. 씬 전체를 훑고 배열을 할당합니다.</summary>
            TerrainScanned
        }

        // --- Private Member Variables ---

        /// <summary>
        /// 셀 항목의 개수입니다. <b>열거형에서 뽑습니다.</b>
        ///
        /// 손으로 적어 두면 항목을 하나 늘렸을 때 배열이 그대로 남아, 새 항목을 세는 순간
        /// 범위를 벗어납니다. 그 실수는 새 항목을 처음 세는 그 자리에서만 드러나므로
        /// 한참 뒤에 발견됩니다.
        /// </summary>
        private static readonly int CounterCount = System.Enum.GetValues(typeof(Counter)).Length;

        /// <summary>지금 창에서 센 값입니다. 인덱스가 <see cref="Counter"/> 값입니다.</summary>
        private static readonly int[] counts = new int[CounterCount];

        /// <summary>직전 창에서 센 값입니다. 화면에 보여 줄 것은 이쪽입니다.</summary>
        private static readonly int[] reported = new int[CounterCount];

        /// <summary>최근 프레임 시간(ms)들입니다. 고리처럼 돌려 씁니다.</summary>
        private static readonly float[] frames = new float[FrameWindow];

        /// <summary>다음에 적어 넣을 자리입니다.</summary>
        private static int frameCursor;

        /// <summary>창이 한 바퀴 이상 찼는지입니다. 덜 찼으면 그만큼만 봅니다.</summary>
        private static int frameFilled;

        /// <summary>이번 창이 시작된 시각입니다.</summary>
        private static float windowStart;

        /// <summary>직전 창이 실제로 얼마나 이어졌는지(초)입니다. 초당 횟수를 낼 때 씁니다.</summary>
        private static float reportedSpan = 1f;

        /// <summary>이번 세션에서 센 끊김 프레임 수입니다. 창과 무관하게 계속 쌓입니다.</summary>
        private static int hitchTotal;

        /// <summary>이번 세션에서 센 심한 끊김 프레임 수입니다.</summary>
        private static int badHitchTotal;

        /// <summary>기동 구간을 뺀 뒤 가장 오래 걸린 프레임(ms)입니다.</summary>
        private static float worstFrame;

        /// <summary>기동 구간에서 가장 오래 걸린 프레임(ms)입니다. 로딩·첫 활성화 비용입니다.</summary>
        private static float startupWorst;

        /// <summary>처음 기록한 시각입니다. 기동 구간의 기준점입니다.</summary>
        private static float firstTickTime;

        /// <summary>기동 구간이 끝났는지입니다.</summary>
        private static bool warmedUp;

        // --- Public Properties ---

        /// <summary>이번 세션의 끊김(33ms 초과) 프레임 수입니다.</summary>
        public static int HitchTotal { get { return hitchTotal; } }

        /// <summary>이번 세션의 심한 끊김(100ms 초과) 프레임 수입니다.</summary>
        public static int BadHitchTotal { get { return badHitchTotal; } }

        /// <summary>
        /// 기동 구간을 <b>뺀</b> 뒤 가장 오래 걸린 프레임(ms)입니다.
        /// 플레이 중에 실제로 얼마나 튀는지를 보려면 이 값을 보세요.
        /// </summary>
        public static float WorstFrame { get { return worstFrame; } }

        /// <summary>
        /// 기동 구간에서 가장 오래 걸린 프레임(ms)입니다.
        ///
        /// 씬 로드·셰이더 컴파일·타일 첫 활성화가 여기 다 들어 있어 <b>어느 하나의 비용이라고
        /// 말할 수 없습니다.</b> 그래도 "기동에 얼마나 튀는가"의 상한으로는 쓸 수 있습니다.
        /// </summary>
        public static float StartupWorstMs { get { return startupWorst; } }

        /// <summary>기동 구간이 끝나 플레이 구간을 재고 있는지입니다.</summary>
        public static bool WarmedUp { get { return warmedUp; } }

        // --- Public Methods : 세기 ---

        /// <summary>
        /// 항목 하나를 셉니다. <b>릴리즈 빌드에서는 이 호출이 사라집니다.</b>
        /// </summary>
        /// <param name="counter">셀 항목</param>
        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        public static void Count(Counter counter)
        {
            counts[(int)counter]++;
        }

        /// <summary>
        /// 항목을 여러 번 한꺼번에 셉니다. 반복문 밖에서 한 번 부를 때 씁니다.
        /// </summary>
        /// <param name="counter">셀 항목</param>
        /// <param name="amount">더할 횟수</param>
        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        public static void Count(Counter counter, int amount)
        {
            if (amount <= 0) return;
            counts[(int)counter] += amount;
        }

        // --- Public Methods : 프레임 ---

        /// <summary>
        /// 이번 프레임을 기록합니다. <see cref="WorldPerfOverlay"/> 가 매 프레임 부릅니다.
        /// </summary>
        /// <param name="unscaledDeltaTime">이번 프레임에 걸린 시간(초)</param>
        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        public static void Tick(float unscaledDeltaTime)
        {
            float ms = unscaledDeltaTime * 1000f;
            float now = Time.realtimeSinceStartup;

            if (firstTickTime <= 0f) firstTickTime = now;

            // <b>기동 구간은 따로 셉니다.</b> 로딩 프레임을 섞으면 그 뒤의 어떤 숫자도
            // 의미가 없어집니다. (위 WarmupSeconds 주석을 보세요)
            if (!warmedUp)
            {
                if (ms > startupWorst) startupWorst = ms;

                if (now - firstTickTime < WarmupSeconds) return;

                // 구간이 끝났습니다. 여기서부터가 플레이 구간입니다.
                warmedUp = true;
                windowStart = now;
                return;
            }

            frames[frameCursor] = ms;
            frameCursor = (frameCursor + 1) % FrameWindow;
            if (frameFilled < FrameWindow) frameFilled++;

            if (ms > HitchMilliseconds) hitchTotal++;
            if (ms > BadHitchMilliseconds) badHitchTotal++;
            if (ms > worstFrame) worstFrame = ms;

            // 1초마다 창을 닫고 다음 창을 엽니다.
            if (windowStart <= 0f) windowStart = now;

            float span = now - windowStart;
            if (span < 1f) return;

            for (int i = 0; i < counts.Length; i++)
            {
                reported[i] = counts[i];
                counts[i] = 0;
            }

            reportedSpan = span;
            windowStart = now;
        }

        // --- Public Methods : 읽기 ---

        /// <summary>
        /// 직전 1초 동안 이 항목이 몇 번 일어났는지 돌려줍니다.
        /// </summary>
        /// <param name="counter">읽을 항목</param>
        /// <returns>초당 횟수</returns>
        public static float PerSecond(Counter counter)
        {
            return reported[(int)counter] / Mathf.Max(0.001f, reportedSpan);
        }

        /// <summary>
        /// 최근 창의 평균 프레임 시간(ms)입니다.
        /// </summary>
        /// <returns>평균 프레임 시간</returns>
        public static float AverageFrameMs()
        {
            if (frameFilled == 0) return 0f;

            float sum = 0f;
            for (int i = 0; i < frameFilled; i++) sum += frames[i];

            return sum / frameFilled;
        }

        /// <summary>
        /// 최근 창에서 <b>가장 오래 걸린</b> 프레임 시간(ms)입니다.
        ///
        /// <b>평균보다 이 값을 보세요.</b> 끊김은 평균을 거의 움직이지 않습니다.
        /// 120프레임 중 한 번 400ms 가 나와도 평균은 3ms 늘 뿐이지만, 사람은 그 한 번을 봅니다.
        /// </summary>
        /// <returns>최근 창에서 가장 오래 걸린 프레임 시간</returns>
        public static float WorstRecentMs()
        {
            float worst = 0f;
            for (int i = 0; i < frameFilled; i++)
            {
                if (frames[i] > worst) worst = frames[i];
            }
            return worst;
        }

        /// <summary>세어 둔 것을 모두 비웁니다. 비교 구간을 새로 시작할 때 씁니다.</summary>
        public static void Reset()
        {
            for (int i = 0; i < counts.Length; i++)
            {
                counts[i] = 0;
                reported[i] = 0;
            }

            frameCursor = 0;
            frameFilled = 0;
            windowStart = 0f;
            reportedSpan = 1f;
            hitchTotal = 0;
            badHitchTotal = 0;
            worstFrame = 0f;

            // <b>기동 구간 값은 남깁니다.</b> 손으로 비우는 것은 "지금부터 다시 보겠다"는
            // 뜻이지 "기동에 얼마나 걸렸는지 잊겠다"는 뜻이 아닙니다.
            // 그것까지 지우려면 ResetAll 을 쓰세요.
        }

        /// <summary>기동 구간 값까지 포함해 전부 비웁니다.</summary>
        public static void ResetAll()
        {
            Reset();

            startupWorst = 0f;
            firstTickTime = 0f;
            warmedUp = false;
        }

        // --- Private Methods ---

        /// <summary>
        /// 플레이 모드에 들어갈 때 센 것을 비웁니다.
        /// 도메인 리로드를 꺼 두면 지난 실행의 숫자가 그대로 남습니다.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            ResetAll();
        }
    }
}
