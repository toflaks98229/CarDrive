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
        /// <param name="grassSpeedScale">속도에 따른 풀 거리 배율. 1이면 줄이지 않습니다.</param>
        /// <returns>계산된 사다리</returns>
        private ViewDistances.Ladder Build(float scale, float weatherFog = 0f, float weatherView = 1f,
                                           float grassSpeedScale = 1f)
        {
            settings.rangeScale = scale;
            return new ViewDistances.Ladder(settings, 340f, 360f, 340f, weatherFog, weatherView, grassSpeedScale);
        }

        /// <summary>
        /// 어떤 배율·어떤 속도 단계에서도 풀 페이드가 <b>잘라내기보다 먼저</b> 끝나야 합니다.
        ///
        /// <b>왜 이 테스트인가.</b> 이 부등식이 실제로 뒤집혀 있었습니다. 페이드 창이
        /// <c>detailDistance</c> 70m 기준으로 재질에 구워져 있었고(35m / 68.6m),
        /// 그 숫자는 <c>rangeScale</c> 도 속도 단계도 몰랐습니다. 실제로 그리는 거리는
        /// 0.7 배율에서 49m 이고 시속 90 이상이면 24.5m 까지 줄어드는데, 24.5m 는
        /// 페이드가 <b>시작도 하기 전</b>이라 풀이 100% 키로 선 채 잘렸습니다.
        ///
        /// 눈으로 확인하려면 차를 몰고 시속 90 을 넘긴 채 지평선을 봐야 하는데,
        /// 그 확인은 반복되지 않습니다.
        /// </summary>
        /// <param name="scale">확인할 전체 거리 배율</param>
        /// <param name="grassSpeed">확인할 속도 단계 배율 (1 · 0.75 · 0.5 이 실제 단계입니다)</param>
        [TestCase(1.0f, 1.0f)]
        [TestCase(1.0f, 0.5f)]
        [TestCase(0.7f, 1.0f)]
        [TestCase(0.7f, 0.75f)]
        [TestCase(0.7f, 0.5f)]
        [TestCase(0.25f, 0.5f)]
        public void 풀_페이드가_풀_잘라내기보다_먼저_끝난다(float scale, float grassSpeed)
        {
            ViewDistances.Ladder l = Build(scale, grassSpeedScale: grassSpeed);

            Assert.Less(l.GrassFadeStart, l.GrassFadeEnd, "풀 페이드 시작이 끝보다 뒤에 있습니다.");
            Assert.Less(l.GrassFadeEnd, l.Grass, "다 지워지기 전에 풀이 잘립니다.");
        }

        /// <summary>
        /// 속도 단계는 풀 거리에 <b>정확히 한 번</b> 곱해져야 합니다.
        ///
        /// 예전에는 <see cref="TerrainDetailLod"/> 가 사다리 밖에서 곱했습니다.
        /// 곱하는 자리를 안으로 옮기면서 <b>양쪽에서 곱해 제곱이 되는</b> 실수를 막습니다.
        /// (컬러가 <c>treeDistance</c> 에 한 번 그렇게 했고, 그것이 위의 3번 버그입니다)
        /// </summary>
        [Test]
        public void 속도_단계가_풀_거리에_한_번만_곱해진다()
        {
            ViewDistances.Ladder full = Build(0.7f, grassSpeedScale: 1f);
            ViewDistances.Ladder half = Build(0.7f, grassSpeedScale: 0.5f);

            Assert.AreEqual(full.Grass * 0.5f, half.Grass, 0.01f,
                "속도 단계가 두 번 곱해졌거나 아예 빠졌습니다.");
        }

        /// <summary>
        /// 속도 단계는 <b>풀만</b> 줄입니다. 나무·시야·파클립은 그대로여야 합니다.
        ///
        /// 빠를 때 풀을 줄이는 것은 안개와 흐름이 가려 주기 때문인데, 나무는 지평선의
        /// 실루엣이라 같은 논리가 통하지 않습니다. 함께 줄면 속도를 올리고 내릴 때마다
        /// 먼 나무가 나타났다 사라집니다.
        /// </summary>
        [Test]
        public void 속도_단계가_풀_말고는_건드리지_않는다()
        {
            ViewDistances.Ladder full = Build(0.7f, grassSpeedScale: 1f);
            ViewDistances.Ladder slow = Build(0.7f, grassSpeedScale: 0.5f);

            Assert.AreEqual(full.View, slow.View, 0.01f, "속도 단계가 시야를 건드렸습니다.");
            Assert.AreEqual(full.TreeCut, slow.TreeCut, 0.01f, "속도 단계가 나무 거리를 건드렸습니다.");
            Assert.AreEqual(full.FarClip, slow.FarClip, 0.01f, "속도 단계가 파클립을 건드렸습니다.");
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
        /// 그림자는 <b>시야를 넘지 못합니다.</b>
        ///
        /// 안개에 다 묻히는 거리까지 그려도 보이지 않고, 섀도맵 해상도만 그만큼 넓게 퍼져
        /// 가까운 그림자가 거칠어집니다.
        /// </summary>
        /// <param name="scale">확인할 전체 거리 배율</param>
        [TestCase(1.0f)]
        [TestCase(0.5f)]
        [TestCase(0.25f)]
        public void 그림자가_시야를_넘지_않는다(float scale)
        {
            ViewDistances.Ladder l = Build(scale);

            Assert.LessOrEqual(l.Shadow, l.View,
                "안개에 다 묻히는 거리까지 그림자를 그리고 있습니다.");
        }

        /// <summary>
        /// 그림자 거리가 시야보다 길면 <b>시야에서 잘려야</b> 합니다.
        ///
        /// 날씨가 시야를 줄이면 실제로 이 상황이 됩니다. URP 에셋에 적힌 값을 그대로 쓰면
        /// 안개에 다 묻힌 거리까지 섀도맵을 펼치게 되고, 그만큼 가까운 그림자가 거칠어집니다.
        /// </summary>
        [Test]
        public void 그림자_거리가_시야보다_길면_시야에서_잘린다()
        {
            settings.shadowDistance = 300f;

            // 폭우로 시야가 절반이 된 상황입니다. 시야 170m, 그림자 요구 300m.
            ViewDistances.Ladder l = Build(1f, weatherView: 0.5f);

            Assert.AreEqual(l.View, l.Shadow, 0.01f,
                "시야보다 긴 그림자 거리가 잘리지 않았습니다.");
        }

        /// <summary>
        /// <b>화면 밖 캐스터를 담을 여유는 그림자 거리 이상이어야 합니다.</b>
        ///
        /// 화면 밖 언덕이 화면 안으로 그림자를 드리울 수 있는 거리가 정확히 그림자 거리입니다.
        /// 그보다 좁게 자르면 화면 가장자리에서 그림자가 통째로 사라집니다.
        ///
        /// 예전에는 이 여유가 설정에 손으로 적힌 55m 하나였고 실제 그림자 거리를 몰랐습니다.
        /// URP 에셋의 50m 와 우연히 맞아떨어져 있었을 뿐이라, 품질을 올리면 어긋났습니다.
        /// </summary>
        /// <param name="shadowDistance">확인할 그림자 기준 거리</param>
        [TestCase(50f)]
        [TestCase(150f)]
        [TestCase(300f)]
        public void 그림자_여유가_그림자_거리_이상이다(float shadowDistance)
        {
            settings.shadowDistance = shadowDistance;
            ViewDistances.Ladder l = Build(1f);

            Assert.GreaterOrEqual(l.ShadowCasterMargin, l.Shadow,
                "여유가 그림자 거리보다 좁습니다. 화면 가장자리에서 그림자가 사라집니다.");
        }

        /// <summary>
        /// 그림자가 짧아도 설정의 하한은 지켜야 합니다.
        /// 먼 언덕의 실루엣처럼 그림자 말고도 남겨 두고 싶은 것이 있습니다.
        /// </summary>
        [Test]
        public void 그림자가_짧으면_설정의_하한을_쓴다()
        {
            settings.shadowDistance = 10f;
            settings.shadowMargin = 55f;

            ViewDistances.Ladder l = Build(1f);

            Assert.AreEqual(55f, l.ShadowCasterMargin, 0.01f);
        }

        /// <summary>
        /// 안개는 <b>어떤 경우에도</b> 맑은 날의 바닥값보다 옅어지지 않아야 합니다.
        /// 날씨가 더 옅게 요청해도 그 요청이 이기면 지형이 끝나는 자리가 그대로 보입니다.
        ///
        /// <b>왜 숫자를 직접 적지 않는가.</b> 예전에는 <c>짙기 × 시야 &gt;= 1.7</c> 로 적어 두었는데,
        /// 그 1.7 은 <c>FogReachFactor</c> 상수를 손으로 베낀 값이라 상수를 조정하는 순간
        /// <b>의도는 그대로인데 테스트만 깨졌습니다.</b> 실제로 그렇게 한 번 깨졌습니다.
        /// 이 테스트가 지키려던 것은 특정 숫자가 아니라 "날씨의 옅은 요청이 바닥을 밀어내지 못한다"
        /// 는 규칙이므로, 맑은 날과 비교하는 쪽으로 바꿉니다. 상수를 바꿔도 규칙은 계속 지켜집니다.
        /// </summary>
        [Test]
        public void 날씨가_옅게_요청해도_안개가_바닥값을_지킨다()
        {
            ViewDistances.Ladder clear = Build(1f);
            ViewDistances.Ladder thin = Build(1f, weatherFog: 0.0001f);

            Assert.AreEqual(clear.FogDensity, thin.FogDensity, 1e-6f,
                "날씨의 옅은 안개 요청이 맑은 날의 최소 짙기를 밀어냈습니다.");
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
