using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace CarDrive.EditorTools
{
    /// <summary>
    /// 차체처럼 <b>볼록 MeshCollider 로 쓰기엔 너무 촘촘한 메시</b>를 위해
    /// 저폴리 충돌 프록시를 굽습니다.
    ///
    /// ── 무엇을 고치는가 ──
    ///
    /// 플레이어 차체는 <c>CityCar03</c> 렌더 메시를 그대로 볼록 콜라이더로 쓰고 있었습니다.
    /// 정점이 15,623개인데 PhysX 의 볼록 껍질 한계는 <b>정점 255 · 면 256</b>입니다.
    /// 그래서 씬을 열 때마다 이것이 나왔습니다.
    ///
    ///   Couldn't create a Convex Mesh from source mesh, within the maximum
    ///   polygons limit (256). The partial hull will be used.
    ///
    /// 잘린 껍질도 볼록 형상이라 충돌은 되지만, 두 가지가 걸립니다.
    ///  · 어떻게 잘릴지 우리가 정하지 못합니다. PhysX 가 알아서 버립니다.
    ///  · 로드할 때마다 정점 15,623개를 쿠킹합니다.
    ///
    /// 저폴리 LOD 로 바꿔 봐야 소용없습니다. 실측으로 LOD1 이 5,116 정점,
    /// LOD2 가 2,646 정점이라 <b>가장 낮은 단계도 한계의 10배</b>입니다.
    /// 게다가 LOD2 는 높이가 7cm 낮아 형상까지 달라집니다.
    ///
    /// ── 어떻게 굽는가 ──
    ///
    /// 차 길이(Z) 방향으로 몇 장의 <b>단면</b>을 뜨고, 단면마다 바깥 점을 몇 개씩만
    /// 골라 이어 붙입니다. 고르는 방법은 <b>서포트 함수</b>입니다. 어떤 방향을 주면
    /// 그 방향으로 가장 멀리 있는 점을 돌려주는 것이라, <b>고른 점은 반드시 볼록 껍질 위</b>에 있습니다.
    /// 원래 껍질에서 벗어난 점이 섞일 수 없다는 뜻입니다.
    ///
    /// 단면을 Z로 뜨는 이유는 차의 실루엣이 그 축을 따라 변하기 때문입니다.
    /// 보닛은 낮고 지붕은 높은 것이 단면마다 그대로 남아, 상자로 감쌌을 때처럼
    /// 앞유리 위가 부풀지 않습니다.
    ///
    /// 단면 <see cref="DefaultSlices"/>장 x 둘레 <see cref="DefaultRing"/>점에 앞뒤 끝점 둘,
    /// 정점 <b>74개</b>입니다. 한계 255의 3분의 1이라 PhysX 가 자를 일이 없습니다.
    ///
    /// 실측으로 프록시의 경계 상자가 원본과 소수점 셋째 자리까지 같습니다.
    /// 중심 (0.000, 0.782, -0.127) · 크기 (2.051, 1.249, 5.818).
    /// </summary>
    public static class VehicleColliderBaker
    {
        // --- Constants ---

        /// <summary>구운 프록시 메시가 놓이는 폴더입니다.</summary>
        private const string GeneratedFolder = "Assets/_Project/04.Art/02.Models/Generated";

        /// <summary>차량 프리팹을 찾을 폴더입니다.</summary>
        private const string PrefabFolder = "Assets/_Project/05.Prefabs";

        /// <summary>
        /// PhysX 가 볼록 껍질 하나에 허용하는 정점 수입니다.
        /// 이것을 넘으면 유니티가 "partial hull will be used" 경고를 내고 알아서 잘라 냅니다.
        /// </summary>
        private const int PhysXHullVertexLimit = 255;

        /// <summary>차 길이 방향으로 뜨는 단면의 수입니다.</summary>
        private const int DefaultSlices = 9;

        /// <summary>단면 하나에서 고르는 둘레 점의 수입니다.</summary>
        private const int DefaultRing = 8;

        // --- Public Methods ---

        /// <summary>
        /// 프리팹을 훑어 <b>한계를 넘는 볼록 MeshCollider</b>를 찾아 프록시로 갈아 끼웁니다.
        ///
        /// 한계 안에 드는 콜라이더는 건드리지 않습니다. 문제가 없는 것을 굳이
        /// 근사로 바꾸면 충돌만 나빠집니다.
        /// </summary>
        [MenuItem("CarDrive/Vehicle/차체 충돌 프록시 굽기")]
        public static void BakeAll()
        {
            List<string> report = new List<string>();

            string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { PrefabFolder });
            int baked = 0;

            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (BakePrefab(path, report)) baked++;
            }

            AssetDatabase.SaveAssets();

            if (baked == 0) report.Add("· 한계를 넘는 볼록 콜라이더가 없습니다. 바꿀 것이 없습니다.");

            Debug.Log("=== 차체 충돌 프록시 ===" + System.Environment.NewLine +
                      string.Join(System.Environment.NewLine, report));
        }

        /// <summary>
        /// 원본 메시에서 저폴리 볼록 프록시를 만듭니다.
        ///
        /// 고르는 점은 <b>전부 원본의 정점을 그대로</b> 쓴 것입니다. 자리를 옮기지 않습니다.
        /// 어떤 점들의 부분집합을 골라도 그 볼록 껍질은 전체의 볼록 껍질 안에 들어가므로,
        /// <b>프록시가 원본보다 커지는 일은 없습니다.</b> 차가 벽을 뚫는 쪽이 아니라
        /// 살짝 파고드는 쪽입니다. 이 방향의 오차라야 안전합니다.
        /// </summary>
        /// <param name="source">원본 렌더 메시</param>
        /// <param name="slices">길이 방향 단면 수</param>
        /// <param name="ring">단면당 둘레 점 수</param>
        /// <returns>만들어진 프록시 메시</returns>
        public static Mesh BuildProxy(Mesh source, int slices, int ring)
        {
            if (source == null) return null;

            slices = Mathf.Max(2, slices);
            ring = Mathf.Max(3, ring);

            Vector3[] points = source.vertices;
            if (points.Length == 0) return null;

            Bounds bounds = source.bounds;

            // 가장 긴 축을 따라 단면을 뜹니다. 차는 보통 Z가 길지만 그렇게 정해 두지 않습니다.
            int axis = LongestAxis(bounds.size);
            int right = (axis + 1) % 3;
            int up = (axis + 2) % 3;

            float min = bounds.min[axis];
            float max = bounds.max[axis];

            List<Vector3> verts = new List<Vector3>(slices * ring);

            for (int s = 0; s < slices; s++)
            {
                float t = s / (float)(slices - 1);
                float center = Mathf.Lerp(min, max, t);

                // 단면에 아무 점도 안 걸리면 두께를 넓혀 다시 봅니다.
                // 앞뒤 끝처럼 뾰족한 자리에서 빈 단면이 나옵니다.
                float half = (max - min) / (slices - 1) * 0.5f;
                List<Vector3> slab = Gather(points, axis, center, half);
                if (slab.Count == 0) slab = Gather(points, axis, center, half * 4f);
                if (slab.Count == 0) continue;

                AddRing(verts, slab, axis, right, up, center, ring);
            }

            if (verts.Count < 4) return null;

            // 앞뒤 <b>끝점</b>을 따로 넣습니다.
            //
            // 둘레 방향으로만 점을 고르면 축을 따라서는 한 번도 고르지 않게 되어,
            // 코와 꼬리의 뾰족한 끝이 빠집니다. 실측으로 차가 26cm 짧아졌습니다.
            // 범퍼가 앞뒤로 13cm씩 파고드는 셈이라 그냥 둘 수 없습니다.
            verts.Add(Extreme(points, axis, false));
            verts.Add(Extreme(points, axis, true));

            return Stitch(verts, ring, source.name);
        }

        // --- Private Methods ---

        /// <summary>
        /// 프리팹 하나를 살펴 필요하면 프록시로 갈아 끼웁니다.
        /// </summary>
        /// <param name="path">프리팹 경로</param>
        /// <param name="report">진행 내용을 적을 목록</param>
        /// <returns>하나라도 갈아 끼웠으면 true</returns>
        private static bool BakePrefab(string path, List<string> report)
        {
            GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (asset == null) return false;

            // 먼저 훑어보고 고칠 것이 있을 때만 엽니다. 여는 것 자체가 무겁습니다.
            if (!NeedsBake(asset)) return false;

            GameObject root = PrefabUtility.LoadPrefabContents(path);
            bool changed = false;

            try
            {
                MeshCollider[] colliders = root.GetComponentsInChildren<MeshCollider>(true);

                for (int i = 0; i < colliders.Length; i++)
                {
                    MeshCollider collider = colliders[i];
                    if (!IsOverLimit(collider)) continue;

                    Mesh source = collider.sharedMesh;
                    Mesh proxy = BuildProxy(source, DefaultSlices, DefaultRing);
                    if (proxy == null)
                    {
                        report.Add("! " + asset.name + " — " + source.name + " 에서 프록시를 만들지 못했습니다.");
                        continue;
                    }

                    Mesh saved = Save(proxy, source.name);
                    if (saved == null) continue;

                    collider.sharedMesh = saved;
                    changed = true;

                    report.Add("· " + asset.name + " / " + collider.name +
                               " — " + source.name + " 정점 " + source.vertexCount.ToString("N0") +
                               " → " + saved.name + " 정점 " + saved.vertexCount +
                               "  (한계 " + PhysXHullVertexLimit + ")");
                }

                if (changed) PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }

            return changed;
        }

        /// <summary>이 프리팹에 한계를 넘는 볼록 콜라이더가 있는지 봅니다.</summary>
        /// <param name="asset">프리팹 에셋</param>
        /// <returns>있으면 true</returns>
        private static bool NeedsBake(GameObject asset)
        {
            MeshCollider[] colliders = asset.GetComponentsInChildren<MeshCollider>(true);
            for (int i = 0; i < colliders.Length; i++)
            {
                if (IsOverLimit(colliders[i])) return true;
            }
            return false;
        }

        /// <summary>이 콜라이더가 PhysX 한계를 넘는지 봅니다.</summary>
        /// <param name="collider">살펴볼 콜라이더</param>
        /// <returns>볼록이고 정점이 한계를 넘으면 true</returns>
        private static bool IsOverLimit(MeshCollider collider)
        {
            if (collider == null || !collider.convex) return false;
            if (collider.sharedMesh == null) return false;

            // 이미 구워 둔 프록시는 다시 굽지 않습니다.
            if (collider.sharedMesh.vertexCount <= PhysXHullVertexLimit) return false;

            return true;
        }

        /// <summary>덩치가 가장 큰 축을 고릅니다.</summary>
        /// <param name="size">경계 상자의 크기</param>
        /// <returns>0=X, 1=Y, 2=Z</returns>
        private static int LongestAxis(Vector3 size)
        {
            if (size.x >= size.y && size.x >= size.z) return 0;
            if (size.y >= size.z) return 1;
            return 2;
        }

        /// <summary>단면에 걸치는 점을 모읍니다.</summary>
        /// <param name="points">원본 정점</param>
        /// <param name="axis">길이 축</param>
        /// <param name="center">단면의 위치</param>
        /// <param name="half">단면의 두께 절반</param>
        /// <returns>걸친 점들</returns>
        private static List<Vector3> Gather(Vector3[] points, int axis, float center, float half)
        {
            List<Vector3> slab = new List<Vector3>();
            for (int i = 0; i < points.Length; i++)
            {
                if (Mathf.Abs(points[i][axis] - center) <= half) slab.Add(points[i]);
            }
            return slab;
        }

        /// <summary>
        /// 단면의 둘레 점을 <b>서포트 함수</b>로 고릅니다.
        ///
        /// 방향을 하나 주면 그 방향으로 가장 멀리 있는 점을 고릅니다. 이렇게 고른 점은
        /// 반드시 볼록 껍질 위에 있습니다. 방향을 둘레로 고르게 돌려 가며 뽑으면
        /// 껍질을 고르게 훑게 됩니다.
        /// </summary>
        /// <param name="verts">점을 넣을 목록</param>
        /// <param name="slab">이 단면에 걸친 점들</param>
        /// <param name="axis">길이 축</param>
        /// <param name="right">단면의 가로 축</param>
        /// <param name="up">단면의 세로 축</param>
        /// <param name="center">쓰이지 않습니다. 단면을 나눌 때만 쓴 값입니다.</param>
        /// <param name="ring">고를 점의 수</param>
        private static void AddRing(List<Vector3> verts, List<Vector3> slab,
                                    int axis, int right, int up, float center, int ring)
        {
            for (int r = 0; r < ring; r++)
            {
                float angle = r / (float)ring * Mathf.PI * 2f;
                float dr = Mathf.Cos(angle);
                float du = Mathf.Sin(angle);

                float best = float.NegativeInfinity;
                Vector3 pick = slab[0];

                for (int i = 0; i < slab.Count; i++)
                {
                    float dot = slab[i][right] * dr + slab[i][up] * du;
                    if (dot <= best) continue;

                    best = dot;
                    pick = slab[i];
                }

                // <b>고른 자리를 그대로 씁니다. 단면 평면으로 눕히지 않습니다.</b>
                //
                // 눕히면 고리가 평평해져 메시로 보기에는 깔끔하지만, 점이 축을 따라 움직이면서
                // <b>원본 껍질 밖으로 밀려날 수 있습니다.</b> 차가 앞으로 갈수록 좁아지는데
                // 뒤쪽의 넓은 점을 앞쪽 단면 자리로 옮기면 원본에 없던 자리로 튀어나갑니다.
                // 단면 두께의 절반, 이 차에서는 36cm까지 부풀 수 있었습니다.
                //
                // 눕히지 않으면 고리가 평평하지 않아 메시로는 조금 뒤틀려 보입니다.
                // 그래도 상관없습니다. <b>PhysX 는 볼록 콜라이더에서 점만 보고 껍질을 다시 뜹니다.</b>
                // 삼각형은 쓰이지 않습니다. 그리고 이렇게 고른 점은 전부 원본의 정점이므로
                // 그 부분집합의 껍질은 반드시 원본 껍질 안에 들어갑니다. 커질 수가 없습니다.
                verts.Add(pick);
            }
        }

        /// <summary>축을 따라 가장 끝에 있는 점을 고릅니다.</summary>
        /// <param name="points">원본 정점</param>
        /// <param name="axis">길이 축</param>
        /// <param name="positive">true면 축의 플러스 쪽 끝</param>
        /// <returns>가장 끝에 있는 원본 정점</returns>
        private static Vector3 Extreme(Vector3[] points, int axis, bool positive)
        {
            Vector3 pick = points[0];
            float best = positive ? float.NegativeInfinity : float.PositiveInfinity;

            for (int i = 0; i < points.Length; i++)
            {
                float v = points[i][axis];
                if (positive ? v <= best : v >= best) continue;

                best = v;
                pick = points[i];
            }

            return pick;
        }

        /// <summary>
        /// 고리들을 옆면으로 잇고 앞뒤 끝점으로 뚜껑을 덮어 닫힌 메시로 만듭니다.
        ///
        /// <b>삼각형은 충돌에 쓰이지 않습니다.</b> PhysX 는 볼록 콜라이더에서 점만 보고
        /// 껍질을 다시 뜹니다. 그래도 제대로 닫아 두는 것은 씬 뷰에서 눈으로 확인하기
        /// 위해서입니다. 형상이 이상해졌을 때 알아볼 수 있어야 합니다.
        /// </summary>
        /// <param name="verts">고리들 뒤에 앞뒤 끝점 둘이 붙은 목록</param>
        /// <param name="ring">단면당 점 수</param>
        /// <param name="sourceName">원본 메시 이름</param>
        /// <returns>만들어진 메시</returns>
        private static Mesh Stitch(List<Vector3> verts, int ring, string sourceName)
        {
            int rings = (verts.Count - 2) / ring;
            int tailTip = verts.Count - 2;
            int noseTip = verts.Count - 1;

            List<int> tris = new List<int>();

            // 옆면 — 이웃한 두 고리를 사각형으로 잇습니다.
            for (int s = 0; s < rings - 1; s++)
            {
                int a = s * ring;
                int b = (s + 1) * ring;

                for (int r = 0; r < ring; r++)
                {
                    int r2 = (r + 1) % ring;

                    tris.Add(a + r); tris.Add(b + r); tris.Add(a + r2);
                    tris.Add(a + r2); tris.Add(b + r); tris.Add(b + r2);
                }
            }

            // 뚜껑 — 끝점에서 첫 고리와 마지막 고리로 부챗살을 폅니다.
            int last = (rings - 1) * ring;
            for (int r = 0; r < ring; r++)
            {
                int r2 = (r + 1) % ring;

                tris.Add(tailTip); tris.Add(r2); tris.Add(r);
                tris.Add(noseTip); tris.Add(last + r); tris.Add(last + r2);
            }

            Mesh mesh = new Mesh();
            mesh.name = sourceName + "_Hull";
            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            return mesh;
        }

        /// <summary>프록시 메시를 에셋으로 저장합니다. 이미 있으면 내용만 갈아 끼웁니다.</summary>
        /// <param name="proxy">저장할 메시</param>
        /// <param name="sourceName">원본 메시 이름</param>
        /// <returns>저장된 메시 에셋</returns>
        private static Mesh Save(Mesh proxy, string sourceName)
        {
            if (!AssetDatabase.IsValidFolder(GeneratedFolder)) return null;

            string path = GeneratedFolder + "/" + sourceName + "_Hull.asset";

            Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing == null)
            {
                AssetDatabase.CreateAsset(proxy, path);
                return proxy;
            }

            existing.Clear();
            existing.name = proxy.name;
            existing.SetVertices(proxy.vertices);
            existing.SetTriangles(proxy.triangles, 0);
            existing.RecalculateNormals();
            existing.RecalculateBounds();

            EditorUtility.SetDirty(existing);
            return existing;
        }
    }
}
