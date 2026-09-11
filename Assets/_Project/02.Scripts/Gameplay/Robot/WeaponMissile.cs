using UnityEngine;
using CarDrive.Common;

namespace CarDrive.Gameplay
{
    /// <summary>
    /// 실제로 <b>날아가는</b> 탄입니다.
    ///
    /// <b>왜 필요한가.</b> 미사일 랙은 여섯 발을 0.22 초 간격으로 쏟아내는데,
    /// 지금까지 그것이 전부 <b>히트스캔</b>이었습니다. 쏘는 순간 이미 맞아 있어서
    /// <b>아무것도 날아가지 않았습니다.</b> 덮개가 열리고 섬광이 터지는데 하늘에는
    /// 아무것도 없고, 멀리 있어도 피할 수 없었습니다.
    ///
    /// 미사일이 미사일인 이유는 <b>도착하는 데 시간이 걸린다</b>는 것입니다.
    /// 90 m 를 초속 55 m 로 가면 1.6 초입니다. 그 1.6 초가 차를 몰고 벗어날 시간이고,
    /// 이 게임에서 로봇을 상대하는 방법이 됩니다.
    ///
    /// <b>물리를 쓰지 않습니다.</b> <c>Rigidbody</c> 로 날리면 여섯 발이 한꺼번에
    /// 물리 단계에 들어가고, 무엇보다 <b>맞는 순간이 프레임마다 달라집니다.</b>
    /// 여기서는 한 프레임에 갈 거리를 직접 재고 그 선분만 확인합니다 —
    /// 빠른 탄이 얇은 벽을 <b>뚫고 지나가는 일</b>도 그래서 안 생깁니다.
    /// </summary>
    [AddComponentMenu("CarDrive/무기 미사일 (WeaponMissile)")]
    public class WeaponMissile : MonoBehaviour
    {
        // --- Public Member Variables ---

        /// <summary>초속(m)입니다.</summary>
        [Header("비행")]
        [Tooltip("초속(m). 90 m 를 55 로 가면 1.6 초입니다")]
        [Range(5f, 200f)]
        public float speed = 55f;

        /// <summary>
        /// 이 거리를 지나면 스스로 사라집니다(m).
        ///
        /// 쏜 무기의 사거리로 덮어씁니다. 여기 값은 그냥 쏴 볼 때의 기본입니다.
        /// </summary>
        [Tooltip("이 거리를 지나면 사라집니다(m). 쏜 무기의 사거리로 덮어씁니다")]
        public float reach = 90f;

        /// <summary>맞은 자리에 낼 것입니다.</summary>
        [Header("맞았을 때")]
        public ParticleSystem impactEffect;

        /// <summary>맞았을 때 낼 소리입니다.</summary>
        public AudioClip[] impactClips;

        [Range(0f, 1f)]
        public float impactVolume = 0.9f;

        // --- Private Member Variables ---

        private float damage;
        private LayerMask mask = ~0;
        private Transform owner;
        private Vector3 heading;
        private float travelled;
        private bool flying;

        // --- Unity Event Functions ---

        void Update()
        {
            Advance(Time.deltaTime);
        }

        // --- Public Methods ---

        /// <summary>
        /// 한 걸음 날아갑니다. <see cref="Update"/> 가 부르고, 테스트도 직접 부릅니다.
        ///
        /// <b>왜 갈라 두는가.</b> 이 부품에서 확인해야 하는 것은 "도착하는 데 시간이
        /// 걸리는가" 하나입니다. 그것을 보려고 로봇과 포탑과 플레이 모드를 세울
        /// 이유가 없습니다.
        /// </summary>
        /// <param name="dt">지난 시간(초)</param>
        /// <returns>아직 날고 있으면 true</returns>
        public bool Advance(float dt)
        {
            if (!flying) return false;

            float step = speed * dt;

            // ⚠ <b>한 프레임을 통째로 건너뛰면 안 됩니다.</b> 초속 55 m 면 한 프레임에
            // 0.9 m 를 갑니다. 위치만 옮기고 그 자리에서 검사하면 그 0.9 m 안에 있던
            // 벽을 <b>지나쳐 버립니다.</b> 지나간 선분을 봐야 합니다.
            if (Physics.Raycast(transform.position, heading, out RaycastHit hit, step,
                                mask, QueryTriggerInteraction.Ignore))
            {
                // 쏜 자신은 건너뜁니다. 포드 앞에서 태어나므로 첫 프레임에 제 다리를
                // 맞는 일이 있습니다.
                if (owner == null || !hit.transform.IsChildOf(owner))
                {
                    Land(hit.point, hit.normal, hit.collider);
                    return false;
                }
            }

            transform.position += heading * step;
            travelled += step;

            if (travelled >= reach)
            {
                Fade();
                return false;
            }

            return true;
        }

        /// <summary>
        /// 쏩니다.
        ///
        /// <b>쏜 쪽이 값을 정합니다.</b> 피해도 사거리도 무기에 적힌 것을 그대로
        /// 받습니다 — 탄 프리팹에 따로 적어 두면 무기의 값과 <b>어긋나는 날</b>이 옵니다.
        /// </summary>
        /// <param name="from">떠나는 자리</param>
        /// <param name="direction">가는 쪽 (정규화 안 해도 됩니다)</param>
        /// <param name="damageAmount">맞았을 때 줄 피해</param>
        /// <param name="range">이 거리를 지나면 사라집니다(m)</param>
        /// <param name="hitMask">무엇에 맞을 것인가</param>
        /// <param name="firedBy">쏜 것의 뿌리. 자기 몸은 안 맞습니다</param>
        public void Launch(Vector3 from, Vector3 direction, float damageAmount,
                           float range, LayerMask hitMask, Transform firedBy)
        {
            transform.position = from;
            heading = direction.sqrMagnitude > 1e-6f ? direction.normalized : Vector3.forward;
            transform.rotation = Quaternion.LookRotation(heading);

            damage = damageAmount;
            reach = range;
            mask = hitMask;
            owner = firedBy;

            travelled = 0f;
            flying = true;
        }

        // --- Private Methods ---

        /// <summary>맞았습니다. 자국과 소리를 내고 피해를 줍니다.</summary>
        private void Land(Vector3 point, Vector3 normal, Collider what)
        {
            flying = false;

            if (impactEffect != null)
            {
                PooledParticleEffect.Spawn(impactEffect, point, Quaternion.LookRotation(normal));
            }

            if (impactClips != null && impactClips.Length > 0)
            {
                AudioClip clip = impactClips[Random.Range(0, impactClips.Length)];
                OneShotAudioPool.Play(clip, point, impactVolume);
            }

            if (damage > 0f && what != null)
            {
                IDamageable victim = what.GetComponentInParent<IDamageable>();
                if (victim != null && !victim.IsDead) victim.TakeDamage(damage);
            }

            Fade();
        }

        /// <summary>풀로 돌아갑니다. 풀에서 나온 것이 아니면 그냥 없앱니다.</summary>
        private void Fade()
        {
            flying = false;

            // ⚠ <b>편집 모드에서는 <c>Destroy</c> 가 막힙니다.</b> 테스트가 이 부품을
            // 플레이 모드 없이 직접 몰기 때문에 여기서 갈라야 합니다.
            //
            // ⚠ <b>풀도 같이 가릅니다.</b> 처음에는 <c>Destroy</c> 만 갈랐는데 그래도
            // 같은 오류가 났습니다 — <c>PrefabPool.Release</c> 가 풀에서 나오지 않은
            // 물체를 <b>스스로 Destroy</b> 하기 때문입니다. 풀은 플레이 모드의 일입니다.
            if (!Application.isPlaying)
            {
                DestroyImmediate(gameObject);
                return;
            }

            if (!PrefabPool.Release(gameObject)) Destroy(gameObject);
        }
    }
}
