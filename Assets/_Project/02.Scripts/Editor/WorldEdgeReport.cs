using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// <b>세계의 끝이 어디인가</b>를 잽니다.
///
/// 이 세계의 대지는 자연 지형이 아니라 거대 건축물의 인공 지반 한 조각이라는
/// 설정이라, 가장자리에 <b>테두리 골조</b>를 세워야 합니다. 그런데 지형은 타일
/// 103 장으로 흩어져 있고 격자(x -3~7, y -3~8)가 <b>꽉 차 있지 않습니다</b> -
/// 132 칸 중 103 장뿐입니다. 그러면 가장자리가 직사각형이 아니라 <b>들쭉날쭉한
/// 계단</b>이고, 그 모양을 모르고서는 테두리를 세울 수 없습니다.
///
/// 타일 하나하나의 자리·크기와, 이웃이 없는 <b>바깥면</b>을 뽑아 남깁니다.
/// 바깥면이 곧 테두리를 세울 자리입니다.
///
/// <code>
/// Unity.exe -batchmode -projectPath . -executeMethod WorldEdgeReport.Run -quit
/// </code>
/// </summary>
public static class WorldEdgeReport
{
    private const string ScenePath = "Assets/_Project/01.Scenes/SampleScene.unity";

    public static void Run()
    {
        int errors = 0;

        try
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            Terrain[] tiles = UnityEngine.Object
                .FindObjectsByType<Terrain>(FindObjectsInactive.Include, FindObjectsSortMode.None);

            if (tiles.Length == 0)
            {
                Debug.LogError("WorldEdgeReport: 지형이 없습니다");
                errors++;
            }
            else
            {
                Describe(tiles);
            }
        }
        catch (Exception e)
        {
            Debug.LogError("WorldEdgeReport: " + e);
            errors++;
        }

        if (Application.isBatchMode) EditorApplication.Exit(errors > 0 ? 2 : 0);
    }

    private static void Describe(Terrain[] tiles)
    {
        Vector3 size = tiles[0].terrainData.size;

        float step = size.x;

        // 타일을 격자 칸으로 바꿉니다. 자리를 타일 크기로 나눈 것이 칸 번호입니다.
        HashSet<Vector2Int> cells = new HashSet<Vector2Int>();
        Dictionary<Vector2Int, Terrain> at = new Dictionary<Vector2Int, Terrain>();

        foreach (Terrain t in tiles)
        {
            Vector3 p = t.transform.position;
            Vector2Int cell = new Vector2Int(
                Mathf.RoundToInt(p.x / step), Mathf.RoundToInt(p.z / size.z));

            cells.Add(cell);
            at[cell] = t;
        }

        float lowX = tiles.Min(t => t.transform.position.x);
        float lowZ = tiles.Min(t => t.transform.position.z);
        float highX = tiles.Max(t => t.transform.position.x) + size.x;
        float highZ = tiles.Max(t => t.transform.position.z) + size.z;

        // 이웃이 없는 면을 셉니다. 여기가 <b>세계의 끝</b>입니다.
        Vector2Int[] around =
        {
            new Vector2Int(1, 0), new Vector2Int(-1, 0),
            new Vector2Int(0, 1), new Vector2Int(0, -1),
        };

        int open = 0;
        List<float> rimHeights = new List<float>();
        List<float> spreads = new List<float>();

        foreach (Vector2Int cell in cells)
        {
            foreach (Vector2Int step2 in around)
            {
                if (cells.Contains(cell + step2)) continue;

                open++;

                Terrain t = at[cell];
                Vector3 middle = t.transform.position
                                 + new Vector3(size.x * 0.5f, 0f, size.z * 0.5f)
                                 + new Vector3(step2.x, 0f, step2.y) * (size.x * 0.48f);

                rimHeights.Add(t.SampleHeight(middle) + t.transform.position.y);

                // <b>한 면 안에서 지형이 얼마나 오르내리는가.</b> 테두리 모듈을
                // 몇 조각으로 끊을지, 잘린 단면을 얼마나 깊게 둘지가 이 숫자에
                // 달려 있습니다 - 조각 하나가 덮어야 할 높이차이기 때문입니다.
                Vector3 along = new Vector3(step2.y, 0f, step2.x) * (size.x * 0.5f);

                float low = float.MaxValue;
                float high = float.MinValue;

                for (int k = 0; k <= 10; k++)
                {
                    Vector3 spot = middle + along * (k / 5f - 1f);
                    float y = t.SampleHeight(spot) + t.transform.position.y;

                    low = Mathf.Min(low, y);
                    high = Mathf.Max(high, y);
                }

                spreads.Add(high - low);
            }
        }

        rimHeights.Sort();
        spreads.Sort();

        Vector3 player = GameObject.FindWithTag("Player") != null
            ? GameObject.FindWithTag("Player").transform.position
            : Vector3.zero;

        Debug.Log(string.Format(
            "WorldEdgeReport:\n" +
            "  타일 {0} 장 · 한 장 {1:F0} x {2:F0} m · 높이 범위 {3:F0} m\n" +
            "  세계 x {4:F0} ~ {5:F0} · z {6:F0} ~ {7:F0}  (곧 {8:F0} x {9:F0} m)\n" +
            "  이웃 없는 바깥면 {10} 개 — 테두리를 세울 자리입니다\n" +
            "  바깥면 지형 높이 — 최저 {11:F1} · 중앙 {12:F1} · 최고 {13:F1} m\n" +
            "  한 면(100 m) 안의 높이차 — 중앙 {16:F1} · 90% {17:F1} · 최대 {18:F1} m\n" +
            "  플레이어 {14} · 가장 가까운 끝까지 {15:F0} m",
            tiles.Length, size.x, size.z, size.y,
            lowX, highX, lowZ, highZ, highX - lowX, highZ - lowZ,
            open,
            rimHeights.Count > 0 ? rimHeights[0] : 0f,
            rimHeights.Count > 0 ? rimHeights[rimHeights.Count / 2] : 0f,
            rimHeights.Count > 0 ? rimHeights[rimHeights.Count - 1] : 0f,
            player,
            Mathf.Min(player.x - lowX, highX - player.x,
                      player.z - lowZ, highZ - player.z),
            spreads[spreads.Count / 2], spreads[(int)(spreads.Count * 0.9f)],
            spreads[spreads.Count - 1]));
    }
}
