using UnityEngine;
using CarDrive.Common;

namespace CarDrive.Systems
{
    /// <summary>
    /// 시간 흐름을 빠르게 돌려 보기 위한 간이 디버거입니다.
    ///
    /// 배속을 올리면 TimeSystem 하나만 바뀌지만, 니즈와 날씨가 모두 이 시계를 보고 있으므로
    /// 하루·니즈·날씨가 함께 빨라집니다. 밤이 오는 모습이나 날씨가 단계를 밟아 바뀌는 과정을
    /// 몇 초 만에 확인할 수 있습니다.
    ///
    /// TimeSystem과 같은 GameObject에 붙여 두면 됩니다.
    /// </summary>
    [RequireComponent(typeof(TimeSystem))]
    public class TimeDebugOverlay : MonoBehaviour
    {
        // --- Public Member Variables ---

        /// <summary>오버레이를 화면에 그릴지 여부입니다. 켜져 있는 동안에는 커서가 풀리고 게임 입력이 멈춥니다.</summary>
        [Header("표시")]
        [Tooltip("오버레이를 켜고 끕니다. 켜져 있는 동안에는 마우스 커서가 풀리고 게임 입력이 멈춥니다.")]
        public bool showOverlay = false;

        /// <summary>
        /// 오버레이가 떠 있는 동안 게임 입력을 막을지 여부입니다.
        /// 끄면 버튼을 누를 수 없고 표시만 보게 됩니다.
        /// </summary>
        [Tooltip("체크를 해제하면 오버레이가 떠 있어도 게임 입력을 막지 않습니다. " +
                 "(대신 버튼을 누를 수 없고 표시만 보게 됩니다)")]
        public bool suspendGameInput = true;

        /// <summary>오버레이 표시를 전환하는 키입니다.</summary>
        [Tooltip("오버레이 표시를 토글하는 키")]
        public KeyCode toggleKey = KeyCode.F2;

        /// <summary>화면 우상단으로부터의 여백입니다.</summary>
        [Tooltip("화면 우상단으로부터의 여백")]
        public Vector2 margin = new Vector2(16f, 16f);

        /// <summary>오버레이 패널의 너비입니다.</summary>
        [Tooltip("패널 너비")]
        public float panelWidth = 300f;

        /// <summary>버튼으로 고를 수 있는 배속 목록입니다. 1이 원래 속도입니다.</summary>
        [Header("배속")]
        [Tooltip("버튼으로 고를 수 있는 배속 목록. 1이 원래 속도입니다.")]
        public float[] speedMultipliers = { 1f, 5f, 20f, 60f, 300f };

        /// <summary>배속을 한 단계 낮추는 키입니다.</summary>
        [Header("단축키")]
        [Tooltip("배속 낮추기")]
        public KeyCode slowerKey = KeyCode.Comma;

        /// <summary>배속을 한 단계 올리는 키입니다.</summary>
        [Tooltip("배속 올리기")]
        public KeyCode fasterKey = KeyCode.Period;

        /// <summary>시간 흐름 일시정지를 전환하는 키입니다.</summary>
        [Tooltip("일시정지 토글")]
        public KeyCode pauseKey = KeyCode.Slash;

        // --- Private Member Variables ---

        /// <summary>조작 대상 시간 시스템입니다. 같은 GameObject에서 가져옵니다.</summary>
        private TimeSystem time;

        /// <summary>날씨 표시에 쓸 날씨 시스템입니다. 없으면 날씨 영역을 그리지 않습니다.</summary>
        private WeatherSystem weather;

        /// <summary>실제 파티클에 적용 중인 빗줄기 값을 읽어 올 리그입니다. 없어도 동작합니다.</summary>
        private WeatherRig rig;

        private float baseRate;        // 원래 시간 배율 (×1 기준)

        /// <summary>지금 고른 배속의 speedMultipliers 인덱스입니다.</summary>
        private int speedIndex;

        /// <summary>막대를 그릴 1x1 흰색 텍스처입니다. 색은 GUI.color로 입힙니다.</summary>
        private Texture2D barTexture;

        /// <summary>본문 라벨 스타일입니다. 처음 필요할 때 만듭니다.</summary>
        private GUIStyle labelStyle;

        /// <summary>제목 라벨 스타일입니다. 처음 필요할 때 만듭니다.</summary>
        private GUIStyle titleStyle;

        // 지금 이 오버레이가 입력 막기를 걸어 둔 상태인지
        private bool holdingInputGate;

        // --- Unity Event Functions ---

        /// <summary>
        /// 시간 시스템 참조와 원래 배율을 저장하고 막대용 텍스처를 만듭니다.
        /// </summary>
        void Awake()
        {
            time = GetComponent<TimeSystem>();
            baseRate = time.gameMinutesPerRealSecond;

            barTexture = new Texture2D(1, 1);
            barTexture.SetPixel(0, 0, Color.white);
            barTexture.Apply();
        }

        /// <summary>
        /// 날씨 표시에 쓸 시스템들을 씬에서 찾습니다. 없으면 날씨 영역만 빠집니다.
        /// </summary>
        void Start()
        {
            weather = GameContext.Resolve<WeatherSystem>(this);
            rig = GameContext.Resolve<WeatherRig>(this);
        }

        /// <summary>
        /// 걸어 둔 입력 막기를 풀고 코드로 만든 텍스처를 해제합니다.
        /// </summary>
        void OnDestroy()
        {
            ReleaseInputGate();
            if (barTexture != null) Destroy(barTexture);
        }

        /// <summary>
        /// 컴포넌트가 꺼질 때 입력 막기가 남지 않도록 풀어 줍니다.
        /// </summary>
        void OnDisable()
        {
            ReleaseInputGate();
        }

        /// <summary>
        /// 표시 전환 키와 배속·일시정지 단축키를 받고, 표시 상태에 맞춰 입력 막기를 갱신합니다.
        /// </summary>
        void Update()
        {
            if (GameInput.GetKeyDownRaw(toggleKey)) showOverlay = !showOverlay;

            // 오버레이가 떠 있는 동안에만 커서를 풀고 게임 입력을 막습니다.
            SyncInputGate();

            if (!showOverlay) return;

            if (GameInput.GetKeyDownRaw(slowerKey)) SetSpeedIndex(speedIndex - 1);
            if (GameInput.GetKeyDownRaw(fasterKey)) SetSpeedIndex(speedIndex + 1);
            if (GameInput.GetKeyDownRaw(pauseKey)) time.paused = !time.paused;
        }

        /// <summary>
        /// 시계·배속·건너뛰기 버튼과 날씨 상태를 화면 우상단 패널에 그립니다.
        /// </summary>
        void OnGUI()
        {
            if (!showOverlay || time == null) return;

            EnsureStyles();

            float x = Screen.width - panelWidth - margin.x;
            float y = margin.y;
            const float lineHeight = 19f;

            // 배경
            GUI.color = new Color(0f, 0f, 0f, 0.6f);
            GUI.DrawTexture(new Rect(x - 8f, y - 6f, panelWidth + 16f, weather != null ? 306f : 176f), barTexture);
            GUI.color = Color.white;

            GUI.Label(new Rect(x, y, panelWidth, lineHeight),
                "시간 디버거  (" + toggleKey + " 로 표시 전환)", titleStyle);
            y += lineHeight + 2f;

            // --- 시계 ---
            GUI.Label(new Rect(x, y, panelWidth, lineHeight),
                time.Day + "일차   " + time.GetClockText() + "   " + time.GetPhaseName()
                + (time.paused ? "   [일시정지]" : ""), labelStyle);
            y += lineHeight;

            GUI.Label(new Rect(x, y, panelWidth, lineHeight),
                "밝기 " + time.DaylightFactor.ToString("F2")
                + "    " + (time.IsNight ? "밤" : "낮")
                + "    배속 ×" + CurrentMultiplier().ToString("0.#"), labelStyle);
            y += lineHeight;

            GUI.Label(new Rect(x, y, panelWidth, lineHeight),
                "실제 1초 = 게임 " + time.gameMinutesPerRealSecond.ToString("0.#") + "분"
                + "   (하루 " + FormatRealDayLength() + ")", labelStyle);
            y += lineHeight + 4f;

            // 하루 진행 막대
            DrawBar(new Rect(x, y, panelWidth, 8f), time.NormalizedDay,
                Color.Lerp(new Color(0.15f, 0.2f, 0.4f), new Color(1f, 0.85f, 0.4f), time.DaylightFactor));
            y += 14f;

            // --- 배속 버튼 ---
            float bw = panelWidth / speedMultipliers.Length;
            for (int i = 0; i < speedMultipliers.Length; i++)
            {
                bool active = i == speedIndex;
                GUI.color = active ? new Color(1f, 0.8f, 0.3f) : Color.white;
                if (GUI.Button(new Rect(x + bw * i, y, bw - 2f, 22f), "×" + speedMultipliers[i].ToString("0.#")))
                {
                    SetSpeedIndex(i);
                }
            }
            GUI.color = Color.white;
            y += 26f;

            // --- 건너뛰기 버튼 ---
            float qw = panelWidth / 4f;
            if (GUI.Button(new Rect(x, y, qw - 2f, 22f), "+1시간")) time.AdvanceMinutes(60f);
            if (GUI.Button(new Rect(x + qw, y, qw - 2f, 22f), "+6시간")) time.AdvanceMinutes(360f);
            if (GUI.Button(new Rect(x + qw * 2f, y, qw - 2f, 22f), "새벽")) time.SkipToHour(time.dawnStartHour);
            if (GUI.Button(new Rect(x + qw * 3f, y, qw - 2f, 22f), "밤")) time.SkipToHour(time.nightStartHour);
            y += 26f;

            if (GUI.Button(new Rect(x, y, panelWidth * 0.5f - 2f, 22f), time.paused ? "재개" : "일시정지"))
            {
                time.paused = !time.paused;
            }
            if (GUI.Button(new Rect(x + panelWidth * 0.5f, y, panelWidth * 0.5f, 22f), "배속 초기화"))
            {
                SetSpeedIndex(0);
            }
            y += 28f;

            // --- 날씨 ---
            if (weather != null) DrawWeather(x, ref y, lineHeight);

            GUI.color = Color.white;
        }

        // --- Private Methods ---

        /// <summary>
        /// 표시 상태에 맞춰 입력 막기를 걸거나 풉니다.
        /// 커서 잠금 해제는 PlayerCameraController가 이 신호를 받아 처리합니다.
        /// </summary>
        private void SyncInputGate()
        {
            bool want = showOverlay && suspendGameInput;
            if (want == holdingInputGate) return;

            if (want) { GameInputGate.Push(); holdingInputGate = true; }
            else ReleaseInputGate();
        }

        /// <summary>
        /// 걸어 둔 입력 막기를 풉니다. 걸어 둔 적이 없으면 아무 일도 하지 않습니다.
        /// </summary>
        private void ReleaseInputGate()
        {
            if (!holdingInputGate) return;

            GameInputGate.Pop();
            holdingInputGate = false;
        }

        /// <summary>
        /// 날씨가 지금 어떤 상태이고 어디로 가는 중인지 보여 줍니다.
        /// 배속을 올리면 이 값들이 변해 가는 과정을 눈으로 볼 수 있습니다.
        /// </summary>
        /// <param name="x">날씨 영역을 그릴 왼쪽 좌표</param>
        /// <param name="y">그리기 시작할 세로 좌표. 그린 높이만큼 늘려서 돌려줍니다.</param>
        /// <param name="lineHeight">글자 한 줄의 높이</param>
        private void DrawWeather(float x, ref float y, float lineHeight)
        {
            GUI.color = new Color(1f, 1f, 1f, 0.55f);
            GUI.DrawTexture(new Rect(x, y, panelWidth, 1f), barTexture);
            GUI.color = Color.white;
            y += 5f;

            string line = "날씨 " + weather.GetDisplayName() + "  강도 " + weather.Intensity.ToString("F2");
            if (weather.IsTransitioning)
            {
                line += "  →  " + weather.Target + " " + Mathf.RoundToInt(weather.Blend * 100f) + "%";
            }
            GUI.Label(new Rect(x, y, panelWidth, lineHeight), line, labelStyle);
            y += lineHeight;

            GUI.Label(new Rect(x, y, panelWidth, lineHeight),
                "구름 " + weather.CloudCover.ToString("F2")
                + "  비 " + weather.RainIntensity.ToString("F2")
                + "  안개 " + weather.FogDensity.ToString("F2"), labelStyle);
            y += lineHeight;

            GUI.Label(new Rect(x, y, panelWidth, lineHeight),
                "시야 ×" + weather.GetEffectiveVisibility().ToString("F2")
                + "  미끄러움 ×" + weather.RoadSlipperiness.ToString("F2")
                + "  귀신 ×" + weather.GetEffectiveGhostActivity().ToString("F2"), labelStyle);
            y += lineHeight + 3f;

            // 비 세기: 목표치와 실제 파티클에 적용 중인 값을 겹쳐 그립니다.
            // 두 막대가 어긋나 있으면 아직 따라가는 중이라는 뜻입니다.
            if (rig != null)
            {
                GUI.Label(new Rect(x, y, panelWidth, lineHeight),
                    "빗줄기  목표 " + weather.RainIntensity.ToString("F2")
                    + "  →  적용 " + rig.DisplayedRain.ToString("F2"), labelStyle);
                y += lineHeight;
            }

            Rect barRect = new Rect(x, y, panelWidth, 6f);
            DrawBar(barRect, weather.RainIntensity, new Color(0.4f, 0.65f, 0.95f, 0.35f));   // 목표 (연하게)
            if (rig != null)
            {
                DrawBarOverlay(barRect, rig.DisplayedRain, new Color(0.45f, 0.75f, 1f));      // 실제 (진하게)
            }
            y += 10f;

            // 날씨 강제 버튼 — 자연 전환은 게임 시간으로 몇 시간이 걸리므로
            // 비가 실제로 내리는지 확인하려면 이렇게 바로 걸어 보는 편이 빠릅니다.
            WeatherType[] quick = { WeatherType.Clear, WeatherType.Cloudy, WeatherType.Rain, WeatherType.Storm, WeatherType.Fog };
            string[] quickNames = { "맑음", "흐림", "비", "폭우", "안개" };

            float bw = panelWidth / quick.Length;
            for (int i = 0; i < quick.Length; i++)
            {
                bool active = weather.Current == quick[i] && !weather.IsTransitioning;
                GUI.color = active ? new Color(1f, 0.8f, 0.3f) : Color.white;
                if (GUI.Button(new Rect(x + bw * i, y, bw - 2f, 22f), quickNames[i]))
                {
                    // 즉시 적용해야 눈으로 바로 확인됩니다. (전환 과정을 보려면 아래 '서서히' 버튼)
                    weather.SetWeather(quick[i], true);
                }
            }
            GUI.color = Color.white;
            y += 25f;

            if (GUI.Button(new Rect(x, y, panelWidth, 20f), "서서히 바꾸기: 폭우까지 (전환 과정 보기)"))
            {
                weather.SetWeather(WeatherType.Storm, false);
            }
            y += 24f;
        }

        /// <summary>배경 없이 채움만 덧그립니다. (목표 막대 위에 실제값을 겹칠 때)</summary>
        /// <param name="rect">막대를 그릴 영역</param>
        /// <param name="fill">채움 비율(0~1)</param>
        /// <param name="color">채움 색상</param>
        private void DrawBarOverlay(Rect rect, float fill, Color color)
        {
            GUI.color = color;
            GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width * Mathf.Clamp01(fill), rect.height), barTexture);
            GUI.color = Color.white;
        }

        /// <summary>
        /// 옅은 배경 위에 채움 막대를 그립니다.
        /// </summary>
        /// <param name="rect">막대를 그릴 영역</param>
        /// <param name="fill">채움 비율(0~1)</param>
        /// <param name="color">채움 색상</param>
        private void DrawBar(Rect rect, float fill, Color color)
        {
            GUI.color = new Color(1f, 1f, 1f, 0.18f);
            GUI.DrawTexture(rect, barTexture);

            GUI.color = color;
            GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width * Mathf.Clamp01(fill), rect.height), barTexture);

            GUI.color = Color.white;
        }

        /// <summary>
        /// 배속을 지정한 단계로 바꾸고 시간 시스템의 배율에 반영합니다.
        /// </summary>
        /// <param name="index">고를 배속의 인덱스. 목록 범위를 벗어나면 양 끝으로 잘립니다.</param>
        private void SetSpeedIndex(int index)
        {
            if (speedMultipliers == null || speedMultipliers.Length == 0) return;

            speedIndex = Mathf.Clamp(index, 0, speedMultipliers.Length - 1);
            time.gameMinutesPerRealSecond = baseRate * speedMultipliers[speedIndex];
        }

        /// <summary>
        /// 지금 적용 중인 배속을 돌려줍니다.
        /// </summary>
        /// <returns>선택된 배속. 목록이 비어 있으면 1을 반환합니다.</returns>
        private float CurrentMultiplier()
        {
            if (speedMultipliers == null || speedMultipliers.Length == 0) return 1f;
            return speedMultipliers[Mathf.Clamp(speedIndex, 0, speedMultipliers.Length - 1)];
        }

        /// <summary>하루가 실제 시간으로 얼마나 걸리는지 사람이 읽기 좋게 만듭니다.</summary>
        /// <returns>90초 미만이면 초 단위, 그 이상이면 분 단위 문자열. 시간이 멈춰 있으면 "정지"입니다.</returns>
        private string FormatRealDayLength()
        {
            float rate = time.gameMinutesPerRealSecond;
            if (rate <= 0.0001f) return "정지";

            float seconds = TimeSystem.MinutesPerDay / rate;
            if (seconds < 90f) return seconds.ToString("0") + "초";
            return (seconds / 60f).ToString("0.#") + "분";
        }

        /// <summary>
        /// 라벨과 제목 스타일을 처음 필요할 때 한 번만 만듭니다.
        /// GUI.skin은 OnGUI 밖에서 읽을 수 없어 Awake가 아니라 여기서 준비합니다.
        /// </summary>
        private void EnsureStyles()
        {
            if (labelStyle == null)
            {
                labelStyle = new GUIStyle(GUI.skin.label);
                labelStyle.fontSize = 12;
            }
            if (titleStyle == null)
            {
                titleStyle = new GUIStyle(GUI.skin.label);
                titleStyle.fontSize = 12;
                titleStyle.fontStyle = FontStyle.Bold;
            }
        }
    }
}
