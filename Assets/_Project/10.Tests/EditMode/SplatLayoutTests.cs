using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using CarDrive.Gameplay;

namespace CarDrive.Tests
{
    /// <summary>
    /// 자국 판이 <b>그려야 할 것을 통째로 담는지</b>와, 셰이더가 판 좌표로 되돌아가지 않았는지
    /// 확인합니다.
    ///
    /// <b>왜 필요한가.</b> 사용자가 보고한 결함 — 웅덩이가 자랄 때 벽에 이미 흘러내린 줄기가
    /// 함께 커지고 자리까지 옮기는 것 — 의 원인은 무늬를 <b>판의 정규화 UV</b>로 그린 것이었습니다.
    /// 판은 자국이 자랄 때 함께 커지므로, UV 로 그린 모든 것이 물리적으로 확대됐습니다.
    ///
    /// 고침은 무늬를 <b>앵커 기준 미터</b>로 옮긴 것입니다. 그런데 이런 종류의 고침은
    /// 되돌아가기 쉽습니다 — 나중에 누가 편하다고 <c>input.uv</c> 를 한 줄 쓰면 결함이
    /// 절반 돌아오는데 컴파일도 되고 화면도 그럴듯합니다. 그래서 <b>셰이더 원문을 글자
    /// 그대로 읽어</b> 못박습니다.
    ///
    /// <b>왜 EditMode 인가.</b> <see cref="SplatQuadLayout"/> 은 순수 static 이라 씬도
    /// 그래픽 장치도 필요 없습니다. 셰이더는 파일로 읽습니다.
    /// </summary>
    public class SplatLayoutTests
    {
        private const string ShaderPath =
            "Assets/_Project/04.Art/03.Shaders/Toon/CarDriveSplat.shader";

        private static string ReadShader()
        {
            Assert.IsTrue(File.Exists(ShaderPath), "셰이더를 못 찾음: " + ShaderPath);
            return File.ReadAllText(ShaderPath);
        }

        /// <summary>
        /// 셰이더 원문에서 프로퍼티 기본값을 읽습니다. C# 이 숫자를 따로 들지 않게.
        ///
        /// 선언은 <c>_EdgeBite ("...", Range(0, 1)) = 0.55</c> 꼴이라 괄호가 겹칩니다.
        /// 안쪽 괄호에서 멈추지 않게 <b>줄 끝의 = 값</b>을 잡습니다.
        /// </summary>
        private static float ShaderDefault(string source, string property)
        {
            Match m = Regex.Match(source,
                Regex.Escape(property) + @"\s*\(.*?\)\s*=\s*([\d.]+)");
            Assert.IsTrue(m.Success, "셰이더에서 " + property + " 기본값을 못 읽음");
            return float.Parse(m.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// 주석을 걷어낸 셰이더 원문입니다.
        ///
        /// 없앤 프로퍼티를 찾을 때는 <b>주석을 세면 안 됩니다.</b> 왜 없앴는지 적어 둔
        /// 설명에 그 이름이 나오는 것은 당연하고, 오히려 남아 있어야 합니다.
        /// </summary>
        private static string ReadShaderCode()
        {
            return Regex.Replace(ReadShader(), "//.*", string.Empty);
        }

        [Test]
        public void 판은_갉힌_테두리까지_통째로_담는다()
        {
            // 갉기가 몸통을 밖으로 밀어내는데 판이 딱 몸통 크기면, 아직 진하게 남은
            // 테두리가 판 변에 부딪혀 <b>직선으로 끊깁니다.</b> 예전 코드가 그랬습니다.
            float edgeBite = ShaderDefault(ReadShader(), "_EdgeBite");

            foreach (float r in new[] { 0.07f, 0.2f, 0.5f, 0.7f })
            {
                float side, down, offset;
                SplatQuadLayout.Measure(r, edgeBite, 0f, 0f, 0f, out side, out down, out offset);

                Assert.GreaterOrEqual(side, r * (1.05f + edgeBite * 0.5f) - 1e-5f,
                    "반지름 " + r + " 에서 판이 갉힌 테두리를 못 담습니다. 자국이 네모로 잘립니다.");
            }
        }

        [Test]
        public void 판은_가장_긴_줄기를_담는다()
        {
            // 셰이더가 줄기 끝을 tipAt = _DripReach * _DripAmount * (0.3 + lane * 1.2) 로 정하므로
            // lane 이 1 일 때 1.5 배가 최대입니다. 이 값이 어긋나면 줄기 끝이 잘립니다.
            string src = ReadShader();
            Assert.IsTrue(src.Contains("(0.3 + lane * 1.2)"),
                "셰이더의 줄기 끝 식이 바뀌었습니다. SplatQuadLayout.DripReachPeak 도 함께 고치세요.");
            Assert.AreEqual(1.5f, SplatQuadLayout.DripReachPeak, 1e-5f);

            float edgeBite = ShaderDefault(src, "_EdgeBite");

            foreach (float reach in new[] { 0.05f, 0.4f, 1.2f })
            {
                float side, down, offset;
                SplatQuadLayout.Measure(0.2f, edgeBite, 0.03f, reach, 1f, out side, out down, out offset);

                Assert.GreaterOrEqual(down, 0.03f + reach * SplatQuadLayout.DripReachPeak,
                    "줄기 길이 " + reach + " 가 판 밖으로 잘립니다.");
            }
        }

        [Test]
        public void 앵커는_늘_판_좌표_bodyOffsetY_에_있다()
        {
            // 셰이더가 posMS.y = (p.y - _BodyOffsetY) * halfH 로 앵커를 원점으로 삼습니다.
            // 이 값이 틀리면 자국 전체가 닿은 자리에서 위아래로 어긋납니다.
            foreach (float r in new[] { 0.07f, 0.3f, 0.7f })
            {
                foreach (float reach in new[] { 0f, 0.2f, 1.2f })
                {
                    float drip = reach > 0f ? 1f : 0f;

                    Vector2 size;
                    float centerLift, offset;
                    SplatQuadLayout.Resolve(r, 0.55f, 0.03f, reach, drip,
                                            out size, out centerLift, out offset);

                    // 판 중심에서 위로 offset * (세로/2) 만큼이 앵커여야 합니다.
                    float anchorFromCenter = offset * size.y * 0.5f;
                    Assert.AreEqual(0f, centerLift + anchorFromCenter, 1e-4f,
                        "앵커가 판 좌표 " + offset + " 에 안 놓입니다 (r=" + r + ", reach=" + reach + ")");
                }
            }
        }

        [Test]
        public void bodyOffsetY_가_셰이더_범위를_안_넘는다()
        {
            // <b>가장 찾기 어려운 종류의 어긋남입니다.</b> MaterialPropertyBlock 은 Range 를
            // 안 자르지만 Material 은 자릅니다. 넘치면 게임은 멀쩡한데 캡처 도구만
            // 틀린 그림을 내놓습니다.
            Match m = Regex.Match(ReadShader(), @"_BodyOffsetY\s*\([^)]*Range\(([\d.]+),\s*([\d.]+)\)");
            Assert.IsTrue(m.Success, "셰이더에서 _BodyOffsetY 의 Range 를 못 읽음");
            float hi = float.Parse(m.Groups[2].Value, System.Globalization.CultureInfo.InvariantCulture);

            // 가장 극단: 작은 몸통 + 가장 긴 줄기
            float side, down, offset;
            SplatQuadLayout.Measure(0.01f, 1f, 0.6f, 2.5f, 1f, out side, out down, out offset);

            Assert.LessOrEqual(offset, hi,
                "_BodyOffsetY 가 " + offset + " 까지 나오는데 셰이더 Range 상한이 " + hi + " 입니다. " +
                "Material 경로에서 조용히 잘립니다.");
        }

        [Test]
        public void 셰이더가_판_좌표로_무늬를_읽지_않는다()
        {
            // 프래그먼트에 판 UV 가 아예 오면 안 됩니다. 오는 순간 되돌아갈 길이 열립니다.
            string src = ReadShaderCode();
            int fragAt = src.IndexOf("half4 frag(");
            Assert.Greater(fragAt, 0, "frag 를 못 찾음");

            string frag = src.Substring(fragAt);
            Assert.IsFalse(frag.Contains("input.uv"),
                "프래그먼트가 판 UV 를 읽습니다. 자국이 자랄 때 무늬가 함께 확대되는 결함이 돌아옵니다.");
            Assert.IsTrue(frag.Contains("input.posMS"),
                "프래그먼트가 앵커 기준 미터(posMS)를 안 씁니다.");
        }

        [Test]
        public void 없앤_프로퍼티가_되살아나지_않았다()
        {
            // 하나라도 살아 있으면 반쯤 되돌린 상태이고, 결함이 절반 돌아옵니다.
            string src = ReadShaderCode();
            foreach (string dead in new[] { "_Grow", "_Aspect", "_DripLength", "_DripWidth" })
                Assert.IsFalse(Regex.IsMatch(src, @"(?<![A-Za-z_])" + Regex.Escape(dead) + @"(?![A-Za-z_])"),
                    dead + " 가 셰이더에 남아 있습니다. 판 상대 단위로 되돌아간 흔적입니다.");
        }

        [Test]
        public void 줄기는_몸통_크기를_안_탄다()
        {
            // <b>이것이 사용자가 보고한 결함의 핵심입니다.</b> 줄기의 자리·머리·굵기·길이는
            // 전부 미터여야 합니다. 몸통 반지름(rBody)을 타도 되는 것은 "몸통 폭 안에서만
            // 시작한다"는 within 하나뿐이고, 그것은 단조 증가라 있던 줄기를 못 움직입니다.
            //
            // <b>블록의 경계는 바뀌지 않는 줄로 잡습니다.</b> 검사 대상이 되는 줄로 잡으면,
            // 결함이 돌아왔을 때 "블록을 못 찾음" 으로 실패해서 <b>무엇이 잘못됐는지</b>가
            // 안 보입니다. 실제로 한 번 그렇게 나왔습니다.
            string src = ReadShaderCode();

            int dripAt = src.IndexOf("float pitch = max(_DripPitch");
            Assert.Greater(dripAt, 0, "줄기 블록의 시작을 못 찾음");

            int dripEnd = src.IndexOf("raw += taper * within", dripAt);
            Assert.Greater(dripEnd, dripAt, "줄기 블록의 끝을 못 찾음");

            string block = src.Substring(dripAt, dripEnd - dripAt);

            Assert.IsTrue(block.Contains("_DripReach"),
                "줄기 길이가 _DripReach 로 정해지지 않습니다. 몸통에 매여 있으면 " +
                "웅덩이가 자랄 때 이미 흘러내린 줄기가 함께 길어집니다.");

            int uses = Regex.Matches(block, @"(?<![A-Za-z_])rBody(?![A-Za-z_])").Count;
            Assert.AreEqual(1, uses,
                "줄기 계산이 몸통 반지름을 " + uses + " 번 씁니다. within 하나만 써야 " +
                "웅덩이가 자라도 이미 그어진 줄기가 안 움직입니다.");
        }

        [Test]
        public void 줄기_머리는_스탬프_때_못박은_미터다()
        {
            // 머리가 몸통 반지름의 배수면, 웅덩이가 자랄 때마다 이미 그어진 줄기의
            // <b>머리가 아래로 내려갑니다.</b> 사용자가 본 "자리가 달라진다"의 절반이 이것입니다.
            string src = ReadShaderCode();
            Assert.IsTrue(src.Contains("float below = -mm.y - _DripStart;"),
                "줄기 머리가 _DripStart(스탬프 때 못박은 미터)로 정해지지 않습니다.");
        }
    }
}
