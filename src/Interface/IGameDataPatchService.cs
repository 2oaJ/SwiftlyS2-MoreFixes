using Microsoft.Extensions.Logging;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Convars;
using SwiftlyS2.Shared.Events;

namespace ZombiEden.CS2.SwiftlyS2.Fixes.Interface
{
    /// <summary>
    /// GameData补丁服务。
    /// 支持通过 ConVar 动态应用和撤销补丁。
    /// </summary>
    public interface IGameDataPatchService : IGameFixService
    {
        ISwiftlyCore Core { get; }

        ILogger Logger { get; }

        string PatchName { get; }

        string ConVarName { get; }

        string ConVarDescription => $"启用 {PatchName} GameData 补丁。关闭会撤销已应用的补丁。";

        bool DefaultEnabled { get; }

        IConVar<bool>? EnableConVar { get; set; }

        bool Installed { get; set; }

        bool Enabled { get; set; }

        bool PatchApplied { get; set; }

        string PatchServiceName => $"GameDataPatch:{PatchName}";

        string IGameFixService.ServiceName => PatchServiceName;

        void IGameFixService.Install()
        {
            var serviceName = PatchServiceName;

            try
            {
                if (Installed)
                {
                    Logger.LogWarning("{ServiceName} 已安装，跳过重复安装。", serviceName);
                    return;
                }

                EnableConVar = Core.ConVar.CreateOrFind(
                    ConVarName,
                    ConVarDescription,
                    DefaultEnabled,
                    ConvarFlags.SERVER_CAN_EXECUTE);

                Enabled = EnableConVar.Value;
                Core.Event.OnConVarValueChanged += OnConVarValueChanged;
                Installed = true;

                TrySyncPatch("插件加载");
                Logger.LogInformation(
                    "{ServiceName} 安装完成，当前开关: {Enabled}, 已应用: {PatchApplied}",
                    serviceName,
                    Enabled,
                    PatchApplied);
            }
            catch (Exception ex)
            {
                try
                {
                    Core.Event.OnConVarValueChanged -= OnConVarValueChanged;
                }
                catch
                {
                }

                EnableConVar = null;
                Installed = false;
                Enabled = false;
                Logger.LogError(ex, "安装 {ServiceName} 失败。", serviceName);
                throw;
            }
        }

        void IGameFixService.Uninstall()
        {
            var serviceName = PatchServiceName;

            if (!Installed)
            {
                return;
            }

            try
            {
                Core.Event.OnConVarValueChanged -= OnConVarValueChanged;
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "卸载 {ServiceName} 时取消 ConVar 监听失败。", serviceName);
            }

            try
            {
                TryRevertPatch("插件卸载");
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "卸载 {ServiceName} 时撤销补丁失败。", serviceName);
            }

            Installed = false;
            Enabled = false;
            EnableConVar = null;
            Logger.LogInformation(
                "{ServiceName} 已卸载。PatchApplied={PatchApplied}",
                serviceName,
                PatchApplied);
        }

        void OnConVarValueChanged(IOnConVarValueChanged @event)
        {
            if (EnableConVar is null || @event.ConVarName != EnableConVar.Name)
            {
                return;
            }

            bool newValue;
            if (!bool.TryParse(@event.NewValue, out newValue))
            {
                if (int.TryParse(@event.NewValue, out var numericValue))
                {
                    newValue = numericValue != 0;
                }
                else
                {
                    Logger.LogWarning(
                        "{ServiceName} 收到无法解析的 ConVar 值: {Value}",
                        ServiceName,
                        @event.NewValue);
                    return;
                }
            }

            if (Enabled == newValue)
            {
                return;
            }

            var previousEnabled = Enabled;
            Enabled = newValue;
            Logger.LogInformation("{ServiceName} 开关切换为 {Enabled}", PatchServiceName, Enabled);

            try
            {
                TrySyncPatch("ConVar切换");
            }
            catch (Exception ex)
            {
                Enabled = previousEnabled;

                if (EnableConVar is not null && EnableConVar.Value != previousEnabled)
                {
                    try
                    {
                        EnableConVar.Value = previousEnabled;
                    }
                    catch (Exception rollbackEx)
                    {
                        Logger.LogWarning(
                            rollbackEx,
                            "{ServiceName} 回滚 ConVar 值失败，当前内部状态已恢复为 {Enabled}。",
                            PatchServiceName,
                            previousEnabled);
                    }
                }

                Logger.LogError(
                    ex,
                    "{ServiceName} 在 ConVar 切换时同步补丁失败，已回滚到 {Enabled}。",
                    PatchServiceName,
                    previousEnabled);
                return;
            }
        }

        void TrySyncPatch(string reason)
        {
            if (Enabled)
            {
                TryApplyPatch(reason);
            }
            else
            {
                TryRevertPatch(reason);
            }
        }

        void TryApplyPatch(string reason)
        {
            if (!Enabled)
            {
                Logger.LogDebug("{ServiceName} 当前开关关闭，跳过应用补丁。", PatchServiceName);
                return;
            }

            if (PatchApplied)
            {
                Logger.LogDebug("{ServiceName} 已应用过补丁，跳过重复 Apply。", PatchServiceName);
                return;
            }

            if (!Core.GameData.HasPatch(PatchName))
            {
                throw new InvalidOperationException($"未找到 GameData 补丁 {PatchName}");
            }

            Core.GameData.ApplyPatch(PatchName);
            PatchApplied = true;
            Logger.LogInformation(
                "{ServiceName} 已在 {Reason} 时应用补丁 {PatchName}。",
                PatchServiceName,
                reason,
                PatchName);
        }

        void TryRevertPatch(string reason)
        {
            if (!PatchApplied)
            {
                Logger.LogDebug("{ServiceName} 当前补丁未应用，跳过 Revert。", PatchServiceName);
                return;
            }

            if (!Core.GameData.HasPatch(PatchName))
            {
                throw new InvalidOperationException($"未找到 GameData 补丁 {PatchName}");
            }

            Core.GameData.RevertPatch(PatchName);
            PatchApplied = false;
            Logger.LogInformation(
                "{ServiceName} 已在 {Reason} 时撤销补丁 {PatchName}。",
                PatchServiceName,
                reason,
                PatchName);
        }
    }
}
