using System.Collections.Generic;
using Nytherion.GamePlay.Combat;
using UnityEngine;

namespace Nytherion.GamePlay.Skills
{
    /// <summary>분열탄의 이동·색상을 적용하고 반환 시 기존 상태를 복구합니다.</summary>
    public sealed class ProjectileGateFlight : MonoBehaviour
    {
        private static readonly int FlashColorId = Shader.PropertyToID("_FlashColor");
        private static readonly int FlashAmountId = Shader.PropertyToID("_FlashAmount");
        private static readonly int OutlineEnabledId = Shader.PropertyToID("_OutlineEnabled");
        private static readonly int OutlineColorId = Shader.PropertyToID("_OutlineColor");
        private static readonly int OutlineWidthId = Shader.PropertyToID("_OutlineWidth");
        private static readonly int PreserveHighlightsId = Shader.PropertyToID("_PreserveHighlights");
        private static readonly int HighlightColorId = Shader.PropertyToID("_HighlightColor");
        private static readonly int HighlightStrengthId = Shader.PropertyToID("_HighlightStrength");
        private readonly List<Behaviour> suspended = new List<Behaviour>();
        private readonly List<RendererState> rendererStates = new List<RendererState>();
        private MaterialPropertyBlock tintProperties;
        private CollisionObject collision;
        private float expiresAt;
        private bool flying;

        private struct RendererState
        {
            public SpriteRenderer renderer;
            public Material material;
            public MaterialPropertyBlock properties;
        }

        public void Begin(float lifetime, Quaternion rotation, Material projectileMaterial)
        {
            collision = GetComponent<CollisionObject>();
            Suspend(GetComponent<HomingProj>());
            Suspend(GetComponent<SineWaveProj>());
            Suspend(GetComponent<BoomerangMovement>());
            // 기존 거리 제한은 출구 위치를 기준으로 새로 시작하지 않도록 비활성화합니다.
            Suspend(GetComponent<ProjDistanceLimit>());
            Suspend(GetComponent<ProjCameraBoundsLimit>());
            Suspend(GetComponent<AutoReturnToPool>());
            ApplyProjectileMaterial(projectileMaterial);
            transform.rotation = rotation;
            expiresAt = Time.time + Mathf.Max(0.1f, lifetime);
            flying = true;
        }

        private void ApplyProjectileMaterial(Material material)
        {
            if (material == null) return;
            if (tintProperties == null) tintProperties = new MaterialPropertyBlock();
            foreach (SpriteRenderer renderer in GetComponentsInChildren<SpriteRenderer>(true))
            {
                MaterialPropertyBlock original = new MaterialPropertyBlock();
                renderer.GetPropertyBlock(original);
                rendererStates.Add(new RendererState
                {
                    renderer = renderer, material = renderer.sharedMaterial, properties = original
                });
                renderer.sharedMaterial = material;
                renderer.GetPropertyBlock(tintProperties);
                // 원래 색이 푸른 투사체도 어두워지지 않게 색상 혼합 셰이더를 사용합니다.
                // 투명도와 애니메이션은 기존 SpriteRenderer 설정을 그대로 따릅니다.
                tintProperties.SetColor(FlashColorId, material.GetColor(FlashColorId));
                tintProperties.SetFloat(FlashAmountId, material.GetFloat(FlashAmountId));
                tintProperties.SetFloat(OutlineEnabledId, material.GetFloat(OutlineEnabledId));
                tintProperties.SetColor(OutlineColorId, material.GetColor(OutlineColorId));
                tintProperties.SetFloat(OutlineWidthId, material.GetFloat(OutlineWidthId));
                tintProperties.SetFloat(PreserveHighlightsId, material.GetFloat(PreserveHighlightsId));
                tintProperties.SetColor(HighlightColorId, material.GetColor(HighlightColorId));
                tintProperties.SetFloat(HighlightStrengthId, material.GetFloat(HighlightStrengthId));
                renderer.SetPropertyBlock(tintProperties);
            }
        }

        private void Suspend(Behaviour component)
        {
            if (component == null || !component.enabled) return;
            suspended.Add(component);
            component.enabled = false;
        }

        private void Update()
        {
            if (flying && Time.time >= expiresAt) collision.ReturnToPool();
        }

        private void OnDisable()
        {
            flying = false;
            foreach (RendererState state in rendererStates)
            {
                if (state.renderer == null) continue;
                state.renderer.sharedMaterial = state.material;
                state.renderer.SetPropertyBlock(state.properties.isEmpty ? null : state.properties);
            }
            rendererStates.Clear();
            foreach (Behaviour component in suspended)
                if (component != null) component.enabled = true;
            suspended.Clear();
        }
    }
}
