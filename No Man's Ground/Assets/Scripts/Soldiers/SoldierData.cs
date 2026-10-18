using System;
using System.Collections.Generic;
using NoMansGround.Map;

namespace NoMansGround.Soldiers
{
    public enum SoldierSkill
    {
        Aim,
        Courage,
        Strength,
        Endurance,
        Speed
    }

    public enum SoldierProfession { Soldier, Engineer, Cook }

    public enum SoldierTask { Hauler, Woodcutter, Miner, Digger, Builder, Cook }

    public static class SoldierTasks
    {
        public static string DisplayName(SoldierTask task)
        {
            switch (task)
            {
                case SoldierTask.Hauler: return "Trasportatore";
                case SoldierTask.Woodcutter: return "Taglialegna";
                case SoldierTask.Miner: return "Minatore";
                case SoldierTask.Digger: return "Scavatore";
                case SoldierTask.Builder: return "Costruttore";
                case SoldierTask.Cook: return "Cuoco";
                default: throw new ArgumentOutOfRangeException(nameof(task));
            }
        }

        public static ItemType? Tool(SoldierTask task)
        {
            switch (task)
            {
                case SoldierTask.Hauler:
                case SoldierTask.Cook: return null;
                case SoldierTask.Woodcutter: return ItemType.Axe;
                case SoldierTask.Miner: return ItemType.Pickaxe;
                case SoldierTask.Digger: return ItemType.Shovel;
                case SoldierTask.Builder: return ItemType.ConstructionHammer;
                default: throw new ArgumentOutOfRangeException(nameof(task));
            }
        }

        public static bool Allows(SoldierTask task, TrenchJobKind kind)
        {
            switch (task)
            {
                case SoldierTask.Hauler: return kind == TrenchJobKind.CollectItem;
                case SoldierTask.Woodcutter: return kind == TrenchJobKind.CutTree;
                case SoldierTask.Miner: return kind == TrenchJobKind.MineRock;
                case SoldierTask.Digger: return kind == TrenchJobKind.Dig;
                case SoldierTask.Builder: return kind == TrenchJobKind.BuildWall
                    || kind == TrenchJobKind.BuildDoor || kind == TrenchJobKind.Ladder;
                case SoldierTask.Cook: return false;
                default: throw new ArgumentOutOfRangeException(nameof(task));
            }
        }
    }

    public sealed class SoldierNeeds
    {
        public const float AverageDepletionSeconds = 20f * 60f;
        public const float MinimumDepletionSeconds = AverageDepletionSeconds * 0.85f;
        public const float MaximumDepletionSeconds = AverageDepletionSeconds * 1.15f;

        private double hunger;
        private double thirst;
        private double physicalNeeds;
        private double stress;

        public float Hunger => (float)hunger;
        public float Thirst => (float)thirst;
        public float PhysicalNeeds => (float)physicalNeeds;
        public float HungerDepletionSeconds { get; }
        public float ThirstDepletionSeconds { get; }
        public float PhysicalNeedsDepletionSeconds { get; }
        public float HungerConsumptionPerSecond => 100f / HungerDepletionSeconds;
        public float ThirstConsumptionPerSecond => 100f / ThirstDepletionSeconds;
        public float PhysicalNeedsConsumptionPerSecond => 100f / PhysicalNeedsDepletionSeconds;
        public float ThermalState { get; }
        public float Stress => (float)stress;
        public bool WaitingForRations => hunger < 35d;
        public float Fear { get; }
        public float Fatigue { get; }
        public event Action Changed;

        public SoldierNeeds(float hunger, float thirst, float physicalNeeds, float thermalState,
            float stress, float fear, float fatigue,
            float hungerDepletionSeconds = AverageDepletionSeconds,
            float thirstDepletionSeconds = AverageDepletionSeconds,
            float physicalNeedsDepletionSeconds = AverageDepletionSeconds)
        {
            this.hunger = Validate(hunger, 0f, nameof(hunger));
            this.thirst = Validate(thirst, 0f, nameof(thirst));
            this.physicalNeeds = Validate(physicalNeeds, 0f, nameof(physicalNeeds));
            HungerDepletionSeconds = ValidateDuration(hungerDepletionSeconds, nameof(hungerDepletionSeconds));
            ThirstDepletionSeconds = ValidateDuration(thirstDepletionSeconds, nameof(thirstDepletionSeconds));
            PhysicalNeedsDepletionSeconds = ValidateDuration(physicalNeedsDepletionSeconds, nameof(physicalNeedsDepletionSeconds));
            ThermalState = Validate(thermalState, -100f, nameof(thermalState));
            this.stress = Validate(stress, 0f, nameof(stress));
            Fear = Validate(fear, 0f, nameof(fear));
            Fatigue = Validate(fatigue, 0f, nameof(fatigue));
        }

        public void EatRation()
        {
            hunger = 100d;
            Changed?.Invoke();
        }

        public void DrinkWater()
        {
            thirst = 100d;
            Changed?.Invoke();
        }

        public void AdvanceTime(float gameSeconds)
        {
            if (!float.IsFinite(gameSeconds) || gameSeconds < 0f)
                throw new ArgumentOutOfRangeException(nameof(gameSeconds));
            if (gameSeconds == 0f)
                return;

            double previousHunger = hunger;
            double previousThirst = thirst;
            double previousPhysical = physicalNeeds;
            double previousStress = stress;
            double untilHungry = Math.Max(0d, (hunger - 35d) * HungerDepletionSeconds / 100d);
            double hungrySeconds = Math.Max(0d, gameSeconds - untilHungry);
            stress = Math.Min(100d, stress + hungrySeconds * 5d / 60d);
            hunger = Math.Max(0d, hunger - 100d * gameSeconds / HungerDepletionSeconds);
            thirst = Math.Max(0d, thirst - 100d * gameSeconds / ThirstDepletionSeconds);
            physicalNeeds = Math.Max(0d, physicalNeeds - 100d * gameSeconds / PhysicalNeedsDepletionSeconds);
            if (previousHunger != hunger || previousThirst != thirst
                || previousPhysical != physicalNeeds || previousStress != stress)
                Changed?.Invoke();
        }

        private static float ValidateDuration(float seconds, string name)
        {
            if (!float.IsFinite(seconds) || seconds < MinimumDepletionSeconds || seconds > MaximumDepletionSeconds)
                throw new ArgumentOutOfRangeException(name, "Depletion duration must be between 17 and 23 game minutes.");
            return seconds;
        }

        private static float Validate(float value, float minimum, string name)
        {
            if (!float.IsFinite(value) || value < minimum || value > 100f)
                throw new ArgumentOutOfRangeException(name, $"Value must be finite and between {minimum} and 100.");
            return value;
        }
    }

    public sealed class SkillProgress
    {
        public const int MaximumLevel = 20;

        public int Level { get; private set; }
        public int Experience { get; private set; }
        public bool IsMaximumLevel => Level == MaximumLevel;
        public int ExperienceToNextLevel => IsMaximumLevel ? 0 : Level * 100;
        public int Value => Level * 5;
        public event Action Changed;

        public SkillProgress(int level = 1)
        {
            if (level < 1 || level > MaximumLevel)
                throw new ArgumentOutOfRangeException(nameof(level), "Skill level must be between 1 and 20.");
            Level = level;
        }

        public void AddExperience(int amount)
        {
            if (amount < 0)
                throw new ArgumentOutOfRangeException(nameof(amount), "Experience cannot be negative.");
            if (amount == 0 || IsMaximumLevel)
                return;

            long remaining = (long)Experience + amount;
            while (!IsMaximumLevel && remaining >= ExperienceToNextLevel)
            {
                remaining -= ExperienceToNextLevel;
                Level++;
            }
            Experience = IsMaximumLevel ? 0 : (int)remaining;
            Changed?.Invoke();
        }
    }

    public sealed class SoldierData
    {
        public const float MinimumCarryWeightKg = 20f;
        public const float MaximumCarryWeightKg = 35f;
        private readonly SkillProgress[] skills;

        public int Id { get; }
        public string FirstName { get; }
        public string LastName { get; }
        public string FullName => FirstName + " " + LastName;
        public SoldierNeeds Needs { get; }
        public ItemStockpile Inventory { get; } = new ItemStockpile();
        public IReadOnlyList<SkillProgress> Skills { get; }
        public SoldierProfession Profession { get; }
        public SoldierTask Task { get; internal set; } = SoldierTask.Hauler;
        public float MaxCarryWeightKg => MinimumCarryWeightKg
            + (MaximumCarryWeightKg - MinimumCarryWeightKg)
            * (GetSkill(SoldierSkill.Strength).Value - 5f) / 95f;

        public SoldierData(int id, string firstName, string lastName, SoldierNeeds needs,
            IReadOnlyList<int> skillLevels, SoldierProfession profession = SoldierProfession.Soldier)
        {
            if (id < 0)
                throw new ArgumentOutOfRangeException(nameof(id));
            if (string.IsNullOrWhiteSpace(firstName) || string.IsNullOrWhiteSpace(lastName))
                throw new ArgumentException("A soldier requires a first name and a last name.");
            if (needs == null)
                throw new ArgumentNullException(nameof(needs));
            if (skillLevels == null || skillLevels.Count != Enum.GetValues(typeof(SoldierSkill)).Length)
                throw new ArgumentException("Provide one level for each soldier skill.", nameof(skillLevels));
            if (!Enum.IsDefined(typeof(SoldierProfession), profession))
                throw new ArgumentOutOfRangeException(nameof(profession));

            Id = id;
            FirstName = firstName;
            LastName = lastName;
            Needs = needs;
            Profession = profession;
            skills = new SkillProgress[skillLevels.Count];
            for (int i = 0; i < skills.Length; i++)
                skills[i] = new SkillProgress(skillLevels[i]);
            Skills = Array.AsReadOnly(skills);
            Inventory.Add(ItemType.FullCanteen, 1);
            Needs.Changed += HandleNeedsChanged;
        }

        private void HandleNeedsChanged()
        {
            TryDrinkWater();
        }

        public bool TryDrinkWater()
        {
            if (Needs.Thirst >= 20f || !Inventory.TryTake(ItemType.FullCanteen, 1))
                return false;
            Inventory.Add(ItemType.EmptyCanteen, 1);
            Needs.DrinkWater();
            return true;
        }

        public void ReceiveRation()
        {
            int emptyCanteens = Inventory.Count(ItemType.EmptyCanteen);
            if (emptyCanteens > 0 && Inventory.TryTake(ItemType.EmptyCanteen, emptyCanteens))
                Inventory.Add(ItemType.FullCanteen, emptyCanteens);
            Needs.EatRation();
        }

        public SkillProgress GetSkill(SoldierSkill skill)
        {
            int index = (int)skill;
            if (index < 0 || index >= skills.Length)
                throw new ArgumentOutOfRangeException(nameof(skill));
            return skills[index];
        }
    }
}
