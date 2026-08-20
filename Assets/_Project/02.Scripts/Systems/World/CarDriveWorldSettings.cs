using System.Collections.Generic;
using UnityEngine;

namespace CarDrive.Systems
{
    /// <summary>
    /// 월드의 겉모습과 풀에 관한 값을 한곳에 모아 둔 설정입니다.
    ///
    /// 이 값들은 원래 도구마다 <c>private const</c> 로 흩어져 있었습니다.
    /// 룩을 이것저것 시험하는 동안에는 그래도 됐지만, 방향이 정해진 지금은
    /// <b>값을 만지려고 코드를 열어야 하는 것</b>이 불편할 뿐입니다.
    ///
    /// 런타임 어셈블리에 두었습니다. 에디터 도구뿐 아니라 게임 중에 도는
    /// <see cref="TerrainChunkCuller"/> 도 같은 값을 봐야 하기 때문입니다.
    /// </summary>
    [CreateAssetMenu(fileName = "CarDriveWorldSettings", menuName = "CarDrive/월드 설정")]
    public class CarDriveWorldSettings : ScriptableObject
    {
        // --- Constants ---

        /// <summary>Resources 에서 찾을 이름입니다.</summary>
        public const string ResourceName = "CarDriveWorldSettings";

        // --- 풀 심기 ---

        /// <summary>격자 한 칸에 심을 최대 포기 수입니다.</summary>
        [Header("풀 심기")]
        [Tooltip("격자 한 칸(약 0.39m)에 심을 최대 포기 수. " +
                 "드로우 콜은 잎 수가 아니라 '포기 수'에 정비례합니다. " +
                 "Unity 터레인 디테일은 드로우 콜 하나에 약 500개까지만 담기 때문입니다. " +
                 "촘촘하게 하고 싶으면 이 값이 아니라 '포기당 잎 수'를 올리세요.")]
        [Range(1, 10)]
        public int maxPerCell = 2;

        /// <summary>밀도 배율입니다.</summary>
        [Tooltip("밀도 배율. 무거우면 이 값을 내리는 것이 가장 빠른 처방입니다.")]
        [Range(0.1f, 1f)]
        public float detailDensity = 0.85f;

        /// <summary>풀을 그리는 최대 거리(m)입니다.</summary>
        [Tooltip("풀을 그리는 최대 거리(m).")]
        [Range(20f, 150f)]
        public float detailDistance = 70f;

        /// <summary>잔디 가중치가 이보다 낮으면 심지 않습니다.</summary>
        [Tooltip("잔디 가중치가 이보다 낮으면 심지 않습니다. 높이 잡으면 풀밭이 흙으로 바뀌는 자리가 칼로 자른 듯 끊깁니다.")]
        [Range(0.1f, 0.9f)]
        public float grassThreshold = 0.4f;

        /// <summary>포기 하나에 세울 잎의 수입니다.</summary>
        [Tooltip("포기 하나에 세울 잎의 수. 잎 하나가 삼각형 한 장입니다. " +
                 "촘촘하게 하려면 여기를 올리세요. 잎이 늘어도 포기 수는 그대로라 " +
                 "드로우 콜이 늘지 않습니다. 삼각형만 늘어나는데 이 게임은 CPU 바운드라 그쪽은 여유가 있습니다.")]
        [Range(2, 32)]
        public int bladesPerTuft = 18;

        /// <summary>한 포기가 퍼지는 반경(m)입니다.</summary>
        [Tooltip("한 포기가 퍼지는 반경(m). 잎을 많이 담을수록 넓게 퍼뜨려야 뭉치지 않습니다.")]
        [Range(0.05f, 0.8f)]
        public float tuftRadius = 0.32f;

        /// <summary>잎의 키(m)입니다.</summary>
        [Tooltip("잎의 키(m).")]
        [Range(0.1f, 2f)]
        public float bladeHeight = 0.55f;

        // --- 식생 종 ---

        /// <summary>
        /// 지면에 심을 식생 종들입니다. <b>비워 두면 위의 값으로 기본 세 종을 만듭니다.</b>
        ///
        /// 예전에는 월드 전체가 풀 한 종류였습니다. 지형은 넓은데 눈에 들어오는 것이
        /// 한 가지뿐이라 어디를 가도 같은 곳처럼 보였습니다.
        ///
        /// <b>포기 수는 늘지 않습니다.</b> <see cref="maxPerCell"/>을 종의 비중대로 나눠 심으므로,
        /// 종을 늘려도 총량은 그대로입니다. 대신 잎 수가 적은 종은 오히려 싸집니다.
        /// 각 종은 경사·고도·군집 규칙을 따로 가집니다.
        /// </summary>
        [Header("식생 종 (비워 두면 기본 3종)")]
        [Tooltip("지면에 심을 식생 종들. 비워 두면 위의 잎 수·반경·키로 기본 3종을 만듭니다. " +
                 "칸당 포기 수는 종의 비중대로 나뉘므로 총량은 늘지 않습니다.")]
        public List<VegetationSpecies> vegetation = new List<VegetationSpecies>();

        // --- 풀 그리기 ---

        /// <summary>이 거리까지는 그라데이션이 온전히 남습니다.</summary>
        [Header("풀 그리기")]
        [Tooltip("이 거리(m)까지는 위에서 빛을 받는 그라데이션이 온전히 남습니다.")]
        public float gradientNear = 10f;

        /// <summary>이 거리부터 단색이 됩니다.</summary>
        [Tooltip("이 거리(m)부터 단색이 됩니다. 멀리서 명암이 남으면 부피가 아니라 잡음으로 보입니다.")]
        public float gradientFar = 30f;

        // --- 밟힘 ---

        /// <summary>얼마나 완전히 눕는지입니다.</summary>
        [Header("밟힘")]
        [Tooltip("얼마나 완전히 눕는지. 1보다 낮추면 차 밑에 풀이 남아 실내로 올라옵니다.")]
        [Range(0f, 1f)]
        public float pushLay = 1f;

        /// <summary>바깥으로 밀리는 거리(m)입니다.</summary>
        [Tooltip("밟힌 풀이 바깥으로 밀리는 거리(m).")]
        [Range(0f, 1f)]
        public float pushSpread = 0.35f;

        /// <summary>위아래로 닿는 높이(m)입니다.</summary>
        [Tooltip("위아래로 이 높이(m)보다 떨어져 있으면 누르지 않습니다.")]
        [Range(0.2f, 8f)]
        public float pushHeightReach = 2f;

        // --- 누르개 배선 ---

        /// <summary>바퀴 하나가 누르는 반경(m)입니다.</summary>
        [Header("누르개 배선")]
        [Tooltip("바퀴 하나가 누르는 반경(m).")]
        public float wheelRadius = 1.2f;

        /// <summary>차체가 누르는 반경(m)입니다.</summary>
        [Tooltip("차체가 누르는 반경(m). 바퀴 넷 사이의 빈 가운데를 덮어야 풀이 실내로 올라오지 않습니다.")]
        public float bodyRadius = 2f;

        /// <summary>사람이 누르는 반경(m)입니다.</summary>
        [Tooltip("걸어 다니는 사람이 누르는 반경(m).")]
        public float playerRadius = 0.5f;

        /// <summary>유령이 누르는 반경(m)입니다.</summary>
        [Tooltip("유령이 누르는 반경(m). 유령은 자국을 남기지 않습니다.")]
        public float ghostRadius = 0.6f;

        /// <summary>사람의 무게(kg)입니다.</summary>
        [Tooltip("사람의 무게(kg). 자국이 남는 시간이 여기서 나옵니다.")]
        public float playerMass = 70f;

        // --- 청크 컬링 ---

        /// <summary>
        /// 이 거리를 넘는 타일은 나무와 풀을 접습니다.
        ///
        /// <b>나무와 풀은 함께 꺼집니다.</b> 유니티의 <c>drawTreesAndFoliage</c> 가 하나뿐이라
        /// 둘을 나눌 수 없습니다. 그래서 이 값이 나무 그리기 거리보다 짧으면
        /// <b>아직 보여야 할 나무가 타일 단위로 사라집니다.</b>
        ///
        /// 실제로 95m 로 두었다가 나무가 눈앞에서 통째로 튀어나오는 문제가 있었습니다.
        /// 지금은 <see cref="TerrainChunkCuller"/> 가 이 값과 터레인의 나무 그리기 거리 중
        /// <b>더 큰 쪽</b>을 쓰므로, 이 값을 낮춰도 나무가 잘리지는 않습니다.
        ///
        /// 풀만 줄이고 싶으면 이 값이 아니라 <see cref="detailDistance"/> 를 만지세요.
        /// </summary>
        [Header("청크 컬링 (게임 중에도 쓰입니다)")]
        [Tooltip("이 거리(m)를 넘는 타일은 나무와 풀을 접습니다. " +
                 "나무 그리기 거리보다 짧게 잡아도 나무는 잘리지 않습니다. " +
                 "풀만 줄이려면 '풀 그리는 거리'를 쓰세요.")]
        public float foliageDistance = 95f;

        /// <summary>화면 판정에 둘 여유(m)입니다.</summary>
        [Tooltip("화면 판정에 둘 여유(m). 화면 밖 언덕이 드리우는 그림자가 사라지지 않도록 넉넉히 둡니다.")]
        public float shadowMargin = 55f;

        /// <summary>
        /// 화면 밖 타일의 <b>지면까지</b> 통째로 끌지 여부입니다. <b>기본은 끄지 않는 것입니다.</b>
        ///
        /// <b>왜 기본이 꺼짐인가.</b> 컬링의 실제 목적은 풀·나무를 추려 내는 일을 없애는 것인데,
        /// 그건 <see cref="TerrainChunkCuller"/> 가 <c>drawTreesAndFoliage</c> 를 끄는 것만으로 달성됩니다.
        /// 지면 자체는 유니티가 이미 패치 단위로 프러스텀 컬링을 하므로, 컴포넌트를 껐다 켜서
        /// 더 벌 것이 거의 없습니다.
        ///
        /// 반면 비용은 큽니다. <c>Terrain.enabled</c> 를 토글하면 그 타일이 렌더링 시스템에서
        /// 빠졌다가 다시 등록되고 렌더 데이터가 재구성됩니다. 시야를 돌리면 여러 장이 동시에
        /// 그 일을 겪습니다. <b>아끼려던 일보다 껐다 켜는 일이 더 비싼 상황</b>이 됩니다.
        ///
        /// 켜서 비교해 보고 싶을 때만 체크하세요.
        /// </summary>
        [Tooltip("체크하면 화면 밖 타일의 지면까지 끕니다. " +
                 "기본은 꺼짐 — 지면은 유니티가 이미 컬링하고, 껐다 켜는 비용이 더 큽니다. " +
                 "풀·나무 접기는 이 값과 무관하게 항상 동작합니다.")]
        public bool cullTerrainSurface = false;

        /// <summary>
        /// 한 프레임에 나무·풀을 새로 켤 수 있는 타일의 최대 수입니다.
        ///
        /// <b>왜 필요한가.</b> 시야를 빠르게 돌리면 프러스텀이 타일을 쓸고 지나가면서
        /// 여러 장이 <b>같은 프레임에</b> 조건을 만족합니다. 타일 하나의 나무·풀을 켜는 일은
        /// 유니티가 그 타일의 렌더 데이터를 다시 짜는 것이라 싸지 않은데, 그것이 한꺼번에
        /// 몰리면 그 프레임이 통째로 늘어집니다. 화면을 돌릴 때만 끊기는 이유가 이것입니다.
        ///
        /// <see cref="Gameplay.WorldStreamer"/> 가 거리 기반 스트리밍에서 쓰는 것과 같은 정책입니다.
        /// <b>켜는 것만 나누고, 끄는 것은 즉시 합니다.</b> 끄는 일은 싸기 때문입니다.
        ///
        /// 타일 자체(지면)는 이 예산과 무관하게 <b>즉시</b> 켜집니다.
        /// 지면이 늦으면 화면에 구멍이 보이지만, 풀이 한두 프레임 늦는 것은 거의 보이지 않습니다.
        /// </summary>
        [Tooltip("한 프레임에 나무·풀을 새로 켤 수 있는 타일 수. " +
                 "시야를 빠르게 돌릴 때 생기는 끊김을 여러 프레임으로 흩어 줍니다. " +
                 "지면 자체는 이 값과 무관하게 즉시 켜집니다.")]
        [Range(1, 8)]
        public int maxFoliageActivationsPerFrame = 2;

        /// <summary>
        /// 켜는 기준과 끄는 기준 사이에 두는 간격(m)입니다.
        ///
        /// 두 기준이 같으면 경계에 걸친 타일이 시야가 미세하게 흔들릴 때마다
        /// <b>껐다 켜기를 반복</b>합니다. 그 전환이 곧 비용이라 제자리에서도 끊깁니다.
        /// 켤 때보다 끌 때를 더 멀리 잡아 그 떨림을 없앱니다.
        /// </summary>
        [Tooltip("켜는 기준과 끄는 기준 사이의 간격(m). 경계에 걸친 타일이 껐다 켜기를 반복하는 것을 막습니다.")]
        [Range(0f, 60f)]
        public float cullingHysteresis = 20f;

        /// <summary>청크 컬링을 켤지 여부입니다.</summary>
        [Tooltip("끄면 모든 타일을 늘 그립니다. 문제를 가릴 때 써 보세요.")]
        public bool chunkCulling = true;

        // --- Public Properties ---

        /// <summary>
        /// 어디서나 쓸 수 있는 설정입니다. 없으면 기본값이 든 것을 하나 만들어 돌려줍니다.
        ///
        /// 없을 때 예외를 던지지 않는 것이 중요합니다. 설정 에셋을 아직 안 만든 상태에서도
        /// 게임은 돌아가야 합니다.
        /// </summary>
        public static CarDriveWorldSettings Instance
        {
            get
            {
                if (cached != null) return cached;

                cached = Resources.Load<CarDriveWorldSettings>(ResourceName);
                if (cached == null) cached = CreateInstance<CarDriveWorldSettings>();

                return cached;
            }
        }

        // --- Private Member Variables ---

        /// <summary>찾아 둔 설정입니다.</summary>
        private static CarDriveWorldSettings cached;

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
