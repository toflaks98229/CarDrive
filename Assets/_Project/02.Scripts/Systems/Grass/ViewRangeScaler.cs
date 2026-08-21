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

        /// <summary>확인 주기(초)입니다. 나무 거리처럼 비싼 대입은 이 주기로만 합니다.</summary>
        private const float RetargetSeconds = 0.5f;

        /// <summary>디더 페이드 시작 거리를 넘길 전역 이름입니다.</summary>
        private static readonly int FadeStartId = Shader.PropertyToID("_CarDriveFadeStart");

        /// <summary>디더 페이드 종료 거리를 넘길 전역 이름입니다.</summary>
        private static readonly int FadeEndId = Shader.PropertyToID("_CarDriveFadeEnd");

        // --- Private Member Variables ---

        /// <summary>마지막으로 비싼 대입을 한 배율입니다.</summary>
        private static float retargeted = -1f;

        /// <summary>다음 비싼 대입 시각입니다.</summary>
        private static float nextRetarget;

        // --- Unity Event Functions ---

        /// <summary>
        /// 날씨와 하늘이 안개를 정한 <b>뒤에</b> 돌면서 순서를 바로잡습니다.
        /// </summary>
        void LateUpdate()
        {
            CarDriveWorldSettings settings = CarDriveWorldSettings.Instance;
            Camera camera = GameContext.MainCamera;
            if (camera == null) return;

            // 기준 시야 거리는 씬의 파클립입니다. 한 번만 알려 줍니다.
            ViewDistances.SetViewBase(camera.farClipPlane);

            // <b>여기서 곱하지 않습니다.</b> 계산은 전부 ViewDistances 안에 있습니다.
            ViewDistances.Ladder ladder = ViewDistances.Current;

            // 1. 안개가 시야 거리를 덮게 합니다. (모자랄 때만 올립니다)
            if (settings.hideDrawDistanceWithFog) EnsureFogCovers(ladder.FogDensity);

            // 2. 파클립. 켜져 있는 타일을 자르지 않을 만큼 멉니다.
            camera.farClipPlane = ladder.FarClip;

            // 3. 나무·바위·건물의 디더 페이드 구간을 셰이더 전역으로 넘깁니다.
            //    전역이라 매 프레임 넘겨도 싸고, 재질을 건드리지 않아 에셋이 오염되지 않습니다.
            Shader.SetGlobalFloat(FadeStartId, ladder.FadeStart);
            Shader.SetGlobalFloat(FadeEndId, ladder.FadeEnd);

            // 4. 나무 잘라내는 거리는 비싸므로 주기로만 맞춥니다.
            if (Time.unscaledTime >= nextRetarget && !Mathf.Approximately(retargeted, ladder.Scale))
            {
                nextRetarget = Time.unscaledTime + RetargetSeconds;
                retargeted = ladder.Scale;

                ApplyTreeCut(ladder.TreeCut);
            }
        }

        // --- Private Methods ---

        /// <summary>
        /// 안개가 시야 거리에서 거의 다 덮도록 <b>바닥값을 보장</b>합니다.
        ///
        /// 날씨가 이미 그만큼 짙게 해 두었으면 건드리지 않습니다.
        /// <see cref="WeatherRig"/> 가 맑은 날씨에 안개를 아예 꺼 버리는데,
        /// 그러면 시야 거리에서 지형이 끝나는 자리가 그대로 보입니다.
        /// </summary>
        /// <param name="needed">시야 거리를 덮는 데 필요한 짙기</param>
        private static void EnsureFogCovers(float needed)
        {
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
        /// 나무를 잘라내는 거리를 지형에 대입합니다.
        ///
        /// 값은 사다리가 정합니다. 디더 페이드가 그보다 먼저 끝나도록
        /// 이미 맞춰져 있으므로, 잘리는 순간은 보이지 않습니다.
        /// </summary>
        /// <param name="treeCut">대입할 거리(m)</param>
        private static void ApplyTreeCut(float treeCut)
        {
            Terrain[] terrains = Object.FindObjectsByType<Terrain>(FindObjectsInactive.Include);

            for (int i = 0; i < terrains.Length; i++)
            {
                if (terrains[i] == null) continue;
                terrains[i].treeDistance = treeCut;
            }

            // 컬러가 접는 거리를 이 값으로 정해 두므로, 바꿨으면 알려 줘야 합니다.
            // 그러지 않으면 2초 동안 낡은 접는 거리가 남아 그 사이 페이드가 보이지 않습니다.
            TerrainChunkCuller.InvalidateCache();
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
            retargeted = -1f;
            nextRetarget = 0f;
        }
    }
}
