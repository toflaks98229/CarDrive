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
            WorldProfiler.ResetAll();
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
        /// <b>로딩 프레임이 플레이 최악값을 오염시키면 안 됩니다.</b>
        ///
        /// 씬을 불러오고 첫 프레임을 그리는 동안에는 수백 ms 짜리 프레임이 반드시 나옵니다.
        /// 그 값이 세션 최악에 섞이면 <b>그 뒤로 어떤 숫자를 봐도 의미가 없습니다</b> —
        /// 플레이 중에 40ms 가 나오든 400ms 가 나오든 최악값은 로딩 프레임 그대로입니다.
        /// 실제로 그 상태로 한 번 계측했다가 "신뢰할 수 없다"는 지적을 받았습니다.
        /// </summary>
        [Test]
        public void 기동_구간_프레임은_플레이_최악값을_오염시키지_않는다()
        {
            // 첫 기록은 언제나 기동 구간입니다. (경과 시간이 0초이므로)
            WorldProfiler.Tick(0.5f);   // 500ms 짜리 로딩 프레임

            Assert.Greater(WorldProfiler.StartupWorstMs, 400f,
                "기동 구간 최악값에 기록되지 않았습니다.");

            Assert.AreEqual(0f, WorldProfiler.WorstFrame, 0.0001f,
                "로딩 프레임이 플레이 최악값에 섞였습니다.");
            Assert.AreEqual(0, WorldProfiler.HitchTotal,
                "로딩 프레임이 끊김으로 세어졌습니다.");
            Assert.AreEqual(0, WorldProfiler.BadHitchTotal);
        }

        /// <summary>
        /// 손으로 비우는 것은 "지금부터 다시 보겠다"는 뜻이지
        /// "기동에 얼마나 걸렸는지 잊겠다"는 뜻이 아닙니다.
        /// </summary>
        [Test]
        public void 비우기는_기동_구간_값을_남기고_전체_비우기는_지운다()
        {
            WorldProfiler.Tick(0.5f);
            Assert.Greater(WorldProfiler.StartupWorstMs, 400f);

            WorldProfiler.Reset();
            Assert.Greater(WorldProfiler.StartupWorstMs, 400f,
                "Reset 이 기동 구간 값까지 지웠습니다.");

            WorldProfiler.ResetAll();
            Assert.AreEqual(0f, WorldProfiler.StartupWorstMs, 0.0001f,
                "ResetAll 이 기동 구간 값을 지우지 않았습니다.");
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
