using UnityEngine;

namespace CarDrive.EditorTools.ProcGen
{
    /// <summary>
    /// <see cref="MountainTool"/> 이 <b>무엇을 얼마나 올렸는지</b> 적어 두는 쪽지입니다.
    ///
    /// 산은 이미 구워진 높이맵에 값을 <b>더해서</b> 만듭니다.
    /// 그냥 두면 도구를 두 번 실행했을 때 산이 두 배로 솟습니다.
    ///
    /// 그런데 산의 높이는 <b>월드 좌표만 넣으면 똑같이 나오는 함수</b>입니다.
    /// 그래서 어떤 값으로 올렸는지만 적어 두면, 다음에 그 함수를 다시 계산해
    /// <b>다시 빼낼 수</b> 있습니다. 원본 높이맵을 통째로 복사해 둘 필요가 없습니다.
    ///
    /// 다만 완전히 무손실은 아닙니다. 터레인 높이맵은 16비트 정수로 저장되므로
    /// 올렸다 내릴 때마다 반올림이 두 번 일어납니다. 실측 잔차는 <b>2.1mm</b> 입니다.
    /// (70m 높이 범위에서 2/65535) 눈에 보이지도, 주행에 영향을 주지도 않습니다.
    ///
    /// 이 쪽지는 <c>03.DataAssets/Terrain/Generated</c> 에 놓입니다.
    /// 터레인을 다시 구우면 산이 없는 상태로 돌아가므로, 그때는 쪽지도 지워야 합니다.
    /// </summary>
    public class MountainStamp : ScriptableObject
    {
        // --- Fields : 상태 ---

        /// <summary>지금 터레인에 산이 올라가 있는지입니다.</summary>
        public bool applied = false;

        // --- Fields : 모양 ---

        /// <summary>난수 씨앗. 같은 값이면 같은 산맥이 나옵니다.</summary>
        public int seed = 71104;

        /// <summary>
        /// 산이 솟는 높이입니다. 터레인 높이 범위에 대한 비율(0~1)입니다.
        ///
        /// 터레인 전체 높이가 70m 이므로 0.5 는 약 35m 입니다.
        /// 그 위로는 올릴 수 없습니다. 기준 높이 0.28 에 자연 기복 0.15 가 얹히므로
        /// 0.5 를 더하면 이미 0.93 입니다. 더 키우면 <b>천장에 부딪혀 봉우리가 잘립니다.</b>
        ///
        /// 진짜 산 높이가 필요하면 터레인 굽기의 "최대 높이(m)"를 올려야 합니다.
        /// 다만 그러면 자연 기복과 도로 기복도 같은 배로 커지므로 둘을 함께 줄여야 합니다.
        /// </summary>
        public float amplitude = 0.5f;

        /// <summary>
        /// 산맥이 어디에 생길지 정하는 주기입니다. 작을수록 산맥 하나가 넓어집니다.
        /// 0.0015 는 대략 660m 짜리 덩어리입니다.
        /// </summary>
        public float rangeFrequency = 0.0015f;

        /// <summary>
        /// 이 값보다 산맥 노이즈가 높은 곳에만 산이 섭니다. 높이면 산이 드물어집니다.
        ///
        /// 절반쯤에 두어 <b>탁 트인 벌판과 산자락이 번갈아</b> 나오게 합니다.
        /// 온 길에 산이 서 있으면 800m 를 달리는 내내 같은 풍경입니다.
        ///
        /// 산이 없는 구간에서 세상 끝이 보이지는 않습니다. 안개가 340m 에서 끝나는데
        /// 터레인은 길 양옆으로 300m 씩 깔려 있어, 끊기는 자리는 이미 안개 속입니다.
        /// </summary>
        public float rangeThreshold = 0.44f;

        /// <summary>산맥 가장자리가 평지로 녹아드는 폭입니다. 0이면 절벽처럼 끊깁니다.</summary>
        public float rangeFade = 0.26f;

        /// <summary>능선 자체의 주기입니다. 클수록 봉우리가 촘촘해집니다.</summary>
        public float ridgeFrequency = 0.0055f;

        /// <summary>능선 노이즈를 몇 겹 쌓을지입니다. 많을수록 잔주름이 늡니다.</summary>
        public int octaves = 4;

        /// <summary>겹마다 주기를 몇 배로 올릴지입니다.</summary>
        public float lacunarity = 2.1f;

        /// <summary>겹마다 세기를 몇 배로 줄일지입니다.</summary>
        public float gain = 0.45f;

        /// <summary>
        /// 좌표를 얼마나 휘게 할지(m)입니다.
        ///
        /// 0이면 능선이 격자를 따라 곧게 뻗어 노이즈 티가 납니다.
        /// 좌표 자체를 흔들어 넣으면 능선이 구불구불해집니다.
        /// </summary>
        public float warpStrength = 90f;

        /// <summary>좌표를 휠 때 쓰는 노이즈의 주기입니다.</summary>
        public float warpFrequency = 0.0016f;

        // --- Fields : 비워 둘 자리 ---

        /// <summary>
        /// 도로 중심에서 이 거리(m) 안쪽은 전혀 올리지 않습니다.
        ///
        /// 터레인을 구울 때 도로는 반폭 9m 로 깎이고 그 영향이 49m 까지 남습니다.
        /// 45m 부터 올리기 시작하면 두 힘이 겹치는 구간이 거의 없어 길가가 매끄럽습니다.
        /// </summary>
        public float roadClearance = 45f;

        /// <summary>
        /// 도로 옆에서 산 높이까지 올라가는 데 걸리는 거리(m)입니다.
        ///
        /// 45 + 180 = 225m 에서 최고 높이에 닿고, 터레인은 300m 까지 있습니다.
        /// 남는 75m 는 능선이 오르내리는 <b>산마루</b>가 됩니다.
        ///
        /// 이 값이 곧 산비탈의 완만함입니다. 35m 를 180m 에 걸쳐 오르니 평균 11도입니다.
        /// 짧게 잡으면 높이는 같은데 벽처럼 서서 산으로 보이지 않습니다.
        /// </summary>
        public float roadFade = 180f;

        /// <summary>마을 밖에서 산 높이까지 올라가는 데 걸리는 거리(m)입니다.</summary>
        public float villageFade = 120f;
    }
}
