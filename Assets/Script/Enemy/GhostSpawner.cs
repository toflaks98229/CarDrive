using UnityEngine;

/// <summary>
/// [신규] CarController에 부착하여 AttachedGhost를 주기적으로 스폰하는 관리자입니다.
/// 스폰된 귀신은 이 오브젝트(차량)의 자식이 됩니다.
/// </summary>
[RequireComponent(typeof(CarController))]
public class GhostSpawner : MonoBehaviour
{
    [Header("귀신 프리팹")]
    [Tooltip("뒤에서 나타날 귀신 프리팹 (AttachedGhostController 필요)")]
    public GameObject rearGhostPrefab;
    [Tooltip("옆에서 나타날 귀신 프리팹 (AttachedGhostController 필요)")]
    public GameObject sideGhostPrefab;

    [Header("스폰 위치 (차량의 자식 Transform)")]
    [Tooltip("뒤쪽 귀신이 처음 생성될 위치를 가진 자식 Transform")]
    public Transform rearSpawnAnchor;
    [Tooltip("왼쪽(Side 1) 귀신이 처음 생성될 위치를 가진 자식 Transform")] // [수정됨]
    public Transform sideSpawnAnchor1;
    [Tooltip("오른쪽(Side 2) 귀신이 처음 생성될 위치를 가진 자식 Transform")] // [수정됨]
    public Transform sideSpawnAnchor2;


    [Header("귀신 공격 목표 (로컬 좌표)")]
    [Tooltip("뒤쪽 귀신이 도달할 차량의 로컬 좌표 (예: (0, 1, -2))")]
    public Vector3 rearGhostTargetOffset = new Vector3(0, 1, -2);
    [Tooltip("왼쪽(Side 1) 귀신이 도달할 차량의 로컬 좌표 (예: (-1, 1, 0))")] // [수정됨]
    public Vector3 sideGhostTargetOffset1 = new Vector3(-1, 1, 0);
    [Tooltip("오른쪽(Side 2) 귀신이 도달할 차량의 로컬 좌표 (예: (1, 1, 0))")] // [수정됨]
    public Vector3 sideGhostTargetOffset2 = new Vector3(1, 1, 0);


    [Header("스폰 타이밍")]
    [Tooltip("최소 스폰 간격 (초)")]
    public float minSpawnInterval = 15f;
    [Tooltip("최대 스폰 간격 (초)")]
    public float maxSpawnInterval = 30f;

    // --- Private Variables ---
    private CarController carController;
    private TextHealthBar carHealthBar;
    private AttachedGhostController currentRearGhost;
    private AttachedGhostController currentSideGhost;
    private float spawnTimer;

    /// <summary>
    /// 스크립트가 처음 활성화될 때 호출됩니다.
    /// </summary>
    void Start()
    {
        carController = GetComponent<CarController>();
        if (carController == null)
        {
            Debug.LogError("GhostSpawner: CarController를 찾을 수 없습니다!");
            this.enabled = false;
            return;
        }

        carHealthBar = carController.healthBar;
        if (carHealthBar == null)
        {
            Debug.LogError("GhostSpawner: CarController에 healthBar가 연결되지 않았습니다!");
            this.enabled = false;
            return;
        }

        // [수정됨] 스폰 위치 앵커 확인
        if (rearSpawnAnchor == null || sideSpawnAnchor1 == null || sideSpawnAnchor2 == null)
        {
            Debug.LogWarning("GhostSpawner: 모든 스폰 앵커(SpawnAnchor)가 설정되지 않았습니다.");
        }

        ResetSpawnTimer();
    }

    /// <summary>
    /// 매 프레임마다 호출됩니다.
    /// </summary>
    void Update()
    {
        // 시동이 켜져 있을 때만 스폰 타이머 작동
        if (!carController.IsEngineOn())
        {
            return;
        }

        spawnTimer -= Time.deltaTime;

        if (spawnTimer <= 0f)
        {
            TrySpawnGhost();
            ResetSpawnTimer();
        }
    }

    /// <summary>
    /// 스폰 타이머를 재설정합니다.
    /// </summary>
    private void ResetSpawnTimer()
    {
        spawnTimer = Random.Range(minSpawnInterval, maxSpawnInterval);
    }

    /// <summary>
    /// 귀신 스폰을 시도합니다.
    /// </summary>
    private void TrySpawnGhost()
    {
        // 50% 확률로 뒤쪽 또는 옆쪽 스폰 시도
        bool spawnRear = Random.Range(0, 2) == 0;

        if (spawnRear)
        {
            // 뒤쪽 귀신 스폰 시도
            if (currentRearGhost == null && rearGhostPrefab != null && rearSpawnAnchor != null)
            {
                // [수정됨] SpawnGhost에 부모 앵커(rearSpawnAnchor) 전달
                SpawnGhost(rearGhostPrefab, rearSpawnAnchor.position, rearGhostTargetOffset, rearSpawnAnchor, true);
            }
            // 스폰 실패 시 (조건 불충족), 다음 턴에 다시 시도
        }
        else
        {
            // 옆쪽 귀신 스폰 시도
            // [수정됨] sideSpawnAnchor1과 sideSpawnAnchor2를 모두 확인
            if (currentSideGhost == null && sideGhostPrefab != null && sideSpawnAnchor1 != null && sideSpawnAnchor2 != null)
            {
                // [추가됨] 50% 확률로 sideSpawnAnchor1 또는 sideSpawnAnchor2 선택
                bool spawnOnSide1 = Random.Range(0, 2) == 0;

                if (spawnOnSide1)
                {
                    SpawnGhost(sideGhostPrefab, sideSpawnAnchor1.position, sideGhostTargetOffset1, sideSpawnAnchor1, false);
                }
                else
                {
                    SpawnGhost(sideGhostPrefab, sideSpawnAnchor2.position, sideGhostTargetOffset2, sideSpawnAnchor2, false);
                }
            }
            // 스폰 실패 시 (조건 불충족), 다음 턴에 다시 시도
        }
    }

    /// <summary>
    /// [수정됨] 지정된 위치에 귀신을 스폰하고 초기화합니다. 부모 앵커를 파라미터로 받습니다.
    /// </summary>
    private void SpawnGhost(GameObject prefab, Vector3 spawnWorldPosition, Vector3 targetLocalOffset, Transform parentAnchor, bool isRearGhost)
    {
        Debug.Log(prefab.name + " 스폰 시도...");

        // 1. 지정된 앵커의 자식으로 귀신을 생성(Instantiate)합니다.
        // 부모를 'parentAnchor'로 설정합니다.
        GameObject ghostObj = Instantiate(prefab, spawnWorldPosition, transform.rotation, parentAnchor);

        // 2. 귀신 컨트롤러 스크립트를 가져와서 초기화합니다.
        AttachedGhostController ghostController = ghostObj.GetComponent<AttachedGhostController>();
        if (ghostController != null)
        {
            ghostController.Initialize(carHealthBar, targetLocalOffset);

            // 3. [수정] 관리 목록에 추가합니다. (부모 설정 로직 제거)
            if (isRearGhost)
            {
                currentRearGhost = ghostController;
            }
            else
            {
                currentSideGhost = ghostController;
            }
        }
        else
        {
            Debug.LogError(prefab.name + " 프리팹에 AttachedGhostController 스크립트가 없습니다!", prefab);
            Destroy(ghostObj); // 잘못된 프리팹이므로 제거
        }
    }
}

