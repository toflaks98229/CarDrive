using NUnit.Framework;
using UnityEngine;
using CarDrive.Systems;

namespace CarDrive.Tests
{
    /// <summary>
    /// 밤빛이 <b>지평선 근처에서 물러서는지</b> 봅니다.
    ///
    /// <b>왜 이 값 하나를 따로 재는가.</b> 이 씬의 빛은 방향광 하나뿐이라 낮과 밤이
    /// 그것을 나눠 씁니다. 그 <b>세기와 자리를 같은 값</b>이 정해야 합니다 —
    /// 어긋나면 세기가 남은 채 자리만 옮겨 그림자가 세계를 휩씁니다.
    /// 씬도 빛도 없이 확인할 수 있어야 하는 규칙입니다.
    ///
    /// ⚠ <b>이 세계에는 하늘이 없습니다.</b> 밤빛은 달이 아니라 머리 위를 덮은
    /// 거대구조물의 천장에서 내려옵니다.
    /// </summary>
    public class NightLightTests
    {
        /// <summary>빛이 나아가는 방향의 y 성분입니다. 해가 지평선 아래 <paramref name="dip"/>도.</summary>
        private static float Forward(float dip)
        {
            return Mathf.Sin(dip * Mathf.Deg2Rad);
        }

        [Test]
        public void 해가_지평선_위면_밤빛은_없다()
        {
            // 빛이 내려오면(y 음수) 해가 하늘에 있다는 뜻입니다.
            Assert.That(SkyController.NightShare(Forward(-30f), 12f), Is.EqualTo(0f));
            Assert.That(SkyController.NightShare(0f, 12f), Is.EqualTo(0f));
        }

        [Test]
        public void 깊은_밤에는_온전한_밤빛이다()
        {
            Assert.That(SkyController.NightShare(Forward(60f), 12f), Is.EqualTo(1f).Within(0.001f));
            Assert.That(SkyController.NightShare(Forward(12f), 12f), Is.EqualTo(1f).Within(0.001f));
        }

        [Test]
        public void 지평선에_가까울수록_물러선다()
        {
            float near = SkyController.NightShare(Forward(1f), 12f);
            float mid = SkyController.NightShare(Forward(6f), 12f);
            float deep = SkyController.NightShare(Forward(11f), 12f);

            Assert.That(near, Is.LessThan(mid), "지평선 바로 밑이 더 약해야 합니다");
            Assert.That(mid, Is.LessThan(deep), "깊을수록 세야 합니다");

            // <b>끝에서 기울기가 0 이어야 합니다.</b> 직선으로 빼면 달이 서는 순간과
            // 다 빠지는 순간에 꺾임이 보입니다.
            Assert.That(near, Is.LessThan(0.03f),
                        "지평선 바로 밑에서는 거의 0 이어야 합니다 — 실제로는 " + near.ToString("F3"));
        }

        [Test]
        public void 깊이를_0_으로_두면_지평선에서_바로_선다()
        {
            Assert.That(SkyController.NightShare(Forward(0.5f), 0f), Is.EqualTo(1f));
        }

        // --- 방향을 누가 가지는가 ---

        [Test]
        public void 해가_더_세면_천장으로_옮기지_않는다()
        {
            // 19시. 해는 지평선 15도 아래인데 밝기 곡선은 아직 0.3 입니다.
            // 여기서 옮기면 노을이 진 쪽이 아니라 천장에서 빛이 들어옵니다.
            Assert.That(SkyController.NightFold(Forward(15f), 12f, 0.3f, 0.1f), Is.EqualTo(0f));
        }

        [Test]
        public void 해가_다_잦아들면_온전히_천장이_된다()
        {
            Assert.That(SkyController.NightFold(Forward(60f), 12f, 0f, 0.1f),
                        Is.EqualTo(1f).Within(0.001f));
        }

        [Test]
        public void 해에서_천장으로_건너가는_동안_이어진다()
        {
            float previous = -1f;

            // 해가 달빛 세기까지 잦아드는 동안, 접히는 정도가 뒤로 가지 않아야 합니다.
            for (int i = 10; i >= 0; i--)
            {
                float sunLevel = 0.1f * i / 10f;
                float fold = SkyController.NightFold(Forward(60f), 12f, sunLevel, 0.1f);

                Assert.That(fold, Is.GreaterThanOrEqualTo(previous),
                            "해가 잦아드는데 자리가 되돌아갔습니다 — 해 " + sunLevel.ToString("F3"));
                previous = fold;
            }

            Assert.That(previous, Is.EqualTo(1f).Within(0.001f));
        }

        [Test]
        public void 지평선을_넘는_순간에는_옮김이_0_이다()
        {
            // 넘는 순간 둘이 같은 자리여야 그림자가 휩쓸리지 않습니다.
            Assert.That(SkyController.NightFold(Forward(0f), 12f, 0f, 0.1f), Is.EqualTo(0f));
            Assert.That(SkyController.NightFold(Forward(-1f), 12f, 0f, 0.1f), Is.EqualTo(0f));
        }
    }
}
