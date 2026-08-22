namespace CarDrive.Systems
{
    /// <summary>
    /// 니즈를 <b>올리고 읽는</b> 계약입니다.
    ///
    /// <b>왜 인터페이스인가.</b> 예전에는 니즈를 올리고 싶은 쪽이 <c>NeedsSystem.Report()</c>
    /// 정적 메서드를 불렀습니다. 달리면 피로가 쌓이고, 귀신이 붙으면 스트레스가 오르는데,
    /// <c>PlayerFootMotor</c>와 <c>AttachedGhostController</c>의 어떤 시그니처에도
    /// 니즈가 나타나지 않았습니다.
    ///
    /// <b>왜 Common 이 아니라 여기인가.</b> 이 계약은 <see cref="NeedType"/>을 씁니다.
    /// 그 열거형은 니즈 시스템의 것이고, 니즈를 모르는 층이 알 이유가 없습니다.
    /// Gameplay 는 Systems 를 참조하므로 여기 두어도 소비자는 그대로 봅니다.
    /// (날씨·시계 계약이 <c>Common</c>에 있는 것은 float 과 bool 만 오가기 때문입니다)
    /// </summary>
    public interface INeedsSink
    {
        /// <summary>
        /// 니즈를 그만큼 올립니다. 음수를 넣으면 해소됩니다.
        /// </summary>
        /// <param name="type">올릴 니즈</param>
        /// <param name="amount">올릴 양. 음수면 해소</param>
        void Add(NeedType type, float amount);

        /// <summary>
        /// 니즈를 그만큼 해소합니다. <c>Add</c>에 음수를 넣는 것과 같지만,
        /// 호출부에서 <b>해소하려는 의도</b>가 드러납니다 — 비를 맞아 갈증이 가시는 코드가
        /// "갈증을 음수만큼 더한다"로 읽히면 곤란합니다.
        /// </summary>
        /// <param name="type">해소할 니즈</param>
        /// <param name="amount">해소할 양(양수)</param>
        void Satisfy(NeedType type, float amount);

        /// <summary>
        /// 지금 니즈 수치입니다. 0이 비어 있음, 1이 가득 참이며 한계까지 더 넘칠 수 있습니다.
        /// </summary>
        /// <param name="type">읽을 니즈</param>
        /// <returns>현재 수치</returns>
        float GetValue(NeedType type);
    }

    /// <summary>
    /// 니즈 시스템이 없을 때 그 자리를 채우는 <b>밑 빠진 독</b>입니다.
    ///
    /// 예전 <c>NeedsSystem.Report()</c>는 <c>Instance</c>가 없으면 조용히 지나갔습니다.
    /// 그 성질을 그대로 옮겼으므로, 니즈 시스템을 빼고 실행해도 달리기와 전투는 계속 돕니다.
    /// </summary>
    public sealed class NullNeedsSink : INeedsSink
    {
        /// <summary>모두가 공유하는 하나뿐인 인스턴스입니다. 상태가 없으므로 나눠 써도 안전합니다.</summary>
        public static readonly NullNeedsSink Instance = new NullNeedsSink();

        private NullNeedsSink() { }

        /// <summary>받을 곳이 없으므로 조용히 버립니다.</summary>
        /// <param name="type">무시됩니다.</param>
        /// <param name="amount">무시됩니다.</param>
        public void Add(NeedType type, float amount) { }

        /// <summary>해소할 니즈가 없으므로 조용히 지나갑니다.</summary>
        /// <param name="type">무시됩니다.</param>
        /// <param name="amount">무시됩니다.</param>
        public void Satisfy(NeedType type, float amount) { }

        /// <summary>니즈가 없으면 언제나 비어 있는 것으로 봅니다.</summary>
        /// <param name="type">무시됩니다.</param>
        /// <returns>항상 0</returns>
        public float GetValue(NeedType type) { return 0f; }
    }
}
