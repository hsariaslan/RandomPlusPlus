using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace RandomPlus
{
    public class Dialog_CustomHealthSelect : Window
    {
        private readonly List<string> options;
        private readonly HashSet<string> selectedKeys;
        private Vector2 scrollPosition = Vector2.zero;

        public Action<List<string>> ConfirmAction = (keys) => { };

        public override Vector2 InitialSize => new Vector2(440f, 584f);

        public Dialog_CustomHealthSelect(IEnumerable<string> options, IEnumerable<string> selectedKeys)
        {
            this.options = options.ToList();
            this.selectedKeys = new HashSet<string>(selectedKeys ?? Enumerable.Empty<string>());
            closeOnCancel = true;
            closeOnClickedOutside = true;
            doCloseX = true;
            absorbInputAroundWindow = true;
        }

        public override void DoWindowContents(Rect inRect)
        {
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(0, 0, inRect.width, 32), "RandomPlus.PanelOthers.CustomHealth.Title".Translate());
            Text.Font = GameFont.Small;

            bool allSelected = options.Count > 0 && options.All(key => selectedKeys.Contains(key));
            bool selectAll = allSelected;
            Rect selectAllRect = new Rect(0, 38, inRect.width - 16, 28);
            Widgets.CheckboxLabeled(selectAllRect, "RandomPlus.PanelOthers.CustomHealth.SelectAll".Translate(), ref selectAll);
            if (selectAll != allSelected)
            {
                if (selectAll)
                {
                    foreach (string key in options)
                        selectedKeys.Add(key);
                }
                else
                {
                    selectedKeys.Clear();
                }
            }

            Rect scrollOuter = new Rect(0, 74, inRect.width, inRect.height - 124);
            float rowHeight = 28f;
            Rect viewRect = new Rect(0, 0, scrollOuter.width - 16, options.Count * rowHeight);

            Widgets.BeginScrollView(scrollOuter, ref scrollPosition, viewRect);
            try
            {
                float cursor = 0;
                foreach (string key in options)
                {
                    bool selected = selectedKeys.Contains(key);
                    bool newSelected = selected;
                    Rect rowRect = new Rect(0, cursor, viewRect.width, rowHeight);
                    Widgets.CheckboxLabeled(rowRect, GetOptionLabel(key), ref newSelected);
                    if (newSelected != selected)
                    {
                        if (newSelected)
                            selectedKeys.Add(key);
                        else
                            selectedKeys.Remove(key);
                    }
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
                ConfirmAction(options.Where(key => selectedKeys.Contains(key)).ToList());
                Close(true);
            }
        }

        private static string GetOptionLabel(string key)
        {
            if (key == PawnFilter.CustomHealthStartConditionsKey)
                return "RandomPlus.PanelOthers.CustomHealth.StartConditions".Translate();
            if (key == PawnFilter.CustomHealthPregnancyKey)
                return "RandomPlus.PanelOthers.CustomHealth.Pregnancy".Translate();
            if (key == PawnFilter.CustomHealthPainKey)
                return "RandomPlus.PanelOthers.CustomHealth.Pain".Translate();
            if (key == PawnFilter.CustomHealthScarKey)
                return "RandomPlus.PanelOthers.CustomHealth.Scar".Translate();
            if (key == PawnFilter.CustomHealthAddictionKey)
                return "RandomPlus.PanelOthers.CustomHealth.Addiction".Translate();
            if (key == PawnFilter.CustomHealthBadModificationsKey)
                return "RandomPlus.PanelOthers.CustomHealth.BadModifications".Translate();
            if (key == PawnFilter.CustomHealthGoodModificationsKey)
                return "RandomPlus.PanelOthers.CustomHealth.GoodModifications".Translate();
            if (key == PawnFilter.CustomHealthOthersKey)
                return "RandomPlus.PanelOthers.CustomHealth.Others".Translate();

            return key;
        }
    }
}
