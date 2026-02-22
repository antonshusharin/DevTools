using Hearthstone.DataModels;
using Hearthstone.UI;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Accessibility
{
    class AccessibleGameModeScene : AccessibleScreen
    {
        private enum State { LOADING, MAIN_MENU };

        private const float MERCENARIES_ENTRY_WAIT_TIMEOUT_SECONDS = 8f;

        private State m_curState = State.LOADING;

        private AccessibleMenu m_mainMenu;

        private GameModeSceneDataModel m_gameModeSceneDataModel;

        private Coroutine m_pendingMercenariesEntryCoroutine;

        private GameModeDisplay m_pendingMercenariesEntryOwner;

        private static AccessibleGameModeScene s_instance = new AccessibleGameModeScene();

        internal static AccessibleGameModeScene Get()
        {
            return s_instance;
        }

        public void OnDisplayReady(GameModeSceneDataModel gameModeSceneDataModel)
        {
            try
            {
                CancelPendingMercenariesEntry();
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
            CancelPendingMercenariesEntry();
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

            GameModeDisplay gameModeDisplay = GameModeDisplay.Get();
            if (gameModeDisplay == null)
            {
                AccessibilityMgr.Output(this, AccessibleSpeechUtils.CombineLines(LocalizedText.GLOBAL_LOADING, LocalizedText.GLOBAL_PLEASE_WAIT), interrupt: true);
                return;
            }

            gameModeDisplay.SelectMode(button);
            if (!gameModeDisplay.CanEnterMode(out var reason, out var unused))
            {
                AccessibilityMgr.Output(this, reason);
                return;
            }

            if (IsMercenariesMode(button))
            {
                TryEnterMercenariesMode(gameModeDisplay, button);
                return;
            }

            CancelPendingMercenariesEntry();
            gameModeDisplay.m_playButton.TriggerRelease();
        }

        private bool IsMercenariesMode(GameModeButtonDataModel button)
        {
            if (button == null)
            {
                return false;
            }
            GameModeDbfRecord record = GameDbf.GameMode.GetRecord(button.GameModeRecordId);
            if (record == null || string.IsNullOrWhiteSpace(record.LinkedScene))
            {
                return false;
            }
            return string.Equals(record.LinkedScene, SceneMgr.Mode.LETTUCE_VILLAGE.ToString(), StringComparison.OrdinalIgnoreCase);
        }

        private bool IsMercenariesPlayerInfoLoaded()
        {
            return NetCache.Get().GetNetObject<NetCache.NetCacheMercenariesPlayerInfo>() != null;
        }

        private void TryEnterMercenariesMode(GameModeDisplay gameModeDisplay, GameModeButtonDataModel button)
        {
            if (gameModeDisplay == null || button == null)
            {
                AccessibilityMgr.Output(this, LocalizationUtils.Get(LocalizationKey.GLOBAL_CANNOT_DO_THAT));
                return;
            }
            if (IsMercenariesPlayerInfoLoaded())
            {
                CancelPendingMercenariesEntry();
                gameModeDisplay.m_playButton.TriggerRelease();
                return;
            }
            if (m_pendingMercenariesEntryCoroutine != null)
            {
                AccessibilityMgr.Output(this, "Mercenaries are still loading. Please wait.", interrupt: true);
                return;
            }
            AccessibilityMgr.Output(this, "Loading Mercenaries data. Entering automatically when ready.", interrupt: true);
            m_pendingMercenariesEntryOwner = gameModeDisplay;
            m_pendingMercenariesEntryCoroutine = gameModeDisplay.StartCoroutine(WaitForMercenariesDataAndEnter(button.GameModeRecordId));
        }

        private IEnumerator WaitForMercenariesDataAndEnter(int gameModeRecordId)
        {
            float elapsed = 0f;
            while (elapsed < MERCENARIES_ENTRY_WAIT_TIMEOUT_SECONDS)
            {
                if (SceneMgr.Get().GetMode() != SceneMgr.Mode.GAME_MODE)
                {
                    m_pendingMercenariesEntryCoroutine = null;
                    m_pendingMercenariesEntryOwner = null;
                    yield break;
                }

                if (m_gameModeSceneDataModel != null && m_gameModeSceneDataModel.LastSelectedGameModeRecordId != gameModeRecordId)
                {
                    m_pendingMercenariesEntryCoroutine = null;
                    m_pendingMercenariesEntryOwner = null;
                    yield break;
                }

                if (IsMercenariesPlayerInfoLoaded())
                {
                    GameModeDisplay display = GameModeDisplay.Get();
                    if (display == null)
                    {
                        break;
                    }
                    GameModeButtonDataModel modeButton = FindGameModeButton(gameModeRecordId);
                    if (modeButton == null)
                    {
                        AccessibilityMgr.Output(this, "Mercenaries mode is no longer available.", interrupt: true);
                        m_pendingMercenariesEntryCoroutine = null;
                        m_pendingMercenariesEntryOwner = null;
                        yield break;
                    }
                    display.SelectMode(modeButton);
                    if (!display.CanEnterMode(out var reason, out var unused))
                    {
                        AccessibilityMgr.Output(this, reason, interrupt: true);
                        m_pendingMercenariesEntryCoroutine = null;
                        m_pendingMercenariesEntryOwner = null;
                        yield break;
                    }
                    AccessibilityMgr.Output(this, "Mercenaries data loaded. Entering mode.", interrupt: true);
                    m_pendingMercenariesEntryCoroutine = null;
                    m_pendingMercenariesEntryOwner = null;
                    display.m_playButton.TriggerRelease();
                    yield break;
                }

                elapsed += Mathf.Max(Time.unscaledDeltaTime, 0.05f);
                yield return null;
            }

            m_pendingMercenariesEntryCoroutine = null;
            m_pendingMercenariesEntryOwner = null;
            AccessibilityMgr.Output(this, "Mercenaries data is still loading. Please wait a moment and try again.", interrupt: true);
        }

        private GameModeButtonDataModel FindGameModeButton(int gameModeRecordId)
        {
            if (m_gameModeSceneDataModel?.GameModeButtons == null)
            {
                return null;
            }
            foreach (GameModeButtonDataModel modeButton in m_gameModeSceneDataModel.GameModeButtons)
            {
                if (modeButton != null && modeButton.GameModeRecordId == gameModeRecordId)
                {
                    return modeButton;
                }
            }
            return null;
        }

        private void CancelPendingMercenariesEntry()
        {
            if (m_pendingMercenariesEntryCoroutine != null && m_pendingMercenariesEntryOwner != null)
            {
                m_pendingMercenariesEntryOwner.StopCoroutine(m_pendingMercenariesEntryCoroutine);
            }
            m_pendingMercenariesEntryCoroutine = null;
            m_pendingMercenariesEntryOwner = null;
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
