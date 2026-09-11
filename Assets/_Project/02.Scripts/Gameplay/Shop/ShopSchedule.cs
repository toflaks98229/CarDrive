using UnityEngine;
using UnityEngine.Events;
using VContainer;
using CarDrive.Common;

namespace CarDrive.Gameplay
{
    /// <summary>
    /// 마트의 <b>영업 시간과 입고</b>를 관리합니다.
    ///
    /// 하루가 이렇게 흘러갑니다.
    ///  - <b>07:00 입고</b> — 진열대가 다시 채워집니다. 문을 여는 시각보다 <b>앞</b>이라,
    ///    손님이 들어왔을 때는 이미 물건이 놓여 있습니다.
    ///  - <b>08:00 개점</b> — 담기와 계산이 열립니다.
    ///  - <b>18:00 폐점</b> — 담기와 계산이 막히고, 담아 두었던 것은 진열대로 돌아갑니다.
    ///
    /// <b>실제 시간이 아니라 게임 시간을 봅니다.</b> 수면이나 기절로 시간을 건너뛰면
    /// 그 사이의 개·폐점과 입고가 <b>건너뛴 채로 반영</b>됩니다 — 자고 일어나면 문이 열려
    /// 있고 물건이 새로 놓여 있어야지, 잠든 시각 그대로 멈춰 있으면 안 됩니다.
    ///
    /// <b>이 컴포넌트가 없으면 마트는 늘 열려 있습니다.</b> <see cref="ShopCounter.IsOpen"/> 의
    /// 기본값이 열림이라, 붙이지 않은 씬에서도 예전처럼 돌아갑니다.
    /// </summary>
    [RequireComponent(typeof(ShopCounter))]
    public class ShopSchedule : MonoBehaviour
    {
        // --- Constants ---

        /// <summary>하루의 분입니다.</summary>
        private const float MinutesPerDay = 1440f;

        // --- Public Member Variables ---

        /// <summary>문을 여는 시각입니다.</summary>
        [Header("영업 시간 (게임 내 시각)")]
        [Tooltip("문을 여는 시각")]
        [Range(0f, 24f)]
        public float openHour = 8f;

        /// <summary>문을 닫는 시각입니다.</summary>
        [Tooltip("문을 닫는 시각")]
        [Range(0f, 24f)]
        public float closeHour = 18f;

        /// <summary>
        /// 진열대를 다시 채우는 시각입니다.
        ///
        /// <b>여는 시각보다 앞에 두세요.</b> 영업 중에 물건이 갑자기 늘어나면
        /// 손님이 보는 앞에서 재고가 솟아납니다.
        /// </summary>
        [Tooltip("진열대를 다시 채우는 시각. 여는 시각보다 앞에 두세요.")]
        [Range(0f, 24f)]
        public float restockHour = 7f;

        /// <summary>문을 열었을 때 발생합니다.</summary>
        [Header("이벤트")]
        [Tooltip("문을 열었을 때. 간판 불이나 문 애니메이션을 연결하세요.")]
        public UnityEvent onOpened;

        /// <summary>문을 닫았을 때 발생합니다.</summary>
        [Tooltip("문을 닫았을 때")]
        public UnityEvent onClosed;

        /// <summary>진열대를 다시 채웠을 때 발생합니다.</summary>
        [Tooltip("진열대를 다시 채웠을 때")]
        public UnityEvent onRestocked;

        // --- Public Properties ---

        /// <summary>지금 영업 중인지 여부입니다.</summary>
        public bool IsOpen { get { return counter != null && counter.IsOpen; } }

        /// <summary>문을 여는 시각을 "08:00" 꼴로 적은 것입니다. 안내 문구에 씁니다.</summary>
        public string OpenTimeText { get { return FormatHour(openHour); } }

        /// <summary>문을 닫는 시각을 "18:00" 꼴로 적은 것입니다.</summary>
        public string CloseTimeText { get { return FormatHour(closeHour); } }

        // --- Private Member Variables ---

        /// <summary>시각을 읽어 올 시계입니다. 주입되지 않으면 시간이 흐르지 않아 늘 열려 있습니다.</summary>
        private IGameClock clock = NullGameClock.Instance;

        /// <summary>여닫을 계산대입니다. 같은 GameObject 에서 가져옵니다.</summary>
        private ShopCounter counter;

        /// <summary>마지막으로 입고한 날짜입니다. 하루에 한 번만 채우기 위해 셉니다.</summary>
        private int lastRestockDay = -1;

        /// <summary>첫 평가를 마쳤는지 여부입니다.</summary>
        private bool seeded;

        // --- Injection ---

        /// <summary>게임 시계를 받습니다.</summary>
        /// <param name="gameClock">게임 시계</param>
        [Inject]
        public void Construct(IGameClock gameClock)
        {
            if (gameClock != null) clock = gameClock;
        }

        // --- Unity Event Functions ---

        /// <summary>여닫을 계산대를 가져옵니다.</summary>
        void Awake()
        {
            EnsureCounter();
        }

        /// <summary>시작할 때 상태를 확정합니다. 밤에 시작하면 처음부터 닫혀 있어야 합니다.</summary>
        void Start()
        {
            Evaluate();
        }

        /// <summary>시각을 확인해 여닫고, 때가 되면 채웁니다.</summary>
        void Update()
        {
            Evaluate();
        }

        // --- Public Methods ---

        /// <summary>
        /// 지금 시각을 보고 개·폐점과 입고를 반영합니다.
        ///
        /// <b>밖에서도 부를 수 있게 열어 두었습니다.</b> 테스트가 프레임 없이 확인합니다.
        /// 여러 번 불려도 상태가 바뀔 때만 이벤트가 나갑니다.
        /// </summary>
        public void Evaluate()
        {
            EnsureCounter();
            if (counter == null) return;

            float total = clock.TotalMinutes;
            int day = Mathf.FloorToInt(total / MinutesPerDay);
            float hour = Mathf.Repeat(total, MinutesPerDay) / 60f;

            SeedRestockDay(day, hour);

            // 입고를 먼저 봅니다. 같은 프레임에 입고와 개점이 함께 일어날 때
            // <b>채운 뒤에 열어야</b> 빈 진열대가 한 순간도 보이지 않습니다.
            if (day > lastRestockDay && hour >= restockHour)
            {
                lastRestockDay = day;
                Restock();
            }

            ApplyOpenState(IsWithinHours(hour));
        }

        /// <summary>
        /// 지금 시각이 영업 시간 안인지 확인합니다.
        ///
        /// 여는 시각이 닫는 시각보다 늦으면 <b>자정을 넘겨 영업하는 것</b>으로 봅니다.
        /// (20시에 열어 4시에 닫는 가게)
        /// </summary>
        /// <param name="hour">확인할 시각(0~24)</param>
        /// <returns>영업 시간 안이면 true</returns>
        public bool IsWithinHours(float hour)
        {
            return DayHours.Within(hour, openHour, closeHour);
        }

        /// <summary>
        /// 진열대를 다시 채웁니다. 시각과 상관없이 바로 채우고 싶을 때도 쓸 수 있습니다.
        /// </summary>
        [ContextMenu("지금 입고")]
        public void Restock()
        {
            if (counter == null) return;

            counter.RestockAll();

            GameLog.Info(GameLog.Channel.Simulation, "ShopSchedule: 진열대를 다시 채웠습니다.");
            if (onRestocked != null) onRestocked.Invoke();
        }

        // --- Private Methods ---

        /// <summary>
        /// 첫 평가에서 <b>오늘 입고가 이미 끝난 것으로 볼지</b>를 정합니다.
        ///
        /// 씬의 진열대는 채워진 채로 시작합니다. 그런데 게임이 아침 8시에 시작하면
        /// 7시는 이미 지난 뒤라, 아무 처리도 하지 않으면 <b>시작하자마자 입고가 한 번 돕니다.</b>
        /// 결과는 같지만 연출과 로그가 헛되이 나갑니다.
        ///
        /// 반대로 새벽 6시에 시작했다면 오늘 7시는 아직 오지 않았으므로 그때 채워야 합니다.
        /// </summary>
        /// <param name="day">지금 날짜</param>
        /// <param name="hour">지금 시각</param>
        private void SeedRestockDay(int day, float hour)
        {
            if (seeded) return;
            seeded = true;

            lastRestockDay = hour >= restockHour ? day : day - 1;
        }

        /// <summary>
        /// 열림 상태를 반영하고, 바뀌었을 때만 알립니다.
        /// </summary>
        /// <param name="open">지금 열려 있어야 하는지</param>
        private void ApplyOpenState(bool open)
        {
            if (counter.IsOpen == open) return;

            counter.SetOpen(open);

            if (open)
            {
                GameLog.Info(GameLog.Channel.Simulation, "ShopSchedule: 마트가 문을 열었습니다.");
                if (onOpened != null) onOpened.Invoke();
                return;
            }

            // 문을 닫을 때 담아 두었던 것은 진열대로 돌아갑니다.
            // 그러지 않으면 다음 날 아침 입고가 <b>담긴 수를 모른 채</b> 진열대를 채워
            // 장바구니와 진열대가 어긋납니다.
            counter.ReturnAll();

            GameLog.Info(GameLog.Channel.Simulation, "ShopSchedule: 마트가 문을 닫았습니다.");
            if (onClosed != null) onClosed.Invoke();
        }

        /// <summary>
        /// 여닫을 계산대 참조가 없으면 찾습니다.
        ///
        /// <b><c>Awake</c> 에만 맡기지 않는 이유가 있습니다.</b> 에디터 테스트에서는
        /// <c>Awake</c> 가 아예 돌지 않습니다. (<c>NeedsSystem.EnsureInitialized</c> 와 같은 사정입니다)
        /// 두 번 불려도 안전합니다.
        /// </summary>
        private void EnsureCounter()
        {
            if (counter == null) counter = GetComponent<ShopCounter>();
        }

        /// <summary>
        /// 시각을 "08:00" 꼴로 적습니다.
        /// </summary>
        /// <param name="hour">적을 시각(0~24)</param>
        /// <returns>사람이 읽을 시각</returns>
        private static string FormatHour(float hour)
        {
            int h = Mathf.FloorToInt(hour);
            int m = Mathf.RoundToInt((hour - h) * 60f);

            if (m >= 60) { m -= 60; h++; }

            return h.ToString("00") + ":" + m.ToString("00");
        }
    }
}
