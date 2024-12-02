using System.Text.RegularExpressions;
namespace Rentences.Domain.Definitions.Game;

public class Word
{
    public Word CreateWord(ulong messageid, string value, ulong author, DateTimeOffset? timestamp = null)
    {
        MessageId = messageid;
        Value = value;
        Author = author;
        TimeStamp = timestamp;

        WordValidator validator = new();
        var validate = validator.Validate(this);
        if (validate.IsValid)
            return this;

        return null;

    }
    public ulong MessageId { get; set; }
    public ulong Author { get; set; }
    public string Value { get; set; }
    public DateTimeOffset? TimeStamp { get; set; }

    public static bool ContainsValidTermination(Word? word)
    {
        if (word == null) return false;

        string value = word.Value;
        string TerminatingChars = ".?!";
        bool containsTerminating = value.Any(c => TerminatingChars.Contains(c));
        // Rule 1: Words can end with terminating characters (.!?)
        return containsTerminating ? TerminatingChars.Contains(value.Last()) : false;
    }
    public string GetStrippedValue()
    {
        return Regex.Replace(Value, @"[^\w\s]", string.Empty);
    }
}
public class WordValidator : AbstractValidator<Word>
{
    private const string ConnectingChars = ",-;";
    private const string TerminatingChars = ".?!";
    private const string Apostrophe = "'";
    public WordValidator()
    {
        RuleFor(word => word.Value)
            .NotEmpty().WithMessage("Word cannot be empty")
            .Must(value => !value.Contains(" ")).WithMessage("Word must not contain spaces")
            .Must(BeValidWord).WithMessage("Word contains invalid characters or incorrect usage");
    }

    private bool BeValidWord(string value)
    {
        try
        {
            // Rule 1: Words can contain either letters or numbers (but not mixed)
            bool isLetterOrNumberOnly = value.All(char.IsLetter) || value.All(char.IsDigit);

            var validEmojiUsage = EmojiDetector.GetEmojiInfo(value);
            if (validEmojiUsage.IsOnlyEmoji && validEmojiUsage.EmojiCount == 1)
            {
                return true;
            }
            // Parentheses check: words can start or end with parentheses
            bool startsOrEndsWithParentheses = value.Contains("(") || value.Contains(")") ? value.StartsWith("(") && value.EndsWith(")") : true;

            // Rule 3: Connective characters (,;-) can only be used with letters
            string pattern = @"^[A-Za-z0-9,;.!?'’\p{L}\p{M}]*$|^(?:\([^)'’]*\))$";


            bool containsOnlyValidCharacters = Regex.IsMatch(value, pattern);


            bool validConnectiveUsage = !value.Any(char.IsDigit) || !ConnectingChars.Any(value.Contains);

            // Rule 4: No invalid punctuation apart from exceptions (Apostrophe, Connective, Terminating)
            bool containsValidPunctuation = value.All(c => char.IsLetterOrDigit(c) || ConnectingChars.Contains(c) || TerminatingChars.Contains(c) || Apostrophe.Contains(c) || startsOrEndsWithParentheses);

            // Rule 5: Valid apostrophe usage
            bool validApostropheUsage = IsApostropheCorrect(value);
            bool containsApostrophe = value.Contains(Apostrophe);

            // Rule 6: Words can contain a connective character at the start and a terminating character at the end
            bool containsConnective = value.Any(c => ConnectingChars.Contains(c));
            bool containsTerminating = value.Any(c => TerminatingChars.Contains(c));
            bool validCharacterCombination = true; // Allow connective and terminating characters together

            // Rule 7: Words can end with terminating characters (.?!)
            bool endsWithTerminating = containsTerminating ? TerminatingChars.Contains(value.Last()) : true;

            // Rule 8: Words can start or end with connective characters (,;-), or start with a connective and end with a terminating character
            bool startsOrEndsWithConnective = containsConnective ? ConnectingChars.Contains(value.First()) || ConnectingChars.Contains(value.Last()) || (ConnectingChars.Contains(value.First()) && TerminatingChars.Contains(value.Last())) : true;

            // Rule 9: Ensure at most one instance of connective or terminating characters
            int connectiveCount = value.Count(c => ConnectingChars.Contains(c));
            int terminatingCount = value.Count(c => TerminatingChars.Contains(c));
            bool validInstanceCount = connectiveCount <= 1 && terminatingCount <= 1;

            // Rule 10: Currency validation
            bool validCurrencyUsage = value.Any(c => c.Equals('£') || c.Equals('$') || c.Equals('€') || c.Equals('¥'))
                ? Regex.IsMatch(value, @"[£$€¥](\d+(\.\d{2})?)")
                : true;

            // Rule 11: No more than 4 repeating letters
            bool noMoreThanFourRepeatingLetters = !Regex.IsMatch(value, @"(\w)\1{3}");

            // Rule 12: Ensure no new line characters in the word
            bool noNewLine = !value.Contains("\n");

            // Rule 13: No two capital letters unless the whole word is uppercase or starts with a capital letter
            string strippedValue = Regex.Replace(value, @"[^a-zA-Z]", string.Empty);
            bool containsCapitalLetters = strippedValue.Any(char.IsUpper);
            bool allUppercase = strippedValue.All(char.IsUpper);
            bool startsWithCapital = char.IsUpper(strippedValue.FirstOrDefault());

            //bool isNotMultipleWords = value.Count() < 60;

            bool validCapitalization = !containsCapitalLetters || allUppercase || (startsWithCapital && strippedValue.Skip(1).All(char.IsLower));
            bool maxCharactersBypassed = !(value.Length > 70);
            // Combine all the rules, adding the invalid pattern check
            return (!(value.Any(char.IsLetter) && value.Any(char.IsDigit)) || validApostropheUsage && containsApostrophe) &&
                   endsWithTerminating &&
                   //isNotMultipleWords &&
                   startsOrEndsWithConnective &&
                   containsOnlyValidCharacters &&
                   validConnectiveUsage &&
                   validApostropheUsage &&
                   containsValidPunctuation &&
                   validCharacterCombination &&
                   validInstanceCount &&
                   validCurrencyUsage &&
                   noMoreThanFourRepeatingLetters &&
                   startsOrEndsWithConnective &&
                   noNewLine &&
                   validCapitalization && 
                   maxCharactersBypassed;
                    
        }
        catch (Exception ex)
        {
            return false;
        }
    }


    public static bool IsApostropheCorrect(string word)
    {
        // If the word doesn't contain an apostrophe, return true
        if (!word.Contains('\'')) return true;

        // Updated Regex patterns:
        // Contractions (e.g., can't, won't, it's, or 'Tis)
        string contractionPattern = @"^\'?\w+\'\w{1,2}$";
        // Possessives (e.g., John's, dog's, children's)
        string possessivePattern = @"^\w+\'s$|^\w+s\'$";
        // Plurals of letters or numbers (e.g., A's, 1990's)
        string pluralPattern = @"^[A-Za-z0-9]\'s$";
        // Words that begin with an apostrophe (e.g., 'Tis)
        string apostropheStartPattern = @"^\'\w+$";

        // Combine patterns with more specific cases
        string combinedPattern = $"({contractionPattern})|({possessivePattern})|({pluralPattern})|({apostropheStartPattern})";

        // Check if the word matches any of the patterns
        return Regex.IsMatch(word, combinedPattern);
    }

}