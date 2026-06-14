using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace RandomPlus
{
    public class Dialog_TraitMultiSelect : Window
    {
        private readonly List<Trait> options;
        private readonly HashSet<string> selectedKeys;
        private Vector2 scrollPosition = Vector2.zero;

        public string HeaderLabel;
        public Func<Trait, bool> EnabledFunc = (t) => true;
        public Func<Trait, string> NameFunc = (t) => t.LabelCap;
        public Func<Trait, string> DescriptionFunc = (t) => t.CurrentData.description;
        public Action<List<Trait>> ConfirmAction = (traits) => { };

        public Dialog_TraitMultiSelect(IEnumerable<Trait> options, IEnumerable<Trait> selectedTraits)
        {
            this.options = options.ToList();
            this.selectedKeys = new HashSet<string>((selectedTraits ?? Enumerable.Empty<Trait>()).Select(TraitKey));
            closeOnCancel = true;
            closeOnClickedOutside = true;
            doCloseX = true;
            absorbInputAroundWindow = true;
        }

        public override Vector2 InitialSize
        {
            get { return new Vector2(520f, 620f); }
        }

        public override void DoWindowContents(Rect inRect)
        {
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(0, 0, inRect.width, 32), HeaderLabel ?? "RandomPlus.PanelTraits.MultiSelect.Title".Translate());
            Text.Font = GameFont.Small;

            List<Trait> enabledOptions = options.Where(t => EnabledFunc(t)).ToList();
            bool allSelected = enabledOptions.Count > 0 && enabledOptions.All(t => selectedKeys.Contains(TraitKey(t)));
            bool selectAll = allSelected;
            Rect selectAllRect = new Rect(0, 38, inRect.width - 16, 28);
            Widgets.CheckboxLabeled(selectAllRect, "RandomPlus.PanelTraits.MultiSelect.SelectAll".Translate(), ref selectAll);
            if (selectAll != allSelected)
            {
                if (selectAll)
                {
                    foreach (Trait trait in enabledOptions)
                        selectedKeys.Add(TraitKey(trait));
                }
                else
                {
                    foreach (Trait trait in enabledOptions)
                        selectedKeys.Remove(TraitKey(trait));
                }
            }

            Rect outRect = new Rect(0, 72, inRect.width, inRect.height - 124);
            float rowHeight = 30f;
            Rect viewRect = new Rect(0, 0, outRect.width - 16, options.Count * rowHeight);
            Widgets.BeginScrollView(outRect, ref scrollPosition, viewRect);
            try
            {
                float cursor = 0;
                foreach (Trait trait in options)
                {
                    bool enabled = EnabledFunc(trait);
                    bool selected = selectedKeys.Contains(TraitKey(trait));
                    Rect rowRect = new Rect(0, cursor, viewRect.width, rowHeight);
                    string label = NameFunc(trait);

                    if (enabled)
                    {
                        bool newSelected = selected;
                        Widgets.CheckboxLabeled(rowRect, label, ref newSelected);
                        if (newSelected != selected)
                        {
                            if (newSelected)
                                selectedKeys.Add(TraitKey(trait));
                            else
                                selectedKeys.Remove(TraitKey(trait));
                        }
                    }
                    else
                    {
                        GUI.color = new Color(0.65f, 0.65f, 0.65f);
                        Widgets.Label(new Rect(0, cursor + 4, rowRect.width - 28, rowHeight), label);
                        GUI.color = Color.white;
                    }

                    string description = DescriptionFunc?.Invoke(trait);
                    if (!string.IsNullOrEmpty(description))
                        TooltipHandler.TipRegion(rowRect, description);

                    cursor += rowHeight;
                }
            }
            finally
            {
                Widgets.EndScrollView();
            }

            Rect cancelRect = new Rect(0, inRect.height - 40, 140, 36);
            Rect confirmRect = new Rect(inRect.width - 140, inRect.height - 40, 140, 36);
            if (Widgets.ButtonText(cancelRect, "RandomPlus.Dialog.Cancel".Translate(), true, true, true))
                Close(true);
            if (Widgets.ButtonText(confirmRect, "RandomPlus.Dialog.Apply".Translate(), true, true, true))
            {
                ConfirmAction(options.Where(t => selectedKeys.Contains(TraitKey(t))).ToList());
                Close(true);
            }
        }

        private static string TraitKey(Trait trait)
        {
            if (trait == null || trait.def == null)
                return "";
            return trait.def.defName + ":" + trait.Degree;
        }
    }
}
