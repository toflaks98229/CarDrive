using UnityEngine;

namespace CarDrive.Gameplay
{
    /// <summary>
    /// 먹을 수 있는 음식 하나입니다. 봉투에서 꺼내 그 자리에서 먹습니다.
    ///
    /// <b>맥주와 같은 방식입니다.</b> 조준해 상호작용하면 음식이 사라지고, 허기가 덜어지고,
    /// 연출이 돌고, 다 먹은 껍데기가 던져집니다. 그 절차는 <see cref="ConsumableItem"/>에
    /// 한 번만 적혀 있고 음료와 나눠 씁니다.
    ///
    /// <b>효과는 이 컴포넌트가 갖습니다.</b> 빵과 통조림이 같은 양의 허기를 덜 이유가 없으므로,
    /// 무엇이 얼마나 바뀌는지는 프리팹마다 다르게 둡니다. 인스펙터에서 우클릭해
    /// 대표적인 값을 바로 채울 수 있습니다.
    /// </summary>
    public class Food : ConsumableItem
    {
        // --- Protected Properties ---

        /// <summary>인스펙터를 비워 두었을 때 쓸 문구입니다.</summary>
        protected override string DefaultPromptLabel { get { return "먹기"; } }

        // --- Context Menu Presets ---
        // NeedSatisfier 와 같은 방식입니다. 인스펙터에서 우클릭해 바로 채울 수 있습니다.

        /// <summary>
        /// 간단한 요기입니다. 허기를 조금 덜고 갈증이 살짝 오릅니다.
        /// </summary>
        [ContextMenu("프리셋: 간식")]
        private void PresetSnack()
        {
            promptLabel = "";
            healAmount = 0f;
            consumeSeconds = 1.2f;
            effects = new System.Collections.Generic.List<CarDrive.Systems.NeedEffect>
            {
                new CarDrive.Systems.NeedEffect { type = CarDrive.Systems.NeedType.Hunger, relief = 0.25f },
                new CarDrive.Systems.NeedEffect { type = CarDrive.Systems.NeedType.Thirst, relief = -0.05f }
            };
        }

        /// <summary>
        /// 제대로 된 한 끼입니다. 허기를 크게 덜지만 짠 음식이라 갈증이 함께 오릅니다.
        /// <c>NeedSatisfier</c> 의 식사 프리셋과 같은 비율입니다.
        /// </summary>
        [ContextMenu("프리셋: 식사")]
        private void PresetMeal()
        {
            promptLabel = "";
            healAmount = 5f;
            consumeSeconds = 2.4f;
            effects = new System.Collections.Generic.List<CarDrive.Systems.NeedEffect>
            {
                new CarDrive.Systems.NeedEffect { type = CarDrive.Systems.NeedType.Hunger, relief = 0.8f },
                new CarDrive.Systems.NeedEffect { type = CarDrive.Systems.NeedType.Thirst, relief = -0.1f }
            };
        }

        /// <summary>
        /// 통조림입니다. 오래 두고 먹을 수 있는 대신 짜서 갈증이 많이 오릅니다.
        /// </summary>
        [ContextMenu("프리셋: 통조림")]
        private void PresetCanned()
        {
            promptLabel = "";
            healAmount = 2f;
            consumeSeconds = 2f;
            effects = new System.Collections.Generic.List<CarDrive.Systems.NeedEffect>
            {
                new CarDrive.Systems.NeedEffect { type = CarDrive.Systems.NeedType.Hunger, relief = 0.5f },
                new CarDrive.Systems.NeedEffect { type = CarDrive.Systems.NeedType.Thirst, relief = -0.2f }
            };
        }
    }
}
