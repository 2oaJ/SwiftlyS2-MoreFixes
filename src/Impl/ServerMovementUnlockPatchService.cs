using Microsoft.Extensions.Logging;
using SwiftlyS2.Shared;

namespace ZombiEden.CS2.SwiftlyS2.Fixes.Impl
{
    /// <summary>
    /// ServerMovementUnlock 补丁服务。
    /// </summary>
    public sealed class ServerMovementUnlockPatchService(
        ISwiftlyCore core,
        ILogger<ServerMovementUnlockPatchService> logger) : GameDataPatchService(core, logger)
    {
        public override string PatchName => "ServerMovementUnlock";

        public override string ConVarName => "sw_patch_server_movement_unlock_enable";

        public override bool DefaultEnabled => true;

        public override string ConVarDescription => "启用 ServerMovementUnlock GameData 补丁。关闭会撤销已应用的补丁。";
    }
}
