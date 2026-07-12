using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Locomotion 逻辑键到 AnimationClip 的映射配置。</summary>
[CreateAssetMenu(fileName = "CharacterAnimationProfile", menuName = "ACT/Character Animation Profile")]
public class CharacterAnimationProfile : ScriptableObject
{
    [Serializable]
    public struct Entry
    {
        public AnimationKey Key;
        public AnimationClip Clip;
    }

    [SerializeField] Entry[] entries = Array.Empty<Entry>();
    [SerializeField] float defaultCrossFadeDuration = 0.15f;

    Dictionary<AnimationKey, AnimationClip> _lookup;

    public float DefaultCrossFadeDuration => defaultCrossFadeDuration;

    /// <summary>按逻辑键取 Clip；未配置时返回 null。</summary>
    public AnimationClip GetClip(AnimationKey key)
    {
        EnsureLookup();
        return _lookup.TryGetValue(key, out AnimationClip clip) ? clip : null;
    }

    /// <summary>尝试按逻辑键取 Clip。</summary>
    public bool TryGetClip(AnimationKey key, out AnimationClip clip)
    {
        EnsureLookup();
        return _lookup.TryGetValue(key, out clip) && clip != null;
    }

    /// <summary>校验 Idle/Walk/Run 均已绑定 Clip。</summary>
    public bool ValidateClips(UnityEngine.Object context)
    {
        bool valid = true;
        ValidateKey(AnimationKey.Idle, context, ref valid);
        ValidateKey(AnimationKey.Walk, context, ref valid);
        ValidateKey(AnimationKey.Run, context, ref valid);
        return valid;
    }

    void ValidateKey(AnimationKey key, UnityEngine.Object context, ref bool valid)
    {
        if (TryGetClip(key, out _))
            return;

        Debug.LogError($"CharacterAnimationProfile: 未绑定 {key} 的 AnimationClip（资产 {name}）。", context);
        valid = false;
    }

    void EnsureLookup()
    {
        if (_lookup != null)
            return;

        _lookup = new Dictionary<AnimationKey, AnimationClip>();
        foreach (Entry entry in entries)
        {
            if (entry.Clip == null)
                continue;

            _lookup[entry.Key] = entry.Clip;
        }
    }

    void OnEnable() => _lookup = null;
}
