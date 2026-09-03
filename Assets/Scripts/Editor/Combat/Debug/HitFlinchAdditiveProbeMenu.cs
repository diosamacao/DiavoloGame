using UnityEditor;
using UnityEngine;

/// <summary>Play 模式下对选中或锁定敌人触发 P-HR0 Additive 探针。</summary>
public static class HitFlinchAdditiveProbeMenu
{
    const string MenuPath = "ACTGame/Combat/Debug Play Flinch Additive";

    /// <summary>仅 Play 可用；Clip 取场景里 CombatDebugHud 的探针字段。</summary>
    [MenuItem(MenuPath)]
    public static void PlayFlinchAdditive()
    {
        CombatDebugHudController hud = Object.FindObjectOfType<CombatDebugHudController>();
        AnimationClip clip = null;
        AvatarMask mask = null;
        float fade = 0.05f;
        if (hud != null)
            ReadHudProbe(hud, out clip, out mask, out fade);

        CharacterActor target = ResolveSelectedEnemy();
        if (target == null)
        {
            PlayerController player = Object.FindObjectOfType<PlayerController>();
            target = HitFlinchAdditiveProbe.ResolveTarget(player);
        }

        if (!HitFlinchAdditiveProbe.TryPlay(target, clip, mask, fade, out string error))
            Debug.LogWarning("[P-HR0] " + error);
    }

    [MenuItem(MenuPath, true)]
    static bool ValidatePlayFlinchAdditive() => Application.isPlaying;

    static CharacterActor ResolveSelectedEnemy()
    {
        GameObject selected = Selection.activeGameObject;
        if (selected == null)
            return null;

        EnemyController enemy = selected.GetComponent<EnemyController>()
            ?? selected.GetComponentInParent<EnemyController>();
        return enemy != null ? enemy.Actor : null;
    }

    static void ReadHudProbe(
        CombatDebugHudController hud,
        out AnimationClip clip,
        out AvatarMask mask,
        out float fade)
    {
        SerializedObject so = new SerializedObject(hud);
        clip = so.FindProperty("flinchProbeClip").objectReferenceValue as AnimationClip;
        mask = so.FindProperty("flinchProbeMask").objectReferenceValue as AvatarMask;
        fade = so.FindProperty("flinchProbeFade").floatValue;
    }
}
