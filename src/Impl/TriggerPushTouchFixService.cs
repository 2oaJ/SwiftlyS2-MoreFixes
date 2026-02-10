using Microsoft.Extensions.Logging;
using Mono.Cecil.Cil;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Convars;
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
        private readonly IConVar<bool> _useOldPush = core.ConVar.CreateOrFind("cs2f_use_old_push", "使用csgo push", false, ConvarFlags.SERVER_CAN_EXECUTE);

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
                    ProcessTriggerPushTouch(push1, other1, next);
                });

                _hook = function;
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

        private void ProcessTriggerPushTouch(CTriggerPush pPush, CBaseEntity pOther, Func<TriggerPushTouchContext> next)
        {
            bool useOldPush = _useOldPush.Value;
            uint spawnFlags = pPush.Spawnflags;
            bool isPushOnce = (spawnFlags & 0x80) != 0;
            bool triggerOnStartTouch = pPush.TriggerOnStartTouch;

            if (!useOldPush || isPushOnce || triggerOnStartTouch)
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

            VectorRotateSafe(vecPushDir, matTransform, out Vector vecAbsDir);

            float speed = pPush.Speed;
            var vecPush = vecAbsDir * speed;

            uint flags = pOther.Flags;
            if ((flags & EntityFlags.FL_BASEVELOCITY) != 0)
            {
                var baseVelocity = pOther.BaseVelocity;
                vecPush += baseVelocity;
            }

            if (vecPush.Z > 0 && (flags & EntityFlags.FL_ONGROUND) != 0)
            {
                pOther.GroundEntity.Value = null;
                var origin = pOther.AbsOrigin!.Value;
                var newOrigin = new Vector(origin.X, origin.Y, origin.Z + 1.0f);
                pOther.Teleport(newOrigin, null, null);
            }

            pOther.BaseVelocity = vecPush;
            pOther.Flags = flags | (uint)EntityFlags.FL_BASEVELOCITY;
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

        private static void VectorRotateSafe(Vector inVec, matrix3x4_t matrix, out Vector outVec)
        {
            outVec = new Vector
            {
                X = inVec.X * matrix[0, 0] + inVec.Y * matrix[0, 1] + inVec.Z * matrix[0, 2],
                Y = inVec.X * matrix[1, 0] + inVec.Y * matrix[1, 1] + inVec.Z * matrix[1, 2],
                Z = inVec.X * matrix[2, 0] + inVec.Y * matrix[2, 1] + inVec.Z * matrix[2, 2]
            };
        }

        public static class EntityFlags
        {
            public const uint FL_ONGROUND = (1 << 0);     // At rest / on the ground
            public const uint FL_DUCKING = (1 << 1);      // Player flag -- Player is fully crouched
            public const uint FL_WATERJUMP = (1 << 2);    // player jumping out of water
            public const uint FL_NOCLIP = (1 << 3);       // Forces MOVETYPE_NOCLIP on the entity
            public const uint FL_PAWN_FAKECLIENT = (1 << 4); // Fake client controlled pawn entity
            public const uint FL_FROZEN = (1 << 5);       // Player is frozen for 3rd person camera
            public const uint FL_ATCONTROLS = (1 << 6);   // Player can't move, but keeps key inputs
            public const uint FL_CLIENT = (1 << 7);       // Is a player
            public const uint FL_CONTROLLER_FAKECLIENT = (1 << 8); // Fake client, simulated server side
            public const uint FL_INWATER = (1 << 9);      // In water

            // NON-PLAYER SPECIFIC
            public const uint FL_FLY = (1 << 10);         // Changes SV_Movestep() behavior
            public const uint FL_SWIM = (1 << 11);        // Changes SV_Movestep() behavior
            public const uint FL_CONVEYOR = (1 << 12);    // Potentially obsolete in s2
            public const uint FL_NPC = (1 << 13);         // Potentially obsolete in s2
            public const uint FL_GODMODE = (1 << 14);
            public const uint FL_NOTARGET = (1 << 15);
            public const uint FL_AIMTARGET = (1 << 16);   // crosshair needs to aim onto the entity
            public const uint FL_PARTIALGROUND = (1 << 17); // not all corners are valid
            public const uint FL_STATICPROP = (1 << 18);  // Eetsa static prop!
            public const uint FL_GRAPHED = (1 << 19);     // worldgraph blocks connection
            public const uint FL_GRENADE = (1 << 20);
            public const uint FL_STEPMOVEMENT = (1 << 21); // Changes SV_Movestep() behavior
            public const uint FL_DONTTOUCH = (1 << 22);   // Doesn't generate touch functions
            public const uint FL_BASEVELOCITY = (1 << 23); // Base velocity has been applied this frame
            public const uint FL_CONVEYOR_NEW = (1 << 24);
            public const uint FL_OBJECT = (1 << 25);      // Object that NPCs should see
            public const uint FL_KILLME = (1 << 26);      // Marked for death
            public const uint FL_ONFIRE = (1 << 27);      // You know...
            public const uint FL_DISSOLVING = (1 << 28);  // We're dissolving!
            public const uint FL_TRANSRAGDOLL = (1 << 29); // Turning into client side ragdoll
            public const uint FL_UNBLOCKABLE_BY_PLAYER = (1 << 30); // pusher can't be blocked by player
                                                                    //public const uint FL_FREEZING = (1 << 31);    // We're becoming frozen!
        }
    }
}