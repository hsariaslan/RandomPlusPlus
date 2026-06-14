using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace RandomPlus
{
    public class PanelTraits : PanelBase
    {
        private struct TraitEntry
        {
            public int Index;
            public TraitContainer Container;
        }

        protected ScrollViewVertical whitelistScrollView = new ScrollViewVertical();
        protected ScrollViewVertical blacklistScrollView = new ScrollViewVertical();
        protected List<Field> whitelistFields = new List<Field>();
        protected List<Field> blacklistFields = new List<Field>();
        protected List<Trait> traitsToRemove = new List<Trait>();
        protected HashSet<TraitDef> disallowedTraitDefs = new HashSet<TraitDef>();
        protected HashSet<string> disallowedTraitLabels = new HashSet<string>();

        protected Vector2 SizeField;
        protected Vector2 SizeTrait;
        protected Vector2 SizeFieldPadding = new Vector2(5, 6);
        protected Vector2 SizeTraitMargin = new Vector2(4, -6);

        protected Rect whitelistHeaderRect;
        protected Rect blacklistHeaderRect;
        protected Rect whitelistScrollFrame;
        protected Rect blacklistScrollFrame;
        protected Rect whitelistScrollViewRect;
        protected Rect blacklistScrollViewRect;

        protected Rect traitPoolLabelRect;
        protected Rect traitPoolButtonRect;

        protected Rect disabledLabelRect;

        private static readonly HashSet<string> SexualityTraitDefNames = new HashSet<string>
        {
            "Asexual",
            "Bisexual",
            "Gay"
        };

        public PanelTraits()
        {
            Resize(new Rect(340, 40, 660, 254));
        }

        public override string PanelHeader
        {
            get
            {
                return "RandomPlus.PanelTraits.Header".Translate();
            }
        }

        public override void Draw()
        {
            UpdateTraitWarning();
            base.Draw();
        }

        protected override void DrawPanelHeader()
        {
            if (PanelHeader == null)
                return;

            var fontValue = Text.Font;
            var anchorValue = Text.Anchor;
            var colorValue = GUI.color;
            Text.Font = GameFont.Medium;
            Text.Anchor = TextAnchor.UpperLeft;

            Rect labelRect = new Rect(15 + PanelRect.xMin, 5 + PanelRect.yMin, PanelRect.width - 30, 40);
            Widgets.Label(labelRect, PanelHeader);

            if (!string.IsNullOrEmpty(Warning))
            {
                float titleWidth = Text.CalcSize(PanelHeader).x;
                Rect alertRect = new Rect(labelRect.x + titleWidth + 8, 9 + PanelRect.yMin, 20, 20);
                GUI.DrawTexture(alertRect, Textures.TextureAlertSmall);
                TooltipHandler.TipRegion(alertRect, Warning);
            }

            Text.Font = fontValue;
            Text.Anchor = anchorValue;
            GUI.color = colorValue;
        }

        public override void Resize(Rect rect)
        {
            base.Resize(rect);

            float panelPadding = 10;
            float columnGap = 12;
            float columnHeaderHeight = 20;
            float footerHeight = 28;
            float fieldHeight = 28;
            float columnWidth = (PanelRect.width - panelPadding * 2 - columnGap) / 2;
            float scrollTop = BodyRect.y + columnHeaderHeight;
            float scrollHeight = BodyRect.height - columnHeaderHeight - footerHeight - panelPadding;

            SizeTrait = new Vector2(columnWidth - 20, fieldHeight + SizeFieldPadding.y * 2);
            SizeField = new Vector2(SizeTrait.x - SizeFieldPadding.x * 2, SizeTrait.y - SizeFieldPadding.y * 2);

            whitelistHeaderRect = new Rect(panelPadding, BodyRect.y, columnWidth, columnHeaderHeight);
            blacklistHeaderRect = new Rect(panelPadding + columnWidth + columnGap, BodyRect.y, columnWidth, columnHeaderHeight);

            whitelistScrollFrame = new Rect(panelPadding, scrollTop, columnWidth, scrollHeight);
            blacklistScrollFrame = new Rect(panelPadding + columnWidth + columnGap, scrollTop, columnWidth, scrollHeight);
            whitelistScrollViewRect = new Rect(0, 0, whitelistScrollFrame.width, whitelistScrollFrame.height);
            blacklistScrollViewRect = new Rect(0, 0, blacklistScrollFrame.width, blacklistScrollFrame.height);

            float footerTop = scrollTop + scrollHeight + 4;
            traitPoolLabelRect = new Rect(panelPadding, footerTop, 200, 24);
            traitPoolButtonRect = new Rect(panelPadding + 182, footerTop + 1, 104, 20);

            disabledLabelRect = new Rect(20, 30, 100, 20);
        }

        protected override void DrawPanelContent()
        {
            base.DrawPanelContent();

            if (Page_RandomEditor.MOD_WELL_MET)
            {
                Widgets.Label(disabledLabelRect, "RandomPlus.PanelTraits.Disabled".Translate());
                return;
            }

            var whitelistEntries = new List<TraitEntry>();
            var blacklistEntries = new List<TraitEntry>();
            int index = 0;
            foreach (TraitContainer traitContainer in RandomSettings.PawnFilter.Traits)
            {
                TraitEntry entry = new TraitEntry
                {
                    Index = index,
                    Container = traitContainer
                };

                if (traitContainer.traitFilter == TraitContainer.TraitFilterType.Excluded)
                    blacklistEntries.Add(entry);
                else
                    whitelistEntries.Add(entry);

                index++;
            }

            DrawTraitColumn(
                whitelistHeaderRect,
                whitelistScrollFrame,
                whitelistScrollViewRect,
                whitelistScrollView,
                whitelistFields,
                whitelistEntries,
                "RandomPlus.PanelTraits.WhitelistHeader",
                "RandomPlus.PanelTraits.NoWhitelist",
                TraitContainer.TraitFilterType.Required);

            DrawTraitColumn(
                blacklistHeaderRect,
                blacklistScrollFrame,
                blacklistScrollViewRect,
                blacklistScrollView,
                blacklistFields,
                blacklistEntries,
                "RandomPlus.PanelTraits.BlacklistHeader",
                "RandomPlus.PanelTraits.NoBlacklist",
                TraitContainer.TraitFilterType.Excluded);

            drawTraitPool();

            if (traitsToRemove.Count > 0)
            {
                foreach (var trait in traitsToRemove)
                {
                    RandomSettings.PawnFilter.TraitRemoved(trait);
                }
                traitsToRemove.Clear();
            }
        }

        private void DrawTraitColumn(
            Rect headerRect,
            Rect scrollFrame,
            Rect scrollViewRect,
            ScrollViewVertical scrollView,
            List<Field> fieldList,
            List<TraitEntry> entries,
            string headerKey,
            string emptyKey,
            TraitContainer.TraitFilterType addFilterType)
        {
            GUI.color = Style.ColorText;
            Widgets.Label(new Rect(headerRect.x, headerRect.y, headerRect.width - 24, headerRect.height),
                $"{headerKey.Translate()} ({entries.Count})");

            Rect clearRect = new Rect(headerRect.xMax - 38, headerRect.y + 1, 16, 16);
            DrawClearTraitButton(clearRect, addFilterType);

            Rect addRect = new Rect(headerRect.xMax - 18, headerRect.y + 1, 16, 16);
            DrawAddTraitButton(addRect, addFilterType);

            GUI.color = Color.white;
            GUI.BeginGroup(scrollFrame);
            float cursor = 0;
            try
            {
                if (!entries.Any())
                {
                    GUI.color = Style.ColorText;
                    Widgets.Label(new Rect(6, 0, scrollFrame.width - 12, 24), emptyKey.Translate());
                    GUI.color = Color.white;
                }

                scrollView.Begin(scrollViewRect);

                for (int i = 0; i < entries.Count; i++)
                {
                    if (i >= fieldList.Count)
                        fieldList.Add(new Field());

                    DrawTraitRow(entries[i], fieldList[i], cursor, scrollView, addFilterType);
                    cursor += SizeTrait.y + SizeTraitMargin.y;
                }

                if (entries.Count > 0)
                    cursor -= SizeTraitMargin.y;
            }
            finally
            {
                scrollView.End(cursor);
                GUI.color = Color.white;
                GUI.EndGroup();
            }
        }

        private void DrawTraitRow(
            TraitEntry entry,
            Field field,
            float cursor,
            ScrollViewVertical scrollView,
            TraitContainer.TraitFilterType columnFilterType)
        {
            TraitContainer traitContainer = entry.Container;
            bool blacklistColumn = columnFilterType == TraitContainer.TraitFilterType.Excluded;

            GUI.color = Style.ColorPanelBackgroundItem;
            Rect traitRect = new Rect(0, cursor, SizeTrait.x - (scrollView.ScrollbarsVisible ? 16 : 0), SizeTrait.y);
            GUI.DrawTexture(traitRect, BaseContent.WhiteTex);
            GUI.color = Color.white;

            Rect fieldRect = new Rect(SizeFieldPadding.x, cursor + SizeFieldPadding.y, SizeField.x, SizeField.y);
            if (scrollView.ScrollbarsVisible)
                fieldRect.width -= 16;

            field.Rect = fieldRect;
            Rect fieldClickRect = fieldRect;
            fieldClickRect.width -= 36;
            field.ClickRect = fieldClickRect;

            if (traitContainer != null && traitContainer.trait != null)
            {
                string filterLabel = blacklistColumn ? "" : LabelForTraitFilter(entry.Index);
                field.Label = $"{traitContainer.trait.LabelCap}{filterLabel} {RandomSettings.GetTraitRollChanceText(traitContainer.trait.def)}";
                field.Tip = traitContainer.trait.CurrentData.description;
            }
            else
            {
                field.Label = null;
                field.Tip = null;
            }

            Trait localTrait = traitContainer.trait;
            int localIndex = entry.Index;
            field.ClickAction = () =>
            {
                Trait selectedTrait = localTrait;
                ComputeDisallowedTraits(localTrait, !blacklistColumn);
                Dialog_Options<Trait> dialog = new Dialog_Options<Trait>(ProviderTraits.Traits)
                {
                    NameFunc = (Trait t) =>
                    {
                        return t.LabelCap;
                    },
                    DescriptionFunc = (Trait t) =>
                    {
                        return t.CurrentData.description;
                    },
                    SelectedFunc = (Trait t) =>
                    {
                        if (selectedTrait == null || t == null)
                            return selectedTrait == t;
                        return selectedTrait.def == t.def && selectedTrait.Label == t.Label;
                    },
                    SelectAction = (Trait t) =>
                    {
                        selectedTrait = t;
                    },
                    EnabledFunc = (Trait t) =>
                    {
                        return !(disallowedTraitDefs.Contains(t.def) || disallowedTraitLabels.Contains(t.Label));
                    },
                    CloseAction = () =>
                    {
                        RandomSettings.PawnFilter.TraitUpdated(localIndex, selectedTrait);
                    },
                    NoneSelectedFunc = () =>
                    {
                        return selectedTrait == null;
                    },
                    SelectNoneAction = () =>
                    {
                        selectedTrait = null;
                    }
                };
                Find.WindowStack.Add(dialog);
            };
            field.Draw();

            Rect deleteRect = new Rect(field.Rect.xMax - 32, field.Rect.y + field.Rect.HalfHeight() - 6, 12, 12);
            GUI.color = deleteRect.Contains(Event.current.mousePosition) ?
                Style.ColorButtonHighlight : Style.ColorButton;
            GUI.DrawTexture(deleteRect, Textures.TextureButtonDelete);
            if (Widgets.ButtonInvisible(deleteRect, false))
            {
                SoundDefOf.Tick_Tiny.PlayOneShotOnCamera();
                traitsToRemove.Add(traitContainer.trait);
            }

            if (!blacklistColumn)
            {
                Rect traitFilterTypeRect = new Rect(field.Rect.xMax + 12, field.Rect.y + field.Rect.HalfHeight() - 6, 12, 12);
                GUI.color = traitFilterTypeRect.Contains(Event.current.mousePosition) ?
                    Style.ColorButtonHighlight : Style.ColorButton;
                GUI.DrawTexture(traitFilterTypeRect, Textures.TextureButtonReset);
                if (Widgets.ButtonInvisible(traitFilterTypeRect, false))
                {
                    SoundDefOf.Tick_Tiny.PlayOneShotOnCamera();
                    CycleTraitFilter(entry.Index);
                }
            }
        }

        protected void DrawAddTraitButton(Rect addRect, TraitContainer.TraitFilterType filterType)
        {
            Style.SetGUIColorForButton(addRect);
            var selectedTraits = GetTraitsForColumn(filterType).ToList();
            int traitCount = RandomSettings.PawnFilter.Traits.Count();
            bool addButtonEnabled = selectedTraits.Any() || traitCount < ProviderTraits.Traits.Count();
            if (!addButtonEnabled)
                GUI.color = Style.ColorButtonDisabled;

            GUI.DrawTexture(addRect, Textures.TextureButtonAdd);
            if (addButtonEnabled && Widgets.ButtonInvisible(addRect, false))
            {
                SoundDefOf.Tick_Low.PlayOneShotOnCamera();
                bool blacklistColumn = filterType == TraitContainer.TraitFilterType.Excluded;
                var oppositeTraitKeys = new HashSet<string>(
                    GetTraitsForColumn(blacklistColumn
                        ? TraitContainer.TraitFilterType.Required
                        : TraitContainer.TraitFilterType.Excluded)
                    .Select(TraitKey));

                Dialog_TraitMultiSelect dialog = new Dialog_TraitMultiSelect(ProviderTraits.Traits, selectedTraits)
                {
                    HeaderLabel = (blacklistColumn
                        ? "RandomPlus.PanelTraits.MultiSelect.BlacklistTitle"
                        : "RandomPlus.PanelTraits.MultiSelect.WhitelistTitle").Translate(),
                    NameFunc = (Trait t) => $"{t.LabelCap} {RandomSettings.GetTraitRollChanceText(t.def)}",
                    DescriptionFunc = (Trait t) => t.CurrentData.description,
                    EnabledFunc = (Trait t) =>
                    {
                        return !oppositeTraitKeys.Contains(TraitKey(t));
                    },
                    ConfirmAction = (List<Trait> traits) =>
                    {
                        RandomSettings.PawnFilter.SetTraitsForFilterType(traits, filterType);
                    }
                };
                Find.WindowStack.Add(dialog);
            }
        }

        protected void DrawClearTraitButton(Rect clearRect, TraitContainer.TraitFilterType filterType)
        {
            var selectedTraits = GetTraitsForColumn(filterType).ToList();
            bool clearButtonEnabled = selectedTraits.Any();

            Style.SetGUIColorForButton(clearRect);
            if (!clearButtonEnabled)
                GUI.color = Style.ColorButtonDisabled;

            GUI.DrawTexture(clearRect, Textures.TextureButtonClearSkills);
            if (clearButtonEnabled && Widgets.ButtonInvisible(clearRect, false))
            {
                SoundDefOf.Tick_Tiny.PlayOneShotOnCamera();
                string confirmationKey = filterType == TraitContainer.TraitFilterType.Excluded
                    ? "RandomPlus.PanelTraits.ClearBlacklistConfirmation"
                    : "RandomPlus.PanelTraits.ClearWhitelistConfirmation";
                Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
                    confirmationKey.Translate(),
                    () => RandomSettings.PawnFilter.SetTraitsForFilterType(new List<Trait>(), filterType),
                    true));
            }
        }

        private IEnumerable<Trait> GetTraitsForColumn(TraitContainer.TraitFilterType filterType)
        {
            bool excludedColumn = filterType == TraitContainer.TraitFilterType.Excluded;
            foreach (TraitContainer tc in RandomSettings.PawnFilter.Traits)
            {
                if (tc.trait == null)
                    continue;

                if (excludedColumn && tc.traitFilter == TraitContainer.TraitFilterType.Excluded)
                    yield return tc.trait;
                else if (!excludedColumn && tc.traitFilter != TraitContainer.TraitFilterType.Excluded)
                    yield return tc.trait;
            }
        }

        private static string TraitKey(Trait trait)
        {
            if (trait == null || trait.def == null)
                return "";
            return trait.def.defName + ":" + trait.Degree;
        }

        protected void ComputeDisallowedTraits(Trait traitToReplace, bool enforceRequiredConflicts)
        {
            disallowedTraitDefs.Clear();
            disallowedTraitLabels.Clear();
            List<string> exTags = new List<string>();

            foreach (TraitContainer tc in RandomSettings.PawnFilter.Traits)
            {
                if (tc.trait == null || tc.trait == traitToReplace)
                    continue;

                disallowedTraitLabels.Add(tc.trait.Label);

                if (enforceRequiredConflicts &&
                    tc.traitFilter == TraitContainer.TraitFilterType.Required)
                {
                    tc.trait.def.exclusionTags.ForEach((t) => exTags.AddDistinct(t));

                    if (tc.trait.def.conflictingTraits != null)
                    {
                        foreach (var c in tc.trait.def.conflictingTraits)
                        {
                            disallowedTraitDefs.Add(c);
                        }
                    }
                    disallowedTraitDefs.Add(tc.trait.def);
                }
            }

            if (enforceRequiredConflicts)
            {
                foreach (TraitDef def in DefDatabase<TraitDef>.AllDefs)
                {
                    if (def.exclusionTags.Intersect(exTags).Any())
                        disallowedTraitDefs.Add(def);
                }
            }
        }

        protected void ClearTrait(int traitIndex)
        {
            RandomSettings.PawnFilter.TraitUpdated(traitIndex, null);
        }

        public void ScrollToTop()
        {
            whitelistScrollView.ScrollToTop();
            blacklistScrollView.ScrollToTop();
        }

        public void ScrollToBottom()
        {
            whitelistScrollView.ScrollToBottom();
            blacklistScrollView.ScrollToBottom();
        }

        protected void CycleTraitFilter(int index)
        {
            if (index < 0 || index >= RandomSettings.PawnFilter.Traits.Count())
                return;

            TraitContainer.TraitFilterType traitFilter = RandomSettings.PawnFilter.Traits.ElementAt(index).traitFilter;

            switch (traitFilter)
            {
                case TraitContainer.TraitFilterType.Required:
                    traitFilter = TraitContainer.TraitFilterType.Optional;
                    break;
                case TraitContainer.TraitFilterType.Optional:
                    traitFilter = TraitContainer.TraitFilterType.Required;
                    break;
                case TraitContainer.TraitFilterType.Excluded:
                    traitFilter = TraitContainer.TraitFilterType.Required;
                    break;
            }

            RandomSettings.PawnFilter.Traits.ElementAt(index).traitFilter = traitFilter;
        }

        protected string LabelForTraitFilter(int index)
        {
            if (index < 0 || index >= RandomSettings.PawnFilter.Traits.Count())
                return "";

            switch (RandomSettings.PawnFilter.Traits.ElementAt(index).traitFilter)
            {
                case TraitContainer.TraitFilterType.Required: return "RandomPlus.PanelTraits.FilterType.Required".Translate();
                case TraitContainer.TraitFilterType.Optional: return "RandomPlus.PanelTraits.FilterType.Pool".Translate();
                case TraitContainer.TraitFilterType.Excluded: return "RandomPlus.PanelTraits.FilterType.Excluded".Translate();
            }

            return "";
        }

        public void drawTraitPool()
        {
            int maxPoolRequired = GetMaxRequiredTraitsInPool();
            bool invalidPoolValue = RandomSettings.PawnFilter.RequiredTraitsInPool > maxPoolRequired;

            Widgets.Label(traitPoolLabelRect, "RandomPlus.PanelTraits.PoolLabel".Translate());
            var savedColor = GUI.color;
            if (invalidPoolValue)
                GUI.color = Color.red;

            if (Widgets.ButtonText(traitPoolButtonRect, RandomSettings.PawnFilter.RequiredTraitsInPool.ToString(), true, true, true))
            {
                List<FloatMenuOption> options = new List<FloatMenuOption>();
                for (int rangeOption = 0; rangeOption <= maxPoolRequired; rangeOption++)
                {
                    int localRangeOption = rangeOption;
                    var menuOption = new FloatMenuOption(rangeOption.ToString(), () =>
                    {
                        RandomSettings.PawnFilter.RequiredTraitsInPool = localRangeOption;
                    });
                    options.Add(menuOption);
                }
                Find.WindowStack.Add(new FloatMenu(options));
            }
            GUI.color = savedColor;
        }

        public string CurrentWarning
        {
            get
            {
                return BuildTraitWarning();
            }
        }

        private void UpdateTraitWarning()
        {
            Warning = CurrentWarning;
        }

        private string BuildTraitWarning()
        {
            if (Page_RandomEditor.MOD_WELL_MET || RandomSettings.PawnFilter == null)
                return null;

            GetWhitelistTraitCounts(out int requiredNonSexual, out int requiredSexual, out int poolNonSexual, out int poolSexual);

            int poolCount = poolNonSexual + poolSexual;
            int maxPoolRequired = CalculateMaxRequiredTraitsInPool(requiredNonSexual, requiredSexual, poolNonSexual, poolSexual);
            List<string> warnings = new List<string>();
            int blacklistCount = GetTraitsForColumn(TraitContainer.TraitFilterType.Excluded)
                .Select(TraitKey)
                .Distinct()
                .Count();

            if (blacklistCount >= ProviderTraits.Traits.Count)
            {
                warnings.Add("RandomPlus.PanelTraits.Warning.AllTraitsBlacklisted".Translate());
            }

            if (requiredNonSexual > 3)
            {
                int traitsToMove = requiredNonSexual - 3;
                warnings.Add(string.Format(
                    "RandomPlus.PanelTraits.Warning.RequiredNonSexualLimit".Translate(),
                    traitsToMove));
            }

            if (requiredSexual > 1)
            {
                warnings.Add("RandomPlus.PanelTraits.Warning.RequiredSexualLimit".Translate());
            }

            if (poolCount > 0 && maxPoolRequired == 0)
            {
                warnings.Add("RandomPlus.PanelTraits.Warning.PoolHasNoAvailableSlots".Translate());
            }
            else if (poolCount > 0 && RandomSettings.PawnFilter.RequiredTraitsInPool == 0)
            {
                warnings.Add("RandomPlus.PanelTraits.Warning.PoolCountZero".Translate());
            }

            if (RandomSettings.PawnFilter.RequiredTraitsInPool > maxPoolRequired)
            {
                warnings.Add(string.Format(
                    "RandomPlus.PanelTraits.Warning.PoolCountTooHigh".Translate(),
                    RandomSettings.PawnFilter.RequiredTraitsInPool,
                    maxPoolRequired));
            }

            if (warnings.Count == 0)
                return null;

            return "RandomPlus.PanelTraits.Warning.TraitLimit".Translate() + "\n\n" + string.Join("\n", warnings);
        }

        private int GetMaxRequiredTraitsInPool()
        {
            GetWhitelistTraitCounts(out int requiredNonSexual, out int requiredSexual, out int poolNonSexual, out int poolSexual);
            return CalculateMaxRequiredTraitsInPool(requiredNonSexual, requiredSexual, poolNonSexual, poolSexual);
        }

        private int CalculateMaxRequiredTraitsInPool(int requiredNonSexual, int requiredSexual, int poolNonSexual, int poolSexual)
        {
            int nonSexualSlots = Math.Max(0, 3 - requiredNonSexual);
            int sexualSlots = requiredSexual > 0 ? 0 : 1;

            return Math.Min(poolNonSexual, nonSexualSlots) + Math.Min(poolSexual, sexualSlots);
        }

        private void GetWhitelistTraitCounts(out int requiredNonSexual, out int requiredSexual, out int poolNonSexual, out int poolSexual)
        {
            requiredNonSexual = 0;
            requiredSexual = 0;
            poolNonSexual = 0;
            poolSexual = 0;

            foreach (TraitContainer tc in RandomSettings.PawnFilter.Traits)
            {
                if (tc.trait == null || tc.traitFilter == TraitContainer.TraitFilterType.Excluded)
                    continue;

                bool sexualityTrait = IsSexualityTrait(tc.trait);
                if (tc.traitFilter == TraitContainer.TraitFilterType.Required)
                {
                    if (sexualityTrait)
                        requiredSexual++;
                    else
                        requiredNonSexual++;
                }
                else if (tc.traitFilter == TraitContainer.TraitFilterType.Optional)
                {
                    if (sexualityTrait)
                        poolSexual++;
                    else
                        poolNonSexual++;
                }
            }
        }

        private static bool IsSexualityTrait(Trait trait)
        {
            return trait != null &&
                trait.def != null &&
                SexualityTraitDefNames.Contains(trait.def.defName);
        }
    }
}
