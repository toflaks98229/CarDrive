using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 메가스트럭처 스파인을 <b>실제 지형 위에</b> 앉힙니다.
///
/// <b>왜 그냥 놓으면 안 되는가.</b> 스파인은 1008 m 짜리 직선이고 데크는 수평이어야
/// 합니다 — 고가도로의 상판은 수평이고 <b>다리 길이가 다릅니다.</b> 그런데 베이는
/// 메시 하나라 다리 길이가 고정입니다. 그래서 지형 기복이 다리가 파묻힐 수 있는
/// 여유보다 크면 어느 다리는 공중에 뜨고 어느 다리는 땅속에 잠깁니다.
///
/// 그래서 <b>먼저 재고 나중에 놓습니다.</b> <see cref="Survey"/> 가 여러 후보 선을
/// 훑어 기복이 가장 작은 선을 찾고, <see cref="Place"/> 가 거기에 앉힙니다.
///
/// <code>
/// Unity.exe -batchmode -nographics -projectPath . -executeMethod MegastructurePlacer.Survey
/// Unity.exe -batchmode -nographics -projectPath . -executeMethod MegastructurePlacer.Place
/// </code>
/// </summary>
public static class MegastructurePlacer
{
    // --- Constants ---

    private const string ScenePath = "Assets/_Project/01.Scenes/SampleScene.unity";
    private const string SpinePath = "Assets/_Project/05.Prefabs/Megastructure/MegaSpine.prefab";
    private const string HolderName = "Megastructure";

    private const string ManifestPath =
        "Assets/_Project/04.Art/02.Models/Megastructure/presets.json";

    /// <summary>
    /// 블렌더가 낸 치수입니다. <b>여기 적어 두면 안 됩니다.</b>
    ///
    /// 다리가 파묻히는 깊이와 스파인의 폭을 C# 에 상수로 두었더니, 폭을 34 m 에서
    /// 96 m 로 넓힌 순간 배치기가 <b>없는 자리에서 지면을 재고</b> 출발 지점과의
    /// 거리도 옛 폭으로 판단했습니다. 만든 쪽이 적고 쓰는 쪽은 읽습니다.
    /// </summary>
    [Serializable]
    private class Manifest
    {
        public float bay;
        public float width;
        public float burial;
        public float deckTop;
    }

    private static Manifest _spec;

    private static float LegBurial { get { return _spec.burial; } }

    /// <summary>다리 중심선에서 바깥 다리까지의 거리(m)입니다.</summary>
    private static float LegOffset { get { return _spec.width * 0.5f - 6.5f; } }

    /// <summary>기복 여유에서 남겨 두는 안전 폭(m)입니다. 딱 맞으면 딱 맞을 뿐입니다.</summary>
    private const float Margin = 2.0f;

    /// <summary>출발 지점에서 떨어뜨릴 최소 거리(m)입니다. 폭의 절반에 여유를 더합니다.</summary>
    private static float Clear { get { return _spec.width * 0.5f + 24f; } }

    private const int Angles = 12;
    private const int Offsets = 9;

    // --- Public Methods ---

    public static void Survey()
    {
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        List<Line> lines = Rank(out Bounds world, out float length, out int bays, out Vector3 start);

        Debug.Log($"MegastructurePlacer: 지형 {Terrain.activeTerrains.Length} 장 · " +
                  $"가로 {world.size.x:F0} × {world.size.z:F0} m · 높이 {world.min.y:F1} ~ {world.max.y:F1} m");
        Debug.Log($"  스파인 {bays} 베이 × {length / bays:F1} m = {length:F0} m · " +
                  $"다리 여유 {LegBurial:F1} m · 출발 지점 ({start.x:F0}, {start.z:F0})");

        foreach (Line line in lines.Take(8))
        {
            Debug.Log($"  각 {line.angle,5:F0}° · 옆으로 {line.offset,7:F0} m → " +
                      $"지면 {line.low:F1} ~ {line.high:F1} m · 기복 {line.relief:F1} m · " +
                      $"출발점에서 {line.reach:F0} m {(line.Fits ? "◀ 가능" : "(기복 초과)")}");
        }

        if (Application.isBatchMode) EditorApplication.Exit(0);
    }

    /// <summary>
    /// 고른 선에 스파인을 앉히고 씬에 저장합니다.
    ///
    /// 높이는 <b>가장 높은 지면</b>에 맞춥니다. 평균에 맞추면 절반의 다리가 뜨는데,
    /// 뜬 다리는 한눈에 보이고 잠긴 다리는 안 보입니다. 어느 쪽으로 틀릴지 고를 수
    /// 있다면 <b>안 보이는 쪽</b>으로 틀립니다.
    /// </summary>
    public static void Place()
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        List<Line> lines = Rank(out Bounds world, out float length, out int bays, out Vector3 start);
        Line best = lines.FirstOrDefault(l => l.Fits);

        if (best.angle == 0f && best.offset == 0f && !best.Fits)
        {
            throw new Exception("놓을 수 있는 선을 찾지 못했습니다");
        }

        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (root.name == HolderName) UnityEngine.Object.DestroyImmediate(root);
        }

        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(SpinePath);
        GameObject spine = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        spine.name = HolderName;

        Vector3 middle = new Vector3(world.center.x, 0f, world.center.z) + Perp(best.angle) * best.offset;
        spine.transform.SetPositionAndRotation(
            new Vector3(middle.x, best.high, middle.z), Quaternion.Euler(0f, best.angle, 0f));

        Collide(scene, spine);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        Debug.Log($"MegastructurePlacer: 각 {best.angle:F0}° · 옆으로 {best.offset:F0} m · " +
                  $"바닥 {best.high:F1} m · 기복 {best.relief:F1} m · 출발점에서 {best.reach:F0} m");

        // 얼룩이 걸리는 높이는 <b>여기서만</b> 알 수 있습니다. 구조물이 지형의
        // 어느 높이에 앉을지는 재 봐야 나오고, 재는 것이 이 스크립트입니다.
        BrutalistTextureSetup.Ground(spine.transform.position.y);

        if (Application.isBatchMode) EditorApplication.Exit(0);
    }

    // --- Private Methods ---

    private struct Line
    {
        public float angle;
        public float offset;
        public float low;
        public float high;
        public float relief;
        public float reach;

        /// <summary>다리가 뜨지 않고, 출발 지점을 깔고 앉지도 않는가.</summary>
        public bool Fits { get { return relief <= LegBurial - Margin && reach >= Clear; } }
    }

    /// <summary>
    /// 후보 선을 훑어 <b>가까운 순으로</b> 돌려줍니다.
    ///
    /// <b>왜 가장 평탄한 선이 아닌가.</b> 다리 여유를 14 m 로 늘린 뒤로는 상위 후보가
    /// 전부 구조적으로 가능합니다. 그러면 기복은 더 이상 고르는 기준이 못 됩니다 —
    /// 통과 조건일 뿐입니다. 남는 기준은 <b>플레이어가 실제로 만나는가</b> 하나입니다.
    /// 세계 구석에 선 것은 아무 일도 하지 않습니다.
    ///
    /// 다만 출발 지점을 깔고 앉으면 안 됩니다. <see cref="Clear"/>
    /// 만큼은 떨어뜨립니다 — 차가 다리 안에서 시작하지 않을 거리입니다.
    /// </summary>
    private static List<Line> Rank(out Bounds world, out float length, out int bays, out Vector3 start)
    {
        _spec = JsonUtility.FromJson<Manifest>(System.IO.File.ReadAllText(ManifestPath));
        if (_spec == null || _spec.width <= 0f) throw new Exception("치수를 읽지 못했습니다");

        Terrain[] terrains = Terrain.activeTerrains;
        if (terrains.Length == 0) throw new Exception("지형을 찾지 못했습니다");

        world = WorldBounds(terrains);
        length = SpineShape();

        // 베이 수는 <b>프리팹의 자식 수가 아닙니다.</b> 자식에는 코어뿐 아니라 프리셋
        // 부품도 섞여 있어, 세면 실제 베이보다 훨씬 많이 나옵니다. 그 수로 표본을
        // 나누면 <b>다리가 서지 않는 자리</b>의 지면을 재게 됩니다 - 재야 하는 것은
        // 다리 발밑이므로 베이 길이로 나눕니다.
        bays = Mathf.Max(1, Mathf.RoundToInt(length / _spec.bay));
        start = PlayerStart();

        List<Line> lines = new List<Line>();
        Vector3 middle = new Vector3(world.center.x, 0f, world.center.z);

        for (int a = 0; a < Angles; a++)
        {
            float angle = a * 180f / Angles;

            for (int o = 0; o < Offsets; o++)
            {
                float offset = (o - (Offsets - 1) * 0.5f) * (world.size.x * 0.8f / Offsets);

                if (!Sample(world, angle, offset, length, bays, out float low, out float high)) continue;

                // 점에서 선까지의 거리는 <b>수직 성분의 차</b>입니다.
                float reach = Mathf.Abs(Vector3.Dot(start - middle, Perp(angle)) - offset);

                lines.Add(new Line
                {
                    angle = angle, offset = offset, low = low, high = high,
                    relief = high - low, reach = reach,
                });
            }
        }

        // 가능한 것을 먼저, <b>여유가 비슷하면</b> 가까운 순으로.
        //
        // 가깝다는 이유만으로 여유 없는 선을 고르면 안 됩니다. 다리는 어차피 파묻히니
        // 티가 안 나지만 <b>경사로가 지면에 못 닿습니다</b> — 경사로 발은 원점보다
        // 6.8 m 아래까지만 내려가는데, 기복이 크면 낮은 쪽 지면이 그 밑으로 빠지기
        // 때문입니다. 실제로 기복 13.8 m 짜리(한도 14 m 를 통과합니다) 선을 골랐더니
        // 경사로가 지면에서 <b>6.2 m 떠서</b> 데크에 올라탈 방법이 없었습니다.
        //
        // 그래서 기복을 <see cref="Margin"/> 단위로 뭉쳐 먼저 봅니다. 그 안에서만
        // 가까운 쪽이 이깁니다.
        return lines.OrderByDescending(l => l.Fits)
                    .ThenBy(l => Mathf.Ceil(l.relief / Margin))
                    .ThenBy(l => l.reach)
                    .ToList();
    }

    /// <summary>
    /// 스파인이 <b>기존에 서 있던 것들과 정말로 닿는지</b> 셉니다.
    ///
    /// 1344 m 짜리를 세계로 지르면 집이나 바위가 콘크리트 속에 박힐 수 있습니다.
    /// 박힌 집은 게임에서 바로 보이고, 배치를 저장한 뒤에는 찾기가 훨씬 어렵습니다.
    ///
    /// <b>바운딩 박스로 재면 안 됩니다.</b> 베이 하나의 상자는 42 × 34 × 96 m 이고
    /// 그 안의 대부분은 <b>데크 밑의 빈 공간</b>입니다. 처음에 그렇게 쟀더니 45 개가
    /// 겹친다고 나왔는데, 실제로는 대부분 그냥 구조물 아래에 서 있는 바위였습니다.
    /// 그것은 겹친 것이 아니라 <b>그늘에 든 것</b>이고, 오히려 있어야 하는 그림입니다.
    /// 그래서 콜라이더로 실제 접촉만 셉니다.
    ///
    /// 닿는다고 멈추지는 않습니다 — 메가스트럭처가 나중에 지어졌다면 먼저 있던 것을
    /// 뚫고 지나가는 편이 맞습니다. 다만 몇 개인지는 알고 있어야 합니다.
    /// </summary>
    private static void Collide(Scene scene, GameObject spine)
    {
        Physics.SyncTransforms();

        HashSet<Collider> mine = new HashSet<Collider>(spine.GetComponentsInChildren<Collider>(true));
        if (mine.Count == 0) return;

        List<string> hit = new List<string>();

        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (root == spine) continue;

            foreach (Renderer r in root.GetComponentsInChildren<Renderer>(true))
            {
                if (r.GetComponent<Terrain>() != null) continue;

                // 렌더러를 감싸는 구 하나로 물어봅니다. 소품은 대개 작아서 이 정도면
                // 콘크리트 속인지 그늘인지 갈립니다.
                float radius = Mathf.Max(r.bounds.extents.magnitude * 0.7f, 0.3f);

                foreach (Collider c in Physics.OverlapSphere(r.bounds.center, radius,
                                                             ~0, QueryTriggerInteraction.Ignore))
                {
                    if (!mine.Contains(c)) continue;
                    hit.Add(r.name);
                    break;
                }
            }
        }

        Debug.Log(hit.Count == 0
            ? "MegastructurePlacer: 콘크리트에 닿는 오브젝트 없음"
            : $"MegastructurePlacer: 닿는 오브젝트 {hit.Count} 개 — " +
              string.Join(", ", hit.Distinct().Take(12)));
    }

    /// <summary>차가 어디서 출발하는지입니다. 없으면 세계 한가운데로 봅니다.</summary>
    private static Vector3 PlayerStart()
    {
        foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
        {
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t.name.Contains("PlayerCar")) return t.position;
            }
        }

        return Vector3.zero;
    }

    private static Bounds WorldBounds(Terrain[] terrains)
    {
        Bounds b = new Bounds(terrains[0].transform.position + terrains[0].terrainData.size * 0.5f,
                              terrains[0].terrainData.size);

        foreach (Terrain t in terrains)
        {
            b.Encapsulate(t.transform.position);
            b.Encapsulate(t.transform.position + t.terrainData.size);
        }

        return b;
    }

    private static float SpineShape()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(SpinePath);
        if (prefab == null) throw new Exception("스파인 프리팹이 없습니다: " + SpinePath);

        Renderer[] renderers = prefab.GetComponentsInChildren<Renderer>(true);
        Bounds b = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);

        return b.size.z;
    }

    private static Vector3 Along(float angle)
    {
        return Quaternion.Euler(0f, angle, 0f) * Vector3.forward;
    }

    private static Vector3 Perp(float angle)
    {
        return Quaternion.Euler(0f, angle, 0f) * Vector3.right;
    }

    /// <summary>
    /// 한 선 위의 <b>모든 다리 발밑</b> 지면 높이를 재서 가장 낮은 곳과 높은 곳을 냅니다.
    ///
    /// 베이 중심이 아니라 다리 자리를 재는 것이 중요합니다. 중심만 재면 좌우로
    /// 11.5 m 떨어진 실제 다리 밑이 어떤지 모릅니다.
    /// </summary>
    private static bool Sample(Bounds world, float angle, float offset, float length, int bays,
                               out float low, out float high)
    {
        Vector3 along = Along(angle);
        Vector3 perp = Perp(angle);
        Vector3 middle = new Vector3(world.center.x, 0f, world.center.z) + perp * offset;

        low = float.MaxValue;
        high = float.MinValue;
        int on = 0;

        float step = length / bays;

        for (int i = 0; i < bays; i++)
        {
            Vector3 at = middle + along * ((i - bays * 0.5f + 0.5f) * step);

            // <b>다리가 네 줄</b>입니다. 바깥 둘만 재면 가운데 두 줄 밑의 지면을
            // 못 봅니다 - 폭이 96 m 라 그 사이에서 지형이 크게 오르내립니다.
            foreach (float lane in new[] { -1f, -0.35f, 0.35f, 1f })
            {
                Vector3 foot = at + perp * (LegOffset * lane);

                // 지형 밖은 <b>건너뜁니다.</b> 스파인은 일부러 세계보다 길어서 양 끝이
                // 밖으로 나갑니다 - 그것을 실패로 치면 놓을 수 있는 선이 하나도 남지
                // 않습니다. 밖으로 나간 부분은 보이지 않으므로 높이도 상관없습니다.
                if (!Ground(foot, out float y)) continue;

                low = Mathf.Min(low, y);
                high = Mathf.Max(high, y);
                on++;
            }
        }

        // 그래도 대부분은 땅 위에 있어야 합니다. 절반도 못 걸치면 세계를 스치고
        // 지나가는 선이라 볼 일이 없습니다.
        return on >= bays;
    }

    /// <summary>그 자리를 덮고 있는 지형 타일을 찾아 높이를 냅니다. 없으면 실패입니다.</summary>
    private static bool Ground(Vector3 at, out float y)
    {
        y = 0f;

        foreach (Terrain terrain in Terrain.activeTerrains)
        {
            Vector3 origin = terrain.transform.position;
            Vector3 size = terrain.terrainData.size;

            if (at.x < origin.x || at.x > origin.x + size.x) continue;
            if (at.z < origin.z || at.z > origin.z + size.z) continue;

            y = terrain.SampleHeight(at) + origin.y;
            return true;
        }

        return false;
    }
}
