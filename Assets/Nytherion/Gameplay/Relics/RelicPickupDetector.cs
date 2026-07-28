using System;
using System.Collections.Generic;
using Nytherion.Core.Managers;
using UnityEngine;

namespace Nytherion.GamePlay.Relics
{
    [RequireComponent(typeof(SpriteRenderer))]
    public class RelicPickupDetector : MonoBehaviour
    {
        private static readonly HashSet<RelicPickupDetector> activeDetectors = new HashSet<RelicPickupDetector>();

        private static readonly int MainTexProperty = Shader.PropertyToID("_MainTex");
        private static readonly int MainTexSTProperty = Shader.PropertyToID("_MainTex_ST");
        private static readonly int OutlineThicknessProperty = Shader.PropertyToID("_OutLineTickness");
        private static readonly int OutlineColorProperty = Shader.PropertyToID("_OutlineColor");

        [Header("Pickup Settings")]
        [SerializeField] private float pickupDistance = 1.25f;

        [Header("Outline Settings")]
        [SerializeField] private float outlineThickness = 1f;
        [SerializeField] private Color outlineColor = Color.white;

        [Header("Debug Info (Inspector)")]
        [SerializeField] private Texture currentSpriteTexture;
        [SerializeField] private Texture currentPropertyBlockTexture;
        [SerializeField] private Texture currentSharedMaterialTexture;
        [SerializeField] private bool hasPropertyBlock;

        private static RelicPickupDetector currentTarget;

        private Transform playerTransform;
        private Action collectAction;
        private InputManager inputManager;
        private SpriteRenderer spriteRenderer;
        private MaterialPropertyBlock propertyBlock;
        private bool isInitialized;
        private bool isHighlighted;
        private bool supportsOutline;

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

            if (spriteRenderer.sprite != null)
            {
                currentSpriteTexture = spriteRenderer.sprite.texture;

                // 머티리얼 인스턴스의 _MainTex에 직접 텍스처 할당
                if (spriteRenderer.material != null)
                {
                    spriteRenderer.material.SetTexture(MainTexProperty, spriteRenderer.sprite.texture);
                }

                spriteRenderer.GetPropertyBlock(propertyBlock);
                propertyBlock.SetTexture(MainTexProperty, spriteRenderer.sprite.texture);
                propertyBlock.SetVector(MainTexSTProperty, new Vector4(1f, 1f, 0f, 0f));
                spriteRenderer.SetPropertyBlock(propertyBlock);

                currentPropertyBlockTexture = propertyBlock.GetTexture(MainTexProperty);
                hasPropertyBlock = spriteRenderer.HasPropertyBlock();
                if (spriteRenderer.material != null)
                {
                    currentSharedMaterialTexture = spriteRenderer.material.mainTexture;
                }

                Debug.Log($"[RelicPickupDetector Debug] GameObj: '{gameObject.name}' | Sprite: '{spriteRenderer.sprite.name}' | SpriteTexture: '{(currentSpriteTexture != null ? currentSpriteTexture.name : "null")}' | PropertyBlock_MainTex: '{(currentPropertyBlockTexture != null ? currentPropertyBlockTexture.name : "null")}' | Material_MainTex: '{(currentSharedMaterialTexture != null ? currentSharedMaterialTexture.name : "null")}' | HasPropertyBlock: {hasPropertyBlock}", this);
            }
            else
            {
                currentSpriteTexture = null;
                currentPropertyBlockTexture = null;
                if (spriteRenderer.material != null)
                {
                    currentSharedMaterialTexture = spriteRenderer.material.mainTexture;
                }
                Debug.LogWarning($"[RelicPickupDetector Debug] GameObj: '{gameObject.name}' | SpriteRenderer.sprite가 null입니다! Material_MainTex: '{(currentSharedMaterialTexture != null ? currentSharedMaterialTexture.name : "null")}'", this);
            }
        }

        private void Update()
        {
            if (!isInitialized)
            {
                return;
            }

            SubscribeInput();
            RefreshTarget();
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
            if (currentTarget != this || !isInitialized)
            {
                return;
            }

            collectAction.Invoke();
        }

        private static void RefreshTarget()
        {
            RelicPickupDetector closestDetector = null;
            float closestDistanceSqr = float.MaxValue;

            foreach (RelicPickupDetector detector in activeDetectors)
            {
                if (detector == null || !detector.isInitialized || !detector.isActiveAndEnabled)
                {
                    continue;
                }

                // 파괴되었거나 null인 Transform/GameObject 참조 체크
                if (detector.playerTransform == null || detector.transform == null)
                {
                    continue;
                }

                float distanceSqr = ((Vector2)(detector.playerTransform.position - detector.transform.position)).sqrMagnitude;
                if (distanceSqr > detector.pickupDistance * detector.pickupDistance || distanceSqr >= closestDistanceSqr)
                {
                    continue;
                }

                closestDetector = detector;
                closestDistanceSqr = distanceSqr;
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
                currentSpriteTexture = spriteRenderer.sprite.texture;

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

            currentPropertyBlockTexture = propertyBlock.GetTexture(MainTexProperty);
            hasPropertyBlock = spriteRenderer.HasPropertyBlock();
            if (spriteRenderer.material != null)
            {
                currentSharedMaterialTexture = spriteRenderer.material.mainTexture;
            }

            Debug.Log($"[RelicPickupDetector Highlight] GameObj: '{gameObject.name}' | Highlighted: {highlighted} | SpriteTexture: '{(currentSpriteTexture != null ? currentSpriteTexture.name : "null")}' | PropertyBlock_MainTex: '{(currentPropertyBlockTexture != null ? currentPropertyBlockTexture.name : "null")}' | Material_MainTex: '{(currentSharedMaterialTexture != null ? currentSharedMaterialTexture.name : "null")}'", this);
        }
    }
}
