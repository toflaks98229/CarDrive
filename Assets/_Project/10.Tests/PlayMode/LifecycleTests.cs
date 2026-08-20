using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using CarDrive.Common;
using CarDrive.Gameplay;
using CarDrive.Systems;

namespace CarDrive.Tests
{
    /// <summary>
    /// MonoBehaviour 생명주기가 실제로 도는 것을 확인하는 PlayMode 테스트입니다.
    ///
    /// <b>EditMode 테스트로는 이것을 확인할 수 없습니다.</b> 에디터 모드에서는
    /// <c>AddComponent</c> 를 해도 <c>Awake</c>·<c>OnEnable</c> 이 호출되지 않습니다.
    /// 실제로 이 프로젝트의 EditMode 테스트 열여덟 개가 그 사실을 모른 채 쓰였다가
    /// 한꺼번에 실패했습니다. 그래서 <b>등록·초기화처럼 생명주기에 기대는 것</b>은
    /// 여기서 검사합니다.
    ///
    /// 검사 대상을 나누는 기준은 분명합니다.
    ///  - 순수 계산·규칙 → EditMode (빠르고 씬이 필요 없습니다)
    ///  - Awake/OnEnable/풀 재사용 → PlayMode (여기)
    /// </summary>
    public class LifecycleTests
    {
        /// <summary>테스트가 만든 오브젝트입니다.</summary>
        private GameObject go;

        [SetUp]
        public void SetUp()
        {
            GameContext.Clear();
        }

        [TearDown]
        public void TearDown()
        {
            if (go != null) Object.Destroy(go);
            go = null;
            GameContext.Clear();
        }

        /// <summary>
        /// 지갑은 Awake 에서 스스로 등록해야 합니다.
        /// 이것이 되어야 풀에서 나온 엑토플라즘이 <see cref="Wallet.Report"/> 로 찾아갑니다.
        /// </summary>
        [UnityTest]
        public IEnumerator 지갑은_Awake에서_스스로_등록한다()
        {
            go = new GameObject("Wallet");
            Wallet wallet = go.AddComponent<Wallet>();

            // 한 프레임 기다려 Awake 가 끝나게 합니다.
            yield return null;

            Assert.AreSame(wallet, GameContext.Get<Wallet>(), "Awake 에서 등록되지 않았습니다.");
            Assert.AreEqual(3, Wallet.Report(CurrencyType.Ectoplasm, 3));
            Assert.AreEqual(3, wallet.Get(CurrencyType.Ectoplasm));
        }

        /// <summary>
        /// 니즈 시스템은 Awake 에서 설정을 갖추고, 프레임이 흐르면 저절로 차올라야 합니다.
        /// </summary>
        [UnityTest]
        public IEnumerator 니즈는_프레임이_흐르면_차오른다()
        {
            go = new GameObject("Needs");
            NeedsSystem needs = go.AddComponent<NeedsSystem>();

            yield return null;

            Assert.AreSame(needs, GameContext.Get<NeedsSystem>(), "Awake 에서 등록되지 않았습니다.");
            Assert.AreEqual(6, needs.GetAllSettings().Count);

            // 시간 배율을 크게 올려 몇 프레임 만에 눈에 띄게 만듭니다.
            needs.gameMinutesPerRealSecond = 6000f;

            float before = needs.GetValue(NeedType.Thirst);
            for (int i = 0; i < 5; i++) yield return null;

            Assert.Greater(needs.GetValue(NeedType.Thirst), before, "시간이 흘렀는데 갈증이 그대로입니다.");
        }

        /// <summary>
        /// 체력은 Awake 에서 최대치로 채워져야 합니다.
        /// 니즈 시스템이 첫 프레임부터 체력을 깎을 수 있기 때문입니다.
        /// </summary>
        [UnityTest]
        public IEnumerator 체력은_Awake에서_가득_찬다()
        {
            go = new GameObject("Player");
            PlayerHealth health = go.AddComponent<PlayerHealth>();
            health.maxHealth = 80f;

            yield return null;

            // maxHealth 를 Awake 이후에 바꿨으므로 기본값 100 으로 차 있는 것이 맞습니다.
            Assert.Greater(health.CurrentHealth, 0f, "Awake 에서 체력이 채워지지 않았습니다.");
            Assert.IsFalse(health.IsDead);
            Assert.AreSame(health, GameContext.Get<PlayerHealth>());
        }

        /// <summary>
        /// 재화 덩어리는 수명이 다하면 스스로 물러나야 합니다.
        ///
        /// 풀에서 나온 것이 아니면 <see cref="PrefabPool.Release"/> 가 <b>파괴</b>합니다.
        /// 바닥에 영원히 남아 쌓이는 것보다 사라지는 편이 낫기 때문입니다.
        /// </summary>
        [UnityTest]
        public IEnumerator 덩어리는_수명이_다하면_사라진다()
        {
            go = new GameObject("Drop");
            CurrencyPickup pickup = go.AddComponent<CurrencyPickup>();
            pickup.lifetime = 0.05f;

            yield return new WaitForSeconds(0.3f);

            // 파괴된 오브젝트는 Unity 의 == null 로만 걸러집니다.
            Assert.IsTrue(go == null, "수명이 다했는데 남아 있습니다.");
            go = null;
        }

        /// <summary>
        /// 재화 덩어리는 다시 켜질 때 <b>흐른 시간이 0으로 되돌아가야</b> 합니다.
        ///
        /// 풀에서 재사용되므로 파괴되지 않습니다. 지난번 수명이 그대로 남아 있으면
        /// 다음에 꺼낸 덩어리가 <b>나오자마자 사라집니다.</b>
        ///
        /// 그래서 이렇게 확인합니다. 수명의 절반쯤 지난 시점에 껐다 켜고,
        /// 다시 절반을 기다립니다. 되돌아왔다면 총 경과가 수명을 넘었어도 살아 있어야 합니다.
        /// </summary>
        [UnityTest]
        public IEnumerator 덩어리는_다시_켜질_때_흐른_시간이_되돌아온다()
        {
            go = new GameObject("Drop");
            CurrencyPickup pickup = go.AddComponent<CurrencyPickup>();
            pickup.lifetime = 0.4f;

            yield return new WaitForSeconds(0.25f);
            Assert.IsTrue(go != null, "아직 수명이 남았는데 사라졌습니다.");

            // 풀 회수와 재사용을 흉내 냅니다.
            go.SetActive(false);
            go.SetActive(true);

            yield return new WaitForSeconds(0.25f);

            // 되돌아오지 않았다면 총 0.5초가 흘러 수명 0.4초를 넘겼을 것입니다.
            Assert.IsTrue(go != null, "다시 켰는데 흐른 시간이 되돌아오지 않았습니다.");
            Assert.IsTrue(go.activeSelf);
        }

        /// <summary>
        /// 같은 시스템이 둘이면 나중 것은 스스로 꺼져야 합니다.
        /// 두 개가 동시에 돌면 니즈가 두 배로 차오릅니다.
        /// </summary>
        [UnityTest]
        public IEnumerator 시스템이_둘이면_나중_것이_꺼진다()
        {
            go = new GameObject("First");
            Wallet first = go.AddComponent<Wallet>();

            GameObject second = new GameObject("Second");
            Wallet duplicate = second.AddComponent<Wallet>();

            yield return null;

            Assert.AreSame(first, GameContext.Get<Wallet>());
            Assert.IsFalse(duplicate.enabled, "중복된 지갑이 스스로 꺼지지 않았습니다.");

            Object.Destroy(second);
        }
    }
}
