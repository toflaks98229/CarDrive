using UnityEngine;
using CarDrive.Systems;

namespace CarDrive.Common
{
    /// <summary>
    /// 오디오 재생을 위한 정적(static) 헬퍼 클래스입니다.
    /// 파괴되는 오브젝트의 사운드(예: 죽음 효과음)를 재생하는 데 유용합니다.
    /// </summary>
    public static class AudioUtility
    {
        /// <summary>
        /// 지정된 위치에서 클립을 한 번 재생합니다.
        /// 소리를 낸 오브젝트가 곧바로 파괴되어도 끝까지 들립니다.
        ///
        /// 예전에는 소리 한 번마다 GameObject를 만들고 클립 길이만큼 뒤에 파괴했습니다.
        /// 앙크 피격음이 0.25초, 귀신 공격음이 1초 간격으로 이 경로를 타므로
        /// 전투 내내 할당과 파괴가 이어졌습니다. 이제는 AudioSource를 돌려 씁니다.
        /// </summary>
        /// <param name="clip">재생할 AudioClip</param>
        /// <param name="position">재생할 월드 좌표</param>
        /// <param name="volume">볼륨 (0.0f ~ 1.0f)</param>
        public static void PlayClipAtPoint(AudioClip clip, Vector3 position, float volume = 1.0f)
        {
            OneShotAudioPool.Play(clip, position, volume);
        }

        /// <summary>
        /// AudioSource가 null이거나 클립이 null인지 확인하고 안전하게 클립을 한 번 재생합니다.
        /// </summary>
        public static void PlayOneShot(AudioSource source, AudioClip clip, float volumeScale = 1.0f)
        {
            if (source != null && clip != null)
            {
                source.PlayOneShot(clip, volumeScale);
            }
        }

        /// <summary>
        /// 클립 배열에서 무작위 클립을 선택하여 안전하게 한 번 재생합니다.
        /// </summary>
        public static void PlayOneShotRandom(AudioSource source, AudioClip[] clips, float volumeScale = 1.0f)
        {
            if (source != null && clips != null && clips.Length > 0)
            {
                AudioClip clip = clips[Random.Range(0, clips.Length)];
                source.PlayOneShot(clip, volumeScale);
            }
        }

        /// <summary>
        /// 루프 사운드를 안전하게 시작하거나 교체합니다.
        /// </summary>
        public static void StartLoop(AudioSource source, AudioClip clip, float volume = 1.0f, float pitch = 1.0f)
        {
            if (source == null || clip == null) return;

            if (source.clip != clip)
            {
                source.clip = clip;
                source.Play();
            }

            source.volume = volume;
            source.pitch = pitch;
            source.loop = true;
        }

        /// <summary>
        /// 루프 사운드를 안전하게 중지합니다.
        /// </summary>
        public static void StopLoop(AudioSource source)
        {
            if (source != null)
            {
                source.clip = null;
                source.Stop();
            }
        }
    }
}
