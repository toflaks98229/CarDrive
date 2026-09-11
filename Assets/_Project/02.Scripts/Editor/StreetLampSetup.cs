using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using CarDrive.Gameplay;

/// <summary>
/// 길가에 <b>등</b>을 세우고, 그 등을 되살리며 걷는 <b>점등기</b>를 놓습니다.
///
/// <b>왜 도구가 만드는가.</b> 등은 모델이 없습니다. 기둥 하나에 갓 하나면 형태가
/// 나오므로 상자로 세웁니다 — 정비 팔과 같은 방식이고, 손으로 만들면 다음에
/// 무엇이 들어 있었는지 알 수 없으므로 여기 적습니다.
///
/// ⚠ <b>절반쯤은 죽은 채로 놓습니다.</b> 사람이 떠난 세계라 등도 죽습니다.
/// 그리고 죽은 등이 없으면 점등기가 <b>할 일이 없습니다</b> — 그 기계가 존재하는
/// 이유가 죽은 등입니다.
///
/// <code>
/// Unity.exe -batchmode -nographics -projectPath . -executeMethod StreetLampSetup.Run
/// </code>
/// </summary>
public static class StreetLampSetup
{
    private const string ScenePath = "Assets/_Project/01.Scenes/SampleScene.unity";
    private const string PrefabPath = "Assets/_Project/05.Prefabs/Prop/StreetLamp.prefab";
    private const string GlassPath = "Assets/_Project/04.Art/00.Materials/LampGlass.mat";
    private const string SteelPath = "Assets/_Project/04.Art/00.Materials/RobotSteel.mat";
    private const string DarkPath = "Assets/_Project/04.Art/00.Materials/RobotDark.mat";
    private const string ClackPath = "Assets/_Project/06.Sound/Impact/metal_hit_04.ogg";
    private const string FixedPath = "Assets/_Project/06.Sound/Impact/metal_hit_02.ogg";
    private const string WalkerPath =
        "Assets/_Project/05.Prefabs/Robot/WalkerRobot_Nomad.prefab";

    private const string WorldRoot = "--- World ---";
    private const string HolderName = "StreetLamps";
    private const string RobotHolder = "PlacedRobots";
    private const string LighterName = "LampLighter";

    /// <summary>부딪히는 레이어입니다. 기둥은 벽입니다.</summary>
    private const int SolidLayer = 0;

    /// <summary>땅 레이어입니다.</summary>
    private const int GroundLayer = 11;

    private const int LayoutSeed = 20260912;

    /// <summary>등과 등 사이입니다(m).</summary>
    private const float Spacing = 80f;

    /// <summary>길 가운데에서 옆으로 물러선 거리입니다(m).</summary>
    private const float SideOffset = 9f;

    /// <summary>이 비율만큼은 죽은 채로 놓습니다.</summary>
    private const float DeadShare = 0.45f;

    /// <summary>등불의 세기입니다. 아래 <see cref="Tune"/> 에 고른 근거가 있습니다.</summary>
    private const float Strength = 18f;

    public static void Run()
    {
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        GameObject prefab = Build();

        if (prefab == null)
        {
            EditorApplication.Exit(1);
            return;
        }

        WorldStreamer streamer = Object.FindAnyObjectByType<WorldStreamer>(
            FindObjectsInactive.Include);

        if (streamer == null)
        {
            Debug.Log("LAMP ⚠ WorldStreamer 가 없어 길을 모릅니다");
            EditorApplication.Exit(1);
            return;
        }

        Transform holder = Holder(HolderName);

        int swept = holder.childCount;
        for (int i = holder.childCount - 1; i >= 0; i--)
        {
            Object.DestroyImmediate(holder.GetChild(i).gameObject);
        }

        // ⚠ 시드를 고정하고 되돌립니다. WorldStreamer 가 하는 그대로입니다.
        Random.State was = Random.state;
        Random.InitState(LayoutSeed);

        int placed = 0;
        int dead = 0;

        try
        {
            Vector3 middle = streamer.origin != null ? streamer.origin.position
                                                     : streamer.transform.position;

            foreach (WorldRoute road in streamer.routes)
            {
                if (road == null) continue;

                Vector3 way = road.direction.sqrMagnitude > 1e-4f
                              ? road.direction.normalized : Vector3.forward;
                Vector3 side = Vector3.Cross(Vector3.up, way);

                float length = streamer.fallbackTileSize * road.tileCount;

                // 길을 따라 일정한 간격으로 세웁니다. 사람이 세운 것이므로
                // 흩어 놓지 않습니다 — 등은 <b>줄</b>로 서 있어야 길로 읽힙니다.
                for (float along = Spacing; along < length; along += Spacing)
                {
                    // 좌우를 번갈아 세웁니다. 한쪽에만 세우면 반대 차선이 캄캄합니다.
                    float lean = (placed % 2 == 0) ? 1f : -1f;
                    Vector3 want = middle + road.startOffset + way * along
                                   + side * (SideOffset * lean);

                    if (!Ground(want, out Vector3 on)) continue;

                    GameObject made = (GameObject)PrefabUtility.InstantiatePrefab(prefab, holder);
                    made.name = "Lamp_" + road.displayName + "_" + placed;
                    made.transform.position = on;

                    // 갓이 길을 보게 돌립니다.
                    made.transform.rotation = Quaternion.LookRotation(-side * lean);

                    StreetLamp lamp = made.GetComponent<StreetLamp>();

                    if (lamp != null && Random.value < DeadShare)
                    {
                        lamp.broken = true;
                        dead++;
                    }

                    placed++;
                }
            }
        }
        finally
        {
            Random.state = was;
        }

        int robots = Lighter(streamer);

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();

        Debug.Log("LAMP 세웠습니다 — " + placed + " 개 (죽은 것 " + dead + " · 지운 것 "
                  + swept + ") · 간격 " + Spacing + " m · 시드 " + LayoutSeed);
        Debug.Log("LAMP 점등기 " + robots + " 대");

        EditorApplication.Exit(0);
    }

    // --- Private Methods ---

    /// <summary>등 프리팹을 만듭니다. 이미 있으면 그것을 씁니다.</summary>
    private static GameObject Build()
    {
        GameObject had = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (had != null) return Dark(had);

        Material steel = AssetDatabase.LoadAssetAtPath<Material>(SteelPath);
        Material dark = AssetDatabase.LoadAssetAtPath<Material>(DarkPath);
        Material glass = Glass();

        GameObject root = new GameObject("StreetLamp");
        root.layer = SolidLayer;

        // ── 기둥 ──
        //
        // ⚠ <b>부딪힙니다.</b> 길가에 선 기둥은 피해야 하는 것입니다. 통과하면
        // 길이 넓어 보이고, 등이 <b>지형지물</b>이 아니라 그림이 됩니다.
        GameObject post = Box("Post", root.transform, new Vector3(0f, 2.6f, 0f),
                              new Vector3(0.22f, 5.2f, 0.22f), steel);
        Object.DestroyImmediate(post.GetComponent<Collider>());

        BoxCollider hit = root.AddComponent<BoxCollider>();
        hit.center = new Vector3(0f, 2.6f, 0f);
        hit.size = new Vector3(0.35f, 5.2f, 0.35f);

        // ── 팔 ──
        GameObject arm = Box("Arm", root.transform, new Vector3(0f, 5.1f, 0.7f),
                             new Vector3(0.16f, 0.16f, 1.4f), steel);
        Object.DestroyImmediate(arm.GetComponent<Collider>());

        // ── 갓 ──
        GameObject hood = Box("Hood", root.transform, new Vector3(0f, 5.0f, 1.35f),
                              new Vector3(0.7f, 0.22f, 0.7f), dark);
        Object.DestroyImmediate(hood.GetComponent<Collider>());

        // ── 알 ──
        GameObject bulb = Box("Glass", root.transform, new Vector3(0f, 4.82f, 1.35f),
                              new Vector3(0.5f, 0.16f, 0.5f), glass);
        Object.DestroyImmediate(bulb.GetComponent<Collider>());

        // ── 빛 ──
        GameObject lightHolder = new GameObject("Bulb");
        lightHolder.transform.SetParent(root.transform, false);
        lightHolder.transform.localPosition = new Vector3(0f, 4.7f, 1.35f);

        Light light = lightHolder.AddComponent<Light>();
        light.type = LightType.Point;
        Tune(light);

        StreetLamp lamp = root.AddComponent<StreetLamp>();
        lamp.bulb = light;
        lamp.glass = bulb.GetComponent<Renderer>();
        lamp.reach = 18f;
        lamp.clack = AssetDatabase.LoadAssetAtPath<AudioClip>(ClackPath);

        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
        {
            t.gameObject.layer = SolidLayer;
        }

        GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);

        if (saved == null)
        {
            Debug.Log("LAMP ⚠ 프리팹을 만들지 못했습니다 — " + PrefabPath);
            return null;
        }

        Debug.Log("LAMP 프리팹을 만들었습니다 — " + PrefabPath);
        return saved;
    }

    /// <summary>
    /// 프리팹의 빛을 <b>꺼 둡니다.</b>
    ///
    /// ⚠ <b>켜진 채로 저장하면 낮에도 켜져 있습니다.</b> 등을 켜고 끄는 것은
    /// <see cref="StreetLamp.Update"/> 인데, 그것은 <b>재생 중에만</b> 돕니다.
    /// 그래서 켜진 채로 구워 두면 에디터의 씬 뷰에서도, 룩 캡처에서도 등 열여섯 개가
    /// 대낮에 타고 있습니다 — 실제로 한밤 운전석 밝기가 0.006 에서 0.111 로 뛰었습니다.
    ///
    /// 꺼진 것이 <b>기본 상태</b>입니다. 켜는 일은 밤이 합니다.
    /// </summary>
    /// <param name="prefab">고칠 프리팹</param>
    /// <returns>같은 프리팹</returns>
    private static GameObject Dark(GameObject prefab)
    {
        GameObject copy = PrefabUtility.LoadPrefabContents(PrefabPath);

        try
        {
            Light inside = copy.GetComponentInChildren<Light>(true);
            if (inside != null) Tune(inside);

            PrefabUtility.SaveAsPrefabAsset(copy, PrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(copy);
        }

        Debug.Log("LAMP 프리팹의 빛을 손봤습니다 — 꺼진 채, 세기 " + Strength);

        return AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
    }

    /// <summary>
    /// 등의 빛을 맞춥니다. <b>도구가 매번 다시 맞춥니다</b> — 값이 프리팹에만 남아
    /// 있으면 다음에 왜 그 값인지 알 수 없습니다.
    ///
    /// <b>세기는 재서 골랐습니다.</b> 한밤에 등 아래 땅의 밝기를 재 보면
    /// 3 → 0.07, 20 → 0.18, 80 → 0.39, 300 → 0.74 입니다. 세계의 한밤 평균이
    /// 0.024 이므로 18 쯤이 <b>웅덩이가 보이되 타지 않는</b> 자리입니다.
    /// </summary>
    /// <param name="light">맞출 빛</param>
    private static void Tune(Light light)
    {
        light.range = 18f;
        light.intensity = Strength;
        light.color = new Color(1f, 0.86f, 0.62f);
        light.shadows = LightShadows.None;

        // ⚠ <b>꺼진 채로 저장합니다.</b> 켜고 끄는 것은 재생 중에만 도는
        // <see cref="StreetLamp.Update"/> 입니다. 켜진 채로 구워 두면 에디터의
        // 씬 뷰에서도, 룩 캡처에서도 등이 대낮에 타고 있습니다 — 실제로 한밤
        // 운전석 밝기가 0.006 에서 0.111 로 뛰었습니다.
        light.enabled = false;
    }

    /// <summary>등갓 안쪽 재질입니다. 꺼져 있을 때가 기본이라 방출은 검정입니다.</summary>
    private static Material Glass()
    {
        Material had = AssetDatabase.LoadAssetAtPath<Material>(GlassPath);
        if (had != null) return had;

        Shader toon = Shader.Find("CarDrive/Toon Lit");
        if (toon == null) return null;

        Material made = new Material(toon) { name = "LampGlass" };
        made.SetColor("_BaseColor", new Color(0.55f, 0.5f, 0.4f));

        // ⚠ <b>켜짐은 등이 정합니다.</b> 여기서 방출을 켜 두면 죽은 등도 빛납니다.
        made.SetColor("_EmissionColor", Color.black);

        AssetDatabase.CreateAsset(made, GlassPath);
        return made;
    }

    /// <summary>점등기를 길 위에 놓습니다.</summary>
    /// <param name="streamer">길을 아는 것</param>
    /// <returns>놓은 수</returns>
    private static int Lighter(WorldStreamer streamer)
    {
        GameObject walker = AssetDatabase.LoadAssetAtPath<GameObject>(WalkerPath);

        if (walker == null)
        {
            Debug.Log("LAMP ⚠ 보행기를 못 찾아 점등기를 못 놓습니다 — " + WalkerPath);
            return 0;
        }

        Transform holder = Holder(RobotHolder);

        Transform had = holder.Find(LighterName);
        if (had != null) Object.DestroyImmediate(had.gameObject);

        Vector3 middle = streamer.origin != null ? streamer.origin.position
                                                 : streamer.transform.position;

        // 마을에서 조금 나간 길 위입니다. 마을 안에 두면 <b>길의</b> 등을 고치러
        // 나가는 데 한참 걸립니다.
        WorldRoute road = streamer.routes != null && streamer.routes.Count > 0
                          ? streamer.routes[0] : null;

        Vector3 way = road != null && road.direction.sqrMagnitude > 1e-4f
                      ? road.direction.normalized : Vector3.forward;

        Vector3 want = middle + (road != null ? road.startOffset : Vector3.zero) + way * 120f;

        if (!Ground(want, out Vector3 on))
        {
            Debug.Log("LAMP ⚠ 길에서 땅을 못 찾아 점등기를 못 놓습니다");
            return 0;
        }

        GameObject made = (GameObject)PrefabUtility.InstantiatePrefab(walker, holder);
        made.name = LighterName;
        made.transform.position = on;
        made.transform.rotation = Quaternion.LookRotation(way);

        LampLighter lighter = made.GetComponent<LampLighter>();
        if (lighter == null) lighter = made.AddComponent<LampLighter>();

        lighter.driver = made.GetComponentInChildren<RobotDriver>(true);
        lighter.arm = made.GetComponentInChildren<RobotTurret>(true);
        lighter.fixedSound = AssetDatabase.LoadAssetAtPath<AudioClip>(FixedPath);

        // ⚠ <b>비무장입니다.</b> 지키고 싶어져야 하는 기계가 총을 들고 있으면
        // 그 뜻이 흐려집니다.
        foreach (RobotWeapon gun in made.GetComponentsInChildren<RobotWeapon>(true))
        {
            gun.enabled = false;
        }

        foreach (RobotCombatant fighter in made.GetComponentsInChildren<RobotCombatant>(true))
        {
            fighter.enabled = false;
        }

        Debug.Log("LAMP 점등기를 놓았습니다 — " + on.ToString("F0") + " · 걷기 "
                  + (lighter.driver != null ? "있음" : "없음"));

        return 1;
    }

    /// <summary>상자 하나입니다.</summary>
    private static GameObject Box(string name, Transform parent, Vector3 at,
                                  Vector3 size, Material material)
    {
        GameObject made = GameObject.CreatePrimitive(PrimitiveType.Cube);
        made.name = name;
        made.transform.SetParent(parent, false);
        made.transform.localPosition = at;
        made.transform.localScale = size;

        if (material != null) made.GetComponent<MeshRenderer>().sharedMaterial = material;

        return made;
    }

    /// <summary>그 자리의 땅입니다.</summary>
    private static bool Ground(Vector3 at, out Vector3 on)
    {
        on = at;

        if (!Physics.Raycast(at + Vector3.up * 200f, Vector3.down, out RaycastHit hit, 600f,
                             1 << GroundLayer, QueryTriggerInteraction.Ignore))
        {
            return false;
        }

        on = hit.point;
        return true;
    }

    /// <summary>놓을 곳입니다.</summary>
    /// <param name="name">폴더 이름</param>
    private static Transform Holder(string name)
    {
        GameObject world = GameObject.Find(WorldRoot);
        Transform under = world != null ? world.transform : null;

        Transform had = under != null ? under.Find(name) : null;
        if (had != null) return had;

        GameObject loose = GameObject.Find(name);
        if (loose != null) return loose.transform;

        GameObject made = new GameObject(name);
        made.transform.SetParent(under, true);

        return made.transform;
    }
}
