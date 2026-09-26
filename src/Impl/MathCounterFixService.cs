using Microsoft.Extensions.Logging;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Convars;
using SwiftlyS2.Shared.Events;
using SwiftlyS2.Shared.GameHooks;
using SwiftlyS2.Shared.Misc;
using SwiftlyS2.Shared.Natives;
using SwiftlyS2.Shared.SchemaDefinitions;
using System.Runtime.InteropServices;
using ZombiEden.CS2.SwiftlyS2.Fixes.Interface;

namespace ZombiEden.CS2.SwiftlyS2.Fixes.Impl;

public class MathCounterFixService : IMathCounterFixService
{
    private const string EnableConVarName = "sw_mathcounterfix_enable";
    public string ServiceName => "MathCounterFix";

    private readonly ISwiftlyCore _core;
    private readonly ILogger _logger;

    private IConVar<bool>? _enableConVar;
    private bool _enabled;

    public MathCounterFixService(ISwiftlyCore core, ILogger<MathCounterFixService> logger)
    {
        _core = core;
        _logger = logger;
    }

    public void Install()
    {
        try
        {
            _enableConVar = _core.ConVar.CreateOrFind(EnableConVarName, "启用 math_counter 浮点传值修复", true, ConvarFlags.SERVER_CAN_EXECUTE);
            _enabled = _enableConVar.Value;

            _core.Event.OnConVarValueChanged += OnConVarValueChanged;

            _core.GameHooks.Entities.AcceptInput.Pre += OnEntityIdentityAcceptInput;

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

            _core.GameHooks.Entities.AcceptInput.Pre -= OnEntityIdentityAcceptInput;

            _logger.LogInformation("{ServiceName} 已卸载。", ServiceName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "卸载 {ServiceName} 失败。", ServiceName);
        }
    }

    private void OnConVarValueChanged(IOnConVarValueChanged @event)
    {
        if (_enableConVar is null || @event.ConVarName != EnableConVarName)
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

    private void OnEntityIdentityAcceptInput(ref AcceptInputEntityPreContext ctx)
    {
        if (!_enabled)
        {
            return;
        }

        if (ctx.Params.EntityInstance is CLogicCase logic_case)
        {
            HandleLogicCase(logic_case, ref ctx);
        }
    }

    private void HandleLogicCase(CLogicCase logic_case, ref AcceptInputEntityPreContext ctx)
    {
        if (!ctx.Params.VariantValue.TryGetFloat(out var fVal))
        {
            return;
        }

        var cases = logic_case.Case;
        if (cases == null)
        {
            return;
        }

        for (int i = 0; i < cases.ElementCount; i++)
        {
            // this is broken, crashing
            // caseVal = cases[i];

            // hackfix
            var pszCase = Marshal.ReadIntPtr(cases.Address + i * 8);
            if (pszCase == 0)
            {
                continue;
            }

            var sCase = Marshal.PtrToStringUTF8(pszCase);
            if (sCase == null)
            {
                continue;
            }

            if (int.TryParse(sCase, out var val) && val == (int)fVal)
            {
                ctx.SetHookResult(HookResult.Stop);
                logic_case.AcceptInput(ctx.Params.InputName, val);
                return;
            }
        }
    }
}
