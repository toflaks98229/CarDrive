using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace CarDrive.EditorTools
{
    /// <summary>
    /// 바위·건물 재질에 <b>디더 페이드를 켭니다.</b> 나무만 켜져 있던 것을 나머지로 넓힙니다.
    ///
    /// <b>왜 나무만 켜져 있었는가.</b> <see cref="ViewDistanceSetup"/> 이 페이드를 배선할 때
    /// <c>{이름}_Fade.mat</c> 라는 <b>이름 규칙</b>으로 재질을 찾습니다. 나무는 그 규칙에 맞는
    /// 전용 재질을 만들어 쓰지만, <c>RockLowPoly</c>·<c>BuildingWall</c>·<c>BuildingRoof</c> 는
    /// 그 규칙 밖이라 도구가 한 번도 건드리지 않았습니다.
    ///
    /// 그래서 바위와 건물은 <b>페이드 없이 잘렸습니다.</b> 그리기 거리에 닿는 순간
    /// 통째로 사라지고, 다가가면 통째로 나타납니다. 시야를 줄일수록 그 경계가
    /// 가까워져 더 눈에 띕니다.
    ///
    /// <b>거리는 여기서 정하지 않습니다.</b> 실행 중에는 <see cref="Systems.ViewRangeScaler"/> 가
    /// 시야 거리에서 유도해 셰이더 전역으로 넘깁니다. 이 도구가 재질에 적는 값은
    /// 그 전역이 없을 때(에디터 미리보기)의 기본값일 뿐입니다.
    ///
    /// 여러 번 눌러도 안전합니다. 이미 켜져 있으면 건너뜁니다.
    /// </summary>
    public static class PropFadeSetup
    {
        // --- Constants ---

        /// <summary>재질을 찾을 폴더입니다.</summary>
        private const string MaterialFolder = "Assets/_Project/04.Art/00.Materials";

        /// <summary>페이드를 켤 셰이더 이름입니다.</summary>
        private const string ShaderName = "CarDrive/Toon Lit";

        /// <summary>에디터 미리보기용 기본 시작 거리입니다.</summary>
        private const float DefaultFadeStart = 240f;

        /// <summary>에디터 미리보기용 기본 종료 거리입니다.</summary>
        private const float DefaultFadeEnd = 330f;

        /// <summary>
        /// 페이드를 켜지 <b>않을</b> 재질입니다.
        ///
        /// 손에 드는 것과 화면에 붙는 것은 거리로 지우면 안 됩니다.
        /// 엑토플라즘은 플레이어 쪽으로 빨려 오는 물건이라 늘 가까이 있고,
        /// 거리로 지우는 규칙이 걸리면 빨려 오는 도중에 깜빡일 수 있습니다.
        /// </summary>
        private static readonly string[] Skip = { "EctoplasmDrop" };

        // --- Public Methods ---

        /// <summary>에디터 메뉴에서 실행합니다.</summary>
        [MenuItem("CarDrive/Look/바위 · 건물에 디더 페이드 켜기")]
        public static void Apply()
        {
            Shader shader = Shader.Find(ShaderName);
            if (shader == null)
            {
                Debug.LogError("PropFadeSetup: " + ShaderName + " 셰이더를 찾지 못했습니다.");
                return;
            }

            string[] guids = AssetDatabase.FindAssets("t:Material", new[] { MaterialFolder });
            List<string> report = new List<string>();
            int changed = 0;

            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);

                if (mat == null || mat.shader != shader) continue;
                if (ShouldSkip(mat.name)) { report.Add("  건너뜀 — " + mat.name + " (거리로 지우면 안 되는 것)"); continue; }
                if (!mat.HasProperty("_UseDitherFade")) continue;

                if (mat.IsKeywordEnabled("_DITHER_FADE") && mat.GetFloat("_UseDitherFade") > 0.5f)
                {
                    report.Add("  이미 켜져 있음 — " + mat.name);
                    continue;
                }

                Undo.RecordObject(mat, "디더 페이드 켜기");

                mat.SetFloat("_UseDitherFade", 1f);
                mat.EnableKeyword("_DITHER_FADE");

                // 전역이 없을 때만 쓰이는 기본값입니다. 실행 중에는 ViewRangeScaler 가 덮습니다.
                if (mat.HasProperty("_FadeStart")) mat.SetFloat("_FadeStart", DefaultFadeStart);
                if (mat.HasProperty("_FadeEnd")) mat.SetFloat("_FadeEnd", DefaultFadeEnd);

                EditorUtility.SetDirty(mat);
                report.Add("  켬 — " + mat.name);
                changed++;
            }

            AssetDatabase.SaveAssets();

            report.Insert(0, "재질 " + changed + "개에 디더 페이드를 켰습니다.");
            report.Add("");
            report.Add("실제 페이드 거리는 실행 중에 ViewRangeScaler 가 시야 거리에서 유도합니다.");
            report.Add("여기 적은 " + DefaultFadeStart + "~" + DefaultFadeEnd + "m 는 에디터 미리보기용 기본값입니다.");

            Debug.Log("PropFadeSetup:" + System.Environment.NewLine +
                      string.Join(System.Environment.NewLine, report));
        }

        // --- Private Methods ---

        /// <summary>
        /// 이 재질을 건너뛸지 확인합니다.
        /// </summary>
        /// <param name="name">재질 이름</param>
        /// <returns>건너뛰어야 하면 true</returns>
        private static bool ShouldSkip(string name)
        {
            for (int i = 0; i < Skip.Length; i++)
            {
                if (name.Contains(Skip[i])) return true;
            }
            return false;
        }
    }
}
