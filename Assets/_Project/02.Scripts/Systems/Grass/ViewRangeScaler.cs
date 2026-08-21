using UnityEngine;
using CarDrive.Common;

namespace CarDrive.Systems
{
    /// <summary>
    /// 터레인이 <b>보이는 만큼만</b> 그려지도록 시야 거리·안개·파클립의 순서를 지킵니다.
    ///
    /// <b>고친 문제.</b> 시야를 줄이면 터레인이 가까이 가야 갑자기 나타났습니다.
    /// 원인이 둘이었습니다.
    ///
    ///  1. <see cref="WeatherRig"/> 가 매 LateUpdate 마다 <c>RenderSettings.fog</c> 를
    ///     다시 정하는데, <b>맑은 날씨에는 아예 끕니다.</b> 씬에 적어 둔 선형 안개
    ///     (70~340m)는 런타임에 한 번도 쓰이지 않습니다.
    ///  2. 그 상태에서 파클립만 당기면 <b>기하가 그대로 잘립니다.</b> 안개가 없으니
    ///     가려 줄 것이 없고, 다가가면 잘린 자리에서 지형이 튀어나옵니다.
    ///
    /// <b>지켜야 하는 순서.</b>
    /// <code>
    ///   안개가 다 덮는 거리  ≤  터레인이 있는 거리  &lt;  파클립
    /// </code>
    /// 이 순서가 지켜지면 경계는 어디에도 보이지 않습니다. 안개가 먼저 덮고,
    /// 지형은 그 뒤에서 끝나고, 파클립은 그보다 멀어 아무것도 자르지 않습니다.
    /// 하나라도 뒤집히면 그 자리가 눈에 보이는 선이 됩니다.
    ///
    /// <b>날씨와 싸우지 않습니다.</b> 안개의 주인은 <see cref="WeatherRig"/> 입니다.
    /// 여기서는 <b>바닥값만</b> 보장합니다 — 날씨가 정한 짙기가 시야 거리를 덮기에
    /// 모자라면 그만큼만 올립니다. 날씨가 더 짙게 하면 그대로 둡니다.
    /// 그래서 실행 순서를 날씨·하늘보다 <b>뒤로</b> 두었습니다.
    /// </summary>
    [DefaultExecutionOrder(200)]
    public class ViewRangeScaler : MonoBehaviour
    {
        // --- Constants ---

        /// <summary>
        /// 지수 제곱 안개가 <b>거의 다 덮는</b> 지점을 정하는 계수입니다.
        ///
        /// 지수 제곱 안개의 가려짐은 1 - exp(-(거리 × 짙기)²) 입니다.
        /// (거리 × 짙기)가 1.73 이면 약 95% 가 가려집니다. 그 지점을 "다 덮었다"로 봅니다.
        /// 100% 를 기다리면 짙기가 지나치게 올라가 가까운 곳까지 뿌예집니다.
        /// </summary>
        private const float FogReachFactor = 1.73f;

        /// <summary>
        /// 파클립에 더할 여유(m)입니다. 타일 대각선만큼입니다.
        ///
        /// 스트리머는 <b>가장 가까운 모서리</b>로 켤지 정합니다. 그래서 켜진 타일의
        /// 반대쪽 모서리는 타일 대각선만큼 더 멉니다. 파클립이 그보다 가까우면
        /// 켜져 있는 타일의 뒤쪽이 잘려 <b>하늘이 뚫려 보입니다.</b>
        /// 100m 타일의 대각선이 141m 라 그보다 조금 넉넉하게 잡았습니다.
        /// </summary>
        private const float FarClipMargin = 150f;

        /// <summary>확인 주기(초)입니다. 나무 거리처럼 비싼 대입은 이 주기로만 합니다.</summary>
        private const float RetargetSeconds = 0.5f;

        // --- Private Member Variables ---

        /// <summary>기준 시야 거리입니다. 처음 본 파클립을 기억합니다.</summary>
        private static float baseViewDistance = -1f;

        /// <summary>마지막으로 비싼 대입을 한 배율입니다.</summary>
        private static float retargeted = -1f;

        /// <summary>다음 비싼 대입 시각입니다.</summary>
        private static float nextRetarget;

        /// <summary>
        /// 터레인을 켜고 끄는 주인입니다. 파클립을 그 거리에 맞추려고 찾아 둡니다.
        ///
        /// <b>이 값을 짐작하지 않습니다.</b> 예전에는 타일이 100m 라는 가정으로 여유를
        /// 상수로 박아 두었는데, 배치를 바꾸면 조용히 어긋납니다. 주인에게 직접 묻습니다.
        /// </summary>
        private static Gameplay.WorldStreamer streamer;

        /// <summary>스트리머를 이미 찾아봤는지입니다. 없을 때 매 프레임 씬을 뒤지지 않기 위한 것입니다.</summary>
        private static bool streamerSearched;

        // --- Unity Event Functions ---

        /// <summary>
        /// 날씨와 하늘이 안개를 정한 <b>뒤에</b> 돌면서 순서를 바로잡습니다.
        /// </summary>
        void LateUpdate()
        {
            CarDriveWorldSettings settings = CarDriveWorldSettings.Instance;
            Camera camera = GameContext.MainCamera;
            if (camera == null) return;

            if (baseViewDistance < 0f) baseViewDistance = camera.farClipPlane;

            float scale = Mathf.Clamp(settings.rangeScale, 0.05f, 1f);
            float view = Mathf.Max(20f, baseViewDistance * scale);

            // 1. 안개가 시야 거리를 덮게 합니다. (모자랄 때만 올립니다)
            if (settings.hideDrawDistanceWithFog) EnsureFogCovers(view);

            // 2. 파클립은 켜져 있는 타일을 자르지 않을 만큼 멀어야 합니다.
            //
            // <b>시야 거리로 당기지 않습니다.</b> 그것이 이 문제의 원인이었습니다.
            // 안개가 이미 시야 거리에서 다 덮으므로, 파클립을 멀리 두어도
            // 그 사이에 보이는 것은 없습니다. 그릴 것도 없습니다 —
            // WorldStreamer 가 그보다 멀리 있는 타일을 아예 켜지 않기 때문입니다.
            camera.farClipPlane = Mathf.Max(baseViewDistance * scale, TerrainExtent(scale, view) + FarClipMargin);

            // 3. 나무 거리는 비싸므로 주기로만 맞춥니다.
            if (Time.unscaledTime >= nextRetarget && !Mathf.Approximately(retargeted, scale))
            {
                nextRetarget = Time.unscaledTime + RetargetSeconds;
                retargeted = scale;

                ApplyTreeDistance(view);
            }
        }

        // --- Private Methods ---

        /// <summary>
        /// 터레인이 실제로 <b>있는</b> 가장 먼 거리입니다.
        ///
        /// 스트리머가 켜는 거리를 그대로 씁니다. 그것보다 파클립이 가까우면
        /// 켜져 있는 타일이 잘려 하늘이 뚫려 보입니다.
        /// 스트리머를 찾지 못하면 시야 거리로 물러섭니다.
        /// </summary>
        /// <param name="scale">지금 거리 배율</param>
        /// <param name="view">시야 거리. 스트리머가 없을 때의 대체값입니다.</param>
        /// <returns>터레인이 있는 가장 먼 거리(m)</returns>
        private static float TerrainExtent(float scale, float view)
        {
            if (!streamerSearched)
            {
                streamerSearched = true;
                streamer = Object.FindAnyObjectByType<Gameplay.WorldStreamer>(FindObjectsInactive.Include);
            }

            if (streamer == null) return view;

            return Mathf.Max(view, streamer.activeDistance * scale);
        }

        /// <summary>
        /// 안개가 시야 거리에서 거의 다 덮도록 <b>바닥값을 보장</b>합니다.
        ///
        /// 날씨가 이미 그만큼 짙게 해 두었으면 건드리지 않습니다.
        /// <see cref="WeatherRig"/> 가 맑은 날씨에 안개를 아예 꺼 버리는데,
        /// 그러면 시야 거리에서 지형이 끝나는 자리가 그대로 보입니다.
        /// </summary>
        /// <param name="view">안개가 다 덮어야 하는 거리(m)</param>
        private static void EnsureFogCovers(float view)
        {
            float needed = FogReachFactor / Mathf.Max(view, 1f);

            // 날씨가 쓰는 것과 같은 방식이어야 합니다. 여기서 모드를 바꾸면
            // 다음 프레임에 날씨가 되돌려 놓아 두 값이 매 프레임 번갈아 적용됩니다.
            RenderSettings.fogMode = FogMode.ExponentialSquared;

            if (!RenderSettings.fog)
            {
                RenderSettings.fog = true;
                RenderSettings.fogDensity = needed;
                return;
            }

            // 날씨가 더 짙게 했으면 그대로 둡니다. 우리는 모자랄 때만 올립니다.
            RenderSettings.fogDensity = Mathf.Max(RenderSettings.fogDensity, needed);
        }

        /// <summary>
        /// 나무 그리는 거리를 시야 거리에 맞춥니다.
        ///
        /// 안개가 다 덮는 지점과 같게 둡니다. 이보다 짧으면 <b>안개에 묻히기 전에</b>
        /// 나무가 타일 단위로 사라져, 이 프로젝트가 한 번 겪은 "눈앞에서 튀어나옴"이
        /// 그대로 재현됩니다. (TerrainPerformanceSetup 의 주석에 그 기록이 있습니다)
        /// </summary>
        /// <param name="view">맞출 거리(m)</param>
        private static void ApplyTreeDistance(float view)
        {
            Terrain[] terrains = Object.FindObjectsByType<Terrain>(FindObjectsInactive.Include);

            for (int i = 0; i < terrains.Length; i++)
            {
                if (terrains[i] == null) continue;
                terrains[i].treeDistance = view;
            }
        }

        /// <summary>씬에 없으면 게임이 시작될 때 스스로 하나 생겨납니다.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Spawn()
        {
            GameObject go = new GameObject("ViewRangeScaler");
            go.hideFlags = HideFlags.DontSave;

            go.AddComponent<ViewRangeScaler>();
            DontDestroyOnLoad(go);
        }

        /// <summary>
        /// 플레이 모드에 들어갈 때 정적 상태를 비웁니다.
        /// 기준 거리가 지난 실행에서 남으면 배율이 곱해진 값을 기준으로 삼게 됩니다.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            baseViewDistance = -1f;
            retargeted = -1f;
            nextRetarget = 0f;
            streamer = null;
            streamerSearched = false;
        }
    }
}
