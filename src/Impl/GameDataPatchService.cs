using Microsoft.Extensions.Logging;
using SwiftlyS2.Shared;
using ZombiEden.CS2.SwiftlyS2.Fixes.Interface;

namespace ZombiEden.CS2.SwiftlyS2.Fixes.Impl
{
    /// <summary>
    /// GameData补丁服务实现（只负责在Load时应用补丁）
    /// </summary>
    public class GameDataPatchService(
        ISwiftlyCore core,
        ILogger<IGameDataPatchService> logger,
        string patchName) : IGameDataPatchService
    {
        public string ServiceName { get; } = $"GameDataPatch:{patchName}";

        public void Install()
        {
            try
            {
                core.GameData.ApplyPatch(patchName);
                logger.LogInformation($"{ServiceName} applied successfully");
            }
            catch (Exception ex)
            {
                logger.LogError($"Failed to apply {ServiceName}: {ex.Message}");
                throw;
            }
        }

        public void Uninstall()
        {
            
        }
    }
}