using System.Text;
using UnityEngine;

/// <summary>
/// IMGUI 战斗调试面板；只读采样 CharacterDebugSnapshot（含 Numeric Attribute/Effects/Flags），不写 Sim。
/// </summary>
#if UNITY_EDITOR || DEVELOPMENT_BUILD
[DefaultExecutionOrder(1000)]
public sealed class CombatDebugHudController : AppControllerBase
{
    [SerializeField] PlayerController playerController;
    [SerializeField] bool visible = true;
    [SerializeField] KeyCode toggleKey = KeyCode.F3;
    [SerializeField] KeyCode hurtboxToggleKey = KeyCode.F4;

    CharacterDebugSnapshot _cached;
    readonly StringBuilder _sb = new(512);
    GUIStyle _boxStyle;
    GUIStyle _labelStyle;

    void Awake()
    {
        if (playerController == null)
            playerController = FindObjectOfType<PlayerController>();

        EnsureHurtboxVisualizer();
    }

    void Update()
    {
        if (UnityEngine.Input.GetKeyDown(toggleKey))
            visible = !visible;

        if (UnityEngine.Input.GetKeyDown(hurtboxToggleKey))
            CombatHurtboxDebugSettings.ShowHurtboxes = !CombatHurtboxDebugSettings.ShowHurtboxes;
    }

    /// <summary>确保场景有 Hurtbox 线框绘制器（可挂本物体上）。</summary>
    void EnsureHurtboxVisualizer()
    {
        if (GetComponent<CombatHurtboxDebugVisualizer>() != null)
            return;
        if (FindObjectOfType<CombatHurtboxDebugVisualizer>() != null)
            return;

        gameObject.AddComponent<CombatHurtboxDebugVisualizer>();
    }

    void LateUpdate()
    {
        if (!visible || playerController == null || playerController.Actor == null)
            return;

        // 在表现插值之后采样，避免 OnGUI 与逻辑帧交错读到半帧状态。
        _cached = playerController.Actor.BuildDebugSnapshot();
    }

    void OnGUI()
    {
        if (!visible)
            return;

        EnsureStyles();
        _sb.Clear();
        AppendSnapshot(_sb, in _cached);
        const float width = 440f;
        float height = Mathf.Min(420f, Screen.height * 0.55f);
        GUI.Box(new Rect(8f, 8f, width, height), GUIContent.none, _boxStyle);
        GUI.Label(new Rect(16f, 16f, width - 16f, height - 16f), _sb.ToString(), _labelStyle);
    }

    static void AppendSnapshot(StringBuilder sb, in CharacterDebugSnapshot s)
    {
        sb.AppendLine("[Combat Debug]  F3 HUD | F4 Hurtbox")
            .Append("Hurtbox Gizmo: ")
            .Append(CombatHurtboxDebugSettings.ShowHurtboxes ? "ON" : "OFF")
            .AppendLine();
        sb.Append("State: ").Append(s.State);
        if (s.ActionActive)
        {
            sb.Append(" | Frame: ").Append(s.ActionFrame).Append('/').Append(s.ActionTotalFrames);
            sb.Append(" | Action: ").Append(s.ActionName);
        }

        sb.Append(" | Freeze: ").Append(s.FreezeFrames).AppendLine();
        sb.Append("HP: ").Append(s.CurrentHp.ToString("0.#")).Append('/')
            .Append(s.MaxHp.ToString("0.#")).AppendLine();
        sb.Append("ATK/DEF: ").Append(s.AttackPoints).Append('/').Append(s.DefensePoints)
            .Append("  Out×").Append((s.OutgoingDamageMultMilli / 1000f).ToString("0.##"))
            .Append(" In×").Append((s.IncomingDamageMultMilli / 1000f).ToString("0.##"))
            .AppendLine();
        sb.Append("EX: ").Append(s.EnergyPoints).Append('/').Append(s.MaxEnergy)
            .Append("  (+regen ").Append(s.EnergyRegenMilliPerFrame).Append("m/f)")
            .Append("   Decibel: ").Append(s.Decibel).Append('/').Append(s.MaxDecibel)
            .AppendLine();
        sb.Append("Next Special: ").Append(s.NextSpecialForm).AppendLine();
        sb.Append("Dodge: ").Append(s.DodgeCharges).Append('/').Append(s.MaxDodgeCharges)
            .Append("  (recharge ").Append(s.DodgeRechargeFramesLeft).Append("f)")
            .AppendLine();
        sb.Append("InCombat: ").Append(s.InCombat ? "YES" : "NO")
            .Append(" (hold ").Append(s.InCombatHoldFrames).Append("f)")
            .Append("  PDCounter: ").Append(s.PerfectDodgeCounterFrames).Append("f")
            .AppendLine();
        sb.Append("Effects:");
        if (s.ActiveEffects.Length == 0)
            sb.Append(" (none)");
        else
        {
            for (int i = 0; i < s.ActiveEffects.Length; i++)
            {
                NumericEffectDebugEntry e = s.ActiveEffects[i];
                sb.Append(' ').Append(e.Id)
                    .Append('[').Append(e.Policy).Append(']')
                    .Append('x').Append(e.StackCount)
                    .Append('@').Append(e.RemainingFrames).Append('f');
            }
        }

        sb.AppendLine();
        sb.Append("FrameIntents:");
        if (s.FrameIntents.Length == 0)
            sb.Append(" (none)");
        else
        {
            for (int i = 0; i < s.FrameIntents.Length; i++)
                sb.Append(' ').Append(s.FrameIntents[i]);
        }

        sb.AppendLine();
        sb.Append("Buffers:");
        if (s.Buffers.Length == 0)
            sb.Append(" (none)");
        else
        {
            for (int i = 0; i < s.Buffers.Length; i++)
            {
                sb.Append(' ').Append(s.Buffers[i].Intent)
                    .Append('(').Append(s.Buffers[i].RemainingFrames).Append("f)");
            }
        }

        sb.AppendLine();
        sb.Append("Lock: ").Append(s.HasLock ? "YES" : "NO");
        if (s.HasLock)
            sb.Append(" | ").Append(s.LockTargetName)
                .Append(" | Dist=").Append(s.LockDistanceMeters.ToString("0.00"));
        sb.AppendLine();
        sb.Append("Motor mm: (").Append(s.MotorXMm).Append(", ").Append(s.MotorYMm)
            .Append(", ").Append(s.MotorZMm).Append(") facing=")
            .Append(s.MotorFacingMilliDeg).AppendLine();
        sb.Append("SoftBody: mass=").Append(s.SoftBodyMass)
            .Append(" immovable=").Append(s.SoftBodyImmovable).AppendLine();
        sb.Append("ActionLateralPeakMm: ").Append(s.ActionLateralPeakMm)
            .Append("  (Wave0 baseline; 对照横摆是否进逻辑根)");
    }

    void EnsureStyles()
    {
        if (_boxStyle != null)
            return;

        _boxStyle = new GUIStyle(GUI.skin.box);
        Texture2D tex = new Texture2D(1, 1);
        tex.SetPixel(0, 0, new Color(0f, 0f, 0f, 0.72f));
        tex.Apply();
        _boxStyle.normal.background = tex;

        _labelStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 13,
            richText = false,
            wordWrap = true,
            normal = { textColor = Color.white },
        };
    }
}
#else
/// <summary>非开发构建设空壳，避免场景引用丢失报错。</summary>
public sealed class CombatDebugHudController : AppControllerBase
{
}
#endif
