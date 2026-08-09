using System.Collections.Generic;
using UnityEngine;

/// <summary>敌人通用冷却表（逻辑帧）；Brain 与 CooldownGate 共用，按 id 分槽。</summary>
public sealed class EnemyCooldownTable
{
    readonly Dictionary<string, int> _remaining = new Dictionary<string, int>(8);
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

        int clamped = Mathf.Max(0, frames);
        if (clamped <= 0)
            _remaining.Remove(id);
        else
            _remaining[id] = clamped;
    }

    /// <summary>指定 id 是否已冷却完毕（未知 id 视为就绪）。</summary>
    public bool IsReady(string id)
    {
        if (string.IsNullOrEmpty(id))
            return true;
        return !_remaining.TryGetValue(id, out int frames) || frames <= 0;
    }

    /// <summary>剩余逻辑帧；就绪时为 0。</summary>
    public int GetRemaining(string id)
    {
        if (string.IsNullOrEmpty(id))
            return 0;
        return _remaining.TryGetValue(id, out int frames) ? Mathf.Max(0, frames) : 0;
    }

    /// <summary>清空全部冷却。</summary>
    public void Clear() => _remaining.Clear();
}

/// <summary>约定冷却 id；基础攻击由 Brain 在起手确认后写入。</summary>
public static class EnemyCooldownIds
{
    /// <summary>基础攻击冷却（Brain 权威写入）。</summary>
    public const string BasicAttack = "basic_attack";

    /// <summary>闪避类 Task / CooldownGate 默认 id。</summary>
    public const string Dodge = "dodge";
}
