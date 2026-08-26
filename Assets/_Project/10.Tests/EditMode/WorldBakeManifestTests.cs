using System.IO;
using NUnit.Framework;
using UnityEngine;
using CarDrive.Systems;

namespace CarDrive.Tests
{
    /// <summary>
    /// 베이크 기록이 <b>지금 저장소에 있는 월드와 맞는지</b> 고정합니다.
    ///
    /// <b>왜 이 테스트인가.</b> 이 기록의 값들은 검증할 방법이 없는 종류입니다 —
    /// 디테일맵에 포기가 얼마나 심겼는지는 구운 순간 정해졌고, 굽는 도구는
    /// 커밋 <c>1170605</c> 가 <c>CarDrive.Editor</c> 어셈블리를 걷어내면서 사라졌습니다.
    /// 그래서 값이 틀려도 <b>틀렸다는 사실 자체가 드러나지 않습니다.</b>
    /// GPU 풀만 혼자 1.8배 촘촘해지고, 눈으로는 "왜 켜니까 더 무겁지"로만 보입니다.
    ///
    /// 전부를 확인할 수는 없지만 <b>하나는 셀 수 있습니다</b> — 타일의 수입니다.
    /// 그것이 기록과 다르면 월드가 구운 뒤에 바뀌었다는 뜻이고, 그러면 같은 기록에
    /// 적힌 밀도와 한 변 길이도 그 월드의 것이 아닙니다. 확인할 수 있는 한 칸을
    /// 붙잡아 두면 나머지가 흔들렸을 때 <b>함께 흔들립니다.</b>
    /// </summary>
    public class WorldBakeManifestTests
    {
        /// <summary>구운 타일들이 있는 폴더입니다.</summary>
        private const string TerrainFolder = "_Project/03.DataAssets/Terrain/Generated";

        /// <summary>
        /// 저장소에 실제로 있는 타일 수를 셉니다.
        /// </summary>
        /// <returns>Tile_*.asset 의 개수</returns>
        private static int CountBakedTiles()
        {
            string full = Path.Combine(Application.dataPath, TerrainFolder);

            Assert.IsTrue(Directory.Exists(full), "구운 지형 폴더를 찾지 못했습니다: " + TerrainFolder);

            return Directory.GetFiles(full, "Tile_*.asset", SearchOption.TopDirectoryOnly).Length;
        }

        /// <summary>
        /// Resources 에 기록이 실제로 놓여 있어야 합니다.
        ///
        /// <b>없어도 게임은 돕니다</b> — <see cref="WorldBakeManifest.Instance"/> 가 기본값이 든
        /// 것을 만들어 주고, 그 기본값은 지금 월드의 값 그대로입니다. 다만 그 상태로 굳으면
        /// 기록이라는 것이 <b>코드 안의 숫자</b>로 되돌아갑니다. 그 자리를 지킵니다.
        /// </summary>
        [Test]
        public void 기록이_Resources_에_있다()
        {
            WorldBakeManifest loaded = Resources.Load<WorldBakeManifest>(WorldBakeManifest.ResourceName);

            Assert.IsNotNull(loaded,
                "WorldBakeManifest 에셋을 Resources 에서 찾지 못했습니다. " +
                "없으면 코드의 기본값으로 물러서므로 기록이 코드 안의 숫자로 되돌아갑니다.");
        }

        /// <summary>
        /// 기록의 타일 수가 <b>저장소에 실제로 있는 수</b>와 같아야 합니다.
        ///
        /// 다르면 둘 중 하나입니다 — 타일을 더하거나 뺐는데 기록을 안 고쳤거나,
        /// 기록을 고쳤는데 월드를 안 구웠거나. 어느 쪽이든 이 기록의 다른 값들을
        /// 믿을 수 없게 됩니다.
        /// </summary>
        [Test]
        public void 기록의_타일_수가_저장소와_같다()
        {
            WorldBakeManifest manifest = WorldBakeManifest.Instance;
            int onDisk = CountBakedTiles();

            Assert.IsTrue(manifest.MatchesTileCount(onDisk),
                "베이크 기록에는 타일이 " + manifest.tileCount + "장으로 적혀 있는데 " +
                "저장소에는 " + onDisk + "장 있습니다. 월드가 구운 뒤에 바뀌었다면 " +
                "밀도(detailDensity)와 한 변 길이(tileSize)도 지금 월드의 값이 아닐 수 있습니다.");
        }

        /// <summary>
        /// 밀도는 <b>0과 1 사이</b>여야 합니다.
        ///
        /// <see cref="GpuGrassRenderer"/> 가 이 값으로 디테일맵을 솎아냅니다. 0이면 풀이
        /// 하나도 남지 않고, 1을 넘으면 솎아내기가 아무 일도 하지 않아 GPU 경로만
        /// 심긴 그대로를 그립니다. 둘 다 화면으로만 보면 원인을 찾기 어렵습니다.
        /// </summary>
        [Test]
        public void 밀도가_쓸_수_있는_범위다()
        {
            WorldBakeManifest manifest = WorldBakeManifest.Instance;

            Assert.Greater(manifest.detailDensity, 0f, "밀도가 0이면 풀이 하나도 남지 않습니다.");
            Assert.LessOrEqual(manifest.detailDensity, 1f, "밀도는 심긴 것 중 남길 비율이라 1을 넘을 수 없습니다.");
        }

        /// <summary>
        /// 타일 한 변의 길이가 <b>양수</b>여야 합니다.
        ///
        /// 0이면 <see cref="Gameplay.WorldStreamer"/> 가 타일을 <b>점</b>으로 봅니다.
        /// 그러면 타일의 원점 모서리까지만 재게 되어, 활성 거리가 "이 안에는 빈 곳이 없다"는
        /// 약속을 지키지 못합니다. 예전에 땅이 눈앞에서 튀어나오던 것이 정확히 그 상태였습니다.
        /// </summary>
        [Test]
        public void 타일_한_변이_양수다()
        {
            Assert.Greater(WorldBakeManifest.Instance.tileSize, 0f,
                "한 변이 0이면 타일을 점으로 보게 되어 활성 거리의 약속이 깨집니다.");
        }
    }
}
