using UnityEngine;

/// <summary>
/// "지금 머리 위가 뚫려 있는가"를 판정합니다.
///
/// 비를 받아 마시는 것도, 비에 젖어 더러워지는 것도 같은 조건에서 성립해야 합니다.
/// 그래서 판정을 여기 한 곳에 두고 RainDrinking과 WeatherExposure가 함께 씁니다.
/// (나중에 판정을 캡슐 검사나 다중 샘플로 바꾸더라도 두 곳이 같이 움직입니다)
///
/// 호출 빈도 조절(주기 검사)은 각 호출부가 알아서 합니다. 여기서는 판정만 합니다.
/// </summary>
public static class SkyCover
{
    /// <summary>
    /// 지정한 위치에서 위로 광선을 쏘아 가림막이 없는지 확인합니다.
    /// </summary>
    /// <param name="origin">검사 시작 위치 (보통 머리 높이)</param>
    /// <param name="distance">이 거리 안에 무언가 있으면 가려진 것으로 봅니다.</param>
    /// <param name="mask">가림으로 칠 레이어. 플레이어 자신은 빼 두세요.</param>
    /// <returns>하늘이 뚫려 있으면 true</returns>
    public static bool IsOpen(Vector3 origin, float distance, LayerMask mask)
    {
        return !Physics.Raycast(origin, Vector3.up, distance, mask, QueryTriggerInteraction.Ignore);
    }
}
