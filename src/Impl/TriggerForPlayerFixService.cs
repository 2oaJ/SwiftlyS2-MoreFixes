using Microsoft.Extensions.Logging;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Memory;
using SwiftlyS2.Shared.SchemaDefinitions;
using ZombiEden.CS2.SwiftlyS2.Fixes.Interface;
using static ZombiEden.CS2.SwiftlyS2.Fixes.Sdk.GameTypes;

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
        public string ServiceName => "TriggerForPlayerFix";

        private unsafe delegate void CGamePlayerEquip_InputTriggerForAllPlayersDelegate(nint pEntity, InputData_t* pInput);
        private unsafe delegate void CGamePlayerEquip_InputTriggerForActivatedPlayerDelegate(nint pEntity, InputData_t* pInput);

        private IUnmanagedFunction<CGamePlayerEquip_InputTriggerForAllPlayersDelegate>? _allPlayersHook;
        private IUnmanagedFunction<CGamePlayerEquip_InputTriggerForActivatedPlayerDelegate>? _activatedPlayerHook;
        private Guid? _allPlayersHookId;
        private Guid? _activatedPlayerHookId;

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

            logger.LogInformation($"{ServiceName} uninstalled");
        }

        private unsafe void InstallTriggerForAllPlayersHook()
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
                return (pEntity, pInput) =>
                {
                    OnInputTriggerForAllPlayers(original, pEntity, pInput);
                };
            });
        }

        private unsafe void InstallTriggerForActivatedPlayerHook()
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
                return (pEntity, pInput) =>
                {
                    OnInputTriggerForActivatedPlayer(original, pEntity, pInput);
                };
            });
        }

        private unsafe void OnInputTriggerForAllPlayers(Func<CGamePlayerEquip_InputTriggerForAllPlayersDelegate> original, nint pEntity, InputData_t* pInput)
        {
            var equipEntity = core.Memory.ToSchemaClass<CGamePlayerEquip>(pEntity);
            TriggerForAllPlayers(equipEntity, pInput);
            original()(pEntity, pInput);
        }

        private unsafe void TriggerForAllPlayers(CGamePlayerEquip entity, InputData_t* pInput)
        {
            uint flags = entity.Spawnflags;
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
                var players = core.PlayerManager.GetAllValidPlayers();
                foreach (var player in players)
                {
                    var pawn = player.PlayerPawn;
                    if (pawn.Valid() && pawn.IsPlayerAlive())
                    {
                        stripFixService.StripPlayerSameWeapons(pawn, entity);
                    }
                }
            }
        }

        private unsafe void OnInputTriggerForActivatedPlayer(Func<CGamePlayerEquip_InputTriggerForActivatedPlayerDelegate> original, nint pEntity, InputData_t* pInput)
        {
            var equipEntity = core.Memory.ToSchemaClass<CGamePlayerEquip>(pEntity);
            bool shouldCallOriginal = TriggerForActivatedPlayer(equipEntity, pInput);
            if (shouldCallOriginal)
            {
                original()(pEntity, pInput);
            }
        }

        private unsafe bool TriggerForActivatedPlayer(CGamePlayerEquip entity, InputData_t* pInput)
        {
            var caller = core.EntitySystem.GetEntityByAddress(pInput->pActivator);
            if (caller is not CCSPlayerPawn pawn)
            {
                return true;
            }

            uint flags = entity.Spawnflags;
            if ((flags & SF_PLAYEREQUIP_STRIPFIRST) != 0)
            {
                stripFixService.StripPlayerWeapons(pawn);
            }
            else if ((flags & SF_PLAYEREQUIP_ONLYSTRIPSAME) != 0)
            {
                stripFixService.StripPlayerSameWeapons(pawn, entity);
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
    }
}