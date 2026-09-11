using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using CarDrive.Gameplay;

/// <summary>
/// 포가 <b>밀린 자세에서</b> 무엇을 파고드는지 잽니다.
///
/// <b>왜 재는가.</b> 이 결함은 <b>레스트 포즈에서 안 보입니다.</b> 포신이 제자리에
/// 있을 때는 멀쩡하고, 쏘는 순간 0.38 m 뒤로 밀리면서 요크나 몸통을 뚫고 나갑니다.
/// 그 순간은 네 프레임짜리라 눈으로는 못 잡습니다. 블렌더 쪽이 부앙 범위를 돌려 가며
/// 같은 것을 재고 있었고(<c>build_strider_guns.py</c> 의 <c>swing_clash</c>),
/// 유니티 쪽에는 그 검사가 없었습니다.
///
/// <b>두 가지를 따로 잽니다.</b>
///
///  1. <b>마운트 뒤로 나왔는가.</b> 포신은 원래 마운트 <b>안으로</b> 들어갑니다 —
///     그것이 반동입니다. 문제는 <b>뒤로 뚫고 나오는</b> 것입니다.
///  2. <b>다른 것을 새로 파고드는가.</b> 요크·몸통처럼 그 포가 아닌 것들입니다.
///
/// ⚠ <b>"겹쳤다" 가 아니라 "새로 겹쳤다" 를 셉니다.</b> 쉬는 자세의 겹침을 먼저 재
/// 두고 밀린 자세에서 <b>늘어난 만큼</b>만 셉니다.
///
/// ⚠ <b>다른 무장은 빼고 잽니다.</b> 프리팹에는 여섯이 다 들어 있지만 배치할 때
/// 하나를 고르는 구조라, 둘이 같은 자리를 쓰는 것은 결함이 아닙니다. 처음에 전부
/// 켜 놓고 쟀더니 공성포와 중포가 서로를 파고든다고 나왔습니다 — <b>같이 달릴 일이
/// 없는 둘</b>입니다.
///
/// ⚠ <b>물리도 재생도 필요 없습니다.</b> 자세를 손으로 밀어 놓고 재는 정적 검사라
/// 프리팹만 열면 됩니다.
///
/// <code>
/// Unity.exe -batchmode -nographics -projectPath . -executeMethod WeaponRecoilClash.Run
/// </code>
/// </summary>
public static class WeaponRecoilClash
{
    private const string PrefabPath =
        "Assets/_Project/05.Prefabs/Robot/WalkerRobot_Strider.prefab";

    private const string OutputDirectory = "Logs/RobotWeapon";

    /// <summary>
    /// 이만큼까지는 봐줍니다(m).
    ///
    /// 상자로 재기 때문에 비스듬한 조각은 <b>실제보다 크게</b> 잡힙니다. 손가락
    /// 한 마디는 그 오차 안입니다.
    /// </summary>
    private const float Tolerance = 0.02f;

    public static void Run()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);

        if (prefab == null)
        {
            Debug.Log("CLASH ⚠ 스트라이더를 못 찾았습니다 — " + PrefabPath);
            EditorApplication.Exit(1);
            return;
        }

        GameObject robot = (GameObject)PrefabUtility.InstantiatePrefab(prefab);

        StringBuilder report = new StringBuilder();
        report.AppendLine("== 밀린 자세에서 파고드는가 ==");
        report.AppendLine("재는 법 : 포를 끝까지 밀어 놓고, 쉬는 자세보다 늘어난 겹침만 셉니다");
        report.AppendLine("봐주는 값 : " + Tolerance.ToString("F2") + " m (상자로 재는 오차)");
        report.AppendLine();

        int bad = 0;
        int measured = 0;

        try
        {
            // ⚠ <b>무장을 전부 켭니다.</b> 프리팹에는 여섯이 다 들어 있고 배치할 때
            // 고르는 구조라, 꺼진 채로 재면 <b>안 켜진 무장은 영영 안 재집니다.</b>
            foreach (RobotWeapon gun in robot.GetComponentsInChildren<RobotWeapon>(true))
            {
                Wake(gun.transform);
            }

            foreach (RobotWeapon gun in robot.GetComponentsInChildren<RobotWeapon>(true))
            {
                WeaponRecoil[] kicks = gun.GetComponentsInChildren<WeaponRecoil>(true);
                if (kicks.Length == 0) continue;

                report.AppendLine("[" + gun.name + "]");

                foreach (WeaponRecoil kick in kicks)
                {
                    measured++;
                    if (Measure(robot.transform, kick, report)) bad++;
                }

                report.AppendLine();
            }
        }
        finally
        {
            Object.DestroyImmediate(robot);
        }

        report.AppendLine(bad == 0
                          ? "밀어도 파고드는 것 없음 — 잰 것 " + measured + " 곳"
                          : "⚠ 파고드는 곳 " + bad + " — 잰 것 " + measured + " 곳");

        string text = report.ToString();

        Directory.CreateDirectory(OutputDirectory);
        File.WriteAllText(Path.Combine(OutputDirectory, "recoil_clash.txt"), text);

        Debug.Log(text);
        EditorApplication.Exit(bad > 0 ? 2 : 0);
    }

    // --- Private Methods ---

    /// <summary>그 조각과 위쪽을 전부 켭니다.</summary>
    /// <param name="part">켤 조각</param>
    private static void Wake(Transform part)
    {
        for (Transform t = part; t != null; t = t.parent)
        {
            t.gameObject.SetActive(true);
        }
    }

    /// <summary>
    /// 한 조각을 끝까지 밀어 보고 잽니다.
    /// </summary>
    /// <param name="root">로봇의 뿌리</param>
    /// <param name="kick">밀 부품</param>
    /// <param name="report">적을 곳</param>
    /// <returns>봐주는 값을 넘겼으면 true</returns>
    private static bool Measure(Transform root, WeaponRecoil kick, StringBuilder report)
    {
        Transform part = kick.part != null ? kick.part : kick.transform;

        List<Renderer> moving = new List<Renderer>(part.GetComponentsInChildren<Renderer>(true));
        if (moving.Count == 0)
        {
            report.AppendLine("  " + part.name + " : 그릴 것이 없어 건너뜁니다");
            return false;
        }

        // 이 포가 달린 마운트입니다. 포신이 그 안으로 들어가는 것은 반동 그 자체라
        // 겹침으로 세지 않고, <b>뒤로 나왔는지</b>만 따로 봅니다.
        Transform mount = part.parent;

        RobotWeapon mine = part.GetComponentInParent<RobotWeapon>();

        // 움직이지 않는 것들입니다.
        List<Renderer> fixedParts = new List<Renderer>();

        foreach (Renderer other in root.GetComponentsInChildren<Renderer>(true))
        {
            if (other == null || moving.Contains(other)) continue;
            if (other.transform.IsChildOf(part)) continue;

            // 마운트 자신은 뺍니다. 들어가라고 있는 것입니다.
            if (mount != null && other.transform == mount) continue;

            // ⚠ 다른 무장은 뺍니다. 같이 달릴 일이 없습니다.
            RobotWeapon owner = other.GetComponentInParent<RobotWeapon>();
            if (owner != null && owner != mine) continue;

            fixedParts.Add(other);
        }

        Vector3 way = kick.axis.sqrMagnitude > 1e-6f ? kick.axis.normalized : Vector3.back;

        Vector3 rest = part.localPosition;
        float[] before = Depths(moving, fixedParts);

        part.localPosition = rest + way * kick.travel;
        float[] after = Depths(moving, fixedParts);

        // ⚠ <b>세 배로도 밀어 봅니다.</b> 늘 "없음" 만 찍는 검사는 <b>고장 났을 때도
        // "없음" 을 찍습니다.</b> 지나치게 밀었을 때 걸리는 자리가 하나라도 있어야
        // 이 자에 눈금이 있다는 뜻이고, 동시에 <b>여유가 얼마나 되는지</b>도 읽힙니다.
        // 실제로 공성포 포신이 세 배에서 0.300 m 걸립니다 — 눈금은 살아 있습니다.
        part.localPosition = rest + way * (kick.travel * 3f);
        float[] far = Depths(moving, fixedParts);

        part.localPosition = rest;

        // 쉬는 자세보다 <b>늘어난</b> 겹침만 셉니다.
        float worst = 0f;
        string culprit = "";
        float overshoot = 0f;

        for (int i = 0; i < fixedParts.Count; i++)
        {
            float grew = after[i] - before[i];

            if (grew > worst)
            {
                worst = grew;
                culprit = fixedParts[i].name;
            }

            float grewFar = far[i] - before[i];
            if (grewFar > overshoot) overshoot = grewFar;
        }

        // 마운트 뒤로 나온 길이입니다. 반동 방향으로 얼마나 더 갔는지를 봅니다.
        float out_ = Behind(moving, mount, root.TransformDirection(
            part.parent != null ? part.parent.TransformDirection(way) : way));

        bool over = worst > Tolerance || out_ > Tolerance;

        report.AppendLine("  " + part.name + " : " + kick.travel.ToString("F2") + " m 밀림");

        report.AppendLine("    마운트 뒤로  : "
                          + (out_ > 0f
                             ? out_.ToString("F3") + " m 나옴"
                             : "여유 " + (-out_).ToString("F3") + " m")
                          + (out_ > Tolerance ? "   ← 뚫고 나옵니다" : ""));

        report.AppendLine("    새로 파고듦  : "
                          + (worst <= 0.0001f
                             ? "없음"
                             : worst.ToString("F3") + " m (" + culprit + ")")
                          + (worst > Tolerance ? "   ← 파고듭니다" : ""));

        report.AppendLine("    세 배로 밀면 : "
                          + (overshoot <= 0.0001f
                             ? "그래도 안 닿음 (여유가 큼)"
                             : overshoot.ToString("F3") + " m"));

        return over;
    }

    /// <summary>
    /// 움직이는 것들이 고정된 것들을 <b>얼마나 파고드는지</b>입니다.
    ///
    /// 축에 정렬된 상자끼리 겹친 부분의 <b>가장 짧은 변</b>을 깊이로 봅니다 —
    /// 그 길이만큼 밀어내면 겹침이 풀리기 때문입니다. 블렌더 쪽이 쓰는 것과 같은
    /// 셈법입니다.
    /// </summary>
    /// <param name="moving">움직이는 것들</param>
    /// <param name="fixedParts">고정된 것들</param>
    /// <returns>고정된 것마다의 깊이</returns>
    private static float[] Depths(List<Renderer> moving, List<Renderer> fixedParts)
    {
        float[] deep = new float[fixedParts.Count];

        for (int i = 0; i < fixedParts.Count; i++)
        {
            Bounds still = fixedParts[i].bounds;
            float worst = 0f;

            for (int m = 0; m < moving.Count; m++)
            {
                float now = Overlap(moving[m].bounds, still);
                if (now > worst) worst = now;
            }

            deep[i] = worst;
        }

        return deep;
    }

    /// <summary>
    /// 밀린 조각이 마운트 <b>뒤로</b> 얼마나 나왔는가.
    ///
    /// 반동 방향으로 가장 멀리 간 점을 마운트의 같은 방향 끝과 견줍니다.
    /// 마운트가 없으면 견줄 것이 없으므로 0 입니다.
    /// </summary>
    /// <param name="moving">밀린 것들</param>
    /// <param name="mount">그 조각이 달린 마운트</param>
    /// <param name="wayWS">반동 방향(월드)</param>
    /// <returns>뒤로 나온 길이(m). 음수면 그만큼 <b>여유가 남은</b> 것입니다</returns>
    private static float Behind(List<Renderer> moving, Transform mount, Vector3 wayWS)
    {
        // 마운트에 몸이 없으면 견줄 것이 없습니다. 0 은 "딱 맞는다" 가 아니라
        // "못 쟀다" 는 뜻이므로, 부르는 쪽이 그렇게 읽도록 그대로 돌려줍니다.
        if (mount == null) return 0f;

        Renderer shell = mount.GetComponent<Renderer>();
        if (shell == null) return 0f;

        Vector3 dir = wayWS.sqrMagnitude > 1e-6f ? wayWS.normalized : Vector3.back;

        float mountEnd = Reach(shell.bounds, dir);
        float partEnd = float.NegativeInfinity;

        for (int i = 0; i < moving.Count; i++)
        {
            float now = Reach(moving[i].bounds, dir);
            if (now > partEnd) partEnd = now;
        }

        // ⚠ <b>잘라서 0 으로 만들지 않습니다.</b> 음수는 "아직 이만큼 남았다" 는
        // 뜻이고, 그 여유가 보여야 <b>재고 있다는 것</b>이 읽힙니다. 늘 "안 나옴" 만
        // 찍는 검사는 고장 났을 때도 "안 나옴" 을 찍습니다.
        return partEnd - mountEnd;
    }

    /// <summary>그 상자가 그 방향으로 가장 멀리 뻗은 자리입니다.</summary>
    /// <param name="box">상자</param>
    /// <param name="dir">방향(정규화됨)</param>
    private static float Reach(Bounds box, Vector3 dir)
    {
        Vector3 far = box.center
                      + new Vector3(Mathf.Sign(dir.x) * box.extents.x,
                                    Mathf.Sign(dir.y) * box.extents.y,
                                    Mathf.Sign(dir.z) * box.extents.z);

        return Vector3.Dot(far, dir);
    }

    /// <summary>두 상자가 겹친 깊이입니다. 안 겹치면 0 입니다.</summary>
    /// <param name="a">한 상자</param>
    /// <param name="b">다른 상자</param>
    private static float Overlap(Bounds a, Bounds b)
    {
        if (!a.Intersects(b)) return 0f;

        float x = Mathf.Min(a.max.x, b.max.x) - Mathf.Max(a.min.x, b.min.x);
        float y = Mathf.Min(a.max.y, b.max.y) - Mathf.Max(a.min.y, b.min.y);
        float z = Mathf.Min(a.max.z, b.max.z) - Mathf.Max(a.min.z, b.min.z);

        return Mathf.Max(Mathf.Min(x, Mathf.Min(y, z)), 0f);
    }
}
