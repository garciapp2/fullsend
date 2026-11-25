using System;
using System.Collections.Generic;
using UnityEngine;

public class AnimatedMeshScriptableObject : ScriptableObject
{
    [Serializable]
    public struct Animation
    {
        public string Name;
        public List<Mesh> Meshes;
    }

    public int AnimationFPS = 8;
    public List<Animation> Animations = new List<Animation>();

    public List<Mesh> Get(string name)
    {
        foreach (var a in Animations)
            if (a.Name == name && a.Meshes != null && a.Meshes.Count > 0) return a.Meshes;
        return Animations.Count > 0 ? Animations[0].Meshes : null;
    }
}
