using System.Runtime.Versioning;
using Microsoft.Extensions.Logging;

namespace Sops.Logging.Tests;

public class SopsLoggingCompatibilityTests
{
    [Fact]
    public void Compatibility_AssemblyTargetsNetStandard20()
    {
        var assembly = typeof(SopsLogging).Assembly;
        var targetFrameworkAttr = assembly.GetCustomAttributes(typeof(TargetFrameworkAttribute), inherit: false)
            .Cast<TargetFrameworkAttribute>()
            .FirstOrDefault();

        Assert.NotNull(targetFrameworkAttr);
        Assert.Equal(".NETStandard,Version=v2.0", targetFrameworkAttr.FrameworkName);
    }

    [Fact]
    public void Compatibility_CanBeConsumedStandalone_WithoutDependencyInjection()
    {
        using var factory = SopsLogging.CreateLoggerFactory(options =>
        {
            options.MinimumLevel = LogLevel.Debug;
        });

        var logger = factory.CreateLogger("StandaloneCategory");
        Assert.NotNull(logger);

        logger.LogInformation("Standalone log executed without DI container");
    }

    [Fact]
    public void Compatibility_TypedLogger_CanBeCreatedStandalone()
    {
        var logger = SopsLogging.CreateLogger<SopsLoggingCompatibilityTests>();
        Assert.NotNull(logger);

        logger.LogInformation("Typed standalone log message");
    }
}
