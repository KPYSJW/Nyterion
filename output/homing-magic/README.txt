정령의 인도(SpiritsGuidance)의 곡선 유도 마법탄

설정 에셋: Assets/Nytherion/Data/ScriptableObjects/Weapons/SpiritsGuidance.asset
- projectileSpeed: 6 (이동 속도)
- hasHomingProjectiles: true (유도 활성화; false로 바꾸면 초기 각도대로 직진)
- useHomingLaunchAngles: true (각도 배열로 기본 1발과 증가한 탄을 배치; false면 기존 단발/추가 탄 패턴)
- homingLaunchAngles: +30, +15, 0, -15, -30 (발사 시 선택한 목표 방향 기준)
- homingTurnSpeed: 180 (도/초; 근거리에서도 동일한 제한)
- homingSearchRadius: 10 (발사 시 한 번만 살아 있는 적을 선택)
- homingLaunchDuration: 0.15 (초기 방향 유지 시간)
- homingLifetime: 4 (초; 기존 AutoReturnToPool을 통한 반환)
- projectileRotationOffset: 0 (이미지 방향 보정; 이동 방향 계산과 독립)

기존 HomingProj, RangedWeapon, CollisionObject, ObjectPoolManager를 확장합니다.
이동은 FixedUpdate에서 Rigidbody2D.velocity로만 처리하며 Transform 위치 이동은 없습니다. 목표가 가까우면서 방향 차이가 클 때 감속하여 회전 반경을 줄이고, 방향이 맞으면 원래 속도로 접근합니다.
목표가 죽거나 비활성화되면 참조를 버리고 마지막 진행 방향으로 직진합니다.
목표가 없으면 조준 방향을 기준으로 발사하며 비행 중 새 목표를 검색하지 않습니다.
기본 발사 수는 1발이며 기존 extraProjectiles 증가 효과만큼 추가됩니다. 단발은 0도, 2발은 ±7.5도, 3발은 ±15도, 5발은 ±30/±15/0도입니다. 5발을 넘어도 ±30도 안에서 고르게 배치합니다.
유도 유물 및 기존 동료의 SetHomingEnabled 호출도 유지합니다.

Homing.png의 기존 32×32 4프레임 및 GUID를 유지하고 새 12fps 루프 애니메이션을 연결했습니다.
기존 정령의 인도 무기/투사체의 GUID, 아이템 ID, 데이터베이스 및 가챠 등록을 유지합니다.
풀 재사용 시 목표/진행 방향/수명/반환 상태/애니메이션/기존 TrailRenderer를 초기화합니다.

Unity 메뉴: Tools > Nytherion > Homing Magic > Verify In Play Mode
검증은 실제 무기 프리팹·투사체·Physics2D·전용 ObjectPoolManager를 사용합니다.
테스트 종료 후 기존 풀 및 시간 배율을 복구하며 씬/프리팹을 저장하지 않습니다.


