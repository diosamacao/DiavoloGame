using UnityEngine;

/// <summary>招式数据：动画、连招衔接、位移与取消窗口等帧级配置。</summary>
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
    [SerializeField] int comboLinkStartFrame;
    [SerializeField] int comboLinkEndFrame;

    [Header("Movement Cancel")]
    [SerializeField] int movementCancelStartFrame;
    [SerializeField] int movementCancelEndFrame;

    [Header("Movement")]
    [Tooltip("开启时由动画 Root Motion 驱动位移，脚本位移（Displacement Distance）将被忽略。")]
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
    public bool UseRootMotion => useRootMotion;
    public float DisplacementDistance => displacementDistance;
    public int DisplacementStartFrame => displacementStartFrame;
    public int DisplacementEndFrame => displacementEndFrame;
    public int MovementCancelStartFrame => movementCancelStartFrame;
    public int MovementCancelEndFrame => movementCancelEndFrame;
    public bool HasScriptedDisplacement => !useRootMotion && Mathf.Abs(displacementDistance) > 0.001f;
    /// <summary>是否配置了移动取消窗口（起止帧均有效且 end &gt; start）。</summary>
    public bool HasMovementCancel => movementCancelEndFrame > movementCancelStartFrame;

    public float DurationSeconds
    {
        get
        {
            if (totalFrames > 0)
                return totalFrames / SampleRate;

            return animationClip != null ? animationClip.length : 0f;
        }
    }

    /// <summary>是否配置了 ComboLink 帧窗口（与下一段招式无关，由运行时按输入解析）。</summary>
    public bool HasComboLink => comboLinkEndFrame > comboLinkStartFrame;

    public bool IsInComboLinkWindow(float elapsedSeconds)
    {
        if (!HasComboLink || totalFrames <= 0)
            return false;

        int frame = FrameAt(elapsedSeconds);
        return frame >= comboLinkStartFrame && frame <= comboLinkEndFrame;
    }

    /// <summary>ComboLink 窗口内是否允许该输入衔接（M2：Attack / Dodge 均可互切）。</summary>
    public bool AllowsComboInput(InputSlot slot) =>
        slot == InputSlot.Attack || slot == InputSlot.Dodge;

    /// <summary>当前播放时刻是否落在移动取消窗口内。</summary>
    public bool IsInMovementCancelWindow(float elapsedSeconds)
    {
        if (!HasMovementCancel || totalFrames <= 0)
            return false;

        int frame = FrameAt(elapsedSeconds);
        return frame >= movementCancelStartFrame && frame <= movementCancelEndFrame;
    }

    public bool IsInDisplacementWindow(float elapsedSeconds)
    {
        if (!HasScriptedDisplacement || totalFrames <= 0)
            return false;

        int frame = FrameAt(elapsedSeconds);
        return frame >= displacementStartFrame && frame <= displacementEndFrame;
    }

    int FrameAt(float elapsedSeconds) => Mathf.FloorToInt(elapsedSeconds * SampleRate);

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

        if (comboLinkEndFrame > 0 && comboLinkStartFrame <= 0)
            comboLinkStartFrame = Mathf.Max(1, Mathf.RoundToInt(totalFrames * 0.5f));

        if (comboLinkStartFrame > 0 && comboLinkEndFrame <= 0)
            comboLinkEndFrame = totalFrames - 1;

        comboLinkStartFrame = Mathf.Clamp(comboLinkStartFrame, 0, Mathf.Max(0, totalFrames - 1));
        comboLinkEndFrame = Mathf.Clamp(comboLinkEndFrame, comboLinkStartFrame, Mathf.Max(0, totalFrames - 1));

        if (Mathf.Abs(displacementDistance) > 0.001f)
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

        if (movementCancelEndFrame > 0 && movementCancelStartFrame <= 0)
            movementCancelStartFrame = Mathf.Max(1, Mathf.RoundToInt(totalFrames * 0.5f));

        if (movementCancelStartFrame > 0 && movementCancelEndFrame <= 0)
            movementCancelEndFrame = totalFrames - 1;

        movementCancelStartFrame = Mathf.Clamp(movementCancelStartFrame, 0, Mathf.Max(0, totalFrames - 1));
        movementCancelEndFrame = Mathf.Clamp(
            movementCancelEndFrame,
            movementCancelStartFrame,
            Mathf.Max(0, totalFrames - 1));
    }
}
