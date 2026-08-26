using NUnit.Framework;
using UnityEngine;
using CarDrive.Systems;

namespace CarDrive.Tests
{
    /// <summary>
    /// 청크 컬링의 <b>히스테리시스 규칙</b>을 고정합니다.
    ///
    /// <b>왜 이 테스트인가.</b> 이 규칙이 한 번 <b>통째로 사라진 적</b>이 있습니다.
    /// 지면과 나무·풀이 판정 하나를 나눠 쓰던 시절, 그 히스테리시스가 지면 상태에
    /// 묶여 있었는데 지면 컬링이 기본에서 꺼지자 그 상태가 <b>모든 타일에서 항상 참</b>이
    /// 되었습니다. 그 순간 나무·풀은 늘 넓은 경계 하나만 보게 되었고, 즉 켜는 기준과
    /// 끄는 기준이 같아졌습니다. 화면 밖 접기가 초당 112회까지 올라갔지만
    /// <b>화면에는 아무 이상이 없었습니다</b> — 계측을 붙이고 나서야 드러났습니다.
    ///
    /// 그런 종류의 회귀는 눈으로 잡히지 않습니다. 그래서 판정 규칙을
    /// <see cref="TerrainChunkCuller.StaysOn"/> 과 <see cref="TerrainChunkCuller.BuildBounds"/>
    /// 로 꺼내 두었고, 둘 다 정적 상태를 읽지 않는 순수 함수라 씬 없이 돕니다.
    /// </summary>
    public class TerrainCullingRuleTests
    {
        /// <summary>
        /// <b>끄는 경계는 켜는 경계를 품어야 합니다.</b>
        ///
        /// 이 포함 관계가 곧 히스테리시스입니다. 두 경계가 같아지면
        /// <see cref="TerrainChunkCuller.StaysOn"/> 이 아무리 옳아도 경계에 걸친 타일이
        /// 시야가 미세하게 흔들릴 때마다 껐다 켜기를 반복합니다.
        /// </summary>
        /// <param name="margin">화면 밖 캐스터를 담을 여유(m)</param>
        /// <param name="hysteresis">켜는 기준과 끄는 기준 사이의 간격(m)</param>
        [TestCase(55f, 20f)]
        [TestCase(150f, 20f)]
        [TestCase(50f, 60f)]
        public void 끄는_경계가_켜는_경계를_품는다(float margin, float hysteresis)
        {
            Bounds raw = new Bounds(new Vector3(50f, 30f, 50f), new Vector3(100f, 60f, 100f));

            Bounds padded, keep;
            TerrainChunkCuller.BuildBounds(raw, margin, hysteresis, out padded, out keep);

            Assert.Greater(padded.extents.x, raw.extents.x, "그림자 여유가 붙지 않았습니다.");

            Assert.IsTrue(keep.Contains(padded.min) && keep.Contains(padded.max),
                "끄는 경계가 켜는 경계를 품지 않습니다. 히스테리시스가 없는 것과 같습니다.");

            Assert.AreEqual(hysteresis, keep.extents.x - padded.extents.x, 0.01f,
                "두 경계의 간격이 설정한 히스테리시스와 다릅니다.");
        }

        /// <summary>
        /// 히스테리시스를 0으로 두면 두 경계가 하나가 됩니다.
        /// 떨림을 감수하고 그리는 범위를 줄이고 싶을 때의 선택지가 실제로 열려 있어야 합니다.
        /// </summary>
        [Test]
        public void 히스테리시스가_0이면_두_경계가_같아진다()
        {
            Bounds raw = new Bounds(Vector3.zero, new Vector3(100f, 60f, 100f));

            Bounds padded, keep;
            TerrainChunkCuller.BuildBounds(raw, 55f, 0f, out padded, out keep);

            Assert.AreEqual(padded.extents.x, keep.extents.x, 0.01f);
        }

        /// <summary>
        /// <b>꺼져 있던 지면은 가까운 거리에서 켜집니다.</b>
        /// 화면 밖이어도 발밑이면 켜 두어야 발밑에 구멍이 보이지 않습니다.
        /// </summary>
        [Test]
        public void 꺼진_지면이_가까워지면_켜진다()
        {
            // 들어오는 거리 160m, 풀려나는 거리 180m 입니다.
            float nearSqr = 160f * 160f;
            float releaseSqr = 180f * 180f;

            bool on = TerrainChunkCuller.StaysOn(false, false, 150f * 150f, nearSqr, releaseSqr);

            Assert.IsTrue(on, "가까운 지면이 화면 밖이라는 이유로 꺼진 채 있습니다.");
        }

        /// <summary>
        /// <b>켜져 있던 지면은 더 멀어져야 꺼집니다.</b>
        ///
        /// 들어오는 거리(160m)와 풀려나는 거리(180m) 사이에서는 <b>지금 상태가 유지</b>되어야
        /// 합니다. 같은 거리인데 상태에 따라 답이 달라지는 것이 히스테리시스의 전부입니다.
        /// </summary>
        /// <param name="distance">확인할 거리(m). 두 기준 사이의 값입니다.</param>
        [TestCase(165f)]
        [TestCase(170f)]
        [TestCase(179f)]
        public void 사이_구간에서는_지금_상태가_유지된다(float distance)
        {
            float nearSqr = 160f * 160f;
            float releaseSqr = 180f * 180f;
            float sqr = distance * distance;

            bool wasOn = TerrainChunkCuller.StaysOn(true, false, sqr, nearSqr, releaseSqr);
            bool wasOff = TerrainChunkCuller.StaysOn(false, false, sqr, nearSqr, releaseSqr);

            Assert.IsTrue(wasOn, "켜져 있던 지면이 풀려나는 거리 전에 꺼졌습니다.");
            Assert.IsFalse(wasOff, "꺼져 있던 지면이 들어오는 거리 밖에서 켜졌습니다.");
        }

        /// <summary>
        /// 풀려나는 거리를 넘고 화면 밖이면 비로소 꺼집니다.
        /// </summary>
        [Test]
        public void 멀고_화면_밖이면_꺼진다()
        {
            float nearSqr = 160f * 160f;
            float releaseSqr = 180f * 180f;

            bool on = TerrainChunkCuller.StaysOn(true, false, 200f * 200f, nearSqr, releaseSqr);

            Assert.IsFalse(on, "멀고 화면 밖인 지면이 계속 켜져 있습니다.");
        }

        /// <summary>
        /// <b>화면 안이면 아무리 멀어도 켭니다.</b> 거리로는 끄지 않습니다 —
        /// 그쪽은 나무·풀 그리는 거리가 이미 반경으로 잘라 냅니다.
        /// </summary>
        [Test]
        public void 화면_안이면_멀어도_켠다()
        {
            float nearSqr = 160f * 160f;
            float releaseSqr = 180f * 180f;

            bool on = TerrainChunkCuller.StaysOn(false, true, 5000f * 5000f, nearSqr, releaseSqr);

            Assert.IsTrue(on, "화면 안인 지면이 거리 때문에 꺼졌습니다.");
        }
    }
}
