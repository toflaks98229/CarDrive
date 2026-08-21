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

            GUI.Box(new Rect(x - 8f, y - 8f, width + 16f, line * 15f + 16f), GUIContent.none);

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

            GUI.Label(new Rect(x, y, width, line),
                "세션 최악 " + WorldProfiler.WorstFrame.ToString("0.0") + "ms",
                labelStyle);
            y += line;

            int hitches = WorldProfiler.HitchTotal;
            int bad = WorldProfiler.BadHitchTotal;

            GUI.Label(new Rect(x, y, width, line),
                "끊김 " + hitches + "회 (33ms↑)   심함 " + bad + "회 (100ms↑)",
                hitches > 0 ? warnStyle : labelStyle);
            y += line * 1.5f;

            // --- 토글 ---
            GUI.Label(new Rect(x, y, width, line), "── 초당 토글 횟수 ──", labelStyle);
            y += line;

            // 임계는 항목마다 다릅니다. 타일을 통째로 켜고 끄는 일은 초당 두 번만 되어도
            // 눈에 띄지만, 나무·풀 접기는 시야를 돌리면 원래 수십 번씩 일어납니다.
            y = Row(x, y, width, line, "타일 켜기  (가장 비쌈)", WorldProfiler.Counter.TileActivated, 2f);
            y = Row(x, y, width, line, "타일 끄기  (가장 비쌈)", WorldProfiler.Counter.TileDeactivated, 2f);
            y = Row(x, y, width, line, "지면 enabled", WorldProfiler.Counter.SurfaceToggled, 5f);
            y = Row(x, y, width, line, "나무·풀 접기", WorldProfiler.Counter.FoliageToggled, 40f);
            y = Row(x, y, width, line, "풀 거리 재대입", WorldProfiler.Counter.DetailDistanceWritten, 20f);
            y = Row(x, y, width, line, "지형 목록 재탐색", WorldProfiler.Counter.TerrainScanned, 2f);

            y += line * 0.4f;

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
