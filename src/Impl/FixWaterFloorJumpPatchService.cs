using Microsoft.Extensions.Logging;
using SwiftlyS2.Shared;

namespace ZombiEden.CS2.SwiftlyS2.Fixes.Impl
{
    /// <summary>
    /// FixWaterFloorJump 补丁服务。
    /// </summary>
    public sealed class FixWaterFloorJumpPatchService(
        ISwiftlyCore core,
        ILogger<FixWaterFloorJumpPatchService> logger) : GameDataPatchService(core, logger)
    {
        public override string PatchName => "FixWaterFloorJump";

        public override string ConVarName => "sw_patch_fix_water_floor_jump_enable";

        public override bool DefaultEnabled => true;

        public override string ConVarDescription => "启用 FixWaterFloorJump GameData 补丁。关闭会撤销已应用的补丁。";
    }
}
