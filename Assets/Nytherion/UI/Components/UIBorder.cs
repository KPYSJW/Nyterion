using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class UIBorder : Graphic
{
    [Tooltip("외곽선의 두께")]
    public float lineThickness = 2.0f;

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();

        Rect rect = rectTransform.rect;

        float xMin = rect.x;
        float yMin = rect.y;
        float xMax = rect.x + rect.width;
        float yMax = rect.y + rect.height;

        var v0 = new Vector3(xMin, yMin);
        var v1 = new Vector3(xMin, yMax);
        var v2 = new Vector3(xMax, yMax);
        var v3 = new Vector3(xMax, yMin);

        var v4 = new Vector3(xMin + lineThickness, yMin + lineThickness);
        var v5 = new Vector3(xMin + lineThickness, yMax - lineThickness);
        var v6 = new Vector3(xMax - lineThickness, yMax - lineThickness);
        var v7 = new Vector3(xMax - lineThickness, yMin + lineThickness);

        vh.AddUIVertexQuad(new UIVertex[]
        {
            new UIVertex { position = v0, color = color },
            new UIVertex { position = v1, color = color },
            new UIVertex { position = v5, color = color },
            new UIVertex { position = v4, color = color }
        });

        vh.AddUIVertexQuad(new UIVertex[]
        {
            new UIVertex { position = v1, color = color },
            new UIVertex { position = v2, color = color },
            new UIVertex { position = v6, color = color },
            new UIVertex { position = v5, color = color }
        });

        vh.AddUIVertexQuad(new UIVertex[]
        {
            new UIVertex { position = v2, color = color },
            new UIVertex { position = v3, color = color },
            new UIVertex { position = v7, color = color },
            new UIVertex { position = v6, color = color }
        });

        vh.AddUIVertexQuad(new UIVertex[]
        {
            new UIVertex { position = v3, color = color },
            new UIVertex { position = v0, color = color },
            new UIVertex { position = v4, color = color },
            new UIVertex { position = v7, color = color }
        });
    }
}