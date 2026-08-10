using System.Collections.Generic;
using UnityEngine;

/// <summary>敌人通用冷却表（逻辑帧）；支持节点暂存、Brain 确认后提交的招式冷却。</summary>
public sealed class EnemyCooldownTable
{
    readonly Dictionary<string, int> _remaining = new Dictionary<string, int>(8);
    readonly Dictionary<string, int> _pending = new Dictionary<string, int>(4);
    readonly List<string> _scratch = new List<string>(8);

    /// <summary>每逻辑帧调用：所有正剩余帧减一。</summary>
    public void TickDown()
    {
        if (_remaining.Count == 0)
            return;

        _scratch.Clear();
        foreach (KeyValuePair<string, int> pair in _remaining)
            _scratch.Add(pair.Key);

        for (int i = 0; i < _scratch.Count; i++)
        {
            string id = _scratch[i];
            int next = Mathf.Max(0, _remaining[id] - 1);
            if (next <= 0)
                _remaining.Remove(id);
            else
                _remaining[id] = next;
        }
    }

    /// <summary>设置冷却剩余逻辑帧；0 表示立即可用并移除槽位。</summary>
    public void Set(string id, int frames)
    {
        if (string.IsNullOrEmpty(id))
            return;

        _pending.Remove(id);
        int clamped = Mathf.Max(0, frames);
        if (clamped <= 0)
            _remaining.Remove(id);
        else
            _remaining[id] = clamped;
    }

    /// <summary>暂存待起手确认的冷却；暂存期间该 id 视为未就绪。</summary>
    public void Stage(string id, int frames)
    {
        if (string.IsNullOrEmpty(id))
            return;

        int clamped = Mathf.Max(0, frames);
        if (clamped <= 0)
            _pending.Remove(id);
        else
            _pending[id] = clamped;
    }

    /// <summary>确认全部暂存冷却并开始倒计时。</summary>
    public void ConfirmPending()
    {
        foreach (KeyValuePair<string, int> pair in _pending)
            _remaining[pair.Key] = pair.Value;
        _pending.Clear();
    }

    /// <summary>丢弃全部未确认冷却；用于请求失败或动作被门闩抢占。</summary>
    public void DiscardPending() => _pending.Clear();

    /// <summary>指定 id 是否有待确认冷却。</summary>
    public bool HasPending(string id) =>
        !string.IsNullOrEmpty(id) && _pending.ContainsKey(id);

    /// <summary>指定 id 是否已冷却完毕且无待确认项（未知 id 视为就绪）。</summary>
    public bool IsReady(string id)
    {
        if (string.IsNullOrEmpty(id))
            return true;
        if (_pending.ContainsKey(id))
            return false;
        return !_remaining.TryGetValue(id, out int frames) || frames <= 0;
    }

    /// <summary>剩余逻辑帧；就绪时为 0。</summary>
    public int GetRemaining(string id)
    {
        if (string.IsNullOrEmpty(id))
            return 0;
        return _remaining.TryGetValue(id, out int frames) ? Mathf.Max(0, frames) : 0;
    }

    /// <summary>清空生效与待确认冷却。</summary>
    public void Clear()
    {
        _remaining.Clear();
        _pending.Clear();
    }
}

/// <summary>约定冷却 id，并统一成功 CD 与请求失败重试门控。</summary>
public static class EnemyCooldownIds
{
    /// <summary>基础攻击成功冷却；帧数由 CooldownGate 节点持有。</summary>
    public const string BasicAttack = "basic_attack";

    /// <summary>任意 Action Entry 请求失败后的全局短重试冷却。</summary>
    public const string ActionEntryRetry = "action_entry_retry";

    /// <summary>闪避类 Task / CooldownGate 默认 id。</summary>
    public const string Dodge = "dodge";

    /// <summary>判断节点冷却门是否就绪；Action Entry 失败重试期间所有招式门保持关闭。</summary>
    public static bool IsGateReady(EnemyCooldownTable cooldowns, string id)
    {
        if (cooldowns == null || !cooldowns.IsReady(id))
            return false;
        return cooldowns.IsReady(ActionEntryRetry);
    }
}
