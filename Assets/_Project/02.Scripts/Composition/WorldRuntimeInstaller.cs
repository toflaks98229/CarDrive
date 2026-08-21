using UnityEngine;
using VContainer;
using VContainer.Unity;
using CarDrive.Common;
using CarDrive.Systems;

namespace CarDrive.Composition
{
    /// <summary>
    /// 지형·시야·풀을 돌리는 <b>런타임 구동체들을 하나만 만들어</b> 컨테이너에 등록합니다.
    ///
    /// <b>무엇을 고쳤는가.</b> 이 다섯은 각자 <c>[RuntimeInitializeOnLoadMethod]</c>로
    /// 자기를 만들었습니다. 주석에는 "씬에 없으면 생겨납니다"라고 적혀 있었지만
    /// 실제로는 <b>확인 없이 무조건</b> 만들었습니다. 그래서 값을 보려고 씬에 하나 얹는 순간
    /// 둘이 되고, <see cref="TerrainChunkCuller"/>는 상태가 전부 static이라
    /// <c>ApplyNow</c>가 프레임당 두 번 돌아 예산제가 두 배로 헐거워졌습니다.
    ///
    /// 이제 <b>무엇이 존재하는지는 여기서만 정합니다.</b> 씬에 이미 있으면 그것을 쓰고,
    /// 없을 때만 만듭니다. 만드는 순서도 여기 적힌 순서 그대로입니다.
    ///
    /// <b>실행 순서는 각 컴포넌트의 <c>[DefaultExecutionOrder]</c>가 계속 정합니다.</b>
    /// 컬러(-100) → 디테일 LOD(-99) → … → 시야 스케일러(200). 그 값들은 서로의 전제라
    /// 여기서 손대지 않습니다.
    /// </summary>
    public class WorldRuntimeInstaller : MonoBehaviour, IInstaller
    {
        // --- Public Member Variables ---

        /// <summary>
        /// 만들어 낸 구동체들을 담아 둘 오브젝트의 이름입니다. 하이어라키에서 찾기 쉬우라고 둡니다.
        /// </summary>
        [Header("런타임 구동체")]
        [Tooltip("자동 생성되는 월드 구동체들을 담을 오브젝트의 이름입니다.")]
        public string driverObjectName = "[World Runtime]";

        /// <summary>
        /// 실험용 GPU 풀 렌더러를 함께 만들지 여부입니다.
        ///
        /// <see cref="CarDriveWorldSettings.gpuGrass"/>가 꺼져 있으면 렌더러가 있어도
        /// 아무 일도 하지 않지만, 아직 실기에서 확인되지 않은 경로라 <b>기본은 만들지 않습니다.</b>
        /// </summary>
        [Tooltip("실험용 GPU 풀 렌더러를 함께 만듭니다. 월드 설정의 gpuGrass 와 함께 켜세요.")]
        public bool createGpuGrassRenderer = false;

        // --- Private Member Variables ---

        /// <summary>이번에 만든(또는 씬에서 찾은) 구동체들의 부모입니다.</summary>
        private Transform driverRoot;

        // --- Public Methods ---

        /// <summary>
        /// 월드 구동체를 하나씩 확보해 컨테이너에 등록합니다.
        /// </summary>
        /// <param name="builder">등록할 컨테이너 빌더</param>
        public void Install(IContainerBuilder builder)
        {
            TerrainChunkCuller culler = Ensure<TerrainChunkCuller>();
            TerrainDetailLod detailLod = Ensure<TerrainDetailLod>();
            ViewRangeScaler viewScaler = Ensure<ViewRangeScaler>();
            GrassPushField pushField = Ensure<GrassPushField>();

            builder.RegisterComponent(culler);
            builder.RegisterComponent(detailLod);
            builder.RegisterComponent(viewScaler);
            builder.RegisterComponent(pushField);

            if (createGpuGrassRenderer)
            {
                builder.RegisterComponent(Ensure<GpuGrassRenderer>());
            }

            GameLog.InfoFormat(GameLog.Channel.World,
                "[WorldRuntimeInstaller] 월드 구동체를 '{0}' 아래에 확보했습니다.", driverObjectName, this);
        }

        // --- Private Methods ---

        /// <summary>
        /// 이 타입의 컴포넌트를 <b>하나만</b> 확보합니다.
        ///
        /// 씬에 이미 있으면(누군가 값을 보려고 얹어 둔 경우) 그것을 그대로 씁니다.
        /// 없을 때만 구동체 오브젝트에 붙입니다. <b>어느 경우에도 둘이 되지 않습니다.</b>
        /// </summary>
        /// <typeparam name="T">확보할 컴포넌트 타입</typeparam>
        /// <returns>씬에 하나만 존재하게 된 컴포넌트</returns>
        private T Ensure<T>() where T : Component
        {
            T existing = Object.FindAnyObjectByType<T>(FindObjectsInactive.Include);
            if (existing != null) return existing;

            return GetDriverRoot().gameObject.AddComponent<T>();
        }

        /// <summary>
        /// 구동체를 담아 둘 오브젝트를 돌려줍니다. 없으면 만듭니다.
        ///
        /// <c>HideFlags</c>를 걸지 않습니다. 예전에는 <c>HideAndDontSave</c>라
        /// 하이어라키에서 보이지 않았고, 그래서 "왜 두 번 도는지" 알아채기 어려웠습니다.
        /// 보이는 편이 낫습니다.
        /// </summary>
        /// <returns>구동체들의 부모 Transform</returns>
        private Transform GetDriverRoot()
        {
            if (driverRoot != null) return driverRoot;

            GameObject go = new GameObject(driverObjectName);
            driverRoot = go.transform;

            // 씬을 다시 불러들여도 살아남아야 합니다. 매번 다시 만들면 첫 프레임에 풀이 튑니다.
            DontDestroyOnLoad(go);

            return driverRoot;
        }
    }
}
