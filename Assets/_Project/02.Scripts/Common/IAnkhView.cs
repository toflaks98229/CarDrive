namespace CarDrive.Common
{
    /// <summary>
    /// 앙크를 <b>보여 주는 쪽</b>과의 계약입니다.
    ///
    /// <b>왜 필요한가.</b> 앙크 연출은 화면에 붙는 UI(<c>RectTransform</c>)이고,
    /// 그것을 부리는 <c>PlayerAttacker</c>는 씬의 배우입니다. 규칙상 참조는
    /// UI → Gameplay 로만 흘러야 하는데, 공격 쪽이 연출 클래스를 이름으로 알고 있어
    /// <b>화살표가 거꾸로 나 있었습니다.</b>
    ///
    /// 계약을 사이에 두면 공격 쪽은 "무엇을 시킬 수 있는가"만 알고, 그것을 누가 어떻게
    /// 그리는지는 모릅니다. 앙크 연출을 Feel 로 갈아끼우거나 월드 스페이스 모델로 바꿔도
    /// 공격 코드는 그대로입니다.
    ///
    /// <b>메서드 이름은 기존 구현 그대로입니다.</b> 옮기는 김에 이름까지 바꾸면
    /// 이 변경이 무엇을 했는지 diff 에서 읽히지 않습니다.
    /// </summary>
    public interface IAnkhView
    {
        /// <summary>앙크를 꺼냅니다.</summary>
        void ShowAnkh();

        /// <summary>앙크를 집어넣습니다.</summary>
        void HideAnkh();

        /// <summary>충전 진행도를 넘깁니다. 0이면 비어 있고 1이면 가득 찼습니다.</summary>
        /// <param name="progress">충전 진행도(0~1)</param>
        void SetTargetChargeProgress(float progress);

        /// <summary>피해를 주는 동안 떨리기 시작합니다.</summary>
        void StartShake();

        /// <summary>떨림을 멈춥니다.</summary>
        void StopShake();
    }
}
