using Microsoft.Extensions.Logging;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Convars;
using SwiftlyS2.Shared.Events;
using SwiftlyS2.Shared.Memory;
using SwiftlyS2.Shared.Natives;
using SwiftlyS2.Shared.SchemaDefinitions;
using ZombiEden.CS2.SwiftlyS2.Fixes.Interface;

namespace ZombiEden.CS2.SwiftlyS2.Fixes.Impl
{
    /// <summary>
    /// 修复未父级化的 beam/laser 实体在生成时进入无限循环的问题。
    /// 参考来源: CS2Fixes commit 0686a807 "Fix unparented beams/lasers hitting infinite loops on spawn" (Vauff, 2026-07-26)
    /// 提交地址: https://github.com/Source2ZE/CS2Fixes/commit/0686a807d790ef22407aa83e33b40bbed6531b51
    /// 移植要点: detour SetBeamOrigin / SetBeamEndPos，未父级化时用等价逻辑绕过游戏代码的无限循环路径
    /// </summary>
    public unsafe sealed class BeamFixService(
        ISwiftlyCore core,
        ILogger<BeamFixService> logger) : IBeamFixService
    {
        private delegate void SetBeamOriginDelegate(nint beam, Vector* position);
        private delegate void SetBeamEndPosDelegate(nint beam, Vector* position);

        private const string EnableConVarName = "sw_beam_fix_enable";

        public string ServiceName => "BeamFix";

        private IConVar<bool>? _enableConVar;
        private bool _enabled;
        private bool _installed;

        private IUnmanagedFunction<SetBeamOriginDelegate>? _setBeamOriginHook;
        private IUnmanagedFunction<SetBeamEndPosDelegate>? _setBeamEndPosHook;
        private Guid? _setBeamOriginHookId;
        private Guid? _setBeamEndPosHookId;

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
                    "启用未父级化 beam/laser 无限循环修复",
                    true,
                    ConvarFlags.SERVER_CAN_EXECUTE);

                _enabled = _enableConVar.Value;
                core.Event.OnConVarValueChanged += OnConVarValueChanged;

                AttachHooks();

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
            try
            {
                core.Event.OnConVarValueChanged -= OnConVarValueChanged;
                DetachHooks();

                if (!_installed)
                {
                    return;
                }

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

            if (!bool.TryParse(@event.NewValue, out var newValue))
            {
                logger.LogWarning("{ServiceName} 收到无法解析的 ConVar 值: {Value}", ServiceName, @event.NewValue);
                return;
            }

            if (_enabled == newValue)
            {
                return;
            }

            _enabled = newValue;
            logger.LogInformation("{ServiceName} 开关切换为 {Enabled}", ServiceName, _enabled);
        }

        private void AttachHooks()
        {
            AttachSetBeamOriginHook();
            AttachSetBeamEndPosHook();
        }

        private void DetachHooks()
        {
            if (_setBeamOriginHookId.HasValue && _setBeamOriginHook is not null)
            {
                _setBeamOriginHook.RemoveHook(_setBeamOriginHookId.Value);
                _setBeamOriginHookId = null;
                _setBeamOriginHook = null;
            }

            if (_setBeamEndPosHookId.HasValue && _setBeamEndPosHook is not null)
            {
                _setBeamEndPosHook.RemoveHook(_setBeamEndPosHookId.Value);
                _setBeamEndPosHookId = null;
                _setBeamEndPosHook = null;
            }
        }

        private void AttachSetBeamOriginHook()
        {
            var function = core.Memory.GetUnmanagedFunctionByAddress<SetBeamOriginDelegate>(
                core.GameData.GetSignature("SetBeamOrigin"));

            if (function is null)
            {
                throw new InvalidOperationException("无法为 SetBeamOrigin 创建 unmanaged function。");
            }

            _setBeamOriginHook = function;
            _setBeamOriginHookId = function.AddHook(next =>
            {
                return (beam, position) =>
                {
                    // 未启用或参数无效时直接放行
                    if (!_enabled || beam == 0 || position == null)
                    {
                        next()(beam, position);
                        return;
                    }

                    var cBeam = core.Memory.ToSchemaClass<CBeam>(beam);
                    if (cBeam is null || !cBeam.IsValid)
                    {
                        next()(beam, position);
                        return;
                    }

                    // 有父级的 beam/laser，游戏代码路径正常，走原函数
                    if (cBeam.CBodyComponent?.SceneNode?.Parent is not null)
                    {
                        next()(beam, position);
                        return;
                    }

                    // 无父级时游戏代码会进入无限循环，用等价逻辑自己实现起点设置
                    cBeam.Teleport(new Vector(position->X, position->Y, position->Z), null, null);
                };
            });

            logger.LogInformation("{ServiceName} SetBeamOrigin hook 已安装。", ServiceName);
        }

        private void AttachSetBeamEndPosHook()
        {
            var function = core.Memory.GetUnmanagedFunctionByAddress<SetBeamEndPosDelegate>(
                core.GameData.GetSignature("SetBeamEndPos"));

            if (function is null)
            {
                throw new InvalidOperationException("无法为 SetBeamEndPos 创建 unmanaged function。");
            }

            _setBeamEndPosHook = function;
            _setBeamEndPosHookId = function.AddHook(next =>
            {
                return (beam, position) =>
                {
                    // 未启用或参数无效时直接放行
                    if (!_enabled || beam == 0 || position == null)
                    {
                        next()(beam, position);
                        return;
                    }

                    var cBeam = core.Memory.ToSchemaClass<CBeam>(beam);
                    if (cBeam is null || !cBeam.IsValid)
                    {
                        next()(beam, position);
                        return;
                    }

                    // 有父级的 beam/laser，游戏代码路径正常，走原函数
                    if (cBeam.CBodyComponent?.SceneNode?.Parent is not null)
                    {
                        next()(beam, position);
                        return;
                    }

                    // 无父级时直接写 m_vecEndPos schema 字段并通知网络同步，绕过无限循环
                    cBeam.EndPos = new Vector(position->X, position->Y, position->Z);
                    cBeam.EndPosUpdated();
                };
            });

            logger.LogInformation("{ServiceName} SetBeamEndPos hook 已安装。", ServiceName);
        }
    }
}
