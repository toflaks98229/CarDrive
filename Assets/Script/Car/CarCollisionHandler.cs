using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 차량의 물리적 충돌을 감지하고, 관련 효과(카메라, UI)를 호출하며
/// 체력을 관리하는 역할만 전담하는 클래스입니다.
/// 이 컴포넌트는 CarController와 같은 GameObject에 추가해야 합니다.
/// </summary>
public class CarCollisionHandler : MonoBehaviour
{
    [Header("연동 컴포넌트")]
    [Tooltip("충돌 시 흔들림 효과를 주기 위한 CarCameraEffects 컴포넌트 (씬에서 찾거나 할당)")]
    public CarCameraEffects carCameraEffects;

    [Tooltip("충돌 시 흔들림 효과를 적용할 UIElementShaker 리스트 (씬에서 찾거나 할당)")]
    public List<UIElementShaker> uiShakers = new List<UIElementShaker>();

    [Tooltip("차량의 체력을 표시하는 TextHealthBar 컴포넌트 (할당 필요)")]
    public TextHealthBar healthBar;

    [Header("충돌 설정")]
    [Tooltip("적과 충돌 시 받을 데미지")]
    public int damageOnEnemyCollision = 10;

    /// <summary>
    /// 스크립트가 처음 활성화될 때 연동 컴포넌트들을 찾습니다.
    /// (인스펙터에서 직접 할당하는 것을 권장합니다)
    /// </summary>
    void Start()
    {
        if (carCameraEffects == null)
        {
            carCameraEffects = FindObjectOfType<CarCameraEffects>();
        }
        if (uiShakers == null || uiShakers.Count == 0)
        {
            uiShakers = new List<UIElementShaker>(FindObjectsOfType<UIElementShaker>());
        }
        if (healthBar == null)
        {
            Debug.LogWarning("CarCollisionHandler: HealthBar가 할당되지 않았습니다. 체력 감소가 작동하지 않습니다.");
        }
    }

    /// <summary>
    /// 다른 Collider와 충돌이 시작될 때 호출됩니다.
    /// (원본 CarController의 OnCollisionEnter 로직)
    /// </summary>
    private void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.CompareTag("Enemy"))
        {
            // 1. 카메라 흔들림
            if (carCameraEffects != null)
            {
                carCameraEffects.TriggerImpactShake();
            }

            // 2. UI 흔들림
            if (uiShakers != null)
            {
                foreach (UIElementShaker shaker in uiShakers)
                {
                    if (shaker != null) shaker.TriggerImpactShake();
                }
            }

            // 3. 체력 감소
            if (healthBar != null)
            {
                healthBar.TakeDamage(damageOnEnemyCollision);
            }
        }
    }
}
