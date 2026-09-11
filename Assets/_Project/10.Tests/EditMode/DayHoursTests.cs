using NUnit.Framework;
using CarDrive.Common;

namespace CarDrive.Tests
{
    /// <summary>
    /// 하루의 시각 구간 규칙입니다.
    ///
    /// <b>왜 따로 재는가.</b> 같은 규칙이 세 곳에 따로 적혀 있었습니다 — 가게의 영업
    /// 시간, 순찰기의 근무 시간, 점등기의 근무 시간. 한 곳으로 모았으니 검사도
    /// 한 곳에 있어야 합니다. 각자의 검사는 <b>그 부품이 이 규칙을 쓰는지</b>만 봅니다.
    /// </summary>
    public class DayHoursTests
    {
        [Test]
        public void 낮_구간은_그_사이에만_참이다()
        {
            Assert.IsFalse(DayHours.Within(6.9f, 7f, 18f));
            Assert.IsTrue(DayHours.Within(7f, 7f, 18f));
            Assert.IsTrue(DayHours.Within(17.9f, 7f, 18f));
            Assert.IsFalse(DayHours.Within(18f, 7f, 18f));
        }

        [Test]
        public void 자정을_넘긴_구간도_이어진다()
        {
            // ⚠ 이 게임은 밤 운전이 본편이라 이쪽이 오히려 기본입니다.
            Assert.IsTrue(DayHours.Within(21f, 20f, 6f));
            Assert.IsTrue(DayHours.Within(0f, 20f, 6f));
            Assert.IsTrue(DayHours.Within(5.9f, 20f, 6f));
            Assert.IsFalse(DayHours.Within(6f, 20f, 6f));
            Assert.IsFalse(DayHours.Within(12f, 20f, 6f));
        }

        [Test]
        public void 시작과_끝이_같으면_하루_종일이다()
        {
            // "쉬지 않는다" 를 적는 방법이 이것뿐입니다.
            Assert.IsTrue(DayHours.Within(0f, 9f, 9f));
            Assert.IsTrue(DayHours.Within(13f, 9f, 9f));
            Assert.IsTrue(DayHours.Within(23.9f, 9f, 9f));
        }

        [Test]
        public void 끝나는_시각은_빼고_센다()
        {
            // 붙어 있는 두 구간이 겹치지 않아야 합니다.
            Assert.IsTrue(DayHours.Within(11.99f, 6f, 12f));
            Assert.IsFalse(DayHours.Within(12f, 6f, 12f));
            Assert.IsTrue(DayHours.Within(12f, 12f, 18f));
        }
    }
}
