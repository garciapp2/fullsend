using System;

[Serializable]
public class RunStats
{
    public float runSpeed = 600f;
    public float airSpeed = 450f;
    public float turnSpeed = 1.5f;
    public float drag = 0.0014f;
    public float gravity = 22f;
    public float fastFallSpeed = 40f;
    public float fastFallLerp = 0.7f;
    public float boost = 0f;
    public float speedLossResistance = 0f;
}

[Serializable]
public class SlowRunStats
{
    public float speed = 130f;
    public float drag = 10f;
    public float airControl = 0.4f;
    public float jumpForce = 30f;
    public float gravity = 200f;
}

public enum MovementType { Slow, Fast }
public enum LandingType { None, Bad, Ok, Good, Perfect }
