using Microsoft.Extensions.Logging;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Convars;
using SwiftlyS2.Shared.Events;
using SwiftlyS2.Shared.GameHooks;
using SwiftlyS2.Shared.Misc;
using SwiftlyS2.Shared.Natives;
using SwiftlyS2.Shared.Players;
using SwiftlyS2.Shared.SchemaDefinitions;
using ZombiEden.CS2.SwiftlyS2.Fixes.Interface;

namespace ZombiEden.CS2.SwiftlyS2.Fixes.Impl
{
    /// <summary>
    /// game_ui 修复服务。
    /// </summary>
    public class GameUIFixService(
        ISwiftlyCore core,
        ILogger<GameUIFixService> logger) : IGameUIFixService
    {
        private const string EnableConVarName = "sw_gameuifix_enable";

        private const uint SpawnFlagFreezePlayer = 0x0020;
        private const uint SpawnFlagJumpDeactivate = 0x0100;

        private const ulong ButtonAttack = 0x1;
        private const ulong ButtonJump = 0x2;
        private const ulong ButtonDuck = 0x4;
        private const ulong ButtonForward = 0x8;
        private const ulong ButtonBack = 0x10;
        private const ulong ButtonUse = 0x20;
        private const ulong ButtonMoveLeft = 0x200;
        private const ulong ButtonMoveRight = 0x400;
        private const ulong ButtonAttack2 = 0x800;
        private const ulong ButtonSpeed = 0x10000;

        public string ServiceName => "GameUIFix";

        private readonly Dictionary<uint, GameUIRuntimeState> _trackedGameUis = new();

        private IConVar<bool>? _enableConVar;
        private bool _enabled;
        private bool _installed;
        private bool _hooksAttached;
        private int _worldUpdateCounter;

        private sealed class GameUIRuntimeState
        {
            public required CHandle<CBaseEntity> EntityHandle { get; init; }

            public required int PlayerId { get; init; }

            public required ulong ButtonSnapshot { get; set; }

            public required bool FreezeApplied { get; init; }

            public bool PendingDeactivate { get; set; }
        }

        public void Install()
        {
            try
            {
                if (_installed)
                {
                    logger.LogWarning("{ServiceName} 已安装，跳过重复安装。", ServiceName);
                    return;
                }

                _enableConVar = core.ConVar.CreateOrFind(
                    EnableConVarName,
                    "启用 game_ui 修复",
                    false,
                    ConvarFlags.SERVER_CAN_EXECUTE);

                _enabled = _enableConVar.Value;
                core.Event.OnConVarValueChanged += OnConVarValueChanged;
                UpdateHooks();

                _installed = true;
                logger.LogInformation("{ServiceName} 安装完成，当前启用状态: {Enabled}", ServiceName, _enabled);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "安装 {ServiceName} 失败。", ServiceName);
                throw;
            }
        }

        public void Uninstall()
        {
            if (!_installed)
            {
                return;
            }

            try
            {
                core.Event.OnConVarValueChanged -= OnConVarValueChanged;
                DetachHooks();
                ClearAllTrackedStates();
                _installed = false;
                logger.LogInformation("{ServiceName} 已卸载。", ServiceName);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "卸载 {ServiceName} 失败。", ServiceName);
            }
        }

        private void OnConVarValueChanged(IOnConVarValueChanged @event)
        {
            if (_enableConVar is null || @event.ConVarName != _enableConVar.Name)
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
                logger.LogWarning(ex, "{ServiceName} 收到无法解析的 ConVar 值: {Value}", ServiceName, @event.NewValue);
                return;
            }

            if (_enabled == newValue)
            {
                return;
            }

            _enabled = newValue;
            logger.LogInformation("{ServiceName} 开关切换为 {Enabled}", ServiceName, _enabled);
            UpdateHooks();

            if (!_enabled)
            {
                ClearAllTrackedStates();
            }
        }

        private void UpdateHooks()
        {
            if (_enabled)
            {
                AttachHooks();
            }
            else
            {
                DetachHooks();
            }
        }

        private void AttachHooks()
        {
            if (_hooksAttached)
            {
                return;
            }

            core.Event.OnWorldUpdate += OnWorldUpdate;
            core.Event.OnMapUnload += OnMapUnload;
            core.Event.OnClientDisconnected += OnClientDisconnected;

            core.GameHooks.Entities.AcceptInput.Pre += OnEntityIdentityAcceptInputHook;

            _hooksAttached = true;
        }

        private void DetachHooks()
        {
            if (!_hooksAttached)
            {
                return;
            }

            core.Event.OnWorldUpdate -= OnWorldUpdate;
            core.Event.OnMapUnload -= OnMapUnload;
            core.Event.OnClientDisconnected -= OnClientDisconnected;

            core.GameHooks.Entities.AcceptInput.Pre -= OnEntityIdentityAcceptInputHook;

            _hooksAttached = false;
            _worldUpdateCounter = 0;
        }

        private void OnEntityIdentityAcceptInputHook(ref AcceptInputEntityPreContext ctx)
        {
            try
            {
                if (!_enabled)
                {
                    return;
                }

                var entityInstance = ctx.Params.EntityInstance;
                if (!IsGameUiEntity(entityInstance))
                {
                    return;
                }

                var inputName = ctx.Params.InputName;
                if (string.Equals(inputName, "Activate", StringComparison.OrdinalIgnoreCase))
                {
                    if (TryHandleActivate(entityInstance, ctx.Params.Activator))
                    {
                        ctx.SetHookResult(HookResult.Stop);
                    }
                }
                else if (string.Equals(inputName, "Deactivate", StringComparison.OrdinalIgnoreCase))
                {
                    if (TryHandleDeactivate(entityInstance))
                    {
                        ctx.SetHookResult(HookResult.Stop);
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "{ServiceName} 处理 AcceptInput Hook 时发生异常。", ServiceName);
            }
        }

        private void OnWorldUpdate()
        {
            if (!_enabled || _trackedGameUis.Count == 0)
            {
                return;
            }

            _worldUpdateCounter++;
            if ((_worldUpdateCounter & 0b11) != 0)
            {
                return;
            }

            ProcessTrackedGameUis();
        }

        private void OnMapUnload(IOnMapUnloadEvent @event)
        {
            ClearAllTrackedStates();
        }

        private void OnClientDisconnected(IOnClientDisconnectedEvent @event)
        {
            ClearTrackedStatesByPlayerId(@event.PlayerId);
        }

        private bool TryHandleActivate(CEntityInstance entityInstance, CEntityInstance? activator)
        {
            if (!TryResolveActivatorPawn(activator, out var pawn))
            {
                return false;
            }

            if (!pawn.IsPlayerAlive())
            {
                return false;
            }

            var controller = pawn.Controller.Value;
            if (controller is null || !controller.IsValid)
            {
                return false;
            }

            int playerId = (int)(controller.Index - 1);
            if (!TryReadButtonSnapshot(pawn, out var buttons, out _))
            {
                return false;
            }

            var baseEntity = entityInstance.As<CBaseEntity>();
            if (baseEntity is null || !baseEntity.IsValid)
            {
                return false;
            }

            var entityHandle = core.EntitySystem.GetRefEHandle(baseEntity);
            if (!entityHandle.IsValid)
            {
                return false;
            }

            var entityKey = entityHandle.Raw;

            ReleaseTrackedState(entityKey, sendPlayerOff: true, entityOverride: baseEntity);
            ReleaseTrackedStatesForPlayer(playerId, exceptEntityKey: entityKey);

            uint spawnFlags = baseEntity.Spawnflags;
            bool shouldFreeze = (spawnFlags & SpawnFlagFreezePlayer) != 0;
            if (shouldFreeze)
            {
                pawn.Flags |= EntityFlagAtControls;
                pawn.FlagsUpdated();
            }

            QueueEntityInput(baseEntity, "InValue", pawn, "PlayerOn");

            _trackedGameUis[entityKey] = new GameUIRuntimeState
            {
                EntityHandle = entityHandle,
                PlayerId = playerId,
                ButtonSnapshot = buttons & ~ButtonUse,
                FreezeApplied = shouldFreeze,
                PendingDeactivate = false
            };

            return true;
        }

        private bool TryHandleDeactivate(CEntityInstance entityInstance)
        {
            var baseEntity = entityInstance.As<CBaseEntity>();
            if (baseEntity is null || !baseEntity.IsValid)
            {
                return false;
            }

            var entityHandle = core.EntitySystem.GetRefEHandle(baseEntity);
            var entityKey = entityHandle.Raw;
            if (!_trackedGameUis.TryGetValue(entityKey, out _))
            {
                return false;
            }

            ReleaseTrackedState(entityKey, sendPlayerOff: true, entityOverride: baseEntity);
            return true;
        }

        private void ProcessTrackedGameUis()
        {
            if (_trackedGameUis.Count == 0)
            {
                return;
            }

            var pendingRelease = new List<uint>();

            foreach (var pair in _trackedGameUis)
            {
                var entityKey = pair.Key;
                var state = pair.Value;
                var entity = state.EntityHandle.Value;
                if (entity is null || !IsGameUiEntity(entity))
                {
                    pendingRelease.Add(entityKey);
                    continue;
                }

                if (state.PendingDeactivate)
                {
                    continue;
                }

                if (!TryResolveTrackedPlayer(state.PlayerId, out _, out var pawn))
                {
                    QueueDeactivate(state, entity, null);
                    continue;
                }

                if (!pawn.IsPlayerAlive())
                {
                    QueueDeactivate(state, entity, pawn);
                    continue;
                }

                if (!TryReadButtonSnapshot(pawn, out var buttons0, out var buttons2))
                {
                    QueueDeactivate(state, entity, pawn);
                    continue;
                }

                uint spawnFlags = entity.Spawnflags;
                if ((spawnFlags & SpawnFlagJumpDeactivate) != 0 && ((buttons0 & ButtonJump) != 0 || (buttons2 & ButtonJump) != 0))
                {
                    QueueDeactivate(state, entity, pawn);
                    continue;
                }

                DispatchButtonTransitions(entity, pawn, state.ButtonSnapshot, buttons0);
                state.ButtonSnapshot = buttons0;
            }

            for (int i = 0; i < pendingRelease.Count; i++)
            {
                ReleaseTrackedState(pendingRelease[i], sendPlayerOff: false);
            }
        }

        private void QueueDeactivate(GameUIRuntimeState state, CBaseEntity entity, CCSPlayerPawn? activator)
        {
            if (state.PendingDeactivate)
            {
                return;
            }

            state.PendingDeactivate = true;
            QueueEntityInput(entity, "Deactivate", activator);
        }

        private void DispatchButtonTransitions(CBaseEntity entity, CCSPlayerPawn pawn, ulong previousButtons, ulong currentButtons)
        {
            var changedButtons = previousButtons ^ currentButtons;
            if (changedButtons == 0)
            {
                return;
            }

            DispatchButtonTransition(entity, pawn, changedButtons, previousButtons, ButtonForward, "PressedForward", "UnpressedForward");
            DispatchButtonTransition(entity, pawn, changedButtons, previousButtons, ButtonMoveLeft, "PressedMoveLeft", "UnpressedMoveLeft");
            DispatchButtonTransition(entity, pawn, changedButtons, previousButtons, ButtonBack, "PressedBack", "UnpressedBack");
            DispatchButtonTransition(entity, pawn, changedButtons, previousButtons, ButtonMoveRight, "PressedMoveRight", "UnpressedMoveRight");
            DispatchButtonTransition(entity, pawn, changedButtons, previousButtons, ButtonAttack, "PressedAttack", "UnpressedAttack");
            DispatchButtonTransition(entity, pawn, changedButtons, previousButtons, ButtonAttack2, "PressedAttack2", "UnpressedAttack2");
            DispatchButtonTransition(entity, pawn, changedButtons, previousButtons, ButtonSpeed, "PressedSpeed", "UnpressedSpeed");
            DispatchButtonTransition(entity, pawn, changedButtons, previousButtons, ButtonDuck, "PressedDuck", "UnpressedDuck");
        }

        private static void DispatchButtonTransition(
            CBaseEntity entity,
            CCSPlayerPawn pawn,
            ulong changedButtons,
            ulong previousButtons,
            ulong targetButton,
            string pressedValue,
            string unpressedValue)
        {
            if ((changedButtons & targetButton) == 0)
            {
                return;
            }

            var value = (previousButtons & targetButton) != 0 ? unpressedValue : pressedValue;
            entity.AcceptInput("InValue", value, pawn, entity, 0);
        }

        private static bool TryResolveActivatorPawn(CEntityInstance? activator, out CCSPlayerPawn pawn)
        {
            pawn = null!;

            if (activator is null || !activator.IsValid || activator.DesignerName != "player")
            {
                return false;
            }

            var resolvedPawn = activator.As<CCSPlayerPawn>();
            if (resolvedPawn is null || !resolvedPawn.IsValid)
            {
                return false;
            }

            pawn = resolvedPawn;
            return true;
        }

        private static bool IsGameUiEntity(CEntityInstance? entityInstance)
        {
            if (entityInstance is null || !entityInstance.IsValid)
            {
                return false;
            }

            var designerName = entityInstance.DesignerName;
            if (string.Equals(designerName, "game_ui", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (!string.Equals(designerName, "logic_case", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var privateVScripts = entityInstance.PrivateVScripts;
            return !string.IsNullOrWhiteSpace(privateVScripts)
                && privateVScripts.Contains("game_ui", StringComparison.OrdinalIgnoreCase);
        }

        private bool TryResolveTrackedPlayer(int playerId, out IPlayer player, out CCSPlayerPawn pawn)
        {
            player = null!;
            pawn = null!;

            var resolvedPlayer = core.PlayerManager.GetPlayer(playerId);
            if (resolvedPlayer is null || !resolvedPlayer.IsValid)
            {
                return false;
            }

            var resolvedPawn = resolvedPlayer.PlayerPawn;
            if (resolvedPawn is null || !resolvedPawn.Valid())
            {
                return false;
            }

            player = resolvedPlayer;
            pawn = resolvedPawn;

            return true;
        }

        private static bool TryReadButtonSnapshot(CCSPlayerPawn pawn, out ulong buttons0, out ulong buttons2)
        {
            buttons0 = 0;
            buttons2 = 0;

            var buttonStates = pawn.MovementServices?.Buttons?.ButtonStates;
            if (buttonStates is null)
            {
                return false;
            }

            buttons0 = buttonStates[0];
            buttons2 = buttonStates[2];
            return true;
        }

        private static void TryRestorePlayerFlags(CCSPlayerPawn pawn, bool clearAtControls)
        {
            if (pawn is null || !pawn.IsValid || !clearAtControls)
            {
                return;
            }

            pawn.Flags &= ~EntityFlagAtControls;
            pawn.FlagsUpdated();
        }

        private void ClearTrackedStatesByPlayerId(int playerId)
        {
            if (_trackedGameUis.Count == 0)
            {
                return;
            }

            var entityKeys = _trackedGameUis
                .Where(pair => pair.Value.PlayerId == playerId)
                .Select(pair => pair.Key)
                .ToArray();

            for (int i = 0; i < entityKeys.Length; i++)
            {
                ReleaseTrackedState(entityKeys[i], sendPlayerOff: false);
            }
        }

        private void ClearAllTrackedStates()
        {
            if (_trackedGameUis.Count == 0)
            {
                return;
            }

            foreach (var state in _trackedGameUis.Values)
            {
                if (state.FreezeApplied && TryResolveTrackedPlayer(state.PlayerId, out _, out var pawn))
                {
                    TryRestorePlayerFlags(pawn, clearAtControls: true);
                }
            }

            _trackedGameUis.Clear();
            _worldUpdateCounter = 0;
        }

        private void ReleaseTrackedStatesForPlayer(int playerId, uint exceptEntityKey)
        {
            if (_trackedGameUis.Count == 0)
            {
                return;
            }

            var entityKeys = _trackedGameUis
                .Where(pair => pair.Value.PlayerId == playerId && pair.Key != exceptEntityKey)
                .Select(pair => pair.Key)
                .ToArray();

            for (int i = 0; i < entityKeys.Length; i++)
            {
                ReleaseTrackedState(entityKeys[i], sendPlayerOff: true);
            }
        }

        private void ReleaseTrackedState(uint entityKey, bool sendPlayerOff, CBaseEntity? entityOverride = null)
        {
            if (!_trackedGameUis.TryGetValue(entityKey, out var state))
            {
                return;
            }

            var entity = entityOverride;
            if (entity is null)
            {
                entity = state.EntityHandle.Value;
            }

            if (TryResolveTrackedPlayer(state.PlayerId, out _, out var pawn))
            {
                if (state.FreezeApplied)
                {
                    TryRestorePlayerFlags(pawn, clearAtControls: true);
                }

                if (sendPlayerOff && entity is not null && entity.IsValid)
                {
                    QueueEntityInput(entity, "InValue", pawn, "PlayerOff");
                }
            }

            _trackedGameUis.Remove(entityKey);
        }

        private void QueueEntityInput(CBaseEntity entity, string inputName, CBaseEntity? activator = null, string value = "")
        {
            core.Scheduler.NextTick(() =>
            {
                try
                {
                    if (entity is null || !entity.IsValid)
                    {
                        return;
                    }

                    if (activator is not null && activator.IsValid)
                    {
                        entity.AcceptInput(inputName, value, activator, entity, 0);
                    }
                    else
                    {
                        entity.AcceptInput(inputName, value, null, entity, 0);
                    }
                }
                catch (Exception ex)
                {
                    logger.LogDebug(ex, "{ServiceName} 延迟执行实体输入 {InputName} 失败。", ServiceName, inputName);
                }
            });
        }

        private const uint EntityFlagAtControls = 1 << 6;
    }
}