using Microsoft.AspNetCore.SignalR;

namespace BattleHub.Memory.Api.Hubs;

/// <summary>
/// Hub propio del juego de memoria, montado en /hubs/memory.
/// Es independiente del Lobby Hub de Matchmaking: toda la lógica
/// de la partida (turnos, cartas volteadas, resultado) vive aquí.
/// </summary>
public class MemoryHub : Hub
{
    public override async Task OnConnectedAsync()
    {
        // TODO: cuando exista el contrato de entrada (matchId, currentUser),
        // aquí se une al jugador al grupo de su partida:
        // await Groups.AddToGroupAsync(Context.ConnectionId, matchId);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        await base.OnDisconnectedAsync(exception);
    }

    // TODO: métodos del juego, por ejemplo:
    // public async Task FlipCard(string matchId, int cardIndex) { ... }
}
