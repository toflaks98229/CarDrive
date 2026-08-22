namespace CarDrive.Common
{
    /// <summary>
    /// 마시는 동작을 <b>보여 주는 쪽</b>과의 계약입니다.
    ///
    /// <see cref="IAnkhView"/>와 같은 이유로 존재합니다 — 연출은 UI 이고 마시는 주체는
    /// 배우이므로, 배우가 연출 클래스를 이름으로 알면 계층 화살표가 거꾸로 납니다.
    ///
    /// <b>재생 시간을 묻는 것까지가 계약입니다.</b> 마시는 쪽은 연출이 끝날 때까지
    /// 기다려야 하는데, 그 길이를 아는 것은 연출 자신뿐입니다.
    /// </summary>
    public interface IDrinkView
    {
        /// <summary>연출 한 번에 걸리는 전체 시간(초)입니다.</summary>
        float TotalDuration { get; }

        /// <summary>마시는 연출을 재생합니다.</summary>
        void PlayDrinkAnimation();
    }
}
