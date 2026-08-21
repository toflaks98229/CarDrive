using NUnit.Framework;
using CarDrive.Systems;

namespace CarDrive.Tests
{
    /// <summary>
    /// 계측기의 <b>결정적인 부분</b>만 고정합니다.
    ///
    /// 창을 닫는 시점은 <c>Time.realtimeSinceStartup</c> 에 달려 있어 EditMode 에서
    /// 흉내 내기 어렵습니다. 그쪽까지 억지로 검사하려고 시간을 주입 가능하게 만들면
    /// <b>계측기가 계측 대상보다 복잡해집니다.</b> 여기서는 세는 것과 비우는 것만 봅니다.
    /// </summary>
    public class WorldProfilerTests
    {
        /// <summary>검사 사이에 값이 새지 않도록 비웁니다.</summary>
        [SetUp]
        public void SetUp()
        {
            WorldProfiler.Reset();
        }

        /// <summary>
        /// 항목을 하나 늘렸을 때 배열이 그대로 남으면 <b>새 항목을 처음 세는 순간</b>
        /// 범위를 벗어납니다. 그 실수는 한참 뒤에야 드러나므로 여기서 막아 둡니다.
        /// </summary>
        [Test]
        public void 모든_항목을_범위_밖_오류_없이_셀_수_있다()
        {
            foreach (WorldProfiler.Counter counter in System.Enum.GetValues(typeof(WorldProfiler.Counter)))
            {
                Assert.DoesNotThrow(() => WorldProfiler.Count(counter),
                    counter + " 를 세다가 실패했습니다. counts 배열 크기를 확인하세요.");
            }
        }

        /// <summary>비운 직후에는 모든 항목이 0이어야 합니다.</summary>
        [Test]
        public void 비우면_모든_항목이_0이_된다()
        {
            WorldProfiler.Count(WorldProfiler.Counter.TileActivated, 10);
            WorldProfiler.Reset();

            foreach (WorldProfiler.Counter counter in System.Enum.GetValues(typeof(WorldProfiler.Counter)))
            {
                Assert.AreEqual(0f, WorldProfiler.PerSecond(counter), 0.0001f, counter + " 가 비워지지 않았습니다.");
            }

            Assert.AreEqual(0, WorldProfiler.HitchTotal);
            Assert.AreEqual(0, WorldProfiler.BadHitchTotal);
            Assert.AreEqual(0f, WorldProfiler.WorstFrame, 0.0001f);
        }

        /// <summary>
        /// 0 이하를 더하라고 하면 아무 일도 없어야 합니다.
        /// 호출부가 "이번엔 없음"을 그대로 넘길 수 있어야 분기가 줄어듭니다.
        /// </summary>
        [Test]
        public void 음수나_0을_더하면_아무_일도_없다()
        {
            Assert.DoesNotThrow(() => WorldProfiler.Count(WorldProfiler.Counter.TileActivated, 0));
            Assert.DoesNotThrow(() => WorldProfiler.Count(WorldProfiler.Counter.TileActivated, -5));
        }
    }
}
