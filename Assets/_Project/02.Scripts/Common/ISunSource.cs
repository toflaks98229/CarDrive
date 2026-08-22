using UnityEngine;

namespace CarDrive.Common
{
    /// <summary>
    /// 씬의 <b>해</b>가 누구이고 얼마나 밝을 수 있는지를 알려 주는 계약입니다.
    ///
    /// <b>왜 시계와 따로 두는가.</b> 지금 이 값들은 <c>TimeSystem</c>의 인스펙터에 적혀 있습니다.
    /// 시각과 해가 함께 움직이므로 그 자리가 편했기 때문입니다. 하지만 "몇 시인가"와
    /// "어느 라이트가 해인가"는 다른 질문이고, <c>SkyController</c>는 뒤쪽만 필요합니다.
    /// 시계 계약(<see cref="IGameClock"/>)에 이것을 섞으면 시계를 쓰는 모든 곳이
    /// <c>Light</c>를 알게 됩니다.
    ///
    /// 나중에 해의 주인이 조명 쪽으로 옮겨 가더라도, 이 계약을 구현하는 쪽만 바뀝니다.
    /// </summary>
    public interface ISunSource
    {
        /// <summary>씬에서 해 역할을 하는 방향광입니다. 지정되지 않았으면 null입니다.</summary>
        Light Sun { get; }

        /// <summary>한낮에 해가 낼 수 있는 최대 밝기입니다.</summary>
        float SunMaxIntensity { get; }
    }
}
