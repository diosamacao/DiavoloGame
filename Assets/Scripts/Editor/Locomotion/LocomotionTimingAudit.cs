using UnityEditor;
using UnityEngine;

/// <summary>只读审计 Locomotion Profile 的 Clip→整数帧 timing 完整性。</summary>
public static class LocomotionTimingAudit
{
    /// <summary>检查已绑定的每个 Clip 是否存在唯一有效 timing；不修改任何资产。</summary>
    public static bool Validate(CharacterLocomotionProfile profile, bool logSuccess)
    {
        if (profile == null)
        {
            Debug.LogError("LocomotionTimingAudit: 缺少 Locomotion Profile。", profile);
            return false;
        }

        // Editor 菜单与启动 Build 复用 Domain 同一规则，禁止两套 Timing/RootMotion 判断漂移。
        bool valid = profile.Validate(profile);

        if (valid && logSuccess)
            Debug.Log($"LocomotionTimingAudit:「{profile.name}」Timing 与 Root Motion 均通过。", profile);
        return valid;
    }

    /// <summary>菜单仅审计当前选中的 Locomotion Profile。</summary>
    [MenuItem("Tools/ACT/Locomotion/Audit Selected Timing")]
    public static void AuditSelected()
    {
        CharacterLocomotionProfile profile = Selection.activeObject as CharacterLocomotionProfile;
        if (profile == null)
        {
            Debug.LogError("请先在 Project 选中 CharacterLocomotionProfile。");
            return;
        }

        Validate(profile, logSuccess: true);
    }
}
