# API 레퍼런스

이 문서는 Nytherion Roguelike 프로젝트의 주요 클래스와 메서드에 대한 상세한 레퍼런스를 제공합니다.

## 목차

- [Core Systems](#core-systems)
  - [GameManager](#gamemanager)
  - [InputManager](#inputmanager)
  - [EventSystem](#eventsystem)
  - [ObjectPoolManager](#objectpoolmanager)
- [Gameplay Systems](#gameplay-systems)
  - [Player Systems](#player-systems)
  - [Combat System](#combat-system)

## Core Systems

### GameManager
`Core/GameManager.cs`

게임의 전반적인 상태를 관리하는 중앙 관리자 클래스입니다.

#### 주요 속성
- `Instance` (static): GameManager의 싱글톤 인스턴스
- `CurrentGameState`: 현재 게임 상태 (GameState enum)

#### 주요 메서드
- `ChangeState(GameState newState)`: 게임 상태를 변경합니다.
- `PauseGame()`: 게임을 일시정지합니다.
- `ResumeGame()`: 게임을 재개합니다.
- `QuitGame()`: 게임을 종료합니다.

### InputManager
`Core/InputManager.cs`

사용자 입력을 처리하고 관련 이벤트를 발생시킵니다.

#### 주요 이벤트
- `OnMoveInput`: 이동 입력 이벤트 (Vector2 방향 벡터)
- `OnAttackInput`: 공격 입력 이벤트
- `OnInteractInput`: 상호작용 입력 이벤트

#### 주요 메서드
- `EnablePlayerControls()`: 플레이어 컨트롤을 활성화합니다.
- `DisablePlayerControls()`: 플레이어 컨트롤을 비활성화합니다.

## Gameplay Systems

### Player Systems

#### PlayerManager
`GamePlay/Characters/Player/PlayerManager.cs`

플레이어 관련 전역 접근점을 제공하는 싱글톤 매니저입니다.

#### 주요 속성
- `Instance` (static): PlayerManager의 싱글톤 인스턴스
- `Player`: 현재 플레이어 게임 오브젝트
- `PlayerHealth`: 플레이어 체력 컴포넌트
- `PlayerMana`: 플레이어 마나 컴포넌트

### Combat System

#### WeaponBase
`GamePlay/Combat/WeaponBase.cs`

모든 무기의 기본 클래스입니다.

#### 주요 속성
- `Damage`: 무기 기본 공격력
- `AttackSpeed`: 공격 속도
- `Range`: 공격 범위

#### 주요 메서드
- `Attack()`: 공격을 실행합니다.
- `OnHit(GameObject target)`: 타격 시 호출되는 메서드입니다.

#### MeleeWeapon
`GamePlay/Combat/MeleeWeapon.cs`

근접 무기 클래스입니다.

#### RangedWeapon
`GamePlay/Combat/RangedWeapon.cs`

원거리 무기 클래스입니다.

## 데이터 구조

### Enums

#### WeaponType
- `Sword`: 검 타입 무기
- `Axe`: 도끼 타입 무기
- `Bow`: 활 타입 무기
- `Staff`: 지팡이 타입 무기

#### GameState
- `MainMenu`: 메인 메뉴 화면
- `Playing`: 게임 플레이 중
- `Paused`: 게임 일시정지
- `GameOver`: 게임 오버
