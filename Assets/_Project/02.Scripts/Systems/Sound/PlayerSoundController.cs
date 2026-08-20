using UnityEngine;
using CarDrive.Common;

namespace CarDrive.Systems
{
    /// <summary>
    /// 플레이어의 행동(앙크 공격, 상호작용) 관련 사운드를 전담합니다.
    /// PlayerAttacker, PlayerInteractor가 있는 카메라에 부착합니다.
    /// </summary>
    public class PlayerSoundController : MonoBehaviour
    {
        // --- Public Member Variables ---

        /// <summary>앙크 발사 루프 사운드를 재생할 AudioSource입니다.</summary>
        [Header("오디오 소스 (AudioSources)")]
        [Tooltip("앙크 발사 루프 사운드를 재생할 AudioSource")]
        public AudioSource ankhLoopSource;

        /// <summary>일회성 효과음(음료 마시기, 충전 등)을 재생할 AudioSource입니다.</summary>
        [Tooltip("음료 마시기, 충전 등 효과음을 재생할 AudioSource")]
        public AudioSource effectsSource;

        /// <summary>앙크 충전을 시작할 때 한 번 재생할 클립입니다.</summary>
        [Header("앙크 공격 클립 (Ankh Clips)")]
        [Tooltip("앙크 충전 시작음")]
        public AudioClip ankhChargeClip;

        /// <summary>앙크를 발사하는 동안 반복 재생할 클립입니다.</summary>
        [Tooltip("앙크 발사 중 루프")]
        public AudioClip ankhFireLoopClip;

        /// <summary>앙크 발사를 멈출 때 한 번 재생할 클립입니다.</summary>
        [Tooltip("앙크 발사 중지음")]
        public AudioClip ankhStopClip;

        /// <summary>음료를 마실 때 재생할 클립입니다.</summary>
        [Header("상호작용 클립 (Interaction Clips)")]
        [Tooltip("음료 마시는 소리")]
        public AudioClip drinkSound;

        /// <summary>상호작용에 실패했을 때(예: 연료 없음) 재생할 클립입니다.</summary>
        [Tooltip("상호작용 실패 (예: 연료 없음) 소리")]
        public AudioClip interactionFailSound;

        // --- Public Methods ---

        // --- 앙크 사운드 메서드 (PlayerAttacker가 호출) ---

        /// <summary>
        /// 앙크 충전 시작음을 한 번 재생합니다.
        /// </summary>
        public void PlayAnkhCharge()
        {
            AudioUtility.PlayOneShot(effectsSource, ankhChargeClip);
        }

        /// <summary>
        /// 앙크 발사 루프 재생을 시작합니다.
        /// </summary>
        public void StartAnkhFireLoop()
        {
            AudioUtility.StartLoop(ankhLoopSource, ankhFireLoopClip, 1.0f, 1.0f);
        }

        /// <summary>
        /// 앙크 발사 루프를 멈추고 중지음을 한 번 재생합니다.
        /// </summary>
        public void StopAnkhFireLoop()
        {
            AudioUtility.StopLoop(ankhLoopSource);
            AudioUtility.PlayOneShot(effectsSource, ankhStopClip);
        }

        // --- 상호작용 사운드 메서드 (PlayerInteractor가 호출) ---

        /// <summary>
        /// 음료 마시는 소리를 한 번 재생합니다.
        /// </summary>
        public void PlayDrinkSound()
        {
            AudioUtility.PlayOneShot(effectsSource, drinkSound);
        }

        /// <summary>
        /// 상호작용 실패음을 한 번 재생합니다.
        /// </summary>
        public void PlayInteractionFailSound()
        {
            AudioUtility.PlayOneShot(effectsSource, interactionFailSound);
        }
    }
}
