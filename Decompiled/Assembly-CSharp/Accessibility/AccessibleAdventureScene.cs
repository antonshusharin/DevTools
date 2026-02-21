using Assets;
using System;
using System.Collections.Generic;

namespace Accessibility
{
    class AccessibleAdventureScene : AccessibleScreen
    {
        private enum State { LOADING, CHOOSING_ADVENTURE, CHOOSING_ADVENTURE_MODE, CHOOSING_DECK, CHOOSING_OPPONENT }; // Finding game is a loading state as well

        private State m_curState = State.LOADING;

        private AccessibleMenu m_curMenu;

        private AccessibleHorizontalMenu<AccessibleCollectionDeckBoxVisual> m_chooseDeckMenu;

        private AccessibleHorizontalMenu<AccessiblePracticeAIButton> m_chooseOpponentMenu;

        private AdventureChooserTray m_adventureChooserTray;
        private AdventureData.Adventuresubscene m_curSubScene = AdventureData.Adventuresubscene.INVALID;
        private List<CustomDeckPage> m_customDeckPages;

        private List<PracticeAIButton> m_practiceAIButtons;

        private bool chosenDeck;

        private static AccessibleAdventureScene s_instance = new AccessibleAdventureScene();
        private List<AdventureDef> adventures = new List<AdventureDef>();
        internal static AccessibleAdventureScene Get()
        {
            return s_instance;
        }

        public void ClearAdventureButtons()
        {
            adventures.Clear();
        }

        public void AddAdventureButton(AdventureDef def)
        {
            if (!adventures.Contains(def))
            {
                adventures.Add(def);
            }
        }

        public void OnAdventureSceneShown()
        {
            try
            {
                m_practiceAIButtons = null; // Cleanup in case of unlocked heroes
                m_curState = State.LOADING;

                GameMgr.Get().RegisterFindGameEvent(OnFindGameEvent);

                AccessibilityMgr.SetScreen(this);
            }
            catch (Exception e)
            {
                AccessibilityUtils.LogFatalError(e);
            }
        }

        public void OnSubSceneLoaded(AdventureData.Adventuresubscene subscene)
        {
            AccessibilityUtils.LogDebug($"OnSubSceneLoaded({subscene})");
            m_curState = State.LOADING;
            m_curSubScene = subscene;
            try
            {
                if (subscene == AdventureData.Adventuresubscene.CHOOSER)
                {
                    AccessibilityMgr.SetScreen(this);
                    SetupAndReadChooseAdventureMenu();
                }
                else if (subscene == AdventureData.Adventuresubscene.PRACTICE || subscene == AdventureData.Adventuresubscene.MISSION_DECK_PICKER)
                {
                    m_curState = State.CHOOSING_DECK;

                    AccessibilityMgr.SetScreen(this);
                    //                    AccessibilityMgr.Output(this, LocalizationUtils.Get(LocalizationKey.SCREEN_CHOOSE_ADVENTURE_SCREEN_MENU_PRACTICE_OPTION));
                }
            }
            catch (Exception e)
            {
                AccessibilityUtils.LogFatalError(e);
            }
        }

        private void SetupAndReadChooseAdventureMenu()
        {
            m_curState = State.CHOOSING_ADVENTURE;
            m_curMenu = new AccessibleMenu(this, LocalizationUtils.Get(LocalizationKey.SCREEN_CHOOSE_ADVENTURE_SCREEN_MENU_TITLE), OnGoBackToHub);

            AdventureModeDbId[] modes = GetSupportedAdventureModes();
            AdventureDbId[] supportedAdventures = { AdventureDbId.BOH, AdventureDbId.BOM, AdventureDbId.PRACTICE, AdventureDbId.ROTLK, AdventureDbId.BTP, AdventureDbId.ICC, AdventureDbId.LOOT, AdventureDbId.GIL, AdventureDbId.TRL };
            foreach (AdventureDef def in adventures)
            {
                bool adventureSupported = Array.Exists(supportedAdventures, d => d == def.GetAdventureId()) || HearthstoneAccessConstants.DEV_MODE;
                for (int i = 0; i < modes.Length; i++)
                {
                    AdventureModeDbId mode = modes[i];
                    if (def.GetSubDef(mode) != null && adventureSupported)
                    {
                        m_curMenu.AddOption(def.GetAdventureName() + ": " + def.GetSubDef(mode).GetDescription(), () => ChooseAdventure(def.GetAdventureId(), def.GetSubDef(mode).GetAdventureModeId()));
                    }
                }

            }
            m_curMenu.AddOption(LocalizedText.SCREEN_GO_BACK, OnGoBackToHub);
            m_curMenu.StartReading();
        }

        private static AdventureModeDbId[] GetSupportedAdventureModes()
        {
            AdventureModeDbId[] modes = (AdventureModeDbId[])Enum.GetValues(typeof(AdventureModeDbId));
            var supportedModes = new List<AdventureModeDbId>();

            foreach (AdventureModeDbId mode in modes)
            {
                if (mode != AdventureModeDbId.INVALID)
                {
                    supportedModes.Add(mode);
                }
            }

            return supportedModes.ToArray();
        }


        public void OnAdventureChooserTrayAwake(AdventureChooserTray adventureChooserTray)
        {
            AccessibilityUtils.LogDebug($"OnAdventureChooserTrayAwake({adventureChooserTray})");
            m_adventureChooserTray = adventureChooserTray;
        }

        private void ChooseAdventure(AdventureDbId adventure, AdventureModeDbId mode)
        {
            AdventureConfig adventureConfig = AdventureConfig.Get();
            if (adventureConfig.GetSelectedAdventure() != adventure || adventureConfig.GetSelectedMode() != mode)
            {
                AdventureConfig.Get().SetSelectedAdventureMode(adventure, mode);
            }
            if (m_adventureChooserTray.m_ChooseButton.IsEnabled())
            {
                m_adventureChooserTray.m_ChooseButton.TriggerRelease();
            }
            else AccessibilityMgr.Output(this, m_adventureChooserTray.m_ChooseButton.m_newPlayButtonText.Text);
        }

        private void OnGoBackToHub()
        {
            m_adventureChooserTray.m_BackButton.TriggerRelease();
        }

        public void OnPracticePickerTrayDisplayShown(List<PracticeAIButton> practiceAIButtons)
        {
            try
            {
                m_practiceAIButtons = practiceAIButtons;
                SetupChooseOpponentMenu();
            }
            catch (Exception e)
            {
                AccessibilityUtils.LogFatalError(e);
            }
        }

        private void SetupChooseOpponentMenu()
        {
            m_curState = State.CHOOSING_OPPONENT;
            var chooseOpponentMenu = new AccessibleHorizontalMenu<AccessiblePracticeAIButton>(this, LocalizationUtils.Get(LocalizationKey.SCREEN_CHOOSE_OPPONENT_MENU_TITLE), OnGoBackToChooseDeckMenuFromChooseOpponentMenu);

            foreach (var btn in m_practiceAIButtons)
            {
                chooseOpponentMenu.AddOption(new AccessiblePracticeAIButton(this, btn), () => SelectOpponent(btn));
            }

            m_chooseOpponentMenu = chooseOpponentMenu;
            m_chooseOpponentMenu.StartReading();
        }

        private void SetupChooseDeckMenu()
        {
            chosenDeck = false;
            m_curState = State.CHOOSING_DECK;
            m_chooseDeckMenu = new AccessibleHorizontalMenu<AccessibleCollectionDeckBoxVisual>(this, LocalizationUtils.Get(LocalizationKey.SCREEN_CHOOSE_DECK_TITLE), OnGoBackToChooseAdventureFromChooseDeckMenu);
            var deckPickerTrayDisplay = DeckPickerTrayDisplay.Get();
            var selectedDeckId = deckPickerTrayDisplay.GetLastChosenDeckId();
            var selectedDeckIdx = 0;
            if (DeckPickerTrayDisplay.Get().m_collectionButton.IsEnabled()) AccessibilityMgr.Output(this, LocalizationUtils.Format(LocalizationKey.SCREEN_CHOOSE_DECK_COLLECTION_AVAILABLE));
            for (int i = 0, curIdx = 0; i < m_customDeckPages.Count; i++)
            {
                var page = m_customDeckPages[i];
                var pageDecks = page.m_customDecks;

                for (int j = 0; j < pageDecks.Count; j++)
                {
                    var deck = pageDecks[j];
                    var deckId = deck.GetDeckID();

                    if (deckId == -1L && !deck.IsLoanerDeck())
                    {
                        continue;
                    }
                    if (deckId == selectedDeckId)
                    {
                        selectedDeckIdx = curIdx;
                    }

                    var deckPageIdx = i;

                    var accessibleDeck = new AccessibleCollectionDeckBoxVisual(this, deck);

                    m_chooseDeckMenu.AddOption(accessibleDeck, () => SelectDeck(deck), () => ShowDeckPage(deckPageIdx));
                    curIdx++;
                }
            }

            m_chooseDeckMenu.SetIndex(selectedDeckIdx);
            m_chooseDeckMenu.StartReading();
        }

        private void OnGoBackToChooseDeckMenuFromChooseOpponentMenu()
        {
            PracticePickerTrayDisplay.Get().m_backButton.TriggerRelease();
            SetupChooseDeckMenu();
        }

        private void OnGoBackToChooseAdventureFromChooseDeckMenu()
        {
            DeckPickerTrayDisplay.Get().m_backButton.TriggerRelease();
        }

        private void SelectOpponent(PracticeAIButton btn)
        {
            btn.TriggerRelease();
            PracticePickerTrayDisplay.Get().m_playButton.TriggerRelease();
        }

        public void HandleInput()
        {
            if (m_curState == State.CHOOSING_DECK)
            {
                m_chooseDeckMenu?.HandleAccessibleInput();
                if (DeckPickerTrayDisplay.Get().m_collectionButton.IsEnabled())
                {
                    if (AccessibleKey.HUB_MY_COLLECTION.IsPressed()) DeckPickerTrayDisplay.Get().m_collectionButton.TriggerRelease();
                }
            }
            else if (m_curState == State.CHOOSING_OPPONENT)
            {
                m_chooseOpponentMenu?.HandleAccessibleInput();
            }
            else if (m_curState != State.LOADING)
            {
                m_curMenu?.HandleAccessibleInput();
            }
        }

        public string GetHelp()
        {
            if (m_curState == State.CHOOSING_DECK)
            {
                return m_chooseDeckMenu?.GetHelp();
            }
            else if (m_curState == State.CHOOSING_OPPONENT)
            {
                return m_chooseOpponentMenu?.GetHelp();
            }
            else if (m_curState != State.LOADING)
            {
                return m_curMenu?.GetHelp();
            }

            return "";
        }

        public void OnDeckPickerTrayDisplayReady(List<CustomDeckPage> pages)
        {
            try
            {
                AccessibilityMgr.SetScreen(this);
                m_customDeckPages = pages;
                m_practiceAIButtons = null; // Cleanup in case we go out of adventure and in again
                SetupChooseDeckMenu();
                m_curState = State.CHOOSING_DECK;
            }
            catch (Exception e)
            {
                AccessibilityUtils.LogFatalError(e);
            }
        }

        private void SelectDeck(CollectionDeckBoxVisual deck)
        {
            chosenDeck = true;
            deck.TriggerRelease();
            if (m_curSubScene == AdventureData.Adventuresubscene.MISSION_DECK_PICKER) DeckPickerTrayDisplay.Get().m_playButton.TriggerRelease();
        }

        public void OnSelectedDeck(CollectionDeckBoxVisual deck)
        {
            if (AccessibilityMgr.IsAccessibilityEnabled() && chosenDeck)
            {
                DeckPickerTrayDisplay.Get().m_playButton.TriggerRelease();
            }
        }

        private void ShowDeckPage(int pageIndex)
        {
            var deckPickerTrayDisplay = DeckPickerTrayDisplay.Get();

            if (deckPickerTrayDisplay.GetCurrentPageIndex() != pageIndex)
            {
                deckPickerTrayDisplay.ShowPage(pageIndex);
            }
        }

        private bool OnFindGameEvent(FindGameEventData eventData, object userData)
        {
            m_curState = State.LOADING;

            return false;
        }

        public void OnGainedFocus()
        {
            if (m_curState == State.LOADING)
            {
                return;
            }

            if (m_curState == State.CHOOSING_DECK)
            {
                m_chooseDeckMenu?.StartReading();
            }
            else if (m_curState == State.CHOOSING_OPPONENT)
            {
                m_chooseOpponentMenu?.StartReading();
            }
            else
            {
                m_curMenu?.StartReading();
            }
        }
    }
}
