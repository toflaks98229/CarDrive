using UnityEngine;
using CarDrive.Common;

namespace CarDrive.Systems
{
    /// <summary>
    /// 월드 시스템이 <b>초당 몇 번 무엇을 하는지</b>와 프레임 상태를 화면에 띄웁니다.
    ///
    /// <b>왜 프로파일러 대신인가.</b> 유니티 프로파일러는 "어느 함수가 몇 ms"를 잘 보여 주지만,
    /// 이 프로젝트에서 확인해야 하는 것은 <b>"타일이 초당 몇 번 껐다 켜지는가"</b>입니다.
    /// 그 숫자는 프로파일러에 나오지 않고, 그런데도 지금까지의 최적화가 전부 그 가정 위에
    /// 서 있었습니다.
    ///
    /// <b>평균이 아니라 최악을 보세요.</b> 끊김은 평균을 거의 움직이지 않습니다.
    /// 120프레임에 한 번 400ms 가 나와도 평균은 3ms 늘 뿐인데, 사람은 그 한 번을 봅니다.
    /// 그래서 평균과 함께 <b>최근 최악</b>과 <b>누적 끊김 횟수</b>를 나란히 둡니다.
    ///
    /// 세는 일 자체는 <see cref="WorldProfiler"/> 가 하고, 릴리즈 빌드에서는 그 호출이
    /// 컴파일 단계에서 사라집니다. 이 오버레이도 개발 빌드에서만 의미가 있습니다.
    ///
    /// 씬에 둘 필요가 없습니다. <c>WorldRuntimeInstaller</c> 가 붙여 줍니다.
    /// </summary>
    [DefaultExecutionOrder(1000)]
    public class WorldPerfOverlay : MonoBehaviour
    {
        // --- Public Member Variables ---

        /// <summary>오버레이를 화면에 그릴지 여부입니다.</summary>
        [Header("표시")]
        [Tooltip("오버레이를 켜고 끕니다.")]
        public bool showOverlay = false;

        /// <summary>오버레이 표시를 전환하는 키입니다.</summary>
        [Tooltip("오버레이 표시를 토글하는 키")]
        public KeyCode toggleKey = KeyCode.F3;

        /// <summary>화면 좌상단으로부터의 여백입니다.</summary>
        [Tooltip("화면 좌상단으로부터의 여백")]
        public Vector2 margin = new Vector2(16f, 16f);

        // --- Private Member Variables ---

        /// <summary>본문 글자 모양입니다. GUI.skin 은 OnGUI 안에서만 읽을 수 있어 늦게 만듭니다.</summary>
        private GUIStyle labelStyle;

        /// <summary>제목 글자 모양입니다.</summary>
        private GUIStyle titleStyle;

        /// <summary>경고 글자 모양입니다. 끊김이 있을 때 씁니다.</summary>
        private GUIStyle warnStyle;

        // --- Unity Event Functions ---

        /// <summary>매 프레임 프레임 시간을 기록하고 토글 키를 받습니다.</summary>
        void Update()
        {
            WorldProfiler.Tick(Time.unscaledDeltaTime);

            // 게이트를 무시하는 조회입니다. 오버레이가 게이트를 걸어도 다시 끌 수 있어야 합니다.
            if (GameInput.GetKeyDownRaw(toggleKey)) showOverlay = !showOverlay;
        }

        /// <summary>오버레이를 그립니다.</summary>
        void OnGUI()
        {
            if (!showOverlay) return;

            EnsureStyles();

            const float width = 360f;
            const float line = 19f;

            float x = margin.x;
            float y = margin.y;

            GUI.Box(new Rect(x - 8f, y - 8f, width + 16f, line * 16f + 16f), GUIContent.none);

            GUI.Label(new Rect(x, y, width, line),
                "월드 계측  (" + toggleKey + " 로 표시 전환)", titleStyle);
            y += line * 1.4f;

            // --- 프레임 ---
            float avg = WorldProfiler.AverageFrameMs();
            float worstRecent = WorldProfiler.WorstRecentMs();

            GUI.Label(new Rect(x, y, width, line), "── 프레임 ──", labelStyle);
            y += line;

            GUI.Label(new Rect(x, y, width, line),
                "평균 " + avg.ToString("0.0") + "ms  (" + (avg > 0.01f ? (1000f / avg).ToString("0") : "0") + " fps)",
                labelStyle);
            y += line;

            GUI.Label(new Rect(x, y, width, line),
                "최근 2초 최악 " + worstRecent.ToString("0.0") + "ms",
                worstRecent > WorldProfiler.HitchMilliseconds ? warnStyle : labelStyle);
            y += line;

            // <b>기동 구간과 플레이 구간을 나눠 보여 줍니다.</b>
            // 로딩 프레임을 섞으면 그 뒤의 어떤 숫자도 의미가 없습니다 —
            // 플레이 중에 40ms 가 나오든 400ms 가 나오든 최악값은 로딩 프레임 그대로입니다.
            if (!WorldProfiler.WarmedUp)
            {
                GUI.Label(new Rect(x, y, width, line),
                    "기동 구간 측정 중… (" + WorldProfiler.WarmupSeconds.ToString("0") + "초)", warnStyle);
                y += line;
            }
            else
            {
                GUI.Label(new Rect(x, y, width, line),
                    "플레이 최악 " + WorldProfiler.WorstFrame.ToString("0.0") + "ms  (기동 제외)",
                    labelStyle);
                y += line;
            }

            int hitches = WorldProfiler.HitchTotal;
            int bad = WorldProfiler.BadHitchTotal;

            GUI.Label(new Rect(x, y, width, line),
                "끊김 " + hitches + "회 (33ms↑)   심함 " + bad + "회 (100ms↑)",
                hitches > 0 ? warnStyle : labelStyle);
            y += line;

            GUI.Label(new Rect(x, y, width, line),
                "기동 구간 최악 " + WorldProfiler.StartupWorstMs.ToString("0") + "ms  (로딩 포함)",
                labelStyle);
            y += line * 1.5f;

            // --- 토글 ---
            GUI.Label(new Rect(x, y, width, line), "── 초당 토글 횟수 ──", labelStyle);
            y += line;

            // <b>임계는 실측에서 나왔습니다.</b> 처음에는 "0보다 크면 경고"로 두었다가
            // 정상 동작까지 전부 노랗게 보여 쓸모가 없었고, 다음에는 접기를 40 으로 잡았다가
            // <b>평범한 시야 회전</b>이 그 값을 넘는 것을 확인했습니다.
            //
            // 실제로 잰 값은 이렇습니다. (타일 103장, 활성 거리 280m 기준)
            //   시야를 30도 돌림      접기 약 40/초
            //   아주 급하게 돌림      접기 약 170/초   ← 이때도 프레임 최악은 18ms 였습니다
            //
            // 즉 접기가 세 자리로 올라가도 그 자체로는 프레임을 흔들지 않습니다.
            // 그래서 기준을 "회전으로는 닿지 않는 값"인 200 에 둡니다.
            // 이 값을 넘으면 회전이 아니라 무언가 떨리고 있다는 뜻입니다.
            y = Row(x, y, width, line, "타일 켜기  (가장 비쌈)", WorldProfiler.Counter.TileActivated, 2f);
            y = Row(x, y, width, line, "타일 끄기  (가장 비쌈)", WorldProfiler.Counter.TileDeactivated, 2f);
            y = Row(x, y, width, line, "지면 enabled", WorldProfiler.Counter.SurfaceToggled, 5f);
            y = Row(x, y, width, line, "나무·풀 접기", WorldProfiler.Counter.FoliageToggled, 200f);
            y = Row(x, y, width, line, "풀 거리 재대입", WorldProfiler.Counter.DetailDistanceWritten, 20f);
            y = Row(x, y, width, line, "지형 목록 재탐색", WorldProfiler.Counter.TerrainScanned, 2f);

            y += line * 1.5f;

            // --- 지금의 풀 거리 ---
            //
            // <b>숫자로 보이지 않아서 놓친 버그가 있었습니다.</b> 인스펙터의 detailDistance 는
            // 70m 라고 적혀 있는데 실제로 그리는 거리는 rangeScale 과 속도 단계를 거쳐
            // 49m·36.8m·24.5m 였습니다. 페이드 창은 재질에 35~68.6m 로 구워져 있었고요.
            // 셋을 나란히 놓고 보기 전에는 어긋난 것을 알아채기 어렵습니다.
            //
            // 페이드 끝이 그리는 거리보다 <b>가까워야</b> 합니다. 넘어가면 잘리는 순간이 보입니다.
            ViewDistances.Ladder ladder = ViewDistances.Current;

            GUI.Label(new Rect(x, y, width, line), "── 지금의 풀 거리 ──", labelStyle);
            y += line;

            GUI.Label(new Rect(x, y, width, line),
                "그리기 " + ladder.Grass.ToString("0") + "m   " +
                "지워짐 " + ladder.GrassFadeStart.ToString("0") + "~" + ladder.GrassFadeEnd.ToString("0") + "m",
                ladder.GrassFadeEnd < ladder.Grass ? labelStyle : warnStyle);
            y += line;

            // 어느 경로가 그리고 있는지입니다. 설정만 봐서는 알 수 없습니다 —
            // gpuGrass 가 켜져 있어도 씬에 렌더러가 없거나, 컴퓨트를 못 쓰거나,
            // 아직 지형을 다 훑지 못했으면 터레인 디테일이 그립니다.
            GUI.Label(new Rect(x, y, width, line),
                GpuGrassRenderer.IsDrawing ? "그리는 쪽: GPU 간접 드로우" : "그리는 쪽: 터레인 디테일",
                labelStyle);
            y += line * 1.4f;

            if (GUI.Button(new Rect(x, y, 110f, 22f), "숫자 초기화"))
            {
                WorldProfiler.Reset();
            }
        }

        // --- Private Methods ---

        /// <summary>
        /// 항목 한 줄을 그립니다.
        /// </summary>
        /// <param name="x">왼쪽 위치</param>
        /// <param name="y">이 줄의 위쪽 위치</param>
        /// <param name="width">줄의 너비</param>
        /// <param name="line">한 줄의 높이</param>
        /// <param name="label">항목 이름</param>
        /// <param name="counter">읽을 항목</param>
        /// <param name="warnAbove">이 값을 넘으면 노랗게 칠할 기준(초당 횟수)</param>
        /// <returns>다음 줄의 위쪽 위치</returns>
        private float Row(float x, float y, float width, float line,
                          string label, WorldProfiler.Counter counter, float warnAbove)
        {
            float perSecond = WorldProfiler.PerSecond(counter);

            // <b>0보다 크다고 경고하지 않습니다.</b> 처음에는 그렇게 만들었는데,
            // 그러면 정상 동작까지 전부 노랗게 보여서 <b>어느 숫자가 실제로 문제인지</b>
            // 구분할 수 없습니다. 항목마다 "이 정도면 많다"는 기준이 다릅니다.
            GUI.Label(new Rect(x, y, width, line),
                label + "   " + perSecond.ToString("0.0") + " /초",
                perSecond > warnAbove ? warnStyle : labelStyle);

            return y + line;
        }

        /// <summary>
        /// 글자 모양을 준비합니다.
        /// <c>GUI.skin</c> 은 OnGUI 밖에서 읽을 수 없어 Awake 가 아니라 여기서 만듭니다.
        /// </summary>
        private void EnsureStyles()
        {
            if (labelStyle != null) return;

            labelStyle = new GUIStyle(GUI.skin.label);
            labelStyle.fontSize = 12;
            labelStyle.normal.textColor = new Color(0.86f, 0.90f, 0.94f);

            titleStyle = new GUIStyle(GUI.skin.label);
            titleStyle.fontSize = 13;
            titleStyle.fontStyle = FontStyle.Bold;
            titleStyle.normal.textColor = new Color(0.55f, 0.80f, 0.88f);

            warnStyle = new GUIStyle(GUI.skin.label);
            warnStyle.fontSize = 12;
            warnStyle.normal.textColor = new Color(0.95f, 0.72f, 0.35f);
        }
    }
}
