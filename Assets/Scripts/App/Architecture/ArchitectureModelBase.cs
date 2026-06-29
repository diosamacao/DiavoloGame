/// <summary>架构 Model 基类；Model 负责保存共享状态并可发送状态变化事件。</summary>
public abstract class ArchitectureModelBase : ArchitectureElementBase, IArchitectureModel
{
    /// <summary>当前 Model 是否已经完成初始化。</summary>
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

    /// <summary>反初始化模型；用于释放持有的运行时资源。</summary>
    public void Deinitialize()
    {
        if (!Initialized)
            return;

        OnDeinit();
        Initialized = false;
    }

    /// <summary>子类实现模型初始化逻辑。</summary>
    protected abstract void OnInit();

    /// <summary>子类可按需释放模型资源。</summary>
    protected virtual void OnDeinit() { }
}
