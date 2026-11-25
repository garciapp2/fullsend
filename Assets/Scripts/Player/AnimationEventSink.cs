using UnityEngine;

// doesn't log "AnimationEvent has no receiver" every step. Hook sounds here later.
public class AnimationEventSink : MonoBehaviour
{
    public void WalkAnimationEvent_PlayFootstep() { }
}
