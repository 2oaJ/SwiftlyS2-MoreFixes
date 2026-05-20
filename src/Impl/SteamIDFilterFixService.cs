using Microsoft.Extensions.Logging;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Convars;
using SwiftlyS2.Shared.Events;
using SwiftlyS2.Shared.GameEventDefinitions;
using SwiftlyS2.Shared.Misc;
using SwiftlyS2.Shared.Natives;
using SwiftlyS2.Shared.Players;
using SwiftlyS2.Shared.SchemaDefinitions;
using ZombiEden.CS2.SwiftlyS2.Fixes.Interface;

namespace ZombiEden.CS2.SwiftlyS2.Fixes.Impl;

// reference: https://github.com/Source2ZE/CS2Fixes/pull/317/changes
public class SteamIDFilterFixService : ISteamIDFilterFixService
{
    private const string EnableConVarName = "sw_map_steamids_enable";

    public string ServiceName => "SteamIDFilterFix";

    private readonly ISwiftlyCore _core;
    private readonly ILogger _logger;

    private IConVar<bool>? _enableConVar;
    private Guid _eventPlayerSpawn;

    public SteamIDFilterFixService(ISwiftlyCore core, ILogger<SteamIDFilterFixService> logger)
    {
        _core = core;
        _logger = logger;
    }

    public void Install()
    {
        try
        {
            _enableConVar = _core.ConVar.CreateOrFind(EnableConVarName, "启用 steamid 过滤", true, ConvarFlags.SERVER_CAN_EXECUTE);

            _core.Event.OnClientSteamAuthorize += OnClientSteamAuthorize;

            _eventPlayerSpawn = _core.GameEvent.HookPost<EventPlayerSpawn>(OnPlayerSpawnPost);

            _logger.LogInformation("{ServiceName} 安装完成", ServiceName);
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
            _core.Event.OnClientSteamAuthorize -= OnClientSteamAuthorize;

            _core.GameEvent.Unhook(_eventPlayerSpawn);

            _logger.LogInformation("{ServiceName} 已卸载。", ServiceName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "卸载 {ServiceName} 失败。", ServiceName);
        }
    }

    private HookResult OnPlayerSpawnPost(EventPlayerSpawn @event)
    {
        var hController = new CHandle<CCSPlayerController>(@event.UserIdController.Entity!.EntityHandle.Raw);

        _core.Scheduler.NextTick(() =>
        {
            var controller = hController.Value;
            if (controller == null)
            {
                return;
            }

            var player = controller.ToPlayer();
            SetSteamIdAttribute(player);
        });

        return HookResult.Continue;
    }

    private void OnClientSteamAuthorize(IOnClientSteamAuthorizeEvent @event)
    {
        var player = _core.PlayerManager.GetPlayer(@event.PlayerId);
        SetSteamIdAttribute(player);
    }

    private void SetSteamIdAttribute(IPlayer? player)
    {
        if (!_enableConVar!.Value)
        {
            return;
        }

        if (player == null || !player.IsValid || player.IsFakeClient)
        {
            return;
        }

        var controller = player.Controller!;
        var pawn = controller.PlayerPawn.Value;
        if (pawn == null)
        {
            return;
        }

        var steamID = player.UnauthorizedSteamID.ToString();
        pawn.AcceptInput("AddAttribute", steamID);
        controller.AcceptInput("AddAttribute", steamID);
    }
}

