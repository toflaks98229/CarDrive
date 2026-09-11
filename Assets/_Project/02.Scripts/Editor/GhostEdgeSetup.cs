using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using CarDrive.Gameplay;

/// <summary>
/// 눈가 얼룩 부품을 씬에 답니다. 그리고 <b>얼룩이 어떻게 보이는지</b> 한 장 찍습니다.
///
/// <b>왜 카메라 리그인가.</b> 이 부품은 "지금 보고 있는 눈" 을 기준으로 방향을
/// 계산합니다. 차에 달면 차를 갈아탈 때마다 옮겨 달아야 하고, 도보 리그에 달면
/// 운전 중에 꺼집니다. 리그는 <b>플레이어의 것</b>이라 둘 다 아닙니다.
///
/// <code>
/// Unity.exe -batchmode -nographics -projectPath . -executeMethod GhostEdgeSetup.Run
/// Unity.exe -batchmode -projectPath . -executeMethod GhostEdgeSetup.Shot
/// </code>
/// </summary>
public static class GhostEdgeSetup
{
    private const string ScenePath = "Assets/_Project/01.Scenes/SampleScene.unity";
    private const string RigName = "CameraRig";
    private const string OutputDirectory = "Logs/Ghost";

    public static void Run()
    {
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        PlayerModeController player = Object.FindAnyObjectByType<PlayerModeController>(
            FindObjectsInactive.Include);

        Transform rig = player != null && player.carCameraFollow != null
                        ? player.carCameraFollow.transform
                        : null;

        if (rig == null)
        {
            GameObject loose = GameObject.Find(RigName);
            rig = loose != null ? loose.transform : null;
        }

        if (rig == null)
        {
            Debug.Log("EDGE ⚠ 카메라 리그를 못 찾았습니다");
            EditorApplication.Exit(1);
            return;
        }

        GhostEdgeCue cue = rig.GetComponent<GhostEdgeCue>();

        if (cue == null)
        {
            cue = rig.gameObject.AddComponent<GhostEdgeCue>();
            Debug.Log("EDGE 달았습니다 — " + rig.name);
        }
        else
        {
            Debug.Log("EDGE 이미 달려 있습니다 — " + rig.name);
        }

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();

        Debug.Log("EDGE 값 — 가까움 " + cue.near + " m · 멂 " + cue.far
                  + " m · 진하기 " + cue.strength);

        EditorApplication.Exit(0);
    }

    /// <summary>
    /// 얼룩을 손으로 켜고 한 장 찍습니다.
    ///
    /// ⚠ <b>재생 중이 아니면 부품이 돌지 않습니다.</b> 그래서 전역을 직접 넣어
    /// <b>후처리 쪽</b>만 봅니다 — 여기서 보려는 것은 "귀신을 찾는가" 가 아니라
    /// "얼룩이 어떻게 생겼는가" 입니다.
    /// </summary>
    public static void Shot()
    {
        EditorSettings.asyncShaderCompilation = false;
        ShaderUtil.allowAsyncCompilation = false;

        Directory.CreateDirectory(OutputDirectory);
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        Camera main = Camera.main;

        if (main == null)
        {
            Debug.Log("EDGE ⚠ 카메라가 없습니다");
            EditorApplication.Exit(1);
            return;
        }

        Camera eye = new GameObject("EdgeShotCamera").AddComponent<Camera>();
        eye.CopyFrom(main);
        eye.rect = new Rect(0f, 0f, 1f, 1f);

        UniversalAdditionalCameraData from = main.GetUniversalAdditionalCameraData();
        UniversalAdditionalCameraData to = eye.GetUniversalAdditionalCameraData();

        if (from != null && to != null)
        {
            to.renderPostProcessing = from.renderPostProcessing;
            to.volumeLayerMask = from.volumeLayerMask;
            to.volumeTrigger = from.volumeTrigger;
            to.renderType = CameraRenderType.Base;
        }

        eye.transform.position = main.transform.position + Vector3.up * 0.2f;
        eye.transform.rotation = Quaternion.Euler(0f, main.transform.eulerAngles.y, 0f);

        RenderTexture target = new RenderTexture(960, 540, 24, RenderTextureFormat.ARGB32);
        Texture2D shot = new Texture2D(960, 540, TextureFormat.RGB24, false);

        int id = Shader.PropertyToID("_CarDriveGhostEdge");

        // 없을 때 · 오른쪽에 있을 때 · 뒤에 있을 때.
        Take(eye, target, shot, id, new Vector4(0f, 0f, 0f, 0f), "edge_off");
        Take(eye, target, shot, id, new Vector4(1f, 0f, 0.55f, 0f), "edge_right");
        Take(eye, target, shot, id, new Vector4(0f, -1f, 0.55f, 0f), "edge_behind");

        Shader.SetGlobalVector(id, Vector4.zero);

        Object.DestroyImmediate(shot);
        target.Release();
        Object.DestroyImmediate(target);
        Object.DestroyImmediate(eye.gameObject);

        Debug.Log("EDGE 찍었습니다 — " + OutputDirectory + "/edge_*.png");
        EditorApplication.Exit(0);
    }

    // --- Private Methods ---

    /// <summary>얼룩 값을 넣고 한 장 찍습니다.</summary>
    /// <param name="eye">찍을 카메라</param>
    /// <param name="target">그릴 곳</param>
    /// <param name="shot">받을 그림</param>
    /// <param name="id">전역 이름표</param>
    /// <param name="edge">넣을 값</param>
    /// <param name="name">파일 이름</param>
    private static void Take(Camera eye, RenderTexture target, Texture2D shot, int id,
                             Vector4 edge, string name)
    {
        Shader.SetGlobalVector(id, edge);

        eye.targetTexture = target;

        for (int i = 0; i < 3; i++)
        {
            eye.Render();
            while (ShaderUtil.anythingCompiling) System.Threading.Thread.Sleep(50);
        }

        RenderTexture previous = RenderTexture.active;
        RenderTexture.active = target;
        shot.ReadPixels(new Rect(0, 0, 960, 540), 0, 0);
        shot.Apply();
        RenderTexture.active = previous;
        eye.targetTexture = null;

        File.WriteAllBytes(Path.Combine(OutputDirectory, name + ".png"), shot.EncodeToPNG());
    }
}
