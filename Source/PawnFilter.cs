using Verse;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;

namespace RandomPlus
{
    public class PawnFilter : IExposable
    {
        public static readonly int PassionMinDefault = 0;
        public static readonly int PassionMaxDefault = DefDatabase<SkillDef>.AllDefs.ToArray().Length;

        public static readonly int SkillMinDefault = 0;
        public static readonly int SkillMaxDefault = DefDatabase<SkillDef>.AllDefs.ToArray().Length * 8;

        public static readonly int MinAgeDefault = 0;
        public static readonly int MaxAgeDefault = 120;

        public static readonly int DefaultPoolSize = 0;
        public enum RerollAlgorithmOptions { Normal, Fast, UltraFast }
        public readonly static string[] _RerollAlgorithmOptionValues = new string[] {
            "RandomPlus.PanelOthers.RerollAlgorithmOptionValues.Normal",
            "RandomPlus.PanelOthers.RerollAlgorithmOptionValues.Fast",
            "RandomPlus.PanelOthers.RerollAlgorithmOptionValues.UltraFast",
        };
        public static string[] RerollAlgorithmOptionValues { 
            get {
                if (ModsConfig.IsActive("erdelf.HumanoidAlienRaces"))
                {
                    return new string[] { "RandomPlus.PanelOthers.RerollAlgorithmOptionValues.Normal" };
                }
                return _RerollAlgorithmOptionValues;
            } 
        }
        public static RerollAlgorithmOptions DefaultRerollAlgorithm
        {
            get {
                if (ModsConfig.IsActive("erdelf.HumanoidAlienRaces"))
                    return RerollAlgorithmOptions.Normal;
                return RerollAlgorithmOptions.Normal;
            }
        } 

        public enum RerollLimitOptions { N100 = 100, N250 = 250, N500 = 500, N1000 = 1000, N2500 = 2500, N5000 = 5000, N10000 = 10000, N50000 = 50000, N100000 = 100000, N500000 = 500000, N1000000 = 1000000, N10000000 = 10000000, N20000000 = 20000000, N50000000 = 50000000, N100000000 = 100000000, N1000000000 = 1000000000 }
        public readonly static string[] RerollLimitOptionValues = new string[] { "100", "250", "500", "1000", "2500", "5000", "10000", "50000", "100000", "500000", "1000000", "10000000", "20000000", "50000000", "100M", "1B" };
        public static readonly RerollLimitOptions DefaultRerollLimit = RerollLimitOptions.N1000;

        public enum HealthOptions { AllowAll, OnlyStartCondition, NoPain, NoAddiction, AllowNone, Custom,
            //OnlyPositiveImplants, 
        }
        public readonly static string[] HealthOptionValues = new string[] {
            "RandomPlus.PanelOthers.HealthOptions.AllowAll",
            "RandomPlus.PanelOthers.HealthOptions.OnlyStartConditions",
            "RandomPlus.PanelOthers.HealthOptions.NoPain",
            "RandomPlus.PanelOthers.HealthOptions.NoAddiction",
            "RandomPlus.PanelOthers.HealthOptions.AllowNone",
            "RandomPlus.PanelOthers.HealthOptions.Custom",
            //"RandomPlus.PanelOthers.HealthOptions.OnlyPositiveImplants",
        };
        private const int CustomHealthVersion = 1;
        public const string CustomHealthStartConditionsKey = "StartConditions";
        public const string CustomHealthPregnancyKey = "Pregnancy";
        public const string CustomHealthPainKey = "Pain";
        public const string CustomHealthScarKey = "Scar";
        public const string CustomHealthAddictionKey = "Addiction";
        public const string CustomHealthBadModificationsKey = "BadModifications";
        public const string CustomHealthGoodModificationsKey = "GoodModifications";
        public const string CustomHealthOthersKey = "Others";

        public enum IncapableOptions { AllowAll, NoDumbLabor, AllowNone, ForcedViolence, Custom }
        public readonly static string[] IncapableOptionValues = new string[] {
            "RandomPlus.PanelOthers.IncapableOptions.AllowAll",
            "RandomPlus.PanelOthers.IncapableOptions.NoDumbLabor",
            "RandomPlus.PanelOthers.IncapableOptions.AllowNone",
            "RandomPlus.PanelOthers.IncapableOptions.ForcedViolence",
            "RandomPlus.PanelOthers.IncapableOptions.Custom"
        };
        private const int CustomIncapableVersion = 2;
        public const string CustomIncapableViolentKey = "Violent";
        public const string CustomIncapableDumbLaborKey = "DumbLabor";
        public const string CustomIncapableFirefightingKey = "Firefighting";
        public const string CustomIncapableCleaningKey = "Cleaning";
        public const string CustomIncapableHaulingKey = "Hauling";
        public const string CustomIncapableChildcareKey = "Childcare";
        public const string BackstoryUncategorizedCategory = "Uncategorized";

        public string name;

        private List<SkillContainer> skills = new List<SkillContainer>();
        public IEnumerable<SkillContainer> Skills
        {
            get
            {
                foreach (var skill in skills)
                    yield return skill;
            }
        }

        #region Traits
        private List<TraitContainer> traits = new List<TraitContainer>();
        public IEnumerable<TraitContainer> Traits
        {
            get
            {
                foreach (var trait in traits)
                    yield return trait;
            }
        }

        public void AddTrait(Trait trait)
        {
            AddTrait(trait, TraitContainer.TraitFilterType.Required);
        }

        public void AddTrait(Trait trait, TraitContainer.TraitFilterType traitFilter)
        {
            traits.Add(new TraitContainer(trait, OnChange)
            {
                traitFilter = traitFilter
            });
            OnChange();
        }

        public void TraitUpdated(int index, Trait trait)
        {
            traits[index].trait = trait;
            OnChange();
        }

        public void TraitRemoved(Trait trait)
        {
            var needToRemoveTC = traits.FirstOrDefault(tc => tc.trait == trait);
            traits.Remove(needToRemoveTC);
            OnChange();
        }

        public void SetTraitsForFilterType(IEnumerable<Trait> selectedTraits, TraitContainer.TraitFilterType filterType)
        {
            var selected = selectedTraits == null ? new List<Trait>() : selectedTraits.Where(t => t != null).ToList();
            var selectedKeys = new HashSet<string>(selected.Select(TraitKey));
            bool excludedFilter = filterType == TraitContainer.TraitFilterType.Excluded;

            traits.RemoveAll(tc =>
                tc.trait != null &&
                (excludedFilter
                    ? tc.traitFilter == TraitContainer.TraitFilterType.Excluded
                    : tc.traitFilter != TraitContainer.TraitFilterType.Excluded) &&
                !selectedKeys.Contains(TraitKey(tc.trait)));

            foreach (Trait trait in selected)
            {
                string key = TraitKey(trait);
                bool alreadyExists = traits.Any(tc => tc.trait != null && TraitKey(tc.trait) == key);
                if (!alreadyExists)
                {
                    traits.Add(new TraitContainer(trait, OnChange)
                    {
                        traitFilter = filterType
                    });
                }
            }

            OnChange();
        }

        private static string TraitKey(Trait trait)
        {
            if (trait == null || trait.def == null)
                return "";
            return trait.def.defName + ":" + trait.Degree;
        }
        #endregion

        private int _RequiredTraitsInPool = DefaultPoolSize;
        public int RequiredTraitsInPool { get => _RequiredTraitsInPool; set => _RequiredTraitsInPool = value; }

        public IntRange passionRange;
        public IntRange skillRange;
        public bool countOnlyHighestAttack;
        public bool countOnlyPassion;

        public IntRange ageRange;

        private Gender gender;
        public Gender Gender
        {
            get => gender;
            set
            {
                gender = value;
                OnChange();
            }
        }

        private RerollAlgorithmOptions rerollAlgorithm = DefaultRerollAlgorithm;
        public RerollAlgorithmOptions RerollAlgorithm
        {
            get => rerollAlgorithm;
            set
            {
                rerollAlgorithm = value;
                OnChange();
            }
        }

        private int rerollLimit = (int)DefaultRerollLimit;
        public int RerollLimit
        {
            get => rerollLimit;
            set
            {
                rerollLimit = value;
                OnChange();
            }
        }

        private HealthOptions filterHealthCondition;
        private List<string> customAllowedHealthKeys = new List<string>();
        private int customHealthVersion = CustomHealthVersion;
        public HealthOptions FilterHealthCondition
        {
            get => filterHealthCondition;
            set
            {
                filterHealthCondition = value;
                OnChange();
            }
        }

        public IEnumerable<string> CustomAllowedHealthKeys
        {
            get
            {
                EnsureCustomAllowedHealthKeys();
                foreach (string key in customAllowedHealthKeys)
                    yield return key;
            }
        }

        public int CustomAllowedHealthCount
        {
            get
            {
                EnsureCustomAllowedHealthKeys();
                return customAllowedHealthKeys.Count;
            }
        }

        private IncapableOptions filterIncapable;
        private List<string> customAllowedIncapableKeys = new List<string>();
        private int customIncapableVersion = CustomIncapableVersion;
        private List<string> allowedChildhoodBackstoryDefNames = new List<string>();
        private List<string> allowedAdulthoodBackstoryDefNames = new List<string>();
        public IncapableOptions FilterIncapable
        {
            get => filterIncapable;
            set
            {
                filterIncapable = value;
                OnChange();
            }
        }

        public IEnumerable<string> CustomAllowedIncapableKeys
        {
            get
            {
                EnsureCustomAllowedIncapableKeys();
                foreach (string key in customAllowedIncapableKeys)
                    yield return key;
            }
        }

        public int CustomAllowedIncapableCount
        {
            get
            {
                EnsureCustomAllowedIncapableKeys();
                return customAllowedIncapableKeys.Count;
            }
        }

        public IEnumerable<string> AllowedChildhoodBackstoryDefNames
        {
            get
            {
                EnsureBackstoryDefNames(BackstorySlot.Childhood);
                foreach (string defName in allowedChildhoodBackstoryDefNames)
                    yield return defName;
            }
        }

        public IEnumerable<string> AllowedAdulthoodBackstoryDefNames
        {
            get
            {
                EnsureBackstoryDefNames(BackstorySlot.Adulthood);
                foreach (string defName in allowedAdulthoodBackstoryDefNames)
                    yield return defName;
            }
        }

        public int AllowedBackstoryCount(BackstorySlot slot)
        {
            EnsureBackstoryDefNames(slot);
            return GetBackstoryList(slot).Count;
        }

        public int TotalBackstoryCount(BackstorySlot slot)
        {
            return GetAllBackstoryDefNames(slot).Count;
        }

        public bool HasBackstoryFilter(BackstorySlot slot)
        {
            return AllowedBackstoryCount(slot) < TotalBackstoryCount(slot);
        }

        public PawnFilter()
        {
            ResetAll();
        }

        public void ResetSkills()
        {
            skills.Clear();
            foreach (var skilldef in DefDatabase<SkillDef>.AllDefs)
            {
                skills.Add(new SkillContainer(skilldef, OnChange));
            }
            passionRange = new IntRange(PassionMinDefault, PassionMaxDefault);
            skillRange = new IntRange(SkillMinDefault, SkillMaxDefault);
            countOnlyHighestAttack = false;
            countOnlyPassion = false;
            OnChange();
        }

        public void ResetTraits()
        {
            traits.Clear();
            RequiredTraitsInPool = DefaultPoolSize;
            OnChange();
        }

        public void ResetOther()
        {
            RerollAlgorithm = DefaultRerollAlgorithm;
            gender = Gender.None;
            rerollLimit = (int)DefaultRerollLimit;
            filterHealthCondition = HealthOptions.AllowAll;
            customAllowedHealthKeys = GetAllCustomHealthKeys();
            customHealthVersion = CustomHealthVersion;
            filterIncapable = IncapableOptions.AllowAll;
            customAllowedIncapableKeys = GetAllCustomIncapableKeys();
            customIncapableVersion = CustomIncapableVersion;
            allowedChildhoodBackstoryDefNames = GetAllBackstoryDefNames(BackstorySlot.Childhood);
            allowedAdulthoodBackstoryDefNames = GetAllBackstoryDefNames(BackstorySlot.Adulthood);

            ageRange = new IntRange(MinAgeDefault, MaxAgeDefault);
            OnChange();
        }

        public static List<BackstoryDef> GetFilterableBackstories(BackstorySlot slot)
        {
            return DefDatabase<BackstoryDef>.AllDefsListForReading
                .Where(backstory => backstory != null && backstory.slot == slot && IsGeneratedBackstory(backstory))
                .OrderBy(backstory => GetBackstoryTitle(backstory))
                .ThenBy(backstory => backstory.defName)
                .ToList();
        }

        private static bool IsGeneratedBackstory(BackstoryDef backstory)
        {
            if (backstory.shuffleable)
                return true;

            return !string.IsNullOrEmpty(backstory.identifier);
        }

        public static List<string> GetAllBackstoryDefNames(BackstorySlot slot)
        {
            return GetFilterableBackstories(slot)
                .Select(backstory => backstory.defName)
                .ToList();
        }

        public static List<string> GetBackstoryCategories(BackstorySlot slot)
        {
            return GetFilterableBackstories(slot)
                .SelectMany(GetBackstoryCategories)
                .Distinct()
                .OrderBy(GetBackstoryCategorySortKey)
                .ToList();
        }

        public static HashSet<string> GetRelevantBackstoryCategories(BackstorySlot slot)
        {
            var categories = new HashSet<string>();

            try
            {
                PawnGenerationRequest request = StartingPawnUtility.GetGenerationRequest(0);
                request.ValidateAndFix();

                AddPawnKindBackstoryCategories(categories, request.KindDef, slot);
                AddFactionBackstoryCategories(categories, request.Faction?.def, slot);

                if (categories.Count == 0)
                    AddFactionBackstoryCategories(categories, Find.FactionManager?.OfPlayer?.def, slot);
            }
            catch
            {
                return null;
            }

            return categories.Count > 0 ? categories : null;
        }

        public static bool IsBackstoryRelevantToCategories(BackstoryDef backstory, HashSet<string> relevantCategories)
        {
            if (relevantCategories == null)
                return true;
            if (backstory?.spawnCategories == null || backstory.spawnCategories.Count == 0)
                return true;

            return GetBackstoryCategories(backstory).Any(category => relevantCategories.Contains(category));
        }

        private static void AddPawnKindBackstoryCategories(HashSet<string> categories, PawnKindDef pawnKindDef, BackstorySlot slot)
        {
            if (pawnKindDef == null)
                return;

            AddBackstoryFilterCategories(categories, pawnKindDef.backstoryFiltersOverride, slot);
            AddBackstoryFilterCategories(categories, pawnKindDef.backstoryFilters, slot);
        }

        private static void AddFactionBackstoryCategories(HashSet<string> categories, FactionDef factionDef, BackstorySlot slot)
        {
            if (factionDef == null)
                return;

            AddBackstoryFilterCategories(categories, factionDef.backstoryFilters, slot);
        }

        private static void AddBackstoryFilterCategories(HashSet<string> categories, IEnumerable<BackstoryCategoryFilter> filters, BackstorySlot slot)
        {
            if (filters == null)
                return;

            foreach (BackstoryCategoryFilter filter in filters)
            {
                if (filter == null)
                    continue;

                AddCategories(categories, filter.categories);
                if (slot == BackstorySlot.Childhood)
                    AddCategories(categories, filter.categoriesChildhood);
                else if (slot == BackstorySlot.Adulthood)
                    AddCategories(categories, filter.categoriesAdulthood);
            }
        }

        private static void AddCategories(HashSet<string> categories, IEnumerable<string> categoryList)
        {
            if (categoryList == null)
                return;

            foreach (string category in categoryList)
            {
                if (!string.IsNullOrEmpty(category))
                    categories.Add(category);
            }
        }

        public static IEnumerable<string> GetBackstoryCategories(BackstoryDef backstory)
        {
            if (backstory?.spawnCategories == null || backstory.spawnCategories.Count == 0)
            {
                yield return BackstoryUncategorizedCategory;
                yield break;
            }

            foreach (string category in backstory.spawnCategories)
            {
                if (!string.IsNullOrEmpty(category))
                    yield return category;
            }
        }

        private static string GetBackstoryCategorySortKey(string category)
        {
            switch (category)
            {
                case "Outlander": return "00_Outlander";
                case "Tribal": return "01_Tribal";
                case "Offworld": return "02_Offworld";
                case "Pirate": return "03_Pirate";
                case "ImperialCommon": return "04_ImperialCommon";
                case "ImperialFighter": return "05_ImperialFighter";
                case "ImperialRoyal": return "06_ImperialRoyal";
                case BackstoryUncategorizedCategory: return "99_Uncategorized";
                default: return "50_" + category;
            }
        }

        public static string GetBackstoryCategoryLabel(string category)
        {
            if (category == BackstoryUncategorizedCategory)
                return "RandomPlus.PanelOthers.BackstoryFilter.Uncategorized".Translate();
            return category;
        }

        public static string GetBackstoryTitle(BackstoryDef backstory)
        {
            if (backstory == null)
                return "";
            if (!string.IsNullOrEmpty(backstory.title))
                return backstory.title.CapitalizeFirst();
            return backstory.defName;
        }

        public static string GetBackstoryOptionLabel(BackstoryDef backstory)
        {
            string title = GetBackstoryTitle(backstory);
            string forcedTraits = GetBackstoryForcedTraitsText(backstory);
            return string.IsNullOrEmpty(forcedTraits) ? title : $"{title} ({forcedTraits})";
        }

        public static string GetBackstoryTooltip(BackstoryDef backstory)
        {
            if (backstory == null)
                return "";

            var parts = new List<string>();

            string description = CleanBackstoryDescription(backstory.description);
            if (!string.IsNullOrEmpty(description))
                parts.Add(description);

            List<string> stats = GetBackstorySkillGainLines(backstory);
            if (stats.Any())
                parts.Add(string.Join("\n", stats));

            List<string> disabledWork = GetBackstoryDisabledWorkLines(backstory);
            if (disabledWork.Any())
                parts.Add(string.Join("\n", disabledWork));

            return string.Join("\n\n", parts);
        }

        private static string CleanBackstoryDescription(string description)
        {
            if (string.IsNullOrEmpty(description))
                return "";

            return description
                .Replace("[PAWN_possessive]self", "themself")
                .Replace("[PAWN_nameDef]", "This pawn")
                .Replace("[PAWN_pronoun]", "they")
                .Replace("[PAWN_possessive]", "their")
                .Replace("[PAWN_objective]", "them");
        }

        private static List<string> GetBackstorySkillGainLines(BackstoryDef backstory)
        {
            if (backstory?.skillGains == null || backstory.skillGains.Count == 0)
                return new List<string>();

            return backstory.skillGains
                .Where(entry => entry.skill != null && entry.amount != 0)
                .OrderByDescending(entry => entry.skill.listOrder)
                .Select(entry => $"{entry.skill.LabelCap}: {FormatSignedValue(entry.amount)}")
                .ToList();
        }

        private static List<string> GetBackstoryDisabledWorkLines(BackstoryDef backstory)
        {
            var lines = new List<string>();
            if (backstory == null || backstory.workDisables == WorkTags.None)
                return lines;

            foreach (WorkTypeDef workType in DefDatabase<WorkTypeDef>.AllDefs.OrderBy(def => def.naturalPriority))
            {
                if (workType == null || (workType.workTags & backstory.workDisables) == WorkTags.None)
                    continue;

                string label = !string.IsNullOrEmpty(workType.gerundLabel)
                    ? workType.gerundLabel.CapitalizeFirst()
                    : workType.LabelCap.ToString();
                if (!string.IsNullOrEmpty(label))
                    lines.Add($"{label} disabled");
            }

            return lines.Distinct().ToList();
        }

        private static string FormatSignedValue(int value)
        {
            return value > 0 ? $"+{value}" : value.ToString();
        }

        private static string GetBackstoryForcedTraitsText(BackstoryDef backstory)
        {
            if (backstory?.forcedTraits == null || backstory.forcedTraits.Count == 0)
                return "";

            var labels = new List<string>();
            foreach (var forcedTrait in backstory.forcedTraits)
            {
                if (forcedTrait.def == null)
                    continue;
                Trait trait = new Trait(forcedTrait.def, forcedTrait.degree, true);
                labels.Add(trait.LabelCap);
            }

            return string.Join(", ", labels);
        }

        public void SetAllowedBackstoryDefNames(BackstorySlot slot, IEnumerable<string> selectedDefNames)
        {
            var validDefNames = new HashSet<string>(GetAllBackstoryDefNames(slot));
            List<string> selected = (selectedDefNames ?? Enumerable.Empty<string>())
                .Where(defName => !string.IsNullOrEmpty(defName) && validDefNames.Contains(defName))
                .Distinct()
                .ToList();

            if (slot == BackstorySlot.Childhood)
                allowedChildhoodBackstoryDefNames = selected;
            else if (slot == BackstorySlot.Adulthood)
                allowedAdulthoodBackstoryDefNames = selected;

            OnChange();
        }

        public bool IsBackstoryAllowed(BackstoryDef backstory, BackstorySlot slot)
        {
            EnsureBackstoryDefNames(slot);
            List<string> selected = GetBackstoryList(slot);
            if (selected.Count >= TotalBackstoryCount(slot))
                return true;

            if (backstory == null)
                return false;

            return selected.Contains(backstory.defName);
        }

        public bool AreBackstoriesAllowed(Pawn pawn)
        {
            if (pawn?.story == null)
                return true;

            return IsBackstoryAllowed(pawn.story.Childhood, BackstorySlot.Childhood)
                && IsBackstoryAllowed(pawn.story.Adulthood, BackstorySlot.Adulthood);
        }

        public List<BackstoryDef> GetAllowedBackstories(BackstorySlot slot)
        {
            EnsureBackstoryDefNames(slot);
            HashSet<string> selectedDefNames = new HashSet<string>(GetBackstoryList(slot));
            return GetFilterableBackstories(slot)
                .Where(backstory => selectedDefNames.Contains(backstory.defName))
                .ToList();
        }

        public bool HasAnyCompatibleSelectedBackstoryPair()
        {
            List<BackstoryDef> childhoodBackstories = GetAllowedBackstories(BackstorySlot.Childhood);
            List<BackstoryDef> adulthoodBackstories = GetAllowedBackstories(BackstorySlot.Adulthood);

            if (childhoodBackstories.Count == 0 || adulthoodBackstories.Count == 0)
                return false;

            foreach (BackstoryDef childhood in childhoodBackstories)
            {
                foreach (BackstoryDef adulthood in adulthoodBackstories)
                {
                    if (AreBackstoriesCompatible(childhood, adulthood))
                        return true;
                }
            }

            return false;
        }

        public static bool AreBackstoriesCompatible(BackstoryDef childhood, BackstoryDef adulthood)
        {
            if (childhood == null || adulthood == null)
                return false;

            return !BackstoryForcedTraitsConflict(childhood, adulthood) &&
                !BackstoryRequiredWorkTagsConflict(childhood, adulthood);
        }

        private static bool BackstoryForcedTraitsConflict(BackstoryDef first, BackstoryDef second)
        {
            return BackstoryForcesDisallowedTrait(first, second) ||
                BackstoryForcesDisallowedTrait(second, first);
        }

        private static bool BackstoryForcesDisallowedTrait(BackstoryDef forcedSource, BackstoryDef disallowedSource)
        {
            if (forcedSource?.forcedTraits == null || disallowedSource?.disallowedTraits == null)
                return false;

            foreach (var forcedTrait in forcedSource.forcedTraits)
            {
                if (forcedTrait.def == null)
                    continue;

                foreach (var disallowedTrait in disallowedSource.disallowedTraits)
                {
                    if (disallowedTrait.def == forcedTrait.def)
                        return true;
                }
            }

            return false;
        }

        private static bool BackstoryRequiredWorkTagsConflict(BackstoryDef childhood, BackstoryDef adulthood)
        {
            WorkTags childhoodRequired = childhood.requiredWorkTags;
            WorkTags adulthoodRequired = adulthood.requiredWorkTags;
            WorkTags childhoodDisabled = childhood.workDisables;
            WorkTags adulthoodDisabled = adulthood.workDisables;

            return (childhoodRequired & adulthoodDisabled) != WorkTags.None ||
                (adulthoodRequired & childhoodDisabled) != WorkTags.None;
        }

        private void EnsureBackstoryDefNames(BackstorySlot slot)
        {
            List<string> list = GetBackstoryList(slot);
            if (list == null)
            {
                SetBackstoryList(slot, GetAllBackstoryDefNames(slot));
                return;
            }

            List<string> allDefNames = GetAllBackstoryDefNames(slot);
            var validDefNames = new HashSet<string>(allDefNames);
            list.RemoveAll(defName => string.IsNullOrEmpty(defName) || !validDefNames.Contains(defName));
            ExpandLegacyAllBackstoriesSelection(slot, list, allDefNames);
        }

        private void ExpandLegacyAllBackstoriesSelection(BackstorySlot slot, List<string> list, List<string> allDefNames)
        {
            if (list == null || list.Count == 0 || list.Count >= allDefNames.Count)
                return;

            var selected = new HashSet<string>(list);
            foreach (BackstoryDef backstory in GetFilterableBackstories(slot))
            {
                if (!backstory.shuffleable)
                    continue;

                if (!selected.Contains(backstory.defName))
                    return;
            }

            SetBackstoryList(slot, new List<string>(allDefNames));
        }

        private List<string> GetBackstoryList(BackstorySlot slot)
        {
            return slot == BackstorySlot.Childhood
                ? allowedChildhoodBackstoryDefNames
                : allowedAdulthoodBackstoryDefNames;
        }

        private void SetBackstoryList(BackstorySlot slot, List<string> value)
        {
            if (slot == BackstorySlot.Childhood)
                allowedChildhoodBackstoryDefNames = value;
            else
                allowedAdulthoodBackstoryDefNames = value;
        }

        public static List<string> GetAllCustomHealthKeys()
        {
            return new List<string>
            {
                CustomHealthStartConditionsKey,
                CustomHealthPregnancyKey,
                CustomHealthPainKey,
                CustomHealthScarKey,
                CustomHealthAddictionKey,
                CustomHealthBadModificationsKey,
                CustomHealthGoodModificationsKey,
                CustomHealthOthersKey
            };
        }

        public void SetCustomAllowedHealthKeys(IEnumerable<string> selectedKeys)
        {
            var validKeys = new HashSet<string>(GetAllCustomHealthKeys());
            customAllowedHealthKeys = (selectedKeys ?? Enumerable.Empty<string>())
                .Where(key => !string.IsNullOrEmpty(key) && validKeys.Contains(key))
                .Distinct()
                .ToList();
            customHealthVersion = CustomHealthVersion;
            OnChange();
        }

        public bool CustomHealthAllows(string key)
        {
            EnsureCustomAllowedHealthKeys();
            return !string.IsNullOrEmpty(key) && customAllowedHealthKeys.Contains(key);
        }

        private void EnsureCustomAllowedHealthKeys()
        {
            if (customAllowedHealthKeys == null)
            {
                customAllowedHealthKeys = GetAllCustomHealthKeys();
                customHealthVersion = CustomHealthVersion;
                return;
            }

            var validKeys = new HashSet<string>(GetAllCustomHealthKeys());
            customAllowedHealthKeys.RemoveAll(key => string.IsNullOrEmpty(key) || !validKeys.Contains(key));

            if (customHealthVersion < CustomHealthVersion)
                customHealthVersion = CustomHealthVersion;
        }

        public static List<string> GetAllCustomIncapableKeys()
        {
            var keys = DefDatabase<SkillDef>.AllDefs
                .OrderByDescending(skill => skill.listOrder)
                .Where(skill => skill.defName != "Shooting" && skill.defName != "Melee")
                .Select(skill => skill.defName)
                .ToList();
            keys.Insert(0, CustomIncapableViolentKey);
            keys.Add(CustomIncapableFirefightingKey);
            keys.Add(CustomIncapableDumbLaborKey);
            keys.Add(CustomIncapableCleaningKey);
            keys.Add(CustomIncapableHaulingKey);
            keys.Add(CustomIncapableChildcareKey);
            return keys;
        }

        public void SetCustomAllowedIncapableKeys(IEnumerable<string> selectedKeys)
        {
            var validKeys = new HashSet<string>(GetAllCustomIncapableKeys());
            customAllowedIncapableKeys = (selectedKeys ?? Enumerable.Empty<string>())
                .Where(key => !string.IsNullOrEmpty(key) && validKeys.Contains(key))
                .Distinct()
                .ToList();
            OnChange();
        }

        public WorkTags GetCustomDisallowedIncapableWorkTags()
        {
            EnsureCustomAllowedIncapableKeys();
            var selectedKeys = new HashSet<string>(customAllowedIncapableKeys);
            WorkTags disallowedTags = WorkTags.None;
            foreach (string key in GetAllCustomIncapableKeys())
            {
                if (!selectedKeys.Contains(key))
                    disallowedTags |= GetCustomIncapableWorkTags(key);
            }
            return disallowedTags;
        }

        public bool CustomIncapableAllows(WorkTags disabledTags)
        {
            return (disabledTags & GetCustomDisallowedIncapableWorkTags()) == WorkTags.None;
        }

        private void EnsureCustomAllowedIncapableKeys()
        {
            if (customAllowedIncapableKeys == null)
            {
                customAllowedIncapableKeys = GetAllCustomIncapableKeys();
                customIncapableVersion = CustomIncapableVersion;
                return;
            }

            MigrateCustomAllowedIncapableKeys();

            var validKeys = new HashSet<string>(GetAllCustomIncapableKeys());
            customAllowedIncapableKeys.RemoveAll(key => string.IsNullOrEmpty(key) || !validKeys.Contains(key));
        }

        private void MigrateCustomAllowedIncapableKeys()
        {
            if (customIncapableVersion >= CustomIncapableVersion)
                return;

            bool allowedShooting = customAllowedIncapableKeys.Contains("Shooting");
            bool allowedMelee = customAllowedIncapableKeys.Contains("Melee");
            if (allowedShooting || allowedMelee)
                customAllowedIncapableKeys.Add(CustomIncapableViolentKey);

            if (!customAllowedIncapableKeys.Contains(CustomIncapableChildcareKey))
                customAllowedIncapableKeys.Add(CustomIncapableChildcareKey);

            customIncapableVersion = CustomIncapableVersion;
        }

        public static WorkTags GetCustomIncapableWorkTags(string key)
        {
            if (key == CustomIncapableViolentKey)
                return ParseWorkTags("Violent");
            if (key == CustomIncapableDumbLaborKey)
                return ParseWorkTags("ManualDumb");
            if (key == CustomIncapableFirefightingKey)
                return ParseWorkTags("Firefighting");
            if (key == CustomIncapableCleaningKey)
                return ParseWorkTags("ManualDumb", "Cleaning");
            if (key == CustomIncapableHaulingKey)
                return ParseWorkTags("ManualDumb", "Hauling");
            if (key == CustomIncapableChildcareKey)
                return ParseWorkTags("Social", "Caring");

            SkillDef skillDef = DefDatabase<SkillDef>.GetNamedSilentFail(key);
            if (skillDef == null)
                return WorkTags.None;

            return GetSkillIncapableWorkTags(skillDef);
        }

        private static WorkTags GetSkillIncapableWorkTags(SkillDef skillDef)
        {
            WorkTags tags = skillDef.disablingWorkTags;
            switch (skillDef.defName)
            {
                case "Construction":
                    return tags | ParseWorkTags("ManualSkilled", "Constructing");
                case "Mining":
                    return tags | ParseWorkTags("ManualSkilled", "Mining");
                case "Cooking":
                    return tags | ParseWorkTags("ManualSkilled", "Cooking");
                case "Plants":
                    return tags | ParseWorkTags("ManualSkilled", "PlantWork");
                case "Animals":
                    return tags | ParseWorkTags("Animals");
                case "Crafting":
                    return tags | ParseWorkTags("ManualSkilled", "Crafting");
                case "Artistic":
                    return tags | ParseWorkTags("Artistic");
                case "Medicine":
                    return tags | ParseWorkTags("Caring");
                case "Social":
                    return tags | ParseWorkTags("Social");
                case "Intellectual":
                    return tags | ParseWorkTags("Intellectual");
                default:
                    return tags | ParseWorkTags(skillDef.defName);
            }
        }

        private static WorkTags ParseWorkTags(params string[] tagNames)
        {
            WorkTags tags = WorkTags.None;
            foreach (string tagName in tagNames)
            {
                try
                {
                    tags |= (WorkTags)Enum.Parse(typeof(WorkTags), tagName);
                }
                catch
                {
                    // Ignore tags that do not exist in the active RimWorld version.
                }
            }
            return tags;
        }

        public void ResetAll()
        {
            ResetSkills();
            ResetTraits();
            ResetOther();
        }

        public bool HasAnyFilter()
        {
            if (skills != null && skills.Any(skill => skill.Passion != Passion.None || skill.MinValue > 0))
                return true;
            if (traits != null && traits.Any())
                return true;
            if (RequiredTraitsInPool != DefaultPoolSize)
                return true;
            if (passionRange.min != PassionMinDefault || passionRange.max != PassionMaxDefault)
                return true;
            if (skillRange.min != SkillMinDefault || skillRange.max != SkillMaxDefault)
                return true;
            if (countOnlyHighestAttack || countOnlyPassion)
                return true;
            if (ageRange.min != MinAgeDefault || ageRange.max != MaxAgeDefault)
                return true;
            if (RerollAlgorithm != DefaultRerollAlgorithm)
                return true;
            if (RerollLimit != (int)DefaultRerollLimit)
                return true;
            if (Gender != Gender.None)
                return true;
            if (FilterHealthCondition != HealthOptions.AllowAll)
                return true;
            if (FilterIncapable != IncapableOptions.AllowAll)
                return true;
            if (HasBackstoryFilter(BackstorySlot.Childhood) || HasBackstoryFilter(BackstorySlot.Adulthood))
                return true;

            return false;
        }

        public void OnChange()
        {

        }

        public void ExposeData()
        {
            int version = 1;
            Scribe_Values.Look(ref this.name, "name", "");
            Scribe_Values.Look(ref version, "version", 1);
            Scribe_Collections.Look(ref this.skills, "skills", LookMode.Deep, null);
            Scribe_Collections.Look(ref this.traits, "traits", LookMode.Deep, null);

            Scribe_Values.Look(ref _RequiredTraitsInPool, "poolSize", DefaultPoolSize);

            Scribe_Values.Look(ref passionRange.min, "passionRangeMin", PassionMinDefault);
            Scribe_Values.Look(ref passionRange.max, "passionRangeMax", PassionMaxDefault);

            Scribe_Values.Look(ref skillRange.min, "skillRangeMin", SkillMinDefault);
            Scribe_Values.Look(ref skillRange.max, "skillRangeMax", SkillMaxDefault);

            Scribe_Values.Look(ref countOnlyHighestAttack, "countOnlyHighestAttack", false);
            Scribe_Values.Look(ref countOnlyPassion, "countOnlyPassion", false);

            Scribe_Values.Look(ref ageRange.min, "ageRangeMin", MinAgeDefault);
            Scribe_Values.Look(ref ageRange.max, "ageRangeMax", MaxAgeDefault);

            Scribe_Values.Look(ref rerollAlgorithm, "rerollAlgorithm", DefaultRerollAlgorithm);
            Scribe_Values.Look(ref rerollLimit, "rerollLimit", (int)DefaultRerollLimit);
            Scribe_Values.Look(ref gender, "gender", Gender.None);
            Scribe_Values.Look(ref filterHealthCondition, "healthCondition", HealthOptions.AllowAll);
            Scribe_Values.Look(ref customHealthVersion, "customHealthVersion", 0);
            Scribe_Collections.Look(ref customAllowedHealthKeys, "customAllowedHealthKeys", LookMode.Value);
            Scribe_Values.Look(ref filterIncapable, "incapable", IncapableOptions.AllowAll);
            Scribe_Values.Look(ref customIncapableVersion, "customIncapableVersion", 0);
            Scribe_Collections.Look(ref customAllowedIncapableKeys, "customAllowedIncapableKeys", LookMode.Value);
            Scribe_Collections.Look(ref allowedChildhoodBackstoryDefNames, "allowedChildhoodBackstoryDefNames", LookMode.Value);
            Scribe_Collections.Look(ref allowedAdulthoodBackstoryDefNames, "allowedAdulthoodBackstoryDefNames", LookMode.Value);

            switch (Scribe.mode)
            {
                case LoadSaveMode.Saving:

                    break;
                case LoadSaveMode.LoadingVars:
                    EnsureCustomAllowedHealthKeys();
                    EnsureCustomAllowedIncapableKeys();
                    EnsureBackstoryDefNames(BackstorySlot.Childhood);
                    EnsureBackstoryDefNames(BackstorySlot.Adulthood);
                    break;
            }
        }
    }
}
