using Rentences.Domain;

namespace Rentences.Application.Services.Game;

public interface IFeaturedGamemodeSelector
{
    Gamemodes? TrySelectFeaturedGamemode(Gamemodes lastGameMode);
}