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

    [Tooltip("조준 광선을 쏠 기준. 비워두면 이 오브젝트가 카메라인지 확인하고, 아니면 Camera.main을 씁니다.")]
    public Transform aimSource;

    [Tooltip("한 번에 판정할 수 있는 최대 콜라이더 수. 버퍼를 미리 잡아 두므로 " +
             "공격 중에도 프레임마다 새로 할당하지 않습니다.")]
    public int maxCollidersPerHit = 16;

    [Header("연동 컴포넌트")]
    [Tooltip("앙크 애니메이션을 재생할 UI 컨트롤러")]
    public AnkhAnimation ankhAnimator; // (AnkhAnimation 스크립트가 필요합니다)

    [Tooltip("물건 들기 컴포넌트. 좌클릭이 들기에 쓰이는 상황이면 앙크를 꺼내지 않습니다. " +
             "비워두면 같은 오브젝트에서 찾습니다.")]
    public PlayerCarrier carrier;

    [Tooltip("앙크 사운드를 재생할 컨트롤러. 비워두면 같은 오브젝트에서 찾습니다.")]
    public PlayerSoundController soundController;


    // --- Private Member Variables ---

    /// <summary>앙크를 꺼내 들고 있는지 여부입니다.</summary>
    private bool isAnkhHeld = false;

    /// <summary>앙크를 충전한 시간(초)입니다. 충전이 끝나면 발사 루프로 넘어갑니다.</summary>
    private float ankhChargeTimer;

    /// <summary>공격 판정 광선을 쏠 기준 Transform입니다. 보통 메인 카메라입니다.</summary>
    private Transform cameraTransform;

    // 충전이 끝나 발사 루프가 돌고 있는지. 루프를 한 번만 시작하기 위해 씁니다.
    private bool isFiring = false;

    // 공격 판정용 버퍼. 미리 잡아 두고 재사용해 GC 압력을 없앱니다.
    private RaycastHit[] hitBuffer;

    /// <summary>한 번의 판정에서 이미 때린 대상들입니다. 같은 적을 중복으로 때리지 않게 합니다.</summary>
    private readonly List<IDamageable> hitTargets = new List<IDamageable>();

    /// <summary>
    /// 스크립트가 처음 활성화될 때 카메라 Transform을 캐시하고 타이머를 초기화합니다.
    /// </summary>
    void Start()
    {
        // "카메라에 붙어 있다"는 가정을 주석이 아니라 코드로 확인합니다.
        cameraTransform = PlayerAim.Resolve(aimSource, this);
        ankhChargeTimer = ankhChargeTime;

        hitBuffer = new RaycastHit[Mathf.Max(1, maxCollidersPerHit)];

        if (carrier == null) carrier = GetComponent<PlayerCarrier>();

        // 사운드는 있으면 쓰고 없으면 조용히 넘어갑니다.
        if (soundController == null) soundController = GetComponent<PlayerSoundController>();

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
        // 오버레이 버튼을 누르는 클릭이 공격으로 들어가지 않게 합니다.
        if (GameInputGate.Suspended)
        {
            if (isAnkhHeld) ReleaseAnkh();
            return;
        }

        HandleAnkhAnimationInput();
        HandleAnkhAttack();
    }

    /// <summary>
    /// 앙크 애니메이션 입력(좌클릭)을 처리합니다.
    /// (원본 PlayerCameraController의 메서드)
    /// </summary>
    private void HandleAnkhAnimationInput()
    {
        // 물건을 들고 있는 중이면 앙크를 내립니다. (좌클릭이 내려놓기로 쓰이므로)
        if (isAnkhHeld && carrier != null && carrier.IsCarrying)
        {
            ReleaseAnkh();
            return;
        }

        if (Input.GetMouseButtonDown(0))
        {
            // 조준점에 들 수 있는 물건이 있거나 이미 들고 있다면
            // 이번 좌클릭은 PlayerCarrier가 씁니다.
            if (carrier != null && carrier.UsesLeftClick) return;

            isAnkhHeld = true;
            ankhChargeTimer = ankhChargeTime; // 충전 타이머 초기화

            if (ankhAnimator != null)
            {
                ankhAnimator.ShowAnkh();
            }

            if (soundController != null) soundController.PlayAnkhCharge();
        }

        if (Input.GetMouseButtonUp(0))
        {
            ReleaseAnkh();
        }
    }

    /// <summary>
    /// 앙크를 내리고 충전 효과를 초기화합니다.
    /// </summary>
    private void ReleaseAnkh()
    {
        isAnkhHeld = false;

        if (ankhAnimator != null)
        {
            ankhAnimator.HideAnkh();
            ankhAnimator.SetTargetChargeProgress(0f); // 충전 효과 0으로
            //ankhAnimator.StopShake(); // 혹시 모를 떨림 중지
        }

        // 발사 중이었다면 루프를 멈춥니다.
        if (isFiring)
        {
            isFiring = false;
            if (soundController != null) soundController.StopAnkhFireLoop();
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

        // 충전이 막 끝난 순간에만 발사 루프를 시작합니다.
        if (!isFiring)
        {
            isFiring = true;
            if (soundController != null) soundController.StartAnkhFireLoop();
        }

        // 3. 공격 판정
        // NonAlloc은 미리 잡아 둔 버퍼를 채우므로 프레임마다 배열을 새로 만들지 않습니다.
        int hitCount = Physics.SphereCastNonAlloc(
            cameraTransform.position,
            ankhAttackRadius,
            cameraTransform.forward,
            hitBuffer,
            ankhAttackDistance,
            enemyLayer
        );

        // 4. 맞은 대상 모으기
        // 적의 종류를 구분하지 않습니다. IDamageable이면 무엇이든 통합니다.
        // (새 적을 추가할 때 이 메서드를 고칠 필요가 없습니다)
        hitTargets.Clear();
        for (int i = 0; i < hitCount; i++)
        {
            Collider collider = hitBuffer[i].collider;
            if (collider == null) continue;

            // 콜라이더가 자식에 있을 수 있으므로 부모까지 올라가며 찾습니다.
            IDamageable target = collider.GetComponentInParent<IDamageable>();
            if (target == null || target.IsDead) continue;

            // 한 대상이 여러 콜라이더로 잡힐 수 있으므로 중복을 걸러냅니다.
            if (hitTargets.Contains(target)) continue;

            hitTargets.Add(target);
        }

        // 5. 데미지 전달
        if (hitTargets.Count > 0)
        {
            ankhAnimator.StartShake(); // 공격 명중 시 떨림

            float damageToDeal = ankhDamagePerSecond * Time.deltaTime;
            for (int i = 0; i < hitTargets.Count; i++)
            {
                hitTargets[i].TakeDamage(damageToDeal);
            }
        }
        else
        {
            ankhAnimator.StopShake(); // 빗나갔을 때 떨림 중지
        }
    }
}
