using NUnit.Framework;
using UnityEngine;
using CarDrive.Gameplay;

namespace CarDrive.Tests
{
    /// <summary>
    /// <see cref="RobotAwareness.Judge"/> 만 봅니다.
    ///
    /// 규칙 하나가 이 부품의 모양을 정합니다 — "로봇은 당신을 모릅니다".
    /// 그래서 여기서 지키는 것은 <b>거의 언제나 무시여야 한다</b> 는 것과,
    /// 항의하는 이유가 "적을 봤다" 가 아니라 <b>"가야 하는데 못 간다"</b> 라는 것입니다.
    /// </summary>
    public class RobotAwarenessTests
    {
        private const float Radius = 26f;
        private const float Angle = 75f;
        private const float Patience = 3.5f;
        private const float Blame = 14f;
        private const float Grudge = 8f;

        private static RobotAwareness.Response Judge(RobotAwareness.Sense s)
        {
            return RobotAwareness.Judge(s, Radius, Angle, Patience, Blame, Grudge);
        }

        /// <summary>아무 일도 없는 기본 상태입니다.</summary>
        private static RobotAwareness.Sense Quiet()
        {
            return new RobotAwareness.Sense
            {
                Distance = 100f,
                Angle = 180f,
                InSight = false,
                Working = true,
                Stuck = 0f,
                SinceHit = 9999f,
            };
        }

        [Test]
        public void 멀리_있으면_모른다()
        {
            Assert.That(Judge(Quiet()), Is.EqualTo(RobotAwareness.Response.Ignore));
        }

        [Test]
        public void 가까이_앞에_있으면_알아챈다()
        {
            RobotAwareness.Sense s = Quiet();
            s.Distance = 10f;
            s.Angle = 20f;
            s.InSight = true;

            Assert.That(Judge(s), Is.EqualTo(RobotAwareness.Response.Notice));
        }

        [Test]
        public void 등_뒤에_있으면_모른다()
        {
            RobotAwareness.Sense s = Quiet();
            s.Distance = 10f;
            s.Angle = 170f;      // 뒤쪽
            s.InSight = true;

            Assert.That(Judge(s), Is.EqualTo(RobotAwareness.Response.Ignore));
        }

        [Test]
        public void 가려져_있으면_모른다()
        {
            RobotAwareness.Sense s = Quiet();
            s.Distance = 10f;
            s.Angle = 10f;
            s.InSight = false;

            Assert.That(Judge(s), Is.EqualTo(RobotAwareness.Response.Ignore));
        }

        // --- 항의 ---

        [Test]
        public void 가야_하는데_못_가고_사람이_앞에_있으면_항의한다()
        {
            RobotAwareness.Sense s = Quiet();
            s.Working = true;
            s.Stuck = Patience + 0.1f;
            s.Distance = 8f;

            Assert.That(Judge(s), Is.EqualTo(RobotAwareness.Response.Protest));
        }

        [Test]
        public void 참는_동안에는_항의하지_않는다()
        {
            // ⚠ 보행기는 한 걸음 사이에 잠깐 멈춥니다. 그 순간을 세면 헛항의합니다.
            RobotAwareness.Sense s = Quiet();
            s.Stuck = Patience - 0.1f;
            s.Distance = 8f;
            s.Angle = 10f;
            s.InSight = true;

            Assert.That(Judge(s), Is.EqualTo(RobotAwareness.Response.Notice),
                        "아직은 알아챔까지입니다");
        }

        [Test]
        public void 바위에_걸린_것을_사람에게_항의하지_않는다()
        {
            RobotAwareness.Sense s = Quiet();
            s.Stuck = 30f;
            s.Distance = 60f;     // 사람은 멀리 있습니다

            Assert.That(Judge(s), Is.EqualTo(RobotAwareness.Response.Ignore));
        }

        [Test]
        public void 갈_곳이_없으면_막힐_일도_없다()
        {
            // 퇴근해서 서 있는 기계는 사람이 옆에 붙어 있어도 항의하지 않습니다.
            RobotAwareness.Sense s = Quiet();
            s.Working = false;
            s.Stuck = 30f;
            s.Distance = 3f;
            s.Angle = 5f;
            s.InSight = true;

            Assert.That(Judge(s), Is.EqualTo(RobotAwareness.Response.Notice));
        }

        [Test]
        public void 맞으면_보든_안_보든_항의한다()
        {
            // ⚠ 뒤에서 받히는 일이 흔합니다. 시야에 기대면 받고도 가만히 있습니다.
            RobotAwareness.Sense s = Quiet();
            s.Distance = 90f;
            s.Angle = 179f;
            s.InSight = false;
            s.SinceHit = 1f;

            Assert.That(Judge(s), Is.EqualTo(RobotAwareness.Response.Protest));
        }

        [Test]
        public void 맞은_기억은_시간이_지나면_풀린다()
        {
            RobotAwareness.Sense s = Quiet();
            s.SinceHit = Grudge + 0.1f;

            Assert.That(Judge(s), Is.EqualTo(RobotAwareness.Response.Ignore));
        }

        [Test]
        public void 거의_언제나_무시다()
        {
            // 규칙 하나를 숫자로 지킵니다 — 아무렇게나 흩뿌린 상황에서
            // 항의가 드물어야 합니다.
            Random.InitState(20260911);
            int protest = 0;
            const int Trials = 2000;

            for (int i = 0; i < Trials; i++)
            {
                RobotAwareness.Sense s = new RobotAwareness.Sense
                {
                    Distance = Random.Range(0f, 90f),
                    Angle = Random.Range(0f, 180f),
                    InSight = Random.value > 0.4f,
                    Working = Random.value > 0.3f,
                    Stuck = Random.value > 0.85f ? Random.Range(0f, 8f) : 0f,
                    SinceHit = 9999f,
                };

                if (Judge(s) == RobotAwareness.Response.Protest) protest++;
            }

            Assert.That(protest / (float)Trials, Is.LessThan(0.05f),
                        "항의가 5% 를 넘으면 기계가 사냥꾼처럼 보입니다");
        }
    }
}
