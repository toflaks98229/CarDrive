using UnityEngine;
using System.Collections.Generic;
using VContainer;
using CarDrive.Systems;
using CarDrive.Common;

namespace CarDrive.Gameplay
{
    /// <summary>
    /// 차량이 적과 부딪혔을 때 일어날 일을 한자리에서 처리합니다.
    ///
    /// 부딪히면 네 가지가 함께 일어납니다. <b>차체와 계기판이 흔들리고</b>, <b>내구도가 줄고</b>,
    /// <b>스트레스가 오르고</b>, <b>충돌음이 납니다.</b> 넷 다 같은 사건의 다른 얼굴이라
    /// 한 곳에서 부르는 편이 낫습니다.
    ///
    /// <b>적 판정은 태그가 아니라 <see cref="IHostile"/>로 합니다.</b> 태그 문자열은 오타가 나도
    /// 컴파일이 통과하고, 씬에서 조용히 어긋납니다. 콜라이더가 자식에 달려 있을 수 있어
    /// 부모까지 거슬러 올라가며 찾습니다.
    ///
    /// <see cref="CarController"/>와 같은 GameObject에 두어야 합니다.
    /// </summary>
    public class CarCollisionHandler : MonoBehaviour
    {
        // --- Public Member Variables ---

        /// <summary>
        /// 이 충돌 처리가 속한 차량입니다. 비워 두면 이 오브젝트와 부모에서 찾습니다.
        ///
        /// 차체 흔들림·계기판·내구도를 전부 여기서 가져옵니다. 이것을 못 찾으면
        /// 충돌이 나도 아무 일도 일어나지 않습니다.
        /// </summary>
        [Header("연동 컴포넌트")]
        [Tooltip("이 충돌 처리가 속한 차량. 비워두면 이 오브젝트와 부모에서 찾습니다. " +
                 "차체 흔들림·계기판·내구도를 전부 여기서 가져옵니다.")]
        public Vehicle vehicle;

        /// <summary>적과 한 번 부딪힐 때 차량 내구도에서 깎을 양입니다.</summary>
        [Header("충돌 설정")]
        [Tooltip("적과 충돌 시 받을 데미지")]
        public int damageOnEnemyCollision = 10;

        /// <summary>
        /// 적과 부딪힐 때 오르는 스트레스 양입니다.
        ///
        /// 니즈 시스템이 주입되지 않았으면 조용히 버려집니다.
        /// 그래서 니즈 없이 차만 있는 씬에서도 그대로 굴러갑니다.
        /// </summary>
        [Tooltip("적과 충돌 시 오르는 스트레스 (NeedsSystem이 씬에 없으면 무시됩니다)")]
        public float stressOnEnemyCollision = 0.06f;

        /// <summary>적과 부딪힐 때 차체가 흔들리는 세기에 곱할 배율입니다.</summary>
        [Tooltip("적과 충돌 시 차체가 흔들리는 세기 배율")]
        public float shakeScaleOnEnemyCollision = 1f;

        /// <summary>충돌음을 재생할 컨트롤러입니다. 비워 두면 같은 오브젝트에서 찾습니다.</summary>
        [Header("사운드")]
        [Tooltip("충돌음을 재생할 컨트롤러. 비워두면 같은 오브젝트에서 찾습니다.")]
        public CarSoundController soundController;

        /// <summary>
        /// 충돌음이 최대 볼륨이 되는 충돌 속도(m/s)입니다. 이보다 느리면 그만큼 작게 납니다.
        ///
        /// 살짝 스친 것과 정면으로 박은 것이 같은 소리를 내면 충돌의 무게가 사라집니다.
        /// </summary>
        [Tooltip("충돌음이 최대 볼륨이 되는 충돌 속도(m/s). 이보다 느리면 더 작게 납니다.")]
        public float soundFullVolumeSpeed = 15f;

        // --- Private Member Variables ---

        /// <summary>충돌 스트레스를 흘려보낼 곳입니다. 주입되지 않으면 조용히 버려집니다.</summary>
        private INeedsSink _needs = NullNeedsSink.Instance;

        // --- Unity Event Functions ---

        /// <summary>
        /// 연동 컴포넌트들을 찾아 두고, 빠진 것이 있으면 알립니다.
        ///
        /// 인스펙터에서 직접 할당하는 쪽을 권장합니다. 여기서 찾는 것은 배선을 빠뜨렸을 때의
        /// 대비책입니다. 차량과 내구도는 없으면 경고를 남기고, 사운드는 없어도
        /// 조용히 넘어갑니다. 소리가 안 나는 것은 고장이 아니기 때문입니다.
        /// </summary>
        void Start()
        {
            if (vehicle == null) vehicle = GetComponentInParent<Vehicle>();
            if (vehicle == null)
            {
                GameLog.Warn(GameLog.Channel.Player, "CarCollisionHandler: 이 차량의 Vehicle을 찾지 못했습니다.", this);
            }
            else if (vehicle.health == null)
            {
                GameLog.Warn(GameLog.Channel.Player, "CarCollisionHandler: VehicleHealth를 찾지 못해 내구도가 줄지 않습니다.", this);
            }

            if (soundController == null)
            {
                // 사운드는 있으면 쓰고 없으면 조용히 넘어갑니다.
                soundController = GetComponent<CarSoundController>();
            }
        }

        /// <summary>
        /// 무언가와 부딪혔을 때 그것이 적이면 흔들림·내구도·스트레스·소리를 한 번에 처리합니다.
        ///
        /// 적이 아니면 아무것도 하지 않습니다. 벽이나 바닥과의 충돌은
        /// 물리 엔진이 알아서 처리하도록 둡니다.
        /// </summary>
        /// <param name="collision">부딪힌 상대와 접촉 지점·상대 속도가 담긴 충돌 정보</param>
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
                _needs.Add(NeedType.Stress, stressOnEnemyCollision);

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

        // --- Public Methods ---

        /// <summary>
        /// 스트레스를 올릴 곳을 받습니다.
        ///
        /// VContainer가 <c>Start</c> 이전에 부릅니다. 주입이 없는 씬에서는
        /// <see cref="NullNeedsSink"/>가 그대로 남아 스트레스가 조용히 버려집니다.
        /// </summary>
        /// <param name="needs">니즈를 받는 쪽. null이면 기존 값을 그대로 둡니다</param>
        [Inject]
        public void Construct(INeedsSink needs)
        {
            if (needs != null) _needs = needs;
        }
    }
}
