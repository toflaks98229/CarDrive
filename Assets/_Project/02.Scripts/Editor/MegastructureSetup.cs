using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 블렌더가 만든 <b>메가스트럭처</b>를 프로젝트에 세웁니다.
///
/// <c>build_megastructure.py</c> 가 씨앗마다 FBX 하나를 냅니다. 여기서는 그것을
/// 임포터 설정 · 머티리얼 · 프리팹까지 이어 붙입니다. 워커와 같은 순서이지만
/// 리그가 없으므로 훨씬 짧습니다 — 메시 하나, 콜라이더 하나, 그게 전부입니다.
///
/// <b>레이어는 Default(0) 입니다.</b> Prop(9) 은 Ground 하고만 부딪히므로 거기에 두면
/// 차와 플레이어가 100 m 콘크리트를 그대로 통과합니다. 집들도 같은 이유로 0 을 씁니다.
///
/// <code>
/// Unity.exe -batchmode -nographics -projectPath . -executeMethod MegastructureSetup.Run
/// </code>
/// </summary>
public static class MegastructureSetup
{
    // --- Constants ---

    private const string ModelDir = "Assets/_Project/04.Art/02.Models/Megastructure";
    private const string PrefabDir = "Assets/_Project/05.Prefabs/Megastructure";
    private const string MaterialDir = "Assets/_Project/04.Art/00.Materials";
    private const string ParentMaterialPath = MaterialDir + "/MartWall.mat";
    private const string ShotDir = "Logs/Megastructure";

    /// <summary>
    /// FBX 안의 이름 → 프로젝트 머티리얼 이름과 <b>설계한 명도</b>입니다.
    ///
    /// 결(텍스처)은 여기서 물리지 않습니다. <see cref="BrutalistTextureSetup"/> 이
    /// 로봇과 <b>같은 표에서</b> 물립니다 — 같은 콘크리트여야 같은 세계로 보입니다.
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

            foreach (string model in models)
            {
                Configure(model, materials);
                Build(model);
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"MegastructureSetup: 구조물 {models.Length} 개 완료");
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
    /// 층이 세어지는지는 여기서만 확인됩니다. 옆에 차 크기 상자를 놓아 자로 씁니다.
    ///
    /// ⚠ 렌더가 필요하므로 <c>-nographics</c> 를 붙이면 안 됩니다.
    /// <code>
    /// Unity.exe -batchmode -projectPath . -executeMethod MegastructureSetup.Preview
    /// </code>
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
        ground.transform.localScale = new Vector3(80f, 1f, 80f);

        Bounds all = new Bounds(Vector3.zero, Vector3.one);
        float x = 0f;
        bool first = true;

        foreach (string path in Directory.GetFiles(PrefabDir, "*.prefab").OrderBy(p => p))
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path.Replace('\\', '/'));
            GameObject go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);

            Bounds b = Measure(go);
            go.transform.position = new Vector3(x - b.center.x + b.extents.x, 0f, 0f);

            Bounds moved = Measure(go);
            if (first) { all = moved; first = false; } else all.Encapsulate(moved);

            x += b.size.x + 40f;
        }

        // 차 한 대. 이것이 없으면 100 m 인지 10 m 인지 그림만 봐서는 알 수 없습니다.
        GameObject car = GameObject.CreatePrimitive(PrimitiveType.Cube);
        car.transform.localScale = new Vector3(4.5f, 1.5f, 1.9f);
        car.transform.position = new Vector3(first ? 0f : all.min.x + 18f, 0.75f, -46f);

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
        cam.fieldOfView = 34f;
        cam.nearClipPlane = 0.5f;
        cam.farClipPlane = 3000f;
        cam.enabled = false;

        // <b>차 눈높이에서</b> 봅니다. 위에서 내려다보면 어떤 건물이든 커 보입니다.
        // 올려다보는 각이 곧 크기의 증거이므로, 그 각이 나오는 자리에서만 판단합니다.
        cam.transform.position = new Vector3(all.min.x + 14f, 2.4f, -all.size.y * 1.15f - 40f);
        cam.transform.LookAt(new Vector3(all.min.x + 40f, all.size.y * 0.42f, 0f));
        Shoot(cam, "megastructure_eye");

        // 넷을 한 줄로. 씨앗이 정말 다른 것을 내놓는지는 나란히 놓아야 보입니다.
        cam.transform.position = new Vector3(all.center.x, all.size.y * 0.30f, -all.size.x * 1.05f - 120f);
        cam.transform.LookAt(new Vector3(all.center.x, all.size.y * 0.34f, 0f));
        Shoot(cam, "megastructure_row");

        Debug.Log($"MegastructureSetup: 미리보기 저장 — {ShotDir}");

        if (Application.isBatchMode) EditorApplication.Exit(0);
    }

    // --- Private Methods ---

    private static Bounds Measure(GameObject go)
    {
        Renderer[] renderers = go.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0) return new Bounds(go.transform.position, Vector3.one);

        Bounds b = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);
        return b;
    }

    private static void Shoot(Camera cam, string name)
    {
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

    /// <summary>
    /// 세 머티리얼을 <c>MartWall</c> 의 <b>변형</b>으로 둡니다.
    ///
    /// 툰 음영 · 빗금 · 거리 페이드를 부모 한 곳에서 관리하기 위해서입니다. 워커가
    /// 쓰는 것과 같은 부모라, 부모를 만지면 로봇과 건물이 함께 움직입니다.
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

    private static void Build(string path)
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

                MeshFilter filter = t.GetComponent<MeshFilter>();
                if (filter == null || filter.sharedMesh == null) continue;

                // 굴을 지나갈 수 있어야 하므로 상자가 아니라 메시 콜라이더입니다.
                // 상자로 두면 기단의 구멍이 막혀 <b>지나갈 수 있는 구멍</b>이라는 뜻이 사라집니다.
                MeshCollider collider = t.GetComponent<MeshCollider>();
                if (collider == null) collider = t.gameObject.AddComponent<MeshCollider>();
                collider.sharedMesh = filter.sharedMesh;
                collider.convex = false;
            }

            PrefabUtility.SaveAsPrefabAsset(root, PrefabDir + "/" + name + ".prefab");
            Debug.Log($"MegastructureSetup: {name} — 프리팹 저장");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }
}
