using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 프리셋을 파츠로 나눈 것이 <b>정말로 이득인지</b> 잽니다.
///
/// 나누면 좋아 보이지만 공짜가 아닙니다. 이 프로젝트의 병목은 삼각형이 아니라
/// <b>드로우 제출</b>(스탠드얼론 프로파일에서 렌더 스레드의 87%)이라, 렌더러를
/// 늘리는 것 자체가 비용입니다. 그러니 "나눴다" 가 아니라 <b>같은 시점에서 얼마나
/// 걸러지는가</b>를 재야 합니다.
///
/// 재는 법은 한 프리셋 인스턴스마다 두 번 묻는 것입니다:
///   · 합쳐 두었다면 — 자식 전부를 감싸는 상자 하나가 화면에 걸리는가. 걸리면
///     그 프리셋의 삼각형 <b>전부</b>가 그려집니다.
///   · 지금 — 파츠 상자를 하나씩 물어, <b>살아 있는 LOD 단계</b>의 것만 셉니다.
///
/// 그러므로 "덜 그림" 은 <b>나눈 것과 LOD 를 합친 결과</b>입니다. 기준(합쳤다면)은
/// 늘 LOD 0 입니다 — 나누기 전에는 LOD 도 없었으므로, 거친 판본을 기준에 더하면
/// 없던 것과 비교하게 됩니다. 어느 쪽이 얼마나 기여했는지는 줄 끝의
/// <c>Citadel=LOD1</c> 표시로 갈라 볼 수 있습니다.
///
/// 시점은 <b>실제로 서게 될 자리</b>에서 잡습니다. 하늘에서 내려다보면 어차피 다
/// 보이므로 아무것도 증명하지 못합니다.
///
/// <code>
/// Unity.exe -batchmode -nographics -projectPath . -executeMethod MegastructureCullCheck.Run
/// </code>
/// </summary>
public static class MegastructureCullCheck
{
    private const string ScenePath = "Assets/_Project/01.Scenes/SampleScene.unity";
    /// <summary>
    /// 씬에 앉은 스파인의 이름입니다. 프리팹 이름(MegaSpine)이 아니라
    /// <see cref="MegastructurePlacer"/> 가 붙이는 이름입니다.
    /// </summary>
    private const string SpineName = "Megastructure";

    /// <summary>배치기가 다리 밑동에 뿌린 파편을 담는 자식의 이름입니다.</summary>
    private const string RubbleName = "Rubble";

    /// <summary>씬 카메라의 화각과 원거리 클립입니다. 실제 값을 못 찾으면 이것을 씁니다.</summary>
    private const float FallbackFov = 60f;
    private const float FallbackFar = 1200f;

    private struct Eye
    {
        public string Name;
        public Vector3 At;      // 스파인 로컬: x 는 스파인을 따라, y 는 높이, z 는 옆으로
        public Vector3 Look;
    }

    public static void Run()
    {
        int errors = 0;

        try
        {
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            Transform spine = Find(scene, SpineName);
            if (spine == null) throw new Exception("씬에 " + SpineName + " 이 없습니다");

            // <b>거울 카메라를 집으면 안 됩니다.</b> 이 씬에는 카메라가 다섯 대이고
            // 그 중 셋이 차의 거울입니다. 거울은 화각도 원거리도 본 카메라와 달라,
            // 여기서 잘못 집으면 <b>있지도 않은 시야로 컬링을 재게</b> 됩니다.
            Camera camera = Camera.main ?? UnityEngine.Object
                .FindObjectsByType<Camera>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                .FirstOrDefault(c => c.cameraType == CameraType.Game && c.targetTexture == null);

            float fov = camera != null ? camera.fieldOfView : FallbackFov;
            float far = camera != null ? camera.farClipPlane : FallbackFar;
            float aspect = 16f / 9f;

            List<Group> groups = Collect(spine);
            int parts = groups.Sum(g => g.Parts.Count);
            int tris = groups.Sum(g => g.Tris);

            // <b>LOD 가 씬까지 닿았는지</b>는 프리팹 파일을 grep 해서는 알 수 없습니다.
            // 중첩 프리팹이라 부모 파일에는 아무것도 안 적히기 때문입니다.
            LODGroup[] lods = spine.GetComponentsInChildren<LODGroup>(true);

            Debug.Log($"MegastructureCullCheck: LODGroup {lods.Length} 개 · " + string.Join(", ",
                lods.Select(l => $"{l.transform.name} size {l.size:F0} · " +
                                 $"단계 {l.lodCount}")));

            Debug.Log($"MegastructureCullCheck: 프리셋 {groups.Count} 개 · 파츠 {parts} 개 · " +
                      $"삼각형 {tris:N0} · 카메라 {(camera != null ? camera.name : "(없음)")} · " +
                      $"화각 {fov:F0}° · 원거리 {far:F0} m");

            foreach (Eye eye in Vantages(spine, groups))
            {
                Measure(eye, fov, aspect, far, groups);
            }
        }
        catch (Exception e)
        {
            Debug.LogError("MegastructureCullCheck: " + e);
            errors++;
        }

        if (Application.isBatchMode) EditorApplication.Exit(errors > 0 ? 2 : 0);
    }

    // --- Private Methods ---

    /// <summary>프리셋 인스턴스 하나와 그 파츠들입니다.</summary>
    private sealed class Group
    {
        public string Name;
        public Bounds Merged;                       // 합쳐 두었다면 이랬을 상자
        /// <summary>
        /// <c>level</c> 은 <b>이 파츠가 속한 LOD 단계</b>입니다. LODGroup 이 없으면
        /// 전부 0 입니다.
        ///
        /// 이것이 없을 때 LOD 0 과 LOD 1 을 <b>함께 셌습니다.</b> 둘은 절대 같이
        /// 그려지지 않는데 삼각형에 둘 다 더해, 시타델에 LOD 를 단 뒤로 이 도구의
        /// 숫자가 부풀려지고 <b>이전 실행과 비교할 수 없게</b> 되었습니다.
        /// </summary>
        public List<(Bounds box, int tris, int draws, int level)> Parts =
            new List<(Bounds, int, int, int)>();

        /// <summary>LOD 0 의 삼각형입니다. <b>합쳤다면</b>의 기준입니다.</summary>
        public int Tris;

        /// <summary>단계마다 갈아타는 화면 높이 비율. LODGroup 이 없으면 비어 있습니다.</summary>
        public float[] Steps = new float[0];

        /// <summary>LODGroup 이 판단 기준으로 쓰는 크기(m)입니다.</summary>
        public float LodSize;

        public Vector3 At;

        /// <summary>
        /// 프리셋 전체가 <b>쓰는</b> 머티리얼 자리들입니다.
        ///
        /// 합쳤을 때의 드로우 수는 파츠들의 서브메시를 <b>더한 것이 아니라 합집합</b>
        /// 입니다. 더하면 합친 쪽이 실제보다 비싸 보여, 나눈 쪽이 공짜로 이깁니다.
        /// </summary>
        public int Mask;
    }

    /// <summary>
    /// 스파인의 <b>직계 자식</b>이 프리셋 인스턴스 하나입니다.
    ///
    /// 렌더러를 통째로 훑어 모으면 안 됩니다 — 그러면 어느 파츠가 어느 프리셋에
    /// 속하는지 잃어버려, "합쳤다면" 을 계산할 수 없습니다.
    /// </summary>
    private static List<Group> Collect(Transform spine)
    {
        List<Group> made = new List<Group>();

        foreach (Transform child in spine)
        {
            // <b>파편은 프리셋이 아닙니다.</b> 배치기가 다리 밑동에 뿌린 바위인데,
            // 스파인의 직계 자식이라 프리셋 하나로 세어져 파츠 수를 39 개 부풀렸습니다.
            if (child.name == RubbleName) continue;

            Renderer[] renderers = child.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) continue;

            Group group = new Group { Name = child.name, At = child.position };

            LODGroup lod = child.GetComponent<LODGroup>();
            Dictionary<Renderer, int> level = new Dictionary<Renderer, int>();

            if (lod != null)
            {
                LOD[] steps = lod.GetLODs();
                group.Steps = steps.Select(l => l.screenRelativeTransitionHeight).ToArray();
                group.LodSize = lod.size;

                for (int i = 0; i < steps.Length; i++)
                {
                    foreach (Renderer r in steps[i].renderers)
                    {
                        if (r != null) level[r] = i;
                    }
                }
            }

            foreach (Renderer r in renderers)
            {
                MeshFilter filter = r.GetComponent<MeshFilter>();
                if (filter == null || filter.sharedMesh == null) continue;

                int count = filter.sharedMesh.triangles.Length / 3;
                int step = level.TryGetValue(r, out int found) ? found : 0;

                // <b>드로우의 단위는 렌더러가 아니라 서브메시</b>입니다. 합친 메시는
                // 늘 머티리얼 셋을 다 쓰므로 늘 3 번 그리지만, 나눈 파츠는 저마다
                // 한두 개만 씁니다 — 렌더러가 늘어도 드로우는 안 늘 수 있습니다.
                int mask = Used(filter.sharedMesh);

                group.Parts.Add((r.bounds, count, Bits(mask), step));

                // <b>합쳤다면</b>의 기준은 LOD 0 입니다. 나누기 전에는 LOD 도
                // 없었으므로, 거친 판본을 여기 더하면 없던 것과 비교하게 됩니다.
                if (step == 0)
                {
                    group.Tris += count;
                    group.Mask |= mask;

                    group.Merged = group.Merged.size == Vector3.zero
                        ? r.bounds
                        : Encapsulated(group.Merged, r.bounds);
                }
            }

            if (group.Parts.Count > 0) made.Add(group);
        }

        return made;
    }

    /// <summary>실제로 삼각형이 들어 있는 서브메시 <b>자리</b>들입니다. 빈 것은 안 그립니다.</summary>
    private static int Used(Mesh mesh)
    {
        int mask = 0;

        for (int i = 0; i < mesh.subMeshCount && i < 31; i++)
        {
            if (mesh.GetIndexCount(i) > 0) mask |= 1 << i;
        }

        return mask == 0 ? 1 : mask;
    }

    private static int Bits(int mask)
    {
        int n = 0;
        while (mask != 0)
        {
            n += mask & 1;
            mask >>= 1;
        }

        return n;
    }

    private static Bounds Encapsulated(Bounds a, Bounds b)
    {
        a.Encapsulate(b);
        return a;
    }

    /// <summary>
    /// <b>실제로 서게 될 자리</b>들입니다.
    ///
    /// 데크 위(운전), 데크 밑(지나가기), 멀리 지면(풍경)이 전부 다른 답을 냅니다.
    /// 하나만 재면 유리한 자리를 고른 것이 됩니다.
    /// </summary>
    private static IEnumerable<Eye> Vantages(Transform spine, List<Group> groups)
    {
        Bounds all = groups[0].Merged;
        foreach (Group g in groups) all.Encapsulate(g.Merged);

        Vector3 along = spine.forward;
        Vector3 side = spine.right;
        Vector3 mid = new Vector3(all.center.x, 0f, all.center.z);

        // 데크 노면의 높이. 검사기가 재 둔 값과 같은 자리입니다.
        Group tall = groups.OrderByDescending(g => g.Merged.max.y).First();
        float road = tall.Merged.min.y + 35.2f;

        yield return new Eye
        {
            Name = "데크 위 · 한가운데",
            At = mid + Vector3.up * (road + 1.6f),
            Look = along,
        };

        yield return new Eye
        {
            Name = "데크 위 · 초거대 옆",
            At = new Vector3(tall.Merged.center.x, road + 1.6f, tall.Merged.center.z)
                 - along * 60f,
            Look = along,
        };

        yield return new Eye
        {
            Name = "데크 밑 · 지면",
            At = mid + Vector3.up * (all.min.y + 22f) - along * (all.size.magnitude * 0.2f),
            Look = along,
        };

        yield return new Eye
        {
            Name = "멀리서 · 옆",
            At = mid + Vector3.up * (all.min.y + 30f) + side * 320f,
            Look = (-side + along * 0.4f).normalized,
        };
    }

    private static void Measure(Eye eye, float fov, float aspect, float far,
                                List<Group> groups)
    {
        Matrix4x4 view = Matrix4x4.TRS(eye.At, Quaternion.LookRotation(eye.Look, Vector3.up),
                                       new Vector3(1f, 1f, -1f)).inverse;
        Matrix4x4 projection = Matrix4x4.Perspective(fov, aspect, 0.3f, far);
        Plane[] planes = GeometryUtility.CalculateFrustumPlanes(projection * view);

        int wholeTris = 0, wholeDraws = 0;
        int splitTris = 0, splitDraws = 0;

        List<(string name, int tris)> heavy = new List<(string, int)>();

        foreach (Group group in groups)
        {
            if (GeometryUtility.TestPlanesAABB(planes, group.Merged))
            {
                wholeTris += group.Tris;

                // 합친 메시는 프리셋이 쓰는 머티리얼을 <b>한 벌씩</b> 갖습니다.
                wholeDraws += Bits(group.Mask);
            }

            // <b>지금 어느 단계가 살아 있는가.</b> 유니티와 같은 식입니다 -
            // 화면 높이 비율 = size / (거리 x 2 tan(화각/2)).
            int active = Active(group, eye, fov);

            int mine = 0;

            foreach ((Bounds box, int tris, int draws, int level) in group.Parts)
            {
                if (level != active) continue;
                if (!GeometryUtility.TestPlanesAABB(planes, box)) continue;

                splitTris += tris;
                splitDraws += draws;
                mine += tris;
            }

            if (mine > 0) heavy.Add((group.Name, mine));
        }

        float saved = wholeTris > 0 ? 100f * (1f - (float)splitTris / wholeTris) : 0f;

        string worst = string.Join(" ", heavy.OrderByDescending(h => h.tris).Take(3)
            .Select(h => $"{h.name.Replace("SM_Mega_", "")} {h.tris:N0}"));

        string lods = string.Join(" ", groups
            .Where(g => g.Steps.Length > 0)
            .Select(g => $"{g.Name.Replace("SM_Mega_", "")}=LOD{Active(g, eye, fov)}"));

        Debug.Log($"  {eye.Name,-16} : 합쳤다면 {wholeTris,7:N0} 삼각형 / 드로우 {wholeDraws,3} · " +
                  $"지금 {splitTris,7:N0} / {splitDraws,3} · {saved,5:F1}% 덜 그림 · " +
                  $"무거운 것 {worst}{(lods.Length > 0 ? " · " + lods : "")}");
    }

    /// <summary>
    /// 이 시점에서 <b>살아 있는 LOD 단계</b>입니다. LODGroup 이 없으면 늘 0 입니다.
    ///
    /// 유니티와 같은 식을 씁니다: 화면 높이 비율 = <c>size / (거리 x 2 tan(화각/2))</c>.
    /// 그 비율보다 임계값이 작은 <b>첫 단계</b>가 그려집니다.
    /// </summary>
    private static int Active(Group group, Eye eye, float fov)
    {
        if (group.Steps.Length == 0) return 0;

        float distance = Vector3.Distance(eye.At, group.At);
        if (distance < 0.01f) return 0;

        float height = group.LodSize
                       / (distance * 2f * Mathf.Tan(fov * 0.5f * Mathf.Deg2Rad));

        for (int i = 0; i < group.Steps.Length; i++)
        {
            if (height >= group.Steps[i]) return i;
        }

        // 마지막 임계값보다도 작으면 아무것도 안 그립니다. 여기서는 <b>없는 단계</b>를
        // 돌려 아무 파츠도 세어지지 않게 합니다.
        return group.Steps.Length;
    }

    private static Transform Find(Scene scene, string name)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (root.name == name) return root.transform;

            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == name) return t;
            }
        }

        return null;
    }
}
