using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using CarDrive.Gameplay;

/// <summary>
/// 버려진 트럭을 <b>길가에</b> 놓습니다. 시드를 고정해 늘 같은 자리입니다.
///
/// <b>왜 길가인가.</b> 이 세계는 사람이 떠난 뒤입니다. 길 위에 멈춘 채 남은 차는
/// 그 사실을 <b>말 없이</b> 말합니다. 그리고 밤 주행에서 그것은 <b>지형지물</b>이
/// 됩니다 — 헤드라이트에 먼저 걸리는 것이 있어야 길이 읽힙니다.
///
/// ⚠ <b>레이어 0(Default) 에 놓습니다.</b> 레이어 9(Prop)는 Ground 하고만 부딪혀서,
/// 그쪽에 두면 <b>차가 트럭을 통과합니다.</b> 건물이 레이어 0 을 쓰는 것과 같은
/// 이유입니다. 이 게임에서 소품은 벽이 아닙니다.
///
/// <code>
/// Unity.exe -batchmode -nographics -projectPath . -executeMethod TruckPlacer.Run
/// </code>
/// </summary>
public static class TruckPlacer
{
    private const string ScenePath = "Assets/_Project/01.Scenes/SampleScene.unity";
    private const string HolderName = "Wrecks";
    private const string WorldRoot = "--- World ---";
    private const string ModelDir =
        "Packages/com.toflaks.vendor.kenney-car-kit/CarKit/Models";

    /// <summary>부딪히는 레이어입니다. 건물과 같습니다.</summary>
    private const int SolidLayer = 0;

    /// <summary>땅 레이어입니다.</summary>
    private const int GroundLayer = 11;

    private const int LayoutSeed = 20260911;

    /// <summary>길 하나가 받는 잔해 수입니다.</summary>
    private const int PerRoad = 3;

    public static void Run()
    {
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        GameObject[] kinds =
        {
            AssetDatabase.LoadAssetAtPath<GameObject>(ModelDir + "/truck.fbx"),
            AssetDatabase.LoadAssetAtPath<GameObject>(ModelDir + "/truckFlat.fbx"),
        };

        if (kinds[0] == null || kinds[1] == null)
        {
            Debug.Log("TRUCK ⚠ 모델을 못 찾았습니다 — " + ModelDir);
            EditorApplication.Exit(1);
            return;
        }

        Transform holder = Holder();

        int swept = holder.childCount;
        for (int i = holder.childCount - 1; i >= 0; i--)
        {
            Object.DestroyImmediate(holder.GetChild(i).gameObject);
        }

        WorldStreamer streamer = Object.FindAnyObjectByType<WorldStreamer>(
            FindObjectsInactive.Include);

        if (streamer == null)
        {
            Debug.Log("TRUCK ⚠ WorldStreamer 가 없어 길을 모릅니다");
            EditorApplication.Exit(1);
            return;
        }

        // ⚠ 시드를 고정하고 되돌립니다. WorldStreamer 가 하는 그대로입니다.
        Random.State was = Random.state;
        Random.InitState(LayoutSeed);

        int placed = 0;

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

                for (int i = 0; i < PerRoad; i++)
                {
                    // 길을 따라 고르게 흩되, 마을 바로 앞은 비웁니다.
                    float along = Mathf.Lerp(length * 0.2f, length * 0.85f,
                                             (i + Random.value * 0.6f) / PerRoad);

                    // ⚠ <b>길 한복판에 두지 않습니다.</b> 갓길에 세워야 지나갈 수 있고,
                    // 지나갈 수 있어야 지형지물이지 벽이 아닙니다.
                    float off = Random.Range(7f, 13f) * (Random.value > 0.5f ? 1f : -1f);

                    Vector3 want = middle + road.startOffset + way * along + side * off;

                    if (!Ground(want, out Vector3 on))
                    {
                        Debug.Log("TRUCK ⚠ 땅을 못 찾아 건너뜀 — " + want.ToString("F0"));
                        continue;
                    }

                    if (Put(kinds[Random.Range(0, kinds.Length)], holder, on, way,
                            road.displayName, placed))
                    {
                        placed++;
                    }
                }
            }
        }
        finally
        {
            Random.state = was;
        }

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();

        Debug.Log("TRUCK 놓았습니다 — " + placed + " 대 (지운 것 " + swept + ") · 시드 "
                  + LayoutSeed);
        EditorApplication.Exit(0);
    }

    /// <summary>
    /// 놓인 잔해를 한 장 찍습니다. ⚠ <c>-nographics</c> 를 붙이면 안 됩니다.
    /// </summary>
    public static void Shot()
    {
        EditorSettings.asyncShaderCompilation = false;
        ShaderUtil.allowAsyncCompilation = false;

        System.IO.Directory.CreateDirectory("Logs/Truck");
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        Transform holder = Holder();

        if (holder.childCount == 0)
        {
            Debug.Log("TRUCK ⚠ 놓인 것이 없습니다");
            EditorApplication.Exit(1);
            return;
        }

        Camera camera = new GameObject("TruckCamera").AddComponent<Camera>();

        try
        {
            Transform one = holder.GetChild(0);
            Bounds box = Box(one);

            float reach = Mathf.Max(box.extents.magnitude * 2.4f, 6f);
            camera.transform.position = box.center
                                        + new Vector3(reach * 0.75f, reach * 0.4f, reach * 0.75f);
            camera.transform.LookAt(box.center);

            RenderTexture target = new RenderTexture(1280, 720, 24, RenderTextureFormat.ARGB32);
            Texture2D shot = new Texture2D(1280, 720, TextureFormat.RGB24, false);

            camera.targetTexture = target;

            for (int i = 0; i < 3; i++)
            {
                camera.Render();
                while (ShaderUtil.anythingCompiling) System.Threading.Thread.Sleep(50);
            }

            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = target;
            shot.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
            shot.Apply();
            RenderTexture.active = previous;

            System.IO.File.WriteAllBytes("Logs/Truck/wreck.png", shot.EncodeToPNG());

            camera.targetTexture = null;
            Object.DestroyImmediate(shot);
            target.Release();
            Object.DestroyImmediate(target);

            Debug.Log("TRUCK 찍었습니다 — Logs/Truck/wreck.png · " + one.name);
        }
        finally
        {
            Object.DestroyImmediate(camera.gameObject);
        }

        EditorApplication.Exit(0);
    }

    // --- Private Methods ---

    /// <summary>한 대를 놓고 부딪히게 만듭니다.</summary>
    private static bool Put(GameObject prefab, Transform holder, Vector3 on, Vector3 way,
                            string road, int index)
    {
        GameObject made = (GameObject)PrefabUtility.InstantiatePrefab(prefab, holder);
        made.name = prefab.name + "_" + road + "_" + index;
        made.transform.position = on;

        // ⚠ <b>레이어를 바꿔야 합니다.</b> FBX 는 Default 로 들어오지만 자식까지
        // 확실히 맞춰 둡니다 — 하나라도 빠지면 그 부분만 통과합니다.
        foreach (Transform t in made.GetComponentsInChildren<Transform>(true))
        {
            t.gameObject.layer = SolidLayer;
        }

        // ── 부딪히는 몸 ──
        //
        // ⚠ <b>돌리기 전에 잽니다.</b> 처음에는 돌린 뒤 월드 AABB 를 재서 로컬 상자로
        // 썼는데, 축에 정렬된 상자를 기울어진 물체에 끼우는 꼴이라 <b>실제보다 큽니다.</b>
        // 높이 2.31 m 짜리 트럭의 몸이 3.0 m 로 잡혔고, 그만큼 차가 <b>허공에</b>
        // 부딪힙니다. 안 돌린 상태의 월드 AABB 는 기울어진 상자와 정확히 같습니다.
        made.transform.rotation = Quaternion.identity;

        Bounds box = Box(made.transform);
        Vector3 scale = made.transform.lossyScale;

        BoxCollider solid = made.AddComponent<BoxCollider>();
        solid.center = made.transform.InverseTransformPoint(box.center);
        solid.size = new Vector3(
            box.size.x / Mathf.Max(Mathf.Abs(scale.x), 0.001f),
            box.size.y / Mathf.Max(Mathf.Abs(scale.y), 0.001f),
            box.size.z / Mathf.Max(Mathf.Abs(scale.z), 0.001f));

        // 버려진 것이므로 길과 나란하지 않습니다. 상자를 다 만든 뒤에 돌립니다.
        made.transform.rotation = Quaternion.LookRotation(way)
                                  * Quaternion.Euler(Random.Range(-3f, 3f),
                                                     Random.Range(-40f, 40f),
                                                     Random.Range(-4f, 4f));

        Debug.Log("TRUCK 놓음 — " + made.name + " · " + on.ToString("F0")
                  + " · 몸 " + box.size.ToString("F1"));
        return true;
    }

    /// <summary>그 물체가 차지하는 자리입니다.</summary>
    private static Bounds Box(Transform root)
    {
        Renderer[] parts = root.GetComponentsInChildren<Renderer>();
        if (parts.Length == 0) return new Bounds(root.position, Vector3.one);

        Bounds box = parts[0].bounds;
        for (int i = 1; i < parts.Length; i++) box.Encapsulate(parts[i].bounds);

        return box;
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

    private static Transform Holder()
    {
        GameObject world = GameObject.Find(WorldRoot);
        Transform under = world != null ? world.transform : null;

        Transform had = under != null ? under.Find(HolderName) : null;
        if (had != null) return had;

        GameObject made = new GameObject(HolderName);
        made.transform.SetParent(under, true);
        return made.transform;
    }
}
