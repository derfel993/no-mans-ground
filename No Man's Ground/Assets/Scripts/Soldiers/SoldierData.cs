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
