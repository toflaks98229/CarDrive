using UnityEngine;
using CarDrive.Common;

namespace CarDrive.Gameplay
{
    /// <summary>
    /// 충격을 받았을 때 차체가 실제로 흔들리도록 Rigidbody에 순간 충격을 줍니다.
    ///
    /// 카메라를 흔드는 것이 아니라 차를 흔듭니다. 카메라는 차량의 자식이므로
    /// 차가 출렁이면 시야도 따라 흔들리고, 그 편이 훨씬 자연스럽게 읽힙니다.
    ///
    /// 힘은 ForceMode.VelocityChange로 넣기 때문에 차량 질량과 무관하게
    /// 아래 수치가 그대로 "몇 m/s, 몇 rad/s 만큼 흔든다"는 뜻이 됩니다.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class CarImpactShake : MonoBehaviour, IImpactShakable
    {
        // --- Public Member Variables ---

        /// <summary>좌우로 기울어지는 정도(rad/s)입니다. 차가 롤링하는 느낌을 만듭니다.</summary>
        [Header("충격 세기")]
        [Tooltip("좌우로 기울어지는 정도 (rad/s). 차가 롤링하는 느낌을 만듭니다.")]
        public float rollImpulse = 1.4f;

        /// <summary>앞뒤로 들썩이는 정도(rad/s)입니다.</summary>
        [Tooltip("앞뒤로 들썩이는 정도 (rad/s)")]
        public float pitchImpulse = 0.7f;

        /// <summary>충돌 방향으로 밀리는 정도(m/s)입니다.</summary>
        [Tooltip("충돌 방향으로 밀리는 정도 (m/s)")]
        public float pushImpulse = 1.2f;

        /// <summary>위로 튀어오르는 정도(m/s)입니다.</summary>
        [Tooltip("위로 튀어오르는 정도 (m/s)")]
        public float liftImpulse = 0.6f;

        /// <summary>충격 후 각속도의 상한(rad/s)입니다. 차가 뒤집히는 것을 막습니다.</summary>
        [Header("안전 장치")]
        [Tooltip("충격 후 각속도를 이 값(rad/s)으로 제한합니다. 차가 뒤집히는 것을 막습니다.")]
        public float maxAngularSpeed = 2.2f;

        /// <summary>이 시간(초) 안에 들어온 연속 충격은 무시합니다.</summary>
        [Tooltip("이 시간(초) 안에 들어온 연속 충격은 무시합니다.")]
        public float cooldown = 0.15f;

        /// <summary>충격이 들어올 때 로그를 남길지 여부입니다.</summary>
        [Header("디버그")]
        [Tooltip("충격이 들어올 때 로그를 남깁니다.")]
        public bool logImpacts = false;

        // --- Private Member Variables ---

        /// <summary>충격을 가할 차체의 Rigidbody입니다.</summary>
        private Rigidbody body;

        /// <summary>마지막으로 충격이 들어온 시각입니다. cooldown 판정에 씁니다.</summary>
        private float lastImpactTime = -99f;

        // --- Unity Event Functions ---

        /// <summary>
        /// 충격을 가할 Rigidbody 참조를 가져옵니다.
        /// </summary>
        void Awake()
        {
            body = GetComponent<Rigidbody>();
        }

        // --- Public Methods ---

        /// <summary>
        /// 차체를 흔듭니다.
        /// </summary>
        /// <param name="worldDirection">충격이 들어온 방향(월드). Vector3.zero면 무작위 방향으로 처리합니다.</param>
        /// <param name="scale">세기 배율</param>
        public void TriggerImpactShake(Vector3 worldDirection, float scale)
        {
            if (body == null) return;
            if (Time.time - lastImpactTime < cooldown) return;
            lastImpactTime = Time.time;

            // 방향이 주어지지 않으면 좌우 중 아무 쪽에서 맞은 것으로 처리합니다.
            if (worldDirection.sqrMagnitude < 0.0001f)
            {
                worldDirection = transform.right * (Random.value < 0.5f ? -1f : 1f);
            }
            worldDirection.y = 0f;
            worldDirection.Normalize();

            // 맞은 쪽이 들리도록 롤 방향을 정합니다.
            // (오른쪽에서 맞으면 왼쪽으로 기울어야 자연스럽습니다)
            float side = Vector3.Dot(worldDirection, transform.right);
            float rollSign = side >= 0f ? -1f : 1f;

            // 앞뒤 성분은 피치로 씁니다.
            float front = Vector3.Dot(worldDirection, transform.forward);

            Vector3 torque =
                transform.forward * (rollImpulse * rollSign * scale) +
                transform.right * (pitchImpulse * -front * scale);

            Vector3 force =
                worldDirection * (pushImpulse * scale) +
                Vector3.up * (liftImpulse * scale);

            body.AddTorque(torque, ForceMode.VelocityChange);
            body.AddForce(force, ForceMode.VelocityChange);

            ClampAngularVelocity();

            if (logImpacts)
            {
                Debug.Log("CarImpactShake: 차체 충격 (세기 " + scale.ToString("F2") + ")", this);
            }
        }

        /// <summary>방향 없이 흔들 때 쓰는 간편 버전입니다.</summary>
        public void TriggerImpactShake()
        {
            TriggerImpactShake(Vector3.zero, 1f);
        }

        // --- Private Methods ---

        /// <summary>
        /// 각속도가 과하면 잘라내 차가 뒤집히지 않게 합니다.
        /// </summary>
        private void ClampAngularVelocity()
        {
            if (body.angularVelocity.magnitude > maxAngularSpeed)
            {
                body.angularVelocity = body.angularVelocity.normalized * maxAngularSpeed;
            }
        }
    }
}
