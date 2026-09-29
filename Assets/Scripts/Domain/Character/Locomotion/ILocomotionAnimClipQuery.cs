using UnityEngine;

/// <summary>查询某 AnimationKey 是否已绑定 Clip（避免 Resolver 依赖具体 Profile 类型）。</summary>
public interface ILocomotionAnimClipQuery
{
    /// <summary>是否已配置非空 Clip。</summary>
    bool HasClip(AnimationKey key);
}
