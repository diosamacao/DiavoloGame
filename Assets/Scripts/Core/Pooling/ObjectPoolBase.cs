using System.Collections.Generic;

/// <summary>通用对象池基类：Get / Return / Prewarm，子类实现实例创建与取出/归还钩子。</summary>
public abstract class ObjectPoolBase<T> where T : class
{
    readonly Stack<T> _inactive = new();

    /// <summary>当前池中 inactive 实例数量。</summary>
    public int InactiveCount => _inactive.Count;

    /// <summary>取出一个实例；池空时调用 CreateInstance 扩容。</summary>
    public T Get()
    {
        while (_inactive.Count > 0)
        {
            T item = _inactive.Pop();
            if (item != null)
            {
                OnGet(item);
                return item;
            }
        }

        T created = CreateInstance();
        OnGet(created);
        return created;
    }

    /// <summary>归还实例到池内 inactive 栈。</summary>
    public void Return(T item)
    {
        if (item == null)
            return;

        OnReturn(item);
        _inactive.Push(item);
    }

    /// <summary>预创建 inactive 实例至 count 数量。</summary>
    public void Prewarm(int count)
    {
        int target = count < 0 ? 0 : count;
        while (_inactive.Count < target)
        {
            T item = CreateInstance();
            OnReturn(item);
            _inactive.Push(item);
        }
    }

    /// <summary>池空 Get 时创建新实例。</summary>
    protected abstract T CreateInstance();

    /// <summary>实例离开池、交给调用方使用前调用。</summary>
    protected virtual void OnGet(T item) { }

    /// <summary>实例归还池之前调用。</summary>
    protected virtual void OnReturn(T item) { }
}
