using UnityEngine;

public class EnemyData : ScriptableObject
{
    public int enemyName;
    public Material material;
    public AnimatedMeshScriptableObject animation;
    public Vector3 rendererOffset;
    public Vector3 rendererRotationOffset;
    public float rendererScale = 1f;
    public float colliderRadius = 1f;
    public float overrideHeight;
    public Vector3 colliderCenter;
    public int hp = 10;
    public int damage = 10;
    public int shield;
    public bool isPoison;
    public float knockbackResistance;
    public bool nukeProtection;
    public float mass = 30f;
    public float speed = 5f;
    public bool isFlying;
    public float teleportCooldown = 10f;
    public bool isRunningFromPlayer;
    public float minStayAtDistance;
    public float maxStayAtDistance;
    public int xp = 1;
    public float despawnTime;
    public bool canBeElite = true;
    public bool canBeExecuted = true;
    public float maxSuckTime = 15f;
    public int maps;
    public int minStage;
    public float creditCost = 1f;
    public float canSpawnAfterTime;
}
