using UnityEngine;

/// <summary>
/// 차량과 충돌 시 위쪽으로 튕겨나가는 장애물 스크립트입니다.
/// 이 스크립트가 적용된 GameObject는 반드시 Rigidbody와 Collider가 있어야 합니다.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(Collider))]
public class ObstacleController : MonoBehaviour
{
    // --- Public Member Variables ---

    [Header("충돌 설정")]
    [Tooltip("차와 부딪혔을 때 받을 최소한의 위쪽 힘")]
    public float baseBounceForce = 10f;

    [Tooltip("충돌 속도에 비례하여 추가될 힘의 배율")]
    public float speedToForceMultiplier = 2f;

    [Tooltip("힘을 가하는 방식 (Impulse: 순간적인 폭발력, Force: 지속적인 힘)")]
    public ForceMode bounceForceMode = ForceMode.Impulse;

    // --- Private Member Variables ---

    /// <summary>
    /// 이 장애물의 Rigidbody 컴포넌트
    /// </summary>
    private Rigidbody rb;

    /// <summary>
    /// (선택 사항) 한 번만 튕기게 하려면 이 플래그를 사용합니다.
    /// </summary>
    private bool hasBeenHit = false;

    // --- Unity Event Functions ---

    /// <summary>
    /// 스크립트가 처음 활성화될 때 호출됩니다.
    /// </summary>
    void Start()
    {
        // Rigidbody 컴포넌트를 가져와서 rb 변수에 저장합니다.
        rb = GetComponent<Rigidbody>();
    }

    /// <summary>
    /// 다른 Collider와 물리적 충돌이 시작될 때 호출됩니다.
    /// </summary>
    /// <param name="collision">충돌 관련 정보를 담고 있는 Collision 객체</param>
    private void OnCollisionEnter(Collision collision)
    {
        // (선택 사항) 이미 부딪혔다면 다시 실행하지 않음
        // if (hasBeenHit) return;

        // 1. 충돌한 대상이 'CarController' 컴포넌트(혹은 그 자식)를 가지고 있는지 확인합니다.
        CarController car = collision.gameObject.GetComponentInParent<CarController>();

        // 2. CarController를 가진 대상(차량)과 부딪힌 것이 맞다면
        if (car != null)
        {
            Debug.Log(gameObject.name + "가 " + collision.gameObject.name + "와 충돌!");

            // 3. 충돌 속도를 계산합니다. (relativeVelocity.magnitude는 두 물체의 상대 속도 크기)
            float impactSpeed = collision.relativeVelocity.magnitude;

            // 4. 충돌 속도에 기반한 동적인 힘(튕겨나갈 힘)을 계산합니다.
            float dynamicForce = baseBounceForce + (impactSpeed * speedToForceMultiplier);

            // 5. 이 오브젝트(장애물)의 Rigidbody에 위쪽(Vector3.up)으로 계산된 힘을 가합니다.
            rb.AddForce(Vector3.up * dynamicForce, bounceForceMode);

            // (선택 사항) 한 번만 튕기도록 플래그 설정
            // hasBeenHit = true;
        }
    }
}
