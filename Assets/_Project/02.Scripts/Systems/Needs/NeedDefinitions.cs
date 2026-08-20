using System.Collections.Generic;
using UnityEngine;
using CarDrive.Common;

namespace CarDrive.Systems
{
    /// <summary>
    /// 플레이어가 관리해야 하는 6종 니즈의 종류입니다.
    /// 모든 니즈는 0에서 시작해 1로 차오르며, 값이 클수록 나쁜 상태입니다.
    /// (청결의 경우 값이 클수록 '더러움'을 뜻하며, 표시만 반전됩니다.)
    /// </summary>
    public enum NeedType
    {
        Hunger,     // 허기
        Thirst,     // 갈증
        Fatigue,    // 피로
        Stress,     // 스트레스
        Urine,      // 배뇨
        Hygiene     // 청결(= 더러움 누적치)
    }

    /// <summary>
    /// 니즈가 한계(overflowLimit)를 넘었을 때 벌어지는 일의 종류입니다.
    /// </summary>
    public enum NeedConsequence
    {
        /// <summary>한계 초과 시 체력이 지속적으로 깎여 결국 사망합니다.</summary>
        Lethal,
        /// <summary>한계 초과 시 기절합니다. (피로)</summary>
        Blackout,
        /// <summary>직접적인 피해는 없고 다른 니즈를 악화시키기만 합니다. (청결)</summary>
        Nuisance
    }

    /// <summary>
    /// 니즈 한 종류의 설정값입니다.
    /// </summary>
    [System.Serializable]
    public class NeedSetting : IDefinition<NeedType>
    {
        [Tooltip("어떤 니즈에 대한 설정인지")]
        public NeedType type;

        /// <summary>표가 이 설정을 찾는 열쇠입니다.</summary>
        public NeedType Key { get { return type; } }

        /// <summary>빠진 항목을 메웠다고 알릴 때 쓸 이름입니다.</summary>
        public string DisplayName { get { return displayName; } }

        [Tooltip("UI에 표시할 이름")]
        public string displayName = "";

        [Tooltip("게임 시간 1분마다 차오르는 양 (1.0 = 가득 참)")]
        public float fillPerGameMinute = 0.001f;

        [Tooltip("가득 찬(1.0) 뒤에도 견딜 수 있는 배수. 마이 썸머 카와 동일하게 1.5를 기본값으로 씁니다.")]
        public float overflowLimit = 1.5f;

        [Tooltip("이 값을 넘으면 경고 상태가 됩니다.")]
        public float warnThreshold = 0.75f;

        [Tooltip("한계를 넘었을 때 벌어지는 일")]
        public NeedConsequence consequence = NeedConsequence.Lethal;

        [Tooltip("Lethal일 때, 한계 초과 상태에서 초당 깎이는 체력")]
        public float criticalHealthDrainPerSecond = 2f;

        [Tooltip("체크하면 UI에서 반대로 표시합니다. (청결: 값이 0일 때 게이지가 가득)")]
        public bool invertDisplay = false;

        [Tooltip("UI 게이지 색상")]
        public Color barColor = Color.white;
    }

    /// <summary>
    /// 한 니즈가 일정 수준을 넘었을 때 다른 니즈를 더 빨리 차오르게 하는 연쇄 규칙입니다.
    /// 마이 썸머 카에서 갈증이 다른 활동에 의해 가속되는 것과 같은 구조입니다.
    /// </summary>
    [System.Serializable]
    public class NeedCoupling
    {
        [Tooltip("원인이 되는 니즈")]
        public NeedType source;

        [Tooltip("원인 니즈가 이 값을 넘어야 발동합니다.")]
        public float sourceThreshold = 0.6f;

        [Tooltip("영향을 받는 니즈")]
        public NeedType target;

        [Tooltip("발동 시 대상 니즈에 추가로 더해지는 게임 시간 1분당 증가량")]
        public float extraFillPerGameMinute = 0.001f;
    }

    /// <summary>
    /// 상호작용 한 번이 니즈에 주는 변화량입니다.
    /// </summary>
    [System.Serializable]
    public class NeedEffect
    {
        [Tooltip("영향을 줄 니즈")]
        public NeedType type;

        [Tooltip("변화량. 양수면 해소(감소), 음수면 악화(증가)입니다. 1.0 = 게이지 전체")]
        public float relief = 0.5f;
    }

    /// <summary>
    /// 니즈 한 종류의 실행 중 상태입니다. 세이브 대상이므로 직렬화 가능하게 두었습니다.
    /// </summary>
    [System.Serializable]
    public class NeedState : IDefinitionState<NeedType, NeedState>
    {
        public NeedType type;

        [Tooltip("현재 수치. 0 ~ overflowLimit 범위입니다.")]
        public float value;

        [Tooltip("경고 임계를 넘은 상태인지 (이벤트 중복 발생 방지용)")]
        public bool isWarning;

        [Tooltip("한계를 넘은 상태인지 (이벤트 중복 발생 방지용)")]
        public bool isCritical;

        /// <summary>표가 이 상태를 찾는 열쇠입니다.</summary>
        public NeedType Key { get { return type; } }

        /// <summary>세이브에 담을 사본을 만듭니다.</summary>
        /// <returns>값이 같은 새 인스턴스</returns>
        public NeedState Clone()
        {
            return new NeedState
            {
                type = type,
                value = value,
                isWarning = isWarning,
                isCritical = isCritical
            };
        }

        /// <summary>
        /// 불러온 값을 받아 옵니다. 종류(<see cref="type"/>)는 이미 제자리이므로 옮기지 않습니다.
        /// </summary>
        /// <param name="other">값을 가져올 상태</param>
        public void CopyFrom(NeedState other)
        {
            if (other == null) return;

            value = other.value;
            isWarning = other.isWarning;
            isCritical = other.isCritical;
        }
    }

    /// <summary>
    /// 프로파일 에셋을 만들지 않아도 바로 동작하도록 기본 설정값을 제공합니다.
    /// 수치를 조정하고 싶으면 NeedsProfile 에셋을 만들어 NeedsSystem에 연결하세요.
    /// </summary>
    public static class NeedDefaults
    {
        /// <summary>
        /// 6종 니즈의 기본 설정을 생성합니다.
        /// 증가 속도는 "게임 시간 몇 시간 만에 가득 차는가"를 기준으로 잡았습니다.
        /// </summary>
        public static List<NeedSetting> CreateSettings()
        {
            return new List<NeedSetting>
            {
                // 약 15시간에 가득 참
                new NeedSetting {
                    type = NeedType.Hunger, displayName = "허기",
                    fillPerGameMinute = 0.0011f, warnThreshold = 0.75f,
                    consequence = NeedConsequence.Lethal, criticalHealthDrainPerSecond = 2f,
                    barColor = new Color(0.85f, 0.55f, 0.20f)
                },
                // 약 10시간에 가득 참 (허기보다 빠름)
                new NeedSetting {
                    type = NeedType.Thirst, displayName = "갈증",
                    fillPerGameMinute = 0.0017f, warnThreshold = 0.75f,
                    consequence = NeedConsequence.Lethal, criticalHealthDrainPerSecond = 3f,
                    barColor = new Color(0.30f, 0.65f, 0.90f)
                },
                // 약 18시간에 가득 참
                new NeedSetting {
                    type = NeedType.Fatigue, displayName = "피로",
                    fillPerGameMinute = 0.0009f, warnThreshold = 0.70f,
                    consequence = NeedConsequence.Blackout, criticalHealthDrainPerSecond = 0f,
                    barColor = new Color(0.60f, 0.50f, 0.80f)
                },
                // 기본 증가는 느리고, 대부분 사건(충돌·귀신·다른 니즈)으로 오릅니다.
                new NeedSetting {
                    type = NeedType.Stress, displayName = "스트레스",
                    fillPerGameMinute = 0.0006f, warnThreshold = 0.70f,
                    consequence = NeedConsequence.Lethal, criticalHealthDrainPerSecond = 1.5f,
                    barColor = new Color(0.85f, 0.30f, 0.25f)
                },
                // 기본 증가는 매우 느리고, 주로 음료를 마셨을 때 오릅니다.
                new NeedSetting {
                    type = NeedType.Urine, displayName = "배뇨",
                    fillPerGameMinute = 0.0005f, warnThreshold = 0.80f,
                    consequence = NeedConsequence.Lethal, criticalHealthDrainPerSecond = 1f,
                    barColor = new Color(0.90f, 0.85f, 0.35f)
                },
                // 약 이틀에 가득 참. 직접 피해는 없지만 스트레스를 밀어 올립니다.
                new NeedSetting {
                    type = NeedType.Hygiene, displayName = "청결",
                    fillPerGameMinute = 0.00035f, warnThreshold = 0.70f,
                    consequence = NeedConsequence.Nuisance, criticalHealthDrainPerSecond = 0f,
                    invertDisplay = true,
                    barColor = new Color(0.55f, 0.75f, 0.55f)
                }
            };
        }

        /// <summary>
        /// 니즈 간 연쇄 규칙의 기본값입니다.
        /// 하나가 나빠지면 다른 것도 나빠지는 악순환을 만드는 것이 목적입니다.
        /// </summary>
        public static List<NeedCoupling> CreateCouplings()
        {
            return new List<NeedCoupling>
            {
                // 스트레스가 높으면 갈증과 피로가 빨리 온다
                new NeedCoupling { source = NeedType.Stress,  sourceThreshold = 0.60f, target = NeedType.Thirst,  extraFillPerGameMinute = 0.0008f },
                new NeedCoupling { source = NeedType.Stress,  sourceThreshold = 0.60f, target = NeedType.Fatigue, extraFillPerGameMinute = 0.0006f },

                // 나머지 니즈가 한계에 가까우면 스트레스가 오른다
                new NeedCoupling { source = NeedType.Hunger,  sourceThreshold = 0.80f, target = NeedType.Stress,  extraFillPerGameMinute = 0.0010f },
                new NeedCoupling { source = NeedType.Thirst,  sourceThreshold = 0.80f, target = NeedType.Stress,  extraFillPerGameMinute = 0.0012f },
                new NeedCoupling { source = NeedType.Fatigue, sourceThreshold = 0.80f, target = NeedType.Stress,  extraFillPerGameMinute = 0.0010f },
                new NeedCoupling { source = NeedType.Urine,   sourceThreshold = 0.80f, target = NeedType.Stress,  extraFillPerGameMinute = 0.0018f },
                new NeedCoupling { source = NeedType.Hygiene, sourceThreshold = 0.70f, target = NeedType.Stress,  extraFillPerGameMinute = 0.0006f }
            };
        }
    }
}
