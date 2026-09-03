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
    [SerializeField] KeyCode flinchProbeKey = KeyCode.F6;
    [Tooltip("P-HR0 探针 Clip；须 Additive 导入、无根移。Agent 不改资产。")]
    [SerializeField] AnimationClip flinchProbeClip;
    [Tooltip("可选上半身 AvatarMask；空则全骨骼叠加。")]
    [SerializeField] AvatarMask flinchProbeMask;
    [SerializeField] float flinchProbeFade = 0.05f;

    CharacterDebugSnapshot _cached;
    CharacterDebugSnapshot _targetCached;
    bool _hasTargetSnapshot;
    /// <summary>探针目标可见体 Additive 权重；Listen 无头 Actor 快照恒为 0，须读 Proxy。</summary>
    float _targetAdditiveWeight;
    HitReactionKind _targetReactionKind;
    CameraManager _cameraManager;
    readonly StringBuilder _sb = new(512);
    GUIStyle _boxStyle;
    GUIStyle _labelStyle;

    void Awake()
    {
        if (playerController == null)
            playerController = SendQuery(new GetLocalPlayerQuery()) as PlayerController;
        _cameraManager = FindObjectOfType<CameraManager>();

        EnsureHurtboxVisualizer();
    }

    void Update()
    {
        // F3：开关战斗 HUD
        if (UnityEngine.Input.GetKeyDown(toggleKey))
            visible = !visible;

        // F4：开关 Hurtbox 线框
        if (UnityEngine.Input.GetKeyDown(hurtboxToggleKey))
            CombatHurtboxDebugSettings.ShowHurtboxes = !CombatHurtboxDebugSettings.ShowHurtboxes;

        // F6：P-HR0 Additive 探针，不走命中 / EnterHit
        if (UnityEngine.Input.GetKeyDown(flinchProbeKey))
            PlayFlinchProbe();
    }

    /// <summary>对锁定敌人或场上第一名敌人叠 Hit_Shake；失败只打日志。</summary>
    void PlayFlinchProbe()
    {
        CharacterActor target = HitFlinchAdditiveProbe.ResolveTarget(playerController);
        if (!HitFlinchAdditiveProbe.TryPlay(
                target,
                flinchProbeClip,
                flinchProbeMask,
                flinchProbeFade,
                out string error))
        {
            Debug.LogWarning("[P-HR0] " + error);
        }
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
        // 本机玩家可能晚于 HUD 创建，每帧补一次查询
        if (playerController == null)
            playerController = SendQuery(new GetLocalPlayerQuery()) as PlayerController;

        if (!visible)
            return;

        // 在 Actor.Render 之后采样，HUD 与画面同一表现帧
        if (playerController != null && playerController.Actor != null)
            _cached = playerController.Actor.BuildDebugSnapshot();

        CharacterActor probeTarget = HitFlinchAdditiveProbe.ResolveTarget(playerController);
        if (probeTarget != null
            && (playerController == null || probeTarget != playerController.Actor))
        {
            _targetCached = probeTarget.BuildDebugSnapshot();
            CharacterAnimationService presentation =
                HitFlinchAdditiveProbe.ResolvePresentation(probeTarget);
            _targetAdditiveWeight = presentation != null ? presentation.AdditiveWeight : 0f;
            _targetReactionKind = _targetCached.LastReactionKind;
            if (RemoteCharacterProxy.TryFindLive(probeTarget.SimulationId, out RemoteCharacterProxy proxy)
                && proxy.LastReplicatedReactionKind != HitReactionKind.None)
            {
                _targetReactionKind = proxy.LastReplicatedReactionKind;
            }

            _hasTargetSnapshot = true;
        }
        else
        {
            _hasTargetSnapshot = false;
        }
    }

    void OnGUI()
    {
        if (!visible)
            return;

        ReplicationRoomHudInfo room = CombatWorldController.Current != null
            ? CombatWorldController.Current.RoomHud
            : default;
        bool hasActor = playerController != null && playerController.Actor != null;
        if (!hasActor && !room.Active)
            return;

        EnsureStyles();
        _sb.Clear();
        AppendRoomLine(_sb, in room);
        if (hasActor)
        {
            AppendPartyLine(_sb, playerController);
            AppendSnapshot(
                _sb,
                in _cached,
                _cameraManager != null && _cameraManager.CameraLockEnabled);
            if (_hasTargetSnapshot)
                AppendTargetSnapshot(_sb, in _targetCached, _targetAdditiveWeight, _targetReactionKind);
        }
        const float width = 440f;
        float height = Mathf.Min(420f, Screen.height * 0.55f);
        GUI.Box(new Rect(8f, 8f, width, height), GUIContent.none, _boxStyle);
        GUI.Label(new Rect(16f, 16f, width - 16f, height - 16f), _sb.ToString(), _labelStyle);
    }

    /// <summary>输出本机阵容槽、稳定 ActorId 与 Active/Exiting 状态。</summary>
    static void AppendPartyLine(StringBuilder sb, PlayerController player)
    {
        if (player?.PartyLoadout == null)
            return;

        sb.Append("Party: ");
        for (int i = 0; i < player.PartyActors.Count; i++)
        {
            if (i > 0)
                sb.Append(" | ");
            CharacterActor member = player.PartyActors[i];
            sb.Append('[').Append(i).Append("] ");
            if (member == null)
            {
                sb.Append("Empty");
                continue;
            }

            sb.Append(member.PartyState)
                .Append(" #")
                .Append(member.SimulationId.Value);
        }
        sb.AppendLine();
    }

    /// <summary>房间角色、权威帧与 W0 网络基线；客机无本地 Actor 时仍显示。</summary>
    static void AppendRoomLine(StringBuilder sb, in ReplicationRoomHudInfo room)
    {
        if (!room.Active)
            return;

        sb.Append("Room: ").Append(room.Role)
            .Append(" | ").Append(room.Status)
            .Append(" | frame=").Append(room.AuthorityFrame);
        if (room.RttMs >= 0)
            sb.Append(" | rtt=").Append(room.RttMs).Append("ms");
        if (room.JitterMs >= 0)
            sb.Append(" | jitter=").Append(room.JitterMs).Append("ms");
        if (room.HealthMilli >= 0)
            sb.Append(" | hpMilli=").Append(room.HealthMilli);
        sb.AppendLine();

        if (room.TickBytes >= 0 || room.CommandBytes >= 0)
        {
            sb.Append("Net: tickB=").Append(room.TickBytes)
                .Append(" | cmdB=").Append(room.CommandBytes);
            if (room.ProxyCount >= 0)
                sb.Append(" | proxies=").Append(room.ProxyCount);
            if (room.PredictionPendingCount >= 0)
                sb.Append(" | pending=").Append(room.PredictionPendingCount);
            if (room.LossPermille >= 0)
                sb.Append(" | loss=").Append(room.LossPermille).Append("‰");
            if (room.InterpolationDelayMs >= 0)
                sb.Append(" | delay=").Append(room.InterpolationDelayMs).Append("ms");
            sb.Append(" | snap=").Append(room.SnapCount)
                .Append(" | replay=").Append(room.ReplayCount);
            sb.AppendLine();
        }
    }

    /// <summary>把角色逻辑快照与本地 CameraLock 状态格式化为只读 HUD。</summary>
    static void AppendSnapshot(
        StringBuilder sb,
        in CharacterDebugSnapshot s,
        bool cameraLockEnabled)
    {
        sb.AppendLine("[Combat Debug]  F3 HUD | F4 Hurtbox | F6 Flinch Additive")
            .Append("Hurtbox Gizmo: ")
            .Append(CombatHurtboxDebugSettings.ShowHurtboxes ? "ON" : "OFF")
            .AppendLine();
        sb.Append("State: ").Append(s.State);
        if (s.ActionActive)
        {
            sb.Append(" | Frame: ").Append(s.ActionFrame).Append('/').Append(s.ActionTotalFrames);
            sb.Append(" | Action: ").Append(s.ActionName);
        }

        sb.Append(" | Freeze: ").Append(s.FreezeFrames)
            .Append(" | Additive: ").Append(s.AdditiveWeight.ToString("0.00"))
            .Append(" | Reaction: ").Append(s.LastReactionKind)
            .AppendLine();
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
        sb.Append("SelectedTarget: ").Append(s.HasSelectedTarget ? "YES" : "NO");
        if (s.HasSelectedTarget)
            sb.Append(" | ").Append(s.SelectedTargetName)
                .Append(" | Dist=").Append(s.SelectedTargetDistanceMeters.ToString("0.00"));
        sb.Append(" | CameraLock=").Append(cameraLockEnabled ? "ON" : "OFF");
        sb.AppendLine();
        sb.Append("Motor mm: (").Append(s.MotorXMm).Append(", ").Append(s.MotorYMm)
            .Append(", ").Append(s.MotorZMm).Append(") facing=")
            .Append(s.MotorFacingMilliDeg).AppendLine();
        sb.Append("SoftBody: mass=").Append(s.SoftBodyMass)
            .Append(" immovable=").Append(s.SoftBodyImmovable).AppendLine();
        sb.Append("ActionLateralPeakMm: ").Append(s.ActionLateralPeakMm)
            .Append("  (Wave0 baseline; 对照横摆是否进逻辑根)");
    }

    /// <summary>探针目标（锁定/场上敌人）的状态、裁档与 Additive 权重，便于对照 F6 前后。</summary>
    static void AppendTargetSnapshot(
        StringBuilder sb,
        in CharacterDebugSnapshot s,
        float presentationAdditive,
        HitReactionKind reactionKind)
    {
        sb.AppendLine();
        sb.Append("FlinchTarget: ").Append(s.State);
        if (s.ActionActive)
        {
            sb.Append(" | Frame: ").Append(s.ActionFrame).Append('/').Append(s.ActionTotalFrames);
            sb.Append(" | Action: ").Append(s.ActionName);
        }

        sb.Append(" | Reaction: ").Append(reactionKind)
            .Append(" | Additive: ").Append(presentationAdditive.ToString("0.00"));
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
