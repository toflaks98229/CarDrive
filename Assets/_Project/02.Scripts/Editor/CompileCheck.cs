using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;

namespace CarDrive.EditorTools
{
    /// <summary>
    /// 에디터를 열지 않고 <b>스크립트가 컴파일되는지만</b> 확인합니다.
    ///
    /// <see cref="BuildRunner"/>는 실제 플레이어를 굽느라 몇 분이 걸립니다.
    /// 리팩토링 중에 알고 싶은 것은 "빌드가 되는가"가 아니라 "컴파일이 되는가"뿐이라,
    /// 그 한 가지만 보고 즉시 끝냅니다.
    ///
    /// <c>Unity.exe -batchmode -quit -projectPath . -executeMethod CarDrive.EditorTools.CompileCheck.Run</c>
    ///
    /// 배치모드에서는 이 메서드가 불릴 때 이미 컴파일이 끝나 있습니다.
    /// 컴파일에 실패했다면 유니티가 여기까지 오지 않고 먼저 멈추므로,
    /// <b>이 로그가 찍혔다는 사실 자체가 컴파일 성공</b>입니다.
    /// 그래도 어셈블리 목록을 함께 남겨 두면 asmdef를 나눈 뒤 의도대로 갈라졌는지 눈으로 확인할 수 있습니다.
    /// </summary>
    public static class CompileCheck
    {
        /// <summary>
        /// 컴파일 결과와 어셈블리 구성을 로그로 남기고 종료합니다.
        /// </summary>
        public static void Run()
        {
            Assembly[] assemblies = CompilationPipeline.GetAssemblies(AssembliesType.Editor);

            int projectCount = 0;
            for (int i = 0; i < assemblies.Length; i++)
            {
                if (!assemblies[i].name.StartsWith("CarDrive")) continue;

                projectCount++;
                Debug.Log("ASSEMBLY: " + assemblies[i].name +
                          " | 소스 " + assemblies[i].sourceFiles.Length + "개" +
                          " | 참조 " + assemblies[i].assemblyReferences.Length + "개");
            }

            Debug.Log("COMPILE CHECK OK | CarDrive 어셈블리 " + projectCount + "개");
            EditorApplication.Exit(0);
        }
    }
}
