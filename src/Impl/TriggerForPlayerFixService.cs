using Microsoft.Extensions.Logging;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Memory;
using SwiftlyS2.Shared.Misc;
using SwiftlyS2.Shared.SchemaDefinitions;
using System.Runtime.InteropServices;
using ZombiEden.CS2.SwiftlyS2.Fixes.Interface;

namespace ZombiEden.CS2.SwiftlyS2.Fixes.Impl
{
    /// <summary>
    /// 玩家触发修复实现
    /// </summary>
    public class TriggerForPlayerFixService(
        ISwiftlyCore core,
        ILogger<ITriggerForPlayerFixService> logger,
        IStripFixService stripFixService) : ITriggerForPlayerFixService
    {
        private delegate void CGamePlayerEquip_InputTriggerForAllPlayersDelegate(nint pEntity, nint pInput);
        private delegate void CGamePlayerEquip_InputTriggerForActivatedPlayerDelegate(nint pEntity, nint pInput);

        public string ServiceName => "TriggerForPlayerFix";

        private Guid? _allPlayersHookId;
        private Guid? _activatedPlayerHookId;

        private IUnmanagedFunction<CGamePlayerEquip_InputTriggerForAllPlayersDelegate>? _allPlayersHook;
        private IUnmanagedFunction<CGamePlayerEquip_InputTriggerForActivatedPlayerDelegate>? _activatedPlayerHook;

        private static readonly Dictionary<uint, HashSet<uint>> s_PlayerEquipMap = new();

        private const uint ENTITY_MURMURHASH_SEED = 0x97984357;
        private const uint ENTITY_UNIQUE_INVALID = 0xFFFFFFFF;
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
                InstallTriggerForAllPlayersHook();
                InstallTriggerForActivatedPlayerHook();
                logger.LogInformation($"{ServiceName} installed successfully");
            }
            catch (Exception ex)
            {
                logger.LogError($"Failed to install {ServiceName}: {ex.Message}");
                throw;
            }
        }

        public void Uninstall()
        {
            if (_allPlayersHookId.HasValue && _allPlayersHook != null)
            {
                _allPlayersHook.RemoveHook(_allPlayersHookId.Value);
            }

            if (_activatedPlayerHookId.HasValue && _activatedPlayerHook != null)
            {
                _activatedPlayerHook.RemoveHook(_activatedPlayerHookId.Value);
            }

            s_PlayerEquipMap.Clear();
            logger.LogInformation($"{ServiceName} uninstalled");
        }

        private void InstallTriggerForAllPlayersHook()
        {
            var sig = core.GameData.GetSignature("CGamePlayerEquip::InputTriggerForAllPlayers");
            _allPlayersHook = core.Memory.GetUnmanagedFunctionByAddress<CGamePlayerEquip_InputTriggerForAllPlayersDelegate>(sig);

            if (_allPlayersHook == null)
            {
                logger.LogError("Failed to create unmanaged function for InputTriggerForAllPlayers");
                return;
            }

            _allPlayersHookId = _allPlayersHook.AddHook(original =>
            {
                return (nint pEntity, nint pInput) =>
                {
                    OnInputTriggerForAllPlayers(original, pEntity, pInput);
                };
            });
        }

        private void InstallTriggerForActivatedPlayerHook()
        {
            var sig = core.GameData.GetSignature("CGamePlayerEquip::InputTriggerForActivatedPlayer");
            _activatedPlayerHook = core.Memory.GetUnmanagedFunctionByAddress<CGamePlayerEquip_InputTriggerForActivatedPlayerDelegate>(sig);

            if (_activatedPlayerHook == null)
            {
                logger.LogError("Failed to create unmanaged function for InputTriggerForActivatedPlayer");
                return;
            }

            _activatedPlayerHookId = _activatedPlayerHook.AddHook(original =>
            {
                return (nint pEntity, nint pInput) =>
                {
                    OnInputTriggerForActivatedPlayer(original, pEntity, pInput);
                };
            });
        }

        private void OnInputTriggerForAllPlayers(Func<CGamePlayerEquip_InputTriggerForAllPlayersDelegate> original, nint pEntity, nint pInput)
        {
            try
            {
                TriggerForAllPlayers(pEntity, pInput);
                original()(pEntity, pInput);
            }
            catch (Exception ex)
            {
                logger.LogError($"Error in OnInputTriggerForAllPlayers: {ex.Message}");
                original()(pEntity, pInput);
            }
        }

        private void TriggerForAllPlayers(nint pEntity, nint pInput)
        {
            try
            {
                var equipEntity = core.Memory.ToSchemaClass<CGamePlayerEquip>(pEntity);
                uint flags = equipEntity.Spawnflags;

                if ((flags & SF_PLAYEREQUIP_STRIPFIRST) != 0)
                {
                    var players = core.PlayerManager.GetAllValidPlayers();
                    foreach (var player in players)
                    {
                        var pawn = player.PlayerPawn;
                        if (pawn.Valid() && pawn.IsPlayerAlive())
                        {
                            stripFixService.StripPlayerWeapons(pawn);
                        }
                    }
                }
                else if ((flags & SF_PLAYEREQUIP_ONLYSTRIPSAME) != 0)
                {
                    uint entityId = GetEntityUnique(equipEntity);
                    if (s_PlayerEquipMap.TryGetValue(entityId, out var stripSet) && stripSet.Count > 0)
                    {
                        var players = core.PlayerManager.GetAllValidPlayers();
                        foreach (var player in players)
                        {
                            var pawn = player.PlayerPawn;
                            if (pawn.Valid() && pawn.IsPlayerAlive())
                            {
                                stripFixService.StripPlayerWeapons(pawn, stripSet);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogError($"Error in TriggerForAllPlayers: {ex.Message}");
            }
        }

        private void OnInputTriggerForActivatedPlayer(Func<CGamePlayerEquip_InputTriggerForActivatedPlayerDelegate> original, nint pEntity, nint pInput)
        {
            try
            {
                bool shouldCallOriginal = TriggerForActivatedPlayer(pEntity, pInput);
                if (shouldCallOriginal)
                {
                    original()(pEntity, pInput);
                }
            }
            catch (Exception ex)
            {
                logger.LogError($"Error in OnInputTriggerForActivatedPlayer: {ex.Message}");
                original()(pEntity, pInput);
            }
        }

        private bool TriggerForActivatedPlayer(nint pEntity, nint pInput)
        {
            try
            {
                var equipEntity = core.Memory.ToSchemaClass<CGamePlayerEquip>(pEntity);
                var inputData = Marshal.PtrToStructure<InputData_t>(pInput);
                var activator = core.Memory.ToSchemaClass<CBaseEntity>(inputData.pActivator);
                uint flags = equipEntity.Spawnflags;

                if (activator == null || activator.DesignerName == "player")
                    return true;

                var pawn = activator.As<CCSPlayerPawn>();

                if ((flags & SF_PLAYEREQUIP_STRIPFIRST) != 0)
                {
                    stripFixService.StripPlayerWeapons(pawn);
                }
                else if ((flags & SF_PLAYEREQUIP_ONLYSTRIPSAME) != 0)
                {
                    uint entityId = GetEntityUnique(equipEntity);
                    if (s_PlayerEquipMap.TryGetValue(entityId, out var stripSet) && stripSet.Count > 0)
                    {
                        stripFixService.StripPlayerWeapons(pawn, stripSet);
                    }
                }

                var itemServices = pawn.ItemServices;
                if (itemServices == null)
                    return true;

                string weaponName = GetVariantString(inputData.value);
                if (!string.IsNullOrEmpty(weaponName) && weaponName != "(null)")
                {
                    itemServices.GiveItem(weaponName);
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                logger.LogError($"Error in TriggerForActivatedPlayer: {ex.Message}");
                return true;
            }
        }

        private uint GetEntityUnique(CGamePlayerEquip entity)
        {
            try
            {
                string uniqueHammerID = entity.UniqueHammerID;
                if (string.IsNullOrEmpty(uniqueHammerID))
                    return ENTITY_UNIQUE_INVALID;

                uint hash = MurmurHash2.HashStringLowercase(uniqueHammerID, ENTITY_MURMURHASH_SEED);
                return hash;
            }
            catch (Exception ex)
            {
                logger.LogError($"Error in GetEntityUnique: {ex.Message}");
                return ENTITY_UNIQUE_INVALID;
            }
        }

        private string GetVariantString(nint variantPtr)
        {
            if (variantPtr == nint.Zero)
                return string.Empty;

            try
            {
                int type = Marshal.ReadInt32(variantPtr, 20);
                if (type == 14 || type == 2)
                {
                    nint stringPtr = Marshal.ReadIntPtr(variantPtr);
                    if (stringPtr != nint.Zero)
                    {
                        if (type == 14)
                            return Marshal.PtrToStringAnsi(stringPtr) ?? string.Empty;
                        else
                        {
                            nint actualStringPtr = Marshal.ReadIntPtr(stringPtr);
                            if (actualStringPtr != nint.Zero)
                                return Marshal.PtrToStringAnsi(actualStringPtr) ?? string.Empty;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogError($"Error reading variant string: {ex.Message}");
            }

            return string.Empty;
        }
    }
}