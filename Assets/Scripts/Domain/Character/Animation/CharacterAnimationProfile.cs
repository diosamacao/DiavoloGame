using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "CharacterAnimationProfile", menuName = "ACT/Character Animation Profile")]
public class CharacterAnimationProfile : ScriptableObject
{
    [Serializable]
    public struct Entry
    {
        public AnimationKey Key;
        public string StateName;
    }

    [SerializeField] Entry[] entries = Array.Empty<Entry>();
    [SerializeField] float defaultCrossFadeDuration = 0.15f;

    Dictionary<AnimationKey, string> _lookup;

    public float DefaultCrossFadeDuration => defaultCrossFadeDuration;

    public string GetStateName(AnimationKey key)
    {
        EnsureLookup();
        return _lookup.TryGetValue(key, out string stateName) ? stateName : key.ToString();
    }

    void EnsureLookup()
    {
        if (_lookup != null)
            return;

        _lookup = new Dictionary<AnimationKey, string>();
        foreach (Entry entry in entries)
            _lookup[entry.Key] = entry.StateName;
    }

    void OnEnable() => _lookup = null;
}
