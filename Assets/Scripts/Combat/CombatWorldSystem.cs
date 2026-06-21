using UnityEngine;

/// <summary>场景级战斗世界入口，集中承载目标注册、命中检测、索敌与反馈系统的生命周期。</summary>
[DisallowMultipleComponent]
public class CombatWorldSystem : MonoBehaviour
{
    /// <summary>当前场景战斗世界；系统查询只把它作为生命周期锚点，不作为业务单例入口。</summary>
    public static CombatWorldSystem Current { get; private set; }

    void Awake()
    {
        if (Current != null && Current != this)
        {
            Debug.LogWarning("CombatWorldSystem: 场景中存在多个实例，后创建的实例将被禁用。", this);
            enabled = false;
            return;
        }

        Current = this;
        EnsureFeedbackSystem();
    }

    void OnDestroy()
    {
        if (Current == this)
            Current = null;
    }

    /// <summary>确保场景有统一反馈系统，避免多个角色各自挂卡肉控制器。</summary>
    void EnsureFeedbackSystem()
    {
        if (GetComponent<FeedbackSystem>() == null)
            gameObject.AddComponent<FeedbackSystem>();
    }
}
