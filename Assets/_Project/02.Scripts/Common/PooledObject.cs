using UnityEngine;

/// <summary>
/// 이 오브젝트가 <see cref="PrefabPool"/>에서 나왔다는 표식입니다.
///
/// 반납할 때 "어느 프리팹의 풀로 돌아가야 하는지"를 알아야 하는데,
/// 반납을 요청하는 쪽(죽는 귀신 자신 등)은 그것을 모릅니다.
/// 그래서 꺼낼 때 출처를 여기에 적어 둡니다.
///
/// 풀이 실행 중에 붙이므로 인스펙터에서 직접 추가할 일은 없습니다.
/// </summary>
public class PooledObject : MonoBehaviour
{
    /// <summary>이 인스턴스를 만든 프리팹입니다. 반납할 풀을 찾는 열쇠입니다.</summary>
    public GameObject SourcePrefab { get; set; }
}
