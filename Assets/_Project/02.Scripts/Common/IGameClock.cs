namespace CarDrive.Common
{
    /// <summary>
    /// 게임 시계를 <b>읽고 돌리는</b> 계약입니다.
    ///
    /// <b>왜 인터페이스인가.</b> 예전에는 시계가 필요한 쪽이 <c>TimeSystem.GetMinutesPerSecond()</c>
    /// 같은 정적 메서드를 불렀습니다. 편했지만 대가가 있었습니다 —
    /// <b>호출부의 어떤 시그니처에도 시계가 나타나지 않았습니다.</b>
    /// 니즈가 시간에 좌우된다는 사실을 알려면 메서드 본문을 열어야 했고,
    /// 씬에 <c>TimeSystem</c>이 없으면 같은 코드가 다른 값을 냈습니다.
    ///
    /// 이제 시계를 쓰는 쪽은 <b>이 계약을 주입받습니다.</b> 의존이 필드로 드러나고,
    /// 테스트는 가짜 시계를 끼워 원하는 시각을 만들 수 있습니다.
    ///
    /// <b>없어도 됩니다.</b> 주입되지 않은 자리는 <see cref="NullGameClock"/>이 채웁니다.
    /// 정적 접근자가 "시스템이 없으면 기본값"으로 하던 일을 그대로 이어받으므로,
    /// 소비자마다 null 검사를 둘 필요가 없습니다.
    /// </summary>
    public interface IGameClock
    {
        /// <summary>첫날 0시부터 흐른 총 게임 시간(분)입니다.</summary>
        float TotalMinutes { get; }

        /// <summary>낮 밝기입니다. 1이면 대낮, 0이면 완전한 밤입니다.</summary>
        float Daylight { get; }

        /// <summary>지금이 밤(또는 새벽)인지 여부입니다.</summary>
        bool IsNight { get; }

        /// <summary>
        /// 이 시계가 <b>실제로 시간을 세고 있는지</b> 여부입니다.
        ///
        /// false 라면 시계가 씬에 없다는 뜻이고, 소비자는 자기 방식으로 시간을 세야 합니다.
        /// (예: <c>WeatherSystem</c>은 <c>Time.time</c>에 자체 배율을 곱합니다)
        /// </summary>
        bool IsRunning { get; }

        /// <summary>
        /// 실제 1초에 흐르는 게임 시간(분)입니다.
        ///
        /// <b>부르는 쪽이 자기 기본값을 넘깁니다.</b> 시계가 없을 때 무엇으로 도는지는
        /// 소비자마다 다르기 때문입니다 — 니즈와 날씨는 각자 인스펙터에 자기 배율을 갖고 있습니다.
        /// 예전 <c>TimeSystem.GetMinutesPerSecond(fallback)</c>과 같은 규약입니다.
        /// </summary>
        /// <param name="fallback">시계가 없을 때 쓸 배율</param>
        /// <returns>시계가 있으면 그 배율, 없으면 <paramref name="fallback"/></returns>
        float GetMinutesPerSecond(float fallback);

        /// <summary>시계를 앞으로 돌립니다. 수면·기절이 시간을 건너뛸 때 씁니다.</summary>
        /// <param name="minutes">건너뛸 게임 시간(분)</param>
        void AdvanceMinutes(float minutes);
    }

    /// <summary>
    /// 시계가 없을 때 그 자리를 채우는 <b>아무 일도 하지 않는 시계</b>입니다.
    ///
    /// 예전 정적 접근자는 <c>Instance</c>가 없으면 "영향 없음"에 해당하는 값을 돌려주어,
    /// 호출부가 시스템의 존재 여부를 신경 쓰지 않아도 되게 했습니다. 그 성질을 여기로 옮겼습니다.
    /// </summary>
    public sealed class NullGameClock : IGameClock
    {
        /// <summary>모두가 공유하는 하나뿐인 인스턴스입니다. 상태가 없으므로 나눠 써도 안전합니다.</summary>
        public static readonly NullGameClock Instance = new NullGameClock();

        private NullGameClock() { }

        /// <summary>시계가 없으면 아무 시간도 흐르지 않았습니다.</summary>
        public float TotalMinutes { get { return 0f; } }

        /// <summary>시계가 없으면 대낮으로 봅니다. 예전 <c>GetDaylight</c>가 1을 돌려주던 것과 같습니다.</summary>
        public float Daylight { get { return 1f; } }

        /// <summary>시계가 없으면 밤이 아닙니다. 예전 <c>IsNightNow</c>가 false를 돌려주던 것과 같습니다.</summary>
        public bool IsNight { get { return false; } }

        /// <summary>이것은 진짜 시계가 아닙니다. 소비자는 자기 방식으로 시간을 세야 합니다.</summary>
        public bool IsRunning { get { return false; } }

        /// <summary>부르는 쪽이 넘긴 기본값을 그대로 돌려줍니다.</summary>
        /// <param name="fallback">소비자가 자기 인스펙터에 적어 둔 배율</param>
        /// <returns><paramref name="fallback"/> 그대로</returns>
        public float GetMinutesPerSecond(float fallback) { return fallback; }

        /// <summary>돌릴 시계가 없으므로 아무 일도 하지 않습니다.</summary>
        /// <param name="minutes">무시됩니다.</param>
        public void AdvanceMinutes(float minutes) { }
    }
}
