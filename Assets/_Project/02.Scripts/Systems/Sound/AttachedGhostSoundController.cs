using UnityEngine;
using CarDrive.Common;
using CarDrive.Gameplay;

namespace CarDrive.Systems
{
    /// <summary>
    /// AttachedGhostController(부착형 적)의 사운드를 전담합니다.
    /// AttachedGhostController와 같은 GameObject에 추가해야 합니다.
    /// </summary>
    [RequireComponent(typeof(AttachedGhostController), typeof(AudioSource))]
    public class AttachedGhostSoundController : MonoBehaviour
    {
        // --- Public Member Variables ---

        /// <summary>공격·속삭임 루프를 재생할 AudioSource입니다.</summary>
        [Header("오디오 소스 (AudioSources)")]
        [Tooltip("공격/속삭임 루프 사운드")]
        public AudioSource loopSource;

        /// <summary>피격·스폰 등 일회성 효과음을 재생할 AudioSource입니다.</summary>
        [Tooltip("피격, 스폰 등 효과음")]
        public AudioSource effectsSource;

        /// <summary>스폰될 때 한 번 재생할 클립입니다.</summary>
        [Header("오디오 클립 (AudioClips)")]
        [Tooltip("스폰 시 사운드")]
        public AudioClip spawnSound;

        /// <summary>차량에 부착된 뒤 공격하는 동안 반복 재생할 클립입니다.</summary>
        [Tooltip("차량에 부착 후 공격(지속) 루프 사운드")]
        public AudioClip attackLoop;

        /// <summary>주기적 공격이 들어갈 때 무작위로 고를 클립 목록입니다.</summary>
        [Tooltip("주기적 공격 시 효과음")]
        public AudioClip[] attackImpactClips;

        /// <summary>피격될 때 무작위로 고를 클립 목록입니다.</summary>
        [Tooltip("피격 시 재생될 무작위 사운드")]
        public AudioClip[] takeDamageClips;

        /// <summary>죽을 때 재생할 클립입니다. 오브젝트가 파괴되므로 위치 기반으로 재생합니다.</summary>
        [Tooltip("죽을 때 사운드 (오브젝트가 파괴되어도 재생됨)")]
        public AudioClip deathClip;

        // --- Unity Event Functions ---

        /// <summary>
        /// 나타날 때마다 스폰 사운드를 재생합니다.
        ///
        /// <b>Start가 아니라 OnEnable입니다.</b> 귀신은 파괴되지 않고 풀에서 재사용되므로
        /// Start는 인스턴스당 한 번만 돕니다. 예전에는 그래서 같은 귀신이 두 번째로
        /// 나타날 때부터 소리 없이 등장했습니다.
        /// </summary>
        void OnEnable()
        {
            AudioUtility.PlayOneShot(effectsSource, spawnSound);
        }

        /// <summary>
        /// 물러날 때 루프를 확실히 끕니다. 쓰러지지 않고 회수되는 경로도 있기 때문입니다.
        /// </summary>
        void OnDisable()
        {
            AudioUtility.StopLoop(loopSource);
        }

        // --- Public Methods ---

        /// <summary>
        /// 차량에 부착되었을 때 공격 루프를 시작합니다. (AttachedGhostController가 호출)
        /// </summary>
        public void StartAttackLoop()
        {
            AudioUtility.StartLoop(loopSource, attackLoop, 1.0f, 1.0f);
        }

        /// <summary>
        /// 주기적 데미지 효과음을 재생합니다. (AttachedGhostController가 호출)
        /// </summary>
        public void PlayAttackImpact()
        {
            AudioUtility.PlayOneShotRandom(effectsSource, attackImpactClips);
        }

        /// <summary>
        /// 피격 사운드를 재생합니다. (AttachedGhostController가 호출)
        /// </summary>
        public void PlayTakeDamageSound()
        {
            AudioUtility.PlayOneShotRandom(effectsSource, takeDamageClips);
        }

        /// <summary>
        /// 죽음 사운드를 재생합니다. (AttachedGhostController가 호출)
        /// 루프를 멈춘 뒤, 오브젝트가 파괴되어도 들리도록 위치 기반으로 재생합니다.
        /// </summary>
        public void PlayDeathSound()
        {
            // 루프 사운드 중지
            AudioUtility.StopLoop(loopSource);

            // 오브젝트가 파괴될 것이므로, AudioUtility를 사용해 위치 기반으로 재생
            AudioUtility.PlayClipAtPoint(deathClip, transform.position);
        }
    }
}
