using System.Collections.Generic;
using ConsoleDisplay.Templates;
using UnityEngine;

namespace ConsoleDisplay.Showcase
{
    /// <summary>
    /// 데모 방에 놓인 버튼 하나입니다. 다가가서 누르면 배정된 템플릿이 실제로 돌아갑니다.
    ///
    /// <b>템플릿을 미리 만들어 두지 않습니다.</b> 열 개의 템플릿을 씬을 켤 때 전부 만들어 두면
    /// 쓰지도 않을 로그 후킹이나 배열 할당이 함께 일어납니다. 누를 때 만듭니다.
    /// </summary>
    public sealed class ShowcaseKiosk : MonoBehaviour
    {
        // --- Public Types ---

        /// <summary>이 버튼이 어떤 템플릿을 맡는지입니다.</summary>
        public enum TemplateKind
        {
            TelemetryDashboard = 0,
            LogStream,
            SparklineGraph,
            BootSequence,
            TerminalDialogue,
            AlertScreen,
            AsciiRadar,
            StatusPanel,
            MatrixRain,
            AsciiAnimation,
        }

        // --- Public Member Variables ---

        [Tooltip("이 버튼이 실행할 템플릿입니다.")]
        [SerializeField] private TemplateKind kind = TemplateKind.TelemetryDashboard;

        [Tooltip("플레이어가 이만큼 안으로 들어오면 누를 수 있습니다.")]
        [SerializeField, Min(0.5f)] private float reach = 2.8f;

        [Tooltip("버튼의 화면 부분입니다. 눌리면 색이 밝아집니다.")]
        [SerializeField] private Renderer screen;

        // --- Private Member Variables ---

        /// <summary>살아 있는 버튼들입니다. <see cref="All"/>이 이것을 내보냅니다.</summary>
        private static readonly List<ShowcaseKiosk> registry = new List<ShowcaseKiosk>();

        private Transform player;
        private Material screenMaterial;
        private Color baseColor;
        private bool running;

        // --- Public Properties ---

        /// <summary>
        /// 지금 씬에 살아 있는 버튼들입니다.
        ///
        /// 씬을 뒤져 찾는 대신 버튼이 스스로 등록합니다. <c>FindObjectsByType</c>은 유니티 버전마다
        /// 인자가 달라져서, 한쪽에 맞추면 다른 쪽에서 경고가 나거나 컴파일이 깨집니다.
        /// 파는 물건이라 여러 버전에서 돌아야 합니다.
        /// </summary>
        public static IReadOnlyList<ShowcaseKiosk> All
        {
            get { return registry; }
        }

        /// <summary>버튼에 붙는 이름입니다.</summary>
        public string Title { get; private set; }

        /// <summary>이 템플릿이 무엇을 보여 주는지입니다.</summary>
        public string Summary { get; private set; }

        /// <summary>지금 이 버튼이 맡은 템플릿이 돌고 있는지입니다.</summary>
        public bool IsRunning
        {
            get { return running; }
        }

        // --- Public Methods ---

        /// <summary>
        /// 빌더가 버튼을 세울 때 불러 줍니다.
        /// </summary>
        public void Configure(TemplateKind templateKind, Renderer screenRenderer, Color tint)
        {
            kind = templateKind;
            screen = screenRenderer;
            baseColor = tint;
        }

        /// <summary>플레이어가 손이 닿는 거리에 있는지입니다.</summary>
        public bool IsPlayerInReach()
        {
            if (player == null)
            {
                return false;
            }

            return Vector3.Distance(player.position, transform.position) <= reach;
        }

        /// <summary>
        /// 버튼을 누릅니다. 배정된 템플릿을 만들어 두 번째 화면에서 돌립니다.
        /// </summary>
        public void Press()
        {
            ConsoleTemplate template = CreateTemplate();
            if (template == null)
            {
                return;
            }

            ConsoleTemplateRunner.Instance.Play(template);
            running = true;
        }

        /// <summary>다른 버튼이 눌렸을 때 이 버튼의 강조를 끕니다.</summary>
        public void MarkStopped()
        {
            running = false;
        }

        // --- Unity Event Functions ---

        private void Awake()
        {
            Title = TitleOf(kind);
            Summary = SummaryOf(kind);

            if (screen != null)
            {
                // 공유 머티리얼을 건드리면 같은 재질을 쓰는 다른 버튼까지 함께 밝아집니다.
                screenMaterial = screen.material;
                if (baseColor == default(Color))
                {
                    baseColor = screenMaterial.color;
                }
            }
        }

        private void OnEnable()
        {
            if (!registry.Contains(this))
            {
                registry.Add(this);
            }
        }

        private void OnDisable()
        {
            registry.Remove(this);
        }

        private void Start()
        {
            ShowcasePlayer found = FindAnyObjectByType<ShowcasePlayer>();
            if (found != null)
            {
                player = found.transform;
            }
        }

        private void Update()
        {
            if (screenMaterial == null)
            {
                return;
            }

            bool near = IsPlayerInReach();

            // 돌고 있으면 밝게, 다가가면 조금 밝게, 아니면 어둡게. 색만 봐도 상태를 압니다.
            Color target = running ? baseColor * 2.2f : (near ? baseColor * 1.5f : baseColor * 0.6f);
            screenMaterial.color = Color.Lerp(screenMaterial.color, target, Time.deltaTime * 8f);

            if (screenMaterial.HasProperty(EmissionColor))
            {
                screenMaterial.SetColor(EmissionColor, target);
            }
        }

        private void OnDestroy()
        {
            if (screenMaterial != null)
            {
                Destroy(screenMaterial);
            }
        }

        // --- Private Members ---

        private static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");

        /// <summary>
        /// 이 버튼이 맡은 템플릿을 만듭니다. 누를 때마다 새로 만들어 상태를 처음부터 시작합니다.
        /// </summary>
        private ConsoleTemplate CreateTemplate()
        {
            switch (kind)
            {
                case TemplateKind.TelemetryDashboard:
                    return new TelemetryDashboardTemplate { Target = player };

                case TemplateKind.LogStream:
                    return new LogStreamTemplate();

                case TemplateKind.SparklineGraph:
                    return new SparklineGraphTemplate();

                case TemplateKind.BootSequence:
                    return new BootSequenceTemplate();

                case TemplateKind.TerminalDialogue:
                    return new TerminalDialogueTemplate();

                case TemplateKind.AlertScreen:
                    return new AlertScreenTemplate();

                case TemplateKind.AsciiRadar:
                    return BuildRadar();

                case TemplateKind.StatusPanel:
                    return new StatusPanelTemplate();

                case TemplateKind.MatrixRain:
                    return new MatrixRainTemplate();

                case TemplateKind.AsciiAnimation:
                    return new AsciiAnimationTemplate();

                default:
                    return null;
            }
        }

        /// <summary>
        /// 레이더는 씬에 실제 대상이 있어야 의미가 있습니다. 방 안의 버튼들을 찍어 줍니다.
        /// </summary>
        private ConsoleTemplate BuildRadar()
        {
            var radar = new AsciiRadarTemplate { Center = player, Range = 22f };

            for (int i = 0; i < registry.Count; i++)
            {
                ShowcaseKiosk kiosk = registry[i];
                bool self = ReferenceEquals(kiosk, this);
                radar.AddBlip(kiosk.transform, self ? 'R' : 'K', self ? ConsoleTint.Yellow : ConsoleTint.Cyan);
            }

            return radar;
        }

        private static string TitleOf(TemplateKind kind)
        {
            switch (kind)
            {
                case TemplateKind.TelemetryDashboard: return "Telemetry Dashboard";
                case TemplateKind.LogStream: return "Log Stream";
                case TemplateKind.SparklineGraph: return "Sparkline Graph";
                case TemplateKind.BootSequence: return "Boot Sequence";
                case TemplateKind.TerminalDialogue: return "Terminal Dialogue";
                case TemplateKind.AlertScreen: return "Alert Screen";
                case TemplateKind.AsciiRadar: return "ASCII Radar";
                case TemplateKind.StatusPanel: return "Status Panel";
                case TemplateKind.MatrixRain: return "Matrix Rain";
                case TemplateKind.AsciiAnimation: return "ASCII Animation";
                default: return kind.ToString();
            }
        }

        private static string SummaryOf(TemplateKind kind)
        {
            switch (kind)
            {
                case TemplateKind.TelemetryDashboard: return "프레임·메모리·좌표를 게임 화면을 가리지 않고 봅니다";
                case TemplateKind.LogStream: return "Debug.Log를 심각도별 색으로. 빌드에서도 콘솔을 봅니다";
                case TemplateKind.SparklineGraph: return "숫자로는 안 보이는 프레임 튐이 그래프에서 보입니다";
                case TemplateKind.BootSequence: return "낡은 기계가 켜지는 연출. 호러·SF 인트로";
                case TemplateKind.TerminalDialogue: return "누가 저쪽에서 치는 것처럼. 해킹·AI 연출";
                case TemplateKind.AlertScreen: return "붉게 깜빡이는 경고와 카운트다운";
                case TemplateKind.AsciiRadar: return "주변 오브젝트를 탑다운 격자로. 옆 화면이 계기판이 됩니다";
                case TemplateKind.StatusPanel: return "게임을 멈추지 않고 보는 능력치와 소지품";
                case TemplateKind.MatrixRain: return "칸 단위로 무엇이든 그릴 수 있다는 증명";
                case TemplateKind.AsciiAnimation: return "아스키 아트 프레임 애니메이션";
                default: return string.Empty;
            }
        }
    }
}
