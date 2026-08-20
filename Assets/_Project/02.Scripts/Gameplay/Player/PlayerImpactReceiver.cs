using UnityEngine;
using UnityEngine.Events;
using CarDrive.Systems;
using CarDrive.Common;

namespace CarDrive.Gameplay
{
    /// <summary>
    /// 도보 플레이어가 유령에게 부딪혔을 때의 반응을 모읍니다.
    /// 시야 흔들림, 스트레스 상승, 체력 감소를 한 곳에서 처리합니다.
    ///
    /// 판정은 태그가 아니라 이 컴포넌트의 존재로 합니다.
    /// (PlayerCar 프리팹이 이미 "Player" 태그를 쓰고 있어서, 도보 리그에 같은 태그를
    ///  붙이면 기존 적 추적 대상이 둘로 갈라지기 때문입니다)
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class PlayerImpactReceiver : MonoBehaviour
    {
        // --- Public Member Variables ---

        /// <summary>흔들 시야입니다. 보통 Head에 붙은 PlayerCameraShake이며, 비워두면 자식에서 찾습니다.</summary>
        [Header("연동")]
        [Tooltip("흔들 시야. 보통 Head에 붙은 PlayerCameraShake입니다.")]
        public PlayerCameraShake cameraShake;

        /// <summary>체력을 깎을 대상입니다. 비워두면 체력은 줄지 않습니다.</summary>
        [Tooltip("체력을 깎을 대상. 보통 플레이어의 PlayerHealth입니다. 비워두면 체력은 줄지 않습니다.")]
        public Health healthBar;

        /// <summary>한 번 부딪혔을 때의 시야 흔들림 배율입니다.</summary>
        [Header("피격 효과")]
        [Tooltip("한 번 부딪혔을 때 시야 흔들림 배율")]
        public float shakeScale = 1f;

        /// <summary>한 번 부딪혔을 때 오르는 스트레스입니다.</summary>
        [Tooltip("한 번 부딪혔을 때 오르는 스트레스")]
        public float stressPerHit = 0.06f;

        /// <summary>한 번 부딪혔을 때 깎이는 체력입니다. 0이면 체력은 줄지 않습니다.</summary>
        [Tooltip("한 번 부딪혔을 때 깎이는 체력. 0이면 체력은 줄지 않습니다.")]
        public float damagePerHit = 0f;

        /// <summary>연속 접촉으로 효과가 도배되지 않도록 하는 최소 간격(초)입니다.</summary>
        [Tooltip("연속 접촉으로 효과가 도배되지 않도록 하는 최소 간격(초)")]
        public float hitCooldown = 0.5f;

        [Header("직접 걸어 들어갔을 때")]
        // 적 판정은 태그 문자열이 아니라 IHostile 컴포넌트로 합니다.
        // 이 프로젝트는 이미 태그 충돌로 한 번 곤란을 겪었습니다.

        /// <summary>플레이어가 직접 걸어 들어가 부딪힌 경우에도 반응할지 여부입니다.</summary>
        [Tooltip("체크를 해제하면 상대가 다가와 부딪힌 경우만 반응합니다.")]
        public bool reactToWalkingIntoEnemies = true;

        /// <summary>피격이 실제로 처리되었을 때 한 번 호출됩니다.</summary>
        [Header("이벤트")]
        [Tooltip("피격이 실제로 처리되었을 때")]
        public UnityEvent onImpact;

        // --- Private Member Variables ---

        /// <summary>마지막으로 피격이 처리된 시각입니다. hitCooldown 판정에 씁니다.</summary>
        private float lastHitTime = -99f;

        // --- Unity Event Functions ---

        /// <summary>
        /// 흔들 시야가 지정되지 않았으면 자식에서 찾습니다. 끝내 없으면 경고를 남깁니다.
        /// </summary>
        void Start()
        {
            if (cameraShake == null)
            {
                // 보통 Head가 자식으로 있으므로 자식에서 찾아 봅니다.
                cameraShake = GetComponentInChildren<PlayerCameraShake>(true);
            }
            if (cameraShake == null)
            {
                Debug.LogWarning("PlayerImpactReceiver: PlayerCameraShake를 찾지 못해 시야가 흔들리지 않습니다.", this);
            }
        }

        /// <summary>
        /// 플레이어가 걸어가다 무언가에 부딪혔을 때 호출됩니다.
        /// (상대가 다가와 부딪힌 경우는 상대 쪽에서 TakeImpact를 호출합니다)
        /// </summary>
        void OnControllerColliderHit(ControllerColliderHit hit)
        {
            if (!reactToWalkingIntoEnemies) return;
            if (hit.collider == null) return;

            // 콜라이더가 자식에 있을 수 있으므로 부모까지 올라가며 찾습니다.
            if (hit.collider.GetComponentInParent<IHostile>() == null) return;

            Vector3 direction = transform.position - hit.point;
            TakeImpact(1f, direction);
        }

        // --- Public Methods ---

        /// <summary>
        /// 충격을 받습니다. 유령 쪽에서 호출하거나 위의 접촉 판정에서 호출됩니다.
        /// </summary>
        /// <param name="intensity">세기 배율</param>
        /// <param name="worldDirection">충격이 들어온 방향(월드). 지금은 연출에 쓰지 않지만 밀려남 등에 쓸 수 있습니다.</param>
        public void TakeImpact(float intensity, Vector3 worldDirection)
        {
            // 접촉이 이어지는 동안 매 프레임 터지지 않도록 막습니다.
            if (Time.time - lastHitTime < hitCooldown) return;
            lastHitTime = Time.time;

            if (cameraShake != null)
            {
                cameraShake.TriggerImpactShake(shakeScale * intensity);
            }

            if (stressPerHit > 0f)
            {
                NeedsSystem.Report(NeedType.Stress, stressPerHit * intensity);
            }

            if (damagePerHit > 0f && healthBar != null)
            {
                healthBar.TakeDamage(damagePerHit * intensity);
            }

            Debug.Log("PlayerImpactReceiver: 플레이어가 충격을 받았습니다.");
            if (onImpact != null) onImpact.Invoke();
        }
    }
}
