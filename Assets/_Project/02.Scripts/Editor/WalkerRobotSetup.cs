using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using CarDrive.Common;
using CarDrive.Gameplay;

namespace CarDrive.EditorTools
{
    /// <summary>
    /// <see cref="WalkerTemplates"/> 의 설계도를 받아 <b>보행 로봇 프리팹</b>을 구워 냅니다.
    ///
    /// 종류가 몇이든 조립 과정은 하나입니다. 다리 수 · 관절 방향 · 몸통 비율만 데이터로 다릅니다.
    /// 그래서 넷째 형태를 추가할 때 여기를 고칠 일이 없습니다.
    ///
    /// <b>이 도구가 지키는 규약</b>이 하나 있습니다. 마디는 <b>로컬 +Z 가 다음 관절을 향합니다.</b>
    /// <see cref="TwoBoneIK"/> 가 그 규약으로 회전을 만들기 때문입니다. 원기둥 메시는 +Y 로 서 있으므로
    /// 자식에서 한 번 눕혀 맞춥니다.
    ///
    /// <b>물리를 함께 붙입니다.</b> 몸통에는 리지드바디 · 콜라이더 · <see cref="RobotPhysicsMotor"/>,
    /// 다리 마디마다 캡슐 콜라이더와 <b>기구학</b> 리지드바디입니다.
    /// 그래서 만들어진 로봇은 바위에 부딪히고, 차에 받히면 밀리고, 낭떠러지에서 떨어지고,
    /// <b>걸어가며 통을 걷어차고 넘어질 때 다리가 지형에 걸립니다.</b>
    ///
    /// <b>발이 자기 몸을 밟지 않도록</b> 지면 레이어를 <c>Ground</c> 와 <c>Prop</c> 으로 좁혀 둡니다.
    /// 로봇의 콜라이더는 <c>Default</c> 에 있으므로 탐침에 걸리지 않습니다.
    /// (예전처럼 모든 레이어를 보게 두면 <b>발이 자기 몸통을 땅으로 착각</b>합니다)
    ///
    /// <b>고관절은 몸통 상자 밖에 답니다.</b> 예전에는 관절이 상자 안에 박혀 있어서, 다리와 몸통의
    /// 충돌을 통째로 꺼야만 로봇이 제자리에서 떨지 않았습니다. 그것은 겹친 모양을 가린 것이지
    /// 고친 것이 아닙니다. 지금은 관절을 밖으로 빼고 그 사이를 <b>장식 받침대</b>로 메우므로,
    /// 다리와 몸통이 <b>실제로 부딪힐 수 있습니다.</b> (여유: 4족 5cm · 2족 23cm · 3족 8cm)
    /// </summary>
    public static class WalkerRobotSetup
    {
        // --- Constants ---

        /// <summary>프리팹이 놓일 폴더입니다.</summary>
        private const string PrefabFolder = "Assets/_Project/05.Prefabs/Robot";

        /// <summary>재질이 놓일 폴더입니다.</summary>
        private const string MaterialFolder = "Assets/_Project/04.Art/00.Materials";

        /// <summary>목표 마커를 로봇 앞 얼마에 놓을지입니다. 몸통 길이의 배수입니다.</summary>
        private const float MarkerDistance = 8f;

        /// <summary>여러 대를 한 번에 놓을 때의 간격입니다.</summary>
        private const float SpawnSpacing = 14f;

        // --- Public Methods : 메뉴 ---

        /// <summary>기초 4족 보행기를 만듭니다.</summary>
        [MenuItem("CarDrive/Gameplay/보행 로봇/1. 4족 보행기")]
        public static void BuildQuadruped()
        {
            BuildOne(WalkerTemplates.Quadruped());
        }

        /// <summary>2족 역관절 드레드노트를 만듭니다.</summary>
        [MenuItem("CarDrive/Gameplay/보행 로봇/2. 2족 드레드노트")]
        public static void BuildDreadnought()
        {
            BuildOne(WalkerTemplates.Dreadnought());
        }

        /// <summary>3족 고공 스트라이더를 만듭니다.</summary>
        [MenuItem("CarDrive/Gameplay/보행 로봇/3. 3족 스트라이더")]
        public static void BuildStrider()
        {
            BuildOne(WalkerTemplates.Strider());
        }

        /// <summary>세 종류를 모두 만들어 나란히 놓습니다.</summary>
        [MenuItem("CarDrive/Gameplay/보행 로봇/전부 만들기")]
        public static void BuildAll()
        {
            List<string> report = new List<string>();
            WalkerTemplate[] templates = WalkerTemplates.All();

            EnsureFolders();
            Material bodyMaterial = EnsureMaterial("RobotBody", new Color(0.18f, 0.19f, 0.22f), 0.75f, 0.5f, report);
            Material limbMaterial = EnsureMaterial("RobotLimb", new Color(0.34f, 0.36f, 0.4f), 0.9f, 0.7f, report);

            Vector3 origin = SceneOrigin();

            for (int i = 0; i < templates.Length; i++)
            {
                GameObject prefab = Bake(templates[i], bodyMaterial, limbMaterial, report);
                Place(prefab, templates[i], origin + Vector3.right * (SpawnSpacing * i), report);
            }

            Finish(report);
        }

        // --- Private Methods : 흐름 ---

        /// <summary>한 종류를 굽고 씬에 놓습니다.</summary>
        /// <param name="template">설계도</param>
        private static void BuildOne(WalkerTemplate template)
        {
            List<string> report = new List<string>();

            EnsureFolders();
            Material bodyMaterial = EnsureMaterial("RobotBody", new Color(0.18f, 0.19f, 0.22f), 0.75f, 0.5f, report);
            Material limbMaterial = EnsureMaterial("RobotLimb", new Color(0.34f, 0.36f, 0.4f), 0.9f, 0.7f, report);

            GameObject prefab = Bake(template, bodyMaterial, limbMaterial, report);
            Place(prefab, template, SceneOrigin(), report);

            Finish(report);
        }

        /// <summary>에셋을 저장하고 결과를 찍습니다.</summary>
        /// <param name="report">진행 내용</param>
        private static void Finish(List<string> report)
        {
            AssetDatabase.SaveAssets();

            Debug.Log("WalkerRobotSetup:" + System.Environment.NewLine +
                      string.Join(System.Environment.NewLine, report));
        }

        // --- Private Methods : 조립 ---

        /// <summary>설계도 하나를 프리팹으로 굽습니다.</summary>
        /// <param name="template">설계도</param>
        /// <param name="bodyMaterial">몸통 재질</param>
        /// <param name="limbMaterial">다리 재질</param>
        /// <param name="report">진행 내용</param>
        /// <returns>구워진 프리팹</returns>
        private static GameObject Bake(WalkerTemplate template, Material bodyMaterial, Material limbMaterial,
            List<string> report)
        {
            GameObject root = new GameObject(template.Name);

            Transform bodyTransform = CreateChild(root.transform, "Body", new Vector3(0f, template.StandHeight, 0f));
            bodyTransform.localRotation = Quaternion.Euler(template.StandPitch, 0f, 0f);
            AddBox(bodyTransform, "Chassis", template.BodyOffset, template.BodySize, bodyMaterial);

            WalkerLeg[] legs = new WalkerLeg[template.Legs.Length];
            for (int i = 0; i < template.Legs.Length; i++)
            {
                legs[i] = BuildLeg(bodyTransform, template, template.Legs[i], limbMaterial);
            }

            WireComponents(root, bodyTransform, legs, template);

            string path = PrefabFolder + "/" + template.Name + ".prefab";
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);

            report.Add("· " + template.Description);
            report.Add("  프리팹: " + path);

            return prefab;
        }

        /// <summary>컴포넌트를 붙이고 설계도의 수치를 옮겨 담습니다.</summary>
        /// <param name="root">로봇 루트</param>
        /// <param name="bodyTransform">몸통</param>
        /// <param name="legs">다리들</param>
        /// <param name="template">설계도</param>
        private static void WireComponents(GameObject root, Transform bodyTransform, WalkerLeg[] legs,
            WalkerTemplate template)
        {
            WalkerRobot robot = root.AddComponent<WalkerRobot>();
            robot.body = bodyTransform;
            robot.legs = legs;
            robot.standHeight = template.StandHeight;
            robot.standPitch = template.StandPitch;
            robot.groundMask = GroundMask();
            robot.stepHeight = template.StepHeight;
            robot.stepDuration = template.StepDurationRange.x;
            robot.minStepDuration = template.StepDurationRange.y;
            robot.strideUsage = template.StrideUsageAndTrigger.x;
            robot.stepTriggerFraction = template.StrideUsageAndTrigger.y;
            robot.bodyBob = template.BodySway.x;
            robot.leanIntoTurn = template.BodySway.y;
            robot.pitchIntoAccel = template.BodySway.z;
            robot.gait = template.Gait;
            robot.autoGait = template.AutoGait;
            robot.alternateSpeed = template.AlternateSpeed;
            robot.bodyPositionSpring = template.PositionSpring;
            robot.bodyRotationSpring = template.RotationSpring;
            robot.impactSpring = template.ImpactSpring;
            robot.impactTiltPerSpeed = template.ImpactTilt.x;
            robot.impactMaxTilt = template.ImpactTilt.y;

            // 발이 오를 수 있는 턱과 내려설 수 있는 깊이는 다리 길이에 비례합니다.
            robot.groundProbeUp = template.StandHeight * 0.6f;
            robot.groundProbeDown = template.StandHeight * 2f;

            RobotDriver driver = root.AddComponent<RobotDriver>();
            driver.cruiseSpeed = template.CruiseSpeed;
            driver.turnRate = template.TurnRate;
            driver.arriveRadius = template.ArriveRadius;
            driver.slowRadius = template.ArriveRadius * 2.5f;
            driver.groundMask = GroundMask();

            AddPhysics(root, template);
        }

        /// <summary>
        /// 리지드바디 · 몸통 콜라이더 · 물리 모터를 붙입니다.
        ///
        /// 콜라이더는 <b>몸통 자리</b>에 둡니다. 루트는 지면에 있고 몸통은 그 위에 떠 있으므로,
        /// 콜라이더를 루트 원점에 두면 땅에 박힙니다. 다리에는 콜라이더를 두지 않습니다.
        /// 다리는 계산으로 움직이므로 물리로 밀 대상이 아니고, 있으면 <b>발이 자기 다리를 밟습니다.</b>
        /// </summary>
        /// <param name="root">로봇 루트</param>
        /// <param name="template">설계도</param>
        private static void AddPhysics(GameObject root, WalkerTemplate template)
        {
            BoxCollider collider = root.AddComponent<BoxCollider>();
            collider.center = new Vector3(0f, template.StandHeight, 0f) + template.BodyOffset;
            collider.size = template.BodySize;

            Rigidbody body = root.AddComponent<Rigidbody>();
            body.mass = template.Mass;
            body.useGravity = true;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.Continuous;
            body.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;

            RobotPhysicsMotor motor = root.AddComponent<RobotPhysicsMotor>();

            // 무거울수록 다리를 단단하게, 발밑을 깊게 봅니다.
            motor.fallThreshold = template.StandHeight * 0.4f;
            motor.stepUpTolerance = template.StandHeight * 0.5f;

            RobotKnockdown knockdown = root.AddComponent<RobotKnockdown>();
            knockdown.knockdownThreshold = template.Knockdown.x;
            knockdown.riseDuration = template.Knockdown.y;
            knockdown.downedTime = template.Knockdown.z;
        }

        /// <summary>
        /// 다리 하나를 만들고 <b>미리 접어</b> 둡니다.
        ///
        /// 곧게 편 채로 저장하면 프리팹이 탁자처럼 보이고, 무릎이 어느 쪽으로 꺾이는지도
        /// 눈으로 확인할 수 없습니다. 그래서 런타임과 <b>같은 <see cref="TwoBoneIK"/></b> 로
        /// 한 번 풀어 그 자세를 그대로 굽습니다. 무릎 방향과 발마디 각도를 뽑는 식도
        /// <see cref="WalkerLeg"/> 와 같아야 프리팹과 첫 프레임이 어긋나지 않습니다.
        /// </summary>
        /// <param name="body">몸통</param>
        /// <param name="template">설계도</param>
        /// <param name="spec">다리 설계도</param>
        /// <param name="material">다리 재질</param>
        /// <returns>만들어진 다리 컴포넌트</returns>
        private static WalkerLeg BuildLeg(Transform body, WalkerTemplate template, WalkerLegSpec spec, Material material)
        {
            // 마디는 몸통의 자식이므로 여기 계산은 전부 <b>몸통 기준</b>입니다. 몸통이 기울어 있으면
            // 루트 기준으로 적어 둔 값(발자리·위쪽·무릎 방향)을 그만큼 <b>되돌려</b> 넣어야
            // 실제로 그 자리에 섭니다. 그러지 않으면 기운 만큼 발이 앞뒤로 밀립니다.
            Quaternion unpitch = Quaternion.Inverse(Quaternion.Euler(template.StandPitch, 0f, 0f));

            Vector3 up = unpitch * Vector3.up;

            Vector3 hip = spec.Hip;
            Vector3 foot = unpitch * new Vector3(spec.Home.x, -template.StandHeight, spec.Home.z);

            Vector3 flat = new Vector3(spec.Home.x, 0f, spec.Home.z);
            Vector3 outward = unpitch * (flat.sqrMagnitude > 1e-8f ? flat.normalized : Vector3.right);

            Vector3 pole = spec.KneePole.sqrMagnitude > 1e-8f
                ? (unpitch * spec.KneePole).normalized
                : (outward * 0.55f + up * 0.83f).normalized;

            // 평지에서 지면 법선은 위쪽입니다. 런타임의 AnkleDirection 과 같은 식입니다.
            Vector3 ankleDirection = (up * (1f - spec.AnkleOutward) + outward * spec.AnkleOutward).normalized;
            Vector3 ankle = foot + ankleDirection * spec.Tarsus;

            TwoBoneIK.Solution pose = TwoBoneIK.Solve(hip, ankle, spec.Femur, spec.Tibia, pole);

            Quaternion femurRotation = Quaternion.LookRotation(pose.Joint - hip, pole);
            Quaternion tibiaRotation = Quaternion.LookRotation(pose.End - pose.Joint, pole);

            Vector3 legPlane = Vector3.Cross(pose.End - pose.Joint, pole);
            Quaternion tarsusRotation = Quaternion.LookRotation(foot - pose.End, legPlane);

            // 몸통과 고관절 사이를 메우는 받침대입니다. 장식이라 콜라이더를 붙이지 않습니다.
            if (spec.MountSize.sqrMagnitude > 1e-6f)
            {
                AddBox(body, spec.Name + "_Mount", spec.MountCenter, spec.MountSize, material);
            }

            Transform legRoot = CreateChild(body, spec.Name, hip);

            Transform femur = CreateChild(legRoot, "Femur", Vector3.zero);
            femur.localRotation = femurRotation;
            AddCylinderAlongZ(femur, "FemurMesh", spec.Femur, spec.Thickness.x, material);
            AddLimbBody(femur, spec.Femur, spec.Thickness.x);

            Transform tibia = CreateChild(femur, "Tibia", new Vector3(0f, 0f, spec.Femur));
            tibia.localRotation = Quaternion.Inverse(femurRotation) * tibiaRotation;
            AddCylinderAlongZ(tibia, "TibiaMesh", spec.Tibia, spec.Thickness.y, material);
            AddLimbBody(tibia, spec.Tibia, spec.Thickness.y);

            Transform tarsus = CreateChild(tibia, "Tarsus", new Vector3(0f, 0f, spec.Tibia));
            tarsus.localRotation = Quaternion.Inverse(tibiaRotation) * tarsusRotation;
            AddCylinderAlongZ(tarsus, "TarsusMesh", spec.Tarsus, spec.Thickness.z, material);
            AddLimbBody(tarsus, spec.Tarsus, spec.Thickness.z);

            // 관절을 구로 덮어 마디 사이가 벌어져 보이지 않게 합니다.
            AddSphere(femur, "HipJoint", Vector3.zero, spec.Thickness.x * 1.3f, material);
            AddSphere(femur, "KneeJoint", new Vector3(0f, 0f, spec.Femur), spec.Thickness.y * 1.4f, material);

            WalkerLeg leg = legRoot.gameObject.AddComponent<WalkerLeg>();
            leg.upperBone = femur;
            leg.lowerBone = tibia;
            leg.ankleBone = tarsus;
            leg.upperLength = spec.Femur;
            leg.lowerLength = spec.Tibia;
            leg.ankleLength = spec.Tarsus;
            leg.ankleOutward = spec.AnkleOutward;
            leg.homeOffset = spec.Home;
            leg.kneePole = spec.KneePole;

            return leg;
        }

        // --- Private Methods : 씬 ---

        /// <summary>씬뷰가 보고 있는 자리의 지면입니다.</summary>
        /// <returns>지면 위의 한 점</returns>
        private static Vector3 SceneOrigin()
        {
            Vector3 spawn = Vector3.zero;

            SceneView view = SceneView.lastActiveSceneView;
            if (view != null) spawn = view.pivot;

            GroundProbe.Invalidate();

            return GroundProbe.Sample(spawn, 60f, 400f, ~0, out Vector3 point, out Vector3 _) ? point : spawn;
        }

        /// <summary>로봇 하나와 목표 마커를 씬에 놓습니다.</summary>
        /// <param name="prefab">놓을 프리팹</param>
        /// <param name="template">설계도</param>
        /// <param name="around">놓을 자리</param>
        /// <param name="report">진행 내용</param>
        private static void Place(GameObject prefab, WalkerTemplate template, Vector3 around, List<string> report)
        {
            if (prefab == null) return;

            Vector3 spawn = GroundProbe.Sample(around, 60f, 400f, ~0, out Vector3 point, out Vector3 _) ? point : around;

            GameObject instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
            if (instance == null) return;

            instance.transform.position = spawn;
            Undo.RegisterCreatedObjectUndo(instance, "보행 로봇 세우기");

            RobotDriver driver = instance.GetComponent<RobotDriver>();
            if (driver != null)
            {
                GameObject marker = new GameObject(template.Name + "_Destination");
                Vector3 markerPosition = spawn + instance.transform.forward * MarkerDistance;

                if (GroundProbe.Sample(markerPosition, 60f, 400f, ~0, out Vector3 markerPoint, out Vector3 _))
                {
                    markerPosition = markerPoint;
                }

                marker.transform.position = markerPosition;
                driver.destinationTarget = marker.transform;

                Undo.RegisterCreatedObjectUndo(marker, "보행 로봇 목표 마커");
            }

            Selection.activeGameObject = instance;
            EditorSceneManager.MarkSceneDirty(instance.scene);

            report.Add("  씬에 놓았습니다: " + spawn.ToString("F1") + " (목표 마커를 끌어 옮기면 걸어갑니다)");
        }

        /// <summary>
        /// 마디를 <b>물리적으로 존재하게</b> 만듭니다. 캡슐 콜라이더와 기구학 리지드바디입니다.
        ///
        /// <b>왜 리지드바디가 필요한가.</b> 콜라이더만 붙이면 유니티는 그것을 <b>정적 콜라이더</b>로 봅니다.
        /// 정적 콜라이더를 매 프레임 옮기면 물리 엔진이 공간 분할을 통째로 다시 만들고,
        /// 그러고도 부딪힌 쪽에 제대로 된 힘을 주지 못합니다. 움직이는 콜라이더에는
        /// <b>반드시 리지드바디가 있어야</b> 합니다.
        ///
        /// <b>왜 기구학인가.</b> 다리는 IK 가 각도를 정합니다. 물리가 다리를 끌고 가면 그 각도가
        /// 무의미해집니다. 기구학으로 두면 <b>다리가 세계를 밀되 세계가 다리를 밀지는 못합니다.</b>
        /// 걸어가며 통을 걷어차고, 넘어질 때 지형에 걸립니다.
        /// (다리가 밀리기까지 하려면 마디마다 관절을 걸어야 하는데, 그때부터는 균형 제어기가 필요합니다)
        ///
        /// 자기 몸끼리 부딪히는 문제는 <see cref="WalkerRobot"/> 이 시작할 때 꺼 줍니다.
        /// </summary>
        /// <param name="bone">마디</param>
        /// <param name="length">마디 길이</param>
        /// <param name="thickness">마디 지름</param>
        private static void AddLimbBody(Transform bone, float length, float thickness)
        {
            CapsuleCollider capsule = bone.gameObject.AddComponent<CapsuleCollider>();

            // 마디는 로컬 +Z 를 따라 뻗어 있습니다. (0=X, 1=Y, 2=Z)
            capsule.direction = 2;
            capsule.center = new Vector3(0f, 0f, length * 0.5f);
            capsule.height = length;
            capsule.radius = thickness * 0.5f;

            Rigidbody body = bone.gameObject.AddComponent<Rigidbody>();

            body.isKinematic = true;
            body.useGravity = false;

            // 기구학 몸이 쓸 수 있는 유일한 연속 검사입니다. 빠르게 휘두르는 다리가 얇은 것을 뚫지 않게 합니다.
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        }

        // --- Private Methods : 도형 ---

        /// <summary>빈 자식 오브젝트를 만듭니다.</summary>
        /// <param name="parent">부모</param>
        /// <param name="name">이름</param>
        /// <param name="localPosition">부모 기준 위치</param>
        /// <returns>만들어진 트랜스폼</returns>
        private static Transform CreateChild(Transform parent, string name, Vector3 localPosition)
        {
            GameObject child = new GameObject(name);

            child.transform.SetParent(parent, false);
            child.transform.localPosition = localPosition;

            return child.transform;
        }

        /// <summary>상자 메시를 붙입니다.</summary>
        /// <param name="parent">부모</param>
        /// <param name="name">이름</param>
        /// <param name="localPosition">부모 기준 위치</param>
        /// <param name="size">크기</param>
        /// <param name="material">재질</param>
        private static void AddBox(Transform parent, string name, Vector3 localPosition, Vector3 size, Material material)
        {
            GameObject box = CreateMeshObject(name, RobotMeshLibrary.Box(size), material);

            box.transform.SetParent(parent, false);
            box.transform.localPosition = localPosition;
        }

        /// <summary>구 메시를 붙입니다.</summary>
        /// <param name="parent">부모</param>
        /// <param name="name">이름</param>
        /// <param name="localPosition">부모 기준 위치</param>
        /// <param name="diameter">지름</param>
        /// <param name="material">재질</param>
        private static void AddSphere(Transform parent, string name, Vector3 localPosition, float diameter, Material material)
        {
            GameObject sphere = CreateMeshObject(name, RobotMeshLibrary.Sphere(diameter), material);

            sphere.transform.SetParent(parent, false);
            sphere.transform.localPosition = localPosition;
        }

        /// <summary>
        /// 마디를 따라 누운 원기둥을 붙입니다.
        ///
        /// <b>눕히는 것도 미는 것도 메시를 구울 때 이미 끝냈습니다</b> (<see cref="RobotMeshLibrary"/>).
        /// 그래서 여기서 만드는 오브젝트는 위치·회전·스케일이 전부 기본값입니다.
        /// 마디 길이를 바꾸려면 스케일을 건드리는 것이 아니라 <b>그 길이의 메시를 새로 굽습니다.</b>
        /// </summary>
        /// <param name="parent">부모 (마디)</param>
        /// <param name="name">이름</param>
        /// <param name="length">마디 길이</param>
        /// <param name="thickness">지름</param>
        /// <param name="material">재질</param>
        private static void AddCylinderAlongZ(Transform parent, string name, float length, float thickness, Material material)
        {
            GameObject limb = CreateMeshObject(name, RobotMeshLibrary.CylinderAlongZ(length, thickness), material);

            limb.transform.SetParent(parent, false);
        }

        /// <summary>
        /// 구워 둔 메시를 걸친 <b>스케일 (1, 1, 1)</b> 오브젝트를 만듭니다.
        ///
        /// 예전에는 유니티 기본 도형을 가져와 <c>localScale</c> 로 눌렀습니다. 지금은 크기가
        /// 메시 안에 들어 있으므로 트랜스폼에 남는 배율이 없습니다. 콜라이더는 붙이지 않습니다 —
        /// 충돌 모양은 몸통 상자와 마디 캡슐이 따로 맡습니다.
        /// </summary>
        /// <param name="name">이름</param>
        /// <param name="mesh">걸칠 메시</param>
        /// <param name="material">재질</param>
        /// <returns>만들어진 오브젝트</returns>
        private static GameObject CreateMeshObject(string name, Mesh mesh, Material material)
        {
            GameObject visual = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));

            visual.GetComponent<MeshFilter>().sharedMesh = mesh;

            MeshRenderer renderer = visual.GetComponent<MeshRenderer>();
            if (material != null) renderer.sharedMaterial = material;

            return visual;
        }

        // --- Private Methods : 에셋 ---

        /// <summary>
        /// 발이 딛을 수 있는 레이어입니다. 지형과 프랍만 봅니다.
        /// 로봇 자신의 콜라이더는 <c>Default</c> 에 있으므로 여기서 빠집니다.
        /// </summary>
        /// <returns>레이어 마스크</returns>
        private static LayerMask GroundMask()
        {
            int mask = LayerMask.GetMask("Ground", "Prop");

            // 프로젝트에 그 레이어가 없다면(다른 씬으로 옮겨 갔다면) 전부 보되 로봇 자신만 뺍니다.
            return mask != 0 ? mask : ~0;
        }

        /// <summary>필요한 폴더를 만들어 둡니다.</summary>
        private static void EnsureFolders()
        {
            EnsureFolder(PrefabFolder);
            EnsureFolder(MaterialFolder);
        }

        /// <summary>재질이 없으면 만듭니다. 이미 있으면 그대로 씁니다.</summary>
        /// <param name="name">재질 이름</param>
        /// <param name="color">기본 색</param>
        /// <param name="metallic">금속성</param>
        /// <param name="smoothness">매끄러움</param>
        /// <param name="report">진행 내용</param>
        /// <returns>재질</returns>
        private static Material EnsureMaterial(string name, Color color, float metallic, float smoothness, List<string> report)
        {
            string path = MaterialFolder + "/" + name + ".mat";

            Material existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) return existing;

            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");

            Material material = new Material(shader);

            material.SetColor("_BaseColor", color);
            material.SetColor("_Color", color);
            material.SetFloat("_Metallic", metallic);
            material.SetFloat("_Smoothness", smoothness);
            material.SetFloat("_Glossiness", smoothness);

            AssetDatabase.CreateAsset(material, path);
            report.Add("· 재질을 만들었습니다: " + path);

            return material;
        }

        /// <summary>폴더가 없으면 만듭니다. (중간 폴더까지 차례로 만듭니다)</summary>
        /// <param name="path">"Assets/A/B" 형태의 폴더 경로</param>
        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;

            string[] parts = path.Split('/');
            string current = parts[0];

            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }
    }
}
