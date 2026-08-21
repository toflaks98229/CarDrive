using System.Collections.Generic;
using UnityEngine;
using CarDrive.Common;

namespace CarDrive.Systems
{
    /// <summary>
    /// 지형 타일의 <b>나무와 풀</b>을 화면 밖일 때 접습니다.
    ///
    /// 이 월드는 100m짜리 타일이 103장인데, 어느 순간이든 화면에 들어오는 것은 몇 장뿐입니다.
    /// 나머지 타일에서 풀 조각을 추려 내고 그리기 명령을 만드는 일을 하지 않게 하는 것이 목적입니다.
    ///
    /// <b>이 컬러가 하는 일은 이제 하나뿐입니다 — 화면 밖 타일의 나무·풀을 접는 것.</b>
    ///
    /// 예전에는 셋을 했습니다. 지면 끄기, 거리로 접기, 화면 밖 접기. 앞의 둘을 걷어냈습니다.
    ///
    ///  1. <b>지면 끄기</b>(<c>cullTerrainSurface</c>)는 기본에서 꺼졌습니다. 유니티가 이미
    ///     터레인을 패치 단위로 프러스텀 컬링하므로 아끼는 것이 거의 없는데,
    ///     <c>Terrain.enabled</c> 토글은 렌더 데이터를 재구성하는 확실한 비용입니다.
    ///     이 프로젝트의 병목이 드로우 콜이 아니라 <b>켜고 끄는 CPU 비용</b>이라면,
    ///     이득이 불확실한 토글은 하지 않는 것이 맞습니다.
    ///
    ///  2. <b>거리로 접기</b>는 없앴습니다. 나무는 <c>treeDistance</c>, 풀은
    ///     <c>detailObjectDistance</c> 가 이미 반경 거리로 잘라 냅니다. 유니티가 하는 그 컬링은
    ///     부드러운데 타일 단위 접기는 <b>하드 스위치</b>라, 같은 일을 두 번 하면서
    ///     결과는 더 나빴고 토글 비용까지 얹혔습니다.
    ///
    /// <b>화면 밖 접기만 남은 이유.</b> 이건 유니티가 대신 해 주지 않습니다.
    /// 터레인의 나무·디테일 컬링 패스는 터레인마다 도는데, 화면에 없는 타일에서도
    /// 그 목록을 훑습니다. 타일이 103장이면 그 비용이 쌓입니다.
    /// <b>타일을 크게 키워 장수가 줄면 이 항목의 이득도 함께 줄어듭니다.</b>
    /// 그때는 <c>foldOffscreenFoliage</c> 를 꺼서 이 컬러를 통째로 재워도 됩니다.
    ///
    /// <b>콜라이더는 어느 경우에도 끄지 않습니다.</b>
    /// Terrain 을 끄면 그리기만 멈추고 TerrainCollider 는 따로 살아 있습니다.
    /// 함께 꺼 버리면 화면 밖으로 나간 차가 땅을 뚫고 떨어집니다.
    ///
    /// <b>켜는 것은 예산제, 끄는 것은 즉시입니다.</b> 켜는 일이 비싸기 때문입니다.
    /// 화면 판정에는 켤 때와 끌 때의 경계를 달리 두어 가장자리에서 떨리지 않게 합니다.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class TerrainChunkCuller : MonoBehaviour
    {
        // --- Constants ---

        // --- Private Types ---

        /// <summary>
        /// 타일 하나에 대해 <b>미리 구해 둔 것</b>입니다.
        ///
        /// <b>왜 캐시하는가.</b> 타일은 움직이지 않습니다. 그런데 예전에는 매 프레임
        /// <c>terrainData.bounds</c>를 읽고 위치를 더하고 <c>Expand</c>로 넓히는 일을
        /// 타일 수만큼 다시 했습니다. 이 월드는 타일이 <b>103장</b>이라
        /// 초당 6천 번 넘게 같은 답을 다시 구하고 있었습니다.
        ///
        /// 지형이 움직이지 않는 한 답은 그대로이므로 목록을 갱신할 때 한 번만 구합니다.
        /// </summary>
        private struct TerrainEntry
        {
            /// <summary>대상 지형입니다.</summary>
            public Terrain Terrain;

            /// <summary>켜져 있는지 확인할 오브젝트입니다. 매 프레임 <c>terrain.gameObject</c>를 타지 않기 위한 것입니다.</summary>
            public GameObject Owner;

            /// <summary>그림자 여유까지 더한 월드 경계입니다. <b>켤지</b> 판정할 때 씁니다.</summary>
            public Bounds PaddedBounds;

            /// <summary>
            /// 히스테리시스만큼 더 넓힌 경계입니다. <b>끌지</b> 판정할 때 씁니다.
            /// 켜는 기준보다 넓어서, 경계에 걸친 타일이 껐다 켜기를 반복하지 않습니다.
            /// </summary>
            public Bounds KeepBounds;

            /// <summary>여유를 더하지 않은 월드 경계입니다. 거리를 잴 때 씁니다.</summary>
            public Bounds RawBounds;

            /// <summary>이 거리 제곱 안이면 지면을 <b>화면 밖이어도</b> 켜 둡니다.</summary>
            public float NearDistanceSqr;

            /// <summary>가까움 판정에서 <b>풀려나는</b> 거리의 제곱입니다. 들어오는 거리보다 멉니다.</summary>
            public float NearReleaseSqr;

            /// <summary>마지막으로 적어 넣은 <c>enabled</c> 값입니다.</summary>
            public bool LastEnabled;

            /// <summary>마지막으로 적어 넣은 <c>drawTreesAndFoliage</c> 값입니다.</summary>
            public bool LastDrawFoliage;
        }

        /// <summary>
        /// 이번 프레임에 나무·풀을 켜 달라고 신청한 타일입니다.
        /// 예산을 넘으면 <b>가까운 것부터</b> 켜기 위해 거리를 함께 담습니다.
        /// </summary>
        private struct FoliageRequest
        {
            /// <summary>신청한 타일의 색인입니다.</summary>
            public int Index;

            /// <summary>카메라까지의 거리 제곱입니다. 가까운 것이 먼저입니다.</summary>
            public float SqrDistance;
        }

        // --- Private Member Variables ---

        private static readonly Plane[] planes = new Plane[6];

        /// <summary>
        /// 이번 프레임의 나무·풀 켜기 신청 목록입니다.
        /// 매 프레임 비우고 다시 채우므로 할당이 생기지 않습니다.
        /// </summary>
        private static readonly List<FoliageRequest> foliageRequests = new List<FoliageRequest>(16);

        /// <summary>
        /// 이번 프레임의 <b>지면</b> 켜기 신청 목록입니다. 나무·풀과 예산을 따로 씁니다.
        /// 지면은 없으면 발밑에 구멍이 보이고, 나무·풀은 없어도 허전할 뿐이라
        /// 한쪽이 다른 쪽의 예산을 잡아먹으면 안 됩니다.
        /// </summary>
        private static readonly List<FoliageRequest> surfaceRequests = new List<FoliageRequest>(16);

        /// <summary>가까운 것이 먼저 오도록 하는 비교 기준입니다.</summary>
        private static readonly System.Comparison<FoliageRequest> ByDistance = CompareByDistance;

        /// <summary>타일별로 미리 구해 둔 값들입니다.</summary>
        private static TerrainEntry[] entries;

        /// <summary>
        /// 경계 캐시를 만들 때 본 지형 목록의 번호입니다.
        ///
        /// <b>자체 주기를 두지 않습니다.</b> 예전에는 이 컬러가 2초 주기로 목록을 다시 찾고
        /// 경계도 함께 다시 구했습니다. 이제 목록은 <see cref="TerrainRegistry"/> 가 갖고 있는데,
        /// 여기서 주기를 또 두면 <b>두 갱신 시점이 어긋납니다.</b> 그 틈에 캐시가 이미 사라진
        /// 지형을 가리키게 됩니다. 목록이 바뀐 그 순간에 캐시도 다시 짜는 것이 맞습니다.
        /// </summary>
        private static int cachedRegistryVersion = -1;

        /// <summary>
        /// 캐시를 만들 때 쓴 설정값입니다. 이 값이 달라지면 경계를 다시 구해야 합니다.
        /// (실행 중에 그림자 여유나 접는 거리를 조절할 수 있습니다)
        /// </summary>
        private static float cachedShadowMargin = float.NaN;

        /// <summary>캐시를 만들 때 쓴 히스테리시스 간격입니다.</summary>
        private static float cachedHysteresis = float.NaN;

        /// <summary>캐시를 만들 때 쓴 "가까운 지면" 거리입니다.</summary>
        private static float cachedNearDistance = float.NaN;

        /// <summary>캐시를 만들 때 쓴 전체 거리 배율입니다.</summary>
        private static float cachedRangeScale = float.NaN;

        /// <summary>다음 지면 판정 시각입니다. 판정은 주기로, 적용은 매 프레임입니다.</summary>
        private static float nextSurfaceCheck;

        /// <summary>다음 나무·풀 판정 시각입니다.</summary>
        private static float nextFoliageCheck;

        /// <summary>마지막으로 센 "그리기로 정한 타일 수"입니다. 판정을 건너뛴 프레임에 돌려줍니다.</summary>
        private static int lastShown;

        /// <summary>
        /// 두 기능이 모두 꺼진 상태에서 지형을 이미 원래대로 돌려놓았는지입니다.
        /// 돌려놓았으면 그 뒤로는 아무것도 하지 않고 즉시 반환합니다.
        /// </summary>
        private static bool idleRestored;

        /// <summary>지금 신청 목록이 가까운 순으로 세워져 있는지입니다. 매 프레임 다시 세우지 않기 위한 것입니다.</summary>
        private static bool surfaceSorted;

        // --- Unity Event Functions ---

        /// <summary>매 프레임 어느 타일을 그릴지 정합니다.</summary>
        void LateUpdate()
        {
            ApplyNow(GameContext.MainCamera);
        }

        // --- Public Methods ---

        /// <summary>
        /// 지금 카메라를 기준으로 타일을 켜고 끕니다.
        ///
        /// 밖에서도 부를 수 있게 열어 두었습니다. 확인 도구가 이 코드를 그대로 부릅니다.
        /// </summary>
        /// <param name="camera">기준이 될 카메라</param>
        /// <returns>그리기로 정한 타일 수</returns>
        public static int ApplyNow(Camera camera)
        {
            if (camera == null) return 0;

            CarDriveWorldSettings settings = CarDriveWorldSettings.Instance;

            // 꺼 두었으면 모두 그립니다. 문제가 컬링 때문인지 가릴 때 씁니다.
            if (!settings.chunkCulling)
            {
                RestoreAll();
                return 0;
            }

            // <b>할 일이 없으면 즉시 반환합니다.</b>
            //
            // 지면 컬링과 화면 밖 접기가 둘 다 꺼져 있으면 이 컬러가 할 일이 없습니다.
            // 그런데도 예전에는 매 프레임 프러스텀 평면을 구하고 타일 103장을 훑었습니다.
            // 타일을 크게 키워 두 기능을 모두 끄는 구성에서 그 비용이 통째로 낭비입니다.
            //
            // 다만 <b>한 번은 돌아야 합니다</b> — 예전에 꺼 둔 것을 도로 켜야 하기 때문입니다.
            if (!settings.cullTerrainSurface && !settings.foldOffscreenFoliage)
            {
                if (idleRestored) return 0;

                RestoreAll();
                idleRestored = true;
                return 0;
            }
            idleRestored = false;

            RefreshIfNeeded(settings);

            // <b>나무·풀 판정도 주기로 합니다.</b>
            //
            // 이 판정에 거리가 섞여 있던 시절에는 매 프레임 해야 했습니다. 플레이어가 움직이면
            // 결과가 바뀌었기 때문입니다. 지금은 <b>화면 안인가</b> 하나뿐이고, 그 판정에는
            // 그림자 여유(55m)가 붙어 있어 실제 화면 가장자리보다 훨씬 바깥에서 켜집니다.
            // 몇 십 밀리초 늦어도 보이지 않는 반면, TestPlanesAABB 를 103장에 매 프레임
            // 돌리는 비용은 그대로 듭니다.
            bool foliageDue = settings.foliageCheckInterval <= 0f
                              || Time.unscaledTime >= nextFoliageCheck;

            bool surfaceDueNow = Time.unscaledTime >= nextSurfaceCheck;

            // 둘 다 아직이면 이번 프레임은 예산만 덜어냅니다. 프러스텀 계산도 건너뜁니다.
            if (!foliageDue && !surfaceDueNow)
            {
                ApplySurfaceBudget(settings.maxSurfaceActivationsPerFrame);
                ApplyFoliageBudget(settings.maxFoliageActivationsPerFrame);
                return lastShown;
            }

            if (foliageDue && settings.foliageCheckInterval > 0f)
            {
                nextFoliageCheck = Time.unscaledTime + settings.foliageCheckInterval;
            }

            GeometryUtility.CalculateFrustumPlanes(camera, planes);

            Vector3 eye = camera.transform.position;
            int shown = 0;

            foliageRequests.Clear();

            // <b>지면 판정은 매 프레임 하지 않습니다.</b>
            //
            // 지면이 화면에 들고 나는 일은 프레임 단위로 일어나지 않습니다. 0.25초 사이에
            // 사람이 돌 수 있는 각도나 차가 가는 거리로는 판정이 뒤집힐 일이 거의 없습니다.
            // 그런데도 매 프레임 판정하면 카메라의 미세한 떨림 하나하나가 토글 후보가 되고,
            // 그 토글이 이 시스템에서 가장 비싼 일입니다.
            //
            // 그래서 <b>판정은 주기로, 적용은 매 프레임 조금씩</b> 합니다.
            // 신청 목록은 주기마다 새로 짜고, 프레임마다 예산만큼 덜어냅니다.
            bool surfaceDue = surfaceDueNow;
            if (surfaceDue)
            {
                nextSurfaceCheck = Time.unscaledTime + Mathf.Max(0.05f, settings.surfaceCheckInterval);
                surfaceRequests.Clear();
                surfaceSorted = false;
            }

            for (int i = 0; i < entries.Length; i++)
            {
                Terrain terrain = entries[i].Terrain;
                if (terrain == null) continue;

                // WorldStreamer 가 꺼 둔 타일은 건드리지 않습니다. 그쪽이 주인입니다.
                if (!entries[i].Owner.activeInHierarchy) continue;

                // 경계는 목록을 갱신할 때 이미 구해 두었습니다. 지형은 움직이지 않습니다.
                //
                // <b>켤 때와 끌 때의 기준이 다릅니다.</b> 같으면 경계에 걸친 타일이
                // 시야가 미세하게 흔들릴 때마다 껐다 켜기를 반복하고, 그 전환이 곧 비용입니다.
                // 이미 켜져 있는 타일은 더 넓은 경계를 벗어나야 꺼집니다.
                bool visible = entries[i].LastEnabled
                    ? GeometryUtility.TestPlanesAABB(planes, entries[i].KeepBounds)
                    : GeometryUtility.TestPlanesAABB(planes, entries[i].PaddedBounds);

                // <b>거리는 필요할 때만 구합니다.</b>
                //
                // 예전에는 여기서 무조건 <c>Bounds.SqrDistance</c> 를 불렀습니다. 지면 판정과
                // 나무·풀 판정이 함께 쓰던 시절에는 맞는 판단이었지만, 지금 이 값을 쓰는 곳은
                // <b>지면 판정 하나뿐</b>이고 그 판정은 기본에서 꺼져 있습니다.
                // 타일 103장에 매 프레임 도는 코드라, 쓰지 않는 값을 구하는 비용이 그대로 쌓였습니다.
                bool surfaceCheck = settings.cullTerrainSurface && surfaceDue;
                float sqrToEyeSurface = surfaceCheck ? entries[i].RawBounds.SqrDistance(eye) : 0f;

                if (surfaceCheck)
                {
                    // <b>가까운 지면은 판정에서 뺍니다.</b>
                    //
                    // 발밑 타일은 제자리에서 한 바퀴만 돌아도 화면을 들락날락합니다.
                    // 그때마다 렌더 데이터를 다시 짜면 아끼는 것보다 비쌉니다.
                    // 그래서 가까운 것은 화면 밖이어도 늘 켜 두고, <b>먼 것만</b> 화면으로 거릅니다.
                    // 여기도 들어오는 거리와 풀려나는 거리를 달리 두어 경계에서 떨리지 않게 합니다.
                    bool isNear = entries[i].LastEnabled
                        ? sqrToEyeSurface < entries[i].NearReleaseSqr
                        : sqrToEyeSurface < entries[i].NearDistanceSqr;

                    bool surfaceOn = isNear || visible;

                    if (entries[i].LastEnabled != surfaceOn)
                    {
                        // <b>끄는 것은 즉시, 켜는 것은 신청만.</b>
                        // 이 옵션이 예전에 손해였던 이유가 정확히 여기입니다.
                        // 시야를 홱 돌리면 여러 장이 한 프레임에 켜져 그 프레임이 통째로 늘어졌습니다.
                        if (!surfaceOn)
                        {
                            entries[i].LastEnabled = false;
                            terrain.enabled = false;
                            WorldProfiler.Count(WorldProfiler.Counter.SurfaceToggled);
                        }
                        else
                        {
                            surfaceRequests.Add(new FoliageRequest { Index = i, SqrDistance = sqrToEyeSurface });
                        }
                    }
                }
                else if (!settings.cullTerrainSurface && !entries[i].LastEnabled)
                {
                    // 지면 컬링을 끈 상태에서는 예전에 꺼 둔 타일을 도로 켜 줘야 합니다.
                    // 그러지 않으면 설정을 바꾼 순간부터 그 타일이 영영 보이지 않습니다.
                    entries[i].LastEnabled = true;
                    terrain.enabled = true;
                }

                if (visible) shown++;

                // <b>거리로는 더 이상 접지 않습니다. 화면 밖일 때만 접습니다.</b>
                //
                // 왜 거리 판정을 뺐는가. 나무는 <c>Terrain.treeDistance</c>, 풀은
                // <c>Terrain.detailObjectDistance</c> 가 이미 <b>반경 거리</b>로 잘라 냅니다.
                // 유니티가 하는 그 컬링은 부드러운데, 여기서 하던 타일 단위 접기는
                // <b>하드 스위치</b>라 한 장 분량이 한꺼번에 나타나고 사라졌습니다.
                // 같은 일을 두 번 하면서 결과는 더 나빴고, 그 토글 비용까지 얹혔습니다.
                //
                // <b>화면 밖 판정은 남깁니다.</b> 이건 유니티가 대신 해 주지 않습니다 —
                // 터레인의 나무·디테일 컬링 패스는 터레인마다 도는데, 화면에 없는 타일에서도
                // 그 목록을 훑습니다. 타일이 103장이면 그 비용이 쌓입니다.
                // (타일을 크게 키워 장수가 줄면 이 항목도 꺼도 됩니다 — foldOffscreenFoliage)
                //
                // 켤 때와 끌 때의 화면 판정 기준이 다른 것은 위의 visible 이 이미 처리합니다.
                bool drawFoliage = !settings.foldOffscreenFoliage || visible;

                if (entries[i].LastDrawFoliage == drawFoliage) continue;

                // <b>끄는 것은 즉시, 켜는 것은 신청만 합니다.</b>
                // 끄는 일은 싸지만 켜는 일은 그 타일의 렌더 데이터를 다시 짜는 것이라,
                // 시야를 빠르게 돌려 여러 장이 한꺼번에 몰리면 그 프레임이 통째로 늘어집니다.
                if (!drawFoliage)
                {
                    entries[i].LastDrawFoliage = false;
                    terrain.drawTreesAndFoliage = false;
                    WorldProfiler.Count(WorldProfiler.Counter.FoliageToggled);
                    continue;
                }

                // 정렬용 거리는 <b>여기서</b> 구합니다. 신청은 화면 경계를 막 넘은 타일만 하므로
                // 대개 몇 장뿐이고, 위에서 103장 전부에 대해 미리 구할 이유가 없습니다.
                foliageRequests.Add(new FoliageRequest
                {
                    Index = i,
                    SqrDistance = entries[i].RawBounds.SqrDistance(eye)
                });
            }

            ApplySurfaceBudget(settings.maxSurfaceActivationsPerFrame);
            ApplyFoliageBudget(settings.maxFoliageActivationsPerFrame);

            lastShown = shown;
            return shown;
        }

        // --- Private Methods ---

        /// <summary>
        /// 타일 목록과 그에 딸린 계산값을 다시 만듭니다.
        ///
        /// 주기가 지났거나, 설정이 바뀌어 경계를 다시 구해야 할 때만 실제로 일합니다.
        /// </summary>
        /// <param name="settings">그림자 여유와 접는 거리를 읽을 설정</param>
        /// <summary>
        /// 신청된 타일 중 예산만큼만 나무·풀을 켭니다. <b>가까운 것이 먼저입니다.</b>
        ///
        /// 나머지는 다음 프레임에 다시 신청됩니다. 조건이 그대로면 계속 신청되므로
        /// 놓치는 타일은 없고, 켜지는 시점만 몇 프레임 흩어집니다.
        ///
        /// 가까운 것을 먼저 켜는 이유가 있습니다. 목록 순서대로 켜면 그 순서는 타일을 구울 때
        /// 정해진 것이라 거리와 아무 상관이 없고, <b>늘 같은 타일이 뒤로 밀립니다.</b>
        /// (<see cref="Gameplay.WorldStreamer"/> 가 같은 문제를 겪고 같은 방식으로 고쳤습니다)
        /// </summary>
        /// <param name="budget">이번 프레임에 켤 수 있는 최대 수</param>
        private static void ApplyFoliageBudget(int budget)
        {
            if (foliageRequests.Count == 0) return;

            // 신청은 조건을 <b>막 만족한</b> 타일만 하므로 대개 몇 개뿐입니다.
            // 예산 안에 들어오면 정렬할 이유가 없습니다.
            if (foliageRequests.Count > budget) foliageRequests.Sort(ByDistance);

            int count = Mathf.Min(budget, foliageRequests.Count);
            for (int i = 0; i < count; i++)
            {
                int index = foliageRequests[i].Index;
                Terrain terrain = entries[index].Terrain;
                if (terrain == null) continue;

                entries[index].LastDrawFoliage = true;
                terrain.drawTreesAndFoliage = true;
                WorldProfiler.Count(WorldProfiler.Counter.FoliageToggled);
            }
        }

        /// <summary>
        /// 가까운 것이 먼저 오도록 비교합니다.
        /// </summary>
        /// <param name="a">앞쪽 신청</param>
        /// <param name="b">뒤쪽 신청</param>
        /// <returns>a가 더 가까우면 음수</returns>
        private static int CompareByDistance(FoliageRequest a, FoliageRequest b)
        {
            return a.SqrDistance.CompareTo(b.SqrDistance);
        }

        /// <summary>
        /// 신청된 타일 중 예산만큼만 지면을 켭니다. <b>가까운 것이 먼저입니다.</b>
        ///
        /// 나무·풀과 예산을 나눠 쓰는 이유가 있습니다. 지면이 늦게 켜지면 발밑에 구멍이
        /// 보이지만 나무가 늦게 서는 것은 허전할 뿐입니다. 한 예산을 나눠 쓰면
        /// 나무 신청이 몰린 프레임에 지면이 밀립니다. 무게가 다른 둘을 같은 줄에 세우지 않습니다.
        ///
        /// 못 켠 것은 다음 프레임에 다시 신청됩니다. 조건이 그대로면 계속 신청되므로
        /// 놓치는 타일은 없고, 켜지는 시점만 몇 프레임 흩어집니다.
        /// </summary>
        /// <param name="budget">이번 프레임에 켤 수 있는 최대 수</param>
        private static void ApplySurfaceBudget(int budget)
        {
            if (surfaceRequests.Count == 0) return;

            // 새로 짜인 목록이면 가까운 것부터 세웁니다. 이미 세워 둔 것은 다시 세우지 않습니다.
            if (!surfaceSorted)
            {
                surfaceRequests.Sort(ByDistance);
                surfaceSorted = true;
            }

            int count = Mathf.Min(budget, surfaceRequests.Count);
            for (int i = 0; i < count; i++)
            {
                int index = surfaceRequests[i].Index;
                if (index < 0 || index >= entries.Length) continue;

                Terrain terrain = entries[index].Terrain;
                if (terrain == null) continue;

                entries[index].LastEnabled = true;
                terrain.enabled = true;
                WorldProfiler.Count(WorldProfiler.Counter.SurfaceToggled);
            }

            // <b>처리한 것은 목록에서 덜어냅니다.</b>
            // 판정은 주기로만 하므로, 남은 것을 그대로 두면 다음 주기까지 켜지지 않습니다.
            // 매 프레임 조금씩 덜어내면 판정은 드물게 하면서 빈 곳은 빨리 메워집니다.
            surfaceRequests.RemoveRange(0, count);
        }

        private static void RefreshIfNeeded(CarDriveWorldSettings settings)
        {
            bool settingsChanged = !Mathf.Approximately(cachedShadowMargin, settings.shadowMargin)
                                   || !Mathf.Approximately(cachedHysteresis, settings.cullingHysteresis)
                                   || !Mathf.Approximately(cachedNearDistance, settings.terrainNearDistance)
                                   || !Mathf.Approximately(cachedRangeScale, settings.rangeScale);

            // 목록이 바뀌었으면(또는 아직 캐시가 없으면) 다시 짭니다.
            int registryVersion = TerrainRegistry.Version;
            bool due = entries == null || registryVersion != cachedRegistryVersion;
            if (!due && !settingsChanged) return;

            // <b>대기 중인 신청을 반드시 함께 비웁니다.</b>
            //
            // 신청 목록은 타일을 <b>색인으로</b> 가리키는데, 아래에서 그 색인이 가리키는 배열을
            // 다시 짭니다. <c>FindObjectsByType</c> 의 순서는 보장되지 않으므로,
            // 남아 있던 신청은 <b>엉뚱한 타일</b>을 켜게 됩니다. 그러면 정작 필요했던 타일은
            // 다음 판정 주기까지 땅에 구멍으로 남습니다.
            //
            // 발현 조건이 우연이 아닙니다. <see cref="ViewRangeScaler"/> 가 지형 값을 바꾼 뒤
            // <see cref="InvalidateCache"/> 를 부르는데, 그 순간이 곧 재구성 시점이고
            // 동시에 지면이 가장 많이 들고 나는 시점입니다.
            surfaceRequests.Clear();
            surfaceSorted = false;

            ViewDistances.Ladder ladder = ViewDistances.Current;

            cachedRegistryVersion = registryVersion;
            cachedShadowMargin = settings.shadowMargin;
            cachedHysteresis = settings.cullingHysteresis;
            cachedNearDistance = settings.terrainNearDistance;
            cachedRangeScale = settings.rangeScale;

            // <b>꺼져 있는 것까지 담습니다.</b>
            // WorldStreamer 가 멀어진 타일을 통째로 껐다가 다시 켜는데,
            // 켜진 것만 담아 두면 다시 켜진 타일이 다음 목록 갱신(2초)까지 목록에 없어
            // 그 사이 화면에 구멍이 남습니다.
            Terrain[] found = TerrainRegistry.All;

            // 타일 수는 좀처럼 바뀌지 않습니다. 길이가 같으면 배열을 다시 만들지 않습니다.
            // (2초마다 103칸짜리 배열을 새로 잡을 이유가 없습니다)
            if (entries == null || entries.Length != found.Length)
            {
                entries = new TerrainEntry[found.Length];
            }

            for (int i = 0; i < found.Length; i++)
            {
                Terrain terrain = found[i];
                if (terrain == null || terrain.terrainData == null)
                {
                    // <b>배열을 재사용하므로 반드시 비워야 합니다.</b> 그냥 건너뛰면
                    // 지난번 갱신 때 담아 둔 다른 타일이 이 자리에 남아, 같은 타일이
                    // 두 번 처리되거나 엉뚱한 경계로 판정됩니다.
                    entries[i] = default;
                    continue;
                }

                Bounds raw = terrain.terrainData.bounds;
                raw.center += terrain.transform.position;

                // 그림자가 넘어오는 만큼 넓혀 둡니다. 이것이 <b>켜는</b> 기준입니다.
                Bounds padded = raw;
                padded.Expand(settings.shadowMargin * 2f);

                // 히스테리시스만큼 더 넓힌 것이 <b>끄는</b> 기준입니다.
                Bounds keep = padded;
                keep.Expand(settings.cullingHysteresis * 2f);

                // 전체 거리 배율을 여기서 곱합니다. 캐시를 짤 때 한 번만 곱하면 되므로
                // 매 프레임 판정에는 아무 비용도 붙지 않습니다.
                // 거리는 ViewDistances 가 한곳에서 계산합니다.
                // 여기서 배율을 곱하지 않습니다 — 그 곱셈이 흩어져 있던 것이
                // 나무가 페이드 전에 접히던 버그의 원인이었습니다.
                float near = ladder.TerrainNear;
                float nearRelease = ladder.TerrainNearRelease;

                entries[i] = new TerrainEntry
                {
                    Terrain = terrain,
                    Owner = terrain.gameObject,
                    RawBounds = raw,
                    PaddedBounds = padded,
                    KeepBounds = keep,
                    NearDistanceSqr = near * near,
                    NearReleaseSqr = nearRelease * nearRelease,

                    // 지금 실제 상태를 기준으로 잡아야, 첫 프레임에 불필요한 대입이 일어나지 않습니다.
                    LastEnabled = terrain.enabled,
                    LastDrawFoliage = terrain.drawTreesAndFoliage
                };
            }
        }

        /// <summary>
        /// 미리 구해 둔 경계와 거리를 <b>다음 프레임에 다시 짜게</b> 합니다.
        ///
        /// 접는 거리는 <c>terrain.treeDistance</c> 를 보고 정해 두는데, 그 값은
        /// <see cref="ViewRangeScaler"/> 가 시야 배율에 따라 바꿉니다.
        /// 캐시는 2초 주기로만 다시 짜므로, 바뀐 직후 잠깐 <b>낡은 접는 거리</b>가 남습니다.
        /// 그 사이에 접는 거리가 나무 페이드보다 앞이면 페이드가 보이지 않습니다.
        ///
        /// 그래서 바꾼 쪽이 여기로 알려 줍니다. 되돌리기(RestoreAll)와 달리
        /// <b>지금 켜고 끈 상태는 건드리지 않습니다.</b> 값만 다시 구합니다.
        /// </summary>
        public static void InvalidateCache()
        {
            cachedRegistryVersion = -1;
        }

        /// <summary>
        /// 껐던 타일을 모두 되돌립니다. 확인이 끝난 뒤에 씁니다.
        /// </summary>
        public static void RestoreAll()
        {
            Terrain[] all = TerrainRegistry.All;

            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] == null) continue;

                all[i].enabled = true;
                all[i].drawTreesAndFoliage = true;
            }

            // 여기서 지형을 직접 건드렸으므로 캐시가 기억하는 상태와 어긋납니다.
            // 비워 두면 다음 호출에서 지금 상태를 다시 읽어 갑니다.
            entries = null;

            // 신청 목록은 지워진 색인을 가리키게 되므로 함께 비웁니다.
            surfaceRequests.Clear();
            surfaceSorted = false;
            nextSurfaceCheck = 0f;
        }

        // --- Private Methods ---

        /// <summary>
        /// 플레이 모드에 들어갈 때 찾아 둔 목록을 비웁니다.
        /// 에디터에서 도메인 리로드를 꺼 두면 지난 실행의 값이 그대로 남기 때문입니다.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            entries = null;
            cachedRegistryVersion = -1;
            cachedShadowMargin = float.NaN;
            cachedHysteresis = float.NaN;
            cachedNearDistance = float.NaN;
            cachedRangeScale = float.NaN;
            nextSurfaceCheck = 0f;
            nextFoliageCheck = 0f;
            lastShown = 0;
            idleRestored = false;
            surfaceRequests.Clear();
            surfaceSorted = false;
        }
    }
}
