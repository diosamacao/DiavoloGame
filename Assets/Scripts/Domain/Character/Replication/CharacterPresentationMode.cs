/// <summary>角色表现装配能力；差异只在工厂，禁止在 Step 里用 Dedicated 开关开双轨。</summary>
public enum CharacterPresentationMode : byte
{
    /// <summary>完整表现：模型、PlayableGraph、VFX/SFX、卡肉骨骼。</summary>
    Full = 0,

    /// <summary>权威无头：Motor/Action/Hitbox 完整，不创建模型与 Graph。</summary>
    AuthorityHeadless = 1,
}
