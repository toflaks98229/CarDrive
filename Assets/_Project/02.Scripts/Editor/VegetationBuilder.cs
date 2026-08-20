using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using CarDrive.Systems;

namespace CarDrive.EditorTools
{
    /// <summary>
    /// 식생 종의 <b>메시와 프리팹</b>을 만듭니다.
    ///
    /// <b>왜 코드로 만드는가.</b> 잎 하나가 삼각형 한 장인 단순한 모양이라, 모델링 파일로
    /// 들고 있을 이유가 없습니다. 잎 수·높이·반경만 바꾸면 다른 종이 되므로
    /// <see cref="VegetationSpecies"/> 값에서 바로 뽑아냅니다.
    /// <b>새 아트 없이 종을 늘릴 수 있는 것</b>이 이 방식의 핵심입니다.
    ///
    /// 예전에는 이 일이 <c>LowPolyLookSetup</c> 안에 풀 한 종류 전용으로 박혀 있었습니다.
    /// </summary>
    public static class VegetationBuilder
    {
        // --- Constants ---

        /// <summary>만들어진 메시와 프리팹이 놓이는 폴더입니다.</summary>
        public const string GeneratedFolder = "Assets/_Project/04.Art/02.Models/Generated";

        // --- Public Methods ---

        /// <summary>
        /// 설정에 적힌 식생 종을 모두 만들어 프리팹 목록으로 돌려줍니다.
        /// </summary>
        /// <param name="species">만들 종 목록</param>
        /// <param name="material">잎에 씌울 머티리얼</param>
        /// <param name="report">결과를 적을 목록. null이어도 됩니다.</param>
        /// <returns>종 순서대로의 프리팹. 만들지 못한 자리는 null입니다.</returns>
        public static GameObject[] BuildAll(List<VegetationSpecies> species, Material material, List<string> report)
        {
            if (species == null || species.Count == 0 || material == null) return new GameObject[0];

            GameObject[] prefabs = new GameObject[species.Count];

            for (int i = 0; i < species.Count; i++)
            {
                prefabs[i] = Build(species[i], material);

                if (report == null || prefabs[i] == null) continue;

                report.Add("· 식생: " + species[i].id +
                           " (잎 " + species[i].bladesPerTuft +
                           " / 키 " + species[i].bladeHeight.ToString("F2") + "m" +
                           " / 비중 " + species[i].weight.ToString("F2") + ")");
            }

            AssetDatabase.SaveAssets();
            return prefabs;
        }

        /// <summary>
        /// 종 하나의 메시와 프리팹을 만듭니다. 이미 있으면 내용만 갈아 끼웁니다.
        /// </summary>
        /// <param name="spec">만들 종</param>
        /// <param name="material">잎에 씌울 머티리얼</param>
        /// <returns>만들어진 프리팹. 실패하면 null입니다.</returns>
        public static GameObject Build(VegetationSpecies spec, Material material)
        {
            if (spec == null || material == null) return null;
            if (string.IsNullOrEmpty(spec.id)) return null;

            Mesh mesh = BuildMesh(spec);
            if (mesh == null) return null;

            string prefabPath = GeneratedFolder + "/" + spec.id + ".prefab";

            GameObject temp = new GameObject(spec.id);
            temp.AddComponent<MeshFilter>().sharedMesh = mesh;

            MeshRenderer renderer = temp.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;

            // 포기가 수십만 개입니다. 그림자를 드리우게 두면 그것만으로 프레임이 무너집니다.
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = true;

            GameObject saved = PrefabUtility.SaveAsPrefabAsset(temp, prefabPath);
            Object.DestroyImmediate(temp);

            return saved;
        }

        // --- Private Methods ---

        /// <summary>
        /// 종의 값대로 포기 메시를 만듭니다.
        ///
        /// <b>잎 하나가 삼각형 한 장입니다.</b> 사각형으로 하면 정점 넷에 삼각형 둘이 드는데,
        /// 잎 끝은 어차피 폭이 거의 0으로 좁아지니 끝을 한 점으로 모으면
        /// 정점 하나와 삼각형 하나가 그대로 빠집니다. 포기가 수십만 개라 이 차이가 큽니다.
        /// </summary>
        /// <param name="spec">모양을 정하는 종</param>
        /// <returns>만들어진 메시</returns>
        private static Mesh BuildMesh(VegetationSpecies spec)
        {
            int bladeCount = Mathf.Max(1, spec.bladesPerTuft);

            List<Vector3> verts = new List<Vector3>(bladeCount * 3);
            List<Vector3> normals = new List<Vector3>(bladeCount * 3);
            List<Vector2> uvs = new List<Vector2>(bladeCount * 3);
            List<int> tris = new List<int>(bladeCount * 3);

            // 언제 다시 구워도 같은 모양이 나오도록 씨앗을 고정합니다.
            Random.State previous = Random.state;
            Random.InitState(spec.seed);

            for (int i = 0; i < bladeCount; i++)
            {
                float angle = (i / (float)bladeCount) * Mathf.PI * 2f + Random.Range(-0.5f, 0.5f);
                float dist = spec.tuftRadius * Mathf.Sqrt(Random.value);
                Vector3 root = new Vector3(Mathf.Cos(angle) * dist, 0f, Mathf.Sin(angle) * dist);

                float yaw = Random.Range(0f, Mathf.PI * 2f);
                Vector3 side = new Vector3(Mathf.Cos(yaw), 0f, Mathf.Sin(yaw)) * spec.bladeWidth;
                Vector3 face = Vector3.Cross(Vector3.up, side).normalized;

                float height = spec.bladeHeight * Random.Range(0.7f, 1.3f);
                Vector3 lean = face * (height * Random.Range(spec.lean * 0.5f, spec.lean * 1.5f));
                Vector3 tip = root + Vector3.up * height + lean;

                int b = verts.Count;
                verts.Add(root - side);
                verts.Add(root + side);
                verts.Add(tip);

                for (int n = 0; n < 3; n++) normals.Add(face);

                uvs.Add(new Vector2(0f, 0f));
                uvs.Add(new Vector2(1f, 0f));
                uvs.Add(new Vector2(0.5f, 1f));

                tris.Add(b);
                tris.Add(b + 2);
                tris.Add(b + 1);
            }

            Random.state = previous;

            string meshPath = GeneratedFolder + "/" + spec.id + ".asset";

            Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
            if (mesh == null)
            {
                mesh = new Mesh();
                AssetDatabase.CreateAsset(mesh, meshPath);
            }

            mesh.Clear();
            mesh.name = spec.id;
            mesh.SetVertices(verts);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();

            EditorUtility.SetDirty(mesh);
            return mesh;
        }
    }
}
