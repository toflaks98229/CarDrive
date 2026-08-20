using UnityEngine;
using CarDrive.Common;
using CarDrive.Gameplay;

namespace CarDrive.Systems
{
    /// <summary>
    /// 차량의 모든 사운드(엔진, 충돌, 시동)를 전담하는 컨트롤러입니다.
    /// CarController와 같은 GameObject에 추가해야 합니다.
    /// </summary>
    [RequireComponent(typeof(CarController))]
    public class CarSoundController : MonoBehaviour
    {
        // --- Public Member Variables ---

        /// <summary>엔진 루프 사운드를 재생할 AudioSource입니다. RPM에 따라 피치가 조절됩니다.</summary>
        [Header("오디오 소스 (AudioSources)")]
        [Tooltip("엔진 루프 사운드를 재생할 AudioSource")]
        public AudioSource engineSource;

        /// <summary>시동·충돌 등 일회성 효과음을 재생할 AudioSource입니다.</summary>
        [Tooltip("시동, 충돌 등 효과음을 재생할 AudioSource")]
        public AudioSource effectsSource;

        /// <summary>시동을 걸 때 한 번 재생할 클립입니다.</summary>
        [Header("오디오 클립 (AudioClips)")]
        [Tooltip("시동 거는 소리")]
        public AudioClip engineStartClip;

        /// <summary>엔진이 켜져 있는 동안 반복 재생할 클립입니다.</summary>
        [Tooltip("엔진 작동 루프 (RPM에 따라 피치 조절됨)")]
        public AudioClip engineLoopClip;

        /// <summary>시동을 끌 때 한 번 재생할 클립입니다.</summary>
        [Tooltip("시동 끄는 소리")]
        public AudioClip engineStopClip;

        /// <summary>충돌할 때 무작위로 고를 클립 목록입니다.</summary>
        [Tooltip("적 또는 장애물과 충돌 시 재생될 무작위 소리")]
        public AudioClip[] collisionClips;

        /// <summary>엔진 피치가 최저가 되는 RPM입니다.</summary>
        [Header("엔진 사운드 설정")]
        [Tooltip("엔진 소리가 나지 않는 최소 RPM")]
        public float minRpm = 800f;

        /// <summary>엔진 피치가 최대가 되는 RPM입니다.</summary>
        [Tooltip("엔진 피치가 최대가 되는 RPM")]
        public float maxRpm = 6000f;

        /// <summary>최소 RPM일 때의 엔진 피치입니다.</summary>
        [Tooltip("최소 RPM일 때의 엔진 피치")]
        public float minPitch = 0.8f;

        /// <summary>최대 RPM일 때의 엔진 피치입니다.</summary>
        [Tooltip("최대 RPM일 때의 엔진 피치")]
        public float maxPitch = 2.5f;

        /// <summary>스로틀에서 발을 뗐을 때의 엔진 볼륨입니다.</summary>
        [Tooltip("스로틀에서 발을 뗐을 때의 엔진 볼륨")]
        [Range(0f, 1f)]
        public float minVolume = 0.5f;

        /// <summary>스로틀을 끝까지 밟았을 때의 엔진 볼륨입니다.</summary>
        [Tooltip("스로틀을 끝까지 밟았을 때의 엔진 볼륨")]
        [Range(0f, 1f)]
        public float maxVolume = 1.0f;

        // --- Private Member Variables ---

        /// <summary>RPM과 시동 상태를 읽어 올 차량 컨트롤러입니다. 같은 GameObject에서 가져옵니다.</summary>
        private CarController carController;

        // --- Unity Event Functions ---

        /// <summary>
        /// 차량 컨트롤러 참조를 가져오고 엔진 루프 클립을 AudioSource에 물려 둡니다.
        /// </summary>
        void Start()
        {
            carController = GetComponent<CarController>();
            if (engineSource != null)
            {
                engineSource.clip = engineLoopClip;
            }
        }

        /// <summary>
        /// 매 프레임 엔진 사운드의 재생 여부와 피치·볼륨을 갱신합니다.
        /// </summary>
        void Update()
        {
            UpdateEngineSound();
        }

        // --- Public Methods ---

        /// <summary>
        /// 시동 사운드를 재생합니다. (CarController에서 호출)
        /// </summary>
        public void PlayEngineStart()
        {
            AudioUtility.PlayOneShot(effectsSource, engineStartClip);
        }

        /// <summary>
        /// 시동 끄는 사운드를 재생합니다. (CarController에서 호출)
        /// </summary>
        public void PlayEngineStop()
        {
            AudioUtility.PlayOneShot(effectsSource, engineStopClip);
        }

        /// <summary>
        /// 충돌 사운드를 재생합니다. (CarCollisionHandler에서 호출)
        /// </summary>
        /// <param name="impactStrength">충돌 강도 (0.0 ~ 1.0), 볼륨 조절에 사용</param>
        public void PlayCollisionSound(float impactStrength)
        {
            float volume = Mathf.Clamp(impactStrength, 0.3f, 1.0f);
            AudioUtility.PlayOneShotRandom(effectsSource, collisionClips, volume);
        }

        // --- Private Methods ---

        /// <summary>
        /// CarController의 현재 RPM을 기반으로 엔진 사운드를 조절합니다.
        /// 시동이 꺼져 있으면 루프를 멈추고, 켜져 있으면 RPM 비율로 피치를 올립니다.
        /// </summary>
        private void UpdateEngineSound()
        {
            if (engineSource == null || !carController.IsEngineOn)
            {
                if (engineSource != null && engineSource.isPlaying)
                {
                    engineSource.Stop();
                }
                return;
            }

            if (!engineSource.isPlaying)
            {
                engineSource.Play();
            }

            // RPM 비율 계산 (0.0 ~ 1.0)
            float rpmRatio = Mathf.InverseLerp(minRpm, maxRpm, carController.CurrentRpm);

            // 피치 조절
            engineSource.pitch = Mathf.Lerp(minPitch, maxPitch, rpmRatio);

            // 볼륨 조절 (스로틀 입력에 따라)
            // 전역 Input이 아니라 이 차량의 CarController에서 읽습니다. 그래야
            //  (1) 차가 여러 대여도 각자 자기 스로틀에만 반응하고,
            //  (2) GameInputGate로 입력이 막히면 CarInput이 0을 내주므로 볼륨도 함께 잦아듭니다.
            float throttleInput = Mathf.Abs(carController.ThrottleInput);
            engineSource.volume = Mathf.Lerp(minVolume, maxVolume, throttleInput);
        }
    }
}
