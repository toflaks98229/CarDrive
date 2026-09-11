using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using CarDrive.Gameplay;

/// <summary>
/// 블렌더에서 만든 보행 로봇을 <b>그 프리팹의 몸으로 갈아 끼웁니다.</b> 스트라이더와
/// 드레드노트가 같은 코드를 씁니다 — 다른 것은 <see cref="Config"/> 의 값뿐입니다.
///
/// <b>치수는 이 파일이 정하지 않습니다.</b> 본 위치·마디 길이·발자리는 전부
/// <c>SM_Strider.rig.json</c> 에 들어 있고, 그것은 블렌더의 아마추어를 그대로 잰 값입니다.
/// 모델을 고쳐 다시 내보내면 json 만 새로 뽑아 이 스크립트를 다시 돌리면 됩니다.
///
/// <b>왜 FBX 계층을 그대로 쓰지 않는가.</b> 블렌더 본은 로컬 +Y 가 뼈 방향이지만
/// <see cref="WalkerLeg"/> 의 규약은 <b>로컬 +Z 가 다음 관절</b>입니다. 그래서 리그 노드는
/// 이 프로젝트 규약대로 두고, FBX 에서는 <b>메시만</b> 가져와 각 노드 밑에 물립니다.
/// json 의 회전값이 그 두 좌표계 사이의 차이입니다.
/// </summary>
public static class WalkerModelSetup
{
    // --- Constants ---

    private const string MaterialDir = "Assets/_Project/04.Art/00.Materials";
    private const string ParentMaterialPath = MaterialDir + "/MartWall.mat";

    /// <summary>동체 상자를 고관절에서 최소한 이만큼 떨어뜨립니다. 파고들면 로봇이 스스로를 밀어냅니다.</summary>
    private const float HipClearance = 0.05f;

    /// <summary>기계 한 대분의 설정입니다. 이 클래스에서 기계마다 다른 것은 이것뿐입니다.</summary>
    private class Config
    {
        public string Fbx;
        public string Rig;
        public string Prefab;

        /// <summary>마디 캡슐의 반지름 — 넓적·종아리·발 순서입니다.</summary>
        public float[] Radius;

        /// <summary>FBX 머티리얼 이름 → 프로젝트 머티리얼 이름과 색입니다.</summary>
        public (string fbx, string asset, Color color, Color emission)[] Materials;
    }

    private static Config Strider()
    {
        return new Config
        {
            Fbx = "Assets/_Project/04.Art/02.Models/Robot/SM_Strider.fbx",
            Rig = "Assets/_Project/04.Art/02.Models/Robot/SM_Strider.rig.json",
            Prefab = "Assets/_Project/05.Prefabs/Robot/WalkerRobot_Strider.prefab",
            Radius = new[] { 0.45f, 0.35f, 0.5f },
            Materials = new[]
            {
                ("M_Strider_Concrete", "RobotConcrete", new Color(0.60f, 0.59f, 0.55f), Color.black),
                ("M_Strider_Steel",    "RobotSteel",    new Color(0.26f, 0.28f, 0.31f), Color.black),
                ("M_Strider_Dark",     "RobotDark",     new Color(0.11f, 0.12f, 0.13f), Color.black),
                ("M_Strider_Lamp",     "RobotLamp",     new Color(0.95f, 0.62f, 0.20f), new Color(2.2f, 1.1f, 0.25f)),
            },
        };
    }

    private static Config Dreadnought()
    {
        return DreadVariant("Castraferrum");
    }

    /// <summary>
    /// 노마드 — 메가스트럭쳐 규격의 거대 스트라이더입니다.
    ///
    /// <b>큰 세 값은 스파인의 머티리얼을 그대로 씁니다.</b> 비슷하게 맞춘 로봇 팔레트가
    /// 아니라 같은 에셋입니다. 스파인의 명도 폭이 로봇보다 넓게 벌어져 있는 것이
    /// 질감 없는 덩어리를 크게 읽히게 하는 장치이고, 같은 것을 쓰면 배칭도 됩니다.
    ///
    /// <b>표시등만 따로 만듭니다.</b> MegaSignal 은 발광이 켜져 있는데 GI 플래그가
    /// EmissiveIsBlack 입니다. 여기서 재지정하면 그 플래그가 뒤집혀 스파인 전체의
    /// 등에 영향이 갑니다. 등 하나 때문에 남의 에셋을 건드릴 이유가 없습니다.
    /// </summary>
    private static Config Nomad()
    {
        return new Config
        {
            Fbx = "Assets/_Project/04.Art/02.Models/Robot/SM_Nomad.fbx",
            Rig = "Assets/_Project/04.Art/02.Models/Robot/SM_Nomad.rig.json",
            Prefab = "Assets/_Project/05.Prefabs/Robot/WalkerRobot_Nomad.prefab",
            // 마디 단면에서 옵니다 - 넓적 5.2, 종아리 4.3~2.5, 발판 9.4 x 10.4.
            Radius = new[] { 1.9f, 1.3f, 2.0f },
            Materials = new[]
            {
                ("M_Nomad_Concrete", "MegaConcrete", new Color(0.42f, 0.415f, 0.395f), Color.black),
                ("M_Nomad_Steel",    "MegaSteel",    new Color(0.22f, 0.235f, 0.26f), Color.black),
                ("M_Nomad_Dark",     "MegaDark",     new Color(0.052f, 0.058f, 0.064f), Color.black),
                ("M_Nomad_Signal",   "NomadSignal",  new Color(0.86f, 0.36f, 0.09f), new Color(0.52f, 0.19f, 0.035f)),
            },
        };
    }

    /// <summary>
    /// 드레드노트 계열의 패턴 이름입니다. 블렌더의 <c>PATTERNS</c> 표와 <b>같은 이름</b>이어야
    /// 합니다 — 저쪽이 파일 이름을 그 이름으로 짓기 때문입니다.
    /// </summary>
    private static readonly string[] DreadPatterns =
    {
        "Castraferrum", "Ironclad", "Mortis", "Ballistus",
    };

    /// <summary>
    /// 드레드노트 계열 하나의 설정입니다.
    ///
    /// <b>기본 패턴만 옛 이름을 씁니다.</b> 씬과 프리팹이 전부 <c>SM_Dreadnought</c> 를
    /// 가리키고 있어서, 이름을 바꾸면 형태와 무관한 곳이 줄줄이 깨집니다.
    /// 변형은 <c>SM_Dread_&lt;패턴&gt;</c> 입니다. 이 규칙은 <c>build_dreadnought.paths_for</c>
    /// 와 짝입니다 — 한쪽만 고치면 파일을 못 찾습니다.
    /// </summary>
    /// <param name="pattern">패턴 이름</param>
    /// <returns>그 패턴의 설정</returns>
    private static Config DreadVariant(string pattern)
    {
        bool baseline = pattern == "Castraferrum";
        string stem = baseline ? "SM_Dreadnought" : "SM_Dread_" + pattern;
        string prefab = baseline ? "WalkerRobot_Dreadnought" : "WalkerRobot_Dread" + pattern;

        return new Config
        {
            Fbx = "Assets/_Project/04.Art/02.Models/Robot/" + stem + ".fbx",
            Rig = "Assets/_Project/04.Art/02.Models/Robot/" + stem + ".rig.json",
            Prefab = "Assets/_Project/05.Prefabs/Robot/" + prefab + ".prefab",
            // 변형끼리 마디 단면이 거의 같습니다(넓적 1.00~1.06). 반지름은 공유합니다.
            Radius = new[] { 0.36f, 0.28f, 0.30f },
            Materials = new[]
            {
                ("M_Dread_Concrete", "RobotConcrete", new Color(0.60f, 0.59f, 0.55f), Color.black),
                ("M_Dread_Steel",    "RobotSteel",    new Color(0.26f, 0.28f, 0.31f), Color.black),
                ("M_Dread_Dark",     "RobotDark",     new Color(0.11f, 0.12f, 0.13f), Color.black),
                ("M_Dread_Lamp",     "RobotLamp",     new Color(0.95f, 0.62f, 0.20f), new Color(2.2f, 1.1f, 0.25f)),
            },
        };
    }

    /// <summary>지금 조립하고 있는 기계입니다. 메서드 사이로 넘기지 않고 여기 둡니다.</summary>
    private static Config _config;

    // --- Public Methods ---

    /// <summary>스트라이더를 조립합니다. 배치모드에서 부릅니다.</summary>
    public static void RunStrider()
    {
        Run(Strider());
    }

    /// <summary>드레드노트를 조립합니다. 배치모드에서 부릅니다.</summary>
    public static void RunDreadnought()
    {
        Run(Dreadnought());
    }

    /// <summary>노마드를 조립합니다. 배치모드에서 부릅니다.</summary>
    public static void RunNomad()
    {
        Run(Nomad());
    }

    /// <summary>
    /// 드레드노트 계열을 <b>한 번에</b> 조립합니다. 배치모드에서 부릅니다.
    ///
    /// 변형마다 따로 부르면 유니티를 네 번 띄워야 하고 그때마다 에셋 데이터베이스가
    /// 다시 올라옵니다. 한 판에 다 돌리는 편이 훨씬 쌉니다.
    /// </summary>
    public static void RunDreadnoughtAll()
    {
        int errors = 0;

        foreach (string pattern in DreadPatterns) errors += RunOne(DreadVariant(pattern));

        AssetDatabase.SaveAssets();
        Debug.Log("WalkerModelSetup: 드레드노트 계열 " + DreadPatterns.Length +
                  "종 — 실패 " + errors + "건");

        if (Application.isBatchMode) EditorApplication.Exit(errors > 0 ? 2 : 0);
    }

    /// <summary>실패하면 종료코드 2 로 나갑니다.</summary>
    private static void Run(Config config)
    {
        int errors = RunOne(config);
        AssetDatabase.SaveAssets();
        if (Application.isBatchMode) EditorApplication.Exit(errors > 0 ? 2 : 0);
    }

    /// <summary>하나를 조립합니다. 실패 건수를 돌려주고 <b>종료하지 않습니다.</b></summary>
    /// <param name="config">조립할 기계의 설정</param>
    /// <returns>실패했으면 1, 아니면 0</returns>
    private static int RunOne(Config config)
    {
        _config = config;
        int errors = 0;

        try
        {
            Rig rig = LoadRig();
            Dictionary<string, Material> materials = EnsureMaterials();
            ConfigureImporter(materials);
            Rebuild(rig);
            PruneSceneOverrides();
            Debug.Log("WalkerModelSetup: 완료 — " + _config.Prefab);
        }
        catch (Exception e)
        {
            Debug.LogError("WalkerModelSetup: " + e);
            errors++;
        }

        return errors;
    }

    // --- Private Methods : 치수 ---

    private static Rig LoadRig()
    {
        TextAsset json = AssetDatabase.LoadAssetAtPath<TextAsset>(_config.Rig);
        if (json == null) throw new Exception("리그 치수를 찾지 못했습니다: " + _config.Rig);

        Rig rig = JsonUtility.FromJson<Rig>(json.text);
        if (rig == null || rig.legs == null || rig.legs.Length < 2) throw new Exception("리그 치수가 온전하지 않습니다");

        return rig;
    }

    // --- Private Methods : 머티리얼 ---

    /// <summary>
    /// 4개 슬롯의 머티리얼을 <c>MartWall</c> 의 <b>변형</b>으로 만듭니다.
    /// 툰 음영·빗금·거리 페이드가 부모 한 곳에서 관리됩니다.
    /// </summary>
    private static Dictionary<string, Material> EnsureMaterials()
    {
        Material parent = AssetDatabase.LoadAssetAtPath<Material>(ParentMaterialPath);
        if (parent == null) throw new Exception("부모 머티리얼이 없습니다: " + ParentMaterialPath);

        Dictionary<string, Material> map = new Dictionary<string, Material>();

        foreach ((string fbx, string asset, Color color, Color emission) in _config.Materials)
        {
            string path = MaterialDir + "/" + asset + ".mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);

            if (material == null)
            {
                material = new Material(parent.shader);
                AssetDatabase.CreateAsset(material, path);
            }

            material.parent = parent;
            material.SetColor("_BaseColor", color);
            material.SetColor("_Color", color);

            bool lit = emission.maxColorComponent > 0f;
            material.SetColor("_EmissionColor", emission);
            if (lit) material.EnableKeyword("_EMISSION");
            else material.DisableKeyword("_EMISSION");
            material.globalIlluminationFlags = lit
                ? MaterialGlobalIlluminationFlags.RealtimeEmissive
                : MaterialGlobalIlluminationFlags.EmissiveIsBlack;

            EditorUtility.SetDirty(material);
            map[fbx] = material;
        }

        return map;
    }

    // --- Private Methods : 임포터 ---

    private static void ConfigureImporter(Dictionary<string, Material> materials)
    {
        ModelImporter importer = AssetImporter.GetAtPath(_config.Fbx) as ModelImporter;
        if (importer == null) throw new Exception("FBX 를 찾지 못했습니다: " + _config.Fbx);

        // FBX 는 <b>Apply Transform(bake_space_transform) 을 켜고</b> 내보내야 합니다. 끄면 축 변환이
        // 메시가 아니라 오브젝트 트랜스폼에 실려, 여기서 꺼내 쓰는 Mesh 는 블렌더의 Z-up 인 채로 옵니다.
        // 켜면 단위(cm)까지 제대로 실리므로 배율은 1 입니다.
        importer.globalScale = 1f;
        importer.useFileScale = true;
        importer.importAnimation = false;
        importer.importCameras = false;
        importer.importLights = false;
        importer.importBlendShapes = false;
        importer.isReadable = false;
        importer.importNormals = ModelImporterNormals.Import;
        importer.importTangents = ModelImporterTangents.None;
        importer.generateSecondaryUV = false;
        importer.weldVertices = false;

        // ⚠ External 로 두면 FBX 안의 머티리얼을 폴더로 꺼내 remap 을 덮어씁니다.
        importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
        importer.materialLocation = ModelImporterMaterialLocation.InPrefab;

        foreach (KeyValuePair<string, Material> pair in materials)
        {
            importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), pair.Key), pair.Value);
        }

        importer.SaveAndReimport();
    }

    // --- Private Methods : 프리팹 ---

    private static void Rebuild(Rig rig)
    {
        GameObject fbx = AssetDatabase.LoadAssetAtPath<GameObject>(_config.Fbx);
        if (fbx == null) throw new Exception("FBX 프리팹을 열지 못했습니다: " + _config.Fbx);

        GameObject sample = (GameObject)PrefabUtility.InstantiatePrefab(fbx);
        Dictionary<string, Renderer> source = sample.GetComponentsInChildren<MeshRenderer>(true)
            .ToDictionary(r => r.name, r => (Renderer)r);

        GameObject root = PrefabUtility.LoadPrefabContents(_config.Prefab);

        try
        {
            Transform body = Find(root.transform, "Body");
            body.localPosition = V(rig.bodyLocal.pos);
            body.localRotation = Quaternion.identity;

            // 옛 상자 몸은 전부 걷어냅니다. 다리 뿌리와 마디 노드만 남깁니다.
            StripMeshes(root.transform);
            StripAim(root.transform, rig);

            // 조준 마디를 먼저 세웁니다. 몸통 파츠 일부가 이 밑으로 들어갑니다.
            Dictionary<string, Transform> nodes = new Dictionary<string, Transform> { { "Body", body } };
            BuildAim(rig, nodes);

            // <b>파츠도 마디로 등록합니다.</b> 무장의 움직이는 조각(포신 · 로터 ·
            // 덮개)은 무장 본체 밑으로 들어가므로, 앞서 만든 파츠를 이름으로 찾을
            // 수 있어야 합니다. 목록은 <b>부모가 먼저</b> 오도록 나옵니다.
            foreach (Part part in rig.bodyParts)
            {
                Transform parent = Resolve(nodes, part.parent, body);
                GameObject go = Attach(parent, part, source);
                go.SetActive(part.active);
                nodes[part.node] = go.transform;
            }

            BuildMuzzles(rig, nodes);
            BuildTurrets(root, rig, nodes);

            foreach (Leg leg in rig.legs)
            {
                Transform node = Find(root.transform, leg.node);
                node.localPosition = V(leg.legLocal.pos);
                node.localRotation = Quaternion.identity;

                Transform femur = Find(node, "Femur");
                Transform tibia = Find(femur, "Tibia");
                Transform tarsus = Find(tibia, "Tarsus");

                Place(femur, leg.femurLocal);
                Place(tibia, leg.tibiaLocal);
                Place(tarsus, leg.tarsusLocal);

                Segment(femur, leg.upperLength, _config.Radius[0]);
                Segment(tibia, leg.lowerLength, _config.Radius[1]);
                Segment(tarsus, leg.ankleLength, _config.Radius[2]);

                foreach (Part part in leg.parts)
                {
                    // HipMesh 는 다리 뿌리에 붙습니다. 고관절 하우징은 넓적마디를 따라 돌면 안 됩니다.
                    Transform parent =
                        part.node == "HipMesh" ? node :
                        part.node == "FemurMesh" ? femur :
                        part.node == "TibiaMesh" ? tibia : tarsus;
                    Attach(parent, part, source);
                }

                WalkerLeg walker = node.GetComponent<WalkerLeg>();
                if (walker == null) throw new Exception(leg.node + " 에 WalkerLeg 가 없습니다");

                walker.upperBone = femur;
                walker.lowerBone = tibia;
                walker.ankleBone = tarsus;
                walker.upperLength = leg.upperLength;
                walker.lowerLength = leg.lowerLength;
                walker.ankleLength = leg.ankleLength;
                walker.ankleOutward = leg.ankleOutward;
                walker.homeOffset = V(leg.homeOffset);
                walker.kneePole = V(leg.kneePole);
            }

            WalkerRobot robot = root.GetComponent<WalkerRobot>();
            if (robot == null) throw new Exception("WalkerRobot 이 없습니다");
            robot.standHeight = rig.standHeight;
            robot.body = body;

            BodyBox(root, rig);

            PrefabUtility.SaveAsPrefabAsset(root, _config.Prefab);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
            UnityEngine.Object.DestroyImmediate(sample);
        }
    }

    /// <summary>
    /// 씬에 놓인 인스턴스에서 <b>마디의 자세 오버라이드를 걷어냅니다.</b>
    ///
    /// <see cref="WalkerRobot"/> 이 <c>[ExecuteAlways]</c> 라 에디터에서 잡은 자세가 그대로
    /// 씬에 override 로 굳어 있습니다. 리그가 바뀐 지금 그 값들은 <b>옛 다리의 각도</b>라,
    /// 두면 씬에서만 로봇이 뒤틀려 보입니다. 재생하면 IK 가 다시 풀지만 그때까지는 아닙니다.
    ///
    /// 루트의 자리·이름·배선은 사람이 정한 것이므로 남깁니다.
    /// </summary>
    private static void PruneSceneOverrides()
    {
        string guid = AssetDatabase.AssetPathToGUID(_config.Prefab);

        // 그 프리팹을 쓰지 않는 씬은 <b>열지도 않습니다.</b> 여는 것만으로도 [ExecuteAlways] 가 돌아
        // 남의 씬을 건드릴 수 있습니다.
        foreach (string scenePath in AssetDatabase.FindAssets("t:Scene", new[] { "Assets/_Project" })
                     .Select(AssetDatabase.GUIDToAssetPath)
                     .Where(p => System.IO.File.ReadAllText(p).Contains(guid)))
        {
            UnityEngine.SceneManagement.Scene scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(
                scenePath, UnityEditor.SceneManagement.OpenSceneMode.Single);

            bool touched = false;

            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                {
                    GameObject go = t.gameObject;
                    if (!PrefabUtility.IsAnyPrefabInstanceRoot(go)) continue;

                    // <b>씬 안의 모든 워커를 걷습니다.</b> 방금 다시 만든 프리팹만 걷으면
                    // 나머지 워커의 찌꺼기가 남습니다 — 씬을 여는 것만으로 [ExecuteAlways] 가
                    // 모든 워커의 본을 자세잡고, 저장하면 그 자세가 오버라이드로 굳습니다.
                    // 실제로 드레드노트를 다시 만들 때마다 스트라이더에 자세 오버라이드가
                    // 220 줄씩 쌓였습니다. 본 회전은 런타임에 덮어써지므로 씬에 남을 이유가
                    // 없고, 뿌리의 위치·회전만 남깁니다.
                    if (go.GetComponent<WalkerRobot>() == null) continue;

                    Transform sourceRoot = PrefabUtility.GetCorrespondingObjectFromSource(go) is GameObject source
                        ? source.transform
                        : null;

                    PropertyModification[] mods = PrefabUtility.GetPropertyModifications(go);
                    if (mods == null) continue;

                    PropertyModification[] kept = mods
                        .Where(m => !(m.target is Transform) || m.target == sourceRoot)
                        .ToArray();

                    if (kept.Length == mods.Length) continue;

                    PrefabUtility.SetPropertyModifications(go, kept);
                    Debug.Log($"WalkerModelSetup: {scenePath} 의 {go.name} 에서 옛 자세 오버라이드 {mods.Length - kept.Length} 개를 걷어냈습니다");
                    touched = true;
                }
            }

            if (touched) UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
        }
    }

    /// <summary>
    /// 동체 상자를 헐 코어에 맞추되, <b>고관절이 상자 밖에 남도록</b> 뒤쪽을 깎습니다.
    /// 고관절이 상자 안에 박히면 자기 충돌이 켜진 채로 로봇이 제자리에서 떱니다.
    /// </summary>
    private static void BodyBox(GameObject root, Rig rig)
    {
        BoxCollider box = root.GetComponent<BoxCollider>();
        if (box == null) return;

        Vector3 center = V(rig.bodyBox.center);
        Vector3 size = V(rig.bodyBox.size);

        float rear = center.z - size.z * 0.5f;
        float floor = center.y - size.y * 0.5f;

        foreach (Leg leg in rig.legs)
        {
            Vector3 hip = V(rig.bodyLocal.pos) + V(leg.legLocal.pos);
            float radius = _config.Radius[0] + HipClearance;

            // 고관절이 상자 <b>아래</b>에 있으면 바닥을 들어 올립니다. 드레드노트는 동체가
            // 고관절 축 위에 바로 얹혀 있어 상자 밑면이 고관절과 같은 높이입니다.
            bool spanned = Mathf.Abs(hip.x - center.x) < size.x * 0.5f
                        && Mathf.Abs(hip.z - center.z) < size.z * 0.5f;

            if (spanned)
            {
                float lift = hip.y + radius;
                if (lift > floor) floor = lift;
            }

            bool inside = Mathf.Abs(hip.x - center.x) < size.x * 0.5f
                       && Mathf.Abs(hip.y - center.y) < size.y * 0.5f;

            if (!inside) continue;

            float limit = hip.z + radius;
            if (limit > rear) rear = limit;
        }

        float front = center.z + size.z * 0.5f;
        float ceiling = center.y + size.y * 0.5f;

        box.size = new Vector3(size.x, Mathf.Max(ceiling - floor, 0.1f), Mathf.Max(front - rear, 0.1f));
        box.center = new Vector3(center.x, (ceiling + floor) * 0.5f, (front + rear) * 0.5f);
    }

    /// <summary>
    /// 조준 마디를 세웁니다. 리그 데이터가 부모를 자식보다 먼저 적어 두므로 한 번 훑으면 됩니다.
    ///
    /// 회전은 넣지 않습니다 — <see cref="RobotTurret"/> 이 매 프레임 자기 축으로 돌리므로,
    /// 여기서 회전을 주면 그것이 조준각에 더해져 규약이 깨집니다.
    /// </summary>
    /// <summary>
    /// 지난번에 세운 조준·총구·포탑 마디를 걷습니다.
    ///
    /// <b>왜 따로 걷어야 하는가.</b> <see cref="StripMeshes"/> 는 메시가 붙은 것만 걷습니다.
    /// 조준 마디는 빈 트랜스폼이라 그 그물에 걸리지 않고 살아남는데,
    /// <see cref="BuildAim"/> 은 조건 없이 새로 만듭니다. 그래서 도구를 두 번 돌리면
    /// 마디가 <b>두 벌</b>이 됩니다. 실제로 드레드노트 프리팹에 빈 껍데기 11 개가
    /// 쌓여 있었습니다 — 부품이 붙은 쪽만 동작해서 실측으로는 드러나지 않았습니다.
    ///
    /// 이 마디들은 전부 리그 JSON 이 이름까지 정해 만드는 것이므로, 남겨 두고 재활용할
    /// 이유가 없습니다. 지우고 다시 세웁니다.
    /// </summary>
    private static void StripAim(Transform root, Rig rig)
    {
        HashSet<string> built = new HashSet<string>();

        if (rig.aim != null)
        {
            foreach (Aim aim in rig.aim) built.Add(aim.node);
        }

        if (rig.muzzles != null)
        {
            foreach (Muzzle muzzle in rig.muzzles) built.Add(muzzle.node);
        }

        if (rig.turrets != null)
        {
            foreach (Turret turret in rig.turrets) built.Add("Turret_" + turret.name);
        }

        // 부모를 지우면 자식도 함께 사라져 목록에 빈 칸이 생깁니다. 먼저 복사해 두고 확인합니다.
        foreach (Transform node in root.GetComponentsInChildren<Transform>(true).ToArray())
        {
            if (node == null || node == root) continue;
            if (!built.Contains(node.name)) continue;

            UnityEngine.Object.DestroyImmediate(node.gameObject);
        }
    }

    private static void BuildAim(Rig rig, Dictionary<string, Transform> nodes)
    {
        if (rig.aim == null) return;

        foreach (Aim aim in rig.aim)
        {
            Transform parent = Resolve(nodes, aim.parent, nodes["Body"]);

            GameObject go = new GameObject(aim.node);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = V(aim.pos);
            go.transform.localRotation = Quaternion.identity;
            go.layer = parent.gameObject.layer;

            nodes[aim.node] = go.transform;
        }
    }

    /// <summary>총구 자리를 빈 오브젝트로 둡니다. 발사·머즐 플래시가 여기 붙습니다.</summary>
    private static void BuildMuzzles(Rig rig, Dictionary<string, Transform> nodes)
    {
        if (rig.muzzles == null) return;

        foreach (Muzzle muzzle in rig.muzzles)
        {
            Transform parent = Resolve(nodes, muzzle.parent, nodes["Body"]);

            GameObject go = new GameObject(muzzle.node);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = V(muzzle.pos);
            go.transform.localRotation = Quaternion.identity;
            go.layer = parent.gameObject.layer;

            nodes[muzzle.node] = go.transform;
        }
    }

    /// <summary>선회·부앙 쌍마다 포탑 부품을 하나씩 답니다. 축과 한계는 리그 데이터가 정합니다.</summary>
    private static void BuildTurrets(GameObject root, Rig rig, Dictionary<string, Transform> nodes)
    {
        foreach (RobotTurret stale in root.GetComponentsInChildren<RobotTurret>(true))
        {
            UnityEngine.Object.DestroyImmediate(stale);
        }

        if (rig.turrets == null) return;

        Dictionary<string, Aim> byNode = new Dictionary<string, Aim>();
        if (rig.aim != null)
        {
            foreach (Aim aim in rig.aim) byNode[aim.node] = aim;
        }

        foreach (Turret turret in rig.turrets)
        {
            if (!nodes.TryGetValue(turret.yaw, out Transform yaw) ||
                !nodes.TryGetValue(turret.pitch, out Transform pitch))
            {
                throw new Exception(turret.name + " 포탑의 마디를 찾지 못했습니다");
            }

            // 포탑 부품은 마디가 아니라 <b>선회 마디의 부모</b>에 답니다. 마디에 달면
            // 자기가 돌리는 트랜스폼과 같은 자리에 있어 인스펙터에서 헷갈립니다.
            GameObject host = new GameObject("Turret_" + turret.name);
            host.transform.SetParent(yaw.parent, false);
            host.layer = yaw.gameObject.layer;

            RobotTurret component = host.AddComponent<RobotTurret>();
            component.yawNode = yaw;
            component.pitchNode = pitch;
            component.muzzle = string.IsNullOrEmpty(turret.muzzle) ? null
                : (nodes.TryGetValue(turret.muzzle, out Transform m) ? m : null);

            if (byNode.TryGetValue(turret.yaw, out Aim yawAim))
            {
                component.yawAxis = V(yawAim.axis);
                component.yawRange = new Vector2(yawAim.range[0], yawAim.range[1]);
            }

            if (byNode.TryGetValue(turret.pitch, out Aim pitchAim))
            {
                component.pitchAxis = V(pitchAim.axis);
                component.pitchRange = new Vector2(pitchAim.range[0], pitchAim.range[1]);
            }
        }
    }

    /// <summary>
    /// 이름으로 마디를 찾습니다. <b>비어 있으면</b> 기본값, <b>있는데 못 찾으면 예외</b>입니다.
    ///
    /// ⚠ 예전에는 못 찾으면 조용히 기본값(몸통)으로 떨어졌습니다. 그래서 무장의
    /// 움직이는 조각을 <c>parent: "Gun_Rotary"</c> 로 적었을 때, 이름이 한 글자만
    /// 틀려도 <b>로터가 몸통 한복판에 붙었습니다</b> - 오류도 경고도 없이 말입니다.
    /// 이름을 적어 놓고 못 찾는 것은 <b>언제나 실수</b>이므로 여기서 멈춥니다.
    /// </summary>
    /// <param name="nodes">지금까지 만든 마디들</param>
    /// <param name="name">찾을 이름. 비어 있으면 <paramref name="fallback"/></param>
    /// <param name="fallback">이름이 비었을 때 쓸 마디</param>
    /// <returns>찾은 마디</returns>
    private static Transform Resolve(Dictionary<string, Transform> nodes, string name, Transform fallback)
    {
        if (string.IsNullOrEmpty(name)) return fallback;

        if (!nodes.TryGetValue(name, out Transform found))
        {
            throw new Exception("리그에 " + name + " 마디가 없습니다. "
                                + "부모가 자식보다 먼저 나오는지 확인하십시오.");
        }

        return found;
    }

    /// <summary>마디 캡슐을 길이에 맞춥니다. 캡슐은 로컬 +Z 로 눕습니다.</summary>
    private static void Segment(Transform node, float length, float radius)
    {
        CapsuleCollider capsule = node.GetComponent<CapsuleCollider>();
        if (capsule == null) return;

        capsule.direction = 2;
        capsule.radius = radius;
        capsule.height = Mathf.Max(length, radius * 2f);
        capsule.center = new Vector3(0f, 0f, length * 0.5f);
    }

    private static GameObject Attach(Transform parent, Part part, Dictionary<string, Renderer> source)
    {
        if (!source.TryGetValue(part.mesh, out Renderer template))
        {
            throw new Exception("FBX 에 " + part.mesh + " 가 없습니다");
        }

        GameObject go = new GameObject(part.node);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = V(part.pos);
        go.transform.localRotation = Q(part.rot);
        go.layer = parent.gameObject.layer;

        MeshFilter filter = go.AddComponent<MeshFilter>();
        filter.sharedMesh = template.GetComponent<MeshFilter>().sharedMesh;

        MeshRenderer renderer = go.AddComponent<MeshRenderer>();
        renderer.sharedMaterials = template.sharedMaterials;

        return go;
    }

    /// <summary>메시를 들고 있는 자식을 전부 지웁니다. 리그 노드(다리 뿌리·마디)는 남깁니다.</summary>
    private static void StripMeshes(Transform root)
    {
        foreach (MeshFilter filter in root.GetComponentsInChildren<MeshFilter>(true).ToArray())
        {
            // ⚠ <b>부모를 지우면 자식도 같이 죽습니다.</b> 목록은 미리 떠 놓은 것이라
            // 죽은 자식이 뒤에 남아 있고, 그것을 만지면 예외가 납니다.
            //
            // 파츠가 전부 평평하게 놓여 있던 동안에는 이 일이 없었습니다. 무장의
            // 움직이는 조각(포신 · 로터 · 덮개)이 <b>무장 파츠 밑으로</b> 들어가면서
            // 처음으로 부모-자식인 메시가 생겼습니다.
            if (filter == null) continue;
            if (filter.transform == root) continue;

            UnityEngine.Object.DestroyImmediate(filter.gameObject);
        }
    }

    private static void Place(Transform node, Trs trs)
    {
        node.localPosition = V(trs.pos);
        node.localRotation = Q(trs.rot);
    }

    /// <summary>바로 아래 자식을 먼저 보고, 없으면 후손까지 내려갑니다. 같은 이름의 마디가 다리마다 있으므로 순서가 중요합니다.</summary>
    private static Transform Find(Transform root, string name)
    {
        foreach (Transform child in root)
        {
            if (child.name == name) return child;
        }

        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
        {
            if (t != root && t.name == name) return t;
        }

        throw new Exception(root.name + " 밑에서 " + name + " 을 찾지 못했습니다");
    }

    private static Vector3 V(float[] v) { return new Vector3(v[0], v[1], v[2]); }

    private static Quaternion Q(float[] q) { return new Quaternion(q[0], q[1], q[2], q[3]); }

    // --- Private Types ---

    [Serializable]
    private class Trs
    {
        public float[] pos;
        public float[] rot;
    }

    [Serializable]
    private class Part
    {
        public float[] pos;
        public float[] rot;
        public string node;
        public string mesh;
        public bool active;

        /// <summary>매달릴 노드입니다. 비어 있으면 몸통입니다.</summary>
        public string parent;
    }

    /// <summary>조준 마디 하나입니다. 축과 한계까지 모델에서 유도해 옵니다.</summary>
    [Serializable]
    private class Aim
    {
        public string node;
        public string parent;
        public float[] pos;
        public float[] axis;
        public float[] range;
    }

    /// <summary>총구 자리입니다. 발사 원점이자 조준 기준점입니다.</summary>
    [Serializable]
    private class Muzzle
    {
        public string node;
        public string parent;
        public float[] pos;
    }

    /// <summary>선회·부앙 한 쌍을 묶어 포탑 하나로 만듭니다.</summary>
    [Serializable]
    private class Turret
    {
        public string name;
        public string yaw;
        public string pitch;
        public string muzzle;
    }

    [Serializable]
    private class Box
    {
        public float[] center;
        public float[] size;
    }

    [Serializable]
    private class Leg
    {
        public string node;
        public Trs legLocal;
        public float upperLength;
        public float lowerLength;
        public float ankleLength;
        public float[] homeOffset;
        public float[] kneePole;
        public float ankleOutward;
        public Trs femurLocal;
        public Trs tibiaLocal;
        public Trs tarsusLocal;
        public Part[] parts;
    }

    [Serializable]
    private class Rig
    {
        public float standHeight;
        public Trs bodyLocal;
        public Part[] bodyParts;
        public Aim[] aim;
        public Muzzle[] muzzles;
        public Turret[] turrets;
        public Leg[] legs;
        public Box bodyBox;
    }
}
