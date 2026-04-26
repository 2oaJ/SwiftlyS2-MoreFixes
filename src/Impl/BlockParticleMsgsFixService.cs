using Microsoft.Extensions.Logging;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Convars;
using SwiftlyS2.Shared.Events;
using SwiftlyS2.Shared.Misc;
using SwiftlyS2.Shared.ProtobufDefinitions;
using ZombiEden.CS2.SwiftlyS2.Fixes.Interface;

namespace ZombiEden.CS2.SwiftlyS2.Fixes.Impl
{
    /// <summary>
    /// 拦截 CUserMsg_ParticleManager，规避 AG2 更新后武器粒子消息导致的客户端卡顿/闪退问题。
    /// 参考: https://github.com/Source2ZE/CS2Fixes/commit/3180a4491dcfc37821270643a09462438bb42dde
    /// </summary>
    public sealed class BlockParticleMsgsFixService(
        ISwiftlyCore core,
        ILogger<BlockParticleMsgsFixService> logger) : IBlockParticleMsgsFixService
    {
        private const string EnableConVarName = "cs2f_block_particle_msgs";

        private IConVar<bool>? _enableConVar;
        private Guid? _hookId;
        private bool _enabled;
        private bool _installed;

        public string ServiceName => "BlockParticleMsgsFix";

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
                    "拦截 CUserMsg_ParticleManager 消息以缓解客户端卡顿/闪退，实验性功能",
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

            _hookId = core.NetMessage.HookServerMessage<CUserMsg_ParticleManager>(OnParticleManagerMessage);
            logger.LogDebug("{ServiceName} 已注册 CUserMsg_ParticleManager server netmessage hook。", ServiceName);
        }

        private void DetachHook()
        {
            if (!_hookId.HasValue)
            {
                return;
            }

            core.NetMessage.Unhook(_hookId.Value);
            _hookId = null;
            logger.LogDebug("{ServiceName} 已注销 CUserMsg_ParticleManager server netmessage hook。", ServiceName);
        }

        private HookResult OnParticleManagerMessage(CUserMsg_ParticleManager message)
        {
            message.Recipients.RemoveAllPlayers();
            return HookResult.Continue;
        }
    }
}
