using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace RandomPlus
{
    public class Dialog_BackstoryAdvancedSearch : Window
    {
        private readonly List<SkillDef> skills;
        private readonly Dictionary<SkillDef, int> skillMinimums;
        private Vector2 scrollPosition = Vector2.zero;

        public Action<Dictionary<SkillDef, int>> ConfirmAction = (skillFilters) => { };

        public override Vector2 InitialSize => new Vector2(460f, 560f);

        public Dialog_BackstoryAdvancedSearch(Dictionary<SkillDef, int> skillMinimums)
        {
            skills = DefDatabase<SkillDef>.AllDefsListForReading
                .OrderByDescending(skill => skill.listOrder)
                .ThenBy(skill => skill.skillLabel)
                .ToList();
            this.skillMinimums = new Dictionary<SkillDef, int>();

            if (skillMinimums != null)
            {
                foreach (KeyValuePair<SkillDef, int> filter in skillMinimums)
                {
                    if (filter.Key != null && filter.Value > 0)
                        this.skillMinimums[filter.Key] = Mathf.Clamp(filter.Value, 0, 20);
                }
            }

            closeOnCancel = true;
            closeOnClickedOutside = true;
            doCloseX = true;
            absorbInputAroundWindow = true;
        }

        public override void DoWindowContents(Rect inRect)
        {
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(0, 0, inRect.width - 150, 32), "RandomPlus.PanelOthers.BackstoryFilter.AdvancedSearchTitle".Translate());
            Text.Font = GameFont.Small;

            Rect clearRect = new Rect(inRect.width - 120, 2, 120, 28);
            if (Widgets.ButtonText(clearRect, "RandomPlus.Dialog.ClearAll".Translate(), true, true, true))
                skillMinimums.Clear();

            Rect headerRect = new Rect(0, 38, inRect.width, 26);
            Widgets.Label(headerRect, "RandomPlus.PanelOthers.BackstoryFilter.AdvancedSearchHint".Translate());

            Rect outRect = new Rect(0, 70, inRect.width, inRect.height - 122);
            float rowHeight = 30f;
            Rect viewRect = new Rect(0, 0, outRect.width - 16, skills.Count * rowHeight);
            Widgets.BeginScrollView(outRect, ref scrollPosition, viewRect);
            try
            {
                float cursor = 0;
                foreach (SkillDef skill in skills)
                {
                    DrawSkillRow(new Rect(0, cursor, viewRect.width, rowHeight), skill);
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
                ConfirmAction(skillMinimums
                    .Where(pair => pair.Key != null && pair.Value > 0)
                    .ToDictionary(pair => pair.Key, pair => pair.Value));
                Close(true);
            }
        }

        private void DrawSkillRow(Rect rowRect, SkillDef skill)
        {
            int value = GetSkillMinimum(skill);
            Rect labelRect = new Rect(rowRect.x, rowRect.y + 4, rowRect.width - 132, rowRect.height);
            Rect minusRect = new Rect(rowRect.xMax - 126, rowRect.y + 3, 28, 24);
            Rect valueRect = new Rect(minusRect.xMax + 6, rowRect.y + 3, 56, 24);
            Rect plusRect = new Rect(valueRect.xMax + 6, rowRect.y + 3, 28, 24);

            Widgets.Label(labelRect, skill.skillLabel.CapitalizeFirst());

            if (Widgets.ButtonText(minusRect, "-", true, true, value > 0))
                SetSkillMinimum(skill, value - 1);

            Text.Anchor = TextAnchor.MiddleCenter;
            Widgets.Label(valueRect, value <= 0 ? "RandomPlus.PanelOthers.BackstoryFilter.AnySkillGain".Translate().ToString() : $"+{value}");
            Text.Anchor = TextAnchor.UpperLeft;

            if (Widgets.ButtonText(plusRect, "+", true, true, value < 20))
                SetSkillMinimum(skill, value + 1);
        }

        private int GetSkillMinimum(SkillDef skill)
        {
            if (skill == null)
                return 0;

            return skillMinimums.TryGetValue(skill, out int value)
                ? Mathf.Clamp(value, 0, 20)
                : 0;
        }

        private void SetSkillMinimum(SkillDef skill, int value)
        {
            if (skill == null)
                return;

            value = Mathf.Clamp(value, 0, 20);
            if (value <= 0)
                skillMinimums.Remove(skill);
            else
                skillMinimums[skill] = value;
        }
    }
}
