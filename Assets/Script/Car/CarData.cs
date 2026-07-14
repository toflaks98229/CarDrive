using UnityEngine;
using System.Collections.Generic;

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

    [Header("조향 설정")]
    [Tooltip("바퀴의 최대 조향 각도 (단위: 도)")]
    public float maxSteerAngle = 30f;

    [Tooltip("핸들링 반응 속도. 값이 클수록 핸들이 빨리 복원되거나 꺾입니다.")]
    public float steerSpeed = 5f;

    [Tooltip("고속 주행 시 조향 각도를 줄여 안정성을 높이는 값. 0(효과 없음) ~ 1(최대 효과)")]
    [Range(0, 1)]
    public float steerHelper = 0.8f;

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

    [Tooltip("기어비 설정 (1단, 2단...). 값이 낮을수록 고속에 유리하고, 높을수록 초반 가속에 유리합니다.")]
    public List<float> gearRatios = new List<float> { 4.0f, 2.5f };

    [Header("연료 설정")]
    [Tooltip("최대 연료량 (리터 또는 임의의 단위)")]
    public float maxFuel = 50.0f;

    [Tooltip("연료 소모율. RPM과 엔진 부하(토크 사용량)에 비례하여 소모됩니다.")]
    public float fuelConsumptionRate = 0.005f;
}
