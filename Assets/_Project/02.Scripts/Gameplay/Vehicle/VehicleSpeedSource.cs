using CarDrive.Common;

namespace CarDrive.Gameplay
{
    /// <summary>
    /// "지금 타고 있는 차가 얼마나 빠른가"로 <see cref="ISpeedSource"/>에 답합니다.
    ///
    /// <b>왜 어댑터인가.</b> <see cref="Systems.TerrainDetailLod"/>는 속도가 붙으면 풀 거리를
    /// 줄이는데, 그 속도를 알려고 Systems 계층이 <see cref="Vehicle"/>을 거꾸로 참조하고 있었습니다.
    /// 이제 답하는 쪽이 Gameplay에 남고, 묻는 쪽은 인터페이스만 압니다.
    ///
    /// <b>도보 속도는 보지 않습니다.</b> 걸어서는 임계 속도(45km/h)에 닿을 일이 없어
    /// 컴포넌트를 하나 더 뒤질 이유가 없습니다. 나중에 필요해지면 이 클래스만 고치면 됩니다 —
    /// 그러라고 인터페이스로 갈라 둔 것입니다.
    ///
    /// MonoBehaviour가 아닙니다. 씬에 놓을 것이 없고, 상태도 없습니다.
    /// </summary>
    public class VehicleSpeedSource : ISpeedSource
    {
        /// <summary>
        /// 지금 타고 있는 차량의 속도(km/h)입니다. 걸어 다니는 중이면 0입니다.
        /// </summary>
        public float CurrentSpeedKmh
        {
            get
            {
                Vehicle vehicle = Vehicle.Current;
                if (vehicle == null || vehicle.controller == null) return 0f;

                return vehicle.controller.CurrentSpeed;
            }
        }
    }
}
