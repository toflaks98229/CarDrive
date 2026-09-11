using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using CarDrive.Gameplay;

/// <summary>
/// 견인기를 <b>마을 어귀에</b> 세웁니다.
///
/// <b>왜 마을인가.</b> 이 기계가 하는 일은 죽은 차를 <b>마을로</b> 끌고 오는 것입니다.
/// 세계 어딘가에 세워 두면 끌고 오는 거리가 두 배가 되고, 무엇보다 플레이어가
/// 그것이 존재한다는 사실을 <b>실패하기 전에</b> 알 수 없습니다. 마을 어귀에 서서
/// 기다리는 기계는 "저것이 언젠가 나를 주우러 온다" 를 미리 말해 줍니다.
///
/// ⚠ <b>스트라이더를 씁니다.</b> 기획이 "스트라이더 급 · 비무장" 이라고 적어 두었고,
/// 그 모델에는 이미 <c>Gun_TowHook</c> 자리와 <see cref="WeaponWinch"/> 가
/// 배선되어 있습니다(<c>StriderWeaponSetup</c>). 새로 만들 것이 없습니다.
///
/// <code>
/// Unity.exe -batchmode -nographics -projectPath . -executeMethod TowRigSetup.Run
/// </code>
/// </summary>
public static class TowRigSetup
{
    private const string ScenePath = "Assets/_Project/01.Scenes/SampleScene.unity";
    private const string StriderPath =
        "Assets/_Project/05.Prefabs/Robot/WalkerRobot_Strider.prefab";
    private const string WorldRoot = "--- World ---";
    private const string HolderName = "PlacedRobots";
    private const string RigName = "TowRig";
    private const string YardName = "TowYard";

    /// <summary>땅 레이어입니다.</summary>
    private const int GroundLayer = 11;

    public static void Run()
    {
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        GameObject strider = AssetDatabase.LoadAssetAtPath<GameObject>(StriderPath);

        if (strider == null)
        {
            Debug.Log("TOW ⚠ 스트라이더를 못 찾았습니다 — " + StriderPath);
            EditorApplication.Exit(1);
            return;
        }

        WorldStreamer streamer = Object.FindAnyObjectByType<WorldStreamer>(
            FindObjectsInactive.Include);

        Vector3 middle = streamer != null && streamer.origin != null
                         ? streamer.origin.position
                         : Vector3.zero;

        float edge = streamer != null ? streamer.villageRadius * 0.6f : 40f;

        Transform holder = Holder();

        // 이미 있으면 지우고 다시 놓습니다. 두 번 돌려서 두 마리가 되면 안 됩니다.
        Transform had = holder.Find(RigName);
        if (had != null) Object.DestroyImmediate(had.gameObject);

        // ── 차를 내려놓을 자리 ──
        //
        // 정비 팔 옆입니다. 끌려온 차가 <b>고칠 수 있는 자리</b>에 놓여야 그다음이
        // 이어집니다 — 마을 반대편에 부려 놓으면 걸어가야 합니다.
        RepairArm arm = Object.FindAnyObjectByType<RepairArm>(FindObjectsInactive.Include);

        Vector3 yardWant = arm != null
                           ? arm.transform.position + arm.transform.right * 4f
                           : middle + new Vector3(edge * 0.5f, 0f, 0f);

        Transform yard = Yard(holder, yardWant);

        // ── 기계가 서 있을 자리 ──
        //
        // 마을 어귀입니다. 정비 팔 반대쪽에 세워 둘을 갈라 놓습니다.
        Vector3 want = middle + new Vector3(-edge, 0f, edge * 0.4f);

        if (!Ground(want, out Vector3 on))
        {
            Debug.Log("TOW ⚠ 마을 어귀에서 땅을 못 찾았습니다 — " + want.ToString("F0"));
            EditorApplication.Exit(1);
            return;
        }

        GameObject made = (GameObject)PrefabUtility.InstantiatePrefab(strider, holder);
        made.name = RigName;
        made.transform.position = on;

        // 마을 한가운데를 보게 세웁니다.
        Vector3 inward = middle - on;
        inward.y = 0f;
        if (inward.sqrMagnitude > 0.01f) made.transform.rotation = Quaternion.LookRotation(inward);

        TowRig tow = made.GetComponent<TowRig>();
        if (tow == null) tow = made.AddComponent<TowRig>();

        tow.driver = made.GetComponentInChildren<RobotDriver>(true);
        tow.winch = made.GetComponentInChildren<WeaponWinch>(true);
        tow.yard = yard;

        // ⚠ <b>넓게 봅니다.</b> 길은 마을에서 뻗어 나가므로, 어귀에 선 기계가 멀리
        // 볼수록 <b>모든 길</b>을 그만큼씩 덮습니다. 좁게 두면 길 하나에 한 마리씩
        // 세워야 하고, 걷는 기계는 다리가 비쌉니다.
        tow.notice = 400f;

        // ⚠ <b>비무장입니다.</b> 기획이 그렇게 적었고, 무장한 채로 두면 이 기계가
        // 플레이어에게 <b>위협</b>이 되어 "주우러 오는 것" 이라는 뜻이 흐려집니다.
        int disarmed = Disarm(made);

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();

        Debug.Log("TOW 세웠습니다 — " + on.ToString("F0") + " · 마을 중심에서 "
                  + Vector3.Distance(on, middle).ToString("F0") + " m · 내려놓을 자리 "
                  + yard.position.ToString("F0") + " · 끈 무장 " + disarmed + " 개");

        Debug.Log("TOW 배선 — 걷기 " + (tow.driver != null ? "있음" : "없음")
                  + " · 갈고리 " + (tow.winch != null ? "있음" : "없음"));

        EditorApplication.Exit(0);
    }

    // --- Private Methods ---

    /// <summary>무장을 끕니다. 갈고리(발사 없음)는 남깁니다.</summary>
    /// <param name="root">기계</param>
    /// <returns>끈 무장 수</returns>
    private static int Disarm(GameObject root)
    {
        int off = 0;

        foreach (RobotWeapon gun in root.GetComponentsInChildren<RobotWeapon>(true))
        {
            // 갈고리는 쏘는 물건이 아닙니다. 그것까지 끄면 줄이 안 움직입니다.
            if (gun.winch != null) continue;

            gun.enabled = false;
            off++;
        }

        foreach (RobotCombatant fighter in root.GetComponentsInChildren<RobotCombatant>(true))
        {
            fighter.enabled = false;
            off++;
        }

        return off;
    }

    /// <summary>차를 내려놓을 자리입니다. 없으면 만듭니다.</summary>
    /// <param name="holder">놓을 곳</param>
    /// <param name="want">두고 싶은 자리</param>
    private static Transform Yard(Transform holder, Vector3 want)
    {
        Transform had = holder.Find(YardName);

        GameObject made = had != null ? had.gameObject : new GameObject(YardName);
        if (had == null) made.transform.SetParent(holder, true);

        made.transform.position = Ground(want, out Vector3 on) ? on : want;

        return made.transform;
    }

    /// <summary>그 자리의 땅입니다.</summary>
    /// <param name="at">볼 자리</param>
    /// <param name="on">땅 위의 자리</param>
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

    /// <summary>놓을 곳입니다. 다른 로봇과 같은 폴더를 씁니다.</summary>
    private static Transform Holder()
    {
        GameObject world = GameObject.Find(WorldRoot);
        Transform under = world != null ? world.transform : null;

        Transform had = under != null ? under.Find(HolderName) : null;
        if (had != null) return had;

        GameObject loose = GameObject.Find(HolderName);
        if (loose != null) return loose.transform;

        GameObject made = new GameObject(HolderName);
        made.transform.SetParent(under, true);

        return made.transform;
    }
}
