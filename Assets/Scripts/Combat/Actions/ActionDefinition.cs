using UnityEngine;

[CreateAssetMenu(fileName = "ActionDefinition", menuName = "ACT/Combat/Action Definition")]
public class ActionDefinition : ScriptableObject
{
    [SerializeField] string id = "player_attack_1";
    [SerializeField] string displayName = "Attack 1";
    [SerializeField] AnimationClip animationClip;
    [SerializeField] float sampleRate = 30f;
    [SerializeField] int totalFrames;
    [SerializeField] CombatActionType actionType = CombatActionType.Attack;
    [SerializeField] float crossFadeDuration = 0.1f;

    public string Id => id;
    public string DisplayName => displayName;
    public AnimationClip AnimationClip => animationClip;
    public float SampleRate => sampleRate > 0f ? sampleRate : 30f;
    public int TotalFrames => totalFrames;
    public CombatActionType ActionType => actionType;
    public float CrossFadeDuration => crossFadeDuration;

    public float DurationSeconds
    {
        get
        {
            if (totalFrames > 0)
                return totalFrames / SampleRate;

            return animationClip != null ? animationClip.length : 0f;
        }
    }

    void OnValidate()
    {
        if (animationClip == null)
            return;

        if (string.IsNullOrEmpty(id))
            id = name;

        sampleRate = Mathf.Max(1f, sampleRate);
        totalFrames = Mathf.Max(1, Mathf.RoundToInt(animationClip.length * sampleRate));
    }
}
