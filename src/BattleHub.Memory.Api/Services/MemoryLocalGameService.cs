
using BattleHub.Memory.Api.Services;

namespace BattleHub.Memory.Api.Services;

/// <summary>
/// Mantiene las partidas locales separadas de las competitivas.
/// Hereda la lógica existente, pero usa una instancia independiente.
/// </summary>
public sealed class MemoryLocalGameService : MemoryGameService
{
}
