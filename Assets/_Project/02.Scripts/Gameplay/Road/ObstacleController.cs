using UnityEngine;
using CarDrive.Common;

namespace CarDrive.Gameplay
{
    /// <summary>
    /// 차와 부딪히면 위로 튕겨 오르는 장애물입니다.
    ///
    /// <b>세게 박을수록 높이 뜹니다.</b> 고정된 힘만 주면 살살 밀어도 크게 날아가
    /// 부딪힌 무게가 전해지지 않습니다. 충돌 속도를 힘에 섞어 그것을 맞춥니다.
    ///
    /// <b>차와 부딪힐 때만 반응합니다.</b> 다른 장애물이나 지형과 스쳐도 튀지 않습니다.
    /// 판정은 <see cref="CarController"/>를 부모까지 거슬러 찾아서 합니다.
    ///
    /// Rigidbody와 Collider가 반드시 함께 있어야 합니다.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(Collider))]
    public class ObstacleController : MonoBehaviour
    {
        // --- Public Member Variables ---

        /// <summary>
        /// 차와 부딪혔을 때 받을 최소한의 위쪽 힘입니다.
        ///
        /// 충돌 속도와 무관하게 항상 더해집니다. 아주 천천히 밀어도
        /// 최소한 이만큼은 반응하게 하는 바닥값입니다.
        /// </summary>
        [Header("충돌 설정")]
        [Tooltip("차와 부딪혔을 때 받을 최소한의 위쪽 힘")]
        public float baseBounceForce = 10f;

        /// <summary>충돌 속도(m/s) 1당 더해질 힘입니다. 세게 박을수록 높이 뜨게 만드는 값입니다.</summary>
        [Tooltip("충돌 속도에 비례하여 추가될 힘의 배율")]
        public float speedToForceMultiplier = 2f;

        /// <summary>
        /// 힘을 가하는 방식입니다.
        ///
        /// <see cref="ForceMode.Impulse"/>는 순간적인 타격이라 부딪힌 그 순간에 튑니다.
        /// <see cref="ForceMode.Force"/>는 지속적인 힘이라 한 프레임분만 걸려 거의 안 움직입니다.
        /// </summary>
        [Tooltip("힘을 가하는 방식 (Impulse: 순간적인 폭발력, Force: 지속적인 힘)")]
        public ForceMode bounceForceMode = ForceMode.Impulse;

        /// <summary>충돌음을 재생할 컨트롤러입니다. 비워 두면 같은 오브젝트에서 찾습니다.</summary>
        [Header("사운드")]
        [Tooltip("충돌음을 재생할 컨트롤러. 비워두면 같은 오브젝트에서 찾습니다.")]
        public EnvironmentSoundController soundController;

        /// <summary>충돌음이 최대 볼륨이 되는 충돌 속도(m/s)입니다. 이보다 느리면 그만큼 작게 납니다.</summary>
        [Tooltip("충돌음이 최대 볼륨이 되는 충돌 속도(m/s). 이보다 느리면 더 작게 납니다.")]
        public float soundFullVolumeSpeed = 12f;

        // --- Private Member Variables ---

        /// <summary>
        /// 이 장애물의 Rigidbody 컴포넌트
        /// </summary>
        private Rigidbody rb;

        // --- Unity Event Functions ---

        /// <summary>
        /// 튕길 때 쓸 Rigidbody와 사운드 컨트롤러를 찾아 둡니다.
        ///
        /// 사운드는 없어도 그대로 굴러갑니다. 소리 없는 장애물은 고장이 아니기 때문입니다.
        /// </summary>
        void Start()
        {
            // Rigidbody 컴포넌트를 가져와서 rb 변수에 저장합니다.
            rb = GetComponent<Rigidbody>();

            // 사운드 컨트롤러는 있으면 쓰고 없으면 조용히 넘어갑니다.
            if (soundController == null) soundController = GetComponent<EnvironmentSoundController>();
        }

        /// <summary>
        /// 무언가와 부딪혔을 때 그것이 차라면 위로 튕겨 내고 충돌음을 냅니다.
        ///
        /// 차가 아니면 아무것도 하지 않습니다. 힘의 크기는 부딪힌 속도에 비례합니다.
        /// </summary>
        /// <param name="collision">충돌 관련 정보를 담고 있는 Collision 객체</param>
        private void OnCollisionEnter(Collision collision)
        {
            // 1. 충돌한 대상이 'CarController' 컴포넌트(혹은 그 자식)를 가지고 있는지 확인합니다.
            CarController car = collision.gameObject.GetComponentInParent<CarController>();

            // 2. CarController를 가진 대상(차량)과 부딪힌 것이 맞다면
            if (car != null)
            {
                GameLog.Info(GameLog.Channel.World, gameObject.name + "가 " + collision.gameObject.name + "와 충돌!");

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
