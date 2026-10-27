using Microsoft.Extensions.Logging;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.EntitySystem;
using SwiftlyS2.Shared.Events;
using SwiftlyS2.Shared.GameHooks;
using SwiftlyS2.Shared.Memory;
using SwiftlyS2.Shared.Misc;
using SwiftlyS2.Shared.Natives;
using SwiftlyS2.Shared.SchemaDefinitions;
using ZombiEden.CS2.SwiftlyS2.Fixes.Interface;
using ZombiEden.CS2.SwiftlyS2.Fixes.Sdk;
using static ZombiEden.CS2.SwiftlyS2.Fixes.Extensions;
using static ZombiEden.CS2.SwiftlyS2.Fixes.Sdk.GameTypes;

namespace ZombiEden.CS2.SwiftlyS2.Fixes.Impl;

/// <summary>
/// 武器剥离修复实现
/// </summary>
public class StripFixService : IStripFixService
{
    public string ServiceName => "StripFix";

    private readonly ISwiftlyCore _core;
    private readonly ILogger _logger;

    private unsafe delegate void CGamePlayerEquip__Precache_t(nint self, CEntityPrecacheContext* pContext);
    private IUnmanagedFunction<CGamePlayerEquip__Precache_t>? _hook1;
    private Guid _hook1Id;

    private unsafe delegate void CGamePlayerEquip__Use_t(nint self, InputData_t* inputData);
    private IUnmanagedFunction<CGamePlayerEquip__Use_t>? _hook2;
    private Guid _hook2Id;

    private bool _installed;

    private readonly Dictionary<uint, HashSet<gear_slot_t>> _playerEquipDict = [];
    private const int MAX_EQUIPMENTS_SIZE = 32;

    private const uint ENTITY_MURMURHASH_SEED = 0x97984357;
    private const uint ENTITY_UNIQUE_INVALID = ~0U;

    public StripFixService(ISwiftlyCore core, ILogger<StripFixService> logger)
    {
        _core = core;
        _logger = logger;
    }

    public void Install()
    {
        if (_installed)
        {
            return;
        }

        try
        {
            Hook_CGamePlayerEquip_Precache();
            Hook_CGamePlayerEquip_Use();

            _core.Event.OnMapLoad += OnMapLoad;

            _core.GameHooks.Entities.AcceptInput.Pre += OnEntityIdentityAcceptInput;

            _installed = true;

            _logger.LogInformation($"{ServiceName} installed successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError($"Failed to install {ServiceName}: {ex.Message}");
            Uninstall();
            throw;
        }
    }

    public void Uninstall()
    {
        RemoveHook(ref _hook1, ref _hook1Id);
        RemoveHook(ref _hook2, ref _hook2Id);

        _core.Event.OnMapLoad -= OnMapLoad;

        _core.GameHooks.Entities.AcceptInput.Pre -= OnEntityIdentityAcceptInput;

        _installed = false;

        _playerEquipDict.Clear();

        _logger.LogInformation($"{ServiceName} uninstalled");
    }

    private static void RemoveHook<TDelegate>(ref IUnmanagedFunction<TDelegate>? hook, ref Guid hookId)
        where TDelegate : Delegate
    {
        if (hook is not null && hookId != Guid.Empty)
        {
            hook.RemoveHook(hookId);
        }

        hook = null;
        hookId = Guid.Empty;
    }

    private void OnMapLoad(IOnMapLoadEvent @event)
    {
        _playerEquipDict.Clear();
    }

    private unsafe void Hook_CGamePlayerEquip_Precache()
    {
        var pCGamePlayerEquipVTable = _core.Memory.GetVTableAddress("server", "CGamePlayerEquip");
        if (!pCGamePlayerEquipVTable.HasValue)
        {
            throw new Exception("Failed to find CGamePlayerEquip vtable");
        }

        int offset = _core.GameData.GetOffset("CBaseEntity::Precache");
        if (offset == -1)
        {
            throw new Exception("Failed to find CBaseEntity::Precache offset");
        }

        _hook1 = _core.Memory.GetUnmanagedFunctionByVTable<CGamePlayerEquip__Precache_t>(pCGamePlayerEquipVTable.Value, offset);
        _hook1Id = _hook1.AddHook(original => (self, pContext) =>
        {
            using var filteredKV = new CEntityKeyValues();

            if (!CGamePlayerEquip_OnPrecache(pContext->m_pKeyValues, filteredKV))
            {
                original()(self, pContext);
                return;
            }

            CEntityPrecacheContext context = *pContext;
            context.m_pKeyValues = filteredKV.Address;
            original()(self, &context);
        });
    }

    private unsafe void Hook_CGamePlayerEquip_Use()
    {
        var pCGamePlayerEquipVTable = _core.Memory.GetVTableAddress("server", "CGamePlayerEquip");
        if (!pCGamePlayerEquipVTable.HasValue)
        {
            throw new Exception("Failed to find CGamePlayerEquip vtable");
        }

        int offset = _core.GameData.GetOffset("CBaseEntity::Use");
        if (offset == -1)
        {
            throw new Exception("Failed to find CBaseEntity::Use offset");
        }

        _hook2 = _core.Memory.GetUnmanagedFunctionByVTable<CGamePlayerEquip__Use_t>(pCGamePlayerEquipVTable.Value, offset);
        _hook2Id = _hook2.AddHook(original => (self, pInput) =>
        {
            var equipEntity = _core.Memory.ToSchemaClass<CGamePlayerEquip>(self);
            CGamePlayerEquip_OnUse(equipEntity, pInput);
            original()(self, pInput);
        });
    }

    private bool CGamePlayerEquip_OnPrecache(nint pEntityKV, CEntityKeyValues filteredKV)
    {
        var hammerUniqueId = NativeCEntityKeyValues__GetString(pEntityKV, "hammerUniqueId");
        var weapons = new HashSet<gear_slot_t>();
        bool bFiltered = false;

        for (int i = 0; i < MAX_EQUIPMENTS_SIZE; i++)
        {
            var key = $"weapon{i}";
            var val = NativeCEntityKeyValues__GetString(pEntityKV, key);

            if (string.IsNullOrEmpty(val))
            {
                continue;
            }

            // Precaching item_* classes crashes since the 2026-10-22 CS2 update
            if (val.StartsWith("item_", StringComparison.OrdinalIgnoreCase))
            {
                bFiltered = true;
            }
            else
            {
                filteredKV.SetString(key, val);
            }

            if (ItemHelper.WeaponGearSlotDict.TryGetValue(val, out var slot))
            {
                weapons.Add(slot);
            }
        }

        if (!string.IsNullOrEmpty(hammerUniqueId) && weapons.Count > 0)
        {
            var hEntity = MurmurHash2.HashStringLowercase(hammerUniqueId, ENTITY_MURMURHASH_SEED);
            _playerEquipDict[hEntity] = weapons;
        }

        return bFiltered;
    }

    private unsafe void CGamePlayerEquip_OnUse(CGamePlayerEquip entity, InputData_t* pInput)
    {
        var caller = _core.EntitySystem.GetEntityByAddress(pInput->pActivator);
        if (caller is not CCSPlayerPawn pawn)
        {
            return;
        }

        uint flags = entity.Spawnflags;
        if ((flags & SF_PLAYEREQUIP_STRIPFIRST) != 0)
        {
            StripPlayerWeapons(pawn);
        }
        else if ((flags & SF_PLAYEREQUIP_ONLYSTRIPSAME) != 0)
        {
            StripPlayerSameWeapons(pawn, entity);
        }
    }

    private void OnEntityIdentityAcceptInput(ref AcceptInputEntityPreContext ctx)
    {
        if (ctx.Params.EntityInstance is not CGamePlayerEquip game_player_equip)
        {
            return;
        }

        var inputName = ctx.Params.InputName;

        if (string.Equals(inputName, "TriggerForAllPlayers", StringComparison.OrdinalIgnoreCase))
        {
            CGamePlayerEquip_OnInputTriggerForAllPlayers(game_player_equip);
        }
        else if (string.Equals(inputName, "TriggerForActivatedPlayer", StringComparison.OrdinalIgnoreCase))
        {
            if (!CGamePlayerEquip_OnInputTriggerForActivatedPlayer(game_player_equip, ctx.Params.Activator?.Address, ctx.Params.VariantValue))
            {
                ctx.SetHookResult(HookResult.Stop);
            }
        }
    }

    private void CGamePlayerEquip_OnInputTriggerForAllPlayers(CGamePlayerEquip entity)
    {
        uint flags = entity.Spawnflags;
        if ((flags & SF_PLAYEREQUIP_STRIPFIRST) != 0)
        {
            var players = _core.PlayerManager.GetAllValidPlayers();
            foreach (var player in players)
            {
                var pawn = player.PlayerPawn;
                if (pawn.Valid() && pawn.IsPlayerAlive())
                {
                    StripPlayerWeapons(pawn);
                }
            }
        }
        else if ((flags & SF_PLAYEREQUIP_ONLYSTRIPSAME) != 0)
        {
            var players = _core.PlayerManager.GetAllValidPlayers();
            foreach (var player in players)
            {
                var pawn = player.PlayerPawn;
                if (pawn.Valid() && pawn.IsPlayerAlive())
                {
                    StripPlayerSameWeapons(pawn, entity);
                }
            }
        }
    }

    private bool CGamePlayerEquip_OnInputTriggerForActivatedPlayer(CGamePlayerEquip entity, nint? pActivator, in CVariant<CVariantDefaultAllocator> value)
    {
        if (!pActivator.HasValue)
        {
            return true;
        }

        var caller = _core.EntitySystem.GetEntityByAddress(pActivator.Value);
        if (caller is not CCSPlayerPawn pawn)
        {
            return true;
        }

        uint flags = entity.Spawnflags;
        if ((flags & SF_PLAYEREQUIP_STRIPFIRST) != 0)
        {
            StripPlayerWeapons(pawn);
        }
        else if ((flags & SF_PLAYEREQUIP_ONLYSTRIPSAME) != 0)
        {
            StripPlayerSameWeapons(pawn, entity);
        }

        var itemServices = pawn.ItemServices;
        if (itemServices == null)
        {
            return true;
        }

        if (value.TryGetString(out var weaponName) && !string.IsNullOrEmpty(weaponName) && weaponName != "(null)")
        {
            itemServices.GiveItem(weaponName);
            return false;
        }

        return true;
    }

    public bool StripPlayerSameWeapons(CCSPlayerPawn pawn, CGamePlayerEquip equipEntity)
    {
        var entityId = GetEntityUnique(equipEntity);
        if (_playerEquipDict.TryGetValue(entityId, out var stripSet) && stripSet.Count > 0)
        {
            return StripPlayerWeapons(pawn, stripSet);
        }

        return false;
    }

    public bool StripPlayerWeapons(CCSPlayerPawn pawn)
    {
        var itemServices = pawn.ItemServices;
        if (itemServices == null)
        {
            return false;
        }

        itemServices.RemoveItems();
        return true;
    }

    public bool StripPlayerWeapons(CCSPlayerPawn pawn, HashSet<gear_slot_t> stripSet)
    {
        var weaponService = pawn.WeaponServices;
        if (weaponService == null)
        {
            return false;
        }

        var removeWeapons = new List<CBasePlayerWeapon>();

        foreach (var hWeapon in weaponService.MyWeapons)
        {
            var weapon = hWeapon.Value?.As<CCSWeaponBase>();
            if (weapon == null)
            {
                continue;
            }

            var slot = weapon.WeaponBaseVData.GearSlot;
            if (stripSet.Contains(slot))
            {
                removeWeapons.Add(weapon);
            }
        }

        foreach (var item in removeWeapons)
        {
            weaponService.DropWeapon(item);
            item.Despawn();
        }

        return true;
    }

    private uint GetEntityUnique(CGamePlayerEquip entity)
    {
        string uniqueHammerID = entity.UniqueHammerID;
        if (string.IsNullOrEmpty(uniqueHammerID))
        {
            return ENTITY_UNIQUE_INVALID;
        }

        return MurmurHash2.HashStringLowercase(uniqueHammerID, ENTITY_MURMURHASH_SEED);
    }
}
