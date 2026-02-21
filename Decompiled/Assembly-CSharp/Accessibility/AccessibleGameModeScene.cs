using Hearthstone.DataModels;
using Hearthstone.UI;
using System;

namespace Accessibility
{
    class AccessibleGameModeScene : AccessibleScreen
    {
        private enum State { LOADING, MAIN_MENU };

        private State m_curState = State.LOADING;

        private AccessibleMenu m_mainMenu;

        private GameModeSceneDataModel m_gameModeSceneDataModel;

        private static AccessibleGameModeScene s_instance = new AccessibleGameModeScene();

        internal static AccessibleGameModeScene Get()
        {
            return s_instance;
        }

        public void OnDisplayReady(GameModeSceneDataModel gameModeSceneDataModel)
        {
            try
            {
                m_gameModeSceneDataModel = gameModeSceneDataModel;

                SetupMainMenu();

                AccessibilityMgr.SetScreen(this);
            }
            catch (Exception e)
            {
                AccessibilityUtils.LogFatalError(e);
            }
        }

        private void SetupMainMenu()
        {
            m_mainMenu = new AccessibleMenu(this, LocalizationUtils.Get(LocalizationKey.GLOBAL_CHOOSE_MODE), OnClickBackButton);
            AddGameModeOptions();
            m_mainMenu.AddOption(LocalizedText.SCREEN_GO_BACK, OnClickBackButton);

            m_curState = State.MAIN_MENU;
        }

        private void OnClickBackButton()
        {
            GameModeDisplay.Get().m_backButton.TriggerRelease();
        }

        private void AddGameModeOptions()
        {
            if (m_gameModeSceneDataModel?.GameModeButtons == null)
            {
                return;
            }

            foreach (var button in m_gameModeSceneDataModel.GameModeButtons)
            {
                if (button == null)
                {
                    continue;
                }

                var buttonCopy = button;
                m_mainMenu.AddOption(GetGameModeOptionText(buttonCopy), () => OnClickGameMode(buttonCopy));
            }
        }

        private string GetGameModeOptionText(GameModeButtonDataModel button)
        {
            var name = GameStrings.Get(button.Name);
            if (!string.IsNullOrWhiteSpace(name))
            {
                return name;
            }

            if (!string.IsNullOrWhiteSpace(button.Name))
            {
                return button.Name;
            }

            return "Unknown mode";
        }

        private void OnClickGameMode(GameModeButtonDataModel button)
        {
            if (button == null)
            {
                AccessibilityMgr.Output(this, LocalizationUtils.Get(LocalizationKey.GLOBAL_CANNOT_DO_THAT));
                return;
            }

            // Special case: some Tavern Brawl variants are known to be inaccessible.
            if (button.GameModeRecordId == 8 && !AccessibilityUtils.CanPlayTavernBrawl())
            {
                AccessibilityMgr.Output(this, LocalizationUtils.Get(LocalizationKey.HUB_TAVERN_BRAWL_INACCESSIBLE));
                return;
            }

            GameModeDisplay.Get().SelectMode(button);
            if (!GameModeDisplay.Get().CanEnterMode(out var reason, out var unused))
            {
                AccessibilityMgr.Output(this, reason);
                return;
            }

            GameModeDisplay.Get().m_playButton.TriggerRelease();
        }

        public void HandleInput()
        {
            if (m_curState == State.MAIN_MENU)
            {
                m_mainMenu?.HandleAccessibleInput();
            }
        }

        public string GetHelp()
        {
            switch (m_curState)
            {
                case State.MAIN_MENU:
                    return m_mainMenu?.GetHelp();
                default:
                    break;
            }

            return "";
        }

        public void OnGainedFocus()
        {
            if (m_curState == State.MAIN_MENU)
            {
                m_mainMenu.StartReading();
            }
        }
    }
}
