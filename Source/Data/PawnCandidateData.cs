using Verse;
using RimWorld;

namespace RandomPlus
{
    public struct SkillSnapshot
    {
        public SkillDef def;
        public Passion passion;
        public int level;
    }

    public struct TraitSnapshot
    {
        public string label;
    }

    public struct SkillFilterEntry
    {
        public SkillDef skillDef;
        public Passion passion;
        public int minValue;
    }

    public struct TraitFilterEntry
    {
        public string label;
        public TraitContainer.TraitFilterType filterType;
    }

    public struct PawnCandidateData
    {
        public int index;
        public uint randState;
        public Gender gender;
        public int ageBiologicalYears;
        public SkillSnapshot[] skills;
        public TraitSnapshot[] traits;
        public WorkTags disabledWorkTags;
    }

    public struct PawnFilterSnapshot
    {
        public Gender gender;
        public int ageMin;
        public int ageMax;
        public SkillFilterEntry[] skillFilters;
        public int passionRangeMin;
        public int passionRangeMax;
        public int skillRangeMin;
        public int skillRangeMax;
        public bool countOnlyHighestAttack;
        public bool countOnlyPassion;
        public TraitFilterEntry[] traitFilters;
        public int requiredTraitsInPool;
        public PawnFilter.IncapableOptions filterIncapable;
        public int rerollLimit;
        public bool modWellMet;
    }
}
