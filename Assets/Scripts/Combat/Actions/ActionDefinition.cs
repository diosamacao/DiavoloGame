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
    [SerializeField] ActionDefinition nextAction;
    [SerializeField] int comboLinkStartFrame;
    [SerializeField] int comboLinkEndFrame;

    [Header("Movement")]
    [SerializeField] bool useRootMotion = true;
    [SerializeField] float displacementDistance;
    [SerializeField] int displacementStartFrame;
    [SerializeField] int displacementEndFrame;

    public string Id => id;
    public string DisplayName => displayName;
    public AnimationClip AnimationClip => animationClip;
    public float SampleRate => sampleRate > 0f ? sampleRate : 30f;
    public int TotalFrames => totalFrames;
    public CombatActionType ActionType => actionType;
    public float CrossFadeDuration => crossFadeDuration;
    public ActionDefinition NextAction => nextAction;
    public bool UseRootMotion => useRootMotion;
    public float DisplacementDistance => displacementDistance;
    public int DisplacementStartFrame => displacementStartFrame;
    public int DisplacementEndFrame => displacementEndFrame;
    public bool HasScriptedDisplacement => !useRootMotion && displacementDistance > 0f;

    public float DurationSeconds
    {
        get
        {
            if (totalFrames > 0)
                return totalFrames / SampleRate;

            return animationClip != null ? animationClip.length : 0f;
        }
    }

    public bool HasComboLink => nextAction != null;

    public bool IsInComboLinkWindow(float elapsedSeconds)
    {
        if (!HasComboLink || totalFrames <= 0)
            return false;

        int frame = Mathf.FloorToInt(elapsedSeconds * SampleRate);
        return frame >= comboLinkStartFrame && frame <= comboLinkEndFrame;
    }

    public bool IsInDisplacementWindow(float elapsedSeconds)
    {
        if (!HasScriptedDisplacement || totalFrames <= 0)
            return false;

        int frame = Mathf.FloorToInt(elapsedSeconds * SampleRate);
        return frame >= displacementStartFrame && frame <= displacementEndFrame;
    }

    public float DisplacementSpeed
    {
        get
        {
            if (!HasScriptedDisplacement)
                return 0f;

            int frameCount = displacementEndFrame - displacementStartFrame + 1;
            if (frameCount <= 0)
                return 0f;

            return displacementDistance / (frameCount / SampleRate);
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

        if (nextAction != null)
        {
            if (comboLinkStartFrame <= 0)
                comboLinkStartFrame = Mathf.Max(1, Mathf.RoundToInt(totalFrames * 0.5f));

            if (comboLinkEndFrame <= 0)
                comboLinkEndFrame = totalFrames - 1;
        }

        comboLinkStartFrame = Mathf.Clamp(comboLinkStartFrame, 0, Mathf.Max(0, totalFrames - 1));
        comboLinkEndFrame = Mathf.Clamp(comboLinkEndFrame, comboLinkStartFrame, Mathf.Max(0, totalFrames - 1));

        if (displacementDistance > 0f)
        {
            if (displacementEndFrame <= 0)
                displacementEndFrame = totalFrames - 1;

            if (displacementStartFrame <= 0 && displacementEndFrame > 0)
                displacementStartFrame = 0;
        }

        displacementStartFrame = Mathf.Clamp(displacementStartFrame, 0, Mathf.Max(0, totalFrames - 1));
        displacementEndFrame = Mathf.Clamp(
            displacementEndFrame,
            displacementStartFrame,
            Mathf.Max(0, totalFrames - 1));
    }
}
