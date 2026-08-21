using NUnit.Framework;
using UnityEngine;
using CarDrive.Systems;

namespace CarDrive.Tests
{
    /// <summary>
    /// 시야 거리 사다리의 <b>순서 불변식</b>을 고정합니다.
    ///
    /// <b>왜 이 테스트인가.</b> 이 부등식이 뒤집히면 그 자리가 화면에 <b>보이는 선</b>이 됩니다.
    /// 실제로 같은 종류의 버그가 세 번 났고, 세 번 다 "한 축에 배율을 빠뜨린" 실수였습니다.
    /// <see cref="ViewDistances.Ladder"/> 가 계산을 한곳에 모은 것이 첫 번째 방어이고,
    /// 이 테스트가 두 번째입니다. 눈으로 확인하려면 게임을 띄우고 날씨를 바꿔 가며
    /// 지평선을 봐야 하는데, 그 확인은 <b>반복되지 않습니다.</b>
    ///
    /// <see cref="ViewDistances.Ladder"/> 가 정적 상태를 읽지 않는 순수 구조체라
    /// 씬 없이 EditMode 에서 돌 수 있습니다.
    /// </summary>
    public class ViewDistanceLadderTests
    {
        /// <summary>테스트에 쓸 설정입니다. 에셋을 건드리지 않도록 메모리에만 만듭니다.</summary>
        private CarDriveWorldSettings settings;

        /// <summary>기본값이 든 설정을 하나 만듭니다.</summary>
        [SetUp]
        public void SetUp()
        {
            settings = ScriptableObject.CreateInstance<CarDriveWorldSettings>();
        }

        /// <summary>만든 설정을 치웁니다.</summary>
        [TearDown]
        public void TearDown()
        {
            if (settings != null) Object.DestroyImmediate(settings);
        }

        /// <summary>
        /// 사다리 한 벌을 만듭니다. 씬의 기준값(파클립 340, 활성 360, 즉시 340)을 씁니다.
        /// </summary>
        /// <param name="scale">전체 거리 배율</param>
        /// <param name="weatherFog">날씨가 요청한 안개 짙기</param>
        /// <param name="weatherView">날씨가 요청한 시야 배율</param>
        /// <returns>계산된 사다리</returns>
        private ViewDistances.Ladder Build(float scale, float weatherFog = 0f, float weatherView = 1f)
        {
            settings.rangeScale = scale;
            return new ViewDistances.Ladder(settings, 340f, 360f, 340f, weatherFog, weatherView);
        }

        /// <summary>
        /// 어떤 배율에서도 페이드가 잘라내기보다 <b>먼저</b> 끝나야 합니다.
        /// 뒤집히면 나무가 디더로 사라지기 전에 통째로 잘려 눈앞에서 튀어나옵니다.
        /// </summary>
        /// <param name="scale">확인할 전체 거리 배율</param>
        [TestCase(1.0f)]
        [TestCase(0.75f)]
        [TestCase(0.5f)]
        [TestCase(0.25f)]
        public void 페이드가_잘라내기보다_먼저_끝난다(float scale)
        {
            ViewDistances.Ladder l = Build(scale);

            Assert.Less(l.FadeStart, l.FadeEnd, "페이드 시작이 끝보다 뒤에 있습니다.");
            Assert.Less(l.FadeEnd, l.TreeCut, "디더가 다 지워지기 전에 나무가 잘립니다.");
        }

        /// <summary>
        /// 타일 단위 접기는 나무 잘라내기보다 <b>멀어야</b> 합니다.
        /// 접기는 하드 스위치라, 이보다 가까우면 디더가 돌기도 전에 한 장 분량이 통째로 사라집니다.
        /// </summary>
        /// <param name="scale">확인할 전체 거리 배율</param>
        [TestCase(1.0f)]
        [TestCase(0.5f)]
        [TestCase(0.25f)]
        public void 접기가_잘라내기보다_멀다(float scale)
        {
            ViewDistances.Ladder l = Build(scale);

            Assert.GreaterOrEqual(l.FoliageFold, l.TreeCut, "접는 거리가 나무 그리는 거리보다 가깝습니다.");
            Assert.Greater(l.FoliageRelease, l.FoliageFold, "켜는 기준과 끄는 기준 사이에 간격이 없습니다.");
        }

        /// <summary>
        /// 파클립은 켜져 있는 타일의 <b>먼 쪽 모서리</b>까지 담아야 합니다.
        /// 그러지 않으면 켜진 타일의 뒤쪽이 잘려 하늘이 뚫려 보입니다.
        /// </summary>
        /// <param name="scale">확인할 전체 거리 배율</param>
        [TestCase(1.0f)]
        [TestCase(0.5f)]
        [TestCase(0.25f)]
        public void 파클립이_켜진_타일을_자르지_않는다(float scale)
        {
            ViewDistances.Ladder l = Build(scale);

            Assert.Greater(l.FarClip, l.TerrainActive, "파클립이 타일 활성 거리보다 가깝습니다.");
            Assert.GreaterOrEqual(l.FarClip, l.View, "파클립이 안개가 걷히는 거리보다 가깝습니다.");
        }

        /// <summary>
        /// 즉시 활성 거리는 활성 거리를 넘지 않아야 합니다.
        /// 넘으면 "켜지지 않은 타일을 즉시 켜라"는 모순이 됩니다.
        /// </summary>
        [Test]
        public void 즉시_활성_거리가_활성_거리를_넘지_않는다()
        {
            ViewDistances.Ladder l = Build(1f);

            Assert.LessOrEqual(l.TerrainInstant, l.TerrainActive);
        }

        /// <summary>
        /// 타일을 <b>끄는</b> 거리는 켜는 거리보다 멀어야 합니다.
        ///
        /// 같으면 경계에 걸친 타일이 플레이어가 그 선을 오갈 때마다 <c>SetActive</c> 를
        /// 반복합니다. 그것이 이 프로젝트에서 가장 비싼 토글이고, 예산제로도 막히지 않습니다.
        /// (예산제는 여러 장이 몰리는 것을 막지, 같은 한 장이 반복되는 것을 막지 못합니다)
        /// </summary>
        /// <param name="scale">확인할 전체 거리 배율</param>
        [TestCase(1.0f)]
        [TestCase(0.5f)]
        [TestCase(0.25f)]
        public void 타일을_끄는_거리가_켜는_거리보다_멀다(float scale)
        {
            ViewDistances.Ladder l = Build(scale);

            Assert.Greater(l.TerrainActiveRelease, l.TerrainActive,
                "타일 스트리밍에 히스테리시스가 없습니다. 경계에서 같은 타일이 반복 토글됩니다.");
        }

        /// <summary>
        /// 히스테리시스를 0으로 두면 켜는 거리와 끄는 거리가 같아집니다.
        /// 떨림을 감수하고 메모리를 아끼고 싶을 때의 선택지가 실제로 열려 있어야 합니다.
        /// </summary>
        [Test]
        public void 히스테리시스를_0으로_두면_경계가_하나가_된다()
        {
            settings.tileStreamingHysteresis = 0f;
            ViewDistances.Ladder l = Build(1f);

            Assert.AreEqual(l.TerrainActive, l.TerrainActiveRelease, 0.01f);
        }

        /// <summary>
        /// 파클립은 <b>꺼지지 않고 남아 있는</b> 타일까지 담아야 합니다.
        ///
        /// 히스테리시스 구간의 타일은 활성 거리 밖에 있지만 아직 켜져 있습니다.
        /// 파클립을 활성 거리 기준으로 잡으면 그 타일들의 뒤쪽이 잘려 하늘이 뚫려 보입니다.
        /// </summary>
        /// <param name="scale">확인할 전체 거리 배율</param>
        [TestCase(1.0f)]
        [TestCase(0.5f)]
        [TestCase(0.25f)]
        public void 파클립이_히스테리시스_구간의_타일까지_담는다(float scale)
        {
            ViewDistances.Ladder l = Build(scale);

            Assert.Greater(l.FarClip, l.TerrainActiveRelease,
                "히스테리시스로 남아 있는 타일이 파클립 밖에 있습니다.");
        }

        /// <summary>
        /// 안개는 <b>어떤 경우에도</b> 시야 거리를 덮을 만큼은 짙어야 합니다.
        /// 날씨가 더 옅게 요청해도 그 요청이 이기면 지형이 끝나는 자리가 그대로 보입니다.
        /// </summary>
        [Test]
        public void 날씨가_옅게_요청해도_안개가_시야를_덮는다()
        {
            ViewDistances.Ladder l = Build(1f, weatherFog: 0.0001f);

            // 시야 거리에서 약 95% 를 덮으려면 (거리 × 짙기) 가 1.73 이어야 합니다.
            Assert.GreaterOrEqual(l.FogDensity * l.View, 1.7f,
                "날씨의 옅은 안개 요청이 시야를 덮는 최소 짙기를 밀어냈습니다.");
        }

        /// <summary>
        /// 날씨가 더 짙게 요청하면 그쪽을 씁니다. 안개 날씨가 맑음과 같아 보이면 안 됩니다.
        /// </summary>
        [Test]
        public void 날씨가_짙게_요청하면_그것을_쓴다()
        {
            ViewDistances.Ladder clear = Build(1f);
            ViewDistances.Ladder foggy = Build(1f, weatherFog: clear.FogDensity * 3f);

            Assert.Greater(foggy.FogDensity, clear.FogDensity,
                "날씨의 짙은 안개 요청이 반영되지 않았습니다.");
        }

        /// <summary>
        /// 날씨가 시야를 줄이면 사다리 전체가 함께 줄되, <b>순서는 그대로여야</b> 합니다.
        /// 한 축만 줄면 그 자리가 보이는 선이 됩니다.
        /// </summary>
        [Test]
        public void 날씨가_시야를_줄여도_순서가_유지된다()
        {
            ViewDistances.Ladder l = Build(1f, weatherView: 0.4f);

            Assert.Less(l.FadeStart, l.FadeEnd);
            Assert.Less(l.FadeEnd, l.TreeCut);
            Assert.GreaterOrEqual(l.FoliageFold, l.TreeCut);
            Assert.Greater(l.FarClip, l.TerrainActive);

            ViewDistances.Ladder clear = Build(1f);
            Assert.Less(l.View, clear.View, "날씨가 시야를 줄이지 못했습니다.");
        }

        /// <summary>
        /// 날씨는 시야를 <b>넓히지 못합니다.</b>
        /// 씬이 적어 둔 거리보다 멀리 보이면 타일이 없는 곳까지 보게 됩니다.
        /// </summary>
        [Test]
        public void 날씨가_시야를_넓히지_못한다()
        {
            ViewDistances.Ladder clear = Build(1f);
            ViewDistances.Ladder overreach = Build(1f, weatherView: 5f);

            Assert.AreEqual(clear.View, overreach.View, 0.01f);
        }

        /// <summary>
        /// 아무리 나쁜 날씨여도 시야가 바닥 아래로 내려가지 않아야 합니다.
        /// 지형이 발밑까지 와서야 보이면 연출이 아니라 고장으로 보입니다.
        /// </summary>
        [Test]
        public void 시야가_바닥_아래로_내려가지_않는다()
        {
            ViewDistances.Ladder worst = Build(1f, weatherView: 0f);

            // 하한(0.35)보다 아래로는 내려가지 않습니다.
            Assert.GreaterOrEqual(worst.View, 340f * 0.35f - 0.01f);
        }

        /// <summary>
        /// 배율을 낮추면 모든 거리가 함께 줄어야 합니다.
        /// 한 축만 줄어서 순서가 뒤집혔던 것이 이 사다리를 만든 이유입니다.
        /// </summary>
        [Test]
        public void 배율을_낮추면_거리가_함께_줄어든다()
        {
            ViewDistances.Ladder full = Build(1f);
            ViewDistances.Ladder half = Build(0.5f);

            Assert.Less(half.View, full.View);
            Assert.Less(half.TreeCut, full.TreeCut);
            Assert.Less(half.FadeStart, full.FadeStart);
            Assert.Less(half.Grass, full.Grass);
            Assert.Less(half.TerrainActive, full.TerrainActive);
            Assert.Less(half.TerrainNear, full.TerrainNear);
        }
    }
}
