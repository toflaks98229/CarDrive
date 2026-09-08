using UnityEditor;
using UnityEngine;

/// <summary>
/// 셰이더를 <b>강제로 다시 임포트해</b> 컴파일 오류를 확인합니다. 확인용 임시 도구입니다.
///
/// <b>왜 필요한가.</b> 배치모드로 프로젝트를 열기만 하면 셰이더는 다시 컴파일되지 않습니다.
/// 이미 만들어 둔 임포트 결과가 있으면 그대로 쓰기 때문입니다. 그래서 셰이더를 고쳐 놓고
/// 테스트가 통과하는 것을 보면 <b>고친 셰이더가 검사됐다고 착각하게</b> 됩니다.
///
/// 오류의 본문은 유니티가 임포트하면서 로그에 직접 찍습니다("Shader error in ...").
/// 여기서는 <b>있는지 없는지</b>만 판정해 종료 코드로 알립니다.
///
/// 쓰는 법:
///   Unity.exe -batchmode -nographics -projectPath . -executeMethod ShaderReimportCheck.Run
/// </summary>
public static class ShaderReimportCheck
{
    /// <summary>확인할 셰이더들입니다.</summary>
    private static readonly string[] Targets =
    {
        "Assets/_Project/04.Art/03.Shaders/Toon/CarDriveToonLit.shader",
        "Assets/_Project/04.Art/03.Shaders/Toon/CarDriveToonGlass.shader",
        "Assets/_Project/04.Art/03.Shaders/LowPoly/LowPolyGrass.shader",
        "Assets/_Project/04.Art/03.Shaders/Toon/CarDriveToonTerrain.shader",
        "Assets/_Project/04.Art/03.Shaders/Toon/CarDriveNeedGauge.shader",
        "Assets/_Project/04.Art/03.Shaders/Toon/CarDriveSplat.shader",
    };

    /// <summary>강제 재임포트하고 오류 여부를 찍습니다. 하나라도 오류면 1로 끝냅니다.</summary>
    public static void Run()
    {
        int failed = 0;

        for (int i = 0; i < Targets.Length; i++)
        {
            string path = Targets[i];

            AssetDatabase.ImportAsset(path,
                ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);

            Shader shader = AssetDatabase.LoadAssetAtPath<Shader>(path);
            if (shader == null)
            {
                Debug.Log("SHADERCHECK 찾지 못함: " + path);
                failed++;
                continue;
            }

            bool hasError = ShaderUtil.ShaderHasError(shader);

            Debug.Log("SHADERCHECK " + (hasError ? "오류 " : "통과 ") + path);

            if (hasError) failed++;
        }

        Debug.Log("SHADERCHECK 끝. 문제 " + failed + " 건");

        EditorApplication.Exit(failed == 0 ? 0 : 1);
    }
}
