using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 메가스트럭처를 <b>진짜 씬에서</b> 찍습니다.
///
/// <see cref="MegastructureSetup.Preview"/> 는 빈 씬에 제가 만든 빛을 켜고 찍습니다.
/// 만든 것이 서로 맞물리는지는 그것으로 충분하지만, <b>어떤 값이 맞는지</b>는 그것으로
/// 판단하면 안 됩니다 - 게임에는 볼륨의 노출, 툰 램프, 팔레트, 안개가 걸려 있고 그
/// 넷이 색과 명도를 전부 다시 씁니다. 실제로 알베도를 0.58 에서 0.66 으로 올린 뒤
/// 미리보기에서는 하얗게 날아갔는데, 그것은 미리보기의 빛이 1.4 여서였습니다.
///
/// <see cref="SceneLookCapture"/> 는 <b>차가 선 자리</b>에서 찍으므로 여기에 쓸 수
/// 없습니다. 메가스트럭처는 출발 지점에서 416 m 떨어져 있고 안개는 280 m 에서 끝나
/// 화면에 아예 안 들어옵니다.
///
/// ⚠ 렌더가 필요하므로 <c>-nographics</c> 를 붙이면 안 됩니다.
/// <code>
/// Unity.exe -batchmode -projectPath . -executeMethod MegastructureLookCapture.Run
/// </code>
/// </summary>
public static class MegastructureLookCapture
{
    private const string ScenePath = "Assets/_Project/01.Scenes/SampleScene.unity";
    private const string ManifestPath =
        "Assets/_Project/04.Art/02.Models/Megastructure/presets.json";
    private const string HolderName = "Megastructure";
    private const string OutputDirectory = "Logs/MegaLook";

    private const int Width = 1280;
    private const int Height = 720;

    [Serializable]
    private class Manifest
    {
        public float burial;
        public float deckTop;
        public float width;
    }

    public static void Run()
    {
        int errors = 0;

        try
        {
            Directory.CreateDirectory(OutputDirectory);

            EditorSettings.asyncShaderCompilation = false;
            ShaderUtil.allowAsyncCompilation = false;

            Manifest spec = JsonUtility.FromJson<Manifest>(File.ReadAllText(ManifestPath));
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            Transform spine = scene.GetRootGameObjects()
                .FirstOrDefault(g => g.name == HolderName)?.transform;

            if (spine == null) throw new Exception("씬에 메가스트럭처가 없습니다");

            Camera source = Camera.allCameras.FirstOrDefault()
                            ?? UnityEngine.Object.FindAnyObjectByType<Camera>(
                                FindObjectsInactive.Include);

            if (source == null) throw new Exception("씬에 카메라가 없습니다");

            // <b>본 카메라를 복제합니다.</b> 화각·클립·포스트 처리 설정이 그대로
            // 따라와야 "게임에서 이렇게 보인다" 가 됩니다. 새로 만들면 그 셋이
            // 기본값이 되어 또 다른 거짓말을 합니다.
            GameObject rig = UnityEngine.Object.Instantiate(source.gameObject);
            rig.name = "MegaLookCam";

            foreach (Transform t in rig.GetComponentsInChildren<Transform>(true))
            {
                if (t != rig.transform) UnityEngine.Object.DestroyImmediate(t.gameObject);
            }

            Camera camera = rig.GetComponent<Camera>();
            camera.enabled = false;

            float deck = spine.position.y + spec.deckTop;
            Vector3 along = spine.forward;
            Vector3 side = spine.right;

            Bounds all = Bounds(spine);
            Vector3 middle = new Vector3(all.center.x, 0f, all.center.z);

            RenderTexture target = new RenderTexture(Width, Height, 24,
                RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);

            try
            {
                // 노면. 이 게임에서 <b>가장 오래 보게 될</b> 화면입니다.
                Shoot(camera, target, middle + Vector3.up * (deck + 1.35f) - side * 11f,
                      along + Vector3.down * 0.03f, "road");

                // 데크 밑. 차로 지나가며 보는 각이고, 지금 가장 어두운 곳입니다.
                Shoot(camera, target,
                      middle + Vector3.up * (spine.position.y + 2.0f) - along * 120f,
                      along, "under");

                // 경사로. 지상에서 데크로 올라가는 입구가 <b>길로 보이는지</b>.
                Renderer ramp = spine.GetComponentsInChildren<Renderer>(true)
                    .FirstOrDefault(r => r.name.Contains("Ramp"));

                if (ramp != null)
                {
                    Vector3 at = ramp.bounds.center;
                    Vector3 eye = new Vector3(at.x, spine.position.y + 6f, at.z)
                                  - side * 92f - along * 30f;
                    Shoot(camera, target, eye, (at - eye).normalized, "ramp");
                }

                // 멀리서. 지형 위에 선 것이 <b>지표로 읽히는지</b>.
                Shoot(camera, target,
                      middle + Vector3.up * (spine.position.y + 26f) + side * 250f,
                      (-side + along * 0.5f).normalized, "far");
            }
            finally
            {
                camera.targetTexture = null;
                target.Release();
                UnityEngine.Object.DestroyImmediate(target);
                UnityEngine.Object.DestroyImmediate(rig);
            }

            Debug.Log("MegastructureLookCapture: 저장 — " + OutputDirectory);
        }
        catch (Exception e)
        {
            Debug.LogError("MegastructureLookCapture: " + e);
            errors++;
        }

        // 씬은 저장하지 않습니다. 찍으려고 만든 카메라가 남으면 안 됩니다.
        if (Application.isBatchMode) EditorApplication.Exit(errors > 0 ? 2 : 0);
    }

    // --- Private Methods ---

    private static void Shoot(Camera camera, RenderTexture target,
                              Vector3 at, Vector3 look, string name)
    {
        camera.transform.SetPositionAndRotation(
            at, Quaternion.LookRotation(look.normalized, Vector3.up));

        camera.targetTexture = target;
        camera.Render();
        camera.targetTexture = null;

        Texture2D shot = new Texture2D(Width, Height, TextureFormat.RGB24, false);
        RenderTexture previous = RenderTexture.active;
        RenderTexture.active = target;
        shot.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
        shot.Apply();
        RenderTexture.active = previous;

        File.WriteAllBytes(Path.Combine(OutputDirectory, name + ".png"), shot.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(shot);

        Debug.Log($"  {name,-6} ({at.x:F0}, {at.y:F0}, {at.z:F0})");
    }

    private static Bounds Bounds(Transform spine)
    {
        Renderer[] renderers = spine.GetComponentsInChildren<Renderer>(true);
        Bounds box = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) box.Encapsulate(renderers[i].bounds);
        return box;
    }
}
