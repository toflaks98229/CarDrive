using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using CarDrive.Systems;

namespace CarDrive.EditorTools
{
    /// <summary>
    /// 맑고 푸른 하늘 사진을 스카이박스로 겁니다.
    ///
    /// 쓰는 에셋: Poly Haven 의 "Kloofendal 43d Clear (Pure Sky)" — <b>CC0</b>
    ///   https://polyhaven.com/a/kloofendal_43d_clear_puresky
    ///   촬영: Greg Zaal. Poly Haven 의 모든 에셋은 CC0 이라 표기 의무도 없지만 적어 둡니다.
    ///
    /// "Pure Sky" 판을 고른 이유가 있습니다. 보통 HDRI 는 아래쪽에 촬영지의 땅이 찍혀 있어
    /// 게임 지형과 겹칩니다. Pure Sky 는 <b>하늘만</b> 들어 있어 지평선 아래가 깔끔합니다.
    ///
    /// ── 사진 하늘의 대가 ──
    ///
    /// 이 하늘은 <b>한낮에 찍은 사진 한 장</b>입니다. 절차적 하늘과 달리 그림이 정해져 있어
    /// 시간이 흘러도 구름이 그 자리에 있고 별도 없습니다.
    /// 그래서 <see cref="SkyController"/> 가 노출과 색으로 밤을 만듭니다. 어두워지긴 해도
    /// <b>별은 나오지 않습니다.</b> 별이 필요하면 <see cref="Revert"/> 로 절차적 하늘
    /// (CarDrive/Sky)로 돌아가세요. 그쪽은 별과 노을을 계산으로 만듭니다.
    /// </summary>
    public static class ClearSkySetup
    {
        /// <summary>메인 씬 경로입니다.</summary>
        private const string ScenePath = "Assets/_Project/01.Scenes/SampleScene.unity";

        /// <summary>내려받아 둔 하늘 사진 경로입니다.</summary>
        private const string HdriPath = "Assets/_Project/04.Art/01.Images/Skybox/ClearBlueSky_Kloofendal.hdr";

        /// <summary>만들 하늘 재질 경로입니다.</summary>
        private const string SkyMatPath = "Assets/_Project/04.Art/01.Images/Skybox/ClearBlueSky.mat";

        /// <summary>
        /// 절차적 하늘 재질 경로입니다. 사진 하늘을 쓰기 전에 쓰던 것입니다.
        ///
        /// 사진 한 장이라 별이 없으므로, 별이 필요하면 이쪽으로 돌아갑니다.
        /// </summary>
        private const string ProceduralSkyMatPath = "Assets/_Project/04.Art/03.Shaders/Sky/CarDriveSky.mat";

        // --- Public Methods ---

        /// <summary>에디터 메뉴에서 실행합니다.</summary>
        [MenuItem("CarDrive/Look/맑은 하늘 적용")]
        public static void Apply()
        {
            List<string> report = new List<string>();

            Cubemap cube = ImportAsCubemap(report);
            Material sky = BuildSkyMaterial(cube, report);
            AssignSky(sky, report);

            AssetDatabase.SaveAssets();

            Debug.Log("ClearSkySetup:" + System.Environment.NewLine +
                      string.Join(System.Environment.NewLine, report));
        }

        /// <summary>
        /// 절차적 하늘(CarDrive/Sky)로 되돌립니다.
        ///
        /// 사진 하늘에는 별이 없습니다. 밤하늘에 별이 필요할 때 이쪽으로 돌아갑니다.
        /// 그림이 계산으로 만들어지므로 시각에 따라 노을과 별이 실제로 변합니다.
        /// </summary>
        [MenuItem("CarDrive/Look/하늘 되돌리기 (절차적 하늘)")]
        public static void Revert()
        {
            List<string> report = new List<string>();

            Material sky = AssetDatabase.LoadAssetAtPath<Material>(ProceduralSkyMatPath);
            if (sky == null)
            {
                Debug.LogWarning("ClearSkySetup: 절차적 하늘 재질을 찾지 못했습니다: " + ProceduralSkyMatPath);
                return;
            }

            AssignSky(sky, report);

            Debug.Log("ClearSkySetup: 절차적 하늘로 되돌렸습니다." + System.Environment.NewLine +
                      string.Join(System.Environment.NewLine, report));
        }

        /// <summary>
        /// 명령줄에서 씬을 열고 적용한 뒤 저장합니다.
        /// <c>Unity.exe -batchmode -quit -executeMethod CarDrive.EditorTools.ClearSkySetup.ApplyFromCommandLine</c>
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
        /// HDR 파노라마를 큐브맵으로 임포트합니다.
        ///
        /// 파노라마 그대로도 하늘로 쓸 수 있지만, 큐브맵으로 바꿔 두면 샘플이 싸고
        /// 지평선 근처에서 늘어나 보이는 일이 없습니다.
        /// </summary>
        /// <param name="report">진행 내용을 적을 목록</param>
        /// <returns>임포트된 큐브맵. 파일이 없으면 null입니다.</returns>
        private static Cubemap ImportAsCubemap(List<string> report)
        {
            TextureImporter importer = AssetImporter.GetAtPath(HdriPath) as TextureImporter;
            if (importer == null)
            {
                report.Add("! 하늘 사진을 찾지 못했습니다: " + HdriPath);
                return null;
            }

            importer.textureShape = TextureImporterShape.TextureCube;
            importer.generateCubemap = TextureImporterGenerateCubemap.AutoCubemap;
            importer.mipmapEnabled = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;

            // 화면이 어차피 픽셀화되므로 크게 잡을 이유가 없습니다.
            importer.maxTextureSize = 2048;

            importer.SaveAndReimport();

            Cubemap cube = AssetDatabase.LoadAssetAtPath<Cubemap>(HdriPath);
            if (cube == null)
            {
                report.Add("! 큐브맵으로 임포트하지 못했습니다.");
                return null;
            }

            report.Add("· 하늘 사진을 큐브맵으로 임포트했습니다. (" + cube.width + "px)");
            return cube;
        }

        /// <summary>
        /// 큐브맵을 담을 하늘 재질을 만듭니다.
        /// </summary>
        /// <param name="cube">쓸 큐브맵</param>
        /// <param name="report">진행 내용을 적을 목록</param>
        /// <returns>하늘 재질</returns>
        private static Material BuildSkyMaterial(Cubemap cube, List<string> report)
        {
            if (cube == null) return null;

            Shader shader = Shader.Find("Skybox/Cubemap");
            if (shader == null)
            {
                report.Add("! Skybox/Cubemap 셰이더를 찾지 못했습니다.");
                return null;
            }

            Material mat = AssetDatabase.LoadAssetAtPath<Material>(SkyMatPath);
            if (mat == null)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, SkyMatPath);
                report.Add("· 하늘 재질을 만들었습니다: " + SkyMatPath);
            }
            else
            {
                mat.shader = shader;
                report.Add("· 하늘 재질이 이미 있습니다.");
            }

            mat.SetTexture("_Tex", cube);

            // 노출과 색은 SkyController 가 매 프레임 덮어씁니다. 여기서는 한낮 값으로 둡니다.
            mat.SetFloat("_Exposure", 1.1f);
            mat.SetColor("_Tint", Color.white);

            EditorUtility.SetDirty(mat);
            return mat;
        }

        /// <summary>
        /// 씬의 스카이박스와 SkyController 를 이 재질로 바꿉니다.
        ///
        /// <b>두 곳을 함께 바꿔야 합니다.</b> RenderSettings 만 바꾸면 화면은 새 하늘인데
        /// SkyController 는 옛 재질을 붙들고 있어 밤이 되어도 하늘이 어두워지지 않습니다.
        /// </summary>
        /// <param name="sky">적용할 하늘 재질</param>
        /// <param name="report">진행 내용을 적을 목록</param>
        private static void AssignSky(Material sky, List<string> report)
        {
            if (sky == null) return;

            RenderSettings.skybox = sky;
            report.Add("· 씬 스카이박스를 바꿨습니다: " + sky.name);

            SkyController controller = Object.FindAnyObjectByType<SkyController>(FindObjectsInactive.Include);
            if (controller == null)
            {
                report.Add("! SkyController 를 찾지 못했습니다. 밤이 되어도 하늘이 밝게 남습니다.");
                return;
            }

            controller.skyMaterial = sky;
            EditorUtility.SetDirty(controller);

            report.Add("· SkyController 가 노출·색으로 밤을 만듭니다. (별은 사진에 없어 나오지 않습니다)");
        }
    }
}
