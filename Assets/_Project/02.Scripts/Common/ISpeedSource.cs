namespace CarDrive.Common
{
    /// <summary>
    /// "플레이어가 지금 얼마나 빨리 움직이는가"를 묻는 창구입니다.
    ///
    /// <b>왜 인터페이스인가.</b> <see cref="Systems.TerrainDetailLod"/>는 속도가 붙으면 풀 그리는
    /// 거리를 줄입니다. 그런데 그 속도를 알려면 <c>Vehicle.Current.controller.CurrentSpeed</c>를
    /// 봐야 했고, 그 한 줄 때문에 <b>Systems 계층이 Gameplay 계층을 거꾸로 참조</b>했습니다.
    /// 어셈블리를 나누는 순간 그 참조가 순환이 되어 컴파일이 막힙니다.
    ///
    /// 그래서 묻는 쪽은 인터페이스만 알고, <b>누가 답하는지는 Composition이 정합니다.</b>
    /// 나중에 도보 속도까지 반영하고 싶어지면 구현을 하나 갈아 끼우면 되고,
    /// <see cref="Systems.TerrainDetailLod"/>는 그대로입니다.
    /// </summary>
    public interface ISpeedSource
    {
        /// <summary>
        /// 지금 이동 속도(km/h)입니다. 움직이는 것이 없으면 0입니다.
        /// </summary>
        float CurrentSpeedKmh { get; }
    }
}
