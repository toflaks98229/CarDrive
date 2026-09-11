using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using CarDrive.Gameplay;

/// <summary>
/// 로봇을 <b>장소에 놓습니다.</b> 시드를 고정해 늘 같은 자리에 놓입니다.
///
/// <b>왜 스포너가 아닌가.</b> 기획이 못 박아 두었습니다 —
/// "로봇은 <b>장소에 속합니다.</b> 씨앗으로 배치하지, 차 뒤에 뽑아내지 않습니다.
/// 이것이 <c>GhostSpawner</c> 를 흉내 내면 안 되는 이유입니다."
/// 귀신은 플레이어를 따라다니지만 기계는 <b>거기 있던 것</b>이어야 합니다.
/// 그래야 "아홉 시에 저 길을 지나간다" 를 외울 수 있습니다.
///
/// <b>어디에 무엇을 놓는가</b>는 "넷의 자리" 가 정합니다.
/// 드레드노트는 <b>현장</b>을 지키고, 스트라이더는 <b>길</b>을 걷습니다.
///
/// <code>
/// Unity.exe -batchmode -nographics -projectPath . -executeMethod RobotPlacer.Survey
/// Unity.exe -batchmode -nographics -projectPath . -executeMethod RobotPlacer.Place
/// </code>
/// </summary>
public static class RobotPlacer
{
    private const string ScenePath = "Assets/_Project/01.Scenes/SampleScene.unity";
    private const string HolderName = "PlacedRobots";
    private const string WorldRoot = "--- World ---";

    private const string DreadnoughtPath =
        "Assets/_Project/05.Prefabs/Robot/WalkerRobot_Dreadnought.prefab";
    private const string StriderPath =
        "Assets/_Project/05.Prefabs/Robot/WalkerRobot_Strider.prefab";

    /// <summary>
    /// 배치 시드입니다.
    ///
    /// <see cref="WorldStreamer.layoutSeed"/> 와 같은 규칙 — 이 숫자가 같으면
    /// <b>같은 세계</b>가 나옵니다. 바꾸면 로봇이 전부 다른 자리에 섭니다.
    /// </summary>
    private const int LayoutSeed = 20260911;

    /// <summary>현장 하나가 받는 파수꾼 수입니다.</summary>
    private const int GuardsPerSite = 1;

    // --- Public Methods ---

    /// <summary>무엇이 있고 어디에 놓을 수 있는지만 봅니다. 씬을 안 바꿉니다.</summary>
    public static void Survey()
    {
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        foreach (WorldLocation place in Object.FindObjectsByType<WorldLocation>(
                     FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            Debug.Log("ROBOTS 장소 — " + place.displayName + " · " + place.kind
                      + " · 반경 " + place.radius.ToString("F0") + " m · "
                      + place.transform.position.ToString("F0"));
        }

        int already = 0;
        foreach (RobotDriver had in Object.FindObjectsByType<RobotDriver>(
                     FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            Debug.Log("ROBOTS 이미 있음 — " + had.name + " · "
                      + had.transform.position.ToString("F0"));
            already++;
        }

        Debug.Log("ROBOTS 훑기 끝 — 이미 선 로봇 " + already + " 마리");
        EditorApplication.Exit(0);
    }

    /// <summary>장소마다 로봇을 놓습니다.</summary>
    public static void Place()
    {
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        GameObject dreadnought = AssetDatabase.LoadAssetAtPath<GameObject>(DreadnoughtPath);
        GameObject strider = AssetDatabase.LoadAssetAtPath<GameObject>(StriderPath);

        if (dreadnought == null || strider == null)
        {
            Debug.Log("ROBOTS ⚠ 프리팹을 못 찾았습니다");
            EditorApplication.Exit(1);
            return;
        }

        Transform holder = Holder();

        // ⚠ <b>우리가 놓은 것만 지웁니다.</b> 손으로 놓아 둔 로봇까지 쓸면
        // 씬에서 맞춰 둔 것이 사라집니다.
        int swept = holder.childCount;
        for (int i = holder.childCount - 1; i >= 0; i--)
        {
            Object.DestroyImmediate(holder.GetChild(i).gameObject);
        }

        // ⚠ <b>시드를 고정하고 되돌립니다.</b> <c>WorldStreamer</c> 가 하는 그대로입니다.
        // 안 되돌리면 이 도구를 돌린 뒤의 모든 난수가 달라집니다.
        Random.State was = Random.state;
        Random.InitState(LayoutSeed);

        int guards = 0;
        int walkers = 0;

        try
        {
            List<WorldLocation> places = new List<WorldLocation>(
                Object.FindObjectsByType<WorldLocation>(FindObjectsInactive.Include,
                                                        FindObjectsSortMode.None));

            // 같은 시드에서 같은 결과가 나오려면 <b>순서가 고정</b>이어야 합니다.
            // FindObjects 의 순서는 보장되지 않으므로 이름으로 세웁니다.
            places.Sort((a, b) => string.CompareOrdinal(a.displayName + a.name,
                                                        b.displayName + b.name));

            foreach (WorldLocation place in places)
            {
                if (place.kind != LocationKind.Site) continue;

                for (int i = 0; i < GuardsPerSite; i++)
                {
                    // 파수꾼은 <b>현장 가장자리</b>에 섭니다. 한복판에 두면 의뢰
                    // 지점과 겹쳐 플레이어가 할 일을 못 합니다.
                    if (Put(dreadnought, holder, place, 0.55f, 0.85f, "Guard")) guards++;
                }
            }

            // 순찰기는 길에 놓습니다. 길은 마을에서 뻗어 나가므로 그 방향을 씁니다.
            WorldStreamer streamer = Object.FindAnyObjectByType<WorldStreamer>(
                FindObjectsInactive.Include);

            if (streamer != null)
            {
                foreach (WorldRoute road in streamer.routes)
                {
                    if (road == null) continue;
                    if (Walk(strider, holder, streamer, road)) walkers++;
                }
            }
        }
        finally
        {
            Random.state = was;
        }

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();

        Debug.Log("ROBOTS 놓았습니다 — 파수꾼 " + guards + " · 순찰기 " + walkers
                  + " (지운 것 " + swept + ") · 시드 " + LayoutSeed);
        EditorApplication.Exit(0);
    }

    // --- Private Methods ---

    /// <summary>장소 안 어딘가에 한 마리 놓습니다.</summary>
    /// <param name="prefab">놓을 것</param>
    /// <param name="holder">담을 곳</param>
    /// <param name="place">장소</param>
    /// <param name="innerRatio">반경의 이 비율보다는 바깥</param>
    /// <param name="outerRatio">반경의 이 비율보다는 안쪽</param>
    /// <param name="role">이름에 붙일 말</param>
    /// <returns>놓았으면 true</returns>
    private static bool Put(GameObject prefab, Transform holder, WorldLocation place,
                            float innerRatio, float outerRatio, string role)
    {
        float angle = Random.value * Mathf.PI * 2f;
        float reach = Random.Range(place.radius * innerRatio, place.radius * outerRatio);

        Vector3 want = place.transform.position
                       + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * reach;

        if (!Ground(want, Walkable(prefab), out Vector3 on))
        {
            Debug.Log("ROBOTS ⚠ 땅을 못 찾아 건너뜀 — " + place.displayName);
            return false;
        }

        GameObject made = (GameObject)PrefabUtility.InstantiatePrefab(prefab, holder);
        made.name = prefab.name + "_" + role + "_" + place.displayName;
        made.transform.position = on;

        // 장소 한가운데를 보게 세웁니다. 파수꾼이 제 현장을 등지고 서면 안 됩니다.
        Vector3 inward = place.transform.position - on;
        inward.y = 0f;
        if (inward.sqrMagnitude > 0.01f) made.transform.rotation = Quaternion.LookRotation(inward);

        Debug.Log("ROBOTS 놓음 — " + made.name + " · " + on.ToString("F0"));
        return true;
    }

    /// <summary>길 위에 순찰기를 한 마리 놓습니다.</summary>
    private static bool Walk(GameObject prefab, Transform holder,
                             WorldStreamer streamer, WorldRoute road)
    {
        Vector3 from = (streamer.origin != null ? streamer.origin.position
                                                : streamer.transform.position)
                       + road.startOffset;

        Vector3 way = road.direction.sqrMagnitude > 1e-4f
                      ? road.direction.normalized : Vector3.forward;

        // 길의 중간쯤에 놓습니다. 끝에 놓으면 순찰로가 세계 밖으로 나갑니다.
        float along = streamer.fallbackTileSize * road.tileCount * Random.Range(0.35f, 0.6f);
        Vector3 want = from + way * along;

        if (!Ground(want, Walkable(prefab), out Vector3 on))
        {
            Debug.Log("ROBOTS ⚠ 길에서 땅을 못 찾아 건너뜀 — " + road.displayName);
            return false;
        }

        GameObject made = (GameObject)PrefabUtility.InstantiatePrefab(prefab, holder);
        made.name = prefab.name + "_Patrol_" + road.displayName;
        made.transform.position = on;
        made.transform.rotation = Quaternion.LookRotation(way);

        Debug.Log("ROBOTS 놓음 — " + made.name + " · " + on.ToString("F0")
                  + " · 길 " + road.displayName);
        return true;
    }

    /// <summary>
    /// 그 자리의 땅입니다.
    ///
    /// ⚠ <b>레이어를 걸러야 합니다.</b> 처음에는 <c>~0</c> 으로 아무거나 맞았는데,
    /// 그 결과 북쪽 길의 순찰기가 <b>y=200 의 메가스트럭처 데크 위</b>에 섰습니다.
    /// 순찰로는 그 아래 지형에 깔려 있어서 <b>제 길에서 183 m 떨어진</b> 로봇이
    /// 나왔습니다. 검사 도구가 그것을 잡았습니다.
    ///
    /// 로봇이 걸을 수 있는 땅은 그 로봇의 <see cref="RobotDriver.groundMask"/> 가
    /// 정합니다. 여기서 다른 기준을 쓰면 배치와 길이 <b>서로 다른 세계</b>를 봅니다.
    /// </summary>
    /// <param name="at">찾을 자리</param>
    /// <param name="mask">무엇을 땅으로 볼 것인가</param>
    /// <param name="on">땅 위의 자리</param>
    private static bool Ground(Vector3 at, LayerMask mask, out Vector3 on)
    {
        on = at;

        if (!Physics.Raycast(at + Vector3.up * 200f, Vector3.down, out RaycastHit hit, 600f,
                             mask, QueryTriggerInteraction.Ignore))
        {
            return false;
        }

        on = hit.point;
        return true;
    }

    /// <summary>그 프리팹이 무엇을 땅으로 보는가.</summary>
    private static LayerMask Walkable(GameObject prefab)
    {
        RobotDriver driver = prefab != null ? prefab.GetComponentInChildren<RobotDriver>(true) : null;
        return driver != null ? driver.groundMask : ~0;
    }

    /// <summary>놓은 것을 담을 곳입니다. 없으면 만듭니다.</summary>
    private static Transform Holder()
    {
        GameObject world = GameObject.Find(WorldRoot);
        Transform under = world != null ? world.transform : null;

        Transform had = under != null ? under.Find(HolderName) : null;

        if (had == null)
        {
            GameObject loose = GameObject.Find(HolderName);
            had = loose != null ? loose.transform : null;
        }

        if (had != null) return had;

        GameObject made = new GameObject(HolderName);
        made.transform.SetParent(under, true);
        return made.transform;
    }
}
