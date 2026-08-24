using UnityEngine;
using CarDrive.Common;
using CarDrive.Gameplay;

namespace CarDrive.EditorTools
{
    /// <summary>다리 하나의 치수입니다. 전부 리그를 굽는 데만 쓰입니다.</summary>
    public struct WalkerLegSpec
    {
        /// <summary>다리 오브젝트의 이름입니다.</summary>
        public string Name;

        /// <summary>다리가 몸통에 붙는 자리입니다. (몸통 로컬)</summary>
        public Vector3 Hip;

        /// <summary>발이 서고 싶은 자리입니다. (루트 로컬, Y는 0)</summary>
        public Vector3 Home;

        /// <summary>
        /// 무릎이 접히는 방향입니다. (루트 로컬)
        /// <b>0이면 발 자리에서 바깥 위로 자동 계산</b>합니다. 옆으로 벌어진 다리의 기본입니다.
        /// 뒤로 꺾이는 다리(드레드노트)는 여기에 뒤쪽을 넣습니다.
        /// </summary>
        public Vector3 KneePole;

        /// <summary>넓적마디(몸통 → 무릎)의 길이입니다.</summary>
        public float Femur;

        /// <summary>종아리마디(무릎 → 발목)의 길이입니다.</summary>
        public float Tibia;

        /// <summary>발마디(발목 → 발끝)의 길이입니다.</summary>
        public float Tarsus;

        /// <summary>마디의 굵기 셋입니다. 순서대로 넓적 · 종아리 · 발입니다.</summary>
        public Vector3 Thickness;

        /// <summary>발마디가 바깥으로 눕는 정도입니다. 0이면 수직 말뚝입니다.</summary>
        public float AnkleOutward;

        /// <summary>
        /// 몸통과 고관절을 잇는 <b>받침대</b>의 크기입니다. 0이면 만들지 않습니다.
        ///
        /// 고관절을 몸통 밖으로 빼면 그 사이가 비어 다리가 공중에 떠 보입니다. 받침대가 그 틈을 메웁니다.
        /// <b>콜라이더는 붙이지 않습니다.</b> 몸통의 충돌 모양은 상자 하나로 두어야
        /// 다리와의 여유를 계산대로 보장할 수 있습니다.
        /// </summary>
        public Vector3 MountSize;

        /// <summary>받침대의 중심입니다. (몸통 로컬)</summary>
        public Vector3 MountCenter;
    }

    /// <summary>로봇 한 종류를 통째로 적어 둔 설계도입니다.</summary>
    public struct WalkerTemplate
    {
        /// <summary>프리팹과 오브젝트의 이름입니다.</summary>
        public string Name;

        /// <summary>무엇을 흉내 낸 것인지 한 줄 설명입니다.</summary>
        public string Description;

        /// <summary>발 평면에서 몸통 중심까지의 높이입니다.</summary>
        public float StandHeight;

        /// <summary>서 있을 때 몸통이 앞뒤로 기우는 각도(도). 양수가 앞으로 숙임</summary>
        public float StandPitch;

        /// <summary>몸통 상자의 크기입니다.</summary>
        public Vector3 BodySize;

        /// <summary>몸통 상자의 중심입니다. (몸통 로컬)</summary>
        public Vector3 BodyOffset;

        /// <summary>리지드바디 질량입니다.</summary>
        public float Mass;

        /// <summary>내고 싶은 속도입니다. 실제로는 다리가 감당하는 만큼으로 묶입니다.</summary>
        public float CruiseSpeed;

        /// <summary>도는 최대 속도(도/초)입니다.</summary>
        public float TurnRate;

        /// <summary>목표에 이만큼 다가가면 멈춥니다.</summary>
        public float ArriveRadius;

        /// <summary>발이 들리는 높이입니다.</summary>
        public float StepHeight;

        /// <summary>걸음 시간의 상한과 하한입니다.</summary>
        public Vector2 StepDurationRange;

        /// <summary>작업 반경을 얼마나 쓸지와, 그 몇 할에서 걸음을 시작할지입니다.</summary>
        public Vector2 StrideUsageAndTrigger;

        /// <summary>몸통의 위아래 흔들림 · 선회 기울임 · 가감속 기울임입니다.</summary>
        public Vector3 BodySway;

        /// <summary>손으로 고른 보행입니다.</summary>
        public WalkerGaitType Gait;

        /// <summary>속도에 따라 보행을 바꿀지입니다.</summary>
        public bool AutoGait;

        /// <summary>이 속도부터 교대보로 바꿉니다.</summary>
        public float AlternateSpeed;

        /// <summary>몸통 위치가 발을 따라가는 방식입니다.</summary>
        public SecondOrderSettings PositionSpring;

        /// <summary>몸통 회전이 발 평면을 따라가는 방식입니다.</summary>
        public SecondOrderSettings RotationSpring;

        /// <summary>충격을 받았을 때 휘청이는 방식입니다. 느리고 덜 감쇠할수록 오래 휘청입니다.</summary>
        public SecondOrderSettings ImpactSpring;

        /// <summary>충격 1m/s 당 최대 기울기(도)와, 아무리 세게 맞아도 넘지 않는 각도입니다.</summary>
        public Vector2 ImpactTilt;

        /// <summary>
        /// 넘어짐 수치 셋입니다. 순서대로 <b>넘어지는 문턱</b>(속도 변화 m/s) ·
        /// <b>일어나는 데 걸리는 시간</b>(초) · <b>누워 뜸을 들이는 시간</b>(초)입니다.
        /// </summary>
        public Vector3 Knockdown;

        /// <summary>다리들입니다. <b>앞에서 뒤로, 각 줄마다 왼쪽 · 오른쪽</b> 순서여야 합니다.</summary>
        public WalkerLegSpec[] Legs;
    }

    /// <summary>
    /// 로봇 보행 형태를 <b>데이터로</b> 적어 둔 표입니다.
    ///
    /// <b>왜 표인가.</b> 다리 수 · 관절 방향 · 몸통 비율만 다를 뿐 조립 과정은 셋 다 똑같습니다.
    /// 종류마다 빌더를 따로 쓰면 같은 코드가 세 벌이 되고, 넷째를 추가할 때 또 한 벌이 됩니다.
    /// 여기에 한 항목을 더하면 <see cref="WalkerRobotSetup"/> 이 그대로 구워 냅니다.
    ///
    /// <b>세 종류를 고른 이유.</b> 보행 로봇의 설계 공간에서 서로 가장 먼 세 점입니다.
    /// <code>
    ///                  다리   관절이 꺾이는 쪽      무게중심   걸음
    /// 4족 보행기        4     바깥 위 (벌어짐)      낮음       교대보 (대각선)
    /// 2족 드레드노트    2     뒤 (역관절)           높음       교대보 (좌우)
    /// 3족 스트라이더    3     바깥 위, 아주 높이    아주 높음   파도보 (한 발씩)
    /// </code>
    /// 셋이 같은 코드로 걸으면 그 사이의 어떤 것도 됩니다. (6족은 표에 한 줄만 더하면 삼각보로 걷습니다)
    /// </summary>
    public static class WalkerTemplates
    {
        // --- Public Methods ---

        /// <summary>모든 설계도를 돌려줍니다.</summary>
        /// <returns>설계도 배열</returns>
        public static WalkerTemplate[] All()
        {
            return new[] { Quadruped(), Dreadnought(), Strider() };
        }

        /// <summary>
        /// <b>기초 4족 보행기.</b> 다리가 옆으로 벌어졌다가 꺾여 내려오고, 몸이 낮게 깔립니다.
        /// 지지 면적이 넓어 가장 안정합니다. 대각선 두 다리가 함께 나가는 교대보입니다.
        /// </summary>
        /// <returns>설계도</returns>
        public static WalkerTemplate Quadruped()
        {
            const float standHeight = 0.42f;
            const float footX = 0.85f;
            const float footZ = 0.55f;
            // 고관절은 몸통 상자(반폭 0.275) <b>바깥</b>에 둡니다. 관절구 반지름 0.055 를 더하고
            // 몸통이 25도까지 기울어도 0.05m 의 여유가 남습니다.
            const float hipX = 0.4f;
            const float hipZ = 0.3f;

            return new WalkerTemplate
            {
                Name = "WalkerRobot_Quadruped",
                Description = "기초 4족 보행기. 낮고 넓게 벌어진 다리, 대각선 교대보.",
                StandHeight = standHeight,
                BodySize = new Vector3(0.55f, 0.22f, 0.95f),
                BodyOffset = Vector3.zero,
                Mass = 320f,
                CruiseSpeed = 1.6f,
                TurnRate = 130f,
                ArriveRadius = 1.2f,
                StepHeight = 0.3f,
                StepDurationRange = new Vector2(0.28f, 0.1f),
                StrideUsageAndTrigger = new Vector2(0.7f, 0.5f),
                BodySway = new Vector3(0.5f, 7f, 6f),
                Gait = WalkerGaitType.Alternate,
                AutoGait = true,
                AlternateSpeed = 0.9f,
                PositionSpring = new SecondOrderSettings(3.4f, 0.8f, 0.35f),
                RotationSpring = new SecondOrderSettings(2.8f, 0.7f, 0.6f),
                ImpactSpring = new SecondOrderSettings(2.4f, 0.35f, 0f),
                ImpactTilt = new Vector2(3f, 25f),
                // 가볍고 낮아 잘 넘어지지만 금방 일어납니다.
                Knockdown = new Vector3(3.5f, 1.3f, 0.6f),
                Legs = new[]
                {
                    SprawledLeg("Leg_FrontLeft", -1f, 1f, hipX, 0.02f, hipZ, footX, footZ, 0.6f, 0.7f, 0.2f,
                        new Vector3(0.085f, 0.06f, 0.038f)),
                    SprawledLeg("Leg_FrontRight", 1f, 1f, hipX, 0.02f, hipZ, footX, footZ, 0.6f, 0.7f, 0.2f,
                        new Vector3(0.085f, 0.06f, 0.038f)),
                    SprawledLeg("Leg_BackLeft", -1f, -1f, hipX, 0.02f, hipZ, footX, footZ, 0.6f, 0.7f, 0.2f,
                        new Vector3(0.085f, 0.06f, 0.038f)),
                    SprawledLeg("Leg_BackRight", 1f, -1f, hipX, 0.02f, hipZ, footX, footZ, 0.6f, 0.7f, 0.2f,
                        new Vector3(0.085f, 0.06f, 0.038f))
                }
            };
        }

        /// <summary>
        /// <b>2족 드레드노트.</b> 워해머 40K 의 걷는 관처럼, 두꺼운 몸통을 짧고 굵은 <b>역관절</b> 다리가 받칩니다.
        ///
        /// 무릎이 <b>뒤로</b> 꺾이는 것이 이 형태의 전부입니다. (닭다리 · 새다리) 사람 무릎처럼 앞으로 꺾으면
        /// 같은 리그가 곧바로 사람처럼 보입니다. 무겁게 보이도록 걸음을 길게 잡고 발을 높이 듭니다.
        /// 다리가 둘뿐이라 한 발을 들면 지지점이 하나가 되고, 그때 몸이 <b>버티는 발 위로 쏠립니다.</b>
        /// 따로 만든 연출이 아니라 발 평균에서 저절로 나오는 움직임입니다.
        /// </summary>
        /// <returns>설계도</returns>
        public static WalkerTemplate Dreadnought()
        {
            // 무릎이 뒤로, 살짝 위로 꺾입니다. 이 벡터 하나가 역관절의 전부입니다.
            Vector3 backwardKnee = new Vector3(0f, 0.35f, -1f).normalized;

            return new WalkerTemplate
            {
                Name = "WalkerRobot_Dreadnought",
                Description = "2족 역관절 보행기. 무겁고 느린 걸음, 뒤로 꺾이는 무릎.",
                StandHeight = 2.2f,
                BodySize = new Vector3(1.7f, 1.7f, 1.3f),
                BodyOffset = new Vector3(0f, 0.35f, 0f),
                Mass = 900f,
                CruiseSpeed = 1.4f,
                TurnRate = 70f,
                ArriveRadius = 2.2f,
                StepHeight = 0.5f,
                StepDurationRange = new Vector2(0.6f, 0.25f),
                StrideUsageAndTrigger = new Vector2(0.5f, 0.55f),
                BodySway = new Vector3(1f, 4f, 5f),
                Gait = WalkerGaitType.Alternate,
                AutoGait = false,
                AlternateSpeed = 0.5f,
                PositionSpring = new SecondOrderSettings(1.8f, 0.9f, 0.2f),
                RotationSpring = new SecondOrderSettings(1.4f, 0.85f, 0.3f),
                // 무겁고 위가 큰 몸이라 한 번 흔들리면 오래 갑니다.
                ImpactSpring = new SecondOrderSettings(1.3f, 0.25f, 0f),
                ImpactTilt = new Vector2(2.4f, 22f),
                // 무거워 웬만해서는 안 넘어지지만, 한 번 넘어지면 일어나는 데 오래 걸립니다.
                Knockdown = new Vector3(5f, 2.2f, 1f),
                Legs = new[]
                {
                    new WalkerLegSpec
                    {
                        Name = "Leg_Left",
                        // 몸통 상자 <b>아래</b>로 내립니다. (상자 밑면이 몸통 로컬 −0.50)
                        // 기울면 관절이 상자 모서리 쪽으로 휩쓸리므로, 22도까지 견디도록 넉넉히 내립니다.
                        Hip = new Vector3(-0.6f, -1.15f, 0f),
                        MountSize = new Vector3(0.45f, 0.65f, 0.5f),
                        MountCenter = new Vector3(-0.6f, -0.83f, 0f),
                        Home = new Vector3(-0.68f, 0f, 0f),
                        KneePole = backwardKnee,
                        Femur = 0.95f, Tibia = 1.05f, Tarsus = 0.35f,
                        Thickness = new Vector3(0.34f, 0.28f, 0.22f),
                        AnkleOutward = 0.12f
                    },
                    new WalkerLegSpec
                    {
                        Name = "Leg_Right",
                        Hip = new Vector3(0.6f, -1.15f, 0f),
                        MountSize = new Vector3(0.45f, 0.65f, 0.5f),
                        MountCenter = new Vector3(0.6f, -0.83f, 0f),
                        Home = new Vector3(0.68f, 0f, 0f),
                        KneePole = backwardKnee,
                        Femur = 0.95f, Tibia = 1.05f, Tarsus = 0.35f,
                        Thickness = new Vector3(0.34f, 0.28f, 0.22f),
                        AnkleOutward = 0.12f
                    }
                }
            };
        }

        /// <summary>
        /// <b>3족 스트라이더.</b> 하프라이프 2 의 그것처럼, 몸통이 아주 높이 매달리고
        /// 가느다란 다리 셋이 <b>무릎을 몸통보다 훨씬 위로</b> 들어 올리며 한 발씩 옮깁니다.
        ///
        /// <b>무릎이 몸통보다 높이 솟는 것</b>은 넓적마디를 짧게, 종아리마디를 길게 잡아서 나옵니다.
        /// 둘이 비슷하면 무릎이 몸통 높이에서 멈춥니다. 무릎이 솟으려면 밑동에서 무릎으로 가는 각이
        /// <b>발까지의 기울기보다 커야</b> 하는데, 그 각은 두 마디의 길이 비가 정합니다.
        ///
        /// 다리가 홀수라 절반으로 나눌 수 없습니다. 그래서 <b>파도보</b>(한 발씩)만 가능하고,
        /// 항상 두 발이 땅에 남아 셋 중 어느 것을 들어도 넘어지지 않습니다.
        /// 다리를 아주 높이 들고 보폭이 커서 걸음 하나하나가 눈에 들어옵니다.
        /// </summary>
        /// <returns>설계도</returns>
        /// <remarks>
        /// <b>서 있는 높이와 다리 길이는 함께 정해야 합니다.</b> 몸통이 다리 뻗기에 가까워질수록
        /// 남는 <b>수평</b> 여유가 급격히 사라집니다. 뻗기 R 에 높이 h 면 쓸 수 있는 수평은
        /// √(R² − h²) 이라, h 가 R 의 96% 가 되면 28% 만 남습니다.
        ///
        /// 실제로 높이를 6.2 로 올려 본 적이 있는데, 작업 반경이 <b>0.22m</b> 로 무너져
        /// 13cm 만 움직여도 걸음이 새로 시작됐습니다. 그때마다 발이 1.2m 씩 들리니
        /// <b>초당 다섯 걸음으로 제자리에서 통통 튀면서 0.43m/s 로 기어갔습니다.</b>
        /// 오류도 경고도 나지 않았습니다 — 속도는 <see cref="WalkerRobot.MaxTravelSpeed"/> 로
        /// 조용히 묶이기 때문입니다. (지금은 <see cref="RobotDriver"/> 가 경고합니다)
        ///
        /// 높이 5.2 · 다리 7.8m 이면 반경 3.3m 로, 순항 2.2m/s 를 걸음 1.6회로 소화합니다.
        /// </remarks>
        public static WalkerTemplate Strider()
        {
            Vector3 thin = new Vector3(0.18f, 0.13f, 0.09f);

            return new WalkerTemplate
            {
                Name = "WalkerRobot_Strider",
                Description = "3족 고공 보행기. 아주 높은 몸통, 가느다란 다리, 한 발씩 옮기는 파도보.",
                StandHeight = 5.2f,
                BodySize = new Vector3(1.2f, 0.9f, 2.4f),
                BodyOffset = Vector3.zero,
                Mass = 2000f,
                CruiseSpeed = 2.2f,
                TurnRate = 55f,
                ArriveRadius = 3.5f,
                StepHeight = 1.2f,
                StepDurationRange = new Vector2(0.7f, 0.2f),
                StrideUsageAndTrigger = new Vector2(0.6f, 0.5f),
                BodySway = new Vector3(0.35f, 3f, 4f),
                Gait = WalkerGaitType.Wave,
                AutoGait = false,
                AlternateSpeed = 1f,
                PositionSpring = new SecondOrderSettings(1.5f, 0.95f, 0.15f),
                RotationSpring = new SecondOrderSettings(1.2f, 0.9f, 0.25f),
                // 아주 크고 높아 각도는 작아도 끝이 크게 움직입니다. 대신 아주 느리게 흔들립니다.
                ImpactSpring = new SecondOrderSettings(0.9f, 0.22f, 0f),
                ImpactTilt = new Vector2(2f, 18f),
                // 2톤이라 어지간한 충돌로는 꿈쩍도 하지 않습니다. 대신 한 번 쓰러지면 한참 걸립니다.
                Knockdown = new Vector3(7f, 3.5f, 1.6f),
                Legs = new[]
                {
                    new WalkerLegSpec
                    {
                        Name = "Leg_FrontLeft",
                        // 선체(반폭 0.60 · 반길이 1.20) 바깥에 답니다.
                        Hip = new Vector3(-0.85f, 0.1f, 0.6f),
                        MountSize = new Vector3(0.46f, 0.26f, 0.3f),
                        MountCenter = new Vector3(-0.62f, 0.1f, 0.6f),
                        Home = new Vector3(-2.1f, 0f, 1.5f),
                        KneePole = Vector3.zero,
                        Femur = 2.9f, Tibia = 4.9f, Tarsus = 0.5f,
                        Thickness = thin,
                        AnkleOutward = 0.5f
                    },
                    new WalkerLegSpec
                    {
                        Name = "Leg_FrontRight",
                        Hip = new Vector3(0.85f, 0.1f, 0.6f),
                        MountSize = new Vector3(0.46f, 0.26f, 0.3f),
                        MountCenter = new Vector3(0.62f, 0.1f, 0.6f),
                        Home = new Vector3(2.1f, 0f, 1.5f),
                        KneePole = Vector3.zero,
                        Femur = 2.9f, Tibia = 4.9f, Tarsus = 0.5f,
                        Thickness = thin,
                        AnkleOutward = 0.5f
                    },
                    new WalkerLegSpec
                    {
                        Name = "Leg_Rear",
                        Hip = new Vector3(0f, 0.1f, -1.5f),
                        MountSize = new Vector3(0.3f, 0.26f, 0.46f),
                        MountCenter = new Vector3(0f, 0.1f, -1.3f),
                        Home = new Vector3(0f, 0f, -2.4f),
                        KneePole = Vector3.zero,
                        Femur = 2.9f, Tibia = 4.9f, Tarsus = 0.5f,
                        Thickness = thin,
                        AnkleOutward = 0.5f
                    }
                }
            };
        }

        // --- Private Methods ---

        /// <summary>옆으로 벌어진 다리 하나를 만듭니다. 좌우·앞뒤 부호만 다르므로 여기서 찍어 냅니다.</summary>
        /// <param name="name">다리 이름</param>
        /// <param name="side">왼쪽이면 −1, 오른쪽이면 1</param>
        /// <param name="front">앞다리면 1, 뒷다리면 −1</param>
        /// <param name="hipX">붙는 자리의 좌우 거리</param>
        /// <param name="hipY">붙는 자리의 높이</param>
        /// <param name="hipZ">붙는 자리의 앞뒤 거리</param>
        /// <param name="footX">발 자리의 좌우 거리</param>
        /// <param name="footZ">발 자리의 앞뒤 거리</param>
        /// <param name="femur">넓적마디 길이</param>
        /// <param name="tibia">종아리마디 길이</param>
        /// <param name="tarsus">발마디 길이</param>
        /// <param name="thickness">마디 굵기 셋</param>
        /// <returns>다리 설계도</returns>
        private static WalkerLegSpec SprawledLeg(string name, float side, float front,
            float hipX, float hipY, float hipZ, float footX, float footZ,
            float femur, float tibia, float tarsus, Vector3 thickness)
        {
            return new WalkerLegSpec
            {
                Name = name,
                Hip = new Vector3(hipX * side, hipY, hipZ * front),
                Home = new Vector3(footX * side, 0f, footZ * front),
                KneePole = Vector3.zero,
                Femur = femur,
                Tibia = tibia,
                Tarsus = tarsus,
                Thickness = thickness,
                AnkleOutward = 0.45f,

                // 몸통 옆면에서 고관절까지를 메우는 받침대입니다.
                MountSize = new Vector3(Mathf.Abs(hipX) * 0.55f, 0.12f, 0.2f),
                MountCenter = new Vector3(hipX * side * 0.72f, hipY, hipZ * front)
            };
        }
    }
}
