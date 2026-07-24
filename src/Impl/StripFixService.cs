using Microsoft.Extensions.Logging;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Memory;
using SwiftlyS2.Shared.Misc;
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

    private unsafe delegate void CGamePlayerEquip__InputTriggerForAllPlayers_t(nint pEntity, InputData_t* pInput);
    private IUnmanagedFunction<CGamePlayerEquip__InputTriggerForAllPlayers_t>? _hook3;
    private Guid _hook3Id;

    private unsafe delegate void CGamePlayerEquip__InputTriggerForActivatedPlayer_t(nint pEntity, InputData_t* pInput);
    private IUnmanagedFunction<CGamePlayerEquip__InputTriggerForActivatedPlayer_t>? _hook4;
    private Guid _hook4Id;

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
        try
        {
            HookCGamePlayerEquipPrecache();
            HookCGamePlayerEquipUse();

            _logger.LogInformation($"{ServiceName} installed successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError($"Failed to install {ServiceName}: {ex.Message}");
            throw;
        }
    }

    public void Uninstall()
    {
        _hook1?.RemoveHook(_hook1Id);
        _hook2?.RemoveHook(_hook2Id);
        _hook3?.RemoveHook(_hook3Id);
        _hook4?.RemoveHook(_hook4Id);

        _playerEquipDict.Clear();

        _logger.LogInformation($"{ServiceName} uninstalled");
    }

    private unsafe void HookCGamePlayerEquipPrecache()
    {
        var pCGamePlayerEquipVTable = _core.Memory.GetVTableAddress("server", "CGamePlayerEquip");
        if (!pCGamePlayerEquipVTable.HasValue)
        {
            _logger.LogError("Failed to find CGamePlayerEquip vtable");
            return;
        }

        int offset = _core.GameData.GetOffset("CBaseEntity::Precache");
        if (offset == -1)
        {
            _logger.LogError("Failed to find CBaseEntity::Precache offset");
            return;
        }

        _hook1 = _core.Memory.GetUnmanagedFunctionByVTable<CGamePlayerEquip__Precache_t>(pCGamePlayerEquipVTable.Value, offset);
        _hook1Id = _hook1.AddHook(original => (self, pContext) =>
        {
            original()(self, pContext);
            CGamePlayerEquip_OnPrecache(pContext->m_pKeyValues);
        });
    }

    private unsafe void HookCGamePlayerEquipUse()
    {
        var pCGamePlayerEquipVTable = _core.Memory.GetVTableAddress("server", "CGamePlayerEquip");
        if (!pCGamePlayerEquipVTable.HasValue)
        {
            _logger.LogError("Failed to find CGamePlayerEquip vtable");
            return;
        }

        int offset = _core.GameData.GetOffset("CBaseEntity::Use");
        if (offset == -1)
        {
            _logger.LogError("Failed to find CBaseEntity::Use offset");
            return;
        }

        _hook2 = _core.Memory.GetUnmanagedFunctionByVTable<CGamePlayerEquip__Use_t>(pCGamePlayerEquipVTable.Value, offset);
        _hook2Id = _hook2.AddHook(original => (self, pInput) =>
        {
            var equipEntity = _core.Memory.ToSchemaClass<CGamePlayerEquip>(self);
            CGamePlayerEquip_OnUse(equipEntity, pInput);
            original()(self, pInput);
        });
    }

    private unsafe void InstallTriggerForAllPlayersHook()
    {
        var sig = _core.GameData.GetSignature("CGamePlayerEquip::InputTriggerForAllPlayers");
        _hook3 = _core.Memory.GetUnmanagedFunctionByAddress<CGamePlayerEquip__InputTriggerForAllPlayers_t>(sig);

        if (_hook3 == null)
        {
            _logger.LogError("Failed to create unmanaged function for InputTriggerForAllPlayers");
            return;
        }

        _hook3Id = _hook3.AddHook(original =>
        {
            return (pEntity, pInput) =>
            {
                var equipEntity = _core.Memory.ToSchemaClass<CGamePlayerEquip>(pEntity);
                TriggerForAllPlayers(equipEntity, pInput);
                original()(pEntity, pInput);
            };
        });
    }

    private unsafe void InstallTriggerForActivatedPlayerHook()
    {
        var sig = _core.GameData.GetSignature("CGamePlayerEquip::InputTriggerForActivatedPlayer");
        _hook4 = _core.Memory.GetUnmanagedFunctionByAddress<CGamePlayerEquip__InputTriggerForActivatedPlayer_t>(sig);

        if (_hook4 == null)
        {
            _logger.LogError("Failed to create unmanaged function for InputTriggerForActivatedPlayer");
            return;
        }

        _hook4Id = _hook4.AddHook(original =>
        {
            return (pEntity, pInput) =>
            {
                var equipEntity = _core.Memory.ToSchemaClass<CGamePlayerEquip>(pEntity);
                bool shouldCallOriginal = TriggerForActivatedPlayer(equipEntity, pInput);
                if (shouldCallOriginal)
                {
                    original()(pEntity, pInput);
                }
            };
        });
    }

    private void CGamePlayerEquip_OnPrecache(nint pEntityKV)
    {
        var hammerUniqueId = NativeCEntityKeyValues__GetString(pEntityKV, "hammerUniqueId");
        if (string.IsNullOrEmpty(hammerUniqueId))
        {
            return;
        }

        var weapons = new HashSet<gear_slot_t>();
        for (int i = 0; i < MAX_EQUIPMENTS_SIZE; i++)
        {
            var val = NativeCEntityKeyValues__GetString(pEntityKV, $"weapon{i}");
            if (string.IsNullOrEmpty(val))
            {
                continue;
            }

            if (ItemHelper.WeaponGearSlotDict.TryGetValue(val, out var slot))
            {
                weapons.Add(slot);
            }
        }

        if (weapons.Count > 0)
        {
            var hEntity = MurmurHash2.HashStringLowercase(hammerUniqueId, ENTITY_MURMURHASH_SEED);
            _playerEquipDict[hEntity] = weapons;
        }
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

    private unsafe void TriggerForAllPlayers(CGamePlayerEquip entity, InputData_t* pInput)
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

    private unsafe bool TriggerForActivatedPlayer(CGamePlayerEquip entity, InputData_t* pInput)
    {
        var caller = _core.EntitySystem.GetEntityByAddress(pInput->pActivator);
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

        if (pInput->value.TryGetString(out var weaponName) && !string.IsNullOrEmpty(weaponName) && weaponName != "(null)")
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