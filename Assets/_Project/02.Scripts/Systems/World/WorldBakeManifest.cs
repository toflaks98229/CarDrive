using UnityEngine;

namespace CarDrive.Systems
{
    /// <summary>
    /// 이 월드를 <b>구울 때 쓴 값</b>을 적어 둔 기록입니다.
    ///
    /// <b>설정과 무엇이 다른가.</b> <see cref="CarDriveWorldSettings"/> 는 <b>지금 돌릴 값</b>이고
    /// 실행 중에 만져도 됩니다. 여기 있는 것은 <b>이미 벌어진 일</b>이라 만지면 안 됩니다.
    /// 디테일맵에 포기가 얼마나 심겼는지, 타일 한 변이 몇 미터인지는 구운 순간 정해졌고,
    /// 그 사실을 바꾸려면 값이 아니라 <b>월드를 다시 구워야</b> 합니다.
    ///
    /// <b>왜 나누는가.</b> 나누기 전에는 그 둘이 <see cref="CarDriveWorldSettings"/> 한곳에
    /// 섞여 있었습니다. 그래서 <c>detailDensity</c> 를 만지는 사람은 그것이 "지금 그릴 밀도"인지
    /// "구울 때 쓴 밀도"인지 알 수 없었고, 실제로는 <b>둘 다</b>였습니다 —
    /// <see cref="GpuGrassRenderer"/> 가 디테일맵을 직접 읽으며 같은 비율로 솎아내야 하므로
    /// 구운 값과 <b>반드시 같아야</b> 하는데, 그 계약이 툴팁 한 줄로만 있었습니다.
    /// 값을 바꾸면 GPU 경로의 밀도만 조용히 달라지고, 확인할 방법이 없었습니다.
    ///
    /// 이제 <b>구운 값은 여기</b>, 돌릴 값은 저기입니다. 섞일 자리가 없습니다.
    ///
    /// <b>읽는 코드가 없는 필드가 있습니다. 그것이 이 에셋의 목적입니다.</b>
    /// 아래 "재생성에만 필요한 기록"의 값들은 지금 아무도 읽지 않습니다. 월드를 다시 구울 때
    /// 같은 세계가 나오게 하려면 필요한 값이고, 그때가 오기 전까지는 <b>적어 두는 것 말고
    /// 할 일이 없습니다.</b> 설정 에셋이었다면 이것은 결함이지만(돌려도 아무 일이 없는 손잡이),
    /// 기록에서는 정반대입니다.
    ///
    /// <b>이 값들의 출처.</b> 굽는 도구(<c>WorldTerrainBaker</c>·<c>VegetationBuilder</c>·
    /// <c>VegetationPainter</c>)는 커밋 <c>1170605</c> 가 <c>CarDrive.Editor</c> 어셈블리를
    /// 통째로 걷어내면서 함께 사라졌습니다. 그 값들은 <c>CarDriveWorldSettings.asset</c> 의
    /// YAML 에 <b>C# 필드가 지워진 뒤에도 고아로 남아</b> 있었고, 여기 옮겨 적은 것이 그것입니다.
    /// 다시 구우려면 <c>git show 1170605^:Assets/_Project/02.Scripts/Editor/WorldTerrainBaker.cs</c>
    /// 로 도구를 먼저 되살리세요.
    /// </summary>
    [CreateAssetMenu(fileName = "WorldBakeManifest", menuName = "CarDrive/월드 베이크 기록")]
    public class WorldBakeManifest : ScriptableObject
    {
        // --- Constants ---

        /// <summary>Resources 에서 찾을 이름입니다.</summary>
        public const string ResourceName = "WorldBakeManifest";

        // --- 런타임이 실제로 읽는 값 ---

        /// <summary>
        /// 포기를 심을 때 <b>디테일맵에 남긴 비율</b>입니다.
        ///
        /// <b>왜 런타임이 이것을 읽어야 하는가.</b> 터레인 디테일 경로는 이 값을 몰라도 됩니다 —
        /// <c>Terrain.detailObjectDensity</c> 가 <b>그릴 때</b> 곱하기 때문입니다.
        /// 그런데 <see cref="GpuGrassRenderer"/> 는 디테일맵을 <b>직접</b> 읽어 포기 자리를
        /// 만들므로, 같은 비율로 <b>스스로</b> 솎아내지 않으면 GPU 경로만 1/0.55 = 1.8배
        /// 촘촘해집니다. 눈으로는 "왜 켜니까 더 무겁지"로만 보입니다.
        ///
        /// 그래서 이 값은 <b>구운 값과 같아야</b> 하는 것이 아니라 <b>구운 값 그 자체</b>입니다.
        /// </summary>
        [Header("런타임이 실제로 읽는 값")]
        [Tooltip("포기를 심을 때 디테일맵에 남긴 비율. GpuGrassRenderer 가 디테일맵을 직접 읽으며 " +
                 "같은 비율로 솎아낼 때 씁니다. 구운 값 그 자체이므로 만지지 마세요 — " +
                 "바꾸려면 월드를 다시 구워야 합니다.")]
        [Range(0.1f, 1f)]
        public float detailDensity = 0.55f;

        /// <summary>
        /// 타일 한 변의 길이(m)입니다.
        ///
        /// <b>왜 인스펙터 값이 아니라 여기인가.</b> <see cref="Gameplay.WorldStreamer"/> 는
        /// 타일에서 <b>가장 가까운 점</b>까지의 거리로 켤지 정하는데, 그 계산에 한 변의 길이가
        /// 필요합니다. 예전에는 <c>fallbackTileSize</c> 를 썼는데 그것은 <b>타일을 새로 깔 때</b>
        /// 다음 지점을 못 찾으면 쓰는 기본 보폭이라, 뜻이 다른 두 값이 <b>숫자가 같다는 이유로</b>
        /// 한 필드를 나눠 쓰고 있었습니다. 절차적 배치 경로의 기본값을 만지면 구운 월드의
        /// 거리 판정이 함께 틀어집니다.
        ///
        /// 활성 거리가 "이 거리 안에는 빈 곳이 없다"는 약속인데, 그 약속의 근거가 되는 수치입니다.
        /// </summary>
        [Tooltip("구운 타일 한 변의 길이(m). WorldStreamer 가 타일의 가장 가까운 점까지 거리를 " +
                 "잴 때 씁니다. 절차적 배치의 fallbackTileSize 와는 뜻이 다른 값입니다.")]
        public float tileSize = 100f;

        /// <summary>
        /// 구운 타일의 수입니다. <b>맞는지 확인하는 데만 씁니다.</b>
        ///
        /// 거리 판정에 쓰이지 않습니다. 씬의 타일 수가 이것과 다르면 누군가 타일을 더하거나
        /// 뺐다는 뜻이고, 그때는 이 기록의 <b>다른 값들도 못 믿습니다.</b>
        /// 0 이하로 두면 확인하지 않습니다.
        /// </summary>
        [Tooltip("구운 타일의 수. 거리 판정에는 쓰이지 않고, 씬의 타일 수와 달라졌는지 " +
                 "확인하는 데만 씁니다. 0 이하면 확인하지 않습니다.")]
        public int tileCount = 103;

        // --- 재생성에만 필요한 기록 (읽는 코드가 없습니다) ---

        /// <summary>
        /// 배치를 정한 난수 시드입니다. 같은 값이어야 같은 세계가 나옵니다.
        ///
        /// <see cref="Gameplay.WorldStreamer.layoutSeed"/> 와 같은 값이어야 하지만, 그쪽은
        /// <b>절차적 배치 경로</b>에서만 쓰이고 지금 월드는 그 경로를 타지 않습니다.
        /// 여기 적어 두는 것은 다시 구울 때를 위해서입니다.
        /// </summary>
        [Header("재생성에만 필요한 기록 (지금은 읽는 코드가 없습니다)")]
        [Tooltip("배치를 정한 난수 시드. 다시 구울 때 같은 세계가 나오게 하려면 필요합니다.")]
        public int layoutSeed = 20260817;

        /// <summary>디테일맵 한 칸에 심은 포기 수입니다.</summary>
        [Tooltip("디테일맵 한 칸에 심은 포기 수.")]
        public int maxPerCell = 1;

        /// <summary>이 값을 넘는 잔디 가중치에만 포기를 심었습니다.</summary>
        [Tooltip("포기를 심을 잔디 가중치의 문턱.")]
        public float grassThreshold = 0.4f;

        /// <summary>한 포기에 넣은 잎의 수입니다. 구워진 메시가 이미 갖고 있는 값입니다.</summary>
        [Tooltip("한 포기에 넣은 잎의 수. 구워진 Grass* 메시가 이미 갖고 있는 값입니다.")]
        public int bladesPerTuft = 18;

        // --- Public Properties ---

        /// <summary>
        /// 어디서나 쓸 수 있는 기록입니다. 없으면 기본값이 든 것을 하나 만들어 돌려줍니다.
        ///
        /// <b>없을 때 예외를 던지지 않는 것이 중요합니다.</b> 이 클래스의 기본값은
        /// <b>지금 저장소에 있는 월드의 값 그대로</b>이므로, 에셋이 없거나 깨져도 동작이
        /// 달라지지 않습니다. 기록이 사라졌을 때 게임이 멈추는 것이 가장 나쁜 결과입니다.
        /// (<see cref="CarDriveWorldSettings.Instance"/> 와 같은 방식입니다)
        /// </summary>
        public static WorldBakeManifest Instance
        {
            get
            {
                if (cached != null) return cached;

                cached = Resources.Load<WorldBakeManifest>(ResourceName);
                if (cached == null) cached = CreateInstance<WorldBakeManifest>();

                return cached;
            }
        }

        // --- Private Member Variables ---

        /// <summary>찾아 둔 기록입니다.</summary>
        private static WorldBakeManifest cached;

        // --- Public Methods ---

        /// <summary>
        /// 씬의 타일 수가 기록과 <b>같은지</b> 봅니다.
        ///
        /// 다르면 누군가 타일을 더하거나 뺐다는 뜻이고, 그러면 이 기록의 다른 값들
        /// (밀도·한 변 길이)도 그 월드의 것이 아닐 수 있습니다. 조용히 넘어가면
        /// GPU 풀의 밀도가 혼자 달라진 채로 굴러가고, <b>그 사실이 어디에도 드러나지 않습니다.</b>
        /// </summary>
        /// <param name="actualTileCount">지금 씬에서 센 타일 수</param>
        /// <returns>기록과 같거나 확인하지 않기로 했으면 참</returns>
        public bool MatchesTileCount(int actualTileCount)
        {
            if (tileCount <= 0) return true;
            return tileCount == actualTileCount;
        }

        // --- Private Methods ---

        /// <summary>
        /// 플레이 모드에 들어갈 때 찾아 둔 것을 비웁니다.
        /// 에디터에서 도메인 리로드를 꺼 두면 지난 실행의 값이 그대로 남기 때문입니다.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            cached = null;
        }
    }
}
