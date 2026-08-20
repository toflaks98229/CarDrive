using System;
using System.Collections.Generic;
using NUnit.Framework;
using CarDrive.Systems;

namespace CarDrive.Tests
{
    /// <summary>
    /// 니즈 기본값에 대한 EditMode 테스트입니다.
    ///
    /// NeedDefaults는 씬도 프레임도 필요 없는 순수 정적 메서드라 가장 먼저 검증할 수 있습니다.
    /// 이 파일은 테스트 어셈블리(CarDrive.Tests.EditMode)가 CarDrive.Runtime의 타입을
    /// 실제로 참조할 수 있는지 확인하는 역할도 겸합니다.
    /// </summary>
    public class NeedDefaultsTests
    {
        /// <summary>
        /// NeedType에 값을 추가하고 기본 설정을 빠뜨리면 NeedsSystem이 실행 중에 경고를 내며
        /// 임시 기본값으로 메웁니다. 그 실수를 실행 전에 잡습니다.
        /// </summary>
        [Test]
        public void CreateSettings_모든_니즈_종류를_포함한다()
        {
            List<NeedSetting> settings = NeedDefaults.CreateSettings();

            foreach (NeedType type in Enum.GetValues(typeof(NeedType)))
            {
                bool found = settings.Exists(s => s.type == type);
                Assert.IsTrue(found, "기본 설정에 " + type + " 가 없습니다.");
            }
        }

        /// <summary>
        /// 호출할 때마다 새 인스턴스를 만들어야 합니다.
        /// 같은 목록을 돌려주면 NeedsSystem이 수치를 고칠 때 다음 호출까지 오염됩니다.
        /// </summary>
        [Test]
        public void CreateSettings_호출할_때마다_새_인스턴스를_만든다()
        {
            List<NeedSetting> first = NeedDefaults.CreateSettings();
            List<NeedSetting> second = NeedDefaults.CreateSettings();

            Assert.AreNotSame(first, second);
            Assert.AreNotSame(first[0], second[0]);
        }

        /// <summary>
        /// 연쇄 규칙의 원인·대상이 실제 니즈 종류를 가리키고, 자기 자신을 가리키지 않아야 합니다.
        /// 자기 참조는 증가량이 스스로를 밀어 올리는 폭주가 됩니다.
        /// </summary>
        [Test]
        public void CreateCouplings_자기_자신을_가리키지_않는다()
        {
            List<NeedCoupling> couplings = NeedDefaults.CreateCouplings();

            Assert.IsNotEmpty(couplings);
            for (int i = 0; i < couplings.Count; i++)
            {
                Assert.AreNotEqual(couplings[i].source, couplings[i].target,
                    "연쇄 규칙 " + i + " 가 자기 자신을 가리킵니다.");
            }
        }

        /// <summary>
        /// 경고 임계는 한계(overflowLimit)보다 반드시 낮아야 합니다.
        /// 뒤집히면 경고 없이 곧바로 처벌이 들어옵니다.
        /// </summary>
        [Test]
        public void CreateSettings_경고_임계가_한계보다_낮다()
        {
            List<NeedSetting> settings = NeedDefaults.CreateSettings();

            for (int i = 0; i < settings.Count; i++)
            {
                NeedSetting s = settings[i];
                Assert.Less(s.warnThreshold, s.overflowLimit,
                    s.displayName + " 의 경고 임계가 한계보다 높습니다.");
            }
        }
    }
}
