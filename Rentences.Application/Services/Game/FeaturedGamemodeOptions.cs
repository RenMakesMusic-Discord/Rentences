using System.Collections.Generic;

namespace Rentences.Application.Services.Game;

public class FeaturedGamemodeOptions
{
    public bool Enabled { get; set; }
    public double Probability { get; set; }
    public List<string> EligibleGamemodes { get; set; } = new();
}