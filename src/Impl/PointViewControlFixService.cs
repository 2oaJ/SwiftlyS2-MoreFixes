using Microsoft.Extensions.Logging;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Convars;
using SwiftlyS2.Shared.Events;
using SwiftlyS2.Shared.GameEventDefinitions;
using SwiftlyS2.Shared.GameHooks;
using SwiftlyS2.Shared.Misc;
using ZombiEden.CS2.SwiftlyS2.Fixes.Impl.CustomEntity;
using ZombiEden.CS2.SwiftlyS2.Fixes.Interface;

namespace ZombiEden.CS2.SwiftlyS2.Fixes.Impl;

public class PointViewControlFixService : IPointViewControlFixService
{
    private const string EnableConVarName = "sw_pointviewcontrolfix_enable";

    public string ServiceName => "PointViewControlFix";

    private readonly ISwiftlyCore _core;
    private readonly ILogger _logger;

    private IConVar<bool>? _enableConVar;
    private bool _enabled;
    private Guid _eventRoundPrestart;

    public PointViewControlFixService(ISwiftlyCore core, ILogger<PointViewControlFixService> logger)
    {
        _core = core;
        _logger = logger;

        CPointViewControlHandler.Setup(core, logger);
    }

    public void Install()
    {
        try
        {
            _enableConVar = _core.ConVar.CreateOrFind(EnableConVarName, "启用 point_viewcontrol 修复", true, ConvarFlags.SERVER_CAN_EXECUTE);
            _enabled = _enableConVar.Value;

            _core.Event.OnConVarValueChanged += OnConVarValueChanged;
            _core.Event.OnEntitySpawned += OnEntitySpawned;
            _core.Event.OnWorldUpdate += OnWorldUpdate;

            _core.GameHooks.Entities.AcceptInput.Pre += OnEntityIdentityAcceptInput;

            _eventRoundPrestart = _core.GameEvent.HookPost<EventRoundPrestart>(OnRoundPrestartPost);

            _logger.LogInformation("{ServiceName} 安装完成，当前启用状态: {Enabled}", ServiceName, _enabled);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "安装 {ServiceName} 失败。", ServiceName);
            throw;
        }
    }

    public void Uninstall()
    {
        try
        {
            _core.Event.OnConVarValueChanged -= OnConVarValueChanged;
            _core.Event.OnEntitySpawned -= OnEntitySpawned;
            _core.Event.OnWorldUpdate -= OnWorldUpdate;

            _core.GameHooks.Entities.AcceptInput.Pre -= OnEntityIdentityAcceptInput;

            _core.GameEvent.Unhook(_eventRoundPrestart);

            _logger.LogInformation("{ServiceName} 已卸载。", ServiceName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "卸载 {ServiceName} 失败。", ServiceName);
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
            _logger.LogWarning(ex, "{ServiceName} 收到无法解析的 ConVar 值: {Value}", ServiceName, @event.NewValue);
            return;
        }

        if (_enabled == newValue)
        {
            return;
        }

        _enabled = newValue;
        _logger.LogInformation("{ServiceName} 开关切换为 {Enabled}", ServiceName, _enabled);
    }

    private void OnEntitySpawned(IOnEntitySpawnedEvent @event)
    {
        CPointViewControlHandler.OnCreated(@event.Entity);
    }

    private void OnEntityIdentityAcceptInput(ref AcceptInputEntityPreContext ctx)
    {
        if (!_enabled)
        {
            return;
        }

        var viewControl = CustomEntityCast.AsPointViewControl(ctx.Params.EntityInstance);
        if (viewControl == null)
        {
            return;
        }

        var inputName = ctx.Params.InputName;
        if (string.Equals(inputName, "EnableCamera", StringComparison.OrdinalIgnoreCase))
        {
            if (viewControl.OnEnable(ctx.Params.Activator))
            {
                ctx.SetHookResult(HookResult.Stop);
            }
        }
        else if (string.Equals(inputName, "DisableCamera", StringComparison.OrdinalIgnoreCase))
        {
            if (viewControl.OnDisable(ctx.Params.Activator))
            {
                ctx.SetHookResult(HookResult.Stop);
            }
        }
        else if (string.Equals(inputName, "EnableCameraAll", StringComparison.OrdinalIgnoreCase))
        {
            if (viewControl.OnEnableAll())
            {
                ctx.SetHookResult(HookResult.Stop);
            }
        }
        else if (string.Equals(inputName, "DisableCameraAll", StringComparison.OrdinalIgnoreCase))
        {
            if (viewControl.OnDisableAll())
            {
                ctx.SetHookResult(HookResult.Stop);
            }
        }
    }

    private void OnWorldUpdate()
    {
        // TODO: move to ServerPreEntityThink
        CPointViewControlHandler.RunThink();
    }

    private HookResult OnRoundPrestartPost(EventRoundPrestart @event)
    {
        CPointViewControlHandler.Shutdown();

        return HookResult.Continue;
    }
}
