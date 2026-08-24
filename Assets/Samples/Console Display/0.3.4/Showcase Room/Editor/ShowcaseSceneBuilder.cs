using ConsoleDisplay.Showcase;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ConsoleDisplay.ShowcaseEditor
{
    /// <summary>
    /// 데모 방을 그 자리에서 지어 줍니다.
    ///
    /// <b>왜 완성된 씬 파일이 아닌가.</b> 씬 파일에 머티리얼을 넣어 두면 구매자의 프로젝트가
    /// 다른 렌더 파이프라인일 때 <b>전부 분홍색으로 뜹니다.</b> URP 프로젝트에 Built-in용 Standard
    /// 머티리얼이 들어오면 그렇게 됩니다. 에셋을 처음 열었을 때 분홍색 방이 나오면
    /// 그것만으로 환불 사유가 됩니다.
    ///
    /// 지금 프로젝트의 파이프라인을 보고 셰이더를 골라 지으면 그 문제가 없습니다.
    /// 외부 아트 에셋도 필요 없습니다. 전부 기본 도형입니다.
    /// </summary>
    internal static class ShowcaseSceneBuilder
    {
        // --- Private Members ---

        private const float RoomHalfWidth = 11f;
        private const float RoomHalfDepth = 8f;
        private const float RoomHeight = 4.5f;

        /// <summary>버튼에 배정할 템플릿과 색입니다. 묶음마다 색을 맞춰 놓았습니다.</summary>
        private static readonly (ShowcaseKiosk.TemplateKind Kind, Color Tint)[] Layout =
        {
            // A. 개발 도구 — 파랑 계열
            (ShowcaseKiosk.TemplateKind.TelemetryDashboard, new Color(0.20f, 0.70f, 0.95f)),
            (ShowcaseKiosk.TemplateKind.LogStream, new Color(0.20f, 0.55f, 0.95f)),
            (ShowcaseKiosk.TemplateKind.SparklineGraph, new Color(0.35f, 0.45f, 0.95f)),

            // B. 게임 연출 — 붉은 계열
            (ShowcaseKiosk.TemplateKind.BootSequence, new Color(0.95f, 0.55f, 0.25f)),
            (ShowcaseKiosk.TemplateKind.TerminalDialogue, new Color(0.95f, 0.35f, 0.35f)),
            (ShowcaseKiosk.TemplateKind.AlertScreen, new Color(0.95f, 0.20f, 0.20f)),

            // C. 게임 정보 — 초록 계열
            (ShowcaseKiosk.TemplateKind.AsciiRadar, new Color(0.25f, 0.90f, 0.45f)),
            (ShowcaseKiosk.TemplateKind.StatusPanel, new Color(0.60f, 0.90f, 0.25f)),

            // D. 눈요기 — 보라 계열
            (ShowcaseKiosk.TemplateKind.MatrixRain, new Color(0.55f, 0.35f, 0.95f)),
            (ShowcaseKiosk.TemplateKind.AsciiAnimation, new Color(0.85f, 0.40f, 0.90f)),
        };

        // --- Menu ---

        [MenuItem("Window/Console Display/데모 방 만들기", false, 1)]
        private static void BuildScene()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            Shader shader = PickShader();
            if (shader == null)
            {
                EditorUtility.DisplayDialog(
                    "Console Display",
                    "쓸 만한 셰이더를 찾지 못했습니다.\n프로젝트의 렌더 파이프라인 설정을 확인해 주세요.",
                    "확인");
                return;
            }

            BuildLighting();
            BuildRoom(shader);
            BuildKiosks(shader);
            GameObject player = BuildPlayer();
            BuildInterface(player);

            EditorSceneManager.MarkSceneDirty(scene);

            // 저장하지 않으면 다른 씬을 열거나 에디터를 닫는 순간 사라집니다.
            // 다시 부르면 다시 지어지는 씬이라, 물어보지 않고 덮어씁니다.
            string scenePath = SaveScene(scene);

            Selection.activeGameObject = player;

            if (string.IsNullOrEmpty(scenePath))
            {
                Debug.LogWarning("[Console Display] 데모 방을 지었지만 파일로 저장하지 못했습니다. " +
                                 "Ctrl+S로 직접 저장해 주세요.");
                return;
            }

            var asset = AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath);
            if (asset != null)
            {
                EditorGUIUtility.PingObject(asset);
            }

            Debug.Log("[Console Display] 데모 방을 지었습니다.\n" +
                      "씬 파일: " + scenePath + "\n" +
                      "셰이더: " + shader.name + "\n" +
                      "플레이를 누르고 버튼 앞으로 가서 E를 누르면 두 번째 화면이 열립니다.", asset);
        }

        // --- Private Methods : 저장 ---

        /// <summary>
        /// 지은 방을 샘플 폴더 옆에 저장하고 그 경로를 돌려줍니다.
        ///
        /// 저장 위치를 샘플 폴더로 잡는 이유는, 데모와 관련된 것이 한군데 모여 있어야
        /// 나중에 <b>통째로 지우기 쉽기</b> 때문입니다. 데모는 언젠가 지우는 물건입니다.
        /// </summary>
        private static string SaveScene(Scene scene)
        {
            string folder = ResolveSampleFolder();
            string path = folder + "/Console Display Showcase.unity";

            try
            {
                return EditorSceneManager.SaveScene(scene, path) ? path : null;
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[Console Display] 씬을 저장하지 못했습니다 - " + e.Message);
                return null;
            }
        }

        /// <summary>
        /// 이 스크립트가 놓인 샘플 폴더를 찾습니다.
        ///
        /// 경로를 상수로 박아 두면 샘플 버전이 올라가거나 사용자가 폴더를 옮겼을 때 틀립니다.
        /// 자기 자신이 어디 있는지를 물어보는 편이 항상 맞습니다.
        /// </summary>
        private static string ResolveSampleFolder()
        {
            string[] guids = AssetDatabase.FindAssets("ShowcaseSceneBuilder t:MonoScript");

            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]).Replace('\\', '/');
                if (!path.EndsWith("/ShowcaseSceneBuilder.cs", System.StringComparison.Ordinal))
                {
                    continue;
                }

                // .../Showcase Room/Editor/ShowcaseSceneBuilder.cs 에서 두 단계 올라갑니다.
                string editorFolder = ParentOf(path);
                string sampleFolder = ParentOf(editorFolder);

                if (!string.IsNullOrEmpty(sampleFolder) && AssetDatabase.IsValidFolder(sampleFolder))
                {
                    return sampleFolder;
                }
            }

            return "Assets";
        }

        private static string ParentOf(string path)
        {
            int cut = path.LastIndexOf('/');
            return cut <= 0 ? string.Empty : path.Substring(0, cut);
        }

        // --- Private Methods : 지형 ---

        /// <summary>
        /// 지금 프로젝트에 맞는 셰이더를 고릅니다.
        /// 파이프라인이 설정되어 있으면 그쪽 것을 먼저 찾고, 없으면 Built-in으로 돌아갑니다.
        /// </summary>
        private static Shader PickShader()
        {
            bool scriptable = GraphicsSettings.defaultRenderPipeline != null ||
                              QualitySettings.renderPipeline != null;

            if (scriptable)
            {
                Shader urp = Shader.Find("Universal Render Pipeline/Lit");
                if (urp != null)
                {
                    return urp;
                }

                Shader hdrp = Shader.Find("HDRP/Lit");
                if (hdrp != null)
                {
                    return hdrp;
                }
            }

            return Shader.Find("Standard") ?? Shader.Find("Diffuse");
        }

        private static void BuildLighting()
        {
            var sun = new GameObject("Directional Light");
            Light light = sun.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.1f;
            light.color = new Color(1f, 0.97f, 0.92f);
            sun.transform.rotation = Quaternion.Euler(48f, 152f, 0f);

            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.16f, 0.17f, 0.22f);
            RenderSettings.ambientEquatorColor = new Color(0.11f, 0.11f, 0.14f);
            RenderSettings.ambientGroundColor = new Color(0.05f, 0.05f, 0.06f);
        }

        /// <summary>바닥·천장·벽 네 장을 세웁니다. 전부 기본 큐브입니다.</summary>
        private static void BuildRoom(Shader shader)
        {
            var room = new GameObject("Room");

            Material floor = MakeMaterial(shader, new Color(0.19f, 0.20f, 0.23f), "Showcase Floor");
            Material wall = MakeMaterial(shader, new Color(0.13f, 0.14f, 0.17f), "Showcase Wall");

            Slab(room, "Floor", new Vector3(0f, -0.5f, 0f),
                new Vector3(RoomHalfWidth * 2f, 1f, RoomHalfDepth * 2f), floor);

            Slab(room, "Ceiling", new Vector3(0f, RoomHeight + 0.5f, 0f),
                new Vector3(RoomHalfWidth * 2f, 1f, RoomHalfDepth * 2f), wall);

            Slab(room, "Wall North", new Vector3(0f, RoomHeight * 0.5f, RoomHalfDepth),
                new Vector3(RoomHalfWidth * 2f, RoomHeight, 1f), wall);

            Slab(room, "Wall South", new Vector3(0f, RoomHeight * 0.5f, -RoomHalfDepth),
                new Vector3(RoomHalfWidth * 2f, RoomHeight, 1f), wall);

            Slab(room, "Wall East", new Vector3(RoomHalfWidth, RoomHeight * 0.5f, 0f),
                new Vector3(1f, RoomHeight, RoomHalfDepth * 2f), wall);

            Slab(room, "Wall West", new Vector3(-RoomHalfWidth, RoomHeight * 0.5f, 0f),
                new Vector3(1f, RoomHeight, RoomHalfDepth * 2f), wall);
        }

        /// <summary>버튼 열 개를 두 줄로 세웁니다.</summary>
        private static void BuildKiosks(Shader shader)
        {
            var group = new GameObject("Kiosks");

            Material stand = MakeMaterial(shader, new Color(0.10f, 0.11f, 0.13f), "Showcase Stand");

            int perRow = Mathf.CeilToInt(Layout.Length / 2f);
            float spacing = (RoomHalfWidth * 2f - 4f) / Mathf.Max(1, perRow - 1);

            for (int i = 0; i < Layout.Length; i++)
            {
                int row = i / perRow;
                int column = i % perRow;

                float x = -RoomHalfWidth + 2f + (column * spacing);
                float z = row == 0 ? RoomHalfDepth - 1.6f : -RoomHalfDepth + 1.6f;
                float facing = row == 0 ? 180f : 0f;

                BuildKiosk(group, Layout[i].Kind, Layout[i].Tint, new Vector3(x, 0f, z), facing, shader, stand);
            }
        }

        private static void BuildKiosk(GameObject parent, ShowcaseKiosk.TemplateKind kind, Color tint,
            Vector3 position, float facing, Shader shader, Material standMaterial)
        {
            var kiosk = new GameObject("Kiosk - " + kind);
            kiosk.transform.SetParent(parent.transform);
            kiosk.transform.position = position;
            kiosk.transform.rotation = Quaternion.Euler(0f, facing, 0f);

            GameObject stand = GameObject.CreatePrimitive(PrimitiveType.Cube);
            stand.name = "Stand";
            stand.transform.SetParent(kiosk.transform, false);
            stand.transform.localPosition = new Vector3(0f, 0.5f, 0f);
            stand.transform.localScale = new Vector3(0.9f, 1f, 0.6f);
            stand.GetComponent<Renderer>().sharedMaterial = standMaterial;

            GameObject screen = GameObject.CreatePrimitive(PrimitiveType.Cube);
            screen.name = "Screen";
            screen.transform.SetParent(kiosk.transform, false);
            screen.transform.localPosition = new Vector3(0f, 1.15f, 0.05f);
            screen.transform.localRotation = Quaternion.Euler(-25f, 0f, 0f);
            screen.transform.localScale = new Vector3(0.85f, 0.55f, 0.08f);

            // 버튼마다 재질을 따로 줍니다. 공유하면 하나가 밝아질 때 전부 밝아집니다.
            Material screenMaterial = MakeMaterial(shader, tint, "Showcase Screen " + kind);
            screen.GetComponent<Renderer>().sharedMaterial = screenMaterial;

            // 부딪히면 걷다가 걸립니다. 화면 부분의 충돌체는 뺍니다.
            Object.DestroyImmediate(screen.GetComponent<Collider>());

            ShowcaseKiosk behaviour = kiosk.AddComponent<ShowcaseKiosk>();
            behaviour.Configure(kind, screen.GetComponent<Renderer>(), tint);
        }

        private static GameObject BuildPlayer()
        {
            var player = new GameObject("Player");
            player.transform.position = new Vector3(0f, 1.1f, 0f);

            CharacterController controller = player.AddComponent<CharacterController>();
            controller.height = 1.8f;
            controller.radius = 0.35f;
            controller.center = new Vector3(0f, -0.1f, 0f);

            var eye = new GameObject("View");
            eye.transform.SetParent(player.transform, false);
            eye.transform.localPosition = new Vector3(0f, 0.65f, 0f);

            Camera camera = eye.AddComponent<Camera>();
            camera.tag = "MainCamera";
            camera.nearClipPlane = 0.05f;
            camera.backgroundColor = new Color(0.05f, 0.05f, 0.07f);
            eye.AddComponent<AudioListener>();

            player.AddComponent<ShowcasePlayer>();
            return player;
        }

        /// <summary>게임 화면 쪽 안내입니다. 두 번째 화면은 템플릿이 통째로 씁니다.</summary>
        private static void BuildInterface(GameObject player)
        {
            var canvasObject = new GameObject("Showcase UI");
            Canvas canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);

            Font font = GetBuiltinFont();

            Text prompt = MakeLabel(canvasObject.transform, font, "Prompt", 34,
                new Vector2(0.5f, 0.5f), new Vector2(0f, -180f), new Vector2(1400f, 120f), TextAnchor.MiddleCenter);

            Text help = MakeLabel(canvasObject.transform, font, "Help", 24,
                new Vector2(0.5f, 0f), new Vector2(0f, 46f), new Vector2(1400f, 40f), TextAnchor.MiddleCenter);
            help.color = new Color(1f, 1f, 1f, 0.55f);

            ShowcaseDirector director = player.AddComponent<ShowcaseDirector>();

            // 인스펙터에 노출된 비공개 필드라 직렬화 창구로 넣어 줍니다.
            var so = new SerializedObject(director);
            so.FindProperty("prompt").objectReferenceValue = prompt;
            so.FindProperty("help").objectReferenceValue = help;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // --- Private Methods : 부품 ---

        private static void Slab(GameObject parent, string name, Vector3 position, Vector3 scale, Material material)
        {
            GameObject slab = GameObject.CreatePrimitive(PrimitiveType.Cube);
            slab.name = name;
            slab.transform.SetParent(parent.transform);
            slab.transform.position = position;
            slab.transform.localScale = scale;
            slab.GetComponent<Renderer>().sharedMaterial = material;
        }

        /// <summary>
        /// 머티리얼을 하나 만듭니다. 파이프라인마다 색 속성 이름이 달라서 둘 다 시도합니다.
        /// </summary>
        private static Material MakeMaterial(Shader shader, Color color, string name)
        {
            var material = new Material(shader) { name = name };

            if (material.HasProperty(BaseColor))
            {
                material.SetColor(BaseColor, color);
            }

            if (material.HasProperty(LegacyColor))
            {
                material.SetColor(LegacyColor, color);
            }

            return material;
        }

        /// <summary>
        /// 유니티에 들어 있는 기본 글꼴을 가져옵니다.
        /// 버전마다 이름이 달라서 순서대로 시도합니다. 못 찾으면 글자가 아예 안 보입니다.
        /// </summary>
        private static Font GetBuiltinFont()
        {
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font == null)
            {
                font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            }

            return font;
        }

        private static Text MakeLabel(Transform parent, Font font, string name, int size,
            Vector2 anchor, Vector2 offset, Vector2 size2d, TextAnchor alignment)
        {
            var labelObject = new GameObject(name);
            labelObject.transform.SetParent(parent, false);

            Text text = labelObject.AddComponent<Text>();
            text.font = font;
            text.fontSize = size;
            text.alignment = alignment;
            text.color = Color.white;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;

            RectTransform rect = text.rectTransform;
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = offset;
            rect.sizeDelta = size2d;

            return text;
        }

        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        private static readonly int LegacyColor = Shader.PropertyToID("_Color");
    }
}
