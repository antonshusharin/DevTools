using System.Collections.Generic;

namespace Accessibility
{
    class AccessibleHeroPickerButton : AccessibleItem
    {
        private readonly HeroPickerButton m_button;

        private bool m_inCreateDeckMode;

        internal AccessibleHeroPickerButton(AccessibleComponent parent, HeroPickerButton button, bool inCreateDeckMode=false) : base(parent)
        {
            m_button = button;
            m_inCreateDeckMode = inCreateDeckMode;
        }

        internal override List<string> GetLines()
        {
            if (m_inCreateDeckMode)
            {
                return GetLinesForCreateDeckMode();
            }
            else
            {
                return GetLinesForAdventureMode();
            }
        }

        private List<string> GetLinesForCreateDeckMode()
        {
            var ret = new List<string>();

            var heroClass = GameStrings.GetClassName(m_button.GetEntityDef().GetClass());

            if (heroClass != null && heroClass.Length > 0)
            {
                ret.Add(heroClass);
            }

            return ret;
        }

        private List<string> GetLinesForAdventureMode()
        {
            var ret = new List<string>();

            var heroName = m_button.GetEntityDef()?.GetName();

            AddLineIfNotEmpty(ret, heroName);

            if (IsRastakhansRumbleSelected())
            {
                AddRastakhansRumbleStatus(ret);
            }

            if (m_button.m_crown != null && m_button.m_crown.activeInHierarchy)
            {
                AddLineIfNotEmpty(ret, LocalizationUtils.Get(LocalizationKey.SCREEN_ADVENTURE_SCREEN_ADVENTURE_COMPLETE));
            }

            return ret;
        }

        private static bool IsRastakhansRumbleSelected()
        {
            if (SceneMgr.Get()?.GetMode() != SceneMgr.Mode.ADVENTURE)
            {
                return false;
            }

            AdventureConfig adventureConfig = AdventureConfig.Get();

            return adventureConfig != null && adventureConfig.GetSelectedAdventure() == AdventureDbId.TRL;
        }

        private void AddRastakhansRumbleStatus(List<string> lines)
        {
            var dataModel = m_button.GetDataModel();
            var isTimeLocked = dataModel?.IsTimelocked ?? false;
            var isUnowned = dataModel?.IsUnowned ?? false;

            if (m_button.IsLocked() || isTimeLocked)
            {
                AddLineIfNotEmpty(lines, GameStrings.Get("GLOBAL_NOT_AVAILABLE"));
                AddLineIfNotEmpty(lines, m_button.m_lockReasonText?.Text);
                AddLineIfNotEmpty(lines, dataModel?.ComingSoonText);
                AddLineIfNotEmpty(lines, dataModel?.UnlockCriteriaText);
                return;
            }

            if (isUnowned)
            {
                AddLineIfNotEmpty(lines, GameStrings.Get("GLUE_HERO_LOCKED_NAME"));
                AddLineIfNotEmpty(lines, dataModel?.UnlockCriteriaText);
            }
        }

        private static void AddLineIfNotEmpty(List<string> lines, string line)
        {
            if (line != null && line.Length > 0 && !lines.Contains(line))
            {
                lines.Add(line);
            }
        }

        internal HeroPickerButton GetHeroPickerButton()
        {
            return m_button;
        }
    }
}
