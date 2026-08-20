using System.Collections.Generic;
using UnityEngine;

namespace CarDrive.EditorTools.ProcGen
{
    /// <summary>
    /// 저폴리 바위와 건물 메시를 만듭니다.
    ///
    /// <b>왜 모델을 사 오지 않고 만드는가.</b> 이 게임의 화면은 세로 215픽셀로 줄어들고
    /// 팔레트로 색이 뭉개집니다. 정교한 모델을 넣어도 그 정보가 화면에 남지 않습니다.
    /// 그보다 <b>실루엣과 면의 각도</b>가 전부인데, 그건 절차적으로 만드는 편이
    /// 크기·비율을 마음대로 맞출 수 있어 오히려 낫습니다.
    ///
    /// 모든 메시는 <b>면마다 정점을 따로 둡니다</b>(플랫 셰이딩). 정점을 공유하면
    /// 법선이 평균되어 면이 둥글게 이어지는데, 저폴리 룩은 그 각진 경계에서 나옵니다.
    /// </summary>
    public static class LowPolyMeshFactory
    {
        // --- Public Methods : 바위 ---

        /// <summary>
        /// 저폴리 바위를 만듭니다.
        ///
        /// 정이십면체를 잘게 나눈 뒤 각 정점을 노이즈로 밀고 당깁니다.
        /// 구를 그대로 쓰면 자갈처럼 보이므로 축마다 다르게 눌러 <b>납작하거나 길쭉하게</b> 만듭니다.
        /// </summary>
        /// <param name="seed">난수 씨앗. 같은 값이면 같은 바위가 나옵니다.</param>
        /// <param name="subdivisions">면 나누기 횟수. 0이면 20면, 1이면 80면입니다.</param>
        /// <param name="roughness">울퉁불퉁한 정도 (0~1)</param>
        /// <returns>플랫 셰이딩된 바위 메시</returns>
        public static Mesh CreateRock(int seed, int subdivisions, float roughness)
        {
            List<Vector3> verts = new List<Vector3>();
            List<int> tris = new List<int>();

            BuildIcosahedron(verts, tris);

            for (int i = 0; i < subdivisions; i++) Subdivide(verts, tris);

            System.Random random = new System.Random(seed);

            // 축마다 다르게 눌러 바위의 <b>기본 비율</b>을 정합니다.
            // 이게 없으면 어떤 바위든 구에서 출발한 티가 납니다.
            Vector3 squash = new Vector3(
                Mathf.Lerp(0.7f, 1.3f, (float)random.NextDouble()),
                Mathf.Lerp(0.45f, 0.95f, (float)random.NextDouble()),
                Mathf.Lerp(0.7f, 1.3f, (float)random.NextDouble()));

            // 노이즈를 읽을 자리. 씨앗마다 달라야 바위 모양이 겹치지 않습니다.
            Vector3 offset = new Vector3(
                (float)random.NextDouble() * 100f,
                (float)random.NextDouble() * 100f,
                (float)random.NextDouble() * 100f);

            for (int i = 0; i < verts.Count; i++)
            {
                Vector3 dir = verts[i].normalized;

                // 두 층으로 밉니다. 굵은 층이 덩어리를, 잔 층이 모서리를 만듭니다.
                float coarse = Noise3(dir * 1.6f + offset);
                float fine = Noise3(dir * 4.3f + offset) * 0.45f;

                float displace = 1f + (coarse + fine - 0.7f) * roughness;

                verts[i] = Vector3.Scale(dir * displace, squash);
            }

            // 바닥을 눌러 땅에 앉힙니다. 둥근 바위는 지면에 심으면 반쯤 묻혀 보입니다.
            float bottom = float.MaxValue;
            for (int i = 0; i < verts.Count; i++) bottom = Mathf.Min(bottom, verts[i].y);

            for (int i = 0; i < verts.Count; i++)
            {
                Vector3 v = verts[i];
                if (v.y < bottom * 0.55f) v.y = bottom * 0.55f;
                verts[i] = v;
            }

            return BuildFlatMesh("LowPolyRock", verts, tris);
        }

        // --- Public Methods : 건물 ---

        /// <summary>
        /// 저폴리 건물을 만듭니다. 상자 몸통에 박공지붕을 얹은 단순한 형태입니다.
        ///
        /// 마을 건물은 <b>도로에서 보이는 실루엣</b>이 전부입니다. 차를 타고 지나가며
        /// 보는 것이라 창문 하나하나가 눈에 들어오지 않습니다. 그래서 지붕 각도와
        /// 높이 비율만 흔들어 다양성을 냅니다.
        /// </summary>
        /// <param name="width">가로 폭(m). 도로와 나란한 방향입니다.</param>
        /// <param name="depth">안쪽 깊이(m)</param>
        /// <param name="wallHeight">벽 높이(m)</param>
        /// <param name="roofHeight">지붕 마루 높이(m). 0이면 평지붕입니다.</param>
        /// <returns>플랫 셰이딩된 건물 메시</returns>
        public static Mesh CreateBuilding(float width, float depth, float wallHeight, float roofHeight)
        {
            List<Vector3> verts = new List<Vector3>();
            List<int> tris = new List<int>();

            float hw = width * 0.5f;
            float hd = depth * 0.5f;

            // --- 벽 (상자) ---
            Vector3 a = new Vector3(-hw, 0f, -hd);
            Vector3 b = new Vector3(hw, 0f, -hd);
            Vector3 c = new Vector3(hw, 0f, hd);
            Vector3 d = new Vector3(-hw, 0f, hd);

            Vector3 a2 = a + Vector3.up * wallHeight;
            Vector3 b2 = b + Vector3.up * wallHeight;
            Vector3 c2 = c + Vector3.up * wallHeight;
            Vector3 d2 = d + Vector3.up * wallHeight;

            AddQuad(verts, tris, a2, b2, b, a);   // 앞
            AddQuad(verts, tris, b2, c2, c, b);   // 오른쪽
            AddQuad(verts, tris, c2, d2, d, c);   // 뒤
            AddQuad(verts, tris, d2, a2, a, d);   // 왼쪽

            if (roofHeight <= 0.01f)
            {
                // 평지붕
                AddQuad(verts, tris, d2, c2, b2, a2);
                return BuildFlatMesh("LowPolyBuilding", verts, tris);
            }

            // --- 박공지붕 ---
            // 마루는 깊이 방향(안쪽)으로 놓습니다. 도로에서 보면 삼각형 박공이 보입니다.
            Vector3 ridgeFront = new Vector3(0f, wallHeight + roofHeight, -hd);
            Vector3 ridgeBack = new Vector3(0f, wallHeight + roofHeight, hd);

            AddTriangle(verts, tris, a2, ridgeFront, b2);   // 앞 박공
            AddTriangle(verts, tris, c2, ridgeBack, d2);    // 뒤 박공

            AddQuad(verts, tris, ridgeFront, ridgeBack, c2, b2);  // 오른쪽 지붕면
            AddQuad(verts, tris, ridgeBack, ridgeFront, a2, d2);  // 왼쪽 지붕면

            return BuildFlatMesh("LowPolyBuilding", verts, tris);
        }

        /// <summary>
        /// 박공지붕만 따로 만듭니다. 벽과 재질이 달라 오브젝트를 나눠야 할 때 씁니다.
        ///
        /// <see cref="CreateBuilding"/> 에 벽 높이를 0으로 넘겨도 지붕이 나오긴 하지만,
        /// 그때는 <b>넓이가 0인 벽 네 장</b>이 함께 따라옵니다. 화면에는 안 보여도
        /// 법선을 구할 수 없는 면이라 남겨 둘 이유가 없습니다.
        /// </summary>
        /// <param name="width">가로 폭(m)</param>
        /// <param name="depth">안쪽 깊이(m)</param>
        /// <param name="height">마루 높이(m)</param>
        /// <returns>플랫 셰이딩된 지붕 메시</returns>
        public static Mesh CreateGableRoof(float width, float depth, float height)
        {
            List<Vector3> verts = new List<Vector3>();
            List<int> tris = new List<int>();

            float hw = width * 0.5f;
            float hd = depth * 0.5f;

            Vector3 a = new Vector3(-hw, 0f, -hd);
            Vector3 b = new Vector3(hw, 0f, -hd);
            Vector3 c = new Vector3(hw, 0f, hd);
            Vector3 d = new Vector3(-hw, 0f, hd);

            Vector3 ridgeFront = new Vector3(0f, height, -hd);
            Vector3 ridgeBack = new Vector3(0f, height, hd);

            AddTriangle(verts, tris, a, ridgeFront, b);   // 앞 박공
            AddTriangle(verts, tris, c, ridgeBack, d);    // 뒤 박공

            AddQuad(verts, tris, ridgeFront, ridgeBack, c, b);   // 오른쪽 지붕면
            AddQuad(verts, tris, ridgeBack, ridgeFront, a, d);   // 왼쪽 지붕면

            // 아래를 막습니다. 언덕에 세우면 처마 밑이 보일 수 있습니다.
            AddQuad(verts, tris, a, b, c, d);

            return BuildFlatMesh("LowPolyRoof", verts, tris);
        }

        /// <summary>
        /// 면이 바깥을 향하는 비율을 구합니다. 1이면 전부 제대로 감겨 있습니다.
        ///
        /// 삼각형이 향하는 방향과 <b>메시 중심에서 그 삼각형으로 가는 방향</b>을 비교합니다.
        /// 두 방향이 같은 쪽이면 바깥을 봅니다. 볼록한 형태에서만 맞는 판정이지만,
        /// 여기서 만드는 것은 바위도 집도 볼록하므로 충분합니다.
        /// </summary>
        /// <param name="mesh">확인할 메시</param>
        /// <returns>0~1. 1이 정상입니다.</returns>
        public static float OutwardFaceRatio(Mesh mesh)
        {
            if (mesh == null) return 0f;

            Vector3[] verts = mesh.vertices;
            int[] tris = mesh.triangles;
            if (tris.Length == 0) return 0f;

            Vector3 center = mesh.bounds.center;

            int outward = 0;
            int total = tris.Length / 3;

            for (int i = 0; i < tris.Length; i += 3)
            {
                Vector3 v0 = verts[tris[i]];
                Vector3 v1 = verts[tris[i + 1]];
                Vector3 v2 = verts[tris[i + 2]];

                Vector3 facing = Vector3.Cross(v1 - v0, v2 - v0);
                Vector3 outDir = (v0 + v1 + v2) / 3f - center;

                if (Vector3.Dot(facing, outDir) > 0f) outward++;
            }

            return outward / (float)total;
        }

        // --- Private Methods : 메시 조립 ---

        /// <summary>
        /// 면마다 정점을 따로 두어 각진 메시를 만듭니다.
        ///
        /// 정점을 공유하면 법선이 평균되어 면이 둥글게 이어집니다.
        /// 저폴리 룩은 그 각진 경계에서 나오므로 <b>일부러 나눕니다.</b>
        /// </summary>
        private static Mesh BuildFlatMesh(string name, List<Vector3> verts, List<int> tris)
        {
            Vector3[] flatVerts = new Vector3[tris.Count];
            Vector3[] flatNormals = new Vector3[tris.Count];
            Vector2[] flatUVs = new Vector2[tris.Count];
            int[] flatTris = new int[tris.Count];

            for (int i = 0; i < tris.Count; i += 3)
            {
                Vector3 v0 = verts[tris[i]];
                Vector3 v1 = verts[tris[i + 1]];
                Vector3 v2 = verts[tris[i + 2]];

                Vector3 normal = Vector3.Cross(v1 - v0, v2 - v0).normalized;

                flatVerts[i] = v0; flatVerts[i + 1] = v1; flatVerts[i + 2] = v2;
                flatNormals[i] = normal; flatNormals[i + 1] = normal; flatNormals[i + 2] = normal;

                // UV 는 정직하게 펴지 않습니다. 이 게임의 재질은 텍스처가 아니라
                // 단색이라 UV 가 화면에 드러나지 않습니다.
                flatUVs[i] = new Vector2(0f, 0f);
                flatUVs[i + 1] = new Vector2(1f, 0f);
                flatUVs[i + 2] = new Vector2(0.5f, 1f);

                flatTris[i] = i; flatTris[i + 1] = i + 1; flatTris[i + 2] = i + 2;
            }

            Mesh mesh = new Mesh();
            mesh.name = name;
            mesh.vertices = flatVerts;
            mesh.normals = flatNormals;
            mesh.uv = flatUVs;
            mesh.triangles = flatTris;
            mesh.RecalculateBounds();

            return mesh;
        }

        /// <summary>
        /// 사각형 하나를 삼각형 둘로 넣습니다.
        ///
        /// <b>감는 방향이 곧 면이 보는 방향입니다.</b> 유니티에서 삼각형 (v0, v1, v2) 가
        /// 향하는 쪽은 <c>Cross(v1 - v0, v2 - v0)</c> 입니다. 이 방향이 물체 <b>바깥</b>을
        /// 향해야 합니다. 반대로 감으면 뒷면 컬링에 걸려 그 면이 사라집니다.
        ///
        /// 툰 셰이더는 뒷면을 부풀려 외곽선을 그리므로, 뒤집힌 메시는 단순히 사라지는 것이
        /// 아니라 <b>바깥면이 부풀려져 덩어리로 덮입니다.</b> 셰이더가 깨진 것처럼 보입니다.
        ///
        /// 실제로 그렇게 만들어 놓은 적이 있어, 이제 <see cref="OutwardFaceRatio"/> 로
        /// 만들 때마다 확인합니다.
        /// </summary>
        private static void AddQuad(List<Vector3> verts, List<int> tris,
                                    Vector3 a, Vector3 b, Vector3 c, Vector3 d)
        {
            AddTriangle(verts, tris, a, b, c);
            AddTriangle(verts, tris, a, c, d);
        }

        /// <summary>삼각형 하나를 넣습니다.</summary>
        private static void AddTriangle(List<Vector3> verts, List<int> tris,
                                        Vector3 a, Vector3 b, Vector3 c)
        {
            int start = verts.Count;

            verts.Add(a);
            verts.Add(b);
            verts.Add(c);

            tris.Add(start);
            tris.Add(start + 1);
            tris.Add(start + 2);
        }

        // --- Private Methods : 정이십면체 ---

        /// <summary>정이십면체를 만듭니다. 구를 나눌 때 가장 고르게 시작하는 도형입니다.</summary>
        private static void BuildIcosahedron(List<Vector3> verts, List<int> tris)
        {
            float t = (1f + Mathf.Sqrt(5f)) * 0.5f;

            Vector3[] source =
            {
                new Vector3(-1,  t,  0), new Vector3( 1,  t,  0),
                new Vector3(-1, -t,  0), new Vector3( 1, -t,  0),
                new Vector3( 0, -1,  t), new Vector3( 0,  1,  t),
                new Vector3( 0, -1, -t), new Vector3( 0,  1, -t),
                new Vector3( t,  0, -1), new Vector3( t,  0,  1),
                new Vector3(-t,  0, -1), new Vector3(-t,  0,  1),
            };

            for (int i = 0; i < source.Length; i++) verts.Add(source[i].normalized);

            int[] faces =
            {
                0,11,5, 0,5,1,  0,1,7,  0,7,10, 0,10,11,
                1,5,9,  5,11,4, 11,10,2, 10,7,6, 7,1,8,
                3,9,4,  3,4,2,  3,2,6,  3,6,8,  3,8,9,
                4,9,5,  2,4,11, 6,2,10, 8,6,7,  9,8,1,
            };

            tris.AddRange(faces);
        }

        /// <summary>모든 면을 넷으로 쪼갭니다. 새 정점은 구면 위로 밀어 올립니다.</summary>
        private static void Subdivide(List<Vector3> verts, List<int> tris)
        {
            Dictionary<long, int> cache = new Dictionary<long, int>();
            List<int> next = new List<int>(tris.Count * 4);

            for (int i = 0; i < tris.Count; i += 3)
            {
                int a = tris[i], b = tris[i + 1], c = tris[i + 2];

                int ab = MidPoint(verts, cache, a, b);
                int bc = MidPoint(verts, cache, b, c);
                int ca = MidPoint(verts, cache, c, a);

                next.AddRange(new[] { a, ab, ca,  b, bc, ab,  c, ca, bc,  ab, bc, ca });
            }

            tris.Clear();
            tris.AddRange(next);
        }

        /// <summary>두 정점 사이의 중점을 구합니다. 이미 만든 것은 다시 쓰지 않습니다.</summary>
        private static int MidPoint(List<Vector3> verts, Dictionary<long, int> cache, int a, int b)
        {
            long key = a < b ? ((long)a << 32) + b : ((long)b << 32) + a;

            int existing;
            if (cache.TryGetValue(key, out existing)) return existing;

            Vector3 mid = ((verts[a] + verts[b]) * 0.5f).normalized;

            verts.Add(mid);
            cache[key] = verts.Count - 1;

            return verts.Count - 1;
        }

        // --- Private Methods : 노이즈 ---

        /// <summary>
        /// 3차원 좌표에서 0~1 노이즈를 뽑습니다.
        /// Unity 에는 3D Perlin 이 없어 2D 를 세 축으로 섞습니다. 바위 표면에는 충분합니다.
        /// </summary>
        private static float Noise3(Vector3 p)
        {
            float xy = Mathf.PerlinNoise(p.x, p.y);
            float yz = Mathf.PerlinNoise(p.y, p.z);
            float zx = Mathf.PerlinNoise(p.z, p.x);

            return (xy + yz + zx) / 3f;
        }
    }
}
