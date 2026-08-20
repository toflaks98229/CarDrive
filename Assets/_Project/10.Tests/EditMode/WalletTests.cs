using NUnit.Framework;
using UnityEngine;
using CarDrive.Common;
using CarDrive.Systems;

namespace CarDrive.Tests
{
    /// <summary>
    /// <see cref="Wallet"/>의 지불 규칙에 대한 EditMode 테스트입니다.
    ///
    /// 재화는 한 번 음수가 되면 그 뒤의 모든 계산과 표시가 함께 망가집니다.
    /// 그래서 "모자라면 아무것도 빼지 않는다"를 가장 먼저 못 박습니다.
    ///
    /// <b>EditMode 라 Awake 가 돌지 않습니다.</b> 설정은 Wallet 이 쓰기 직전에
    /// 스스로 준비하지만(EnsureInitialized), 레지스트리 등록만은 테스트가 직접 합니다.
    /// </summary>
    public class WalletTests
    {
        private GameObject go;
        private Wallet wallet;

        [SetUp]
        public void SetUp()
        {
            GameContext.Clear();
            go = new GameObject("Wallet");
            wallet = go.AddComponent<Wallet>();

            // EditMode 에서는 Awake 가 돌지 않으므로 등록을 직접 합니다.
            // (Wallet.Report 가 레지스트리를 거쳐 지갑을 찾습니다)
            GameContext.Register(wallet);
        }

        [TearDown]
        public void TearDown()
        {
            if (go != null) Object.DestroyImmediate(go);
            go = null;
            wallet = null;
            GameContext.Clear();
        }

        /// <summary>기본 설정에는 돈과 엑토플라즘이 모두 있어야 합니다.</summary>
        [Test]
        public void 두_재화가_모두_준비된다()
        {
            Assert.AreEqual(0, wallet.Get(CurrencyType.Money));
            Assert.AreEqual(0, wallet.Get(CurrencyType.Ectoplasm));
            Assert.AreEqual(2, wallet.GetAllSettings().Count);
        }

        /// <summary>넣은 만큼 늘어야 합니다.</summary>
        [Test]
        public void 넣으면_늘어난다()
        {
            Assert.AreEqual(10, wallet.Add(CurrencyType.Ectoplasm, 10));
            Assert.AreEqual(10, wallet.Get(CurrencyType.Ectoplasm));
        }

        /// <summary>0 이하를 넣는 것은 무시되어야 합니다. Add로 빼는 길을 열어 두면 안 됩니다.</summary>
        [Test]
        public void 음수나_0을_넣으면_무시된다()
        {
            wallet.Add(CurrencyType.Money, 100);

            Assert.AreEqual(0, wallet.Add(CurrencyType.Money, 0));
            Assert.AreEqual(0, wallet.Add(CurrencyType.Money, -50));
            Assert.AreEqual(100, wallet.Get(CurrencyType.Money));
        }

        /// <summary>가진 만큼은 쓸 수 있어야 합니다.</summary>
        [Test]
        public void 가진_만큼은_쓸_수_있다()
        {
            wallet.Add(CurrencyType.Money, 100);

            Assert.IsTrue(wallet.TrySpend(CurrencyType.Money, 100));
            Assert.AreEqual(0, wallet.Get(CurrencyType.Money));
        }

        /// <summary>
        /// 이 테스트가 이 클래스의 핵심입니다.
        /// 모자라면 <b>한 푼도 빠지지 않고</b> false여야 합니다.
        /// </summary>
        [Test]
        public void 모자라면_아무것도_빠지지_않는다()
        {
            wallet.Add(CurrencyType.Money, 30);

            Assert.IsFalse(wallet.TrySpend(CurrencyType.Money, 50));
            Assert.AreEqual(30, wallet.Get(CurrencyType.Money), "실패한 지불에서 잔액이 줄었습니다.");
        }

        /// <summary>보유량은 절대 음수가 되지 않아야 합니다.</summary>
        [Test]
        public void 음수가_되지_않는다()
        {
            for (int i = 0; i < 10; i++) wallet.TrySpend(CurrencyType.Ectoplasm, 5);

            Assert.GreaterOrEqual(wallet.Get(CurrencyType.Ectoplasm), 0);
        }

        /// <summary>지불 가능 여부 확인이 실제 지불 결과와 어긋나면 안 됩니다.</summary>
        [Test]
        public void 확인과_지불_결과가_같다()
        {
            wallet.Add(CurrencyType.Ectoplasm, 7);

            Assert.IsTrue(wallet.CanAfford(CurrencyType.Ectoplasm, 7));
            Assert.IsTrue(wallet.TrySpend(CurrencyType.Ectoplasm, 7));

            Assert.IsFalse(wallet.CanAfford(CurrencyType.Ectoplasm, 1));
            Assert.IsFalse(wallet.TrySpend(CurrencyType.Ectoplasm, 1));
        }

        /// <summary>표기는 설정의 접두·형식을 따라야 합니다. UI가 따로 정하면 규칙이 갈라집니다.</summary>
        [Test]
        public void 표기에_접두와_천단위가_붙는다()
        {
            wallet.Add(CurrencyType.Money, 1250);

            string text = wallet.Format(CurrencyType.Money);

            StringAssert.StartsWith("₩", text);
            StringAssert.Contains("1,250", text);
        }

        /// <summary>담았다가 되돌리면 보유량이 그대로여야 합니다.</summary>
        [Test]
        public void 담았다가_되돌리면_보유량이_같다()
        {
            wallet.Add(CurrencyType.Money, 480);
            wallet.Add(CurrencyType.Ectoplasm, 12);

            System.Collections.Generic.List<CurrencyState> saved = wallet.CaptureState();

            wallet.ResetAll();
            Assert.AreEqual(0, wallet.Get(CurrencyType.Money));

            wallet.RestoreState(saved);
            Assert.AreEqual(480, wallet.Get(CurrencyType.Money));
            Assert.AreEqual(12, wallet.Get(CurrencyType.Ectoplasm));
        }

        /// <summary>
        /// 풀에서 나온 덩어리는 참조 없이 <see cref="Wallet.Report"/>로 넣습니다.
        /// 등록된 지갑을 찾아가야 합니다.
        /// </summary>
        [Test]
        public void 정적_보고가_등록된_지갑에_들어간다()
        {
            Assert.AreEqual(3, Wallet.Report(CurrencyType.Ectoplasm, 3));
            Assert.AreEqual(3, wallet.Get(CurrencyType.Ectoplasm));
        }
    }
}
