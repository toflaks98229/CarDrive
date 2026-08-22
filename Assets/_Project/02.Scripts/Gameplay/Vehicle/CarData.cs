using UnityEngine;
using System.Collections.Generic;

namespace CarDrive.Gameplay
{
    /// <summary>
    /// 자동차의 핵심 성능 데이터를 담는 ScriptableObject 클래스입니다.
    /// 이 데이터를 통해 다양한 종류의 차량 성능을 에셋 파일로 관리할 수 있습니다.
    /// (ScriptableObject는 주로 데이터 컨테이너로 사용되므로, 변수 선언만 있습니다.)
    /// </summary>
    [CreateAssetMenu(fileName = "New Car Data", menuName = "Car/Car Data")] // Assets/Create/Car/Car Data 메뉴를 통해 생성 가능
    public class CarData : ScriptableObject
    {
        [Header("엔진/브레이크 설정")]
        [Tooltip("엔진의 기본적인 최대 토크(힘). 이 값은 토크 곡선(torqueCurve)에 의해 조절됩니다.")]
        public float motorTorque = 2000f;

        [Tooltip("브레이크의 최대 제동 토크")]
        public float brakeTorque = 3000f;

        [Tooltip("최대 후진 속도 (km/h). 이 속도에 도달하면 후진 가속이 멈춥니다.")]
        public float maxReverseSpeed = 20f;

        [Tooltip("스로틀을 놓고 굴러갈 때 걸리는 제동 토크입니다. 클수록 빨리 잦아듭니다.")]
        public float engineBrakeTorque = 50f;

        [Tooltip("이 속도(km/h) 아래에서는 엔진 브레이크를 걸지 않습니다. " +
                 "거의 멈춘 차를 계속 붙잡아 덜컹거리는 것을 막습니다.")]
        public float engineBrakeMinSpeed = 1f;

        [Header("조향 설정")]
        [Tooltip("바퀴의 최대 조향 각도 (단위: 도)")]
        public float maxSteerAngle = 30f;

        [Tooltip("핸들링 반응 속도. 값이 클수록 핸들이 빨리 복원되거나 꺾입니다.")]
        public float steerSpeed = 5f;

        [Tooltip("고속 주행 시 조향 각도를 줄여 안정성을 높이는 값. 0(효과 없음) ~ 1(최대 효과)")]
        [Range(0, 1)]
        public float steerHelper = 0.8f;

        [Tooltip("조향 억제를 계산할 기준 속도 (km/h). 이 속도에서 steerHelper 가 최대로 작용합니다. " +
                 "낮출수록 더 낮은 속도부터 핸들이 무거워집니다.")]
        public float steerReferenceSpeed = 100f;

        [Tooltip("고속에서도 남겨 둘 최소 조향각 (단위: 도). " +
                 "0에 가깝게 두면 고속에서 핸들이 아예 돌지 않습니다.")]
        public float minSteerAngle = 10f;

        [Header("엔진 및 기어 상세 설정")]
        [Tooltip("RPM에 따른 엔진 토크 곡선. X축: RPM 비율(0~1), Y축: 토크 배율(0~1)")]
        public AnimationCurve torqueCurve;

        [Tooltip("엔진의 아이들(최소) RPM. 시동이 켜져 있을 때의 기본 RPM입니다.")]
        public float idleRPM = 800f;

        [Tooltip("엔진의 최대 RPM. 이 값을 넘으면 엔진이 손상되거나 컷오프됩니다.")]
        public float maxRPM = 6000f;

        [Tooltip("기어를 다음 단으로 올리는 RPM 임계값")]
        public float shiftUpRPM = 4500f;

        [Tooltip("기어를 이전 단으로 내리는 RPM 임계값")]
        public float shiftDownRPM = 2000f;

        [Tooltip("이 속도(km/h) 아래에서만 후진·중립으로 바뀝니다. " +
                 "달리는 중에 스로틀을 놓았다고 기어가 빠지지 않도록 막는 문턱입니다.")]
        public float gearChangeSpeed = 5f;

        [Tooltip("중립·후진에서 스로틀을 끝까지 밟았을 때 공회전 위로 더 오르는 RPM 폭입니다. " +
                 "정차 중 공회전 소리와 계기판 바늘이 이 값으로 움직입니다.")]
        public float neutralRpmSpan = 1500f;

        [Tooltip("시동이 꺼진 뒤 RPM 이 0 으로 잦아드는 속도입니다. 클수록 빨리 멎습니다.")]
        public float engineStopRpmDecay = 2f;

        [Tooltip("기어비 설정 (1단, 2단...). 값이 높을수록 토크가 세지고 엔진 RPM이 빨리 오릅니다. " +
                 "낮을수록 같은 속도에서 RPM이 낮아 고속에 유리합니다. 앞에서부터 큰 값으로 두세요.")]
        public List<float> gearRatios = new List<float> { 4.0f, 2.5f };

        [Tooltip("기어비를 정규화할 기준값입니다. motorTorque는 '기어비가 이 값일 때 바퀴에 걸리는 토크'를 뜻합니다.\n\n" +
                 "0으로 두면 1단 기어비를 씁니다. 즉 motorTorque가 1단 토크가 되고 위 단수로 갈수록 줄어듭니다. " +
                 "대부분 이대로 두면 됩니다.\n\n" +
                 "1로 두면 기어비가 그대로 곱해집니다. 이때는 motorTorque를 엔진 토크 수준(수백 단위)으로 " +
                 "낮춰야 합니다. 그러지 않으면 토크가 기어비만큼 몇 배로 뜁니다.")]
        public float referenceGearRatio = 0f;

        [Header("연료 설정")]
        [Tooltip("최대 연료량 (리터 또는 임의의 단위)")]
        public float maxFuel = 50.0f;

        [Tooltip("연료 소모율. RPM과 엔진 부하(토크 사용량)에 비례하여 소모됩니다.")]
        public float fuelConsumptionRate = 0.005f;

        [Tooltip("스로틀을 밟지 않아도 공회전만으로 나가는 몫입니다. " +
                 "0.1이면 연료 소모율의 10%가 시동이 걸려 있는 동안 항상 나갑니다.")]
        [Range(0f, 1f)]
        public float idleFuelPortion = 0.1f;
    }
}
