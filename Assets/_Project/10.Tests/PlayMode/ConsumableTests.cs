using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using CarDrive.Common;
using CarDrive.Gameplay;
using CarDrive.Systems;

namespace CarDrive.Tests
{
    /// <summary>
    /// 먹고 마시는 절차에 대한 PlayMode 테스트입니다.
    ///
    /// <b>PlayMode 인 이유.</b> 소비는 코루틴입니다 — 감추고, 효과를 먹이고, 연출이 끝날 때까지
    /// 기다렸다가 껍데기를 던집니다. 프레임이 흐르지 않으면 시작조차 하지 않습니다.
    ///
    /// <b>무엇을 지키는가.</b> 음식과 음료는 같은 길을 지나되 <b>서로 다른 효과</b>를 내야 합니다.
    /// 그 갈림이 무너지면 빵을 먹었는데 갈증이 가시거나, 맥주를 마셨는데 배가 부릅니다.
    /// 예전에는 효과가 소비하는 쪽에 박혀 있어 모든 물건이 같은 반응을 냈습니다.
    /// </summary>
    public class ConsumableTests
    {
        private GameObject player;
        private readonly List<GameObject> spawnedItems = new List<GameObject>();
        private NeedsSystem needs;
        private BeverageConsumer consumer;

        [SetUp]
        public void SetUp()
        {
            GameContext.Clear();
        }

        [TearDown]
        public void TearDown()
        {
            if (player != null) Object.Destroy(player);

            for (int i = 0; i < spawnedItems.Count; i++)
            {
                if (spawnedItems[i] != null) Object.Destroy(spawnedItems[i]);
            }
            spawnedItems.Clear();

            player = null; needs = null; consumer = null;
            GameContext.Clear();
        }

        /// <summary>
        /// 음식은 자기 효과대로 허기를 덜어야 합니다.
        /// </summary>
        [UnityTest]
        public IEnumerator 음식은_자기_효과대로_허기를_던다()
        {
            yield return BuildPlayer();

            needs.Add(NeedType.Hunger, 0.9f);
            float before = needs.GetValue(NeedType.Hunger);

            Food bread = BuildItem<Food>("빵");
            bread.consumeSeconds = 0.05f;
            bread.effects = new List<NeedEffect>
            {
                new NeedEffect { type = NeedType.Hunger, relief = 0.5f }
            };

            Assert.IsTrue(consumer.Consume(bread), "소비가 시작되어야 합니다.");
            yield return new WaitForSeconds(0.3f);

            float after = needs.GetValue(NeedType.Hunger);
            Assert.AreEqual(before - 0.5f, after, 0.01f, "적어 둔 만큼 허기가 덜어져야 합니다.");
        }

        /// <summary>
        /// 음식을 먹었다고 갈증이 가시면 안 됩니다.
        /// 효과가 소비하는 쪽에 박혀 있던 시절의 고장입니다.
        /// </summary>
        [UnityTest]
        public IEnumerator 음식은_음료의_효과를_내지_않는다()
        {
            yield return BuildPlayer();

            needs.Add(NeedType.Thirst, 0.8f);
            float thirstBefore = needs.GetValue(NeedType.Thirst);

            Food bread = BuildItem<Food>("빵");
            bread.consumeSeconds = 0.05f;
            bread.effects = new List<NeedEffect>
            {
                new NeedEffect { type = NeedType.Hunger, relief = 0.5f }
            };

            consumer.Consume(bread);
            yield return new WaitForSeconds(0.3f);

            Assert.AreEqual(thirstBefore, needs.GetValue(NeedType.Thirst), 0.01f,
                "빵을 먹었는데 갈증이 움직였습니다.");
        }

        /// <summary>
        /// 자기 효과가 없는 음료는 예전처럼 소비하는 쪽의 값을 따라야 합니다.
        /// 이미 배선된 병이 그대로 돌아가는지 확인합니다.
        /// </summary>
        [UnityTest]
        public IEnumerator 효과가_없는_음료는_예전_값을_따른다()
        {
            yield return BuildPlayer();

            needs.Add(NeedType.Thirst, 0.9f);
            float before = needs.GetValue(NeedType.Thirst);

            Beverage bottle = BuildItem<Beverage>("맥주");
            bottle.consumeSeconds = 0.05f;
            // effects 를 비워 둡니다. 예전 프리팹과 같은 상태입니다.

            consumer.Consume(bottle);
            yield return new WaitForSeconds(0.3f);

            Assert.AreEqual(before - consumer.thirstRelief, needs.GetValue(NeedType.Thirst), 0.01f,
                "소비하는 쪽에 적힌 갈증 해소량이 쓰여야 합니다.");
        }

        /// <summary>소비하는 동안에는 다른 것을 집을 수 없어야 합니다.</summary>
        [UnityTest]
        public IEnumerator 소비하는_동안에는_다른_것을_집지_못한다()
        {
            yield return BuildPlayer();

            Food first = BuildItem<Food>("빵");
            first.consumeSeconds = 0.3f;

            Assert.IsTrue(consumer.Consume(first));
            Assert.IsTrue(consumer.IsBusy, "소비 중이어야 합니다.");

            Food second = BuildItem<Food>("사탕");
            Assert.IsFalse(consumer.Consume(second), "이미 소비 중이면 거절해야 합니다.");

            yield return new WaitForSeconds(0.5f);
            Assert.IsFalse(consumer.IsBusy, "다 먹고 나면 손이 비어야 합니다.");
        }

        // --- Helpers ---

        /// <summary>니즈와 소비 담당을 갖춘 플레이어를 세웁니다.</summary>
        /// <returns>한 프레임 대기. Awake·Start 가 돌게 합니다.</returns>
        private IEnumerator BuildPlayer()
        {
            player = new GameObject("Player", typeof(Camera));

            needs = player.AddComponent<NeedsSystem>();
            needs.needsEnabled = false;   // 시간에 따라 저절로 차오르면 값 비교가 흔들립니다

            consumer = player.AddComponent<BeverageConsumer>();
            consumer.needsSystem = needs;

            yield return null;
        }

        /// <summary>소비할 물건 하나를 만듭니다.</summary>
        /// <typeparam name="T">만들 물건의 종류</typeparam>
        /// <param name="name">오브젝트 이름</param>
        /// <returns>만들어진 물건</returns>
        private T BuildItem<T>(string name) where T : ConsumableItem
        {
            GameObject go = new GameObject(name);
            go.transform.position = Vector3.forward * 2f;
            spawnedItems.Add(go);
            return go.AddComponent<T>();
        }
    }
}
