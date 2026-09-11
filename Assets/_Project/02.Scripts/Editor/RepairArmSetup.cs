using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using CarDrive.Gameplay;

/// <summary>
/// 정비 팔을 만들고 <b>마을에</b> 놓습니다.
///
/// <b>왜 도구가 만드는가.</b> 이 기계는 모델이 없습니다. 그런데 기획이 적어 둔 대로
/// "받침대에 <see cref="RobotTurret"/> 을 올리면 끝" 이라, 상자 몇 개면 형태가
/// 나옵니다. 손으로 만들면 다음에 무엇이 들어 있었는지 알 수 없으므로 여기 적습니다.
///
/// <code>
/// Unity.exe -batchmode -nographics -projectPath . -executeMethod RepairArmSetup.Run
/// </code>
/// </summary>
public static class RepairArmSetup
{
    private const string ScenePath = "Assets/_Project/01.Scenes/SampleScene.unity";
    private const string PrefabPath = "Assets/_Project/05.Prefabs/Robot/RepairArm.prefab";
    private const string SteelPath = "Assets/_Project/04.Art/00.Materials/RobotSteel.mat";
    private const string DarkPath = "Assets/_Project/04.Art/00.Materials/RobotDark.mat";
    private const string WorldRoot = "--- World ---";

    /// <summary>상호작용 레이어입니다. 이것이 아니면 <b>조준점이 안 걸립니다.</b></summary>
    private const int InteractableLayer = 6;

    /// <summary>땅 레이어입니다.</summary>
    private const int GroundLayer = 11;

    public static void Run()
    {
        GameObject prefab = Build();

        if (prefab == null)
        {
            EditorApplication.Exit(1);
            return;
        }

        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        Transform holder = Holder();

        if (holder.Find(prefab.name) != null)
        {
            Debug.Log("REPAIR 이미 놓여 있습니다");
            EditorApplication.Exit(0);
            return;
        }

        // 마을 한가운데가 아니라 <b>가장자리</b>에 둡니다. 한복판은 상점과 침대가
        // 쓰고 있고, 차를 대야 하는 기계라 자리가 필요합니다.
        WorldStreamer streamer = Object.FindAnyObjectByType<WorldStreamer>(
            FindObjectsInactive.Include);

        Vector3 middle = streamer != null && streamer.origin != null
                         ? streamer.origin.position
                         : Vector3.zero;

        float out_ = streamer != null ? streamer.villageRadius * 0.45f : 30f;
        Vector3 want = middle + new Vector3(out_, 0f, -out_ * 0.35f);

        if (!Ground(want, out Vector3 on))
        {
            Debug.Log("REPAIR ⚠ 마을에서 땅을 못 찾았습니다 — " + want.ToString("F0"));
            EditorApplication.Exit(1);
            return;
        }

        GameObject made = (GameObject)PrefabUtility.InstantiatePrefab(prefab, holder);
        made.transform.position = on;

        // 마을 한가운데를 보게 세웁니다.
        Vector3 inward = middle - on;
        inward.y = 0f;
        if (inward.sqrMagnitude > 0.01f) made.transform.rotation = Quaternion.LookRotation(inward);

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();

        Debug.Log("REPAIR 놓았습니다 — " + on.ToString("F0") + " · 마을 중심에서 "
                  + Vector3.Distance(on, middle).ToString("F0") + " m");
        EditorApplication.Exit(0);
    }

    // --- Private Methods ---

    /// <summary>프리팹을 만듭니다. 이미 있으면 그것을 씁니다.</summary>
    private static GameObject Build()
    {
        GameObject had = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (had != null) return had;

        Material steel = AssetDatabase.LoadAssetAtPath<Material>(SteelPath);
        Material dark = AssetDatabase.LoadAssetAtPath<Material>(DarkPath);

        GameObject root = new GameObject("RepairArm");
        root.layer = InteractableLayer;

        // ── 받침대 ──
        GameObject bed = Box("Bed", root.transform, new Vector3(0f, 0.35f, 0f),
                             new Vector3(1.6f, 0.7f, 1.6f), dark);

        // ⚠ <b>콜라이더는 뿌리에 답니다.</b> 조각마다 달면 조준점이 어느 것에 걸리느냐에
        // 따라 부모 탐색이 달라집니다. 하나만 두면 늘 같은 것이 잡힙니다.
        Object.DestroyImmediate(bed.GetComponent<Collider>());

        BoxCollider touch = root.AddComponent<BoxCollider>();
        touch.center = new Vector3(0f, 0.9f, 0f);
        touch.size = new Vector3(1.8f, 1.8f, 1.8f);

        // ── 도는 기둥 ──
        GameObject yaw = new GameObject("Yaw");
        yaw.transform.SetParent(root.transform, false);
        yaw.transform.localPosition = new Vector3(0f, 0.7f, 0f);

        GameObject post = Box("Post", yaw.transform, new Vector3(0f, 0.45f, 0f),
                              new Vector3(0.45f, 0.9f, 0.45f), steel);
        Object.DestroyImmediate(post.GetComponent<Collider>());

        // ── 굽는 팔 ──
        GameObject pitch = new GameObject("Pitch");
        pitch.transform.SetParent(yaw.transform, false);
        pitch.transform.localPosition = new Vector3(0f, 0.9f, 0f);

        GameObject boom = Box("Boom", pitch.transform, new Vector3(0f, 0f, 1.15f),
                              new Vector3(0.22f, 0.22f, 2.3f), steel);
        Object.DestroyImmediate(boom.GetComponent<Collider>());

        GameObject headPart = Box("Head", pitch.transform, new Vector3(0f, 0f, 2.35f),
                                  new Vector3(0.4f, 0.4f, 0.4f), dark);
        Object.DestroyImmediate(headPart.GetComponent<Collider>());

        Transform tip = new GameObject("Tip").transform;
        tip.SetParent(pitch.transform, false);
        tip.localPosition = new Vector3(0f, 0f, 2.6f);

        // ── 부품 ──
        RobotTurret turret = root.AddComponent<RobotTurret>();
        turret.yawNode = yaw.transform;
        turret.pitchNode = pitch.transform;
        turret.muzzle = tip;
        turret.yawRange = new Vector2(-160f, 160f);

        // 팔은 아래를 봅니다. 차는 바닥에 있습니다.
        turret.pitchRange = new Vector2(-60f, 20f);
        turret.restPose = new Vector2(0f, -20f);

        RepairArm arm = root.AddComponent<RepairArm>();
        arm.arm = turret;

        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
        {
            t.gameObject.layer = InteractableLayer;
        }

        GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);

        if (saved == null)
        {
            Debug.Log("REPAIR ⚠ 프리팹을 만들지 못했습니다 — " + PrefabPath);
            return null;
        }

        Debug.Log("REPAIR 프리팹을 만들었습니다 — " + PrefabPath);
        return saved;
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
    private static Transform Holder()
    {
        GameObject world = GameObject.Find(WorldRoot);
        Transform under = world != null ? world.transform : null;

        Transform had = under != null ? under.Find("Village") : null;
        if (had != null) return had;

        GameObject made = new GameObject("Village");
        made.transform.SetParent(under, true);
        return made.transform;
    }
}
