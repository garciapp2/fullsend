using UnityEngine;
using UnityEngine.UI;

// skew = horizontal offset of the top edge as a fraction of the height. Works with Mask (portrait clip).
[RequireComponent(typeof(CanvasRenderer))]
public class Para : MaskableGraphic
{
    public float skew = 0.22f;
    public bool triangle;   // pointing up

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        var r = GetPixelAdjustedRect();
        Color32 c = color;
        if (triangle)
        {
            vh.AddVert(new Vector3(r.xMin, r.yMin), c, Vector2.zero);
            vh.AddVert(new Vector3(r.center.x, r.yMax), c, Vector2.zero);
            vh.AddVert(new Vector3(r.xMax, r.yMin), c, Vector2.zero);
            vh.AddTriangle(0, 1, 2);
            return;
        }
        float k = r.height * skew;
        vh.AddVert(new Vector3(r.xMin, r.yMin), c, Vector2.zero);
        vh.AddVert(new Vector3(r.xMin + k, r.yMax), c, Vector2.zero);
        vh.AddVert(new Vector3(r.xMax + k, r.yMax), c, Vector2.zero);
        vh.AddVert(new Vector3(r.xMax, r.yMin), c, Vector2.zero);
        vh.AddTriangle(0, 1, 2); vh.AddTriangle(2, 3, 0);
    }
}
