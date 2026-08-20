using System.Collections.Generic;

namespace CarDrive.EditorTools
{
    /// <summary>
    /// 도구 하나를 설명하는 항목입니다.
    ///
    /// <b>실행은 메뉴 경로로 합니다.</b> 도구 클래스를 직접 참조하지 않으므로
    /// 패널이 스물여덟 개 클래스를 알 필요가 없고, 도구를 하나 늘려도
    /// 이 표에 한 줄 추가하면 끝입니다.
    /// </summary>
    public struct CarDriveTool
    {
        /// <summary>실행할 메뉴 경로입니다.</summary>
        public string MenuPath;

        /// <summary>버튼에 쓸 짧은 이름입니다.</summary>
        public string Label;

        /// <summary>이 도구가 무엇을 하는지 한 줄 설명입니다.</summary>
        public string Description;

        /// <summary>
        /// 되돌리기 어려운 작업인지 여부입니다.
        /// 지형을 다시 굽거나 배치를 지우는 것처럼, 누르기 전에 한 번 더 묻는 편이 나은 것들입니다.
        /// </summary>
        public bool NeedsConfirm;
    }

    /// <summary>
    /// 도구를 묶는 단위입니다. 패널이 이 순서대로 그립니다.
    /// </summary>
    public struct CarDriveToolGroup
    {
        /// <summary>묶음 이름입니다.</summary>
        public string Title;

        /// <summary>이 묶음을 언제 쓰는지 알려 주는 한두 줄입니다.</summary>
        public string Hint;

        /// <summary>
        /// 위에서 아래로 <b>순서대로</b> 눌러야 하는 묶음인지 여부입니다.
        /// 켜면 버튼에 번호가 붙습니다.
        /// </summary>
        public bool Ordered;

        /// <summary>이 묶음에 속한 도구들입니다.</summary>
        public CarDriveTool[] Tools;
    }

    /// <summary>
    /// 흩어져 있던 도구들을 <b>작업 순서대로</b> 모아 둔 목록입니다.
    ///
    /// <b>왜 만들었는가.</b> 도구가 스물여덟 개인데 <c>CarDrive</c> 아래 네 갈래
    /// (World · Look · Gameplay · Feel)로 흩어져 있었습니다. 메뉴는 알파벳순이라
    /// <b>무엇을 먼저 눌러야 하는지가 드러나지 않고</b>, 이름만 보고는 무엇이 바뀌는지도 알 수 없었습니다.
    /// 실제로 월드를 만들려면 산 → 프리팹 → 흩뿌리기 → 마을 → 굽기 순으로 눌러야 하는데,
    /// 그 순서는 아무 데도 적혀 있지 않았습니다.
    ///
    /// 여기서는 <b>순서와 설명을 데이터로</b> 적어 둡니다.
    /// 메뉴 항목은 그대로 두었으니 익숙한 쪽으로 쓰셔도 됩니다.
    /// </summary>
    public static class CarDriveToolCatalog
    {
        // --- Public Methods ---

        /// <summary>
        /// 모든 도구 묶음을 순서대로 돌려줍니다.
        /// </summary>
        /// <returns>패널이 위에서 아래로 그릴 묶음 목록</returns>
        public static List<CarDriveToolGroup> CreateGroups()
        {
            return new List<CarDriveToolGroup>
            {
                new CarDriveToolGroup
                {
                    Title = "월드 만들기",
                    Hint = "빈 씬에서 세계를 처음 세울 때 위에서 아래로 누릅니다.\n" +
                           "이미 만들어진 월드가 있다면 다시 누를 필요가 없습니다.",
                    Ordered = true,
                    Tools = new[]
                    {
                        new CarDriveTool
                        {
                            MenuPath = "CarDrive/World/0. 산 만들기",
                            Label = "산 만들기",
                            Description = "지형의 큰 굴곡을 찍습니다. 가장 먼저 합니다."
                        },
                        new CarDriveTool
                        {
                            MenuPath = "CarDrive/World/1. 바위 · 건물 프리팹 만들기",
                            Label = "바위 · 건물 프리팹 만들기",
                            Description = "흩뿌릴 재료를 프리팹으로 굽습니다."
                        },
                        new CarDriveTool
                        {
                            MenuPath = "CarDrive/World/2. 나무 · 바위 흩뿌리기",
                            Label = "나무 · 바위 흩뿌리기",
                            Description = "지형 위에 재료를 뿌립니다. 도로 위는 피해 갑니다."
                        },
                        new CarDriveTool
                        {
                            MenuPath = "CarDrive/World/3. 마을 세우기",
                            Label = "마을 세우기",
                            Description = "중심 마을을 배치합니다."
                        },
                        new CarDriveTool
                        {
                            MenuPath = "CarDrive/World/터레인 월드 굽기",
                            Label = "터레인 월드 굽기",
                            Description = "지형 타일을 에셋으로 구워 냅니다. 오래 걸립니다.",
                            NeedsConfirm = true
                        }
                    }
                },

                new CarDriveToolGroup
                {
                    Title = "룩 전환",
                    Hint = "지면·풀·하늘·색보정을 한 세트로 바꿉니다.\n" +
                           "셋 중 하나만 적용된 상태로 두세요. 섞으면 지면과 풀의 색이 어긋납니다.",
                    Tools = new[]
                    {
                        new CarDriveTool
                        {
                            MenuPath = "CarDrive/World/PSX 룩으로 전환",
                            Label = "PSX 룩",
                            Description = "저해상도 픽셀 질감. 지형을 성기게 만듭니다.",
                            NeedsConfirm = true
                        },
                        new CarDriveTool
                        {
                            MenuPath = "CarDrive/World/로우폴리 코지 룩으로 전환",
                            Label = "로우폴리 코지 룩",
                            Description = "플랫 셰이딩에 부드러운 색. 지면과 풀을 함께 갈아 끼웁니다.",
                            NeedsConfirm = true
                        },
                        new CarDriveTool
                        {
                            MenuPath = "CarDrive/Look/툰 룩 적용",
                            Label = "툰 룩",
                            Description = "램프 기반 셰이딩. 적용 전에 아래에서 램프를 구워야 합니다.",
                            NeedsConfirm = true
                        },
                        new CarDriveTool
                        {
                            MenuPath = "CarDrive/Look/툰 램프 굽기",
                            Label = "툰 램프 굽기",
                            Description = "툰 룩이 쓰는 그라데이션 텍스처를 만듭니다."
                        },
                        new CarDriveTool
                        {
                            MenuPath = "CarDrive/Look/툰 룩 되돌리기",
                            Label = "툰 룩 되돌리기",
                            Description = "툰 적용 전 상태로 머티리얼을 돌립니다."
                        }
                    }
                },

                new CarDriveToolGroup
                {
                    Title = "하늘과 조명",
                    Hint = "시간대·날씨 연출이 기대는 배선입니다.",
                    Tools = new[]
                    {
                        new CarDriveTool
                        {
                            MenuPath = "CarDrive/World/하늘과 조명 설정",
                            Label = "하늘과 조명 배선",
                            Description = "태양·하늘 컨트롤러를 씬에 연결합니다."
                        },
                        new CarDriveTool
                        {
                            MenuPath = "CarDrive/Look/맑은 하늘 적용",
                            Label = "맑은 하늘 (HDRI)",
                            Description = "HDRI 스카이박스로 바꿉니다."
                        },
                        new CarDriveTool
                        {
                            MenuPath = "CarDrive/Look/하늘 되돌리기 (절차적 하늘)",
                            Label = "절차적 하늘로 되돌리기",
                            Description = "시간에 따라 색이 변하는 기본 하늘로 돌립니다."
                        },
                        new CarDriveTool
                        {
                            MenuPath = "CarDrive/Look/구름 그림자 설정",
                            Label = "구름 그림자 배선",
                            Description = "지면에 흐르는 구름 그림자를 겁니다."
                        }
                    }
                },

                new CarDriveToolGroup
                {
                    Title = "성능",
                    Hint = "프레임이 떨어질 때 이쪽부터 봅니다.\n" +
                           "점검을 먼저 눌러 지금 상태를 확인한 뒤 적용하세요.",
                    Tools = new[]
                    {
                        new CarDriveTool
                        {
                            MenuPath = "CarDrive/World/터레인 렌더링 설정 점검",
                            Label = "터레인 설정 점검",
                            Description = "타일 103장의 렌더링 설정을 표로 찍습니다. 아무것도 바꾸지 않습니다."
                        },
                        new CarDriveTool
                        {
                            MenuPath = "CarDrive/World/터레인 렌더링 최적화 적용",
                            Label = "터레인 렌더링 최적화",
                            Description = "지형 단순화·빌보드 전환 거리를 모든 타일에 적용합니다."
                        },
                        new CarDriveTool
                        {
                            MenuPath = "CarDrive/World/터레인 그림자 단면으로 (확인 필요)",
                            Label = "터레인 그림자 단면으로",
                            Description = "그림자 지오메트리가 절반이 됩니다. 적용 후 새벽·석양을 확인하세요.",
                            NeedsConfirm = true
                        },
                        new CarDriveTool
                        {
                            MenuPath = "CarDrive/Look/시야 거리 배선",
                            Label = "시야 거리 배선",
                            Description = "안개·카메라 far·터레인 거리를 한 벌로 맞춥니다."
                        },
                        new CarDriveTool
                        {
                            MenuPath = "CarDrive/Look/시야 거리 검증",
                            Label = "시야 거리 검증",
                            Description = "거리 설정이 서로 어긋나지 않는지 봅니다."
                        }
                    }
                },

                new CarDriveToolGroup
                {
                    Title = "게임플레이 배선",
                    Hint = "씬의 오브젝트에 컴포넌트와 참조를 이어 줍니다.\n" +
                           "이미 배선된 씬에 다시 눌러도 안전합니다.",
                    Tools = new[]
                    {
                        new CarDriveTool
                        {
                            MenuPath = "CarDrive/Gameplay/경제 배선 설정 (돈 · 엑토플라즘)",
                            Label = "경제 배선",
                            Description = "지갑·재화 UI·드롭을 연결합니다."
                        },
                        new CarDriveTool
                        {
                            MenuPath = "CarDrive/Gameplay/음료 배선 설정",
                            Label = "음료 배선",
                            Description = "음료·상자·마시기 연출을 연결합니다."
                        },
                        new CarDriveTool
                        {
                            MenuPath = "CarDrive/World/풀 눕히기 배선",
                            Label = "풀 눕히기 배선",
                            Description = "바퀴·발·유령에 누르개를 붙입니다."
                        },
                        new CarDriveTool
                        {
                            MenuPath = "CarDrive/Feel/레시피 만들기 (적 · 엑토플라즘 · 재화 · 니즈 게이지)",
                            Label = "Feel 레시피 만들기",
                            Description = "타격·획득·게이지 연출을 MMF_Player 로 만듭니다."
                        }
                    }
                },

                new CarDriveToolGroup
                {
                    Title = "점검",
                    Hint = "아무것도 바꾸지 않습니다. 결과는 Console 에 남습니다.",
                    Tools = new[]
                    {
                        new CarDriveTool
                        {
                            MenuPath = "CarDrive/World/4. 월드 점검",
                            Label = "월드 점검",
                            Description = "타일·장소·배치가 제대로 서 있는지 봅니다."
                        },
                        new CarDriveTool
                        {
                            MenuPath = "CarDrive/Gameplay/경제 배선 점검",
                            Label = "경제 배선 점검",
                            Description = "지갑과 재화 연결이 빠진 곳을 찾습니다."
                        },
                        new CarDriveTool
                        {
                            MenuPath = "CarDrive/World/심긴 풀 검사 (도로에 심기지 않았는지)",
                            Label = "심긴 풀 검사",
                            Description = "도로 위에 풀이 심기지 않았는지 봅니다. 씬을 렌더링하므로 시간이 걸립니다."
                        },
                        new CarDriveTool
                        {
                            MenuPath = "CarDrive/World/누르개 검사 (무엇이 자국을 남기는지)",
                            Label = "누르개 검사",
                            Description = "무엇이 풀에 자국을 남기는지 목록으로 찍습니다."
                        },
                        new CarDriveTool
                        {
                            MenuPath = "CarDrive/World/그리기 비용 세기 (드로우 콜 · 삼각형)",
                            Label = "그리기 비용 세기",
                            Description = "드로우 콜과 삼각형 수를 셉니다. 밀리초와 달리 흔들리지 않는 값입니다."
                        }
                    }
                },

                new CarDriveToolGroup
                {
                    Title = "다시 만들기",
                    Hint = "이미 있는 것을 지우고 다시 만듭니다. 되돌리기 어렵습니다.",
                    Tools = new[]
                    {
                        new CarDriveTool
                        {
                            MenuPath = "CarDrive/World/지면 텍스처 다시 만들기",
                            Label = "지면 텍스처 다시 만들기",
                            Description = "픽셀 지면 텍스처를 새로 굽습니다.",
                            NeedsConfirm = true
                        },
                        new CarDriveTool
                        {
                            MenuPath = "CarDrive/World/지면 단장 다시 입히기",
                            Label = "지면 단장 다시 입히기",
                            Description = "스플랫과 디테일을 다시 칠합니다.",
                            NeedsConfirm = true
                        },
                        new CarDriveTool
                        {
                            MenuPath = "CarDrive/World/0. 산 지우기",
                            Label = "산 지우기",
                            Description = "찍어 둔 굴곡을 평평하게 되돌립니다.",
                            NeedsConfirm = true
                        }
                    }
                }
            };
        }
    }
}
