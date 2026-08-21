using UnityEngine;

namespace CarDrive.Systems
{
    /// <summary>
    /// 시야에 걸린 <b>모든 거리를 한곳에서</b> 계산합니다.
    ///
    /// <b>왜 만들었는가.</b> 이 거리들은 서로 순서를 지켜야 합니다.
    /// <code>
    ///   풀 &lt; 나무 페이드 시작 &lt; 페이드 끝 ≤ 타일 접기 = 나무 컷 ≤ 터레인 있는 거리 &lt; 파클립
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

        /// <summary>기준값이 등록되기 전에 쓸 시야 거리(m)입니다.</summary>
        private const float FallbackView = 340f;

        /// <summary>기준값이 등록되기 전에 쓸 타일 활성 거리(m)입니다.</summary>
        private const float FallbackActive = 360f;

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

            /// <summary>풀을 그리는 거리(m)입니다. 속도 단계는 여기에 <b>더</b> 곱해집니다.</summary>
            public readonly float Grass;

            /// <summary>나무·바위·건물이 디더로 지워지기 <b>시작</b>하는 거리(m)입니다.</summary>
            public readonly float FadeStart;

            /// <summary>디더로 <b>다 지워지는</b> 거리(m)입니다.</summary>
            public readonly float FadeEnd;

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

            /// <summary>타일이 켜져 있는 가장 먼 거리(m)입니다.</summary>
            public readonly float TerrainActive;

            /// <summary>카메라 파클립(m)입니다. 켜져 있는 타일을 자르지 않을 만큼 멉니다.</summary>
            public readonly float FarClip;

            /// <summary>
            /// 한 벌을 계산합니다. <b>모든 곱셈이 여기 한 곳에 있습니다.</b>
            /// </summary>
            /// <param name="settings">기준 수치를 읽을 설정</param>
            /// <param name="baseView">기준 시야 거리(m). 보통 씬의 카메라 파클립입니다.</param>
            /// <param name="baseActive">기준 타일 활성 거리(m)</param>
            /// <param name="baseInstant">기준 즉시 활성 거리(m)</param>
            public Ladder(CarDriveWorldSettings settings, float baseView, float baseActive, float baseInstant)
            {
                Scale = Mathf.Clamp(settings.rangeScale, 0.05f, 1f);

                View = Mathf.Max(20f, baseView * Scale);
                FogDensity = FogReachFactor / View;

                Grass = settings.detailDistance * Scale;

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

                // 켜져 있는 타일의 먼 쪽 모서리까지 담아야 합니다.
                FarClip = Mathf.Max(View, TerrainActive + FarClipMargin);
            }
        }

        // --- Private Member Variables ---

        /// <summary>씬이 알려 준 기준 시야 거리입니다.</summary>
        private static float baseView = -1f;

        /// <summary>씬이 알려 준 기준 타일 활성 거리입니다.</summary>
        private static float baseActive = -1f;

        /// <summary>씬이 알려 준 기준 즉시 활성 거리입니다.</summary>
        private static float baseInstant = -1f;

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
                    baseInstant > 0f ? baseInstant : FallbackActive);
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
        }
    }
}
