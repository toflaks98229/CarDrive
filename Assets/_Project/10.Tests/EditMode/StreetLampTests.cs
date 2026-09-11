using NUnit.Framework;
using CarDrive.Gameplay;

namespace CarDrive.Tests
{
    /// <summary>
    /// 등과 점등기와 귀신을 잇는 <b>세 판단</b>을 봅니다.
    ///
    /// 셋 다 씬도 빛도 귀신도 없이 확인할 수 있어야 하는 규칙이라 밖으로 꺼내 두었습니다.
    /// </summary>
    public class StreetLampTests
    {
        // --- 언제 켜지는가 ---

        [Test]
        public void 대낮에는_꺼져_있는다()
        {
            Assert.IsFalse(StreetLamp.ShouldBurn(1f, false, 0.35f, 0.5f));
        }

        [Test]
        public void 어두워지면_켜진다()
        {
            Assert.IsTrue(StreetLamp.ShouldBurn(0.2f, false, 0.35f, 0.5f));
        }

        [Test]
        public void 한_번_켜지면_더_밝아질_때까지_버틴다()
        {
            // ⚠ 문턱이 하나면 밝기가 그 언저리에서 떨릴 때 등이 깜빡입니다.
            // 0.4 는 켜는 문턱(0.35) 위지만 끄는 문턱(0.5) 아래이므로 버텨야 합니다.
            Assert.IsTrue(StreetLamp.ShouldBurn(0.4f, true, 0.35f, 0.5f));
            Assert.IsFalse(StreetLamp.ShouldBurn(0.4f, false, 0.35f, 0.5f));
        }

        [Test]
        public void 충분히_밝아지면_꺼진다()
        {
            Assert.IsFalse(StreetLamp.ShouldBurn(0.6f, true, 0.35f, 0.5f));
        }

        [Test]
        public void 문턱을_거꾸로_줘도_깜빡이지_않는다()
        {
            // 끄는 문턱이 켜는 문턱보다 낮게 잡히면 벌어진 구간이 없어집니다.
            // 그때도 한 번 켜진 등은 켜는 문턱까지는 버텨야 합니다.
            Assert.IsTrue(StreetLamp.ShouldBurn(0.3f, true, 0.35f, 0.1f));
        }

        // --- 점등기가 갈 만한 등인가 ---

        [Test]
        public void 성한_등은_고치러_가지_않는다()
        {
            Assert.IsFalse(LampLighter.Worth(false, 10f, 300f));
        }

        [Test]
        public void 죽었고_가까우면_고치러_간다()
        {
            Assert.IsTrue(LampLighter.Worth(true, 10f, 300f));
        }

        [Test]
        public void 너무_멀면_두고_간다()
        {
            Assert.IsFalse(LampLighter.Worth(true, 400f, 300f));
        }

        // --- 등불이 귀신을 얼마나 미루는가 ---

        [Test]
        public void 캄캄한_데서는_평소대로_나온다()
        {
            Assert.That(GhostSpawner.Calm(0f, 2.5f), Is.EqualTo(1f).Within(0.001f));
        }

        [Test]
        public void 등_바로_아래에서는_가장_뜸하다()
        {
            Assert.That(GhostSpawner.Calm(1f, 2.5f), Is.EqualTo(2.5f).Within(0.001f));
        }

        [Test]
        public void 등에서_멀어질수록_평소로_돌아온다()
        {
            float near = GhostSpawner.Calm(0.8f, 2.5f);
            float far = GhostSpawner.Calm(0.2f, 2.5f);

            Assert.That(near, Is.GreaterThan(far));
            Assert.That(far, Is.GreaterThan(1f));
        }

        [Test]
        public void 배율을_1_미만으로_줘도_귀신이_더_나오지는_않는다()
        {
            // ⚠ 등불이 귀신을 <b>불러들이면</b> 규칙이 뒤집힙니다. 1 이 바닥입니다.
            Assert.That(GhostSpawner.Calm(1f, 0.2f), Is.EqualTo(1f).Within(0.001f));
        }
    }
}
