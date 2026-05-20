<div align="center">
  <img src="https://pan.samyyc.dev/s/VYmMXE" />
  <h2><strong>MoreFixes</strong></h2>
  <h3>用于替代CS2Fixes提供的修复功能</h3>
</div>

<p align="center">
  <img src="https://img.shields.io/badge/build-passing-brightgreen" alt="Build Status">
  <img src="https://img.shields.io/github/downloads/2oaJ/SwiftlyS2-MoreFixes/total" alt="Downloads">
  <img src="https://img.shields.io/github/stars/2oaJ/SwiftlyS2-MoreFixes?style=flat&logo=github" alt="Stars">
  <img src="https://img.shields.io/github/license/2oaJ/SwiftlyS2-MoreFixes" alt="License">
</p>

## 🌐 语言

- [English](README.md)

## 🎯 功能特性

- 实现了 [CS2Fixes Custom Mapping Features](https://github.com/Source2ZE/CS2Fixes/wiki/Custom-Mapping-Features) 的部分功能。
- GameData Patch 支持通过 ConVar 动态应用与撤销。

### 📊 功能对比表

| 功能分类                        | 功能名称                        | 状态 | 说明                                                            |
| ------------------------------- | ------------------------------- | :--: | --------------------------------------------------------------- |
| **GameData Patchs**       | ServerMovementUnlock            |  ✅  |                                                                 |
|                                 | FixWaterFloorJump               |  ✅  |                                                                 |
| **Push Fix**              | TriggerPushFix                  |  ✅  |                                                                 |
| **trigger_gravity Fix**   | Precache Hook                   |  ✅  |                                                                 |
|                                 | GravityTouch Hook               |  ✅  |                                                                 |
|                                 | EndTouch Hook                   |  ✅  |                                                                 |
| **game_player_equip**     | Strip First Fix                 |  ✅  |                                                                 |
|                                 | TriggerForActivatedPlayer Fix   |  ✅  |                                                                 |
|                                 | TriggerForAllPlayer Fix         |  ✅  |                                                                 |
|                                 | Only Strip Same Weapon Type Fix |  ✅  |                                                                 |
| **KeyValue Input**        | IgniteLifetime Input            |  ❌  |                                                                 |
|                                 | AddScore                        |  ❌  | 不考虑移植                                                      |
|                                 | SetMessage                      |  ❌  | 不考虑移植                                                      |
|                                 | SetModel                        |  ❌  | 不考虑移植                                                      |
| **Entity Implementation** | game_ui                         |  ✅  | 需要测试                                                        |
|                                 | TeleportBorkenFix               |  ✅  | 修复 AG2 后玩家被 Teleport 后模型异常和视角显示异常的问题       |
|                                 | point_viewcontrol               |  ✅  |                                                                 |
| **Filtering**             | Steam ID Filtering              |  ❌  |                                                                 |
| **Network Message Fix**   | BlockParticleMsgsFix            |  ✅  | 拦截 `CUserMsg_ParticleManager` 以缓解客户端卡顿/闪退，实验性 |
| **Physics Sim Fix**       | ShufflePlayerPhysicsSimFix      |  ✅  | 模拟 CSGO 风格的随机实体碰撞                                    |
| **subtick service**       | subtick movement disable        |  ✅  | 需要测试                                                        |
|                                 | subtick shooting disable        |  ✅  | 需要测试                                                        |

- KeyValueFix请使用专为SwiftlyS2移植的[CS2-CustomIO-For-SW2](https://github.com/himenekocn/CS2-CustomIO-For-SW2)

## ⚙️ ConVars

| ConVar                                     | 描述                                                                    | 默认值    | 权限               |
| ------------------------------------------ | ----------------------------------------------------------------------- | --------- | ------------------ |
| `sw_patch_server_movement_unlock_enable` | 启用 `ServerMovementUnlock` GameData 补丁（关闭会撤销已应用的补丁）   | `true`  | SERVER_CAN_EXECUTE |
| `sw_patch_fix_water_floor_jump_enable`   | 启用 `FixWaterFloorJump` GameData 补丁（关闭会撤销已应用的补丁）      | `true`  | SERVER_CAN_EXECUTE |
| `cs2f_use_old_push`                      | 是否使用 CSGO 风格的旧推动机制                                          | `false` | SERVER_CAN_EXECUTE |
| `sw_gameuifix_enable`                    | 启用 `game_ui` 代理实体修复                                           | `false` | SERVER_CAN_EXECUTE |
| `sw_pointviewcontrolfix_enable`          | 启用 `point_viewcontrol` 代理实体修复                                 | `false` | SERVER_CAN_EXECUTE |
| `sw_block_particle_msgs_enable`          | 拦截 `CUserMsg_ParticleManager` 消息以缓解客户端卡顿/闪退，实验性功能 | `false` | SERVER_CAN_EXECUTE |
| `sw_teleport_borken_fix_enable`          | 启用玩家 Teleport 角度修复                                             | `false` | SERVER_CAN_EXECUTE |
| `sw_shuffle_player_physics_sim`          | 启用模拟 CSGO 风格的随机实体碰撞                                        | `false` | SERVER_CAN_EXECUTE |
| `sw_disable_subtick_movement`            | 禁用Subtick移动                                                         | `false` | SERVER_CAN_EXECUTE |
| `sw_disable_subtick_shooting`            | 禁用Subtick射击                                                         | `false` | SERVER_CAN_EXECUTE |

## 🛡️ 要求

- [SwiftlyS2](https://github.com/swiftly-solution/swiftlys2) (不低于 `v1.3.3-beta.15`)

## 🔧 安装

1. 从最新发行版下载插件
2. 提取文件夹至 `addons/swiftly/plugins/`
3. 文件夹结构应为：`addons/swiftly/plugins/ZombiEden.CS2.SwiftlyS2.Fixes/`
4. 启动服务器

## ✅ 稳定性验证

在 **40+ 人的服务器** 上进行了充分测试，

### 测试地图：

- workshopid:3473359782(mg_kirbys_brawl)
- workshopid:3469210194(mg_16_battles)
- workshopid:3293887607(mg_dobaba_t)
- workshopid:3327928867(mg_fiery_t)

## 🙏 致谢

感谢以下项目的启发和参考：

- [CS2Fixes](https://github.com/Source2ZE/CS2Fixes) - 参考了其代码实现和 [Custom Mapping Features](https://github.com/Source2ZE/CS2Fixes/wiki/Custom-Mapping-Features) 的功能设计
- [SwiftlyS2](https://github.com/swiftly-solution/swiftlys2) - 插件框架和开发工具

## 👥 作者

- **ZombiEden Team**
- **DEEP4R**
- 网站：https://zombieden.cn
