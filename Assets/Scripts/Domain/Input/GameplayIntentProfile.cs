using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>物理 InputAction 到设备无关玩法意图的映射配置。</summary>
[CreateAssetMenu(fileName = "GameplayIntentProfile", menuName = "ACT/Input/Gameplay Intent Profile")]
public sealed class GameplayIntentProfile : ScriptableObject
{
    [SerializeField] GameplayIntentBinding[] bindings = Array.Empty<GameplayIntentBinding>();
    [Tooltip("Action 内输入缓冲有效逻辑帧数；小于等于 0 时使用 9 帧（60Hz 下 0.15 秒）。")]
    [SerializeField] int actionBufferDurationFrames = 9;

    /// <summary>全部意图映射；运行时只读取有效项。</summary>
    public IReadOnlyList<GameplayIntentBinding> Bindings =>
        bindings ?? Array.Empty<GameplayIntentBinding>();

    /// <summary>Action Cancel / Recovery 预输入的统一有效逻辑帧数。</summary>
    public int ActionBufferDurationFrames =>
        actionBufferDurationFrames > 0 ? actionBufferDurationFrames : 9;

    /// <summary>收集需要 InputReader 轮询的物理输入引用。</summary>
    public InputActionReference[] CollectInputReferences()
    {
        var references = new List<InputActionReference>(Bindings.Count);
        for (int i = 0; i < Bindings.Count; i++)
        {
            GameplayIntentBinding binding = Bindings[i];
            if (binding.IsValid)
                references.Add(binding.Input);
        }

        return InputBindingUtils.CollectUniqueReferences(references);
    }

    /// <summary>启动期校验确定性意图映射；空项或无效 InputActionReference 均阻止 Catalog 冻结。</summary>
    public bool ValidateContent(UnityEngine.Object context)
    {
        if (Bindings.Count == 0)
        {
            Debug.LogError($"GameplayIntentProfile: '{name}' 未配置任何意图映射。", this);
            return false;
        }

        bool valid = true;
        for (int i = 0; i < Bindings.Count; i++)
        {
            if (Bindings[i].IsValid)
                continue;
            Debug.LogError(
                $"GameplayIntentProfile: '{name}' 的 bindings[{i}] 缺少有效输入引用或 GameplayIntentType。",
                this);
            valid = false;
        }

        return valid;
    }

    /// <summary>按序复制影响 Authority 解释结果的稳定字段，供 Content Fingerprint 使用。</summary>
    public void CopyStableSignatures(List<string> results)
    {
        if (results == null)
            throw new ArgumentNullException(nameof(results));
        results.Clear();
        results.Add($"buffer:{ActionBufferDurationFrames}");
        for (int i = 0; i < Bindings.Count; i++)
        {
            GameplayIntentBinding binding = Bindings[i];
            results.Add(
                $"{i}:{(int)binding.Button}:{(int)binding.Phase}:{(int)binding.Intent}:"
                + $"{(int)binding.Condition}:{binding.HoldFrames}:{binding.Priority}");
        }
    }
}
