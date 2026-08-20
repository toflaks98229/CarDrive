namespace CarDrive.Gameplay
{
    /// <summary>
    /// 적의 체력입니다.
    ///
    /// <see cref="PlayerHealth"/>·<see cref="VehicleHealth"/>와 같은 이유로 존재합니다.
    /// 타입이 다르다는 것 자체가 목적이라, 인스펙터에서 "적 체력" 자리에
    /// 플레이어 체력이나 차량 내구도를 끼워 넣는 일이 타입 때문에 불가능해집니다.
    ///
    /// 예전에는 EnemyController와 AttachedGhostController가 각자
    /// <c>private float currentHealth</c>를 들고 있었습니다. 그래서 이 프로젝트에
    /// 체력을 다루는 방식이 <b>두 갈래</b>였고, 적만 <see cref="Health"/>가 제공하는
    /// Revision·Heal·Revive·onDeath를 쓸 수 없었습니다.
    /// </summary>
    public class EnemyHealth : Health
    {
        // 상태와 동작은 전부 Health에 있습니다.
        // 이 클래스는 "적의 것"이라는 구분을 위해 존재합니다.
    }
}
