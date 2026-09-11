using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using CarDrive.Common;
using CarDrive.Gameplay;

namespace CarDrive.Tests
{
    /// <summary>
    /// 등이 <b>실제로</b> 어두워지면 켜지고, 점등기가 죽은 등을 되살리는지 봅니다.
    ///
    /// <b>걷는 부품 없이 봅니다.</b> 점등기의 다리는 <see cref="RobotDriver"/> 의 몫이라
    /// 여기서는 등을 손이 닿는 자리에 두고 <b>순서</b>만 잽니다.
    /// </summary>
    public class StreetLampNightTests
    {
        /// <summary>원하는 밝기를 그대로 돌려주는 가짜 시계입니다.</summary>
        private sealed class Clock : IGameClock
        {
            public float Daylight { get; set; }
            public float TotalMinutes { get { return 0f; } }
            public bool IsNight { get { return Daylight < 0.5f; } }
            public bool IsRunning { get { return true; } }
            public float GetMinutesPerSecond(float fallback) { return fallback; }
            public void AdvanceMinutes(float minutes) { }
        }

        private GameObject post;
        private StreetLamp lamp;
        private Clock clock;

        [SetUp]
        public void SetUp()
        {
            post = new GameObject("Lamp");
            post.transform.position = Vector3.zero;

            GameObject bulb = new GameObject("Bulb");
            bulb.transform.SetParent(post.transform, false);

            Light light = bulb.AddComponent<Light>();
            light.type = LightType.Point;

            lamp = post.AddComponent<StreetLamp>();
            lamp.bulb = light;
            lamp.reach = 20f;

            clock = new Clock { Daylight = 1f };
            lamp.Construct(clock);
        }

        [TearDown]
        public void TearDown()
        {
            if (post != null) Object.Destroy(post);
        }

        [UnityTest]
        public IEnumerator 어두워지면_켜지고_밝아지면_꺼진다()
        {
            yield return null;
            Assert.IsFalse(lamp.Burning, "대낮인데 켜져 있습니다");

            clock.Daylight = 0.1f;
            yield return null;
            Assert.IsTrue(lamp.Burning, "어두워졌는데 안 켜집니다");

            clock.Daylight = 1f;
            yield return null;
            Assert.IsFalse(lamp.Burning, "밝아졌는데 안 꺼집니다");
        }

        [UnityTest]
        public IEnumerator 죽은_등은_어두워져도_안_켜진다()
        {
            lamp.Break();
            clock.Daylight = 0f;

            yield return null;

            Assert.IsFalse(lamp.Burning, "죽은 등이 켜졌습니다");
        }

        [UnityTest]
        public IEnumerator 켜진_등_아래가_밝다()
        {
            clock.Daylight = 0f;
            yield return null;

            float under = StreetLamp.LightAt(Vector3.zero);
            float edge = StreetLamp.LightAt(new Vector3(15f, 0f, 0f));
            float outside = StreetLamp.LightAt(new Vector3(60f, 0f, 0f));

            Assert.That(under, Is.EqualTo(1f).Within(0.001f), "등 바로 아래가 1 이어야 합니다");
            Assert.That(edge, Is.GreaterThan(0f).And.LessThan(under), "멀수록 어두워야 합니다");
            Assert.That(outside, Is.EqualTo(0f), "반경 밖은 0 이어야 합니다");
        }

        [UnityTest]
        public IEnumerator 꺼진_등은_밝히지_않는다()
        {
            clock.Daylight = 1f;
            yield return null;

            Assert.That(StreetLamp.LightAt(Vector3.zero), Is.EqualTo(0f),
                        "꺼진 등이 자리를 밝히고 있습니다");
        }

        [UnityTest]
        public IEnumerator 점등기가_죽은_등을_되살린다()
        {
            lamp.Break();
            clock.Daylight = 0f;

            GameObject robot = new GameObject("Lighter");
            robot.transform.position = new Vector3(2f, 0f, 0f);

            LampLighter lighter = robot.AddComponent<LampLighter>();
            lighter.search = 50f;
            lighter.reach = 8f;
            lighter.workSeconds = 0f;
            lighter.restSeconds = 0f;

            try
            {
                // 찾고 → 다가가고 → 손보고.
                for (int i = 0; i < 10 && lamp.broken; i++) yield return null;

                Assert.IsFalse(lamp.broken, "점등기가 등을 못 살렸습니다");
                Assert.That(lighter.Lit, Is.EqualTo(1), "살린 수가 안 맞습니다");

                yield return null;
                Assert.IsTrue(lamp.Burning, "살린 등이 어두운데도 안 켜집니다");
            }
            finally
            {
                Object.Destroy(robot);
            }
        }
    }
}
