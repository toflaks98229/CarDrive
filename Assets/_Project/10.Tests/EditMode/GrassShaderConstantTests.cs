using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using CarDrive.Systems;

namespace CarDrive.Tests
{
    /// <summary>
    /// C# 과 셰이더가 <b>같은 배열 크기</b>를 쓰고 있는지 고정합니다.
    ///
    /// <b>왜 이 테스트인가.</b> 이 수들은 양쪽에 따로 적혀 있었고, 한쪽만 고쳐도
    /// <b>컴파일이 통과합니다.</b> C# 이 더 크면 넘긴 자리 일부가 조용히 버려지고,
    /// 셰이더가 더 크면 채워지지 않은 칸을 읽어 엉뚱한 자리가 눌립니다.
    /// 어느 쪽이든 "가끔 이상하다"로만 보여서 원인에 닿기가 어렵습니다.
    ///
    /// 컴파일러도 런타임도 잡아 주지 않는 종류의 어긋남이라, 잡을 수 있는 자리가
    /// 여기뿐입니다. 셰이더 쪽 숫자는 <c>GrassShared.hlsl</c> 한곳으로 모아 두었고,
    /// 이 테스트는 그 파일을 <b>글자 그대로 읽어</b> C# 상수와 견줍니다.
    ///
    /// <b>에디터 도구로 생성하지 않는 이유.</b> 커밋 <c>1170605</c> 가
    /// <c>CarDrive.Editor</c> 어셈블리를 통째로 걷어냈고, 그 뒤로 이 프로젝트의 확인은
    /// 배치모드 테스트로 합니다. 생성기를 되살리는 것보다 <b>어긋나면 테스트가 빨개지는</b>
    /// 편이 지금 구조에 맞습니다.
    /// </summary>
    public class GrassShaderConstantTests
    {
        /// <summary>셰이더들이 있는 폴더입니다.</summary>
        private const string ShaderFolder = "_Project/04.Art/03.Shaders/LowPoly/";

        /// <summary>공용 상수를 담아 둔 헤더입니다.</summary>
        private const string SharedHeader = ShaderFolder + "GrassShared.hlsl";

        /// <summary>
        /// 프로젝트 안의 파일을 읽습니다.
        /// </summary>
        /// <param name="relativePath">Assets 아래의 상대 경로</param>
        /// <returns>파일 내용</returns>
        private static string ReadAsset(string relativePath)
        {
            string full = Path.Combine(Application.dataPath, relativePath);

            Assert.IsTrue(File.Exists(full), "파일을 찾지 못했습니다: " + relativePath);

            return File.ReadAllText(full);
        }

        /// <summary>
        /// 헤더에서 <c>#define 이름 값</c> 의 값을 읽습니다.
        /// </summary>
        /// <param name="source">헤더 내용</param>
        /// <param name="name">찾을 이름</param>
        /// <returns>적혀 있는 정수</returns>
        private static int ReadDefine(string source, string name)
        {
            Match match = Regex.Match(source, @"^\s*#define\s+" + name + @"\s+(\d+)\s*$",
                                      RegexOptions.Multiline);

            Assert.IsTrue(match.Success, "GrassShared.hlsl 에서 " + name + " 을(를) 찾지 못했습니다.");

            return int.Parse(match.Groups[1].Value);
        }

        /// <summary>
        /// 누르개 배열의 크기가 양쪽에서 같아야 합니다.
        ///
        /// C# 이 더 크면 <see cref="GrassPushField"/> 가 채운 뒤쪽 자리를 셰이더가 읽지 못해
        /// 그 누르개들이 <b>조용히 사라집니다.</b> 반대면 채워지지 않은 칸을 읽습니다.
        /// </summary>
        [Test]
        public void 누르개_배열_크기가_셰이더와_같다()
        {
            int inShader = ReadDefine(ReadAsset(SharedHeader), "GRASS_PUSHER_MAX");

            Assert.AreEqual(GrassPushField.MaxPushers, inShader,
                "GrassPushField.MaxPushers 와 GrassShared.hlsl 의 GRASS_PUSHER_MAX 가 다릅니다.");
        }

        /// <summary>
        /// 자국 선분 배열의 크기가 양쪽에서 같아야 합니다.
        /// </summary>
        [Test]
        public void 자국_선분_배열_크기가_셰이더와_같다()
        {
            int inShader = ReadDefine(ReadAsset(SharedHeader), "TRAMPLE_SEGMENT_MAX");

            Assert.AreEqual(GrassTrampleMap.MaxSegments, inShader,
                "GrassTrampleMap.MaxSegments 와 GrassShared.hlsl 의 TRAMPLE_SEGMENT_MAX 가 다릅니다.");
        }

        /// <summary>
        /// 셰이더들이 <b>공용 헤더를 실제로 쓰고 있어야</b> 합니다.
        ///
        /// <b>이 테스트가 없으면 위의 둘이 거짓 안심이 됩니다.</b> 누군가 셰이더 안에
        /// <c>#define</c> 을 다시 적으면 그쪽이 이기는데, 헤더는 여전히 C# 과 맞으므로
        /// 위의 두 테스트는 <b>초록으로 통과합니다.</b> 값은 어긋났는데 아무도 모릅니다.
        ///
        /// 그래서 "헤더를 include 하는가"와 "직접 적어 두지 않았는가"를 함께 봅니다.
        /// </summary>
        /// <param name="shaderName">확인할 셰이더 파일 이름</param>
        /// <param name="defineName">그 셰이더에 다시 적혀 있으면 안 되는 이름</param>
        [TestCase("LowPolyGrass.shader", "GRASS_PUSHER_MAX")]
        [TestCase("GrassTrampleMap.shader", "TRAMPLE_SEGMENT_MAX")]
        public void 셰이더가_공용_헤더를_쓴다(string shaderName, string defineName)
        {
            string source = ReadAsset(ShaderFolder + shaderName);

            StringAssert.Contains("#include \"GrassShared.hlsl\"", source,
                shaderName + " 이(가) 공용 헤더를 포함하지 않습니다.");

            bool redefined = Regex.IsMatch(source, @"^\s*#define\s+" + defineName + @"\s+\d+",
                                           RegexOptions.Multiline);

            Assert.IsFalse(redefined,
                shaderName + " 이(가) " + defineName + " 을(를) 직접 다시 적었습니다. " +
                "그러면 공용 헤더가 무시되고, 헤더만 보는 테스트는 통과해 버립니다.");
        }
    }
}
