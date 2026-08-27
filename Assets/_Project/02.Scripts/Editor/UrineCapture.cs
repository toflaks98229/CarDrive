using System.Globalization;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// 소변 줄기와 <b>떨어지는 지점</b>을 찍습니다.
///
/// <b>왜 따로 필요한가.</b> SceneLookCapture 는 정지 화면을 찍는데, 파티클은 시간이
/// 흘러야 모양이 나옵니다. 여기서는 <c>ParticleSystem.Simulate</c> 로 시간을 감아
/// 줄기가 땅에 닿은 뒤의 상태를 잡습니다.
///
/// 빈 씬에 바닥 판 하나와 줄기를 세웁니다. 본 씬을 열면 지형 103장이 따라오고,
/// 무엇보다 줄기가 어디로 날아가는지 카메라를 맞추기 어렵습니다.
///
/// 쓰는 법 (그래픽 장치가 필요하므로 -nographics 를 붙이지 않습니다):
///   Unity.exe -batchmode -projectPath . -executeMethod UrineCapture.Run
/// 결과: Logs/Urine/*.png
/// </summary>
public static class UrineCapture
{
    private const string ScenePath = "Assets/_Project/01.Scenes/SampleScene.unity";
    private const string OutputDirectory = "Logs/Urine";
    private const int Width = 900;
    private const int Height = 520;

    /// <summary>감을 시간(초). 줄기가 날아가 땅에 닿고 튀기까지.</summary>
    private static readonly float[] Moments = { 0.35f, 0.8f, 1.6f };

    public static void Run()
    {
        Directory.CreateDirectory(OutputDirectory);
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        ParticleSystem stream = null;
        foreach (ParticleSystem ps in Object.FindObjectsByType<ParticleSystem>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (ps.name == "UrineStream") { stream = ps; break; }
        if (stream == null) { Debug.Log("URINECAP UrineStream 을 못 찾음"); EditorApplication.Exit(1); return; }

        // 줄기를 빈 곳으로 옮겨 세웁니다. 원래 자리는 차 안이라 시야가 막힙니다.
        Transform t = stream.transform;
        Transform parent = t.parent;
        Vector3 keepPos = t.position;
        Quaternion keepRot = t.rotation;
        t.SetParent(null, true);
        t.position = new Vector3(0f, 1.0f, 0f);
        t.rotation = Quaternion.Euler(40f, 0f, 0f);   // 실제 배뇨 각도에 가깝게

        // 받아 줄 바닥. 충돌이 World 라 콜라이더가 있어야 튑니다.
        GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
        ground.transform.position = Vector3.zero;
        ground.transform.localScale = new Vector3(2f, 1f, 2f);
        Renderer gr = ground.GetComponent<Renderer>();
        Material gm = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        gm.color = new Color(0.42f, 0.33f, 0.22f, 1f);   // 흙빛 바닥
        gr.sharedMaterial = gm;

        GameObject camObj = new GameObject("UrineCam");
        Camera cam = camObj.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.52f, 0.55f, 0.60f, 1f);
        cam.fieldOfView = 50f;
        camObj.transform.position = new Vector3(1.9f, 0.95f, -0.5f);
        camObj.transform.LookAt(new Vector3(0f, 0.3f, 0.85f));

        RenderTexture target = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32);

        ParticleSystem.EmissionModule em = stream.emission;
        bool keepEmission = em.enabled;
        em.enabled = true;   // 게임에서는 코드가 직접 Emit 하지만, 여기서는 스스로 뿜게 둡니다.
        ParticleSystem.MinMaxCurve keepRate = em.rateOverTime;
        em.rateOverTime = new ParticleSystem.MinMaxCurve(160f);

        for (int i = 0; i < Moments.Length; i++)
        {
            float seconds = Moments[i];
            stream.Clear(true);
            stream.Simulate(seconds, true, true, true);

            cam.targetTexture = target;
            cam.Render();
            cam.targetTexture = null;

            Texture2D shot = new Texture2D(Width, Height, TextureFormat.RGB24, false);
            RenderTexture prev = RenderTexture.active;
            RenderTexture.active = target;
            shot.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
            shot.Apply();
            RenderTexture.active = prev;

            string name = string.Format(CultureInfo.InvariantCulture, "t{0:0.00}", seconds).Replace('.', '_');
            File.WriteAllBytes(Path.Combine(OutputDirectory, name + ".png"), shot.EncodeToPNG());
            // <b>편집 모드에서는 충돌이 돌지 않습니다.</b> Simulate 는 물리 질의를 하지 않아
            // 충돌 서브이미터가 영영 불리지 않습니다 — 게임에서는 배선대로 돕니다.
            // 튄 물의 <b>그림</b>만이라도 보려고 여기서 직접 뿜습니다.
            ParticleSystem sp = null;
            Transform spt = stream.transform.Find("UrineSplash");
            if (spt != null) sp = spt.GetComponent<ParticleSystem>();
            if (sp != null)
            {
                sp.Clear(true);
                sp.Play(true);
                ParticleSystem.EmitParams ep = new ParticleSystem.EmitParams();
                for (int n = 0; n < 26; n++)
                {
                    ep.position = new Vector3(Random.Range(-0.12f, 0.12f), 0.02f,
                                              0.95f + Random.Range(-0.14f, 0.14f));
                    ep.applyShapeToPosition = true;
                    sp.Emit(ep, 1);
                }
                sp.Simulate(Mathf.Min(seconds, 0.22f), true, false, true);
            }
            Debug.Log(string.Format(CultureInfo.InvariantCulture,
                "URINECAP {0}s 줄기={1} 튄물={2}",
                seconds, stream.particleCount, sp != null ? sp.particleCount : -1));
            Object.DestroyImmediate(shot);
        }

        em.rateOverTime = keepRate;
        em.enabled = keepEmission;
        t.SetParent(parent, true);
        t.position = keepPos;
        t.rotation = keepRot;

        cam.targetTexture = null;
        target.Release();
        Object.DestroyImmediate(target);
        Object.DestroyImmediate(camObj);
        Object.DestroyImmediate(ground);
        Object.DestroyImmediate(gm);

        Debug.Log("URINECAP 끝. 그림은 " + OutputDirectory);
        EditorApplication.Exit(0);   // 씬은 저장하지 않습니다.
    }
}
