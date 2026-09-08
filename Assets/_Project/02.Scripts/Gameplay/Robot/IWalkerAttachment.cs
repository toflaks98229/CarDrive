namespace CarDrive.Gameplay
{
    /// <summary>
    /// 몸통에 얹혀 <b>몸통이 자리를 잡은 뒤에</b> 자세를 잡아야 하는 부품입니다.
    ///
    /// <b>왜 스스로 돌지 않는가.</b> 유니티는 같은 종류의 콜백 사이의 순서를 보장하지 않습니다.
    /// 포탑이 자기 <c>LateUpdate</c> 에서 돌면 어떤 프레임에는 몸통보다 먼저 돌고, 그 한 프레임
    /// 차이가 그대로 <b>총구의 떨림</b>이 됩니다. 다리가 <see cref="WalkerRobot"/> 에게 불려
    /// 다니는 것과 같은 이유입니다.
    ///
    /// <b>왜 인터페이스인가.</b> <see cref="WalkerRobot"/> 은 리그입니다 — 포탑이 무엇을 겨누는지,
    /// 머리가 무엇을 쫓는지 알 필요가 없습니다. 순서만 지켜 주면 됩니다.
    /// </summary>
    public interface IWalkerAttachment
    {
        /// <summary>몸통이 자리를 잡은 뒤 한 프레임분을 진행합니다.</summary>
        /// <param name="dt">시간 간격(초)</param>
        void Pose(float dt);
    }
}
