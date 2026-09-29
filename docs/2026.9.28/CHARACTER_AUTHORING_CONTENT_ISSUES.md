# 全内容审计剩余项

最新 Unity 审计：85 个失败项，其中 51 项生产源码结构规则、33 个 Action 烘焙内容问题、1 个 Locomotion 内容问题。新增 Graph 校验无错误。

下表对照迁移前原始资产，未对生产烘焙内容做猜测式重写。

| Action | 问题 | 迁移前证据 |
|---|---|---|
| `Assets/Data/Combat/Actions/Unagi/ActioniDefinition/Unagi_EX.asset` | BAKED_FRAME_COUNT_MISMATCH: bakedMotion.frameCount=460，TotalFrames=457。 | totalFrames=457; baked.frameCount=460; bakeStatus=1 |
| `Assets/Data/Combat/Actions/Anbi/ActionDefinition/Anbi_Attack_Normal_03.asset` | BAKED_FRAME_COUNT_MISMATCH: bakedMotion.frameCount=173，TotalFrames=172。 | totalFrames=172; baked.frameCount=173; bakeStatus=1 |
| `Assets/Data/Combat/Actions/Unagi/ActioniDefinition/Unagi_ParryAid_H_Success.asset` | BAKED_FRAME_COUNT_MISMATCH: bakedMotion.frameCount=270，TotalFrames=268。 | totalFrames=268; baked.frameCount=270; bakeStatus=1 |
| `Assets/Data/Combat/Actions/Anbi/ActionDefinition/Anbi_SwitchOut.asset` | BAKED_FRAME_COUNT_MISMATCH: bakedMotion.frameCount=57，TotalFrames=56。 | totalFrames=56; baked.frameCount=57; bakeStatus=1 |
| `Assets/Data/Combat/Actions/Unagi/ActioniDefinition/Unagi_Attack_04.asset` | BAKED_FRAME_COUNT_MISMATCH: bakedMotion.frameCount=263，TotalFrames=262。 | totalFrames=262; baked.frameCount=263; bakeStatus=1 |
| `Assets/Data/Combat/Actions/Monster/ActionDefinition/Monster_Goblin_Attack_01.asset` | BAKED_FRAME_COUNT_MISMATCH: bakedMotion.frameCount=212，TotalFrames=211。 | totalFrames=211; baked.frameCount=212; bakeStatus=1 |
| `Assets/Data/Combat/Actions/Unagi/ActioniDefinition/Unagi_Attack_Branch_01_Perfect.asset` | BAKED_FRAME_COUNT_MISMATCH: bakedMotion.frameCount=238，TotalFrames=236。 | totalFrames=236; baked.frameCount=238; bakeStatus=1 |
| `Assets/Data/Combat/Actions/Anbi/ActionDefinition/Anbi_Attack_Normal_04_Perfect.asset` | BAKED_FRAME_COUNT_MISMATCH: bakedMotion.frameCount=218，TotalFrames=216。 | totalFrames=216; baked.frameCount=218; bakeStatus=1 |
| `Assets/Data/Combat/Actions/Unagi/ActioniDefinition/Unagi_Attack_Counter.asset` | BAKED_FRAME_COUNT_MISMATCH: bakedMotion.frameCount=330，TotalFrames=240。 | totalFrames=240; baked.frameCount=330; bakeStatus=1 |
| `Assets/Data/Combat/Actions/Unagi/ActioniDefinition/Unagi_Attack_Branch_01.asset` | BAKED_FRAME_COUNT_MISMATCH: bakedMotion.frameCount=258，TotalFrames=256。 | totalFrames=256; baked.frameCount=258; bakeStatus=1 |
| `Assets/Data/Combat/Actions/Unagi/ActioniDefinition/Unagi_Dodge_Backward.asset` | BAKED_FRAME_COUNT_MISMATCH: bakedMotion.frameCount=232，TotalFrames=231。 | totalFrames=231; baked.frameCount=232; bakeStatus=1 |
| `Assets/Data/Combat/Actions/Unagi/ActioniDefinition/Unagi_Combo_1.asset` | BAKED_FRAME_COUNT_MISMATCH: bakedMotion.frameCount=142，TotalFrames=140。 | totalFrames=140; baked.frameCount=142; bakeStatus=1 |
| `Assets/Data/Combat/Actions/Anbi/ActionDefinition/Anbi_Evade_Front.asset` | BAKED_FRAME_COUNT_MISMATCH: bakedMotion.frameCount=113，TotalFrames=112。 | totalFrames=112; baked.frameCount=113; bakeStatus=1 |
| `Assets/Data/Combat/Actions/Unagi/ActioniDefinition/Unagi_Attack_Branch_03.asset` | BAKED_FRAME_COUNT_MISMATCH: bakedMotion.frameCount=273，TotalFrames=272。 | totalFrames=272; baked.frameCount=273; bakeStatus=1 |
| `Assets/Data/Combat/Actions/Anbi/ActionDefinition/Anbi_Attack_Normal_02.asset` | BAKED_FRAME_COUNT_MISMATCH: bakedMotion.frameCount=162，TotalFrames=160。 | totalFrames=160; baked.frameCount=162; bakeStatus=1 |
| `Assets/Data/Combat/Actions/Unagi/ActioniDefinition/Unagi_ChargeAttack_01.asset` | BAKED_FRAME_COUNT_MISMATCH: bakedMotion.frameCount=218，TotalFrames=216。 | totalFrames=216; baked.frameCount=218; bakeStatus=1 |
| `Assets/Data/Combat/Actions/Anbi/ActionDefinition/Anbi_Attack_Normal_01.asset` | BAKED_FRAME_COUNT_MISMATCH: bakedMotion.frameCount=177，TotalFrames=176。 | totalFrames=176; baked.frameCount=177; bakeStatus=1 |
| `Assets/Data/Combat/Actions/Unagi/ActioniDefinition/Unagi_ChargeAttack_03.asset` | BAKED_MODE_NOT_READY: BaseMotionMode=BakedMotion 但运动表未就绪。 | totalFrames=336; baked.frameCount=0; bakeStatus=0 |
| `Assets/Data/Combat/Actions/Unagi/ActioniDefinition/Unagi_Attack_Branch_Start.asset` | BAKED_FRAME_COUNT_MISMATCH: bakedMotion.frameCount=128，TotalFrames=127。 | totalFrames=127; baked.frameCount=128; bakeStatus=1 |
| `Assets/Data/Combat/Actions/Unagi/ActioniDefinition/Unagi_Dodge_Forward.asset` | BAKED_FRAME_COUNT_MISMATCH: bakedMotion.frameCount=242，TotalFrames=241。 | totalFrames=241; baked.frameCount=242; bakeStatus=1 |
| `Assets/Data/Combat/Actions/Unagi/ActioniDefinition/Unagi_Attack_03.asset` | BAKED_FRAME_COUNT_MISMATCH: bakedMotion.frameCount=261，TotalFrames=260。 | totalFrames=260; baked.frameCount=261; bakeStatus=1 |
| `Assets/Data/Combat/Actions/Unagi/ActioniDefinition/Unagi_Attack_Rush_02.asset` | BAKED_FRAME_COUNT_MISMATCH: bakedMotion.frameCount=261，TotalFrames=260。 | totalFrames=260; baked.frameCount=261; bakeStatus=1 |
| `Assets/Data/Combat/Actions/Unagi/ActioniDefinition/Unagi_SwitchOut.asset` | BAKED_FRAME_COUNT_MISMATCH: bakedMotion.frameCount=62，TotalFrames=61。 | totalFrames=61; baked.frameCount=62; bakeStatus=1 |
| `Assets/Data/Combat/Actions/Unagi/ActioniDefinition/Unagi_Attack_Rush.asset` | BAKED_FRAME_COUNT_MISMATCH: bakedMotion.frameCount=254，TotalFrames=252。 | totalFrames=252; baked.frameCount=254; bakeStatus=1 |
| `Assets/Data/Combat/Actions/Monster/ActionDefinition/Monster_Goblin_Hit_L_Front.asset` | BAKED_FRAME_COUNT_MISMATCH: bakedMotion.frameCount=106，TotalFrames=105。 | totalFrames=105; baked.frameCount=106; bakeStatus=1 |
| `Assets/Data/Combat/Actions/Unagi/ActioniDefinition/Unagi_Attack_02.asset` | BAKED_FRAME_COUNT_MISMATCH: bakedMotion.frameCount=246，TotalFrames=244。 | totalFrames=244; baked.frameCount=246; bakeStatus=1 |
| `Assets/Data/Combat/Actions/Monster/ActionDefinition/Monster_Goblin_Attack_02.asset` | BAKED_FRAME_COUNT_MISMATCH: bakedMotion.frameCount=224，TotalFrames=223。 | totalFrames=223; baked.frameCount=224; bakeStatus=1 |
| `Assets/Data/Combat/Actions/Unagi/ActioniDefinition/Unagi_Combo_2.asset` | BAKED_FRAME_COUNT_MISMATCH: bakedMotion.frameCount=179，TotalFrames=178。 | totalFrames=178; baked.frameCount=179; bakeStatus=1 |
| `Assets/Data/Combat/Actions/Unagi/ActioniDefinition/Unagi_Attack_06.asset` | BAKED_FRAME_COUNT_MISMATCH: bakedMotion.frameCount=286，TotalFrames=284。 | totalFrames=284; baked.frameCount=286; bakeStatus=1 |
| `Assets/Data/Combat/Actions/Unagi/ActioniDefinition/Unagi_SwitchIn.asset` | BAKED_FRAME_COUNT_MISMATCH: bakedMotion.frameCount=212，TotalFrames=211。 | totalFrames=211; baked.frameCount=212; bakeStatus=1 |
| `Assets/Data/Combat/Actions/Anbi/ActionDefinition/Anbi_SwitchIn.asset` | BAKED_FRAME_COUNT_MISMATCH: bakedMotion.frameCount=107，TotalFrames=106。 | totalFrames=106; baked.frameCount=107; bakeStatus=1 |
| `Assets/Data/Combat/Actions/Monster/ActionDefinition/Monster_Goblin_Hit_Shake.asset` | BAKED_MODE_NOT_READY: BaseMotionMode=BakedMotion 但运动表未就绪。 | totalFrames=25; baked.frameCount=0; bakeStatus=0 |
| `Assets/Data/Combat/Actions/Unagi/ActioniDefinition/Unagi_Attack_01.asset` | BAKED_FRAME_COUNT_MISMATCH: bakedMotion.frameCount=242，TotalFrames=240。 | totalFrames=240; baked.frameCount=242; bakeStatus=1 |

敌人 Unagi 的 StartEnd/StopL/StopR/PivotTurn 根位移轨均为空；文件语义对照证明与迁移前相同。结构规则结果见 CHARACTER_AUTHORING_STRUCTURE_CHECK.json，新增源码规则差异为 0。

Test Runner 最新结果为 62 passed / 0 failed / 0 skipped，其中本次新增测试 16 项。结果文件：CHARACTER_AUTHORING_UNITY_RESULTS.xml。实际 Play、隔离预览视觉检查、双进程内容指纹仍未执行。
