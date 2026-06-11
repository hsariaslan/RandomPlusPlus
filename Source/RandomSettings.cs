using Verse;
using RimWorld;
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.InteropServices;
using Verse.AI;

namespace RandomPlus
{
    public class RandomSettings
    {
        static MethodInfo randomAgeMethodInfo;
        static MethodInfo randomTraitMethodInfo;
        static MethodInfo randomSkillMethodInfo;
        static MethodInfo randomHealthMethodInfo;
        static MethodInfo randomBodyTypeMethodInfo;
        static MethodInfo randomGeneMethodInfo;

        // Cached delegates for fast invocation (avoid MethodInfo.Invoke boxing overhead)
        static Action<Pawn, PawnGenerationRequest> generateAge;
        static Action<Pawn, PawnGenerationRequest> generateTraits;
        static Action<Pawn, PawnGenerationRequest> generateSkills;
        static Action<Pawn, PawnGenerationRequest> generateHealth;
        static Action<Pawn, PawnGenerationRequest> generateBodyType;
        static Action<Pawn, XenotypeDef, PawnGenerationRequest> generateGenes;

        static PropertyInfo startingAndOptionalPawnsPropertyInfo;

        // Cached HediffDef references for OnlyStartCondition health check (ref equality vs string compare)
        static HediffDef cryptosleepSicknessDef;
        static HediffDef malnutritionDef;

        public static int MinSkillRange;

        public static int randomRerollCounter = 0;

        // Set by RerollUltraFast when the user right-clicks to cancel; consumed by
        // Patch_RandomizeMethod to break out of its outer retry loop without re-randomizing.
        public static bool RerollCancelledByUser = false;

        public static List<PawnFilter> pawnFilterList = new List<PawnFilter>();

        private static PawnFilter pawnFilter;
        public static PawnFilter PawnFilter
        {
            get { return pawnFilter; }
            set { pawnFilter = value; }
        }

        public static int RandomRerollCounter()
        {
            return randomRerollCounter;
        }

        public static void Init()
        {
            pawnFilter = new PawnFilter();

            // RimWorld 1.6: Updated reflection for potentially changed method signatures
            try
            {
                randomAgeMethodInfo = typeof(PawnGenerator)
                    .GetMethod("GenerateRandomAge", BindingFlags.NonPublic | BindingFlags.Static);

                randomTraitMethodInfo = typeof(PawnGenerator)
                    .GetMethod("GenerateTraits", BindingFlags.NonPublic | BindingFlags.Static);

                randomSkillMethodInfo = typeof(PawnGenerator)
                    .GetMethod("GenerateSkills", BindingFlags.NonPublic | BindingFlags.Static);

                randomHealthMethodInfo = typeof(PawnGenerator)
                    .GetMethod("GenerateInitialHediffs", BindingFlags.NonPublic | BindingFlags.Static);

                randomBodyTypeMethodInfo = typeof(PawnGenerator)
                    .GetMethod("GenerateBodyType", BindingFlags.NonPublic | BindingFlags.Static);

                randomGeneMethodInfo = typeof(PawnGenerator)
                    .GetMethod("GenerateGenes", BindingFlags.NonPublic | BindingFlags.Static);

                startingAndOptionalPawnsPropertyInfo = typeof(StartingPawnUtility)
                    .GetProperty("StartingAndOptionalPawns", BindingFlags.NonPublic | BindingFlags.Static);
            }
            catch (Exception ex)
            {
                Log.Error($"RandomPlus: Failed to initialize reflection methods: {ex.Message}");
            }

            // Create cached delegates for Ultra Fast (avoid MethodInfo.Invoke boxing overhead)
            generateAge = CreateFastDelegate(randomAgeMethodInfo);
            generateTraits = CreateFastDelegate(randomTraitMethodInfo);
            generateSkills = CreateFastDelegate(randomSkillMethodInfo);
            generateHealth = CreateFastDelegate(randomHealthMethodInfo);
            generateBodyType = CreateFastDelegate(randomBodyTypeMethodInfo);
            generateGenes = CreateFastGeneDelegate(randomGeneMethodInfo);

            // Cache start-condition HediffDef references for fast ref-equality in health check
            cryptosleepSicknessDef = DefDatabase<HediffDef>.GetNamedSilentFail("CryptosleepSickness");
            malnutritionDef = DefDatabase<HediffDef>.GetNamedSilentFail("Malnutrition");
        }

        public static void ResetRerollCounter()
        {
            randomRerollCounter = 0;
        }

        public static void Reroll(int pawnIndex)
        {
            List<Pawn> pawnList = (List<Pawn>)startingAndOptionalPawnsPropertyInfo.GetValue(null);
            Pawn pawn = pawnList[pawnIndex];

            SpouseRelationUtility.Notify_PawnRegenerated(pawn);
            pawn = StartingPawnUtility.RandomizeInPlace(pawn);

            randomRerollCounter++;

            if (CheckPawnIsSatisfied(pawn))
                return;

            if (PawnFilter.RerollAlgorithm == PawnFilter.RerollAlgorithmOptions.Normal ||
                Find.WindowStack.currentlyDrawnWindow is Dialog_ChooseNewWanderers)
            {
                while (true)
                {
                    if (CheckPawnIsSatisfied(pawn))
                        break;

                    SpouseRelationUtility.Notify_PawnRegenerated(pawn);
                    pawn = StartingPawnUtility.RandomizeInPlace(pawn);

                    randomRerollCounter++;
                }
                return;
            }

            if (PawnFilter.RerollAlgorithm == PawnFilter.RerollAlgorithmOptions.UltraFast)
            {
                RerollUltraFast(pawn);
                return;
            }

            int index = StartingPawnUtility.PawnIndex(pawn);
            PawnGenerationRequest request = StartingPawnUtility.GetGenerationRequest(index);
            request.ValidateAndFix();

            // RimWorld 1.6: Enhanced faction handling
            Faction faction1;
            Faction faction2 = request.Faction ??
                (!Find.FactionManager.TryGetRandomNonColonyHumanlikeFaction(out faction1, false, true)
                    ? Faction.OfAncients : faction1);

            XenotypeDef xenotype = ModsConfig.BiotechActive ? PawnGenerator.GetXenotypeForGeneratedPawn(request) : null;

            while (randomRerollCounter < PawnFilter.RerollLimit)
            {
                try
                {
                    randomRerollCounter++;

                    PawnGenerator.RedressPawn(pawn, request);

                    // RimWorld 1.6: Safer age generation with null checks
                    pawn.ageTracker = new Pawn_AgeTracker(pawn);
                    if (randomAgeMethodInfo != null)
                    {
                        randomAgeMethodInfo.Invoke(null, new object[] { pawn, request });
                    }
                    else
                    {
                        // Fallback if reflection fails
                        pawn.ageTracker.AgeBiologicalTicks = (long)(Rand.Range(16, 65) * 3600000L);
                        pawn.ageTracker.AgeChronologicalTicks = pawn.ageTracker.AgeBiologicalTicks;
                    }
                    
                    if (!CheckAgeIsSatisfied(pawn))
                        continue;

                    pawn.story.traits = new TraitSet(pawn);
                    pawn.skills = new Pawn_SkillTracker(pawn);

                    PawnBioAndNameGenerator.GiveAppropriateBioAndNameTo(pawn, faction2.def, request, xenotype);
                    
                    // RimWorld 1.6: Safe method invocation with null checks
                    randomTraitMethodInfo?.Invoke(null, new object[] { pawn, request });
                    randomSkillMethodInfo?.Invoke(null, new object[] { pawn, request });
                    
                    if (!CheckSkillsIsSatisfied(pawn) || !CheckTraitsIsSatisfied(pawn))
                        continue;

                    // RimWorld 1.6: Improved health generation loop with better error handling
                    bool healthGenSuccess = false;
                    for (int i = 0; i < 100 && !healthGenSuccess; i++)
                    {
                        pawn.health.Reset();
                        try
                        {
                            // Internally, this method only adds custom Scenario health
                            Find.Scenario.Notify_NewPawnGenerating(pawn, request.Context);
                            randomHealthMethodInfo?.Invoke(null, new object[] { pawn, request });
                            
                            if (!(pawn.Dead || pawn.Destroyed || pawn.Downed))
                            {
                                healthGenSuccess = true;
                            }
                        }
                        catch (Exception ex)
                        {
                            Log.Warning($"RandomPlus: Health generation failed on attempt {i}: {ex.Message}");
                            continue;
                        }
                    }
                    
                    if (!CheckHealthIsSatisfied(pawn))
                        continue;

                    pawn.workSettings?.EnableAndInitialize();
                    if (!CheckWorkIsSatisfied(pawn))
                        continue;

                    // Handle custom scenario e.g forced traits
                    Find.Scenario.Notify_PawnGenerated(pawn, request.Context, true);
                    if (!CheckPawnIsSatisfied(pawn))
                        continue;

                    // RimWorld 1.6: Enhanced gene and body type generation
                    if (ModsConfig.BiotechActive)
                    {
                        pawn.genes = new Pawn_GeneTracker(pawn);
                        randomGeneMethodInfo?.Invoke(null, new object[] { pawn, xenotype, request });
                    }
                    
                    randomBodyTypeMethodInfo?.Invoke(null, new object[] { pawn, request });
                    GeneratePawnStyle(pawn);

                    return;
                }
                catch (Exception ex)
                {
                    Log.Warning($"RandomPlus: Error during pawn generation (attempt {randomRerollCounter}): {ex.Message}");
                    try
                    {
                        Find.WorldPawns.RemoveAndDiscardPawnViaGC(pawn);
                        SpouseRelationUtility.Notify_PawnRegenerated(pawn);
                        pawn = StartingPawnUtility.RandomizeInPlace(pawn);
                    }
                    catch (Exception ex2)
                    {
                        Log.Error($"RandomPlus: Critical error in pawn cleanup: {ex2.Message}");
                        break; // Exit to prevent infinite loop
                    }
                }
            }
        }

        public static bool CheckPawnIsSatisfied(Pawn pawn)
        {
            if (RandomRerollCounter() >= PawnFilter.RerollLimit)
            {
                return true;
            }
            if (!CheckGenderIsSatisfied(pawn))
                return false;
            if (!CheckSkillsIsSatisfied(pawn))
                return false;
            if (!CheckTraitsIsSatisfied(pawn))
                return false;
            if (!CheckHealthIsSatisfied(pawn))
                return false;
            if (!CheckWorkIsSatisfied(pawn))
                return false;
            if (!CheckAgeIsSatisfied(pawn))
                return false;
            return true;
        }

        public static bool CheckAgeIsSatisfied(Pawn pawn)
        {
            if (pawnFilter.ageRange.min != PawnFilter.MinAgeDefault ||
                pawnFilter.ageRange.max != PawnFilter.MaxAgeDefault)
            {
                if (pawnFilter.ageRange.min > pawn.ageTracker.AgeBiologicalYears ||
                    (pawnFilter.ageRange.max != PawnFilter.MaxAgeDefault && pawnFilter.ageRange.max < pawn.ageTracker.AgeBiologicalYears))
                    return false;
            }
            return true;
        }

        public static bool CheckGenderIsSatisfied(Pawn pawn)
        {
            if (pawnFilter.Gender != Gender.None && pawn.gender != Gender.None)
                if (pawnFilter.Gender != pawn.gender)
                    return false;
            return true;
        }

        public static bool CheckSkillsIsSatisfied(Pawn pawn)
        {
            List<SkillRecord> skillList = pawn.skills.skills;
            
            foreach (var skillFilter in pawnFilter.Skills)
            {
                if (skillFilter.Passion != Passion.None ||
                    skillFilter.MinValue > 0)
                {
                    var skillRecord = skillList.FirstOrDefault(i => i.def == skillFilter.SkillDef);
                    if (skillRecord != null)
                    {
                        if (skillRecord.passion < skillFilter.Passion ||
                            skillRecord.Level < skillFilter.MinValue)
                        {
                            return false;
                        }
                    }
                    else
                    {
                        Log.Error("RandomPlus: Skill record not found - this shouldn't happen!");
                    }
                }
            }

            // handle total passion range
            if (pawnFilter.passionRange.min > PawnFilter.PassionMinDefault ||
                pawnFilter.passionRange.max < PawnFilter.PassionMaxDefault)
            {
                int totalPassions = skillList.Where(skill => skill.passion > 0).Count();
                if (totalPassions < pawnFilter.passionRange.min ||
                    totalPassions > pawnFilter.passionRange.max)
                {
                    return false;
                }
            }

            // handle total skill range
            if (pawnFilter.skillRange.min != PawnFilter.SkillMinDefault ||
                pawnFilter.skillRange.max != PawnFilter.SkillMaxDefault)
            {
                int skillTotalCounter = 0;
                for (int i = 0; i < skillList.Count; i++)
                {
                    var skill = skillList[i];
                    if (PawnFilter.countOnlyHighestAttack)
                    {
                        if (i == 0) // Shooting[i=0] Melee[i=1]
                        {
                            var meleeSkill = skillList[1];
                            skillTotalCounter += meleeSkill.Level > skill.Level ? meleeSkill.Level : skill.Level;
                            i = 1; // skip next loop (melee)
                            continue;
                        }
                    }
                    if (PawnFilter.countOnlyPassion)
                    {
                        if (skill.passion > 0)
                            skillTotalCounter += skill.Level;
                    }
                    else
                    {
                        skillTotalCounter += skill.Level;
                    }
                }
                
                if (pawnFilter.skillRange.min > skillTotalCounter ||
                pawnFilter.skillRange.max < skillTotalCounter)
                    return false;
            }

            return true;
        }

        public static bool CheckTraitsIsSatisfied(Pawn pawn)
        {
            if (Page_RandomEditor.MOD_WELL_MET)
                return true;

            // handle required and exclude traits
            var traitFilterList = pawnFilter.Traits;
            foreach (var traitContainer in traitFilterList)
            {
                bool has = HasTrait(pawn, traitContainer.trait);

                switch (traitContainer.traitFilter)
                {
                    case TraitContainer.TraitFilterType.Required:
                        if (!has)
                            return false;
                        break;
                    case TraitContainer.TraitFilterType.Excluded:
                        if (has)
                            return false;
                        break;
                }
            }

            // handle trait pool (optional)
            if (pawnFilter.RequiredTraitsInPool > 0 &&
                pawnFilter.RequiredTraitsInPool <= pawnFilter.Traits.Count())
            {
                int pawnHasTraitCounter = 0;
                var traitPool = pawnFilter.Traits.Where(t => t.traitFilter == TraitContainer.TraitFilterType.Optional);
                foreach (var traitContainer in traitPool)
                {
                    if (HasTrait(pawn, traitContainer.trait))
                    {
                        pawnHasTraitCounter++;
                        if (pawnFilter.RequiredTraitsInPool == pawnHasTraitCounter)
                            break;
                    }
                }
                if (pawnHasTraitCounter < pawnFilter.RequiredTraitsInPool)
                    return false;
            }

            return true;
        }

        private static bool IsGeneAffectedHealth(Hediff hediff)
        {
            if (!ModsConfig.BiotechActive)
                return false;

            if (hediff is Hediff_ChemicalDependency chemicalDependency && chemicalDependency.LinkedGene != null)
                return true;

            return false;
        }

        public static bool CheckHealthIsSatisfied(Pawn pawn)
        {
            var hediffs = pawn.health.hediffSet.hediffs;
            int count = hediffs.Count;

            switch (pawnFilter.FilterHealthCondition)
            {
                case PawnFilter.HealthOptions.AllowAll:
                    break;
                case PawnFilter.HealthOptions.OnlyStartCondition:
                    for (int i = 0; i < count; i++)
                    {
                        var h = hediffs[i];
                        var def = h.def;
                        if (def != cryptosleepSicknessDef && def != malnutritionDef
                            && !(h is Hediff_Pregnant)
                            && !IsGeneAffectedHealth(h))
                            return false;
                    }
                    break;
                case PawnFilter.HealthOptions.NoPain:
                    for (int i = 0; i < count; i++)
                    {
                        var h = hediffs[i];
                        if (h.PainOffset > 0f && !IsGeneAffectedHealth(h))
                            return false;
                    }
                    break;
                case PawnFilter.HealthOptions.NoAddiction:
                    for (int i = 0; i < count; i++)
                    {
                        var h = hediffs[i];
                        if (h is Hediff_Addiction && !IsGeneAffectedHealth(h))
                            return false;
                    }
                    break;
                case PawnFilter.HealthOptions.AllowNone:
                    if (ModsConfig.BiotechActive)
                    {
                        for (int i = 0; i < count; i++)
                        {
                            if (!IsGeneAffectedHealth(hediffs[i]))
                                return false;
                        }
                    }
                    else
                    {
                        if (count > 0)
                            return false;
                    }
                    break;
            }
            return true;
        }

        public static bool CheckWorkIsSatisfied(Pawn pawn)
        {
            // handle work options
            var disabled = pawn.story.DisabledWorkTagsBackstoryAndTraits;
            switch (pawnFilter.FilterIncapable)
            {
                case PawnFilter.IncapableOptions.AllowAll:
                    break;
                case PawnFilter.IncapableOptions.NoDumbLabor:
                    if ((disabled & WorkTags.ManualDumb) == WorkTags.ManualDumb)
                        return false;
                    break;
                case PawnFilter.IncapableOptions.AllowNone:
                    if (disabled != WorkTags.None)
                        return false;
                    break;
                case PawnFilter.IncapableOptions.ForcedViolence:
                    if ((disabled & WorkTags.Violent) == WorkTags.Violent)
                        return false;
                    break;
            }
            return true;
        }

        public static bool HasTrait(Pawn pawn, Trait trait)
        {
            return pawn.story.traits.allTraits.Find((Trait t) => {
                if (t == null && trait == null)
                {
                    return true;
                }
                else if (trait == null || t == null)
                {
                    return false;
                }
                else if (trait.Label.Equals(t.Label))
                {
                    return true;
                }
                else
                {
                    return false;
                }
            }) != null;
        }

        public static void SetGenderFilter(Gender gender)
        {
            pawnFilter.Gender = gender;
        }

        private static float _cacheTraitCommonalityMale;
        private static float _cacheTraitCommonalityFemale;

        private static float GetTotalTraitCommonality(Gender gender)
        {
            if (gender == Gender.Male && _cacheTraitCommonalityMale > 0)
                return _cacheTraitCommonalityMale;
            if (gender == Gender.Female && _cacheTraitCommonalityFemale > 0)
                return _cacheTraitCommonalityFemale;

            float total = 0;
            foreach (var trait in DefDatabase<TraitDef>.AllDefsListForReading)
            {
                total += trait.GetGenderSpecificCommonality(gender);
            }

            if (gender == Gender.Male)
                _cacheTraitCommonalityMale = total;
            if (gender == Gender.Female)
                _cacheTraitCommonalityFemale = total;

            return total;
        }

        public static float GetTraitRollChance(TraitDef traitDef, Gender gender = Gender.Male)
        {
            float total = GetTotalTraitCommonality(gender);
            return traitDef.GetGenderSpecificCommonality(gender) * 100 / total;
        }

        public static string GetTraitRollChanceText(TraitDef traitDef)
        {
            string pecentMale = GetTraitRollChance(traitDef, Gender.Male).ToString("0.0");
            string pecentFemale = GetTraitRollChance(traitDef, Gender.Female).ToString("0.0");

            if (traitDef.GetGenderSpecificCommonality(Gender.Male) == traitDef.GetGenderSpecificCommonality(Gender.Female))
                return $"({pecentMale}%)";
            return $"(♂:{pecentMale}%,♀:{pecentFemale}%)";
        }

        // RimWorld 1.6: Enhanced pawn style generation with better error handling
        public static void GeneratePawnStyle(Pawn pawn)
        {
            if (pawn.RaceProps.Humanlike)
            {
                try
                {
                    pawn.story.hairDef = PawnStyleItemChooser.RandomHairFor(pawn);
                    if (pawn.style != null)
                    {
                        pawn.style.beardDef = pawn.gender == Gender.Male ? PawnStyleItemChooser.RandomBeardFor(pawn) : BeardDefOf.NoBeard;
                        if (ModsConfig.IdeologyActive)
                        {
                            pawn.style.FaceTattoo = PawnStyleItemChooser.RandomTattooFor(pawn, TattooType.Face);
                            pawn.style.BodyTattoo = PawnStyleItemChooser.RandomTattooFor(pawn, TattooType.Body);
                        }
                        else
                        {
                            pawn.style.SetupTattoos_NoIdeology();
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log.Warning($"RandomPlus: Failed to generate pawn style: {ex.Message}");
                }
            }
        }

        #region Ultra Fast Algorithm

        // === Right-click cancellation ===
        // The reroll loop blocks the main thread, so Unity's Input class never updates.
        // GetAsyncKeyState reads OS-level key state directly, so it works mid-loop.
        // Windows-only; on other platforms the P/Invoke fails and cancellation is disabled.
        [DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(int vKey);
        private const int VK_RBUTTON = 0x02;

        private static bool IsRightMouseDown()
        {
            try { return (GetAsyncKeyState(VK_RBUTTON) & 0x8000) != 0; }
            catch { return false; }
        }

        private static Action<Pawn, PawnGenerationRequest> CreateFastDelegate(MethodInfo method)
        {
            if (method == null) return null;
            try
            {
                return (Action<Pawn, PawnGenerationRequest>)
                    Delegate.CreateDelegate(typeof(Action<Pawn, PawnGenerationRequest>), method);
            }
            catch
            {
                var args = new object[2];
                return (pawn, req) => { args[0] = pawn; args[1] = req; method.Invoke(null, args); };
            }
        }

        private static Action<Pawn, XenotypeDef, PawnGenerationRequest> CreateFastGeneDelegate(MethodInfo method)
        {
            if (method == null) return null;
            try
            {
                return (Action<Pawn, XenotypeDef, PawnGenerationRequest>)
                    Delegate.CreateDelegate(typeof(Action<Pawn, XenotypeDef, PawnGenerationRequest>), method);
            }
            catch
            {
                var args = new object[3];
                return (pawn, xeno, req) => { args[0] = pawn; args[1] = xeno; args[2] = req; method.Invoke(null, args); };
            }
        }

        private static void RerollUltraFast(Pawn pawn)
        {
            int pawnIdx = StartingPawnUtility.PawnIndex(pawn);
            PawnGenerationRequest request = StartingPawnUtility.GetGenerationRequest(pawnIdx);
            request.ValidateAndFix();

            Faction faction2 = request.Faction ??
                (!Find.FactionManager.TryGetRandomNonColonyHumanlikeFaction(out var faction1, false, true)
                    ? Faction.OfAncients : faction1);

            XenotypeDef xenotype = ModsConfig.BiotechActive
                ? PawnGenerator.GetXenotypeForGeneratedPawn(request) : null;

            // === Cache scalar filter params as locals (avoid property chains in hot loop) ===
            int rerollLimit = pawnFilter.RerollLimit;
            int ageMin = pawnFilter.ageRange.min;
            int ageMax = pawnFilter.ageRange.max;
            Gender requiredGender = pawnFilter.Gender;
            var incapableOption = pawnFilter.FilterIncapable;

            bool hasAgeFilter = ageMin != PawnFilter.MinAgeDefault || ageMax != PawnFilter.MaxAgeDefault;
            bool hasGenderFilter = requiredGender != Gender.None;
            bool hasWorkFilter = incapableOption != PawnFilter.IncapableOptions.AllowAll;

            // === Pre-cache trait filters as TraitDef+Degree arrays ===
            // Avoids: iterator allocation from yield-return property, HasTrait closure allocation,
            //         string Label comparison (replaced with reference equality + int comparison)
            bool hasTraitFilter = !Page_RandomEditor.MOD_WELL_MET && pawnFilter.Traits.Any();
            TraitDef[] reqTraitDefs = null;
            int[] reqTraitDegrees = null;
            TraitDef[] exclTraitDefs = null;
            int[] exclTraitDegrees = null;
            TraitDef[] poolTraitDefs = null;
            int[] poolTraitDegrees = null;
            int poolRequired = 0;

            if (hasTraitFilter)
            {
                var reqDefList = new List<TraitDef>();
                var reqDegList = new List<int>();
                var exclDefList = new List<TraitDef>();
                var exclDegList = new List<int>();
                var poolDefList = new List<TraitDef>();
                var poolDegList = new List<int>();

                foreach (var tc in pawnFilter.Traits)
                {
                    switch (tc.traitFilter)
                    {
                        case TraitContainer.TraitFilterType.Required:
                            reqDefList.Add(tc.trait.def);
                            reqDegList.Add(tc.trait.Degree);
                            break;
                        case TraitContainer.TraitFilterType.Excluded:
                            exclDefList.Add(tc.trait.def);
                            exclDegList.Add(tc.trait.Degree);
                            break;
                        case TraitContainer.TraitFilterType.Optional:
                            poolDefList.Add(tc.trait.def);
                            poolDegList.Add(tc.trait.Degree);
                            break;
                    }
                }

                reqTraitDefs = reqDefList.ToArray();
                reqTraitDegrees = reqDegList.ToArray();
                exclTraitDefs = exclDefList.ToArray();
                exclTraitDegrees = exclDegList.ToArray();
                poolTraitDefs = poolDefList.ToArray();
                poolTraitDegrees = poolDegList.ToArray();
                poolRequired = pawnFilter.RequiredTraitsInPool;
            }

            // === Pre-cache skill filters with pre-computed list indices ===
            // SkillDef order in Pawn_SkillTracker.skills is stable (from DefDatabase<SkillDef>.AllDefs),
            // so indices computed here are valid for any new Pawn_SkillTracker created in the loop.
            int passionRangeMin = pawnFilter.passionRange.min;
            int passionRangeMax = pawnFilter.passionRange.max;
            int skillRangeMin = pawnFilter.skillRange.min;
            int skillRangeMax = pawnFilter.skillRange.max;
            bool countHighestAttack = pawnFilter.countOnlyHighestAttack;
            bool countPassion = pawnFilter.countOnlyPassion;
            bool hasPassionRange = passionRangeMin > PawnFilter.PassionMinDefault ||
                                   passionRangeMax < PawnFilter.PassionMaxDefault;
            bool hasSkillRange = skillRangeMin != PawnFilter.SkillMinDefault ||
                                 skillRangeMax != PawnFilter.SkillMaxDefault;

            var currentSkillList = pawn.skills.skills;
            int sfCount = 0;
            int[] sfIndices = null;
            Passion[] sfPassions = null;
            int[] sfMinValues = null;

            {
                var idxList = new List<int>();
                var passList = new List<Passion>();
                var valList = new List<int>();

                foreach (var sf in pawnFilter.Skills)
                {
                    if (sf.Passion != Passion.None || sf.MinValue > 0)
                    {
                        for (int i = 0; i < currentSkillList.Count; i++)
                        {
                            if (currentSkillList[i].def == sf.SkillDef)
                            {
                                idxList.Add(i);
                                passList.Add(sf.Passion);
                                valList.Add(sf.MinValue);
                                break;
                            }
                        }
                    }
                }

                if (idxList.Count > 0)
                {
                    sfIndices = idxList.ToArray();
                    sfPassions = passList.ToArray();
                    sfMinValues = valList.ToArray();
                    sfCount = sfIndices.Length;
                }
            }

            bool hasSkillFilter = sfCount > 0 || hasPassionRange || hasSkillRange;

            // === Pre-cache backstory pools (bypass GiveAppropriateBioAndNameTo per-iteration filtering) ===
            // GiveAppropriateBioAndNameTo iterates ALL ~300+ BackstoryDefs twice per iteration
            // (childhood + adulthood) to filter by slot, faction category, etc. Pre-caching moves
            // this to a one-time cost and also pre-filters backstories that disallow required traits.
            BackstoryDef[] childPool = null;
            BackstoryDef[] adultPool = null;
            bool useDirectBackstory = false;

            try
            {
                var childList = new List<BackstoryDef>();
                var adultList = new List<BackstoryDef>();

                // Build faction category set for matching
                var factionCategories = new HashSet<string>();
                if (faction2.def.backstoryFilters != null)
                {
                    foreach (var filter in faction2.def.backstoryFilters)
                    {
                        if (filter.categories != null)
                            foreach (var cat in filter.categories)
                                factionCategories.Add(cat);
                    }
                }

                foreach (var bs in DefDatabase<BackstoryDef>.AllDefsListForReading)
                {
                    if (!bs.shuffleable) continue;

                    // Category match: backstory must share at least one category with faction
                    if (factionCategories.Count > 0 && bs.spawnCategories != null)
                    {
                        bool match = false;
                        foreach (var cat in bs.spawnCategories)
                        {
                            if (factionCategories.Contains(cat)) { match = true; break; }
                        }
                        if (!match) continue;
                    }

                    // Pre-filter: exclude backstories that disallow any required trait
                    if (hasTraitFilter && reqTraitDefs != null && reqTraitDefs.Length > 0
                        && bs.disallowedTraits != null && bs.disallowedTraits.Count > 0)
                    {
                        bool blocks = false;
                        foreach (var dt in bs.disallowedTraits)
                        {
                            for (int r = 0; r < reqTraitDefs.Length; r++)
                            {
                                if (dt.def == reqTraitDefs[r]) { blocks = true; break; }
                            }
                            if (blocks) break;
                        }
                        if (blocks) continue;
                    }

                    if (bs.slot == BackstorySlot.Childhood)
                        childList.Add(bs);
                    else if (bs.slot == BackstorySlot.Adulthood)
                        adultList.Add(bs);
                }

                if (childList.Count > 0 && adultList.Count > 0)
                {
                    childPool = childList.ToArray();
                    adultPool = adultList.ToArray();
                    useDirectBackstory = true;
                }
            }
            catch { }

            // === Pre-detect scenario-forced traits ===
            // If no scenario part can add traits post-generation, we can skip the full
            // CheckPawnIsSatisfied re-check after Notify_PawnGenerated (saves a full
            // skill+trait+health+work+age re-iteration per iteration that reaches the tail).
            bool scenarioForcesTraits = false;
            try
            {
                var scenario = Find.Scenario;
                if (scenario != null)
                {
                    foreach (var part in scenario.AllParts)
                    {
                        if (part is ScenPart_ForcedTrait)
                        {
                            scenarioForcesTraits = true;
                            break;
                        }
                    }
                }
            }
            catch { scenarioForcesTraits = true; } // fail safe: keep re-check

            // === Main reroll loop ===
            // Activate name-skip + solid-bio-skip Harmony patches for the hot loop (fallback path)
            Patch_SkipNameGeneration.active = true;
            bool winnerFound = false;
            bool cancelledByUser = false;
            try
            {
                while (randomRerollCounter < rerollLimit)
                {
                    try
                    {
                        randomRerollCounter++;

                        // Right-click cancel poll (every 1024 iterations, ~negligible overhead)
                        if ((randomRerollCounter & 0x3FF) == 0 && IsRightMouseDown())
                        {
                            cancelledByUser = true;
                            break;
                        }

                        // --- Lazy age generation ---
                        // Check existing age first (free). Regenerate only when out of range.
                        // Once a valid age lands, it persists across iterations (nothing downstream
                        // modifies ageTracker), so age gen runs effectively ~once per reroll session.
                        if (hasAgeFilter)
                        {
                            int age = pawn.ageTracker.AgeBiologicalYears;
                            if (age < ageMin || (ageMax != PawnFilter.MaxAgeDefault && age > ageMax))
                            {
                                pawn.ageTracker = new Pawn_AgeTracker(pawn);
                                if (generateAge != null)
                                    generateAge(pawn, request);
                                else
                                {
                                    pawn.ageTracker.AgeBiologicalTicks = (long)(Rand.Range(16, 65) * 3600000L);
                                    pawn.ageTracker.AgeChronologicalTicks = pawn.ageTracker.AgeBiologicalTicks;
                                }
                                age = pawn.ageTracker.AgeBiologicalYears;
                                if (age < ageMin || (ageMax != PawnFilter.MaxAgeDefault && age > ageMax))
                                    continue;
                            }
                        }

                        // --- Generate bio + traits ---
                        // Reuse existing TraitSet by clearing its list (avoids per-iter allocation)
                        if (pawn.story.traits == null)
                            pawn.story.traits = new TraitSet(pawn);
                        else
                            pawn.story.traits.allTraits.Clear();
                        if (useDirectBackstory)
                        {
                            // Direct backstory from pre-filtered pools (~1μs vs ~200-300μs)
                            // Pools already exclude backstories that disallow required traits
                            pawn.story.Childhood = childPool[Rand.Range(0, childPool.Length)];
                            pawn.story.Adulthood = adultPool[Rand.Range(0, adultPool.Length)];
                        }
                        else
                        {
                            // Fallback: full backstory generation (name/bio skip patches active)
                            PawnBioAndNameGenerator.GiveAppropriateBioAndNameTo(pawn, faction2.def, request, xenotype);

                            // Backstory disallowed-trait pre-check
                            if (hasTraitFilter && reqTraitDefs.Length > 0)
                            {
                                bool backstoryBlocks = false;
                                try
                                {
                                    var childhood = pawn.story.Childhood;
                                    var adulthood = pawn.story.Adulthood;
                                    if (childhood?.disallowedTraits != null)
                                    {
                                        foreach (var dt in childhood.disallowedTraits)
                                        {
                                            for (int r = 0; r < reqTraitDefs.Length; r++)
                                            {
                                                if (dt.def == reqTraitDefs[r]) { backstoryBlocks = true; break; }
                                            }
                                            if (backstoryBlocks) break;
                                        }
                                    }
                                    if (!backstoryBlocks && adulthood?.disallowedTraits != null)
                                    {
                                        foreach (var dt in adulthood.disallowedTraits)
                                        {
                                            for (int r = 0; r < reqTraitDefs.Length; r++)
                                            {
                                                if (dt.def == reqTraitDefs[r]) { backstoryBlocks = true; break; }
                                            }
                                            if (backstoryBlocks) break;
                                        }
                                    }
                                }
                                catch { }
                                if (backstoryBlocks) continue;
                            }
                        }

                        if (generateTraits != null) generateTraits(pawn, request);

                        // Inline gender check
                        if (hasGenderFilter && pawn.gender != Gender.None && requiredGender != pawn.gender)
                            continue;

                        // --- INLINE TRAIT CHECK (TraitDef+Degree: reference eq + int, no string comparison) ---
                        if (hasTraitFilter)
                        {
                            var allTraits = pawn.story.traits.allTraits;
                            bool traitFail = false;

                            // Required traits
                            for (int r = 0; r < reqTraitDefs.Length; r++)
                            {
                                bool found = false;
                                for (int t = 0; t < allTraits.Count; t++)
                                {
                                    if (allTraits[t] != null &&
                                        allTraits[t].def == reqTraitDefs[r] &&
                                        allTraits[t].Degree == reqTraitDegrees[r])
                                    {
                                        found = true;
                                        break;
                                    }
                                }
                                if (!found) { traitFail = true; break; }
                            }

                            // Excluded traits
                            if (!traitFail)
                            {
                                for (int e = 0; e < exclTraitDefs.Length; e++)
                                {
                                    for (int t = 0; t < allTraits.Count; t++)
                                    {
                                        if (allTraits[t] != null &&
                                            allTraits[t].def == exclTraitDefs[e] &&
                                            allTraits[t].Degree == exclTraitDegrees[e])
                                        {
                                            traitFail = true;
                                            break;
                                        }
                                    }
                                    if (traitFail) break;
                                }
                            }

                            // Optional trait pool
                            if (!traitFail && poolRequired > 0 &&
                                poolRequired <= poolTraitDefs.Length + reqTraitDefs.Length + exclTraitDefs.Length)
                            {
                                int poolMatches = 0;
                                for (int p = 0; p < poolTraitDefs.Length; p++)
                                {
                                    for (int t = 0; t < allTraits.Count; t++)
                                    {
                                        if (allTraits[t] != null &&
                                            allTraits[t].def == poolTraitDefs[p] &&
                                            allTraits[t].Degree == poolTraitDegrees[p])
                                        {
                                            poolMatches++;
                                            break;
                                        }
                                    }
                                    if (poolMatches >= poolRequired) break;
                                }
                                if (poolMatches < poolRequired) traitFail = true;
                            }

                            if (traitFail) continue;
                        }

                        // Inline work check (no method call)
                        if (hasWorkFilter)
                        {
                            var disabledTags = pawn.story.DisabledWorkTagsBackstoryAndTraits;
                            if (incapableOption == PawnFilter.IncapableOptions.NoDumbLabor &&
                                (disabledTags & WorkTags.ManualDumb) == WorkTags.ManualDumb)
                                continue;
                            if (incapableOption == PawnFilter.IncapableOptions.AllowNone &&
                                disabledTags != WorkTags.None)
                                continue;
                            if (incapableOption == PawnFilter.IncapableOptions.ForcedViolence &&
                                (disabledTags & WorkTags.Violent) == WorkTags.Violent)
                                continue;
                        }

                        // --- Generate skills (ONLY after traits+work pass) ---
                        pawn.skills = new Pawn_SkillTracker(pawn);
                        if (generateSkills != null) generateSkills(pawn, request);

                        // --- INLINE SKILL CHECK (direct index access, no LINQ/FirstOrDefault) ---
                        if (hasSkillFilter)
                        {
                            var skillList = pawn.skills.skills;
                            bool skillFail = false;

                            // Per-skill filters (O(1) index access per filter)
                            for (int f = 0; f < sfCount; f++)
                            {
                                var record = skillList[sfIndices[f]];
                                if (record.passion < sfPassions[f] || record.Level < sfMinValues[f])
                                {
                                    skillFail = true;
                                    break;
                                }
                            }

                            // Total passion range
                            if (!skillFail && hasPassionRange)
                            {
                                int totalPassions = 0;
                                for (int i = 0; i < skillList.Count; i++)
                                {
                                    if (skillList[i].passion > 0) totalPassions++;
                                }
                                if (totalPassions < passionRangeMin || totalPassions > passionRangeMax)
                                    skillFail = true;
                            }

                            // Total skill range
                            if (!skillFail && hasSkillRange)
                            {
                                int skillTotal = 0;
                                for (int i = 0; i < skillList.Count; i++)
                                {
                                    var skill = skillList[i];
                                    if (countHighestAttack && i == 0)
                                    {
                                        var meleeSkill = skillList[1];
                                        skillTotal += meleeSkill.Level > skill.Level ? meleeSkill.Level : skill.Level;
                                        i = 1;
                                        continue;
                                    }
                                    if (countPassion)
                                    {
                                        if (skill.passion > 0)
                                            skillTotal += skill.Level;
                                    }
                                    else
                                    {
                                        skillTotal += skill.Level;
                                    }
                                }
                                if (skillTotal < skillRangeMin || skillTotal > skillRangeMax)
                                    skillFail = true;
                            }

                            if (skillFail) continue;
                        }

                        // --- Health generation + finalization ---
                        bool healthGenSuccess = false;
                        for (int h = 0; h < 100 && !healthGenSuccess; h++)
                        {
                            pawn.health.Reset();
                            try
                            {
                                Find.Scenario.Notify_NewPawnGenerating(pawn, request.Context);
                                if (generateHealth != null) generateHealth(pawn, request);
                                if (!(pawn.Dead || pawn.Destroyed || pawn.Downed))
                                    healthGenSuccess = true;
                            }
                            catch { continue; }
                        }

                        if (!CheckHealthIsSatisfied(pawn))
                            continue;

                        // Notify scenario — only re-check when scenario parts can add forced traits,
                        // which is the only thing that could invalidate our inline checks above.
                        Find.Scenario.Notify_PawnGenerated(pawn, request.Context, true);
                        if (scenarioForcesTraits && !CheckPawnIsSatisfied(pawn))
                            continue;

                        // --- Winner: finalize ---
                        pawn.workSettings?.EnableAndInitialize();

                        if (ModsConfig.BiotechActive)
                        {
                            pawn.genes = new Pawn_GeneTracker(pawn);
                            if (generateGenes != null) generateGenes(pawn, xenotype, request);
                        }

                        if (generateBodyType != null) generateBodyType(pawn, request);
                        GeneratePawnStyle(pawn);

                        // Generate real name for winner (name-skip was active during hot loop)
                        Patch_SkipNameGeneration.active = false;
                        pawn.Name = PawnBioAndNameGenerator.GeneratePawnName(pawn);
                        winnerFound = true;
                        return;
                    }
                    catch (Exception ex)
                    {
                        Log.Warning($"RandomPlus: Ultra Fast error (attempt {randomRerollCounter}): {ex.Message}");
                        try
                        {
                            Find.WorldPawns.RemoveAndDiscardPawnViaGC(pawn);
                            SpouseRelationUtility.Notify_PawnRegenerated(pawn);
                            pawn = StartingPawnUtility.RandomizeInPlace(pawn);
                        }
                        catch (Exception ex2)
                        {
                            Log.Error($"RandomPlus: Critical error in Ultra Fast cleanup: {ex2.Message}");
                            break;
                        }
                    }
                }
            }
            finally
            {
                Patch_SkipNameGeneration.active = false;

                // Defensive finalization for cancel / limit-reached exits.
                // Pawn may have dummy name (from name-skip patch) and lack body/genes/style
                // since those only run on the winner path. Bring it to a valid display state.
                if (!winnerFound)
                {
                    try
                    {
                        if (pawn.Name is NameTriple nt && nt.First == "X" && nt.Last == "X")
                            pawn.Name = PawnBioAndNameGenerator.GeneratePawnName(pawn);
                        pawn.workSettings?.EnableAndInitialize();
                        if (ModsConfig.BiotechActive && pawn.genes == null)
                        {
                            pawn.genes = new Pawn_GeneTracker(pawn);
                            if (generateGenes != null) generateGenes(pawn, xenotype, request);
                        }
                        if (generateBodyType != null) generateBodyType(pawn, request);
                        GeneratePawnStyle(pawn);

                        if (cancelledByUser)
                        {
                            RerollCancelledByUser = true;
                            Log.Message("RandomPlus: Reroll cancelled by user (right-click).");
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Warning($"RandomPlus: Defensive finalization failed: {ex.Message}");
                    }
                }
            }
        }

        #endregion

    }
}
