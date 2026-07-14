using UnityEngine;

/// <summary>
/// 플레이어의 행동(앙크 공격, 상호작용) 관련 사운드를 전담합니다.
/// PlayerAttacker, PlayerInteractor가 있는 카메라에 부착합니다.
/// </summary>
public class PlayerSoundController : MonoBehaviour
{
    [Header("오디오 소스 (AudioSources)")]
    [Tooltip("앙크 발사 루프 사운드를 재생할 AudioSource")]
    public AudioSource ankhLoopSource;
    [Tooltip("음료 마시기, 충전 등 효과음을 재생할 AudioSource")]
    public AudioSource effectsSource;

    [Header("앙크 공격 클립 (Ankh Clips)")]
    [Tooltip("앙크 충전 시작음")]
    public AudioClip ankhChargeClip;
    [Tooltip("앙크 발사 중 루프")]
    public AudioClip ankhFireLoopClip;
    [Tooltip("앙크 발사 중지음")]
    public AudioClip ankhStopClip;

    [Header("상호작용 클립 (Interaction Clips)")]
    [Tooltip("음료 마시는 소리")]
    public AudioClip drinkSound;
    [Tooltip("상호작용 실패 (예: 연료 없음) 소리")]
    public AudioClip interactionFailSound;

    // --- 앙크 사운드 메서드 (PlayerAttacker가 호출) ---

    public void PlayAnkhCharge()
    {
        AudioUtility.PlayOneShot(effectsSource, ankhChargeClip);
    }

    public void StartAnkhFireLoop()
    {
        AudioUtility.StartLoop(ankhLoopSource, ankhFireLoopClip, 1.0f, 1.0f);
    }

    public void StopAnkhFireLoop()
    {
        AudioUtility.StopLoop(ankhLoopSource);
        AudioUtility.PlayOneShot(effectsSource, ankhStopClip);
    }

    // --- 상호작용 사운드 메서드 (PlayerInteractor가 호출) ---

    public void PlayDrinkSound()
    {
        AudioUtility.PlayOneShot(effectsSource, drinkSound);
    }

    public void PlayInteractionFailSound()
    {
        AudioUtility.PlayOneShot(effectsSource, interactionFailSound);
    }
}
