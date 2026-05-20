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
        Version = "1.1.3",
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
            services.AddSwiftly(Core);

            GameFunctions.Setup(Core);
            Extensions.Setup(Core);

            var fixServiceFactories = new List<(string Name, Func<IServiceProvider, IGameFixService> Factory)>();

            AddFixService<ServerMovementUnlockPatchService>(services, fixServiceFactories);
            AddFixService<FixWaterFloorJumpPatchService>(services, fixServiceFactories);

            // 注册其他修复服务
            AddFixService<IStripFixService, StripFixService>(services, fixServiceFactories);
            AddFixService<ITriggerPushTouchFixService, TriggerPushTouchFixService>(services, fixServiceFactories);
            AddFixService<ITriggerForPlayerFixService, TriggerForPlayerFixService>(services, fixServiceFactories);
            AddFixService<IGravityTouchFixService, GravityTouchFixService>(services, fixServiceFactories);
            AddFixService<ISubtickDisableService, SubtickDisableService>(services, fixServiceFactories);
            AddFixService<IGameUIFixService, GameUIFixService>(services, fixServiceFactories);
            AddFixService<IBlockParticleMsgsFixService, BlockParticleMsgsFixService>(services, fixServiceFactories);
            AddFixService<ITeleportBorkenFixService, TeleportBorkenFixService>(services, fixServiceFactories);
            AddFixService<IShufflePlayerPhysicsSimFixService, ShufflePlayerPhysicsSimFixService>(services, fixServiceFactories);
            AddFixService<IPointViewControlFixService, PointViewControlFixService>(services, fixServiceFactories);

            var serviceProvider = services.BuildServiceProvider();

            // 安装所有服务
            foreach (var (registrationName, factory) in fixServiceFactories)
            {
                IGameFixService? service = null;

                try
                {
                    service = factory(serviceProvider);
                    service.Install();
                    _fixServices.Add(service);
                }
                catch (Exception ex)
                {
                    if (service is not null)
                    {
                        try
                        {
                            service.Uninstall();
                        }
                        catch (Exception cleanupEx)
                        {
                            Core.Logger.LogError(cleanupEx, "Failed to cleanup {RegistrationName} after install failure", registrationName);
                        }
                    }

                    Core.Logger.LogError(ex, "Failed to install {RegistrationName}", registrationName);
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

        private static void AddFixService<TImplementation>(
            IServiceCollection services,
            ICollection<(string Name, Func<IServiceProvider, IGameFixService> Factory)> fixServiceFactories)
            where TImplementation : class, IGameFixService
        {
            services.AddSingleton<TImplementation>();
            fixServiceFactories.Add((typeof(TImplementation).Name, static sp => sp.GetRequiredService<TImplementation>()));
        }

        private static void AddFixService<TService, TImplementation>(
            IServiceCollection services,
            ICollection<(string Name, Func<IServiceProvider, IGameFixService> Factory)> fixServiceFactories)
            where TService : class, IGameFixService
            where TImplementation : class, TService
        {
            services.AddSingleton<TService, TImplementation>();
            fixServiceFactories.Add((typeof(TImplementation).Name, static sp => sp.GetRequiredService<TService>()));
        }
    }
}
