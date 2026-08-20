using UnityEngine;
using CarDrive.Systems;

namespace CarDrive.Gameplay
{
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

        [Header("사운드")]
        [Tooltip("충돌음을 재생할 컨트롤러. 비워두면 같은 오브젝트에서 찾습니다.")]
        public EnvironmentSoundController soundController;

        [Tooltip("충돌음이 최대 볼륨이 되는 충돌 속도(m/s). 이보다 느리면 더 작게 납니다.")]
        public float soundFullVolumeSpeed = 12f;

        // --- Private Member Variables ---

        /// <summary>
        /// 이 장애물의 Rigidbody 컴포넌트
        /// </summary>
        private Rigidbody rb;

        // --- Unity Event Functions ---

        /// <summary>
        /// 스크립트가 처음 활성화될 때 호출됩니다.
        /// </summary>
        void Start()
        {
            // Rigidbody 컴포넌트를 가져와서 rb 변수에 저장합니다.
            rb = GetComponent<Rigidbody>();

            // 사운드 컨트롤러는 있으면 쓰고 없으면 조용히 넘어갑니다.
            if (soundController == null) soundController = GetComponent<EnvironmentSoundController>();
        }

        /// <summary>
        /// 다른 Collider와 물리적 충돌이 시작될 때 호출됩니다.
        /// </summary>
        /// <param name="collision">충돌 관련 정보를 담고 있는 Collision 객체</param>
        private void OnCollisionEnter(Collision collision)
        {
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

                // 6. 충돌음. 세게 부딪힐수록 크게 납니다.
                if (soundController != null)
                {
                    float strength = soundFullVolumeSpeed > 0.01f
                        ? Mathf.Clamp01(impactSpeed / soundFullVolumeSpeed)
                        : 1f;
                    soundController.PlayHitSound(strength);
                }
            }
        }
    }
}
