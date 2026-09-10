using Domain.Models;

namespace Domain.State.Helpers
{
    internal static class OfferStateExtensions
    {
        public static bool IsExpired(this Offer offer)
            => offer.ValidUntil < DateTime.UtcNow;
    }
}
