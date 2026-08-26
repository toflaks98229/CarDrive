using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using CarDrive.Common;

namespace CarDrive.Systems
{
    /// <summary>
    /// 풀을 <b>GPU 구동 간접 드로우</b>로 그립니다. 터레인 디테일을 대신합니다.
    ///
    /// <b>무엇이 달라지는가.</b> 터레인 디테일은 CPU가 패치(25m 격자)마다 그리기 명령을
    /// 만들고 한 명령에 약 500 포기까지만 담습니다. 즉 그리기 횟수가 포기 수에 정비례합니다.
    /// 이 게임은 CPU 바운드라 그 비례가 그대로 프레임 예산을 먹습니다.
    ///
    /// 여기서는 포기 전부를 GPU 버퍼에 한 번 올려 두고, 컴퓨트 셰이더가 화면 안의 것만
    /// 추려 냅니다. CPU는 <b>종당 그리기 명령 하나</b>만 냅니다.
    ///
    /// <b>밟힘과 바람은 손대지 않았습니다.</b> 풀 셰이더의 밟힘·바람·색은 전부
    /// <c>unity_ObjectToWorld</c> 와 셰이더 전역(<c>_GrassPushers</c>,
    /// <c>_GrassTrampleMap</c>)만 보고 있습니다. 여기서 행렬을 만들어 주므로
    /// <see cref="GrassPushField"/> 와 <see cref="GrassTrampleMap"/> 은 그대로 동작합니다.
    ///
    /// <b>넘겨받기는 자동입니다.</b> 예전에는 "켤 때 터레인 디테일을 함께 끄세요"라고
    /// 적어 두고 사람에게 맡겼는데, 그러면 잊었을 때 풀이 두 겹으로 그려지고
    /// <b>느려진 이유가 어디에도 드러나지 않습니다.</b>
    ///
    /// 지금은 <see cref="IsDrawing"/> 이 신호를 냅니다. 이쪽이 <b>실제로 한 프레임을 그린 뒤에야</b>
    /// <see cref="TerrainDetailLod"/> 가 터레인 디테일의 거리를 0 으로 내립니다.
    /// 설정이 아니라 결과를 보는 것이라, 씬에 렌더러가 없거나 기기가 컴퓨트를 못 쓰면
    /// 저절로 예전 경로가 계속 그립니다.
    ///
    /// <b>훑기는 나눠서 합니다.</b> 심어진 자리를 만들려면 지형 103장의 디테일맵을
    /// 전부 읽어야 하는데, 한 프레임에 하면 로딩이 초 단위로 멈춰 섭니다.
    /// 한 장씩 훑고, 다 끝나야 버퍼를 올립니다. 그동안은 터레인 디테일이 계속 그립니다.
    ///
    /// <b>주의 — 간접 드로우 경로는 눈으로 확인해야 합니다.</b>
    /// 절두체 컬링과 절차적 인스턴싱은 컴파일이 되어도 화면이 비는 경우가 흔합니다.
    /// 켠 뒤 풀이 보이지 않으면 <see cref="CarDriveWorldSettings.gpuGrass"/> 를 끄세요.
    /// 즉시 물러나고 터레인 디테일이 거리를 되찾습니다.
    /// </summary>
    [DefaultExecutionOrder(-98)]
    public class GpuGrassRenderer : MonoBehaviour
    {
        // --- Constants ---

        /// <summary>컴퓨트 셰이더의 스레드 그룹 크기입니다. .compute 의 numthreads 와 같아야 합니다.</summary>
        private const int ThreadGroupSize = 64;

        /// <summary>인스턴스 하나의 바이트 수입니다. float4 하나 — xyz 자리, w 회전입니다.</summary>
        private const int InstanceStride = 16;

        /// <summary>컴퓨트 셰이더 에셋의 Resources 이름입니다.</summary>
        private const string CullShaderName = "GrassCull";

        /// <summary>
        /// 한 프레임에 훑을 지형 수입니다.
        ///
        /// <b>한꺼번에 훑으면 로딩이 한 프레임에 멈춰 섭니다.</b> 지형 한 장은 디테일 격자가
        /// 256×256 이고 종이 셋이라 20만 칸이며, 심긴 칸마다 <c>SampleHeight</c> 를 부릅니다.
        /// 103 장을 한 번에 하면 그 프레임 하나가 초 단위로 늘어집니다.
        ///
        /// 나눠 훑으면 총 시간은 같지만 <b>어느 프레임도 멈추지 않습니다.</b> 그동안은
        /// 터레인 디테일이 계속 풀을 그리므로 화면이 비지도 않습니다.
        /// (<see cref="IsDrawing"/> 가 참이 되기 전까지 <see cref="TerrainDetailLod"/> 가 넘겨주지 않습니다)
        /// </summary>
        private const int ScanBudgetPerFrame = 1;

        // --- Private Types ---

        /// <summary>종 하나가 쓰는 버퍼 묶음입니다.</summary>
        private class SpeciesBatch
        {
            /// <summary>이 종의 잎 메시입니다.</summary>
            public Mesh Mesh;

            /// <summary>이 종의 재질입니다. 터레인 디테일이 쓰던 것과 같습니다.</summary>
            public Material Material;

            /// <summary>심어진 포기 전부입니다. 한 번 채우고 바뀌지 않습니다.</summary>
            public GraphicsBuffer Instances;

            /// <summary>이번 프레임에 그릴 색인입니다. 매 프레임 컴퓨트가 채웁니다.</summary>
            public GraphicsBuffer Visible;

            /// <summary>
            /// 간접 드로우 인자입니다. 보이는 수를 여기에 복사해 넣습니다.
            ///
            /// <b>다섯 칸 중 넷은 만들 때 한 번 채우면 끝입니다.</b> 색인 수·시작 색인·기준
            /// 정점·시작 인스턴스는 메시가 바뀌지 않는 한 그대로이고, 매 프레임 달라지는 것은
            /// <c>instanceCount</c> 하나뿐인데 그 칸은 <see cref="GraphicsBuffer.CopyCount"/> 가
            /// GPU 안에서 직접 덮어씁니다.
            /// </summary>
            public GraphicsBuffer Args;

            /// <summary>실제 포기 수입니다.</summary>
            public int Count;

            /// <summary>
            /// 그릴 때 넘길 값 한 벌입니다. <b>만들 때 한 번 채웁니다.</b>
            ///
            /// 경계·재질·그림자 설정이 전부 상수라 매 프레임 다시 만들 이유가 없습니다.
            /// </summary>
            public RenderParams Params;

            /// <summary>
            /// 재질 인스턴스에 값을 넘길 때 씁니다. <b>내용도 만들 때 한 번만 채웁니다.</b>
            ///
            /// 물리는 버퍼 둘과 크기 범위 모두 만든 뒤로 바뀌지 않는데, 예전에는 종마다
            /// 매 프레임 다시 물렸습니다. <c>SetBuffer</c> 는 관리 영역에서 네이티브로 넘어가는
            /// 호출이라 공짜가 아니고, 이 경로가 없애려는 것이 바로 그런 <b>포기 수와 무관한
            /// 프레임당 CPU 비용</b>입니다.
            /// </summary>
            public MaterialPropertyBlock Properties;
        }

        /// <summary>
        /// 종 하나에서 훑어 낸 것입니다. <b>그릴 수 없는 종도 여기 담깁니다.</b>
        ///
        /// <see cref="Positions"/> 가 null 이면 그릴 수 없다는 뜻이고, 그때는 그 종의
        /// 디테일맵을 <b>아예 읽지 않습니다.</b> 예전에는 못 그리는 종도 103장 내내 훑어
        /// 자리를 백만 개 담아 두고, 다 끝난 뒤 <see cref="FinishScan"/> 이 통째로 버렸습니다.
        /// 그 사실은 어느 로그에도 남지 않았습니다.
        /// </summary>
        private class ScannedLayer
        {
            /// <summary>잎 메시입니다.</summary>
            public Mesh Mesh;

            /// <summary>잎 재질입니다.</summary>
            public Material Material;

            /// <summary>모아 둔 포기 자리들입니다. null 이면 그릴 수 없는 종입니다.</summary>
            public List<Vector4> Positions;
        }

        // --- Public Properties ---

        /// <summary>
        /// GPU 풀이 <b>실제로 그리고 있는지</b>입니다.
        ///
        /// <b>이 신호가 필요한 이유.</b> 켜는 것은 설정의 <c>gpuGrass</c> 하나지만, 실제로
        /// 그려지기까지는 넘어야 할 것이 여럿입니다 — 씬에 이 컴포넌트가 있어야 하고,
        /// 기기가 컴퓨트 셰이더를 지원해야 하고, 지형을 다 훑어 버퍼를 올려야 합니다.
        /// 하나라도 걸리면 이쪽은 아무것도 그리지 않습니다.
        ///
        /// <see cref="TerrainDetailLod"/> 는 이 값이 참이 된 <b>뒤에야</b> 터레인 디테일을 끕니다.
        /// 설정만 보고 미리 꺼 버리면, 못 켜진 경우에 풀이 통째로 없어집니다.
        /// </summary>
        public static bool IsDrawing { get; private set; }

        // --- Private Member Variables ---

        // 컴퓨트 셰이더(솎아내기)에 넘길 프로퍼티들입니다.

        /// <summary>전체 인스턴스 버퍼의 컴퓨트 프로퍼티 ID입니다.</summary>
        private static readonly int InstancesId = Shader.PropertyToID("_Instances");

        /// <summary>솎아내기를 통과한 인스턴스 색인 버퍼의 컴퓨트 프로퍼티 ID입니다.</summary>
        private static readonly int VisibleId = Shader.PropertyToID("_VisibleIndices");

        /// <summary>절두체 평면 여섯 장의 컴퓨트 프로퍼티 ID입니다.</summary>
        private static readonly int PlanesId = Shader.PropertyToID("_FrustumPlanes");

        /// <summary>카메라 위치의 컴퓨트 프로퍼티 ID입니다. 거리 솎아내기에 씁니다.</summary>
        private static readonly int CameraId = Shader.PropertyToID("_CameraPosition");

        /// <summary>그리는 최대 거리의 제곱값에 대한 컴퓨트 프로퍼티 ID입니다. 제곱근을 피하려고 제곱으로 넘깁니다.</summary>
        private static readonly int MaxDistanceId = Shader.PropertyToID("_MaxDistanceSqr");

        /// <summary>인스턴스 하나의 경계 반경에 대한 컴퓨트 프로퍼티 ID입니다.</summary>
        private static readonly int RadiusId = Shader.PropertyToID("_InstanceRadius");

        /// <summary>검사할 인스턴스 개수의 컴퓨트 프로퍼티 ID입니다.</summary>
        private static readonly int CountId = Shader.PropertyToID("_InstanceCount");

        // 그리기 재질에 넘길 프로퍼티들입니다.

        /// <summary>그리기 재질이 읽을 인스턴스 버퍼의 프로퍼티 ID입니다.</summary>
        private static readonly int GrassInstancesId = Shader.PropertyToID("_GrassInstances");

        /// <summary>그리기 재질이 읽을 가시 인스턴스 색인 버퍼의 프로퍼티 ID입니다.</summary>
        private static readonly int GrassVisibleId = Shader.PropertyToID("_GrassVisibleIndices");

        /// <summary>풀 크기 범위의 프로퍼티 ID입니다.</summary>
        private static readonly int ScaleRangeId = Shader.PropertyToID("_GrassScaleRange");

        /// <summary>
        /// 포기마다 곱할 크기의 아래·위 값입니다. 셰이더가 자리 해시로 이 사이를 고릅니다.
        ///
        /// 예전에는 렌더 루프 안에 숫자로 박혀 있었습니다. 매 프레임 같은 값을 다시 만들어
        /// 다시 대입하고 있었고, 무엇보다 <b>어디서 만져야 하는지 알 수 없는 값</b>이었습니다.
        /// </summary>
        private static readonly Vector4 ScaleRange = new Vector4(0.85f, 1.25f, 0f, 0f);

        /// <summary>절두체 평면을 담아 두는 곳입니다. 매 프레임 새로 잡지 않습니다.</summary>
        private static readonly Plane[] planes = new Plane[6];

        /// <summary>컴퓨트에 넘길 평면입니다. Plane 을 그대로 넘길 수 없어 옮겨 담습니다.</summary>
        private static readonly Vector4[] planeVectors = new Vector4[6];

        /// <summary>종별 묶음입니다.</summary>
        private readonly List<SpeciesBatch> batches = new List<SpeciesBatch>();

        /// <summary>골라내는 컴퓨트 셰이더입니다.</summary>
        private ComputeShader cullShader;

        /// <summary>커널 색인입니다.</summary>
        private int kernel;

        /// <summary>준비가 끝났는지입니다.</summary>
        private bool ready;

        /// <summary>
        /// 이 기기에서는 <b>안 되는 것으로 판명</b>됐는지입니다.
        ///
        /// 컴퓨트 셰이더를 못 쓰거나 <c>.compute</c> 를 못 찾은 경우입니다. 다시 시도해도
        /// 답이 같으므로 매 프레임 로그를 뱉지 않도록 접어 둡니다.
        ///
        /// <b>지형이 아직 없는 것은 여기 들어가지 않습니다.</b> 예전에는 그 경우에도 접어서,
        /// 스트리머가 지형을 늦게 올리면 GPU 풀이 <b>영영 켜지지 않았습니다.</b>
        /// </summary>
        private bool unsupported;

        /// <summary>훑는 중인 지형들입니다. 시작할 때 한 번 복사해 둡니다.</summary>
        private Terrain[] scanTargets;

        /// <summary>다음에 훑을 지형의 색인입니다.</summary>
        private int scanIndex;

        /// <summary>훑는 동안 종별로 모아 두는 것들입니다. 종 색인이 열쇠입니다.</summary>
        private Dictionary<int, ScannedLayer> scanned;

        /// <summary>
        /// 간접 드로우 인자를 GPU에 올릴 때 거쳐 가는 배열입니다.
        ///
        /// <c>SetData</c> 가 배열을 받으므로 한 칸짜리라도 있어야 합니다. <b>이제 이것을 쓰는
        /// 곳은 <see cref="CreateBatch"/> 뿐입니다</b> — 인자는 만들 때 한 번 채우면 그만이고,
        /// 매 프레임 달라지는 <c>instanceCount</c> 는 <see cref="GraphicsBuffer.CopyCount"/> 가
        /// GPU 안에서 직접 덮어씁니다.
        ///
        /// (예전에는 <c>RenderAll</c> 안에서 종마다 배열을 새로 만들었다가, 돌려쓰게 바꿨다가,
        /// 이제는 프레임당 쓰지 않게 되었습니다. 세 번째가 맞는 답입니다)
        /// </summary>
        private readonly GraphicsBuffer.IndirectDrawIndexedArgs[] argsScratch =
            new GraphicsBuffer.IndirectDrawIndexedArgs[1];

        /// <summary>지금 돌고 있는 것입니다. 둘이 되었는지 알아채려고만 둡니다.</summary>
        private static GpuGrassRenderer active;

        // --- Unity Event Functions ---

        /// <summary>
        /// 둘이 되었으면 알립니다.
        ///
        /// <b>여기는 상태가 이미 인스턴스별인데도 위험합니다.</b> 버퍼와 묶음은 각자 갖지만
        /// <see cref="IsDrawing"/> 은 <c>static</c> 이라 공유합니다. 그래서 둘이면
        /// 풀이 <b>두 겹으로 그려지고</b>(각자 간접 드로우를 겁니다), 한쪽이 꺼지는 순간
        /// 아직 그리고 있는 다른 쪽이 있는데도 신호가 내려가 터레인 디테일이 함께 켜집니다.
        /// 메모리도 두 배입니다 — 포기 백만 개짜리 버퍼가 두 벌이 됩니다.
        /// </summary>
        void Awake()
        {
            if (active != null && active != this)
            {
                GameLog.Error(GameLog.Channel.World,
                    "GpuGrassRenderer 가 둘입니다. 풀이 두 겹으로 그려지고 GPU 버퍼도 두 벌이 됩니다. " +
                    "WorldRuntimeInstaller 를 보세요.", this);
            }

            active = this;
        }

        /// <summary>
        /// 설정이 켜져 있으면 준비하고, 매 프레임 골라내어 그립니다.
        /// </summary>
        void LateUpdate()
        {
            CarDriveWorldSettings settings = CarDriveWorldSettings.Instance;

            if (!settings.gpuGrass)
            {
                // 실행 중에 끄면 즉시 물러납니다. TerrainDetailLod 가 IsDrawing 이 거짓이 된 것을
                // 보고 터레인 디테일의 거리를 되돌려 놓습니다. 그것이 이 경로의 비상구입니다.
                if (ready || scanTargets != null) Teardown();
                return;
            }

            if (!ready)
            {
                Scan();
                if (!ready) return;
            }

            Camera camera = Common.GameContext.MainCamera;
            if (camera == null) return;

            RenderAll(camera, settings);

            // 실제로 그린 <b>뒤에</b> 켭니다. 이 순서여야 터레인 디테일이 꺼지는 프레임에
            // 이미 GPU 풀이 화면에 있습니다.
            IsDrawing = true;
        }

        /// <summary>
        /// 꺼지면 <see cref="IsDrawing"/> 을 내립니다.
        ///
        /// 이것이 없으면 오브젝트를 꺼 둔 채로 <c>LateUpdate</c> 가 멈추고, 신호만 참으로 남아
        /// <see cref="TerrainDetailLod"/> 가 <b>영영 풀을 끈 채</b>로 있게 됩니다.
        /// </summary>
        void OnDisable()
        {
            IsDrawing = false;
        }

        /// <summary>버퍼를 반드시 반납합니다. GraphicsBuffer 는 GC가 거두지 않습니다.</summary>
        void OnDestroy()
        {
            Teardown();
        }

        // --- Private Methods : 준비 ---

        /// <summary>
        /// 지형을 <b>예산만큼만</b> 훑고, 다 훑었으면 GPU 버퍼를 올립니다.
        ///
        /// <b>에셋으로 굽지 않는 이유가 있습니다.</b> 이 월드는 포기가 백만 단위라
        /// 자리만 담아도 수십 MB 입니다. 디테일맵은 이미 지형 안에 있으므로,
        /// 로드할 때 한 번 훑어 만드는 편이 낫습니다.
        ///
        /// <b>밀도는 설정이 아니라 기록에서 옵니다.</b> 여기서 솎아내는 비율은 "지금 얼마나
        /// 그릴까"가 아니라 <b>"심을 때 얼마를 남겼나"</b>이고, 그것은 이미 벌어진 일이라
        /// 실행 중에 만질 수 있는 값이 아닙니다. (<see cref="WorldBakeManifest.detailDensity"/>)
        /// </summary>
        private void Scan()
        {
            if (unsupported) return;
            if (scanTargets == null && !BeginScan()) return;

            float density = WorldBakeManifest.Instance.detailDensity;
            int budget = ScanBudgetPerFrame;

            while (budget > 0 && scanIndex < scanTargets.Length)
            {
                ScanTerrain(scanTargets[scanIndex], density);
                scanIndex++;
                budget--;
            }

            if (scanIndex < scanTargets.Length) return;

            FinishScan();
        }

        /// <summary>
        /// 훑기를 시작할 수 있는지 보고, 되면 대상 목록을 잡아 둡니다.
        /// </summary>
        /// <returns>시작했으면 참</returns>
        private bool BeginScan()
        {
            if (!SystemInfo.supportsComputeShaders)
            {
                unsupported = true;
                GameLog.Warn(GameLog.Channel.World, "GpuGrassRenderer: 이 기기가 컴퓨트 셰이더를 지원하지 않습니다. " +
                                 "터레인 디테일 경로를 그대로 씁니다.");
                return false;
            }

            cullShader = Resources.Load<ComputeShader>(CullShaderName);
            if (cullShader == null)
            {
                unsupported = true;
                GameLog.Warn(GameLog.Channel.World, "GpuGrassRenderer: " + CullShaderName + ".compute 를 Resources 에서 찾지 못했습니다. " +
                                 "Assets/_Project/03.DataAssets/Resources 아래로 옮기거나 링크하세요.");
                return false;
            }

            kernel = cullShader.FindKernel("CSMain");

            Terrain[] found = TerrainRegistry.All;

            // <b>아직 없으면 다음 프레임에 다시 봅니다.</b> 스트리머가 지형을 올리는 중일 수 있습니다.
            // 여기서 접어 버리면 GPU 풀이 영영 켜지지 않습니다.
            if (found.Length == 0) return false;

            // <b>복사합니다.</b> 레지스트리가 돌려주는 배열은 다음 갱신 때 내용이 바뀝니다.
            // 우리는 이것을 여러 프레임에 걸쳐 들고 있어야 합니다.
            scanTargets = new Terrain[found.Length];
            System.Array.Copy(found, scanTargets, found.Length);

            scanIndex = 0;
            scanned = new Dictionary<int, ScannedLayer>();

            return true;
        }

        /// <summary>
        /// 종 하나를 훑을 수 있는지 보고, 쓸 수 있으면 담을 자리를 마련합니다.
        ///
        /// <b>한 번만 봅니다.</b> 프로토타입은 지형마다 같으므로 첫 지형에서 정해진 답이
        /// 나머지 102장에도 그대로 적용됩니다.
        /// </summary>
        /// <param name="proto">볼 디테일 프로토타입</param>
        /// <returns>훑은 것을 담을 자리. 그릴 수 없는 종이면 <c>Positions</c> 가 null 입니다.</returns>
        private static ScannedLayer CreateLayer(DetailPrototype proto)
        {
            ScannedLayer layer = new ScannedLayer();

            if (proto.prototype == null) return layer;

            MeshFilter filter = proto.prototype.GetComponentInChildren<MeshFilter>();
            MeshRenderer renderer = proto.prototype.GetComponentInChildren<MeshRenderer>();

            if (filter == null || filter.sharedMesh == null) return layer;

            // 재질이 없으면 여기서 접습니다. 예전에는 이 확인이 없어서 재질이 빠진 종이
            // <c>new Material(null)</c> 로 넘어갔습니다.
            if (renderer == null || renderer.sharedMaterial == null) return layer;

            layer.Mesh = filter.sharedMesh;
            layer.Material = renderer.sharedMaterial;
            layer.Positions = new List<Vector4>(1 << 16);

            return layer;
        }

        /// <summary>
        /// 지형 한 장의 디테일맵을 훑어 종별 자리 목록에 보탭니다.
        /// </summary>
        /// <param name="terrain">훑을 지형</param>
        /// <param name="density">
        /// 심을 때 디테일맵에 남긴 비율입니다. <see cref="WorldBakeManifest.detailDensity"/> 에서 옵니다.
        /// </param>
        private void ScanTerrain(Terrain terrain, float density)
        {
            if (terrain == null || terrain.terrainData == null) return;

            TerrainData data = terrain.terrainData;
            DetailPrototype[] protos = data.detailPrototypes;
            if (protos == null || protos.Length == 0) return;

            int res = data.detailResolution;
            if (res <= 0) return;

            Vector3 origin = terrain.transform.position;
            Vector3 size = data.size;
            float cell = size.x / res;

            // <b>밀도는 디테일맵에 들어 있지 않습니다.</b> <c>detailObjectDensity</c> 는 터레인이
            // <b>그릴 때</b> 곱하는 값이라 지도에는 심긴 그대로가 남아 있습니다.
            // 여기서 같은 비율로 솎아내지 않으면 GPU 경로만 1/0.55 = 1.8배 촘촘해지고,
            // 그만큼 비싸집니다. 눈으로는 "왜 켜니까 더 무겁지"로만 보입니다.
            //
            // 그 "같은 비율"이 곧 <b>심을 때 쓴 비율</b>이므로, 이 값은 설정이 아니라
            // <see cref="WorldBakeManifest"/> 가 갖고 있습니다.
            float keep = Mathf.Clamp01(density);

            // <b>타일마다 다른 씨앗을 섞습니다.</b> 아래 해시는 격자 좌표만 보는데, 그 좌표는
            // 타일마다 0~255 로 똑같이 돕니다. 섞지 않으면 <b>103장이 전부 같은 무늬</b>가 되어
            // 솎아낸 자리가 100m 주기로 되풀이됩니다.
            int tileSeed = Mathf.RoundToInt(origin.x) * 73856093 ^ Mathf.RoundToInt(origin.z) * 19349663;

            for (int layer = 0; layer < protos.Length; layer++)
            {
                ScannedLayer target;
                if (!scanned.TryGetValue(layer, out target))
                {
                    target = CreateLayer(protos[layer]);
                    scanned[layer] = target;
                }

                // <b>그릴 수 없는 종은 훑지 않습니다.</b> 아래 <c>GetDetailLayer</c> 는 한 번에
                // res × res 짜리 배열을 새로 잡고(256이면 256KB), 그 뒤로 칸마다 해시와
                // <c>SampleHeight</c> 를 돌립니다. 그렇게 만든 자리를 FinishScan 이 버리는 것이
                // 예전 동작이었습니다.
                if (target.Positions == null) continue;

                List<Vector4> list = target.Positions;

                int[,] map = data.GetDetailLayer(0, 0, res, res, layer);

                for (int z = 0; z < res; z++)
                {
                    for (int x = 0; x < res; x++)
                    {
                        int count = map[z, x];
                        if (count <= 0) continue;

                        for (int n = 0; n < count; n++)
                        {
                            // 솎아내기도 자리 해시로 정합니다. 난수로 하면 실행할 때마다
                            // 남는 포기가 달라져 세이브를 불러올 때 풀밭 모양이 바뀝니다.
                            if (keep < 1f && Hash01(tileSeed ^ x * 19349663 ^ z * 40503 ^ n * 83492791) >= keep) continue;

                            // 칸 안에서 흩뿌립니다. 자리를 씨앗으로 삼아 결정적으로 뽑습니다.
                            // 실행할 때마다 자리가 달라지면 밟힌 자국과 어긋납니다.
                            float jx = Hash01(tileSeed ^ x * 73856093 ^ z * 19349663 ^ n * 83492791);
                            float jz = Hash01(tileSeed ^ x * 19349663 ^ z * 83492791 ^ n * 73856093);

                            float wx = origin.x + (x + jx) * cell;
                            float wz = origin.z + (z + jz) * cell;
                            float wy = terrain.SampleHeight(new Vector3(wx, 0f, wz)) + origin.y;

                            float yaw = Hash01(tileSeed ^ x * 83492791 ^ z * 73856093 ^ n * 19349663) * Mathf.PI * 2f;

                            list.Add(new Vector4(wx, wy, wz, yaw));
                        }
                    }
                }
            }
        }

        /// <summary>
        /// 다 훑은 자리들을 종별 GPU 버퍼로 올립니다.
        /// </summary>
        private void FinishScan()
        {
            // 쓸 수 있는지는 <see cref="CreateLayer"/> 가 훑기 전에 이미 가려 두었습니다.
            foreach (KeyValuePair<int, ScannedLayer> entry in scanned)
            {
                ScannedLayer layer = entry.Value;
                if (layer.Positions == null || layer.Positions.Count == 0) continue;

                batches.Add(CreateBatch(layer.Mesh, layer.Material, layer.Positions));
            }

            // 훑는 데 쓴 것은 놓아 줍니다. 백만 개짜리 List 가 그대로 남으면 수십 MB 입니다.
            int scannedTerrains = scanTargets.Length;
            scanTargets = null;
            scanned = null;

            ready = batches.Count > 0;

            if (!ready)
            {
                GameLog.Warn(GameLog.Channel.World, "GpuGrassRenderer: 지형 " + scannedTerrains +
                          "장을 훑었지만 올릴 포기가 없었습니다. 디테일 프로토타입이 비어 있거나 " +
                          "메시·재질이 빠져 있습니다. 터레인 디테일 경로를 그대로 씁니다.");
                return;
            }

            int total = 0;
            for (int i = 0; i < batches.Count; i++) total += batches[i].Count;

            GameLog.Info(GameLog.Channel.World, "GpuGrassRenderer: 지형 " + scannedTerrains + "장 · 종 " + batches.Count +
                      "개 · 포기 " + total.ToString("N0") + "개를 GPU 버퍼에 올렸습니다. (" +
                      (total * InstanceStride / 1024 / 1024) + "MB) " +
                      "이제 TerrainDetailLod 가 터레인 디테일을 끕니다.");
        }

        /// <summary>
        /// 종 하나의 버퍼를 만듭니다.
        /// </summary>
        /// <param name="mesh">잎 메시</param>
        /// <param name="material">잎 재질</param>
        /// <param name="instances">이 종의 포기 자리들</param>
        /// <returns>준비된 묶음</returns>
        private SpeciesBatch CreateBatch(Mesh mesh, Material material, List<Vector4> instances)
        {
            SpeciesBatch batch = new SpeciesBatch();
            batch.Mesh = mesh;

            // 재질을 복제합니다. 원본은 터레인 디테일이 계속 쓰고 있을 수 있고,
            // 여기서 버퍼를 물리면 그쪽까지 영향을 받습니다.
            batch.Material = new Material(material);
            batch.Material.enableInstancing = true;

            batch.Count = instances.Count;

            batch.Instances = new GraphicsBuffer(GraphicsBuffer.Target.Structured, batch.Count, InstanceStride);
            batch.Instances.SetData(instances);

            batch.Visible = new GraphicsBuffer(GraphicsBuffer.Target.Append, batch.Count, sizeof(uint));

            batch.Args = new GraphicsBuffer(GraphicsBuffer.Target.IndirectArguments, 1,
                                            GraphicsBuffer.IndirectDrawIndexedArgs.size);

            // 경계는 넉넉히 잡습니다. 여기가 좁으면 유니티가 통째로 컬링해 <b>아무것도 그려지지 않습니다.</b>
            Bounds bounds = new Bounds(instances[0], Vector3.zero);
            for (int i = 1; i < instances.Count; i++) bounds.Encapsulate(instances[i]);
            bounds.Expand(4f);

            // --- 여기부터는 만든 뒤로 바뀌지 않는 것들입니다. ---
            //
            // 예전에는 아래 셋을 <b>매 프레임 종마다</b> 다시 했습니다. 종이 넷이면
            // 인자 버퍼 업로드 4회, 버퍼 물리기 8회, 크기 범위 대입 4회가 프레임마다 들었습니다.
            // 전부 같은 값을 다시 쓰는 것이었고, 이 경로의 목적이 <b>포기 수와 무관한
            // 프레임당 CPU 비용을 없애는 것</b>이라 이런 것이 남아 있으면 앞뒤가 맞지 않습니다.

            // 1. 간접 드로우 인자. 매 프레임 달라지는 것은 instanceCount 하나뿐이고
            //    그 칸은 CopyCount 가 GPU 안에서 덮어씁니다.
            argsScratch[0].indexCountPerInstance = batch.Mesh.GetIndexCount(0);
            argsScratch[0].instanceCount = 0;
            argsScratch[0].startIndex = batch.Mesh.GetIndexStart(0);
            argsScratch[0].baseVertexIndex = batch.Mesh.GetBaseVertex(0);
            argsScratch[0].startInstance = 0;
            batch.Args.SetData(argsScratch);

            // 2. 재질에 넘길 값들. 버퍼 둘은 이 묶음이 살아 있는 동안 그대로입니다.
            batch.Properties = new MaterialPropertyBlock();
            batch.Properties.SetBuffer(GrassInstancesId, batch.Instances);
            batch.Properties.SetBuffer(GrassVisibleId, batch.Visible);
            batch.Properties.SetVector(ScaleRangeId, ScaleRange);

            // 3. 그리기 설정. 경계도 그림자 설정도 상수입니다.
            batch.Params = new RenderParams(batch.Material)
            {
                worldBounds = bounds,
                matProps = batch.Properties,
                shadowCastingMode = ShadowCastingMode.Off,
                receiveShadows = true
            };

            return batch;
        }

        // --- Private Methods : 그리기 ---

        /// <summary>
        /// 종마다 골라내고 간접 드로우를 겁니다.
        /// </summary>
        /// <param name="camera">기준 카메라</param>
        /// <param name="settings">거리와 크기 범위를 읽을 설정</param>
        private void RenderAll(Camera camera, CarDriveWorldSettings settings)
        {
            GeometryUtility.CalculateFrustumPlanes(camera, planes);
            for (int i = 0; i < 6; i++)
            {
                planeVectors[i] = new Vector4(planes[i].normal.x, planes[i].normal.y,
                                              planes[i].normal.z, planes[i].distance);
            }

            // <b>설정의 detailDistance 를 그대로 쓰지 않습니다.</b>
            //
            // 그 값은 기준값일 뿐이고, 실제로 그려야 할 거리는 rangeScale 과 속도 단계까지
            // 곱해진 사다리의 값입니다. 예전에는 여기서 기준값을 그대로 써서 이 경로만
            // 혼자 70m 를 그렸습니다. 그러면 셰이더가 받는 페이드 창(사다리에서 나옵니다)과
            // 어긋나, 다 지워진 뒤로도 한참을 더 그리게 됩니다.
            float distance = ViewDistances.Current.Grass;

            cullShader.SetVectorArray(PlanesId, planeVectors);
            cullShader.SetVector(CameraId, camera.transform.position);
            cullShader.SetFloat(MaxDistanceId, distance * distance);
            cullShader.SetFloat(RadiusId, settings.tuftRadius + settings.bladeHeight);

            for (int i = 0; i < batches.Count; i++)
            {
                SpeciesBatch batch = batches[i];

                // 지난 프레임에 담긴 것을 비웁니다. 이것을 잊으면 색인이 계속 쌓입니다.
                batch.Visible.SetCounterValue(0);

                cullShader.SetBuffer(kernel, InstancesId, batch.Instances);
                cullShader.SetBuffer(kernel, VisibleId, batch.Visible);
                cullShader.SetInt(CountId, batch.Count);

                int groups = Mathf.Max(1, Mathf.CeilToInt(batch.Count / (float)ThreadGroupSize));
                cullShader.Dispatch(kernel, groups, 1, 1);

                // <b>보이는 수만 GPU 안에서 갈아 끼웁니다.</b> 인자 버퍼의 나머지 네 칸은
                // 만들 때 채워 둔 그대로이고, 이 복사는 그중 instanceCount 한 칸만 덮어씁니다.
                // 재질에 넘길 값과 그리기 설정도 만들 때 한 번 채워 두었습니다.
                GraphicsBuffer.CopyCount(batch.Visible, batch.Args, sizeof(uint));

                Graphics.RenderMeshIndirect(batch.Params, batch.Mesh, batch.Args, 1);
            }
        }

        // --- Private Methods : 정리 ---

        /// <summary>
        /// 버퍼를 모두 반납합니다. <b>GraphicsBuffer 는 GC가 거두지 않습니다.</b>
        /// </summary>
        private void Teardown()
        {
            for (int i = 0; i < batches.Count; i++)
            {
                SpeciesBatch batch = batches[i];

                if (batch.Instances != null) batch.Instances.Release();
                if (batch.Visible != null) batch.Visible.Release();
                if (batch.Args != null) batch.Args.Release();
                if (batch.Material != null) Destroy(batch.Material);
            }

            batches.Clear();

            ready = false;
            IsDrawing = false;

            // 훑던 중이었다면 그것도 버립니다. 다음에 켜면 처음부터 다시 훑습니다.
            // <b>unsupported 는 지우지 않습니다.</b> 기기가 컴퓨트를 못 쓴다는 답은 그대로입니다.
            scanTargets = null;
            scanned = null;
            scanIndex = 0;
        }

        /// <summary>
        /// 정수 하나로 0~1 값을 뽑습니다. 자리가 실행할 때마다 달라지지 않게 씁니다.
        /// </summary>
        /// <param name="seed">씨앗</param>
        /// <returns>0 이상 1 미만</returns>
        private static float Hash01(int seed)
        {
            uint h = (uint)seed;
            h ^= h >> 16;
            h *= 0x7FEB352Du;
            h ^= h >> 15;
            h *= 0x846CA68Bu;
            h ^= h >> 16;

            return (h & 0xFFFFFF) / (float)0x1000000;
        }

        /// <summary>
        /// 플레이 모드에 들어갈 때 정적 상태를 비웁니다.
        /// 도메인 리로드를 꺼 두면 지난 실행의 신호가 그대로 남아,
        /// 아직 아무것도 그리지 않았는데 터레인 디테일이 꺼진 채로 시작합니다.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            IsDrawing = false;
            active = null;
        }
    }
}
