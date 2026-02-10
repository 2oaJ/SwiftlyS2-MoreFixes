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


## 🎯 功能特性

### 1. **TriggerPushFix**
- 添加csgo push机制
- 支持通过 `cs2f_use_old_push` ConVar 控制是否启用

### 2. **Strip Fix**
- 添加对地图移除玩家武器的支持

### 3. **TriggerForActivatedPlayer parameter fix** 
- 添加 `CGamePlayerEquip::InputTriggerForAllPlayers`
- 添加 `CGamePlayerEquip::InputTriggerForActivatedPlayer`
- 依赖并复用 `IStripFixService` 进行武器移除

### 4. **More Patch**
- `ServerMovementUnlock` GameDataPatch
- `FixWaterFloorJump` GameDataPatch

## ⚙️ ConVars

| ConVar | 描述 | 默认值 | 权限 |
|--------|------|--------|------|
| `cs2f_use_old_push` | 是否使用 CSGO 风格的旧推动机制 | `false` | SERVER_CAN_EXECUTE |

## 🛡️ 要求

- [SwiftlyS2](https://github.com/swiftly-solution/swiftlys2) (不低于v1.1.5-beta49)

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

## 🙏 致谢

感谢以下项目的启发和参考：
- [CS2Fixes](https://github.com/Source2ZE/CS2Fixes) - 参考了其代码实现和 [Custom Mapping Features](https://github.com/Source2ZE/CS2Fixes/wiki/Custom-Mapping-Features) 的功能设计
- [SwiftlyS2](https://github.com/swiftly-solution/swiftlys2) - 插件框架和开发工具

## 👥 作者

- **ZombiEden Team**
- **DEEP4R**
- 网站：https://zombieden.cn
