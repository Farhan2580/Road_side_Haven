using System;
namespace RoadsideHaven
{
    /// <summary>Integer-only coin balance for a single motel session.</summary>
    public sealed class Wallet : IEconomyService
    {
        public int Coins { get; private set; }

        public Wallet(int startingCoins)
        {
            if (startingCoins < 0)
                throw new ArgumentOutOfRangeException(nameof(startingCoins));
            Coins = startingCoins;
        }

        public void Credit(int amount)
        {
            if (amount < 0)
                throw new ArgumentOutOfRangeException(nameof(amount));
            Coins = checked(Coins + amount);
        }

        /// <summary>Spends coins atomically; rejected requests never change the balance.</summary>
        public bool TryDebit(int amount)
        {
            if (amount < 0 || amount > Coins)
                return false;
            Coins -= amount;
            return true;
        }
    }
}

