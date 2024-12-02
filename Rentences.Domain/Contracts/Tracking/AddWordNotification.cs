using Rentences.Domain.Definitions.Game;

namespace Rentences.Domain.Contracts.Tracking
{
    public class AddWordNotification : INotification
    {
        public Word Word { get; }

        public AddWordNotification(Word word)
        {
            Word = word;
        }
    }
}
