using NUnit.Framework;
using UnityEngine;
using CarDrive.Common;
using CarDrive.Systems;

namespace CarDrive.Tests
{
    /// <summary>
    /// <see cref="NeedsSystem"/>의 진행 규칙에 대한 EditMode 테스트입니다.
    ///
    /// <c>Tick(float)</c>은 "프레임 없이 진행시키고 싶을 때 쓰라"고 주석에 명시된
    /// 메서드입니다. 씬도 재생도 필요 없으므로 여기서 그대로 부릅니다.
    /// </summary>
    public class NeedsSystemTests
    {
        /// <summary>테스트가 만든 오브젝트입니다.</summary>
        private GameObject go;

        /// <summary>검사 대상입니다.</summary>
        private NeedsSystem needs;

        [SetUp]
        public void SetUp()
        {
            GameContext.Clear();

            go = new GameObject("Needs");
            needs = go.AddComponent<NeedsSystem>();

            // Update가 돌지 않는 EditMode지만, 의도를 분명히 해 둡니다.
            needs.needsEnabled = false;
        }

        [TearDown]
        public void TearDown()
        {
            if (go != null) Object.DestroyImmediate(go);
            go = null;
            needs = null;
            GameContext.Clear();
        }

        /// <summary>시작할 때 모든 니즈는 0이어야 합니다.</summary>
        [Test]
        public void 처음에는_모든_니즈가_0이다()
        {
            Assert.AreEqual(0f, needs.GetValue(NeedType.Hunger), 0.0001f);
            Assert.AreEqual(0f, needs.GetValue(NeedType.Thirst), 0.0001f);
            Assert.AreEqual(0f, needs.GetValue(NeedType.Fatigue), 0.0001f);
        }

        /// <summary>
        /// 갈증은 허기보다 빨리 차오릅니다. (기본값 0.0017 대 0.0011)
        /// 수치를 조정하다 이 관계가 뒤집히면 설계 의도가 깨진 것입니다.
        /// </summary>
        [Test]
        public void 갈증이_허기보다_빨리_차오른다()
        {
            needs.Tick(600f);

            Assert.Greater(needs.GetValue(NeedType.Thirst), needs.GetValue(NeedType.Hunger));
        }

        /// <summary>충분히 시간이 흐르면 갈증은 경고 구간에 들어가야 합니다.</summary>
        [Test]
        public void 오래_두면_갈증이_경고에_들어간다()
        {
            Assert.IsFalse(needs.IsWarning(NeedType.Thirst));

            needs.Tick(600f);

            Assert.IsTrue(needs.IsWarning(NeedType.Thirst));
        }

        /// <summary>
        /// 아무리 오래 두어도 한계(overflowLimit)를 넘지 않아야 합니다.
        /// 넘어가면 게이지와 처벌 계산이 모두 어긋납니다.
        /// </summary>
        [Test]
        public void 한계를_넘어서_차오르지_않는다()
        {
            for (int i = 0; i < 20; i++) needs.Tick(600f);

            foreach (NeedSetting setting in needs.GetAllSettings())
            {
                Assert.LessOrEqual(needs.GetValue(setting.type), setting.overflowLimit,
                    setting.displayName + "이(가) 한계를 넘었습니다.");
            }
        }

        /// <summary>해소하면 값이 줄고, 0 아래로는 내려가지 않아야 합니다.</summary>
        [Test]
        public void 해소하면_줄고_0_아래로는_안_내려간다()
        {
            needs.Tick(300f);
            float before = needs.GetValue(NeedType.Thirst);
            Assert.Greater(before, 0f);

            needs.Satisfy(NeedType.Thirst, 0.1f);
            Assert.Less(needs.GetValue(NeedType.Thirst), before);

            needs.Satisfy(NeedType.Thirst, 999f);
            Assert.AreEqual(0f, needs.GetValue(NeedType.Thirst), 0.0001f);
        }

        /// <summary>
        /// 청결은 표시를 뒤집습니다. 값이 0일 때 게이지가 가득 차 있어야 합니다.
        /// (0 = 깨끗함)
        /// </summary>
        [Test]
        public void 청결은_표시가_반전된다()
        {
            Assert.AreEqual(1f, needs.GetDisplayFill(NeedType.Hygiene), 0.0001f);
            Assert.AreEqual(0f, needs.GetDisplayFill(NeedType.Thirst), 0.0001f);
        }

        /// <summary>세이브에 담았다가 되돌리면 수치가 그대로여야 합니다.</summary>
        [Test]
        public void 담았다가_되돌리면_수치가_같다()
        {
            needs.Tick(450f);
            float thirst = needs.GetValue(NeedType.Thirst);

            System.Collections.Generic.List<NeedState> saved = needs.CaptureState();

            needs.ResetAll();
            Assert.AreEqual(0f, needs.GetValue(NeedType.Thirst), 0.0001f);

            needs.RestoreState(saved);
            Assert.AreEqual(thirst, needs.GetValue(NeedType.Thirst), 0.0001f);
        }
    }
}
