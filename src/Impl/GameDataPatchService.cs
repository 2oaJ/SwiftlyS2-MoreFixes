using Microsoft.Extensions.Logging;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Convars;
using ZombiEden.CS2.SwiftlyS2.Fixes.Interface;

namespace ZombiEden.CS2.SwiftlyS2.Fixes.Impl
{
    /// <summary>
    /// GameData补丁服务状态承载基类。
    /// 具体补丁实现类只需要提供补丁名和对应的 ConVar。
    /// </summary>
    public abstract class GameDataPatchService(
        ISwiftlyCore core,
        ILogger logger) : IGameDataPatchService
    {
        protected ISwiftlyCore Core { get; } = core;

        protected ILogger Logger { get; } = logger;

        ISwiftlyCore IGameDataPatchService.Core => Core;

        ILogger IGameDataPatchService.Logger => Logger;

        IConVar<bool>? IGameDataPatchService.EnableConVar { get; set; }

        bool IGameDataPatchService.Installed { get; set; }

        bool IGameDataPatchService.Enabled { get; set; }

        bool IGameDataPatchService.PatchApplied { get; set; }

        public abstract string PatchName { get; }

        public abstract string ConVarName { get; }

        public virtual bool DefaultEnabled => false;

        public virtual string ConVarDescription => $"启用 {PatchName} GameData 补丁。关闭会撤销已应用的补丁。";
    }
}
