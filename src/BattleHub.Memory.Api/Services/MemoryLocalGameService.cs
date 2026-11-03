
using BattleHub.Memory.Api.Services;
using Microsoft.Extensions.Options;

namespace BattleHub.Memory.Api.Services;


public sealed class MemoryLocalGameService(IOptions<GameTimingOptions> timing) : MemoryGameService(timing)
{
}



//prueba test y ya