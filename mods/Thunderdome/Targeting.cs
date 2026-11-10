namespace Thunderdome
{
    internal enum WardMode
    {
        // Lightning strikes down hostile creatures inside the dome
        Thunderdome,
        // The traders' force field: monsters turn away
        Repel,
        Both,
    }

    // Who a ward acts on, kept to plain values so it's testable without the game running
    internal static class Targeting
    {
        public static bool Strikes(WardMode mode) => mode != WardMode.Repel;

        public static bool Repels(WardMode mode) => mode != WardMode.Thunderdome;

        // Anything that would fight a player: monsters (bosses included) and angered dvergr. Never players, tamed
        // creatures, the player's own summons, passive animals or training dummies.
        public static bool ShouldStrike(Character.Faction faction, bool isPlayer, bool tamed, bool aggravated)
        {
            if (isPlayer || tamed) return false;
            switch (faction)
            {
                case Character.Faction.Players:
                case Character.Faction.AnimalsVeg:
                case Character.Faction.PlayerSpawned:
                case Character.Faction.TrainingDummy:
                    return false;
                case Character.Faction.Dverger:
                    return aggravated;
                default:
                    return true;
            }
        }
    }
}
