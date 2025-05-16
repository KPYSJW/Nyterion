# Nytherion Roguelike Project Documentation

## Table of Contents
1. [Core Systems](#core-systems)
   - [GameManager](#gamemanager)
   - [InputManager](#inputmanager)
   - [EventSystem](#eventsystem)
   - [ObjectPoolManager](#objectpoolmanager)
   - [SlotManager](#slotmanager)
   - [StageManager](#stagemanager)

2. [Gameplay Systems](#gameplay-systems)
   - [Player Systems](#player-systems)
     - [Player](#player)
     - [PlayerCombat](#playercombat)
     - [PlayerController](#playercontroller)
     - [PlayerManager](#playermanager)
   - [Enemy Systems](#enemy-systems)
     - [EnemyBase](#enemybase)
     - [EnemyAIController](#enemyaicontroller)
     - [EnemySpawner](#enemyspawner)
   - [Combat System](#combat-system)
     - [WeaponBase](#weaponbase)
     - [MeleeWeapon](#meleeweapon)
     - [RangedWeapon](#rangedweapon)
     - [IAttackBehavior](#iattackbehavior)
     - [SynergyEvaluator](#synergyevaluator)

3. [Data](#data)
   - [Scriptable Objects](#scriptable-objects)
   - [Enums](#enums)
   - [Interfaces](#interfaces)

## Core Systems

### GameManager
**Location**: `Core/GameManager.cs`  
**Responsibility**: 게임의 전반적인 상태를 관리하고 다른 매니저들을 조율합니다.
- 게임 상태 관리 (시작, 일시정지, 종료)
- 씬 전환 관리
- 게임 루프 제어

### InputManager
**Location**: `Core/InputManager.cs`  
**Responsibility**: 모든 사용자 입력을 처리하고 관련 이벤트를 발생시킵니다.
- 키보드/마우스/게임패드 입력 처리
- 입력 액션 매핑
- 입력 이벤트 발생

### EventSystem
**Location**: `Core/EventSystem.cs`  
**Responsibility**: 게임 내 이벤트 시스템을 관리합니다.
- 이벤트 구독/발행 시스템
- 게임 오브젝트 간 느슨한 결합 제공
- 이벤트 버스 패턴 구현

### ObjectPoolManager
**Location**: `Core/ObjectPoolManager.cs`  
**Responsibility**: 오브젝트 풀링을 관리하여 성능을 최적화합니다.
- 자주 생성/제거되는 오브젝트 풀링
- 동적 풀 크기 조정
- 오브젝트 재사용 관리

## Gameplay Systems

### Player Systems

#### Player
**Location**: `GamePlay/Characters/Player/Player.cs`  
**Responsibility**: 플레이어 캐릭터의 기본 동작과 상태를 관리합니다.
- 플레이어 상태 관리 (체력, 스테미나 등)
- 컴포넌트 참조 관리
- 게임 오브젝트로서의 기본 동작

#### PlayerCombat
**Location**: `GamePlay/Characters/Player/PlayerCombat.cs`  
**Responsibility**: 플레이어의 전투 관련 로직을 처리합니다.
- 무기 장착/해제
- 공격 입력 처리
- 데미지 계산 및 적용

#### PlayerController
**Location**: `GamePlay/Characters/Player/PlayerController.cs`  
**Responsibility**: 플레이어의 움직임과 입력을 처리합니다.
- 이동 입력 처리
- 점프 및 회전
- 애니메이션 제어

#### PlayerManager
**Location**: `GamePlay/Characters/Player/PlayerManager.cs`  
**Responsibility**: 플레이어 관련 전역 접근점을 제공하는 싱글톤 매니저입니다.
- 플레이어 인스턴스에 대한 전역 접근 제공
- 플레이어 컴포넌트 관리
- 게임 전반에서의 플레이어 상태 추적

### Combat System

#### WeaponBase
**Location**: `GamePlay/Combat/WeaponBase.cs`  
**Responsibility**: 모든 무기의 기본 클래스로, 무기 공통 기능을 정의합니다.
- 공격 로직 처리
- 데미지 계산
- 이펙트 및 사운드 재생

#### MeleeWeapon
**Location**: `GamePlay/Combat/MeleeWeapon.cs`  
**Responsibility**: 근접 무기의 특수한 동작을 구현합니다.
- 충돌 감지
- 공격 범위 계산
- 타격 이펙트 처리

#### RangedWeapon
**Location**: `GamePlay/Combat/RangedWeapon.cs`  
**Responsibility**: 원거리 무기의 특수한 동작을 구현합니다.
- 발사체 생성 및 관리
- 탄약 시스템
- 조준 보조 기능

#### IAttackBehavior
**Location**: `GamePlay/Combat/IAttackBehavior.cs`  
**Responsibility**: 공격 행위를 위한 인터페이스를 정의합니다.
- 공격 시작/종료 메서드
- 공격 상태 관리
- 콤보 시스템 지원

## Data

### Scriptable Objects
- `EnemyData`: 적 캐릭터의 스탯 및 동작 설정
- `WeaponData`: 무기별 속성 및 공격 파라미터
- `StageData`: 스테이지 구성 정보
- `EngravingData`: 각인 효과 설정

### Enums
- `WeaponSynergyType`: 무기 간 시너지 유형 정의

### Interfaces
- `IDamageable`: 데미지를 받을 수 있는 객체를 위한 인터페이스
- `IUseableItem`: 사용 가능한 아이템을 위한 인터페이스

## Best Practices
1. **싱글톤 패턴**: `PlayerManager`와 같은 글로벌 접근이 필요한 매니저 클래스에 사용
2. **이벤트 기반 아키텍처**: `EventSystem`을 통해 컴포넌트 간 의존성 감소
3. **객체 풀링**: 빈번한 생성/제거가 일어나는 객체는 `ObjectPoolManager` 사용
4. **데이터 분리**: 게임 밸런스 데이터는 Scriptable Object로 분리하여 관리
5. **인터페이스 활용**: `IDamageable`, `IUseableItem`과 같은 인터페이스를 통해 유연한 시스템 구현

## Troubleshooting
- **PlayerManager 인스턴스를 찾을 수 없음**: 씬에 PlayerManager가 있는지 확인하세요.
- **컴포넌트 참조 오류**: 인스펙터에서 필요한 컴포넌트가 올바르게 할당되었는지 확인하세요.
- **이벤트 구독 해제**: 이벤트 구독은 반드시 `OnDisable`이나 `OnDestroy`에서 해제하세요.
