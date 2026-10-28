using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Nytherion.Core.Managers;
using Nytherion.Data.ScriptableObjects.Relics;
using Nytherion.GamePlay.Characters.Player;
using Nytherion.GamePlay.Relics;
using Nytherion.UI.Components;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Nytherion.Editor
{
    public static class RelicCollectAllVerification
    {
        [MenuItem("Tools/Nytherion/유물/모두 획득 검증")]
        public static void Verify()
        {
            if (Application.isPlaying) throw new InvalidOperationException("플레이 모드 밖에서 실행해 주세요.");
            var results = new List<string>();
            Scene preview = EditorSceneManager.NewPreviewScene();
            var created = new List<DroppedRelic>();
            var registry = (HashSet<DroppedRelic>)typeof(DroppedRelic)
                .GetField("activeDroppedRelics", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
            PlayerAction actions = null;
            Keyboard keyboard = null;
            RelicCollectAllController controller = null;
            GameObject hud = null;
            RelicData data = ScriptableObject.CreateInstance<RelicData>();
            float previousTimeScale = Time.timeScale;
            try
            {
                GameObject player = Create(preview, "검증 플레이어", typeof(PlayerController));
                GameObject otherPlayer = Create(preview, "다른 플레이어");
                GameObject inputObject = Create(preview, "검증 입력", typeof(InputManager));
                InputManager input = inputObject.GetComponent<InputManager>();
                actions = new PlayerAction();
                actions.Enable();
                Set(input, "playerActions", actions);
                Time.timeScale = 1f;
                DroppedRelic near = AddRelic(0.5f);
                DroppedRelic edge = AddRelic(3f);
                DroppedRelic far = AddRelic(3.01f);
                Set(AddRelic(1f), "isConcealed", true);
                Set(AddRelic(1f), "isLaunching", true);
                Set(AddRelic(1f), "isRevealing", true);
                Set(AddRelic(1f), "isAutoCollecting", true);
                AddRelic(1f).GetComponent<RelicPickupDetector>().enabled = false;
                Set(AddRelic(1f).GetComponent<RelicPickupDetector>(), "playerTransform", otherPlayer.transform);
                Require(DroppedRelic.CountNearbyCollectible(player.transform) == 2,
                    "반경 3 경계 포함, 범위 밖·연출 중·이동 중·비활성·다른 플레이어 제외");

                GameSceneUIRefs uiRefs = Create(preview, "검증 UI 참조", typeof(GameSceneUIRefs)).GetComponent<GameSceneUIRefs>();
                Set(uiRefs, "relicCollectAllKeyIcon", AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Nytherion/Art/Gacha/Sprites/KeyG.png"));
                Set(uiRefs, "interactionKeyIcon", AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Nytherion/Art/Gacha/Sprites/KeyF.png"));
                Set(uiRefs, "gachaSingleKeyIcon", AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Nytherion/Art/Gacha/Sprites/KeyX.png"));
                Set(uiRefs, "gachaTenKeyIcon", AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Nytherion/Art/Gacha/Sprites/KeyC.png"));
                InteractionManager interactions = Create(preview, "검증 상호작용", typeof(InteractionManager)).GetComponent<InteractionManager>();
                controller = new RelicCollectAllController(input, player.GetComponent<PlayerController>(), uiRefs, interactions);
                controller.Initialize();
                controller.Tick();
                hud = (GameObject)typeof(RelicCollectAllController).GetField("canvasObject", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(controller);
                TMP_Text label = hud.transform.Find("CollectAllPrompt/Count").GetComponent<TMP_Text>();
                Require(label.text == "모두 획득 (2)" && label.gameObject.activeInHierarchy,
                    "HUD에 정확한 대상 개수와 모두 획득 문구 표시");
                Require(!hud.GetComponent<CanvasGroup>().blocksRaycasts && !label.raycastTarget,
                    "획득 안내가 마우스 선택을 가로막지 않음");
                Require(input.CollectAllRelicsKey == Key.G, "기본 모두 획득 키는 G");
                keyboard = InputSystem.AddDevice<Keyboard>();
                int keyEvents = 0;
                input.onCollectAllRelics += () => keyEvents++;
                PressG();
                Require(keyEvents == 1 && IsCollecting(near) && IsCollecting(edge) && !IsCollecting(far),
                    "실제 G 입력 이벤트로 범위 안 유물만 이동 획득 시작");
                Require(DroppedRelic.CountNearbyCollectible(player.transform) == 0 &&
                    DroppedRelic.BeginAutoCollectNearby(player.transform) == 0,
                    "획득 중인 유물은 개수와 재실행 대상에서 제외");
                Require(!label.gameObject.activeInHierarchy, "획득을 시작하면 대상 0개 안내 숨김");
                InputSystem.Update();
                InvokeUpdate();
                Require(keyEvents == 1, "G를 누르고 있어도 획득 입력 반복 없음");
                Time.timeScale = 0f;
                ReleaseG();
                PressG();
                Require(keyEvents == 1, "일시정지 중 모두 획득 차단");
                Time.timeScale = 1f;
                actions.Player.Disable();
                ReleaseG();
                PressG();
                Require(keyEvents == 1, "플레이어 입력 비활성화 중 모두 획득 차단");
                Require(DroppedRelic.CountNearbyCollectible(null) == 0 && DroppedRelic.BeginAutoCollectNearby(null) == 0,
                    "플레이어가 없으면 안전하게 무시");

                DroppedRelic AddRelic(float distance)
                {
                    GameObject instance = Create(preview, "검증 유물", typeof(SpriteRenderer), typeof(Rigidbody2D), typeof(CircleCollider2D), typeof(DroppedRelic));
                    instance.transform.position = new Vector3(distance, 0, 0);
                    DroppedRelic relic = instance.GetComponent<DroppedRelic>();
                    RelicPickupDetector detector = instance.GetComponent<RelicPickupDetector>();
                    Set(detector, "playerTransform", player.transform);
                    Set(detector, "isInitialized", true);
                    Set(relic, "relicPickupDetector", detector);
                    Set(relic, "relicData", data);
                    Set(relic, "rb", instance.GetComponent<Rigidbody2D>());
                    registry.Add(relic);
                    created.Add(relic);
                    return relic;
                }
                void InvokeUpdate() => typeof(InputManager).GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(input, null);
                void PressG() { InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.G)); InputSystem.Update(); InvokeUpdate(); }
                void ReleaseG() { InputSystem.QueueStateEvent(keyboard, new KeyboardState()); InputSystem.Update(); }
                void Require(bool condition, string message)
                {
                    if (!condition) throw new InvalidOperationException(message);
                    results.Add("PASS " + message);
                }
            }
            catch (Exception error)
            {
                results.Add("FAIL " + error);
                throw;
            }
            finally
            {
                if (keyboard != null) InputSystem.RemoveDevice(keyboard);
                if (hud != null) Object.DestroyImmediate(hud);
                controller?.Dispose();
                foreach (DroppedRelic relic in created) registry.Remove(relic);
                EditorSceneManager.ClosePreviewScene(preview);
                actions?.Dispose();
                Object.DestroyImmediate(data);
                Time.timeScale = previousTimeScale;
                Directory.CreateDirectory("output/relic-collect-all");
                File.WriteAllLines("output/relic-collect-all/verification.txt", results);
            }
        }

        private static GameObject Create(Scene scene, string name, params Type[] components)
        {
            var instance = new GameObject(name, components);
            SceneManager.MoveGameObjectToScene(instance, scene);
            return instance;
        }
        private static void Set(object instance, string field, object value) => instance.GetType()
            .GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(instance, value);
        private static bool IsCollecting(DroppedRelic relic) => (bool)typeof(DroppedRelic)
            .GetField("isAutoCollecting", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(relic);
    }
}
