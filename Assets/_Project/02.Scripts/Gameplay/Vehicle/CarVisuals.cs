using UnityEngine;

namespace CarDrive.Gameplay
{
    /// <summary>
    /// 휠 콜라이더의 물리 상태를 눈에 보이는 휠 모델과 운전대에 옮겨 담습니다.
    ///
    /// <b>물리와 보이는 것은 서로 다른 물건입니다.</b> <see cref="WheelCollider"/>는 위치를
    /// 스스로 갖고 있을 뿐 아무것도 그리지 않습니다. 바퀴처럼 보이는 메시는 별도의
    /// Transform이고, 둘을 매 프레임 맞춰 주지 않으면 차체만 굴러가고 바퀴는 제자리에
    /// 붙어 있습니다.
    ///
    /// <b>여기서는 읽어서 옮기기만 합니다.</b> 힘을 걸거나 조향각을 정하지 않습니다.
    /// 그 판단은 <see cref="CarController"/>와 <see cref="WheelDriveline"/>에 있습니다.
    /// 그래서 이 컴포넌트를 꺼도 차는 그대로 달립니다. 바퀴만 안 돌 뿐입니다.
    ///
    /// <see cref="CarController"/>와 같은 GameObject에 두어야 합니다.
    /// </summary>
    public class CarVisuals : MonoBehaviour
    {
        // --- Public Member Variables ---

        /// <summary>앞 왼쪽 휠 콜라이더입니다. 물리 위치를 읽어 올 곳입니다.</summary>
        [Header("휠 콜라이더 (물리)")]
        public WheelCollider frontLeftWheelCollider;

        /// <summary>앞 오른쪽 휠 콜라이더입니다.</summary>
        public WheelCollider frontRightWheelCollider;

        /// <summary>뒤 왼쪽 휠 콜라이더입니다.</summary>
        public WheelCollider rearLeftWheelCollider;

        /// <summary>뒤 오른쪽 휠 콜라이더입니다.</summary>
        public WheelCollider rearRightWheelCollider;

        /// <summary>앞 왼쪽 바퀴 메시입니다. 콜라이더의 위치·회전이 이곳으로 옮겨집니다.</summary>
        [Header("휠 모델 (시각적)")]
        public Transform frontLeftWheelTransform;

        /// <summary>앞 오른쪽 바퀴 메시입니다.</summary>
        public Transform frontRightWheelTransform;

        /// <summary>뒤 왼쪽 바퀴 메시입니다.</summary>
        public Transform rearLeftWheelTransform;

        /// <summary>뒤 오른쪽 바퀴 메시입니다.</summary>
        public Transform rearRightWheelTransform;

        /// <summary>운전대 메시입니다. 비워 두면 운전대만 갱신을 건너뜁니다.</summary>
        [Header("시각적 요소 (운전대)")]
        public Transform steeringWheelTransform;

        /// <summary>
        /// 바퀴가 끝까지 꺾였을 때 운전대가 도는 각도(도)입니다.
        ///
        /// 실제 차의 운전대는 바퀴보다 몇 배 더 돕니다. 바퀴 각도를 그대로 쓰면
        /// 운전대가 거의 안 도는 것처럼 보여, 이 값으로 따로 부풀립니다.
        /// </summary>
        public float maxSteeringWheelAngle = 90f;

        // --- Private Member Variables ---

        /// <summary>
        /// 조향 비율을 낼 때 나눌 기준 각도입니다. <see cref="Initialize"/>로 받습니다.
        ///
        /// <see cref="CarData"/>를 직접 읽지 않고 받아 두는 쪽을 택했습니다.
        /// 이 컴포넌트가 알아야 할 것은 "지금 각도가 최대의 몇 할인가" 하나뿐입니다.
        /// </summary>
        private float maxSteerAngleFromData; // CarController가 설정해줄 값

        // --- Public Methods ---

        /// <summary>
        /// 조향 비율을 계산할 기준 각도를 받아 둡니다.
        ///
        /// <see cref="CarController"/>가 Start()에서 한 번 부릅니다.
        /// 이 값이 0으로 남아 있으면 운전대는 움직이지 않습니다.
        /// </summary>
        /// <param name="maxSteerAngle">이 차량이 낼 수 있는 최대 조향각(도)</param>
        public void Initialize(float maxSteerAngle)
        {
            maxSteerAngleFromData = maxSteerAngle;
        }

        /// <summary>
        /// 바퀴 네 개와 운전대를 지금 물리 상태에 맞춰 갱신합니다.
        ///
        /// <see cref="CarController"/>가 LateUpdate()에서 부릅니다. 물리 계산이 끝난 뒤라야
        /// 이번 프레임의 최종 위치를 읽을 수 있기 때문입니다.
        /// </summary>
        /// <param name="currentSteerAngle">지금 앞바퀴가 꺾여 있는 각도(도)</param>
        public void UpdateVisuals(float currentSteerAngle)
        {
            UpdateSingleWheel(frontLeftWheelCollider, frontLeftWheelTransform);
            UpdateSingleWheel(frontRightWheelCollider, frontRightWheelTransform);
            UpdateSingleWheel(rearLeftWheelCollider, rearLeftWheelTransform);
            UpdateSingleWheel(rearRightWheelCollider, rearRightWheelTransform);
            UpdateSteeringWheel(currentSteerAngle);
        }

        // --- Private Methods ---

        /// <summary>
        /// 휠 콜라이더 하나의 월드 위치·회전을 바퀴 메시에 옮깁니다.
        ///
        /// 메시가 비어 있으면 조용히 넘어갑니다. 바퀴 모델을 아직 안 붙인 차량도
        /// 물리는 정상으로 굴러가야 하기 때문입니다.
        /// </summary>
        /// <param name="wheelCollider">위치를 읽어 올 휠 콜라이더</param>
        /// <param name="wheelTransform">위치를 적용할 바퀴 메시. null이면 아무것도 하지 않습니다</param>
        private void UpdateSingleWheel(WheelCollider wheelCollider, Transform wheelTransform)
        {
            if (wheelTransform == null) return;

            Vector3 pos;
            Quaternion rot;
            wheelCollider.GetWorldPose(out pos, out rot);
            wheelTransform.position = pos;
            wheelTransform.rotation = rot;
        }

        /// <summary>
        /// 지금 조향각을 운전대 회전으로 옮깁니다.
        ///
        /// 바퀴 각도를 최대 각도로 나눠 비율을 내고, 그 비율만큼
        /// <see cref="maxSteeringWheelAngle"/>을 돌립니다.
        /// 기준 각도가 0이면 나눌 수 없으므로 건너뜁니다.
        /// </summary>
        /// <param name="currentSteerAngle">지금 앞바퀴가 꺾여 있는 각도(도)</param>
        private void UpdateSteeringWheel(float currentSteerAngle)
        {
            if (steeringWheelTransform != null && maxSteerAngleFromData != 0)
            {
                float steerRatio = currentSteerAngle / maxSteerAngleFromData;
                float steeringWheelAngle = steerRatio * maxSteeringWheelAngle;
                steeringWheelTransform.localRotation = Quaternion.Euler(0, steeringWheelAngle, 0);
            }
        }
    }
}
