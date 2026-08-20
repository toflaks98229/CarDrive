using System.IO;
using UnityEditor;
using UnityEngine;
using CarDrive.Systems;

namespace CarDrive.EditorTools
{
    /// <summary>
    /// 월드의 겉모습과 풀을 한 창에서 다룹니다.
    ///
    /// 그동안 도구가 메뉴 여기저기에 흩어져 있었고, 만질 값은 코드 안에 상수로 박혀 있었습니다.
    /// 이것저것 시험하는 동안에는 그래도 됐지만 방향이 정해진 지금은 불편할 뿐입니다.
    ///
    /// 이 창이 하는 일은 둘입니다.
    ///   1. 설정 에셋 하나를 만들고 그 값을 보여 줍니다.
    ///   2. 그 값으로 도는 도구들을 버튼으로 모아 둡니다.
    ///
    /// <b>값을 바꾸면 다시 적용해야 합니다.</b> 심는 밀도 같은 것은 지형에 구워 넣는 값이라,
    /// 설정만 바꾼다고 이미 심긴 풀이 달라지지 않습니다. 그래서 버튼을 바로 옆에 두었습니다.
    /// </summary>
    public class CarDriveWorldWindow : EditorWindow
    {
        // --- Constants ---

        /// <summary>설정 에셋을 둘 자리입니다. Resources 안이라 게임 중에도 읽힙니다.</summary>
        private const string SettingsFolder = "Assets/_Project/03.DataAssets/Resources";

        /// <summary>설정 에셋 경로입니다.</summary>
        private const string SettingsPath = SettingsFolder + "/" + CarDriveWorldSettings.ResourceName + ".asset";

        // --- Private Member Variables ---

        private CarDriveWorldSettings settings;
        private SerializedObject serialized;
        private Vector2 scroll;

        // --- Unity Event Functions ---

        /// <summary>창을 엽니다.</summary>
        [MenuItem("CarDrive/월드 창", priority = 0)]
        public static void Open()
        {
            CarDriveWorldWindow window = GetWindow<CarDriveWorldWindow>("CarDrive 월드");
            window.minSize = new Vector2(360f, 420f);
        }

        /// <summary>창이 열릴 때 설정을 찾아 둡니다.</summary>
        void OnEnable()
        {
            Rebind();
        }

        /// <summary>창을 그립니다.</summary>
        void OnGUI()
        {
            if (settings == null)
            {
                DrawMissingSettings();
                return;
            }

            if (serialized == null) serialized = new SerializedObject(settings);
            serialized.Update();

            scroll = EditorGUILayout.BeginScrollView(scroll);

            DrawSettings();
            EditorGUILayout.Space(8f);
            DrawActions();

            EditorGUILayout.EndScrollView();

            serialized.ApplyModifiedProperties();
        }

        /// <summary>
        /// 명령줄에서 설정 에셋을 만듭니다. 이미 있으면 그대로 둡니다.
        /// <c>Unity.exe -batchmode -quit -executeMethod CarDriveWorldWindow.CreateSettingsFromCommandLine</c>
        /// </summary>
        public static void CreateSettingsFromCommandLine()
        {
            CarDriveWorldSettings existing = AssetDatabase.LoadAssetAtPath<CarDriveWorldSettings>(SettingsPath);
            if (existing != null)
            {
                Debug.Log("WORLD 설정 에셋이 이미 있습니다: " + SettingsPath);
                return;
            }

            Directory.CreateDirectory(SettingsFolder);
            AssetDatabase.Refresh();

            CarDriveWorldSettings created = CreateInstance<CarDriveWorldSettings>();
            AssetDatabase.CreateAsset(created, SettingsPath);
            AssetDatabase.SaveAssets();

            Debug.Log("WORLD 설정 에셋을 만들었습니다: " + SettingsPath +
                      "  (칸당 " + created.maxPerCell + "포기 / 거리 " + created.detailDistance + "m / 밀도 " + created.detailDensity + ")");
        }

        // --- Private Methods ---

        /// <summary>설정 에셋이 없을 때 만들 수 있게 안내합니다.</summary>
        private void DrawMissingSettings()
        {
            EditorGUILayout.HelpBox(
                "월드 설정 에셋이 없습니다.\n" +
                "만들면 " + SettingsPath + " 에 놓입니다.\n\n" +
                "없어도 게임은 기본값으로 돌아갑니다. 값을 만지려면 하나 만들어 두세요.",
                MessageType.Info);

            if (GUILayout.Button("설정 에셋 만들기", GUILayout.Height(28f)))
            {
                Directory.CreateDirectory(SettingsFolder);
                AssetDatabase.Refresh();

                CarDriveWorldSettings created = CreateInstance<CarDriveWorldSettings>();
                AssetDatabase.CreateAsset(created, SettingsPath);
                AssetDatabase.SaveAssets();

                Rebind();
            }
        }

        /// <summary>설정 값을 항목별로 그립니다.</summary>
        private void DrawSettings()
        {
            EditorGUILayout.LabelField("설정", EditorStyles.boldLabel);

            SerializedProperty property = serialized.GetIterator();
            bool first = true;

            while (property.NextVisible(first))
            {
                first = false;

                // 스크립트 참조 줄은 보여 줄 이유가 없습니다.
                if (property.name == "m_Script") continue;

                EditorGUILayout.PropertyField(property, true);
            }

            EditorGUILayout.Space(4f);
            EditorGUILayout.ObjectField("에셋", settings, typeof(CarDriveWorldSettings), false);
        }

        /// <summary>도구 버튼들을 그립니다.</summary>
        private void DrawActions()
        {
            EditorGUILayout.LabelField("적용", EditorStyles.boldLabel);

            EditorGUILayout.HelpBox(
                "심는 밀도와 잎 모양은 지형과 메시에 구워 넣는 값입니다.\n" +
                "위에서 숫자만 바꾸면 이미 심긴 풀은 그대로이니, 바꾼 뒤에는 아래를 눌러 주세요.",
                MessageType.None);

            if (GUILayout.Button("① 룩과 풀 적용  (지면·풀·하늘·색보정)", GUILayout.Height(26f)))
            {
                LowPolyLookSetup.Apply();
            }

            if (GUILayout.Button("② 풀 눕히기 배선  (차·사람·유령)", GUILayout.Height(26f)))
            {
                GrassTrampleSetup.Apply();
            }

            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("확인", EditorStyles.boldLabel);

            EditorGUILayout.HelpBox(
                "확인 도구는 씬을 열고 렌더링하므로 시간이 걸립니다.\n" +
                "결과는 Console 과 Logs 폴더에 남습니다.",
                MessageType.None);

            if (GUILayout.Button("심긴 풀 검사  (도로에 심기지 않았는지)"))
            {
                LowPolyLookSetup.VerifyFromCommandLine();
            }

            if (GUILayout.Button("누르개 검사  (무엇이 자국을 남기는지)"))
            {
                GrassTrampleSetup.VerifyFromCommandLine();
            }

            if (GUILayout.Button("그리기 비용 세기  (드로우 콜 · 삼각형)"))
            {
                LowPolyLookSetup.CountFromCommandLine();
            }

            EditorGUILayout.Space(6f);

            EditorGUILayout.HelpBox(
                "밀리초를 재던 도구는 걷어냈습니다. 배치 렌더링으로 잰 시간은 같은 조건으로\n" +
                "두 번 돌려도 크게 달라, 무엇이 나아졌는지 판단할 수 없었습니다.\n" +
                "드로우 콜은 세는 값이라 흔들리지 않고, 이 게임은 CPU 바운드라 드로우 콜이 곧 프레임입니다.",
                MessageType.None);
        }

        /// <summary>설정 에셋을 다시 찾습니다.</summary>
        private void Rebind()
        {
            settings = AssetDatabase.LoadAssetAtPath<CarDriveWorldSettings>(SettingsPath);
            serialized = settings != null ? new SerializedObject(settings) : null;
        }
    }
}
