using System.Collections.Generic;
using UnityEngine;
using CarDrive.Common;

namespace CarDrive.Systems
{
    /// <summary>
    /// 빠를 때 풀 그리는 거리를 줄이고, 서면 되돌립니다.
    ///
    /// <b>왜 드라이빙 게임에서 특히 값싼가.</b> 시속 120km 로 달리면 30m 앞의 풀 결은
    /// 보이지 않습니다. 흐름과 안개가 가려 줍니다. 그런데 그 보이지도 않는 풀을
    /// 그리는 비용은 서 있을 때와 똑같습니다. 속도가 붙은 동안만 거리를 줄이면
    /// <b>화면에서 잃는 것 없이</b> 가장 무거운 구간의 부담을 덜 수 있습니다.
    ///
    /// <b>연속으로 바꾸지 않습니다.</b> 속도에 비례해 매 프레임 거리를 조금씩 옮기면
    /// 지형이 매 프레임 디테일 패치를 다시 짭니다. 아끼려던 것보다 그쪽이 비쌉니다.
    /// 그래서 <b>단계</b>로 끊고, 단계가 바뀔 때만 대입하며, 그 대입도 나눠서 합니다.
    ///
    /// 단계가 오르내리는 속도에 간격(히스테리시스)을 둡니다. 임계 언저리에서
    /// 가속과 감속을 반복할 때 단계가 뒤집히면, 그 전환 자체가 이 최적화가 아끼려는 비용입니다.
    ///
    /// <b>밀도는 건드리지 않습니다.</b> 밀도를 줄이면 풀밭이 눈에 띄게 성겨지지만
    /// 거리는 줄여도 끝이 안개에 묻혀 알아채기 어렵습니다. 같은 이득에 값이 싼 쪽만 씁니다.
    ///
    /// <b>GPU 풀에 자리를 내주는 것도 여기서 합니다.</b>
    /// <see cref="GpuGrassRenderer.IsDrawing"/> 이 참이 되면 대입할 거리를 0 으로 바꿔
    /// 터레인 디테일을 끕니다. <c>detailObjectDistance</c> 를 이미 예산제로 다루고 있는
    /// 곳이 여기뿐이라, 끄는 일도 같은 통로를 지나는 편이 맞습니다.
    ///
    /// <b>스스로 생겨나지 않습니다.</b> 예전에는 <c>[RuntimeInitializeOnLoadMethod]</c>로
    /// 자기를 만들었는데, 그러면 씬에 하나 얹어 둔 경우 <b>둘이 되어</b> 예산이 두 배가 됩니다.
    /// 이제 <c>WorldRuntimeInstaller</c>가 하나만 만들어 붙입니다.
    /// (<see cref="TerrainChunkCuller"/> 와 같은 방식입니다)
    ///
    /// <b>단계와 대기열은 인스턴스가 들고 있습니다.</b> 예전에는 <c>static</c> 이라 둘이 되면
    /// 서로의 <c>pending</c> 을 덮어쓰고 <b>다른 거리를 번갈아 대입</b>할 수 있었습니다.
    /// 밖에서 꽂아 주는 <see cref="SpeedSource"/> 만 정적으로 남습니다 — 그것은 상태가 아니라
    /// <b>Composition 이 정하는 배선</b>이기 때문입니다.
    /// </summary>
    [DefaultExecutionOrder(-99)]
    public class TerrainDetailLod : MonoBehaviour
    {
        // --- Constants ---

        /// <summary>단계를 다시 판정하는 주기(초)입니다. 속도는 프레임 단위로 뒤집히지 않습니다.</summary>
        private const float CheckSeconds = 0.5f;

        // --- Public Properties ---

        /// <summary>
        /// 속도를 물어볼 곳입니다. <b>Composition이 기동할 때 한 번 꽂아 줍니다.</b>
        ///
        /// 비어 있으면 속도를 0으로 봅니다. 즉 이 최적화만 쉬고 게임은 그대로 돕니다 —
        /// 배선이 빠졌다고 화면이 멈추면 안 되기 때문입니다.
        /// </summary>
        public static ISpeedSource SpeedSource { get; set; }

        // --- Private Member Variables ---

        /// <summary>지금 적용 중인 단계입니다. 0이 가장 멀리, 2가 가장 가깝게 그립니다.</summary>
        private int currentLevel;

        /// <summary>다음 판정 시각입니다.</summary>
        private float nextCheck;

        /// <summary>아직 새 거리를 대입하지 못한 지형들입니다. 매 프레임 예산만큼 덜어냅니다.</summary>
        private readonly List<Terrain> pending = new List<Terrain>(128);

        /// <summary>대기 중인 지형에 대입할 거리입니다.</summary>
        private float pendingDistance;

        /// <summary>마지막으로 반영한 전체 거리 배율입니다. 이것이 바뀌면 단계가 그대로여도 다시 대입합니다.</summary>
        private float appliedRangeScale = -1f;

        /// <summary>이번 단계가 <b>목표로 하는</b> 속도 배율입니다.</summary>
        private float targetSpeedScale = 1f;

        /// <summary>
        /// <see cref="ViewDistances"/> 에 <b>실제로 알린</b> 속도 배율입니다.
        ///
        /// 목표와 다를 수 있습니다. 대입이 몇 프레임에 걸쳐 끝나는 동안에는
        /// 옛 거리와 새 거리를 안은 타일이 섞여 있고, 그때 알려야 하는 것은
        /// <b>둘 중 짧은 쪽</b>이기 때문입니다.
        /// </summary>
        private float reportedSpeedScale = 1f;

        /// <summary>
        /// 마지막으로 반영한 <see cref="GpuGrassRenderer.IsDrawing"/> 값입니다.
        ///
        /// GPU 풀이 그리기 시작하거나 물러나는 것은 단계·배율과 무관하게 대입을 다시 해야
        /// 하는 사건이라, 따로 기억해 두고 견줍니다.
        /// </summary>
        private bool appliedHandOff;

        /// <summary>지금 돌고 있는 것입니다. 둘이 되었는지 알아채려고만 둡니다.</summary>
        private static TerrainDetailLod active;

        // --- Unity Event Functions ---

        /// <summary>
        /// 둘이 되었으면 알립니다. 예산제가 두 배로 헐거워지는 자리라 조용히 두면 안 됩니다.
        /// </summary>
        void Awake()
        {
            if (active != null && active != this)
            {
                GameLog.Error(GameLog.Channel.World,
                    "TerrainDetailLod 가 둘입니다. 프레임당 대입 예산이 두 배가 되고 " +
                    "서로 다른 거리를 번갈아 대입할 수 있습니다. WorldRuntimeInstaller 를 보세요.", this);
            }

            active = this;
        }

        /// <summary>주기가 되면 단계를 다시 정하고, 매 프레임 대입을 조금씩 진행합니다.</summary>
        void Update()
        {
            CarDriveWorldSettings settings = CarDriveWorldSettings.Instance;

            // <b>GPU 풀이 그리기 시작했는지</b>가 바뀌면 단계와 무관하게 다시 대입해야 합니다.
            // 주기를 기다리지 않는 이유가 있습니다 — 넘겨받는 쪽이 이미 그리고 있으므로,
            // 기다리는 동안은 풀이 두 겹으로 보입니다.
            bool handOffChanged = appliedHandOff != GpuGrassRenderer.IsDrawing;

            if (!settings.speedLodEnabled)
            {
                // 껐다면 원래 거리로 돌려놓아야 합니다. 그러지 않으면 끈 순간의 단계가 굳습니다.
                //
                // <b>배율이 바뀐 경우도 여기서 받습니다.</b> 예전에는 단계만 보고 있어서,
                // 속도 적응을 꺼 두면 <c>rangeScale</c> 이 풀에만 닿지 않았습니다. 그러면
                // 지형은 씬에 구워진 70m 를 그리는데 셰이더는 46m 에서 다 지워, 보이지도 않는
                // 24m 어치를 계속 그리게 됩니다.
                //
                // 매 프레임 도는 자리지만 Retarget 이 appliedRangeScale 을 맞춰 두므로
                // 실제로 일하는 것은 바뀐 그 프레임 한 번뿐입니다.
                bool scaleMoved = !Mathf.Approximately(appliedRangeScale, settings.rangeScale);

                if (currentLevel != 0 || scaleMoved || handOffChanged) Retarget(settings, 0);
            }
            else if (handOffChanged || Time.unscaledTime >= nextCheck)
            {
                nextCheck = Time.unscaledTime + CheckSeconds;

                // 단계가 그대로여도 전체 거리 배율이 바뀌었으면 다시 대입해야 합니다.
                // (옵션을 실행 중에 켜고 끌 수 있어야 하기 때문입니다)
                int level = DecideLevel(settings, currentLevel);
                bool scaleChanged = !Mathf.Approximately(appliedRangeScale, settings.rangeScale);

                if (level != currentLevel || scaleChanged || handOffChanged) Retarget(settings, level);
            }

            DrainPending(settings.maxDetailLodApplicationsPerFrame);
        }

        // --- Private Methods ---

        /// <summary>
        /// 지금 속도로 어느 단계여야 하는지 정합니다.
        ///
        /// <b>올라갈 때와 내려갈 때의 기준이 다릅니다.</b> 같으면 임계 속도 언저리에서
        /// 단계가 계속 뒤집히고, 그 전환이 곧 비용입니다.
        /// </summary>
        /// <param name="settings">임계 속도와 간격을 읽을 설정</param>
        /// <param name="level">지금 단계</param>
        /// <returns>이번에 적용할 단계 (0 = 원래 거리, 2 = 가장 가깝게)</returns>
        private int DecideLevel(CarDriveWorldSettings settings, int level)
        {
            float speed = CurrentSpeed();
            float gap = Mathf.Max(0f, settings.speedLodHysteresisKmh);

            // 낮은 단계에 이미 있으면, 그 간격만큼 더 느려져야 풀려납니다.
            float midOn = settings.speedLodMidKmh;
            float midOff = Mathf.Max(0f, settings.speedLodMidKmh - gap);
            float highOn = Mathf.Max(midOn, settings.speedLodHighKmh);
            float highOff = Mathf.Max(midOn, settings.speedLodHighKmh - gap);

            if (level >= 2) return speed >= highOff ? 2 : (speed >= midOff ? 1 : 0);
            if (level == 1) return speed >= highOn ? 2 : (speed >= midOff ? 1 : 0);

            return speed >= highOn ? 2 : (speed >= midOn ? 1 : 0);
        }

        /// <summary>
        /// 지금 이동 속도(km/h)입니다.
        ///
        /// <b>차량을 직접 보지 않습니다.</b> 예전에는 <c>Vehicle.Current.controller.CurrentSpeed</c>를
        /// 읽었는데, 그 한 줄 때문에 Systems 계층이 Gameplay 계층을 거꾸로 참조했습니다.
        /// 어셈블리를 나누면 그 참조가 순환이 되어 컴파일이 막힙니다.
        /// 이제 <see cref="SpeedSource"/>가 답하고, 누가 답할지는 Composition이 정합니다.
        /// </summary>
        /// <returns>연결된 속도원이 있으면 그 속도, 없으면 0</returns>
        private static float CurrentSpeed()
        {
            return SpeedSource != null ? SpeedSource.CurrentSpeedKmh : 0f;
        }

        /// <summary>
        /// 새 단계로 목표를 바꾸고, 모든 지형을 대기열에 넣습니다.
        /// </summary>
        /// <param name="settings">기준 거리와 배율을 읽을 설정</param>
        /// <param name="level">적용할 단계</param>
        private void Retarget(CarDriveWorldSettings settings, int level)
        {
            currentLevel = level;

            float scale = 1f;
            if (level == 1) scale = settings.speedLodMidScale;
            else if (level >= 2) scale = settings.speedLodHighScale;

            appliedRangeScale = settings.rangeScale;
            targetSpeedScale = Mathf.Clamp(scale, 0.05f, 1f);

            // <b>여기서 곱하지 않습니다.</b> 곱셈은 전부 사다리 안에 있습니다.
            // 속도 단계를 알려 준 뒤 그 결과를 되받습니다.
            //
            // 예전에는 이 줄이 <c>ViewDistances.Current.Grass * scale</c> 이었습니다.
            // 그래서 사다리는 <b>실제로 몇 미터에 풀이 잘리는지 몰랐고</b>, 페이드 창을
            // 낼 수 없어 재질에 숫자를 구워 두는 수밖에 없었습니다. 그 숫자가 배율을 몰라
            // 시속 90 이상에서 풀이 통짜로 튀어나왔습니다.
            ViewDistances.ReportGrassSpeedScale(targetSpeedScale);
            float target = ViewDistances.Current.Grass;

            // <b>GPU 풀이 그리기 시작했으면 터레인 디테일을 끕니다.</b> 둘 다 그리면 두 겹입니다.
            //
            // 끄는 방법으로 거리 0 을 씁니다. 디테일 <b>데이터</b>는 그대로 두어야 하기 때문입니다 —
            // GPU 쪽이 바로 그 디테일맵을 읽어 포기 자리를 만듭니다. 레이어를 비우거나 밀도를 0으로
            // 두면 되돌릴 때 다시 심어야 합니다.
            //
            // <b>준비되기 전에는 끄지 않습니다.</b> 신호가 "설정이 켜졌다"가 아니라
            // "실제로 그리고 있다"인 이유가 이것입니다. 씬에 렌더러가 없거나 기기가 컴퓨트를
            // 못 쓰면 이쪽이 계속 그려야 합니다.
            appliedHandOff = GpuGrassRenderer.IsDrawing;

            pendingDistance = appliedHandOff ? 0f : target;

            // <b>알리는 값은 아직 목표가 아닐 수 있습니다.</b>
            //
            // 대입은 아래 DrainPending 이 몇 프레임에 걸쳐 합니다. 그동안 지형에는 옛 거리와
            // 새 거리가 섞여 있으므로, 페이드 창은 <b>둘 중 짧은 쪽</b>에 맞춰야 합니다.
            //
            // 줄이는 쪽이면 새 값이 곧 짧은 쪽이라 그대로 갑니다. 아직 옛(긴) 거리인 타일이
            // 새 페이드 끝보다 멀리까지 그리지만, 그 구간의 풀은 이미 다 지워져 있어 보이지 않습니다.
            // 늘리는 쪽이면 옛 값을 그대로 안고 있다가 대입이 끝난 뒤에 올립니다.
            // 먼저 올리면 아직 짧은 타일에서 페이드가 끝나기 전에 풀이 잘립니다.
            //
            // <b>넘겨준 뒤에는 기다릴 이유가 없습니다.</b> 페이드 창을 보는 쪽이 GPU 렌더러
            // 하나뿐이고, 그쪽은 잘라내는 거리도 같은 사다리에서 매 프레임 새로 읽습니다.
            reportedSpeedScale = appliedHandOff
                ? targetSpeedScale
                : Mathf.Min(reportedSpeedScale, targetSpeedScale);

            ViewDistances.ReportGrassSpeedScale(reportedSpeedScale);

            // 목록은 TerrainRegistry 가 한 번만 찾아 나눠 씁니다.
            //
            // <b>꺼져 있는 것까지 담겨 옵니다.</b> WorldStreamer 가 멀어진 타일을 꺼 두는데,
            // 그것만 옛 거리를 안고 있으면 다가갔을 때 혼자 다르게 보입니다.
            Terrain[] all = TerrainRegistry.All;

            pending.Clear();
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] == null) continue;
                if (Mathf.Approximately(all[i].detailObjectDistance, pendingDistance)) continue;

                pending.Add(all[i]);
            }
        }

        /// <summary>
        /// 대기열에서 예산만큼 덜어내 거리를 대입합니다.
        ///
        /// 한꺼번에 대입하지 않는 이유가 있습니다. <c>detailObjectDistance</c> 를 바꾸면
        /// 그 타일이 디테일 패치를 다시 짭니다. 103장을 한 프레임에 처리하면
        /// <b>아끼려는 것보다 그 한 프레임이 더 비쌉니다.</b>
        /// 나눠 대입하면 몇 프레임에 걸쳐 서서히 바뀌어 전환도 덜 보입니다.
        /// </summary>
        /// <param name="budget">이번 프레임에 대입할 최대 수</param>
        private void DrainPending(int budget)
        {
            if (pending.Count == 0)
            {
                // 대입이 다 끝났으면 이제 모든 타일이 목표 거리를 갖고 있습니다.
                // 그제야 페이드 창을 목표에 맞춰 올립니다. (늘리는 쪽에서만 실제로 움직입니다)
                if (!Mathf.Approximately(reportedSpeedScale, targetSpeedScale))
                {
                    reportedSpeedScale = targetSpeedScale;
                    ViewDistances.ReportGrassSpeedScale(reportedSpeedScale);
                }

                return;
            }

            int count = Mathf.Min(Mathf.Max(1, budget), pending.Count);
            for (int i = 0; i < count; i++)
            {
                Terrain terrain = pending[i];
                if (terrain == null) continue;

                terrain.detailObjectDistance = pendingDistance;
                WorldProfiler.Count(WorldProfiler.Counter.DetailDistanceWritten);
            }

            pending.RemoveRange(0, count);
        }

        /// <summary>
        /// 플레이 모드에 들어갈 때 남은 정적 상태를 비웁니다.
        ///
        /// <b>단계와 대기열은 이제 인스턴스가 들고 있어서 비울 필요가 없습니다.</b>
        /// 남는 것은 밖에서 꽂아 주는 <see cref="SpeedSource"/> 와 중복 감시용 참조뿐입니다.
        /// 속도원을 비우지 않으면 도메인 리로드를 꺼 둔 채 다시 플레이할 때
        /// <b>지난 실행에서 파괴된 차</b>를 계속 물어보게 됩니다.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            SpeedSource = null;
            active = null;
        }
    }
}
