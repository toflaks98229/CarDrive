using UnityEngine;

namespace CarDrive.Systems
{
    /// <summary>
    /// 시야에 걸린 <b>모든 거리를 한곳에서</b> 계산합니다.
    ///
    /// <b>왜 만들었는가.</b> 이 거리들은 서로 순서를 지켜야 합니다.
    /// <code>
    ///   풀 페이드 시작 &lt; 풀 페이드 끝 &lt; 풀
    ///   그림자 ≤ 시야   ·   풀 &lt; 페이드 시작 &lt; 페이드 끝 ≤ 타일 접기 = 나무 컷 ≤ 터레인 &lt; 파클립
    /// </code>
    /// 하나라도 뒤집히면 그 자리가 <b>눈에 보이는 선</b>이 됩니다. 안개보다 파클립이
    /// 가까우면 지형이 잘리고, 페이드보다 접기가 가까우면 나무가 통째로 사라집니다.
    ///
    /// 그런데 이 순서가 어디에도 적혀 있지 않고 <b>파일 셋에 흩어져</b> 각자 배율을
    /// 곱하고 있었습니다. 그래서 같은 종류의 버그가 세 번 났습니다.
    ///  1. 파클립만 배율로 당겨 안개 없이 지형이 잘렸습니다.
    ///  2. 나무 페이드가 재질에 구워진 채라 컷보다 뒤에 있었습니다.
    ///  3. 이미 배율이 적용된 <c>treeDistance</c> 에 컬러가 배율을 <b>또</b> 곱했습니다.
    ///
    /// 셋 다 "한 축을 빠뜨린" 실수입니다. 계산이 한곳에 있으면 빠뜨릴 축이 없습니다.
    /// <b>여기서만 곱하고, 쓰는 쪽은 결과만 읽습니다.</b>
    ///
    /// <b>기준값은 씬이 갖고 있습니다.</b> 카메라의 파클립과 스트리머의 활성 거리가
    /// 그것입니다. 각자 시작할 때 여기에 등록합니다. 등록 전에 물어보면
    /// 설정만으로 낼 수 있는 값으로 물러섭니다.
    ///
    /// <b><see cref="Ladder"/> 는 순수합니다.</b> 필요한 값을 전부 인자로 받고 정적 상태를
    /// 읽지 않습니다. 그래서 위의 부등식을 <b>씬 없이 EditMode 테스트로 고정</b>할 수 있습니다.
    /// 같은 종류의 버그가 세 번 난 자리라, 코드로 막아 두는 편이 낫습니다.
    /// </summary>
    public static class ViewDistances
    {
        // --- Constants ---

        /// <summary>
        /// 지수 제곱 안개가 <b>거의 다 덮는</b> 지점을 정하는 계수입니다.
        ///
        /// 가려짐은 1 - exp(-(거리 × 짙기)²) 이고, (거리 × 짙기)가 1.73 이면 약 95% 입니다.
        /// 100% 를 기다리면 짙기가 지나치게 올라가 가까운 곳까지 뿌예집니다.
        /// </summary>
        private const float FogReachFactor = 1.73f;

        /// <summary>나무 디더가 <b>시작</b>되는 지점입니다. 시야 거리에 대한 비율입니다.</summary>
        private const float FadeStartRatio = 0.70f;

        /// <summary>
        /// 나무 디더가 <b>끝나는</b> 지점입니다. 시야 거리에 대한 비율입니다.
        ///
        /// 1 보다 작아야 합니다. 디더로 다 지워지는 일이 잘라내는 것보다 먼저 끝나야
        /// 잘리는 순간이 보이지 않습니다.
        /// </summary>
        private const float FadeEndRatio = 0.97f;

        /// <summary>
        /// 파클립에 더할 여유(m)입니다.
        ///
        /// 스트리머는 타일의 <b>가장 가까운 모서리</b>로 켤지 정합니다. 그래서 켜진 타일의
        /// 반대쪽 모서리는 타일 대각선만큼 더 멉니다. 파클립이 그보다 가까우면
        /// 켜져 있는 타일의 뒤쪽이 잘려 하늘이 뚫려 보입니다.
        /// 100m 타일의 대각선이 141m 라 그보다 조금 넉넉하게 잡았습니다.
        /// </summary>
        private const float FarClipMargin = 150f;

        /// <summary>
        /// 풀이 <b>흩어져 사라지기 시작</b>하는 지점입니다. 풀 그리는 거리에 대한 비율입니다.
        ///
        /// 예전에는 이 두 비율이 재질에 <b>숫자로 구워져</b> 있었습니다(35m / 68.6m).
        /// <c>detailDistance</c> 70m 에 0.5 와 0.98 을 곱해 에디터 도구가 적어 넣은 값인데,
        /// 그 도구는 <see cref="CarDriveWorldSettings.rangeScale"/> 도 속도 단계도 몰랐습니다.
        /// 그래서 실제 그리는 거리가 49m·36.8m·24.5m 로 줄어드는 동안 페이드 창은 그대로였고,
        /// <b>시속 90 이상에서는 페이드가 시작되기도 전에 풀이 통짜로 잘렸습니다.</b>
        /// (나무가 똑같은 이유로 한 번 튀었고, 그 기록이 위의 2번입니다)
        ///
        /// 비율로 바꾸면 거리가 어떻게 줄어도 창이 함께 줄어듭니다.
        /// </summary>
        private const float GrassFadeStartRatio = 0.50f;

        /// <summary>
        /// 풀이 <b>다 사라지는</b> 지점입니다. 풀 그리는 거리에 대한 비율입니다.
        ///
        /// 1 보다 작아야 합니다. <c>Terrain.detailObjectDistance</c> 의 잘라내기는
        /// 하드 컷이라, 그보다 먼저 다 지워져 있어야 잘리는 순간이 보이지 않습니다.
        /// </summary>
        private const float GrassFadeEndRatio = 0.95f;

        /// <summary>기준값이 등록되기 전에 쓸 시야 거리(m)입니다.</summary>
        private const float FallbackView = 340f;

        /// <summary>기준값이 등록되기 전에 쓸 타일 활성 거리(m)입니다.</summary>
        private const float FallbackActive = 360f;

        /// <summary>
        /// 날씨가 시야를 줄일 수 있는 하한입니다.
        ///
        /// 아무리 나빠도 이보다 좁아지지 않습니다. 폭우에서 시야가 0에 가까워지면
        /// 지형이 발밑까지 와서야 보이고, 그것은 <b>연출이 아니라 고장으로 보입니다.</b>
        /// </summary>
        private const float MinWeatherVisibility = 0.35f;

        // --- Public Types ---

        /// <summary>
        /// 지금 배율로 계산한 거리 한 벌입니다. 가까운 것부터 먼 순서로 적었습니다.
        ///
        /// <b>이 순서가 곧 이 구조체의 필드 순서입니다.</b> 읽는 사람이 순서를
        /// 눈으로 확인할 수 있어야 하기 때문입니다.
        /// </summary>
        public readonly struct Ladder
        {
            /// <summary>지금 적용 중인 배율입니다.</summary>
            public readonly float Scale;

            /// <summary>
            /// 풀이 <b>흩어져 사라지기 시작</b>하는 거리(m)입니다.
            ///
            /// 이 구조체에서 가장 가까운 값이라 맨 앞에 적었습니다.
            /// </summary>
            public readonly float GrassFadeStart;

            /// <summary>풀이 <b>다 사라지는</b> 거리(m)입니다. 잘라내는 거리보다 가깝습니다.</summary>
            public readonly float GrassFadeEnd;

            /// <summary>
            /// 풀을 그리는 거리(m)입니다. <b>속도 단계까지 이미 곱해져 있습니다.</b>
            ///
            /// 예전에는 여기까지만 계산하고 속도 단계는 <see cref="TerrainDetailLod"/> 가
            /// 밖에서 <b>또</b> 곱했습니다. 그래서 이 사다리는 실제로 몇 미터에 풀이 잘리는지
            /// 몰랐고, 페이드 창을 여기서 낼 수가 없었습니다. 이제 속도 단계도
            /// <see cref="ReportGrassSpeedScale"/> 로 들어와 <b>곱셈이 다시 한곳에 모였습니다.</b>
            /// </summary>
            public readonly float Grass;

            /// <summary>나무·바위·건물이 디더로 지워지기 <b>시작</b>하는 거리(m)입니다.</summary>
            public readonly float FadeStart;

            /// <summary>디더로 <b>다 지워지는</b> 거리(m)입니다.</summary>
            public readonly float FadeEnd;

            /// <summary>
            /// 그림자를 그리는 거리(m)입니다. <b>시야 거리를 넘지 않습니다.</b>
            ///
            /// 안개에 다 묻히는 거리까지 그림자를 그릴 이유가 없습니다. 그린다 해도
            /// 보이지 않고, 섀도맵 해상도만 그만큼 넓게 퍼져 가까운 그림자가 거칠어집니다.
            /// </summary>
            public readonly float Shadow;

            /// <summary>
            /// 화면 밖 지형을 판정할 때 둘 여유(m)입니다.
            ///
            /// <b>왜 그림자 거리와 같은가.</b> 화면 밖 언덕이 화면 안으로 그림자를 드리울 수 있는
            /// 거리가 정확히 그림자 거리입니다. 그보다 좁게 자르면 화면 가장자리에서
            /// 그림자가 통째로 사라집니다.
            ///
            /// 예전에는 이 값이 설정에 손으로 적힌 55m 하나였고 <b>실제 그림자 거리를 몰랐습니다.</b>
            /// URP 에셋의 50m 와 우연히 맞아떨어져 있었을 뿐이라, 품질을 올리면 어긋났습니다.
            /// </summary>
            public readonly float ShadowCasterMargin;

            /// <summary>안개가 거의 다 덮는 거리(m)입니다. 시야 거리와 같습니다.</summary>
            public readonly float View;

            /// <summary>그 거리에서 95% 를 덮는 지수 제곱 안개의 짙기입니다.</summary>
            public readonly float FogDensity;

            /// <summary>지형의 나무를 잘라내는 거리(<c>Terrain.treeDistance</c>)입니다.</summary>
            public readonly float TreeCut;

            /// <summary>타일 단위로 나무·풀을 <b>접는</b> 거리(m)입니다. 잘라내는 거리보다 멉니다.</summary>
            public readonly float FoliageFold;

            /// <summary>접기가 <b>풀려나는</b> 거리(m)입니다. 경계에서 떨리지 않게 벌려 둡니다.</summary>
            public readonly float FoliageRelease;

            /// <summary>이 안의 지면은 화면 밖이어도 늘 켭니다.</summary>
            public readonly float TerrainNear;

            /// <summary>늘 켜기에서 <b>풀려나는</b> 거리(m)입니다.</summary>
            public readonly float TerrainNearRelease;

            /// <summary>이 안쪽 타일은 예산 천장까지 즉시 켭니다.</summary>
            public readonly float TerrainInstant;

            /// <summary>타일을 <b>켜는</b> 거리(m)입니다.</summary>
            public readonly float TerrainActive;

            /// <summary>
            /// 타일을 <b>끄는</b> 거리(m)입니다. 켜는 거리보다 멉니다.
            ///
            /// <b>이 항목이 없어서 생긴 문제가 있었습니다.</b> 예전에는 켜는 기준과 끄는 기준이
            /// 같은 값 하나였습니다. 그래서 경계에 걸친 타일은 플레이어가 그 선을 오갈 때마다
            /// <c>SetActive</c> 를 반복했고, <b>그것이 이 프로젝트에서 가장 비싼 토글입니다.</b>
            /// (지형이 렌더링 시스템에서 빠졌다 다시 등록되고 렌더 데이터가 재구성됩니다)
            ///
            /// 더 이상한 것은 <see cref="TerrainChunkCuller"/> 는 훨씬 싼 토글에 이미
            /// 히스테리시스를 쓰고 있었다는 점입니다. 비싼 쪽에만 빠져 있었습니다.
            /// </summary>
            public readonly float TerrainActiveRelease;

            /// <summary>카메라 파클립(m)입니다. 켜져 있는 타일을 자르지 않을 만큼 멉니다.</summary>
            public readonly float FarClip;

            /// <summary>
            /// 한 벌을 계산합니다. <b>모든 곱셈이 여기 한 곳에 있습니다.</b>
            /// </summary>
            /// <param name="settings">기준 수치를 읽을 설정</param>
            /// <param name="baseView">기준 시야 거리(m). 보통 씬의 카메라 파클립입니다.</param>
            /// <param name="baseActive">기준 타일 활성 거리(m)</param>
            /// <param name="baseInstant">기준 즉시 활성 거리(m)</param>
            /// <param name="weatherFog">날씨가 요청한 안개 짙기. 0이면 요청 없음입니다.</param>
            /// <param name="weatherView">날씨가 요청한 시야 배율(0~1). 1이면 요청 없음입니다.</param>
            /// <param name="grassSpeedScale">속도 단계가 풀 거리에 곱할 배율(0~1). 1이면 요청 없음입니다.</param>
            public Ladder(CarDriveWorldSettings settings, float baseView, float baseActive, float baseInstant,
                          float weatherFog, float weatherView, float grassSpeedScale)
            {
                Scale = Mathf.Clamp(settings.rangeScale, 0.05f, 1f);

                // <b>날씨는 시야를 좁히기만 합니다.</b> 넓히지 않습니다 —
                // 씬이 적어 둔 거리보다 멀리 보이게 만들면 타일이 없는 곳까지 보게 됩니다.
                View = Mathf.Max(20f, baseView * Scale * Mathf.Clamp(weatherView, MinWeatherVisibility, 1f));

                // 시야 거리를 덮는 데 필요한 짙기가 바닥이고, 날씨가 더 짙게 하려 하면 그것을 씁니다.
                FogDensity = Mathf.Max(FogReachFactor / View, weatherFog);

                // 속도 단계까지 <b>여기서</b> 곱합니다. 밖에서 곱하면 페이드 창이 이 값을
                // 따라올 수 없고, 그것이 정확히 풀이 통짜로 잘리던 이유였습니다.
                Grass = settings.detailDistance * Scale * Mathf.Clamp(grassSpeedScale, 0.05f, 1f);

                // 페이드는 <b>비율</b>입니다. 거리가 어떻게 줄어도 창이 함께 줄어듭니다.
                GrassFadeStart = Grass * GrassFadeStartRatio;
                GrassFadeEnd = Grass * GrassFadeEndRatio;

                // <b>그림자는 시야를 넘지 못합니다.</b> 안개에 다 묻히는 거리까지 그려도
                // 보이지 않고, 섀도맵만 넓게 퍼져 가까운 그림자가 거칠어집니다.
                Shadow = Mathf.Min(settings.shadowDistance * Scale, View);

                // 화면 밖 캐스터를 담을 여유는 <b>그림자 거리</b>입니다.
                // 설정의 값은 하한으로만 씁니다. (그림자가 짧아도 먼 언덕 실루엣은 남기고 싶을 때)
                ShadowCasterMargin = Mathf.Max(Shadow, settings.shadowMargin);

                FadeStart = View * FadeStartRatio;
                FadeEnd = View * FadeEndRatio;

                // 잘라내는 거리는 시야 거리와 같습니다. 디더가 그보다 먼저(0.97) 끝납니다.
                TreeCut = View;

                // <b>접기는 잘라내기보다 멀어야 합니다.</b> 접기는 타일 단위 하드 스위치라,
                // 이보다 가까우면 디더가 돌기도 전에 통째로 사라집니다.
                //
                // foliageDistance 는 인스펙터의 기준값이라 배율을 곱합니다.
                // TreeCut 은 <b>이미 곱해진</b> 값이라 다시 곱하지 않습니다. 여기가 한 번 틀렸던 자리입니다.
                FoliageFold = Mathf.Max(settings.foliageDistance * Scale, TreeCut);
                FoliageRelease = FoliageFold + settings.cullingHysteresis;

                TerrainNear = settings.terrainNearDistance * Scale;
                TerrainNearRelease = TerrainNear + settings.cullingHysteresis;

                TerrainInstant = baseInstant * Scale;
                TerrainActive = Mathf.Max(TerrainInstant, baseActive * Scale);

                // 끄는 거리는 켜는 거리보다 <b>넉넉히</b> 멉니다.
                // 컬링 히스테리시스(20m)보다 크게 잡는 이유가 있습니다 — 타일을 껐다 켜는 일이
                // 나무·풀을 접는 일보다 훨씬 비싸므로, 떨림을 막는 값도 그만큼 커야 합니다.
                TerrainActiveRelease = TerrainActive + Mathf.Max(0f, settings.tileStreamingHysteresis);

                // <b>꺼지지 않고 남아 있는</b> 타일의 먼 쪽 모서리까지 담아야 합니다.
                // 여기에 TerrainActive 를 쓰면 히스테리시스 구간의 타일 뒤쪽이 잘려 하늘이 뚫립니다.
                FarClip = Mathf.Max(View, TerrainActiveRelease + FarClipMargin);
            }
        }

        // --- Private Member Variables ---

        /// <summary>씬이 알려 준 기준 시야 거리입니다.</summary>
        private static float baseView = -1f;

        /// <summary>씬이 알려 준 기준 타일 활성 거리입니다.</summary>
        private static float baseActive = -1f;

        /// <summary>씬이 알려 준 기준 즉시 활성 거리입니다.</summary>
        private static float baseInstant = -1f;

        /// <summary>
        /// 날씨가 요청한 안개 짙기입니다. 0이면 요청이 없다는 뜻입니다.
        ///
        /// <b>요청이지 명령이 아닙니다.</b> 시야 거리를 덮는 데 필요한 짙기가 이보다 크면
        /// 그쪽이 이깁니다. 안개가 시야보다 옅으면 지형이 끝나는 자리가 그대로 보이기 때문입니다.
        /// </summary>
        private static float weatherFogDensity;

        /// <summary>날씨가 요청한 시야 배율입니다. 1이면 요청이 없다는 뜻입니다.</summary>
        private static float weatherVisibility = 1f;

        /// <summary>
        /// 속도 단계가 요청한 풀 거리 배율입니다. 1이면 요청이 없다는 뜻입니다.
        ///
        /// <b>날씨와 같은 방식입니다.</b> 요청은 밖에서 오고 곱셈은 사다리가 합니다.
        /// </summary>
        private static float grassSpeedScale = 1f;

        // --- Public Properties ---

        /// <summary>
        /// 지금 배율로 계산한 거리 한 벌입니다.
        ///
        /// 곱셈 몇 번이라 매 프레임 여러 번 물어도 됩니다.
        /// 값을 들고 있지 말고 <b>쓸 때마다 물어보세요.</b> 배율은 실행 중에 바뀝니다.
        /// </summary>
        public static Ladder Current
        {
            get
            {
                return new Ladder(
                    CarDriveWorldSettings.Instance,
                    baseView > 0f ? baseView : FallbackView,
                    baseActive > 0f ? baseActive : FallbackActive,
                    baseInstant > 0f ? baseInstant : FallbackActive,
                    weatherFogDensity,
                    weatherVisibility,
                    grassSpeedScale);
            }
        }

        // --- Public Methods ---

        /// <summary>
        /// 기준 시야 거리를 알립니다. 카메라를 아는 쪽이 시작할 때 한 번 부르세요.
        ///
        /// <b>한 번만 받습니다.</b> 배율이 곱해진 값을 다시 기준으로 삼으면
        /// 거리가 계속 깎여 나가고, 배율을 1 로 되돌려도 원래대로 돌아오지 않습니다.
        /// </summary>
        /// <param name="view">배율을 곱하기 <b>전</b>의 시야 거리(m)</param>
        public static void SetViewBase(float view)
        {
            if (baseView > 0f || view <= 0f) return;
            baseView = view;
        }

        /// <summary>
        /// 기준 타일 거리를 알립니다. <see cref="Gameplay.WorldStreamer"/> 가 시작할 때 부릅니다.
        /// </summary>
        /// <param name="active">배율을 곱하기 전의 활성 거리(m)</param>
        /// <param name="instant">배율을 곱하기 전의 즉시 활성 거리(m)</param>
        public static void SetTileBases(float active, float instant)
        {
            if (active > 0f) baseActive = active;
            if (instant > 0f) baseInstant = instant;
        }

        /// <summary>
        /// 날씨가 시야에 미칠 영향을 <b>요청합니다.</b> 매 프레임 불러도 됩니다.
        ///
        /// <b>왜 요청인가.</b> 예전에는 <see cref="WeatherRig"/>가 <c>RenderSettings.fog*</c>와
        /// <c>camera.farClipPlane</c>을 <b>직접</b> 썼습니다. 그런데 같은 값을
        /// <see cref="ViewRangeScaler"/>도 매 프레임 썼고, 실행 순서가 각각 0과 200이라
        /// <b>늦게 도는 쪽이 언제나 이겼습니다.</b> 그래서 날씨의 시야 축소는 한 프레임도
        /// 화면에 남지 못했고, 그 사실이 어디에도 드러나지 않았습니다.
        ///
        /// 이제 <b>전역 상태를 쓰는 곳은 <see cref="ViewRangeScaler"/> 하나뿐입니다.</b>
        /// 날씨는 값을 여기에 맡기고, 두 요구의 조정은 <see cref="Ladder"/>의 읽을 수 있는
        /// 두 줄이 합니다. 순서를 다투던 것이 계산 한 줄로 바뀌었습니다.
        /// </summary>
        /// <param name="fogDensity">날씨가 원하는 안개 짙기. 0이면 요청하지 않습니다.</param>
        /// <param name="visibility">날씨가 원하는 시야 배율(0~1). 1이면 요청하지 않습니다.</param>
        public static void ReportWeather(float fogDensity, float visibility)
        {
            weatherFogDensity = Mathf.Max(0f, fogDensity);
            weatherVisibility = Mathf.Clamp(visibility, 0f, 1f);
        }

        /// <summary>
        /// 속도 단계가 풀 거리에 곱할 배율을 <b>요청합니다.</b> 매 프레임 불러도 됩니다.
        ///
        /// <b>왜 요청인가.</b> 속도 단계를 아는 것은 <see cref="TerrainDetailLod"/> 하나뿐인데,
        /// 그것이 직접 곱해 버리면 <b>사다리가 실제 풀 거리를 모릅니다.</b> 그러면 페이드 창을
        /// 여기서 낼 수 없고, 재질에 숫자를 구워 넣는 수밖에 없습니다. 실제로 그렇게 되어 있었고
        /// 그 숫자가 배율을 몰라서 시속 90 이상에서 풀이 통짜로 잘렸습니다.
        ///
        /// <b>알리는 쪽이 지켜야 할 것이 하나 있습니다.</b> 이 값을 바꾸면 사다리는 즉시
        /// 새 거리를 내지만, 지형 103장에 <c>detailObjectDistance</c> 를 대입하는 일은
        /// 예산제라 몇 프레임에 걸쳐 끝납니다. 그 사이에는 옛 거리와 새 거리가 섞여 있으므로
        /// <b>둘 중 짧은 쪽</b>을 알려야 합니다. 길게 알리면 아직 옛 거리인 타일에서
        /// 페이드가 끝나기 전에 풀이 잘립니다.
        /// </summary>
        /// <param name="scale">속도 단계가 원하는 배율(0~1). 1이면 요청하지 않습니다.</param>
        public static void ReportGrassSpeedScale(float scale)
        {
            grassSpeedScale = Mathf.Clamp(scale, 0.05f, 1f);
        }

        /// <summary>
        /// 날씨의 요청을 물립니다. 날씨 표현이 꺼질 때 부르세요.
        /// 부르지 않으면 마지막으로 요청한 폭우가 그대로 남습니다.
        /// </summary>
        public static void ClearWeather()
        {
            weatherFogDensity = 0f;
            weatherVisibility = 1f;
        }

        // --- Private Methods ---

        /// <summary>
        /// 플레이 모드에 들어갈 때 기준값을 비웁니다.
        /// 도메인 리로드를 꺼 두면 지난 실행의 기준이 그대로 남습니다.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            baseView = -1f;
            baseActive = -1f;
            baseInstant = -1f;
            weatherFogDensity = 0f;
            weatherVisibility = 1f;
            grassSpeedScale = 1f;
        }
    }
}
