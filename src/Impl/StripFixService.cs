using Microsoft.Extensions.Logging;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Memory;
using SwiftlyS2.Shared.SchemaDefinitions;
using System.Runtime.InteropServices;
using ZombiEden.CS2.SwiftlyS2.Fixes.Interface;

namespace ZombiEden.CS2.SwiftlyS2.Fixes.Impl
{
    /// <summary>
    /// 武器剥离修复实现
    /// </summary>
    public class StripFixService(ISwiftlyCore core, ILogger<IStripFixService> logger) : IStripFixService
    {
        private delegate void CGamePlayerEquipUseDelegate(nint self, nint inputData);

        public string ServiceName => "StripFix";

        private readonly ILogger<IStripFixService> _logger = logger;

        private Guid? _hookId;
        private IUnmanagedFunction<CGamePlayerEquipUseDelegate>? _hook;

        private const int SF_PLAYEREQUIP_STRIPFIRST = 0x0002;
        private const int SF_PLAYEREQUIP_ONLYSTRIPSAME = 0x0004;

        [StructLayout(LayoutKind.Sequential)]
        private struct InputData_t
        {
            public nint pActivator;
            public nint pCaller;
            public nint value;
            public int nOutputID;
        }

        public void Install()
        {
            try
            {
                var pCGamePlayerEquipVTable = core.Memory.GetVTableAddress("server", "CGamePlayerEquip");

                if (!pCGamePlayerEquipVTable.HasValue)
                {
                    _logger.LogError("Failed to find CGamePlayerEquip vtable");
                    return;
                }

                int useOffset = core.GameData.GetOffset("CBaseEntity::Use");
                if (useOffset == -1)
                {
                    _logger.LogError("Failed to find CBaseEntity::Use offset");
                    return;
                }

                _hook = core.Memory.GetUnmanagedFunctionByVTable<CGamePlayerEquipUseDelegate>(
                    pCGamePlayerEquipVTable.Value,
                    useOffset);

                if (_hook == null)
                {
                    _logger.LogError("Failed to create unmanaged function");
                    return;
                }

                _hookId = _hook.AddHook(original => (nint self, nint inputDataPtr) =>
                {
                    OnCGamePlayerEquipUse(original, self, inputDataPtr);
                });

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
            if (_hookId.HasValue && _hook != null)
            {
                _hook.RemoveHook(_hookId.Value);
                _logger.LogInformation($"{ServiceName} uninstalled");
            }
        }

        private void OnCGamePlayerEquipUse(Func<CGamePlayerEquipUseDelegate> original, nint self, nint inputDataPtr)
        {
            try
            {
                var equipEntity = core.Memory.ToSchemaClass<CGamePlayerEquip>(self);
                var inputData = Marshal.PtrToStructure<InputData_t>(inputDataPtr);
                var activator = core.Memory.ToSchemaClass<CBaseEntity>(inputData.pActivator);
                uint flags = equipEntity.Spawnflags;

                bool hasStripFlag = (flags & SF_PLAYEREQUIP_STRIPFIRST) != 0;
                bool hasOnlyStripFlag = (flags & SF_PLAYEREQUIP_ONLYSTRIPSAME) != 0;

                if ((hasStripFlag || hasOnlyStripFlag) &&
                    activator != null &&
                    activator.DesignerName == "player")
                {
                    var pawn = activator.As<CCSPlayerPawn>();
                    if (hasStripFlag)
                    {
                        _logger.LogInformation($"Stripping weapons from player {pawn.Controller.Value?.DesignerName}");
                        StripPlayerWeapons(pawn);
                    }
                }

                original()(self, inputDataPtr);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error in OnCGamePlayerEquipUse: {ex.Message}");
                original()(self, inputDataPtr);
            }
        }

        public bool StripPlayerWeapons(CCSPlayerPawn pawn)
        {
            try
            {
                if (pawn == null)
                {
                    _logger.LogWarning("Attempted to strip weapons from null pawn");
                    return false;
                }

                var playerName = pawn.Controller.Value?.DesignerName ?? "Unknown";
                _logger.LogInformation($"Stripping weapons from player: {playerName}");

                var itemServices = pawn.ItemServices;
                if (itemServices != null)
                {
                    itemServices.RemoveItems();
                    return true;
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error in StripPlayerWeapons: {ex.Message}");
                return false;
            }
        }

        public bool StripPlayerWeapons(CCSPlayerPawn pawn, HashSet<uint> stripSet)
        {
            try
            {
                if (pawn == null)
                {
                    _logger.LogWarning("Attempted to strip weapons from null pawn");
                    return false;
                }

                var playerName = pawn.Controller.Value?.DesignerName ?? "Unknown";
                _logger.LogInformation($"Stripping weapons from player: {playerName}");

                var weaponService = pawn.WeaponServices;
                var removeWeapons = new List<CBasePlayerWeapon>();

                if (weaponService != null)
                {
                    foreach (var weapon in weaponService.MyValidWeapons)
                    {
                        if (weapon is { IsValid: true })
                        {
                            var slot = (uint)weapon.PlayerWeaponVData.Slot;
                            if (stripSet.Contains(slot))
                            {
                                removeWeapons.Add(weapon);
                            }
                        }
                    }

                    foreach (var item in removeWeapons)
                    {
                        weaponService.DropWeapon(item);
                        item.DispatchSpawn();
                    }

                    return true;
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error in StripPlayerWeapons: {ex.Message}");
                return false;
            }
        }
    }
}