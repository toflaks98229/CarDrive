using UnityEngine;
using System.Collections.Generic;
using CarDrive.Systems;
using CarDrive.UI;
using CarDrive.Common;

namespace CarDrive.Gameplay
{
    /// <summary>
    /// 차량의 물리적 충돌을 감지하고, 관련 효과(카메라, UI)를 호출하며
    /// 체력을 관리하는 역할만 전담하는 클래스입니다.
    /// 이 컴포넌트는 CarController와 같은 GameObject에 추가해야 합니다.
    /// </summary>
    public class CarCollisionHandler : MonoBehaviour
    {
        [Header("연동 컴포넌트")]
        [Tooltip("이 충돌 처리가 속한 차량. 비워두면 이 오브젝트와 부모에서 찾습니다. " +
                 "차체 흔들림·계기판·내구도를 전부 여기서 가져옵니다.")]
        public Vehicle vehicle;

        [Header("충돌 설정")]
        [Tooltip("적과 충돌 시 받을 데미지")]
        public int damageOnEnemyCollision = 10;

        [Tooltip("적과 충돌 시 오르는 스트레스 (NeedsSystem이 씬에 없으면 무시됩니다)")]
        public float stressOnEnemyCollision = 0.06f;

        [Tooltip("적과 충돌 시 차체가 흔들리는 세기 배율")]
        public float shakeScaleOnEnemyCollision = 1f;

        [Header("사운드")]
        [Tooltip("충돌음을 재생할 컨트롤러. 비워두면 같은 오브젝트에서 찾습니다.")]
        public CarSoundController soundController;

        [Tooltip("충돌음이 최대 볼륨이 되는 충돌 속도(m/s). 이보다 느리면 더 작게 납니다.")]
        public float soundFullVolumeSpeed = 15f;

        /// <summary>
        /// 스크립트가 처음 활성화될 때 연동 컴포넌트들을 찾습니다.
        /// (인스펙터에서 직접 할당하는 것을 권장합니다)
        /// </summary>
        void Start()
        {
            if (vehicle == null) vehicle = GetComponentInParent<Vehicle>();
            if (vehicle == null)
            {
                Debug.LogWarning("CarCollisionHandler: 이 차량의 Vehicle을 찾지 못했습니다.", this);
            }
            else if (vehicle.health == null)
            {
                Debug.LogWarning("CarCollisionHandler: VehicleHealth를 찾지 못해 내구도가 줄지 않습니다.", this);
            }

            if (soundController == null)
            {
                // 사운드는 있으면 쓰고 없으면 조용히 넘어갑니다.
                soundController = GetComponent<CarSoundController>();
            }
        }

        /// <summary>
        /// 다른 Collider와 충돌이 시작될 때 호출됩니다.
        /// (원본 CarController의 OnCollisionEnter 로직)
        /// </summary>
        private void OnCollisionEnter(Collision collision)
        {
            if (vehicle == null) return;

            // 적 판정은 태그 문자열이 아니라 컴포넌트로 합니다.
            // 콜라이더가 자식에 있을 수 있으므로 부모까지 올라가며 찾습니다.
            if (collision.collider != null && collision.collider.GetComponentInParent<IHostile>() != null)
            {
                // 1. 흔들림 — 차체와 계기판을 <b>한 목록으로</b> 훑습니다.
                //
                //    예전에는 차체용과 계기판용을 각각 다른 코드 경로로 불렀습니다.
                //    이제 둘 다 IImpactShakable 이라 무엇이 흔들리는지 알 필요가 없습니다.
                //    (계기판은 이 차량의 것만 들어 있습니다. 예전에는 씬 전체를 긁어모아
                //     차가 둘이면 서로의 UI가 흔들렸습니다)
                //
                //    부딪힌 지점에서 차량 중심으로 향하는 방향을 충격 방향으로 씁니다.
                //    방향을 안 쓰는 구현(계기판)은 그냥 무시합니다.
                Vector3 direction = Vector3.zero;
                if (collision.contactCount > 0)
                {
                    direction = transform.position - collision.GetContact(0).point;
                }

                IReadOnlyList<IImpactShakable> shakables = vehicle.Shakables;
                for (int i = 0; i < shakables.Count; i++)
                {
                    shakables[i].TriggerImpactShake(direction, shakeScaleOnEnemyCollision);
                }

                // 2. 내구도 감소
                if (vehicle.health != null)
                {
                    vehicle.health.TakeDamage(damageOnEnemyCollision);
                }

                // 3. 스트레스 상승
                NeedsSystem.Report(NeedType.Stress, stressOnEnemyCollision);

                // 4. 충돌음. 세게 부딪힐수록 크게 납니다.
                if (soundController != null)
                {
                    float strength = soundFullVolumeSpeed > 0.01f
                        ? Mathf.Clamp01(collision.relativeVelocity.magnitude / soundFullVolumeSpeed)
                        : 1f;
                    soundController.PlayCollisionSound(strength);
                }
            }
        }
    }
}
