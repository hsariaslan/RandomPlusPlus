using System;
using System.Linq;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace RandomPlus
{
    public class PanelOthers : PanelBase
    {
        private Rect mainRect = new Rect(0, 0, 500, 500);

        public PanelOthers()
        {
            Resize(new Rect(340, 314, 658, 226));
        }

        public override string PanelHeader
        {
            get
            {
                return "RandomPlus.PanelOthers.Header".Translate();
            }
        }

        private readonly static Vector2 otherLabelOffset = new Vector2(0, 24);
        private readonly static Vector2 otherButtonOffset = new Vector2(0, 24);

        private Rect randomRerollAlgorithmLabelRect, randomRerollAlgorithmButtonRect;
        private Rect randomRerollLimitLabelRect, randomRerollLimitButtonRect;
        private Rect genderLabelRect, genderButtonRect;
        private Rect ageLabelRect, ageRangeRect;

        private Rect healthConditionLabelRect, healthConditionButtonRect;
        private Rect incapableLabelRect, incapableButtonRect;
        private Rect childhoodBackstoryLabelRect, childhoodBackstoryButtonRect;
        private Rect adulthoodBackstoryLabelRect, adulthoodBackstoryButtonRect;
        private string childhoodBackstoryWarning;
        private string adulthoodBackstoryWarning;
        private string backstoryPairWarning;

        public override void Resize(Rect rect)
        {
            base.Resize(rect);
            mainRect = new Rect(0, 0, rect.width, rect.height);
            randomRerollAlgorithmLabelRect = new Rect(18, 42, 150, 30);
            randomRerollAlgorithmButtonRect = new Rect(150, 43, 154, 20);

            randomRerollLimitLabelRect = randomRerollAlgorithmLabelRect.OffsetBy(otherLabelOffset);
            randomRerollLimitButtonRect = randomRerollAlgorithmButtonRect.OffsetBy(otherButtonOffset);

            genderLabelRect = randomRerollLimitLabelRect.OffsetBy(otherLabelOffset);
            genderButtonRect = randomRerollLimitButtonRect.OffsetBy(otherButtonOffset);

            healthConditionLabelRect = genderLabelRect.OffsetBy(otherLabelOffset);
            healthConditionButtonRect = genderButtonRect.OffsetBy(otherButtonOffset);

            incapableLabelRect = healthConditionLabelRect.OffsetBy(otherLabelOffset);
            incapableButtonRect = healthConditionButtonRect.OffsetBy(otherButtonOffset);

            ageLabelRect = incapableLabelRect.OffsetBy(0, 28);
            ageLabelRect.width = 284;
            ageRangeRect = ageLabelRect.OffsetBy(0, 8);

            childhoodBackstoryLabelRect = new Rect(342, 42, 120, 30);
            childhoodBackstoryButtonRect = new Rect(474, 43, 154, 20);

            adulthoodBackstoryLabelRect = childhoodBackstoryLabelRect.OffsetBy(otherLabelOffset);
            adulthoodBackstoryButtonRect = childhoodBackstoryButtonRect.OffsetBy(otherButtonOffset);
        }
        
        protected override void DrawPanelContent()
        {
            base.DrawPanelContent();
            UpdateBackstoryWarnings();

            GUI.BeginGroup(mainRect);
            try 
            { 
                Widgets.Label(randomRerollAlgorithmLabelRect, "RandomPlus.PanelOthers.RerollAlgorithmLabel".Translate());
                drawRerollAlgorithm(randomRerollAlgorithmButtonRect);

                Widgets.Label(randomRerollLimitLabelRect, "RandomPlus.PanelOthers.RerollLimitLabel".Translate());
                drawRerollLimit(randomRerollLimitButtonRect);

                Widgets.Label(genderLabelRect, "RandomPlus.PanelOthers.GenderLabel".Translate());
                drawGender(genderButtonRect);

                Widgets.Label(healthConditionLabelRect, "RandomPlus.PanelOthers.HealthOptionLabel".Translate());
                drawHealthCondition(healthConditionButtonRect);

                Widgets.Label(incapableLabelRect, "RandomPlus.PanelOthers.IncapableOptionLabel".Translate());
                drawIncapable(incapableButtonRect);

                DrawLabelWithWarning(
                    childhoodBackstoryLabelRect,
                    "RandomPlus.PanelOthers.ChildhoodBackstoryLabel".Translate(),
                    childhoodBackstoryWarning,
                    !string.IsNullOrEmpty(backstoryPairWarning));
                drawBackstoryFilter(childhoodBackstoryButtonRect, BackstorySlot.Childhood);

                DrawLabelWithWarning(
                    adulthoodBackstoryLabelRect,
                    "RandomPlus.PanelOthers.AdulthoodBackstoryLabel".Translate(),
                    adulthoodBackstoryWarning,
                    !string.IsNullOrEmpty(backstoryPairWarning));
                drawBackstoryFilter(adulthoodBackstoryButtonRect, BackstorySlot.Adulthood);

                string minAgeString = RandomSettings.PawnFilter.ageRange.min.ToString();
                string maxAgeString = (RandomSettings.PawnFilter.ageRange.max == PawnFilter.MaxAgeDefault) ? "∞" : RandomSettings.PawnFilter.ageRange.max.ToString();

                string labelText = string.Format("RandomPlus.PanelOthers.AgeLabel".Translate(),
                    minAgeString,
                    maxAgeString);
                Widgets.Label(ageLabelRect, labelText);
                Widgets.IntRange(ageRangeRect, 20, ref RandomSettings.PawnFilter.ageRange,
                    0, //PawnFilter.MinAgeDefault, 
                    PawnFilter.MaxAgeDefault,
                    "", 2);
            }
            finally
            {
                GUI.EndGroup();
            }

            GUI.color = Color.white;
        }

        public string CurrentWarning
        {
            get
            {
                UpdateBackstoryWarnings();
                List<string> warnings = new List<string>();
                if (!string.IsNullOrEmpty(childhoodBackstoryWarning))
                    warnings.Add(childhoodBackstoryWarning);
                if (!string.IsNullOrEmpty(adulthoodBackstoryWarning))
                    warnings.Add(adulthoodBackstoryWarning);
                if (!string.IsNullOrEmpty(backstoryPairWarning))
                    warnings.Add(backstoryPairWarning);

                if (warnings.Count == 0)
                    return null;

                return "RandomPlus.PanelOthers.BackstoryFilter.Warning.Title".Translate() +
                    "\n\n" + string.Join("\n", warnings);
            }
        }

        private void UpdateBackstoryWarnings()
        {
            childhoodBackstoryWarning = null;
            adulthoodBackstoryWarning = null;
            backstoryPairWarning = null;

            if (RandomSettings.PawnFilter == null)
                return;

            int childhoodCount = RandomSettings.PawnFilter.AllowedBackstoryCount(BackstorySlot.Childhood);
            int adulthoodCount = RandomSettings.PawnFilter.AllowedBackstoryCount(BackstorySlot.Adulthood);

            if (childhoodCount == 0)
            {
                childhoodBackstoryWarning = "RandomPlus.PanelOthers.BackstoryFilter.Warning.NoChildhood".Translate();
            }

            if (RandomSettings.PawnFilter.ageRange.min >= 20 && adulthoodCount == 0)
            {
                adulthoodBackstoryWarning = "RandomPlus.PanelOthers.BackstoryFilter.Warning.NoAdultWithAdultAge".Translate();
            }

            if (childhoodCount > 0 && adulthoodCount > 0 &&
                !RandomSettings.PawnFilter.HasAnyCompatibleSelectedBackstoryPair())
            {
                backstoryPairWarning = "RandomPlus.PanelOthers.BackstoryFilter.Warning.NoCompatiblePair".Translate();
            }
        }

        private void DrawLabelWithWarning(Rect rect, string label, string warning, bool includePairWarning)
        {
            GUI.color = Style.ColorText;
            Widgets.Label(rect, label);

            List<string> warnings = new List<string>();
            if (!string.IsNullOrEmpty(warning))
                warnings.Add(warning);
            if (includePairWarning && !string.IsNullOrEmpty(backstoryPairWarning))
                warnings.Add(backstoryPairWarning);

            if (warnings.Count > 0)
            {
                float labelWidth = Text.CalcSize(label).x;
                Rect alertRect = new Rect(rect.x + labelWidth + 6, rect.y + 1, 18, 18);
                GUI.color = Color.white;
                GUI.DrawTexture(alertRect, Textures.TextureAlertSmall);
                TooltipHandler.TipRegion(alertRect, string.Join("\n", warnings));
            }

            GUI.color = Color.white;
        }

        private readonly static Action<Enum> rerollAlgorithmCallback = (Enum val) => RandomSettings.PawnFilter.RerollAlgorithm = (PawnFilter.RerollAlgorithmOptions)val;
        public void drawRerollAlgorithm(Rect rect)
        {
            drawButton(
                rect, 
                PawnFilter.RerollAlgorithmOptionValues[(int)RandomSettings.PawnFilter.RerollAlgorithm], 
                typeof(PawnFilter.RerollAlgorithmOptions), 
                PawnFilter.RerollAlgorithmOptionValues, 
                rerollAlgorithmCallback);
        }

        private readonly static Action<Enum> rerollLimitCallback = (Enum val) => RandomSettings.PawnFilter.RerollLimit = (int)(PawnFilter.RerollLimitOptions)val;
        public void drawRerollLimit(Rect rect)
        {
            drawButton(rect, RandomSettings.PawnFilter.RerollLimit.ToString(), typeof(PawnFilter.RerollLimitOptions), PawnFilter.RerollLimitOptionValues, rerollLimitCallback);
        }

        private readonly static Action<Enum> genderCallback = (Enum val) => RandomSettings.SetGenderFilter((Gender)val);
        public void drawGender(Rect rect)
        {
            var displayedNameArray = Enum.GetValues(typeof(Gender)).Cast<Gender>().ToList().Select((gender) => GenderUtility.GetLabel(gender)).ToArray();
            drawButton(rect, GenderUtility.GetLabel(RandomSettings.PawnFilter.Gender), typeof(Gender), displayedNameArray, genderCallback, false);
        }

        public void drawHealthCondition(Rect rect)
        {
            if (Widgets.ButtonText(rect, GetHealthButtonLabel(), true, true, true))
            {
                List<FloatMenuOption> options = new List<FloatMenuOption>();
                var enumOptions = Enum.GetValues(typeof(PawnFilter.HealthOptions)).Cast<PawnFilter.HealthOptions>().ToArray();
                for (int i = 0; i < enumOptions.Length; i++)
                {
                    var option = enumOptions[i];
                    if (PawnFilter.HealthOptionValues.Length <= i)
                        continue;

                    string displayedName = PawnFilter.HealthOptionValues[i].Translate().CapitalizeFirst().ToString();
                    options.Add(new FloatMenuOption(displayedName, () =>
                    {
                        RandomSettings.PawnFilter.FilterHealthCondition = option;
                        if (option == PawnFilter.HealthOptions.Custom)
                            OpenCustomHealthDialog();
                    }));
                }
                Find.WindowStack.Add(new FloatMenu(options));
            }
        }

        private string GetHealthButtonLabel()
        {
            if (RandomSettings.PawnFilter.FilterHealthCondition == PawnFilter.HealthOptions.Custom)
            {
                string customLabel = "RandomPlus.PanelOthers.HealthOptions.CustomWithCount".Translate().ToString();
                return string.Format(customLabel, RandomSettings.PawnFilter.CustomAllowedHealthCount);
            }

            return PawnFilter.HealthOptionValues[(int)RandomSettings.PawnFilter.FilterHealthCondition]
                .Translate()
                .CapitalizeFirst()
                .ToString();
        }

        private void OpenCustomHealthDialog()
        {
            var dialog = new Dialog_CustomHealthSelect(
                PawnFilter.GetAllCustomHealthKeys(),
                RandomSettings.PawnFilter.CustomAllowedHealthKeys);
            dialog.ConfirmAction = (selectedKeys) =>
            {
                RandomSettings.PawnFilter.SetCustomAllowedHealthKeys(selectedKeys);
            };
            Find.WindowStack.Add(dialog);
        }

        public void drawIncapable(Rect rect)
        {
            if (Widgets.ButtonText(rect, GetIncapableButtonLabel(), true, true, true))
            {
                List<FloatMenuOption> options = new List<FloatMenuOption>();
                var enumOptions = Enum.GetValues(typeof(PawnFilter.IncapableOptions)).Cast<PawnFilter.IncapableOptions>().ToArray();
                for (int i = 0; i < enumOptions.Length; i++)
                {
                    var option = enumOptions[i];
                    if (PawnFilter.IncapableOptionValues.Length <= i)
                        continue;

                    string displayedName = PawnFilter.IncapableOptionValues[i].Translate().CapitalizeFirst().ToString();
                    options.Add(new FloatMenuOption(displayedName, () =>
                    {
                        RandomSettings.PawnFilter.FilterIncapable = option;
                        if (option == PawnFilter.IncapableOptions.Custom)
                            OpenCustomIncapableDialog();
                    }));
                }
                Find.WindowStack.Add(new FloatMenu(options));
            }
        }

        private string GetIncapableButtonLabel()
        {
            if (RandomSettings.PawnFilter.FilterIncapable == PawnFilter.IncapableOptions.Custom)
            {
                string customLabel = "RandomPlus.PanelOthers.IncapableOptions.CustomWithCount".Translate().ToString();
                return string.Format(customLabel, RandomSettings.PawnFilter.CustomAllowedIncapableCount);
            }

            return PawnFilter.IncapableOptionValues[(int)RandomSettings.PawnFilter.FilterIncapable]
                .Translate()
                .CapitalizeFirst()
                .ToString();
        }

        private void OpenCustomIncapableDialog()
        {
            var dialog = new Dialog_CustomIncapableSelect(
                PawnFilter.GetAllCustomIncapableKeys(),
                RandomSettings.PawnFilter.CustomAllowedIncapableKeys);
            dialog.ConfirmAction = (selectedKeys) =>
            {
                RandomSettings.PawnFilter.SetCustomAllowedIncapableKeys(selectedKeys);
            };
            Find.WindowStack.Add(dialog);
        }

        private void drawBackstoryFilter(Rect rect, BackstorySlot slot)
        {
            if (Widgets.ButtonText(rect, GetBackstoryButtonLabel(slot), true, true, true))
                OpenBackstoryDialog(slot);
        }

        private string GetBackstoryButtonLabel(BackstorySlot slot)
        {
            int selected = RandomSettings.PawnFilter.AllowedBackstoryCount(slot);
            int total = RandomSettings.PawnFilter.TotalBackstoryCount(slot);
            if (selected >= total)
                return "RandomPlus.PanelOthers.BackstoryFilter.All".Translate().ToString();

            string countLabel = "RandomPlus.PanelOthers.BackstoryFilter.Count".Translate().ToString();
            return string.Format(countLabel, selected, total);
        }

        private void OpenBackstoryDialog(BackstorySlot slot)
        {
            IEnumerable<string> selected = slot == BackstorySlot.Childhood
                ? RandomSettings.PawnFilter.AllowedChildhoodBackstoryDefNames
                : RandomSettings.PawnFilter.AllowedAdulthoodBackstoryDefNames;

            var dialog = new Dialog_BackstoryMultiSelect(slot, selected);
            dialog.ConfirmAction = (selectedDefNames) =>
            {
                RandomSettings.PawnFilter.SetAllowedBackstoryDefNames(slot, selectedDefNames);
            };
            Find.WindowStack.Add(dialog);
        }

        public void drawButton(Rect rect, string label, Type enumOptionType, string[] displayedNameArray, Action<Enum> callback, bool translate = true)
        {
            string displayLabel = (translate) ? label.Translate().CapitalizeFirst().ToString() : label.CapitalizeFirst();
            if (Widgets.ButtonText(rect, displayLabel, true, true, true))
            {
                List<FloatMenuOption> options = new List<FloatMenuOption>();
                var enumOptions = Enum.GetValues(enumOptionType).Cast<Enum>().ToArray();
                for (int i=0; i < enumOptions.Length; i++)
                {
                    var option = enumOptions[i];
                    if (displayedNameArray.Length > i)
                    {
                        var displayedName = (translate) ? displayedNameArray[i].Translate().CapitalizeFirst().ToString() : displayedNameArray[i].CapitalizeFirst();
                        var menuOption = new FloatMenuOption(displayedName, () => {
                            callback?.Invoke(option);
                        });
                        options.Add(menuOption);
                    }
                }
                Find.WindowStack.Add(new FloatMenu(options));
            }
        }
    }
}
