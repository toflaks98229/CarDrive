using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using CarDrive.Gameplay;
using CarDrive.Systems;

namespace CarDrive.EditorTools
{
    /// <summary>
    /// <b>얼마나 멀리까지 보이는지</b>에 관한 값들을 한 줄로 꿰어 맞춥니다.
    ///
    /// ── 왜 한곳에 모았는가 ──
    ///
    /// 시야 거리는 한 값이 아니라 <b>다섯 값이 맞물린 사슬</b>입니다.
    /// 하나만 따로 만지면 나머지가 조용히 무의미해집니다. 실제로 그런 일이 있었습니다.
    ///
    /// 안개를 340m 에 맞추고, 나무 페이드를 240~330m 에 걸고, 타일을 400m 에서 켜도록
    /// 맞춰 놓았는데 — <b>카메라가 100m 까지만 그리고 있었습니다.</b>
    /// 그 값은 코드가 아니라 씬의 Main Camera 에 직렬화되어 있어 검색에도 걸리지 않았고,
    /// 결과적으로 100m 밖을 겨냥한 조정이 전부 헛돌았습니다.
    ///
    /// 클립 평면은 <b>평면</b>이라 물체를 자릅니다. 그래서 100m 에 걸친 집은
    /// 사라지는 것이 아니라 먼 쪽부터 썰려 나갔습니다.
    ///
    /// ── 사슬의 순서 ──
    ///
    ///   안개 끝(340m)  ← 여기서 화면이 완전히 안개 색이 됩니다. 모든 계산의 기준입니다.
    ///     · 카메라 far clip = 안개 끝. 더 그려도 안개 색이라 보이지 않습니다.
    ///     · 나무 그리기 거리 = 안개 끝.
    ///     · 나무 디더 페이드 = 그보다 앞에서 끝나야 잘리는 것이 안 보입니다. (240 → 330m)
    ///     · 타일 즉시 켜기 = 안개 끝. 이 안쪽이 꺼져 있으면 빈 곳이 그대로 보입니다.
    ///     · 타일 활성 거리 = 안개 끝보다 멀리(400m). 그 차이가 나눠 켤 시간입니다.
    ///
    /// 안개 끝은 코드에 박지 않고 <b>씬에서 읽어</b> 씁니다. 안개를 옮기면 나머지가 따라옵니다.
    /// </summary>
    public static class ViewDistanceSetup
    {
        /// <summary>메인 씬 경로입니다.</summary>
        private const string ScenePath = "Assets/_Project/01.Scenes/SampleScene.unity";

        /// <summary>나무 프리팹이 있는 폴더입니다.</summary>
        private const string TreeFolder = "Assets/_Project/05.Prefabs/Prop/Tree";

        /// <summary>만들 머티리얼을 둘 폴더입니다.</summary>
        private const string MaterialFolder = "Assets/_Project/04.Art/00.Materials";

        /// <summary>쓸 셰이더 이름입니다.</summary>
        private const string ShaderName = "CarDrive/Toon Lit";

        /// <summary>안개를 읽지 못했을 때 쓸 기준 거리(m)입니다.</summary>
        private const float FallbackFogEnd = 340f;

        /// <summary>나무가 성글어지기 시작하는 지점입니다. 안개 끝에 대한 비율입니다.</summary>
        private const float FadeStartRatio = 0.70f;

        /// <summary>나무가 완전히 지워지는 지점입니다. 안개 끝에 대한 비율입니다.</summary>
        private const float FadeEndRatio = 0.97f;

        /// <summary>타일을 켜기 시작하는 거리입니다. 안개 끝에 대한 비율입니다.</summary>
        private const float ActiveDistanceRatio = 1.18f;

        /// <summary>
        /// 한 번 검사할 때 켤 타일 수입니다.
        ///
        /// 켜는 일은 콜라이더와 풀 조각을 다시 짜는 것이라 싸지 않습니다.
        /// 그래서 나눠서 켜는데, <b>1장이면 따라잡지 못합니다.</b>
        /// 한 줄(100m)에 4~8장이 필요한데 0.25초에 1장이면 초당 4장뿐입니다.
        /// </summary>
        private const int ActivationsPerCheck = 2;

        // --- Public Methods ---

        /// <summary>에디터 메뉴에서 실행합니다.</summary>
        [MenuItem("CarDrive/Look/시야 거리 배선")]
        public static void Apply()
        {
            List<string> report = new List<string>();

            float fogEnd = ResolveFogEnd(report);

            TuneCamera(fogEnd, report);
            ConvertTreeMaterials(fogEnd, report);
            TuneTerrains(fogEnd, report);
            TuneStreaming(fogEnd, report);
            WarnAboutWeather(report);
            ReportShaderErrors(report);

            AssetDatabase.SaveAssets();

            Debug.Log("ViewDistanceSetup:" + System.Environment.NewLine +
                      string.Join(System.Environment.NewLine, report));
        }

        /// <summary>
        /// 디더 페이드가 <b>실제로 픽셀을 지우는지</b> 재어 봅니다.
        ///
        /// 재질에 키워드가 켜져 있어도 셰이더가 그 분기를 돌지 않으면 화면은 그대로입니다.
        /// 설정만 봐서는 구분할 수 없으므로 <b>강제로 전부 지워 보고</b> 확인합니다.
        ///
        /// 같은 자리를 세 번 그립니다.
        ///   기준 — 페이드 끔
        ///   절반 — 남는 정도 0.5 로 강제
        ///   전부 — 남는 정도 0 으로 강제
        ///
        /// 디더가 돌면 "전부"에서 나무가 통째로 사라져 화면이 크게 달라집니다.
        /// 달라지지 않으면 <b>코드가 돌지 않는 것</b>입니다.
        /// </summary>
        [MenuItem("CarDrive/Look/시야 거리 검증")]
        public static void Verify()
        {
            List<string> report = new List<string>();

            Terrain terrain = PickTerrainWithTrees(out Vector3 treePos);
            if (terrain == null)
            {
                report.Add("! 나무가 심긴 터레인을 찾지 못했습니다.");
                Log(report);
                return;
            }

            List<Material> materials = TreeMaterials(report);
            if (materials.Count == 0)
            {
                Log(report);
                return;
            }

            // 재는 동안 그림자를 끕니다.
            //
            // 디더는 <b>그림자에는 걸지 않습니다.</b> 그림자 거리는 50~150m 인데 디더가
            // 시작되는 것은 240m 부터라 늘 1이기 때문입니다. 그런데 그러면 물체가 지워져도
            // 그림자는 남아, "안 지워졌다"고 잘못 세어집니다. 실제로 그렇게 오판했습니다.
            Light sun = FindSun();
            LightShadows savedShadows = sun != null ? sun.shadows : LightShadows.None;
            if (sun != null) sun.shadows = LightShadows.None;

            Terrain[] terrains = Object.FindObjectsByType<Terrain>(FindObjectsInactive.Include);

            // 나무가 화면을 얼마나 덮는지 모르면 "1.4% 가 달라졌다"는 말은 아무 뜻이 없습니다.
            // 그래서 <b>나무를 아예 끈 화면</b>을 먼저 찍어 분모로 씁니다.
            bool[] savedDraw = new bool[terrains.Length];
            for (int i = 0; i < terrains.Length; i++)
            {
                savedDraw[i] = terrains[i].drawTreesAndFoliage;
                terrains[i].enabled = true;
                terrains[i].drawTreesAndFoliage = true;
            }

            // 나무를 크게 담아야 셈이 흔들리지 않습니다.
            const float Distance = 18f;

            Camera cam = BuildCamera(treePos + new Vector3(Distance, 3f, 0f), treePos + Vector3.up * 3.5f);

            Vector2[] saved = new Vector2[materials.Count];
            for (int i = 0; i < materials.Count; i++)
            {
                saved[i] = new Vector2(materials[i].GetFloat("_FadeStart"), materials[i].GetFloat("_FadeEnd"));
            }

            try
            {
                // 1) 나무 없는 화면 — 분모를 구할 기준
                for (int i = 0; i < terrains.Length; i++) terrains[i].drawTreesAndFoliage = false;
                Color32[] noTrees = Shoot(cam, materials, 0f, 0f, dither: false);

                // 2) 나무 있고 디더 끔 — 여기서 나무 픽셀 집합이 나옵니다
                for (int i = 0; i < terrains.Length; i++) terrains[i].drawTreesAndFoliage = true;
                Color32[] full = Shoot(cam, materials, 0f, 0f, dither: false);

                bool[] treePixel = Differs(noTrees, full);
                int treeCount = Count(treePixel);

                report.Add("· 나무가 덮은 화면 비율: " +
                           (treeCount * 100f / noTrees.Length).ToString("F2") + "% (" + treeCount + "픽셀)");

                if (treeCount < 200)
                {
                    report.Add("! 나무가 화면에 거의 없어 셀 수 없습니다. 카메라 자리를 바꿔야 합니다.");
                    return;
                }

                // 3) 남는 정도를 강제로 걸고, <b>나무 픽셀 중 몇 개가 사라졌는지</b> 셉니다.
                float halfErased = ErasedRatio(cam, materials, treePixel, noTrees, Distance * 2f, refresh: true);
                float goneErased = ErasedRatio(cam, materials, treePixel, noTrees, Distance * 0.5f, refresh: true);

                report.Add("· 나무 픽셀 중 지워진 비율");
                report.Add("   남는 정도 0.5 → " + (halfErased * 100f).ToString("F1") + "%   (기대 50%)");
                report.Add("   남는 정도 0   → " + (goneErased * 100f).ToString("F1") + "%   (기대 100%)");

                if (goneErased < 0.9f)
                {
                    report.Add("! 전부 지우라고 했는데 " + ((1f - goneErased) * 100f).ToString("F0") +
                               "% 가 남았습니다. 디더가 제대로 걸리지 않습니다.");
                }
                else if (halfErased < 0.3f || halfErased > 0.7f)
                {
                    report.Add("! 전부 지우기는 되는데 중간 단계가 " + (halfErased * 100f).ToString("F0") +
                               "% 입니다. 점진적이지 않습니다.");
                }
                else
                {
                    report.Add("· 나무: 디더가 정상 동작합니다. 남는 정도에 비례해 지워집니다.");
                }

                VerifyMesh(report);
            }
            finally
            {
                for (int i = 0; i < materials.Count; i++)
                {
                    materials[i].SetFloat("_FadeStart", saved[i].x);
                    materials[i].SetFloat("_FadeEnd", saved[i].y);
                    materials[i].SetFloat("_UseDitherFade", 1f);
                    materials[i].EnableKeyword("_DITHER_FADE");
                    EditorUtility.SetDirty(materials[i]);
                }

                for (int i = 0; i < terrains.Length; i++) terrains[i].drawTreesAndFoliage = savedDraw[i];
                if (sun != null) sun.shadows = savedShadows;

                AssetDatabase.SaveAssets();
                Object.DestroyImmediate(cam.gameObject);
            }

            Log(report);
        }

        /// <summary>
        /// 명령줄에서 씬을 열고 검증합니다.
        /// <c>Unity.exe -batchmode -quit -executeMethod CarDrive.EditorTools.ViewDistanceSetup.VerifyFromCommandLine</c>
        /// </summary>
        public static void VerifyFromCommandLine()
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            Verify();
        }

        /// <summary>
        /// 명령줄에서 씬을 열고 적용한 뒤 저장합니다.
        /// <c>Unity.exe -batchmode -quit -executeMethod CarDrive.EditorTools.ViewDistanceSetup.ApplyFromCommandLine</c>
        /// </summary>
        public static void ApplyFromCommandLine()
        {
            UnityEngine.SceneManagement.Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            Apply();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
        }

        // --- Private Methods : 기준 ---

        /// <summary>
        /// 화면이 완전히 안개에 덮이는 거리를 씬에서 읽습니다.
        ///
        /// 이 값이 사슬 전체의 기준입니다. 코드에 박아 두면 안개를 옮겼을 때
        /// 나머지가 조용히 어긋나므로, <b>실제 설정을 읽어</b> 씁니다.
        /// </summary>
        /// <param name="report">진행 내용을 적을 목록</param>
        /// <returns>안개가 완전히 덮는 거리(m)</returns>
        private static float ResolveFogEnd(List<string> report)
        {
            if (!RenderSettings.fog)
            {
                report.Add("! 안개가 꺼져 있습니다. 세상 끝을 가려 줄 것이 없어 " +
                           FallbackFogEnd + "m 를 기준으로 잡습니다.");
                return FallbackFogEnd;
            }

            if (RenderSettings.fogMode == FogMode.Linear)
            {
                float end = RenderSettings.fogEndDistance;
                report.Add("· 기준: 선형 안개가 " + RenderSettings.fogStartDistance.ToString("F0") +
                           "m 에서 시작해 " + end.ToString("F0") + "m 에서 끝납니다.");
                return end;
            }

            // 지수 안개는 끝나는 거리가 따로 없습니다. 거의 덮이는 지점을 밀도에서 역산합니다.
            float density = Mathf.Max(0.0001f, RenderSettings.fogDensity);
            float reach = RenderSettings.fogMode == FogMode.ExponentialSquared
                ? Mathf.Sqrt(Mathf.Log(50f)) / density
                : Mathf.Log(50f) / density;

            report.Add("· 기준: " + RenderSettings.fogMode + " 안개(밀도 " + density.ToString("F4") +
                       ") 가 약 " + reach.ToString("F0") + "m 에서 화면을 덮습니다.");

            return reach;
        }

        // --- Private Methods : 적용 ---

        /// <summary>
        /// 월드를 그리는 카메라의 far clip 을 안개 끝에 맞춥니다.
        ///
        /// 더 멀리 그려도 안개 색만 나오므로 얻는 것이 없고, 짧으면 <b>물체가 잘립니다.</b>
        /// </summary>
        /// <remarks>
        /// 백미러·사이드미러 카메라도 함께 맞춥니다. 거울은 같은 월드를 다시 그리므로,
        /// 거울만 멀리 그리면 <b>보이지도 않는 것을 세 번 더 그립니다.</b>
        /// 거울에 비친 것도 같은 안개를 통과하므로 기준이 같아야 합니다.
        /// </remarks>
        /// <param name="fogEnd">안개가 덮는 거리</param>
        /// <param name="report">진행 내용을 적을 목록</param>
        private static void TuneCamera(float fogEnd, List<string> report)
        {
            Camera[] cameras = Object.FindObjectsByType<Camera>(FindObjectsInactive.Include);
            int touched = 0;

            for (int i = 0; i < cameras.Length; i++)
            {
                Camera cam = cameras[i];

                // UI만 그리는 카메라는 건드리지 않습니다.
                // 월드를 그리는 카메라는 Default 레이어를 포함합니다.
                if ((cam.cullingMask & 1) == 0) continue;

                float before = cam.farClipPlane;
                if (Mathf.Abs(before - fogEnd) < 0.5f)
                {
                    report.Add("· " + cam.name + " 의 far clip 은 이미 " + fogEnd.ToString("F0") + "m 입니다.");
                    touched++;
                    continue;
                }

                Undo.RecordObject(cam, "시야 거리 배선");
                cam.farClipPlane = fogEnd;
                EditorUtility.SetDirty(cam);

                report.Add("· " + cam.name + " far clip: " + before.ToString("F0") + "m → " +
                           fogEnd.ToString("F0") + "m");
                touched++;
            }

            if (touched == 0) report.Add("! 월드를 그리는 카메라를 찾지 못했습니다.");
        }

        /// <summary>
        /// 나무 프리팹마다 디더 페이드를 켠 머티리얼을 만들어 붙입니다.
        ///
        /// 원본 머티리얼은 <c>Imports</c> 안에 있어 손대지 않습니다.
        /// 바탕 텍스처와 컷오프 값만 옮겨 옵니다.
        /// </summary>
        /// <param name="fogEnd">안개가 덮는 거리</param>
        /// <param name="report">진행 내용을 적을 목록</param>
        private static void ConvertTreeMaterials(float fogEnd, List<string> report)
        {
            Shader shader = Shader.Find(ShaderName);
            if (shader == null)
            {
                report.Add("! " + ShaderName + " 셰이더를 찾지 못했습니다.");
                return;
            }

            float fadeStart = Mathf.Round(fogEnd * FadeStartRatio / 10f) * 10f;
            float fadeEnd = Mathf.Round(fogEnd * FadeEndRatio / 10f) * 10f;

            string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { TreeFolder });
            if (guids.Length == 0)
            {
                report.Add("! 나무 프리팹을 찾지 못했습니다: " + TreeFolder);
                return;
            }

            int converted = 0;
            int retuned = 0;

            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                GameObject contents = PrefabUtility.LoadPrefabContents(path);

                try
                {
                    MeshRenderer renderer = contents.GetComponentInChildren<MeshRenderer>(true);
                    if (renderer == null || renderer.sharedMaterial == null)
                    {
                        report.Add("! " + contents.name + " 에 렌더러가 없습니다.");
                        continue;
                    }

                    Material source = renderer.sharedMaterial;

                    // 이미 옮겨 둔 것이면 거리만 다시 맞춥니다.
                    // 원본 값을 다시 읽을 수 없으므로 머티리얼을 새로 만들지 않습니다.
                    if (source.shader == shader)
                    {
                        source.SetFloat("_FadeStart", fadeStart);
                        source.SetFloat("_FadeEnd", fadeEnd);
                        EditorUtility.SetDirty(source);
                        retuned++;
                        continue;
                    }

                    renderer.sharedMaterial = BuildMaterial(contents.name, source, shader, fadeStart, fadeEnd);

                    PrefabUtility.SaveAsPrefabAsset(contents, path);
                    converted++;
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(contents);
                }
            }

            // 터레인은 나무 프로토타입의 재질 상태를 캐시합니다.
            // 재질만 고치고 갱신하지 않으면 <b>이미 심긴 나무에는 반영되지 않습니다.</b>
            // 실측으로 확인했습니다 — 갱신 전 1.7%, 갱신 후 91.6% 가 지워졌습니다.
            if (retuned > 0 || converted > 0) RefreshTreePrototypes();

            report.Add("· 나무: 새로 옮긴 것 " + converted + "개, 거리만 다시 맞춘 것 " + retuned + "개");
            report.Add("  " + fadeStart.ToString("F0") + "m 부터 성글어져 " +
                       fadeEnd.ToString("F0") + "m 에서 완전히 지워집니다.");
        }

        /// <summary>
        /// 원본 머티리얼의 값을 물려받은 새 머티리얼을 만듭니다.
        /// </summary>
        /// <param name="name">나무 이름</param>
        /// <param name="source">원본 머티리얼</param>
        /// <param name="shader">쓸 셰이더</param>
        /// <param name="fadeStart">성글어지기 시작하는 거리</param>
        /// <param name="fadeEnd">완전히 지워지는 거리</param>
        /// <returns>만들어진 머티리얼</returns>
        private static Material BuildMaterial(string name, Material source, Shader shader,
                                              float fadeStart, float fadeEnd)
        {
            string path = MaterialFolder + "/" + name + "_Fade.mat";

            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, path);
            }
            else
            {
                mat.shader = shader;
            }

            if (source.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", source.GetTexture("_BaseMap"));
            if (source.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", source.GetColor("_BaseColor"));

            // 잎이 뚫려 있으므로 컷아웃을 켭니다. 끄면 잎이 사각형 판으로 보입니다.
            float cutoff = source.HasProperty("_Cutoff") ? source.GetFloat("_Cutoff") : 0.5f;

            mat.SetFloat("_AlphaClip", 1f);
            mat.SetFloat("_Cutoff", cutoff);
            mat.EnableKeyword("_ALPHATEST_ON");

            mat.SetFloat("_UseDitherFade", 1f);
            mat.SetFloat("_FadeStart", fadeStart);
            mat.SetFloat("_FadeEnd", fadeEnd);
            mat.EnableKeyword("_DITHER_FADE");

            // 컷아웃은 불투명 뒤쪽에서 그려야 겹치는 잎이 제대로 가려집니다.
            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest;

            EditorUtility.SetDirty(mat);
            return mat;
        }

        /// <summary>
        /// 터레인의 나무 그리기 거리를 안개 끝에 맞춥니다.
        /// </summary>
        /// <param name="fogEnd">안개가 덮는 거리</param>
        /// <param name="report">진행 내용을 적을 목록</param>
        private static void TuneTerrains(float fogEnd, List<string> report)
        {
            Terrain[] terrains = Object.FindObjectsByType<Terrain>(FindObjectsInactive.Include);

            for (int i = 0; i < terrains.Length; i++)
            {
                if (terrains[i] == null) continue;

                Undo.RecordObject(terrains[i], "시야 거리 배선");
                terrains[i].treeDistance = fogEnd;
                EditorUtility.SetDirty(terrains[i]);
            }

            report.Add("· 터레인 " + terrains.Length + "장의 나무 그리기 거리를 " +
                       fogEnd.ToString("F0") + "m 로 맞췄습니다.");

            // 풀은 훨씬 가까이서 끝납니다. 안개와 무관하게 비용 때문에 정해진 값입니다.
            report.Add("  (풀은 " + CarDriveWorldSettings.Instance.detailDistance +
                       "m 까지만 그립니다. 비용 때문이며 안개와 무관합니다.)");
        }

        /// <summary>
        /// 타일이 안개 밖에서 켜지도록 스트리밍 거리를 맞춥니다.
        /// </summary>
        /// <param name="fogEnd">안개가 덮는 거리</param>
        /// <param name="report">진행 내용을 적을 목록</param>
        private static void TuneStreaming(float fogEnd, List<string> report)
        {
            WorldStreamer world = Object.FindAnyObjectByType<WorldStreamer>(FindObjectsInactive.Include);
            if (world == null)
            {
                report.Add("! WorldStreamer 를 찾지 못했습니다. 타일이 눈앞에서 켜지는 것은 그대로입니다.");
                return;
            }

            // 10m 단위로 반올림합니다. 401 같은 값은 읽는 사람에게 근거가 있어 보이지만 없습니다.
            float active = Mathf.Round(fogEnd * ActiveDistanceRatio / 10f) * 10f;

            float beforeDistance = world.activeDistance;
            int beforeBudget = world.maxTileActivationsPerCheck;

            Undo.RecordObject(world, "시야 거리 배선");

            world.activeDistance = active;
            world.instantDistance = fogEnd;
            world.maxTileActivationsPerCheck = ActivationsPerCheck;

            EditorUtility.SetDirty(world);

            report.Add("· 타일 활성 거리: " + beforeDistance.ToString("F0") + "m → " + active.ToString("F0") + "m");
            report.Add("· 타일 즉시 켜기: " + fogEnd.ToString("F0") + "m — 이 안쪽은 예산을 무시합니다.");
            report.Add("· 한 번에 켜는 수: " + beforeBudget + " → " + ActivationsPerCheck + "장");
        }

        /// <summary>
        /// 날씨가 시야 거리를 곱으로 줄이도록 켜져 있으면 알려 줍니다.
        ///
        /// 켜져 있으면 far clip 이 <c>기준 × 배율</c> 로 줄어, 여기서 맞춘 사슬이
        /// 날씨에 따라 조용히 어긋납니다. 끄고 쓰거나, 하한을 알고 써야 합니다.
        /// </summary>
        /// <param name="report">진행 내용을 적을 목록</param>
        private static void WarnAboutWeather(List<string> report)
        {
            WeatherRig rig = Object.FindAnyObjectByType<WeatherRig>(FindObjectsInactive.Include);
            if (rig == null || !rig.controlVisibility) return;

            report.Add("! WeatherRig 의 controlVisibility 가 켜져 있습니다.");
            report.Add("  날씨가 나쁘면 far clip 이 최대 " + (rig.minVisibilityFactor * 100f).ToString("F0") +
                       "% 까지 줄어듭니다. 그때는 물체가 다시 잘립니다.");
        }

        /// <summary>
        /// 셰이더가 실제로 컴파일되었는지 확인합니다.
        /// </summary>
        /// <param name="report">진행 내용을 적을 목록</param>
        private static void ReportShaderErrors(List<string> report)
        {
            Shader shader = Shader.Find(ShaderName);
            if (shader == null) return;

            int count = ShaderUtil.GetShaderMessageCount(shader);
            if (count == 0)
            {
                report.Add("· 셰이더 정상: " + ShaderName);
                return;
            }

            report.Add("! 셰이더 메시지 " + count + "건: " + ShaderName);

            // ShaderMessage 가 어느 네임스페이스에 있는지 버전마다 달라 var 를 씁니다.
            var messages = ShaderUtil.GetShaderMessages(shader);
            for (int m = 0; m < messages.Length && m < 10; m++)
            {
                report.Add("   [" + messages[m].severity + "] " + messages[m].message +
                           " (" + messages[m].file + ":" + messages[m].line + ")");
            }
        }

        // --- Private Methods : 검증 도우미 ---

        /// <summary>나무가 심긴 터레인 하나와 그 나무의 월드 위치를 고릅니다.</summary>
        /// <param name="treePos">고른 나무의 월드 위치</param>
        /// <returns>터레인. 없으면 null입니다.</returns>
        private static Terrain PickTerrainWithTrees(out Vector3 treePos)
        {
            treePos = Vector3.zero;

            Terrain[] terrains = Object.FindObjectsByType<Terrain>(FindObjectsInactive.Include);
            for (int i = 0; i < terrains.Length; i++)
            {
                TerrainData data = terrains[i].terrainData;
                if (data == null) continue;

                TreeInstance[] trees = data.treeInstances;

                // 타일 가운데쯤에 있는 것을 고릅니다. 가장자리는 이웃 타일이 꺼져 있을 수 있습니다.
                for (int t = 0; t < trees.Length; t++)
                {
                    Vector3 n = trees[t].position;
                    if (n.x < 0.35f || n.x > 0.65f || n.z < 0.35f || n.z > 0.65f) continue;

                    treePos = terrains[i].transform.position +
                              new Vector3(n.x * data.size.x, n.y * data.size.y, n.z * data.size.z);
                    return terrains[i];
                }
            }

            return null;
        }

        /// <summary>터레인에 심긴 나무들이 쓰는 재질을 모읍니다.</summary>
        /// <param name="report">진행 내용을 적을 목록</param>
        /// <returns>재질 목록</returns>
        private static List<Material> TreeMaterials(List<string> report)
        {
            List<Material> found = new List<Material>();
            Terrain[] terrains = Object.FindObjectsByType<Terrain>(FindObjectsInactive.Include);

            for (int i = 0; i < terrains.Length; i++)
            {
                TerrainData data = terrains[i].terrainData;
                if (data == null) continue;

                TreePrototype[] protos = data.treePrototypes;
                for (int p = 0; p < protos.Length; p++)
                {
                    if (protos[p] == null || protos[p].prefab == null) continue;

                    Renderer renderer = protos[p].prefab.GetComponentInChildren<Renderer>(true);
                    if (renderer == null || renderer.sharedMaterial == null) continue;

                    if (!found.Contains(renderer.sharedMaterial)) found.Add(renderer.sharedMaterial);
                }
            }

            if (found.Count == 0) report.Add("! 나무 재질을 찾지 못했습니다.");
            else report.Add("· 나무 재질 " + found.Count + "개를 찾았습니다.");

            return found;
        }

        /// <summary>재는 데 쓸 카메라를 만듭니다.</summary>
        /// <param name="eye">카메라 위치</param>
        /// <param name="look">바라볼 자리</param>
        /// <returns>만들어진 카메라</returns>
        private static Camera BuildCamera(Vector3 eye, Vector3 look)
        {
            GameObject go = new GameObject("ViewDistanceProbe");
            go.transform.position = eye;
            go.transform.LookAt(look);

            Camera cam = go.AddComponent<Camera>();
            cam.fieldOfView = 50f;
            cam.nearClipPlane = 0.3f;
            cam.farClipPlane = 400f;

            return cam;
        }

        /// <summary>
        /// 페이드 값을 강제로 걸고 한 장 그립니다.
        /// </summary>
        /// <param name="cam">쓸 카메라</param>
        /// <param name="materials">건드릴 나무 재질들</param>
        /// <param name="fadeStart">지워지기 시작하는 거리</param>
        /// <param name="fadeEnd">완전히 지워지는 거리</param>
        /// <param name="dither">디더를 켤지</param>
        /// <returns>찍은 픽셀</returns>
        private static Color32[] Shoot(Camera cam, List<Material> materials,
                                       float fadeStart, float fadeEnd, bool dither,
                                       bool refreshPrototypes = false)
        {
            const int Size = 256;

            for (int i = 0; i < materials.Count; i++)
            {
                Material mat = materials[i];
                mat.SetFloat("_UseDitherFade", dither ? 1f : 0f);
                mat.SetFloat("_FadeStart", fadeStart);
                mat.SetFloat("_FadeEnd", fadeEnd);

                if (dither) mat.EnableKeyword("_DITHER_FADE");
                else mat.DisableKeyword("_DITHER_FADE");
            }

            // 터레인은 나무 재질을 프로토타입에 물려 둔 채 캐시합니다.
            // 재질만 바꾸고 갱신하지 않으면 화면에는 옛 상태가 그대로 나옵니다.
            if (refreshPrototypes)
            {
                Terrain[] all = Object.FindObjectsByType<Terrain>(FindObjectsInactive.Include);
                for (int i = 0; i < all.Length; i++)
                {
                    if (all[i].terrainData != null) all[i].terrainData.RefreshPrototypes();
                }
            }

            RenderTexture rt = new RenderTexture(Size, Size, 24, RenderTextureFormat.ARGB32);
            cam.targetTexture = rt;
            cam.Render();

            RenderTexture prev = RenderTexture.active;
            RenderTexture.active = rt;

            Texture2D shot = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
            shot.ReadPixels(new Rect(0, 0, Size, Size), 0, 0);
            shot.Apply();

            RenderTexture.active = prev;
            cam.targetTexture = null;

            Color32[] px = shot.GetPixels32();

            Object.DestroyImmediate(shot);
            rt.Release();
            Object.DestroyImmediate(rt);

            return px;
        }

        /// <summary>두 화면에서 눈에 띄게 다른 픽셀에 표시합니다.</summary>
        /// <param name="a">기준 화면</param>
        /// <param name="b">비교할 화면</param>
        /// <returns>픽셀마다 달라졌는지</returns>
        private static bool[] Differs(Color32[] a, Color32[] b)
        {
            bool[] mask = new bool[a.Length];

            for (int i = 0; i < a.Length; i++)
            {
                int diff = Mathf.Abs(a[i].r - b[i].r) + Mathf.Abs(a[i].g - b[i].g) + Mathf.Abs(a[i].b - b[i].b);
                mask[i] = diff > 24;
            }

            return mask;
        }

        /// <summary>표시된 픽셀 수를 셉니다.</summary>
        /// <param name="mask">표시</param>
        /// <returns>개수</returns>
        private static int Count(bool[] mask)
        {
            int n = 0;
            for (int i = 0; i < mask.Length; i++)
            {
                if (mask[i]) n++;
            }
            return n;
        }

        /// <summary>
        /// 페이드를 걸고 그린 뒤, <b>나무 픽셀 중 몇 개가 배경으로 돌아갔는지</b> 셉니다.
        ///
        /// 화면 전체 대비가 아니라 나무 픽셀 대비로 재야 뜻이 있는 숫자가 나옵니다.
        /// </summary>
        /// <param name="cam">쓸 카메라</param>
        /// <param name="materials">나무 재질들</param>
        /// <param name="treePixel">나무가 그려졌던 자리</param>
        /// <param name="noTrees">나무 없는 화면</param>
        /// <param name="fadeEnd">완전히 지워지는 거리</param>
        /// <returns>지워진 비율(0~1)</returns>
        private static float ErasedRatio(Camera cam, List<Material> materials, bool[] treePixel,
                                         Color32[] noTrees, float fadeEnd, bool refresh = false)
        {
            Color32[] shot = Shoot(cam, materials, 0f, fadeEnd, dither: true, refreshPrototypes: refresh);

            int total = 0;
            int erased = 0;

            for (int i = 0; i < treePixel.Length; i++)
            {
                if (!treePixel[i]) continue;
                total++;

                int diff = Mathf.Abs(noTrees[i].r - shot[i].r) +
                           Mathf.Abs(noTrees[i].g - shot[i].g) +
                           Mathf.Abs(noTrees[i].b - shot[i].b);

                // 배경과 같아졌으면 그 자리의 나무가 지워진 것입니다.
                if (diff <= 24) erased++;
            }

            return total > 0 ? erased / (float)total : 0f;
        }

        /// <summary>검증 결과를 한 번에 찍습니다.</summary>
        /// <param name="report">적어 둔 줄들</param>
        private static void Log(List<string> report)
        {
            Debug.Log("ViewDistanceSetup(검증):" + System.Environment.NewLine +
                      string.Join(System.Environment.NewLine, report));
        }

        /// <summary>
        /// 일반 게임오브젝트(바위)에서도 디더가 도는지 봅니다.
        ///
        /// 나무는 터레인이 그리고 바위는 렌더러가 그립니다. <b>둘 다 같은 셰이더</b>를 쓰므로,
        /// 바위에서 되고 나무에서 안 되면 문제는 셰이더가 아니라 터레인 트리 경로입니다.
        /// 둘 다 안 되면 셰이더 쪽입니다. 원인을 반으로 가르기 위한 것입니다.
        /// </summary>
        /// <param name="report">진행 내용을 적을 목록</param>
        private static void VerifyMesh(List<string> report)
        {
            GameObject root = GameObject.Find("WorldScatter");
            if (root == null || root.transform.childCount == 0)
            {
                report.Add("! 바위를 찾지 못해 비교하지 못했습니다.");
                return;
            }

            MeshRenderer rock = null;
            for (int i = 0; i < root.transform.childCount && rock == null; i++)
            {
                rock = root.transform.GetChild(i).GetComponent<MeshRenderer>();
            }

            if (rock == null || rock.sharedMaterial == null)
            {
                report.Add("! 바위 렌더러를 찾지 못했습니다.");
                return;
            }

            Material mat = rock.sharedMaterial;
            Vector3 center = rock.bounds.center;
            float radius = Mathf.Max(1.5f, rock.bounds.extents.magnitude);
            float distance = radius * 4f;

            Camera cam = BuildCamera(center + new Vector3(distance, distance * 0.3f, 0f), center);

            float savedStart = mat.HasProperty("_FadeStart") ? mat.GetFloat("_FadeStart") : 0f;
            float savedEnd = mat.HasProperty("_FadeEnd") ? mat.GetFloat("_FadeEnd") : 0f;
            bool savedOn = mat.IsKeywordEnabled("_DITHER_FADE");

            List<Material> one = new List<Material> { mat };

            try
            {
                bool wasActive = rock.gameObject.activeSelf;

                rock.gameObject.SetActive(false);
                Color32[] noRock = Shoot(cam, one, 0f, 0f, dither: false);

                rock.gameObject.SetActive(true);
                Color32[] full = Shoot(cam, one, 0f, 0f, dither: false);

                bool[] rockPixel = Differs(noRock, full);
                int count = Count(rockPixel);

                if (count < 200)
                {
                    report.Add("! 바위가 화면에 거의 없어 비교하지 못했습니다.");
                    return;
                }

                float gone = ErasedRatio(cam, one, rockPixel, noRock, distance * 0.5f);

                report.Add("· 바위(일반 렌더러) 같은 셰이더로 비교 — 덮은 픽셀 " + count + "개");
                report.Add("   남는 정도 0 → " + (gone * 100f).ToString("F1") + "% 지워짐   (기대 100%)");

                report.Add(gone >= 0.9f
                    ? "  → 셰이더의 디더 자체는 정상입니다."
                    : "  → 바위에서도 지워지지 않습니다. 셰이더의 디더 자체가 동작하지 않습니다. " +
                      "나무가 된다면 원인은 셰이더가 아니라 재질 설정 쪽입니다.");

                rock.gameObject.SetActive(wasActive);
            }
            finally
            {
                mat.SetFloat("_FadeStart", savedStart);
                mat.SetFloat("_FadeEnd", savedEnd);
                mat.SetFloat("_UseDitherFade", savedOn ? 1f : 0f);

                if (savedOn) mat.EnableKeyword("_DITHER_FADE");
                else mat.DisableKeyword("_DITHER_FADE");

                EditorUtility.SetDirty(mat);
                Object.DestroyImmediate(cam.gameObject);
            }
        }

        /// <summary>씬의 방향광을 찾습니다.</summary>
        /// <returns>태양광. 없으면 null입니다.</returns>
        private static Light FindSun()
        {
            Light[] lights = Object.FindObjectsByType<Light>(FindObjectsInactive.Include);
            for (int i = 0; i < lights.Length; i++)
            {
                if (lights[i].type == LightType.Directional) return lights[i];
            }
            return null;
        }

        /// <summary>
        /// 터레인이 들고 있는 나무 프로토타입 캐시를 갱신합니다.
        ///
        /// 재질의 값이나 키워드를 바꿔도, 갱신하지 않으면 이미 심긴 나무는 옛 상태로 그려집니다.
        /// </summary>
        private static void RefreshTreePrototypes()
        {
            Terrain[] terrains = Object.FindObjectsByType<Terrain>(FindObjectsInactive.Include);

            for (int i = 0; i < terrains.Length; i++)
            {
                if (terrains[i].terrainData == null) continue;

                terrains[i].terrainData.RefreshPrototypes();
                EditorUtility.SetDirty(terrains[i].terrainData);
            }
        }
    }
}
