using System.Collections.Generic;
using UnityEngine;

namespace Nytherion.GamePlay.Characters.Enemy
{
    [DisallowMultipleComponent]
    public sealed class GoblinChargePreview : MonoBehaviour
    {
        [Header("Colors")]
        [SerializeField] private Color fillColor = new(1f, 0f, 0f, 0.13f);
        [SerializeField] private Color lineColor = new(1f, 0.08f, 0.08f, 0.82f);

        [Header("Shape")]
        [SerializeField, Min(0.01f)] private float outlineThickness = 0.04f;
        [SerializeField, Min(0.01f)] private float chevronThickness = 0.14f;
        [SerializeField, Min(0.05f)] private float arrowHeadLength = 0.42f;
        [SerializeField, Min(0.1f)] private float chevronSpacing = 0.8f;
        [SerializeField, Min(0f)] private float chevronMoveSpeed = 2f;
        [SerializeField] private int sortingOrder = -1;

        private readonly List<LineRenderer> chevrons = new();
        private MeshFilter meshFilter;
        private MeshRenderer meshRenderer;
        private LineRenderer outline;
        private Material fillMaterial;
        private Material lineMaterial;
        private Mesh fillMesh;
        private float previewLength = 1f;
        private float previewWidth = 0.65f;
        private float flowOffset;

        private void Awake()
        {
            EnsureRenderers();
            RebuildShape();
        }

        private void OnEnable()
        {
            EnsureRenderers();
            RebuildShape();
        }

        private void Update()
        {
            if (chevronSpacing <= 0f)
                return;

            flowOffset = Mathf.Repeat(
                flowOffset + chevronMoveSpeed * Time.deltaTime,
                chevronSpacing);
            UpdateChevrons();
        }

        public void Configure(float length, float width)
        {
            float newLength = Mathf.Max(0.1f, length);
            float newWidth = Mathf.Max(0.05f, width);

            if (Mathf.Approximately(previewLength, newLength) &&
                Mathf.Approximately(previewWidth, newWidth))
            {
                return;
            }

            previewLength = newLength;
            previewWidth = newWidth;
            EnsureRenderers();
            RebuildShape();
        }

        private void EnsureRenderers()
        {
            Shader spriteShader = Shader.Find("Sprites/Default");
            if (spriteShader == null)
                return;

            if (fillMaterial == null)
            {
                fillMaterial = new Material(spriteShader)
                {
                    name = "Goblin Charge Preview Fill (Runtime)",
                    color = fillColor
                };
            }

            if (lineMaterial == null)
            {
                lineMaterial = new Material(spriteShader)
                {
                    name = "Goblin Charge Preview Line (Runtime)",
                    color = Color.white
                };
            }

            if (meshFilter == null)
            {
                meshFilter = GetComponent<MeshFilter>();
                if (meshFilter == null) meshFilter = gameObject.AddComponent<MeshFilter>();
            }

            if (meshRenderer == null)
            {
                meshRenderer = GetComponent<MeshRenderer>();
                if (meshRenderer == null) meshRenderer = gameObject.AddComponent<MeshRenderer>();
                meshRenderer.sharedMaterial = fillMaterial;
                meshRenderer.sortingOrder = sortingOrder - 1;
            }

            if (fillMesh == null)
            {
                fillMesh = new Mesh { name = "Goblin Charge Preview Mesh" };
                meshFilter.sharedMesh = fillMesh;
            }

            if (outline == null)
            {
                GameObject outlineObject = new("Outline");
                outlineObject.layer = gameObject.layer;
                outlineObject.transform.SetParent(transform, false);
                outline = outlineObject.AddComponent<LineRenderer>();
                ConfigureLineRenderer(outline, outlineThickness);
                outline.loop = true;
                outline.positionCount = 5;
            }
        }

        private void ConfigureLineRenderer(LineRenderer line, float width)
        {
            line.useWorldSpace = false;
            line.sharedMaterial = lineMaterial;
            line.startColor = lineColor;
            line.endColor = lineColor;
            line.startWidth = width;
            line.endWidth = width;
            line.numCapVertices = 0;
            line.numCornerVertices = 0;
            line.alignment = LineAlignment.View;
            line.textureMode = LineTextureMode.Stretch;
            line.sortingOrder = sortingOrder;
        }

        private void RebuildShape()
        {
            if (fillMesh == null || outline == null)
                return;

            float halfWidth = previewWidth * 0.5f;
            float headLength = Mathf.Min(arrowHeadLength, previewLength * 0.25f);
            float bodyEnd = previewLength - headLength;

            Vector3[] vertices =
            {
                new(0f, -halfWidth, 0f),
                new(bodyEnd, -halfWidth, 0f),
                new(previewLength, 0f, 0f),
                new(bodyEnd, halfWidth, 0f),
                new(0f, halfWidth, 0f)
            };

            fillMesh.Clear();
            fillMesh.vertices = vertices;
            fillMesh.triangles = new[]
            {
                0, 1, 4,
                1, 3, 4,
                1, 2, 3
            };
            fillMesh.RecalculateBounds();

            outline.SetPositions(vertices);
            EnsureChevronCount(bodyEnd);
            UpdateChevrons();
        }

        private void EnsureChevronCount(float bodyEnd)
        {
            int requiredCount = Mathf.CeilToInt(bodyEnd / chevronSpacing) + 2;
            while (chevrons.Count < requiredCount)
            {
                GameObject chevronObject = new($"Chevron_{chevrons.Count}");
                chevronObject.layer = gameObject.layer;
                chevronObject.transform.SetParent(transform, false);

                LineRenderer chevron = chevronObject.AddComponent<LineRenderer>();
                ConfigureLineRenderer(chevron, chevronThickness);
                chevron.positionCount = 3;
                chevrons.Add(chevron);
            }

            for (int i = 0; i < chevrons.Count; i++)
            {
                chevrons[i].gameObject.SetActive(i < requiredCount);
            }
        }

        private void UpdateChevrons()
        {
            float headLength = Mathf.Min(arrowHeadLength, previewLength * 0.25f);
            float bodyEnd = previewLength - headLength;
            float halfWidth = previewWidth * 0.5f;
            float chevronHalfHeight = Mathf.Max(
                0f,
                halfWidth - chevronThickness * 0.5f);
            float chevronDepth = previewWidth * 0.55f;
            float cycleLength = bodyEnd + chevronSpacing;

            for (int i = 0; i < chevrons.Count; i++)
            {
                LineRenderer chevron = chevrons[i];
                float tipX = Mathf.Repeat(
                    i * chevronSpacing + flowOffset,
                    cycleLength);
                bool visible = tipX >= chevronDepth && tipX <= bodyEnd;

                chevron.enabled = visible;
                if (!visible)
                    continue;

                chevron.SetPosition(0, new Vector3(
                    tipX - chevronDepth,
                    -chevronHalfHeight,
                    0f));
                chevron.SetPosition(1, new Vector3(tipX, 0f, 0f));
                chevron.SetPosition(2, new Vector3(
                    tipX - chevronDepth,
                    chevronHalfHeight,
                    0f));
            }
        }

        private void OnDestroy()
        {
            if (fillMesh != null) Destroy(fillMesh);
            if (fillMaterial != null) Destroy(fillMaterial);
            if (lineMaterial != null) Destroy(lineMaterial);
        }

        private void OnValidate()
        {
            outlineThickness = Mathf.Max(0.01f, outlineThickness);
            chevronThickness = Mathf.Max(0.01f, chevronThickness);
            arrowHeadLength = Mathf.Max(0.05f, arrowHeadLength);
            chevronSpacing = Mathf.Max(0.1f, chevronSpacing);
            chevronMoveSpeed = Mathf.Max(0f, chevronMoveSpeed);
        }
    }
}
