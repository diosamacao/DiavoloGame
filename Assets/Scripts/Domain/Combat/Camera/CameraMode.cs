using System.Collections.Generic;

/// <summary>相机导演模式；数值顺序不代表 Priority，优先级由栈条目显式保存。</summary>
public enum CameraMode
{
    Free = 0,
    LockOn = 1,
    SkillShot = 2,
    Cutscene = 3,
}
