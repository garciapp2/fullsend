using System.Collections.Generic;
using UnityEngine;

// KeyPropSnapToGround) by SledbonkBuilder.MakeObstaclePrefabs and stored on the cleaned prefab, so the game can play the
public class ObstacleData : MonoBehaviour
{
    [System.Serializable] public class Spin { public Transform t; public Vector3 rotation; public float random; public bool world; }
    [System.Serializable] public class Mover
    {
        public Transform t; public List<Transform> points = new List<Transform>();
        public int startId; public float baseSpeed = 5f, threshold = 2f, lerp = 0.1f, drag = 0.98f, rotateSpeed = 1f;
        public Vector2 mult = Vector2.one; public bool randomStart, startAtFirst = true, loop = true;
    }
    [System.Serializable] public class Follow { public Transform t, target; public float damp = 0.5f, period = 0.5f, rotSpring = 5f; }
    [System.Serializable] public class Beam { public Transform t; public float maxLength = 300f, radius = 3f; }
    [System.Serializable] public class Snap { public Transform t; public float radius; }

    public List<Spin> spins = new List<Spin>();
    public List<Mover> movers = new List<Mover>();
    public List<Follow> follows = new List<Follow>();
    public List<Beam> beams = new List<Beam>();
    public List<Snap> snaps = new List<Snap>();
}
