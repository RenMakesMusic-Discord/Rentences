using Microsoft.Extensions.Options;
using Rentences.Domain;

namespace Rentences.Application.Services.Game;

public class FeaturedGamemodeSelector : IFeaturedGamemodeSelector
{
    private readonly FeaturedGamemodeOptions _options;
    private readonly Random _random;

    public FeaturedGamemodeSelector(IOptions<FeaturedGamemodeOptions> options)
    {
        _options = options.Value ?? new FeaturedGamemodeOptions();
        _random = new Random();
    }

    public Gamemodes? TrySelectFeaturedGamemode(Gamemodes lastGameMode)
    {
        if (!_options.Enabled)
        {
            return null;
        }

        if (_options.Probability <= 0)
        {
            return null;
        }

        if (_options.Probability >= 1)
        {
            // Always attempt featured when enabled and probability is 1 or more
        }
        else
        {
            var roll = _random.NextDouble();
            if (roll >= _options.Probability)
            {
                return null;
            }
        }

        if (_options.EligibleGamemodes is null || _options.EligibleGamemodes.Count == 0)
        {
            return null;
        }

        var candidates = new List<Gamemodes>();

        foreach (var name in _options.EligibleGamemodes)
        {
            if (Enum.TryParse<Gamemodes>(name, ignoreCase: false, out var mode))
            {
                candidates.Add(mode);
            }
        }

        if (candidates.Count == 0)
        {
            return null;
        }

        var index = _random.Next(candidates.Count);
        return candidates[index];
    }
}