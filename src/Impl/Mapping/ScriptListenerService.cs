using Microsoft.Extensions.Logging;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Events;
using SwiftlyS2.Shared.GameEventDefinitions;
using SwiftlyS2.Shared.Players;
using ZombiEden.CS2.SwiftlyS2.Fixes.Interface;

namespace ZombiEden.CS2.SwiftlyS2.Fixes.Impl.Mapping;

// reference: https://github.com/Source2ZE/CS2Fixes/blob/bb3be3449c122bebf2d74e85b61987de408ac339/src/playermanager.cpp#L132-L143
public class ScriptListenerService : IScriptListenerService
{
    public string ServiceName => "ScriptListener";

    private readonly ISwiftlyCore _core;
    private readonly ILogger _logger;

    public ScriptListenerService(ISwiftlyCore core, ILogger<ScriptListenerService> logger)
    {
        _core = core;
        _logger = logger;
    }

    public void Install()
    {
        try
        {
            _core.Event.OnClientSteamAuthorize += OnClientSteamAuthorize;

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

            _logger.LogInformation("{ServiceName} 已卸载。", ServiceName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "卸载 {ServiceName} 失败。", ServiceName);
        }
    }

    private void OnClientSteamAuthorize(IOnClientSteamAuthorizeEvent @event)
    {
        var player = _core.PlayerManager.GetPlayer(@event.PlayerId);
        CreateEvent_PlayerConnect(player);
    }

    private void CreateEvent_PlayerConnect(IPlayer? player)
    {
        if (player == null || player.Controller == null)
        {
            return;
        }

        _core.GameEvent.FireToServer<EventPlayerConnect>(@e =>
        {
            e.Name = player.Name;
            e.UserId = player.Slot;
            e.Bot = false;

            // Needs a custom name, because CS2 strips keys like xuid/networkid etc from logic_eventlistener output, and can't use uint64 because JS can't parse it..
            var sSteamid = player.UnauthorizedSteamID.ToString();
            e.Accessor.SetString("steam_id", sSteamid);
        });
    }
}