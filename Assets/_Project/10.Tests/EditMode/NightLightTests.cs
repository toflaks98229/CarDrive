using NUnit.Framework;
using UnityEngine;
using CarDrive.Systems;

namespace CarDrive.Tests
{
    /// <summary>
    /// 빛이 <b>천장에서 내려오는지</b> 봅니다.
    ///
    /// ⚠ <b>이 세계에는 하늘이 없습니다.</b> 머리 위는 거대구조물이고 하늘처럼 보이는
    /// 것은 그 천장을 구운 파노라마입니다. 그러니 빛의 근원은 해도 달도 아닌
    /// <b>천장의 등불</b>이고, 천장은 하루 종일 같은 자리에 있습니다 —
    /// <b>그림자의 방향은 시간이 가도 바뀌지 않아야 합니다.</b>
    ///
    /// 씬도 빛도 없이 확인할 수 있어야 하는 규칙이라 밖으로 꺼내 두고 여기서 잽니다.
    /// </summary>
    public class NightLightTests
    {
        [Test]
        public void 한낮이_가장_서고_한밤이_가장_눕는다()
        {
            float noon = SkyController.CeilingPitch(1f, 65f, 6f);
            float night = SkyController.CeilingPitch(0f, 65f, 6f);

            Assert.That(noon, Is.EqualTo(71f).Within(0.001f));
            Assert.That(night, Is.EqualTo(59f).Within(0.001f));
        }

        [Test]
        public void 한낮과_한밤의_한가운데가_기준이다()
        {
            Assert.That(SkyController.CeilingPitch(0.5f, 65f, 6f), Is.EqualTo(65f).Within(0.001f));
        }

        [Test]
        public void 하루_내내_아침_해처럼_눕지_않는다()
        {
            // 게임이 시작하는 08시의 해는 지평선 30도 위였고, 그것이 대낮에 그림자가
            // 길게 눕던 원인이었습니다. 이제 어느 시각에도 그보다 훨씬 섭니다.
            for (int i = 0; i <= 10; i++)
            {
                float pitch = SkyController.CeilingPitch(i / 10f, 65f, 6f);

                Assert.That(pitch, Is.GreaterThan(45f),
                            "밝기 " + (i / 10f).ToString("F1") + " 에서 " + pitch.ToString("F0")
                            + "도까지 누웠습니다 — 아침 해가 돌아왔습니다");
            }
        }

        [Test]
        public void 숨_폭이_0_이면_하루가_고정이다()
        {
            Assert.That(SkyController.CeilingPitch(0f, 65f, 0f), Is.EqualTo(65f));
            Assert.That(SkyController.CeilingPitch(1f, 65f, 0f), Is.EqualTo(65f));
        }

        [Test]
        public void 지평선_아래로는_내려가지_않는다()
        {
            // ⚠ 90도를 넘기면 빛이 반대편에서 올라오고, 10도 아래로 누우면 아침 해가 됩니다.
            Assert.That(SkyController.CeilingPitch(1f, 88f, 20f), Is.EqualTo(90f));
            Assert.That(SkyController.CeilingPitch(0f, 12f, 20f), Is.EqualTo(10f));
        }

        [Test]
        public void 밝기가_올라가면_기울기도_같이_올라간다()
        {
            float previous = -1f;

            for (int i = 0; i <= 10; i++)
            {
                float pitch = SkyController.CeilingPitch(i / 10f, 65f, 6f);

                Assert.That(pitch, Is.GreaterThanOrEqualTo(previous),
                            "밝아지는데 빛이 도로 누웠습니다 — 밝기 " + (i / 10f).ToString("F1"));
                previous = pitch;
            }
        }
    }
}
