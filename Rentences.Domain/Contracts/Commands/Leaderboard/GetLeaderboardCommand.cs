using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Rentences.Domain.Contracts.Commands.Leaderboard;

public record struct GetLeaderboardCommand : IRequest<GetLeaderboardCommand>
{
    public SocketMessage socketMessage { get; set; }    
}
