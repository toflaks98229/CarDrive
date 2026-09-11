using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using CarDrive.Gameplay;

/// <summary>
/// 스트라이더에게 <b>걸을 길과 걸을 시각</b>을 놓아 줍니다.
///
/// <b>스트라이더만입니다.</b> 기획이 넷의 자리를 이렇게 갈라 두었습니다
/// (<c>로봇_기획.md</c> 의 "넷의 자리") —
/// 스트라이더는 <b>교통</b>이라 "정해진 길을 정해진 시각에 걷고",
/// 드레드노트는 <b>규칙</b>이라 "가만히 서 있다가 규칙을 어겼을 때만" 움직입니다.
/// 파수꾼에게 순찰로를 주면 그 자리가 무너집니다.
///
/// <code>
/// Unity.exe -batchmode -nographics -projectPath . -executeMethod RobotPatrolSetup.Run
/// </code>
/// </summary>
public static class RobotPatrolSetup
{
    private const string ScenePath = "Assets/_Project/01.Scenes/SampleScene.unity";
    private const string RouteRoot = "--- World ---";

    /// <summary>자리 사이의 거리(m)입니다.</summary>
    private const float Spacing = 60f;

    /// <summary>놓을 자리의 수입니다.</summary>
    private const int Stops = 4;

    public static void Run()
    {
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        int made = 0;

        foreach (RobotDriver driver in Object.FindObjectsByType<RobotDriver>(
                     FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            // 이름으로 가릅니다. 프리팹이 갈라져 있어도 이름은 남습니다.
            if (driver.name.IndexOf("Strider", System.StringComparison.OrdinalIgnoreCase) < 0)
            {
                Debug.Log("PATROL 건너뜀 (순찰하는 기계가 아닙니다) — " + driver.name);
                continue;
            }

            made += Give(driver) ? 1 : 0;
        }

        // ⚠ <b>지각은 둘 다 답니다.</b> 순찰기에게는 "길을 막았다" 를 말할 입이 되고,
        // 파수꾼에게는 싸움 앞 단계가 됩니다 — 기획의 "지킴이와 순찰기에 필요합니다".
        int eyes = 0;

        foreach (RobotDriver driver in Object.FindObjectsByType<RobotDriver>(
                     FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (driver.GetComponent<RobotAwareness>() != null) continue;

            RobotAwareness aware = driver.gameObject.AddComponent<RobotAwareness>();
            aware.sightMask = driver.groundMask;
            EditorUtility.SetDirty(aware);
            eyes++;

            Debug.Log("PATROL 지각을 달았습니다 — " + driver.name);
        }

        // ⚠ 길과 눈은 <b>따로 셉니다.</b> 한 숫자로 합치면 길을 안 놓은 날에도
        // "길을 놓았다" 고 말합니다.

        if (made + eyes > 0)
        {
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveOpenScenes();
        }

        Debug.Log("PATROL 길을 놓은 로봇 " + made + " 마리 · 지각을 단 로봇 " + eyes + " 마리");
        EditorApplication.Exit(0);
    }

    /// <summary>
    /// 놓인 길이 <b>걸을 수 있는 길인지</b> 봅니다.
    ///
    /// 판단 자체는 <c>RobotPatrolTests</c> 가 지킵니다. 여기서 보는 것은 <b>씬에 놓인
    /// 것</b>입니다 — 자리가 땅에 붙어 있는지, 자리 사이가 로봇이 넘을 수 있는
    /// 경사인지, 로봇이 길에서 너무 멀리 떨어져 있지 않은지.
    ///
    /// <code>
    /// Unity.exe -batchmode -nographics -projectPath . -executeMethod RobotPatrolSetup.Check
    /// </code>
    /// </summary>
    public static void Check()
    {
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        int problems = 0;
        int looked = 0;

        foreach (RobotPatrol patrol in Object.FindObjectsByType<RobotPatrol>(
                     FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            looked++;

            Transform parent = patrol.routeParent;
            int count = parent != null ? parent.childCount : 0;

            if (count < 2)
            {
                Debug.Log("PATROL ⚠ 자리가 " + count + " 곳뿐입니다 — " + patrol.name);
                problems++;
                continue;
            }

            RobotDriver driver = patrol.GetComponent<RobotDriver>();
            LayerMask mask = driver != null ? driver.groundMask : ~0;

            Vector3 previous = Vector3.zero;
            float worst = 0f;
            float longest = 0f;

            for (int i = 0; i < count; i++)
            {
                Vector3 at = parent.GetChild(i).position;

                // 땅에 붙어 있는가. 1 m 넘게 뜨거나 잠겨 있으면 못 닿습니다.
                if (!Ground(at, mask, out Vector3 on) || Mathf.Abs(on.y - at.y) > 1f)
                {
                    Debug.Log("PATROL ⚠ 땅에서 떨어진 자리 — " + parent.GetChild(i).name);
                    problems++;
                }

                if (i == 0) { previous = at; continue; }

                Vector3 leg = at - previous;
                float flat = new Vector2(leg.x, leg.z).magnitude;
                float slope = flat > 0.01f ? Mathf.Abs(leg.y) / flat : 0f;

                worst = Mathf.Max(worst, slope);
                longest = Mathf.Max(longest, flat);
                previous = at;
            }

            // 로봇이 길 위에 있는가. 멀면 첫 걸음이 엉뚱한 데서 시작합니다.
            float away = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                away = Mathf.Min(away, Vector3.Distance(patrol.transform.position,
                                                        parent.GetChild(i).position));
            }

            Debug.Log("PATROL " + patrol.name + " — 자리 " + count + " 곳 · 가장 긴 구간 "
                      + longest.ToString("F0") + " m · 가장 가파른 경사 "
                      + (worst * 100f).ToString("F0") + "% · 로봇에서 가장 가까운 자리 "
                      + away.ToString("F0") + " m · " + patrol.onDutyHour.ToString("F0")
                      + "시~" + patrol.offDutyHour.ToString("F0") + "시");

            // ⚠ 보행기는 계단을 오르지 못합니다. 100% 는 45 도입니다.
            if (worst > 0.6f)
            {
                Debug.Log("PATROL ⚠ 경사 " + (worst * 100f).ToString("F0") + "% 는 너무 가파릅니다");
                problems++;
            }

            if (away > 80f)
            {
                Debug.Log("PATROL ⚠ 로봇이 길에서 " + away.ToString("F0") + " m 떨어져 있습니다");
                problems++;
            }
        }

        Debug.Log("PATROL 검사 끝 — 길 " + looked + " 개 · 문제 " + problems + " 건");
        EditorApplication.Exit(problems > 0 ? 2 : 0);
    }

    // --- Private Methods ---

    /// <summary>이 로봇에게 길과 시각표를 줍니다.</summary>
    /// <param name="driver">걷는 부품</param>
    /// <returns>새로 놓았으면 true</returns>
    private static bool Give(RobotDriver driver)
    {
        RobotPatrol had = driver.GetComponent<RobotPatrol>();

        if (had != null && had.routeParent != null)
        {
            Debug.Log("PATROL 이미 길이 있습니다 — " + driver.name);
            return false;
        }

        RobotPatrol patrol = had != null ? had : driver.gameObject.AddComponent<RobotPatrol>();

        // ── 어느 쪽으로 난 길인가 ──
        //
        // 스파인이 이 세계의 큰 축입니다. 그것을 따라 놓으면 길 위를 걷는 것으로
        // 보입니다. 없으면 로봇이 보고 있는 쪽으로 놓습니다.
        Transform spine = GameObject.Find("Megastructure")?.transform;
        Vector3 along = spine != null ? spine.forward : driver.transform.forward;
        along.y = 0f;
        along = along.sqrMagnitude > 1e-4f ? along.normalized : Vector3.forward;

        Transform parent = Folder(driver.name);
        Vector3 start = driver.transform.position - along * (Spacing * (Stops - 1) * 0.5f);

        int landed = 0;

        for (int i = 0; i < Stops; i++)
        {
            Vector3 want = start + along * (Spacing * i);

            // ⚠ <b>땅에 붙여야 합니다.</b> 공중에 뜬 자리를 주면 걷는 부품이 영영
            // 못 닿아 그 자리에서 맴돕니다.
            if (!Ground(want, driver.groundMask, out Vector3 on))
            {
                Debug.Log("PATROL ⚠ 땅을 못 찾아 건너뜀 — " + want.ToString("F0"));
                continue;
            }

            GameObject stop = new GameObject(driver.name + "_Stop" + landed);
            stop.transform.SetParent(parent, true);
            stop.transform.position = on;
            landed++;
        }

        if (landed < 2)
        {
            Debug.Log("PATROL ⚠ 자리가 둘도 안 되어 길을 못 놓았습니다 — " + driver.name);
            return false;
        }

        patrol.route = null;              // 부모의 자식을 쓰겠다는 뜻입니다
        patrol.routeParent = parent;

        // ⚠ <b>한 줄로 난 길이라 고리로 돌면 안 됩니다.</b> 끝에서 처음으로 뛰면
        // 길 밖으로 질러갑니다. 왔던 길을 되짚습니다.
        patrol.loop = false;
        patrol.dwellSeconds = 8f;

        // 저녁부터 새벽까지. ⚠ <b>이 시각은 고른 것이지 잰 것이 아닙니다</b> —
        // 플레이어가 운전하는 시간대에 길에서 마주치라는 뜻이고, 놀아 보고 고칠 값입니다.
        patrol.onDutyHour = 18f;
        patrol.offDutyHour = 6f;

        // 격납고는 아직 없습니다. 퇴근하면 선 자리에 섭니다.
        patrol.berth = null;

        EditorUtility.SetDirty(patrol);

        Debug.Log("PATROL 길을 놓았습니다 — " + driver.name + " · 자리 " + landed + " 곳 · "
                  + Spacing * (landed - 1) + " m · " + patrol.onDutyHour.ToString("F0") + "시~"
                  + patrol.offDutyHour.ToString("F0") + "시");
        return true;
    }

    /// <summary>그 자리의 땅입니다.</summary>
    /// <param name="at">찾을 자리</param>
    /// <param name="mask">무엇을 땅으로 볼 것인가</param>
    /// <param name="on">땅 위의 자리</param>
    /// <returns>찾았으면 true</returns>
    private static bool Ground(Vector3 at, LayerMask mask, out Vector3 on)
    {
        on = at;

        if (!Physics.Raycast(at + Vector3.up * 120f, Vector3.down, out RaycastHit hit, 400f,
                             mask, QueryTriggerInteraction.Ignore))
        {
            return false;
        }

        on = hit.point;
        return true;
    }

    /// <summary>자리들을 담을 오브젝트입니다. 없으면 만듭니다.</summary>
    private static Transform Folder(string robotName)
    {
        GameObject world = GameObject.Find(RouteRoot);
        Transform under = world != null ? world.transform : null;

        Transform routes = Find(under, "RobotRoutes");

        if (routes == null)
        {
            GameObject made = new GameObject("RobotRoutes");
            made.transform.SetParent(under, true);
            routes = made.transform;
        }

        Transform mine = Find(routes, robotName + "_Route");

        if (mine == null)
        {
            GameObject made = new GameObject(robotName + "_Route");
            made.transform.SetParent(routes, true);
            mine = made.transform;
        }

        return mine;
    }

    private static Transform Find(Transform parent, string name)
    {
        if (parent == null)
        {
            GameObject loose = GameObject.Find(name);
            return loose != null ? loose.transform : null;
        }

        return parent.Find(name);
    }
}
