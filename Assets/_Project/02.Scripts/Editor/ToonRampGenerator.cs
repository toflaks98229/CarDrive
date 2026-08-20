using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace CarDrive.EditorTools
{
    /// <summary>
    /// 툰 셰이더가 쓸 램프 텍스처를 굽습니다.
    ///
    /// Flat Kit 의 Steps · Curve 모드에 해당하는 것을 텍스처로 만드는 도구입니다.
    /// 가로축이 밝기(왼쪽=그늘, 오른쪽=빛)이고, 셰이더가 이 띠에서 색을 읽습니다.
    ///
    /// ── 이 프로젝트에서 램프가 <b>왜</b> 값을 하는가 ──
    ///
    /// 화면은 <see cref="Assets"/> 의 팔레트 후처리를 거치는데, 그 후처리는
    /// <b>휘도만</b> 단계로 끊고 색상은 그대로 둡니다.
    /// (PixelizePalette.shader 의 LuminanceQuantize: 휘도를 끊은 비율을 RGB 에 곱함)
    ///
    /// 그래서 <b>밝기만 다른 램프는 헛수고입니다.</b> 후처리가 어차피 같은 단계로 뭉갭니다.
    /// 램프가 실제로 화면에 남기는 것은 <b>색조</b>입니다. 그늘을 그냥 어둡게 하는 대신
    /// 남색 쪽으로 <b>돌리면</b> 그 색은 양자화를 통과합니다.
    ///
    /// 그래서 아래 프리셋은 전부 <b>밝기와 함께 색조가 도는</b> 형태로 잡았습니다.
    /// 밤길 주행 게임의 인상 — 그늘이 파랗고 빛이 나트륨 등 색으로 따뜻한 — 이 여기서 나옵니다.
    ///
    /// 텍스처는 점 필터로 굽습니다. 보간이 들어가면 띠 사이가 흐려지고,
    /// 그 흐린 구간을 후처리가 다시 끊어 경계가 <b>두 번</b> 생깁니다.
    /// </summary>
    public static class ToonRampGenerator
    {
        /// <summary>램프를 저장할 폴더입니다.</summary>
        private const string RampFolder = "Assets/_Project/04.Art/03.Shaders/Toon/Ramps";

        /// <summary>램프 가로 해상도입니다. 띠가 몇 개든 이 안에서 나뉩니다.</summary>
        private const int RampWidth = 64;

        /// <summary>램프 한 단계를 정의합니다.</summary>
        private struct Band
        {
            /// <summary>이 띠가 끝나는 지점 (0~1). 앞 띠의 끝부터 여기까지가 이 띠입니다.</summary>
            public float end;

            /// <summary>이 띠의 색입니다.</summary>
            public Color color;

            public Band(float end, Color color)
            {
                this.end = end;
                this.color = color;
            }
        }

        // --- Public Methods ---

        /// <summary>에디터 메뉴에서 실행합니다. 프리셋 램프를 전부 굽습니다.</summary>
        [MenuItem("CarDrive/Look/툰 램프 굽기")]
        public static void BakeAll()
        {
            EnsureFolder(RampFolder);

            List<string> report = new List<string>();

            // ── 밤길 (기본) ──
            // 그늘은 남색으로 돌리고, 빛은 나트륨 가로등의 따뜻한 색으로 살짝 기울입니다.
            // 밝기 차이보다 <b>색조 차이</b>가 크다는 점이 핵심입니다.
            Bake("ToonRamp_NightDrive", new[]
            {
                new Band(0.30f, new Color(0.22f, 0.26f, 0.44f)),  // 깊은 그늘 — 남색
                new Band(0.52f, new Color(0.42f, 0.44f, 0.58f)),  // 그늘 — 푸른 회색
                new Band(0.78f, new Color(0.82f, 0.80f, 0.78f)),  // 중간 — 거의 무채색
                new Band(1.00f, new Color(1.00f, 0.96f, 0.86f)),  // 빛 — 따뜻한 흰색
            }, report);

            // ── 흐린 낮 ──
            // 대비를 낮추고 전체를 푸르게. 비 오는 날 낮의 인상입니다.
            Bake("ToonRamp_Overcast", new[]
            {
                new Band(0.38f, new Color(0.44f, 0.48f, 0.56f)),
                new Band(0.72f, new Color(0.70f, 0.73f, 0.78f)),
                new Band(1.00f, new Color(0.92f, 0.94f, 0.96f)),
            }, report);

            // ── 맑은 낮 ──
            // 그늘에 청록을, 빛에 노랑을 넣어 색 대비를 만듭니다.
            Bake("ToonRamp_Clear", new[]
            {
                new Band(0.34f, new Color(0.30f, 0.42f, 0.44f)),
                new Band(0.62f, new Color(0.66f, 0.70f, 0.62f)),
                new Band(1.00f, new Color(1.00f, 0.98f, 0.84f)),
            }, report);

            // ── 지면 (띠를 적게) ──
            // 넓은 면에 띠가 많으면 등고선처럼 보입니다. 세 단계로 충분합니다.
            Bake("ToonRamp_Ground", new[]
            {
                new Band(0.40f, new Color(0.34f, 0.38f, 0.52f)),
                new Band(0.75f, new Color(0.74f, 0.76f, 0.74f)),
                new Band(1.00f, new Color(1.00f, 0.98f, 0.90f)),
            }, report);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("ToonRampGenerator:" + System.Environment.NewLine +
                      string.Join(System.Environment.NewLine, report));
        }

        // --- Private Methods ---

        /// <summary>
        /// 램프 텍스처 하나를 굽습니다.
        /// </summary>
        /// <param name="name">파일 이름 (확장자 제외)</param>
        /// <param name="bands">왼쪽부터 순서대로 놓을 띠들</param>
        /// <param name="report">진행 내용을 적을 목록</param>
        private static void Bake(string name, Band[] bands, List<string> report)
        {
            Texture2D texture = new Texture2D(RampWidth, 1, TextureFormat.RGBA32, false, false);

            for (int x = 0; x < RampWidth; x++)
            {
                // 픽셀 중심으로 읽어야 첫 띠와 마지막 띠가 한 픽셀씩 잘리지 않습니다.
                float t = (x + 0.5f) / RampWidth;
                texture.SetPixel(x, 0, PickBand(bands, t));
            }

            texture.Apply();

            string path = RampFolder + "/" + name + ".png";
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            ConfigureImporter(path);

            report.Add("· 램프를 구웠습니다: " + name + " (띠 " + bands.Length + "개)");
        }

        /// <summary>
        /// 밝기 t 가 속한 띠의 색을 돌려줍니다.
        /// </summary>
        /// <param name="bands">띠 목록</param>
        /// <param name="t">밝기 (0~1)</param>
        private static Color PickBand(Band[] bands, float t)
        {
            for (int i = 0; i < bands.Length; i++)
            {
                if (t <= bands[i].end) return bands[i].color;
            }
            return bands[bands.Length - 1].color;
        }

        /// <summary>
        /// 램프에 맞는 임포트 설정을 겁니다.
        ///
        /// <b>점 필터와 Clamp 가 핵심입니다.</b> 보간하면 띠 사이가 흐려지고,
        /// 반복(Repeat)하면 가장 어두운 띠와 가장 밝은 띠가 맞닿아 경계에 줄이 생깁니다.
        /// </summary>
        /// <param name="path">임포트할 텍스처 경로</param>
        private static void ConfigureImporter(string path)
        {
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) return;

            importer.textureType = TextureImporterType.Default;
            importer.filterMode = FilterMode.Point;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.mipmapEnabled = false;
            importer.sRGBTexture = true;
            importer.alphaSource = TextureImporterAlphaSource.None;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.textureCompression = TextureImporterCompression.Uncompressed;

            importer.SaveAndReimport();
        }

        /// <summary>폴더가 없으면 만듭니다.</summary>
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
