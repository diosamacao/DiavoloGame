using System.Collections.Generic;

/// <summary>不依赖 Unity 对象的 CameraDirector 优先级栈，供 Runtime 与 EditMode 测试。</summary>
public sealed class CameraDirectorStack
{
    readonly List<CameraDirectorEntry> _entries = new();

    /// <summary>初始化始终存在的 Free 基线。</summary>
    public CameraDirectorStack()
    {
        _entries.Add(new CameraDirectorEntry(CameraMode.Free, 10));
    }

    /// <summary>当前最高优先级模式；同优先级后入者胜出。</summary>
    public CameraDirectorEntry Active
    {
        get
        {
            CameraDirectorEntry best = _entries[0];
            for (int i = 1; i < _entries.Count; i++)
            {
                CameraDirectorEntry candidate = _entries[i];
                if (candidate.Priority >= best.Priority)
                    best = candidate;
            }

            return best;
        }
    }

    /// <summary>插入模式；同模式只保留最后一次请求，禁止双份状态。</summary>
    public void Push(CameraMode mode, int priority)
    {
        Remove(mode);
        _entries.Add(new CameraDirectorEntry(mode, priority));
    }

    /// <summary>移除指定模式；Free 永不移除。</summary>
    public void Remove(CameraMode mode)
    {
        if (mode == CameraMode.Free)
            return;

        for (int i = _entries.Count - 1; i >= 0; i--)
        {
            if (_entries[i].Mode == mode)
                _entries.RemoveAt(i);
        }
    }

    /// <summary>查询栈中是否存在指定模式。</summary>
    public bool Contains(CameraMode mode)
    {
        for (int i = 0; i < _entries.Count; i++)
        {
            if (_entries[i].Mode == mode)
                return true;
        }

        return false;
    }
}
