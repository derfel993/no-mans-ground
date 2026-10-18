using System;
using System.Collections.Generic;

namespace NoMansGround.Soldiers
{
    public static class SoldierGenerator
    {
        private static readonly string[] FirstNames =
        {
            "Giovanni", "Giuseppe", "Antonio", "Francesco", "Luigi", "Carlo", "Mario", "Pietro",
            "Paolo", "Angelo", "Vincenzo", "Domenico", "Ettore", "Enrico", "Umberto", "Alberto"
        };

        private static readonly string[] LastNames =
        {
            "Rossi", "Russo", "Ferrari", "Esposito", "Bianchi", "Romano", "Colombo", "Ricci",
            "Marino", "Greco", "Bruno", "Gallo", "Conti", "De Luca", "Mancini", "Costa"
        };

        public const int MaximumSoldiers = 64;

        public static IReadOnlyList<SoldierData> Generate(int count, int seed)
        {
            return Generate(count, seed, false);
        }

        internal static IReadOnlyList<SoldierData> Generate(int count, int seed, bool includeCook)
        {
            if (count < 1 || count > MaximumSoldiers)
                throw new ArgumentOutOfRangeException(nameof(count), "Soldier count must be between 1 and 64.");

            var random = new Random(seed);
            var consumptionRandom = new Random(unchecked(seed ^ 0x4E454544));
            var identities = new List<int>();
            for (int i = 0; i < FirstNames.Length * LastNames.Length; i++)
                identities.Add(i);

            var soldiers = new SoldierData[count];
            for (int i = 0; i < count; i++)
            {
                int choice = random.Next(identities.Count);
                int identity = identities[choice];
                identities.RemoveAt(choice);
                int thermalState = random.Next(-25, 26);
                random.Next(0, 31);
                random.Next(0, 21);
                random.Next(0, 36);
                var needs = new SoldierNeeds(100f, 100f, 100f,
                    thermalState, 0f, 0f, 0f,
                    GenerateDepletionSeconds(consumptionRandom), GenerateDepletionSeconds(consumptionRandom),
                    GenerateDepletionSeconds(consumptionRandom));
                var levels = new int[5];
                for (int skill = 0; skill < levels.Length; skill++)
                    levels[skill] = random.Next(1, 11);
                soldiers[i] = new SoldierData(i, FirstNames[identity / LastNames.Length],
                    LastNames[identity % LastNames.Length], needs, levels,
                    includeCook && i == count - 1 ? SoldierProfession.Cook : SoldierProfession.Engineer);
            }
            return Array.AsReadOnly(soldiers);
        }

        private static float GenerateDepletionSeconds(Random random)
        {
            return SoldierNeeds.MinimumDepletionSeconds + (float)random.NextDouble()
                * (SoldierNeeds.MaximumDepletionSeconds - SoldierNeeds.MinimumDepletionSeconds);
        }
    }
}
