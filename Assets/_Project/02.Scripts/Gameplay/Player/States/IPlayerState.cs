namespace CarDrive.Gameplay
{
    /// <summary>
    /// 플레이어가 취할 수 있는 상태 하나의 규약입니다.
    ///
    /// 예전에는 PlayerModeController가 탑승·하차마다 여덟 개 참조의 enabled를
    /// 직접 켜고 끄면서 두 상태를 오갔습니다. 상태가 둘일 때는 성립하지만,
    /// 수면·정비·대화·사망이 하나씩 붙을 때마다 켜고 끌 조합이 배로 늘어납니다.
    ///
    /// 그래서 "이 상태가 되면 무엇을 켜고 무엇을 끄는가"를 상태 자신이 들고 있게 합니다.
    /// 새 상태를 추가할 때는 이 인터페이스를 구현한 클래스를 하나 만들면 되고,
    /// <b>기존 상태 클래스는 건드리지 않습니다.</b>
    ///
    /// 참조는 상태가 나눠 갖지 않고 PlayerModeController가 계속 들고 있습니다.
    /// 상태마다 인스펙터 연결이 생기면 씬에서 이어야 할 곳이 상태 수만큼 늘어나기 때문입니다.
    /// </summary>
    public interface IPlayerState
    {
        /// <summary>이 상태가 나타내는 모드입니다. 바깥에서 상태를 구분하는 데 씁니다.</summary>
        PlayerMode Mode { get; }

        /// <summary>
        /// 이 상태로 들어올 때 한 번 호출됩니다.
        /// </summary>
        /// <param name="player">참조와 공용 동작을 들고 있는 컨트롤러</param>
        void Enter(PlayerModeController player);

        /// <summary>
        /// 이 상태에서 나갈 때 한 번 호출됩니다.
        /// </summary>
        /// <param name="player">참조와 공용 동작을 들고 있는 컨트롤러</param>
        void Exit(PlayerModeController player);
    }
}
