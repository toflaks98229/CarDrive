using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CarDrive.EditorTools
{
    /// <summary>
    /// 월드를 툰 셰이더로 바꿉니다.
    ///
    ///  1. 툰 지면 재질을 만들고, 기존 LowPoly 지면의 색 설정을 <b>그대로 옮깁니다.</b>
    ///  2. 씬의 모든 터레인을 그 재질로 갈아 끼웁니다.
    ///  3. URP Lit 을 쓰는 월드 재질을 CarDrive/Toon Lit 으로 바꿉니다.
    ///     (바탕 텍스처와 색은 옮기고 나머지는 툰 기본값을 씁니다)
    ///  4. 툰 룩에 맞게 조명을 정리합니다. 그림자를 또렷하게, 색 공간을 선형으로.
    ///
    /// <b>되돌릴 수 있습니다.</b> 원래 셰이더를 재질에 기록해 두므로
    /// "툰 룩 되돌리기" 로 원래대로 돌아갑니다.
    ///
    /// 툰 조명 기법은 ColinLeung-NiloCat 의 UnityURPToonLitShaderExample (MIT) 을 참고했습니다.
    /// </summary>
    public static class ToonLookSetup
    {
        /// <summary>메인 씬 경로입니다.</summary>
        private const string ScenePath = "Assets/_Project/01.Scenes/SampleScene.unity";

        /// <summary>툰 지면 재질을 둘 경로입니다.</summary>
        private const string ToonTerrainMatPath = "Assets/_Project/04.Art/03.Shaders/Toon/CarDriveToonTerrain.mat";

        /// <summary>기존 로우폴리 지면 재질 경로입니다. 색을 여기서 가져옵니다.</summary>
        private const string LowPolyTerrainMatPath = "Assets/_Project/04.Art/03.Shaders/LowPoly/LowPolyTerrain.mat";

        /// <summary>메시용 툰 셰이더 이름입니다.</summary>
        private const string ToonLitName = "CarDrive/Toon Lit";

        /// <summary>지면용 툰 셰이더 이름입니다.</summary>
        private const string ToonTerrainName = "CarDrive/Toon Terrain";

        /// <summary>풀 셰이더 이름입니다. 지면과 같은 램프를 씁니다.</summary>
        private const string GrassShaderName = "CarDrive/LowPoly Grass";

        /// <summary>메시가 쓸 램프 경로입니다.</summary>
        private const string MeshRampPath = "Assets/_Project/04.Art/03.Shaders/Toon/Ramps/ToonRamp_NightDrive.png";

        /// <summary>지면이 쓸 램프 경로입니다. 띠가 적어 넓은 면에서 등고선이 덜 보입니다.</summary>
        private const string GroundRampPath = "Assets/_Project/04.Art/03.Shaders/Toon/Ramps/ToonRamp_Ground.png";

        /// <summary>되돌리기용으로 원래 셰이더 이름을 적어 두는 키입니다.</summary>
        private const string OriginalShaderKey = "CarDriveToonOriginalShader";

        /// <summary>지면 재질에서 옮겨 올 색 속성들입니다.</summary>
        private static readonly string[] GroundColorKeys =
        {
            "_GrassColorA", "_GrassColorB",
            "_DirtColorA",  "_DirtColorB",
            "_RoadColorA",  "_RoadColorB",
        };

        // --- Public Methods ---

        /// <summary>에디터 메뉴에서 실행합니다.</summary>
        [MenuItem("CarDrive/Look/툰 룩 적용")]
        public static void Apply()
        {
            List<string> report = new List<string>();

            Material terrainMat = BuildToonTerrainMaterial(report);
            ApplyToTerrains(terrainMat, report);
            ApplyToMeshMaterials(report);
            TuneLighting(report);
            AssignRamps(report);
            ReportShaderErrors(report);

            AssetDatabase.SaveAssets();

            Debug.Log("ToonLookSetup:" + System.Environment.NewLine +
                      string.Join(System.Environment.NewLine, report));
        }

        /// <summary>툰 룩을 적용하기 전으로 되돌립니다.</summary>
        [MenuItem("CarDrive/Look/툰 룩 되돌리기")]
        public static void Revert()
        {
            List<string> report = new List<string>();
            int restored = 0;

            string[] guids = AssetDatabase.FindAssets("t:Material", new[] { "Assets/_Project" });
            for (int i = 0; i < guids.Length; i++)
            {
                Material mat = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guids[i]));
                if (mat == null) continue;

                string original = GetOriginalShader(mat);
                if (string.IsNullOrEmpty(original)) continue;

                Shader shader = Shader.Find(original);
                if (shader == null) continue;

                mat.shader = shader;
                ClearOriginalShader(mat);
                EditorUtility.SetDirty(mat);
                restored++;
            }

            report.Add("· 재질 " + restored + "개를 원래 셰이더로 되돌렸습니다.");

            Terrain[] terrains = Object.FindObjectsByType<Terrain>(FindObjectsInactive.Include);
            Material lowPoly = AssetDatabase.LoadAssetAtPath<Material>(LowPolyTerrainMatPath);
            if (lowPoly != null)
            {
                for (int i = 0; i < terrains.Length; i++) terrains[i].materialTemplate = lowPoly;
                report.Add("· 터레인 " + terrains.Length + "개를 LowPoly 지면으로 되돌렸습니다.");
            }

            AssetDatabase.SaveAssets();
            Debug.Log("ToonLookSetup(되돌리기):" + System.Environment.NewLine +
                      string.Join(System.Environment.NewLine, report));
        }

        /// <summary>
        /// 명령줄에서 씬을 열고 적용한 뒤 저장합니다.
        /// <c>Unity.exe -batchmode -quit -executeMethod CarDrive.EditorTools.ToonLookSetup.ApplyFromCommandLine</c>
        /// </summary>
        public static void ApplyFromCommandLine()
        {
            UnityEngine.SceneManagement.Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            Apply();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
        }

        // --- Private Methods ---

        /// <summary>
        /// 툰 지면 재질을 만들고 기존 색 설정을 옮깁니다. 이미 있으면 그대로 씁니다.
        /// </summary>
        /// <param name="report">진행 내용을 적을 목록</param>
        /// <returns>툰 지면 재질. 셰이더를 찾지 못하면 null입니다.</returns>
        private static Material BuildToonTerrainMaterial(List<string> report)
        {
            Shader shader = Shader.Find(ToonTerrainName);
            if (shader == null)
            {
                report.Add("! " + ToonTerrainName + " 셰이더를 찾지 못했습니다.");
                return null;
            }

            Material mat = AssetDatabase.LoadAssetAtPath<Material>(ToonTerrainMatPath);
            if (mat == null)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, ToonTerrainMatPath);
                report.Add("· 툰 지면 재질을 만들었습니다: " + ToonTerrainMatPath);
            }
            else
            {
                mat.shader = shader;
                report.Add("· 툰 지면 재질이 이미 있습니다.");
            }

            // 기존 지면의 색을 그대로 물려받습니다. 색까지 바뀌면 무엇이 달라졌는지 알 수 없습니다.
            Material source = AssetDatabase.LoadAssetAtPath<Material>(LowPolyTerrainMatPath);
            if (source != null)
            {
                int moved = 0;
                for (int i = 0; i < GroundColorKeys.Length; i++)
                {
                    string key = GroundColorKeys[i];
                    if (!source.HasProperty(key) || !mat.HasProperty(key)) continue;

                    mat.SetColor(key, source.GetColor(key));
                    moved++;
                }
                if (source.HasProperty("_ColorNoiseScale") && mat.HasProperty("_ColorNoiseScale"))
                {
                    mat.SetFloat("_ColorNoiseScale", source.GetFloat("_ColorNoiseScale"));
                }
                report.Add("· 기존 지면 색 " + moved + "개를 옮겼습니다.");
            }

            EditorUtility.SetDirty(mat);
            return mat;
        }

        /// <summary>
        /// 씬의 모든 터레인에 툰 지면 재질을 적용합니다.
        /// </summary>
        /// <param name="mat">적용할 재질</param>
        /// <param name="report">진행 내용을 적을 목록</param>
        private static void ApplyToTerrains(Material mat, List<string> report)
        {
            if (mat == null) return;

            Terrain[] terrains = Object.FindObjectsByType<Terrain>(FindObjectsInactive.Include);
            for (int i = 0; i < terrains.Length; i++)
            {
                terrains[i].materialTemplate = mat;
                EditorUtility.SetDirty(terrains[i]);
            }

            report.Add("· 터레인 " + terrains.Length + "개에 툰 지면을 적용했습니다.");
        }

        /// <summary>
        /// URP Lit 을 쓰는 월드 재질을 툰으로 바꿉니다.
        ///
        /// <b>스프라이트·UI·후처리 재질은 건드리지 않습니다.</b> 그것들은 조명을 받지 않거나
        /// 다른 방식으로 그려지므로, 툰 셰이더로 바꾸면 그냥 깨집니다.
        /// </summary>
        /// <param name="report">진행 내용을 적을 목록</param>
        private static void ApplyToMeshMaterials(List<string> report)
        {
            Shader toon = Shader.Find(ToonLitName);
            if (toon == null)
            {
                report.Add("! " + ToonLitName + " 셰이더를 찾지 못했습니다.");
                return;
            }

            string[] guids = AssetDatabase.FindAssets("t:Material", new[] { "Assets/_Project" });
            int changed = 0;
            List<string> skipped = new List<string>();

            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (mat == null || mat.shader == null) continue;

                string name = mat.shader.name;

                // 이미 툰이면 건너뜁니다.
                if (name == ToonLitName || name == ToonTerrainName) continue;

                // 조명을 받는 불투명 재질만 대상으로 삼습니다.
                bool isLitMesh = name == "Universal Render Pipeline/Lit"
                                 || name == "Universal Render Pipeline/Simple Lit"
                                 || name == "Standard";

                if (!isLitMesh)
                {
                    skipped.Add(mat.name);
                    continue;
                }

                // 옮길 수 있는 것만 옮깁니다. 나머지는 툰 기본값을 씁니다.
                Texture baseMap = mat.HasProperty("_BaseMap") ? mat.GetTexture("_BaseMap") : null;
                Color baseColor = mat.HasProperty("_BaseColor") ? mat.GetColor("_BaseColor") : Color.white;

                RememberOriginalShader(mat, name);
                mat.shader = toon;

                if (baseMap != null && mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", baseMap);
                if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", baseColor);

                EditorUtility.SetDirty(mat);
                changed++;
            }

            report.Add("· 메시 재질 " + changed + "개를 툰으로 바꿨습니다.");
            if (skipped.Count > 0)
            {
                report.Add("  (건드리지 않음: " + skipped.Count + "개 — 스프라이트·UI·후처리 등)");
            }
        }

        /// <summary>
        /// 툰 룩에 어울리게 조명을 정리합니다.
        ///
        /// 툰은 <b>경계가 또렷해야</b> 살아납니다. 그림자가 흐리면 명암 경계만 딱딱하고
        /// 그림자만 뭉개져서 둘이 따로 놉니다.
        /// </summary>
        /// <param name="report">진행 내용을 적을 목록</param>
        private static void TuneLighting(List<string> report)
        {
            Light sun = null;
            Light[] lights = Object.FindObjectsByType<Light>(FindObjectsInactive.Include);
            for (int i = 0; i < lights.Length; i++)
            {
                if (lights[i].type != LightType.Directional) continue;
                if (sun == null || lights[i].intensity > sun.intensity) sun = lights[i];
            }

            if (sun == null)
            {
                report.Add("! 태양광(Directional Light)을 찾지 못했습니다.");
                return;
            }

            sun.shadows = LightShadows.Hard;
            sun.shadowStrength = 0.85f;
            EditorUtility.SetDirty(sun);

            report.Add("· 태양광 '" + sun.name + "' 의 그림자를 또렷하게(Hard) 바꿨습니다.");
            report.Add("  세기와 각도는 TimeSystem 이 매 프레임 정하므로 건드리지 않았습니다.");
        }



        /// <summary>
        /// 구워 둔 램프를 재질에 연결하고 램프 모드를 켭니다.
        ///
        /// 램프가 없으면 굽지 않고 조용히 넘어갑니다. 램프 없이도 툰은 동작하기 때문입니다.
        /// (그때는 단계(_Steps) 방식으로 돌아갑니다)
        /// </summary>
        /// <param name="report">진행 내용을 적을 목록</param>
        private static void AssignRamps(List<string> report)
        {
            Texture2D meshRamp = AssetDatabase.LoadAssetAtPath<Texture2D>(MeshRampPath);
            Texture2D groundRamp = AssetDatabase.LoadAssetAtPath<Texture2D>(GroundRampPath);

            if (meshRamp == null && groundRamp == null)
            {
                report.Add("· 램프가 없어 단계(_Steps) 방식을 씁니다. " +
                           "CarDrive > Look > 툰 램프 굽기 를 실행하면 램프 방식으로 바뀝니다.");
                return;
            }

            int applied = 0;
            string[] guids = AssetDatabase.FindAssets("t:Material", new[] { "Assets/_Project" });

            for (int i = 0; i < guids.Length; i++)
            {
                Material mat = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guids[i]));
                if (mat == null || mat.shader == null) continue;

                bool isTerrain = mat.shader.name == ToonTerrainName;
                bool isMesh = mat.shader.name == ToonLitName;

                // 풀은 지면 위에 서 있으므로 <b>지면과 같은 램프</b>를 써야
                // 풀밭이 시작되는 자리에 명암 경계가 어긋나지 않습니다.
                bool isGrass = mat.shader.name == GrassShaderName;

                if (!isTerrain && !isMesh && !isGrass) continue;

                Texture2D ramp = (isTerrain || isGrass) ? groundRamp : meshRamp;
                if (ramp == null) continue;

                mat.SetTexture("_ToonRampMap", ramp);

                // <b>텍스처가 실제로 붙었는지 확인한 뒤에</b> 키워드를 켭니다.
                //
                // 램프 없이 키워드만 켜면 셰이더가 기본 흰색 텍스처를 읽습니다.
                // 그러면 밝기와 상관없이 흰색이 나와 <b>그림자 계산 결과가 통째로 버려집니다.</b>
                // 화면은 그냥 "그림자가 안 지는" 상태가 되고, 어디가 잘못됐는지 드러나지 않습니다.
                // 실제로 풀이 이 상태로 한동안 남아 있었습니다.
                if (mat.GetTexture("_ToonRampMap") == null)
                {
                    mat.SetFloat("_UseRamp", 0f);
                    mat.DisableKeyword("_TOON_RAMP");

                    report.Add("! " + mat.name + " 에 램프를 붙이지 못해 단계 방식으로 되돌렸습니다.");
                    EditorUtility.SetDirty(mat);
                    continue;
                }

                mat.SetFloat("_UseRamp", 1f);
                mat.EnableKeyword("_TOON_RAMP");

                EditorUtility.SetDirty(mat);
                applied++;
            }

            report.Add("· 램프를 재질 " + applied + "개에 연결했습니다. (지면과 메시가 다른 램프를 씁니다)");
            ReportRampMismatch(report);
        }

        /// <summary>
        /// 툰 셰이더가 실제로 컴파일되었는지 확인합니다.
        ///
        /// <b>임포트가 되었다는 것과 컴파일된다는 것은 다릅니다.</b> 셰이더 오류는
        /// 재질이 분홍색으로 나올 때에야 눈에 띄는데, 그때는 이미 씬을 다 바꾼 뒤입니다.
        /// 그래서 적용 직후에 여기서 확인합니다.
        /// </summary>
        /// <param name="report">진행 내용을 적을 목록</param>
        private static void ReportShaderErrors(List<string> report)
        {
            string[] names = { ToonLitName, ToonTerrainName };

            for (int i = 0; i < names.Length; i++)
            {
                Shader shader = Shader.Find(names[i]);
                if (shader == null)
                {
                    report.Add("! 셰이더를 찾지 못했습니다: " + names[i]);
                    continue;
                }

                int count = UnityEditor.ShaderUtil.GetShaderMessageCount(shader);
                if (count == 0)
                {
                    report.Add("· 셰이더 정상: " + names[i]);
                    continue;
                }

                report.Add("! 셰이더 메시지 " + count + "건: " + names[i]);

                // 이 프로젝트는 보통 타입을 명시하지만 여기서는 var 를 씁니다.
                // ShaderMessage 가 어느 네임스페이스에 있는지 Unity 버전마다 달라서,
                // 이름을 적으면 버전이 바뀔 때 컴파일이 깨집니다.
                var messages = UnityEditor.ShaderUtil.GetShaderMessages(shader);

                for (int m = 0; m < messages.Length && m < 8; m++)
                {
                    report.Add("   [" + messages[m].severity + "] " + messages[m].message);
                }
            }
        }

        /// <summary>원래 셰이더 이름을 재질에 적어 둡니다. 되돌리기에 씁니다.</summary>
        /// <param name="mat">기록할 재질</param>
        /// <param name="shaderName">원래 셰이더 이름</param>
        private static void RememberOriginalShader(Material mat, string shaderName)
        {
            // 이미 기록이 있으면 덮어쓰지 않습니다. 두 번 적용해도 최초 값이 남아야 합니다.
            if (!string.IsNullOrEmpty(GetOriginalShader(mat))) return;

            string label = OriginalShaderKey + ":" + shaderName;
            List<string> labels = new List<string>(AssetDatabase.GetLabels(mat));
            labels.Add(label);
            AssetDatabase.SetLabels(mat, labels.ToArray());
        }

        /// <summary>재질에 적어 둔 원래 셰이더 이름을 읽습니다.</summary>
        /// <param name="mat">읽을 재질</param>
        /// <returns>원래 셰이더 이름. 기록이 없으면 빈 문자열입니다.</returns>
        private static string GetOriginalShader(Material mat)
        {
            string[] labels = AssetDatabase.GetLabels(mat);
            for (int i = 0; i < labels.Length; i++)
            {
                if (labels[i].StartsWith(OriginalShaderKey + ":"))
                {
                    return labels[i].Substring(OriginalShaderKey.Length + 1);
                }
            }
            return "";
        }

        /// <summary>기록을 지웁니다.</summary>
        /// <param name="mat">지울 재질</param>
        private static void ClearOriginalShader(Material mat)
        {
            List<string> kept = new List<string>();
            string[] labels = AssetDatabase.GetLabels(mat);

            for (int i = 0; i < labels.Length; i++)
            {
                if (!labels[i].StartsWith(OriginalShaderKey + ":")) kept.Add(labels[i]);
            }

            AssetDatabase.SetLabels(mat, kept.ToArray());
        }

        /// <summary>
        /// <b>램프 없이 키워드만 켜진</b> 재질이 있는지 봅니다.
        ///
        /// 이 상태는 화면에서 "그림자가 안 진다"로만 나타나 원인을 찾기 어렵습니다.
        /// 셰이더가 기본 흰색 텍스처를 읽어 밝기를 무시하기 때문인데,
        /// 재질을 열어 봐도 키워드는 정상으로 보입니다. 그래서 여기서 세어 알립니다.
        /// </summary>
        /// <param name="report">진행 내용을 적을 목록</param>
        private static void ReportRampMismatch(List<string> report)
        {
            string[] guids = AssetDatabase.FindAssets("t:Material", new[] { "Assets/_Project" });
            int broken = 0;

            for (int i = 0; i < guids.Length; i++)
            {
                Material mat = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guids[i]));
                if (mat == null || !mat.IsKeywordEnabled("_TOON_RAMP")) continue;
                if (!mat.HasProperty("_ToonRampMap") || mat.GetTexture("_ToonRampMap") != null) continue;

                report.Add("! " + mat.name + " 은 램프 키워드가 켜져 있는데 램프가 없습니다. " +
                           "이 재질은 그림자를 받지 않습니다.");
                broken++;
            }

            if (broken == 0) report.Add("· 램프 키워드와 텍스처가 모두 짝이 맞습니다.");
        }
    }
}
