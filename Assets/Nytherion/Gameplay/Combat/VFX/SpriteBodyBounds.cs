using System.Collections.Generic;
using UnityEngine;

namespace Nytherion.GamePlay.Combat
{
    // 상태이상 이펙트를 이미지의 투명 여백을 제외한 몸체에 배치합니다.
    internal sealed class SpriteBodyBounds
    {
        private Sprite cachedBodySprite;
        private Bounds localBodyBounds;
        private readonly List<Vector2> outlinePoints = new List<Vector2>();

        public Bounds GetBounds(SpriteRenderer body)
        {
            Sprite sprite = body.sprite;
            if (cachedBodySprite != sprite)
            {
                cachedBodySprite = sprite;
                bool hasPoint = false;
                localBodyBounds = default;
                // 물리 외형 윤곽은 이미지의 투명 여백을 제외합니다. 스프라이트별로 한 번만 읽습니다.
                for (int shape = 0; shape < sprite.GetPhysicsShapeCount(); shape++)
                {
                    sprite.GetPhysicsShape(shape, outlinePoints);
                    foreach (Vector2 point in outlinePoints)
                        IncludePoint(point, ref hasPoint);
                }
                if (!hasPoint)
                    foreach (Vector2 point in sprite.vertices)
                        IncludePoint(point, ref hasPoint);
                if (!hasPoint) localBodyBounds = sprite.bounds;
            }

            Vector3 center = localBodyBounds.center;
            if (body.flipX) center.x = -center.x;
            if (body.flipY) center.y = -center.y;
            Bounds world = new Bounds(body.transform.TransformPoint(center), Vector3.zero);
            for (int i = 0; i < 4; i++)
            {
                Vector3 corner = center + new Vector3(
                    (i % 2 == 0 ? -1f : 1f) * localBodyBounds.extents.x,
                    (i < 2 ? -1f : 1f) * localBodyBounds.extents.y, 0f);
                world.Encapsulate(body.transform.TransformPoint(corner));
            }
            return world;
        }

        private void IncludePoint(Vector2 point, ref bool hasPoint)
        {
            if (hasPoint) localBodyBounds.Encapsulate(point);
            else
            {
                localBodyBounds = new Bounds(point, Vector3.zero);
                hasPoint = true;
            }
        }

    }
}
