using Microsoft.Extensions.Logging;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Convars;
using SwiftlyS2.Shared.Events;
using SwiftlyS2.Shared.Memory;
using SwiftlyS2.Shared.Natives;
using System.Runtime.InteropServices;
using ZombiEden.CS2.SwiftlyS2.Fixes.Interface;

namespace ZombiEden.CS2.SwiftlyS2.Fixes.Impl
{
    /// <summary>
    /// 修复 AG2 后玩家被 Teleport 后模型异常和视角显示异常的问题。
    /// </summary>
    public unsafe sealed class TeleportBorkenFixService(
        ISwiftlyCore core,
        ILogger<TeleportBorkenFixService> logger) : ITeleportBorkenFixService
    {
        private delegate void CCSPlayerPawn_TeleportDelegate(nint pawn, Vector* position, QAngle* angles, Vector* velocity);
        private delegate void CBasePlayerPawn__SnapViewAngles_t(nint pPlayerPawn, QAngle* ang);

        private const string EnableConVarName = "sw_teleport_borken_fix_enable";

        public string ServiceName => "TeleportBorkenFix";

        private IConVar<bool>? _enableConVar;
        private Guid? _hookId;
        private IUnmanagedFunction<CCSPlayerPawn_TeleportDelegate>? _hook;
        private CBasePlayerPawn__SnapViewAngles_t? _fnCBasePlayerPawn__SnapViewAngles;
        private bool _enabled;
        private bool _installed;

        public void Install()
        {
            try
            {
                if (_installed)
                {
                    logger.LogWarning("{ServiceName} 已安装，跳过重复安装。", ServiceName);
                    return;
                }

                _enableConVar = core.ConVar.CreateOrFind(
                    EnableConVarName,
                    "启用玩家 Teleport 角度修复",
                    true,
                    ConvarFlags.SERVER_CAN_EXECUTE);

                _enabled = _enableConVar.Value;
                core.Event.OnConVarValueChanged += OnConVarValueChanged;
                AttachHook();

                _fnCBasePlayerPawn__SnapViewAngles = Marshal.GetDelegateForFunctionPointer<CBasePlayerPawn__SnapViewAngles_t>(core.GameData.GetSignature("CBasePlayerPawn::SnapViewAngles"));

                _installed = true;
                logger.LogInformation("{ServiceName} 安装完成，当前启用状态: {Enabled}", ServiceName, _enabled);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "安装 {ServiceName} 失败。", ServiceName);
                throw;
            }
        }

        public void Uninstall()
        {
            if (!_installed)
            {
                return;
            }

            try
            {
                core.Event.OnConVarValueChanged -= OnConVarValueChanged;
                DetachHook();
                _installed = false;
                logger.LogInformation("{ServiceName} 已卸载。", ServiceName);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "卸载 {ServiceName} 失败。", ServiceName);
            }
        }

        private void OnConVarValueChanged(IOnConVarValueChanged @event)
        {
            if (_enableConVar is null || @event.ConVarName != _enableConVar.Name)
            {
                return;
            }

            bool newValue;
            try
            {
                newValue = bool.Parse(@event.NewValue);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "{ServiceName} 收到无法解析的 ConVar 值: {Value}", ServiceName, @event.NewValue);
                return;
            }

            if (_enabled == newValue)
            {
                return;
            }

            _enabled = newValue;
            logger.LogInformation("{ServiceName} 开关切换为 {Enabled}", ServiceName, _enabled);
        }

        private void AttachHook()
        {
            if (_hookId.HasValue)
            {
                return;
            }

            try
            {
                var vtable = core.Memory.GetVTableAddress("server", "CCSPlayerPawn");
                if (!vtable.HasValue)
                {
                    throw new InvalidOperationException("无法找到 CCSPlayerPawn vtable。");
                }

                var offset = core.GameData.GetOffset("CCSPlayerPawn::Teleport");
                if (offset == -1)
                {
                    throw new InvalidOperationException("无法找到 Teleport offset。");
                }

                _hook = core.Memory.GetUnmanagedFunctionByVTable<CCSPlayerPawn_TeleportDelegate>(vtable.Value, offset);
                if (_hook is null)
                {
                    throw new InvalidOperationException("无法创建 CCSPlayerPawn::Teleport hook。");
                }

                _hookId = _hook.AddHook(original =>
                {
                    return (pawn, position, angles, velocity) =>
                    {
                        var trampoline = original();

                        if (!_enabled)
                        {
                            trampoline(pawn, position, angles, velocity);
                            return;
                        }

                        if (angles != null)
                        {
                            _fnCBasePlayerPawn__SnapViewAngles!(pawn, angles);
                            angles = null;
                        }

                        trampoline(pawn, position, angles, velocity);
                    };
                });

                logger.LogInformation("{ServiceName} hook 已安装。", ServiceName);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "安装 {ServiceName} hook 失败。", ServiceName);
                throw;
            }
        }

        private void DetachHook()
        {
            if (!_hookId.HasValue || _hook is null)
            {
                return;
            }

            try
            {
                _hook.RemoveHook(_hookId.Value);
                _hookId = null;
                _hook = null;
                logger.LogInformation("{ServiceName} hook 已卸载。", ServiceName);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "卸载 {ServiceName} hook 失败。", ServiceName);
            }
        }
    }
}
