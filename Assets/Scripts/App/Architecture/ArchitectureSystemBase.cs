/// <summary>架构 System 基类；封装 Architecture 注入与初始化生命周期。</summary>
public abstract class ArchitectureSystemBase : ArchitectureElementBase, IArchitectureSystem
{
    /// <summary>当前 System 是否已经完成初始化。</summary>
    public bool Initialized { get; private set; }

    /// <summary>绑定架构并执行一次性初始化；重复调用不会再次 OnInit。</summary>
    public void Initialize(ACTGameArchitecture architecture)
    {
        SetArchitecture(architecture);
        if (Initialized)
            return;

        Initialized = true;
        OnInit();
    }

    /// <summary>反初始化系统；用于释放事件订阅或运行时缓存。</summary>
    public void Deinitialize()
    {
        if (!Initialized)
            return;

        OnDeinit();
        Initialized = false;
    }

    /// <summary>子类实现系统初始化逻辑。</summary>
    protected abstract void OnInit();

    /// <summary>子类可按需释放系统资源。</summary>
    protected virtual void OnDeinit() { }
}
