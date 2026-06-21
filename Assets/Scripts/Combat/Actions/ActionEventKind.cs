/// <summary>时间轴事件类型骨架；M5+ 编辑器轨道与运行时派发共用。</summary>
public enum ActionEventKind
{
    SpawnHitbox = 0,
    DisableHitbox = 1,
    PlayVfx = 2,
    PlaySfx = 3,
    ApplyImpulse = 4,
    CameraShake = 5,
    HitStop = 6,
    ChangePhase = 7,
    Custom = 99,
}
