using UnityEngine;
using CarDrive.Common;

namespace CarDrive.Gameplay
{
    /// <summary>
    /// 장애물 같은 환경 오브젝트의 소리를 전담합니다.
    ///
    /// <b>언제 울릴지는 정하지 않습니다.</b> <see cref="ObstacleController"/>가 충돌을 판정하고
    /// 세기를 계산해 넘겨주면, 여기서는 그 세기대로 클립을 고르고 재생하기만 합니다.
    /// 소리를 내는 일과 언제 낼지 정하는 일을 갈라 두면, 소리를 바꾸려고
    /// 충돌 판정 코드를 건드릴 일이 없습니다.
    ///
    /// <see cref="ObstacleController"/>·<see cref="AudioSource"/>와 같은 GameObject에 두어야 합니다.
    /// </summary>
    [RequireComponent(typeof(ObstacleController), typeof(AudioSource))]
    public class EnvironmentSoundController : MonoBehaviour
    {
        // --- Public Member Variables ---

        /// <summary>충돌 효과음을 재생할 AudioSource입니다.</summary>
        [Header("오디오 소스 (AudioSource)")]
        [Tooltip("충돌 효과음을 재생할 AudioSource")]
        public AudioSource effectsSource;

        /// <summary>
        /// 충돌할 때 재생할 소리 후보들입니다. 이 중 하나가 무작위로 뽑힙니다.
        ///
        /// 하나만 넣어도 동작하지만, 같은 소리가 반복되면 금세 기계적으로 들립니다.
        /// 여러 개를 넣어 두면 부딪힐 때마다 조금씩 다르게 들립니다.
        /// </summary>
        [Header("오디오 클립 (AudioClips)")]
        [Tooltip("충돌 시 재생될 무작위 사운드")]
        public AudioClip[] hitClips;

        // --- Public Methods ---

        /// <summary>
        /// 충돌음을 한 번 재생합니다. <see cref="ObstacleController"/>가 부릅니다.
        ///
        /// 볼륨은 0.2 아래로 내려가지 않습니다. 살짝 스친 충돌이라도
        /// 아예 안 들리면 부딪혔다는 사실 자체가 전달되지 않기 때문입니다.
        /// </summary>
        /// <param name="impactStrength">충돌 강도 (0.0 ~ 1.0), 볼륨 조절에 사용</param>
        public void PlayHitSound(float impactStrength)
        {
            float volume = Mathf.Clamp(impactStrength, 0.2f, 1.0f);
            AudioUtility.PlayOneShotRandom(effectsSource, hitClips, volume);
        }
    }
}
