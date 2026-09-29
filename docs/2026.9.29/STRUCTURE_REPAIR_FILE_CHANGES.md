# 结构修复逐文件清单

仅记录本轮改动；此前本地修改保留。43 个文件拆为 165 个类型文件，其中 34 个原文件保留、9 个原文件连同 .meta 改名、122 个新增脚本由 Unity 生成 .meta。类型正文未改，程序集和命名空间保持。

| 操作 | 文件 | 说明 |
|---|---|---|
| 新增 | `Assets/Scripts/App/Architecture/Contracts/IBelongToArchitecture.cs` | 从 Assets/Scripts/App/Architecture/Contracts/ArchitectureCapabilities.cs 拆出 IBelongToArchitecture |
| 新增 | `Assets/Scripts/App/Architecture/Contracts/ICanSetArchitecture.cs` | 从 Assets/Scripts/App/Architecture/Contracts/ArchitectureCapabilities.cs 拆出 ICanSetArchitecture |
| 新增 | `Assets/Scripts/App/Architecture/Contracts/ICanGetSystem.cs` | 从 Assets/Scripts/App/Architecture/Contracts/ArchitectureCapabilities.cs 拆出 ICanGetSystem |
| 新增 | `Assets/Scripts/App/Architecture/Contracts/ICanGetModel.cs` | 从 Assets/Scripts/App/Architecture/Contracts/ArchitectureCapabilities.cs 拆出 ICanGetModel |
| 新增 | `Assets/Scripts/App/Architecture/Contracts/ICanGetUtility.cs` | 从 Assets/Scripts/App/Architecture/Contracts/ArchitectureCapabilities.cs 拆出 ICanGetUtility |
| 新增 | `Assets/Scripts/App/Architecture/Contracts/ICanSendCommand.cs` | 从 Assets/Scripts/App/Architecture/Contracts/ArchitectureCapabilities.cs 拆出 ICanSendCommand |
| 新增 | `Assets/Scripts/App/Architecture/Contracts/ICanSendQuery.cs` | 从 Assets/Scripts/App/Architecture/Contracts/ArchitectureCapabilities.cs 拆出 ICanSendQuery |
| 新增 | `Assets/Scripts/App/Architecture/Contracts/ICanRegisterEvent.cs` | 从 Assets/Scripts/App/Architecture/Contracts/ArchitectureCapabilities.cs 拆出 ICanRegisterEvent |
| 新增 | `Assets/Scripts/App/Architecture/Contracts/ICanSendEvent.cs` | 从 Assets/Scripts/App/Architecture/Contracts/ArchitectureCapabilities.cs 拆出 ICanSendEvent |
| 移动 | `Assets/Scripts/App/Architecture/Contracts/ArchitectureCapabilityExtensions.cs` | 从 Assets/Scripts/App/Architecture/Contracts/ArchitectureCapabilities.cs 移入；保留原 .meta/GUID |
| 修改 | `Assets/Scripts/App/Controllers/Gameplay/EnemySpawnController.cs` | 保留主类型并移出同文件其他公开类型 |
| 新增 | `Assets/Scripts/App/Controllers/Gameplay/EnemySpawnEntry.cs` | 从 Assets/Scripts/App/Controllers/Gameplay/EnemySpawnController.cs 拆出 EnemySpawnEntry |
| 修改 | `Assets/Scripts/App/Networking/Adapters/ActAuthorityReplicationAdapter.cs` | 保留主类型并移出同文件其他公开类型 |
| 新增 | `Assets/Scripts/App/Networking/Adapters/ActAuthorityInputApplyResult.cs` | 从 Assets/Scripts/App/Networking/Adapters/ActAuthorityReplicationAdapter.cs 拆出 ActAuthorityInputApplyResult |
| 修改 | `Assets/Scripts/App/Networking/Adapters/ActGameSessionHandler.cs` | 保留主类型并移出同文件其他公开类型 |
| 新增 | `Assets/Scripts/App/Networking/Adapters/ActGameSessionServices.cs` | 从 Assets/Scripts/App/Networking/Adapters/ActGameSessionHandler.cs 拆出 ActGameSessionServices |
| 新增 | `Assets/Scripts/App/Networking/Adapters/ActGameGuest.cs` | 从 Assets/Scripts/App/Networking/Adapters/ActGameSessionHandler.cs 拆出 ActGameGuest |
| 修改 | `Assets/Scripts/App/Systems/Combat/CombatActorSystem.cs` | 保留主类型并移出同文件其他公开类型 |
| 新增 | `Assets/Scripts/App/Systems/Combat/CombatActorEntry.cs` | 从 Assets/Scripts/App/Systems/Combat/CombatActorSystem.cs 拆出 CombatActorEntry |
| 修改 | `Assets/Scripts/Domain/Character/CharacterConfig.cs` | 保留主类型并移出同文件其他公开类型 |
| 新增 | `Assets/Scripts/Domain/Character/CharacterMotorConfig.cs` | 从 Assets/Scripts/Domain/Character/CharacterConfig.cs 拆出 CharacterMotorConfig |
| 新增 | `Assets/Scripts/Domain/Character/CharacterCombatConfig.cs` | 从 Assets/Scripts/Domain/Character/CharacterConfig.cs 拆出 CharacterCombatConfig |
| 修改 | `Assets/Scripts/Domain/Character/Locomotion/CharacterLocomotionProfile.cs` | 保留主类型并移出同文件其他公开类型 |
| 新增 | `Assets/Scripts/Domain/Character/Locomotion/FootPlantMarker.cs` | 从 Assets/Scripts/Domain/Character/Locomotion/CharacterLocomotionProfile.cs 拆出 FootPlantMarker |
| 修改 | `Assets/Scripts/Domain/Character/Locomotion/ILocomotionAnimResolver.cs` | 保留主类型并移出同文件其他公开类型 |
| 新增 | `Assets/Scripts/Domain/Character/Locomotion/ILocomotionAnimClipQuery.cs` | 从 Assets/Scripts/Domain/Character/Locomotion/ILocomotionAnimResolver.cs 拆出 ILocomotionAnimClipQuery |
| 修改 | `Assets/Scripts/Domain/Character/Locomotion/LocomotionGaitPolicy.cs` | 保留主类型并移出同文件其他公开类型 |
| 新增 | `Assets/Scripts/Domain/Character/Locomotion/GaitPolicyInput.cs` | 从 Assets/Scripts/Domain/Character/Locomotion/LocomotionGaitPolicy.cs 拆出 GaitPolicyInput |
| 新增 | `Assets/Scripts/Domain/Character/Locomotion/GaitPolicyResult.cs` | 从 Assets/Scripts/Domain/Character/Locomotion/LocomotionGaitPolicy.cs 拆出 GaitPolicyResult |
| 修改 | `Assets/Scripts/Domain/Combat/Actions/Definitions/Timeline/ActionNotifyChannel.cs` | 保留主类型并移出同文件其他公开类型 |
| 新增 | `Assets/Scripts/Domain/Combat/Actions/Definitions/Timeline/ActionNotifyClassification.cs` | 从 Assets/Scripts/Domain/Combat/Actions/Definitions/Timeline/ActionNotifyChannel.cs 拆出 ActionNotifyClassification |
| 新增 | `Assets/Scripts/Domain/Combat/Actions/Definitions/Timeline/ActionTimelineTrackKind.cs` | 从 Assets/Scripts/Domain/Combat/Actions/Definitions/Timeline/ActionTimelineTrack.cs 拆出 ActionTimelineTrackKind |
| 修改 | `Assets/Scripts/Domain/Combat/Actions/Definitions/Timeline/ActionTimelineTrack.cs` | 保留主类型并移出同文件其他公开类型 |
| 修改 | `Assets/Scripts/Domain/Combat/Actions/Definitions/Timeline/IActionNotifyConsumer.cs` | 保留主类型并移出同文件其他公开类型 |
| 新增 | `Assets/Scripts/Domain/Combat/Actions/Definitions/Timeline/IActionVisibilityResetConsumer.cs` | 从 Assets/Scripts/Domain/Combat/Actions/Definitions/Timeline/IActionNotifyConsumer.cs 拆出 IActionVisibilityResetConsumer |
| 新增 | `Assets/Scripts/Domain/Combat/Actions/Frames/ActionFrameQueryResult.cs` | 从 Assets/Scripts/Domain/Combat/Actions/Frames/ActionFrameQuery.cs 拆出 ActionFrameQueryResult |
| 修改 | `Assets/Scripts/Domain/Combat/Actions/Frames/ActionFrameQuery.cs` | 保留主类型并移出同文件其他公开类型 |
| 修改 | `Assets/Scripts/Domain/Combat/Actions/Resolution/ActionGraph.cs` | 保留主类型并移出同文件其他公开类型 |
| 新增 | `Assets/Scripts/Domain/Combat/Actions/Resolution/ActionGraphNode.cs` | 从 Assets/Scripts/Domain/Combat/Actions/Resolution/ActionGraph.cs 拆出 ActionGraphNode |
| 新增 | `Assets/Scripts/Domain/Combat/Actions/Resolution/ActionGraphEdge.cs` | 从 Assets/Scripts/Domain/Combat/Actions/Resolution/ActionGraph.cs 拆出 ActionGraphEdge |
| 修改 | `Assets/Scripts/Domain/Combat/Actions/Resolution/ActionResolveContext.cs` | 保留主类型并移出同文件其他公开类型 |
| 新增 | `Assets/Scripts/Domain/Combat/Actions/Resolution/ActionResolveOrigin.cs` | 从 Assets/Scripts/Domain/Combat/Actions/Resolution/ActionResolveContext.cs 拆出 ActionResolveOrigin |
| 新增 | `Assets/Scripts/Domain/Combat/Actions/Validation/ActionDefinitionAuditSeverity.cs` | 从 Assets/Scripts/Domain/Combat/Actions/Validation/ActionDefinitionAuditIssue.cs 拆出 ActionDefinitionAuditSeverity |
| 修改 | `Assets/Scripts/Domain/Combat/Actions/Validation/ActionDefinitionAuditIssue.cs` | 保留主类型并移出同文件其他公开类型 |
| 新增 | `Assets/Scripts/Domain/Combat/Camera/CameraMode.cs` | 从 Assets/Scripts/Domain/Combat/Camera/CameraDirectorStack.cs 拆出 CameraMode |
| 新增 | `Assets/Scripts/Domain/Combat/Camera/CameraDirectorEntry.cs` | 从 Assets/Scripts/Domain/Combat/Camera/CameraDirectorStack.cs 拆出 CameraDirectorEntry |
| 修改 | `Assets/Scripts/Domain/Combat/Camera/CameraDirectorStack.cs` | 保留主类型并移出同文件其他公开类型 |
| 新增 | `Assets/Scripts/Domain/Combat/Camera/CameraBindingSource.cs` | 从 Assets/Scripts/Domain/Combat/Camera/CameraShotTypes.cs 拆出 CameraBindingSource |
| 新增 | `Assets/Scripts/Domain/Combat/Camera/CameraBindingSpace.cs` | 从 Assets/Scripts/Domain/Combat/Camera/CameraShotTypes.cs 拆出 CameraBindingSpace |
| 新增 | `Assets/Scripts/Domain/Combat/Camera/CameraSplineCurveRule.cs` | 从 Assets/Scripts/Domain/Combat/Camera/CameraShotTypes.cs 拆出 CameraSplineCurveRule |
| 新增 | `Assets/Scripts/Domain/Combat/Camera/CameraTransformBinding.cs` | 从 Assets/Scripts/Domain/Combat/Camera/CameraShotTypes.cs 拆出 CameraTransformBinding |
| 新增 | `Assets/Scripts/Domain/Combat/Camera/ICameraAnchorProvider.cs` | 从 Assets/Scripts/Domain/Combat/Camera/CameraShotTypes.cs 拆出 ICameraAnchorProvider |
| 移动 | `Assets/Scripts/Domain/Combat/Camera/CameraReferencePose.cs` | 从 Assets/Scripts/Domain/Combat/Camera/CameraShotTypes.cs 移入；保留原 .meta/GUID |
| 新增 | `Assets/Scripts/Domain/Combat/Camera/CameraShotPose.cs` | 从 Assets/Scripts/Domain/Combat/Camera/CameraShotTypes.cs 拆出 CameraShotPose |
| 新增 | `Assets/Scripts/Domain/Combat/Camera/CameraRestoreMode.cs` | 从 Assets/Scripts/Domain/Combat/Camera/CameraShotTypes.cs 拆出 CameraRestoreMode |
| 新增 | `Assets/Scripts/Domain/Combat/Camera/CameraTrackSettings.cs` | 从 Assets/Scripts/Domain/Combat/Camera/CameraShotTypes.cs 拆出 CameraTrackSettings |
| 新增 | `Assets/Scripts/Domain/Combat/Hitbox/HitboxOrientedBox.cs` | 从 Assets/Scripts/Domain/Combat/Hitbox/HitboxMath.cs 拆出 HitboxOrientedBox |
| 修改 | `Assets/Scripts/Domain/Combat/Hitbox/HitboxMath.cs` | 保留主类型并移出同文件其他公开类型 |
| 修改 | `Assets/Scripts/Domain/Combat/Numeric/NumericDebugSnapshot.cs` | 保留主类型并移出同文件其他公开类型 |
| 新增 | `Assets/Scripts/Domain/Combat/Numeric/NumericEffectDebugEntry.cs` | 从 Assets/Scripts/Domain/Combat/Numeric/NumericDebugSnapshot.cs 拆出 NumericEffectDebugEntry |
| 修改 | `Assets/Scripts/Domain/Enemy/BehaviorTree/EnemyCooldownTable.cs` | 保留主类型并移出同文件其他公开类型 |
| 新增 | `Assets/Scripts/Domain/Enemy/BehaviorTree/EnemyCooldownIds.cs` | 从 Assets/Scripts/Domain/Enemy/BehaviorTree/EnemyCooldownTable.cs 拆出 EnemyCooldownIds |
| 修改 | `Assets/Scripts/Domain/Enemy/BehaviorTree/IEnemyBehaviorRandom.cs` | 保留主类型并移出同文件其他公开类型 |
| 新增 | `Assets/Scripts/Domain/Enemy/BehaviorTree/SystemEnemyBehaviorRandom.cs` | 从 Assets/Scripts/Domain/Enemy/BehaviorTree/IEnemyBehaviorRandom.cs 拆出 SystemEnemyBehaviorRandom |
| 新增 | `Assets/Scripts/Domain/Enemy/BehaviorTree/SequenceEnemyBehaviorRandom.cs` | 从 Assets/Scripts/Domain/Enemy/BehaviorTree/IEnemyBehaviorRandom.cs 拆出 SequenceEnemyBehaviorRandom |
| 新增 | `Assets/Scripts/Domain/Enemy/BehaviorTree/Nodes/StopMoveAction.cs` | 从 Assets/Scripts/Domain/Enemy/BehaviorTree/Nodes/ActionNodes.cs 拆出 StopMoveAction |
| 移动 | `Assets/Scripts/Domain/Enemy/BehaviorTree/Nodes/MoveTowardTargetAction.cs` | 从 Assets/Scripts/Domain/Enemy/BehaviorTree/Nodes/ActionNodes.cs 移入；保留原 .meta/GUID |
| 新增 | `Assets/Scripts/Domain/Enemy/BehaviorTree/Nodes/BackOffFromTargetAction.cs` | 从 Assets/Scripts/Domain/Enemy/BehaviorTree/Nodes/ActionNodes.cs 拆出 BackOffFromTargetAction |
| 新增 | `Assets/Scripts/Domain/Enemy/BehaviorTree/Nodes/StrafeAroundTargetAction.cs` | 从 Assets/Scripts/Domain/Enemy/BehaviorTree/Nodes/ActionNodes.cs 拆出 StrafeAroundTargetAction |
| 新增 | `Assets/Scripts/Domain/Enemy/BehaviorTree/Nodes/FaceTargetAction.cs` | 从 Assets/Scripts/Domain/Enemy/BehaviorTree/Nodes/ActionNodes.cs 拆出 FaceTargetAction |
| 新增 | `Assets/Scripts/Domain/Enemy/BehaviorTree/Nodes/RequestCombatAction.cs` | 从 Assets/Scripts/Domain/Enemy/BehaviorTree/Nodes/ActionNodes.cs 拆出 RequestCombatAction |
| 新增 | `Assets/Scripts/Domain/Enemy/BehaviorTree/Nodes/WaitWhileInActionAction.cs` | 从 Assets/Scripts/Domain/Enemy/BehaviorTree/Nodes/ActionNodes.cs 拆出 WaitWhileInActionAction |
| 新增 | `Assets/Scripts/Domain/Enemy/BehaviorTree/Nodes/WaitFramesAction.cs` | 从 Assets/Scripts/Domain/Enemy/BehaviorTree/Nodes/ActionNodes.cs 拆出 WaitFramesAction |
| 移动 | `Assets/Scripts/Domain/Enemy/BehaviorTree/Nodes/RandomSelectorNode.cs` | 从 Assets/Scripts/Domain/Enemy/BehaviorTree/Nodes/CompositeNodes.cs 移入；保留原 .meta/GUID |
| 新增 | `Assets/Scripts/Domain/Enemy/BehaviorTree/Nodes/SelectorNode.cs` | 从 Assets/Scripts/Domain/Enemy/BehaviorTree/Nodes/CompositeNodes.cs 拆出 SelectorNode |
| 新增 | `Assets/Scripts/Domain/Enemy/BehaviorTree/Nodes/SequenceNode.cs` | 从 Assets/Scripts/Domain/Enemy/BehaviorTree/Nodes/CompositeNodes.cs 拆出 SequenceNode |
| 新增 | `Assets/Scripts/Domain/Enemy/BehaviorTree/Nodes/ConditionalDecoratorNode.cs` | 从 Assets/Scripts/Domain/Enemy/BehaviorTree/Nodes/ConditionNodes.cs 拆出 ConditionalDecoratorNode |
| 新增 | `Assets/Scripts/Domain/Enemy/BehaviorTree/Nodes/DistanceBandMode.cs` | 从 Assets/Scripts/Domain/Enemy/BehaviorTree/Nodes/ConditionNodes.cs 拆出 DistanceBandMode |
| 移动 | `Assets/Scripts/Domain/Enemy/BehaviorTree/Nodes/DistanceBandCondition.cs` | 从 Assets/Scripts/Domain/Enemy/BehaviorTree/Nodes/ConditionNodes.cs 移入；保留原 .meta/GUID |
| 新增 | `Assets/Scripts/Domain/Enemy/BehaviorTree/Nodes/HasTargetCondition.cs` | 从 Assets/Scripts/Domain/Enemy/BehaviorTree/Nodes/ConditionNodes.cs 拆出 HasTargetCondition |
| 新增 | `Assets/Scripts/Domain/Enemy/BehaviorTree/Nodes/InCombatAggroCondition.cs` | 从 Assets/Scripts/Domain/Enemy/BehaviorTree/Nodes/ConditionNodes.cs 拆出 InCombatAggroCondition |
| 新增 | `Assets/Scripts/Domain/Enemy/BehaviorTree/Nodes/InAttackRangeCondition.cs` | 从 Assets/Scripts/Domain/Enemy/BehaviorTree/Nodes/ConditionNodes.cs 拆出 InAttackRangeCondition |
| 新增 | `Assets/Scripts/Domain/Enemy/BehaviorTree/Nodes/IsCharacterStateCondition.cs` | 从 Assets/Scripts/Domain/Enemy/BehaviorTree/Nodes/ConditionNodes.cs 拆出 IsCharacterStateCondition |
| 新增 | `Assets/Scripts/Domain/Enemy/BehaviorTree/Nodes/CooldownReadyCondition.cs` | 从 Assets/Scripts/Domain/Enemy/BehaviorTree/Nodes/ConditionNodes.cs 拆出 CooldownReadyCondition |
| 新增 | `Assets/Scripts/Domain/Enemy/BehaviorTree/Nodes/CooldownNotReadyCondition.cs` | 从 Assets/Scripts/Domain/Enemy/BehaviorTree/Nodes/ConditionNodes.cs 拆出 CooldownNotReadyCondition |
| 新增 | `Assets/Scripts/Domain/Enemy/BehaviorTree/Nodes/DistanceLessEqualCondition.cs` | 从 Assets/Scripts/Domain/Enemy/BehaviorTree/Nodes/ConditionNodes.cs 拆出 DistanceLessEqualCondition |
| 新增 | `Assets/Scripts/Domain/Enemy/BehaviorTree/Nodes/DistanceGreaterCondition.cs` | 从 Assets/Scripts/Domain/Enemy/BehaviorTree/Nodes/ConditionNodes.cs 拆出 DistanceGreaterCondition |
| 新增 | `Assets/Scripts/Domain/Enemy/BehaviorTree/Nodes/InverterNode.cs` | 从 Assets/Scripts/Domain/Enemy/BehaviorTree/Nodes/DecoratorNodes.cs 拆出 InverterNode |
| 新增 | `Assets/Scripts/Domain/Enemy/BehaviorTree/Nodes/SucceederNode.cs` | 从 Assets/Scripts/Domain/Enemy/BehaviorTree/Nodes/DecoratorNodes.cs 拆出 SucceederNode |
| 移动 | `Assets/Scripts/Domain/Enemy/BehaviorTree/Nodes/CooldownGateNode.cs` | 从 Assets/Scripts/Domain/Enemy/BehaviorTree/Nodes/DecoratorNodes.cs 移入；保留原 .meta/GUID |
| 新增 | `Assets/Scripts/Domain/Enemy/BehaviorTree/Nodes/AggroGateNode.cs` | 从 Assets/Scripts/Domain/Enemy/BehaviorTree/Nodes/DecoratorNodes.cs 拆出 AggroGateNode |
| 修改 | `Assets/Scripts/Domain/Enemy/BehaviorTree/Serialization/EnemyBehaviorGraphLayout.cs` | 保留主类型并移出同文件其他公开类型 |
| 新增 | `Assets/Scripts/Domain/Enemy/BehaviorTree/Serialization/EnemyBehaviorGraphNodeLayout.cs` | 从 Assets/Scripts/Domain/Enemy/BehaviorTree/Serialization/EnemyBehaviorGraphLayout.cs 拆出 EnemyBehaviorGraphNodeLayout |
| 新增 | `Assets/Scripts/Domain/Enemy/BehaviorTree/Serialization/EnemyBehaviorGraphStickyNote.cs` | 从 Assets/Scripts/Domain/Enemy/BehaviorTree/Serialization/EnemyBehaviorGraphLayout.cs 拆出 EnemyBehaviorGraphStickyNote |
| 修改 | `Assets/Scripts/Domain/Enemy/BehaviorTree/Serialization/EnemyBehaviorNodeDef.cs` | 保留主类型并移出同文件其他公开类型 |
| 新增 | `Assets/Scripts/Domain/Enemy/BehaviorTree/Serialization/SelectorNodeDef.cs` | 从 Assets/Scripts/Domain/Enemy/BehaviorTree/Serialization/EnemyBehaviorNodeDef.cs 拆出 SelectorNodeDef |
| 新增 | `Assets/Scripts/Domain/Enemy/BehaviorTree/Serialization/SequenceNodeDef.cs` | 从 Assets/Scripts/Domain/Enemy/BehaviorTree/Serialization/EnemyBehaviorNodeDef.cs 拆出 SequenceNodeDef |
| 新增 | `Assets/Scripts/Domain/Enemy/BehaviorTree/Serialization/RandomSelectorNodeDef.cs` | 从 Assets/Scripts/Domain/Enemy/BehaviorTree/Serialization/EnemyBehaviorNodeDef.cs 拆出 RandomSelectorNodeDef |
| 新增 | `Assets/Scripts/Domain/Enemy/BehaviorTree/Serialization/InverterNodeDef.cs` | 从 Assets/Scripts/Domain/Enemy/BehaviorTree/Serialization/EnemyBehaviorNodeDef.cs 拆出 InverterNodeDef |
| 新增 | `Assets/Scripts/Domain/Enemy/BehaviorTree/Serialization/SucceederNodeDef.cs` | 从 Assets/Scripts/Domain/Enemy/BehaviorTree/Serialization/EnemyBehaviorNodeDef.cs 拆出 SucceederNodeDef |
| 新增 | `Assets/Scripts/Domain/Enemy/BehaviorTree/Serialization/CooldownGateNodeDef.cs` | 从 Assets/Scripts/Domain/Enemy/BehaviorTree/Serialization/EnemyBehaviorNodeDef.cs 拆出 CooldownGateNodeDef |
| 新增 | `Assets/Scripts/Domain/Enemy/BehaviorTree/Serialization/AggroGateNodeDef.cs` | 从 Assets/Scripts/Domain/Enemy/BehaviorTree/Serialization/EnemyBehaviorNodeDef.cs 拆出 AggroGateNodeDef |
| 新增 | `Assets/Scripts/Domain/Enemy/BehaviorTree/Serialization/EnemyBehaviorConditionNodeDef.cs` | 从 Assets/Scripts/Domain/Enemy/BehaviorTree/Serialization/EnemyBehaviorNodeDef.cs 拆出 EnemyBehaviorConditionNodeDef |
| 新增 | `Assets/Scripts/Domain/Enemy/BehaviorTree/Serialization/HasTargetConditionDef.cs` | 从 Assets/Scripts/Domain/Enemy/BehaviorTree/Serialization/EnemyBehaviorNodeDef.cs 拆出 HasTargetConditionDef |
| 新增 | `Assets/Scripts/Domain/Enemy/BehaviorTree/Serialization/InCombatAggroConditionDef.cs` | 从 Assets/Scripts/Domain/Enemy/BehaviorTree/Serialization/EnemyBehaviorNodeDef.cs 拆出 InCombatAggroConditionDef |
| 新增 | `Assets/Scripts/Domain/Enemy/BehaviorTree/Serialization/InAttackRangeConditionDef.cs` | 从 Assets/Scripts/Domain/Enemy/BehaviorTree/Serialization/EnemyBehaviorNodeDef.cs 拆出 InAttackRangeConditionDef |
| 新增 | `Assets/Scripts/Domain/Enemy/BehaviorTree/Serialization/IsCharacterStateConditionDef.cs` | 从 Assets/Scripts/Domain/Enemy/BehaviorTree/Serialization/EnemyBehaviorNodeDef.cs 拆出 IsCharacterStateConditionDef |
| 新增 | `Assets/Scripts/Domain/Enemy/BehaviorTree/Serialization/CooldownReadyConditionDef.cs` | 从 Assets/Scripts/Domain/Enemy/BehaviorTree/Serialization/EnemyBehaviorNodeDef.cs 拆出 CooldownReadyConditionDef |
| 新增 | `Assets/Scripts/Domain/Enemy/BehaviorTree/Serialization/CooldownNotReadyConditionDef.cs` | 从 Assets/Scripts/Domain/Enemy/BehaviorTree/Serialization/EnemyBehaviorNodeDef.cs 拆出 CooldownNotReadyConditionDef |
| 新增 | `Assets/Scripts/Domain/Enemy/BehaviorTree/Serialization/DistanceLessEqualConditionDef.cs` | 从 Assets/Scripts/Domain/Enemy/BehaviorTree/Serialization/EnemyBehaviorNodeDef.cs 拆出 DistanceLessEqualConditionDef |
| 新增 | `Assets/Scripts/Domain/Enemy/BehaviorTree/Serialization/DistanceGreaterConditionDef.cs` | 从 Assets/Scripts/Domain/Enemy/BehaviorTree/Serialization/EnemyBehaviorNodeDef.cs 拆出 DistanceGreaterConditionDef |
| 新增 | `Assets/Scripts/Domain/Enemy/BehaviorTree/Serialization/DistanceBandConditionDef.cs` | 从 Assets/Scripts/Domain/Enemy/BehaviorTree/Serialization/EnemyBehaviorNodeDef.cs 拆出 DistanceBandConditionDef |
| 新增 | `Assets/Scripts/Domain/Enemy/BehaviorTree/Serialization/StopMoveActionDef.cs` | 从 Assets/Scripts/Domain/Enemy/BehaviorTree/Serialization/EnemyBehaviorNodeDef.cs 拆出 StopMoveActionDef |
| 新增 | `Assets/Scripts/Domain/Enemy/BehaviorTree/Serialization/MoveTowardTargetActionDef.cs` | 从 Assets/Scripts/Domain/Enemy/BehaviorTree/Serialization/EnemyBehaviorNodeDef.cs 拆出 MoveTowardTargetActionDef |
| 新增 | `Assets/Scripts/Domain/Enemy/BehaviorTree/Serialization/BackOffFromTargetActionDef.cs` | 从 Assets/Scripts/Domain/Enemy/BehaviorTree/Serialization/EnemyBehaviorNodeDef.cs 拆出 BackOffFromTargetActionDef |
| 新增 | `Assets/Scripts/Domain/Enemy/BehaviorTree/Serialization/StrafeAroundTargetActionDef.cs` | 从 Assets/Scripts/Domain/Enemy/BehaviorTree/Serialization/EnemyBehaviorNodeDef.cs 拆出 StrafeAroundTargetActionDef |
| 新增 | `Assets/Scripts/Domain/Enemy/BehaviorTree/Serialization/FaceTargetActionDef.cs` | 从 Assets/Scripts/Domain/Enemy/BehaviorTree/Serialization/EnemyBehaviorNodeDef.cs 拆出 FaceTargetActionDef |
| 新增 | `Assets/Scripts/Domain/Enemy/BehaviorTree/Serialization/RequestCombatActionDef.cs` | 从 Assets/Scripts/Domain/Enemy/BehaviorTree/Serialization/EnemyBehaviorNodeDef.cs 拆出 RequestCombatActionDef |
| 新增 | `Assets/Scripts/Domain/Enemy/BehaviorTree/Serialization/WaitWhileInActionActionDef.cs` | 从 Assets/Scripts/Domain/Enemy/BehaviorTree/Serialization/EnemyBehaviorNodeDef.cs 拆出 WaitWhileInActionActionDef |
| 新增 | `Assets/Scripts/Domain/Enemy/BehaviorTree/Serialization/WaitFramesActionDef.cs` | 从 Assets/Scripts/Domain/Enemy/BehaviorTree/Serialization/EnemyBehaviorNodeDef.cs 拆出 WaitFramesActionDef |
| 新增 | `Assets/Scripts/Domain/Enemy/BehaviorTree/Serialization/EnemyBehaviorTreeValidationResult.cs` | 从 Assets/Scripts/Domain/Enemy/BehaviorTree/Serialization/EnemyBehaviorTreeValidator.cs 拆出 EnemyBehaviorTreeValidationResult |
| 修改 | `Assets/Scripts/Domain/Enemy/BehaviorTree/Serialization/EnemyBehaviorTreeValidator.cs` | 保留主类型并移出同文件其他公开类型 |
| 新增 | `Assets/Scripts/Domain/Enemy/EnemyPerceptionSnapshot.cs` | 从 Assets/Scripts/Domain/Enemy/EnemyPerception.cs 拆出 EnemyPerceptionSnapshot |
| 修改 | `Assets/Scripts/Domain/Enemy/EnemyPerception.cs` | 保留主类型并移出同文件其他公开类型 |
| 修改 | `Assets/Scripts/Domain/Input/GameplayIntentProfile.cs` | 保留主类型并移出同文件其他公开类型 |
| 新增 | `Assets/Scripts/Domain/Input/GameplayIntentBinding.cs` | 从 Assets/Scripts/Domain/Input/GameplayIntentProfile.cs 拆出 GameplayIntentBinding |
| 新增 | `Assets/Scripts/Domain/Simulation/Party/PartyAssistPointSettings.cs` | 从 Assets/Scripts/Domain/Simulation/Party/PartyAssistPoints.cs 拆出 PartyAssistPointSettings |
| 修改 | `Assets/Scripts/Domain/Simulation/Party/PartyAssistPoints.cs` | 保留主类型并移出同文件其他公开类型 |
| 修改 | `Assets/Scripts/Domain/Simulation/Party/PartyDeathSwitchPolicy.cs` | 保留主类型并移出同文件其他公开类型 |
| 新增 | `Assets/Scripts/Domain/Simulation/Party/PartyDeathSwitchGate.cs` | 从 Assets/Scripts/Domain/Simulation/Party/PartyDeathSwitchPolicy.cs 拆出 PartyDeathSwitchGate |
| 新增 | `Assets/Scripts/Domain/Simulation/Party/PartyDeathCloseout.cs` | 从 Assets/Scripts/Domain/Simulation/Party/PartyDeathSwitchPolicy.cs 拆出 PartyDeathCloseout |
| 新增 | `Assets/Scripts/Domain/Simulation/Party/PartyLoadoutValidationError.cs` | 从 Assets/Scripts/Domain/Simulation/Party/PartyLoadoutRules.cs 拆出 PartyLoadoutValidationError |
| 修改 | `Assets/Scripts/Domain/Simulation/Party/PartyLoadoutRules.cs` | 保留主类型并移出同文件其他公开类型 |
| 修改 | `Assets/Scripts/Domain/Simulation/Party/PartyMemberState.cs` | 保留主类型并移出同文件其他公开类型 |
| 新增 | `Assets/Scripts/Domain/Simulation/Party/PartyReplicationPacking.cs` | 从 Assets/Scripts/Domain/Simulation/Party/PartyMemberState.cs 拆出 PartyReplicationPacking |
| 新增 | `Assets/Scripts/Domain/Simulation/Party/PartySwitchKind.cs` | 从 Assets/Scripts/Domain/Simulation/Party/PartySwitchCommand.cs 拆出 PartySwitchKind |
| 新增 | `Assets/Scripts/Domain/Simulation/Party/PartySwitchPresentation.cs` | 从 Assets/Scripts/Domain/Simulation/Party/PartySwitchCommand.cs 拆出 PartySwitchPresentation |
| 修改 | `Assets/Scripts/Domain/Simulation/Party/PartySwitchCommand.cs` | 保留主类型并移出同文件其他公开类型 |
| 新增 | `Assets/Scripts/Domain/Simulation/Party/PartySwitchPlacement.cs` | 从 Assets/Scripts/Domain/Simulation/Party/PartySwitchCommand.cs 拆出 PartySwitchPlacement |
| 新增 | `Assets/Scripts/Editor/Combat/ActionEditorPreviewContext.cs` | 从 Assets/Scripts/Editor/Combat/ActionEditorPreview.cs 拆出 ActionEditorPreviewContext |
| 新增 | `Assets/Scripts/Editor/Combat/IActionEditorPreviewExtension.cs` | 从 Assets/Scripts/Editor/Combat/ActionEditorPreview.cs 拆出 IActionEditorPreviewExtension |
| 新增 | `Assets/Scripts/Editor/Combat/ActionEditorPreviewAttachPoint.cs` | 从 Assets/Scripts/Editor/Combat/ActionEditorPreview.cs 拆出 ActionEditorPreviewAttachPoint |
| 新增 | `Assets/Scripts/Editor/Combat/ActionEditorAnimationSampler.cs` | 从 Assets/Scripts/Editor/Combat/ActionEditorPreview.cs 拆出 ActionEditorAnimationSampler |
| 移动 | `Assets/Scripts/Editor/Combat/ActionEditorPreviewSession.cs` | 从 Assets/Scripts/Editor/Combat/ActionEditorPreview.cs 移入；保留原 .meta/GUID |
| 新增 | `Assets/Scripts/Editor/Combat/ActionEditorVfxPreviewExtension.cs` | 从 Assets/Scripts/Editor/Combat/ActionEditorPreview.cs 拆出 ActionEditorVfxPreviewExtension |
| 新增 | `Assets/Scripts/Editor/Combat/Motion/MotionClipBakePair.cs` | 从 Assets/Scripts/Editor/Combat/Motion/MotionClipPairMatcher.cs 拆出 MotionClipBakePair |
| 新增 | `Assets/Scripts/Editor/Combat/Motion/MotionClipMatchIssue.cs` | 从 Assets/Scripts/Editor/Combat/Motion/MotionClipPairMatcher.cs 拆出 MotionClipMatchIssue |
| 修改 | `Assets/Scripts/Editor/Combat/Motion/MotionClipPairMatcher.cs` | 保留主类型并移出同文件其他公开类型 |
| 修改 | `Assets/Scripts/Framework/ACTNet/Replication/ReplicationClient.cs` | 保留主类型并移出同文件其他公开类型 |
| 新增 | `Assets/Scripts/Framework/ACTNet/Replication/ReplicationSnapshotApplyResult.cs` | 从 Assets/Scripts/Framework/ACTNet/Replication/ReplicationClient.cs 拆出 ReplicationSnapshotApplyResult |
| 新增 | `Assets/Scripts/Framework/ACTNet/Replication/ReplicationLifecycle.cs` | 从 Assets/Scripts/Framework/ACTNet/Replication/ReplicationProtocolV2.cs 拆出 ReplicationLifecycle |
| 移动 | `Assets/Scripts/Framework/ACTNet/Replication/ReplicationSnapshot.cs` | 从 Assets/Scripts/Framework/ACTNet/Replication/ReplicationProtocolV2.cs 移入；保留原 .meta/GUID |
| 新增 | `Assets/Scripts/Framework/ACTNet/Replication/ReplicationPacketCommitToken.cs` | 从 Assets/Scripts/Framework/ACTNet/Replication/ReplicationProtocolV2.cs 拆出 ReplicationPacketCommitToken |
| 新增 | `Assets/Scripts/Framework/ACTNet/Replication/ReplicationTickDelta.cs` | 从 Assets/Scripts/Framework/ACTNet/Replication/ReplicationProtocolV2.cs 拆出 ReplicationTickDelta |
| 新增 | `Assets/Scripts/Framework/ACTNet/Replication/PreparedReplicationPacket.cs` | 从 Assets/Scripts/Framework/ACTNet/Replication/ReplicationProtocolV2.cs 拆出 PreparedReplicationPacket |
| 新增 | `Assets/Scripts/Framework/ACTNet/Session/ClientSessionState.cs` | 从 Assets/Scripts/Framework/ACTNet/Session/ClientSession.cs 拆出 ClientSessionState |
| 修改 | `Assets/Scripts/Framework/ACTNet/Session/ClientSession.cs` | 保留主类型并移出同文件其他公开类型 |
| 新增 | `Assets/Scripts/Framework/ACTNet/Session/SessionJoinRequest.cs` | 从 Assets/Scripts/Framework/ACTNet/Session/SessionMessages.cs 拆出 SessionJoinRequest |
| 移动 | `Assets/Scripts/Framework/ACTNet/Session/SessionJoinAccept.cs` | 从 Assets/Scripts/Framework/ACTNet/Session/SessionMessages.cs 移入；保留原 .meta/GUID |
| 新增 | `Assets/Scripts/Framework/ACTNet/Session/SessionHeartbeat.cs` | 从 Assets/Scripts/Framework/ACTNet/Session/SessionMessages.cs 拆出 SessionHeartbeat |
| 新增 | `Assets/Scripts/Framework/ACTNet/Session/SessionPlayerRequest.cs` | 从 Assets/Scripts/Framework/ACTNet/Session/SessionMessages.cs 拆出 SessionPlayerRequest |
| 新增 | `Assets/Scripts/Framework/ACTNet/Session/SessionApplicationPacket.cs` | 从 Assets/Scripts/Framework/ACTNet/Session/SessionMessages.cs 拆出 SessionApplicationPacket |
| 新增 | `Assets/Scripts/Framework/ACTNet/Session/SessionDisconnected.cs` | 从 Assets/Scripts/Framework/ACTNet/Session/SessionMessages.cs 拆出 SessionDisconnected |
| 修改 | `Assets/Scripts/Previews/MalevolentShrine/MalevolentShrineDestructibleBuilding.cs` | 保留主类型并移出同文件其他公开类型 |
| 新增 | `Assets/Scripts/Previews/MalevolentShrine/MalevolentShrineSlicePiece.cs` | 从 Assets/Scripts/Previews/MalevolentShrine/MalevolentShrineDestructibleBuilding.cs 拆出 MalevolentShrineSlicePiece |
| 修改 | `Assets/Scripts/Editor/Architecture/StructureAuditRuleSet.cs` | 修正同名条件声明计数；登记 ActionSim 单职责；明确两个 UI 程序集的依赖集合；移除已拆分 NodeDef 集合的长度登记 |
| 修改 | `Assets/Scripts/App/Controllers/Combat/CombatWorldController.cs` | 配置文件读取仅捕获预期异常并记录异常类型，继续交由 ConfigFailed 处理 |
| 修改 | `Assets/Scripts/Editor/Combat/ActionEditor/Timeline/ActionTimelineClipboard.cs` | 损坏剪贴板粘贴返回前输出警告，反馈失败原因 |
| 修改 | `Assets/Scripts/Framework/ACTNet/Transport/ChannelMuxTransport.cs` | 按完整固定头和声明载荷长度拒绝坏包，移除 catch-all，保留调用方丢包计数 |
| 修改 | `Assets/Scripts/Editor/Character/CharacterAuthoringValidationRunner.cs` | 新增 run-all 入口，执行全部 EditMode 测试 |
| 新增 | `Assets/Scripts/UI/ACTGame.UI.asmdef` | 新增 UI 示例程序集，仅引用 UIFramework |
| 修改 | `Assets/Scripts/UI/UITestA.cs` | 增加责任注释与从 Assembly-CSharp 迁移的 MovedFrom 标记，保留脚本 GUID |
| 修改 | `Assets/Scripts/UI/UITestController.cs` | 增加责任注释与程序集迁移标记，原 UI 行为不变 |
| 修改 | `Assets/Tests/Editor/Architecture/StructureAuditRuleSetTests.cs` | 新增 5 个条件声明与 UI 依赖白名单用例，仍验证真正违规不能被放行 |
| 修改 | `Assets/Tests/Editor/Architecture/AssemblyReferenceBoundaryTests.cs` | 归一化 Windows 路径，修正程序集归属检查；目录比较加边界分隔符 |
| 修改 | `Assets/Tests/EditMode/ACTNet/Transport/ChannelMuxTransportTests.cs` | 新增 6 个坏包用例，断言丢包计数及后续合法包继续交付 |
