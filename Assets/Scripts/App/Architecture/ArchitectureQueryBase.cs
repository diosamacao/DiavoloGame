/// <summary>架构 Query 基类；查询只能读取 System、Model 或 Utility。</summary>
public abstract class ArchitectureQueryBase<TResult> :
    ArchitectureElementBase,
    IArchitectureQuery<TResult>,
    ICanGetSystem,
    ICanGetModel,
    ICanGetUtility
{
    /// <summary>绑定架构后执行无副作用查询。</summary>
    public TResult Execute(ACTGameArchitecture architecture)
    {
        SetArchitecture(architecture);
        return OnQuery();
    }

    /// <summary>子类实现无副作用读取逻辑。</summary>
    protected abstract TResult OnQuery();
}
