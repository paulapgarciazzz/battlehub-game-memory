using System.ComponentModel.DataAnnotations;

namespace BattleHub.Memory.Api.Services;

/// <summary>
/// Tiempos de la partida, en segundos. Salen solo de la configuración
/// (appsettings.json o variables GameTiming__...): si falta alguno o no es
/// válido, la API no arranca.
/// </summary>
public sealed class GameTimingOptions
{
    public const string SectionName = "GameTiming";

    /// <summary>Tiempo que tiene cada jugador para hacer su jugada.</summary>
    [Range(1, 3600)]
    public int TurnTimeoutSeconds { get; set; }

    /// <summary>Tiempo que se muestran las cartas al empezar la partida.</summary>
    [Range(1, 3600)]
    public int PreviewSeconds { get; set; }

    /// <summary>Cada cuánto se reintenta guardar los resultados pendientes.</summary>
    [Range(1, 3600)]
    public int ResultRetryIntervalSeconds { get; set; }
}
