using System.Collections.Generic;
using UnityEngine;

// A grind rail (SSX): a polyline the TrickSystem snaps onto when you land on or brush it.
public class Rail : MonoBehaviour
{
    public static readonly List<Rail> all = new List<Rail>();
    public List<Vector3> points = new List<Vector3>();
    public float[] dist;
    public float Length => dist != null && dist.Length > 0 ? dist[dist.Length - 1] : 0f;

    void OnEnable() { all.Add(this); }
    void OnDisable() { all.Remove(this); }

    public void Bake()
    {
        dist = new float[points.Count];
        for (int i = 1; i < points.Count; i++) dist[i] = dist[i - 1] + Vector3.Distance(points[i - 1], points[i]);
    }

    public Vector3 PointAt(float s, out Vector3 tangent)
    {
        s = Mathf.Clamp(s, 0f, Length);
        int i = 1; while (i < points.Count - 1 && dist[i] < s) i++;
        float seg = dist[i] - dist[i - 1];
        float t = seg > 0.0001f ? (s - dist[i - 1]) / seg : 0f;
        tangent = (points[i] - points[i - 1]).normalized;
        return Vector3.Lerp(points[i - 1], points[i], t);
    }

    // closest point on the rail to p: returns distance along rail and the point
    public float Closest(Vector3 p, out Vector3 point, out Vector3 tangent)
    {
        float best = float.MaxValue, bestS = 0f; point = points[0]; tangent = Vector3.forward;
        for (int i = 1; i < points.Count; i++)
        {
            var a = points[i - 1]; var b = points[i]; var ab = b - a; float l2 = ab.sqrMagnitude;
            float t = l2 > 0f ? Mathf.Clamp01(Vector3.Dot(p - a, ab) / l2) : 0f;
            var c = a + ab * t; float d = (p - c).sqrMagnitude;
            if (d < best) { best = d; bestS = dist[i - 1] + Mathf.Sqrt(l2) * t; point = c; tangent = ab.normalized; }
        }
        return bestS;
    }
}
