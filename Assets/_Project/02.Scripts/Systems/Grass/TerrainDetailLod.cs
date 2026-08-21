using System.Collections.Generic;
using UnityEngine;
using CarDrive.Gameplay;

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
    /// 씬에 없으면 게임이 시작될 때 스스로 하나 생겨납니다.
    /// (<see cref="TerrainChunkCuller"/> 와 같은 방식입니다)
    /// </summary>
    [DefaultExecutionOrder(-99)]
    public class TerrainDetailLod : MonoBehaviour
    {
        // --- Constants ---

        /// <summary>단계를 다시 판정하는 주기(초)입니다. 속도는 프레임 단위로 뒤집히지 않습니다.</summary>
        private const float CheckSeconds = 0.5f;

        // --- Private Member Variables ---

        /// <summary>지금 적용 중인 단계입니다. 0이 가장 멀리, 2가 가장 가깝게 그립니다.</summary>
        private static int currentLevel;

        /// <summary>다음 판정 시각입니다.</summary>
        private static float nextCheck;

        /// <summary>아직 새 거리를 대입하지 못한 지형들입니다. 매 프레임 예산만큼 덜어냅니다.</summary>
        private static readonly List<Terrain> pending = new List<Terrain>(128);

        /// <summary>대기 중인 지형에 대입할 거리입니다.</summary>
        private static float pendingDistance;

        /// <summary>마지막으로 반영한 전체 거리 배율입니다. 이것이 바뀌면 단계가 그대로여도 다시 대입합니다.</summary>
        private static float appliedRangeScale = -1f;

        // --- Unity Event Functions ---

        /// <summary>주기가 되면 단계를 다시 정하고, 매 프레임 대입을 조금씩 진행합니다.</summary>
        void Update()
        {
            CarDriveWorldSettings settings = CarDriveWorldSettings.Instance;

            if (!settings.speedLodEnabled)
            {
                // 껐다면 원래 거리로 돌려놓아야 합니다. 그러지 않으면 끈 순간의 단계가 굳습니다.
                if (currentLevel != 0) Retarget(settings, 0);
            }
            else if (Time.unscaledTime >= nextCheck)
            {
                nextCheck = Time.unscaledTime + CheckSeconds;

                // 단계가 그대로여도 전체 거리 배율이 바뀌었으면 다시 대입해야 합니다.
                // (옵션을 실행 중에 켜고 끌 수 있어야 하기 때문입니다)
                int level = DecideLevel(settings, currentLevel);
                bool scaleChanged = !Mathf.Approximately(appliedRangeScale, settings.rangeScale);

                if (level != currentLevel || scaleChanged) Retarget(settings, level);
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
        private static int DecideLevel(CarDriveWorldSettings settings, int level)
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
        /// 차를 타고 있을 때만 봅니다. 걸어 다닐 때는 임계에 닿을 일이 없고,
        /// 도보 속도를 재려고 컴포넌트를 하나 더 뒤질 이유가 없습니다.
        /// </summary>
        /// <returns>주행 중이면 차량 속도, 아니면 0</returns>
        private static float CurrentSpeed()
        {
            Vehicle vehicle = Vehicle.Current;
            if (vehicle == null || vehicle.controller == null) return 0f;

            return vehicle.controller.CurrentSpeed;
        }

        /// <summary>
        /// 새 단계로 목표를 바꾸고, 모든 지형을 대기열에 넣습니다.
        /// </summary>
        /// <param name="settings">기준 거리와 배율을 읽을 설정</param>
        /// <param name="level">적용할 단계</param>
        private static void Retarget(CarDriveWorldSettings settings, int level)
        {
            currentLevel = level;

            float scale = 1f;
            if (level == 1) scale = settings.speedLodMidScale;
            else if (level >= 2) scale = settings.speedLodHighScale;

            // 거리 배율은 ViewDistances 가 이미 적용해 둡니다. 여기서는 <b>속도 단계만</b> 곱합니다.
            // 두 곳에서 곱하면 배율의 제곱이 됩니다. 컬러가 한 번 그랬습니다.
            appliedRangeScale = settings.rangeScale;

            pendingDistance = ViewDistances.Current.Grass * Mathf.Clamp(scale, 0.05f, 1f);

            // <b>꺼져 있는 것까지 담습니다.</b> WorldStreamer 가 멀어진 타일을 꺼 두는데,
            // 그것만 옛 거리를 안고 있으면 다가갔을 때 혼자 다르게 보입니다.
            //
            // 단계가 바뀌는 일은 드물어서(주기 0.5초 + 히스테리시스) 여기서 씬을 훑어도 됩니다.
            // 목록을 계속 들고 있으면 지형이 늘고 줄 때 어긋납니다.
            Terrain[] all = Object.FindObjectsByType<Terrain>(FindObjectsInactive.Include);

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
        private static void DrainPending(int budget)
        {
            if (pending.Count == 0) return;

            int count = Mathf.Min(Mathf.Max(1, budget), pending.Count);
            for (int i = 0; i < count; i++)
            {
                Terrain terrain = pending[i];
                if (terrain == null) continue;

                terrain.detailObjectDistance = pendingDistance;
            }

            pending.RemoveRange(0, count);
        }

        /// <summary>
        /// 씬에 없으면 게임이 시작될 때 스스로 하나 생겨납니다.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Spawn()
        {
            GameObject go = new GameObject("TerrainDetailLod");
            go.hideFlags = HideFlags.HideAndDontSave;

            go.AddComponent<TerrainDetailLod>();
            DontDestroyOnLoad(go);
        }

        /// <summary>
        /// 플레이 모드에 들어갈 때 정적 상태를 비웁니다.
        /// 도메인 리로드를 꺼 두면 지난 실행의 단계가 그대로 남기 때문입니다.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            currentLevel = 0;
            nextCheck = 0f;
            pendingDistance = 0f;
            appliedRangeScale = -1f;
            pending.Clear();
        }
    }
}
