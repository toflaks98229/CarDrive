using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 블렌더가 만든 <b>메가프레임 베이</b>를 프로젝트에 세우고, 그것을 이어 스파인을 만듭니다.
///
/// <c>build_megastructure.py</c> 가 베이 하나짜리 FBX 를 씨앗마다 냅니다. 베이는
/// <b>이어 붙는 모듈</b>이지 완성된 건물이 아닙니다 — 메가스트럭처의 정의(Wilcoxon)가
/// 모듈 단위와 무한 연장이므로, 여기서 할 일의 절반은 <b>실제로 이어 붙이는 것</b>입니다.
///
/// <b>레이어는 Default(0) 입니다.</b> Prop(9) 은 Ground 하고만 부딪히므로 거기에 두면
/// 차와 플레이어가 콘크리트를 그대로 통과합니다. 집들도 같은 이유로 0 을 씁니다.
///
/// <code>
/// Unity.exe -batchmode -nographics -projectPath . -executeMethod MegastructureSetup.Run
/// Unity.exe -batchmode -projectPath . -executeMethod MegastructureSetup.Preview
/// </code>
/// </summary>
public static class MegastructureSetup
{
    // --- Constants ---

    private const string ModelDir = "Assets/_Project/04.Art/02.Models/Megastructure";
    private const string PrefabDir = "Assets/_Project/05.Prefabs/Megastructure";
    private const string MaterialDir = "Assets/_Project/04.Art/00.Materials";
    private const string ParentMaterialPath = MaterialDir + "/MartWall.mat";
    private const string SpinePath = PrefabDir + "/MegaSpine.prefab";
    private const string ShotDir = "Logs/Megastructure";

    /// <summary>
    /// 스파인 하나에 들어가는 베이 수입니다.
    ///
    /// 실측한 지형이 1100 × 1200 m 입니다. 24 베이(1008 m)로는 세계 안에서 <b>양 끝이
    /// 보입니다</b> — 끝이 보이면 "무한히 연장 가능"이 거짓말이 됩니다. 32 베이면
    /// 1344 m 라 가장 긴 축(1200 m)보다 길어 어느 방향으로 놓아도 밖으로 나갑니다.
    /// 길이는 미학이 아니라 정의에서 나오는 값입니다.
    /// </summary>
    private const int SpineBays = 32;

    /// <summary>
    /// FBX 안의 이름 → 프로젝트 머티리얼 이름과 <b>설계한 명도</b>입니다.
    ///
    /// <b>값이 두 수명을 가릅니다.</b> 콘크리트는 골조(영구), 어두운 것은 꽂아 넣은
    /// 캡슐(임시). 둘이 같은 명도면 메가스트럭처가 아니라 그냥 큰 건물로 보입니다.
    ///
    /// 결(텍스처)은 여기서 물리지 않습니다. <see cref="BrutalistTextureSetup"/> 이
    /// 로봇과 같은 표에서 물립니다 — 같은 콘크리트여야 같은 세계로 보입니다.
    /// </summary>
    private static readonly (string fbx, string asset, Color value)[] Materials =
    {
        ("M_Mega_Concrete", "MegaConcrete", new Color(0.58f, 0.57f, 0.53f)),
        ("M_Mega_Steel", "MegaSteel", new Color(0.26f, 0.28f, 0.31f)),
        ("M_Mega_Dark", "MegaDark", new Color(0.09f, 0.10f, 0.11f)),
    };

    // --- Public Methods ---

    public static void Run()
    {
        int errors = 0;

        try
        {
            Dictionary<string, Material> materials = EnsureMaterials();

            string[] models = Directory.GetFiles(ModelDir, "*.fbx")
                .Select(p => p.Replace('\\', '/'))
                .OrderBy(p => p)
                .ToArray();

            if (models.Length == 0) throw new Exception("FBX 가 하나도 없습니다: " + ModelDir);

            Directory.CreateDirectory(PrefabDir);

            List<GameObject> bays = new List<GameObject>();
            foreach (string model in models)
            {
                Configure(model, materials);
                bays.Add(BuildBay(model));
            }

            BuildSpine(bays);

            AssetDatabase.SaveAssets();
            Debug.Log($"MegastructureSetup: 베이 {bays.Count} 종 · 스파인 {SpineBays} 베이 완료");
        }
        catch (Exception e)
        {
            Debug.LogError("MegastructureSetup: " + e);
            errors++;
        }

        if (Application.isBatchMode) EditorApplication.Exit(errors > 0 ? 2 : 0);
    }

    /// <summary>
    /// 만든 것을 <b>프로젝트 셰이더로</b> 찍어 봅니다.
    ///
    /// 블렌더 렌더로는 알 수 없는 것이 있습니다 — 툰 램프가 명암을 계단으로 끊고,
    /// 팔레트가 색 수를 줄이고, 안개가 중경을 걷어 갑니다. 그 셋을 지난 뒤에도
    /// 골조와 캡슐이 갈려 보이는지는 여기서만 확인됩니다.
    ///
    /// ⚠ 렌더가 필요하므로 <c>-nographics</c> 를 붙이면 안 됩니다.
    /// </summary>
    public static void Preview()
    {
        Directory.CreateDirectory(ShotDir);

        EditorSettings.asyncShaderCompilation = false;
        ShaderUtil.allowAsyncCompilation = false;

        UnityEditor.SceneManagement.EditorSceneManager.NewScene(
            UnityEditor.SceneManagement.NewSceneSetup.EmptyScene,
            UnityEditor.SceneManagement.NewSceneMode.Single);

        GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
        ground.transform.localScale = new Vector3(200f, 1f, 200f);

        GameObject spine = (GameObject)PrefabUtility.InstantiatePrefab(
            AssetDatabase.LoadAssetAtPath<GameObject>(SpinePath));
        Bounds all = Measure(spine);

        // 차 한 대. 이것이 없으면 100 m 인지 10 m 인지 그림만 봐서는 알 수 없습니다.
        GameObject car = GameObject.CreatePrimitive(PrimitiveType.Cube);
        car.transform.localScale = new Vector3(1.9f, 1.5f, 4.5f);
        car.transform.position = new Vector3(0f, 0.75f, all.center.z + 140f);

        GameObject sun = new GameObject("Sun");
        Light light = sun.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1.4f;
        light.shadows = LightShadows.Soft;
        sun.transform.rotation = Quaternion.Euler(38f, -34f, 0f);

        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.55f, 0.62f, 0.72f);
        RenderSettings.ambientEquatorColor = new Color(0.38f, 0.40f, 0.42f);
        RenderSettings.ambientGroundColor = new Color(0.22f, 0.20f, 0.18f);
        RenderSettings.fog = false;

        GameObject camObject = new GameObject("Shot");
        Camera cam = camObject.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.62f, 0.66f, 0.72f);
        cam.fieldOfView = 38f;
        cam.nearClipPlane = 0.4f;
        cam.farClipPlane = 4000f;
        cam.enabled = false;

        // 밑을 지나가며 보는 그림. 운전 게임에서 <b>가장 많이 보게 될 각</b>입니다.
        Shoot(cam, new Vector3(0f, 2.2f, all.center.z - all.extents.z * 0.9f),
              new Vector3(0f, 26f, all.center.z + all.extents.z * 0.6f), "under");

        // 옆에서 본 단면. 골조와 캡슐이 갈려 보이는지는 여기서 판단합니다.
        Shoot(cam, new Vector3(-320f, 60f, all.center.z),
              new Vector3(0f, 34f, all.center.z), "side");

        Debug.Log($"MegastructureSetup: 미리보기 저장 — {ShotDir}");

        if (Application.isBatchMode) EditorApplication.Exit(0);
    }

    // --- Private Methods ---

    /// <summary>
    /// 세 머티리얼을 <c>MartWall</c> 의 <b>변형</b>으로 둡니다.
    ///
    /// 툰 음영 · 빗금 · 거리 페이드를 부모 한 곳에서 관리하기 위해서입니다. 워커가
    /// 쓰는 것과 같은 부모라, 부모를 만지면 로봇과 구조물이 함께 움직입니다.
    ///
    /// 인스턴싱을 켭니다. 스파인은 같은 메시를 스물네 번 놓으므로, 켜지 않으면
    /// 드로우 호출이 그대로 스물네 배가 됩니다 — 이 프로젝트의 병목이 렌더 스레드의
    /// 드로우 제출입니다.
    /// </summary>
    private static Dictionary<string, Material> EnsureMaterials()
    {
        Material parent = AssetDatabase.LoadAssetAtPath<Material>(ParentMaterialPath);
        if (parent == null) throw new Exception("부모 머티리얼이 없습니다: " + ParentMaterialPath);

        Dictionary<string, Material> map = new Dictionary<string, Material>();

        foreach ((string fbx, string asset, Color value) in Materials)
        {
            string path = MaterialDir + "/" + asset + ".mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);

            if (material == null)
            {
                material = new Material(parent.shader);
                AssetDatabase.CreateAsset(material, path);
            }

            material.parent = parent;
            material.SetColor("_BaseColor", value);
            material.SetColor("_Color", value);
            material.enableInstancing = true;

            EditorUtility.SetDirty(material);
            map[fbx] = material;
        }

        return map;
    }

    private static void Configure(string path, Dictionary<string, Material> materials)
    {
        ModelImporter importer = AssetImporter.GetAtPath(path) as ModelImporter;
        if (importer == null) throw new Exception("FBX 를 열지 못했습니다: " + path);

        // 블렌더에서 bake_space_transform 을 켜고 내보내므로 배율은 1 입니다.
        importer.globalScale = 1f;
        importer.useFileScale = true;
        importer.importAnimation = false;
        importer.importCameras = false;
        importer.importLights = false;
        importer.importBlendShapes = false;
        importer.importNormals = ModelImporterNormals.Import;
        importer.importTangents = ModelImporterTangents.None;
        importer.weldVertices = false;

        // 라이트맵을 굽지 않는 프로젝트이므로 uv2 를 만들지 않습니다.
        // (LightmapPolicySetup 참조 — 셰이더가 라이트맵을 읽지 않습니다.)
        importer.generateSecondaryUV = false;

        importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
        importer.materialLocation = ModelImporterMaterialLocation.InPrefab;

        foreach (KeyValuePair<string, Material> pair in materials)
        {
            importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), pair.Key), pair.Value);
        }

        importer.SaveAndReimport();
    }

    private static GameObject BuildBay(string path)
    {
        GameObject fbx = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (fbx == null) throw new Exception("FBX 프리팹을 열지 못했습니다: " + path);

        string name = Path.GetFileNameWithoutExtension(path);
        GameObject root = (GameObject)PrefabUtility.InstantiatePrefab(fbx);

        try
        {
            root.name = name;
            root.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);

            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                t.gameObject.layer = 0;

                // 굽지 않으므로 ContributeGI 는 빼고, 배칭·오클루전만 켭니다.
                GameObjectUtility.SetStaticEditorFlags(t.gameObject,
                    StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccluderStatic |
                    StaticEditorFlags.OccludeeStatic);

                MeshFilter filter = t.GetComponent<MeshFilter>();
                if (filter == null || filter.sharedMesh == null) continue;

                // 다리 사이를 지나갈 수 있어야 하므로 상자가 아니라 메시 콜라이더입니다.
                // 상자면 통로가 막혀 <b>밑으로 지나간다</b>는 뜻이 통째로 사라집니다.
                MeshCollider collider = t.GetComponent<MeshCollider>();
                if (collider == null) collider = t.gameObject.AddComponent<MeshCollider>();
                collider.sharedMesh = filter.sharedMesh;
                collider.convex = false;
            }

            return PrefabUtility.SaveAsPrefabAsset(root, PrefabDir + "/" + name + ".prefab");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    /// <summary>
    /// 베이를 이어 스파인 하나를 만듭니다.
    ///
    /// <b>베이 길이는 재서 씁니다.</b> 42 라고 적어 두면 블렌더 쪽 상수가 바뀌는 순간
    /// 조용히 틈이 생기거나 겹칩니다. 메시의 x 폭이 곧 베이 길이이므로 그것을 읽고,
    /// <b>모든 베이가 같은 길이인지 확인</b>합니다 — 다르면 이어 붙는다는 전제가
    /// 깨진 것이라 여기서 멈춰야 합니다.
    /// </summary>
    private static void BuildSpine(List<GameObject> bays)
    {
        float length = Measure(bays[0]).size.x;

        foreach (GameObject bay in bays)
        {
            float other = Measure(bay).size.x;
            if (Mathf.Abs(other - length) > 0.01f)
            {
                throw new Exception($"베이 길이가 다릅니다: {bays[0].name} {length:F3} m vs " +
                                    $"{bay.name} {other:F3} m — 이어 붙지 않습니다");
            }
        }

        GameObject root = new GameObject("MegaSpine");

        try
        {
            System.Random rng = new System.Random(20260908);

            for (int i = 0; i < SpineBays; i++)
            {
                GameObject bay = (GameObject)PrefabUtility.InstantiatePrefab(bays[rng.Next(bays.Count)]);
                bay.transform.SetParent(root.transform, false);
                bay.transform.localPosition = new Vector3(0f, 0f, (i - SpineBays * 0.5f + 0.5f) * length);

                // 스파인이 z 로 흐르게 돌립니다. 블렌더에서는 x 가 진행 방향이었습니다.
                bay.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
            }

            PrefabUtility.SaveAsPrefabAsset(root, SpinePath);
            Debug.Log($"MegastructureSetup: 스파인 {SpineBays} 베이 × {length:F1} m = {SpineBays * length:F0} m");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    private static Bounds Measure(GameObject go)
    {
        Renderer[] renderers = go.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0) return new Bounds(go.transform.position, Vector3.one);

        Bounds b = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);
        return b;
    }

    private static void Shoot(Camera cam, Vector3 from, Vector3 at, string name)
    {
        cam.transform.position = from;
        cam.transform.LookAt(at);

        RenderTexture rt = new RenderTexture(1600, 700, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
        cam.targetTexture = rt;

        for (int i = 0; i < 3; i++)
        {
            cam.Render();
            while (ShaderUtil.anythingCompiling) System.Threading.Thread.Sleep(50);
        }

        RenderTexture previous = RenderTexture.active;
        RenderTexture.active = rt;

        Texture2D shot = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
        shot.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
        shot.Apply();

        RenderTexture.active = previous;
        cam.targetTexture = null;

        File.WriteAllBytes(Path.Combine(ShotDir, name + ".png"), shot.EncodeToPNG());

        UnityEngine.Object.DestroyImmediate(shot);
        rt.Release();
        UnityEngine.Object.DestroyImmediate(rt);
    }
}
