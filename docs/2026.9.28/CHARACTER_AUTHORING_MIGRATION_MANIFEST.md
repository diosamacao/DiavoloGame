# 角色配置迁移资产清单

> 只读磁盘清点；尚未迁移。原始字节与 meta 备份在 `.utmp/character-authoring-baseline.json`。
> Unity 正打开该项目；磁盘快照不代表尚未保存的 Editor 内存值。执行迁移前必须确认已保存相关配置。

修改：Config 内嵌模式、Locomotion 内嵌动画映射、Definition 删除预留标签、Action 删除采样率字段。
删除：已内嵌的两类 Profile 资产及其 meta。其它资产不修改，不改场景、Prefab、模型或 Clip。

| 操作 | 类型 | 路径 | SHA256 |
|---|---|---|---|
| modify | CharacterDefinition | `Assets/Data/CharacterConfig/Anbi.asset` | `b49547a21c419a585c7c48c926c113e4142ffb2143733c89b82f13c677d0a63d` |
| modify | CharacterDefinition | `Assets/Data/CharacterConfig/Unagi.asset` | `3f8c186180c3167a1809bda8186ffb0a5cdd066ba56fc58b0499a7ffef0dcb93` |
| modify | ActionDefinition | `Assets/Data/Combat/Actions/Anbi/ActionDefinition/Anbi_Attack_Normal_01.asset` | `9523588d1f551efdf862a08dd1443e18c12dd6e5e0a1b57293fc630b0f0e2beb` |
| modify | ActionDefinition | `Assets/Data/Combat/Actions/Anbi/ActionDefinition/Anbi_Attack_Normal_02.asset` | `e6c2d880bf988f7ae231bdf17aa3e988b2b3b6da544f7f0d2b45ece3265ae4c9` |
| modify | ActionDefinition | `Assets/Data/Combat/Actions/Anbi/ActionDefinition/Anbi_Attack_Normal_03.asset` | `a298d638d0d07609bcc8dcb0aa01bbdfdf58e2e7a793056dfa79df64ad815bd2` |
| modify | ActionDefinition | `Assets/Data/Combat/Actions/Anbi/ActionDefinition/Anbi_Attack_Normal_04.asset` | `fc5c7bc570bdf39943e34d2871c9439496ec51cf8e9fe435633a9b1c412896d1` |
| modify | ActionDefinition | `Assets/Data/Combat/Actions/Anbi/ActionDefinition/Anbi_Attack_Normal_04_Perfect.asset` | `1453a14f1a63de74aeb76dfaae4a4188aba4318b10d4befcf06e28359a3baeaf` |
| modify | ActionDefinition | `Assets/Data/Combat/Actions/Anbi/ActionDefinition/Anbi_Evade_Back.asset` | `4210704b3c0cce8c4eecbdc8abbb7021e4d03f8fbddeedc91d687ba4f5d57062` |
| modify | ActionDefinition | `Assets/Data/Combat/Actions/Anbi/ActionDefinition/Anbi_Evade_Front.asset` | `0555758054b58ae36bf227a6e49f60da60aa8a929464a3f029b7e66bf7f59320` |
| modify | ActionDefinition | `Assets/Data/Combat/Actions/Anbi/ActionDefinition/Anbi_Hurt.asset` | `e649028be792d64fd16c45ec6bf9329546957d16f7ffcc45607f93bb64c3436d` |
| modify | ActionDefinition | `Assets/Data/Combat/Actions/Anbi/ActionDefinition/Anbi_SwitchIn.asset` | `b650ac5f55a43bb66288f6099dc92f68fd6fc706ad0356053e44e8cc36b836e6` |
| modify | ActionDefinition | `Assets/Data/Combat/Actions/Anbi/ActionDefinition/Anbi_SwitchOut.asset` | `40b7fb82e456e89de337f5894e6d9096325013b3b8c3e66cbdf15c7c59ef10fa` |
| read-only | ActionGraph | `Assets/Data/Combat/Actions/Anbi/AnbiActionGraph.asset` | `4ca73ce68c7964c9fa7b5a9502fc21710569fb8e8330b07115a6b2a46ff0de54` |
| delete | CharacterAnimationProfile | `Assets/Data/Combat/Actions/Anbi/AnbiAnimation.asset` | `8b16c9ae214d293335c1646092dd94499b59eb7b28839f933b7a66a694943698` |
| delete | CombatModeProfile | `Assets/Data/Combat/Actions/Anbi/AnbiCombatModeProfile.asset` | `efbfaf657335ffe4e5b46a53022691157bcd24a1197a5fcd401ed46c4bfa8845` |
| modify | CharacterConfig | `Assets/Data/Combat/Actions/Anbi/AnbiConfig.asset` | `d519f94cdd6a88f837362c63aa60b9432c22156c30d964b4acd8b288ed1a077f` |
| modify | CharacterLocomotionProfile | `Assets/Data/Combat/Actions/Anbi/AnbiLocomotion.asset` | `0f36ea8731f86959a134ffd4929989e2fc7467df94a5828c737ae56ad96e9757` |
| modify | ActionDefinition | `Assets/Data/Combat/Actions/Giant/ActionDefinition/Giant_Attack_01.asset` | `8c7cf0eb4b69a44a4050b4c6ed86523bbac11f00667621cf83277eb61c3e03d8` |
| modify | ActionDefinition | `Assets/Data/Combat/Actions/Monster/ActionDefinition/Monster_Goblin_Attack_01.asset` | `202c82b5865065a816ebd19c3dde83bf5f54ec92f01d82bb02f0b942437592c6` |
| modify | ActionDefinition | `Assets/Data/Combat/Actions/Monster/ActionDefinition/Monster_Goblin_Attack_02.asset` | `d49996f334561d73fd444944119b8225f53275fa6c0136e4cecef9effce71e59` |
| modify | ActionDefinition | `Assets/Data/Combat/Actions/Monster/ActionDefinition/Monster_Goblin_Hit_L_Front.asset` | `20875585d356df0537a812d5b3760200804f5a634de5068c818198e6ad28c1df` |
| modify | ActionDefinition | `Assets/Data/Combat/Actions/Monster/ActionDefinition/Monster_Goblin_Hit_Shake.asset` | `77723d98251149527adb0fa17d5b63418fada43996525e22dd8db32be5f076ba` |
| modify | ActionDefinition | `Assets/Data/Combat/Actions/Monster/ActionDefinition/Monster_Goblin_Hit_Stay.asset` | `7acf2389e4926434be64cbc035a8bc7cbb6f7b61dea329814b10ae537dd24ddf` |
| modify | CharacterConfig | `Assets/Data/Combat/Actions/Monster/MonsterConfig.asset` | `62a17e48d01b8edb7a043fd57312af44ebe20bf70725687cc552231f39348a61` |
| modify | ActionDefinition | `Assets/Data/Combat/Actions/Unagi/ActioniDefinition/Unagi_Attack_01.asset` | `e549c341e6bd811549336a61513caf305163b6e5994b99d8b752406c86fa7cb1` |
| modify | ActionDefinition | `Assets/Data/Combat/Actions/Unagi/ActioniDefinition/Unagi_Attack_02.asset` | `ed727e014cc4b1f54fb1f18c0b5e617aeb4c8587c8e0b7c60d8cebd1de618740` |
| modify | ActionDefinition | `Assets/Data/Combat/Actions/Unagi/ActioniDefinition/Unagi_Attack_03.asset` | `93805dfa12e5fdfb66f4cfdd7b5b96ba946d1155532587e3b16b6facef7f7ee4` |
| modify | ActionDefinition | `Assets/Data/Combat/Actions/Unagi/ActioniDefinition/Unagi_Attack_04.asset` | `ba9dff7162c4a65705eaf3049252e0d55140d2a6445c66d47efa8f0ac2275af9` |
| modify | ActionDefinition | `Assets/Data/Combat/Actions/Unagi/ActioniDefinition/Unagi_Attack_05.asset` | `a8d4d355afc1bb7ea11ddc6ed207ed9560f99f5b180a2817171c5dcad0a6b8bf` |
| modify | ActionDefinition | `Assets/Data/Combat/Actions/Unagi/ActioniDefinition/Unagi_Attack_06.asset` | `c12e01a1f6d7417baa04b0d5112dfc9ce8e3c420090381f1e3d23a78c60a575a` |
| modify | ActionDefinition | `Assets/Data/Combat/Actions/Unagi/ActioniDefinition/Unagi_Attack_Branch_01.asset` | `a22a4457da31a33c86fc535788e00bdbd85ea83b32efb2184bc3eeda1fc2c9da` |
| modify | ActionDefinition | `Assets/Data/Combat/Actions/Unagi/ActioniDefinition/Unagi_Attack_Branch_01_Perfect.asset` | `2f1a05d011b89c7fe6e4e76771473ed1059cc27af0a8f0be813ae8d8308e659f` |
| modify | ActionDefinition | `Assets/Data/Combat/Actions/Unagi/ActioniDefinition/Unagi_Attack_Branch_02.asset` | `b72b33d6af8b2a69fab61112ac355d5e7d1ceb2a2c13fd0bd70c0c20d175f1ec` |
| modify | ActionDefinition | `Assets/Data/Combat/Actions/Unagi/ActioniDefinition/Unagi_Attack_Branch_02_Perfect.asset` | `bd96fba11f902620dff677098094af5da91d13c68a71408be14e835a20333eec` |
| modify | ActionDefinition | `Assets/Data/Combat/Actions/Unagi/ActioniDefinition/Unagi_Attack_Branch_03.asset` | `bf20b1a367583c6b934cdea0e52b872a6d7dc6b3f64845b546f574edfdb8c85d` |
| modify | ActionDefinition | `Assets/Data/Combat/Actions/Unagi/ActioniDefinition/Unagi_Attack_Branch_Start.asset` | `c43f278b18cf6b0eaf9c72afa7d84704c9606f276fff84c41f468b3a776e273d` |
| modify | ActionDefinition | `Assets/Data/Combat/Actions/Unagi/ActioniDefinition/Unagi_Attack_Counter.asset` | `e099702edc4be9f595e5c369d183f06114a5c3ae60405a870cf5462d44e9c910` |
| modify | ActionDefinition | `Assets/Data/Combat/Actions/Unagi/ActioniDefinition/Unagi_Attack_Rush.asset` | `9a904dd0103e38fc4e4b3e9805b368658a88db063d551c4ea624d79c4ce182f3` |
| modify | ActionDefinition | `Assets/Data/Combat/Actions/Unagi/ActioniDefinition/Unagi_Attack_Rush_02.asset` | `6d80c9b6473e3f1f7721a8c5ccea229fd9af536433ddcf9aa6cb223b24858521` |
| modify | ActionDefinition | `Assets/Data/Combat/Actions/Unagi/ActioniDefinition/Unagi_ChargeAttack_01.asset` | `f55c232365d03c5d4c4a145a5b571eb67ceb0a406d7b5db64e68a039d7d1a9b0` |
| modify | ActionDefinition | `Assets/Data/Combat/Actions/Unagi/ActioniDefinition/Unagi_ChargeAttack_03.asset` | `3a667f1a2392d57e1cd3f7f9fafcf82ee8c18b94b93f3580e0f2e28407105fe3` |
| modify | ActionDefinition | `Assets/Data/Combat/Actions/Unagi/ActioniDefinition/Unagi_Combo_1.asset` | `a9e307a411192c190d268429eab1aa3fb16b8e1b414b59a731836da4c8222e8a` |
| modify | ActionDefinition | `Assets/Data/Combat/Actions/Unagi/ActioniDefinition/Unagi_Combo_2.asset` | `ba7febe9320093174344345e816476b7a8ab8865dcc9b922b2700350243dbf3d` |
| modify | ActionDefinition | `Assets/Data/Combat/Actions/Unagi/ActioniDefinition/Unagi_Death.asset` | `618eb4377bc632e0c9fcda0ec095ee4a87e691467e6c9e84bf773581a5c77447` |
| modify | ActionDefinition | `Assets/Data/Combat/Actions/Unagi/ActioniDefinition/Unagi_Dodge_Backward.asset` | `e284dd416283010bd4b3016bf0e31e1760b7ed07daccb22c8b9c8c58ad17768f` |
| modify | ActionDefinition | `Assets/Data/Combat/Actions/Unagi/ActioniDefinition/Unagi_Dodge_Backward_Left.asset` | `2586bc5c8f0f67c13eded9a61b800a565237637508d8cb8a57bf3f2e6c0f7fb9` |
| modify | ActionDefinition | `Assets/Data/Combat/Actions/Unagi/ActioniDefinition/Unagi_Dodge_Backward_Right.asset` | `bba94ff7a1dfedd00078cce9bedf1f369f095c0cd724f54dfa5a1155949c8a73` |
| modify | ActionDefinition | `Assets/Data/Combat/Actions/Unagi/ActioniDefinition/Unagi_Dodge_Forward.asset` | `a2d33758b7f5d78e98e64f9061d6d4d04697ae0e85b7eb3c58e7171946c34dd3` |
| modify | ActionDefinition | `Assets/Data/Combat/Actions/Unagi/ActioniDefinition/Unagi_Dodge_Forward_Left.asset` | `e0c5d81039051b31d5d27af434e17db439cece15a0c3f0cda304e8223cd2bd8f` |
| modify | ActionDefinition | `Assets/Data/Combat/Actions/Unagi/ActioniDefinition/Unagi_Dodge_Forward_Right.asset` | `4aec01e27108949d21ff54147c819ccd4c9839dc1fab7800a000c4f3a6b97e6b` |
| modify | ActionDefinition | `Assets/Data/Combat/Actions/Unagi/ActioniDefinition/Unagi_EX.asset` | `db5d960da2d21964e190b04c2cfb8697f0b9abe042a402fababf8dd7dbc12733` |
| modify | ActionDefinition | `Assets/Data/Combat/Actions/Unagi/ActioniDefinition/Unagi_Hurt.asset` | `8191e9e25d2276dbffe9e1ab12138f5c2334add3ce3e12fde3b69530a12e126a` |
| modify | ActionDefinition | `Assets/Data/Combat/Actions/Unagi/ActioniDefinition/Unagi_ParryAid_H_Guard.asset` | `71408be393ef1be621e2b22ddf77e176afa9787268c59ed7b563b4e4d748e2d5` |
| modify | ActionDefinition | `Assets/Data/Combat/Actions/Unagi/ActioniDefinition/Unagi_ParryAid_H_Success.asset` | `2663663780e28ff4e597ae58995550ce62740da57ec66fd0f8bf9db567687476` |
| modify | ActionDefinition | `Assets/Data/Combat/Actions/Unagi/ActioniDefinition/Unagi_SwitchIn.asset` | `f9d437c4a08969944782b61648e80c5bc7fe465889be6415afd423fa701e01a8` |
| modify | ActionDefinition | `Assets/Data/Combat/Actions/Unagi/ActioniDefinition/Unagi_SwitchOut.asset` | `2783dee7021b94f3422abe23fc6b80111344858d7d5a987a4d13c96ba3669300` |
| delete | CharacterAnimationProfile | `Assets/Data/Combat/Actions/Unagi/Unagi_AnimationSO.asset` | `1303a7ddd143a1ce235df572194daaf4b92f290f524b93b708c82540ad4d45f0` |
| modify | CharacterLocomotionProfile | `Assets/Data/Combat/Actions/Unagi/Unagi_LocomotionSO.asset` | `f6ccee61ccffdc092ece641489e1573b634d4c704023742a13f2ae7550dba4b8` |
| read-only | ActionGraph | `Assets/Data/Combat/Actions/Unagi/UnagiActionGraph.asset` | `7b859f7eb3157e7e5f2b7e77e1c2562f496fb3276953eb400e2f9563e4396328` |
| delete | CombatModeProfile | `Assets/Data/Combat/Actions/Unagi/UnagiCombatModeSO.asset` | `8bc3ac7af962af5ffc2be40efbd2144b56f48b9a93af311684f0ec7f10eec854` |
| modify | CharacterConfig | `Assets/Data/Combat/Actions/Unagi/UnagiConfig.asset` | `4738edbb8a6c7afc80c5ade59b3f0af1b16bc9b0a515b55a1226c5885fd9a3ae` |
| modify | CharacterConfig | `Assets/Data/Combat/Actions/Unagi/UnagiEnemyConfig.asset` | `5edf38b99215eafa4a6fa5399d2947907e1fc486638a22e4b598732ffc495711` |
| read-only | ActionGraph | `Assets/Data/Enemy/Monster/MonsterActionGraph.asset` | `609222c5b422fd131ee45c33c8ac3624158d481b44f621edaa6f2803a931f3da` |
| delete | CharacterAnimationProfile | `Assets/Data/Enemy/Monster/MonsterAnimationSO.asset` | `0e5b7a6cb7725239732ec4256eeeff8991b546b18673a33068b466b971883488` |
| delete | CombatModeProfile | `Assets/Data/Enemy/Monster/MonsterCombatModeProfile.asset` | `26b6eda2b1d7ad3b4b44143a4d432bfec203138b565b8d9b650883762bea6008` |
| modify | CharacterLocomotionProfile | `Assets/Data/Enemy/Monster/MonsterLocomotion.asset` | `3bb58d4a5bf36b123554de3853af41ebadd2524c780f164136ad122b887c671a` |
| delete | CharacterAnimationProfile | `Assets/Data/Enemy/Unagi/Unagi_Enemy_AnimationSO.asset` | `d4501da283217ed99c9455af133ad67c01e49143d972c62a633d7db93191a1db` |
| delete | CombatModeProfile | `Assets/Data/Enemy/Unagi/Unagi_Enemy_CombatModeSO.asset` | `3e5e56e63b3acd416074e5a1010cd9e5dbf2647ed7e99c5ef85ecef519364a90` |
| read-only | ActionGraph | `Assets/Data/Enemy/Unagi/Unagi_Enemy_ComboGraph.asset` | `03e99c90614289c2223129f93d4524f69d5ff57e464060b37e909456d6db0637` |
| modify | CharacterLocomotionProfile | `Assets/Data/Enemy/Unagi/Unagi_Enemy_LocomotionSO.asset` | `e8b815c6419ba028306390bb45fb92da70c348b87796fa06ef2db6d1a55be06c` |
