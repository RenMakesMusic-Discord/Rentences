namespace Rentences.Domain.Definitions.Game;
public class WordUsage
{
    public int Id { get; set; }
    public string WordValue { get; set; } // Punctuation-stripped word
    public int Count { get; set; } // How many times the word has been used
}