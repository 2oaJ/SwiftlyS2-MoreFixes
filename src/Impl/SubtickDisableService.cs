using Microsoft.Extensions.Logging;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Convars;
using SwiftlyS2.Shared.Events;
using SwiftlyS2.Shared.GameHooks;
using SwiftlyS2.Shared.ProtobufDefinitions;
using SwiftlyS2.Shared.SchemaDefinitions;
using ZombiEden.CS2.SwiftlyS2.Fixes.Interface;

namespace ZombiEden.CS2.SwiftlyS2.Fixes.Impl
{
    /// <summary>
    /// Subtick禁用服务实现
    /// 参考: https://github.com/Source2ZE/CS2Fixes/blob/efba3ef3f365a6b2cea356e9189a937b25d75832/src/detours.cpp#L562
    /// </summary>
    public class SubtickDisableService(
        ISwiftlyCore core,
        ILogger<SubtickDisableService> logger) : ISubtickDisableService
    {
        public string ServiceName => "SubtickDisable";

        private bool _isInstalled = false;
        private IConVar<bool>? _disableMovementConVar;
        private IConVar<bool>? _disableShootingConVar;

        private bool _disableMovement = false;
        private bool _disableShooting = false;
        private bool _useOldPush = false;

        public void Install()
        {
            try
            {
                if (_isInstalled)
                {
                    logger.LogWarning($"{ServiceName} is already installed");
                    return;
                }

                _disableMovementConVar = core.ConVar.CreateOrFind(
                    "sw_disable_subtick_movement",
                    "禁用Subtick移动",
                    false,
                    ConvarFlags.SERVER_CAN_EXECUTE);

                _disableShootingConVar = core.ConVar.CreateOrFind(
                    "sw_disable_subtick_shooting",
                    "禁用Subtick射击",
                    false,
                    ConvarFlags.SERVER_CAN_EXECUTE);

                // 初始化缓存值
                _disableMovement = _disableMovementConVar.Value;
                _disableShooting = _disableShootingConVar.Value;

                var cvarUseOldPush = core.ConVar.Find<bool>("cs2f_use_old_push");
                if (cvarUseOldPush != null)
                {
                    _useOldPush = cvarUseOldPush.Value;
                }

                core.Event.OnConVarValueChanged += OnConVarValueChanged;

                core.GameHooks.Controller.ProcessUsercmds.Pre += OnClientProcessUsercmds;

                _isInstalled = true;
                logger.LogInformation($"{ServiceName} installed successfully - Subtick processing configured (Movement: {_disableMovement}, Shooting: {_disableShooting})");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, $"Failed to install {ServiceName}: {ex.Message}");
                throw;
            }
        }

        public void Uninstall()
        {
            if (!_isInstalled)
            {
                return;
            }

            try
            {
                core.Event.OnConVarValueChanged -= OnConVarValueChanged;

                core.GameHooks.Controller.ProcessUsercmds.Pre -= OnClientProcessUsercmds;

                logger.LogInformation($"{ServiceName} uninstalled");
                _isInstalled = false;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, $"Failed to uninstall {ServiceName}: {ex.Message}");
            }
        }

        /// <summary>
        /// 监听ConVar值变化事件
        /// </summary>
        private void OnConVarValueChanged(IOnConVarValueChanged @event)
        {
            var convarName = @event.ConVarName;

            if (_disableMovementConVar != null && convarName == _disableMovementConVar.Name)
            {
                var newValue = bool.Parse(@event.NewValue);
                if (_disableMovement != newValue)
                {
                    _disableMovement = newValue;
                    logger.LogInformation($"{ServiceName}: Subtick movement disable changed to {newValue}");
                }
            }
            else if (_disableShootingConVar != null && convarName == _disableShootingConVar.Name)
            {
                var newValue = bool.Parse(@event.NewValue);
                if (_disableShooting != newValue)
                {
                    _disableShooting = newValue;
                    logger.LogInformation($"{ServiceName}: Subtick shooting disable changed to {newValue}");
                }
            }
            else if (convarName == "cs2f_use_old_push")
            {
                _useOldPush = bool.Parse(@event.NewValue);
            }
        }

        /// <summary>
        /// 处理客户端Usercmds,移除Subtick输入
        /// </summary>
        private void OnClientProcessUsercmds(ref ProcessUsercmdsPreContext ctx)
        {
            if (!_disableMovement && !_disableShooting && !_useOldPush)
            {
                return;
            }

            var usercmds = ctx.Params.Usercmds;
            for (int i = 0; i < usercmds.Count; i++)
            {
                var cmd = usercmds[i];

                if (_disableMovement || _useOldPush)
                {
                    ProcessSubtickMovementRemoval(cmd.CSGOUserCmd);
                }

                if (_disableShooting)
                {
                    ProcessSubtickShootingRemoval(cmd.CSGOUserCmd);
                }
            }
        }

        /// <summary>
        /// 移除Subtick移动输入
        /// 对应C++代码中的subtick_moves处理
        /// </summary>
        private static void ProcessSubtickMovementRemoval(CSGOUserCmdPB cmd)
        {
            if (cmd.Base?.SubtickMoves == null || cmd.Base.SubtickMoves.Count == 0)
                return;

            var moves = cmd.Base.SubtickMoves;
            for (int i = 0; i < moves.Count; i++)
            {
                var move = moves.Get(i);
                if (move == null)
                {
                    continue;
                }

                var button = (InputBitMask_t)move.Button;
                if (button >= InputBitMask_t.IN_DUCK && button <= InputBitMask_t.IN_MOVERIGHT && button != InputBitMask_t.IN_USE)
                {
                    move.Button = 0;
                    move.Pressed = false;
                    move.When = 0;
                    move.AnalogForwardDelta = 0f;
                    move.AnalogLeftDelta = 0f;
                    move.PitchDelta = 0f;
                    move.YawDelta = 0f;
                }
                else
                {
                    // Remove subtick movement viewangles by pitch/yaw
                    if (move.PitchDelta != 0f)
                    {
                        move.PitchDelta = 0f;
                    }

                    if (move.YawDelta != 0f)
                    {
                        move.YawDelta = 0f;
                    }
                }
            }
        }

        /// <summary>
        /// 移除Subtick射击输入
        /// </summary>
        private static void ProcessSubtickShootingRemoval(CSGOUserCmdPB cmd)
        {
            if (cmd.Attack1StartHistoryIndex != -1)
                cmd.Attack1StartHistoryIndex = -1;

            if (cmd.Attack2StartHistoryIndex != -1)
                cmd.Attack2StartHistoryIndex = -1;

            if (cmd.InputHistory?.Count > 0)
                cmd.InputHistory.Clear();
        }
    }

}
