# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Unity Project Commands

This is a Unity 2022.3+ project. Common Unity operations:
- Open project in Unity Hub and use the Unity Editor Play button to test
- Build: File > Build Settings > Build (or Ctrl+Shift+B)
- No specific lint/test commands - Unity handles compilation automatically

## Architecture Overview

This is a roguelike game built with Unity using **Zenject for dependency injection** and an **event-driven architecture**. Key architectural patterns:

### Dependency Injection System
- **GameInstaller** (`Core/Systems/GameInstaller.cs:58-206`) is the main DI container setup
- All managers are registered as singletons with NonLazy initialization
- Uses Zenject's `[Inject]` attribute for constructor injection
- ISaveable implementations are bound for the save/load system

### Manager System
Core managers handle different game aspects:
- **PlayerManager** - Player state, health, combat
- **InventoryManager** - Item storage with 24-slot system
- **CurrencyManager** - Gold/Token economy  
- **EngravingManager** - Equipment enhancement system
- **SaveLoadManager** - Persistence using ISaveable pattern
- **EventManager** - Event system for decoupled communication
- **GameSceneUIManager** - UI orchestration

### Core Systems
- **ISaveable Interface** (`Core/Interfaces/ISaveable.cs`) - Standardized save/load pattern
- **InventoryModel** (`Core/Systems/InventoryModel.cs`) - Inventory logic with events
- **ItemDatabase** - Centralized item data management using ScriptableObjects

### Player System
- **State Machine Pattern** - PlayerController uses state pattern (IdleState, etc.)
- **Component-based** - PlayerHealth, PlayerCombat, PlayerController are separate components
- **Input Management** - Centralized through InputManager

### UI Architecture  
- **MVP Pattern** - Uses Presenters (e.g., InventoryPresenter) to separate UI logic
- **Event-driven Updates** - UI responds to manager events (OnInventoryUpdated, etc.)

## Key Development Patterns

### Adding New Items
1. Create ScriptableObject asset in `Data/ScriptableObjects/Items/`
2. Add to ItemDatabaseSO
3. Item will be available through ItemDatabase.GetItemByID()

### Adding New Managers
1. Create manager class inheriting MonoBehaviour
2. Implement ISaveable if persistence needed
3. Register in GameInstaller.InstallBindings()
4. Initialize in GameInstaller.Start()

### Save System
All persistent data uses ISaveable:
- Implement `PopulateSaveData(SaveData saveData)` for saving
- Implement `LoadFromSaveData(SaveData saveData)` for loading
- Register in GameInstaller ISaveable bindings

## Debug Features
- **F1** - Add 1000 gold (GameManager.cs:16-20)
- **F2** - Add 10 tokens (GameManager.cs:21-25)

## Current Branch Status
Working on branch: **ParkSiWoo**
Recent changes include inventory system refactoring and UI improvements.