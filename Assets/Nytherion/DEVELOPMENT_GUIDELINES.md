# 개발 가이드라인

이 문서는 Nytherion Roguelike 프로젝트의 개발 표준과 가이드라인을 설명합니다.

## 코드 스타일

### 명명 규칙
- **클래스, 메서드, 프로퍼티**: PascalCase
  ```csharp
  public class PlayerController : MonoBehaviour
  {
      public int MaxHealth { get; private set; }
      
      private void MovePlayer() { ... }
  }
  ```

- **private 필드**: _camelCase (언더스코어 접두사)
  ```csharp
  private int _currentHealth;
  private bool _isMoving;
  ```

- **상수**: PascalCase
  ```csharp
  public const float Gravity = 9.81f;
  ```

### 주석 규칙
- 공개 API에는 XML 문서화 주석을 사용합니다.
  ```csharp
  /// <summary>
  /// 플레이어의 체력을 회복합니다.
  /// </summary>
  /// <param name="amount">회복량</param>
  /// <returns>실제로 회복된 양</returns>
  public int Heal(int amount) { ... }
  ```

- 복잡한 로직에는 설명 주석을 추가합니다.
  ```csharp
  // 체력이 0 이하로 떨어지지 않도록 보정
  _currentHealth = Mathf.Max(0, _currentHealth - damage);
  ```

## 아키텍처

### 싱글톤 패턴
- 매니저 클래스에만 사용합니다.
- 인스펙터에서 할당이 필요한 경우 `[SerializeField] private static`을 사용합니다.
  ```csharp
  public class GameManager : MonoBehaviour
  {
      public static GameManager Instance { get; private set; }
      
      private void Awake()
      {
          if (Instance == null)
          {
              Instance = this;
              DontDestroyOnLoad(gameObject);
          }
          else
          {
              Destroy(gameObject);
          }
      }
  }
  ```

### 이벤트 시스템
- 컴포넌트 간 통신에는 EventSystem을 사용합니다.
- 이벤트 구독은 반드시 `OnEnable`/`OnDisable`에서 관리합니다.
  ```csharp
  private void OnEnable()
  {
      EventSystem.Instance.OnPlayerDamaged += HandlePlayerDamaged;
  }

  private void OnDisable()
  {
      if (EventSystem.Instance != null)
      {
          EventSystem.Instance.OnPlayerDamaged -= HandlePlayerDamaged;
      }
  }
  ```

## 성능 최적화

### 오브젝트 풀링
- 빈번히 생성/제거되는 오브젝트는 ObjectPoolManager를 사용합니다.
  ```csharp
  // 오브젝트 가져오기
  var bullet = ObjectPoolManager.Instance.GetObject(bulletPrefab);
  
  // 오브젝트 반환
  ObjectPoolManager.Instance.ReturnObject(bullet);
  ```

### 물리 연산
- `FixedUpdate` 대신 `Update`에서 물리 연산을 피합니다.
- `GetComponent` 호출을 최소화하고 캐싱을 사용합니다.
  ```csharp
  private Rigidbody _rigidbody;
  
  private void Awake()
  {
      _rigidbody = GetComponent<Rigidbody>();
  }
  ```

## 버전 관리

### 브랜치 전략
- `main`: 안정적인 릴리즈 버전
- `develop`: 다음 릴리즈를 위한 개발 브랜치
- `feature/기능명`: 새로운 기능 개발
- `bugfix/버그명`: 버그 수정

### 커밋 메시지
- 타입: 제목 (50자 이내)
- 본문 (선택사항): 상세 설명 (72자마다 줄바꿈)
- 푸터 (선택사항): 이슈 트래커 ID 참조

예시:
```
feat: 플레이어 체력 시스템 추가

- 최대 체력, 현재 체력 속성 추가
- 데미지/치유 메서드 구현
- 체력 UI 연동

Resolves: #123
```

## 테스트

### 유닛 테스트
- 새로운 기능에는 반드시 유닛 테스트를 작성합니다.
- 테스트 클래스명: `[클래스명]Tests`
- 테스트 메서드명: `[시나리오]_[예상결과]`

```csharp
public class PlayerTests
{
    [Test]
    public void TakeDamage_WhenDamageTaken_HealthDecreases()
    {
        // Arrange
        var player = new Player(health: 100);
        
        // Act
        player.TakeDamage(20);
        
        // Assert
        Assert.AreEqual(80, player.CurrentHealth);
    }
}
```

## 기여하기

1. 이슈를 생성하여 변경사항을 논의합니다.
2. `develop` 브랜치에서 `feature/기능명` 브랜치를 생성합니다.
3. 코드 변경 후 테스트를 실행합니다.
4. Pull Request를 생성하고 리뷰를 요청합니다.

## 라이선스

이 프로젝트는 [MIT 라이선스](LICENSE)를 따릅니다.
