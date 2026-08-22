namespace CarDrive.Common
{
    /// <summary>
    /// 날씨가 <b>노면과 차량에</b> 주는 영향입니다. 차량 부품이 봅니다.
    ///
    /// <b>왜 날씨 전체가 아니라 이것만인가.</b> <c>Powertrain</c>은 구름이 얼마나 꼈는지,
    /// 별이 몇 개 가려졌는지 알 필요가 없습니다. 필요한 것은 연료를 얼마나 더 먹는가뿐입니다.
    /// 계약을 이만큼으로 좁혀 두면, 날씨에 새 수치가 생겨도 차량 코드는 다시 컴파일되지 않고,
    /// 테스트는 float 두 개짜리 가짜 하나로 주행 계산을 검증할 수 있습니다.
    /// </summary>
    public interface IRoadConditions
    {
        /// <summary>노면 미끄러움입니다. 1이면 마른 노면, 클수록 미끄럽습니다.</summary>
        float Slipperiness { get; }

        /// <summary>연료 소모 배율입니다. 1이면 평소, 맞바람·젖은 노면에서 커집니다.</summary>
        float FuelConsumptionMultiplier { get; }
    }

    /// <summary>
    /// 지금 귀신이 얼마나 활발한지입니다. 스포너가 봅니다.
    ///
    /// <b>밤이 이미 반영된 값입니다.</b> 예전 <c>WeatherSystem.GetGhostActivity()</c>가
    /// 날씨와 시간대를 함께 곱해 돌려주던 것과 같습니다 — 폭우 ×1.9 에 밤 ×1.5.
    /// 스포너가 시계와 날씨를 각각 알 필요가 없도록 곱셈은 날씨 쪽에 남겨 둡니다.
    /// </summary>
    public interface IGhostActivity
    {
        /// <summary>활동량 배율입니다. 1이면 평소, 클수록 자주 나타납니다.</summary>
        float Activity { get; }
    }

    /// <summary>
    /// 하늘 아래 서 있는 <b>몸이 받는</b> 영향입니다. 도보 상태의 부품들이 봅니다.
    ///
    /// 비를 맞으면 씻기고, 하늘을 보면 빗물을 마실 수 있고, 궂은 날에는 잠이 얕습니다.
    /// 셋 다 "밖에 있는 사람에게 날씨가 하는 일"이라 한 계약으로 묶었습니다.
    /// </summary>
    public interface IExposureConditions
    {
        /// <summary>비의 세기입니다. 0이면 비가 오지 않습니다.</summary>
        float RainIntensity { get; }

        /// <summary>수면 회복 배율입니다. 1이면 평소, 궂은 날에는 작아집니다.</summary>
        float SleepQuality { get; }

        /// <summary>
        /// 밖에 서 있을 때 초당 받는 영향들을 한 번에 돌려줍니다.
        ///
        /// <b>네 값을 따로 묻지 않는 이유가 있습니다.</b> 넷은 같은 프레임의 같은 날씨에서
        /// 나와야 합니다. 따로 물으면 그 사이에 날씨가 바뀔 수 있고, 실제로 전환 중에는
        /// 매 프레임 값이 움직입니다.
        /// </summary>
        /// <param name="hygieneChange">초당 더러움 변화. 음수면 씻깁니다.</param>
        /// <param name="stress">초당 오르는 스트레스</param>
        /// <param name="thirstRelief">초당 줄어드는 갈증</param>
        /// <param name="stressRelief">초당 줄어드는 스트레스</param>
        void GetExposureRates(out float hygieneChange, out float stress,
                              out float thirstRelief, out float stressRelief);
    }

    /// <summary>
    /// 하늘의 겉모습입니다. 하늘·구름 그림자 표현이 봅니다.
    ///
    /// 이 계약을 보는 쪽은 전부 <b>그리기만</b> 합니다. 시뮬레이션에 되먹임하지 않습니다.
    /// </summary>
    public interface ISkyConditions
    {
        /// <summary>
        /// 구름이 하늘을 덮은 정도(0~1)입니다.
        ///
        /// <b>부르는 쪽이 기본값을 넘깁니다.</b> 날씨가 없을 때 무엇으로 볼지가
        /// 소비자마다 다르기 때문입니다 — 별 가림은 0(가리지 않음)이지만,
        /// 구름 그림자는 인스펙터에 적어 둔 값으로 그림자를 계속 흘려보냅니다.
        /// </summary>
        /// <param name="fallback">날씨가 없을 때 쓸 값</param>
        /// <returns>날씨가 있으면 실제 구름량, 없으면 <paramref name="fallback"/></returns>
        float GetCloudCover(float fallback);

        /// <summary>바람 세기입니다. 구름이 흐르는 속도에 쓰입니다.</summary>
        float WindStrength { get; }

        /// <summary>날씨로 인한 어둡기(0~1)입니다. 1이면 한낮에도 캄캄합니다.</summary>
        float Darkness { get; }
    }

    /// <summary>
    /// 날씨가 없을 때 그 자리를 채우는 <b>아무 일도 하지 않는 날씨</b>입니다.
    ///
    /// 예전 정적 접근자는 <c>Instance</c>가 없으면 "영향 없음"에 해당하는 값을 돌려주었습니다.
    /// 미끄러움과 연료·수면 배율은 1(평소), 비와 어둡기와 노출은 0입니다.
    /// 그 규약을 그대로 옮겼으므로, 날씨 시스템을 빼고 실행해도 예전과 똑같이 동작합니다.
    /// </summary>
    public sealed class NullWeather : IRoadConditions, IGhostActivity, IExposureConditions, ISkyConditions
    {
        /// <summary>모두가 공유하는 하나뿐인 인스턴스입니다. 상태가 없으므로 나눠 써도 안전합니다.</summary>
        public static readonly NullWeather Instance = new NullWeather();

        private NullWeather() { }

        /// <summary>날씨가 없으면 노면은 마른 상태입니다.</summary>
        public float Slipperiness { get { return 1f; } }

        /// <summary>날씨가 없으면 연료를 평소만큼 먹습니다.</summary>
        public float FuelConsumptionMultiplier { get { return 1f; } }

        /// <summary>날씨가 없으면 귀신은 평소만큼 나타납니다.</summary>
        public float Activity { get { return 1f; } }

        /// <summary>날씨가 없으면 비가 오지 않습니다.</summary>
        public float RainIntensity { get { return 0f; } }

        /// <summary>날씨가 없으면 잠은 평소만큼 회복됩니다.</summary>
        public float SleepQuality { get { return 1f; } }

        /// <summary>날씨가 없으면 밖에 서 있어도 아무 영향이 없습니다.</summary>
        /// <param name="hygieneChange">항상 0</param>
        /// <param name="stress">항상 0</param>
        /// <param name="thirstRelief">항상 0</param>
        /// <param name="stressRelief">항상 0</param>
        public void GetExposureRates(out float hygieneChange, out float stress,
                                     out float thirstRelief, out float stressRelief)
        {
            hygieneChange = 0f;
            stress = 0f;
            thirstRelief = 0f;
            stressRelief = 0f;
        }

        /// <summary>부르는 쪽이 넘긴 기본값을 그대로 돌려줍니다.</summary>
        /// <param name="fallback">소비자가 정한 기본 구름량</param>
        /// <returns><paramref name="fallback"/> 그대로</returns>
        public float GetCloudCover(float fallback) { return fallback; }

        /// <summary>날씨가 없으면 바람도 없습니다.</summary>
        public float WindStrength { get { return 0f; } }

        /// <summary>날씨가 없으면 하늘이 어두워지지 않습니다.</summary>
        public float Darkness { get { return 0f; } }
    }
}
