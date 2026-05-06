<div align="center">
  <img src="https://pan.samyyc.dev/s/VYmMXE" />
  <h2><strong>MoreFixes</strong></h2>
  <h3>SwiftlyS2 fixes intended to replace selected CS2Fixes features</h3>
</div>

<p align="center">
  <img src="https://img.shields.io/badge/build-passing-brightgreen" alt="Build Status">
  <img src="https://img.shields.io/github/downloads/2oaJ/SwiftlyS2-MoreFixes/total" alt="Downloads">
  <img src="https://img.shields.io/github/stars/2oaJ/SwiftlyS2-MoreFixes?style=flat&logo=github" alt="Stars">
  <img src="https://img.shields.io/github/license/2oaJ/SwiftlyS2-MoreFixes" alt="License">
</p>

## Languages

- [中文文档](README.zh-CN.md)

## Features

- Implements selected [CS2Fixes Custom Mapping Features](https://github.com/Source2ZE/CS2Fixes/wiki/Custom-Mapping-Features) for SwiftlyS2.
- GameData patches support ConVar-controlled apply and revert.

### Feature Matrix

| Category | Feature | Status | Notes |
|---------|---------|:----:|------|
| **GameData Patches** | ServerMovementUnlock | ✅ | Supports ConVar-controlled apply and revert |
| | FixWaterFloorJump | ✅ | Supports ConVar-controlled apply and revert |
| **Push Fix** | TriggerPushFix | ✅ |  |
| **trigger_gravity Fix** | Precache Hook | ✅ |  |
| | GravityTouch Hook | ✅ |  |
| | EndTouch Hook | ✅ |  |
| **game_player_equip** | Strip First Fix | ✅ |  |
| | TriggerForActivatedPlayer Fix | ✅ |  |
| | TriggerForAllPlayer Fix | ✅ |  |
| | Only Strip Same Weapon Type Fix | ✅ |  |
| **KeyValue Input** | IgniteLifetime Input | ❌ |  |
| | AddScore | ❌ | Not planned |
| | SetMessage | ❌ | Not planned |
| | SetModel | ❌ | Not planned |
| **Entity Implementation** | game_ui | ✅ | Needs testing |
| | TeleportBorkenFix | ✅ | Clears non-Yaw player Teleport angles after AG2 |
| | point_viewcontrol | ❌ |  |
| **Filtering** | Steam ID Filtering | ❌ |  |
| **Network Message Fix** | BlockParticleMsgsFix | ✅ | Blocks `CUserMsg_ParticleManager` to mitigate client lag/crashes; experimental |
| **Physics Sim Fix** | ShufflePlayerPhysicsSimFix | ✅ | Shuffles active physics touching links to reduce player collision ordering bias |
| **subtick service** | subtick movement disable | ✅ | Needs testing |
| | subtick shooting disable | ✅ | Needs testing |

For KeyValue fixes, use the SwiftlyS2-specific [CS2-CustomIO-For-SW2](https://github.com/himenekocn/CS2-CustomIO-For-SW2).

## ConVars

| ConVar | Description | Default | Permission |
|--------|-------------|---------|------------|
| `sw_patch_server_movement_unlock_enable` | Enables the `ServerMovementUnlock` GameData patch. Disabling reverts the applied patch. | `true` | SERVER_CAN_EXECUTE |
| `sw_patch_fix_water_floor_jump_enable` | Enables the `FixWaterFloorJump` GameData patch. Disabling reverts the applied patch. | `true` | SERVER_CAN_EXECUTE |
| `cs2f_use_old_push` | Uses the CS:GO-style old push behavior. | `false` | SERVER_CAN_EXECUTE |
| `sw_gameuifix_enable` | Enables the `game_ui` proxy entity fix. | `false` | SERVER_CAN_EXECUTE |
| `sw_block_particle_msgs_enable` | Blocks `CUserMsg_ParticleManager` messages to mitigate client lag/crashes. Experimental. | `false` | SERVER_CAN_EXECUTE |
| `sw_teleport_borken_fix_enable` | Enables the player Teleport non-Yaw angle cleanup fix. | `false` | SERVER_CAN_EXECUTE |
| `sw_shuffle_player_physics_sim` | Enables physics touching list shuffle for player collision ordering. | `false` | SERVER_CAN_EXECUTE |
| `sw_disable_subtick_movement` | Disables subtick movement. | `false` | SERVER_CAN_EXECUTE |
| `sw_disable_subtick_shooting` | Disables subtick shooting. | `false` | SERVER_CAN_EXECUTE |

## Requirements

- [SwiftlyS2](https://github.com/swiftly-solution/swiftlys2) `v1.3.3-beta.15` or newer

## Installation

1. Download the plugin from the latest release.
2. Extract the folder into `addons/swiftly/plugins/`.
3. The final path should be `addons/swiftly/plugins/ZombiEden.CS2.SwiftlyS2.Fixes/`.
4. Start the server.

## Stability Notes

Tested on servers with more than 40 players.

### Tested Maps

- workshopid:3473359782 (`mg_kirbys_brawl`)
- workshopid:3469210194 (`mg_16_battles`)

## Credits

- [CS2Fixes](https://github.com/Source2ZE/CS2Fixes) for implementation references and the [Custom Mapping Features](https://github.com/Source2ZE/CS2Fixes/wiki/Custom-Mapping-Features) design.
- [SwiftlyS2](https://github.com/swiftly-solution/swiftlys2) for the plugin framework and tooling.

## Authors

- **ZombiEden Team**
- **DEEP4R**
- Website: https://zombieden.cn
