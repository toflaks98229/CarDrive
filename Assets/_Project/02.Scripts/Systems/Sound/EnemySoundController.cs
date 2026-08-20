using UnityEngine;
using CarDrive.Common;
using CarDrive.Gameplay;

namespace CarDrive.Systems
{
    /// <summary>
    /// EnemyController(추적형 적)의 사운드를 전담합니다.
    /// EnemyController와 같은 GameObject에 추가해야 합니다.
    /// </summary>
    [RequireComponent(typeof(EnemyController), typeof(AudioSource))]
    public class EnemySoundController : MonoBehaviour
    {
        // --- Public Member Variables ---

        /// <summary>추적하는 동안 반복 재생할 루프용 AudioSource입니다.</summary>
        [Header("오디오 소스 (AudioSources)")]
        [Tooltip("추적 중 루프 사운드를 재생할 AudioSource")]
        public AudioSource loopSource;

        /// <summary>피격 등 일회성 효과음을 재생할 AudioSource입니다.</summary>
        [Tooltip("피격 등 효과음을 재생할 AudioSource")]
        public AudioSource effectsSource;

        /// <summary>스폰(활성화)될 때 한 번 재생할 클립입니다.</summary>
        [Header("오디오 클립 (AudioClips)")]
        [Tooltip("스폰(활성화) 시 사운드")]
        public AudioClip spawnSound;

        /// <summary>추적하는 동안 반복 재생할 클립입니다.</summary>
        [Tooltip("추적 중 루프 사운드")]
        public AudioClip chaseLoop;

        /// <summary>피격될 때 무작위로 고를 클립 목록입니다.</summary>
        [Tooltip("피격 시 재생될 무작위 사운드")]
        public AudioClip[] takeDamageClips;

        /// <summary>죽을 때 재생할 클립입니다. 오브젝트가 파괴되므로 위치 기반으로 재생합니다.</summary>
        [Tooltip("죽을 때 사운드 (오브젝트가 파괴되어도 재생됨)")]
        public AudioClip deathClip;

        // --- Unity Event Functions ---

        /// <summary>
        /// 나타날 때마다 스폰 사운드를 재생하고 추적 루프를 시작합니다.
        ///
        /// <b>Start가 아니라 OnEnable입니다.</b> 적이 풀에서 재사용되면 Start는
        /// 인스턴스당 한 번만 돌아, 두 번째 등장부터 소리가 나지 않습니다.
        /// </summary>
        void OnEnable()
        {
            AudioUtility.PlayOneShot(effectsSource, spawnSound);
            AudioUtility.StartLoop(loopSource, chaseLoop, 1.0f, 1.0f);
        }

        /// <summary>
        /// 물러날 때 추적 루프를 끕니다. 켜 둔 채 회수되면 다음에 겹쳐 재생됩니다.
        /// </summary>
        void OnDisable()
        {
            AudioUtility.StopLoop(loopSource);
        }

        // --- Public Methods ---

        /// <summary>
        /// 피격 사운드를 재생합니다. (EnemyController가 호출)
        /// </summary>
        public void PlayTakeDamageSound()
        {
            AudioUtility.PlayOneShotRandom(effectsSource, takeDamageClips);
        }

        /// <summary>
        /// 죽음 사운드를 재생합니다. (EnemyController가 호출)
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
