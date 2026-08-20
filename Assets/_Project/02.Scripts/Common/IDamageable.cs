namespace CarDrive.Common
{
    /// <summary>
    /// 피해를 받을 수 있는 모든 대상의 공통 규약입니다.
    ///
    /// 예전에는 피해 전달이 네 가지 방식으로 흩어져 있었습니다.
    /// (컴포넌트 타입 분기 / 주입된 참조 / 컴포넌트 탐색 / 태그 비교)
    /// 공격하는 쪽은 상대가 무엇인지 몰라도 되어야 하므로 이 인터페이스 하나로 모읍니다.
    ///
    /// 새 적을 추가할 때 이것만 구현하면 앙크 공격이 자동으로 통합니다.
    /// PlayerAttacker를 고칠 필요가 없습니다.
    /// </summary>
    public interface IDamageable
    {
        /// <summary>이미 죽어서 더 이상 피해를 받지 않는 상태인지 여부입니다.</summary>
        bool IsDead { get; }

        /// <summary>
        /// 피해를 입힙니다.
        /// 앙크처럼 지속 피해를 주는 공격은 매 프레임 아주 작은 값으로 호출하므로
        /// 정수가 아니라 실수를 받습니다.
        /// </summary>
        void TakeDamage(float amount);
    }
}
