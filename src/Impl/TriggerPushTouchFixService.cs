using Microsoft.Extensions.Logging;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Convars;
using SwiftlyS2.Shared.Events;
using SwiftlyS2.Shared.Memory;
using SwiftlyS2.Shared.Natives;
using SwiftlyS2.Shared.SchemaDefinitions;
using ZombiEden.CS2.SwiftlyS2.Fixes.Interface;

namespace ZombiEden.CS2.SwiftlyS2.Fixes.Impl
{
    /// <summary>
    /// TriggerPush触摸修复实现
    /// </summary>
    public class TriggerPushTouchFixService(
        ISwiftlyCore core) : ITriggerPushTouchFixService
    {
        private delegate void TriggerPushTouchContext(nint push, nint other);

        public string ServiceName => "TriggerPushTouchFix";

        private readonly ILogger _logger = core.Logger;
        private readonly IConVar<bool> _cvarUseOldPush = core.ConVar.CreateOrFind("cs2f_use_old_push", "使用csgo push", false, ConvarFlags.SERVER_CAN_EXECUTE);
        private bool _useOldPush;

        private Guid? _hookId;
        private IUnmanagedFunction<TriggerPushTouchContext>? _hook;

        public unsafe delegate bool PassesTriggerFiltersDelegate(nint trigger, nint entity);
        private IUnmanagedFunction<PassesTriggerFiltersDelegate>? _passesTriggerFiltersObject;

        public void Install()
        {
            try
            {
                var function = core.Memory.GetUnmanagedFunctionByAddress<TriggerPushTouchContext>(
                    core.GameData.GetSignature("TriggerPush_Touch"));

                _hookId = function.AddHook((next) => (push, other) =>
                {
                    var push1 = core.Memory.ToSchemaClass<CTriggerPush>(push);
                    var other1 = core.Memory.ToSchemaClass<CBaseEntity>(other);
                    if (push1 == null || !push1.IsValid || other1 == null || !other1.IsValid)
                    {
                        next()(push, other);
                        return;
                    }

                    ProcessTriggerPushTouch(push1, other1, next);
                });

                _hook = function;
                _useOldPush = _cvarUseOldPush.Value;

                core.Event.OnMapUnload += OnMapUnload;
                core.Event.OnConVarValueChanged += OnConVarValueChanged;

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

            core.Event.OnMapUnload -= OnMapUnload;
            core.Event.OnConVarValueChanged -= OnConVarValueChanged;
        }

        private void ProcessTriggerPushTouch(CTriggerPush pPush, CBaseEntity pOther, Func<TriggerPushTouchContext> next)
        {
            uint spawnFlags = pPush.Spawnflags;
            bool isPushOnce = (spawnFlags & 0x80) != 0;
            bool triggerOnStartTouch = pPush.TriggerOnStartTouch;

            if (!_useOldPush || isPushOnce || triggerOnStartTouch)
            {
                next()(pPush.Address, pOther.Address);
                return;
            }

            MoveType_t moveType = pOther.ActualMoveType;

            if (moveType == MoveType_t.MOVETYPE_VPHYSICS)
            {
                next()(pPush.Address, pOther.Address);
                return;
            }

            if (moveType == MoveType_t.MOVETYPE_NONE ||
                moveType == MoveType_t.MOVETYPE_PUSH ||
                moveType == MoveType_t.MOVETYPE_NOCLIP)
                return;

            var collisionProp = pOther.Collision;
            if (collisionProp == null)
                return;

            const int FSOLID_NOT_SOLID = 0x0004;
            const SolidType_t SOLID_NONE = SolidType_t.SOLID_NONE;

            var solidType = collisionProp.SolidType;
            var solidFlags = collisionProp.SolidFlags;

            if (solidType == SOLID_NONE || (solidFlags & FSOLID_NOT_SOLID) != 0)
                return;

            if (!PassesTriggerFilters(pPush, pOther))
                return;

            var sceneNode = pOther.CBodyComponent?.SceneNode;
            if (sceneNode == null || sceneNode.Parent != null)
                return;

            var vecPushDir = pPush.PushDirEntitySpace;
            var matTransform = pPush.CBodyComponent!.SceneNode!.EntityToWorldTransform();

            VectorRotate(vecPushDir, matTransform, out Vector vecAbsDir);

            float speed = pPush.Speed;
            var vecPush = vecAbsDir * speed;

            var flags = (Flags_t)pOther.Flags;
            if ((flags & Flags_t.FL_BASEVELOCITY) != 0)
            {
                vecPush += pOther.BaseVelocity;
            }

            if (vecPush.Z > 0 && (flags & Flags_t.FL_ONGROUND) != 0)
            {
                pOther.SetGroundEntity(null);
                var origin = pOther.AbsOrigin!.Value;
                var newOrigin = new Vector(origin.X, origin.Y, origin.Z + 1.0f);
                pOther.Teleport(newOrigin, null, null);
            }

            pOther.BaseVelocity = vecPush;
            pOther.BaseVelocityUpdated();

            pOther.Flags = (uint)(flags | Flags_t.FL_BASEVELOCITY);
            pOther.FlagsUpdated();
        }

        public bool PassesTriggerFilters(CBaseTrigger trigger, CBaseEntity entity)
        {
            var vtable = core.Memory.GetVTableAddress("server", "CBaseTrigger");
            if (vtable is null)
                return false;

            _passesTriggerFiltersObject = core.Memory.GetUnmanagedFunctionByVTable<PassesTriggerFiltersDelegate>(
                vtable.Value,
                core.GameData.GetOffset("CBaseTrigger::PassesTriggerFilters"));

            return _passesTriggerFiltersObject?.Call(trigger.Address, entity.Address) ?? false;
        }

        private void OnMapUnload(IOnMapUnloadEvent @event)
        {
            _cvarUseOldPush.Value = _cvarUseOldPush.DefaultValue;
        }

        private void OnConVarValueChanged(IOnConVarValueChanged @event)
        {
            if (@event.ConVarName == "cs2f_use_old_push")
            {
                _useOldPush = bool.Parse(@event.NewValue);
            }
        }

        private static void VectorRotate(in Vector inVec, in matrix3x4_t matrix, out Vector outVec)
        {
            outVec = new Vector
            {
                X = inVec.X * matrix[0, 0] + inVec.Y * matrix[0, 1] + inVec.Z * matrix[0, 2],
                Y = inVec.X * matrix[1, 0] + inVec.Y * matrix[1, 1] + inVec.Z * matrix[1, 2],
                Z = inVec.X * matrix[2, 0] + inVec.Y * matrix[2, 1] + inVec.Z * matrix[2, 2]
            };
        }
    }
}