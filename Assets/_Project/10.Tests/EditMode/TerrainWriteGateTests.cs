using NUnit.Framework;
using UnityEngine;
using CarDrive.Systems;

namespace CarDrive.Tests
{
    /// <summary>
    /// 사다리가 낸 값을 <b>지형에 옮기는 판단</b>을 고정합니다.
    ///
    /// <b>왜 이 테스트가 뒤늦게 생겼는가.</b> <see cref="ViewDistanceLadderTests"/> 가
    /// 거리 부등식을 지키고 있었는데도 같은 종류의 버그가 한 번 더 났습니다. 사다리는
    /// 옳은 값을 냈지만 <see cref="ViewRangeScaler"/> 가 <b>그 값을 지형에 적어 넣을지
    /// 판단하는 조건에서 한 축을 빠뜨렸습니다</b> — <c>rangeScale</c> 만 보고 있어서
    /// 날씨가 시야를 0.35배로 줄여도 "바뀐 것 없음"이었습니다.
    ///
    /// 즉 <b>계산을 한곳에 모으는 것만으로는 부족합니다.</b> 그 값이 실제로 나가는
    /// 길목도 함께 봐야 합니다. 이 테스트가 그 길목입니다.
    ///
    /// <see cref="ViewRangeScaler.NeedsTerrainWrite"/> 가 정적 상태를 읽지 않는 순수 함수라
    /// 씬도 지형도 없이 EditMode 에서 돌 수 있습니다.
    /// </summary>
    public class TerrainWriteGateTests
    {
        // --- Constants ---

        /// <summary>LOD 값은 이 테스트의 관심사가 아니므로 늘 같은 값을 넘깁니다.</summary>
        private const float Lod = 20f;

        /// <summary>베이스맵 거리도 마찬가지입니다.</summary>
        private const float Basemap = 100f;

        // --- Private Methods ---

        /// <summary>
        /// 나무 거리만 물어봅니다. LOD 쪽은 바뀌지 않은 것으로 두어,
        /// 참이 나오면 그 이유가 <b>나무 거리 하나뿐</b>이 되게 합니다.
        /// </summary>
        /// <param name="applied">지형에 적어 넣어 둔 나무 거리(m)</param>
        /// <param name="view">지금 시야 거리(m). 나무 거리와 같고 디더 끝은 그 97%입니다.</param>
        /// <returns>다시 적어 넣어야 하면 참</returns>
        private static bool Needs(float applied, float view)
        {
            // 사다리의 실제 관계를 그대로 씁니다 — TreeCut = View, FadeEnd = View × 0.97.
            return ViewRangeScaler.NeedsTerrainWrite(applied, view, view * 0.97f,
                                                     Lod, Lod, Basemap, Basemap);
        }

        // --- Tests ---

        /// <summary>
        /// 한 번도 쓴 적이 없으면 씁니다.
        ///
        /// 씬에 적혀 있던 값이 무엇이든 사다리에 맞춰 두어야 합니다.
        /// </summary>
        [Test]
        public void 처음에는_무조건_쓴다()
        {
            Assert.IsTrue(Needs(-1f, 340f), "첫 대입을 건너뛰면 씬에 적힌 값이 그대로 남습니다.");
        }

        /// <summary>
        /// <b>이 테스트가 놓쳤던 버그를 잡습니다.</b>
        ///
        /// 폭우가 시야를 0.35배로 줄이면 나무 거리도 함께 줄어야 합니다.
        /// 예전 판단은 <c>rangeScale</c> 만 보았고 날씨는 그 값을 건드리지 않으므로
        /// 여기서 거짓을 돌려주었습니다.
        /// </summary>
        [Test]
        public void 날씨가_시야를_줄이면_다시_쓴다()
        {
            // 맑은 날 340m 에 맞춰 두었는데 폭우로 시야가 119m 가 되었습니다.
            Assert.IsTrue(Needs(340f, 119f),
                "날씨가 시야를 줄였는데 나무 거리가 그대로 남습니다. 보이지 않는 곳을 계속 그립니다.");
        }

        /// <summary>
        /// 반대 방향이 더 급합니다. 적어 둔 거리가 디더가 끝나는 곳보다 가까우면
        /// 아직 다 지워지지 않은 나무가 통째로 잘립니다.
        /// </summary>
        [Test]
        public void 적어_둔_거리가_디더보다_가까우면_즉시_쓴다()
        {
            // 흐린 날 119m 에 맞춰 두었는데 날이 개어 시야가 340m 로 돌아왔습니다.
            // 디더는 330m 에서 끝나는데 지형은 119m 에서 자릅니다.
            Assert.IsTrue(Needs(119f, 340f),
                "디더가 끝나기 전에 나무가 잘립니다. 튀는 것이 보입니다.");
        }

        /// <summary>
        /// 시야가 그대로면 쓰지 않습니다. 이것이 지켜지지 않으면 이 판단 자체가 무의미합니다.
        /// </summary>
        [Test]
        public void 그대로면_쓰지_않는다()
        {
            Assert.IsFalse(Needs(340f, 340f), "바뀐 것이 없는데 지형 103장에 다시 대입합니다.");
        }

        /// <summary>
        /// <b>가까운 쪽에는 여유가 없어야 합니다.</b>
        ///
        /// 디더가 끝나는 곳(시야의 97%)보다 조금이라도 가까우면 잘리는 것이 보이므로,
        /// 아주 작은 차이에도 다시 써야 합니다.
        /// </summary>
        [TestCase(100f)]
        [TestCase(200f)]
        [TestCase(340f)]
        public void 디더_끝보다_가까우면_아무리_조금이라도_쓴다(float view)
        {
            // 디더 끝보다 1m 만 가깝게 둡니다.
            float applied = view * 0.97f - 1f;

            Assert.IsTrue(Needs(applied, view),
                "디더 끝보다 가까운데 그냥 둡니다. view=" + view + " applied=" + applied);
        }

        /// <summary>
        /// <b>먼 쪽에는 여유가 있어야 합니다.</b>
        ///
        /// 이미 다 지워진 나무를 그리는 것은 낭비일 뿐 화면은 멀쩡합니다.
        /// 여유가 없으면 날씨가 전이하는 15초 내내 0.5초마다 지형 103장에 대입하게 됩니다.
        /// </summary>
        [Test]
        public void 조금_멀기만_한_것은_그냥_둔다()
        {
            // 필요한 것보다 10% 멀지만 여유(15%) 안입니다.
            Assert.IsFalse(Needs(220f, 200f),
                "여유 안인데도 다시 씁니다. 전이 중에 대입이 몰립니다.");
        }

        /// <summary>
        /// 여유를 넘도록 멀면 그때는 씁니다. 낭비를 무한히 두지는 않습니다.
        /// </summary>
        [Test]
        public void 여유를_넘게_멀면_쓴다()
        {
            // 필요한 것보다 두 배 멉니다.
            Assert.IsTrue(Needs(400f, 200f), "두 배 먼 거리를 그대로 둡니다.");
        }

        /// <summary>
        /// <b>전이 한 번에 대입이 몇 번 일어나는지</b>를 고정합니다.
        ///
        /// 이 판단의 목적이 "따라가게 하는 것"과 "몰리지 않게 하는 것" 둘이라,
        /// 앞의 것만 테스트하면 여유를 0으로 줄여도 테스트가 통과합니다.
        /// 폭우가 밀려오는 동안(시야 340 → 119) 실제로 몇 번 쓰는지 세어 둡니다.
        /// </summary>
        [Test]
        public void 시야가_줄어드는_동안_대입이_몰리지_않는다()
        {
            float applied = 340f;
            int writes = 0;

            // 15초 전이를 0.5초 간격으로 흉내 냅니다. (대입 주기가 0.5초입니다)
            const int steps = 30;

            for (int i = 1; i <= steps; i++)
            {
                float view = Mathf.Lerp(340f, 119f, i / (float)steps);

                if (!Needs(applied, view)) continue;

                applied = view;
                writes++;
            }

            // 여유가 없으면 30번, 있으면 열 번 안쪽입니다.
            Assert.LessOrEqual(writes, 10,
                "전이 중 대입이 " + writes + "회입니다. 여유(TreeCutSlack)가 너무 좁습니다.");

            // 그러면서도 끝에는 따라잡아야 합니다.
            Assert.IsFalse(Needs(applied, 119f),
                "전이가 끝났는데 나무 거리가 아직 시야를 따라가지 못했습니다.");
        }

        /// <summary>
        /// 나무 거리가 그대로여도 LOD 값이 바뀌면 씁니다.
        /// 이 축은 사람이 설정을 만질 때만 움직이므로 여유를 두지 않습니다.
        /// </summary>
        [Test]
        public void LOD_값이_바뀌면_쓴다()
        {
            Assert.IsTrue(
                ViewRangeScaler.NeedsTerrainWrite(340f, 340f, 330f, Lod, 5f, Basemap, Basemap),
                "LOD 오차가 바뀌었는데 지형에 반영하지 않습니다.");

            Assert.IsTrue(
                ViewRangeScaler.NeedsTerrainWrite(340f, 340f, 330f, Lod, Lod, Basemap, 40f),
                "베이스맵 거리가 바뀌었는데 지형에 반영하지 않습니다.");
        }

        /// <summary>
        /// 런타임 반영을 꺼 두면 LOD 값이 -1 로 들어옵니다.
        /// 그 상태가 이어지는 동안에는 다시 쓰지 않아야 합니다.
        /// </summary>
        [Test]
        public void LOD_반영을_꺼_두면_그_상태를_유지한다()
        {
            Assert.IsFalse(
                ViewRangeScaler.NeedsTerrainWrite(340f, 340f, 330f, -1f, -1f, -1f, -1f),
                "런타임 반영을 껐는데도 계속 대입합니다.");
        }
    }
}
