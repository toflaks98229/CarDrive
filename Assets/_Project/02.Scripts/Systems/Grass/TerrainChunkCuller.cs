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
    ///
    /// <b>상태는 인스턴스가 들고 있습니다.</b> 예전에는 경계 캐시부터 타이머·신청 목록까지
    /// 전부 <c>static</c> 이라, 값을 보려고 씬에 하나 얹으면 <b>둘이 되어 프레임당 두 번</b>
    /// 돌면서 예산제가 두 배로 헐거워졌습니다. 실제로 겪은 일이고, 아무 로그도 남지 않았습니다.
    /// 지금은 각자 자기 캐시를 굴리며 <c>Awake</c> 가 중복을 알립니다.
    /// 밖으로 열린 것은 <see cref="InvalidateCache"/> 하나뿐입니다.
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

        private readonly Plane[] planes = new Plane[6];

        /// <summary>
        /// 이번 프레임의 나무·풀 켜기 신청 목록입니다.
        /// 매 프레임 비우고 다시 채우므로 할당이 생기지 않습니다.
        /// </summary>
        private readonly List<FoliageRequest> foliageRequests = new List<FoliageRequest>(16);

        /// <summary>
        /// 이번 프레임의 <b>지면</b> 켜기 신청 목록입니다. 나무·풀과 예산을 따로 씁니다.
        /// 지면은 없으면 발밑에 구멍이 보이고, 나무·풀은 없어도 허전할 뿐이라
        /// 한쪽이 다른 쪽의 예산을 잡아먹으면 안 됩니다.
        /// </summary>
        private readonly List<FoliageRequest> surfaceRequests = new List<FoliageRequest>(16);

        /// <summary>가까운 것이 먼저 오도록 하는 비교 기준입니다.</summary>
        private static readonly System.Comparison<FoliageRequest> ByDistance = CompareByDistance;

        /// <summary>타일별로 미리 구해 둔 값들입니다.</summary>
        private TerrainEntry[] entries;

        /// <summary>
        /// 경계 캐시를 만들 때 본 지형 목록의 번호입니다.
        ///
        /// <b>자체 주기를 두지 않습니다.</b> 예전에는 이 컬러가 2초 주기로 목록을 다시 찾고
        /// 경계도 함께 다시 구했습니다. 이제 목록은 <see cref="TerrainRegistry"/> 가 갖고 있는데,
        /// 여기서 주기를 또 두면 <b>두 갱신 시점이 어긋납니다.</b> 그 틈에 캐시가 이미 사라진
        /// 지형을 가리키게 됩니다. 목록이 바뀐 그 순간에 캐시도 다시 짜는 것이 맞습니다.
        /// </summary>
        private int cachedRegistryVersion = -1;

        /// <summary>
        /// 캐시를 만들 때 <b>실제로 쓴</b> 그림자 여유입니다. 달라지면 경계를 다시 구합니다.
        ///
        /// 설정의 <c>shadowMargin</c> 이 아니라 사다리가 낸 <c>ShadowCasterMargin</c> 을 기억합니다.
        /// 여유는 그림자 거리에서도 나오므로, 설정값만 보면 <b>배율이나 그림자 거리가 바뀐 것을
        /// 놓칩니다.</b>
        /// </summary>
        private float cachedShadowMargin = float.NaN;

        /// <summary>캐시를 만들 때 쓴 히스테리시스 간격입니다.</summary>
        private float cachedHysteresis = float.NaN;

        /// <summary>캐시를 만들 때 쓴 "가까운 지면" 거리입니다.</summary>
        private float cachedNearDistance = float.NaN;

        /// <summary>캐시를 만들 때 쓴 전체 거리 배율입니다.</summary>
        private float cachedRangeScale = float.NaN;

        /// <summary>다음 지면 판정 시각입니다. 판정은 주기로, 적용은 매 프레임입니다.</summary>
        private float nextSurfaceCheck;

        /// <summary>다음 나무·풀 판정 시각입니다.</summary>
        private float nextFoliageCheck;

        /// <summary>마지막으로 센 "그리기로 정한 타일 수"입니다. 판정을 건너뛴 프레임에 돌려줍니다.</summary>
        private int lastShown;

        /// <summary>
        /// 할 일이 없는 상태에서 지형을 이미 원래대로 돌려놓았는지입니다.
        /// 돌려놓았으면 그 뒤로는 아무것도 하지 않고 즉시 반환합니다.
        ///
        /// <b>두 경우가 함께 씁니다.</b> 컬링을 통째로 껐을 때(<c>chunkCulling</c>)와
        /// 지면 끄기·화면 밖 접기가 둘 다 꺼졌을 때입니다. 어느 쪽이든 되돌린 결과가 같으므로
        /// 플래그를 나눌 이유가 없습니다.
        /// </summary>
        private bool idleRestored;

        /// <summary>지금 신청 목록이 가까운 순으로 세워져 있는지입니다. 매 프레임 다시 세우지 않기 위한 것입니다.</summary>
        private bool surfaceSorted;

        /// <summary>나무·풀 신청 목록이 가까운 순으로 세워져 있는지입니다.</summary>
        private bool foliageSorted;

        /// <summary>
        /// 지금 돌고 있는 것입니다.
        ///
        /// 둘이 되었는지 알아채는 데 쓰고, <see cref="InvalidateCache"/> 가 밖에서 들어올 때
        /// 넘겨줄 상대이기도 합니다.
        /// </summary>
        private static TerrainChunkCuller active;

        // --- Unity Event Functions ---

        /// <summary>
        /// 둘이 되었으면 알립니다.
        ///
        /// <b>이 클래스에서 특히 중요합니다.</b> 예전에 실제로 둘이 되었을 때
        /// <c>ApplyNow</c> 가 프레임당 두 번 돌아 <b>예산제가 두 배로 헐거워졌고</b>,
        /// 그 사실이 어디에도 드러나지 않아 한참 뒤에야 발견됐습니다.
        /// 상태를 인스턴스로 내려 피해는 줄였지만, 두 번 도는 것 자체는 여전히 낭비입니다.
        /// </summary>
        void Awake()
        {
            if (active != null && active != this)
            {
                GameLog.Error(GameLog.Channel.World,
                    "TerrainChunkCuller 가 둘입니다. 프레임당 판정과 예산이 두 배가 됩니다. " +
                    "WorldRuntimeInstaller 가 하나만 확보하도록 되어 있으니, 씬에 직접 얹은 것이 있는지 보세요.", this);
            }

            active = this;
        }

        /// <summary>매 프레임 어느 타일을 그릴지 정합니다.</summary>
        void LateUpdate()
        {
            Apply(GameContext.MainCamera);
        }

        // --- Private Methods ---

        /// <summary>
        /// 지금 카메라를 기준으로 타일을 켜고 끕니다.
        ///
        /// <b>이 메서드는 순서만 정합니다.</b> 게이트 → 캐시 → 주기 판정 → 훑기 → 예산 소진.
        /// 각 단계가 실제로 무엇을 하는지는 아래 메서드들이 갖고 있습니다.
        ///
        /// 예전에는 이 다섯이 한 메서드 220줄에 이어져 있었습니다. 그 안에서 가장 자주
        /// 회귀가 난 것이 히스테리시스인데(아래 <see cref="StaysOn"/> 의 주석을 보세요),
        /// 그 규칙이 <b>씬과 카메라 없이는 확인할 수 없는 자리</b>에 있었습니다.
        /// 지금은 판정 규칙이 순수 함수로 나와 있어 EditMode 테스트가 고정합니다.
        /// </summary>
        /// <param name="camera">기준이 될 카메라</param>
        /// <returns>그리기로 정한 타일 수</returns>
        private int Apply(Camera camera)
        {
            if (camera == null) return 0;

            CarDriveWorldSettings settings = CarDriveWorldSettings.Instance;

            // 할 일이 없으면 한 번만 되돌리고, 그 뒤로는 아무것도 하지 않습니다.
            if (!HasWorkToDo(settings))
            {
                if (idleRestored) return 0;

                RestoreAll();
                idleRestored = true;
                return 0;
            }
            idleRestored = false;

            RefreshIfNeeded(settings);

            // <b>두 주기는 서로를 모릅니다.</b>
            //
            // 예전에는 한쪽이라도 차례가 되면 훑기가 통째로 돌았고, 그 안에서 나무·풀 판정이
            // <b>자기 차례가 아닌데도</b> 함께 다시 돌았습니다. 기본값(나무·풀 0.1초, 지면 0.25초)
            // 에서 나무·풀은 초당 10회면 되는데 실제로는 13회쯤 돌았고, 그 3회는 타일 103장에
            // 대한 <c>TestPlanesAABB</c> 를 통째로 다시 하는 비용이었습니다.
            //
            // 무엇보다 <c>foliageCheckInterval</c> 이라는 이름이 <b>사실이 아니게</b> 됩니다.
            // 값을 0.1 로 두고 그 주기로 돈다고 믿을 수 없으면 그 손잡이는 없는 것과 같습니다.
            bool foliageDue = TakeFoliageTurn(settings);
            bool surfaceDue = TakeSurfaceTurn(settings);

            // 둘 다 아직이면 프러스텀 계산도 훑기도 건너뜁니다. 예산만 덜어냅니다.
            if (foliageDue || surfaceDue)
            {
                GeometryUtility.CalculateFrustumPlanes(camera, planes);
                Sweep(settings, camera.transform.position, foliageDue, surfaceDue);
            }

            ApplySurfaceBudget(settings.maxSurfaceActivationsPerFrame);
            ApplyFoliageBudget(settings.maxFoliageActivationsPerFrame);

            return lastShown;
        }

        /// <summary>
        /// 이 컬러가 <b>할 일이 있는지</b>입니다.
        ///
        /// 컬링을 통째로 껐거나(<c>chunkCulling</c>), 지면 끄기와 화면 밖 접기가 둘 다 꺼져
        /// 있으면 할 일이 없습니다. 예전에는 이 둘이 <b>똑같은 일을 하는 분기 두 개</b>로
        /// 나뉘어 있었는데, 되돌린 결과가 같으므로 나눌 이유가 없었습니다.
        ///
        /// 할 일이 없어도 <b>한 번은 돌아야 합니다</b> — 예전에 꺼 둔 것을 도로 켜야 하기
        /// 때문입니다. 그 "한 번"은 <see cref="idleRestored"/> 가 셉니다.
        /// </summary>
        /// <param name="settings">확인할 설정</param>
        /// <returns>할 일이 있으면 참</returns>
        private static bool HasWorkToDo(CarDriveWorldSettings settings)
        {
            if (!settings.chunkCulling) return false;

            return settings.cullTerrainSurface || settings.foldOffscreenFoliage;
        }

        /// <summary>
        /// 나무·풀 판정의 차례가 되었는지 보고, 되었으면 <b>다음 차례를 예약합니다.</b>
        ///
        /// <b>왜 주기로 하는가.</b> 이 판정에 거리가 섞여 있던 시절에는 매 프레임 해야 했습니다.
        /// 플레이어가 움직이면 결과가 바뀌었기 때문입니다. 지금은 <b>화면 안인가</b> 하나뿐이고,
        /// 그 판정에는 그림자 여유가 붙어 있어 실제 화면 가장자리보다 훨씬 바깥에서 켜집니다.
        /// 몇 십 밀리초 늦어도 보이지 않는 반면, <c>TestPlanesAABB</c> 를 103장에 매 프레임
        /// 돌리는 비용은 그대로 듭니다.
        /// </summary>
        /// <param name="settings">주기를 읽을 설정. 0 이면 매 프레임입니다.</param>
        /// <returns>이번 프레임에 판정해야 하면 참</returns>
        private bool TakeFoliageTurn(CarDriveWorldSettings settings)
        {
            if (settings.foliageCheckInterval <= 0f) return true;
            if (Time.unscaledTime < nextFoliageCheck) return false;

            nextFoliageCheck = Time.unscaledTime + settings.foliageCheckInterval;
            return true;
        }

        /// <summary>
        /// 지면 판정의 차례가 되었는지 보고, 되었으면 <b>다음 차례를 예약합니다.</b>
        ///
        /// 지면이 화면에 들고 나는 일은 프레임 단위로 일어나지 않습니다. 0.25초 사이에
        /// 사람이 돌 수 있는 각도나 차가 가는 거리로는 판정이 뒤집힐 일이 거의 없습니다.
        /// 그런데도 매 프레임 판정하면 카메라의 미세한 떨림 하나하나가 토글 후보가 되고,
        /// 그 토글이 이 시스템에서 가장 비싼 일입니다.
        ///
        /// 그래서 <b>판정은 주기로, 적용은 매 프레임 조금씩</b> 합니다.
        /// </summary>
        /// <param name="settings">주기를 읽을 설정</param>
        /// <returns>이번 프레임에 판정해야 하면 참</returns>
        private bool TakeSurfaceTurn(CarDriveWorldSettings settings)
        {
            if (Time.unscaledTime < nextSurfaceCheck) return false;

            nextSurfaceCheck = Time.unscaledTime + Mathf.Max(0.05f, settings.surfaceCheckInterval);
            return true;
        }

        /// <summary>
        /// 타일을 <b>한 번만</b> 훑으며 차례가 된 판정을 합니다.
        ///
        /// <b>왜 한 바퀴인가.</b> 지면과 나무·풀을 따로 훑으면 읽기는 쉬워지지만 103장을
        /// 두 번 돌게 됩니다. 판정 자체보다 배열을 훑는 것이 아까운 규모는 아니지만,
        /// 이 컬러가 존재하는 이유가 <b>타일 수에 비례하는 비용을 줄이는 것</b>이라
        /// 그 비용을 두 배로 만드는 것은 앞뒤가 맞지 않습니다.
        /// </summary>
        /// <param name="settings">접기 여부와 지면 컬링 여부를 읽을 설정</param>
        /// <param name="eye">거리를 잴 기준 위치</param>
        /// <param name="foliageDue">나무·풀을 판정할 차례인지</param>
        /// <param name="surfaceDue">지면을 판정할 차례인지</param>
        private void Sweep(CarDriveWorldSettings settings, Vector3 eye, bool foliageDue, bool surfaceDue)
        {
            bool cullSurface = settings.cullTerrainSurface;
            bool surfacePass = cullSurface && surfaceDue;

            // 신청 목록은 <b>자기 차례에만</b> 새로 짭니다.
            // 남의 차례에 비우면 아직 못 켠 신청이 사라져, 그 타일은 다음 차례까지 밀립니다.
            //
            // 지면은 컬링이 꺼져 있어도 차례가 되면 비웁니다. 끄는 순간 남아 있던 신청은
            // 이미 뜻이 없고(아래 되돌리기가 어차피 전부 켭니다), 들고 있으면 색인만 낡습니다.
            if (surfaceDue)
            {
                surfaceRequests.Clear();
                surfaceSorted = false;
            }

            if (foliageDue)
            {
                foliageRequests.Clear();
                foliageSorted = false;
            }

            int shown = 0;

            for (int i = 0; i < entries.Length; i++)
            {
                Terrain terrain = entries[i].Terrain;
                if (terrain == null) continue;

                // WorldStreamer 가 꺼 둔 타일은 건드리지 않습니다. 그쪽이 주인입니다.
                if (!entries[i].Owner.activeInHierarchy) continue;

                if (surfacePass) SweepSurface(i, terrain, eye);
                else if (!cullSurface && !entries[i].LastEnabled)
                {
                    // 지면 컬링을 끈 상태에서는 예전에 꺼 둔 타일을 도로 켜 줘야 합니다.
                    // 그러지 않으면 설정을 바꾼 순간부터 그 타일이 영영 보이지 않습니다.
                    entries[i].LastEnabled = true;
                    terrain.enabled = true;
                }

                if (foliageDue && SweepFoliage(i, terrain, settings.foldOffscreenFoliage, eye)) shown++;
            }

            // 세어 둔 수는 <b>나무·풀을 판정한 프레임의 것</b>입니다.
            // 지면만 돈 프레임에는 세지 않았으므로 지난 값을 그대로 둡니다.
            if (foliageDue) lastShown = shown;
        }

        /// <summary>
        /// 타일 하나의 <b>지면</b>을 판정합니다. 끄는 것은 즉시, 켜는 것은 신청만 합니다.
        ///
        /// 켜는 것을 미루는 이유가 있습니다. 이 옵션이 예전에 손해였던 까닭이 정확히
        /// 여기인데, 시야를 홱 돌리면 여러 장이 한 프레임에 켜져 그 프레임이 통째로 늘어졌습니다.
        /// </summary>
        /// <param name="i">타일의 색인</param>
        /// <param name="terrain">대상 지형</param>
        /// <param name="eye">거리를 잴 기준 위치</param>
        private void SweepSurface(int i, Terrain terrain, Vector3 eye)
        {
            // 지면은 <b>자기 상태</b>를 기준으로 판정합니다. 나무·풀과 상태가 다를 수 있으므로
            // 그쪽 결과를 빌려 쓸 수 없습니다. (아래 StaysOn 의 주석을 보세요)
            bool visible = StaysInView(entries[i].LastEnabled, i);

            float sqrToEye = entries[i].RawBounds.SqrDistance(eye);

            bool surfaceOn = StaysOn(entries[i].LastEnabled, visible, sqrToEye,
                                    entries[i].NearDistanceSqr, entries[i].NearReleaseSqr);

            if (entries[i].LastEnabled == surfaceOn) return;

            if (!surfaceOn)
            {
                entries[i].LastEnabled = false;
                terrain.enabled = false;
                WorldProfiler.Count(WorldProfiler.Counter.SurfaceToggled);
                return;
            }

            surfaceRequests.Add(new FoliageRequest { Index = i, SqrDistance = sqrToEye });
        }

        /// <summary>
        /// 타일 하나의 <b>나무·풀</b>을 판정합니다. 끄는 것은 즉시, 켜는 것은 신청만 합니다.
        ///
        /// <b>거리로는 접지 않습니다. 화면 밖일 때만 접습니다.</b>
        /// 나무는 <c>Terrain.treeDistance</c>, 풀은 <c>Terrain.detailObjectDistance</c> 가
        /// 이미 <b>반경 거리</b>로 잘라 냅니다. 유니티가 하는 그 컬링은 부드러운데
        /// 타일 단위 접기는 <b>하드 스위치</b>라, 같은 일을 두 번 하면서 결과는 더 나빴고
        /// 토글 비용까지 얹혔습니다.
        ///
        /// 화면 밖 판정만은 유니티가 대신 해 주지 않습니다 — 터레인의 나무·디테일 컬링
        /// 패스는 터레인마다 도는데, 화면에 없는 타일에서도 그 목록을 훑습니다.
        /// </summary>
        /// <param name="i">타일의 색인</param>
        /// <param name="terrain">대상 지형</param>
        /// <param name="foldEnabled">화면 밖 접기를 켜 두었는지</param>
        /// <param name="eye">정렬용 거리를 잴 기준 위치</param>
        /// <returns>이 타일이 화면 안(여유 포함)이면 참. 세는 데 씁니다.</returns>
        private bool SweepFoliage(int i, Terrain terrain, bool foldEnabled, Vector3 eye)
        {
            bool visible = StaysInView(entries[i].LastDrawFoliage, i);

            bool drawFoliage = !foldEnabled || visible;
            if (entries[i].LastDrawFoliage == drawFoliage) return visible;

            // 끄는 일은 싸지만 켜는 일은 그 타일의 렌더 데이터를 다시 짜는 것이라,
            // 시야를 빠르게 돌려 여러 장이 한꺼번에 몰리면 그 프레임이 통째로 늘어집니다.
            if (!drawFoliage)
            {
                entries[i].LastDrawFoliage = false;
                terrain.drawTreesAndFoliage = false;
                WorldProfiler.Count(WorldProfiler.Counter.FoliageToggled);
                return visible;
            }

            // 정렬용 거리는 <b>여기서</b> 구합니다. 신청은 화면 경계를 막 넘은 타일만 하므로
            // 대개 몇 장뿐이고, 103장 전부에 대해 미리 구할 이유가 없습니다.
            foliageRequests.Add(new FoliageRequest
            {
                Index = i,
                SqrDistance = entries[i].RawBounds.SqrDistance(eye)
            });

            return visible;
        }

        /// <summary>
        /// 화면 안인지 <b>지금 상태에 맞는 경계</b>로 판정합니다.
        ///
        /// <b>켤 때와 끌 때의 기준이 다릅니다.</b> 같으면 경계에 걸친 타일이 시야가 미세하게
        /// 흔들릴 때마다 껐다 켜기를 반복하고, 그 전환이 곧 비용입니다. 이미 켜져 있는 것은
        /// 더 넓은 경계(<see cref="TerrainEntry.KeepBounds"/>)를 벗어나야 꺼집니다.
        ///
        /// <b>두 경계를 다 재지 않습니다.</b> 지금 상태가 어느 쪽을 볼지 이미 정하므로
        /// <c>TestPlanesAABB</c> 는 한 번만 돕니다. 103장에 매 프레임 도는 자리입니다.
        /// </summary>
        /// <param name="currentlyOn">이 항목이 지금 켜져 있는지</param>
        /// <param name="i">타일의 색인</param>
        /// <returns>지금 상태 기준으로 화면 안이면 참</returns>
        private bool StaysInView(bool currentlyOn, int i)
        {
            return currentlyOn
                ? GeometryUtility.TestPlanesAABB(planes, entries[i].KeepBounds)
                : GeometryUtility.TestPlanesAABB(planes, entries[i].PaddedBounds);
        }

        /// <summary>
        /// 지면을 켜 둘지 정합니다. <b>순수 함수입니다 — 씬 없이 확인할 수 있습니다.</b>
        ///
        /// <b>가까운 지면은 판정에서 뺍니다.</b> 발밑 타일은 제자리에서 한 바퀴만 돌아도
        /// 화면을 들락날락합니다. 그때마다 렌더 데이터를 다시 짜면 아끼는 것보다 비쌉니다.
        /// 그래서 가까운 것은 화면 밖이어도 늘 켜 두고, <b>먼 것만</b> 화면으로 거릅니다.
        /// 들어오는 거리와 풀려나는 거리를 달리 두어 경계에서도 떨리지 않게 합니다.
        ///
        /// <b>이 규칙이 한 번 통째로 사라진 적이 있습니다.</b> 예전에는 지면과 나무·풀이
        /// <c>visible</c> 하나를 나눠 썼고 그 히스테리시스가 <c>LastEnabled</c> 에 묶여
        /// 있었는데, 지면 컬링을 기본에서 끄자 <c>LastEnabled</c> 가 <b>모든 타일에서 항상 참</b>이
        /// 되어 나무·풀 판정은 늘 넓은 경계 하나만 보게 되었습니다. 계측을 붙이고 나서야
        /// 드러났습니다 — 화면 밖 접기가 초당 112회까지 올라갔습니다.
        /// 눈으로 확인하려면 게임을 띄우고 시야를 돌려 봐야 하는데, 그 확인은 반복되지 않습니다.
        /// </summary>
        /// <param name="wasEnabled">지금 켜져 있는지</param>
        /// <param name="visible">지금 상태 기준으로 화면 안인지</param>
        /// <param name="sqrToEye">기준 위치까지의 거리 제곱</param>
        /// <param name="nearSqr">가까움으로 <b>들어오는</b> 거리의 제곱</param>
        /// <param name="nearReleaseSqr">가까움에서 <b>풀려나는</b> 거리의 제곱. 들어오는 쪽보다 멉니다.</param>
        /// <returns>지면을 켜 두어야 하면 참</returns>
        public static bool StaysOn(bool wasEnabled, bool visible, float sqrToEye,
                                   float nearSqr, float nearReleaseSqr)
        {
            bool isNear = wasEnabled ? sqrToEye < nearReleaseSqr : sqrToEye < nearSqr;

            return isNear || visible;
        }

        /// <summary>
        /// 타일 하나의 <b>켜는 경계와 끄는 경계</b>를 구합니다.
        /// <b>순수 함수입니다 — 씬 없이 확인할 수 있습니다.</b>
        ///
        /// <b>끄는 경계는 반드시 켜는 경계를 품어야 합니다.</b> 그 포함 관계가 곧
        /// 히스테리시스이고, 뒤집히거나 같아지면 경계에 걸친 타일이 시야가 미세하게
        /// 흔들릴 때마다 껐다 켜기를 반복합니다. <see cref="StaysOn"/> 이 아무리 옳아도
        /// 여기서 두 경계가 같으면 떨림은 그대로 남습니다.
        ///
        /// <b>여유는 사다리가 정합니다.</b> 예전에는 설정의 <c>shadowMargin</c>(55m) 하나를
        /// 썼는데, 그 값은 <b>실제 그림자 거리를 몰랐습니다.</b> URP 에셋의 50m 와 우연히
        /// 맞아떨어져 있었을 뿐이라, 품질을 High Fidelity(150m)로 올리면 여유가 모자라
        /// 화면 가장자리에서 그림자가 통째로 사라졌을 것입니다.
        /// </summary>
        /// <param name="raw">여유를 더하지 않은 월드 경계</param>
        /// <param name="shadowCasterMargin">화면 밖 캐스터를 담을 여유(m). 사다리가 냅니다.</param>
        /// <param name="hysteresis">켜는 기준과 끄는 기준 사이의 간격(m)</param>
        /// <param name="padded">그림자 여유까지 더한 <b>켜는</b> 경계</param>
        /// <param name="keep">히스테리시스만큼 더 넓힌 <b>끄는</b> 경계</param>
        public static void BuildBounds(Bounds raw, float shadowCasterMargin, float hysteresis,
                                       out Bounds padded, out Bounds keep)
        {
            // 그림자가 넘어오는 만큼 넓혀 둡니다. 이것이 <b>켜는</b> 기준입니다.
            padded = raw;
            padded.Expand(shadowCasterMargin * 2f);

            // 히스테리시스만큼 더 넓힌 것이 <b>끄는</b> 기준입니다.
            keep = padded;
            keep.Expand(Mathf.Max(0f, hysteresis) * 2f);
        }

        // --- Private Methods ---

        /// <summary>
        /// 신청된 타일 중 예산만큼만 나무·풀을 켭니다. <b>가까운 것이 먼저입니다.</b>
        ///
        /// <b>처리한 것은 목록에서 덜어냅니다.</b> 예전에는 덜어내지 않고 "나머지는 다음
        /// 프레임에 다시 신청된다"고 적어 두었는데, <b>그 말이 사실이 아니었습니다.</b>
        /// 신청 목록은 <c>foliageCheckInterval</c>(0.1초)마다만 다시 짜이므로, 그 사이
        /// 프레임들은 <b>이미 켜진 앞쪽 두 장을 다시 켜기만</b> 하고 뒤쪽은 손대지 않았습니다.
        /// 예산이 "프레임당 2장"이 아니라 <b>"0.1초당 2장"</b>으로 동작한 셈이라
        /// 60fps 기준 여섯 배 느렸습니다.
        ///
        /// 그것이 눈에 보이는 방식이 이렇습니다 — 시야를 돌리면 타일 여럿이 한꺼번에
        /// 화면 경계를 넘는데, 초당 20장씩만 펴지니 <b>나무와 풀이 타일 단위로 하나씩
        /// 툭툭 나타납니다.</b> 거리 디더는 이 순간을 가려 주지 못합니다. 그 타일의 나무는
        /// 대개 페이드가 이미 끝난 가까운 거리에 있어서 <b>처음부터 완전히 불투명</b>하기 때문입니다.
        ///
        /// 가까운 것을 먼저 켜는 이유가 있습니다. 목록 순서대로 켜면 그 순서는 타일을 구울 때
        /// 정해진 것이라 거리와 아무 상관이 없고, <b>늘 같은 타일이 뒤로 밀립니다.</b>
        /// (<see cref="Gameplay.WorldStreamer"/> 가 같은 문제를 겪고 같은 방식으로 고쳤습니다)
        /// </summary>
        /// <param name="budget">이번 프레임에 켤 수 있는 최대 수</param>
        private void ApplyFoliageBudget(int budget)
        {
            if (foliageRequests.Count == 0) return;

            // 새로 짜인 목록이면 가까운 것부터 세웁니다. 덜어내도 순서는 그대로라
            // 이미 세워 둔 것은 다시 세우지 않습니다.
            if (!foliageSorted)
            {
                foliageRequests.Sort(ByDistance);
                foliageSorted = true;
            }

            int count = Mathf.Min(budget, foliageRequests.Count);
            for (int i = 0; i < count; i++)
            {
                int index = foliageRequests[i].Index;
                if (index < 0 || index >= entries.Length) continue;

                Terrain terrain = entries[index].Terrain;
                if (terrain == null) continue;

                entries[index].LastDrawFoliage = true;
                terrain.drawTreesAndFoliage = true;
                WorldProfiler.Count(WorldProfiler.Counter.FoliageToggled);
            }

            // <b>처리한 것은 덜어냅니다.</b> 그래야 판정을 0.1초마다 하면서도
            // 펴는 일은 매 프레임 이어집니다. (지면 예산이 이미 이렇게 하고 있었습니다)
            foliageRequests.RemoveRange(0, count);
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
        private void ApplySurfaceBudget(int budget)
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

        /// <summary>
        /// 타일 목록과 그에 딸린 계산값을 다시 만듭니다.
        ///
        /// 목록이 바뀌었거나, 설정이 바뀌어 경계를 다시 구해야 할 때만 실제로 일합니다.
        /// (이 문서 주석은 한동안 <see cref="ApplyFoliageBudget"/> 위에 붙어 있었습니다.
        /// 리팩토링 때 메서드만 옮겨 가고 주석이 남은 것입니다)
        /// </summary>
        /// <param name="settings">그림자 여유와 히스테리시스를 읽을 설정</param>
        private void RefreshIfNeeded(CarDriveWorldSettings settings)
        {
            // 사다리를 먼저 구합니다. 여유가 그림자 거리에서 나오므로, 바뀌었는지 판단하려면
            // 설정값이 아니라 <b>사다리가 낸 값</b>을 봐야 합니다.
            ViewDistances.Ladder ladder = ViewDistances.Current;

            bool settingsChanged = !Mathf.Approximately(cachedShadowMargin, ladder.ShadowCasterMargin)
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
            foliageRequests.Clear();
            foliageSorted = false;

            cachedRegistryVersion = registryVersion;
            cachedShadowMargin = ladder.ShadowCasterMargin;
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

                Bounds padded, keep;
                BuildBounds(raw, ladder.ShadowCasterMargin, settings.cullingHysteresis, out padded, out keep);

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
        ///
        /// <b>이것만 정적으로 남습니다.</b> 상태는 인스턴스가 들고 있지만, 알려 주는 쪽
        /// (<see cref="ViewRangeScaler"/>)이 이 컴포넌트의 참조를 갖고 있지 않기 때문입니다.
        /// 지금 돌고 있는 것에 넘겨줍니다. 아직 <c>Awake</c> 전이면 할 일이 없습니다 —
        /// 캐시가 아예 없으므로 어차피 다음 프레임에 새로 짜입니다.
        /// </summary>
        public static void InvalidateCache()
        {
            if (active != null) active.cachedRegistryVersion = -1;
        }

        /// <summary>
        /// 껐던 타일을 모두 되돌립니다. 컬링을 꺼 둔 동안 한 번만 부릅니다.
        /// </summary>
        private void RestoreAll()
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
            foliageRequests.Clear();
            foliageSorted = false;
            nextSurfaceCheck = 0f;
        }

        /// <summary>
        /// 플레이 모드에 들어갈 때 남은 정적 상태를 비웁니다.
        ///
        /// <b>이제 비울 것이 하나뿐입니다.</b> 경계 캐시·타이머·신청 목록이 전부 인스턴스로
        /// 내려가서, 새 인스턴스가 만들어지는 것만으로 깨끗하게 시작합니다.
        /// 도메인 리로드를 꺼 둔 채 다시 플레이할 때 <b>지난 실행의 지형을 가리키던 캐시</b>가
        /// 남는 문제가 구조적으로 사라졌습니다.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            active = null;
        }
    }
}
