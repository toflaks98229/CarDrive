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
    /// <b>전역 렌더 상태를 쓰는 유일한 곳입니다.</b> 예전에는 <see cref="WeatherRig"/> 도
    /// <c>RenderSettings.fog*</c> 와 카메라 파클립을 직접 썼고, 실행 순서가 각각 0과 200이라
    /// <b>늦게 도는 이쪽이 언제나 이겼습니다.</b> 날씨의 시야 축소는 한 프레임도 화면에
    /// 남지 못했는데 그 사실이 어디에도 드러나지 않았습니다.
    ///
    /// 지금은 날씨가 <see cref="ViewDistances.ReportWeather"/> 로 <b>요청만</b> 하고,
    /// 두 요구의 조정은 <see cref="ViewDistances.Ladder"/> 안의 읽을 수 있는 두 줄이 합니다.
    /// 순서를 다투던 것이 계산으로 바뀌었습니다. 실행 순서를 날씨·하늘보다 뒤에 두는 것은
    /// 그래도 유지합니다 — 그쪽이 사다리에 값을 넣은 뒤에 읽어야 하기 때문입니다.
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

        /// <summary>마지막으로 대입한 LOD 오차입니다. -1 이면 런타임 반영이 꺼진 상태입니다.</summary>
        private static float appliedLodError = float.NaN;

        /// <summary>마지막으로 대입한 베이스맵 거리입니다. -1 이면 런타임 반영이 꺼진 상태입니다.</summary>
        private static float appliedBasemap = float.NaN;

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

            // 1. 안개를 씁니다. 사다리가 날씨 요청과 시야 요구를 이미 합쳐 두었습니다.
            if (settings.hideDrawDistanceWithFog) ApplyFog(ladder.FogDensity);

            // 2. 파클립. 켜져 있는 타일을 자르지 않을 만큼 멉니다.
            camera.farClipPlane = ladder.FarClip;

            // 3. 나무·바위·건물의 디더 페이드 구간을 셰이더 전역으로 넘깁니다.
            //    전역이라 매 프레임 넘겨도 싸고, 재질을 건드리지 않아 에셋이 오염되지 않습니다.
            Shader.SetGlobalFloat(FadeStartId, ladder.FadeStart);
            Shader.SetGlobalFloat(FadeEndId, ladder.FadeEnd);

            // 4. 지형에 직접 쓰는 값들은 비싸므로 <b>바뀌었을 때만, 주기로만</b> 맞춥니다.
            //
            // 나무 거리·LOD 오차·베이스맵 거리를 한 번의 순회로 함께 씁니다.
            // 예전에는 나무 거리만 여기서 쓰고 LOD 둘은 에디터 도구가 씬에 구워 넣었는데,
            // 그러면 실행 중에 비교해 볼 수가 없었습니다.
            float lodError = settings.applyTerrainLodAtRuntime ? settings.heightmapPixelError : -1f;
            float basemap = settings.applyTerrainLodAtRuntime ? settings.basemapDistance : -1f;

            bool changed = !Mathf.Approximately(retargeted, ladder.Scale)
                           || !Mathf.Approximately(appliedLodError, lodError)
                           || !Mathf.Approximately(appliedBasemap, basemap);

            if (changed && Time.unscaledTime >= nextRetarget)
            {
                nextRetarget = Time.unscaledTime + RetargetSeconds;
                retargeted = ladder.Scale;
                appliedLodError = lodError;
                appliedBasemap = basemap;

                ApplyTerrainSettings(ladder.TreeCut, lodError, basemap);
            }
        }

        // --- Private Methods ---

        /// <summary>
        /// 안개를 <b>씁니다.</b> 이 프로젝트에서 <c>RenderSettings.fog*</c> 를 쓰는 유일한 곳입니다.
        ///
        /// <b>무엇이 바뀌었는가.</b> 예전에는 여기서 <c>Mathf.Max(RenderSettings.fogDensity, needed)</c> 로
        /// <b>남이 써 둔 값을 읽어</b> 바닥만 올렸습니다. <see cref="WeatherRig"/> 도 같은 값을
        /// 쓰고 있었기 때문에, 서로 덮어쓰지 않으려고 그렇게 한 것입니다.
        ///
        /// 그 방식에는 문제가 둘 있었습니다. 하나는 <b>누가 주인인지 코드에 드러나지 않는다</b>는 것이고,
        /// 다른 하나는 <see cref="WeatherRig"/> 의 <c>controlRenderFog</c> 가 꺼져 있으면
        /// <b>날씨가 계산한 안개가 통째로 버려진다</b>는 것이었습니다. 실제로 씬에서 그 체크가
        /// 꺼져 있어서, 안개 날씨와 맑음이 화면에서 구분되지 않았습니다.
        ///
        /// 이제 두 요구를 <see cref="ViewDistances"/> 가 합쳐 하나의 값으로 내주고,
        /// 여기서는 <b>그 값을 그대로 씁니다.</b> 읽지 않으므로 순서를 다툴 상대가 없습니다.
        /// </summary>
        /// <param name="density">사다리가 정한 짙기. 날씨 요청과 시야 요구 중 짙은 쪽입니다.</param>
        private static void ApplyFog(float density)
        {
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = density;
        }

        /// <summary>
        /// 지형에 직접 쓰는 값들을 <b>한 번의 순회로</b> 대입합니다.
        ///
        /// 나무 잘라내기 거리는 사다리가 정합니다. 디더 페이드가 그보다 먼저 끝나도록
        /// 이미 맞춰져 있으므로 잘리는 순간은 보이지 않습니다.
        ///
        /// LOD 오차와 베이스맵 거리는 설정이 정합니다. 예전에는 에디터 도구가
        /// <c>private const</c> 로 들고 있다가 씬에 구워 넣었는데, 그러면 실행 중에
        /// 값을 바꿔 가며 비교할 수가 없었습니다.
        /// </summary>
        /// <param name="treeCut">대입할 나무 잘라내기 거리(m)</param>
        /// <param name="lodError">대입할 지형 LOD 오차. 0 미만이면 건드리지 않습니다.</param>
        /// <param name="basemapDistance">대입할 베이스맵 거리(m). 0 미만이면 건드리지 않습니다.</param>
        private static void ApplyTerrainSettings(float treeCut, float lodError, float basemapDistance)
        {
            Terrain[] terrains = Object.FindObjectsByType<Terrain>(FindObjectsInactive.Include);

            for (int i = 0; i < terrains.Length; i++)
            {
                if (terrains[i] == null) continue;

                terrains[i].treeDistance = treeCut;

                // 음수는 "이 값은 씬이 정한 대로 두라"는 뜻입니다.
                // 손으로 맞춰 둔 값을 코드가 덮어쓰지 않게 하는 통로입니다.
                if (lodError >= 0f) terrains[i].heightmapPixelError = lodError;
                if (basemapDistance >= 0f) terrains[i].basemapDistance = basemapDistance;
            }

            // 컬러가 접는 거리를 이 값으로 정해 두므로, 바꿨으면 알려 줘야 합니다.
            // 그러지 않으면 2초 동안 낡은 접는 거리가 남아 그 사이 페이드가 보이지 않습니다.
            TerrainChunkCuller.InvalidateCache();
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
            appliedLodError = float.NaN;
            appliedBasemap = float.NaN;
        }
    }
}
