using Microsoft.Extensions.Logging;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Memory;
using SwiftlyS2.Shared.Misc;
using SwiftlyS2.Shared.SchemaDefinitions;
using ZombiEden.CS2.SwiftlyS2.Fixes.Interface;
using ZombiEden.CS2.SwiftlyS2.Fixes.Sdk;
using static ZombiEden.CS2.SwiftlyS2.Fixes.Extensions;
using static ZombiEden.CS2.SwiftlyS2.Fixes.Sdk.GameTypes;

namespace ZombiEden.CS2.SwiftlyS2.Fixes.Impl
{
    /// <summary>
    /// 武器剥离修复实现
    /// </summary>
    public class StripFixService(ISwiftlyCore core, ILogger<IStripFixService> logger) : IStripFixService
    {
        public string ServiceName => "StripFix";

        private readonly ILogger<IStripFixService> _logger = logger;

        private unsafe delegate void CGamePlayerEquip__Precache_t(nint self, CEntityPrecacheContext* pContext);
        private IUnmanagedFunction<CGamePlayerEquip__Precache_t>? _hook;
        private Guid _hookId;

        private unsafe delegate void CGamePlayerEquip__Use_t(nint self, InputData_t* inputData);
        private IUnmanagedFunction<CGamePlayerEquip__Use_t>? _hook2;
        private Guid _hook2Id;

        private readonly Dictionary<uint, HashSet<gear_slot_t>> _playerEquipDict = [];
        private const int MAX_EQUIPMENTS_SIZE = 32;

        private const uint ENTITY_MURMURHASH_SEED = 0x97984357;
        private const uint ENTITY_UNIQUE_INVALID = ~0U;

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
            _hook?.RemoveHook(_hookId);
            _hook2?.RemoveHook(_hook2Id);

            _playerEquipDict.Clear();

            _logger.LogInformation($"{ServiceName} uninstalled");
        }

        private unsafe void HookCGamePlayerEquipPrecache()
        {
            var pCGamePlayerEquipVTable = core.Memory.GetVTableAddress("server", "CGamePlayerEquip");
            if (!pCGamePlayerEquipVTable.HasValue)
            {
                _logger.LogError("Failed to find CGamePlayerEquip vtable");
                return;
            }

            int offset = core.GameData.GetOffset("CBaseEntity::Precache");
            if (offset == -1)
            {
                _logger.LogError("Failed to find CBaseEntity::Precache offset");
                return;
            }

            _hook = core.Memory.GetUnmanagedFunctionByVTable<CGamePlayerEquip__Precache_t>(pCGamePlayerEquipVTable.Value, offset);
            _hookId = _hook.AddHook(original => (self, pContext) =>
            {
                original()(self, pContext);
                CGamePlayerEquip_OnPrecache(pContext->m_pKeyValues);
            });
        }

        private unsafe void HookCGamePlayerEquipUse()
        {
            var pCGamePlayerEquipVTable = core.Memory.GetVTableAddress("server", "CGamePlayerEquip");
            if (!pCGamePlayerEquipVTable.HasValue)
            {
                _logger.LogError("Failed to find CGamePlayerEquip vtable");
                return;
            }

            int offset = core.GameData.GetOffset("CBaseEntity::Use");
            if (offset == -1)
            {
                _logger.LogError("Failed to find CBaseEntity::Use offset");
                return;
            }

            _hook2 = core.Memory.GetUnmanagedFunctionByVTable<CGamePlayerEquip__Use_t>(pCGamePlayerEquipVTable.Value, offset);
            _hook2Id = _hook2.AddHook(original => (self, pInput) =>
            {
                var equipEntity = core.Memory.ToSchemaClass<CGamePlayerEquip>(self);
                CGamePlayerEquip_OnUse(equipEntity, pInput);
                original()(self, pInput);
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
            var caller = core.EntitySystem.GetEntityByAddress(pInput->pActivator);
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
}