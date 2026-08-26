#ifndef CARDRIVE_GRASS_SHARED_INCLUDED
#define CARDRIVE_GRASS_SHARED_INCLUDED

// 풀 셰이더와 C# 이 <b>함께 쓰는 배열 크기</b>입니다.
//
// 왜 한곳에 모았는가. 이 수들은 양쪽에 따로 적혀 있었습니다 —
//   GRASS_PUSHER_MAX    ↔ GrassPushField.MaxPushers
//   TRAMPLE_SEGMENT_MAX ↔ GrassTrampleMap.MaxSegments
// 한쪽만 고쳐도 컴파일은 통과합니다. C# 이 더 크면 넘긴 자리 일부가 조용히 버려지고,
// 셰이더가 더 크면 채워지지 않은 칸을 읽어 엉뚱한 자리가 눌립니다.
// 어느 쪽이든 화면에 "가끔 이상하다"로만 나타나서, 원인에 닿기가 어렵습니다.
//
// 이제 셰이더 쪽 숫자는 여기 하나뿐이고, C# 과 어긋나지 않는지는
// GrassShaderConstantTests(EditMode) 가 이 파일을 읽어 확인합니다.
// 배치모드 테스트에서 걸리므로 게임을 띄우지 않아도 드러납니다.
//
// 고칠 때는 여기와 해당 C# 상수를 함께 고치세요. 테스트가 그것을 강제합니다.

// GrassPushField.MaxPushers 와 같아야 합니다.
#define GRASS_PUSHER_MAX 16

// GrassTrampleMap.MaxSegments 와 같아야 합니다.
#define TRAMPLE_SEGMENT_MAX 16

#endif
