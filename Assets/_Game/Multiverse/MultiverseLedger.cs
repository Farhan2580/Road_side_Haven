using System;
namespace RoadsideHaven
{
    public enum UniverseId { Normal, Neon, Apocalypse, Fantasy }
    public enum DimensionItem { Supplies, CyberCore, SurvivorKit, ManaCrystal }

    /// <summary>Session-local wallets and portable trade inventory. All trade operations validate before mutation.</summary>
    public sealed class MultiverseLedger
    {
        private readonly Wallet[] wallets;
        private readonly int[] cargo = new int[4];
        private readonly int capacity;
        private static readonly int[,] BuyPrices = {{10,40,32,50},{22,16,46,65},{28,55,14,70},{25,60,48,20}};
        private static readonly int[,] SellPrices = {{6,28,22,35},{16,10,32,45},{20,38,9,48},{18,42,34,12}};
        public UniverseId Current { get; private set; }
        public Wallet CurrentWallet => wallets[(int)Current];
        public int TotalCargo => cargo[0]+cargo[1]+cargo[2]+cargo[3];
        public int Capacity => capacity;
        public MultiverseLedger(Wallet normalWallet, int startingBalance = 120, int cargoCapacity = 12)
        {
            if (normalWallet == null || startingBalance < 0 || cargoCapacity < 1) throw new ArgumentOutOfRangeException();
            wallets = new[] {normalWallet,new Wallet(startingBalance),new Wallet(startingBalance),new Wallet(startingBalance)};
            capacity = cargoCapacity;
        }
        public Wallet GetWallet(UniverseId universe) { Validate((int)universe); return wallets[(int)universe]; }
        public void Travel(UniverseId destination) { Validate((int)destination); Current=destination; }
        public int Count(DimensionItem item) { Validate((int)item); return cargo[(int)item]; }
        public int BuyPrice(DimensionItem item) { Validate((int)item); return BuyPrices[(int)Current,(int)item]; }
        public int SellPrice(DimensionItem item) { Validate((int)item); return SellPrices[(int)Current,(int)item]; }
        public bool TryBuy(DimensionItem item)
        {
            int price=BuyPrice(item);
            if (TotalCargo>=capacity || !CurrentWallet.TryDebit(price)) return false;
            cargo[(int)item]++; return true;
        }
        public bool TrySell(DimensionItem item)
        {
            int price=SellPrice(item);
            if (cargo[(int)item]<=0 || (long)CurrentWallet.Coins+price>int.MaxValue) return false;
            CurrentWallet.Credit(price); cargo[(int)item]--; return true;
        }
        public bool TryConsume(DimensionItem item)
        { Validate((int)item); if(cargo[(int)item]<1)return false; cargo[(int)item]--;return true; }
        private static void Validate(int value) { if(value<0 || value>3)throw new ArgumentOutOfRangeException(); }
        public static string Currency(UniverseId universe) => universe==UniverseId.Neon ? "Credits" : universe==UniverseId.Apocalypse ? "Scrap" : universe==UniverseId.Fantasy ? "Runes" : "Coins";
    }
}
