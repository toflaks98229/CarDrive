using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 메가스트럭처 데크를 <b>정말로 달릴 수 있는지</b> 잽니다.
///
/// <b>왜 눈으로는 안 되는가.</b> 데크가 그려져 있는 것과 그 위를 차가 갈 수 있는 것은
/// 다른 문제입니다. 메시 콜라이더가 붙어 있어도 경사로 꼭대기가 데크에 닿지 않으면
/// 못 올라가고, 난간이 가로막으면 올라가서도 못 들어갑니다. 경사로 밑이 지형보다
/// 높이 떠 있으면 애초에 진입할 수 없습니다.
///
/// <b>처음 판은 셋 다 잘못 쟀습니다.</b> 위에서 쏜 광선이 데크가 아니라 <b>맨 위
/// 지붕</b>(171 m)을 맞았고, 경사로 "발밑"은 주행면이 아니라 <b>땅에 묻힌 다리</b>를
/// 쟀고, 스파인 전체를 대상으로 삼아 <b>이웃 프리셋</b>까지 섞여 들어왔습니다. 그래서
/// 지금은 노면 높이를 프리셋 목록에서 읽어 그 근처의 면만 고르고, 경사로는 자기
/// 콜라이더만 봅니다.
///
/// <code>
/// Unity.exe -batchmode -nographics -projectPath . -executeMethod MegastructureDriveCheck.Run
/// </code>
/// </summary>
public static class MegastructureDriveCheck
{
    // --- Constants ---

    private const string ScenePath = "Assets/_Project/01.Scenes/SampleScene.unity";
    private const string ManifestPath = "Assets/_Project/04.Art/02.Models/Megastructure/presets.json";
    private const string HolderName = "Megastructure";
    private const string RampMesh = "SM_Mega_Ramp";

    /// <summary>차가 넘을 수 있는 단차(m)입니다. 이보다 크면 걸립니다.</summary>
    private const float Step = 0.45f;

    /// <summary>노면으로 인정할 높이 오차(m)입니다. 이 밖은 지붕이거나 밑판입니다.</summary>
    private const float Band = 3.0f;

    private const float Sky = 300f;

    [Serializable]
    private class Manifest
    {
        public float bay;
        public float width;
        public float deckTop;
        public float gate;
    }

    // --- Public Methods ---

    public static void Run()
    {
        Manifest manifest = JsonUtility.FromJson<Manifest>(
            System.IO.File.ReadAllText(ManifestPath));

        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        Physics.SyncTransforms();

        Transform spine = null;
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (root.name == HolderName) spine = root.transform;
        }

        if (spine == null) throw new Exception("씬에 메가스트럭처가 없습니다");

        float deck = spine.position.y + manifest.deckTop;
        Debug.Log($"MegastructureDriveCheck: 데크 노면 {deck:F1} m · 스파인 바닥 {spine.position.y:F1} m");

        Deck(spine, deck, manifest);
        Edges(spine, deck, manifest);
        Ramps(spine, deck);

        if (Application.isBatchMode) EditorApplication.Exit(0);
    }

    // --- Private Methods ---

    /// <summary>
    /// 데크 노면을 스파인 방향으로 훑습니다.
    ///
    /// 가운데 한 줄만 보면 안 됩니다 — 가운데가 이어져 있어도 폭이 좁아지는 곳이
    /// 있으면 차선이 끊깁니다. 그래서 좌우로도 나눠 쏩니다.
    /// </summary>
    private static void Deck(Transform spine, float deck, Manifest manifest)
    {
        Vector3 along = spine.forward;
        Vector3 side = spine.right;

        // <b>조각의 자리에서 길이를 뽑습니다.</b> 회전한 스파인의 AABB 를 쓰면 옆으로
        // 82 m 뻗은 Overpass 가 x·z 를 함께 부풀려 양 끝이 허공인 표본이 생깁니다.
        float first = float.MaxValue;
        float last = float.MinValue;

        foreach (Transform child in spine)
        {
            float z = spine.InverseTransformPoint(child.position).z;
            first = Mathf.Min(first, z);
            last = Mathf.Max(last, z);
        }

        float length = last - first;
        float middle = (first + last) * 0.5f;
        int samples = Mathf.CeilToInt(length / 3f);
        float half = manifest.width * 0.5f - 2.5f;

        foreach (float offset in new[] { -half, 0f, half })
        {
            List<float> heights = new List<float>();
            int missed = 0;
            int steps = 0;
            float worst = 0f;

            for (int i = 0; i < samples; i++)
            {
                float t = middle + (i / (float)(samples - 1) - 0.5f) * length;
                Vector3 at = spine.position + along * t + side * offset;

                if (!SurfaceNear(at, spine, deck, 2.5f, out float y, out _))
                {
                    missed++;
                    heights.Add(float.NaN);
                    continue;
                }

                if (heights.Count > 0 && !float.IsNaN(heights[heights.Count - 1]))
                {
                    float jump = Mathf.Abs(y - heights[heights.Count - 1]);
                    if (jump > Step) { steps++; worst = Mathf.Max(worst, jump); }
                }

                heights.Add(y);
            }

            List<float> good = heights.Where(h => !float.IsNaN(h)).ToList();

            // <b>가장 긴 끊기지 않은 구간</b>이 실제로 달릴 수 있는 거리입니다.
            // 전체 표본 수는 회전한 스파인의 AABB 에서 나오므로 양 끝이 허공입니다.
            int run = 0;
            int best = 0;
            int breaks = 0;
            bool wasHit = false;

            foreach (float h in heights)
            {
                bool now = !float.IsNaN(h);
                if (!now && wasHit) breaks++;
                run = now ? run + 1 : 0;
                best = Mathf.Max(best, run);
                wasHit = now;
            }

            Debug.Log($"  노면 {offset,5:F1} m 줄 : 닿음 {good.Count}/{samples} · " +
                      $"끊긴 곳 {breaks} · 가장 긴 연속 {best * 3f:F0} m · " +
                      $"단차 {steps} 곳(최대 {worst:F2} m) · " +
                      $"높이 {(good.Count > 0 ? good.Min() : 0f):F1} ~ {(good.Count > 0 ? good.Max() : 0f):F1} m");
        }
    }

    /// <summary>
    /// 데크 가장자리가 <b>어디에서 열려 있는지</b> 봅니다.
    ///
    /// 난간이 이어져 있으면 데크에서 내려올 방법이 없고, 경사로에서 올라온 차도
    /// 못 들어갑니다. 그래서 범퍼 높이에서 <b>옆으로</b> 쏴 막혔는지 봅니다 —
    /// 위에서 쏘는 것으로는 난간이 있는지 없는지 알 수 없습니다.
    /// </summary>
    private static void Edges(Transform spine, float deck, Manifest manifest)
    {
        Vector3 along = spine.forward;
        Vector3 side = spine.right;

        float first = float.MaxValue;
        float last = float.MinValue;

        foreach (Transform child in spine)
        {
            float z = spine.InverseTransformPoint(child.position).z;
            first = Mathf.Min(first, z);
            last = Mathf.Max(last, z);
        }

        float length = last - first;
        float middle = (first + last) * 0.5f;
        int samples = Mathf.CeilToInt(length / 2f);
        float reach = manifest.width * 0.5f + 6f;

        foreach (float sign in new[] { -1f, 1f })
        {
            int open = 0;
            int best = 0;
            int run = 0;

            for (int i = 0; i < samples; i++)
            {
                float t = middle + (i / (float)(samples - 1) - 0.5f) * length;

                // 높이는 <b>더하는 것이 아니라 정하는 것</b>입니다. 처음에 스파인의
                // 바닥 높이에 데크 높이를 더해 20 m 위 허공에서 쏘았고, 그래서
                // 난간의 77% 가 "열려 있다" 고 나왔습니다.
                Vector3 at = spine.position + along * t;
                at.y = deck + 0.7f;

                bool blocked = false;

                foreach (RaycastHit hit in Physics.RaycastAll(at, side * sign, reach))
                {
                    if (hit.collider.GetComponentInParent<Terrain>() != null) continue;
                    if (!hit.collider.transform.IsChildOf(spine)) continue;

                    blocked = true;
                    break;
                }

                run = blocked ? 0 : run + 1;
                if (!blocked) open++;
                best = Mathf.Max(best, run);
            }

            Debug.Log($"  가장자리 {(sign < 0 ? "왼" : "오른")}쪽 : 열린 표본 {open}/{samples} " +
                      $"({open * 2f:F0} m) · 가장 긴 열린 구간 {best * 2f:F0} m " +
                      $"{(open > 0 ? "" : "◀ 전부 막힘 — 뛰어내릴 수 없음")}");
        }
    }

    /// <summary>
    /// 경사로마다 <b>지면에서 데크까지 이어지는지</b> 봅니다.
    ///
    /// 자기 콜라이더만 봅니다. 스파인 전체를 대상으로 삼으면 대각선으로 놓인 이웃
    /// 프리셋이 같은 x/z 를 덮어 남의 지붕을 경사로라고 부르게 됩니다.
    /// </summary>
    private static void Ramps(Transform spine, float deck)
    {
        List<Transform> ramps = new List<Transform>();

        foreach (Transform child in spine)
        {
            if (child.name.StartsWith(RampMesh)) ramps.Add(child);
        }

        Debug.Log($"MegastructureDriveCheck: 경사로 {ramps.Count} 곳");

        foreach (Transform ramp in ramps)
        {
            Bounds box = Measure(ramp.gameObject);

            float low = float.MaxValue;
            float high = float.MinValue;
            Vector3 lowest = box.center;
            int hit = 0;
            int landing = 0;

            // <b>차가 올라타는 자리</b>까지의 단차입니다. 경사로는 일부러 지면 아래에서
            // 시작하므로 "가장 낮은 점"은 파묻힌 곳이고, 올라타는 곳은 경사면이 땅을
            // 뚫고 나오는 자리입니다. 그 자리에서 지면과 얼마나 어긋나는지를 봅니다.
            float entry = float.MaxValue;

            for (int ix = 0; ix <= 40; ix++)
            {
                for (int iz = 0; iz <= 40; iz++)
                {
                    Vector3 at = new Vector3(
                        Mathf.Lerp(box.min.x, box.max.x, ix / 40f), 0f,
                        Mathf.Lerp(box.min.z, box.max.z, iz / 40f));

                    if (!Lowest(at, ramp, spine.position.y - 6f, deck + 0.4f, out float y)) continue;

                    float here = Mathf.Abs(Ground(new Vector3(at.x, y, at.z)));
                    if (!float.IsNaN(here) && here < entry) entry = here;

                    if (y < low) { low = y; lowest = new Vector3(at.x, y, at.z); }
                    if (y > high) high = y;

                    // 데크 높이에 닿는 면이 있으면 <b>거기서 갈아탈 수 있습니다.</b>
                    if (SurfaceNear(at, ramp, deck, 0.9f, out _, out _)) landing++;

                    hit++;
                }
            }

            // 지형 타일 밖에 선 경사로는 잴 것이 없습니다. 스파인은 일부러 세계보다
            // 길어서 양 끝이 밖으로 나가므로, 이것은 결함이 아니라 설계입니다.
            bool onLand = entry < float.MaxValue;

            Debug.Log($"  {ramp.name,-24} 주행면 {low:F1} ~ {high:F1} m ({hit} 점) · " +
                      $"데크에 닿는 면 {landing,3} 점 · " +
                      $"지면과 만나는 최소 단차 {(onLand ? entry.ToString("F2") : "지형 밖"),8} m " +
                      $"{(landing > 0 ? "" : "◀ 데크에 못 닿음")}" +
                      $"{(!onLand || entry <= Step ? "" : " ◀ 지면과 안 만남")}");
        }
    }

    /// <summary>
    /// <paramref name="want"/> 높이 <b>근처의</b> 위를 보는 면을 찾습니다.
    ///
    /// <b>하늘에서 한 번 쏘면 안 됩니다.</b> 유니티의 <c>RaycastAll</c> 은 콜라이더
    /// 하나당 한 번만 보고합니다. 프리셋 한 조각이 메시 하나짜리 콜라이더이므로,
    /// 위에서 쏘면 탱크나 상부 골조의 꼭대기만 잡히고 <b>그 밑의 데크는 영영 안
    /// 나옵니다.</b> 실제로 이 때문에 데크의 절반이 "없다" 고 나왔습니다 — 없던 것이
    /// 아니라 못 본 것이었습니다.
    ///
    /// 그래서 찾는 높이 바로 위에서 짧게 쏩니다. 창을 좁히면 그 높이의 면만 걸립니다.
    /// </summary>
    private static bool SurfaceNear(Vector3 at, Transform root, float want, float window,
                                    out float y, out Vector3 normal)
    {
        y = 0f;
        normal = Vector3.up;

        Vector3 from = new Vector3(at.x, want + window, at.z);
        float best = float.MaxValue;
        bool found = false;

        foreach (RaycastHit hit in Physics.RaycastAll(from, Vector3.down, window * 2f))
        {
            if (hit.collider.GetComponentInParent<Terrain>() != null) continue;
            if (!hit.collider.transform.IsChildOf(root)) continue;
            if (Vector3.Dot(hit.normal, Vector3.up) < 0.55f) continue;

            float score = Mathf.Abs(hit.point.y - want);
            if (score >= best) continue;

            best = score;
            y = hit.point.y;
            normal = hit.normal;
            found = true;
        }

        return found;
    }

    /// <summary>
    /// 한 자리에서 <b>바닥부터 데크까지</b> 층층이 훑어 가장 낮은 주행면을 찾습니다.
    ///
    /// 콜라이더 하나당 한 번이라는 제약 때문에, 여러 높이를 나눠 쏘는 것 말고는
    /// 한 기둥 안의 여러 층을 볼 방법이 없습니다.
    /// </summary>
    private static bool Lowest(Vector3 at, Transform root, float floor, float ceiling,
                               out float y)
    {
        for (float level = floor; level <= ceiling; level += 1.5f)
        {
            if (SurfaceNear(at, root, level, 0.9f, out y, out _)) return true;
        }

        y = 0f;
        return false;
    }

    /// <summary>그 자리의 <b>지형</b>이 얼마나 아래에 있는지입니다.</summary>
    private static float Ground(Vector3 at)
    {
        foreach (Terrain terrain in Terrain.activeTerrains)
        {
            Vector3 origin = terrain.transform.position;
            Vector3 size = terrain.terrainData.size;

            if (at.x < origin.x || at.x > origin.x + size.x) continue;
            if (at.z < origin.z || at.z > origin.z + size.z) continue;

            return at.y - (terrain.SampleHeight(at) + origin.y);
        }

        return float.NaN;
    }

    private static Bounds Measure(GameObject go)
    {
        Renderer[] renderers = go.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0) return new Bounds(go.transform.position, Vector3.one);

        Bounds b = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);
        return b;
    }
}
