using UnityEngine;

/// <summary>
/// 운전대에 붙여 시동 상호작용을 제공합니다.
/// 차에 타고 있고 조준점이 운전대에 걸려 있을 때만 시동을 걸거나 끌 수 있습니다.
/// </summary>
public class SteeringWheelInteractable : MonoBehaviour, IInteractable
{
    // --- Public Member Variables ---

    /// <summary>이 운전대가 속한 차량입니다. 비워두면 Start에서 부모를 거슬러 찾습니다.</summary>
    [Header("연동")]
    [Tooltip("이 운전대가 속한 차량. 비워두면 부모에서 찾습니다.")]
    public Vehicle vehicle;

    /// <summary>시동이 꺼져 있을 때 표시할 안내 문구입니다.</summary>
    [Header("문구 (다국어 대응)")]
    [Tooltip("시동이 꺼져 있을 때 표시할 문구")]
    public string startLabel = "시동 걸기";

    /// <summary>시동이 켜져 있을 때 표시할 안내 문구입니다.</summary>
    [Tooltip("시동이 켜져 있을 때 표시할 문구")]
    public string stopLabel = "시동 끄기";

    // --- Unity Event Functions ---

    /// <summary>
    /// 비어 있는 차량 참조를 부모에서 채웁니다. 끝내 찾지 못하면 경고를 남깁니다.
    /// </summary>
    void Start()
    {
        if (vehicle == null) vehicle = GetComponentInParent<Vehicle>();

        if (vehicle == null)
        {
            Debug.LogWarning("SteeringWheelInteractable: 이 운전대가 속한 Vehicle을 찾지 못해 시동을 걸 수 없습니다.", this);
        }
    }

    // --- Public Methods ---

    // --- IInteractable ---

    /// <summary>
    /// <b>이 차에 타고 있을 때만</b> 시동을 만질 수 있습니다.
    /// 차가 여러 대일 때 창밖으로 다른 차의 운전대를 조준해 시동을 거는 일을 막습니다.
    /// </summary>
    /// <returns>차량과 컨트롤러가 있고 플레이어가 탑승 중이면 true를 반환합니다.</returns>
    public bool CanInteract()
    {
        return vehicle != null && vehicle.controller != null && vehicle.IsOccupied;
    }

    /// <summary>
    /// 현재 시동 상태에 맞는 안내 문구를 돌려줍니다.
    /// </summary>
    /// <returns>시동이 켜져 있으면 stopLabel, 꺼져 있으면 startLabel. 상호작용할 수 없으면 빈 문자열입니다.</returns>
    public string GetInteractionLabel()
    {
        if (!CanInteract()) return "";
        return vehicle.controller.IsEngineOn() ? stopLabel : startLabel;
    }

    /// <summary>
    /// 시동을 켜거나 끕니다. 상호작용할 수 없는 상태면 아무 일도 하지 않습니다.
    /// </summary>
    public void Interact()
    {
        if (!CanInteract()) return;
        vehicle.controller.ToggleEngine();
    }
}
