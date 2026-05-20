using Microsoft.Extensions.Logging;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Convars;
using SwiftlyS2.Shared.Events;
using SwiftlyS2.Shared.Memory;
using SwiftlyS2.Shared.Natives;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using ZombiEden.CS2.SwiftlyS2.Fixes.Interface;

namespace ZombiEden.CS2.SwiftlyS2.Fixes.Impl
{
    /// <summary>
    /// 打乱物理 touching list 中仍在 touching 的玩家处理顺序，规避固定排序导致的碰撞/挤压偏置。
    /// 参考: https://github.com/Source2ZE/CS2Fixes/blob/461c691/src/cs2fixes.cpp
    /// </summary>
    public unsafe sealed class ShufflePlayerPhysicsSimFixService(
        ISwiftlyCore core,
        ILogger<ShufflePlayerPhysicsSimFixService> logger) : IShufflePlayerPhysicsSimFixService
    {
        private delegate void CVPhys2World_GetTouchingListDelegate(nint world, CUtlVector<TouchLinked>* list, bool unknown);

        private const string EnableConVarName = "sw_shuffle_player_physics_sim";
        private const string OffsetName = "CVPhys2World::GetTouchingList";
        private const int UntouchFlag = 0x10;

        private IConVar<bool>? _enableConVar;
        private Guid? _hookId;
        private IUnmanagedFunction<CVPhys2World_GetTouchingListDelegate>? _hook;
        private TouchLinked[] _touchingLinks = [];
        private TouchLinked[] _untouchLinks = [];
        private uint _randomState = (uint)Environment.TickCount;
        private bool _enabled;
        private bool _installed;
        private bool _callbackFailureLogged;

        public string ServiceName => "ShufflePlayerPhysicsSimFix";

        public void Install()
        {
            try
            {
                if (_installed)
                {
                    logger.LogWarning("{ServiceName} 已安装，跳过重复安装。", ServiceName);
                    return;
                }

                ValidateTouchLinkedLayout();

                _enableConVar = core.ConVar.CreateOrFind(
                    EnableConVarName,
                    "启用物理 touching list 随机排序，规避玩家碰撞/挤压处理偏置",
                    false,
                    ConvarFlags.SERVER_CAN_EXECUTE);

                _enabled = _enableConVar.Value;
                core.Event.OnConVarValueChanged += OnConVarValueChanged;
                UpdateHook();

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
                DetachHook();
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
            UpdateHook();
            logger.LogInformation("{ServiceName} 开关切换为 {Enabled}", ServiceName, _enabled);
        }

        private void UpdateHook()
        {
            if (_enabled)
            {
                AttachHook();
            }
            else
            {
                DetachHook();
            }
        }

        private void AttachHook()
        {
            if (_hookId.HasValue)
            {
                return;
            }

            try
            {
                var vtable = core.Memory.GetVTableAddress("vphysics2", "CVPhys2World");
                if (!vtable.HasValue)
                {
                    throw new InvalidOperationException("无法找到 CVPhys2World vtable。");
                }

                var offset = core.GameData.GetOffset(OffsetName);
                if (offset == -1)
                {
                    throw new InvalidOperationException($"无法找到 {OffsetName} offset。");
                }

                _hook = core.Memory.GetUnmanagedFunctionByVTable<CVPhys2World_GetTouchingListDelegate>(vtable.Value, offset);
                if (_hook is null)
                {
                    throw new InvalidOperationException($"无法创建 {OffsetName} hook。");
                }

                _hookId = _hook.AddHook(original =>
                {
                    return (world, list, unknown) =>
                    {
                        original()(world, list, unknown);
                        ShuffleTouchingList(list);
                    };
                });

                logger.LogInformation("{ServiceName} hook 已安装。", ServiceName);
            }
            catch (Exception ex)
            {
                DetachHook();
                logger.LogError(ex, "安装 {ServiceName} hook 失败，功能保持未启用。", ServiceName);
            }
        }

        private void DetachHook()
        {
            if (!_hookId.HasValue || _hook is null)
            {
                _hookId = null;
                _hook = null;
                return;
            }

            try
            {
                _hook.RemoveHook(_hookId.Value);
                logger.LogInformation("{ServiceName} hook 已卸载。", ServiceName);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "卸载 {ServiceName} hook 失败。", ServiceName);
            }
            finally
            {
                _hookId = null;
                _hook = null;
            }
        }

        private void ShuffleTouchingList(CUtlVector<TouchLinked>* list)
        {
            try
            {
                if (list is null || list->Count <= 1)
                {
                    return;
                }

                var count = list->Count;
                EnsureBufferCapacity(count);

                var touchingCount = 0;
                var untouchCount = 0;

                for (var i = 0; i < count; i++)
                {
                    var link = (*list)[i];
                    if (link.IsUntouching)
                    {
                        _untouchLinks[untouchCount++] = link;
                    }
                    else
                    {
                        _touchingLinks[touchingCount++] = link;
                    }
                }

                if (touchingCount <= 1)
                {
                    return;
                }

                Shuffle(_touchingLinks, touchingCount);

                list->RemoveAll();
                for (var i = 0; i < touchingCount; i++)
                {
                    list->AddToTail(_touchingLinks[i]);
                }

                for (var i = 0; i < untouchCount; i++)
                {
                    list->AddToTail(_untouchLinks[i]);
                }
            }
            catch (Exception ex)
            {
                if (_callbackFailureLogged)
                {
                    return;
                }

                _callbackFailureLogged = true;
                logger.LogError(ex, "{ServiceName} 处理 touching list 时发生异常，后续同类异常不再逐帧记录。", ServiceName);
            }
        }

        private void EnsureBufferCapacity(int count)
        {
            if (_touchingLinks.Length < count)
            {
                Array.Resize(ref _touchingLinks, count);
            }

            if (_untouchLinks.Length < count)
            {
                Array.Resize(ref _untouchLinks, count);
            }
        }

        private void Shuffle(TouchLinked[] links, int count)
        {
            for (var i = count - 1; i > 0; i--)
            {
                var j = (int)(NextRandom() % (uint)(i + 1));
                if (i == j)
                {
                    continue;
                }

                (links[i], links[j]) = (links[j], links[i]);
            }
        }

        private uint NextRandom()
        {
            if (_randomState == 0)
            {
                _randomState = 0x9E3779B9;
            }

            var value = _randomState;
            value ^= value << 13;
            value ^= value >> 17;
            value ^= value << 5;
            _randomState = value;
            return value;
        }

        private static void ValidateTouchLinkedLayout()
        {
            if (Unsafe.SizeOf<TouchLinked>() != 256)
            {
                throw new InvalidOperationException($"TouchLinked size mismatch: {Unsafe.SizeOf<TouchLinked>()} != 256。");
            }

            if (Marshal.OffsetOf<TouchLinked>(nameof(TouchLinked.TouchFlags)).ToInt32() != 0 ||
                Marshal.OffsetOf<TouchLinked>(nameof(TouchLinked.SourceHandle)).ToInt32() != 24 ||
                Marshal.OffsetOf<TouchLinked>(nameof(TouchLinked.TargetHandle)).ToInt32() != 28)
            {
                throw new InvalidOperationException("TouchLinked 字段偏移不匹配。");
            }
        }

        [StructLayout(LayoutKind.Explicit, Size = 256)]
        private struct TouchLinked
        {
            [FieldOffset(0)]
            public uint TouchFlags;

            [FieldOffset(24)]
            public int SourceHandle;

            [FieldOffset(28)]
            public int TargetHandle;

            public readonly bool IsUntouching => (TouchFlags & UntouchFlag) != 0;
        }
    }
}
