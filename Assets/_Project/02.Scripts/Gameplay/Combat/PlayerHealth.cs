using CarDrive.Common;

namespace CarDrive.Gameplay
{
    /// <summary>
    /// 플레이어 본인의 체력입니다.
    ///
    /// VehicleHealth와 <b>타입이 다르다는 것 자체가 목적</b>입니다.
    /// 음료를 마셨을 때 회복할 대상을 이 타입으로 받으면,
    /// 인스펙터에서 차량 내구도를 잘못 끼워 넣는 일이 아예 불가능해집니다.
    /// </summary>
    public class PlayerHealth : Health
    {
        // 상태와 동작은 전부 Health에 있습니다.
        // 이 클래스는 "플레이어의 것"이라는 구분을 위해 존재합니다.

        /// <summary>
        /// 자신을 레지스트리에 등록합니다. 세이브 시스템 등이 Start에서 찾아 씁니다.
        ///
        /// <b>플레이어 체력만 등록합니다.</b> 차량 내구도와 적 체력은 여럿이라
        /// "씬에 하나"라는 전제가 성립하지 않습니다.
        /// </summary>
        protected override void Awake()
        {
            base.Awake();
            GameContext.Register(this);
        }

        /// <summary>등록을 해제합니다.</summary>
        void OnDestroy()
        {
            GameContext.Unregister(this);
        }
    }
}
