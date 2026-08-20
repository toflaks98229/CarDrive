namespace CarDrive.Gameplay
{
    /// <summary>
    /// 차량의 내구도입니다.
    ///
    /// 차량마다 자기 것을 하나씩 가집니다. 충돌 처리·귀신 공격·계기판 표시가
    /// 모두 이 컴포넌트 하나를 바라보므로, 예전처럼 CarController와
    /// CarCollisionHandler가 각각 따로 체력바를 들고 있다가 한쪽만 연결되어
    /// "귀신은 피해를 주는데 충돌은 안 준다" 같은 증상이 생기지 않습니다.
    /// </summary>
    public class VehicleHealth : Health
    {
        // 상태와 동작은 전부 Health에 있습니다.
        // 이 클래스는 "차량의 것"이라는 구분을 위해 존재합니다.
    }
}
