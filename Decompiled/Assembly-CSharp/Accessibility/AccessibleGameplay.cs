using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace Accessibility
{
    class AccessibleGameplay : AccessibleScreen
    {
        protected enum AccessibleGamePhase
        {
            WAITING_FOR_GAME_TO_START,
            MULLIGAN,
            WAITING_FOR_OPPONENT_MULLIGAN,
            PLAYING,
            GAME_OVER
        }

        protected enum AccessibleGameState
        {
            UNKNOWN,
            WAITING,
            OPPONENT_TURN,
            MAIN_OPTION_MODE,
            SUB_OPTION_MODE,
            TARGET_MODE,
            CHOICE_MODE,
            CHOICE_MODE_CHOICES_HIDDEN,
            SUMMONING_MINION,
            PLAYING_CARD,
            TRADING_CARD,
            CONFIRMING_END_TURN,
            BROWSING_HISTORY,
            ALL_MINIONS_TO_FACE,
            MINION_TO_FACE,

            // Battlegrounds-only
            BUYING_CARD,
            SELLING_MINION,
            MOVING_MINION,
            PASSING_CARD,
            VIEWING_TEAMMATES_BOARD,
            VIEWING_TEAMMATES_CHOICES,
            TEAMMATES_CHOICES_HIDDEN,
            READING_LEADERBOARD,
        }

        private static AccessibleGameplay s_instance;

        protected AccessibleGamePhase m_curPhase;

        protected AccessibleGameState m_curState;
        private bool IsInBeginningChooseOne = false;//used to allow player to pick options after their mulligan.

        protected AccessibleGameState m_prevState;

        protected GameState.ResponseMode m_curResponseMode;
        protected GameState.ResponseMode m_prevResponseMode;

        protected IZone m_curZone;

        protected AccessibleCard m_cardBeingRead;

        protected Card m_heldCard;

        protected bool m_playerTurn;

        private bool m_confirmingEndTurn;

        private bool m_sendingAllMinionsToFace;
        private Card m_curFaceAttacker;
        private bool m_sendingMinionToFace;
        private int m_minionAttackState;

        private float m_nextAction = 0; // Needed to circumvent a lot of frame-related issues that would arise otherwise

        // Choice mode
        protected AccessibleListOfItems<AccessibleCard> m_accessibleChoiceCards;

        protected bool m_waitingForChoiceConfirmation; // Multi-step choices (currently just BG trinkets)

        // Mulligan
        protected AccessibleListOfItems<AccessibleCard> m_accessibleMulliganCards;
        private Dictionary<AccessibleCard, bool> m_mulliganMarkedForReplacement;
        protected NormalButton m_mulliganConfirmButton;
        private bool m_waitingForMulliganReplacementCards;

        protected bool m_justReconnected;

        private bool m_tradingCard;
        private bool m_tradingCardWaitingForHold; // Used to prevent "Summon?" or "Play?" when a card is traded straight from hand but we still need to wait for it to be held

        protected bool m_forceAnnounceChooseTarget; // Needed so that cards with multiple targets can correctly inform the player.

        // New tutorial stuff
        private bool m_disableManaCounters;
        private bool m_disableCorpseCounters;

        private struct ShrineStateSnapshot
        {
            internal bool Exists;
            internal int EntityId;
            internal string Name;
            internal bool Dormant;
            internal string BannerText;
        }

        private bool m_shrineStateInitialized;
        private ShrineStateSnapshot m_friendlyShrineState;
        private ShrineStateSnapshot m_opponentShrineState;

        internal static AccessibleGameplay Get()
        {
            if (s_instance == null)
            {
                InitInstance();
            }

            return s_instance;
        }

        private static void InitInstance()
        {
            if (AccessibleGameplayUtils.IsFindingOrPlayingBattlegrounds())
            {
                s_instance = new AccessibleBattlegroundsGameplay();
            }
            else
            {
                s_instance = new AccessibleGameplay();
            }
        }

        public void OnTurnStart()
        {
            try
            {
                AccessiblePowerTaskListDescriber.Get().OnTurnStart(!m_disableManaCounters);
            }
            catch (Exception e)
            {
                AccessibilityUtils.LogFatalError(e);
            }
        }

        public void OnCoinResult(bool friendlyPlayerGoesFirst)
        {
            if (friendlyPlayerGoesFirst)
            {
                AccessibilityMgr.Output(this, LocalizationUtils.Get(LocalizationKey.GAMEPLAY_YOU_GO_FIRST));
            }
            else
            {
                AccessibilityMgr.Output(this, LocalizationUtils.Get(LocalizationKey.GAMEPLAY_OPPONENT_GOES_FIRST));
            }
        }

        public void OnStartingHand(List<Card> cards)
        {
            try
            {
                if (AccessibilityUtils.IsInPvPGame())
                {
                    var opponent = GameState.Get().GetOpposingSidePlayer();
                    var opponentName = opponent.GetName();
                    var opponentClass = opponent.GetHero().GetClass();
                    var opponentClassName = GameStrings.GetClassName(opponentClass);
                    AccessibilityMgr.Output(this, LocalizationUtils.Format(LocalizationKey.GAMEPLAY_VS_PLAYER_ANNOUNCEMENT, opponentName, opponentClassName));
                    AccessibleHistoryMgr.Get().AddEntry(LocalizationUtils.Format(LocalizationKey.GAMEPLAY_VS_PLAYER_ANNOUNCEMENT, opponentName, opponentClassName));
                }
            }
            catch (Exception e)
            {
                AccessibilityUtils.LogFatalError(e);
            }

            AccessibilityMgr.Output(this, LocalizationUtils.Format(LocalizationKey.GAMEPLAY_YOU_START_WITH_N_CARDS, cards.Count));
        }

        public virtual void EndMulligan()
        {
            try
            {
                if (m_accessibleMulliganCards != null)
                {
                    var originalCards = new HashSet<Card>(m_accessibleMulliganCards.Count);
                    m_accessibleMulliganCards.Items.ForEach(c => originalCards.Add(c.GetCard()));
                    var cardsAfterMulligan = GameState.Get().GetFriendlySidePlayer().GetHandZone().GetCards();
                    var newCards = new List<Card>();
                    var droppedCards = new List<Card>();

                    foreach (var card in originalCards)
                    {
                        if (!cardsAfterMulligan.Contains(card))
                        {
                            droppedCards.Add(card);
                        }
                    }

                    foreach (var card in cardsAfterMulligan)
                    {
                        if (!originalCards.Contains(card) && !IsCoinCard(card.GetEntity()))
                        {
                            newCards.Add(card);
                        }
                    }

                    if (droppedCards.Count > 0)
                    {
                        var droppedNames = AccessibleSpeechUtils.GetNames(droppedCards);
                        var newNames = AccessibleSpeechUtils.GetNames(newCards);
AccessibleHistoryMgr.Get().AddEntry(LocalizationUtils.Format(LocalizationKey.GAMEPLAY_PLAYER_DISCARDED_CARDS, droppedNames));
                        AccessibilityMgr.Output(this, LocalizationUtils.Format(LocalizationKey.GAMEPLAY_PLAYER_DREW_CARDS, newNames));
                        AccessibleHistoryMgr.Get().AddEntry(LocalizationUtils.Format(LocalizationKey.GAMEPLAY_PLAYER_DREW_CARDS, newNames));
                    }
                }
            }
            catch (Exception e)
            {
                AccessibilityUtils.LogFatalError(e);
            }

            TransitionFromMulliganToGame();
        }

        protected void TransitionFromMulliganToGame()
        {
            m_accessibleMulliganCards = null;
            m_waitingForMulliganReplacementCards = false;

            OnGameStart();
        }

        private bool IsCoinCard(Entity entity)
        {
            return CosmeticCoinManager.Get()?.IsCoinCard(entity.GetCardId()) ?? false;
        }

        public void OnMulliganCardsDealt(List<Card> cards)
        {
        }

        public void OnGameplayScreenStart()
        {
            Reset();

            InitInstance();
            s_instance.Reset();

            AccessibilityMgr.SetScreen(s_instance);
        }

        protected virtual void Reset()
        {
            m_curPhase = AccessibleGamePhase.WAITING_FOR_GAME_TO_START;
            m_curState = AccessibleGameState.UNKNOWN;
            m_prevState = AccessibleGameState.UNKNOWN;
            m_curResponseMode = GameState.ResponseMode.NONE;
            m_prevResponseMode = GameState.ResponseMode.NONE;
            m_curZone = null;
            m_cardBeingRead = null;
            m_heldCard = null;
            m_playerTurn = false;
            m_confirmingEndTurn = false;
            m_sendingAllMinionsToFace = false;
            m_curFaceAttacker = null;
            m_sendingMinionToFace = false;
            m_minionAttackState = 0;
            m_nextAction = 0;
            m_accessibleChoiceCards = null;
            m_zoneSelectedListeners.Clear();
            m_cardSelectedListeners.Clear();
            m_summoningMinionListeners.Clear();
            m_stopHidingMouse = false;
            m_waitingForMulliganReplacementCards = false;
            m_tradingCard = false;
            m_tradingCardWaitingForHold = false;
            m_forceAnnounceChooseTarget = false;
            m_disableManaCounters = false;
            m_disableCorpseCounters = false;
            AccessibleHistoryMgr.Get().Reset();
        }

        public void DisableManaCounters()
        {
            m_disableManaCounters = true;
        }

        public void DisableCorpseCounters()
        {
            m_disableCorpseCounters = true;
        }

        public void OnCoinCard()
        {
            AccessibilityMgr.Output(this, LocalizationUtils.Get(LocalizationKey.GAMEPLAY_YOU_GET_THE_COIN));
        }

        public virtual void WaitingForOpponentToFinishMulligan()
        {
            AccessibilityMgr.Output(this, LocalizationUtils.Get(LocalizationKey.GAMEPLAY_WAITING_FOR_OPPONENT));
            m_curPhase = AccessibleGamePhase.WAITING_FOR_OPPONENT_MULLIGAN;
        }

        public void OnDrawCard(Card card)
        {
        }

        private void SetCardBeingRead(Card card, bool forceZoneRead)
        {
            m_cardBeingRead = AccessibleCard.CreateCard(this, card);
            var prevZone = m_curZone;
            m_curZone = card.GetAccessibleZone();
            ReadZoneChangeIfNecessary(card, prevZone, m_curZone, forceZoneRead);
        }

        protected void ReadZoneChangeIfNecessary(Card card, IZone fromZone, IZone toZone, bool forceZoneRead)
        {
            if (fromZone == toZone && !forceZoneRead)
            {
                return;
            }

            var player = GameState.Get().GetFriendlySidePlayer();
            var opponent = GameState.Get().GetOpposingSidePlayer();

            if (card.GetEntity().IsHeroPower() && card.GetEntity().IsControlledByFriendlySidePlayer())
            {
                AccessibilityMgr.Output(this, LocalizationUtils.Get(LocalizationKey.GAMEPLAY_ZONE_PLAYER_HERO_POWER));
            }
            else if (card.GetEntity().IsHeroPower() && card.GetEntity().IsControlledByOpposingSidePlayer())
            {
                AccessibilityMgr.Output(this, LocalizationUtils.Get(LocalizationKey.GAMEPLAY_ZONE_OPPONENT_HERO_POWER));
            }
            else if (card == player.GetWeaponCard())
            {
                AccessibilityMgr.Output(this, LocalizationUtils.Get(LocalizationKey.GAMEPLAY_ZONE_PLAYER_WEAPON));
            }
            else if (card == opponent.GetWeaponCard())
            {
                AccessibilityMgr.Output(this, LocalizationUtils.Get(LocalizationKey.GAMEPLAY_ZONE_OPPONENT_WEAPON));
            }
            else if (toZone == GetPlayerHand())
            {
                AccessibilityMgr.Output(this, LocalizationUtils.Get(LocalizationKey.GAMEPLAY_ZONE_PLAYER_HAND));
            }
            else if (toZone == opponent.GetHandZone())
            {
                AccessibilityMgr.Output(this, LocalizationUtils.Get(LocalizationKey.GAMEPLAY_ZONE_OPPONENT_HAND)); // Not needed atm
            }
            else if (toZone == GetPlayerMinions())
            {
                AccessibilityMgr.Output(this, LocalizationUtils.Get(LocalizationKey.GAMEPLAY_ZONE_PLAYER_MINIONS));
            }
            else if (toZone == GetOpponentMinions())
            {
                ReadOpponentZoneName();
            }
            else if (toZone == GetPlayerSecrets())
            {
                AccessibilityMgr.Output(this, LocalizationUtils.Get(LocalizationKey.GAMEPLAY_ZONE_PLAYER_SECRETS));
            }
            else if (toZone == GetOpponentSecrets())
            {
                AccessibilityMgr.Output(this, LocalizationUtils.Get(LocalizationKey.GAMEPLAY_ZONE_OPPONENT_SECRETS)); // Not needed atm
            }
            else if (card.GetEntity().IsBattlegroundTrinket())
            {
                AccessibilityMgr.Output(this, LocalizationUtils.Get(card.GetEntity().IsControlledByFriendlySidePlayer() ? LocalizationKey.BATTLEGROUNDS_GAMEPLAY_ZONE_PLAYER_TRINKETS : LocalizationKey.BATTLEGROUNDS_GAMEPLAY_ZONE_OPPONENT_TRINKETS));
            }
        }

        protected virtual void ReadOpponentZoneName()
        {
            AccessibilityMgr.Output(this, LocalizationUtils.Get(LocalizationKey.GAMEPLAY_ZONE_OPPONENT_MINIONS));
        }

        public virtual void HandleInput()
        {
            if (GameState.Get() == null || InputManager.Get() == null)
            {
                // Game hasn't even started yet
                return;
            }

            if (GameState.Get().IsMulliganPhase() && !IsInBeginningChooseOne)
            {
                m_curPhase = AccessibleGamePhase.MULLIGAN;
            }
            else if (GameState.Get().IsGameOver())
            {
                m_curPhase = AccessibleGamePhase.GAME_OVER;
            }
            else if (GameState.Get().IsMulliganPhasePending())
            {
                m_curPhase = AccessibleGamePhase.WAITING_FOR_GAME_TO_START;
            }
            else if (GameState.Get().IsGameCreated())
            {
                m_curPhase = AccessibleGamePhase.PLAYING;
            }
            else
            {
                m_curPhase = AccessibleGamePhase.WAITING_FOR_GAME_TO_START;
            }

            if (AccessibleKey.READ_TOOLTIP.IsPressed())
            {
                HandleTooltipReading();
            }

            switch (m_curPhase)
            {
                case AccessibleGamePhase.PLAYING:
                    HandleInGameInput();
                    break;
                case AccessibleGamePhase.MULLIGAN:
                    HandleMulliganInput();
                    break;
                default:
                    break;
            }
        }

        private void HandleMulliganInput()
        {
            if (m_accessibleMulliganCards == null)
            {
                return; // yield
            }

            if (m_justReconnected)
            {
                m_justReconnected = false;
            }

            HandleReadAnomaliesKey();

            if (AccessibleKey.MULLIGAN_MARK_CARD.IsPressed())
            {
                var focusedCard = m_accessibleMulliganCards.GetItemBeingRead();
                AccessibleInputMgr.MoveMouseTo(focusedCard.GetCard());
                AccessibleInputMgr.ClickLeftMouseButton();
                m_mulliganMarkedForReplacement[focusedCard] = !m_mulliganMarkedForReplacement[focusedCard];

                if (m_mulliganMarkedForReplacement[focusedCard])
                {
                    AccessibilityMgr.Output(this, LocalizationUtils.Get(LocalizationKey.GAMEPLAY_MULLIGAN_WILL_BE_REPLACED));
                }
                else
                {
                    AccessibilityMgr.Output(this, LocalizationUtils.Get(LocalizationKey.GAMEPLAY_MULLIGAN_WILL_NOT_BE_REPLACED));
                }
            }
            else if (AccessibleKey.CONFIRM.IsPressed())
            {
                m_mulliganConfirmButton.TriggerRelease();

                // Prevent flicker while the game starts and new cards are drawn
                m_waitingForMulliganReplacementCards = true;
                HideMouse();
            }
            else if (!m_waitingForMulliganReplacementCards)
            {
                m_accessibleMulliganCards.HandleAccessibleInput();
                MoveMouseToCard(m_accessibleMulliganCards.GetItemBeingRead().GetCard());
            }
        }

        private void HandleInGameInput()
        {
            if (m_justReconnected)
            {
                m_justReconnected = false;

                if (GameState.Get().IsFriendlySidePlayerTurn())
                {
                    AccessibilityMgr.Output(this, LocalizedText.GAMEPLAY_YOUR_TURN);
                }
                else
                {
                    AccessibilityMgr.Output(this, LocalizedText.GAMEPLAY_OPPONENT_TURN);
                }

                GameState.Get().RegisterGameOverListener(OnGameOver);
                AccessiblePowerTaskListDescriber.Get().OnReconnected();
            }

            UpdateState();
            UpdateShrineAnnouncements();

            if (m_curState == AccessibleGameState.ALL_MINIONS_TO_FACE)
            {
                HandleAllMinionsToFace();
                return;
            }
            else if (m_curState == AccessibleGameState.MINION_TO_FACE)
            {
                HandleMinionToFace();
                return;
            }
            else if (m_curState == AccessibleGameState.TRADING_CARD)
            {
                HandleTradingCard();
                return;
            }

            try
            {
                UpdateMousePosition();
            }
            catch (Exception e)
            {
                AccessibilityUtils.LogFatalError(e);
                StopReadingCard();
            }

            if (m_curState == AccessibleGameState.CONFIRMING_END_TURN)
            {
                if (AccessibleKey.CONFIRM.IsPressed() || AccessibleKey.END_TURN.IsPressed())
                {
                    EndTurn();
                    return;
                }
                else if (Input.anyKeyDown)
                {
                    m_confirmingEndTurn = false;
                    UpdateState();
                }
            }

            if ((m_curState == AccessibleGameState.OPPONENT_TURN || m_curState == AccessibleGameState.MAIN_OPTION_MODE) && HandleEmotes())
            {
                return;
            }

            switch (m_curState)
            {
                case AccessibleGameState.WAITING:
                case AccessibleGameState.UNKNOWN:
                case AccessibleGameState.OPPONENT_TURN:
                    HandleCardReadingInput();
                    HandleCheckStatusKeys();
                    HandleZoneInput();
                    HandleValidOptionsSelectionInput();
                    HandleZoneSelection();
                    HandleHistoryInput();
                    return;
                case AccessibleGameState.MAIN_OPTION_MODE:
                    HandleCardReadingInput();
                    HandleCheckStatusKeys();
                    HandleMainOptionMode();
                    HandleEndTurnInput();
                    HandleHistoryInput();
                    return;
                case AccessibleGameState.SUB_OPTION_MODE:
                    HandleCheckStatusKeys();
                    HandleSubOptionMode();
                    return;
                case AccessibleGameState.CHOICE_MODE:
                    HandleCheckStatusKeys();
                    bool canHandleChoiceMode=HandleChoiceMode();
                    if(!canHandleChoiceMode) HandleTargetMode();
                    return;
                    case AccessibleGameState.CHOICE_MODE_CHOICES_HIDDEN:
                    HandleCheckStatusKeys();
                    HandleZoneSelection();
                    HandleZoneInput();
                    HandleCardReadingInput();
                    if (AccessibleKey.READ_NEXT_VALID_ITEM.IsPressed())
                    {
                        ChoiceCardMgr.Get().GetToggleButton().TriggerRelease();
                    }
                    return;
                case AccessibleGameState.TARGET_MODE:
                    HandleCheckStatusKeys();
                    HandleTargetMode();
                    return;
                case AccessibleGameState.SUMMONING_MINION:
                    HandleCardReadingInput();
                    HandleCheckStatusKeys();
                    HandleSummoningMinion();
                    HandleTradeCardWhenHoldingCardInput();
                    return;
                case AccessibleGameState.PLAYING_CARD:
                    //HandleCardReadingInput();
                    HandleCheckStatusKeys();
                    HandlePlayingCard();
                    HandleTradeCardWhenHoldingCardInput();
                    return;
                case AccessibleGameState.BROWSING_HISTORY:
                    HandleHistoryInput();
                    return;
                default:
                    return;
            }
        }

        protected void HandleTooltipReading()
        {
            AccessibilityUtils.ReadTooltip(this);
        }

        private void HandleAllMinionsToFace()
        {
            if (Time.time < m_nextAction)
            {
                return;
            }

            if (m_curFaceAttacker != null)
            {
                ClickCard(GameState.Get().GetOpposingSidePlayer().GetHeroCard());
                m_curFaceAttacker = null;
                SetNextAction();
            }
            else
            {
                List<Card> remainingAttackers = GetValidFaceAttackers();

                if (remainingAttackers.Count > 0)
                {
                    m_curFaceAttacker = remainingAttackers[0];
                    ClickCard(m_curFaceAttacker);
                    SetNextAction();
                }
                else
                {
                    m_sendingAllMinionsToFace = false;
                }
            }
        }

        private void HandleMinionToFace()
        {
            if (Time.time < m_nextAction)
            {
                return;
            }

            if (m_minionAttackState == 0)
            {
                ClickCard(m_cardBeingRead.GetCard());
                m_minionAttackState = 1;
                SetNextAction(0.1f);
            }
            else if (m_minionAttackState == 1)
            {
                ClickCard(GameState.Get().GetOpposingSidePlayer().GetHeroCard());
                m_minionAttackState = 2;
            }
            else
            {
                m_minionAttackState = 0;
                m_sendingMinionToFace = false;
            }
        }

        private void SetNextAction(float delay = 0f)
        {
            if(delay==0f) delay=Options.Get().GetFloat(Option.ACCESSIBILITY_AUTO_ATTACK_SPEED);
            if(delay==0f) delay=1.0f;
            m_nextAction = Time.time + delay;
        }

        private void ClickCard(Card card)
        {
            MoveMouseToCard(card);
            AccessibleInputMgr.ClickLeftMouseButton();
        }

        private void UpdateMousePosition()
        {
            if (m_heldCard != null)
            {
                return;
            }

            if (m_cardBeingRead != null)
            {
                MoveMouseToCard(m_cardBeingRead.GetCard());
            }
            else
            {
                HideMouse();
            }
        }

        private void HideMouse()
        {
            if (!m_stopHidingMouse)
            {
                AccessibleInputMgr.HideMouse();
            }
        }

        private void HandleTradeCardWhenHoldingCardInput()
        {
            if (AccessibleKey.TRADE_CARD.IsPressed() && CanTradeCard(m_heldCard))
            {
                QueryTradeOrForgeCard(m_heldCard);
            }
        }

        private void QueryTradeOrForgeCard(Card card)
        {
            m_tradingCard = true;
            var key = card.GetEntity().IsTradeable() ? LocalizationKey.GAMEPLAY_QUERY_TRADE_CARD : LocalizationKey.GAMEPLAY_QUERY_FORGE_CARD;
            AccessibilityMgr.Output(this, LocalizationUtils.Get(key));
        }

        private bool CanTradeCard(Card card)
        {
            if (card == null || card.GetAccessibleZone() != GameState.Get().GetFriendlySidePlayer().GetHandZone())
            {
                return false;
            }

            return card.GetEntity().IsTradeable() || card.GetEntity().IsForgeable();
        }

        private int m_summonPos;

        protected void HandleSummoningMinion()
        {
            var playerMinions = GameState.Get().GetFriendlySidePlayer().GetBattlefieldZone();

            if (playerMinions.GetCardCount() == 0)
            {
                if (m_prevState != AccessibleGameState.SUMMONING_MINION)
                {
                    AccessibilityMgr.Output(this, LocalizationUtils.Get(LocalizationKey.GAMEPLAY_QUERY_SUMMON_MINION));
                    OnSummoningMinion(m_heldCard);
                }

                MoveMouseToZone(GameState.Get().GetFriendlySidePlayer().GetBattlefieldZone());
            }
            else
            {
                if (m_prevState != AccessibleGameState.SUMMONING_MINION)
                {
                    m_summonPos = playerMinions.GetLastPos();
                    QuerySummonPosition(0);
                }

                HandleSummoningPositionInput();

                if (m_summonPos == playerMinions.GetLastPos())
                {
                    MoveMouseToRightOfZone(playerMinions);
                }
                else if (m_summonPos == 1)
                {
                    MoveMouseToLeftOfZone(playerMinions);
                }
                else
                {
                    var prevMinion = playerMinions.GetCardAtSlot(m_summonPos - 1);
                    var nextMinion = playerMinions.GetCardAtSlot(m_summonPos);
                    var pos = prevMinion.transform.position + (nextMinion.transform.position - prevMinion.transform.position) / 2;
                    AccessibleInputMgr.MoveMouseToWorldPosition(pos);
                }
            }

            HandleConfirmOrCancel();
        }

        private void HandleSummoningPositionInput()
        {
            if (AccessibleKey.READ_PREV_ITEM.IsPressed())
            {
                QuerySummonPosition(-1);
            }
            else if (AccessibleKey.READ_NEXT_ITEM.IsPressed())
            {
                QuerySummonPosition(1);
            }
            else if (AccessibleKey.READ_FIRST_ITEM.IsPressed())
            {
                m_summonPos = 1;
                QuerySummonPosition(0);
            }
            else if (AccessibleKey.READ_LAST_ITEM.IsPressed())
            {
                var playerMinions = GameState.Get().GetFriendlySidePlayer().GetBattlefieldZone();
                var lastPos = playerMinions.GetLastPos();
                m_summonPos = lastPos;
                QuerySummonPosition(0);
            }
            else
            {
                int? numKeyPressed = AccessibleInputMgr.TryGetPressedNumKey();

                if (numKeyPressed.HasValue)
                {
                    var playerMinions = GameState.Get().GetFriendlySidePlayer().GetBattlefieldZone();
                    var lastPos = playerMinions.GetLastPos();

                    m_summonPos = Math.Min(lastPos, numKeyPressed.Value);
                    QuerySummonPosition(0);
                }
            }
        }

        private void QuerySummonPosition(int inc)
        {
            var prevSummonPos = m_summonPos;
            var playerMinions = GameState.Get().GetFriendlySidePlayer().GetBattlefieldZone();
            var lastPos = playerMinions.GetLastPos();

            m_summonPos += inc;

            if (m_summonPos > lastPos)
            {
                m_summonPos = lastPos;
            }
            else if (m_summonPos < 1)
            {
                m_summonPos = 1;
            }

            if (inc != 0 && prevSummonPos == m_summonPos)
            {
                return;
            }

            if (m_summonPos == lastPos)
            {
                AccessibilityMgr.Output(this, LocalizationUtils.Get(LocalizationKey.GAMEPLAY_QUERY_SUMMON_MINION_AT_THE_RIGHT));
                OnSummoningMinion(m_heldCard);
            }
            else if (m_summonPos == 1)
            {
                AccessibilityMgr.Output(this, LocalizationUtils.Get(LocalizationKey.GAMEPLAY_QUERY_SUMMON_MINION_AT_THE_LEFT));
                OnSummoningMinion(m_heldCard);
            }
            else
            {
                var prevMinion = playerMinions.GetCardAtSlot(m_summonPos - 1);
                var nextMinion = playerMinions.GetCardAtSlot(m_summonPos);
                var prevMinionName = GetPreferredCardName(prevMinion);
                var nextMinionName = GetPreferredCardName(nextMinion);
                AccessibilityMgr.Output(this, LocalizationUtils.Format(LocalizationKey.GAMEPLAY_QUERY_SUMMON_MINION_BETWEEN, prevMinionName, nextMinionName));
                OnSummoningMinion(m_heldCard);
            }
        }

        protected virtual string GetPreferredCardName(Card card)
        {
            return card.GetEntity().GetName();
        }

        protected void HandlePlayingCard()
        {
            if (AccessibleUnityInput.Get().GetMousePosition().y < AccessibleInputMgr.GetMousePosition(GameState.Get().GetFriendlySidePlayer().GetHeroCard()).y)
            {
                MoveMouseToZone(GameState.Get().GetFriendlySidePlayer().GetBattlefieldZone());
            }

            if (GameState.Get().IsInTargetMode())
            {
                HandleTargetMode();
            }
            else
            {
                if (m_prevState != AccessibleGameState.PLAYING_CARD && !RequiresTarget(m_heldCard)) // If a target is required, "choose a target" will already indicate we're using it
                {
                    QueryPlayCard();
                }

                HandleConfirmOrCancel();
            }
        }

        private void HandleTradingCard()
        {
            Collider collider = Board.Get().GetDeckActionArea();
            if (collider != null)
            {
                var cardBounds = m_heldCard.GetActor().GetMeshRenderer().bounds;
                Vector3 tradeAreaCenter = collider.bounds.ClosestPoint(m_heldCard.gameObject.transform.position);
                Vector3 target = tradeAreaCenter;
                target.x += cardBounds.size.x / 2;
                AccessibleInputMgr.MoveMouseToWorldPosition(target);
            }

            HandleConfirmOrCancel();
        }

        private bool RequiresTarget(Card heldCard)
        {
            return GameState.Get().EntityHasTargets(heldCard.GetEntity());
        }

        private void QueryPlayCard()
        {
            AccessibilityMgr.Output(this, LocalizationUtils.Get(LocalizationKey.GAMEPLAY_QUERY_PLAY_CARD));
        }

        private void HandleConfirmOrCancel(bool targetRequired = false)
        {
            if (AccessibleKey.CONFIRM.IsPressed())
            {
                if (!targetRequired || m_cardBeingRead != null)
                {
                    AccessibleInputMgr.ClickLeftMouseButton();
                }
            }
            else if (AccessibleKey.BACK.IsPressed())
            {
                CancelOption();
            }
        }

        private void HandleEndTurnInput()
        {
            if (!GameState.Get().IsInMainOptionMode())
            {
                return;
            }

            if (AccessibleKey.FORCE_END_TURN.IsPressed())
            {
                EndTurn();
            }

            if (AccessibleKey.END_TURN.IsPressed())
            {
                if (EndTurnButton.Get().HasNoMorePlays())
                {
                    EndTurn();
                }
                else
                {
                    if (PlayerHasReadyAttackers() || PlayerHeroCanStillAttack())
                    {
                        AccessibilityMgr.Output(this, LocalizationUtils.Get(LocalizationKey.GAMEPLAY_QUERY_END_TURN_WHEN_CAN_ATK));
                    }
                    else if (PlayerHasReadyLocations())
                    {
                        AccessibilityMgr.Output(this, LocalizationUtils.Get(LocalizationKey.GAMEPLAY_QUERY_END_TURN_WHEN_CAN_USE_LOCATION));
                    }
                    else if (PlayerCanStillUseHeroPower())
                    {
                        AccessibilityMgr.Output(this, LocalizationUtils.Get(LocalizationKey.GAMEPLAY_QUERY_END_TURN_WHEN_CAN_USE_HERO_POWER));
                    }
                    else
                    {
                        AccessibilityMgr.Output(this, LocalizationUtils.Get(LocalizationKey.GAMEPLAY_QUERY_END_TURN_WHEN_VALID_PLAYS));
                    }

                    StopReadingCard();
                    m_confirmingEndTurn = true;
                }
            }
        }

        private bool PlayerHasReadyAttackers()
        {
            List<Card> cards = GameState.Get().GetFriendlySidePlayer().GetBattlefieldZone().GetCards();

            for (int i = 0; i < cards.Count; i++)
            {
                if (cards[i].GetEntity().IsMinion() && GameState.Get().HasResponse(cards[i].GetEntity()))
                {
                    return true;
                }
            }
            return false;
        }

        private bool PlayerHeroCanStillAttack()
        {
            var player = GameState.Get().GetFriendlySidePlayer();

            if (GameState.Get().HasResponse(player.GetHero()))
            {
                return true;
            }
            return false;
        }

        private bool PlayerHasReadyLocations()
        {
            List<Card> cards = GameState.Get().GetFriendlySidePlayer().GetBattlefieldZone().GetCards();

            for (int i = 0; i < cards.Count; i++)
            {
                if (cards[i].GetEntity().IsLocation() && GameState.Get().HasResponse(cards[i].GetEntity()))
                {
                    return true;
                }
            }
            return false;
        }

        private bool PlayerCanStillUseHeroPower()
        {
            var player = GameState.Get().GetFriendlySidePlayer();
            var heroPower = player.GetHeroPower();

            if (heroPower == null)
            {
                return false;
            }

            return GameState.Get().HasResponse(heroPower);
        }

        private void HandleMainOptionMode()
        {
            if (AccessibleKey.SEND_ALL_MINIONS_TO_FACE.IsPressed())
            {
                SendAllMinionsToFace();
                return;
            }
            else if (AccessibleKey.SEND_MINION_TO_FACE.IsPressed())
            {
                SendMinionToFace();
                return;
            }

            HandleZoneInput();
            HandleValidOptionsSelectionInput();
            HandleZoneSelection();

            if (AccessibleKey.CONFIRM.IsPressed())
            {
                ClickCard();
            }
            else if (AccessibleKey.TRADE_CARD.IsPressed() && m_cardBeingRead != null && CanTradeCard(m_cardBeingRead.GetCard()))
            {
                QueryTradeOrForgeCard(m_cardBeingRead.GetCard());
                ClickCard(true);
            }
        }

        private void SendAllMinionsToFace()
        {
            if (GameState.Get().GetFriendlySidePlayer().GetBattlefieldZone().GetCardCount() == 0)
            {
                AccessibilityMgr.Output(this, LocalizationUtils.Get(LocalizationKey.GAMEPLAY_SEND_TO_FACE_NO_MINIONS));
                return;
            }

            if (!GameState.Get().GetOpposingSidePlayer().GetHero().CanBeAttacked())
            {
                AccessibilityMgr.Output(this, LocalizationUtils.Get(LocalizationKey.GAMEPLAY_SEND_TO_FACE_HERO_CANT_BE_ATTACKED));
                return;
            }

            if (!GameState.Get().GetOpposingSidePlayer().GetHero().CanBeTargetedByOpponents())
            {
                AccessibilityMgr.Output(this, LocalizationUtils.Get(LocalizationKey.GAMEPLAY_SEND_TO_FACE_HERO_CANT_BE_TARGETED));
                return;
            }

            List<Card> validAttackers = GetValidFaceAttackers();

            if (validAttackers.Count == 0)
            {
                AccessibilityMgr.Output(this, LocalizationUtils.Get(LocalizationKey.GAMEPLAY_SEND_TO_FACE_HERO_NO_VALID_ATTACKERS));
            }
            else
            {
                m_sendingAllMinionsToFace = true;
            }
        }

        private void SendMinionToFace()
        {
            if (m_cardBeingRead == null)
            {
                return;
            }

            if (m_cardBeingRead.GetCard().GetAccessibleZone() != GameState.Get().GetFriendlySidePlayer().GetBattlefieldZone())
            {
                AccessibilityMgr.Output(this, LocalizationUtils.Get(LocalizationKey.GAMEPLAY_SEND_TO_FACE_NOT_FRIENDLY_MINION));
                return;
            }

            if (!m_cardBeingRead.GetCard().GetEntity().IsMinion())
            {
                AccessibilityMgr.Output(this, LocalizationUtils.Get(LocalizationKey.GAMEPLAY_SEND_TO_FACE_NOT_MINION));
                return;
            }

            if (!IsValidFaceAttacker(m_cardBeingRead.GetCard()))
            {
                AccessibilityMgr.Output(this, LocalizationUtils.Get(LocalizationKey.GAMEPLAY_SEND_TO_FACE_NOT_VALID_ATTACKER));
                return;
            }

            if (!GameState.Get().GetOpposingSidePlayer().GetHero().CanBeAttacked())
            {
                AccessibilityMgr.Output(this, LocalizationUtils.Get(LocalizationKey.GAMEPLAY_SEND_TO_FACE_HERO_CANT_BE_ATTACKED));
                return;
            }

            if (!GameState.Get().GetOpposingSidePlayer().GetHero().CanBeTargetedByOpponents())
            {
                AccessibilityMgr.Output(this, LocalizationUtils.Get(LocalizationKey.GAMEPLAY_SEND_TO_FACE_HERO_CANT_BE_TARGETED));
                return;
            }

            m_sendingMinionToFace = true;
        }

        public void OnReconnected()
        {
            AccessibilityMgr.InterruptTexts();
            AccessibilityMgr.Output(this, LocalizedText.GLOBAL_RECONNECTED);

            m_justReconnected = true;
        }

        private List<Card> GetValidFaceAttackers()
        {
            List<Card> ret = new List<Card>();
            List<Entity> faceTargetters = GetOptionsWithTarget(GameState.Get().GetOpposingSidePlayer().GetHero());

            foreach (var entity in faceTargetters)
            {
                if (entity.IsMinion() && !entity.HasUsableTitanAbilities() && entity.GetCard()?.GetAccessibleZone() == GameState.Get().GetFriendlySidePlayer().GetBattlefieldZone())
                {
                    ret.Add(entity.GetCard());
                }
            }

            return ret;
        }

        private List<Entity> GetOptionsWithTarget(Entity target)
        {
            List<Entity> ret = new List<Entity>();
            Network.Options optionsPacket = GameState.Get().GetOptionsPacket();

            if (optionsPacket == null)
            {
                return ret;
            }

            for (int i = 0; i < optionsPacket.List.Count; i++)
            {
                Network.Options.Option option = optionsPacket.List[i];
                if (option.Type == Network.Options.Option.OptionType.POWER)
                {
                    if (option.Main.IsValidTarget(target.GetEntityId()))
                    {
                        ret.Add(GameState.Get().GetEntity(option.Main.ID));
                    }
                }
            }

            return ret;
        }

        private bool IsValidFaceAttacker(Card minion)
        {
            return GetValidFaceAttackers().Contains(minion);
        }

        private void EndTurn()
        {
            InputManager.Get().DoEndTurnButton();
            StopReadingCard();
        }

        protected void HandleSubOptionMode()
        {
            HandleChoiceMode();
        }

        protected bool HandleChoiceMode()
        {
            if (IsTargetChoice() || m_accessibleChoiceCards == null || !m_accessibleChoiceCards.IsReading())
            {
                return false; // yield
            }
            try
            {
                m_cardBeingRead = m_accessibleChoiceCards.GetItemBeingRead();
            }
            catch (Exception e)
            {
                AccessibilityUtils.LogFatalError(e);
            }

            if (HandleChoiceConfirmation())
            {
                return true;
            }

            if (AccessibleKey.CONFIRM.IsPressed())
            {
                AccessibleInputMgr.MoveMouseTo(m_cardBeingRead.GetCard());
                AccessibleInputMgr.ClickLeftMouseButton();
            }
            else if (AccessibleKey.READ_NEXT_VALID_ITEM.IsPressed() && m_curState == AccessibleGameState.CHOICE_MODE)
            {
                ChoiceCardMgr.Get().GetToggleButton().TriggerRelease();
            }
            else
            {
                m_accessibleChoiceCards.HandleAccessibleInput();
            }
            return true;
        }

        private bool HandleChoiceConfirmation()
        {
            if (!m_waitingForChoiceConfirmation)
            {
                return false;
            }
            if (AccessibleKey.CONFIRM.IsPressed())
            {
                ChoiceCardMgr.Get().GetConfirmButton().TriggerRelease();
            }
            else if (AccessibleKey.BACK.IsPressed())
            {
                m_waitingForChoiceConfirmation = false;
                m_accessibleChoiceCards.StartReading();
            }
            return true;
        }

        protected bool IsTargetChoice()
        {
            return GameState.Get().GetFriendlyEntityChoices()?.ChoiceType == CHOICE_TYPE.TARGET;
        }

        public void OnChoicesHidden()
        {
            AccessibilityMgr.Output(this, LocalizationUtils.Get(LocalizationKey.GAMEPLAY_CHOICES_HIDDEN));
            StopReadingCard();
            m_waitingForChoiceConfirmation = false;
        }

        public void OnWaitingForChoiceConfirmation()
        {
            m_waitingForChoiceConfirmation = true;
            AccessibilityMgr.Output(this, LocalizedText.GLOBAL_PRESS_ENTER_TO_CONFIRM_OR_BACKSPACE_TO_CANCEL);
        }

        protected void HandleTargetMode()
        {
            //if (m_prevState != AccessibleGameState.TARGET_MODE) // Was broken due to play
            if (m_forceAnnounceChooseTarget || (m_prevResponseMode != m_curResponseMode && m_curResponseMode != GameState.ResponseMode.CHOICE))
            {
                m_forceAnnounceChooseTarget = false;
                AccessibilityMgr.Output(this, LocalizationUtils.Get(LocalizationKey.GAMEPLAY_CHOOSE_TARGET));

                if (m_cardBeingRead != null && m_cardBeingRead.GetCard().GetAccessibleZone() == GameState.Get().GetFriendlySidePlayer().GetHandZone())
                {
                    StopReadingCard(false);
                }
            }

            HandleZoneInput();
                            HandleCardReadingInput();
            HandleValidOptionsSelectionInput();
            HandleZoneSelection(m_curResponseMode != GameState.ResponseMode.OPTION_REVERSE_TARGET);
            HandleConfirmOrCancel(true);
        }

        public void ForceAnnounceChooseTarget()
        {
            m_forceAnnounceChooseTarget = true;
        }

        private void UpdateState()
        {
            try
            {
                UpdateCardBeingReadState();
            }
            catch (Exception e)
            {
                AccessibilityUtils.LogFatalError(e);
                StopReadingCard();
            }

            m_heldCard = InputManager.Get().GetHeldCard();
            m_playerTurn = GameState.Get().IsFriendlySidePlayerTurn();

            if (!GameState.Get().IsInMainOptionMode())
            {
                m_confirmingEndTurn = false;
            }

            // TODO: Debug this properly as I think this is the flow causing the tab right after choose one issues
            ResetChoiceCardsIfNecessary();

            if (m_heldCard == null)
            {
                m_tradingCard = false;
            }
            else if (m_tradingCardWaitingForHold && CanTradeCard(m_heldCard))
            {
                // Additional check via CanTradeCard to prevent potential race conditions around turns ending in-between trades/selections
                m_tradingCardWaitingForHold = false;
                m_tradingCard = true;
            }

            m_prevState = m_curState;
            m_prevResponseMode = m_curResponseMode;
            m_curResponseMode = GameState.Get().GetResponseMode();

            // Proper states
            if (!m_playerTurn)
            {
                m_curState = AccessibleGameState.OPPONENT_TURN;
                m_sendingAllMinionsToFace = false;
                m_sendingMinionToFace = false;
            }
            else if (m_sendingAllMinionsToFace)
            {
                m_curState = AccessibleGameState.ALL_MINIONS_TO_FACE;
            }
            else if (m_sendingMinionToFace)
            {
                m_curState = AccessibleGameState.MINION_TO_FACE;
            }
            else if (m_tradingCard)
            {
                m_curState = AccessibleGameState.TRADING_CARD;
            }
            else if (m_confirmingEndTurn)
            {
                m_curState = AccessibleGameState.CONFIRMING_END_TURN;
            }
            else if (AccessibleHistoryMgr.Get().IsReadingHistory())
            {
                m_curState = AccessibleGameState.BROWSING_HISTORY;
            }
            else if (m_heldCard != null)
            {
                if (m_heldCard.GetEntity().IsMinion())
                {
                    m_curState = AccessibleGameState.SUMMONING_MINION;
                }
                else if (GameState.Get().IsInMainOptionMode())
                {
                    m_curState = AccessibleGameState.PLAYING_CARD;
                }
            }
            else if (GameState.Get().IsInMainOptionMode())
            {
                m_curState = AccessibleGameState.MAIN_OPTION_MODE;
            }
            else if (GameState.Get().IsInSubOptionMode())
            {
                m_curState = AccessibleGameState.SUB_OPTION_MODE;
            }
            else if (GameState.Get().IsInChoiceMode())
            {
                if (IsTargetChoice() || ChoiceCardMgr.Get().IsShowingFriendlyCards())
                {
                    m_curState = AccessibleGameState.CHOICE_MODE;
                }
                else
                {
                    m_curState = AccessibleGameState.CHOICE_MODE_CHOICES_HIDDEN;
                }
            }
            else if (GameState.Get().IsInTargetMode() || m_curResponseMode==GameState.ResponseMode.OPTION_TARGET || m_curResponseMode == GameState.ResponseMode.OPTION_REVERSE_TARGET)
            {
                m_curState = AccessibleGameState.TARGET_MODE;
            }
            else
            {
                // Normally happens in between turns (i.e. after button press but before response) due to network time
                m_curState = AccessibleGameState.UNKNOWN;
            }

        }

        protected void ResetChoiceCardsIfNecessary()
        {
            if (m_prevState != m_curState && (m_prevState == AccessibleGameState.CHOICE_MODE || m_prevState == AccessibleGameState.SUB_OPTION_MODE))
            {
                if (!ChoiceCardMgr.Get()?.IsFriendlyShown() ?? false)
                {
                    // Potential race condition when multiple choices happen in a game
                    m_accessibleChoiceCards = null;
                    m_waitingForChoiceConfirmation = false;
                }
            }
        }

        protected void UpdateCardBeingReadState()
        {
            if (m_cardBeingRead == null || m_curZone == null)
            {
                return;
            }

            if (m_cardBeingRead.GetCard().GetAccessibleZone() != m_curZone)
            {
                StopReadingCard();
            }
        }

        protected void StopReadingCard(bool hideMouse = true)
        {
            m_cardBeingRead = null;
            m_curZone = null;

            if (hideMouse)
            {
                HideMouse();
            }
        }

        protected void HandleZoneSelection(bool minionsAndHeroesOnly = false)
        {
            if (AccessibleKey.SEE_PLAYER_HAND.IsPressed() && !minionsAndHeroesOnly)
            {
                SeePlayerHand();
            }
            else if (IsSeePlayerSecretsPressed() && !minionsAndHeroesOnly)
            {
                SeePlayerSecrets();
            }
            else if (IsSeeOpponentSecretsPressed() && !minionsAndHeroesOnly)
            {
                SeeOpponentSecrets();
            }
            else if (AccessibleKey.SEE_PLAYER_MINIONS.IsPressed())
            {
                SeePlayerMinions();
            }
            else if (AccessibleKey.SEE_OPPONENT_MINIONS.IsPressed())
            {
                SeeOpponentMinions();
            }
            else if (IsSeeOpponentHeroPressed())
            {
                SeeOpponentHero();
            }
            else if (AccessibleKey.SEE_PLAYER_HERO.IsPressed())
            {
                SeePlayerHero();
            }
            else if (IsSeePlayerHeroPowerPressed())
            {
                SeePlayerHeroPower();
            }
            else if (IsSeeOpponentHeroPowerPressed())
            {
                SeeOpponentHeroPower();
            }
            else if (AccessibleKey.SEE_PLAYER_WEAPON.IsPressed())
            {
                SeePlayerWeapon();
            }
            else if (AccessibleKey.SEE_OPPONENT_WEAPON.IsPressed())
            {
                SeeOpponentWeapon();
            }
        }

        private bool IsSeePlayerSecretsPressed()
        {
            return AccessibleKey.SEE_PLAYER_SECRETS.IsPressed();
        }

        private bool IsSeeOpponentSecretsPressed()
        {
            return AccessibleKey.SEE_OPPONENT_SECRETS.IsPressed();
        }

        protected virtual bool IsSeeOpponentHeroPressed()
        {
            return AccessibleKey.SEE_OPPONENT_HERO.IsPressed();
        }

        protected virtual bool IsSeePlayerHeroPowerPressed()
        {
            return AccessibleKey.SEE_PLAYER_HERO_POWER.IsPressed();
        }

        protected virtual bool IsSeeOpponentHeroPowerPressed()
        {
            return AccessibleKey.SEE_OPPONENT_HERO_POWER.IsPressed();
        }

        private void HandleHistoryInput()
        {
            AccessibleHistoryMgr.Get().HandleAccessibleInput();
        }

        internal virtual void HandleCheckStatusKeys()
        {
            if (!AccessibleGameplayUtils.IsPlayingBattlegrounds())
            {
            HandleReadAnomaliesKey();
            }

            if (AccessibleKey.SEE_PLAYER_MANA.IsPressed() && !m_disableManaCounters)
            {
                ReadPlayerResources();
            }
            else if (AccessibleKey.SEE_OPPONENT_MANA.IsPressed() && !m_disableManaCounters)
            {
                ReadOpponentResources();
            }
            else if (AccessibleKey.SEE_PLAYER_DECK.IsPressed())
            {
                ReadPlayerDeck();
            }
            else if (AccessibleKey.SEE_OPPONENT_DECK.IsPressed())
            {
                ReadOpponentDeck();
            }
            else if (AccessibleKey.SEE_OPPONENT_HAND.IsPressed())
            {
                ReadOpponentHand();
            }
        }

        protected void HandleReadAnomaliesKey()
        {
            if (AccessibleKey.READ_ANOMALIES.IsPressed())
            {
                if (ReadShrinesQuickStatus())
                {
                    return;
                }

                ReadAnomalies();
            }
        }

        protected void HandleCardReadingInput()
        {
            if (m_cardBeingRead == null)
            {
                return;
            }

            if (AccessibleKey.READ_ORIGINAL_CARD_STATS.IsPressed() && m_cardBeingRead.GetCard().GetEntity().IsMinion())
            {
                ReadBigCard();
            }
            else
            {
                m_cardBeingRead.HandleAccessibleInput();
            }
        }

        private void ReadBigCard()
        {
            ReadBigCardStats();

            var bigCard = BigCard.Get();
            if (bigCard != null && bigCard.GetCard() != null && bigCard.isActiveAndEnabled && bigCard.m_enchantmentBanner.isActiveAndEnabled)
            {
                // Banner (e.g. turns until this revives: 2 on BoH valeera 5)
                var bannerText = bigCard.m_enchantmentBanner?.m_EnchantmentBannerText;

                if (bannerText?.gameObject.activeInHierarchy ?? false)
                {
                    AccessibilityMgr.Output(this, bannerText.Text);
                }
            }
            ReadCardEnchantments();
        }

        private void ReadCardEnchantments()
        {
            Dictionary<Tuple<string, string>, uint> enchantmentCounts = new Dictionary<Tuple<string, string>, uint>();
            List<Tuple<string, string>> enchantmentInfos = new List<Tuple<string, string>>();
            var enchantments = m_cardBeingRead.GetCard().GetEntity().GetDisplayedEnchantments();
            foreach (var enchantment in enchantments)
            {
                var enchantmentInfo = new Tuple<string, string>(enchantment.GetName(), enchantment.GetCardTextInHand());
                if (!enchantmentInfos.Contains(enchantmentInfo))
                {
                    enchantmentInfos.Add(enchantmentInfo);
                }
                uint count = 0;
                enchantmentCounts.TryGetValue(enchantmentInfo, out count);
                enchantmentCounts[enchantmentInfo] = (uint)Mathf.Max(enchantment.GetTag(GAME_TAG.SPAWN_TIME_COUNT), 1) + count;
            }

            foreach (var enchantmentInfo in enchantmentInfos)
            {
                AccessibilityMgr.Output(this, LocalizedText.GLOBAL_ENCHANTMENT);
                var enchantmentCount = enchantmentCounts[enchantmentInfo];
                var header = enchantmentCount > 1 ? GameStrings.Format("GAMEPLAY_ENCHANTMENT_MULTIPLIER_HEADER", enchantmentCount, enchantmentInfo.Item1) : enchantmentInfo.Item1;
                AccessibilityMgr.Output(this,  header);
                AccessibilityMgr.Output(this, enchantmentInfo.Item2);
            }
        }

        private void ReadBigCardStats()
        {
            var entity = m_cardBeingRead.GetCard().GetEntity();

            if (AccessibleCardUtils.HasHiddenStats(entity))
            {
                // e.g. BoH Valeera 06
                return;
            }

            var atk = entity.GetEntityDef().GetATK();
            var hp = entity.GetEntityDef().GetHealth();

            AccessibilityMgr.Output(this, LocalizationUtils.Format(LocalizationKey.READ_CARD_ATK_HEALTH, atk, hp));
        }

        protected void SeeOpponentMinions()
        {
            var opponentMinions = GetOpponentMinions();

            if (opponentMinions.GetCardCount() == 0)
            {
                ReadOpponentMinionsEmpty();
            }
            else
            {
                SeeZone(opponentMinions);
            }
        }

        protected virtual IZone GetOpponentMinions()
        {
            return GameState.Get().GetOpposingSidePlayer().GetBattlefieldZone();
        }

        protected virtual void ReadOpponentMinionsEmpty()
        {
            AccessibilityMgr.Output(this, LocalizationUtils.Get(LocalizationKey.GAMEPLAY_SEE_ZONE_OPPONENT_MINIONS_EMPTY));
        }

        protected void SeePlayerMinions()
        {
            var playerMinions = GetPlayerMinions();

            if (playerMinions.GetCardCount() == 0)
            {
                AccessibilityMgr.Output(this, LocalizationUtils.Get(LocalizationKey.GAMEPLAY_SEE_ZONE_PLAYER_MINIONS_EMPTY));
            }
            else
            {
                SeeZone(playerMinions);
            }
        }

        protected virtual IZone GetPlayerMinions()
        {
            return GameState.Get().GetFriendlySidePlayer().GetBattlefieldZone();
        }

        protected void SeePlayerSecrets()
        {
            var playerSecrets = GetPlayerSecrets();

            if (playerSecrets.GetCardCount() == 0)
            {
                AccessibilityMgr.Output(this, LocalizationUtils.Get(LocalizationKey.GAMEPLAY_SEE_ZONE_PLAYER_SECRETS_EMPTY));
            }
            else
            {
                SeeZone(playerSecrets);
            }
        }

        protected virtual  IZone GetPlayerSecrets()
        {
            return GameState.Get().GetFriendlySidePlayer().GetSecretZone();
        }

        protected void SeeOpponentSecrets()
        {
            var opponentSecrets = GetOpponentSecrets();

            if (opponentSecrets.GetCardCount() == 0)
            {
                AccessibilityMgr.Output(this, LocalizationUtils.Get(LocalizationKey.GAMEPLAY_SEE_ZONE_OPPONENT_SECRETS_EMPTY));
            }
            else
            {
                SeeZone(opponentSecrets);
            }
        }

        protected virtual IZone GetOpponentSecrets()
        {
            return GameState.Get().GetOpposingSidePlayer().GetSecretZone();
        }

        protected void SeeOpponentHero()
        {
            FocusOnCard(GetOpponentHero(), false);
        }

        protected void SeePlayerHero()
        {
            FocusOnCard(GetPlayerHero(), false);
        }

        protected virtual Card GetPlayerHero()
        {
            return GameState.Get().GetFriendlySidePlayer().GetHeroCard();
        }

        protected virtual Card GetOpponentHero()
        {
            return GameState.Get().GetOpposingSidePlayer().GetHeroCard();
        }

        protected void SeePlayerHeroPower()
        {
            var heroPowers = GetPlayerHeroPowers();
            if (heroPowers.GetCardCount() != 0)
            {
                SeeZone(heroPowers);
            }
            else
            {
                AccessibilityMgr.Output(this, LocalizationUtils.Get(LocalizationKey.GAMEPLAY_SEE_ZONE_PLAYER_HERO_POWER_EMPTY));
            }
        }

        protected virtual IZone GetPlayerHeroPowers()
        {
            return GameState.Get().GetFriendlySidePlayer().GetHeroPowers();
        }

        protected void SeeOpponentHeroPower()
        {
            var heroPowers = GameState.Get().GetOpposingSidePlayer().GetHeroPowers();
            if (heroPowers.GetCardCount() != 0)
            {
                SeeZone(heroPowers);
            }
            else
            {
                AccessibilityMgr.Output(this, LocalizationUtils.Get(LocalizationKey.GAMEPLAY_SEE_ZONE_OPPONENT_HERO_POWER_EMPTY));
            }
        }

        protected virtual void SeePlayerWeapon()
        {
            var weapon = GameState.Get().GetFriendlySidePlayer().GetWeaponCard();
            if (weapon != null)
            {
                FocusOnCard(weapon, false);
            }
            else
            {
                AccessibilityMgr.Output(this, LocalizationUtils.Get(LocalizationKey.GAMEPLAY_SEE_ZONE_PLAYER_WEAPON_EMPTY));
            }
        }

        protected virtual void SeeOpponentWeapon()
        {
            var weapon = GameState.Get().GetOpposingSidePlayer().GetWeaponCard();
            if (weapon != null)
            {
                FocusOnCard(weapon, false);
            }
            else
            {
                AccessibilityMgr.Output(this, LocalizationUtils.Get(LocalizationKey.GAMEPLAY_SEE_ZONE_OPPONENT_WEAPON_EMPTY));
            }
        }

        private void ClickCard(bool tradingCard = false)
        {
            if (m_cardBeingRead == null)
            {
                return;
            }

            if (tradingCard)
            {
                m_tradingCardWaitingForHold = true;
            }

            if(m_cardBeingRead.GetCard()!=InputManager.Get().GetMousedOverCard()) {
                AccessibilityMgr.Output(null,LocalizationUtils.Get(LocalizationKey.GAMEPLAY_TRY_AGAIN));
                InputManager.Get().SetMousedOverCard(m_cardBeingRead.GetCard());
            }

            AccessibleInputMgr.ClickLeftMouseButton();
        }

        internal virtual void ReadPlayerResources()
        {
            var player = GameState.Get().GetFriendlySidePlayer();
            int availableMana = player.GetNumAvailableResources();
            int totalMana = player.GetTag(GAME_TAG.RESOURCES);

            if (availableMana != totalMana)
            {
                AccessibilityMgr.Output(this, LocalizationUtils.Format(LocalizationKey.GAMEPLAY_READ_PLAYER_MANA_CURRENT_AND_TOTAL, availableMana, totalMana));
            }
            else
            {
                AccessibilityMgr.Output(this, LocalizationUtils.Format(LocalizationKey.GAMEPLAY_READ_PLAYER_MANA, availableMana));
            }

            // Overloaded crystals this turn
            var overloadedMana = player.GetTag(GAME_TAG.OVERLOAD_OWED);
            if (overloadedMana > 0)
            {
                AccessibilityMgr.Output(this, LocalizationUtils.Format(LocalizationKey.GAMEPLAY_READ_PLAYER_OVERLOADED_MANA, overloadedMana));
            }

            // Locked crystals due to overload last turn
            var lockedMana = player.GetTag(GAME_TAG.OVERLOAD_LOCKED);
            if (lockedMana > 0)
            {
                AccessibilityMgr.Output(this, LocalizationUtils.Format(LocalizationKey.GAMEPLAY_READ_PLAYER_LOCKED_MANA, lockedMana));
            }
            // Death knight corpses
            if (CorpseCounter.ShouldShowCorpseCounter(player) && !m_disableCorpseCounters)
            {
                var corpses = player.GetNumAvailableCorpses();
                if (corpses == 0)
                {
                    AccessibilityMgr.Output(this, LocalizationUtils.Get(LocalizationKey.GAMEPLAY_READ_CORPSES_EMPTY));
                }
                else
                {
                    AccessibilityMgr.Output(this, LocalizationUtils.Format(LocalizationKey.GAMEPLAY_READ_CORPSES, corpses));
                }
            }

            ReadStarshipLaunchCostIfNecessary(player);
        }

        internal virtual void ReadOpponentResources()
        {
            var opponent = GameState.Get().GetOpposingSidePlayer();

            int availableMana = opponent.GetNumAvailableResources();
            int totalMana = opponent.GetTag(GAME_TAG.RESOURCES);
            if (availableMana != totalMana)
            {
                AccessibilityMgr.Output(this, LocalizationUtils.Format(LocalizationKey.GAMEPLAY_READ_OPPONENT_MANA_CURRENT_AND_TOTAL, availableMana, totalMana));
            }
            else
            {
                AccessibilityMgr.Output(this, LocalizationUtils.Format(LocalizationKey.GAMEPLAY_READ_OPPONENT_MANA, availableMana));
            }


            // Overloaded crystals this turn
            var overloadedMana = opponent.GetTag(GAME_TAG.OVERLOAD_OWED);
            if (overloadedMana > 0)
            {
                AccessibilityMgr.Output(this, LocalizationUtils.Format(LocalizationKey.GAMEPLAY_READ_OPPONENT_OVERLOADED_MANA, overloadedMana));
            }
            // Death knight corpses
            if (CorpseCounter.ShouldShowCorpseCounter(opponent) && !m_disableCorpseCounters)
            {
                var corpses = opponent.GetNumAvailableCorpses();
                if (corpses == 0)
                {
                    AccessibilityMgr.Output(this, LocalizationUtils.Get(LocalizationKey.GAMEPLAY_READ_CORPSES_EMPTY));
                }
                else
                {
                    AccessibilityMgr.Output(this, LocalizationUtils.Format(LocalizationKey.GAMEPLAY_READ_CORPSES, corpses));
                }
            }

            ReadStarshipLaunchCostIfNecessary(opponent);
        }

        private void ReadStarshipLaunchCostIfNecessary(Player player)
        {
            foreach (var card in player.GetBattlefieldZone().GetCards())
            {
                if (card.GetEntity().IsLaunchpad())
                {
                    AccessibilityMgr.Output(this, LocalizationUtils.Format(LocalizationKey.GAMEPLAY_READ_STARSHIP_LAUNCH_COST, GameUtils.StarshipLaunchCost(player)));
                    return;
                }
            }
        }

        internal void ReadAnomalies(bool addToHistory = false)
        {
            var anomalyIds = MulliganManager.GetAnomalies();
            if (anomalyIds.Count == 0)
            {
                AccessibilityMgr.Output(this, LocalizationUtils.Get(LocalizationKey.GAMEPLAY_NO_ANOMELIES));
                return;
            }

            foreach (var anomalyId in anomalyIds.Select((entityId) => GameUtils.TranslateCardIdToDbId(GameState.Get().GetEntity(entityId).GetCardId())))
            {
                string text = AccessibleCardUtils.GetAnomalyText(anomalyId);
                AccessibilityMgr.Output(this, text);
                if (addToHistory)
                {
                    AccessibleHistoryMgr.Get().AddEntry(text);
                }
            }
        }

        private bool ReadShrinesQuickStatus(bool addToHistory = false)
        {
            var gameState = GameState.Get();
            var friendlyPlayer = gameState?.GetFriendlySidePlayer();
            var opposingPlayer = gameState?.GetOpposingSidePlayer();

            if (friendlyPlayer == null || opposingPlayer == null)
            {
                return false;
            }

            var friendlyShrine = FindShrineCard(friendlyPlayer);
            var opposingShrine = FindShrineCard(opposingPlayer);

            if (friendlyShrine == null && opposingShrine == null)
            {
                return false;
            }

            ReadSingleShrineStatus(friendlyShrine, friendly: true, addToHistory);
            ReadSingleShrineStatus(opposingShrine, friendly: false, addToHistory);

            return true;
        }

        private void UpdateShrineAnnouncements()
        {
            var gameState = GameState.Get();
            var friendlyPlayer = gameState?.GetFriendlySidePlayer();
            var opponentPlayer = gameState?.GetOpposingSidePlayer();

            if (friendlyPlayer == null || opponentPlayer == null)
            {
                m_shrineStateInitialized = false;
                return;
            }

            var friendlyState = BuildShrineStateSnapshot(friendlyPlayer);
            var opponentState = BuildShrineStateSnapshot(opponentPlayer);

            var hasAnyShrineNow = friendlyState.Exists || opponentState.Exists;
            var hadAnyShrine = m_friendlyShrineState.Exists || m_opponentShrineState.Exists;

            if (!m_shrineStateInitialized)
            {
                m_friendlyShrineState = friendlyState;
                m_opponentShrineState = opponentState;
                m_shrineStateInitialized = hasAnyShrineNow;
                return;
            }

            if (!hasAnyShrineNow && !hadAnyShrine)
            {
                m_shrineStateInitialized = false;
                return;
            }

            AnnounceShrineStateTransition(m_friendlyShrineState, friendlyState, friendly: true);
            AnnounceShrineStateTransition(m_opponentShrineState, opponentState, friendly: false);

            m_friendlyShrineState = friendlyState;
            m_opponentShrineState = opponentState;
            m_shrineStateInitialized = hasAnyShrineNow;
        }

        private static ShrineStateSnapshot BuildShrineStateSnapshot(Player player)
        {
            var shrineCard = FindShrineCard(player);

            if (shrineCard == null)
            {
                return new ShrineStateSnapshot
                {
                    Exists = false,
                    EntityId = -1,
                    Name = "",
                    Dormant = false,
                    BannerText = ""
                };
            }

            var entity = shrineCard.GetEntity();

            return new ShrineStateSnapshot
            {
                Exists = true,
                EntityId = entity.GetEntityId(),
                Name = entity.GetName(),
                Dormant = entity.IsDormant() || entity.HasTag(GAME_TAG.DORMANT_VISUAL),
                BannerText = GetShrineBannerText(entity)
            };
        }

        private void AnnounceShrineStateTransition(ShrineStateSnapshot previous, ShrineStateSnapshot current, bool friendly)
        {
            if (!previous.Exists && !current.Exists)
            {
                return;
            }

            var sideText = friendly
                ? LocalizationUtils.Get(LocalizationKey.GAMEPLAY_ZONE_PLAYER_HERO)
                : LocalizationUtils.Get(LocalizationKey.GAMEPLAY_ZONE_OPPONENT_HERO);

            if (!previous.Exists && current.Exists)
            {
                var appearedText = AccessibleSpeechUtils.CombineWordsWithColon(sideText, BuildShrineStatusText(FindShrineCard(friendly ? GameState.Get().GetFriendlySidePlayer() : GameState.Get().GetOpposingSidePlayer()), friendly));
                AccessibilityMgr.Output(this, appearedText);
                return;
            }

            if (previous.Exists && !current.Exists)
            {
                var removedText = AccessibleSpeechUtils.CombineLines(new List<string>
                {
                    previous.Name,
                    LocalizationUtils.Get(LocalizationKey.GAMEPLAY_DIFF_ENTITY_DIED)
                });

                AccessibilityMgr.Output(this, AccessibleSpeechUtils.CombineWordsWithColon(sideText, removedText));
                return;
            }

            if (previous.EntityId != current.EntityId)
            {
                var changedText = AccessibleSpeechUtils.CombineWordsWithColon(sideText, BuildShrineStatusText(FindShrineCard(friendly ? GameState.Get().GetFriendlySidePlayer() : GameState.Get().GetOpposingSidePlayer()), friendly));
                AccessibilityMgr.Output(this, changedText);
                return;
            }

            if (!previous.Dormant && current.Dormant)
            {
                var becameDormantText = AccessibleSpeechUtils.CombineLines(new List<string>
                {
                    current.Name,
                    LocalizationUtils.Get(LocalizationKey.GAMEPLAY_DIFF_ENTITY_BECAME_DORMANT),
                    current.BannerText
                });

                AccessibilityMgr.Output(this, AccessibleSpeechUtils.CombineWordsWithColon(sideText, becameDormantText));
                return;
            }

            if (previous.Dormant && !current.Dormant)
            {
                var revivedText = AccessibleSpeechUtils.CombineLines(new List<string>
                {
                    current.Name,
                    LocalizationUtils.Get(LocalizationKey.GAMEPLAY_DIFF_ENTITY_NO_LONGER_DORMANT)
                });

                AccessibilityMgr.Output(this, AccessibleSpeechUtils.CombineWordsWithColon(sideText, revivedText));
                return;
            }

            if (current.Dormant && previous.BannerText != current.BannerText && current.BannerText.Length > 0)
            {
                var timerText = AccessibleSpeechUtils.CombineLines(new List<string>
                {
                    current.Name,
                    current.BannerText
                });

                AccessibilityMgr.Output(this, AccessibleSpeechUtils.CombineWordsWithColon(sideText, timerText));
            }
        }

        private static Card FindShrineCard(Player player)
        {
            if (player == null)
            {
                return null;
            }

            foreach (var card in player.GetBattlefieldZone().GetCards())
            {
                if (card?.GetEntity()?.HasTag(GAME_TAG.SHRINE) ?? false)
                {
                    return card;
                }
            }

            return null;
        }

        private void ReadSingleShrineStatus(Card shrineCard, bool friendly, bool addToHistory)
        {
            var prefix = friendly
                ? LocalizationUtils.Get(LocalizationKey.GAMEPLAY_ZONE_PLAYER_HERO)
                : LocalizationUtils.Get(LocalizationKey.GAMEPLAY_ZONE_OPPONENT_HERO);

            var text = AccessibleSpeechUtils.CombineWordsWithColon(prefix, BuildShrineStatusText(shrineCard, friendly));

            AccessibilityMgr.Output(this, text);
            if (addToHistory)
            {
                AccessibleHistoryMgr.Get().AddEntry(text);
            }
        }

        private string BuildShrineStatusText(Card shrineCard, bool friendly)
        {
            if (shrineCard == null)
            {
                return friendly
                    ? LocalizationUtils.Get(LocalizationKey.GAMEPLAY_SEE_ZONE_PLAYER_MINIONS_EMPTY)
                    : LocalizationUtils.Get(LocalizationKey.GAMEPLAY_SEE_ZONE_OPPONENT_MINIONS_EMPTY);
            }

            var entity = shrineCard.GetEntity();
            var details = new List<string>();

            details.Add(entity.GetName());

            if (entity.IsDormant() || entity.HasTag(GAME_TAG.DORMANT_VISUAL))
            {
                details.Add(LocalizedText.GLOBAL_DORMANT);

                var bannerText = GetShrineBannerText(entity);
                if (bannerText.Length > 0)
                {
                    details.Add(bannerText);
                }
            }
            else if (!AccessibleCardUtils.HasHiddenStats(entity))
            {
                details.Add(LocalizationUtils.Format(LocalizationKey.READ_CARD_ATK_HEALTH, entity.GetATK(), entity.GetCurrentHealth()));
            }

            return AccessibleSpeechUtils.CombineLines(details);
        }

        private static string GetShrineBannerText(Entity entity)
        {
            if (entity == null || !entity.HasTag(GAME_TAG.ENCHANTMENT_BANNER_TEXT))
            {
                return "";
            }

            var text = entity.GetCustomEnchantmentBannerText();
            if (text == null || text.Length == 0)
            {
                return "";
            }

            return text;
        }

        protected void HandleZoneInput()
        {
            if (m_curZone == null)
            {
                return;
            }

            int? numKeyPressed = AccessibleInputMgr.TryGetPressedNumKey();

            if (numKeyPressed.HasValue)
            {
                ReadCardInZone(numKeyPressed.Value);
            }
            else if (AccessibleKey.READ_NEXT_ITEM.IsPressed())
            {
                int curPos = GetCardBeingReadPosition();
                ReadCardInZone(curPos + 1);
            }
            else if (AccessibleKey.READ_PREV_ITEM.IsPressed())
            {
                int curPos = GetCardBeingReadPosition();
                ReadCardInZone(curPos - 1);
            }
            else if (AccessibleKey.READ_FIRST_ITEM.IsPressed())
            {
                ReadCardInZone(1);
            }
            else if (AccessibleKey.READ_LAST_ITEM.IsPressed())
            {
                ReadCardInZone(m_curZone.GetCardCount());
            }
        }

        protected void HandleValidOptionsSelectionInput()
        {
            if (AccessibleKey.READ_NEXT_VALID_ITEM.IsPressed())
            {
                FindNextValidCard();
            }
            else if (AccessibleKey.READ_PREV_VALID_ITEM.IsPressed())
            {
                FindNextValidCard(true);
            }
        }

        private void FindNextValidCard(bool reverseDirection = false)
        {
            List<Card> candidates = GetCandidateOptions();
            List<Card> validOptions = GetValidOptions(candidates);

            if (validOptions.Count == 0)
            {
                OnNoValidPlays();
                return;
            }
            else if (validOptions.Count == 1)
            {
                // The old interface read "no more valid options" instead of reading the focused card again when this happened
                // However, quite a few players found it confusing and often mistakenly ended their turn as "no more valid options" is quite similar to "no valid plays"
                FocusOnCard(validOptions[0], true);
            }

            if (GameState.Get().IsInTargetMode() && m_cardBeingRead?.GetCard() == GameState.Get().GetFriendlySidePlayer().GetHeroPowerCard())
            {
                m_cardBeingRead = null;
            }

            if (m_cardBeingRead == null)
            {
                if (reverseDirection)
                {
                    FocusOnCard(validOptions[validOptions.Count - 1], false);
                    return;
                }
                else
                {
                    FocusOnCard(validOptions[0], false);
                    return;
                }
            }

            int curCardIndex = 0;
            for (int i = 0; i < candidates.Count; i++)
            {
                if (candidates[i] == m_cardBeingRead.GetCard())
                {
                    curCardIndex = i;
                }
            }

            int inc = reverseDirection ? -1 : 1;

            for (int toRead = candidates.Count - 1, i = curCardIndex + inc; toRead > 0; toRead--, i += inc)
            {
                if (i < 0)
                {
                    i = candidates.Count - 1;
                }
                else if (i >= candidates.Count)
                {
                    i = 0;
                }

                var card = candidates[i];

                if (IsValidOption(card))
                {
                    FocusOnCard(card, false);
                    return;
                }
            }
        }

        private List<Card> GetValidOptions(List<Card> candidates)
        {
            var ret = new List<Card>();

            foreach (var card in candidates)
            {
                if (IsValidOption(card))
                {
                    ret.Add(card);
                }
            }

            return ret;
        }

        private List<Card> GetCandidateOptions()
        {
            var cycleFriendlyEntitiesFirst = true;

            if (GameState.Get().IsInTargetMode())
            {
                var source = GameState.Get().GetSelectedNetworkOption();
                var sourceEntity = GameState.Get().GetEntity(source.Main.ID);

                if (CardEffectInterpreter.GetEffect(sourceEntity.GetCardId()) == CardEffectInterpreter.CardEffect.UNFRIENDLY)
                {
                    cycleFriendlyEntitiesFirst = false;
                }
            }

            var ret = new List<Card>();
            var player = GameState.Get().GetFriendlySidePlayer();
            var opponent = GameState.Get().GetOpposingSidePlayer();

            var playerWeapon = player.GetWeaponCard();
            var playerHero = player.GetHeroCard();
            var playerHeroPower = player.GetHeroPowerCard();
            var opponentWeapon = opponent.GetWeaponCard();
            var opponentHero = opponent.GetHeroCard();
            var opponentHeroPower = opponent.GetHeroPowerCard();

            var playerEntities = new List<Card>();
            var opponentEntities = new List<Card>();

            playerEntities.AddRange(player.GetHandZone().GetCards());
            if (playerWeapon != null) playerEntities.Add(playerWeapon);
            if (playerHero != null) playerEntities.Add(playerHero);
            if (playerHeroPower != null) playerEntities.Add(playerHeroPower);
            playerEntities.AddRange(player.GetBattlefieldZone().GetCards());

            opponentEntities.AddRange(opponent.GetBattlefieldZone().GetCards());
            if (opponentHero != null) opponentEntities.Add(opponentHero);
            if (opponentWeapon != null) opponentEntities.Add(opponentWeapon);
            if (opponentHeroPower != null) opponentEntities.Add(opponentHeroPower);

            if (cycleFriendlyEntitiesFirst)
            {
                ret.AddRange(playerEntities);
                ret.AddRange(opponentEntities);
            }
            else
            {
                ret.AddRange(opponentEntities);
                ret.AddRange(playerEntities);
            }


            return ret;
        }

        private bool IsValidOption(Card card)
        {
            if (card == null)
            {
                return false;
            }

            Entity entity = card.GetEntity();

            if (GameState.Get().IsInMainOptionMode())
            {
                return GameState.Get().IsValidOption(entity);
            }
            else if (GameState.Get().IsInSubOptionMode())
            {
                return GameState.Get().IsValidSubOption(entity);
            }
            else if (GameState.Get().IsInChoiceMode())
            {
                return GameState.Get().IsChoice(entity);
            }
            else if (GameState.Get().IsInTargetMode() || GameState.Get().IsInReverseTargetMode())
            {
                return GameState.Get().IsValidOptionTarget(entity, true);
            }

            return false;
        }

        private int GetCardBeingReadPosition()
        {
            for (int i = 1; i <= m_curZone.GetCardCount(); i++)
            {
                if (m_curZone.GetCardAtSlot(i) == m_cardBeingRead.GetCard())
                {
                    return i;
                }
            }

            return 0;
        }

        private void ReadCardInZone(int pos)
        {
            Card card = m_curZone.GetCardAtSlot(pos);

            if (card != null)
            {
                FocusOnCard(card, false);
            }
        }

        protected void FocusOnCard(Card card, bool forceZoneRead)
        {
            SetCardBeingRead(card, forceZoneRead);

            var speech = m_cardBeingRead.GetLine(0);

            var zonePos = card.GetAccessibleZone().FindCardPos(card); // card.GetAccessibleZonePosition() isn't trustworthy. Wasn't working for secrets

            if (ShouldReadCardAsList(card))
            {
                AccessibilityMgr.Output(this, AccessibleSpeech.MENU_OPTION(speech, zonePos, card.GetAccessibleZone().GetCardCount()));
            }
            else
            {
                AccessibilityMgr.Output(this, speech);
            }
            MoveMouseToCard(card);
            OnCardSelected(card, m_cardBeingRead);
        }

        protected bool ShouldReadCardAsList(Card card)
        {
            var player = GameState.Get().GetFriendlySidePlayer();
            var opponent = GameState.Get().GetOpposingSidePlayer();
            var zone = card.GetAccessibleZone();

            return zone == GetPlayerHand() ||
                zone == opponent.GetHandZone() ||
                zone == GetPlayerMinions() ||
                zone == GetOpponentMinions() ||
                zone == GetPlayerSecrets() ||
                zone == GetOpponentSecrets() ||
                card.GetEntity().IsBattlegroundTrinket() ||
                card.GetEntity().IsHeroPower() && zone.GetCardCount() > 1;
        }

        protected virtual void MoveMouseToCard(Card card)
        {
            if (card.GetAccessibleZone()?.GetType() == typeof(ZoneHand))
            {
                if (!card.IsMousedOver())
                {
                    // Note: All of this code was added to trace a rare edge case some players have fallen into with cards such as shadowstep.
                    // I haven't been able to reproduce so far but I think the bug has something to do with a NPE somewhere in the actor/manaObject chain
                    // TODO: Clean all of this up once the bug is detected/fixed
                    try
                    {
                        AccessibleInputMgr.MoveMouseTo(card.GetActor().m_manaObject.transform);
                    }
                    catch (Exception e)
                    {
                        AccessibilityUtils.LogFatalError(e);
                        AccessibilityUtils.LogFatalError($"card.GetActor(): {card.GetActor()}");
                        AccessibilityUtils.LogFatalError($"card.GetActor()?.m_manaObject: {card.GetActor()?.m_manaObject}");
                        AccessibilityUtils.LogFatalError($"card.GetActor()?.m_manaObject?.transform: {card.GetActor()?.m_manaObject?.transform}");
                        AccessibilityUtils.LogFatalError($"card.GetEntity()?.GetCardId(): {card.GetEntity()?.GetCardId()}");

                        if (card.GetActor() != null)
                        {
                            if (card.GetActor().m_attackObject?.transform != null) AccessibleInputMgr.MoveMouseTo(card.GetActor().m_attackObject.transform);
                            else if (card.GetActor().m_healthObject?.transform != null) AccessibleInputMgr.MoveMouseTo(card.GetActor().m_healthObject.transform);
                            else AccessibilityUtils.LogFatalError("Unable to recover - Aborting MoveMouseToCard");
                        }
                    }
                }
            }
            else
            {
                AccessibleInputMgr.MoveMouseTo(card);
            }
        }

        private void MoveMouseToZone(Zone zone)
        {
            Vector3 zoneCenter = zone.GetComponent<Collider>().bounds.center;
            AccessibleInputMgr.MoveMouseToWorldPosition(zoneCenter);
        }

        protected void MoveMouseToRightOfZone(Zone zone)
        {
            var bounds = zone.GetComponent<Collider>().bounds;
            Vector3 pos = bounds.center;
            pos.x += bounds.extents.x;
            AccessibleInputMgr.MoveMouseToWorldPosition(pos);
        }

        protected void MoveMouseToLeftOfZone(Zone zone)
        {
            var bounds = zone.GetComponent<Collider>().bounds;
            Vector3 pos = bounds.center;
            pos.x -= bounds.extents.x;
            AccessibleInputMgr.MoveMouseToWorldPosition(pos);
        }

        private void CancelOption()
        {
            AccessibleInputMgr.ClickRightMouseButton();
            HideMouse();
        }

        internal virtual void ReadOpponentHand()
        {
            var opponent = GameState.Get().GetOpposingSidePlayer();
            int numCards = opponent.GetHandZone().GetCardCount();

            AccessibilityMgr.Output(this, LocalizationUtils.Format(LocalizationKey.GAMEPLAY_READ_OPPONENT_HAND, numCards));
        }

        internal virtual void ReadPlayerDeck()
        {
            var player = GameState.Get().GetFriendlySidePlayer();
            int numCards = player.GetDeckZone().GetCardCount();

            AccessibilityMgr.Output(this, LocalizationUtils.Format(LocalizationKey.GAMEPLAY_READ_PLAYER_DECK, numCards));
        }

        internal virtual void ReadOpponentDeck()
        {
            var opponent = GameState.Get().GetOpposingSidePlayer();
            int numCards = opponent.GetDeckZone().GetCardCount();

            AccessibilityMgr.Output(this, LocalizationUtils.Format(LocalizationKey.GAMEPLAY_READ_OPPONENT_DECK, numCards));
        }

        internal Card GetSelectedCard()
        {
            return m_cardBeingRead?.GetCard();
        }

        protected void SeePlayerHand()
        {
            var playerHand = GetPlayerHand();
            if (playerHand.GetCardCount() == 0)
            {
                AccessibilityMgr.Output(this, LocalizationUtils.Get(LocalizationKey.GAMEPLAY_SEE_ZONE_PLAYER_HAND_EMPTY));
            }
            else
            {
                SeeZone(playerHand);
            }
        }

        protected virtual IZone GetPlayerHand()
        {
            return GameState.Get().GetFriendlySidePlayer().GetHandZone();
        }

        protected void SeeZone(IZone zone)
        {
            if (zone == null)
            {
                return;
            }

            Card card = zone.GetCardAtSlot(1);

            if (card != null) // Race conditions
            {
                FocusOnCard(card, true);
            }
        }

        private void OnGameStart()
        {
            try
            {
                m_curPhase = AccessibleGamePhase.PLAYING;
                IsInBeginningChooseOne = false;
                m_curState = AccessibleGameState.WAITING;

                GameState.Get().RegisterGameOverListener(OnGameOver);
            }
            catch (Exception e)
            {
                AccessibilityUtils.LogFatalError(e);
            }
        }

        protected void OnGameOver(TAG_PLAYSTATE playState, object userData)
        {
            m_curPhase = AccessibleGamePhase.GAME_OVER;

            AccessibilityMgr.Output(this, GetGameOverMessage(playState));
            SaveGameHistory();
        }
private void SaveGameHistory() {
                if (AccessibleGameplayUtils.IsFindingOrPlayingBattlegrounds()) return;
if(Options.Get().GetBool(Option.ACCESSIBILITY_SAVE_BATTLE_LOGS)) {
    AccessibleHistoryMgr.Get().SaveToFile(DateTime.Now.ToString("yyyy-M-d H_mm")+" "+GameState.Get().GetFriendlySidePlayer().GetName()+" v "+GameState.Get().GetOpposingSidePlayer().GetName()+".txt");
    }
}
        private string GetGameOverMessage(TAG_PLAYSTATE playState)
        {
            switch (playState)
            {
                case TAG_PLAYSTATE.WON:
                    return LocalizationUtils.Get(LocalizationKey.GAMEPLAY_GAME_OVER_WON);
                case TAG_PLAYSTATE.LOST:
                case TAG_PLAYSTATE.CONCEDED:
                    return LocalizationUtils.Get(LocalizationKey.GAMEPLAY_GAME_OVER_LOST);
                case TAG_PLAYSTATE.TIED:
                    return LocalizationUtils.Get(LocalizationKey.GAMEPLAY_GAME_OVER_TIED);
                default:
                    return LocalizationUtils.Get(LocalizationKey.GAMEPLAY_GAME_OVER_GENERIC);
            }
        }

        public void OnRevealDrawnOpponentCard(Card card)
        {
        }

        public void OnDrawUnknownOpponentCard(Card card)
        {
        }

        public void OnCardToDeck(Card card)
        {
        }

        public void OnShowBigCard(HistoryCard card)
        {
            try
            {
                AccessibilityUtils.LogDebug($"Card played: {card.GetEntity().GetName()} / type = {card.m_historyInfoType}");

                if (card.m_historyInfoType == HistoryInfoType.CARD_PLAYED)
                {
                    OnCardPlayed(card.OriginTaskList, card.GetEntity());
                }
                else if (card.m_historyInfoType == HistoryInfoType.TRIGGER)
                {
                    OnCardTriggered(card.OriginTaskList, card.GetEntity());
                }
            }
            catch (Exception e)
            {
                AccessibilityUtils.LogFatalError(e);
            }
        }

        private void OnCardTriggered(PowerTaskList taskList, Entity card)
        {
            AccessiblePlayDescriber.Get().OnBigCardTriggered(taskList, card);
        }

        private void OnCardPlayed(PowerTaskList taskList, Entity card)
        {
            AccessiblePlayDescriber.Get().OnBigCardPlayed(taskList, card);
        }

        public void OnChoice(List<Card> cards, Banner choiceBanner, bool isTrinketDiscover)
        {
            // Clear up any previous choices
            m_accessibleChoiceCards = null;
            StopReadingCard();
            m_waitingForChoiceConfirmation = false;

            try
            {
                var accessibleCards = new List<AccessibleCard>(cards.Count);
                cards.ForEach(c => accessibleCards.Add(AccessibleCard.CreateCard(this, c)));

                m_accessibleChoiceCards = new AccessibleListOfItems<AccessibleCard>(this, accessibleCards);
                var title = isTrinketDiscover ? LocalizationUtils.Get(LocalizationKey.BATTLEGROUNDS_GAMEPLAY_BUY_TRINKET) : choiceBanner.m_headline.Text;
                AccessibilityMgr.Output(this, title);
                m_curPhase = AccessibleGamePhase.PLAYING;
                IsInBeginningChooseOne = true;
                m_accessibleChoiceCards.StartReading();
                AccessibleHistoryMgr.Get().AddEntry(title+": "+AccessibleSpeechUtils.GetNames(cards));
            }
            catch (Exception e)
            {
                AccessibilityUtils.LogFatalError(e);
            }
        }

        public void OnSubOption(List<Card> cards)
        {
            // Clear up any previous choices
            m_accessibleChoiceCards = null;
            m_waitingForChoiceConfirmation = false;
            StopReadingCard();

            try
            {
                var accessibleCards = new List<AccessibleCard>(cards.Count);
                cards.ForEach(c => accessibleCards.Add(AccessibleCard.CreateCard(this, c)));

                m_accessibleChoiceCards = new AccessibleListOfItems<AccessibleCard>(this, accessibleCards);

                AccessibilityMgr.Output(this, LocalizationUtils.Get(LocalizationKey.GAMEPLAY_CHOOSE_ONE));
                m_accessibleChoiceCards.StartReading();
            }
            catch (Exception e)
            {
                AccessibilityUtils.LogFatalError(e);
            }
        }

        public void OnMulliganChoiceStart(List<Card> startingCards, NormalButton mulliganConfirmButton)
        {
            try
            {
                m_mulliganConfirmButton = mulliganConfirmButton;

                var accessibleCards = new List<AccessibleCard>(startingCards.Count);
                startingCards.ForEach(c => accessibleCards.Add(AccessibleCard.CreateCard(this, c)));

                m_accessibleMulliganCards = new AccessibleListOfItems<AccessibleCard>(this, accessibleCards);
                m_mulliganMarkedForReplacement = new Dictionary<AccessibleCard, bool>();
                accessibleCards.ForEach(c => m_mulliganMarkedForReplacement.Add(c, false));

                AccessibilityMgr.Output(this, LocalizationUtils.Get(LocalizationKey.GAMEPLAY_MULLIGAN));
                AccessibilityMgr.Output(this, LocalizationUtils.Get(LocalizationKey.GAMEPLAY_MULLIGAN_KEEP_OR_REPLACE_CARDS));
                m_accessibleMulliganCards.StartReading();
            }
            catch (Exception e)
            {
                AccessibilityUtils.LogFatalError(e);
            }
        }

        #region Help
        public virtual string GetHelp()
        {
            switch (m_curPhase)
            {
                case AccessibleGamePhase.WAITING_FOR_GAME_TO_START:
                    return GetWaitingForGameToStartHelp();
                case AccessibleGamePhase.MULLIGAN:
                    return GetMulliganHelp();
                case AccessibleGamePhase.WAITING_FOR_OPPONENT_MULLIGAN:
                    return GetWaitingForOpponentHelp();
                case AccessibleGamePhase.PLAYING:
                    return GetPlayingHelp();
                case AccessibleGamePhase.GAME_OVER:
                    return GetGameOverHelp();
                default:
                    return "";
            }
        }

        private string GetGameOverHelp()
        {
            return LocalizationUtils.Get(LocalizationKey.GAMEPLAY_GAME_OVER_GENERIC);
        }

        protected virtual string GetPlayingHelp()
        {
            if (EmoteHandler.Get()?.AreEmotesActive() ?? false)
            {
                return m_accessibleEmotes?.GetHelp();
            }
            else if (EnemyEmoteHandler.Get()?.AreEmotesActive() ?? false)
            {
                return m_accessibleEnemyEmotes?.GetHelp();
            }

            switch (m_curState)
            {
                case AccessibleGameState.OPPONENT_TURN:
                    return GetOpponentTurnHelp();
                case AccessibleGameState.MAIN_OPTION_MODE:
                    return GetMainOptionModeHelp();
                case AccessibleGameState.SUB_OPTION_MODE:
                    return GetSubOptionModeHelp();
                case AccessibleGameState.TARGET_MODE:
                    return GetTargetModeHelp();
                case AccessibleGameState.CHOICE_MODE:
                    return GetChoiceModeHelp();
                    case AccessibleGameState.CHOICE_MODE_CHOICES_HIDDEN:
                    return GetHiddenChoiceHelp();
                case AccessibleGameState.SUMMONING_MINION:
                    return GetSummoningMinionHelp();
                case AccessibleGameState.PLAYING_CARD:
                    return GetPlayingCardHelp();
                case AccessibleGameState.CONFIRMING_END_TURN:
                    return GetConfirmingEndTurnHelp();
                case AccessibleGameState.BROWSING_HISTORY:
                    return GetBrowsingHistoryHelp();
                case AccessibleGameState.TRADING_CARD:
                    return GetTradingCardHelp();
                default:
                    return "";
            }
        }

        private string GetWaitingForOpponentHelp()
        {
            return LocalizationUtils.Get(LocalizationKey.GAMEPLAY_WAITING_FOR_OPPONENT);
        }

        protected virtual string GetMulliganHelp()
        {
            return LocalizationUtils.Format(LocalizationKey.GAMEPLAY_MULLIGAN_HELP, AccessibleKey.MULLIGAN_MARK_CARD, AccessibleKey.CONFIRM);
        }

        private string GetWaitingForGameToStartHelp()
        {
            return LocalizationUtils.Get(LocalizationKey.GAMEPLAY_MULLIGAN_HELP_WAITING_FOR_GAME_TO_START);
        }

        private string GetBrowsingHistoryHelp()
        {
            return GetOrNarrateHelpSpeech(AccessibleSpeech.GAMEPLAY_READ_HISTORY_HELP);
        }

        private string GetConfirmingEndTurnHelp()
        {
            return GetOrNarrateHelpSpeech(AccessibleSpeech.GAMEPLAY_CONFIRM_END_TURN_HELP);
        }

        private string GetPlayingCardHelp()
        {
            return GetOrNarrateHelpSpeech(AccessibleSpeech.GAMEPLAY_PLAY_CARD_HELP);
        }

        private string GetTradingCardHelp()
        {
            return GetOrNarrateHelpSpeech(AccessibleSpeech.TUTORIAL_HOGGER_2_5);
        }

        private string GetSummoningMinionHelp()
        {
            return GetOrNarrateHelpSpeech(AccessibleSpeech.GAMEPLAY_SUMMON_MINION_HELP);
        }

        private string GetChoiceModeHelp()
        {
            if (m_waitingForChoiceConfirmation)
            {
                return LocalizedText.GLOBAL_PRESS_ENTER_TO_CONFIRM_OR_BACKSPACE_TO_CANCEL;
            }
            return GetOrNarrateHelpSpeech(AccessibleSpeech.GAMEPLAY_CHOICE_MODE_HELP);
        }

        private string GetHiddenChoiceHelp()
        {
            return LocalizationUtils.Format(LocalizationKey.GAMEPLAY_HIDDEN_CHOICE_HELP, AccessibleKey.READ_NEXT_VALID_ITEM);
        }

        private string GetTargetModeHelp()
        {
            return GetOrNarrateHelpSpeech(AccessibleSpeech.GAMEPLAY_CHOOSE_TARGET_HELP);
        }

        private string GetSubOptionModeHelp()
        {
            return GetOrNarrateHelpSpeech(AccessibleSpeech.GAMEPLAY_CHOICE_MODE_HELP);
        }

        protected virtual string GetMainOptionModeHelp()
        {
            if (m_cardBeingRead != null)
            {
                return NarrateMainOptionWhenCardBeingRead();
            }
            else
            {
                return NarrateMainOption();
            }
        }

        private string NarrateMainOption()
        {
            var speeches = GetMainOptionSpeeches(false);

            return GetOrNarrateHelpSpeeches(speeches);
        }

        private List<HSASpeech> GetMainOptionSpeeches(bool hasReadEndTurn, bool readManaFirst = false)
        {
            var playerHasValidOptions = PlayerHasValidOptions();

            var speeches = new List<HSASpeech>();

            if (!hasReadEndTurn && !playerHasValidOptions)
            {
                speeches.Add(AccessibleSpeech.GAMEPLAY_END_TURN_HELP);
            }

            if (readManaFirst)
            {
                speeches.Add(AccessibleSpeech.GAMEPLAY_READ_PLAYER_MANA_HELP);
            }

            if (PlayerHasValidOptions())
            {
                speeches.Add(AccessibleSpeech.GAMEPLAY_SEE_VALID_OPTIONS);
            }

            if (GameState.Get().GetFriendlySidePlayer().GetHandZone().GetCardCount() > 0)
            {
                speeches.Add(AccessibleSpeech.GAMEPLAY_SEE_PLAYER_HAND_HELP);
            }

            if (GameState.Get().GetFriendlySidePlayer().GetBattlefieldZone().GetCardCount() > 0)
            {
                speeches.Add(AccessibleSpeech.GAMEPLAY_SEE_PLAYER_MINIONS_HELP);
            }

            if (GameState.Get().GetFriendlySidePlayer().GetSecretZone().GetCardCount() > 0)
            {
                speeches.Add(AccessibleSpeech.GAMEPLAY_SEE_PLAYER_SECRETS_HELP);
            }

            speeches.Add(AccessibleSpeech.GAMEPLAY_SEE_PLAYER_HERO_HELP);

            if (GameState.Get().GetFriendlySidePlayer().GetHeroPower() != null)
            {
                speeches.Add(AccessibleSpeech.GAMEPLAY_READ_PLAYER_HERO_POWER_HELP);
            }

            if (GameState.Get().GetOpposingSidePlayer().GetHeroPower() != null)
            {
                speeches.Add(AccessibleSpeech.GAMEPLAY_READ_OPPONENT_HERO_POWER_HELP);
            }

            speeches.Add(AccessibleSpeech.GAMEPLAY_SEE_OPPONENT_HERO_HELP);

            if (GameState.Get().GetOpposingSidePlayer().GetBattlefieldZone().GetCardCount() > 0)
            {
                speeches.Add(AccessibleSpeech.GAMEPLAY_SEE_OPPONENT_MINIONS_HELP);
            }

            // Counts
            if (!IsPlayingTutorial())
            {
                speeches.Add(AccessibleSpeech.GAMEPLAY_COUNT_PLAYER_DECK_HELP);
                speeches.Add(AccessibleSpeech.GAMEPLAY_COUNT_OPPONENT_DECK_HELP);
                speeches.Add(AccessibleSpeech.GAMEPLAY_COUNT_OPPONENT_HAND_HELP);
                speeches.Add(AccessibleSpeech.GAMEPLAY_COUNT_OPPONENT_SECRETS_HELP);
            }

            if (!readManaFirst)
            {
                speeches.Add(AccessibleSpeech.GAMEPLAY_READ_PLAYER_MANA_HELP);
            }

            if (!IsPlayingTutorial())
            {
                speeches.Add(AccessibleSpeech.GAMEPLAY_READ_OPPONENT_MANA_HELP);
                speeches.Add(AccessibleSpeech.GAMEPLAY_OPEN_HISTORY_LOG_HELP);
            }

            return speeches;
        }

        private bool IsPlayingTutorial()
        {
            var gameEntity = GameState.Get().GetGameEntity();

            return gameEntity.GetType().IsSubclassOf(typeof(TutorialEntity));
        }

        private bool PlayerHasValidOptions()
        {
            var candidates = GetCandidateOptions();
            return GetValidOptions(candidates).Count > 0;
        }

        private string NarrateMainOptionWhenCardBeingRead()
        {
            var card = m_cardBeingRead.GetCard();
            var playerHand = GameState.Get().GetFriendlySidePlayer().GetHandZone();
            var playerBattlefield = GameState.Get().GetFriendlySidePlayer().GetBattlefieldZone();
            var playerHero = GameState.Get().GetFriendlySidePlayer().GetHeroCard();
            var playerHeroPower = GameState.Get().GetFriendlySidePlayer().GetHeroPowerCard();

            var speeches = new List<HSASpeech>();

            var hasReadEndTurn = false;

            if (!PlayerHasValidOptions())
            {
                speeches.Add(AccessibleSpeech.GAMEPLAY_END_TURN_HELP);
                hasReadEndTurn = true;
            }

            speeches.Add(AccessibleSpeech.GAMEPLAY_READ_CARD_HELP);

            if (TooltipPanelManager.Get()?.GetTooltipPanels()?.Count > 0 || TutorialKeywordManager.Get()?.GetPanels()?.Count > 0)
            {
                speeches.Add(AccessibleSpeech.GAMEPLAY_READ_CARD_TOOLTIP_HELP);
            }

            var readManaFirst = false;

            if (IsValidOption(card))
            {
                if (card.GetAccessibleZone() == playerHand)
                {
                    speeches.Add(AccessibleSpeech.GAMEPLAY_PLAY_CARD_HELP);

                    if (card.GetEntity().IsTradeable())
                    {
                        speeches.Add(AccessibleSpeech.GAMEPLAY_TRADE_CARD_HELP);
                    }
                }
                else if (card.GetAccessibleZone() == playerBattlefield)
                {
                    speeches.Add(AccessibleSpeech.GAMEPLAY_ATTACK_WITH_MINION_HELP);
                }
                else if (card == playerHero)
                {
                    speeches.Add(AccessibleSpeech.GAMEPLAY_ATTACK_WITH_HERO_HELP);
                }
                else if (card == playerHeroPower)
                {
                    speeches.Add(AccessibleSpeech.GAMEPLAY_USE_HERO_POWER_HELP);
                }
            }
            else
            {
                if (card.GetAccessibleZone() == playerHand)
                {
                    readManaFirst = true;
                }
            }

            speeches.AddRange(GetMainOptionSpeeches(hasReadEndTurn, readManaFirst));

            return GetOrNarrateHelpSpeeches(speeches);
        }

        protected virtual string GetOpponentTurnHelp()
        {
            return GetOrNarrateHelpSpeech(AccessibleSpeech.GAMEPLAY_OPPONENT_TURN_VOICE);
        }

        private string GetOrNarrateHelpSpeech(HSASpeech speech)
        {
            return speech.GetLocalizedText();
        }

        private string GetOrNarrateHelpSpeeches(List<HSASpeech> speeches)
        {
            var lines = new List<string>(speeches.Count);
            speeches.ForEach(s => lines.Add(s.GetLocalizedText()));

            return AccessibleSpeechUtils.CombineLines(lines);
        }

        #endregion Help

        public void OnGainedFocus()
        {
            // TODO: Think about this but we probably don't want to say anything given that no one will forget they're playing a game
        }

        #region Tutorial stuff
        private Dictionary<IZone, Action> m_zoneSelectedListeners = new Dictionary<IZone, Action>();
        private Dictionary<Card, Action<AccessibleCard>> m_cardSelectedListeners = new Dictionary<Card, Action<AccessibleCard>>();
        private Dictionary<Card, Action> m_summoningMinionListeners = new Dictionary<Card, Action>();

        private void OnZoneSelected(IZone zone)
        {
            if (m_zoneSelectedListeners.ContainsKey(zone))
            {
                m_zoneSelectedListeners[zone]();
                m_zoneSelectedListeners.Remove(zone);
            }
        }

        private void OnCardSelected(Card card, AccessibleCard accessibleCard)
        {
            OnZoneSelected(card.GetAccessibleZone());

            if (m_cardSelectedListeners.ContainsKey(card))
            {
                m_cardSelectedListeners[card](accessibleCard);
                m_cardSelectedListeners.Remove(card);
            }

            OnCardHovered(card);
        }

        private void OnSummoningMinion(Card card)
        {
            if (m_summoningMinionListeners.ContainsKey(card))
            {
                m_summoningMinionListeners[card]();
                m_summoningMinionListeners.Remove(card);
            }
        }

        internal void RegisterZoneSelectedListener(Zone zone, Action action)
        {
            m_zoneSelectedListeners[zone] = action;
        }

        internal void RegisterCardSelectedListener(Card card, Action<AccessibleCard> action)
        {
            m_cardSelectedListeners[card] = action;
        }

        internal void RegisterSummoningCardListener(Card card, Action action)
        {
            m_summoningMinionListeners[card] = action;
        }

        private Action m_noValidPlaysListener;

        internal void RegisterNoValidPlaysListener(Action action)
        {
            m_noValidPlaysListener = action;
        }

        private void OnNoValidPlays()
        {
            AccessibilityMgr.Output(this, LocalizationUtils.Get(LocalizationKey.GAMEPLAY_NO_VALID_PLAYS));
            StopReadingCard();

            if (m_noValidPlaysListener != null)
            {
                m_noValidPlaysListener();
                m_noValidPlaysListener = null;
            }
        }

        private bool m_stopHidingMouse;

        // Useful for e.g. pack opening in the tutorial
        internal void StopHidingMouse()
        {
            m_stopHidingMouse = true;
        }

        internal void StartHidingMouse()
        {
            m_stopHidingMouse = true;
        }
        #endregion

        #region Emotes
        private AccessibleMenu m_accessibleEmotes;
        private AccessibleMenu m_accessibleEnemyEmotes;

        internal void OnEmotesShown(List<EmoteOption> availableEmotes)
        {
            m_accessibleEmotes = new AccessibleMenu(this, "", EmoteHandler.Get().HideEmotes, false, true);

            foreach (var emote in availableEmotes)
            {
                m_accessibleEmotes.AddOption(emote.m_Text.Text, emote.DoClick);
            }

            m_accessibleEmotes.StartReading();
        }

        internal void OnEnemyEmotesShown(string curSquelchText)
        {
            m_accessibleEnemyEmotes = new AccessibleMenu(this, "", EnemyEmoteHandler.Get().HideEmotes, false, true);

            m_accessibleEnemyEmotes.AddOption(curSquelchText, () => EnemyEmoteHandler.Get().DoSquelchClick());

            m_accessibleEnemyEmotes.StartReading();
        }

        private bool HandleEmotes()
        {
            if (EmoteHandler.Get()?.AreEmotesActive() ?? false)
            {
                m_accessibleEmotes?.HandleAccessibleInput();
                return true;
            }
            else if (EnemyEmoteHandler.Get()?.AreEmotesActive() ?? false)
            {
                m_accessibleEnemyEmotes?.HandleAccessibleInput();
                return true;
            }
            else if (AccessibleKey.SPACE.IsPressed())
            {
                if (m_cardBeingRead == null)
                {
                    return false;
                }

                if (GameMgr.Get().IsSpectator())
                {
                    return false;
                }

                var entity = m_cardBeingRead.GetCard().GetEntity();

                if (!entity.IsHero() || entity.GetZone() != TAG_ZONE.PLAY)
                {
                    return false;
                }

                if (entity.IsControlledByFriendlySidePlayer())
                {
                    EmoteHandler.Get()?.ShowEmotes();
                    return true;
                }
                else
                {
                    EnemyEmoteHandler.Get()?.ShowEmotes();
                    return true;
                }
            }

            return false;
        }
        #endregion

        #region Battlegrounds-only

        internal virtual void OnBattlegroundsCombatPhasePopupShown()
        {
            // No-op
        }

        internal virtual void OnBattlegroundsShopPhasePopupShown()
        {
            // No-op
        }

        internal virtual void OnFreezeOrUnfreezeEvent()
        {
            // No-op
        }

        internal virtual void OnOpponentHeroChanged()
        {
            // No-op
        }

        internal virtual void OnAnyHeroGainedAtk()
        {
            // No-op
        }

        internal virtual void OnPlayerAvailableResourcesChanged(int before, int after)
        {
            // No-op
        }

        internal virtual void OnTurnEnded()
        {
            // No-op
        }

        internal virtual void OnFirstTaskListStart()
        {
            // No-op
        }

        internal virtual void OnMainStepEnd()
        {
            // No-op
        }

        internal virtual void OnEnterMultiplayerWaitingArea(List<Card> startingCards, string mulliganBannerText, string mulliganBannerSubtitleText, NormalButton confirmButton)
        {
            // No-op
        }

        internal virtual void OnMainActionStep()
        {
            // No-op
        }

        protected virtual void OnCardHovered(Card card)
        {
            // No-op
        }

        #endregion
    }
}
