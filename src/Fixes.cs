using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Plugins;
using ZombiEden.CS2.SwiftlyS2.Fixes.Impl;
using ZombiEden.CS2.SwiftlyS2.Fixes.Interface;

namespace ZombiEden.CS2.SwiftlyS2.Fixes
{
    [PluginMetadata(
        Id = "ZombiEden.CS2.SwiftlyS2.Fixes",
        Name = "ZombiEden Fixes",
        Author = "ZombiEden",
        Version = "1.0.1",
        Description = "僵尸乐园 Fixes",
        Website = "https://zombieden.cn"
    )]
    public partial class Fixes(ISwiftlyCore core) : BasePlugin(core)
    {
        private readonly List<IGameFixService> _fixServices = new();

        public override void Load(bool hotReload)
        {
            // 创建依赖注入容器
            var services = new ServiceCollection();
            services.AddSwiftly(core);
            
            services.AddKeyedSingleton<IGameDataPatchService>("ServerMovementUnlock", (sp, key) =>
                new GameDataPatchService(
                    Core, 
                    sp.GetRequiredService<ILogger<GameDataPatchService>>(), 
                    "ServerMovementUnlock"
                ));

            services.AddKeyedSingleton<IGameDataPatchService>("FixWaterFloorJump", (sp, key) =>
                new GameDataPatchService(
                    Core, 
                    sp.GetRequiredService<ILogger<GameDataPatchService>>(), 
                    "FixWaterFloorJump"
                ));

            // 注册其他修复服务
            services.AddSingleton<IStripFixService, StripFixService>();
            services.AddSingleton<ITriggerPushTouchFixService, TriggerPushTouchFixService>();
            services.AddSingleton<ITriggerForPlayerFixService, TriggerForPlayerFixService>();

            var serviceProvider = services.BuildServiceProvider();

            // 获取所有修复服务
            var allServices = new IGameFixService[]
            {
                serviceProvider.GetRequiredKeyedService<IGameDataPatchService>("ServerMovementUnlock"),
                serviceProvider.GetRequiredKeyedService<IGameDataPatchService>("FixWaterFloorJump"),
                serviceProvider.GetRequiredService<IStripFixService>(),
                serviceProvider.GetRequiredService<ITriggerPushTouchFixService>(),
                serviceProvider.GetRequiredService<ITriggerForPlayerFixService>()
            };

            // 安装所有服务
            foreach (var service in allServices)
            {
                try
                {
                    service.Install();
                    _fixServices.Add(service);
                }
                catch (Exception ex)
                {
                    Core.Logger.LogError($"Failed to install {service.ServiceName}: {ex.Message}");
                }
            }
        }

        public override void Unload()
        {
            foreach (var service in _fixServices)
            {
                try
                {
                    service.Uninstall();
                }
                catch (Exception ex)
                {
                    Core.Logger.LogError($"Failed to uninstall {service.ServiceName}: {ex.Message}");
                }
            }

            _fixServices.Clear();
        }
    }
}