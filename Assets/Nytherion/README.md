# Nytherion Roguelike 프로젝트

이 프로젝트는 로그라이크 장르의 게임을 위한 유니티 프로토타입입니다. 다양한 무기 시스템, 전투 메커니즘, 그리고 확장 가능한 아키텍처를 제공합니다.

## ✨ 주요 기능

- **확장 가능한 무기 시스템**
  - 근접 및 원거리 무기 지원
  - 다양한 공격 패턴과 이펙트
  - 무기 별 고유한 특성 및 시너지 시스템

- **전투 시스템**
  - 유연한 공격 행동 시스템 (IAttackBehavior)
  - 다양한 타입의 적 AI
  - 피격 및 데미지 처리 시스템

- **게임플레이**
  - 스테이지 기반 진행 시스템
  - 오브젝트 풀링을 통한 성능 최적화
  - 이벤트 기반 아키텍처

## 🚀 시작하기

### 필수 사항
- Unity 2022.3 이상
- .NET 4.x 런타임

### 설치 및 실행
1. 저장소를 클론합니다:
   ```bash
   git clone [저장소 URL]
   ```
2. Unity Hub를 통해 프로젝트를 엽니다.
3. `Assets/Scenes` 폴더에서 `Main.unity` 씬을 엽니다.
4. 플레이 버튼을 눌러 게임을 실행합니다.

## 🏗️ 프로젝트 구조

```
Assets/Nytherion/
├── Audio/           # 게임 내 사용되는 모든 사운드 에셋
├── Combat/          # 전투 시스템 관련 스크립트
│   ├── Weapons/     # 무기 관련 클래스
│   └── Behaviors/   # 공격 행동 패턴
├── Core/            # 게임의 핵심 시스템
│   ├── Managers/    # 게임 매니저들
│   └── Utils/       # 유틸리티 클래스
├── Data/            # 게임 데이터
│   ├── Enums/       # 열거형 정의
│   └── ScriptableObjects/ # 스크립터블 오브젝트
├── GamePlay/        # 실제 게임플레이 관련 스크립트
│   ├── Characters/  # 플레이어 및 적 캐릭터
│   └── Items/       # 아이템 시스템
├── Interfaces/      # 게임 전반에서 사용되는 인터페이스
└── UI/              # 사용자 인터페이스
```

## 📚 문서

- [API 레퍼런스](API_REFERENCE.md) - 주요 클래스와 메서드에 대한 상세 문서
- [시스템 문서](DOCUMENTATION.md) - 게임의 전반적인 아키텍처 및 시스템 설명
- [개발 가이드라인](DEVELOPMENT_GUIDELINES.md) - 코드 스타일 및 아키텍처 가이드

## 🛠️ 사용 방법 예시

### 새로운 무기 추가하기

1. `WeaponBase`를 상속받는 새 스크립트를 생성합니다.
2. 공격 로직을 `Attack` 메서드에 구현합니다.
3. 필요한 경우 `IAttackBehavior`를 구현한 새 동작을 추가합니다.

```csharp
public class MyCustomWeapon : WeaponBase
{
    [SerializeField] private float specialAbilityPower = 10f;
    
    public override void Attack(Vector2 direction)
    {
        if (!CanAttack()) return;
        
        // 커스텀 공격 로직 구현
        Debug.Log($"특수 능력 발동! {specialAbilityPower}의 데미지!");
        
        base.Attack(direction);
    }
}
```

## 🤝 기여하기

버그 리포트나 기능 요청은 이슈 트래커를 이용해 주세요. 풀 리퀘스트도 환영합니다!

## 📄 라이선스

이 프로젝트는 [MIT 라이선스](LICENSE)를 따릅니다.
