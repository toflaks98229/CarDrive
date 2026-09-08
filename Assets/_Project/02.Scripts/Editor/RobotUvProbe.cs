using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 임포트된 FBX 의 <b>UV 가 월드 스케일인지</b>를 확인합니다.
///
/// 블렌더에서 UV 1.0 = 표면 1 m 로 구웠는데, FBX 를 거치는 동안 스케일이 바뀌거나
/// 채널이 뒤바뀌면 조용히 어긋납니다. 프리팹 YAML 로는 알 수 없고 렌더로도
/// 애매하므로, 삼각형마다 <b>UV 면적 ÷ 실제 면적</b>을 재서 1.0 인지 봅니다.
///
/// <code>
/// Unity.exe -batchmode -nographics -projectPath . -executeMethod RobotUvProbe.Run
/// </code>
/// </summary>
public static class RobotUvProbe
{
    private static readonly string[] Models =
    {
        "Assets/_Project/04.Art/02.Models/Robot/SM_Strider.fbx",
        "Assets/_Project/04.Art/02.Models/Robot/SM_Dreadnought.fbx",
    };

    public static void Run()
    {
        int errors = 0;

        foreach (string path in Models)
        {
            Mesh[] meshes = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Mesh>().ToArray();

            if (meshes.Length == 0)
            {
                Debug.LogError("RobotUvProbe: 메시가 없습니다: " + path);
                errors++;
                continue;
            }

            float low = float.MaxValue;
            float high = 0f;
            int missing = 0;

            foreach (Mesh mesh in meshes)
            {
                Vector2[] uv = mesh.uv;
                if (uv == null || uv.Length == 0)
                {
                    missing++;
                    continue;
                }

                Vector3[] v = mesh.vertices;
                int[] tris = mesh.triangles;

                double world = 0.0;
                double texel = 0.0;

                for (int i = 0; i < tris.Length; i += 3)
                {
                    Vector3 a = v[tris[i]], b = v[tris[i + 1]], c = v[tris[i + 2]];
                    world += Vector3.Cross(b - a, c - a).magnitude * 0.5;

                    Vector2 ua = uv[tris[i]], ub = uv[tris[i + 1]], uc = uv[tris[i + 2]];
                    texel += Mathf.Abs((ub.x - ua.x) * (uc.y - ua.y) - (uc.x - ua.x) * (ub.y - ua.y)) * 0.5;
                }

                if (world < 1e-9) continue;

                float density = (float)(texel / world);
                low = Mathf.Min(low, density);
                high = Mathf.Max(high, density);
            }

            if (missing > 0)
            {
                Debug.LogError($"RobotUvProbe: {path} — UV 없는 메시 {missing} 개");
                errors++;
            }

            float spread = high / Mathf.Max(low, 1e-6f);
            Debug.Log($"RobotUvProbe: {System.IO.Path.GetFileName(path)} — 메시 {meshes.Length} 개 · " +
                      $"밀도 {low:F3}~{high:F3} (1.0 이 월드 스케일) · 편차 {spread:F2}배");

            // 박스 투영이라 챔퍼 띠에서만 조금 눌립니다. 그 이상 벌어지면 규약이 깨진 것입니다.
            if (spread > 1.6f)
            {
                Debug.LogError($"RobotUvProbe: {path} — 밀도 편차 {spread:F2}배. 월드 스케일이 아닙니다");
                errors++;
            }
        }

        if (Application.isBatchMode) EditorApplication.Exit(errors > 0 ? 2 : 0);
    }
}
