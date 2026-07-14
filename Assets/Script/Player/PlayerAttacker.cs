using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// [신규]
/// 플레이어의 앙크 공격 로직(입력, 충전, 공격 판정)을 전담하는 클래스입니다.
/// 이 컴포넌트는 PlayerCameraController와 같은 카메라 GameObject에 추가해야 합니다.
/// </summary>
public class PlayerAttacker : MonoBehaviour
{
    [Header("앙크 공격 설정")]
    [Tooltip("앙크의 초당 데미지")]
    public float ankhDamagePerSecond = 20f;

    [Tooltip("앙크 공격이 활성화되기까지의 충전 시간(초)")]
    public float ankhChargeTime = 1.0f;

    [Tooltip("앙크 공격의 최대 사거리")]
    public float ankhAttackDistance = 10f;

    [Tooltip("앙크 공격 판정의 반경 (실린더/구체의 굵기)")]
    public float ankhAttackRadius = 0.5f;

    [Tooltip("앙크 공격이 감지할 적 레이어")]
    public LayerMask enemyLayer;

    [Header("연동 컴포넌트")]
    [Tooltip("앙크 애니메이션을 재생할 UI 컨트롤러")]
    public AnkhAnimation ankhAnimator; // (AnkhAnimation 스크립트가 필요합니다)


    // --- Private Member Variables ---
    private bool isAnkhHeld = false;
    private float ankhChargeTimer;
    private Transform cameraTransform;

    /// <summary>
    /// 스크립트가 처음 활성화될 때 카메라 Transform을 캐시하고 타이머를 초기화합니다.
    /// </summary>
    void Start()
    {
        cameraTransform = transform; // 이 스크립트가 카메라에 붙어있다고 가정
        ankhChargeTimer = ankhChargeTime;

        if (ankhAnimator == null)
        {
            Debug.LogWarning("PlayerAttacker: AnkhAnimator가 할당되지 않았습니다. 앙크 공격 애니메이션/효과가 작동하지 않습니다.");
        }
    }

    /// <summary>
    /// 매 프레임마다 호출됩니다.
    /// </summary>
    void Update()
    {
        HandleAnkhAnimationInput();
        HandleAnkhAttack();
    }

    /// <summary>
    /// 앙크 애니메이션 입력(좌클릭)을 처리합니다.
    /// (원본 PlayerCameraController의 메서드)
    /// </summary>
    private void HandleAnkhAnimationInput()
    {
        if (Input.GetMouseButtonDown(0))
        {
            isAnkhHeld = true;
            ankhChargeTimer = ankhChargeTime; // 충전 타이머 초기화

            if (ankhAnimator != null)
            {
                ankhAnimator.ShowAnkh();
            }
        }

        if (Input.GetMouseButtonUp(0))
        {
            isAnkhHeld = false;

            if (ankhAnimator != null)
            {
                ankhAnimator.HideAnkh();
                ankhAnimator.SetTargetChargeProgress(0f); // 충전 효과 0으로
                //ankhAnimator.StopShake(); // 혹시 모를 떨림 중지
            }
        }
    }

    /// <summary>
    /// 앙크를 들고 있을 때의 공격 로직 및 충전 효과를 처리합니다.
    /// (원본 PlayerCameraController의 메서드)
    /// </summary>
    private void HandleAnkhAttack()
    {
        if (!isAnkhHeld || ankhAnimator == null) return;

        // 1. 앙크 충전
        if (ankhChargeTimer > 0)
        {
            ankhChargeTimer -= Time.deltaTime;
            float chargeProgress = Mathf.Clamp01(1.0f - (ankhChargeTimer / ankhChargeTime));
            ankhAnimator.SetTargetChargeProgress(chargeProgress);
            return; // 아직 충전 중
        }

        // 2. 충전 완료 (공격 활성화)
        ankhAnimator.SetTargetChargeProgress(1.0f); // 최대 충전 상태 유지

        // 3. 공격 판정 (SphereCastAll)
        RaycastHit[] hits = Physics.SphereCastAll(
            cameraTransform.position,
            ankhAttackRadius,
            cameraTransform.forward,
            ankhAttackDistance,
            enemyLayer
        );

        // 4. 데미지 처리
        if (hits.Length > 0)
        {
            ankhAnimator.StartShake(); // 공격 명중 시 떨림

            // [수정] 두 종류의 적을 모두 처리하기 위한 리스트
            List<EnemyController> hitEnemies = new List<EnemyController>();
            List<AttachedGhostController> hitGhosts = new List<AttachedGhostController>();

            foreach (RaycastHit hit in hits)
            {
                // Collider가 붙은 오브젝트 또는 그 부모에서 컴포넌트를 찾습니다.
                // (Collider가 자식 오브젝트에 있을 경우 대비)

                // 4-1. EnemyController (기존 적) 확인
                EnemyController enemy = hit.collider.GetComponentInParent<EnemyController>();
                if (enemy != null && !hitEnemies.Contains(enemy))
                {
                    hitEnemies.Add(enemy);
                    continue; // 다음 히트 대상으로
                }

                // 4-2. AttachedGhostController (신규 귀신) 확인
                AttachedGhostController ghost = hit.collider.GetComponentInParent<AttachedGhostController>();
                if (ghost != null && !hitGhosts.Contains(ghost))
                {
                    hitGhosts.Add(ghost);
                }
            }

            // 4-3. 감지된 모든 적에게 데미지 전달
            float damageToDeal = ankhDamagePerSecond * Time.deltaTime;

            foreach (EnemyController enemy in hitEnemies)
            {
                enemy.TakeDamage(damageToDeal);
            }

            foreach (AttachedGhostController ghost in hitGhosts)
            {
                ghost.TakeDamage(damageToDeal);
            }
        }
        else
        {
            ankhAnimator.StopShake(); // 빗나갔을 때 떨림 중지
        }
    }
}
