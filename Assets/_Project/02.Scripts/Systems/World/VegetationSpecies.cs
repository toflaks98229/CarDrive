using System.Collections.Generic;
using UnityEngine;

namespace CarDrive.Systems
{
    /// <summary>
    /// 지면에 심을 식생 한 종류입니다. 모양과 <b>어디에 심을지</b>를 함께 들고 있습니다.
    ///
    /// <b>왜 필요한가.</b> 예전에는 월드 전체가 풀 한 종류였습니다. 잎 수·높이·반경이
    /// 월드 설정에 하나씩만 있어서, 언덕이든 물가든 도로변이든 똑같은 포기가 심겼습니다.
    /// 지형은 넓은데 눈에 들어오는 것이 한 가지뿐이라 어디를 가도 같은 곳처럼 보였습니다.
    ///
    /// 메시는 이 값들로 <b>실행 중에 만들어집니다.</b> 새 아트가 필요 없습니다.
    /// 잎 하나가 삼각형 한 장이라, 잎 수를 줄인 종은 그만큼 싸집니다.
    /// </summary>
    [System.Serializable]
    public class VegetationSpecies
    {
        // --- 표시 ---

        /// <summary>구분하기 위한 이름입니다. 프리팹과 메시 파일 이름이 됩니다.</summary>
        [Tooltip("구분용 이름. 생성되는 메시·프리팹 파일 이름이 됩니다. 영문/숫자를 쓰세요.")]
        public string id = "Grass";

        // --- 모양 ---

        /// <summary>한 포기에 들어가는 잎의 수입니다. 삼각형 수와 같습니다.</summary>
        [Header("모양")]
        [Tooltip("한 포기의 잎 수. 잎 하나가 삼각형 한 장이라 이 값이 곧 비용입니다.")]
        [Range(2, 32)]
        public int bladesPerTuft = 18;

        /// <summary>포기가 퍼지는 반경(m)입니다.</summary>
        [Tooltip("한 포기가 퍼지는 반경(m)")]
        [Range(0.05f, 0.8f)]
        public float tuftRadius = 0.32f;

        /// <summary>잎 하나의 높이(m)입니다.</summary>
        [Tooltip("잎 높이(m)")]
        [Range(0.1f, 2f)]
        public float bladeHeight = 0.55f;

        /// <summary>잎 하나의 밑동 폭(m)입니다.</summary>
        [Tooltip("잎 밑동의 폭(m). 넓히면 두꺼운 잎, 좁히면 가는 풀이 됩니다.")]
        [Range(0.005f, 0.2f)]
        public float bladeWidth = 0.034f;

        /// <summary>
        /// 잎이 기우는 정도입니다. 0이면 곧게 서고, 크면 늘어집니다.
        /// </summary>
        [Tooltip("잎이 기우는 정도. 0이면 곧게 서고 크면 늘어집니다.")]
        [Range(0f, 0.8f)]
        public float lean = 0.2f;

        /// <summary>
        /// 이 종에 곱할 색입니다. 흰색이면 머티리얼 색 그대로입니다.
        ///
        /// 터레인 디테일의 <c>healthyColor</c> 로 들어가므로, 같은 머티리얼을 쓰면서도
        /// 종마다 다른 색조를 낼 수 있습니다.
        /// </summary>
        [Tooltip("이 종에 곱할 색. 흰색이면 머티리얼 색 그대로입니다.")]
        public Color tint = Color.white;

        /// <summary>모양을 흩뜨릴 씨앗입니다. 종마다 다르게 두면 서로 다른 포기가 됩니다.</summary>
        [Tooltip("모양 무작위 씨앗. 종마다 다르게 두면 서로 다른 포기가 나옵니다.")]
        public int seed = 20260818;

        // --- 어디에 심을지 ---

        /// <summary>
        /// 이 종이 차지하는 비중입니다. 여러 종의 비중을 합쳐 <see cref="CarDriveWorldSettings.maxPerCell"/>을 나눕니다.
        /// </summary>
        [Header("어디에 심을지")]
        [Tooltip("칸당 포기 수를 종끼리 나눌 때의 비중. 0이면 심지 않습니다.")]
        [Range(0f, 4f)]
        public float weight = 1f;

        /// <summary>이 각도(도)보다 완만해야 심습니다.</summary>
        [Tooltip("이 각도(도)보다 가팔라지면 심지 않습니다. 절벽에 풀이 붙는 것을 막습니다.")]
        [Range(0f, 90f)]
        public float maxSlope = 40f;

        /// <summary>이 각도(도)보다 가팔라야 심습니다. 비탈 전용 종에 씁니다.</summary>
        [Tooltip("이 각도(도)보다 완만하면 심지 않습니다. 비탈에만 나는 종에 씁니다.")]
        [Range(0f, 90f)]
        public float minSlope = 0f;

        /// <summary>이 높이(m, 월드 Y) 위로는 심지 않습니다.</summary>
        [Tooltip("이 높이(월드 Y) 위로는 심지 않습니다. 고지대에 다른 종을 두고 싶을 때 씁니다.")]
        public float maxHeight = 10000f;

        /// <summary>이 높이(m, 월드 Y) 아래로는 심지 않습니다.</summary>
        [Tooltip("이 높이(월드 Y) 아래로는 심지 않습니다.")]
        public float minHeight = -10000f;

        // --- 군집 ---

        /// <summary>
        /// 군집 무늬의 크기입니다. 작을수록 넓은 덩어리, 클수록 잘게 흩어집니다.
        ///
        /// <b>왜 군집이 필요한가.</b> 밀도를 스플랫 알파만으로 정하면 풀밭 전체가
        /// 고르게 덮여 <b>카펫처럼</b> 보입니다. 실제 들판은 빽빽한 덤불과 드문 자리가 섞여 있습니다.
        /// 노이즈로 군집을 만들면 <b>총 포기 수를 늘리지 않고도</b> 훨씬 풍성해 보입니다.
        /// </summary>
        [Header("군집")]
        [Tooltip("군집 무늬의 크기. 작을수록 넓은 덩어리가 됩니다.")]
        [Range(0.001f, 0.2f)]
        public float patchScale = 0.02f;

        /// <summary>
        /// 이 값보다 노이즈가 낮은 자리는 비웁니다. 0이면 군집 없이 고르게 심습니다.
        /// </summary>
        [Tooltip("이 값보다 노이즈가 낮으면 비웁니다. 0이면 군집 없이 고르게 심습니다.")]
        [Range(0f, 0.9f)]
        public float patchThreshold = 0.35f;

        /// <summary>군집 무늬를 종마다 어긋나게 하는 값입니다.</summary>
        [Tooltip("군집 무늬 위치를 어긋나게 하는 값. 종마다 다르게 두면 서로 겹치지 않습니다.")]
        public float patchOffset = 0f;
    }

    /// <summary>
    /// 식생 종을 하나도 적어 두지 않았을 때 쓸 기본 구성입니다.
    ///
    /// <see cref="NeedDefaults"/>·<see cref="CurrencyDefaults"/>와 같은 방식입니다.
    /// 설정을 비워 두어도 게임과 도구가 그대로 돌아가야 하기 때문입니다.
    /// </summary>
    public static class VegetationDefaults
    {
        /// <summary>
        /// 기본 세 종을 만듭니다. 예전의 단일 풀을 <b>주된 종</b>으로 두고,
        /// 키 작은 덤불과 마른 억새를 곁들여 같은 포기 수로 변화를 냅니다.
        /// </summary>
        /// <param name="legacyBlades">예전 설정의 잎 수. 주된 종이 이어받습니다.</param>
        /// <param name="legacyRadius">예전 설정의 포기 반경</param>
        /// <param name="legacyHeight">예전 설정의 잎 높이</param>
        /// <returns>기본 식생 목록</returns>
        public static List<VegetationSpecies> Create(int legacyBlades, float legacyRadius, float legacyHeight)
        {
            return new List<VegetationSpecies>
            {
                // 들판을 덮는 주된 풀. 예전 값을 그대로 물려받아 지금 모습이 유지됩니다.
                new VegetationSpecies
                {
                    id = "GrassTuft",
                    bladesPerTuft = legacyBlades,
                    tuftRadius = legacyRadius,
                    bladeHeight = legacyHeight,
                    bladeWidth = 0.034f,
                    lean = 0.2f,
                    tint = Color.white,
                    seed = 20260818,
                    weight = 1f,
                    maxSlope = 40f,
                    patchScale = 0.015f,
                    patchThreshold = 0.2f
                },

                // 낮고 촘촘한 덤불. 잎이 적어 싸고, 주된 풀 사이를 메웁니다.
                new VegetationSpecies
                {
                    id = "GrassShrub",
                    bladesPerTuft = Mathf.Max(4, legacyBlades / 2),
                    tuftRadius = legacyRadius * 0.7f,
                    bladeHeight = legacyHeight * 0.55f,
                    bladeWidth = 0.055f,
                    lean = 0.45f,
                    tint = new Color(0.82f, 0.90f, 0.78f),
                    seed = 771103,
                    weight = 0.55f,
                    maxSlope = 55f,
                    patchScale = 0.035f,
                    patchThreshold = 0.45f,
                    patchOffset = 137f
                },

                // 키 큰 마른 억새. 드물게 솟아 실루엣에 변화를 줍니다.
                new VegetationSpecies
                {
                    id = "GrassReed",
                    bladesPerTuft = Mathf.Max(3, legacyBlades / 3),
                    tuftRadius = legacyRadius * 0.5f,
                    bladeHeight = legacyHeight * 1.9f,
                    bladeWidth = 0.022f,
                    lean = 0.1f,
                    tint = new Color(0.95f, 0.88f, 0.62f),
                    seed = 480921,
                    weight = 0.25f,
                    maxSlope = 25f,
                    patchScale = 0.05f,
                    patchThreshold = 0.62f,
                    patchOffset = 913f
                }
            };
        }
    }
}
