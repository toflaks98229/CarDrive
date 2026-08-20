using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using CarDrive.Systems;

namespace CarDrive.EditorTools
{
    /// <summary>
    /// 흩어져 있던 에디터 도구를 <b>한 창에서 순서대로</b> 쓰기 위한 패널입니다.
    ///
    /// <b>왜 만들었는가.</b> 도구가 스물여덟 개인데 <c>CarDrive</c> 메뉴 아래 네 갈래로
    /// 흩어져 있었습니다. 메뉴는 알파벳순이라 <b>무엇을 먼저 눌러야 하는지가 드러나지 않고</b>,
    /// 이름만 보고는 무엇이 바뀌는지도 알 수 없었습니다.
    /// 예전 월드 창은 그중 두 개만 노출하고 있었습니다.
    ///
    /// 여기서는 세 가지를 고칩니다.
    ///  1. <b>작업 순서대로</b> 묶습니다. 순서가 있는 묶음에는 번호가 붙습니다.
    ///  2. 버튼마다 <b>무엇이 바뀌는지</b> 한 줄로 적습니다.
    ///  3. 되돌리기 어려운 것은 <b>누르기 전에 한 번 더 묻습니다.</b>
    ///
    /// 도구를 실행할 때는 클래스를 직접 부르지 않고 <b>메뉴 경로</b>를 씁니다.
    /// 그래서 이 창은 스물여덟 개 클래스를 몰라도 되고, 도구가 늘어도
    /// <see cref="CarDriveToolCatalog"/> 에 한 줄 추가하면 끝입니다.
    ///
    /// 기존 메뉴 항목은 그대로 두었습니다. 익숙한 쪽으로 쓰셔도 됩니다.
    /// </summary>
    public class CarDriveControlPanel : EditorWindow
    {
        // --- Constants ---

        /// <summary>월드 설정 에셋이 놓이는 폴더입니다.</summary>
        private const string SettingsFolder = "Assets/_Project/03.DataAssets/Resources";

        /// <summary>월드 설정 에셋의 경로입니다.</summary>
        private const string SettingsPath = SettingsFolder + "/" + CarDriveWorldSettings.ResourceName + ".asset";

        /// <summary>접힘 상태를 기억해 두는 키의 앞머리입니다.</summary>
        private const string FoldoutKeyPrefix = "CarDrive.Panel.Foldout.";

        /// <summary>버튼 높이입니다.</summary>
        private const float ButtonHeight = 24f;

        // --- Private Member Variables ---

        /// <summary>그릴 도구 묶음입니다.</summary>
        private List<CarDriveToolGroup> groups;

        /// <summary>월드 설정 에셋입니다. 없을 수도 있습니다.</summary>
        private CarDriveWorldSettings settings;

        /// <summary>설정을 인스펙터처럼 그리기 위한 래퍼입니다.</summary>
        private SerializedObject serializedSettings;

        /// <summary>스크롤 위치입니다.</summary>
        private Vector2 scroll;

        /// <summary>도구 목록과 설정 중 어느 쪽을 보고 있는지입니다.</summary>
        private int tab;

        /// <summary>설정 탭에서 쓰는 스크롤 위치입니다. 도구 탭과 따로 기억합니다.</summary>
        private Vector2 settingsScroll;

        // --- Unity Event Functions ---

        /// <summary>패널을 엽니다.</summary>
        [MenuItem("CarDrive/컨트롤 패널 %#c", priority = 0)]
        public static void Open()
        {
            CarDriveControlPanel window = GetWindow<CarDriveControlPanel>("CarDrive");
            window.minSize = new Vector2(380f, 480f);
        }

        /// <summary>목록과 설정을 준비합니다.</summary>
        private void OnEnable()
        {
            groups = CarDriveToolCatalog.CreateGroups();
            Rebind();
        }

        /// <summary>창을 그립니다.</summary>
        private void OnGUI()
        {
            tab = GUILayout.Toolbar(tab, new[] { "도구", "월드 설정" }, GUILayout.Height(24f));
            EditorGUILayout.Space(6f);

            if (tab == 0) DrawToolsTab();
            else DrawSettingsTab();
        }

        // --- Private Methods : 도구 탭 ---

        /// <summary>도구 묶음을 순서대로 그립니다.</summary>
        private void DrawToolsTab()
        {
            EditorGUILayout.HelpBox(
                "버튼은 기존 메뉴(CarDrive/...)와 같은 것을 실행합니다.\n" +
                "여기서는 작업 순서대로 묶고 설명을 붙였을 뿐입니다.",
                MessageType.None);

            EditorGUILayout.Space(4f);

            scroll = EditorGUILayout.BeginScrollView(scroll);

            for (int i = 0; i < groups.Count; i++)
            {
                DrawGroup(groups[i]);
                EditorGUILayout.Space(6f);
            }

            EditorGUILayout.EndScrollView();
        }

        /// <summary>
        /// 묶음 하나를 접이식으로 그립니다.
        /// </summary>
        /// <param name="group">그릴 묶음</param>
        private void DrawGroup(CarDriveToolGroup group)
        {
            string key = FoldoutKeyPrefix + group.Title;
            bool open = EditorPrefs.GetBool(key, true);

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            bool next = EditorGUILayout.Foldout(open, group.Title, true, EditorStyles.foldoutHeader);
            if (next != open) EditorPrefs.SetBool(key, next);

            if (next)
            {
                if (!string.IsNullOrEmpty(group.Hint))
                {
                    EditorGUILayout.LabelField(group.Hint, EditorStyles.wordWrappedMiniLabel);
                }

                EditorGUILayout.Space(2f);

                for (int i = 0; i < group.Tools.Length; i++)
                {
                    DrawTool(group.Tools[i], group.Ordered ? i + 1 : 0);
                }
            }

            EditorGUILayout.EndVertical();
        }

        /// <summary>
        /// 도구 하나를 버튼과 설명으로 그립니다.
        /// </summary>
        /// <param name="tool">그릴 도구</param>
        /// <param name="order">순서 번호. 0이면 번호를 붙이지 않습니다.</param>
        private void DrawTool(CarDriveTool tool, int order)
        {
            string label = order > 0 ? order + ". " + tool.Label : tool.Label;
            if (tool.NeedsConfirm) label += "  ⚠";

            if (GUILayout.Button(label, GUILayout.Height(ButtonHeight)))
            {
                Run(tool);
            }

            if (!string.IsNullOrEmpty(tool.Description))
            {
                EditorGUILayout.LabelField("    " + tool.Description, EditorStyles.wordWrappedMiniLabel);
            }

            EditorGUILayout.Space(2f);
        }

        /// <summary>
        /// 도구를 실행합니다. 되돌리기 어려운 것은 먼저 묻습니다.
        /// </summary>
        /// <param name="tool">실행할 도구</param>
        private static void Run(CarDriveTool tool)
        {
            if (tool.NeedsConfirm)
            {
                bool go = EditorUtility.DisplayDialog(
                    tool.Label,
                    tool.Description + "\n\n되돌리기 어려운 작업입니다. 진행할까요?",
                    "진행", "취소");

                if (!go) return;
            }

            // <b>메뉴 경로로 실행합니다.</b> 도구 클래스를 직접 참조하지 않으므로
            // 이 창이 스물여덟 개 클래스를 알 필요가 없습니다.
            // 경로가 틀렸거나 도구가 사라졌으면 false 가 돌아오므로 조용히 실패하지 않습니다.
            if (!EditorApplication.ExecuteMenuItem(tool.MenuPath))
            {
                Debug.LogError("CarDrive 컨트롤 패널: 메뉴를 찾지 못했습니다 — " + tool.MenuPath +
                               "\n도구가 사라졌거나 이름이 바뀌었다면 CarDriveToolCatalog 를 고치세요.");
            }
        }

        // --- Private Methods : 설정 탭 ---

        /// <summary>월드 설정을 그립니다.</summary>
        private void DrawSettingsTab()
        {
            if (settings == null)
            {
                DrawMissingSettings();
                return;
            }

            if (serializedSettings == null) serializedSettings = new SerializedObject(settings);
            serializedSettings.Update();

            EditorGUILayout.HelpBox(
                "심는 밀도와 잎 모양은 지형에 구워 넣는 값입니다.\n" +
                "여기서 숫자만 바꾸면 이미 심긴 풀은 그대로이니, 바꾼 뒤 '다시 만들기'를 눌러 주세요.\n" +
                "반면 컬링·거리 값은 실행 중에도 바로 반영됩니다.",
                MessageType.None);

            EditorGUILayout.Space(4f);

            settingsScroll = EditorGUILayout.BeginScrollView(settingsScroll);

            SerializedProperty property = serializedSettings.GetIterator();
            bool first = true;
            while (property.NextVisible(first))
            {
                first = false;

                // 스크립트 참조 줄은 보여 줄 이유가 없습니다.
                if (property.name == "m_Script") continue;

                EditorGUILayout.PropertyField(property, true);
            }

            EditorGUILayout.Space(6f);
            EditorGUILayout.ObjectField("에셋", settings, typeof(CarDriveWorldSettings), false);

            EditorGUILayout.EndScrollView();

            serializedSettings.ApplyModifiedProperties();
        }

        /// <summary>설정 에셋이 없을 때의 안내입니다.</summary>
        private void DrawMissingSettings()
        {
            EditorGUILayout.HelpBox(
                "월드 설정 에셋이 없습니다.\n" +
                "만들면 " + SettingsPath + " 에 놓입니다.\n\n" +
                "없어도 게임은 기본값으로 돌아갑니다. 값을 만지려면 하나 만들어 두세요.",
                MessageType.Info);

            if (!GUILayout.Button("설정 에셋 만들기", GUILayout.Height(28f))) return;

            CreateSettingsAsset();
            Rebind();
        }

        // --- Private Methods : 설정 에셋 ---

        /// <summary>월드 설정 에셋을 만듭니다.</summary>
        private static void CreateSettingsAsset()
        {
            Directory.CreateDirectory(SettingsFolder);
            AssetDatabase.Refresh();

            CarDriveWorldSettings created = CreateInstance<CarDriveWorldSettings>();
            AssetDatabase.CreateAsset(created, SettingsPath);
            AssetDatabase.SaveAssets();

            Debug.Log("CarDrive: 월드 설정 에셋을 만들었습니다 — " + SettingsPath);
        }

        /// <summary>설정 에셋을 다시 찾아 붙입니다.</summary>
        private void Rebind()
        {
            settings = AssetDatabase.LoadAssetAtPath<CarDriveWorldSettings>(SettingsPath);
            serializedSettings = settings != null ? new SerializedObject(settings) : null;
        }
    }
}
