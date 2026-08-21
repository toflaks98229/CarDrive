using System.Text;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace CarDrive.EditorTools
{
    /// <summary>
    /// 한글 폰트 에셋의 아틀라스를 <b>Static 에서 Dynamic 으로</b> 바꿉니다.
    ///
    /// <b>왜 필요한가.</b> <c>korean SDF.asset</c> 은 한글 음절 전체(AC00~D7A3, 11,172자)를
    /// 미리 구워 둔 <b>4096x4096 아틀라스</b>를 안고 있습니다. 파일이 38.6MB 이고,
    /// 그중 32.0MB 가 아틀라스 텍스처, 4.9MB 가 글리프 표입니다.
    /// 그런데 이 게임이 화면에 실제로 띄우는 한글은 <b>약 90자</b>입니다
    /// (씬·프리팹의 TMP 텍스트 15자 + NeedDefinitions·WeatherDefinitions 등
    /// 데이터 기본값 88자. 디버그 오버레이는 IMGUI 라 아틀라스와 무관합니다).
    /// 즉 쓰지 않는 글자 때문에 100배가 넘는 값을 치르고 있습니다.
    ///
    /// <b>Dynamic 이 무엇을 바꾸는가.</b> 아틀라스를 비우고, 화면에 나온 글자만
    /// 그때그때 구워 넣습니다. 대신 원본 <c>korean.ttf</c>(1.32MB, includeFontData:1)가
    /// 빌드에 실립니다. TMP 는 Static 일 때 <c>m_SourceFontFile</c> 을 null 로 지우고
    /// Dynamic 일 때만 채우므로(TMP_FontAsset.cs 의 atlasPopulationMode 세터),
    /// <b>둘 중 하나만</b> 빌드에 들어갑니다.
    ///
    /// <b>빌드 시점.</b> <c>TMP_PreBuildProcessor</c> 가 Dynamic + clearDynamicDataOnBuild 인
    /// 폰트를 찾아 <c>ClearCharacterAndGlyphTablesInternal()</c> 을 부릅니다. 이것은
    /// 글리프 표를 비우고 <c>ClearAtlasTextures(true)</c> 로 <b>아틀라스를 1x1 로 줄입니다.</b>
    /// 그래서 아래 <see cref="TargetAtlasSize"/> 는 빌드 크기가 아니라
    /// <b>런타임에 잡을 아틀라스 크기</b>를 정합니다.
    ///
    /// <b>왜 2048 인가.</b> 이 폰트는 pointSize 90 · padding 5 라 한글 한 자가 약 100px 칸을
    /// 차지합니다. 필요한 90자에 ASCII 를 더하면 약 1.4M px^2 이 필요한데
    /// 1024x1024 는 1.05M px^2 이라 빠듯합니다. 2048x2048 은 4.2M px^2 로 세 배 여유가 있고,
    /// 아틀라스가 한 장이면 텍스트가 <b>한 머티리얼로 배칭</b>됩니다.
    /// (여유가 넘쳐도 <c>isMultiAtlasTexturesEnabled</c> 를 켜 두어, 넘칠 때 글자가
    /// 사라지는 대신 페이지가 늘어나게 합니다. 페이지가 늘면 드로우 콜도 늘어납니다.)
    /// </summary>
    public static class FontAtlasSetup
    {
        // --- Constants ---

        /// <summary>바꿀 대상 폰트 에셋입니다.</summary>
        private const string KoreanFontPath = "Assets/TextMesh Pro/Fonts/korean SDF.asset";

        /// <summary>
        /// 런타임에 잡을 아틀라스 한 변의 길이입니다.
        /// 빌드에는 1x1 로 줄어들어 들어가므로 <b>빌드 크기와는 무관</b>합니다.
        /// </summary>
        private const int TargetAtlasSize = 2048;

        // --- Menu ---

        /// <summary>
        /// 지금 상태만 읽어서 보고합니다. 아무것도 바꾸지 않습니다.
        /// 바꾸기 전후로 한 번씩 돌려 비교하려고 둡니다.
        /// </summary>
        [MenuItem("CarDrive/UI/한글 폰트 아틀라스 점검")]
        public static void InspectKoreanFont()
        {
            TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(KoreanFontPath);
            if (font == null)
            {
                Debug.LogError("폰트 에셋을 찾지 못했습니다 : " + KoreanFontPath);
                return;
            }

            Debug.Log(Describe(font, "점검"));
        }

        /// <summary>
        /// <c>Unity.exe -batchmode -quit -executeMethod CarDrive.EditorTools.FontAtlasSetup.MakeKoreanFontDynamic</c>
        ///
        /// 이미 Dynamic 이면 아무것도 하지 않고 끝냅니다(여러 번 돌려도 안전합니다).
        /// </summary>
        [MenuItem("CarDrive/UI/한글 폰트 아틀라스 동적 전환")]
        public static void MakeKoreanFontDynamic()
        {
            TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(KoreanFontPath);
            if (font == null)
            {
                Debug.LogError("폰트 에셋을 찾지 못했습니다 : " + KoreanFontPath);
                EditorApplication.Exit(1);
                return;
            }

            Debug.Log(Describe(font, "바꾸기 전"));

            if (font.atlasPopulationMode == AtlasPopulationMode.Dynamic
                && font.atlasWidth == TargetAtlasSize
                && font.atlasHeight == TargetAtlasSize)
            {
                Debug.Log("이미 Dynamic " + TargetAtlasSize + "x" + TargetAtlasSize + " 입니다. 건드리지 않았습니다.");
                return;
            }

            // 원본 TTF. Dynamic 은 이것이 있어야 런타임에 글자를 구울 수 있습니다.
            SerializedObject so = new SerializedObject(font);
            SerializedProperty guidProp = so.FindProperty("m_SourceFontFileGUID");
            string sourceGuid = guidProp != null ? guidProp.stringValue : null;

            Font sourceFont = null;
            if (!string.IsNullOrEmpty(sourceGuid))
            {
                string ttfPath = AssetDatabase.GUIDToAssetPath(sourceGuid);
                sourceFont = AssetDatabase.LoadAssetAtPath<Font>(ttfPath);
            }

            if (sourceFont == null)
            {
                Debug.LogError("원본 TTF 를 찾지 못했습니다 (m_SourceFontFileGUID = " + sourceGuid + "). "
                             + "Dynamic 으로 바꾸면 글자가 아예 안 나오므로 중단합니다.");
                EditorApplication.Exit(1);
                return;
            }

            // 내부(internal) 세터가 많아 SerializedObject 로 직접 씁니다.
            SetInt(so, "m_AtlasPopulationMode", (int)AtlasPopulationMode.Dynamic);
            SetInt(so, "m_AtlasWidth", TargetAtlasSize);
            SetInt(so, "m_AtlasHeight", TargetAtlasSize);
            SetBool(so, "m_IsMultiAtlasTexturesEnabled", true);
            SetBool(so, "m_ClearDynamicDataOnBuild", true);

            SerializedProperty srcProp = so.FindProperty("m_SourceFontFile");
            if (srcProp != null) srcProp.objectReferenceValue = sourceFont;

            so.ApplyModifiedPropertiesWithoutUndo();

            // 글리프 표를 비우고 아틀라스 텍스처를 1x1 로 줄입니다(true).
            //
            // 2048 로 잡아 두지 않는 이유. 빈 아틀라스라도 Alpha8 2048x2048 은
            // 저장소에서 8MB(YAML 16진수)를 차지합니다. TMP 는 글자를 처음 구울 때
            // 텍스처가 1x1 이면 m_AtlasWidth x m_AtlasHeight 로 다시 잡습니다
            // (TMP_FontAsset.TryAddCharacters 의 "Resize the Atlas Texture" 구간).
            // 그래서 <b>저장은 1x1, 런타임은 2048</b> 이 됩니다.
            //
            // 아틀라스 Texture2D 오브젝트 자체는 재사용되므로
            // 폰트 머티리얼의 _MainTex 참조는 끊기지 않습니다.
            font.ClearFontAssetData(true);

            EditorUtility.SetDirty(font);
            if (font.material != null) EditorUtility.SetDirty(font.material);
            if (font.atlasTexture != null) EditorUtility.SetDirty(font.atlasTexture);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log(Describe(font, "바꾼 뒤") + "\n원본 TTF : " + AssetDatabase.GetAssetPath(sourceFont));
        }

        /// <summary>
        /// Dynamic 전환이 <b>실제로 동작하는지</b> 확인합니다.
        ///
        /// 이 게임이 화면에 띄우는 한글 <see cref="UsedKorean"/> 을 아틀라스에 넣어 보고,
        /// 하나라도 못 넣으면 실패로 봅니다. 원본 TTF 를 못 읽거나 자소가 폰트에 없으면
        /// 여기서 걸립니다. <b>확인이 끝나면 다시 비웁니다</b> — 구워 둔 채로 두면
        /// 2048x2048 아틀라스가 저장소에서 8MB 를 도로 차지합니다.
        /// </summary>
        [MenuItem("CarDrive/UI/한글 폰트 아틀라스 검증")]
        public static void VerifyKoreanFontDynamic()
        {
            TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(KoreanFontPath);
            if (font == null)
            {
                Debug.LogError("폰트 에셋을 찾지 못했습니다 : " + KoreanFontPath);
                EditorApplication.Exit(1);
                return;
            }

            if (font.atlasPopulationMode != AtlasPopulationMode.Dynamic)
            {
                Debug.LogError("아직 Dynamic 이 아닙니다. 먼저 '한글 폰트 아틀라스 동적 전환' 을 돌리세요.");
                EditorApplication.Exit(1);
                return;
            }

            string probe = UsedKorean + UsedAscii;
            string missing;
            bool ok = font.TryAddCharacters(probe, out missing);

            int baked = font.characterTable != null ? font.characterTable.Count : 0;
            Texture2D tex = font.atlasTexture;

            StringBuilder sb = new StringBuilder();
            sb.Append("[폰트 아틀라스 검증] 넣어 본 글자 ").Append(probe.Length).Append("자\n");
            sb.Append("  결과           : ").Append(ok ? "전부 들어감 OK" : "일부 실패").Append('\n');
            sb.Append("  아틀라스에 구움: ").Append(baked).Append("자\n");
            sb.Append("  아틀라스 텍스처: ");
            if (tex != null) sb.Append(tex.width).Append('x').Append(tex.height); else sb.Append("없음");
            sb.Append('\n');
            sb.Append("  못 넣은 글자   : ").Append(string.IsNullOrEmpty(missing) ? "없음" : missing);

            // 확인이 끝났으니 되돌립니다. 저장소 용량을 위해 반드시 비웁니다.
            font.ClearFontAssetData(true);
            EditorUtility.SetDirty(font);
            if (font.atlasTexture != null) EditorUtility.SetDirty(font.atlasTexture);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            sb.Append("\n  → 확인 후 다시 비움. 현재 글리프 ")
              .Append(font.glyphTable != null ? font.glyphTable.Count : 0).Append("자, 텍스처 ")
              .Append(font.atlasTexture != null ? font.atlasTexture.width : 0).Append('x')
              .Append(font.atlasTexture != null ? font.atlasTexture.height : 0);

            Debug.Log(sb.ToString());

            if (!ok) EditorApplication.Exit(1);
        }

        // --- Helpers ---

        /// <summary>
        /// 씬·프리팹의 TMP 텍스트와 NeedDefinitions·WeatherDefinitions·CurrencyDefinitions 등
        /// 데이터 기본값에서 뽑은, <b>화면에 실제로 나가는</b> 한글입니다.
        /// (디버그 오버레이는 IMGUI 라 여기 포함하지 않습니다.)
        /// </summary>
        private const string UsedKorean =
            "갈개걸결계과귀기길끄내놓뇨는니도돈동두뜩라량레려력로루를름림마맑모배보볼비" +
            "사상세소속쉬스슬승시식신씻안없엑요용우음이일자잔잠장적전정줄즈즘증지차청체" +
            "초탑태테토트폭표플피하한허화환흐";

        /// <summary>퍼센트 표시·수치 등에 쓰는 아스키입니다.</summary>
        private const string UsedAscii = "0123456789/%. ";

        /// <summary>폰트 에셋의 현재 상태를 한 덩어리 문자열로 만듭니다.</summary>
        private static string Describe(TMP_FontAsset font, string label)
        {
            string path = AssetDatabase.GetAssetPath(font);
            long bytes = 0;
            System.IO.FileInfo info = new System.IO.FileInfo(path);
            if (info.Exists) bytes = info.Length;

            StringBuilder sb = new StringBuilder();
            sb.Append("[폰트 아틀라스 ").Append(label).Append("] ").Append(path).Append('\n');
            sb.Append("  파일 크기      : ").Append(bytes.ToString("N0")).Append(" B (")
              .Append((bytes / 1048576f).ToString("F2")).Append(" MB)\n");
            sb.Append("  아틀라스 모드  : ").Append(font.atlasPopulationMode).Append('\n');
            sb.Append("  아틀라스 크기  : ").Append(font.atlasWidth).Append('x').Append(font.atlasHeight).Append('\n');
            sb.Append("  아틀라스 장수  : ")
              .Append(font.atlasTextures != null ? font.atlasTextures.Length : 0).Append('\n');
            sb.Append("  실제 텍스처    : ");
            if (font.atlasTexture != null)
                sb.Append(font.atlasTexture.width).Append('x').Append(font.atlasTexture.height)
                  .Append(' ').Append(font.atlasTexture.format);
            else
                sb.Append("없음");
            sb.Append('\n');
            sb.Append("  글리프 / 글자  : ")
              .Append(font.glyphTable != null ? font.glyphTable.Count : 0).Append(" / ")
              .Append(font.characterTable != null ? font.characterTable.Count : 0).Append('\n');

            // clearDynamicDataOnBuild 는 internal 이라 직렬화 필드로 읽습니다.
            SerializedProperty clearProp = new SerializedObject(font).FindProperty("m_ClearDynamicDataOnBuild");
            sb.Append("  빌드 시 비우기 : ").Append(clearProp != null ? clearProp.boolValue.ToString() : "?").Append('\n');
            sb.Append("  다중 아틀라스  : ").Append(font.isMultiAtlasTexturesEnabled).Append('\n');
            sb.Append("  소스 TTF 참조  : ").Append(font.sourceFontFile != null ? font.sourceFontFile.name : "null");
            return sb.ToString();
        }

        private static void SetInt(SerializedObject so, string name, int value)
        {
            SerializedProperty p = so.FindProperty(name);
            if (p == null) { Debug.LogWarning("직렬화 필드를 못 찾음 : " + name); return; }
            p.intValue = value;
        }

        private static void SetBool(SerializedObject so, string name, bool value)
        {
            SerializedProperty p = so.FindProperty(name);
            if (p == null) { Debug.LogWarning("직렬화 필드를 못 찾음 : " + name); return; }
            p.boolValue = value;
        }
    }
}
