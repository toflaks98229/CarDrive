using NUnit.Framework;
using UnityEngine;
using CarDrive.Gameplay;

namespace CarDrive.Tests
{
    /// <summary>
    /// <see cref="LookMood.Blend"/> 의 규칙만 봅니다.
    ///
    /// 위협은 잉크로, 피로는 거칠어짐으로 말합니다. 목소리가 갈려 있어야
    /// 화면이 무거워진 이유를 읽을 수 있습니다.
    ///
    /// 로봇도 날씨도 세우지 않습니다. 이 함수가 이 컴포넌트에서 유일하게 판단이
    /// 들어 있는 곳이고, 나머지는 그 값을 읽어 눌러서 전역에 넣는 배관입니다.
    /// </summary>
    public class LookMoodTests
    {
        private const float Threat = 0.55f;
        private const float Fatigue = 0.45f;
        private const float Rain = 0.25f;
        private const float Coarse = 12f;

        private static Vector3 Blend(float t, float f, float r)
        {
            return LookMood.Blend(t, f, r, Threat, Fatigue, Rain, Coarse);
        }

        [Test]
        public void 아무_일도_없으면_그림을_안_건드린다()
        {
            Vector3 v = Blend(0f, 0f, 0f);

            Assert.That(v.x, Is.EqualTo(0f).Within(1e-5f), "획을 더 긋지 않아야 합니다");
            Assert.That(v.y, Is.EqualTo(0f).Within(1e-5f), "채도 밀도도 그대로여야 합니다");
            Assert.That(v.z, Is.EqualTo(0f).Within(1e-5f), "계단도 그대로여야 합니다");
        }

        [Test]
        public void 로봇이_겨누면_획이_더_그어진다()
        {
            Assert.That(Blend(1f, 0f, 0f).x, Is.EqualTo(Threat).Within(1e-5f));

            // 기분이 중간이면 덤도 중간입니다.
            Assert.That(Blend(0.5f, 0f, 0f).x, Is.EqualTo(Threat * 0.5f).Within(1e-5f));
        }

        [Test]
        public void 지치면_그림이_거칠어진다()
        {
            Vector3 v = Blend(0f, 1f, 0f);

            Assert.That(v.y, Is.EqualTo(Fatigue).Within(1e-5f));
            Assert.That(v.z, Is.EqualTo(Coarse).Within(1e-5f), "계단이 줄어야 합니다");
            Assert.That(v.x, Is.EqualTo(0f).Within(1e-5f), "피로는 획을 직접 긋지 않습니다");
        }

        [Test]
        public void 위협과_비가_겹쳐도_한_사람_몫을_안_넘는다()
        {
            // ⚠ 이것이 없으면 비 오는 날 겨눔을 당할 때 화면을 못 봅니다.
            Vector3 both = Blend(1f, 0f, 1f);

            Assert.That(both.x, Is.EqualTo(Mathf.Max(Threat, Rain)).Within(1e-5f));
            Assert.That(both.x, Is.LessThan(Threat + Rain), "그냥 더하면 안 됩니다");
        }

        [Test]
        public void 비만_와도_획이_그어진다()
        {
            Assert.That(Blend(0f, 0f, 1f).x, Is.EqualTo(Rain).Within(1e-5f));
        }
    }
}
