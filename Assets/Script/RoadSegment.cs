using UnityEngine;

/// <summary>
/// 개별 도로 조각에 부착되는 스크립트입니다.
/// 플레이어가 자신의 트리거 영역에 들어오면 RoadManager에게 다음 도로 생성을 요청합니다.
/// </summary>
public class RoadSegment : MonoBehaviour
{
    // --- Public Member Variables (Static) ---

    /// <summary>
    /// RoadManager에 쉽게 접근하기 위한 static 참조 변수.
    /// RoadManager가 시작할 때 이 변수에 자기 자신을 할당합니다.
    /// </summary>
    public static RoadManager roadManager;

    // --- Private Member Variables ---

    /// <summary>
    /// 중복 생성을 방지하기 위한 플래그.
    /// true가 되면 이 세그먼트는 더 이상 생성 요청을 하지 않습니다.
    /// </summary>
    private bool hasTriggered = false;

    // --- Unity Event Functions ---

    /// <summary>
    /// 이 도로의 트리거 영역(Collider)에 다른 Collider가 들어왔을 때 호출됩니다.
    /// </summary>
    /// <param name="other">트리거에 들어온 대상의 Collider</param>
    private void OnTriggerEnter(Collider other)
    {
        // 이미 한번 발동했거나, 들어온 객체가 'Player' 태그가 아니면 무시
        if (hasTriggered || !other.CompareTag("Player"))
        {
            return;
        }

        // 중복 실행 방지 플래그 설정
        hasTriggered = true;

        // RoadManager에게 다음 도로를 생성하라고 요청
        if (roadManager != null)
        {
            roadManager.SpawnRoad();
        }
        else
        {
            Debug.LogError("RoadSegment: RoadManager가 할당되지 않았습니다!");
        }
    }
}
