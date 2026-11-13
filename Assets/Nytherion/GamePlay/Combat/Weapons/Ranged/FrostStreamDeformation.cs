using System.Collections.Generic;
using UnityEngine;

namespace Nytherion.GamePlay.Combat.Weapons
{
    /// <summary>냉기 이미지와 피해 윤곽을 같은 중심선으로 휘고 끝부분의 조준 반응을 늦춥니다.</summary>
    public sealed class FrostStreamDeformation : MonoBehaviour
    {
        private const int Segments = 24;
        private readonly float[] angles = new float[Segments + 1];
        private readonly Vector2[] centers = new Vector2[Segments + 1];
        private readonly Vector2[] normals = new Vector2[Segments + 1];
        private readonly Vector3[] vertices = new Vector3[(Segments + 1) * 2];
        private readonly Vector2[] uvs = new Vector2[(Segments + 1) * 2];
        private readonly List<Vector2> sourceShape = new List<Vector2>();
        private readonly List<Vector2> bentShape = new List<Vector2>();
        private Mesh mesh;
        private MeshRenderer meshRenderer;
        private Material material;
        private MaterialPropertyBlock properties;

        private void Awake() => EnsureInitialized();

        private bool EnsureInitialized()
        {
            if (meshRenderer != null && mesh != null && material != null) return true;
            Shader shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
            if (shader == null || !shader.isSupported) shader = Shader.Find("Sprites/Default");
            if (shader == null || !shader.isSupported) return false;

            // SpriteRenderer와 MeshFilter는 같은 오브젝트에 공존할 수 없어 별도 자식에 둡니다.
            GameObject visual = new GameObject("냉기 곡선 이미지");
            visual.transform.SetParent(transform, false);
            mesh = new Mesh { name = "냉기 방사 변형 메시" };
            mesh.MarkDynamic();
            visual.AddComponent<MeshFilter>().sharedMesh = mesh;
            meshRenderer = visual.AddComponent<MeshRenderer>();
            material = new Material(shader);
            meshRenderer.sharedMaterial = material;
            meshRenderer.enabled = false;
            meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;
            properties = new MaterialPropertyBlock();
            int[] triangles = new int[Segments * 6];
            Color[] colors = new Color[vertices.Length];
            Vector3[] vertexNormals = new Vector3[vertices.Length];
            Vector4[] tangents = new Vector4[vertices.Length];
            for (int i = 0; i < vertices.Length; i++)
            {
                colors[i] = Color.white;
                vertexNormals[i] = Vector3.back;
                tangents[i] = new Vector4(1f, 0f, 0f, -1f);
            }
            for (int i = 0; i < Segments; i++)
            {
                int v = i * 2;
                int t = i * 6;
                triangles[t] = v;
                triangles[t + 1] = v + 1;
                triangles[t + 2] = v + 2;
                triangles[t + 3] = v + 2;
                triangles[t + 4] = v + 1;
                triangles[t + 5] = v + 3;
            }
            mesh.vertices = vertices;
            mesh.colors = colors;
            mesh.normals = vertexNormals;
            mesh.tangents = tangents;
            mesh.triangles = triangles;
            return true;
        }

        public void ResetDirection(Vector2 aim)
        {
            float angle = Mathf.Atan2(aim.y, aim.x) * Mathf.Rad2Deg;
            for (int i = 0; i <= Segments; i++) angles[i] = angle;
        }

        public bool SetVisible(bool visible)
        {
            if (!EnsureInitialized()) return false;
            meshRenderer.enabled = visible;
            return true;
        }

        public void Draw(Sprite frame, Vector2 aim, float length, float followDelay, float maxAngle,
            float deltaTime, PolygonCollider2D collider, int sortingLayer, int sortingOrder, float widthSign = 1f,
            bool updateCollider = true)
        {
            float targetAngle = Mathf.Atan2(aim.y, aim.x) * Mathf.Rad2Deg;
            float step = length / Segments;
            centers[0] = Vector2.zero;
            for (int i = 0; i <= Segments; i++)
            {
                float progress = (float)i / Segments;
                // 총구는 고정하고 끝부분일수록 반응을 늦춥니다.
                float delay = Mathf.Max(0.001f, followDelay * progress * progress);
                float offset = Mathf.Clamp(Mathf.DeltaAngle(targetAngle, angles[i]), -maxAngle, maxAngle);
                angles[i] = i == 0 ? targetAngle :
                    Mathf.LerpAngle(targetAngle + offset, targetAngle, 1f - Mathf.Exp(-deltaTime / delay));
                float radians = angles[i] * Mathf.Deg2Rad;
                Vector2 direction = new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));
                normals[i] = new Vector2(-direction.y, direction.x) * widthSign;
                if (i > 0)
                {
                    float middle = Mathf.LerpAngle(angles[i - 1], angles[i], 0.5f) * Mathf.Deg2Rad;
                    centers[i] = centers[i - 1] + new Vector2(Mathf.Cos(middle), Mathf.Sin(middle)) * step;
                }
            }

            Bounds bounds = frame.bounds;
            float scale = length / bounds.size.x;
            Rect textureRect = frame.textureRect;
            for (int i = 0; i <= Segments; i++)
            {
                float progress = (float)i / Segments;
                vertices[i * 2] = centers[i] + normals[i] * (bounds.min.y * scale);
                vertices[i * 2 + 1] = centers[i] + normals[i] * (bounds.max.y * scale);
                float u = (textureRect.x + textureRect.width * progress) / frame.texture.width;
                uvs[i * 2] = new Vector2(u, textureRect.yMin / frame.texture.height);
                uvs[i * 2 + 1] = new Vector2(u, textureRect.yMax / frame.texture.height);
            }
            mesh.vertices = vertices;
            mesh.uv = uvs;
            mesh.RecalculateBounds();
            properties.SetTexture("_MainTex", frame.texture);
            meshRenderer.SetPropertyBlock(properties);
            meshRenderer.sortingLayerID = sortingLayer;
            meshRenderer.sortingOrder = sortingOrder;

            // 시각적 변형은 매 프레임 유지하고 비싼 물리 도형 재생성은 피해 틱으로 제한합니다.
            if (!updateCollider) return;
            collider.pathCount = frame.GetPhysicsShapeCount();
            for (int path = 0; path < collider.pathCount; path++)
            {
                frame.GetPhysicsShape(path, sourceShape);
                bentShape.Clear();
                for (int i = 0; i < sourceShape.Count; i++)
                {
                    Vector2 from = sourceShape[i];
                    Vector2 to = sourceShape[(i + 1) % sourceShape.Count];
                    // 긴 윤곽선도 나눠 표시와 같은 곡선을 따르게 합니다.
                    int divisions = Mathf.Max(1, Mathf.CeilToInt(Mathf.Abs(to.x - from.x) / bounds.size.x * Segments));
                    for (int j = 0; j < divisions; j++)
                    {
                        Vector2 point = Vector2.Lerp(from, to, (float)j / divisions);
                        bentShape.Add(TransformSpritePoint(frame, point, length));
                    }
                }
                collider.SetPath(path, bentShape);
            }
        }

        public Vector2 TransformSpritePoint(Sprite frame, Vector2 point, float length)
        {
            Bounds bounds = frame.bounds;
            float position = Mathf.Clamp01((point.x - bounds.min.x) / bounds.size.x) * Segments;
            int index = Mathf.Min(Mathf.FloorToInt(position), Segments - 1);
            float fraction = position - index;
            Vector2 center = Vector2.Lerp(centers[index], centers[index + 1], fraction);
            Vector2 normal = Vector2.Lerp(normals[index], normals[index + 1], fraction);
            return center + normal * (point.y * length / bounds.size.x);
        }

        private void OnDestroy()
        {
            if (Application.isPlaying)
            {
                if (mesh != null) Destroy(mesh);
                if (material != null) Destroy(material);
            }
            else
            {
                if (mesh != null) DestroyImmediate(mesh);
                if (material != null) DestroyImmediate(material);
            }
        }
    }
}
