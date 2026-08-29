using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using CarDrive.Systems;

namespace CarDrive.Tests
{
    /// <summary>
    /// 전역 젖음 지도의 <b>좌표 계약</b>이 C# 과 HLSL 에서 같은지 못박습니다.
    ///
    /// <b>왜 필요한가.</b> 월드 좌표를 UV 로 바꾸는 식이 세 곳에 있습니다 —
    /// 칠하는 쪽(SplatManager), 읽는 쪽(CarDriveSplatMap.hlsl), 그리고 컴퓨트의 텍셀 환산.
    /// 한 곳만 고치면 <b>컴파일도 되고 화면도 그럴듯한데 자국이 엉뚱한 자리에 찍힙니다.</b>
    /// 눈으로는 "어? 좀 밀렸나?" 정도로만 보여서 원인을 찾기가 매우 어렵습니다.
    ///
    /// <b>왜 EditMode 인가.</b> 여기서 검사하는 것은 순수 계산과 셰이더 <b>원문</b>뿐이라
    /// 씬도 그래픽 장치도 필요 없습니다. 지도를 실제로 칠하는 것은 GPU 가 있어야 하므로
    /// 이 테스트의 몫이 아닙니다.
    /// </summary>
    public class SplatMapTests
    {
        private const string IncludePath =
            "Assets/_Project/04.Art/03.Shaders/Toon/CarDriveSplatMap.hlsl";

        private const string ComputePath =
            "Assets/_Project/03.DataAssets/Resources/SplatPainter.compute";

        private static string Read(string path)
        {
            Assert.IsTrue(File.Exists(path), "파일을 못 찾음: " + path);
            return File.ReadAllText(path);
        }

        /// <summary>주석을 걷어냅니다. 설명에 적힌 식을 코드로 세면 안 됩니다.</summary>
        private static string Code(string path)
        {
            return Regex.Replace(Read(path), "//.*", string.Empty);
        }

        [Test]
        public void 월드에서_UV_로_가는_식이_경계에서_맞는다()
        {
            Vector2 origin = new Vector2(-300f, -300f);
            Vector2 size = new Vector2(1100f, 1200f);

            // 구석은 0, 반대쪽 끝은 1.
            Vector2 atOrigin = SplatManager.WorldToUv(new Vector3(-300f, 12f, -300f), origin, size);
            Assert.AreEqual(0f, atOrigin.x, 1e-5f);
            Assert.AreEqual(0f, atOrigin.y, 1e-5f);

            Vector2 atFar = SplatManager.WorldToUv(new Vector3(800f, -5f, 900f), origin, size);
            Assert.AreEqual(1f, atFar.x, 1e-5f);
            Assert.AreEqual(1f, atFar.y, 1e-5f);

            // 한가운데는 0.5.
            Vector2 mid = SplatManager.WorldToUv(new Vector3(250f, 0f, 300f), origin, size);
            Assert.AreEqual(0.5f, mid.x, 1e-5f);
            Assert.AreEqual(0.5f, mid.y, 1e-5f);
        }

        [Test]
        public void 지도_밖은_0_1_밖으로_나간다()
        {
            // 읽는 쪽이 이 값을 보고 0 을 돌려줍니다. 안 나가면 가장자리가 번집니다.
            Vector2 origin = new Vector2(0f, 0f);
            Vector2 size = new Vector2(100f, 100f);

            Assert.Less(SplatManager.WorldToUv(new Vector3(-1f, 0f, 50f), origin, size).x, 0f);
            Assert.Greater(SplatManager.WorldToUv(new Vector3(101f, 0f, 50f), origin, size).x, 1f);
            Assert.Less(SplatManager.WorldToUv(new Vector3(50f, 0f, -1f), origin, size).y, 0f);
            Assert.Greater(SplatManager.WorldToUv(new Vector3(50f, 0f, 101f), origin, size).y, 1f);
        }

        [Test]
        public void 높이는_UV_에_영향을_주지_않는다()
        {
            // 위에서 내려다본 투영이므로 y 는 무시돼야 합니다. 비탈에서 자국이
            // 높이에 따라 밀리면 이것부터 의심할 자리입니다.
            Vector2 origin = new Vector2(-300f, -300f);
            Vector2 size = new Vector2(1100f, 1200f);

            Vector2 low = SplatManager.WorldToUv(new Vector3(10f, 0f, 20f), origin, size);
            Vector2 high = SplatManager.WorldToUv(new Vector3(10f, 500f, 20f), origin, size);

            Assert.AreEqual(low.x, high.x, 1e-6f);
            Assert.AreEqual(low.y, high.y, 1e-6f);
        }

        [Test]
        public void HLSL_이_같은_UV_식을_쓴다()
        {
            // 읽는 쪽이 (positionWS.xz - rect.xy) * rect.zw 여야 합니다.
            // rect.zw 는 <b>역수</b>라 곱해야 합니다 — 나누면 값이 뒤집힙니다.
            string src = Code(IncludePath);

            Assert.IsTrue(
                Regex.IsMatch(src, @"positionWS\.xz\s*-\s*_GlobalSplatMapRect\.xy"),
                "HLSL 의 UV 식이 계약과 다릅니다. (positionWS.xz - rect.xy) 여야 합니다.");

            Assert.IsTrue(
                Regex.IsMatch(src, @"\)\s*\*\s*_GlobalSplatMapRect\.zw"),
                "HLSL 이 rect.zw 를 곱하지 않습니다. zw 는 역수라 곱해야 합니다.");
        }

        [Test]
        public void 전역_이름_셋이_양쪽에서_같다()
        {
            string hlsl = Code(IncludePath);

            foreach (string name in new[] { "_GlobalSplatMap", "_GlobalSplatMapRect", "_GlobalSplatMapOn" })
                Assert.IsTrue(hlsl.Contains(name), "HLSL 에 " + name + " 이 없습니다.");
        }

        [Test]
        public void 지도가_없으면_젖지_않는다()
        {
            // 안 바인딩된 텍스처를 그냥 샘플하면 플랫폼마다 다른 값이 나와
            // <b>온 세상이 젖은 것처럼</b> 보입니다. 전역 스위치로 먼저 걸러야 합니다.
            string hlsl = Code(IncludePath);

            Assert.IsTrue(
                Regex.IsMatch(hlsl, @"_GlobalSplatMapOn\s*<\s*0\.5"),
                "지도가 없을 때 일찍 빠져나가는 길이 없습니다. 세상이 통째로 젖어 보입니다.");
        }

        /// <summary>
        /// 얼룩이 <b>켠 머티리얼만</b> 컴파일되는지 못박습니다.
        ///
        /// CarDriveToonLit 은 머티리얼 35개가 함께 씁니다. 무조건 컴파일하면 하늘·차 내부처럼
        /// 오줌이 닿을 수 없는 것까지 배리언트가 생기고, 셰이더를 고칠 때마다 재컴파일 범위가
        /// 그만큼 넓어집니다. 앞으로 벽을 여러 에셋으로 바꿔 나갈 때 그 마찰이 실제 비용입니다.
        ///
        /// 누가 <c>#ifdef</c> 를 지우면 컴파일도 되고 화면도 그대로라 <b>조용히 되돌아갑니다.</b>
        /// 그래서 셰이더 원문을 글자 그대로 읽어 봅니다.
        /// </summary>
        [Test]
        public void 벽_얼룩은_켠_머티리얼만_컴파일된다()
        {
            const string LitPath = "Assets/_Project/04.Art/03.Shaders/Toon/CarDriveToonLit.shader";
            string src = Read(LitPath);

            Assert.IsTrue(src.Contains("#pragma shader_feature_local_fragment _SPLAT_ON"),
                "옵트인 키워드가 없습니다. 35개 머티리얼이 전부 얼룩을 컴파일하게 됩니다.");

            Assert.IsTrue(Regex.IsMatch(src, @"\[Toggle\(_SPLAT_ON\)\]"),
                "인스펙터 토글이 없습니다. 새 에셋이 체크로 참여할 수 없습니다.");

            // 인클루드까지 가려야 켜지 않은 머티리얼이 그 파일을 아예 안 봅니다.
            Assert.IsTrue(Regex.IsMatch(src, @"#ifdef _SPLAT_ON\s+#include ""CarDriveSplatMap\.hlsl"""),
                "얼룩 인클루드가 키워드로 안 가려져 있습니다.");

            // 프래그먼트 본문도 가려야 합니다.
            Assert.IsTrue(src.Contains("CarDriveSplatStainTriplanar"),
                "벽이 삼중평면 얼룩을 안 읽습니다.");

            int stainAt = src.IndexOf("CarDriveSplatStainTriplanar");
            string before = src.Substring(0, stainAt);
            int lastIf = before.LastIndexOf("#ifdef _SPLAT_ON");
            int lastEnd = before.LastIndexOf("#endif");
            Assert.Greater(lastIf, lastEnd,
                "얼룩 계산이 키워드 밖에 있습니다. 켜지 않은 머티리얼도 컴파일합니다.");
        }

        [Test]
        public void 컴퓨트가_해상도를_스스로_가정하지_않는다()
        {
            // 해상도는 인스펙터에서 바뀝니다. 셰이더에 상수로 적어 두면 조용히 어긋납니다.
            string cs = Code(ComputePath);

            Assert.IsTrue(cs.Contains("_MapSize"), "컴퓨트가 _MapSize 를 받지 않습니다.");
            Assert.IsTrue(cs.Contains("PaintSplat") && cs.Contains("FadeSplat"),
                          "계약의 두 커널이 없습니다.");
        }

        [Test]
        public void 요청_구조체가_양쪽에서_같은_크기다()
        {
            // C# 은 16바이트로 잡아 ComputeBuffer stride 에 쓰고, 컴퓨트는 같은 순서로 읽습니다.
            // 어긋나면 값이 밀려 반경이 강도로, 강도가 좌표로 들어갑니다.
            string cs = Code(ComputePath);

            Match m = Regex.Match(cs, @"struct\s+SplatRequest\s*\{(.*?)\}", RegexOptions.Singleline);
            Assert.IsTrue(m.Success, "컴퓨트에서 SplatRequest 를 못 찾음");

            string body = m.Groups[1].Value;
            int bytes = Regex.Matches(body, @"\bfloat2\b").Count * 8
                      + Regex.Matches(body, @"\bfloat\b(?!\d)").Count * 4;

            Assert.AreEqual(16, bytes,
                "SplatRequest 가 16바이트가 아닙니다. C# 의 stride 와 어긋나 값이 밀립니다.");
        }
    }
}
