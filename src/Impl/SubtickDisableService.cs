using Microsoft.Extensions.Logging;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Convars;
using SwiftlyS2.Shared.Events;
using SwiftlyS2.Shared.ProtobufDefinitions;
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
        
        // 缓存ConVar值，避免频繁读取
        private bool _disableMovement = false;
        private bool _disableShooting = false;
        
        // 标记事件是否已注册
        private bool _eventHooked = false;

        public void Install()
        {
            try
            {
                if (_isInstalled)
                {
                    logger.LogWarning($"{ServiceName} is already installed");
                    return;
                }

                // 创建ConVars用于运行时控制
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

                // 注册ConVar值变化监听
                core.Event.OnConVarValueChanged += OnConVarValueChanged;

                // 根据初始值决定是否注册事件
                UpdateEventHook();

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
                // 取消注册ConVar值变化监听
                core.Event.OnConVarValueChanged -= OnConVarValueChanged;

                // 取消注册事件hook
                UnhookEvent();

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
            
            // 检查是否是我们关心的ConVar
            if (_disableMovementConVar != null && convarName == _disableMovementConVar.Name)
            {
                var newValue = bool.Parse(@event.NewValue);
                if (_disableMovement != newValue)
                {
                    _disableMovement = newValue;
                    logger.LogInformation($"{ServiceName}: Subtick movement disable changed to {newValue}");
                    UpdateEventHook();
                }
            }
            else if (_disableShootingConVar != null && convarName == _disableShootingConVar.Name)
            {
                var newValue = bool.Parse(@event.NewValue);
                if (_disableShooting != newValue)
                {
                    _disableShooting = newValue;
                    logger.LogInformation($"{ServiceName}: Subtick shooting disable changed to {newValue}");
                    UpdateEventHook();
                }
            }
        }

        /// <summary>
        /// 根据ConVar值动态注册/注销事件处理器
        /// 只要有一个功能开启就需要hook
        /// </summary>
        private void UpdateEventHook()
        {
            bool shouldHook = _disableMovement || _disableShooting;

            if (shouldHook && !_eventHooked)
            {
                // 需要hook但还未hook
                core.Event.OnClientProcessUsercmds += OnClientProcessUsercmds;
                _eventHooked = true;
                logger.LogDebug($"{ServiceName}: OnClientProcessUsercmds hook registered");
            }
            else if (!shouldHook && _eventHooked)
            {
                // 不需要hook但已经hook
                core.Event.OnClientProcessUsercmds -= OnClientProcessUsercmds;
                _eventHooked = false;
                logger.LogDebug($"{ServiceName}: OnClientProcessUsercmds hook unregistered");
            }
        }

        /// <summary>
        /// 取消注册事件hook
        /// </summary>
        private void UnhookEvent()
        {
            if (_eventHooked)
            {
                core.Event.OnClientProcessUsercmds -= OnClientProcessUsercmds;
                _eventHooked = false;
            }
        }

        /// <summary>
        /// 处理客户端Usercmds,移除Subtick输入
        /// 使用引用传递避免拷贝，提升性能
        /// </summary>
        private void OnClientProcessUsercmds(IOnClientProcessUsercmdsEvent @event)
        {
            try
            {
                // 直接使用缓存的字段值，避免频繁读取ConVar
                // 使用for循环而非foreach，以便使用索引直接访问，避免枚举器开销
                var usercmds = @event.Usercmds;
                for (int i = 0; i < usercmds.Count; i++)
                {
                    var cmd = usercmds[i]; // 直接获取引用，不创建副本
                    
                    if (_disableMovement)
                    {
                        ProcessSubtickMovementRemoval(ref cmd);
                    }

                    if (_disableShooting)
                    {
                        ProcessSubtickShootingRemoval(ref cmd);
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, $"Error in {ServiceName}.OnClientProcessUsercmds");
            }
        }

        /// <summary>
        /// 移除Subtick移动输入
        /// 对应C++代码中的subtick_moves处理
        /// 使用in参数确保按引用传递且不会重新赋值
        /// </summary>
        private static void ProcessSubtickMovementRemoval(ref CSGOUserCmdPB cmd)
        {
            if (cmd.Base?.SubtickMoves == null || cmd.Base.SubtickMoves.Count == 0)
                return;

            var moves = cmd.Base.SubtickMoves;
            
            // 预分配容量以避免List动态扩容
            var toKeep = new List<CSubtickMoveStep>(moves.Count);
            
            for (int i = 0; i < moves.Count; i++)
            {
                var move = moves.Get(i);
                // 移除移动按钮的subtick输入 (button >= IN_JUMP && button <= IN_MOVERIGHT && button != IN_USE)
                // 以及移除视角变化
                if ((move.Button >= 0x2 && move.Button <= 0x400 && move.Button != 0x20) 
                    || move.PitchDelta != 0.0f 
                    || move.YawDelta != 0.0f)
                {
                    // 跳过（即移除）
                    continue;
                }
                
                // 保留此move
                toKeep.Add(move);
            }
            
            // 只在有变化时才重建列表
            if (toKeep.Count != moves.Count)
            {
                moves.Clear();
                foreach (var move in toKeep)
                {
                    var newMove = moves.Add();
                    newMove.Button = move.Button;
                    newMove.Pressed = move.Pressed;
                    newMove.When = move.When;
                    newMove.AnalogForwardDelta = move.AnalogForwardDelta;
                    newMove.AnalogLeftDelta = move.AnalogLeftDelta;
                    newMove.PitchDelta = move.PitchDelta;
                    newMove.YawDelta = move.YawDelta;
                }
            }
        }

        /// <summary>
        /// 移除Subtick射击输入
        /// </summary>
        private static void ProcessSubtickShootingRemoval(ref CSGOUserCmdPB cmd)
        {
            // 清除攻击历史索引
            if (cmd.Attack1StartHistoryIndex != -1)
                cmd.Attack1StartHistoryIndex = -1;

            if (cmd.Attack2StartHistoryIndex != -1)
                cmd.Attack2StartHistoryIndex = -1;

            // 清除输入历史 (对应C++的mutable_input_history()->Clear())
            if (cmd.InputHistory?.Count > 0)
                cmd.InputHistory.Clear();
        }
    }
}