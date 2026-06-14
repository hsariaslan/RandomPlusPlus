using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace RandomPlus
{
    public class Dialog_BackstoryMultiSelect : Window
    {
        private readonly BackstorySlot slot;
        private readonly List<BackstoryDef> options;
        private readonly List<string> categories;
        private readonly HashSet<string> relevantCategories;
        private readonly HashSet<string> selectedDefNames;
        private readonly Dictionary<SkillDef, int> advancedSkillMinimums = new Dictionary<SkillDef, int>();
        private string activeCategory;
        private string searchText = "";
        private Vector2 categoryScrollPosition = Vector2.zero;
        private Vector2 optionScrollPosition = Vector2.zero;

        public Action<List<string>> ConfirmAction = (defNames) => { };

        public override Vector2 InitialSize => new Vector2(760f, 620f);

        public Dialog_BackstoryMultiSelect(BackstorySlot slot, IEnumerable<string> selectedDefNames)
        {
            this.slot = slot;
            options = PawnFilter.GetFilterableBackstories(slot);
            categories = PawnFilter.GetBackstoryCategories(slot);
            relevantCategories = PawnFilter.GetRelevantBackstoryCategories(slot);
            activeCategory = categories.FirstOrDefault(IsCategoryEnabled)
                ?? categories.FirstOrDefault()
                ?? PawnFilter.BackstoryUncategorizedCategory;
            var validDefNames = new HashSet<string>(options.Select(backstory => backstory.defName));
            this.selectedDefNames = new HashSet<string>((selectedDefNames ?? Enumerable.Empty<string>())
                .Where(defName => validDefNames.Contains(defName)));
            closeOnCancel = true;
            closeOnClickedOutside = true;
            doCloseX = true;
            absorbInputAroundWindow = true;
        }

        public override void DoWindowContents(Rect inRect)
        {
            Text.Font = GameFont.Medium;
            string title = slot == BackstorySlot.Childhood
                ? "RandomPlus.PanelOthers.BackstoryFilter.ChildhoodTitle".Translate()
                : "RandomPlus.PanelOthers.BackstoryFilter.AdulthoodTitle".Translate();
            Widgets.Label(new Rect(0, 0, inRect.width, 32), title);

            Text.Font = GameFont.Small;
            string countLabel = string.Format(
                "RandomPlus.PanelOthers.BackstoryFilter.Count".Translate(),
                selectedDefNames.Count,
                options.Count);
            Text.Anchor = TextAnchor.UpperRight;
            Widgets.Label(new Rect(inRect.width - 180, 6, 180, 24), countLabel);
            Text.Anchor = TextAnchor.UpperLeft;

            Rect searchLabelRect = new Rect(0, 38, 66, 28);
            Rect searchRect = new Rect(searchLabelRect.xMax + 6, 38, inRect.width - 258, 28);
            Rect advancedRect = new Rect(inRect.width - 170, 38, 170, 28);
            Widgets.Label(searchLabelRect, "RandomPlus.PanelOthers.BackstoryFilter.SearchLabel".Translate());
            string searchControlName = $"RandomPlus.BackstorySearch.{slot}";
            GUI.SetNextControlName(searchControlName);
            string newSearchText = Widgets.TextField(searchRect, searchText ?? "");
            if (newSearchText != searchText)
            {
                searchText = newSearchText;
                optionScrollPosition = Vector2.zero;
            }
            if (string.IsNullOrEmpty(searchText) && GUI.GetNameOfFocusedControl() != searchControlName)
                DrawSearchPlaceholder(searchRect);

            if (Widgets.ButtonText(advancedRect, GetAdvancedSearchButtonLabel(), true, true, true))
                OpenAdvancedSearchDialog();

            EnsureActiveCategoryAvailable();

            List<BackstoryDef> selectAllOptions = GetSelectAllOptions();
            bool allSelected = selectAllOptions.Count > 0 && selectAllOptions.All(backstory => selectedDefNames.Contains(backstory.defName));
            bool selectAll = allSelected;
            Rect selectAllRect = new Rect(0, 74, inRect.width - 16, 28);
            Widgets.CheckboxLabeled(selectAllRect, "RandomPlus.PanelOthers.BackstoryFilter.SelectAll".Translate(), ref selectAll);
            if (selectAll != allSelected)
            {
                if (selectAll)
                {
                    foreach (BackstoryDef backstory in selectAllOptions)
                        selectedDefNames.Add(backstory.defName);
                }
                else
                {
                    foreach (BackstoryDef backstory in selectAllOptions)
                        selectedDefNames.Remove(backstory.defName);
                }
            }

            Rect bodyRect = new Rect(0, 110, inRect.width, inRect.height - 162);
            Rect categoryRect = new Rect(bodyRect.x, bodyRect.y, 190, bodyRect.height);
            Rect optionRect = new Rect(categoryRect.xMax + 14, bodyRect.y, bodyRect.width - categoryRect.width - 14, bodyRect.height);

            DrawCategories(categoryRect);
            DrawOptions(optionRect);

            Rect cancelRect = new Rect(0, inRect.height - 40, 140, 36);
            Rect confirmRect = new Rect(inRect.width - 140, inRect.height - 40, 140, 36);
            if (Widgets.ButtonText(cancelRect, "RandomPlus.Dialog.Cancel".Translate(), true, true, true))
                Close(true);
            if (Widgets.ButtonText(confirmRect, "RandomPlus.Dialog.Apply".Translate(), true, true, true))
            {
                ConfirmAction(options.Where(backstory => selectedDefNames.Contains(backstory.defName))
                    .Select(backstory => backstory.defName)
                    .ToList());
                Close(true);
            }
        }

        private void DrawCategories(Rect outRect)
        {
            float rowHeight = 30f;
            Rect viewRect = new Rect(0, 0, outRect.width - 16, categories.Count * rowHeight);
            Widgets.BeginScrollView(outRect, ref categoryScrollPosition, viewRect);
            try
            {
                float cursor = 0;
                foreach (string category in categories)
                {
                    Rect rowRect = new Rect(0, cursor, viewRect.width, rowHeight);
                    bool active = category == activeCategory;
                    bool enabled = IsCategoryEnabled(category);
                    if (active && enabled)
                        Widgets.DrawHighlightSelected(rowRect);
                    else if (enabled)
                        Widgets.DrawHighlightIfMouseover(rowRect);

                    string label = GetCategoryLabel(category);
                    Color previousColor = GUI.color;
                    if (!enabled)
                        GUI.color = new Color(0.55f, 0.55f, 0.55f);
                    if (Widgets.ButtonText(rowRect.ContractedBy(2), label, true, true, enabled))
                    {
                        activeCategory = category;
                        optionScrollPosition = Vector2.zero;
                    }
                    GUI.color = previousColor;

                    if (!enabled)
                        TooltipHandler.TipRegion(rowRect, "RandomPlus.PanelOthers.BackstoryFilter.CategoryDisabledTooltip".Translate());

                    cursor += rowHeight;
                }
            }
            finally
            {
                Widgets.EndScrollView();
            }
        }

        private void DrawOptions(Rect outRect)
        {
            List<BackstoryDef> visibleOptions = options
                .Where(backstory => PawnFilter.GetBackstoryCategories(backstory).Contains(activeCategory))
                .Where(IsVisibleByCurrentFilters)
                .ToList();

            float rowHeight = 30f;
            Rect viewRect = new Rect(0, 0, outRect.width - 16, visibleOptions.Count * rowHeight);
            Widgets.BeginScrollView(outRect, ref optionScrollPosition, viewRect);
            try
            {
                float cursor = 0;
                foreach (BackstoryDef backstory in visibleOptions)
                {
                    bool enabled = IsBackstoryEnabled(backstory);
                    bool selected = selectedDefNames.Contains(backstory.defName);
                    bool newSelected = selected;
                    Rect rowRect = new Rect(0, cursor, viewRect.width, rowHeight);
                    if (enabled)
                    {
                        Widgets.CheckboxLabeled(rowRect, PawnFilter.GetBackstoryOptionLabel(backstory), ref newSelected);
                        if (newSelected != selected)
                        {
                            if (newSelected)
                                selectedDefNames.Add(backstory.defName);
                            else
                                selectedDefNames.Remove(backstory.defName);
                        }
                    }
                    else
                    {
                        GUI.color = new Color(0.55f, 0.55f, 0.55f);
                        Widgets.Label(new Rect(rowRect.x + 28, rowRect.y + 4, rowRect.width - 28, rowRect.height), PawnFilter.GetBackstoryOptionLabel(backstory));
                        GUI.color = Color.white;
                    }

                    string tooltip = PawnFilter.GetBackstoryTooltip(backstory);
                    if (!string.IsNullOrEmpty(tooltip))
                        TooltipHandler.TipRegion(rowRect, tooltip);

                    cursor += rowHeight;
                }
            }
            finally
            {
                Widgets.EndScrollView();
            }
        }

        private string GetCategoryLabel(string category)
        {
            int total = 0;
            int selected = 0;
            foreach (BackstoryDef backstory in options)
            {
                if (!PawnFilter.GetBackstoryCategories(backstory).Contains(category))
                    continue;
                if (!IsVisibleByCurrentFilters(backstory))
                    continue;

                total++;
                if (selectedDefNames.Contains(backstory.defName))
                    selected++;
            }

            return $"{PawnFilter.GetBackstoryCategoryLabel(category)} ({selected}/{total})";
        }

        private static void DrawSearchPlaceholder(Rect rect)
        {
            Color previousColor = GUI.color;
            TextAnchor previousAnchor = Text.Anchor;
            GUI.color = new Color(0.65f, 0.65f, 0.65f);
            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(new Rect(rect.x + 6, rect.y, rect.width - 12, rect.height),
                "RandomPlus.PanelOthers.BackstoryFilter.SearchPlaceholder".Translate());
            Text.Anchor = previousAnchor;
            GUI.color = previousColor;
        }

        private bool IsCategoryEnabled(string category)
        {
            return options.Any(backstory =>
                PawnFilter.GetBackstoryCategories(backstory).Contains(category) &&
                IsVisibleByCurrentFilters(backstory));
        }

        private bool IsBackstoryEnabled(BackstoryDef backstory)
        {
            return PawnFilter.IsBackstoryRelevantToCategories(backstory, relevantCategories);
        }

        private void EnsureActiveCategoryAvailable()
        {
            if (IsCategoryEnabled(activeCategory))
                return;

            activeCategory = categories.FirstOrDefault(IsCategoryEnabled)
                ?? categories.FirstOrDefault()
                ?? PawnFilter.BackstoryUncategorizedCategory;
            optionScrollPosition = Vector2.zero;
        }

        private List<BackstoryDef> GetFilteredEnabledOptions()
        {
            return options
                .Where(IsVisibleByCurrentFilters)
                .ToList();
        }

        private List<BackstoryDef> GetSelectAllOptions()
        {
            if (string.IsNullOrWhiteSpace(searchText) && advancedSkillMinimums.Count == 0)
                return options;

            return GetFilteredEnabledOptions();
        }

        private bool IsVisibleByCurrentFilters(BackstoryDef backstory)
        {
            return IsBackstoryEnabled(backstory) &&
                MatchesSearchText(backstory) &&
                MatchesAdvancedSearch(backstory);
        }

        private bool MatchesSearchText(BackstoryDef backstory)
        {
            if (string.IsNullOrWhiteSpace(searchText))
                return true;

            string needle = searchText.Trim().ToLowerInvariant();
            return ContainsIgnoreCase(PawnFilter.GetBackstoryTitle(backstory), needle) ||
                ContainsIgnoreCase(backstory?.defName, needle) ||
                ContainsIgnoreCase(PawnFilter.GetBackstoryOptionLabel(backstory), needle);
        }

        private static bool ContainsIgnoreCase(string text, string lowerNeedle)
        {
            return !string.IsNullOrEmpty(text) &&
                text.ToLowerInvariant().Contains(lowerNeedle);
        }

        private bool MatchesAdvancedSearch(BackstoryDef backstory)
        {
            if (advancedSkillMinimums.Count == 0)
                return true;
            if (backstory?.skillGains == null)
                return false;

            foreach (KeyValuePair<SkillDef, int> filter in advancedSkillMinimums)
            {
                if (filter.Key == null || filter.Value <= 0)
                    continue;

                if (GetBackstorySkillGain(backstory, filter.Key) < filter.Value)
                    return false;
            }

            return true;
        }

        private static int GetBackstorySkillGain(BackstoryDef backstory, SkillDef skillDef)
        {
            if (backstory?.skillGains == null || skillDef == null)
                return 0;

            foreach (var skillGain in backstory.skillGains)
            {
                if (skillGain.skill == skillDef)
                    return skillGain.amount;
            }

            return 0;
        }

        private string GetAdvancedSearchButtonLabel()
        {
            int activeCount = advancedSkillMinimums.Count(pair => pair.Key != null && pair.Value > 0);
            if (activeCount <= 0)
                return "RandomPlus.PanelOthers.BackstoryFilter.AdvancedSearch".Translate().ToString();

            return string.Format(
                "RandomPlus.PanelOthers.BackstoryFilter.AdvancedSearchWithCount".Translate(),
                activeCount);
        }

        private void OpenAdvancedSearchDialog()
        {
            var dialog = new Dialog_BackstoryAdvancedSearch(advancedSkillMinimums);
            dialog.ConfirmAction = (skillMinimums) =>
            {
                advancedSkillMinimums.Clear();
                foreach (KeyValuePair<SkillDef, int> filter in skillMinimums)
                {
                    if (filter.Key != null && filter.Value > 0)
                        advancedSkillMinimums[filter.Key] = filter.Value;
                }
                optionScrollPosition = Vector2.zero;
            };
            Find.WindowStack.Add(dialog);
        }
    }
}
