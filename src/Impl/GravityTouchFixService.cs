using Microsoft.Extensions.Logging;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Memory;
using SwiftlyS2.Shared.Misc;
using SwiftlyS2.Shared.SchemaDefinitions;
using System.Collections.Concurrent;
using ZombiEden.CS2.SwiftlyS2.Fixes.Interface;

namespace ZombiEden.CS2.SwiftlyS2.Fixes.Impl
{
    /// <summary>
    /// 重力触发修复
    /// 参考 CS2Fixes: CTriggerGravityHandler
    /// </summary>
    public class GravityTouchFixService(
        ISwiftlyCore core,
        ILogger<IGravityTouchFixService> logger) : IGravityTouchFixService
    {
        private delegate void CTriggerGravity_GravityTouchDelegate(nint pEntity, nint pOther);
        private delegate void CTriggerGravity_PrecacheDelegate(nint pEntity, nint pContext);
        private delegate void CTriggerGravity_EndTouchDelegate(nint pEntity, nint pOther);
        private delegate void CBaseEntity_SetGravityScaleDelegate(nint pEntity, float flGravityScale);

        private const uint ENTITY_MURMURHASH_SEED = 0x97984357;
        private const uint ENTITY_UNIQUE_INVALID = ~0u;
        private const float DEFAULT_GRAVITY_SCALE = 1.0f;

        public string ServiceName => "GravityTouchFix";

        private Guid? _gravityTouchHookId;
        private Guid? _precacheHookId;
        private Guid? _endTouchHookId;

        private IUnmanagedFunction<CTriggerGravity_GravityTouchDelegate>? _gravityTouchHook;
        private IUnmanagedFunction<CTriggerGravity_PrecacheDelegate>? _precacheHook;
        private IUnmanagedFunction<CTriggerGravity_EndTouchDelegate>? _endTouchHook;
        private IUnmanagedFunction<CBaseEntity_SetGravityScaleDelegate>? _setGravityScaleFunc;

        // 存储实体 Hash -> 重力值映射（对应 C++ 的 s_gravityMap）
        private readonly ConcurrentDictionary<uint, float> _gravityMap = new();

        #region Install/Uninstall

        public void Install()
        {
            try
            {
                InitializeSetGravityScaleFunction();

                InstallGravityTouchHook();

                InstallPrecacheHook();

                InstallEndTouchHook();

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
            if (_gravityTouchHookId.HasValue && _gravityTouchHook != null)
            {
                _gravityTouchHook.RemoveHook(_gravityTouchHookId.Value);
            }

            if (_precacheHookId.HasValue && _precacheHook != null)
            {
                _precacheHook.RemoveHook(_precacheHookId.Value);
            }

            if (_endTouchHookId.HasValue && _endTouchHook != null)
            {
                _endTouchHook.RemoveHook(_endTouchHookId.Value);
            }

            _gravityMap.Clear();

            logger.LogInformation($"{ServiceName} uninstalled");
        }

        #endregion

        #region Hook Installation

        private void InitializeSetGravityScaleFunction()
        {
            try
            {
                var signature = core.GameData.GetSignature("CBaseEntity::SetGravityScale");
                _setGravityScaleFunc = core.Memory.GetUnmanagedFunctionByAddress<CBaseEntity_SetGravityScaleDelegate>(signature);

                if (_setGravityScaleFunc == null)
                {
                    logger.LogWarning("SetGravityScale function not found");
                }
            }
            catch (Exception ex)
            {
                logger.LogError($"Failed to initialize SetGravityScale: {ex.Message}");
            }
        }

        private void InstallGravityTouchHook()
        {
            var signature = core.GameData.GetSignature("CTriggerGravity::GravityTouch");
            _gravityTouchHook = core.Memory.GetUnmanagedFunctionByAddress<CTriggerGravity_GravityTouchDelegate>(signature);

            if (_gravityTouchHook == null)
            {
                throw new Exception("Failed to find CTriggerGravity::GravityTouch signature");
            }

            _gravityTouchHookId = _gravityTouchHook.AddHook(original =>
            {
                return (nint pEntity, nint pOther) =>
                {
                    if (HandleGravityTouching(pEntity, pOther))
                    {
                        return;
                    }
                    original()(pEntity, pOther);
                };
            });

            logger.LogDebug("GravityTouch hook installed");
        }

        private void InstallPrecacheHook()
        {
            try
            {
                // 获取 CTriggerGravity 虚表
                var vtable = core.Memory.GetVTableAddress("server", "CTriggerGravity");
                if (!vtable.HasValue)
                {
                    logger.LogWarning("Failed to find CTriggerGravity vtable, Precache hook skipped");
                    return;
                }

                // 获取 Precache 偏移
                var offset = core.GameData.GetOffset("CBaseEntity::Precache");
                if (offset == -1)
                {
                    logger.LogWarning("Failed to find CBaseEntity::Precache offset");
                    return;
                }

                // 通过虚表+偏移创建 Hook
                _precacheHook = core.Memory.GetUnmanagedFunctionByVTable<CTriggerGravity_PrecacheDelegate>(vtable.Value, offset);

                if (_precacheHook == null)
                {
                    logger.LogWarning("Failed to create Precache hook function");
                    return;
                }

                _precacheHookId = _precacheHook.AddHook(original =>
                {
                    return (nint pEntity, nint pKeyValues) =>
                    {
                        original()(pEntity, pKeyValues);
                        OnPrecache(pEntity, pKeyValues);
                    };
                });

                logger.LogDebug("Precache hook installed (VTable)");
            }
            catch (Exception ex)
            {
                logger.LogWarning($"Precache hook installation failed (non-critical): {ex.Message}");
            }
        }

        private void InstallEndTouchHook()
        {
            try
            {
                // 获取 CTriggerGravity 虚表
                var vtable = core.Memory.GetVTableAddress("server", "CTriggerGravity");
                if (!vtable.HasValue)
                {
                    logger.LogWarning("Failed to find CTriggerGravity vtable, EndTouch hook skipped");
                    return;
                }

                // 获取 EndTouch 偏移
                var offset = core.GameData.GetOffset("CBaseEntity::EndTouch");
                if (offset == -1)
                {
                    logger.LogWarning("Failed to find CBaseEntity::EndTouch offset");
                    return;
                }

                // 通过虚表+偏移创建 Hook
                _endTouchHook = core.Memory.GetUnmanagedFunctionByVTable<CTriggerGravity_EndTouchDelegate>(vtable.Value, offset);

                if (_endTouchHook == null)
                {
                    logger.LogWarning("Failed to create EndTouch hook function");
                    return;
                }

                _endTouchHookId = _endTouchHook.AddHook(original =>
                {
                    return (nint pEntity, nint pOther) =>
                    {
                        original()(pEntity, pOther);
                        OnEndTouch(pEntity, pOther);
                    };
                });

                logger.LogDebug("EndTouch hook installed (VTable)");
            }
            catch (Exception ex)
            {
                logger.LogWarning($"EndTouch hook installation failed (non-critical): {ex.Message}");
            }
        }

        #endregion

        #region Hook Callbacks

        /// <summary>
        /// Hook_CTriggerGravityPrecache
        /// </summary>
        private void OnPrecache(nint pEntity, nint pContext)
        {
            try
            {
                
                var entity = core.Memory.ToSchemaClass<CBaseEntity>(pEntity);
                core.Scheduler.NextWorldUpdate(() => 
                {
                    if (entity == null || !entity.IsValid)
                        return;

                    var entityId = GetEntityUnique(entity);
                    if (entityId == ENTITY_UNIQUE_INVALID)
                        return;

                    float gravity = entity.GravityScale;

                    if (gravity != 0 && gravity != DEFAULT_GRAVITY_SCALE)
                    {
                        _gravityMap[entityId] = gravity;
                        logger.LogInformation($"Precache: Registered gravity {gravity} for entity {entity.UniqueHammerID} (hash: 0x{entityId:X})");
                    }
                });
            }
            catch (Exception ex)
            {
                logger.LogError($"Error in OnPrecache: {ex.Message}");
            }
        }

        /// <summary>
        /// CTriggerGravityHandler::GravityTouching
        /// </summary>
        private bool HandleGravityTouching(nint pEntity, nint pOther)
        {
            try
            {
                var entity = core.Memory.ToSchemaClass<CBaseEntity>(pEntity);
                var other = core.Memory.ToSchemaClass<CBaseEntity>(pOther);

                var entityId = GetEntityUnique(entity);
                if (entityId == ENTITY_UNIQUE_INVALID)
                    return false;

                if (!_gravityMap.TryGetValue(entityId, out var gravity))
                    return false;

                if (other.DesignerName != "player")
                    return false;
                var pawn = other.As<CCSPlayerPawn>();
                if (!pawn.Valid() || !pawn.IsPlayerAlive())
                    return false;

                SetGravityScale(pawn, gravity);

                logger.LogTrace($"Applied gravity {gravity} to pawn");
                return true;
            }
            catch (Exception ex)
            {
                logger.LogError($"Error in HandleGravityTouching: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// CTriggerGravityHandler::OnEndTouch
        /// </summary>
        private void OnEndTouch(nint pEntity, nint pOther)
        {
            try
            {
                var other = core.Memory.ToSchemaClass<CBaseEntity>(pOther);

                // 只处理玩家
                if (other.DesignerName != "player")
                    return;
                var pawn = other.As<CCSPlayerPawn>();
                if (!pawn.Valid() || !pawn.IsPlayerAlive())
                    return;

                // 恢复默认重力
                SetGravityScale(pawn, DEFAULT_GRAVITY_SCALE);

                logger.LogTrace("Restored default gravity on EndTouch");
            }
            catch (Exception ex)
            {
                logger.LogError($"Error in OnEndTouch: {ex.Message}");
            }
        }

        #endregion

        #region Helper Methods

        private void SetGravityScale(CBaseEntity pEntity, float flGravityScale)
        {
            try
            {
                if (_setGravityScaleFunc == null || pEntity == null || !pEntity.IsValid)
                    return;
                pEntity.ActualGravityScale = flGravityScale;
                _setGravityScaleFunc.Call(pEntity.Address, flGravityScale);
            }
            catch (Exception ex)
            {
                logger.LogError($"Error in SetGravityScale: {ex.Message}");
            }
        }

        private uint GetEntityUnique(CBaseEntity entity)
        {
            try
            {
                string uniqueHammerID = entity.UniqueHammerID;
                if (string.IsNullOrEmpty(uniqueHammerID))
                    return ENTITY_UNIQUE_INVALID;

                return MurmurHash2.HashStringLowercase(uniqueHammerID, ENTITY_MURMURHASH_SEED);
            }
            catch (Exception ex)
            {
                logger.LogError($"Error in GetEntityUnique: {ex.Message}");
                return ENTITY_UNIQUE_INVALID;
            }
        }

        #endregion

        #region Public API

        public void SetEntityGravity(uint entityId, float gravity)
        {
            _gravityMap[entityId] = gravity;
            logger.LogInformation($"Manually set gravity {gravity} for entity ID 0x{entityId:X}");
        }

        public void RemoveEntityGravity(uint entityId)
        {
            if (_gravityMap.TryRemove(entityId, out var removedGravity))
            {
                logger.LogInformation($"Removed gravity {removedGravity} for entity ID 0x{entityId:X}");
            }
        }

        public float? GetEntityGravity(uint entityId)
        {
            return _gravityMap.TryGetValue(entityId, out var gravity) ? gravity : null;
        }

        public void ClearAllGravity()
        {
            var count = _gravityMap.Count;
            _gravityMap.Clear();
            logger.LogInformation($"Cleared {count} gravity entries");
        }

        #endregion
    }
}
