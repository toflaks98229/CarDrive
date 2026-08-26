using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
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
    ///
    /// <b>상태는 인스턴스가 들고 있습니다.</b> 예전에는 전부 <c>static</c> 이었습니다.
    /// 그래서 값을 보려고 씬에 하나 얹으면 <b>둘이 되어</b> 같은 프레임에 두 번 대입하고,
    /// "마지막으로 적어 넣은 값"을 서로 덮어써 재대입 판단이 무너졌습니다.
    /// 그 사실은 어디에도 드러나지 않았습니다. 지금은 둘이 되어도 각자 자기 상태를 굴리고,
    /// <c>Awake</c> 가 그 사실을 로그로 알립니다.
    /// </summary>
    [DefaultExecutionOrder(200)]
    public class ViewRangeScaler : MonoBehaviour
    {
        // --- Constants ---

        /// <summary>확인 주기(초)입니다. 나무 거리처럼 비싼 대입은 이 주기로만 합니다.</summary>
        private const float RetargetSeconds = 0.5f;

        /// <summary>
        /// 적어 둔 나무 거리가 <b>필요한 것보다 이 배수만큼 멀어도</b> 그냥 둡니다.
        ///
        /// <b>왜 한쪽에만 여유가 있는가.</b> 어긋나는 방향에 따라 결과가 다릅니다.
        ///   너무 <b>가까우면</b> — 디더가 다 지우기 전에 나무가 잘려 <b>튀는 것이 보입니다.</b>
        ///   너무 <b>멀면</b> — 이미 다 지워진 나무를 계속 그립니다. <b>낭비지만 보이지는 않습니다.</b>
        ///
        /// 그래서 가까운 쪽은 즉시 고치고, 먼 쪽은 여유를 두고 미룹니다.
        /// (<see cref="TerrainDetailLod"/> 가 풀 거리에서 쓰는 것과 같은 비대칭입니다)
        ///
        /// <b>이 값이 곧 전이 중 대입 횟수를 정합니다.</b> 날씨 전이는 최소 15초이고
        /// 시야가 연속으로 움직이므로, 여유가 없으면 그동안 0.5초마다 지형 103장에
        /// 대입하게 됩니다. 1.15 면 시야가 15% 움직일 때마다 한 번이라
        /// 폭우가 밀려오는 15초 동안 <b>여덟 번쯤</b>입니다.
        /// </summary>
        private const float TreeCutSlack = 1.15f;

        /// <summary>디더 페이드 시작 거리를 넘길 전역 이름입니다.</summary>
        private static readonly int FadeStartId = Shader.PropertyToID("_CarDriveFadeStart");

        /// <summary>디더 페이드 종료 거리를 넘길 전역 이름입니다.</summary>
        private static readonly int FadeEndId = Shader.PropertyToID("_CarDriveFadeEnd");

        /// <summary>풀이 흩어져 사라지기 시작하는 거리를 넘길 전역 이름입니다.</summary>
        private static readonly int GrassFadeStartId = Shader.PropertyToID("_CarDriveGrassFadeStart");

        /// <summary>풀이 다 사라지는 거리를 넘길 전역 이름입니다.</summary>
        private static readonly int GrassFadeEndId = Shader.PropertyToID("_CarDriveGrassFadeEnd");

        /// <summary>
        /// 거리 페이드의 기준이 될 <b>카메라 자리</b>를 넘길 전역 이름입니다. w 는 1입니다.
        ///
        /// <b>왜 셰이더가 스스로 못 구하는가.</b> 셰이더의 <c>GetCameraPositionWS()</c> 는
        /// 지금 그리고 있는 카메라를 돌려주는데, <b>그림자 패스에서는 그것이 빛의 가상 카메라</b>입니다.
        /// 그 자리를 기준으로 거리를 재면 나무가 <b>해에서 먼 순서로</b> 지워집니다.
        ///
        /// 페이드의 기준은 언제나 플레이어가 보는 카메라여야 하므로 여기서 넘깁니다.
        /// </summary>
        private static readonly int EyeId = Shader.PropertyToID("_CarDriveEye");

        // --- Private Member Variables ---

        /// <summary>
        /// 지형에 <b>마지막으로 적어 넣은</b> 나무 거리(m)입니다. 0 미만이면 아직 쓴 적이 없습니다.
        ///
        /// <b>입력이 아니라 결과를 기억합니다.</b> 예전에는 이 자리에 <c>rangeScale</c> 을
        /// 담아 두고 그것이 바뀌었는지로 판단했습니다. 그런데 실제로 적어 넣는 값
        /// (<see cref="ViewDistances.Ladder.TreeCut"/>)은 배율뿐 아니라 <b>날씨가 요청한
        /// 시야 배율</b>에도 걸립니다. 그래서 폭우가 시야를 0.35배로 줄여도 이 판단은
        /// "바뀐 것 없음"이었고, 나무 거리는 <b>기동 직후의 값에 굳어 있었습니다.</b>
        ///
        /// 결과를 기억하면 앞으로 어떤 축이 <c>TreeCut</c> 에 새로 붙어도
        /// (구역별 시야, 품질 프리셋) 이 판단을 다시 손볼 필요가 없습니다.
        /// </summary>
        private float appliedTreeCut = -1f;

        /// <summary>다음 비싼 대입 시각입니다.</summary>
        private float nextRetarget;

        /// <summary>마지막으로 대입한 LOD 오차입니다. -1 이면 런타임 반영이 꺼진 상태입니다.</summary>
        private float appliedLodError = float.NaN;

        /// <summary>마지막으로 대입한 베이스맵 거리입니다. -1 이면 런타임 반영이 꺼진 상태입니다.</summary>
        private float appliedBasemap = float.NaN;

        /// <summary>마지막으로 대입한 나무 메시 상한입니다. -1 이면 아직 쓴 적이 없습니다.</summary>
        private int appliedTreeMeshLimit = -1;


        /// <summary>지금 돌고 있는 것입니다. 둘이 되었는지 알아채려고만 둡니다.</summary>
        private static ViewRangeScaler active;

        // --- Unity Event Functions ---

        /// <summary>
        /// 둘이 되었으면 알립니다. <b>고치지 않더라도 드러나기만 하면 반은 해결됩니다.</b>
        /// 이 문제의 본질이 "조용히 두 배로 돈다"였기 때문입니다.
        /// </summary>
        void Awake()
        {
            if (active != null && active != this)
            {
                GameLog.Error(GameLog.Channel.World,
                    "ViewRangeScaler 가 둘입니다. 안개·파클립·그림자를 한 프레임에 두 번 씁니다. " +
                    "WorldRuntimeInstaller 가 하나만 확보하도록 되어 있으니, 씬에 직접 얹은 것이 있는지 보세요.", this);
            }

            active = this;
        }

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

            // 3. 그림자 거리. <b>이 프로젝트에서 URP 에셋의 그림자 거리를 쓰는 유일한 곳입니다.</b>
            if (settings.applyShadowDistanceAtRuntime) ApplyShadowDistance(ladder.Shadow);

            // 4. 나무·바위·건물의 디더 페이드 구간을 셰이더 전역으로 넘깁니다.
            //    전역이라 매 프레임 넘겨도 싸고, 재질을 건드리지 않아 에셋이 오염되지 않습니다.
            Shader.SetGlobalFloat(FadeStartId, ladder.FadeStart);
            Shader.SetGlobalFloat(FadeEndId, ladder.FadeEnd);

            // 5. 풀의 페이드 구간도 같은 방식으로 넘깁니다.
            //
            //    <b>나무와 따로 두는 이유.</b> 풀이 잘리는 거리는 나무의 1/7 이고
            //    (49m 대 340m) 속도 단계에 따라 실행 중에 또 반으로 줄어듭니다.
            //    나무의 창을 그대로 쓰면 풀은 페이드가 시작도 하기 전에 잘립니다.
            //
            //    예전에는 이 두 값이 재질에 <b>숫자로 구워져</b> 있었습니다(35m / 68.6m).
            //    <c>detailDistance</c> 70m 기준으로 에디터 도구가 적어 넣은 값인데,
            //    그 도구는 rangeScale 도 속도 단계도 몰랐고 지금은 도구 자체가 없습니다.
            Shader.SetGlobalFloat(GrassFadeStartId, ladder.GrassFadeStart);
            Shader.SetGlobalFloat(GrassFadeEndId, ladder.GrassFadeEnd);

            // 6. 페이드의 기준이 될 카메라 자리입니다. 그림자 패스가 이것을 봐야
            //    나무가 해에서 먼 순서가 아니라 <b>플레이어에게서 먼 순서</b>로 지워집니다.
            Vector3 eye = camera.transform.position;
            Shader.SetGlobalVector(EyeId, new Vector4(eye.x, eye.y, eye.z, 1f));

            // 6. 지형에 직접 쓰는 값들은 비싸므로 <b>바뀌었을 때만, 주기로만</b> 맞춥니다.
            //
            // 나무 거리·LOD 오차·베이스맵 거리를 한 번의 순회로 함께 씁니다.
            // 예전에는 나무 거리만 여기서 쓰고 LOD 둘은 에디터 도구가 씬에 구워 넣었는데,
            // 그러면 실행 중에 비교해 볼 수가 없었습니다.
            float lodError = settings.applyTerrainLodAtRuntime ? settings.heightmapPixelError : -1f;
            float basemap = settings.applyTerrainLodAtRuntime ? settings.basemapDistance : -1f;

            // <b>나무 설정은 게이트 밖에서 견줍니다.</b> 거리가 아니라서 연속으로 움직이지
            // 않고, 사람이 설정을 만질 때만 바뀝니다. 여유를 둘 이유가 없으므로 그냥 같은지만
            // 봅니다. (게이트 안의 비대칭 규칙은 거리에만 뜻이 있습니다)
            bool treeLodChanged = appliedTreeMeshLimit != settings.treeMeshLimit;

            bool changed = treeLodChanged
                           || NeedsTerrainWrite(appliedTreeCut, ladder.TreeCut, ladder.FadeEnd,
                                                appliedLodError, lodError,
                                                appliedBasemap, basemap);

            if (changed && Time.unscaledTime >= nextRetarget)
            {
                nextRetarget = Time.unscaledTime + RetargetSeconds;
                appliedTreeCut = ladder.TreeCut;
                appliedLodError = lodError;
                appliedBasemap = basemap;
                appliedTreeMeshLimit = settings.treeMeshLimit;

                ApplyTerrainSettings(ladder.TreeCut, lodError, basemap, settings.treeMeshLimit);
            }
        }

        // --- Public Methods ---

        /// <summary>
        /// 지형에 값을 다시 적어 넣어야 하는지 <b>판단만</b> 합니다.
        ///
        /// <b>왜 밖으로 꺼냈는가.</b> 이 판단이 한 번 틀려서, 날씨가 시야를 줄여도 나무 거리가
        /// 따라가지 않았습니다. 사다리(<see cref="ViewDistances.Ladder"/>)는 순수 계산이라
        /// EditMode 테스트가 부등식을 지키고 있었는데, <b>그 값을 지형에 옮기는 이 계층은
        /// 아무도 보고 있지 않았습니다.</b> 사다리를 만든 것으로 이 유형이 끝나지 않는다는 뜻입니다.
        ///
        /// 그래서 사다리와 같은 방식으로 만듭니다 — 필요한 값을 전부 인자로 받고 정적 상태를
        /// 읽지 않습니다. 씬도 지형도 없이 검사할 수 있습니다.
        /// </summary>
        /// <param name="appliedTreeCut">지형에 마지막으로 적어 넣은 나무 거리(m). 0 미만이면 쓴 적 없음</param>
        /// <param name="treeCut">지금 사다리가 정한 나무 거리(m)</param>
        /// <param name="fadeEnd">디더가 나무를 다 지우는 거리(m)</param>
        /// <param name="appliedLodError">마지막으로 적어 넣은 LOD 오차</param>
        /// <param name="lodError">지금 적용해야 할 LOD 오차</param>
        /// <param name="appliedBasemap">마지막으로 적어 넣은 베이스맵 거리</param>
        /// <param name="basemap">지금 적용해야 할 베이스맵 거리</param>
        /// <returns>다시 적어 넣어야 하면 참</returns>
        public static bool NeedsTerrainWrite(float appliedTreeCut, float treeCut, float fadeEnd,
                                             float appliedLodError, float lodError,
                                             float appliedBasemap, float basemap)
        {
            // 아직 한 번도 쓰지 않았습니다. 씬에 적힌 값이 무엇이든 맞춰 두어야 합니다.
            if (appliedTreeCut < 0f) return true;

            // <b>가까운 쪽은 급합니다.</b> 적어 둔 거리가 디더가 끝나는 곳보다 가까우면,
            // 아직 다 지워지지 않은 나무가 그 자리에서 통째로 잘립니다. 그것이 보입니다.
            if (appliedTreeCut < fadeEnd) return true;

            // <b>먼 쪽은 급하지 않습니다.</b> 이미 다 지워진 나무를 계속 그리는 것이라
            // 낭비일 뿐 화면은 멀쩡합니다. 그래서 여유를 두고, 전이 중에 대입이 몰리지 않게 합니다.
            if (appliedTreeCut > treeCut * TreeCutSlack) return true;

            // LOD 값들은 사람이 설정을 만질 때만 바뀝니다. 연속으로 움직이지 않으므로
            // 여유를 둘 이유가 없고, 예전처럼 정확히 견줍니다.
            return !Mathf.Approximately(appliedLodError, lodError)
                   || !Mathf.Approximately(appliedBasemap, basemap);
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
        /// 그림자 거리를 URP 에셋에 씁니다.
        ///
        /// <b>왜 여기인가.</b> 이 값은 URP 에셋 안에 있어서 이 프로젝트의 다른 거리들과
        /// <b>서로를 모르는 채</b>였습니다. 그래서 <see cref="TerrainChunkCuller"/> 가 화면 밖
        /// 캐스터를 담으려고 둔 여유(55m)가 실제 그림자 거리를 모르고 있었고,
        /// 품질을 올리면 어긋날 상태였습니다. 이제 사다리가 둘을 함께 냅니다.
        ///
        /// <b>주의: 에디터에서는 이 쓰기가 디스크에 저장됩니다.</b> URP 에셋은 실제 에셋 파일이라
        /// 플레이 중에 바꾸면 그 변경이 남습니다. 그래서 <b>값이 실제로 달라질 때만</b> 씁니다.
        /// 그것도 싫으면 설정에서 <c>applyShadowDistanceAtRuntime</c> 을 끄세요.
        ///
        /// 품질 레벨을 바꾸면 활성 에셋이 통째로 바뀌므로, 대입해 둔 값을 기억하는 것만으로는
        /// 부족합니다. <b>에셋의 현재 값과 직접 견줍니다.</b>
        /// </summary>
        /// <param name="distance">사다리가 정한 그림자 거리(m)</param>
        private static void ApplyShadowDistance(float distance)
        {
            UniversalRenderPipelineAsset urp = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if (urp == null) return;

            if (Mathf.Approximately(urp.shadowDistance, distance)) return;

            urp.shadowDistance = distance;

            // 컬러의 경계 여유가 이 거리에서 나오므로, 바꿨으면 다시 구하게 합니다.
            TerrainChunkCuller.InvalidateCache();
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
        /// <param name="treeMeshLimit">메시로 그릴 나무의 최대 수</param>
        private static void ApplyTerrainSettings(float treeCut, float lodError, float basemapDistance,
                                                 int treeMeshLimit)
        {
            // 목록은 TerrainRegistry 가 한 번만 찾아 나눠 씁니다. 여기서 씬을 다시 훑지 않습니다.
            Terrain[] terrains = TerrainRegistry.All;

            for (int i = 0; i < terrains.Length; i++)
            {
                if (terrains[i] == null) continue;

                terrains[i].treeDistance = treeCut;

                // <b>디더가 일할 나무를 실제로 메시로 그리게 합니다.</b>
                //
                // 씬에는 <c>maxFullLOD = 50</c> 이 구워져 있었습니다. 이 월드의 나무 밀도
                // (2,642그루 / 0.00257그루/㎡)로 환산하면 <b>반경 79m</b> 어치입니다.
                // 그런데 디더 페이드 구간은 167~231m 라 <b>두 구간이 겹치지 않았습니다</b> —
                // 페이드가 일할 나무가 하나도 메시로 그려지지 않고, 전부 유니티 내부
                // 임포스터로 넘어갔습니다. 그쪽은 프로토타입 재질을 쓰지 않으므로
                // <c>_DITHER_FADE</c> 가 걸리지 않고, 결과가 <b>먼 나무의 통짜 팝</b>이었습니다.
                //
                // <b><c>Terrain.drawInstanced</c> 는 건드리지 않습니다.</b> 그 값은 나무가 아니라
                // <b>지면</b>의 GPU 인스턴싱이고, 켜려면 지면 셰이더에 인스턴싱 지원이
                // 있어야 하는데 <c>CarDriveToonTerrain</c> 에는 없습니다. 켜면 지면이 깨집니다.
                terrains[i].treeMaximumFullLODCount = treeMeshLimit;

                // 음수는 "이 값은 씬이 정한 대로 두라"는 뜻입니다.
                // 손으로 맞춰 둔 값을 코드가 덮어쓰지 않게 하는 통로입니다.
                if (lodError >= 0f) terrains[i].heightmapPixelError = lodError;
                if (basemapDistance >= 0f) terrains[i].basemapDistance = basemapDistance;

                WorldProfiler.Count(WorldProfiler.Counter.TreeDistanceWritten);
            }

            // 컬러가 접는 거리를 이 값으로 정해 두므로, 바꿨으면 알려 줘야 합니다.
            // 그러지 않으면 2초 동안 낡은 접는 거리가 남아 그 사이 페이드가 보이지 않습니다.
            TerrainChunkCuller.InvalidateCache();
        }

        /// <summary>
        /// 플레이 모드에 들어갈 때 남은 정적 상태를 비웁니다.
        ///
        /// <b>비울 것이 하나뿐입니다.</b> 대입 상태는 전부 인스턴스가 들고 있어서
        /// 새 인스턴스가 만들어지는 것만으로 깨끗하게 시작합니다.
        /// 도메인 리로드를 꺼 두었을 때 지난 실행의 값이 남는 문제가 그만큼 줄었습니다.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            active = null;
        }
    }
}
