using System.IO;
using UnityEditor;
using UnityEngine;
using CarDrive.Gameplay;

/// <summary>
/// 미사일 칸이 <b>비어 가는 것이 보이는지</b> 찍어서 확인합니다.
///
/// <b>왜 찍어야 하는가.</b> 칸은 덮개 뒤에 있습니다 — 값이 맞아도 <b>화면에
/// 안 보이면</b> 없는 것과 같습니다. 처음에 칸을 0.26 m 안쪽에 뒀다가 콘크리트
/// 속에 묻혀 여섯 개가 통째로 안 보였고, 숫자(남은 칸 0/6)는 멀쩡했습니다.
///
/// 세 장을 찍습니다 — 다 찬 것 · 셋 쏜 것 · 다 쏜 것. 나란히 놓고 봐야
/// <b>비어 가는 것</b>이 읽힙니다.
///
/// ⚠ <b>덮개를 열고 랙만 남깁니다.</b> 닫힌 채로 찍으면 안에 든 것이 안 보이고,
/// 다른 조각이 앞을 가려도 마찬가지입니다.
///
/// <code>
/// Unity.exe -batchmode -projectPath . -executeMethod MissileCellCheck.Run
/// </code>
/// 결과: <c>Logs/RobotWeapon/missile_*.png</c>
/// </summary>
public static class MissileCellCheck
{
    private const string PrefabPath =
        "Assets/_Project/05.Prefabs/Robot/WalkerRobot_Strider.prefab";

    private const string OutputDirectory = "Logs/RobotWeapon";

    public static void Run()
    {
        EditorSettings.asyncShaderCompilation = false;
        ShaderUtil.allowAsyncCompilation = false;

        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);

        if (prefab == null)
        {
            Debug.Log("CELL ⚠ 스트라이더를 못 찾았습니다");
            EditorApplication.Exit(1);
            return;
        }

        GameObject robot = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        robot.transform.position = Vector3.zero;
        robot.transform.rotation = Quaternion.identity;

        Transform rack = Find(robot.transform, "Gun_MissileRack");

        if (rack == null)
        {
            Debug.Log("CELL ⚠ 미사일 랙이 없습니다");
            Object.DestroyImmediate(robot);
            EditorApplication.Exit(1);
            return;
        }

        // 랙과 그 아래를 전부 켭니다.
        for (Transform t = rack; t != null; t = t.parent) t.gameObject.SetActive(true);

        foreach (Transform t in rack.GetComponentsInChildren<Transform>(true))
        {
            t.gameObject.SetActive(true);
        }

        // ⚠ <b>랙만 남깁니다.</b> 다른 조각이 앞을 가리면 정작 보려던 칸이 안 보입니다.
        foreach (Renderer other in robot.GetComponentsInChildren<Renderer>(true))
        {
            if (other.transform.IsChildOf(rack)) continue;

            other.enabled = false;
        }

        foreach (Renderer skin in rack.GetComponentsInChildren<Renderer>(true))
        {
            Bounds box = skin.bounds;

            Debug.Log("CELL 조각 " + skin.name
                      + " · 가운데 " + rack.InverseTransformPoint(box.center).ToString("F2")
                      + " · 크기 " + box.size.ToString("F2"));
        }

        // ⚠ <b>덮개를 열어 둡니다.</b> 닫힌 채로 찍으면 안에 든 것이 안 보여서,
        // 정작 보려던 것을 못 봅니다.
        Transform lid = Find(rack, "Gun_MissileRack_Doors");

        if (lid != null)
        {
            WeaponHatch gate = lid.GetComponent<WeaponHatch>();
            Vector3 open = gate != null ? gate.openOffset : new Vector3(0f, 0.9f, 0f);

            lid.localPosition += open;
            Debug.Log("CELL 덮개를 열었습니다 — " + open.ToString("F2"));
        }

        // 랙의 얼굴을 정면에서 찍습니다. 총구는 +Z 입니다.
        Bounds whole = new Bounds(rack.position, Vector3.zero);
        foreach (Renderer skin in rack.GetComponentsInChildren<Renderer>(true))
        {
            whole.Encapsulate(skin.bounds);
        }

        Debug.Log("CELL 랙 전체 · 가운데 " + whole.center.ToString("F2")
                  + " · 크기 " + whole.size.ToString("F2"));

        Camera camera = new GameObject("CellCamera").AddComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.15f, 0.16f, 0.18f);
        camera.orthographic = true;
        camera.orthographicSize = whole.extents.y * 1.4f;

        camera.transform.position = whole.center + Vector3.forward * 6f;
        camera.transform.rotation = Quaternion.LookRotation(Vector3.back);

        Light lamp = new GameObject("CellLight").AddComponent<Light>();
        lamp.type = LightType.Directional;
        lamp.intensity = 1.2f;
        lamp.transform.rotation = Quaternion.Euler(35f, 200f, 0f);

        RenderTexture target = new RenderTexture(960, 540, 24, RenderTextureFormat.ARGB32);
        Texture2D shot = new Texture2D(960, 540, TextureFormat.RGB24, false);

        Directory.CreateDirectory(OutputDirectory);

        WeaponCells cells = rack.GetComponentInChildren<WeaponCells>(true);

        // 다 찬 것 · 셋 쏜 것 · 다 쏜 것. 세 장이 나란히 있어야 <b>비어 가는 것</b>이 읽힙니다.
        Take(camera, target, shot, "missile_full");

        if (cells != null)
        {
            cells.Fill();
            for (int i = 0; i < 3; i++) cells.Spend();

            Take(camera, target, shot, "missile_spent3");

            for (int i = 0; i < 3; i++) cells.Spend();

            Take(camera, target, shot, "missile_empty");

            Debug.Log("CELL 쏜 뒤 남은 칸 " + cells.Left + " / " + cells.Count);

            cells.Fill();
        }

        camera.targetTexture = null;
        Object.DestroyImmediate(shot);
        target.Release();
        Object.DestroyImmediate(target);
        Object.DestroyImmediate(camera.gameObject);
        Object.DestroyImmediate(lamp.gameObject);
        Object.DestroyImmediate(robot);

        Debug.Log("CELL 찍었습니다 — " + OutputDirectory + "/missile_*.png");
        EditorApplication.Exit(0);
    }

    /// <summary>한 장 찍습니다.</summary>
    /// <param name="camera">찍을 카메라</param>
    /// <param name="target">그릴 곳</param>
    /// <param name="shot">받을 그림</param>
    /// <param name="name">파일 이름</param>
    private static void Take(Camera camera, RenderTexture target, Texture2D shot, string name)
    {
        camera.targetTexture = target;

        for (int i = 0; i < 3; i++)
        {
            camera.Render();
            while (ShaderUtil.anythingCompiling) System.Threading.Thread.Sleep(50);
        }

        RenderTexture.active = target;
        shot.ReadPixels(new Rect(0, 0, 960, 540), 0, 0);
        shot.Apply();
        RenderTexture.active = null;

        File.WriteAllBytes(Path.Combine(OutputDirectory, name + ".png"), shot.EncodeToPNG());
    }

    private static Transform Find(Transform root, string name)
    {
        if (root.name == name) return root;

        for (int i = 0; i < root.childCount; i++)
        {
            Transform found = Find(root.GetChild(i), name);
            if (found != null) return found;
        }

        return null;
    }
}
