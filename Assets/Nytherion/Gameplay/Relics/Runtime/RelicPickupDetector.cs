using System;
using System.Collections.Generic;
using Nytherion.Core.Managers;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Nytherion.GamePlay.Relics
{
    [RequireComponent(typeof(SpriteRenderer))]
    [DefaultExecutionOrder(100)]
    public class RelicPickupDetector : MonoBehaviour
    {
        private static readonly HashSet<RelicPickupDetector> activeDetectors = new HashSet<RelicPickupDetector>();

        private static readonly int MainTexProperty = Shader.PropertyToID("_MainTex");
        private static readonly int MainTexSTProperty = Shader.PropertyToID("_MainTex_ST");
        private static readonly int OutlineThicknessProperty = Shader.PropertyToID("_OutLineTickness");
        private static readonly int OutlineColorProperty = Shader.PropertyToID("_OutlineColor");

        [Header("Pickup Settings")]
        [SerializeField, Min(0f), Tooltip("플레이어가 이 거리 안에 있을 때 마우스를 올려 유물을 선택할 수 있습니다.")]
        private float pickupDistance = 3f;

        [Header("Outline Settings")]
        [SerializeField] private float outlineThickness = 1f;
        [SerializeField] private Color outlineColor = Color.white;

        private static RelicPickupDetector currentTarget;
        private static int lastCollectionFrame = -1;
        private static int lastTargetRefreshFrame = -1;
        private static EventSystem pointerEventSystem;
        private static PointerEventData pointerData;
        private static readonly List<RaycastResult> uiRaycasts = new List<RaycastResult>();

        private Transform playerTransform;
        private Action collectAction;
        private InputManager inputManager;
        private SpriteRenderer spriteRenderer;
        private MaterialPropertyBlock propertyBlock;
        private bool isInitialized;
        private bool isHighlighted;
        private bool supportsOutline;
        private Sprite sourceSprite;
        private Sprite paddedSprite;
        private Texture2D paddedTexture;

        private void Awake()
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
            propertyBlock = new MaterialPropertyBlock();
            supportsOutline = spriteRenderer != null &&
                              spriteRenderer.sharedMaterial != null &&
                              spriteRenderer.sharedMaterial.HasProperty(OutlineThicknessProperty) &&
                              spriteRenderer.sharedMaterial.HasProperty(OutlineColorProperty);

            if (!supportsOutline)
            {
                Debug.LogWarning("[RelicPickupDetector] SpriteRenderer에 외곽선 속성을 가진 머티리얼이 없습니다.", this);
            }

            UpdateMainTexture();
            ApplyHighlightState(false);
        }

        public void Initialize(Transform player, Action onCollect)
        {
            playerTransform = player;
            collectAction = onCollect;
            isInitialized = playerTransform != null && collectAction != null;

            if (!isInitialized)
            {
                Debug.LogError("[RelicPickupDetector] 플레이어 또는 획득 동작이 초기화되지 않았습니다.", this);
                return;
            }

            UpdateMainTexture();
            ApplyHighlightState(false);

            activeDetectors.Add(this);
            SubscribeInput();
            RefreshTarget();
        }

        private void LateUpdate()
        {
            // 카메라 추적과 유물 이동이 끝난 위치로 선택을 갱신합니다.
            if (isInitialized) RefreshTarget(false);
        }

        public void UpdateMainTexture()
        {
            if (this == null || spriteRenderer == null)
            {
                return;
            }

            if (propertyBlock == null)
            {
                propertyBlock = new MaterialPropertyBlock();
            }

            if (isInitialized && supportsOutline)
            {
                PrepareOutlineSprite();
            }

            if (spriteRenderer.sprite != null)
            {
                // 머티리얼 인스턴스의 _MainTex에 직접 텍스처 할당
                if (spriteRenderer.material != null)
                {
                    spriteRenderer.material.SetTexture(MainTexProperty, spriteRenderer.sprite.texture);
                }

                spriteRenderer.GetPropertyBlock(propertyBlock);
                propertyBlock.SetTexture(MainTexProperty, spriteRenderer.sprite.texture);
                propertyBlock.SetVector(MainTexSTProperty, new Vector4(1f, 1f, 0f, 0f));
                spriteRenderer.SetPropertyBlock(propertyBlock);
            }
        }

        private void PrepareOutlineSprite()
        {
            Sprite sprite = spriteRenderer.sprite;
            if (sprite == null || sprite == paddedSprite) return;

            if (sprite == sourceSprite && paddedSprite != null)
            {
                spriteRenderer.sprite = paddedSprite;
                return;
            }

            ReleaseOutlineSprite();

            // Tight 메시와 이미지 끝부분에도 테두리를 그릴 공간을 확보합니다.
            // 원본 에셋 대신 드롭 표시용 복사본만 FullRect 메시로 만듭니다.
            Rect rect = sprite.textureRect;
            int padding = Mathf.CeilToInt(Mathf.Max(0f, outlineThickness)) + 1;
            int width = Mathf.RoundToInt(rect.width) + padding * 2;
            int height = Mathf.RoundToInt(rect.height) + padding * 2;
            paddedTexture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                name = sprite.name + "_DropOutline",
                filterMode = sprite.texture.filterMode,
                wrapMode = TextureWrapMode.Clamp
            };
            paddedTexture.SetPixels32(new Color32[width * height]);

            // Read/Write가 꺼진 텍스처도 GPU를 거쳐 복사합니다.
            RenderTexture previous = RenderTexture.active;
            RenderTexture temporary = RenderTexture.GetTemporary(
                sprite.texture.width, sprite.texture.height, 0, RenderTextureFormat.ARGB32);
            try
            {
                Graphics.Blit(sprite.texture, temporary);
                RenderTexture.active = temporary;
                paddedTexture.ReadPixels(rect, padding, padding, false);
                paddedTexture.Apply(false, true);
            }
            finally
            {
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(temporary);
            }

            Vector2 pivot = sprite.pivot - sprite.textureRectOffset + Vector2.one * padding;
            pivot = new Vector2(pivot.x / width, pivot.y / height);
            paddedSprite = Sprite.Create(paddedTexture, new Rect(0, 0, width, height),
                pivot, sprite.pixelsPerUnit, 0, SpriteMeshType.FullRect);
            paddedSprite.name = sprite.name + "_DropOutline";
            sourceSprite = sprite;
            spriteRenderer.sprite = paddedSprite;
        }

        private void ReleaseOutlineSprite()
        {
            if (paddedSprite != null) Destroy(paddedSprite);
            if (paddedTexture != null) Destroy(paddedTexture);
            paddedSprite = null;
            paddedTexture = null;
            sourceSprite = null;
        }

        private void OnDestroy()
        {
            ReleaseOutlineSprite();
        }

        private void Update()
        {
            if (!isInitialized)
            {
                return;
            }

            SubscribeInput();
        }

        private void OnDisable()
        {
            UnsubscribeInput();
            activeDetectors.Remove(this);

            if (currentTarget == this)
            {
                currentTarget = null;
                RefreshTarget();
            }

            ApplyHighlightState(false);
        }

        private void SubscribeInput()
        {
            if (inputManager != null)
            {
                return;
            }

            inputManager = InputManager.Instance;
            if (inputManager != null)
            {
                inputManager.onInteract += TryCollectCurrentTarget;
            }
        }

        private void UnsubscribeInput()
        {
            if (inputManager == null)
            {
                return;
            }

            inputManager.onInteract -= TryCollectCurrentTarget;
            inputManager = null;
        }

        private void TryCollectCurrentTarget()
        {
            // 입력 콜백이 Update보다 먼저 실행되어도 현재 마우스 위치와 거리를 확인합니다.
            RefreshTarget();
            if (currentTarget != this || !isInitialized || lastCollectionFrame == Time.frameCount)
            {
                return;
            }

            // 첫 유물이 풀로 돌아가며 다음 유물이 같은 입력 이벤트 안에서
            // 새 currentTarget이 되는 경우를 막는다.
            lastCollectionFrame = Time.frameCount;
            collectAction.Invoke();
        }

        public bool IsInPickupRange(Transform player)
        {
            return isInitialized && isActiveAndEnabled && player != null && playerTransform == player &&
                   ((Vector2)(player.position - transform.position)).sqrMagnitude <= pickupDistance * pickupDistance;
        }

        public static bool HasSelectedRelic(Transform player)
        {
            RefreshTarget();
            return currentTarget != null && currentTarget.IsInPickupRange(player);
        }

        private static void RefreshTarget(bool force = true)
        {
            // 모든 유물이 공유하는 선택 판정은 LateUpdate에서 프레임당 한 번만 수행합니다.
            // 초기화·비활성화·획득 입력은 즉시 다시 검사합니다.
            if (!force)
            {
                if (lastTargetRefreshFrame == Time.frameCount) return;
                lastTargetRefreshFrame = Time.frameCount;
            }

            RelicPickupDetector closestDetector = null;
            float closestDistanceSqr = float.MaxValue;
            Camera selectionCamera = Camera.main;
            Mouse mouse = Mouse.current;
            bool canSelect = selectionCamera != null && mouse != null &&
                             Time.timeScale > 0f && !UIPanelBase.IsAnyPanelOpen;
            Vector2 screenPosition = mouse != null ? mouse.position.ReadValue() : Vector2.zero;
            canSelect = canSelect && selectionCamera.pixelRect.Contains(screenPosition) &&
                        !IsPointerOverUI(screenPosition);
            Ray pointerRay = canSelect ? selectionCamera.ScreenPointToRay(screenPosition) : default;

            foreach (RelicPickupDetector detector in activeDetectors)
            {
                if (!canSelect || detector == null || !detector.isInitialized || !detector.isActiveAndEnabled)
                {
                    continue;
                }

                // 파괴되었거나 null인 Transform/GameObject 참조 체크
                if (detector.playerTransform == null || detector.transform == null)
                {
                    continue;
                }

                if (!detector.IsInPickupRange(detector.playerTransform))
                {
                    continue;
                }

                if (!TryGetPointerDistance(detector.spriteRenderer, pointerRay, out float pointerDistanceSqr))
                {
                    continue;
                }

                // 겹친 유물은 마우스에 중심이 가장 가까운 하나만 선택합니다.
                if (pointerDistanceSqr > closestDistanceSqr ||
                    (pointerDistanceSqr == closestDistanceSqr && closestDetector != null &&
                     detector.GetInstanceID() >= closestDetector.GetInstanceID()))
                {
                    continue;
                }

                closestDetector = detector;
                closestDistanceSqr = pointerDistanceSqr;
            }

            if (currentTarget == closestDetector)
            {
                return;
            }

            if (currentTarget != null)
            {
                currentTarget.SetHighlighted(false);
            }

            currentTarget = closestDetector;

            if (currentTarget != null)
            {
                currentTarget.SetHighlighted(true);
            }
        }

        private static bool TryGetPointerDistance(SpriteRenderer spriteRenderer, Ray pointerRay, out float distanceSqr)
        {
            distanceSqr = float.MaxValue;
            if (spriteRenderer == null || !spriteRenderer.enabled || spriteRenderer.sprite == null)
            {
                return false;
            }

            // SpriteRenderer의 Z 크기는 0이므로 3D Bounds 교차 대신 스프라이트 평면을 검사합니다.
            Transform spriteTransform = spriteRenderer.transform;
            Plane spritePlane = new Plane(spriteTransform.forward, spriteTransform.position);
            if (!spritePlane.Raycast(pointerRay, out float rayDistance)) return false;

            Vector3 worldPoint = pointerRay.GetPoint(rayDistance);
            Vector3 localPoint = spriteTransform.InverseTransformPoint(worldPoint);
            if (spriteRenderer.flipX) localPoint.x = -localPoint.x;
            if (spriteRenderer.flipY) localPoint.y = -localPoint.y;
            Bounds bounds = spriteRenderer.sprite.bounds;
            if (localPoint.x < bounds.min.x || localPoint.x > bounds.max.x ||
                localPoint.y < bounds.min.y || localPoint.y > bounds.max.y)
            {
                return false;
            }

            Vector3 center = bounds.center;
            if (spriteRenderer.flipX) center.x = -center.x;
            if (spriteRenderer.flipY) center.y = -center.y;
            distanceSqr = ((Vector2)(worldPoint - spriteTransform.TransformPoint(center))).sqrMagnitude;
            return true;
        }

        private static bool BlocksWorldSelection(RaycastResult result)
        {
            if (!(result.module is GraphicRaycaster raycaster)) return false;

            Canvas canvas = raycaster.GetComponent<Canvas>();
            if (canvas == null || canvas.renderMode != RenderMode.WorldSpace) return true;

            // 월드의 키 안내·체력바는 투명 여백까지 UI로 검출되지만 입력을 받는 UI는 아닙니다.
            // 실제 클릭·드래그·호버 동작이 있는 월드 UI만 유물 선택보다 우선합니다.
            GameObject target = result.gameObject;
            return target != null &&
                   (ExecuteEvents.GetEventHandler<IPointerClickHandler>(target) != null ||
                    ExecuteEvents.GetEventHandler<IPointerDownHandler>(target) != null ||
                    ExecuteEvents.GetEventHandler<IPointerEnterHandler>(target) != null ||
                    ExecuteEvents.GetEventHandler<IBeginDragHandler>(target) != null ||
                    ExecuteEvents.GetEventHandler<IDragHandler>(target) != null ||
                    ExecuteEvents.GetEventHandler<IScrollHandler>(target) != null);
        }

        private static bool IsPointerOverUI(Vector2 screenPosition)
        {
            EventSystem eventSystem = EventSystem.current;
            if (eventSystem == null) return false;

            if (pointerData == null || pointerEventSystem != eventSystem)
            {
                pointerEventSystem = eventSystem;
                pointerData = new PointerEventData(eventSystem);
            }

            pointerData.Reset();
            pointerData.position = screenPosition;
            uiRaycasts.Clear();
            eventSystem.RaycastAll(pointerData, uiRaycasts);
            bool isOverUI = false;
            foreach (RaycastResult result in uiRaycasts)
            {
                if (BlocksWorldSelection(result))
                {
                    isOverUI = true;
                    break;
                }
            }
            uiRaycasts.Clear();
            return isOverUI;
        }

        private void SetHighlighted(bool highlighted)
        {
            if (isHighlighted == highlighted)
            {
                return;
            }

            ApplyHighlightState(highlighted);
        }

        private void ApplyHighlightState(bool highlighted)
        {
            if (this == null || spriteRenderer == null)
            {
                return;
            }

            isHighlighted = highlighted;

            if (propertyBlock == null)
            {
                propertyBlock = new MaterialPropertyBlock();
            }

            if (spriteRenderer.sprite != null)
            {
                if (spriteRenderer.material != null)
                {
                    spriteRenderer.material.SetTexture(MainTexProperty, spriteRenderer.sprite.texture);
                }

                spriteRenderer.GetPropertyBlock(propertyBlock);
                propertyBlock.SetTexture(MainTexProperty, spriteRenderer.sprite.texture);
                propertyBlock.SetVector(MainTexSTProperty, new Vector4(1f, 1f, 0f, 0f));
            }
            else
            {
                spriteRenderer.GetPropertyBlock(propertyBlock);
            }

            if (supportsOutline)
            {
                Color appliedOutlineColor = outlineColor;
                appliedOutlineColor.a = highlighted ? outlineColor.a : 0f;

                propertyBlock.SetFloat(OutlineThicknessProperty, highlighted ? outlineThickness : 0f);
                propertyBlock.SetColor(OutlineColorProperty, appliedOutlineColor);
            }

            spriteRenderer.SetPropertyBlock(propertyBlock);
        }
    }
}
