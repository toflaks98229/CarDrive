using UnityEngine;
using System.Collections.Generic;

namespace CarDrive.Gameplay
{
    /// <summary>
    /// 차량 한 종류의 성능 수치를 담는 에셋입니다.
    ///
    /// <b>동력계의 튜닝 값이 전부 여기 모입니다.</b> <see cref="Powertrain"/>·<see cref="WheelDriveline"/>·
    /// <see cref="CarController"/>가 이 에셋을 읽어 갑니다. 코드에는 수치를 두지 않습니다.
    /// 그래야 경차와 트럭을 스크립트 수정 없이 에셋 두 개로 나눌 수 있습니다.
    ///
    /// <b>동작은 갖지 않습니다.</b> 순수한 수치 묶음이라 필드 선언만 있습니다.
    /// 값을 해석하는 규칙(기어비 정규화, 엔진 브레이크 문턱 등)은 읽어 가는 쪽에 있습니다.
    ///
    /// Project 창에서 우클릭 → Create → Car → Car Data 로 만듭니다.
    /// </summary>
    [CreateAssetMenu(fileName = "New Car Data", menuName = "Car/Car Data")] // Assets/Create/Car/Car Data 메뉴를 통해 생성 가능
    public class CarData : ScriptableObject
    {
        // --- 엔진/브레이크 설정 ---

        /// <summary>
        /// 바퀴에 걸 수 있는 최대 토크(N·m)입니다.
        ///
        /// 실제로 걸리는 값은 <see cref="torqueCurve"/>가 RPM에 따라 깎고,
        /// 기어비가 <see cref="referenceGearRatio"/> 기준으로 정규화해 곱합니다.
        /// </summary>
        [Header("엔진/브레이크 설정")]
        [Tooltip("엔진의 기본적인 최대 토크(힘). 이 값은 토크 곡선(torqueCurve)에 의해 조절됩니다.")]
        public float motorTorque = 2000f;

        /// <summary>브레이크를 끝까지 밟았을 때 바퀴에 걸리는 제동 토크(N·m)입니다.</summary>
        [Tooltip("브레이크의 최대 제동 토크")]
        public float brakeTorque = 3000f;

        /// <summary>
        /// 후진으로 낼 수 있는 최대 속도(km/h)입니다.
        ///
        /// 이 속도에 닿으면 후진 가속을 멈춥니다. 후진에는 기어가 한 단뿐이라
        /// 이 문턱이 없으면 전진과 같은 속도까지 밀려납니다.
        /// </summary>
        [Tooltip("최대 후진 속도 (km/h). 이 속도에 도달하면 후진 가속이 멈춥니다.")]
        public float maxReverseSpeed = 20f;

        /// <summary>
        /// 스로틀을 놓고 굴러갈 때 걸리는 제동 토크(N·m)입니다.
        ///
        /// 클수록 빨리 잦아듭니다. 이것이 0이면 스로틀을 놓아도 차가 거의 줄지 않아
        /// 얼음 위를 미끄러지는 느낌이 납니다.
        /// </summary>
        [Tooltip("스로틀을 놓고 굴러갈 때 걸리는 제동 토크입니다. 클수록 빨리 잦아듭니다.")]
        public float engineBrakeTorque = 50f;

        /// <summary>
        /// 엔진 브레이크를 걸기 시작하는 최저 속도(km/h)입니다.
        ///
        /// 이 아래에서는 걸지 않습니다. 거의 멈춘 차를 계속 붙잡으면
        /// 정지와 미끄러짐을 오가며 덜컹거립니다.
        /// </summary>
        [Tooltip("이 속도(km/h) 아래에서는 엔진 브레이크를 걸지 않습니다. " +
                 "거의 멈춘 차를 계속 붙잡아 덜컹거리는 것을 막습니다.")]
        public float engineBrakeMinSpeed = 1f;

        // --- 조향 설정 ---

        /// <summary>앞바퀴가 꺾일 수 있는 최대 각도(도)입니다. 저속에서 쓰이는 값입니다.</summary>
        [Header("조향 설정")]
        [Tooltip("바퀴의 최대 조향 각도 (단위: 도)")]
        public float maxSteerAngle = 30f;

        /// <summary>
        /// 조향각이 목표를 따라가는 속도입니다.
        ///
        /// 클수록 핸들이 빨리 꺾이고 빨리 돌아옵니다. 너무 크면 입력이 그대로 튀어
        /// 조향이 딱딱해집니다.
        /// </summary>
        [Tooltip("핸들링 반응 속도. 값이 클수록 핸들이 빨리 복원되거나 꺾입니다.")]
        public float steerSpeed = 5f;

        /// <summary>
        /// 고속에서 조향각을 얼마나 줄일지 정하는 비율입니다. 0이면 억제 없음, 1이면 최대입니다.
        ///
        /// 고속에서 최대 각도로 꺾으면 차가 그대로 뒤집힙니다. 속도가 오를수록
        /// 꺾을 수 있는 각을 좁혀 그것을 막습니다.
        /// </summary>
        [Tooltip("고속 주행 시 조향 각도를 줄여 안정성을 높이는 값. 0(효과 없음) ~ 1(최대 효과)")]
        [Range(0, 1)]
        public float steerHelper = 0.8f;

        /// <summary>
        /// <see cref="steerHelper"/>가 최대로 작용하는 기준 속도(km/h)입니다.
        ///
        /// 낮출수록 더 낮은 속도부터 핸들이 무거워집니다.
        /// </summary>
        [Tooltip("조향 억제를 계산할 기준 속도 (km/h). 이 속도에서 steerHelper 가 최대로 작용합니다. " +
                 "낮출수록 더 낮은 속도부터 핸들이 무거워집니다.")]
        public float steerReferenceSpeed = 100f;

        /// <summary>
        /// 억제가 최대로 걸려도 남겨 둘 조향각(도)입니다.
        ///
        /// 0에 가까우면 고속에서 핸들이 아예 돌지 않아 차선 변경조차 못 합니다.
        /// </summary>
        [Tooltip("고속에서도 남겨 둘 최소 조향각 (단위: 도). " +
                 "0에 가깝게 두면 고속에서 핸들이 아예 돌지 않습니다.")]
        public float minSteerAngle = 10f;

        // --- 엔진 및 기어 상세 설정 ---

        /// <summary>
        /// RPM에 따른 토크 배율 곡선입니다. X축은 RPM 비율(0~1), Y축은 토크 배율(0~1)입니다.
        ///
        /// <see cref="motorTorque"/>에 곱해집니다. 중간이 봉긋한 모양이라야
        /// 특정 RPM에서 힘이 붙는 느낌이 납니다.
        /// </summary>
        [Header("엔진 및 기어 상세 설정")]
        [Tooltip("RPM에 따른 엔진 토크 곡선. X축: RPM 비율(0~1), Y축: 토크 배율(0~1)")]
        public AnimationCurve torqueCurve;

        /// <summary>시동이 걸려 있을 때 유지되는 최저 RPM입니다.</summary>
        [Tooltip("엔진의 아이들(최소) RPM. 시동이 켜져 있을 때의 기본 RPM입니다.")]
        public float idleRPM = 800f;

        /// <summary>엔진이 낼 수 있는 최대 RPM입니다. 이 위로는 연료가 끊깁니다.</summary>
        [Tooltip("엔진의 최대 RPM. 이 값을 넘으면 엔진이 손상되거나 컷오프됩니다.")]
        public float maxRPM = 6000f;

        /// <summary>기어를 한 단 올리는 RPM 문턱입니다.</summary>
        [Tooltip("기어를 다음 단으로 올리는 RPM 임계값")]
        public float shiftUpRPM = 4500f;

        /// <summary>
        /// 기어를 한 단 내리는 RPM 문턱입니다.
        ///
        /// <see cref="shiftUpRPM"/>보다 충분히 낮아야 합니다. 두 값이 붙으면
        /// 변속 직후 바로 되돌아가 기어가 계속 오르내립니다.
        /// </summary>
        [Tooltip("기어를 이전 단으로 내리는 RPM 임계값")]
        public float shiftDownRPM = 2000f;

        /// <summary>
        /// 후진·중립으로 바꿀 수 있는 최고 속도(km/h)입니다.
        ///
        /// 달리는 중에 스로틀을 놓았다고 기어가 빠지면 안 되므로 문턱을 둡니다.
        /// </summary>
        [Tooltip("이 속도(km/h) 아래에서만 후진·중립으로 바뀝니다. " +
                 "달리는 중에 스로틀을 놓았다고 기어가 빠지지 않도록 막는 문턱입니다.")]
        public float gearChangeSpeed = 5f;

        /// <summary>
        /// 중립·후진에서 스로틀을 끝까지 밟았을 때 공회전 위로 더 오르는 RPM 폭입니다.
        ///
        /// 정차 중에는 바퀴 회전으로 RPM을 낼 수 없습니다. 이 값이 공회전 소리와
        /// 계기판 바늘을 대신 움직입니다.
        /// </summary>
        [Tooltip("중립·후진에서 스로틀을 끝까지 밟았을 때 공회전 위로 더 오르는 RPM 폭입니다. " +
                 "정차 중 공회전 소리와 계기판 바늘이 이 값으로 움직입니다.")]
        public float neutralRpmSpan = 1500f;

        /// <summary>시동이 꺼진 뒤 RPM이 0으로 잦아드는 속도입니다. 클수록 빨리 멎습니다.</summary>
        [Tooltip("시동이 꺼진 뒤 RPM 이 0 으로 잦아드는 속도입니다. 클수록 빨리 멎습니다.")]
        public float engineStopRpmDecay = 2f;

        /// <summary>
        /// 1단부터 차례로 늘어놓은 기어비입니다. 앞에서부터 큰 값으로 두세요.
        ///
        /// 값이 클수록 토크가 세지고 RPM이 빨리 오릅니다. 작을수록 같은 속도에서
        /// RPM이 낮아 고속에 유리합니다.
        /// </summary>
        [Tooltip("기어비 설정 (1단, 2단...). 값이 높을수록 토크가 세지고 엔진 RPM이 빨리 오릅니다. " +
                 "낮을수록 같은 속도에서 RPM이 낮아 고속에 유리합니다. 앞에서부터 큰 값으로 두세요.")]
        public List<float> gearRatios = new List<float> { 4.0f, 2.5f };

        /// <summary>
        /// 기어비를 정규화할 기준값입니다. <see cref="motorTorque"/>는 "기어비가 이 값일 때
        /// 바퀴에 걸리는 토크"를 뜻하게 됩니다.
        ///
        /// <b>0으로 두면</b> 1단 기어비를 씁니다. 즉 <see cref="motorTorque"/>가 1단 토크가 되고
        /// 위 단수로 갈수록 줄어듭니다. 대부분 이대로 두면 됩니다.
        ///
        /// <b>1로 두면</b> 기어비가 그대로 곱해집니다. 이때는 <see cref="motorTorque"/>를
        /// 엔진 토크 수준(수백 단위)으로 낮춰야 합니다. 그러지 않으면 토크가 기어비만큼
        /// 몇 배로 뜁니다.
        /// </summary>
        [Tooltip("기어비를 정규화할 기준값입니다. motorTorque는 '기어비가 이 값일 때 바퀴에 걸리는 토크'를 뜻합니다.\n\n" +
                 "0으로 두면 1단 기어비를 씁니다. 즉 motorTorque가 1단 토크가 되고 위 단수로 갈수록 줄어듭니다. " +
                 "대부분 이대로 두면 됩니다.\n\n" +
                 "1로 두면 기어비가 그대로 곱해집니다. 이때는 motorTorque를 엔진 토크 수준(수백 단위)으로 " +
                 "낮춰야 합니다. 그러지 않으면 토크가 기어비만큼 몇 배로 뜁니다.")]
        public float referenceGearRatio = 0f;

        // --- 연료 설정 ---

        /// <summary>연료통이 가득 찼을 때의 연료량입니다. 계기판의 최대 눈금으로도 쓰입니다.</summary>
        [Header("연료 설정")]
        [Tooltip("최대 연료량 (리터 또는 임의의 단위)")]
        public float maxFuel = 50.0f;

        /// <summary>초당 연료 소모율입니다. RPM과 엔진 부하(토크 사용량)에 비례해 늘어납니다.</summary>
        [Tooltip("연료 소모율. RPM과 엔진 부하(토크 사용량)에 비례하여 소모됩니다.")]
        public float fuelConsumptionRate = 0.005f;

        /// <summary>
        /// 스로틀을 밟지 않아도 공회전만으로 나가는 몫입니다.
        ///
        /// 0.1이면 <see cref="fuelConsumptionRate"/>의 10%가 시동이 걸려 있는 동안 항상 나갑니다.
        /// 세워 둔 채 시동만 켜 놔도 연료가 준다는 뜻입니다.
        /// </summary>
        [Tooltip("스로틀을 밟지 않아도 공회전만으로 나가는 몫입니다. " +
                 "0.1이면 연료 소모율의 10%가 시동이 걸려 있는 동안 항상 나갑니다.")]
        [Range(0f, 1f)]
        public float idleFuelPortion = 0.1f;
    }
}
