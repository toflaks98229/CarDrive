using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Kenney 트럭을 이 게임의 <b>그림체로</b> 갈아입힙니다.
///
/// 원본에는 색판도 UV 도 없고 부품마다 <b>단색 머티리얼</b>이 하나씩 붙어 있을
/// 뿐입니다(`plastic` · `paintGreen` · `window` · `carTire` …). 그래서 색판을 밤
/// 팔레트로 다시 굽는 일 없이 툰 머티리얼로 바로 바꿔 끼울 수 있습니다.
///
/// ⚠ <b><c>materialLocation = External</c> 을 쓰지 않습니다.</b> 그 설정은 FBX 안의
/// 머티리얼을 모델 옆 <c>Materials/</c> 폴더로 꺼내 그것을 쓰게 만들고, 여기서
/// 지정한 짝을 <b>덮어씁니다.</b> 결과는 URP Lit 이 붙은 프리팹입니다.
/// <c>InPrefab</c> 으로 두고 <c>AddRemap</c> 만 씁니다.
///
/// ⚠ <b>FBX 안의 머티리얼 이름은 추측하지 않습니다.</b> 이름이 어긋나면 remap 이
/// 조용히 무시되어 흰 트럭이 나옵니다. <c>LoadAllAssetsAtPath</c> 로 읽어서 씁니다.
///
/// <code>
/// Unity.exe -batchmode -nographics -projectPath . -executeMethod TruckImport.Run
/// </code>
/// </summary>
public static class TruckImport
{
    private const string ModelDir =
        "Packages/com.toflaks.vendor.kenney-car-kit/CarKit/Models";
    private const string MaterialDir = "Assets/_Project/04.Art/00.Materials";
    private const string ToonShader = "CarDrive/Toon Lit";

    /// <summary>
    /// 창에 쓸 셰이더입니다.
    ///
    /// ⚠ <b>차창은 불투명이면 안 됩니다.</b> 툰 셰이더는 서른 몇 개 머티리얼이
    /// 불투명이라는 전제 위에 함께 쓰고 있어서 거기에 투명을 넣지 않았고,
    /// 대신 유리용 셰이더가 따로 있습니다(<c>CarDriveToonGlass</c>). 세단의
    /// <c>CarGlass</c> 가 쓰는 바로 그것입니다.
    /// </summary>
    private const string GlassShader = "CarDrive/Toon Glass";

    /// <summary>
    /// 원본 머티리얼 이름과 이 게임에서 쓸 색입니다.
    ///
    /// 색은 원본의 <c>Kd</c> 를 그대로 쓰지 않고 <b>눌러서</b> 씁니다 — 이 게임은
    /// 밤 주행이 기본이라 카툰 팔레트를 그대로 들이면 헤드라이트와 귀신이 묻힙니다.
    /// 지면 텍스처를 밤 팔레트로 다시 구운 것과 같은 이유입니다.
    /// </summary>
    private static readonly (string Name, Color Colour)[] Palette =
    {
        ("plastic",     new Color(0.26f, 0.26f, 0.28f)),
        ("paintGreen",  new Color(0.20f, 0.42f, 0.30f)),
        ("paintRed",    new Color(0.44f, 0.18f, 0.16f)),
        ("paintBlue",   new Color(0.18f, 0.28f, 0.44f)),
        ("paintYellow", new Color(0.52f, 0.44f, 0.18f)),
        ("paintWhite",  new Color(0.62f, 0.62f, 0.60f)),
        ("paintBlack",  new Color(0.10f, 0.10f, 0.11f)),
        // ⚠ <b>알파가 유리의 진하기입니다.</b> 1 로 두면 유리 셰이더를 써도 통짜 판입니다.
        ("window",      new Color(0.16f, 0.19f, 0.22f, 0.45f)),
        ("carTire",     new Color(0.09f, 0.09f, 0.10f)),
        ("lightFront",  new Color(0.86f, 0.83f, 0.72f)),
        ("lightBack",   new Color(0.52f, 0.14f, 0.10f)),
        ("_defaultMat", new Color(0.45f, 0.45f, 0.45f)),
    };

    // --- Public Methods ---

    /// <summary>FBX 안에 어떤 머티리얼이 있는지만 읽습니다. 아무것도 안 바꿉니다.</summary>
    public static void Survey()
    {
        foreach (string path in Models())
        {
            List<string> found = new List<string>();

            foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(path))
            {
                if (asset is Material material) found.Add(material.name);
            }

            GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            string size = "?";

            if (model != null)
            {
                Renderer probe = model.GetComponentInChildren<Renderer>();
                if (probe != null) size = probe.bounds.size.ToString("F2");
            }

            Debug.Log("TRUCK " + System.IO.Path.GetFileName(path) + " — 크기 " + size
                      + " · 머티리얼 [" + string.Join(", ", found) + "]");
        }

        // 무엇에 맞출 것인가. 이 게임의 차가 기준입니다.
        GameObject car = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/_Project/05.Prefabs/Player/PlayerCar.prefab");

        if (car != null)
        {
            Bounds box = new Bounds();
            bool first = true;

            foreach (Renderer part in car.GetComponentsInChildren<Renderer>(true))
            {
                if (part is ParticleSystemRenderer || part is TrailRenderer) continue;

                if (first) { box = part.bounds; first = false; }
                else box.Encapsulate(part.bounds);
            }

            if (!first) Debug.Log("TRUCK 플레이어 차 크기 — " + box.size.ToString("F2"));
        }

        EditorApplication.Exit(0);
    }

    /// <summary>임포터를 맞추고 툰 머티리얼로 갈아 끼웁니다.</summary>
    public static void Run()
    {
        Shader toon = Shader.Find(ToonShader);
        Shader glass = Shader.Find(GlassShader);

        if (toon == null || glass == null)
        {
            Debug.Log("TRUCK ⚠ 셰이더를 못 찾았습니다 — "
                      + (toon == null ? ToonShader : GlassShader));
            EditorApplication.Exit(1);
            return;
        }

        Dictionary<string, Material> made = new Dictionary<string, Material>();
        int remapped = 0;

        foreach (string path in Models())
        {
            ModelImporter importer = AssetImporter.GetAtPath(path) as ModelImporter;

            if (importer == null)
            {
                Debug.Log("TRUCK ⚠ 임포터를 못 찾았습니다 — " + path);
                continue;
            }

            // ⚠ InPrefab 입니다. External 로 두면 아래 remap 이 전부 덮어써집니다.
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            importer.materialLocation = ModelImporterMaterialLocation.InPrefab;

            // ── 크기 ──
            //
            // Kenney FBX 는 이미 미터 단위이지만 <b>장난감 크기</b>입니다 —
            // 트럭이 2.90 m 로 들어오는데 이 게임의 승용차가 <b>5.82 m</b> 입니다.
            // 그대로 두면 트럭이 세단의 절반입니다.
            //
            // 2.1 배로 키우면 6.09 m 가 되어 차보다 조금 길고 2.31 m 로 높습니다.
            // ⚠ <b>비례는 못 고칩니다.</b> Kenney 의 비례는 폭/길이가 0.52 인데
            // 실제 트럭은 0.37 입니다. 균일 배율로는 넓적한 채로 커질 뿐이고,
            // 그것이 이 팩의 그림체입니다.
            importer.useFileScale = true;
            importer.globalScale = 2.1f;

            importer.importNormals = ModelImporterNormals.Import;
            importer.importTangents = ModelImporterTangents.None;
            importer.importAnimation = false;
            importer.importCameras = false;
            importer.importLights = false;

            // ⚠ <b>이름은 파일에서 읽습니다.</b> 추측해서 지정하면 어긋난 remap 이
            // 조용히 무시되어 <b>흰 트럭</b>이 나옵니다.
            foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(path))
            {
                Material inside = asset as Material;
                if (inside == null) continue;

                Material mine = Mine(inside.name, toon, glass, made);
                if (mine == null) continue;

                importer.AddRemap(
                    new AssetImporter.SourceAssetIdentifier(typeof(Material), inside.name), mine);
                remapped++;
            }

            importer.SaveAndReimport();
            Debug.Log("TRUCK 반입 — " + System.IO.Path.GetFileName(path));
        }

        AssetDatabase.SaveAssets();
        Debug.Log("TRUCK 끝 — 머티리얼 " + made.Count + " 종 · 짝지은 자리 " + remapped);
        EditorApplication.Exit(0);
    }

    // --- Private Methods ---

    /// <summary>그 이름에 쓸 툰 머티리얼입니다. 없으면 만듭니다.</summary>
    /// <param name="fbxName">FBX 안의 머티리얼 이름</param>
    /// <param name="toon">툰 셰이더</param>
    /// <param name="glass">유리 셰이더</param>
    /// <param name="cache">이번 실행에서 만든 것들</param>
    private static Material Mine(string fbxName, Shader toon, Shader glass,
                                Dictionary<string, Material> cache)
    {
        // ⚠ <b>이름에 확장자가 붙어 오는 경우가 있습니다</b>(색판을 쓰는 팩이 그렇습니다).
        // 앞부분만 맞춰 봅니다.
        string key = fbxName;
        int dot = key.LastIndexOf('.');
        if (dot > 0) key = key.Substring(0, dot);

        Color colour = new Color(0.45f, 0.45f, 0.45f);
        bool known = false;

        foreach ((string name, Color value) in Palette)
        {
            if (!string.Equals(name, key, System.StringComparison.OrdinalIgnoreCase)) continue;

            colour = value;
            known = true;
            break;
        }

        if (!known)
        {
            Debug.Log("TRUCK ⚠ 모르는 머티리얼 이름 — " + fbxName + " (회색으로 둡니다)");
        }

        if (cache.TryGetValue(key, out Material had)) return had;

        // 창은 유리 셰이더를 씁니다. 나머지는 툰입니다.
        Shader want = string.Equals(key, "window", System.StringComparison.OrdinalIgnoreCase)
                      ? glass : toon;

        string path = MaterialDir + "/Truck_" + key + ".mat";
        Material found = AssetDatabase.LoadAssetAtPath<Material>(path);

        if (found == null)
        {
            found = new Material(want) { name = "Truck_" + key };
            found.SetColor("_BaseColor", colour);
            AssetDatabase.CreateAsset(found, path);
        }
        else
        {
            // ⚠ <b>이미 있는 것의 셰이더도 맞춰 둡니다.</b> 안 그러면 한 번 불투명으로
            // 만들어진 창이 이 도구를 다시 돌려도 <b>불투명인 채로</b> 색만 바뀝니다.
            if (found.shader != want) found.shader = want;

            found.SetColor("_BaseColor", colour);
            EditorUtility.SetDirty(found);
        }

        cache[key] = found;
        return found;
    }

    private static string[] Models()
    {
        return new[]
        {
            ModelDir + "/truck.fbx",
            ModelDir + "/truckFlat.fbx",
            ModelDir + "/wheelTruck.fbx",
            ModelDir + "/wheelDefault.fbx",
        };
    }
}
