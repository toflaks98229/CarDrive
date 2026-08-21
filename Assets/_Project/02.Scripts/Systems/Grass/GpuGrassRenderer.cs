using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

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
    /// <b>기본은 꺼져 있습니다.</b> 켜면 터레인 디테일을 끄고 이쪽이 그립니다.
    /// 둘 다 켜면 풀이 두 겹으로 보입니다.
    ///
    /// <b>주의 — 이 코드는 아직 실기에서 확인되지 않았습니다.</b>
    /// 간접 드로우와 절차적 인스턴싱은 컴파일이 되어도 화면이 비는 경우가 흔합니다.
    /// 켠 뒤 풀이 보이지 않으면 <see cref="CarDriveWorldSettings.gpuGrass"/> 를 꺼서
    /// 즉시 예전 경로로 돌아갈 수 있습니다.
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

            /// <summary>간접 드로우 인자입니다. 보이는 수를 여기에 복사해 넣습니다.</summary>
            public GraphicsBuffer Args;

            /// <summary>실제 포기 수입니다.</summary>
            public int Count;

            /// <summary>이 종이 덮는 월드 경계입니다. 간접 드로우에 넘길 값입니다.</summary>
            public Bounds Bounds;

            /// <summary>재질 인스턴스에 값을 넘길 때 씁니다.</summary>
            public MaterialPropertyBlock Properties;
        }

        // --- Private Member Variables ---

        private static readonly int InstancesId = Shader.PropertyToID("_Instances");
        private static readonly int VisibleId = Shader.PropertyToID("_VisibleIndices");
        private static readonly int PlanesId = Shader.PropertyToID("_FrustumPlanes");
        private static readonly int CameraId = Shader.PropertyToID("_CameraPosition");
        private static readonly int MaxDistanceId = Shader.PropertyToID("_MaxDistanceSqr");
        private static readonly int RadiusId = Shader.PropertyToID("_InstanceRadius");
        private static readonly int CountId = Shader.PropertyToID("_InstanceCount");

        private static readonly int GrassInstancesId = Shader.PropertyToID("_GrassInstances");
        private static readonly int GrassVisibleId = Shader.PropertyToID("_GrassVisibleIndices");
        private static readonly int ScaleRangeId = Shader.PropertyToID("_GrassScaleRange");

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

        /// <summary>준비가 끝났는지입니다. 실패하면 다시 시도하지 않습니다.</summary>
        private bool ready;

        /// <summary>준비를 한 번이라도 시도했는지입니다.</summary>
        private bool attempted;

        // --- Unity Event Functions ---

        /// <summary>
        /// 설정이 켜져 있으면 준비하고, 매 프레임 골라내어 그립니다.
        /// </summary>
        void LateUpdate()
        {
            CarDriveWorldSettings settings = CarDriveWorldSettings.Instance;

            if (!settings.gpuGrass)
            {
                if (ready) Teardown();
                return;
            }

            if (!attempted) TryPrepare(settings);
            if (!ready) return;

            Camera camera = Common.GameContext.MainCamera;
            if (camera == null) return;

            RenderAll(camera, settings);
        }

        /// <summary>버퍼를 반드시 반납합니다. GraphicsBuffer 는 GC가 거두지 않습니다.</summary>
        void OnDestroy()
        {
            Teardown();
        }

        // --- Private Methods : 준비 ---

        /// <summary>
        /// 지형의 디테일맵을 읽어 포기 자리를 만들고 GPU 버퍼에 올립니다.
        ///
        /// <b>에셋으로 굽지 않는 이유가 있습니다.</b> 이 월드는 포기가 50만~200만이라
        /// 자리만 담아도 8~32MB 입니다. 디테일맵은 이미 지형 안에 있으므로,
        /// 로드할 때 한 번 훑어 만드는 편이 낫습니다.
        /// </summary>
        /// <param name="settings">거리·밀도를 읽을 설정</param>
        private void TryPrepare(CarDriveWorldSettings settings)
        {
            attempted = true;

            if (!SystemInfo.supportsComputeShaders)
            {
                Debug.LogWarning("GpuGrassRenderer: 이 기기가 컴퓨트 셰이더를 지원하지 않습니다. " +
                                 "터레인 디테일 경로를 그대로 씁니다.");
                return;
            }

            cullShader = Resources.Load<ComputeShader>(CullShaderName);
            if (cullShader == null)
            {
                Debug.LogWarning("GpuGrassRenderer: " + CullShaderName + ".compute 를 Resources 에서 찾지 못했습니다. " +
                                 "Assets/_Project/03.DataAssets/Resources 아래로 옮기거나 링크하세요.");
                return;
            }

            kernel = cullShader.FindKernel("CSMain");

            Terrain[] terrains = Object.FindObjectsByType<Terrain>(FindObjectsInactive.Include);
            if (terrains.Length == 0) return;

            BuildBatches(terrains);

            ready = batches.Count > 0;

            if (ready)
            {
                int total = 0;
                for (int i = 0; i < batches.Count; i++) total += batches[i].Count;

                Debug.Log("GpuGrassRenderer: 종 " + batches.Count + "개 · 포기 " + total.ToString("N0") +
                          "개를 GPU 버퍼에 올렸습니다. (" + (total * InstanceStride / 1024 / 1024) + "MB) " +
                          "터레인 디테일을 끄지 않으면 풀이 두 겹으로 보입니다.");
            }
        }

        /// <summary>
        /// 지형마다 디테일맵을 훑어 종별 포기 목록을 만듭니다.
        /// </summary>
        /// <param name="terrains">훑을 지형들</param>
        private void BuildBatches(Terrain[] terrains)
        {
            // 종은 지형의 디테일 프로토타입 순서를 그대로 씁니다.
            // 그래야 지금 심겨 있는 것과 같은 종·같은 재질이 나옵니다.
            Dictionary<int, List<Vector4>> byPrototype = new Dictionary<int, List<Vector4>>();
            Dictionary<int, DetailPrototype> prototypes = new Dictionary<int, DetailPrototype>();

            for (int t = 0; t < terrains.Length; t++)
            {
                Terrain terrain = terrains[t];
                if (terrain == null || terrain.terrainData == null) continue;

                TerrainData data = terrain.terrainData;
                DetailPrototype[] protos = data.detailPrototypes;
                if (protos == null || protos.Length == 0) continue;

                int res = data.detailResolution;
                if (res <= 0) continue;

                Vector3 origin = terrain.transform.position;
                Vector3 size = data.size;
                float cell = size.x / res;

                for (int layer = 0; layer < protos.Length; layer++)
                {
                    if (!prototypes.ContainsKey(layer)) prototypes[layer] = protos[layer];

                    List<Vector4> list;
                    if (!byPrototype.TryGetValue(layer, out list))
                    {
                        list = new List<Vector4>(1 << 16);
                        byPrototype[layer] = list;
                    }

                    int[,] map = data.GetDetailLayer(0, 0, res, res, layer);

                    for (int z = 0; z < res; z++)
                    {
                        for (int x = 0; x < res; x++)
                        {
                            int count = map[z, x];
                            if (count <= 0) continue;

                            for (int n = 0; n < count; n++)
                            {
                                // 칸 안에서 흩뿌립니다. 자리를 씨앗으로 삼아 결정적으로 뽑습니다.
                                // 실행할 때마다 자리가 달라지면 밟힌 자국과 어긋납니다.
                                float jx = Hash01(x * 73856093 ^ z * 19349663 ^ n * 83492791);
                                float jz = Hash01(x * 19349663 ^ z * 83492791 ^ n * 73856093);

                                float wx = origin.x + (x + jx) * cell;
                                float wz = origin.z + (z + jz) * cell;
                                float wy = terrain.SampleHeight(new Vector3(wx, 0f, wz)) + origin.y;

                                float yaw = Hash01(x * 83492791 ^ z * 73856093 ^ n * 19349663) * Mathf.PI * 2f;

                                list.Add(new Vector4(wx, wy, wz, yaw));
                            }
                        }
                    }
                }
            }

            foreach (KeyValuePair<int, List<Vector4>> entry in byPrototype)
            {
                if (entry.Value.Count == 0) continue;

                DetailPrototype proto = prototypes[entry.Key];
                if (proto.prototype == null) continue;

                MeshFilter filter = proto.prototype.GetComponentInChildren<MeshFilter>();
                MeshRenderer renderer = proto.prototype.GetComponentInChildren<MeshRenderer>();
                if (filter == null || filter.sharedMesh == null || renderer == null) continue;

                batches.Add(CreateBatch(filter.sharedMesh, renderer.sharedMaterial, entry.Value));
            }
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
            batch.Bounds = bounds;

            batch.Properties = new MaterialPropertyBlock();

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

            float distance = settings.detailDistance;

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

                // 인자를 채웁니다. 색인 수는 GPU만 아는 값이라 카운터를 복사해 넣습니다.
                GraphicsBuffer.IndirectDrawIndexedArgs[] args = new GraphicsBuffer.IndirectDrawIndexedArgs[1];
                args[0].indexCountPerInstance = batch.Mesh.GetIndexCount(0);
                args[0].instanceCount = 0;
                args[0].startIndex = batch.Mesh.GetIndexStart(0);
                args[0].baseVertexIndex = batch.Mesh.GetBaseVertex(0);
                args[0].startInstance = 0;
                batch.Args.SetData(args);

                GraphicsBuffer.CopyCount(batch.Visible, batch.Args, sizeof(uint));

                batch.Properties.SetBuffer(GrassInstancesId, batch.Instances);
                batch.Properties.SetBuffer(GrassVisibleId, batch.Visible);
                batch.Properties.SetVector(ScaleRangeId, new Vector2(0.85f, 1.25f));

                RenderParams rp = new RenderParams(batch.Material);
                rp.worldBounds = batch.Bounds;
                rp.matProps = batch.Properties;
                rp.shadowCastingMode = ShadowCastingMode.Off;
                rp.receiveShadows = true;

                Graphics.RenderMeshIndirect(rp, batch.Mesh, batch.Args, 1);
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
            attempted = false;
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
        /// 씬에 없으면 게임이 시작될 때 스스로 하나 생겨납니다.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Spawn()
        {
            GameObject go = new GameObject("GpuGrassRenderer");
            go.hideFlags = HideFlags.DontSave;

            go.AddComponent<GpuGrassRenderer>();
            DontDestroyOnLoad(go);
        }
    }
}
