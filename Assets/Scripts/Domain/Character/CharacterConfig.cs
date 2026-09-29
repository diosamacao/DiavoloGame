using System;
using UnityEngine;

/// <summary>
/// 角色装配根配置。Locomotion（含 Clip）在 CombatMode；输入/意图由运行时与 Content Catalog 注入；
/// 本资产只保留模型、Motor、战斗身体与资源。
/// </summary>
[CreateAssetMenu(fileName = "CharacterConfig", menuName = "ACT/Character/Character Config")]
public class CharacterConfig : ScriptableObject
{
    [Header("Model")]
    [SerializeField] GameObject modelPrefab = null;
    [SerializeField] Vector3 modelLocalPosition = Vector3.zero;
    [SerializeField] Vector3 modelLocalEulerAngles = Vector3.zero;

    [Header("Movement")]
    [SerializeField] CharacterMotorConfig motor = CharacterMotorConfig.Default;

    [Header("Combat")]
    [Tooltip("mode → ActionGraph + LocomotionProfile（内含动画映射）。")]
    [SerializeField] CharacterCombatModes combatModes = new();
    [SerializeField] CharacterCombatConfig combat = CharacterCombatConfig.Default;

    [Header("Resources")]
    [Tooltip("Energy / Decibel / Dodge；嵌本配置，禁止另开 Profile 双轨。")]
    [SerializeField] CharacterResourceConfig resources = null;

    /// <summary>角色模型 Prefab；运行时会实例化为 PlayerController 子物体。</summary>
    public GameObject ModelPrefab => modelPrefab;

    /// <summary>模型实例本地位置。</summary>
    public Vector3 ModelLocalPosition => modelLocalPosition;

    /// <summary>模型实例本地旋转。</summary>
    public Quaternion ModelLocalRotation => Quaternion.Euler(modelLocalEulerAngles);

    /// <summary>移动和 CharacterController 参数。</summary>
    public CharacterMotorConfig Motor => motor;

    /// <summary>战斗模式与出招图 / Clip 映射。</summary>
    public CharacterCombatModes CombatModes => combatModes;

    /// <summary>战斗运行时装配参数。</summary>
    public CharacterCombatConfig Combat => combat;

    /// <summary>玩法资源上限与回复；未序列化时用默认骨架值。</summary>
    public CharacterResourceConfig Resources => resources ?? CharacterResourceConfig.Default;

    /// <summary>检查敌人角色 Gameplay 内容；不校验 Client-only Input/Intent 设置。</summary>
    public bool ValidateForEnemy(UnityEngine.Object context) =>
        ValidateGameplayContent(context);

    /// <summary>校验模型、CombatMode、Locomotion 与受击内容；可供 Client、Listen、Dedicated 共用。</summary>
    public bool ValidateGameplayContent(UnityEngine.Object context)
    {
        bool valid = true;
        if (modelPrefab == null)
        {
            Debug.LogError("CharacterConfig: ModelPrefab 未配置。", context);
            valid = false;
        }

        if (combatModes == null)
        {
            Debug.LogError("CharacterConfig: CombatModes 未配置。", context);
            valid = false;
        }
        else if (!combatModes.Validate(context))
        {
            valid = false;
        }

        if (!Combat.Reactions.Validate(context))
            valid = false;

        return valid;
    }

    /// <summary>旧资产未填韧性时写成杂兵默认，避免 Inspector 显示 0。</summary>
    void OnValidate()
    {
        CharacterCombatConfig copy = combat;
        copy.EnsureInterruptResistDefault();
        combat = copy;
    }
}
