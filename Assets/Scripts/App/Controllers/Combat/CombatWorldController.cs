using UnityEngine;

/// <summary>场景级战斗世界入口控制器，集中承载目标注册、命中检测、索敌与反馈系统的生命周期。</summary>
[DefaultExecutionOrder(-200)]
[DisallowMultipleComponent]
public class CombatWorldController : AppControllerBase
{
    [Tooltip("可选：静态障碍烘焙资产。未绑定则使用空场地（地面 Y=0、无硬挡）。")]
    [SerializeField] StaticCollisionBake staticCollisionBake = null;

    /// <summary>当前场景战斗世界；系统查询只把它作为生命周期锚点，不作为业务单例入口。</summary>
    public static CombatWorldController Current { get; private set; }

    /// <summary>当前战斗世界唯一固定帧宿主。</summary>
    public SimulationHost SimulationHost { get; private set; }

    void Awake()
    {
        if (Current != null && Current != this)
        {
            Debug.LogWarning("CombatWorldController: 场景中存在多个实例，后创建的实例将被禁用。", this);
            enabled = false;
            return;
        }

        Current = this;
        EnsureSimulationHost();
        ApplyStaticCollisionBake();
        EnsureFeedbackController();
    }

    void OnDestroy()
    {
        if (Current == this)
            Current = null;
    }

    /// <summary>确保场景有统一反馈系统，避免多个角色各自挂卡肉控制器。</summary>
    void EnsureFeedbackController()
    {
        if (GetComponent<FeedbackController>() == null)
            gameObject.AddComponent<FeedbackController>();
    }

    /// <summary>确保固定帧宿主与本战斗世界同生命周期，并返回唯一实例。</summary>
    public SimulationHost EnsureSimulationHost()
    {
        if (SimulationHost == null)
            SimulationHost = GetComponent<SimulationHost>();
        if (SimulationHost == null)
            SimulationHost = gameObject.AddComponent<SimulationHost>();

        return SimulationHost;
    }

    /// <summary>把 Inspector 绑定的烘焙资产装入 Host；无资产则空场地。</summary>
    public void ApplyStaticCollisionBake()
    {
        SimulationHost host = EnsureSimulationHost();
        ISimCollisionWorld world = staticCollisionBake != null
            ? staticCollisionBake.CreateWorld()
            : OpenFieldSimCollisionWorld.Instance;
        host.SetCollisionWorld(world);
    }
}
