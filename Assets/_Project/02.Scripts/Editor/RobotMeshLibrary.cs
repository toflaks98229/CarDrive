using System.Collections.Generic;
using System.Globalization;
using UnityEditor;
using UnityEngine;
using UnityEngine.ProBuilder;

namespace CarDrive.EditorTools
{
    /// <summary>
    /// 보행 로봇이 쓸 메시를 <b>실제 크기로</b> 구워 에셋으로 저장합니다. ProBuilder 로 만듭니다.
    ///
    /// <b>왜 스케일을 쓰지 않는가.</b> 예전에는 유니티 기본 도형(반지름 0.5 · 높이 2)을 가져다
    /// <c>localScale</c> 로 눌러 마디 모양을 냈습니다. 그러면 오브젝트마다 (0.085, 0.3, 0.085) 같은
    /// <b>비균등 스케일</b>이 남습니다. 그 자체로 틀린 것은 아니지만 위험한 상태입니다.
    ///  1. 비균등 스케일이 붙은 트랜스폼 <b>아래에 무언가를 달면</b> 그 자식이 찌그러집니다.
    ///     회전까지 섞이면 기울어진 채 늘어나 되돌릴 수 없습니다.
    ///  2. 콜라이더·리지드바디가 그 아래로 들어가면 물리 크기가 <b>눈에 보이는 것과 달라집니다.</b>
    ///  3. 길이를 바꾸려면 스케일을 고쳐야 하는데, 그 값이 <b>메시 원본 크기에 대한 비율</b>이라
    ///     "이 마디는 0.6m" 같은 뜻을 잃습니다.
    ///
    /// 메시를 처음부터 그 크기로 만들면 모든 오브젝트가 <b>스케일 (1, 1, 1)</b> 로 남습니다.
    /// 길이는 메시가 갖고, 트랜스폼은 위치와 방향만 갖습니다.
    ///
    /// <b>축까지 구워 넣습니다.</b> ProBuilder 원기둥은 Y 축으로 서 있고 가운데가 원점입니다.
    /// 이 프로젝트의 마디 규약은 <b>로컬 +Z 가 다음 관절을 향하고 원점이 관절</b>이므로,
    /// 눕히고 미는 변환을 <b>정점에 미리 적용</b>해 둡니다. 그러면 메시를 붙이는 오브젝트가
    /// 위치·회전·스케일 전부 기본값이 됩니다.
    ///
    /// 같은 치수는 한 번만 굽고 다시 씁니다. 이름에 치수가 들어 있어 에셋만 봐도 무엇인지 압니다.
    /// </summary>
    public static class RobotMeshLibrary
    {
        // --- Constants ---

        /// <summary>구운 메시가 놓일 폴더입니다.</summary>
        private const string Folder = "Assets/_Project/04.Art/02.Models/Robot";

        /// <summary>원기둥의 둘레 분할 수입니다. 화면이 픽셀화되므로 많이 나눌 이유가 없습니다.</summary>
        private const int CylinderSides = 12;

        /// <summary>구의 분할 횟수입니다. 0이면 20면체, 1이면 80면체입니다.</summary>
        private const int SphereSubdivisions = 1;

        // --- Private Member Variables ---

        /// <summary>이번 실행에서 이미 구운 메시입니다. 같은 치수를 두 번 굽지 않습니다.</summary>
        private static readonly Dictionary<string, Mesh> Cache = new Dictionary<string, Mesh>();

        // --- Public Methods ---

        /// <summary>가운데가 원점인 상자를 굽습니다.</summary>
        /// <param name="size">상자의 크기</param>
        /// <returns>그 크기로 만들어진 메시</returns>
        public static Mesh Box(Vector3 size)
        {
            string name = string.Format(CultureInfo.InvariantCulture, "Box_{0:0.###}x{1:0.###}x{2:0.###}",
                size.x, size.y, size.z);

            return GetOrBake(name, () => ShapeGenerator.GenerateCube(PivotLocation.Center, size), Matrix4x4.identity);
        }

        /// <summary>
        /// <b>원점에서 +Z 방향으로</b> 뻗은 원기둥을 굽습니다. 마디 메시가 이것입니다.
        /// </summary>
        /// <param name="length">마디 길이</param>
        /// <param name="diameter">마디 지름</param>
        /// <returns>눕혀서 구운 메시</returns>
        public static Mesh CylinderAlongZ(float length, float diameter)
        {
            string name = string.Format(CultureInfo.InvariantCulture, "Limb_L{0:0.###}_D{1:0.###}", length, diameter);

            // Y 로 선 원기둥을 X 축으로 90도 눕히면 +Z 를 봅니다.
            // 그 상태에서 절반만큼 밀면 아래 끝이 원점(관절)에 옵니다.
            Matrix4x4 bake = Matrix4x4.TRS(new Vector3(0f, 0f, length * 0.5f), Quaternion.Euler(90f, 0f, 0f), Vector3.one);

            return GetOrBake(name,
                () => ShapeGenerator.GenerateCylinder(PivotLocation.Center, CylinderSides, diameter * 0.5f, length, 0),
                bake);
        }

        /// <summary>가운데가 원점인 구를 굽습니다. 관절을 덮는 데 씁니다.</summary>
        /// <param name="diameter">구의 지름</param>
        /// <returns>그 크기로 만들어진 메시</returns>
        public static Mesh Sphere(float diameter)
        {
            string name = string.Format(CultureInfo.InvariantCulture, "Joint_D{0:0.###}", diameter);

            return GetOrBake(name,
                () => ShapeGenerator.GenerateIcosahedron(PivotLocation.Center, diameter * 0.5f, SphereSubdivisions),
                Matrix4x4.identity);
        }

        /// <summary>이번 실행의 기억을 비웁니다. 치수를 바꿔 다시 구울 때 부릅니다.</summary>
        public static void Clear()
        {
            Cache.Clear();
        }

        /// <summary>
        /// 씬에서 <b>손으로 다듬은 메시</b>를 로봇 메시 에셋에 덮어씁니다.
        ///
        /// ProBuilder 로 마디 모양을 직접 깎으려면 씬의 오브젝트를 다듬어야 하는데, 그렇게 만든
        /// 메시는 <b>그 씬의 것</b>이라 프리팹이 쓰지 못합니다. 이 명령이 그것을 에셋으로 옮깁니다.
        ///
        /// <b>이미 있는 에셋에 덮어쓰면 GUID 가 그대로 남습니다.</b> 그래서 그 메시를 쓰는
        /// <b>모든 프리팹이 한 번에</b> 바뀝니다 — 프리팹을 다시 구울 필요가 없습니다.
        /// 네 다리가 같은 메시를 나눠 쓰므로 하나만 고치면 넷이 함께 바뀝니다.
        ///
        /// 오브젝트의 <b>위치·회전·스케일은 굽지 않습니다.</b> 마디 메시는 원점이 관절이고 +Z 가
        /// 다음 관절이라는 규약을 지켜야 하므로, 그 자리에 놓인 그대로 다듬어야 합니다.
        /// </summary>
        [MenuItem("CarDrive/Gameplay/보행 로봇/선택한 메시를 로봇 메시로 굽기")]
        public static void BakeSelectionIntoAsset()
        {
            GameObject selection = Selection.activeGameObject;

            if (selection == null)
            {
                Debug.LogWarning("RobotMeshLibrary: 구울 오브젝트를 먼저 고르세요.");
                return;
            }

            MeshFilter filter = selection.GetComponent<MeshFilter>();

            if (filter == null || filter.sharedMesh == null)
            {
                Debug.LogWarning("RobotMeshLibrary: " + selection.name + " 에는 메시가 없습니다.", selection);
                return;
            }

            EnsureFolder(Folder);

            string path = EditorUtility.SaveFilePanelInProject(
                "로봇 메시로 굽기", selection.name, "asset",
                "이미 있는 에셋을 고르면 덮어씁니다. 그 메시를 쓰는 프리팹이 전부 함께 바뀝니다.", Folder);

            if (string.IsNullOrEmpty(path)) return;

            Mesh source = filter.sharedMesh;
            Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);

            if (existing != null)
            {
                // 이름은 지키고 내용만 갈아 끼웁니다. GUID 가 살아 있어야 참조가 끊기지 않습니다.
                string keep = existing.name;

                EditorUtility.CopySerialized(source, existing);

                existing.name = keep;

                EditorUtility.SetDirty(existing);
                AssetDatabase.SaveAssets();

                Cache[keep] = existing;

                Debug.Log("RobotMeshLibrary: 덮어썼습니다 — " + path + System.Environment.NewLine +
                    "  이 메시를 쓰는 프리팹이 전부 함께 바뀝니다.", existing);
                return;
            }

            Mesh baked = Object.Instantiate(source);
            baked.name = System.IO.Path.GetFileNameWithoutExtension(path);

            AssetDatabase.CreateAsset(baked, path);
            AssetDatabase.SaveAssets();

            Cache[baked.name] = baked;

            Debug.Log("RobotMeshLibrary: 새로 구웠습니다 — " + path + System.Environment.NewLine +
                "  프리팹의 Mesh Filter 칸에 끼워 넣으세요.", baked);
        }

        // --- Private Methods ---

        /// <summary>
        /// 이미 있으면 그것을 쓰고, 없으면 굽고 저장합니다.
        /// </summary>
        /// <param name="name">에셋 이름. 치수가 들어 있어 이름이 곧 열쇠입니다.</param>
        /// <param name="build">ProBuilder 도형을 만드는 식</param>
        /// <param name="bake">정점에 미리 적용할 변환</param>
        /// <returns>구워진 메시 에셋</returns>
        private static Mesh GetOrBake(string name, System.Func<ProBuilderMesh> build, Matrix4x4 bake)
        {
            if (Cache.TryGetValue(name, out Mesh cached) && cached != null) return cached;

            string path = Folder + "/" + name + ".asset";

            Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing != null)
            {
                Cache[name] = existing;
                return existing;
            }

            ProBuilderMesh shape = build();

            shape.ToMesh();
            shape.Refresh();

            // ProBuilder 가 만든 메시는 그 씬 오브젝트의 것입니다. 복사해서 에셋으로 남깁니다.
            Mesh mesh = Object.Instantiate(shape.GetComponent<MeshFilter>().sharedMesh);
            Object.DestroyImmediate(shape.gameObject);

            Bake(mesh, bake);

            mesh.name = name;

            EnsureFolder(Folder);
            AssetDatabase.CreateAsset(mesh, path);

            Cache[name] = mesh;
            return mesh;
        }

        /// <summary>변환을 정점과 법선에 미리 적용합니다.</summary>
        /// <param name="mesh">고칠 메시</param>
        /// <param name="matrix">적용할 변환</param>
        private static void Bake(Mesh mesh, Matrix4x4 matrix)
        {
            if (matrix.isIdentity) return;

            Vector3[] vertices = mesh.vertices;
            for (int i = 0; i < vertices.Length; i++) vertices[i] = matrix.MultiplyPoint3x4(vertices[i]);
            mesh.vertices = vertices;

            Vector3[] normals = mesh.normals;
            if (normals != null && normals.Length == vertices.Length)
            {
                for (int i = 0; i < normals.Length; i++) normals[i] = matrix.MultiplyVector(normals[i]).normalized;
                mesh.normals = normals;
            }

            mesh.RecalculateBounds();
        }

        /// <summary>폴더가 없으면 만듭니다. (중간 폴더까지 차례로 만듭니다)</summary>
        /// <param name="path">"Assets/A/B" 형태의 폴더 경로</param>
        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;

            string[] parts = path.Split('/');
            string current = parts[0];

            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }
    }
}
