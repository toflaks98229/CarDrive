using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 무한 도로 생성을 총괄하는 관리자 클래스입니다.
/// 도로 프리팹들을 이용해 플레이어 앞에 새로운 도로를 생성하고,
/// 플레이어 뒤의 오래된 도로는 제거하여 성능을 관리합니다.
/// </summary>
public class RoadManager : MonoBehaviour
{
    // --- Public Member Variables ---

    [Header("도로 프리팹 설정")]
    [Tooltip("생성할 도로 조각들의 프리팹 리스트")]
    public List<GameObject> roadPrefabs;

    [Header("플레이어 및 생성 설정")]
    [Tooltip("플레이어 차량의 Transform (현재 사용되고 있지는 않지만, 추후 거리 기반 삭제 등에 사용 가능)")]
    public Transform playerTransform;

    [Tooltip("처음에 생성할 도로의 개수")]
    public int initialRoadCount = 5;

    [Tooltip("플레이어 뒤에 유지할 도로의 최대 개수 (성능 관리를 위해 오래된 도로 삭제)")]
    public int maxActiveRoads = 10;

    // --- Private Member Variables ---

    /// <summary>
    /// 현재 씬에 활성화(생성)되어 있는 도로 조각들의 리스트.
    /// </summary>
    private List<GameObject> activeRoads = new List<GameObject>();

    /// <summary>
    /// 다음 도로가 생성될 위치와 방향을 가진 Transform.
    /// (일반적으로 마지막에 생성된 도로의 끝에 있는 'NextSpawnPoint' 오브젝트)
    /// </summary>
    private Transform nextSpawnPoint;

    // --- Unity Event Functions ---

    /// <summary>
    /// 스크립트가 처음 활성화될 때 호출됩니다.
    /// </summary>
    void Start()
    {
        // RoadSegment 스크립트가 Manager를 쉽게 찾을 수 있도록 static 변수에 자기 자신을 할당
        RoadSegment.roadManager = this;

        // 초기 시작 지점 설정 (Manager 자신의 위치에서 시작)
        nextSpawnPoint = transform;

        // 게임 시작 시 초기 도로 생성
        for (int i = 0; i < initialRoadCount; i++)
        {
            SpawnRoad();
        }
    }

    // --- Public Methods ---

    /// <summary>
    /// 새로운 도로 조각을 생성하고 관리 리스트에 추가합니다.
    /// (RoadSegment의 OnTriggerEnter에 의해 호출됨)
    /// </summary>
    public void SpawnRoad()
    {
        // 도로 프리팹이 설정되지 않았으면 경고를 출력하고 종료합니다.
        if (roadPrefabs == null || roadPrefabs.Count == 0)
        {
            Debug.LogError("RoadManager: roadPrefabs 리스트가 비어있습니다!");
            return;
        }

        // 프리팹 리스트에서 무작위로 도로 하나를 선택
        GameObject randomRoadPrefab = roadPrefabs[Random.Range(0, roadPrefabs.Count)];

        // 선택된 도로를 nextSpawnPoint의 위치와 회전값에 맞춰 생성
        GameObject newRoad = Instantiate(randomRoadPrefab, nextSpawnPoint.position, nextSpawnPoint.rotation);

        // 생성된 도로를 활성화된 도로 리스트에 추가
        activeRoads.Add(newRoad);

        // 다음 도로가 생성될 위치를 새로 생성된 도로의 'NextSpawnPoint' 자식 오브젝트로 갱신
        Transform spawnPoint = newRoad.transform.Find("NextSpawnPoint");
        if (spawnPoint != null)
        {
            nextSpawnPoint = spawnPoint;
        }
        else
        {
            Debug.LogError("RoadManager: 생성된 도로 프리팹(" + newRoad.name + ")에 'NextSpawnPoint' 자식 오브젝트가 없습니다!");
        }

        // 활성화된 도로의 수가 너무 많아지면 가장 오래된 도로를 제거
        if (activeRoads.Count > maxActiveRoads)
        {
            DeleteOldestRoad();
        }
    }

    // --- Private Methods ---

    /// <summary>
    /// 리스트에서 가장 오래된 도로 조각을 씬에서 제거(파괴)합니다.
    /// </summary>
    private void DeleteOldestRoad()
    {
        // 가장 오래된 도로(리스트의 첫 번째 요소)를 가져옵니다.
        GameObject oldRoad = activeRoads[0];

        // 리스트에서 제거합니다.
        activeRoads.RemoveAt(0);

        // 씬에서 오브젝트를 파괴합니다.
        if (oldRoad != null)
        {
            Destroy(oldRoad);
        }
    }
}
