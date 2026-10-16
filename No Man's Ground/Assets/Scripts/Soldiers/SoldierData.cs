using System;
using System.Collections.Generic;

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

    public sealed class SoldierNeeds
    {
        public float Hunger { get; }
        public float Thirst { get; }
        public float PhysicalNeeds { get; }
        public float ThermalState { get; }
        public float Stress { get; }
        public float Fear { get; }
        public float Fatigue { get; }

        public SoldierNeeds(float hunger, float thirst, float physicalNeeds, float thermalState,
            float stress, float fear, float fatigue)
        {
            Hunger = Validate(hunger, 0f, nameof(hunger));
            Thirst = Validate(thirst, 0f, nameof(thirst));
            PhysicalNeeds = Validate(physicalNeeds, 0f, nameof(physicalNeeds));
            ThermalState = Validate(thermalState, -100f, nameof(thermalState));
            Stress = Validate(stress, 0f, nameof(stress));
            Fear = Validate(fear, 0f, nameof(fear));
            Fatigue = Validate(fatigue, 0f, nameof(fatigue));
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
        private readonly SkillProgress[] skills;

        public int Id { get; }
        public string FirstName { get; }
        public string LastName { get; }
        public string FullName => FirstName + " " + LastName;
        public SoldierNeeds Needs { get; }
        public IReadOnlyList<SkillProgress> Skills { get; }

        public SoldierData(int id, string firstName, string lastName, SoldierNeeds needs,
            IReadOnlyList<int> skillLevels)
        {
            if (id < 0)
                throw new ArgumentOutOfRangeException(nameof(id));
            if (string.IsNullOrWhiteSpace(firstName) || string.IsNullOrWhiteSpace(lastName))
                throw new ArgumentException("A soldier requires a first name and a last name.");
            if (needs == null)
                throw new ArgumentNullException(nameof(needs));
            if (skillLevels == null || skillLevels.Count != Enum.GetValues(typeof(SoldierSkill)).Length)
                throw new ArgumentException("Provide one level for each soldier skill.", nameof(skillLevels));

            Id = id;
            FirstName = firstName;
            LastName = lastName;
            Needs = needs;
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
