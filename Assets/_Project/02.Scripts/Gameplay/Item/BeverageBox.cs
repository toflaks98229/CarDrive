using UnityEngine;
using System.Collections.Generic; // List를 사용하기 위해 필요합니다.

/// <summary>
/// 게임 시작 시 자신의 자식 오브젝트들을 검사하여,
/// 'Beverage' 컴포넌트를 가진 오브젝트들을 찾아 리스트에 저장합니다.
/// 또한 상호작용 시 음료를 하나씩 제거하는 기능을 제공합니다.
/// </summary>
public class BeverageBox : MonoBehaviour
{
    // --- Public Member Variables ---

    [Header("찾은 음료 목록")]
    [Tooltip("시작할 때 자동으로 채워지는 음료(Beverage) 리스트입니다. (읽기 전용)")]
    // [SerializeField] // 인스펙터에서 보려면 이 속성을 추가할 수 있습니다.
    public List<Beverage> foundBeverages;

    // --- Unity Event Functions ---

    /// <summary>
    /// Start보다 먼저, 스크립트 인스턴스가 로드될 때 호출됩니다.
    /// </summary>
    void Awake()
    {
        // 리스트를 초기화합니다.
        foundBeverages = new List<Beverage>();

        // 이 오브젝트의 모든 '직계' 자식 Transform을 순회합니다.
        foreach (Transform child in transform)
        {
            // 자식 오브젝트에서 'Beverage' 컴포넌트를 가져옵니다.
            Beverage beverage = child.GetComponent<Beverage>();

            // 'Beverage' 컴포넌트가 존재한다면 (null이 아니라면)
            if (beverage != null)
            {
                // 찾은 음료를 리스트에 추가합니다.
                foundBeverages.Add(beverage);
            }
        }

        // (선택 사항) 몇 개를 찾았는지 로그를 출력합니다.
        Debug.Log(gameObject.name + "가 " + foundBeverages.Count + "개의 음료를 찾았습니다.");
    }

    // --- Public Methods ---

    /// <summary>
    /// 음료 상자와 상호작용하여 가장 앞에 있는 음료를 하나 꺼내 제거합니다.
    /// 이 함수는 플레이어의 상호작용 스크립트(PlayerInteractor) 등 외부에서 호출됩니다.
    /// </summary>
    /// <returns>실제로 음료를 꺼냈으면 true, 상자가 비어 있으면 false</returns>
    public bool TakeBeverage()
    {
        // 1. 음료가 남아있는지 확인합니다.
        if (foundBeverages.Count == 0)
        {
            Debug.Log(gameObject.name + ": 음료가 더 이상 없습니다!");
            return false; // 꺼낼 음료가 없으므로 함수를 종료합니다.
        }

        // 2. 리스트의 가장 첫 번째(0번 인덱스) 음료를 가져옵니다.
        Beverage beverageToTake = foundBeverages[0];

        // 3. 리스트에서 해당 음료를 제거합니다.
        foundBeverages.RemoveAt(0);

        // 4. (선택 사항) 로그를 남깁니다.
        Debug.Log(beverageToTake.gameObject.name + "을(를) 꺼냈습니다. 남은 음료: " + foundBeverages.Count);

        // 5. 씬(Scene)에서 음료의 GameObject를 파괴(제거)합니다.
        if (beverageToTake != null)
        {
            Destroy(beverageToTake.gameObject);
        }

        return true;
    }

    /// <summary>
    /// 지금 꺼낼 음료가 남아 있는지 여부입니다. (안내 문구 표시에 씁니다)
    /// </summary>
    public bool HasBeverage()
    {
        return foundBeverages != null && foundBeverages.Count > 0;
    }
}
