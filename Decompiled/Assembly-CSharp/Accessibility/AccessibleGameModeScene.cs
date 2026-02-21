using Hearthstone.DataModels;
using Hearthstone.UI;
using System;
using System.Collections.Generic;

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
                m_mainMenu.AddOption(GetGameModeOptionText(buttonCopy), () => OnClickGameMode(buttonCopy), () => OnReadGameMode(buttonCopy));
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

        private void OnReadGameMode(GameModeButtonDataModel button)
        {
            var details = GetGameModeDetailsText(button);
            if (!string.IsNullOrWhiteSpace(details))
            {
                AccessibilityMgr.Output(this, details);
            }
        }

        private string GetGameModeDetailsText(GameModeButtonDataModel button)
        {
            if (button == null)
            {
                return "";
            }

            var details = new List<string>();

            var description = GetGameModeDescriptionText(button);
            if (!string.IsNullOrWhiteSpace(description))
            {
                details.Add(description);
            }

            var statuses = GetGameModeStatuses(button);
            if (statuses.Count > 0)
            {
                details.Add(AccessibleSpeechUtils.HumanizeList(statuses));
            }

            return AccessibleSpeechUtils.CombineLines(details);
        }

        private string GetGameModeDescriptionText(GameModeButtonDataModel button)
        {
            if (button == null || string.IsNullOrWhiteSpace(button.Description))
            {
                return "";
            }

            var description = GameStrings.Get(button.Description);
            if (!string.IsNullOrWhiteSpace(description))
            {
                return description;
            }

            return button.Description;
        }

        private List<string> GetGameModeStatuses(GameModeButtonDataModel button)
        {
            var statuses = new List<string>();

            if (button == null)
            {
                return statuses;
            }

            if (button.IsDownloading)
            {
                statuses.Add(GameStrings.Format("GLUE_GAME_MODE_TOOLTIP_DOWNLOADING_DESCRIPTION", GetGameModeOptionText(button)));
            }
            else if (button.IsDownloadRequired)
            {
                statuses.Add(GameStrings.Format("GLUE_GAME_MODE_TOOLTIP_DOWNLOAD_REQUIRED_DESCRIPTION", GetGameModeOptionText(button)));
            }

            if (button.IsNew)
            {
                statuses.Add(LocalizedText.COLLECTION_CARD_NEW);
            }

            if (button.IsEarlyAccess)
            {
                statuses.Add(GetGameStringOrFallback("GLOBAL_EARLY_ACCESS", "Early access"));
            }

            if (button.IsBeta)
            {
                statuses.Add(GetGameStringOrFallback("GLOBAL_BETA", "Beta"));
            }

            return statuses;
        }

        private string GetGameStringOrFallback(string key, string fallback)
        {
            var text = GameStrings.Get(key);
            if (string.IsNullOrWhiteSpace(text) || text == key)
            {
                return fallback;
            }

            return text;
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
