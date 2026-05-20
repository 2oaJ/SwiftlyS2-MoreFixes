using Microsoft.Extensions.Logging;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Natives;
using SwiftlyS2.Shared.SchemaDefinitions;

namespace ZombiEden.CS2.SwiftlyS2.Fixes.Impl.CustomEntity;

public class CPointViewControl(CLogicRelay _proxy)
{
    private const int SF_POINT_VIEWCONTROL_FROZEN = 1 << 5;
    private const int SF_POINT_VIEWCONTROL_FOV = 1 << 6;
    private const int SF_POINT_VIEWCONTROL_DISARM = 1 << 7;

    public CHandle<CEntityInstance> GetHandle()
    {
        return _proxy.Entity!.EntityHandle;
    }

    public CLogicRelay GetProxy()
    {
        return _proxy;
    }

    public CBaseEntity? GetTargetCameraEntity()
    {
        var target = GameFunctions.UTIL_FindEntityByName(null, _proxy.Target);
        return target != null && (target.Collision != null) ? target : null;
    }

    public bool HasTargetCameraEntity()
    {
        var target = _proxy.Target;
        return !string.IsNullOrEmpty(target) && target.Length >= 2;
    }

    public bool HasFrozen()
    {
        return (_proxy.Spawnflags & SF_POINT_VIEWCONTROL_FROZEN) != 0;
    }

    public bool HasFOV()
    {
        return (_proxy.Spawnflags & SF_POINT_VIEWCONTROL_FOV) != 0;
    }

    public bool HasDisarm()
    {
        return (_proxy.Spawnflags & SF_POINT_VIEWCONTROL_DISARM) != 0;
    }

    public uint GetFOV()
    {
        return uint.Clamp((uint)_proxy.Health, 16, 179);
    }
}

public static class CPointViewControlHandler
{
    private const uint INVALID_FOV = 0xFFFFFFFF;
    private const uint RESET_FOV = 0xFFFFFFFE;

    private class ViewControl
    {
        public List<CHandle<CCSPlayerPawn>> Players = [];
        public string? ViewTarget = null;
        public string? Name = null;
    };

    private static ISwiftlyCore? _core;
    private static ILogger? _logger;
    private static readonly Dictionary<uint, ViewControl> _repository = [];
    private static CHandle<CBaseEntity> INVALID_HANDLE = CHandle<CBaseEntity>.Invalid;

    public static void Setup(ISwiftlyCore core, ILogger logger)
    {
        _core = core;
        _logger = logger;
    }

    public static void UpdatePlayerState(CCSPlayerPawn? pawn, CHandle<CBaseEntity> target, bool frozen, uint fov = INVALID_FOV, bool disarm = false)
    {
        if (pawn == null)
        {
            return;
        }

        var cameraService = pawn.CameraServices;
        if (cameraService != null)
        {
            cameraService.ViewEntity = target;
            cameraService.ZoomOwner = INVALID_HANDLE;

            if (fov != INVALID_FOV)
            {
                var controller = pawn.Controller.Value;
                if (controller != null)
                {
                    if (fov == RESET_FOV)
                    {
                        cameraService.FOV = controller.DesiredFOV;
                    }
                    else
                    {
                        cameraService.FOV = fov;
                    }
                }
            }
        }

        if (disarm)
        {
            var weaponService = pawn.WeaponServices;
            if (weaponService != null)
            {
                var activeWeapon = weaponService.ActiveWeapon.Value;
                activeWeapon?.Disarm();
            }
        }

        ref var flags = ref pawn.Flags;
        var gamerules = _core!.EntitySystem.GetGameRules();

        if (gamerules != null && gamerules.FreezePeriod)
        {
            frozen = true;
        }

        if (frozen)
        {
            flags |= (uint)Flags_t.FL_FROZEN;
        }
        else
        {
            flags &= ~(uint)Flags_t.FL_FROZEN;
        }

        pawn.FlagsUpdated();
    }

    public static void OnCreated(CEntityInstance? entity)
    {
        var viewControl = CustomEntityCast.AsPointViewControl(entity);
        if (viewControl == null)
        {
            return;
        }

        if (!viewControl.HasTargetCameraEntity())
        {
            _logger!.LogWarning("PointViewControl {name} has no target camera entity", viewControl.GetProxy().Entity!.Name);
            return;
        }

        var proxy = viewControl.GetProxy();
        _repository[viewControl.GetHandle().Raw] = new ViewControl()
        {
            ViewTarget = proxy.Target,
            Name = proxy.Entity!.Name
        };
    }

    public static bool OnEnable(this CPointViewControl entity, CEntityInstance? activator)
    {
        if (activator == null || activator is not CCSPlayerPawn pawn || !pawn.IsPlayerAlive())
        {
            return false;
        }

        var key = entity.GetHandle().Raw;
        if (!_repository.TryGetValue(key, out var repoVc))
        {
            return false;
        }

        var controller = pawn.Controller.Value;
        if (controller == null)
        {
            return false;
        }

        if (controller.IsBot() || controller.IsHLTV)
        {
            _logger!.LogWarning("PointViewControl {vcname} try enable for bot or HLTV: {playername}", repoVc.Name, controller.PlayerName);
            return false;
        }

        CHandle<CCSPlayerPawn> hPawn = new(pawn.Entity!.EntityHandle.Raw);
        foreach (var pair in _repository)
        {
            var vk = pair.Key;
            var vc = pair.Value;
            var index = vc.Players.FindIndex(x => x == hPawn);
            if (index != -1)
            {
                if (vk == key)
                {
                    _logger!.LogWarning("PointViewControl {vcname} was enabled twice in a row! player: {playername}", vc.Name, controller.PlayerName);
                    return false;
                }

                vc.Players.RemoveAt(index);
                UpdatePlayerState(pawn, INVALID_HANDLE, false, RESET_FOV);
                _logger!.LogWarning("PointViewControl {vcname} already enabled for {playername}", vc.Name, controller.PlayerName);
                break;
            }
        }

        repoVc.Players.Add(hPawn);
        return true;
    }

    public static bool OnDisable(this CPointViewControl entity, CEntityInstance? activator)
    {
        if (activator == null || activator is not CCSPlayerPawn pawn || !pawn.IsPlayerAlive())
        {
            return false;
        }

        var key = entity.GetHandle().Raw;
        if (!_repository.TryGetValue(key, out var repoVc))
        {
            return false;
        }

        var controller = pawn.Controller.Value;
        if (controller == null)
        {
            return false;
        }

        if (controller.IsBot() || controller.IsHLTV)
        {
            _logger!.LogWarning("PointViewControl {vcname} try enable for bot or HLTV: {playername}", repoVc.Name, controller.PlayerName);
            return false;
        }

        CHandle<CCSPlayerPawn> hPawn = new(pawn.Entity!.EntityHandle.Raw);
        UpdatePlayerState(pawn, INVALID_HANDLE, false, RESET_FOV);
        return repoVc.Players.Remove(hPawn);
    }

    public static bool OnEnableAll(this CPointViewControl entity)
    {
        var key = entity.GetHandle().Raw;
        if (!_repository.TryGetValue(key, out var repoVc))
        {
            return false;
        }

        foreach (var player in _core!.PlayerManager.GetAllValidPlayers())
        {
            var controller = player.Controller;
            if (controller == null || controller.IsBot() || controller.IsHLTV)
            {
                continue;
            }

            var hPawn = controller.PlayerPawn;
            var pawn = hPawn.Value;
            if (pawn == null || !pawn.IsPlayerAlive())
            {
                continue;
            }

            foreach (var pair in _repository)
            {
                var vk = pair.Key;
                var vc = pair.Value;
                var index = vc.Players.FindIndex(x => x == hPawn);
                if (index != -1)
                {
                    vc.Players.RemoveAt(index);

                    if (vk == key)
                    {
                        continue;
                    }

                    UpdatePlayerState(pawn, INVALID_HANDLE, false, RESET_FOV);
                    _logger!.LogWarning("PointViewControl {vcname} already enabled for {playername}", vc.Name, controller.PlayerName);
                }
            }

            repoVc.Players.Add(hPawn);
        }

        return true;
    }

    public static bool OnDisableAll(this CPointViewControl entity)
    {
        var key = entity.GetHandle().Raw;
        if (!_repository.TryGetValue(key, out var repoVc))
        {
            return false;
        }

        foreach (var hPawn in repoVc.Players)
        {
            var pawn = hPawn.Value;
            if (pawn != null)
            {
                UpdatePlayerState(pawn, INVALID_HANDLE, false, RESET_FOV);
            }
        }

        repoVc.Players.Clear();

        return true;
    }

    // Must be called on game frame pre, and timer done in post!
    public static void RunThink()
    {
        // validate
        {
            var vcToRemove = new List<uint>();
            foreach (var pair in _repository)
            {
                var key = pair.Key;
                var hEnt = new CHandle<CEntityInstance>(key);
                var entity = hEnt.Value;
                if (entity == null)
                {
                    foreach (var hPawn in pair.Value.Players)
                    {
                        var pawn = hPawn.Value;
                        if (pawn != null)
                        {
                            UpdatePlayerState(pawn, INVALID_HANDLE, false, RESET_FOV);
                        }
                    }

                    vcToRemove.Add(key);
                }
            }

            foreach (var item in vcToRemove)
            {
                _repository.Remove(item);
            }
        }

        // think every tick
        {
            foreach (var pair in _repository)
            {
                var vk = pair.Key;
                var vc = pair.Value;
                var hEnt = new CHandle<CEntityInstance>(vk);
                var entity = CustomEntityCast.AsPointViewControl(hEnt.Value);
                if (entity == null)
                {
                    _logger!.LogError("Why invalid entity here?");
                    continue;
                }

                if (vc.Players.Count == 0)
                {
                    continue;
                }

                var target = entity.GetTargetCameraEntity();
                if (target == null)
                {
                    foreach (var hPawn in vc.Players)
                    {
                        var pawn = hPawn.Value;
                        if (pawn != null)
                        {
                            UpdatePlayerState(pawn, INVALID_HANDLE, false, RESET_FOV);
                        }
                    }

                    vc.Players.Clear();
                    continue;
                }

                var pawnToRemove = new List<CHandle<CCSPlayerPawn>>();
                foreach (var hPawn in vc.Players)
                {
                    var pawn = hPawn.Value;
                    if (pawn == null)
                    {
                        pawnToRemove.Add(hPawn);
                        continue;
                    }

                    if (!pawn.IsPlayerAlive())
                    {
                        UpdatePlayerState(pawn, INVALID_HANDLE, false, RESET_FOV);
                        pawnToRemove.Add(hPawn);
                        continue;
                    }

                    UpdatePlayerState(pawn, new(target.Entity!.EntityHandle.Raw), entity.HasFrozen(), entity.HasFOV() ? entity.GetFOV() : INVALID_FOV, entity.HasDisarm());
                }

                foreach (var item in pawnToRemove)
                {
                    vc.Players.Remove(item);
                }
            }
        }
    }

    public static bool IsViewControl(this CCSPlayerPawn pawn)
    {
        var hPawn = pawn.Entity!.EntityHandle.Raw;
        foreach (var pair in _repository)
        {
            var vc = pair.Value;
            foreach (var item in vc.Players)
            {
                if (item.Raw == hPawn)
                {
                    return true;
                }
            }
        }

        return false;
    }

    public static void Shutdown()
    {
        foreach (var pair in _repository)
        {
            var vc = pair.Value;
            foreach (var hPawn in vc.Players)
            {
                var pawn = hPawn.Value;
                if (pawn != null)
                {
                    UpdatePlayerState(pawn, INVALID_HANDLE, false, RESET_FOV);
                }
            }

            vc.Players.Clear();
        }

        _repository.Clear();
    }
}