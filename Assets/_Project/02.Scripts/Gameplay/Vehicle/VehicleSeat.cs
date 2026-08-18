using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 차량에 붙여 승하차 지점을 정의합니다.
/// 운전석 위치와, 내릴 때 플레이어를 놓을 후보 지점들을 가집니다.
/// </summary>
public class VehicleSeat : MonoBehaviour
{
    // --- Public Member Variables ---

    /// <summary>주행 중 카메라 리그가 따라갈 위치입니다. 비워두면 이 오브젝트를 씁니다.</summary>
    [Header("운전석")]
    [Tooltip("주행 중 카메라 리그가 따라갈 위치. 비워두면 이 오브젝트를 씁니다.")]
    public Transform driverAnchor;

    /// <summary>내릴 때 플레이어를 놓을 후보 지점들입니다. 앞에서부터 비어 있는 곳을 고릅니다.</summary>
    [Header("하차 지점")]
    [Tooltip("내릴 때 플레이어를 놓을 후보 지점들. 앞에서부터 비어 있는 곳을 고릅니다.")]
    public List<Transform> exitPoints = new List<Transform>();

    /// <summary>하차 지점이 비었는지 검사할 캡슐의 반경입니다.</summary>
    [Tooltip("하차 지점이 비었는지 검사할 반경")]
    public float exitClearanceRadius = 0.4f;

    /// <summary>하차 지점이 비었는지 검사할 캡슐의 높이입니다.</summary>
    [Tooltip("하차 지점이 비었는지 검사할 높이")]
    public float exitClearanceHeight = 1.8f;

    /// <summary>이 레이어에 무언가 있으면 하차 지점이 막힌 것으로 봅니다.</summary>
    [Tooltip("이 레이어에 무언가 있으면 막힌 것으로 봅니다.")]
    public LayerMask exitBlockMask = ~0;

    /// <summary>이 거리 안에서 바라봐야 탑승할 수 있습니다.</summary>
    [Header("탑승")]
    [Tooltip("이 거리 안에서 바라봐야 탑승할 수 있습니다.")]
    public float enterDistance = 3.5f;

    /// <summary>
    /// 탑승 중에만 보일 이 차량의 체력 표시입니다.
    /// 비워두면 차량 안의 TextHealthBar 오브젝트를 찾아 씁니다.
    /// 차마다 자기 것을 가리키므로 어느 차에 타든 그 차의 체력이 보입니다.
    /// </summary>
    [Header("체력 표시")]
    [Tooltip("탑승 중에만 보일 이 차량의 체력 표시. 비워두면 차량 안의 TextHealthBar 오브젝트를 찾아 씁니다. " +
             "차마다 자기 것을 가리키므로 어느 차에 타든 그 차의 체력이 보입니다.")]
    public GameObject healthDisplay;

    /// <summary>시작할 때 체력 표시를 꺼 둘지 여부입니다. 탑승하면 켜집니다.</summary>
    [Tooltip("체크하면 시작할 때 표시를 꺼 둡니다. 탑승하면 켜집니다.")]
    public bool hideHealthDisplayWhenEmpty = true;

    // --- Unity Event Functions ---

    /// <summary>
    /// 체력 표시 오브젝트가 지정되지 않았으면 차량 안에서 찾아 둡니다.
    /// </summary>
    void Awake()
    {
        ResolveHealthDisplay();
    }

    /// <summary>
    /// 씬 뷰에서 하차 지점을 눈으로 확인할 수 있게 그려 줍니다.
    /// 막힌 지점은 빨간색, 비어 있는 지점은 초록색으로 표시합니다.
    /// </summary>
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Transform anchor = GetDriverAnchor();
        if (anchor != null) Gizmos.DrawWireSphere(anchor.position, 0.25f);

        for (int i = 0; i < exitPoints.Count; i++)
        {
            if (exitPoints[i] == null) continue;

            Vector3 p = exitPoints[i].position;
            Gizmos.color = Application.isPlaying && !IsClear(p) ? Color.red : Color.green;
            Gizmos.DrawWireSphere(p + Vector3.up * exitClearanceRadius, exitClearanceRadius);
            Gizmos.DrawLine(p, p + Vector3.up * exitClearanceHeight);
        }
    }

    // --- Public Methods ---

    /// <summary>
    /// 주행 중 카메라가 위치할 기준 Transform입니다.
    /// </summary>
    /// <returns>지정된 운전석 앵커. 비어 있으면 이 오브젝트의 Transform입니다.</returns>
    public Transform GetDriverAnchor()
    {
        return driverAnchor != null ? driverAnchor : transform;
    }

    /// <summary>
    /// 이 차량의 체력 표시를 켜거나 끕니다.
    /// 탑승하면 켜고 내리면 끄기 때문에, 차 밖에서는 계기판이 보이지 않습니다.
    /// </summary>
    /// <param name="visible">체력 표시를 켤지 끌지 여부</param>
    public void SetHealthDisplayVisible(bool visible)
    {
        if (healthDisplay == null) ResolveHealthDisplay();
        if (healthDisplay == null) return;

        healthDisplay.SetActive(visible);
    }

    /// <summary>이 차량의 내구도입니다. (표시가 아니라 값)</summary>
    /// <returns>차량의 VehicleHealth. 찾지 못하면 null입니다.</returns>
    public VehicleHealth GetHealth()
    {
        if (healthDisplay != null)
        {
            VehicleHealth onDisplay = healthDisplay.GetComponent<VehicleHealth>();
            if (onDisplay != null) return onDisplay;
        }
        return GetComponentInChildren<VehicleHealth>(true);
    }

    /// <summary>
    /// 막히지 않은 하차 지점을 찾습니다.
    /// 모두 막혀 있으면 첫 번째 지점을, 지점 자체가 없으면 차량 왼쪽을 돌려줍니다.
    /// </summary>
    /// <returns>플레이어를 내려놓을 월드 좌표</returns>
    public Vector3 GetExitPosition()
    {
        for (int i = 0; i < exitPoints.Count; i++)
        {
            Transform point = exitPoints[i];
            if (point == null) continue;

            if (IsClear(point.position)) return point.position;
        }

        // 후보가 전부 막혔으면 그래도 첫 번째 지점으로 내립니다.
        for (int i = 0; i < exitPoints.Count; i++)
        {
            if (exitPoints[i] != null) return exitPoints[i].position;
        }

        // 지점을 하나도 설정하지 않았을 때의 마지막 수단입니다.
        Debug.LogWarning("VehicleSeat: 하차 지점이 없어 차량 왼쪽으로 내립니다.", this);
        return transform.position - transform.right * 2f;
    }

    /// <summary>
    /// 하차 지점 주변이 비어 있는지 검사합니다.
    /// </summary>
    /// <param name="position">검사할 하차 지점의 월드 좌표</param>
    /// <returns>사람이 설 공간이 있으면 true를 반환합니다.</returns>
    public bool IsClear(Vector3 position)
    {
        // 캡슐 형태로 사람이 설 공간이 있는지 확인합니다.
        Vector3 bottom = position + Vector3.up * exitClearanceRadius;
        Vector3 top = position + Vector3.up * Mathf.Max(exitClearanceHeight - exitClearanceRadius, exitClearanceRadius);

        return !Physics.CheckCapsule(bottom, top, exitClearanceRadius, exitBlockMask, QueryTriggerInteraction.Ignore);
    }

    // --- Private Methods ---

    /// <summary>
    /// 표시 오브젝트가 지정되지 않았으면 차량 안에서 찾아 둡니다.
    /// </summary>
    private void ResolveHealthDisplay()
    {
        if (healthDisplay != null) return;

        TextHealthBar bar = GetComponentInChildren<TextHealthBar>(true);
        if (bar != null) healthDisplay = bar.gameObject;
    }
}
