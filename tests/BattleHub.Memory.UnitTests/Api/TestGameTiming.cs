using BattleHub.Memory.Api.Services;
using Microsoft.Extensions.Options;

namespace BattleHub.Memory.UnitTests.Api;

/// <summary>
/// Tiempos para armar los servicios en las pruebas. No se leen de
/// appsettings.json: cambiar la configuración no debe romper las pruebas.
/// </summary>
internal static class TestGameTiming
{
    public static IOptions<GameTimingOptions> Create(
        int turnTimeoutSeconds = 10,
        int previewSeconds = 5,
        int resultRetryIntervalSeconds = 5) =>
        Options.Create(new GameTimingOptions
        {
            TurnTimeoutSeconds = turnTimeoutSeconds,
            PreviewSeconds = previewSeconds,
            ResultRetryIntervalSeconds = resultRetryIntervalSeconds
        });
}
