using UnityEngine;

namespace CarDrive.Common
{
    /// <summary>
    /// 충격을 받으면 흔들릴 줄 아는 것의 규약입니다.
    ///
    /// <b>왜 만들었는가.</b> 흔들림 구현이 네 개인데 공통 인터페이스가 없었습니다.
    /// <see cref="Gameplay.CarImpactShake"/>(차체), <see cref="UI.UIElementShaker"/>(계기판),
    /// <see cref="Gameplay.CarCameraEffects"/>(주행 진동), <see cref="Gameplay.PlayerCameraShake"/>(시야).
    /// 넷 다 <c>TriggerImpactShake</c>를 공개하는데 시그니처가 제각각이라,
    /// 충돌 처리가 <b>차체용과 계기판용을 각각 다른 코드 경로로</b> 불러야 했습니다.
    /// 흔들 대상이 하나 늘 때마다 그 분기도 하나씩 늘어납니다.
    ///
    /// 이제 부딪히는 쪽은 목록 하나를 훑기만 합니다. 무엇이 흔들리는지 알 필요가 없습니다.
    ///
    /// <b>방향과 세기를 함께 받습니다.</b> 방향이 필요 없는 구현(계기판·시야)은 그냥 무시하면 됩니다.
    /// 반대로 방향을 빼 버리면 차체가 "어느 쪽에서 맞았는지"를 알 수 없게 되어,
    /// 맞은 쪽이 들리는 연출이 불가능해집니다. <b>더 많이 아는 쪽에 맞춰야 합니다.</b>
    /// </summary>
    public interface IImpactShakable
    {
        /// <summary>
        /// 충격을 받아 흔들립니다.
        /// </summary>
        /// <param name="worldDirection">충격이 들어온 방향(월드). <c>Vector3.zero</c>면 방향을 모른다는 뜻입니다.</param>
        /// <param name="scale">세기 배율. 1이 기본입니다.</param>
        void TriggerImpactShake(Vector3 worldDirection, float scale);
    }
}
