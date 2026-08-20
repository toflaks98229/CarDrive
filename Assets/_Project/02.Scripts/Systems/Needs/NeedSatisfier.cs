using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using CarDrive.Common;

namespace CarDrive.Systems
{
    /// <summary>
    /// 니즈를 해소하는 상호작용 대상에 붙이는 컴포넌트입니다.
    /// 음식·물·침대·화장실·샤워·라디오를 전부 이 하나로 표현합니다.
    ///
    /// 예) 침대  : 피로 +1.0 해소, 허기 -0.15 / 갈증 -0.2 (자는 동안 배가 고파짐), 480분 경과
    ///     화장실: 배뇨 +1.0 해소, 청결 -0.02
    ///     샤워  : 청결 +1.0 해소, 스트레스 +0.15 해소, 20분 경과
    /// </summary>
    public class NeedSatisfier : MonoBehaviour, IInteractable
    {
        // --- Public Member Variables ---

        /// <summary>상호작용 안내에 표시할 동작 이름입니다. (예: 침대에서 자기)</summary>
        [Header("표시")]
        [Tooltip("상호작용 안내에 쓸 이름 (예: 침대에서 자기)")]
        public string promptLabel = "사용하기";

        /// <summary>
        /// 사용할 때 적용할 니즈 효과 목록입니다.
        /// relief가 양수면 해소, 음수면 악화이며 1.0이 게이지 전체에 해당합니다.
        /// </summary>
        [Header("니즈 효과")]
        [Tooltip("relief가 양수면 해소, 음수면 악화입니다. 1.0 = 게이지 전체")]
        public List<NeedEffect> effects = new List<NeedEffect>();

        /// <summary>사용할 때 흐르는 게임 시간(분)입니다. 수면처럼 시간을 크게 건너뛸 때 씁니다.</summary>
        [Header("시간")]
        [Tooltip("사용할 때 흐르는 게임 시간(분). 수면처럼 시간을 크게 건너뛸 때 씁니다.")]
        public float gameMinutesElapsed = 0f;

        /// <summary>시간을 먼저 흘려보낸 뒤 니즈 효과를 적용할지 여부입니다. (수면은 켜는 편이 자연스럽습니다)</summary>
        [Tooltip("체크하면 시간이 먼저 흐른 뒤 니즈 효과가 적용됩니다. (수면은 켜는 편이 자연스럽습니다)")]
        public bool advanceTimeBeforeEffects = true;

        /// <summary>
        /// 날씨의 "수면 회복 배율"을 해소량에 곱할지 여부입니다. 침대에만 켜세요.
        /// 빗소리를 들으며 자면 더 잘 쉬고(비 1.25), 폭우에는 설칩니다(0.85).
        /// </summary>
        [Header("날씨 연동")]
        [Tooltip("체크하면 날씨의 '수면 회복 배율'이 해소량에 곱해집니다. 침대에만 켜세요. " +
                 "빗소리를 들으며 자면 더 잘 쉬고(비 1.25), 폭우에는 설칩니다(0.85).")]
        public bool affectedBySleepQuality = false;

        /// <summary>남은 사용 가능 횟수입니다. -1이면 무제한입니다.</summary>
        [Header("사용 횟수")]
        [Tooltip("사용 가능 횟수. -1이면 무제한입니다.")]
        public int remainingUses = -1;

        /// <summary>횟수를 다 썼을 때 이 GameObject를 비활성화할지 여부입니다.</summary>
        [Tooltip("횟수를 다 쓰면 이 GameObject를 비활성화합니다.")]
        public bool disableWhenDepleted = false;

        /// <summary>사용에 성공했을 때 한 번 호출됩니다.</summary>
        [Header("이벤트")]
        [Tooltip("사용에 성공했을 때")]
        public UnityEvent onUsed;

        /// <summary>횟수가 없어 사용에 실패했을 때 한 번 호출됩니다.</summary>
        [Tooltip("횟수가 없어 사용에 실패했을 때")]
        public UnityEvent onDepleted;

        // --- Public Methods ---

        // --- IInteractable ---
        // 조준점에 걸렸을 때 PlayerInteractor가 호출합니다.

        /// <summary>지금 사용할 수 있는 상태인지 확인합니다.</summary>
        /// <returns>남은 횟수가 있으면 true를 반환합니다.</returns>
        public bool CanInteract()
        {
            return CanUse();
        }

        /// <summary>화면에 표시할 동작 이름입니다.</summary>
        /// <returns>인스펙터에서 설정한 안내 문구</returns>
        public string GetInteractionLabel()
        {
            return promptLabel;
        }

        /// <summary>조준한 상태에서 상호작용 키를 눌렀을 때 실행됩니다.</summary>
        public void Interact()
        {
            TryUse(NeedsSystem.Instance);
        }

        /// <summary>
        /// 지금 사용할 수 있는 상태인지 확인합니다.
        /// </summary>
        /// <returns>남은 횟수가 0이 아니면(무제한 포함) true를 반환합니다.</returns>
        public bool CanUse()
        {
            return remainingUses != 0;
        }

        /// <summary>
        /// 니즈 효과를 적용합니다. 성공하면 true를 돌려줍니다.
        /// </summary>
        /// <param name="needs">효과를 적용할 니즈 시스템. null이면 경고만 남기고 실패합니다.</param>
        /// <returns>효과를 적용했으면 true, 횟수가 없거나 니즈 시스템이 없으면 false를 반환합니다.</returns>
        public bool TryUse(NeedsSystem needs)
        {
            if (!CanUse())
            {
                Debug.Log(gameObject.name + ": 더 이상 사용할 수 없습니다.");
                if (onDepleted != null) onDepleted.Invoke();
                return false;
            }

            if (needs == null)
            {
                Debug.LogWarning("NeedSatisfier: NeedsSystem이 없어 효과를 적용할 수 없습니다.", this);
                return false;
            }

            // 잠자리의 질은 '잠들기 시작하는 시점'의 날씨로 정해집니다.
            // 시간을 먼저 흘려보내면 날씨가 바뀌어 버리므로 여기서 미리 읽어 둡니다.
            float reliefScale = affectedBySleepQuality ? WeatherSystem.GetSleepQuality() : 1f;

            if (advanceTimeBeforeEffects)
            {
                needs.AdvanceGameMinutes(gameMinutesElapsed);
                needs.ApplyEffects(effects, reliefScale);
            }
            else
            {
                needs.ApplyEffects(effects, reliefScale);
                needs.AdvanceGameMinutes(gameMinutesElapsed);
            }

            if (remainingUses > 0)
            {
                remainingUses--;
                if (remainingUses == 0 && disableWhenDepleted)
                {
                    gameObject.SetActive(false);
                }
            }

            Debug.Log(gameObject.name + " 사용: " + promptLabel);
            if (onUsed != null) onUsed.Invoke();
            return true;
        }

        // --- Private Methods ---

        // --- Context Menu Presets ---
        // 인스펙터에서 우클릭해 바로 대표적인 설정을 채워 넣을 수 있게 해 둡니다.

        /// <summary>
        /// 식사 설정을 채워 넣습니다. 허기를 크게 덜지만 짠 음식이라 갈증이 조금 오릅니다.
        /// </summary>
        [ContextMenu("프리셋: 식사")]
        private void PresetMeal()
        {
            promptLabel = "식사하기";
            gameMinutesElapsed = 20f;
            effects = new List<NeedEffect>
            {
                new NeedEffect { type = NeedType.Hunger, relief = 0.8f },
                new NeedEffect { type = NeedType.Thirst, relief = -0.1f }   // 짠 음식은 갈증을 부릅니다
            };
        }

        /// <summary>
        /// 음료 설정을 채워 넣습니다. 갈증을 덜지만 그만큼 배뇨가 차오릅니다.
        /// </summary>
        [ContextMenu("프리셋: 음료")]
        private void PresetDrink()
        {
            promptLabel = "마시기";
            gameMinutesElapsed = 2f;
            effects = new List<NeedEffect>
            {
                new NeedEffect { type = NeedType.Thirst, relief = 0.5f },
                new NeedEffect { type = NeedType.Urine, relief = -0.25f }   // 마시면 배뇨가 차오릅니다
            };
        }

        /// <summary>
        /// 수면 설정을 채워 넣습니다. 8시간을 건너뛰며 날씨의 수면 회복 배율을 함께 받습니다.
        /// </summary>
        [ContextMenu("프리셋: 수면")]
        private void PresetSleep()
        {
            promptLabel = "잠자기";
            gameMinutesElapsed = 480f;
            advanceTimeBeforeEffects = true;
            affectedBySleepQuality = true;
            effects = new List<NeedEffect>
            {
                new NeedEffect { type = NeedType.Fatigue, relief = 1.5f },
                new NeedEffect { type = NeedType.Stress, relief = 0.3f }
            };
        }

        /// <summary>
        /// 화장실 설정을 채워 넣습니다. 배뇨를 모두 해소하는 대신 청결이 조금 나빠집니다.
        /// </summary>
        [ContextMenu("프리셋: 화장실")]
        private void PresetToilet()
        {
            promptLabel = "볼일 보기";
            gameMinutesElapsed = 5f;
            effects = new List<NeedEffect>
            {
                new NeedEffect { type = NeedType.Urine, relief = 1.5f },
                new NeedEffect { type = NeedType.Hygiene, relief = -0.03f }
            };
        }

        /// <summary>
        /// 샤워 설정을 채워 넣습니다. 청결을 모두 해소하고 스트레스도 조금 덜어 줍니다.
        /// </summary>
        [ContextMenu("프리셋: 샤워")]
        private void PresetShower()
        {
            promptLabel = "씻기";
            gameMinutesElapsed = 20f;
            effects = new List<NeedEffect>
            {
                new NeedEffect { type = NeedType.Hygiene, relief = 1.5f },
                new NeedEffect { type = NeedType.Stress, relief = 0.15f }
            };
        }

        /// <summary>
        /// 휴식 설정을 채워 넣습니다. 한 시간을 들여 스트레스를 주로 덜어 냅니다.
        /// </summary>
        [ContextMenu("프리셋: 휴식")]
        private void PresetRest()
        {
            promptLabel = "쉬기";
            gameMinutesElapsed = 60f;
            effects = new List<NeedEffect>
            {
                new NeedEffect { type = NeedType.Stress, relief = 0.4f },
                new NeedEffect { type = NeedType.Fatigue, relief = 0.1f }
            };
        }
    }
}
