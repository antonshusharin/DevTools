using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace Accessibility
{
    class AccessibleBattlegroundsGameplay : AccessibleGameplay
    {
        private bool m_refreshingTavern;

        private bool m_freezingTavern;
        private bool m_unfreezingTavern;

        private bool m_tavernFrozen;

        private bool m_upgradingTavern;

        private bool m_movingMinion;
        private bool m_movingMinionWaitingForHold; // Used to prevent "Sell?" while we wait for the minion to be held

        private bool m_buyingCard;
        private bool m_sellingMinion;

        private bool m_tradingCard;
        private bool m_tradingCardWaitingForHold;

        private bool m_passingCard;
        private bool m_passingCardWaitingForHold;
        private bool m_viewingTeammatesBoard;

        private bool m_isShopPhase;
        private bool m_isCombatPhase;

        private bool m_hasAnyPlayerWonThisPhase; // Used to output ties when no player won a combat phase

        private bool m_wasShopPhasePopupShown; // Used for reconnects

        private AccessibleInGameState m_lastDescribedState; // Used for storing the state of the board (i.e. Entity clones) at the start of a combat phase

        private int m_numTeamsAlive; // Used for saying x players died at the start of recruit phase

        private AccessibleListOfItems<AccessibleBattlegroundsCard> m_teammatesChoiceCards;

		internal override void OnBattlegroundsCombatPhasePopupShown()
		{
            ResetStateBetweenPhases();
			AccessibilityMgr.Output(this, LocalizationUtils.Get(LocalizationKey.BATTLEGROUNDS_GAMEPLAY_COMBAT_PHASE));
            StopReadingCard();
		}

		private void OnBattlegroundsCombatPhaseStart()
		{
            if (m_isCombatPhase)
			{
                // Kel'Thuzad would read twice as Bob transforms into the real Hero first and into Kel'Thuzad afterwards
                return;
			}

            m_isCombatPhase = true;
            m_hasAnyPlayerWonThisPhase = false;
            m_lastDescribedState = AccessiblePowerTaskListDescriber.Get().GetLastDescribedState();

			var opponent = GameState.Get().GetOpposingSidePlayer();

			var opponentHero = opponent.GetHero();
            var opponentTier = GetOpponentTavernTier(opponent);
            var opponentHealth = GetOpponentHealth(opponent);
            var opponentArmor = GetOpponentArmor(opponent);
            var opponentBattlefield = opponent.GetBattlefieldZone();
            var opponentMinions = opponentBattlefield.GetCards();
            var numOpponentMinions = opponentBattlefield.GetCardCount();

			var opponentHeroName = opponentHero.GetName();

            if (IsOpponentDead(opponent))
			{
				AccessibilityMgr.Output(this, LocalizationUtils.Format(LocalizationKey.BATTLEGROUNDS_GAMEPLAY_COMBAT_PHASE_START_DESCRIBE_AGAINST_DEAD_PLAYER,
                    opponentHeroName
				));
			}
            else if (opponentArmor > 0)
			{
				AccessibilityMgr.Output(this, LocalizationUtils.Format(LocalizationKey.BATTLEGROUNDS_GAMEPLAY_COMBAT_PHASE_START_DESCRIBE_INCLUDE_ARMOR,
                    opponentTier, opponentHeroName, opponentHealth, numOpponentMinions, opponentArmor
				));
			}
			else
			{
				AccessibilityMgr.Output(this, LocalizationUtils.Format(LocalizationKey.BATTLEGROUNDS_GAMEPLAY_COMBAT_PHASE_START_DESCRIBE,
                    opponentTier, opponentHeroName, opponentHealth, numOpponentMinions
			    ));
			}

            ReadOpponentMinions(opponentMinions);
		}

		private int GetOpponentHealth(Player opponent)
		{
            // Edge cases around Tutorial vs PVP

            var nextOpponentTile = PlayerLeaderboardManager.Get().GetNextOpponentTile();

            if (nextOpponentTile == null)
			{
                return opponent.GetHero().GetCurrentHealth();
			}
            else
			{
                return nextOpponentTile.m_playerHeroEntity.GetCurrentHealth();
			}
		}

		private int GetOpponentArmor(Player opponent)
		{
            // Edge cases around Tutorial vs PVP

            var nextOpponentTile = PlayerLeaderboardManager.Get().GetNextOpponentTile();

            if (nextOpponentTile == null)
			{
                return opponent.GetHero().GetArmor();
			}
            else
			{
                return nextOpponentTile.m_playerHeroEntity.GetArmor();
			}
		}

		private int GetOpponentTavernTier(Player opponent)
		{
            // Edge cases around Tutorial vs PVP

            var nextOpponentTile = PlayerLeaderboardManager.Get().GetNextOpponentTile();

            if (nextOpponentTile == null)
			{
                return opponent.GetHero().GetRealTimePlayerTechLevel();
			}
            else
			{
                return nextOpponentTile.m_playerHeroEntity.GetRealTimePlayerTechLevel();
			}
		}

		private bool IsOpponentDead(Player opponent)
		{
            // Edge cases around Tutorial vs PVP

            var nextOpponentTile = PlayerLeaderboardManager.Get().GetNextOpponentTile();

            if (nextOpponentTile == null)
			{
                return opponent.GetHero().GetCurrentHealth() <= 0;
			}
            else
			{
                return nextOpponentTile.m_playerHeroEntity.GetCurrentHealth() <= 0;
			}
		}

		private void ReadOpponentMinions(List<Card> opponentMinions)
		{
            var minionSummaries = new List<string>();

			foreach (var card in opponentMinions)
			{
                var accessibleCard = new AccessibleBattlegroundsCard(this, card);

                minionSummaries.Add(accessibleCard.GetMinionSummary());
			}

            AccessibilityMgr.Output(this, AccessibleSpeechUtils.HumanizeList(minionSummaries));
		}

		internal override void OnBattlegroundsShopPhasePopupShown()
		{
            m_isCombatPhase = false;
            m_isShopPhase = false;
            m_tavernFrozen = false;

            // Prevent your opponent's hero became Bob speeches
            AccessiblePowerTaskListDescriber.Get().OnBattlegroundsHeroAttackPhaseEnd();

            if (!m_wasShopPhasePopupShown)
			{
				AccessibilityMgr.Output(this, LocalizationUtils.Get(LocalizationKey.BATTLEGROUNDS_GAMEPLAY_RECRUIT_PHASE));

                var numTeamsAlive = AccessibleBattlegroundsLeaderboardMgr.Get().GetNumTeamsAlive();

                if (numTeamsAlive < m_numTeamsAlive)
				{
                    var numTeamsDied = m_numTeamsAlive - numTeamsAlive;
					AccessibilityMgr.Output(this, LocalizationUtils.Format(LocalizationKey.BATTLEGROUNDS_GAMEPLAY_COMBAT_PHASE_ENDED_N_PLAYERS_DIED, numTeamsDied));
					AccessibilityMgr.Output(this, LocalizationUtils.Format(LocalizationKey.BATTLEGROUNDS_GAMEPLAY_COMBAT_PHASE_ENDED_N_PLAYERS_REMAINING, numTeamsAlive));
				}

                m_numTeamsAlive = numTeamsAlive;
			}

			m_wasShopPhasePopupShown = true;
            StopReadingCard();
		}

		private void OnBattlegroundsShopPhaseStart()
		{
            m_isShopPhase = true;
			var player = GameState.Get().GetFriendlySidePlayer();

            if (player != null)
			{
                // Reconnects start before the player is set
				var gold = player.GetNumAvailableResources();
                var tavernTier = GetTavernTier(player);

				AccessibilityMgr.Output(this, LocalizationUtils.Format(LocalizationKey.BATTLEGROUNDS_GAMEPLAY_RECRUIT_PHASE_START_DESCRIBE, tavernTier, gold));
			}

            // Read Bob's minions
			var bob = GameState.Get().GetOpposingSidePlayer();
            var minionsForSale = bob.GetBattlefieldZone().GetCards();

            if (minionsForSale.Count > 0)
            {
                var minionNames = new List<string>();

                foreach (var minion in minionsForSale)
				{
                    minionNames.Add(AccessibleSpeechUtils.GetEntityName(minion.GetEntity()));
				}

                var newMinionsInShopMessage = AccessibleSpeechUtils.FormatZoneMovementText(minionNames,
                    LocalizationKey.BATTLEGROUNDS_GAMEPLAY_DIFF_MOVEMENT_CARD_ADDED_TO_TAVERN,
                    LocalizationKey.BATTLEGROUNDS_GAMEPLAY_DIFF_MOVEMENT_CARDS_ADDED_TO_TAVERN
				);

                AccessibilityMgr.Output(this, newMinionsInShopMessage);
            }
		}

        private void ResetState()
		{
            m_freezingTavern = false;
            m_unfreezingTavern = false;
            m_refreshingTavern = false;
            m_upgradingTavern = false;
            m_rerollingHero = false;
            m_movingMinion = false;
            m_movingMinionWaitingForHold = false;
            m_buyingCard = false;
            m_sellingMinion = false;
            m_tradingCard = false;
            m_tradingCardWaitingForHold = false;
            m_passingCard = false;
            m_passingCardWaitingForHold = false;
		}

		internal override void ReadPlayerResources()
		{
            if (m_viewingTeammatesBoard)
            {
                var viewer = TeammateBoardViewer.Get().GetViewer<TeammateGameModeButtonViewer>();
                int teammateGold = viewer.GetTeammateGold();
                int teammateMaxGold = viewer.GetTeammateMaxGold();
                if (teammateGold != teammateMaxGold)
                {
                    AccessibilityMgr.Output(this, LocalizationUtils.Format(LocalizationKey.BATTLEGROUNDS_GAMEPLAY_READ_PLAYER_GOLD_CURRENT_AND_TOTAL, teammateGold, teammateMaxGold));
                }
                else
                {
                    AccessibilityMgr.Output(this, LocalizationUtils.Format(LocalizationKey.BATTLEGROUNDS_GAMEPLAY_READ_PLAYER_GOLD, teammateGold));
                }
                return;
            }

            var player = GameState.Get().GetFriendlySidePlayer();
            int availableGold = player.GetNumAvailableResources();
            int totalGold = player.GetTag(GAME_TAG.RESOURCES);

            if (availableGold != totalGold)
            {
                AccessibilityMgr.Output(this, LocalizationUtils.Format(LocalizationKey.BATTLEGROUNDS_GAMEPLAY_READ_PLAYER_GOLD_CURRENT_AND_TOTAL, availableGold, totalGold));
            }
            else
            {
                ReadAvailablePlayerResources();
            }
		}

		private void ReadAvailablePlayerResources()
		{
            int curGold = GameState.Get().GetFriendlySidePlayer().GetNumAvailableResources();
			AccessibilityMgr.Output(this, LocalizationUtils.Format(LocalizationKey.BATTLEGROUNDS_GAMEPLAY_READ_PLAYER_GOLD, curGold));
		}

		private void ReadRemainingPlayerResources()
		{
            int curGold = GameState.Get().GetFriendlySidePlayer().GetNumAvailableResources();
			AccessibilityMgr.Output(this, LocalizationUtils.Format(LocalizationKey.BATTLEGROUNDS_GAMEPLAY_READ_REMAINING_GOLD, curGold));
		}

		public override void HandleInput()
        {
            if (GameState.Get() == null || InputManager.Get() == null)
            {
                // Game hasn't even started yet
                return;
            }

            if (GameState.Get().IsMulliganPhase())
			{
                m_curPhase = AccessibleGamePhase.MULLIGAN;
			}
            else if (GameState.Get().IsGameOver())
            {
                m_curPhase = AccessibleGamePhase.GAME_OVER;
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

            if (m_curPhase == AccessibleGamePhase.PLAYING)
			{
                HandleInGameInput();
			}
            else if (m_curPhase == AccessibleGamePhase.MULLIGAN)
			{
                HandleChooseHeroInput();
			}
        }

        private void HandleInGameInput()
        {
            if (m_justReconnected)
            {
                m_justReconnected = false;

                if (AccessibleGameplayUtils.IsInBattlegroundsShopPhase())
				{
                    OnBattlegroundsShopPhaseStart();
				}
                else if (AccessibleGameplayUtils.IsInBattlegroundsCombatPhase())
				{
                    OnBattlegroundsCombatPhaseStart();
				}

                GameState.Get().RegisterGameOverListener(OnGameOver);
                AccessiblePowerTaskListDescriber.Get().OnReconnected();
            }

            UpdateState();

            try
            {
                UpdateMousePosition();
            }
            catch (Exception e)
            {
                AccessibilityUtils.LogFatalError(e);
                StopReadingCard();
            }

            if (AccessibleGameplayUtils.IsInBattlegroundsShopPhase())
			{
                HandleShopPhaseInput();
			}
            else if (AccessibleGameplayUtils.IsInBattlegroundsCombatPhase())
			{
                HandleCombatPhaseInput();
			}
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

            // Note: We can't hide the mouse ever or refreshes etc will stop working
		}

		private void HandleShopPhaseInput()
		{
            HandleGeneralShopPhaseInput();

            // Give pings priority, otherwise they get clobbered by virtual queries.
            HandlePingCardInput();

            // Handle virtual queries first so we can smoothly click a different key to cancel by reading something else simultaneously
            if (m_upgradingTavern && HandleUpgradingTavern())
			{
                return;
			}
            else if (m_freezingTavern && HandleFreezingTavern())
			{
                return;
			}
            else if (m_unfreezingTavern && HandleUnfreezingTavern())
			{
                return;
			}
            else if (m_refreshingTavern && HandleRefreshTavern())
			{
                return;
			}

            if (GameMgr.Get().IsBattlegroundDuoGame() && m_heldCard == null) HandleDuosPortalInput();

            switch(m_curState)
            {
                case AccessibleGameState.MAIN_OPTION_MODE:
                    HandleCardReadingInput();
                    HandleCheckStatusKeys();
                    HandleMainOptionMode();
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
                    HandlePassCardWhenHoldingCardInput();
                    return;
                case AccessibleGameState.BUYING_CARD:
                    HandleCheckStatusKeys();
                    HandleBuyingCard();
                    return;
                case AccessibleGameState.SELLING_MINION:
                    HandleCheckStatusKeys();
                    HandleSellingMinion();
                    return;
                case AccessibleGameState.MOVING_MINION:
                    HandleCheckStatusKeys();
                    HandleMovingMinion();
                    return;
                case AccessibleGameState.PLAYING_CARD:
                    HandleCardReadingInput();
                    HandleCheckStatusKeys();
                    HandlePlayingCard();
                    HandleTradeCardWhenHoldingCardInput();
                    HandlePassCardWhenHoldingCardInput();
                    return;
                case AccessibleGameState.TRADING_CARD:
                    HandleCheckStatusKeys();
                    HandleTradingCard();
                    return;
                case AccessibleGameState.READING_LEADERBOARD:
                    HandleReadLeaderboardInput();
                    return;
                    case AccessibleGameState.PASSING_CARD:
                    HandleCheckStatusKeys();
                    HandlePassingCard();
                    return;
                    case AccessibleGameState.VIEWING_TEAMMATES_BOARD:
                    case AccessibleGameState.TEAMMATES_CHOICES_HIDDEN:
                    HandleCardReadingInput();
                    HandleCheckStatusKeys();
                    HandleViewingTeammatesBoard();
                    return;
                    case AccessibleGameState.VIEWING_TEAMMATES_CHOICES:
                    HandleCheckStatusKeys();
                    HandleViewingTeammatesChoices();
                    return;
                default:
                    return;
            }
		}

        private void HandleDuosPortalInput()
        {
            if (AccessibleKey.BATTLEGROUNDS_DUOS_SWITCH_TO_TEAMMATES_BOARD.IsPressed())
            {
                ClickDuosPortal();
            }
        }

        private void ClickDuosPortal()
        {
            var teammateBoardViewer = TeammateBoardViewer.Get();
            var duosPortal = teammateBoardViewer?.GetDuosPortal();
            if (duosPortal == null)
            {
                return;
            }

            var wasViewingTeammate = teammateBoardViewer.IsViewingTeammate();
            duosPortal.PortalPushed();

            // Use current state when available; otherwise fall back to expected toggled state.
            var isViewingTeammate = teammateBoardViewer.IsViewingTeammate();
            var switchedToTeammateBoard = (isViewingTeammate == wasViewingTeammate) ? !wasViewingTeammate : isViewingTeammate;
            var key = switchedToTeammateBoard
                ? LocalizationKey.BATTLEGROUNDS_DUOS_GAMEPLAY_SWITCH_TO_TEAMMATES_BOARD
                : LocalizationKey.BATTLEGROUNDS_DUOS_GAMEPLAY_SWITCH_TO_PLAYERS_BOARD;
            AccessibilityMgr.Output(this, LocalizationUtils.Get(key));
        }

        internal override void HandleCheckStatusKeys()
        {
            base.HandleCheckStatusKeys();

            if (AccessibleKey.BATTLEGROUNDS_READ_NEXT_OPPONENT_STATS.IsPressed())
            {
                AccessibleBattlegroundsLeaderboardMgr.Get().ReadNextOpponent();
            }
            else if (AccessibleKey.BATTLEGROUNDS_READ_MY_STATS.IsPressed())
			{
                AccessibleBattlegroundsLeaderboardMgr.Get().ReadMyself();
			}
            else if (AccessibleKey.BATTLEGROUNDS_READ_NEXT_OPPONENT_STATS_TO_END.IsPressed())
            {
                AccessibleBattlegroundsLeaderboardMgr.Get().ReadNextOpponentToEnd();
            }
            else if (AccessibleKey.BATTLEGROUNDS_READ_MY_STATS_TO_END.IsPressed())
			{
                AccessibleBattlegroundsLeaderboardMgr.Get().ReadMyselfToEnd();
			}
            else if (AccessibleKey.BATTLEGROUNDS_READ_LEADERBOARD.IsPressed())
			{
                AccessibleBattlegroundsLeaderboardMgr.Get().StartReadingLeaderboard();
			}
            else if (AccessibleKey.BATTLEGROUNDS_READ_RACES_IN_GAME.IsPressed())
			{
                ReadRacesAndAnomaly();
			}
            else if (AccessibleKey.BATTLEGROUNDS_READ_HERO_BUDDY.IsPressed())
			{
                ReadHeroBuddy();
			}
            else if (AccessibleKey.BATTLEGROUNDS_READ_TRINKETS.IsPressed())
            {
                ReadTrinkets(Player.Side.FRIENDLY);
            }
        }

		internal AccessibleInGameState GetStateAtStartOfCombat()
		{
            return m_lastDescribedState;
		}

		private void ReadHeroBuddy()
        {
            if (!GameState.Get().BattlegroundAllowBuddies())
            {
                return;
            }
            var heroBuddyCard = GetHeroBuddyCard();

            if (heroBuddyCard == null)
            {
                return;
            }

            m_cardBeingRead = new AccessibleBattlegroundsHeroBuddyCard(this, heroBuddyCard, GetPlayerHero());
            m_curZone = heroBuddyCard.GetAccessibleZone();
            m_cardBeingRead.ReadLine();
        }

        private void ReadTrinkets(Player.Side side)
        {
            if (!GameState.Get().BattlegroundsAllowTrinkets())
            {
                return;
            }
            VirtualZone zone = GetTrinketsZone(side);
            if (zone == null || zone.GetCardCount() == 0)
            {
                return;
            }
            SeeZone(zone);
        }

        private VirtualZone GetTrinketsZone(Player.Side side)
        {
            // Normally every trinket gets its own individual zone, but we put them into a single virtual zone to avoid introducing special case code in AccessibleGameplay.

            if (m_viewingTeammatesBoard && side == Player.Side.FRIENDLY)
            {
                return TeammateBoardViewer.Get().GetViewer<TeammateHeroViewer>().GetTrinketVirtualZone();
            }

            var ret = new VirtualZone();
            foreach (var zone in ZoneMgr.Get().FindZonesOfType<ZoneBattlegroundTrinket>(side))
            {
                var card = zone.GetFirstCard();
                if (card != null)
                {
                    ret.UpdateCardPosition(card, zone.slot);
                }
            }
            return ret;
        }


		private void ReadRacesAndAnomaly()
		{
            var racesInGame = TB_BaconShop.GetAvailableRacesText();
            if (racesInGame != null)   AccessibilityMgr.Output(this, racesInGame);
            var anomalyText = GetAnomalyText();
            if (anomalyText != null)   AccessibilityMgr.Output(this, anomalyText);
		}

        private string GetAnomalyText()
        {
            var anomalyId = GameState.Get().GetGameEntity().GetAnomalyId();
            if (anomalyId == 0)
            {
                return null;
            }

            if (GameState.Get().IsMulliganManagerActive())
            {
                return AccessibleCardUtils.GetAnomalyText(anomalyId);
            }

            var anomalyCard = ZoneMgr.Get().FindZoneOfType<ZoneBattlegroundAnomaly>(Player.Side.NEUTRAL)?.GetFirstCard();
            if (anomalyCard == null)
            {
                return null;
            }

            return LocalizationUtils.Format(LocalizationKey.BATTLEGROUNDS_GAMEPLAY_READ_ANOMALY, anomalyCard.GetEntity().GetName(), anomalyCard.GetEntity().GetCardTextInHand());
        }

		private void HandleGeneralShopPhaseInput()
		{
            if (AccessibleKey.END_TURN.IsPressed())
			{
                var secondsRemaining = TurnTimer.Get().GetSecondsRemaining();

                if (secondsRemaining > 0)
				{
					AccessibilityUtils.OutputSecondsRemaining(secondsRemaining);
				}
			}
		}

		private void HandleBuyingCard()
		{
            AccessibleInputMgr.MoveMouseTo(GameState.Get().GetFriendlySidePlayer().GetHeroCard());

            HandleForcedConfirmOrCancel(ConfirmActionByClicking, CancelActionWithHeldCard);
		}

		private void HandleSellingMinion()
		{
            AccessibleInputMgr.MoveMouseTo(GameState.Get().GetOpposingSidePlayer().GetHeroCard());

            HandleForcedConfirmOrCancel(ConfirmActionByClicking, CancelActionWithHeldCard);
		}

        private void HandlePassingCard()
        {
            AccessibleInputMgr.MoveMouseTo(TeammateBoardViewer.Get().GetDuosPortal());

            HandleForcedConfirmOrCancel(ConfirmActionByClicking, CancelActionWithHeldCard, true);
        }

        private void HandleTradingCard()
        {
            var collider = Board.Get().GetDeckActionArea();

            if (collider != null)
            {
                var cardBounds = m_heldCard.GetActor().GetMeshRenderer().bounds;
                var tradeAreaCenter = collider.bounds.ClosestPoint(m_heldCard.gameObject.transform.position);
                var target = tradeAreaCenter;
                target.x += cardBounds.size.x / 2;
                AccessibleInputMgr.MoveMouseToWorldPosition(target);
            }

            HandleForcedConfirmOrCancel(ConfirmActionByClicking, CancelActionWithHeldCard);
        }

		private bool HandleFreezingTavern()
		{
            return HandleSmoothConfirmOrCancel(ForceFreezeOrUnfreezeTavern, ResetState, AccessibleKey.FREEZE_TAVERN);
		}

		private bool HandleUnfreezingTavern()
		{
            return HandleSmoothConfirmOrCancel(ForceFreezeOrUnfreezeTavern, ResetState, AccessibleKey.FREEZE_TAVERN);
		}

		private bool HandleUpgradingTavern()
		{
            return HandleSmoothConfirmOrCancel(ForceUpgradeTavern, ResetState, AccessibleKey.UPGRADE_TAVERN);
		}

		internal bool IsInShopPhase()
		{
            return m_isShopPhase;
		}

		internal bool IsInCombatPhase()
		{
            return m_isCombatPhase;
		}

		private bool HandleRefreshTavern()
		{
			return HandleSmoothConfirmOrCancel(ForceRefreshTavern, ResetState, AccessibleKey.REFRESH_TAVERN);
		}

		private bool HandleReadLeaderboardInput()
		{
            return AccessibleBattlegroundsLeaderboardMgr.Get().HandleAccessibleInput();
		}

        private void ConfirmActionByClicking()
		{
			ResetState();
			AccessibleInputMgr.ClickLeftMouseButton();
		}

        private void CancelActionWithHeldCard()
		{
            ResetState();
            AccessibleInputMgr.ClickRightMouseButton();
		}

		private void HandleForcedConfirmOrCancel(Action confirmAction, Action cancelAction, bool includeSpaceAsWell=false)
        {
            if (AccessibleKey.CONFIRM.IsPressed() ||
                (includeSpaceAsWell && AccessibleKey.SPACE.IsPressed()))
            {
                confirmAction();
			}
			else if (AccessibleKey.BACK.IsPressed())
            {
                cancelAction();
            }
        }

		private bool HandleSmoothConfirmOrCancel(Action confirmAction, Action cancelAction, AccessibleKey originalKey)
        {
            // Note: OriginalKey is used to make it so e.g. pressing U twice forces a tavern upgrade the same way that U+Enter would as players
            // are used to this due to the end turn behaviour
            if (AccessibleKey.CONFIRM.IsPressed() || originalKey.IsPressed())
            {
                confirmAction();
                return true;
			}
			else if (!Input.GetKey(KeyCode.LeftShift) && !Input.GetKey(KeyCode.RightShift) && Input.anyKeyDown) // Ignore Shift for cancelation purposes so that we can still ping game mode buttons
            {
                cancelAction();
            }

            return false;
        }

		private void HandleCombatPhaseInput()
		{
			HandleCardReadingInput();
			HandleCheckStatusKeys();
			HandleZoneInput();
			HandleValidOptionsSelectionInput();
			HandleZoneSelection();

            if (AccessibleKey.BATTLEGROUNDS_READ_OPPONENT_TRINKETS.IsPressed())
            {
                ReadTrinkets(Player.Side.OPPOSING);
            }
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

            ResetChoiceCardsIfNecessary();
            ReadStateAfterClosingLeaderboardIfNecessary();

            if (m_heldCard == null)
            {
                m_movingMinion = false;
                m_tradingCard = false;
                m_passingCard = false;
            }
            else if (m_movingMinionWaitingForHold)
			{
                m_movingMinionWaitingForHold = false;
                m_movingMinion = true;
			}
            else if (m_passingCardWaitingForHold && CanPassCard(m_heldCard))
            {
                // Additional check via CanPassCard to prevent race conditions around turn transitions.
                m_passingCardWaitingForHold = false;
                m_passingCard = true;
            }
            else if (m_tradingCardWaitingForHold && CanTradeCard(m_heldCard))
            {
                // Additional check via CanTradeCard to prevent race conditions around turn transitions.
                m_tradingCardWaitingForHold = false;
                m_tradingCard = true;
            }

            m_prevState = m_curState;
            m_prevResponseMode = m_curResponseMode;
            m_curResponseMode = GameState.Get().GetResponseMode();
            m_viewingTeammatesBoard = IsViewingTeammatesBoard();

            // Proper states
            if (!m_playerTurn)
            {
                m_curState = AccessibleGameState.WAITING;
            }
            else if (AccessibleBattlegroundsLeaderboardMgr.Get().IsReadingLeaderboard())
            {
                m_curState = AccessibleGameState.READING_LEADERBOARD;
            }
            else if (m_heldCard != null)
            {
                if (m_heldCard.GetEntity().GetCardType() == TAG_CARDTYPE.BATTLEGROUND_SPELL && m_heldCard.GetEntity().GetZone() == TAG_ZONE.PLAY && m_heldCard.GetEntity().IsControlledByOpposingSidePlayer())
                {
                    m_curState = AccessibleGameState.BUYING_CARD;
                    if (m_curState != m_prevState)
                    {
                        var spellName = AccessibleSpeechUtils.GetEntityName(m_heldCard.GetEntity());
                        AccessibilityMgr.Output(this, LocalizationUtils.Format(LocalizationKey.BATTLEGROUNDS_GAMEPLAY_QUERY_BUY_MINION, spellName));
                    }
                }
                else if (m_passingCard)
                {
                    m_curState = AccessibleGameState.PASSING_CARD;
                    if (m_prevState != m_curState)
                    {
                        var cardName = AccessibleSpeechUtils.GetEntityName(m_heldCard.GetEntity());
                        AccessibilityMgr.Output(this, LocalizationUtils.Format(LocalizationKey.BATTLEGROUNDS_DUOS_GAMEPLAY_QUERY_PASS_CARD, cardName));
                    }
                }
                else if (m_tradingCard)
                {
                    m_curState = AccessibleGameState.TRADING_CARD;
                }
                else if (m_heldCard.GetEntity().IsMinion())
                {
                    var entity = m_heldCard.GetEntity();
                    var zone = entity.GetZone();

                    if (zone == TAG_ZONE.PLAY)
					{
                        if (entity.IsControlledByFriendlySidePlayer() && m_movingMinion)
						{
                            m_curState = AccessibleGameState.MOVING_MINION;
						}
                        else if (entity.IsControlledByFriendlySidePlayer() && m_sellingMinion)
						{
							m_curState = AccessibleGameState.SELLING_MINION;

                            if (m_curState != m_prevState)
							{
                                var minionName = AccessibleSpeechUtils.GetEntityName(entity);
								AccessibilityMgr.Output(this, LocalizationUtils.Format(LocalizationKey.BATTLEGROUNDS_GAMEPLAY_QUERY_SELL_MINION, minionName));
							}
						}
                        else if (m_buyingCard)
						{
							m_curState = AccessibleGameState.BUYING_CARD;

                            if (m_curState != m_prevState)
							{
                                var minionName = AccessibleSpeechUtils.GetEntityName(entity);
								AccessibilityMgr.Output(this, LocalizationUtils.Format(LocalizationKey.BATTLEGROUNDS_GAMEPLAY_QUERY_BUY_MINION, minionName));
							}
						}
					}
                    else if (m_prevState != AccessibleGameState.PASSING_CARD)
					{
						m_curState = AccessibleGameState.SUMMONING_MINION;
					}
                }
                else if (GameState.Get().IsInMainOptionMode())
                {
                    m_curState = AccessibleGameState.PLAYING_CARD;
                }
            }
            else if (m_viewingTeammatesBoard)
            {
                m_curState = AccessibleGameState.VIEWING_TEAMMATES_BOARD;

                var teammateDiscoverViewer = TeammateBoardViewer.Get().GetViewer<TeammateDiscoverViewer>();
                if (teammateDiscoverViewer == null)
                {
                    return;
                }
                if (teammateDiscoverViewer.IsActive())
                {
                    m_curState = teammateDiscoverViewer.IsShowingChoices() ? AccessibleGameState.VIEWING_TEAMMATES_CHOICES : AccessibleGameState.TEAMMATES_CHOICES_HIDDEN;

                    if (m_curState != m_prevState)
                    {
                        if (m_curState == AccessibleGameState.VIEWING_TEAMMATES_CHOICES)
                        {
                            AccessibilityMgr.Output(this, GameStrings.Get("GAMEPLAY_CHOOSE_ONE"));
                            m_teammatesChoiceCards.StartReading();
                        }
                        else if (m_curState == AccessibleGameState.TEAMMATES_CHOICES_HIDDEN)
                        {
                            AccessibilityMgr.Output(this, LocalizationUtils.Get(LocalizationKey.GAMEPLAY_CHOICES_HIDDEN));
                        }
                    }
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
            else if (GameState.Get().IsInTargetMode())
            {
                m_curState = AccessibleGameState.TARGET_MODE;
            }
            else
            {
                // Normally happens in between turns (i.e. after button press but before response) due to network time
                m_curState = AccessibleGameState.UNKNOWN;
            }
		}

        private bool IsViewingTeammatesBoard()
        {
            if (!GameMgr.Get().IsBattlegroundDuoGame())
            {
                return false;
            }
            return TeammateBoardViewer.Get()?.IsViewingTeammate() ?? false;
        }

		private void ReadStateAfterClosingLeaderboardIfNecessary()
		{
            if (m_prevState == m_curState || m_prevState != AccessibleGameState.READING_LEADERBOARD)
			{
                return;
			}

            if (m_curState == AccessibleGameState.CHOICE_MODE || m_curState == AccessibleGameState.SUB_OPTION_MODE)
            {
                m_accessibleChoiceCards.StartReading();
            }

            else if (m_cardBeingRead != null && m_cardBeingRead.GetCard() != null)
			{
                // Reread zone regardless
                var card = m_cardBeingRead.GetCard();
				ReadZoneChangeIfNecessary(card, null, card.GetAccessibleZone(), true);
				m_cardBeingRead.Reset();
                var speech = m_cardBeingRead.GetLine(0);

				if (ShouldReadCardAsList(card))
				{
					var zonePos = card.GetAccessibleZone().FindCardPos(card); // card.GetAccessibleZonePosition() isn't trustworthy. Wasn't working for secrets
					AccessibilityMgr.Output(this, AccessibleSpeech.MENU_OPTION(speech, zonePos, card.GetAccessibleZone().GetCardCount()));
				}
                else
				{
                    AccessibilityMgr.Output(this, speech);
				}
			}
		}

		private void OnCombatPhaseCombatEnded()
		{
            if (m_hasAnyPlayerWonThisPhase)
			{
                // Heroes gain atk twice (one for tavern tier and one for all minions)
                return;
			}

			m_hasAnyPlayerWonThisPhase = true;

			var numPlayerMinions = GameState.Get().GetFriendlySidePlayer().GetBattlefieldZone().GetCardCount();
            var numOpponentMinions = GameState.Get().GetOpposingSidePlayer().GetBattlefieldZone().GetCardCount();

            if (numPlayerMinions > numOpponentMinions)
			{
				AccessibilityMgr.Output(this, LocalizationUtils.Format(LocalizationKey.BATTLEGROUNDS_GAMEPLAY_COMBAT_PHASE_ENDED_PLAYER_WON, numPlayerMinions));
			}
            else if (numOpponentMinions > numPlayerMinions)
			{
				AccessibilityMgr.Output(this, LocalizationUtils.Format(LocalizationKey.BATTLEGROUNDS_GAMEPLAY_COMBAT_PHASE_ENDED_OPPONENT_WON, numOpponentMinions));
			}

            // Note: ties are narrated in OnMainStepEnd as we have no proper way of knowing before then i.e. OnCombatPhaseCombatEnded is not invoked if no Hero launches
            // an attack
		}

		protected override void ReadOpponentZoneName()
		{
            if (AccessibleGameplayUtils.IsInBattlegroundsShopPhase())
			{
				AccessibilityMgr.Output(this, LocalizationUtils.Get(LocalizationKey.BATTLEGROUNDS_GAMEPLAY_ZONE_MINIONS_FOR_SALE));
			}
			else
			{
                base.ReadOpponentZoneName();
			}
		}

		protected override void ReadOpponentMinionsEmpty()
		{
            if (AccessibleGameplayUtils.IsInBattlegroundsShopPhase())
			{
				AccessibilityMgr.Output(this, LocalizationUtils.Get(LocalizationKey.BATTLEGROUNDS_GAMEPLAY_SEE_ZONE_MINIONS_FOR_SALE_EMPTY));
			}
            else
			{
                base.ReadOpponentMinionsEmpty();
			}
		}

        protected override void SeePlayerWeapon()
        {
            var bgQuestReward = GameState.Get().GetFriendlySidePlayer().GetQuestRewardCard();

            if (bgQuestReward != null)
			{
                FocusOnCard(bgQuestReward, false);
			}

            ReadBuddyMeterForHero(GetPlayerHero());
        }

        protected override void SeeOpponentWeapon()
        {
            var bgQuestReward = GameState.Get().GetOpposingSidePlayer().GetQuestRewardCard();

            if (bgQuestReward != null)
			{
                FocusOnCard(bgQuestReward, false);
			}

            ReadBuddyMeterForHero(GetOpponentHero());
        }

        private void ReadBuddyMeterForHero(Card heroCard)
        {
            if (heroCard == null || !GameState.Get().BattlegroundAllowBuddies())
            {
                return;
            }

            var heroEntity = heroCard.GetEntity();
            if (heroEntity == null)
            {
                return;
            }

            var progress = heroEntity.GetTag(GAME_TAG.BACON_HERO_BUDDY_PROGRESS);
            AccessibilityMgr.Output(this, LocalizationUtils.Format(LocalizationKey.BATTLEGROUNDS_GAMEPLAY_READ_PLAYER_BUDDY_METER, progress));
        }

        private void HandleMainOptionMode()
        {
            HandleZoneInput();
            HandleValidOptionsSelectionInput();
            HandleZoneSelection();

            if (AccessibleKey.FREEZE_TAVERN.IsPressed())
			{
                QueryFreezeOrUnfreezeTavern();
			}
            else if (AccessibleKey.FORCE_FREEZE_TAVERN.IsPressed())
			{
                ForceFreezeOrUnfreezeTavern();
			}
            else if (AccessibleKey.UPGRADE_TAVERN.IsPressed())
			{
                QueryUpgradeTavern();
			}
            else if (AccessibleKey.FORCE_UPGRADE_TAVERN.IsPressed())
			{
                ForceUpgradeTavern();
			}
            else if (AccessibleKey.REFRESH_TAVERN.IsPressed())
			{
                QueryRefreshTavern();
			}
            else if (AccessibleKey.FORCE_REFRESH_TAVERN.IsPressed())
			{
                ForceRefreshTavern();
			}
            else if (AccessibleKey.CONFIRM.IsPressed())
            {
                ClickCard();
            }
            else if (AccessibleKey.TRADE_CARD.IsPressed())
            {
                ClickCardForTrading();
            }
            else if (AccessibleKey.SPACE.IsPressed())
			{
                ClickCardForMovingOrPassing();
			}
        }

        private void HandleViewingTeammatesBoard()
        {
            HandleZoneInput();
            HandleZoneSelection();

            if (AccessibleKey.FREEZE_TAVERN.IsPressed())
			{
                QueryFreezeOrUnfreezeTavern();
			}
            else if (AccessibleKey.UPGRADE_TAVERN.IsPressed())
			{
                QueryUpgradeTavern();
			}
            else if (AccessibleKey.REFRESH_TAVERN.IsPressed())
			{
                QueryRefreshTavern();
			}

            if (m_curState == AccessibleGameState.TEAMMATES_CHOICES_HIDDEN && AccessibleKey.READ_NEXT_VALID_ITEM.IsPressed())
            {
                TeammateBoardViewer.Get().GetViewer<TeammateDiscoverViewer>().GetToggleButton().TriggerRelease();
            }
        }

        private void HandleViewingTeammatesChoices()
        {
            if (m_teammatesChoiceCards == null)
            {
                return;
            }

            m_cardBeingRead = m_teammatesChoiceCards.GetItemBeingRead();
            if (AccessibleKey.READ_NEXT_VALID_ITEM.IsPressed())
            {
                TeammateBoardViewer.Get().GetViewer<TeammateDiscoverViewer>().GetToggleButton().TriggerRelease();
            }
            else
            {
                m_teammatesChoiceCards.HandleAccessibleInput();
            }
        }

        private void ClickCard()
        {
            if (m_cardBeingRead == null)
            {
                return;
            }

			ResetState();

            var entity = m_cardBeingRead.GetCard().GetEntity();

            if (entity.GetZone() == TAG_ZONE.PLAY)
			{
				if (entity.IsControlledByFriendlySidePlayer())
				{
					m_sellingMinion = true;
				}
                else
				{
					m_buyingCard = true;
				}
			}

            AccessibleInputMgr.ClickLeftMouseButton();
        }

		private void ClickButtonCard(Card card)
		{
            StopReadingCard();
            AccessibleInputMgr.Click(card);
		}

        private void ClickCardForMovingOrPassing()
        {
            if (m_cardBeingRead == null)
            {
                return;
            }

            var card = m_cardBeingRead.GetCard();

            var playerBattlefield = GameState.Get().GetFriendlySidePlayer().GetBattlefieldZone();

            if (card.GetAccessibleZone() == playerBattlefield && playerBattlefield.GetCardCount() > 1)
            {
                ResetState();

                m_movingMinionWaitingForHold = true;

                AccessibleInputMgr.ClickLeftMouseButton();
            }
            else if (CanPassCard(card))
            {
                ResetState();

                m_passingCardWaitingForHold = true;

                AccessibleInputMgr.ClickLeftMouseButton();
            }
        }

        private void ClickCardForTrading()
        {
            if (m_cardBeingRead == null)
            {
                return;
            }

            var card = m_cardBeingRead.GetCard();

            if (!CanTradeCard(card))
            {
                return;
            }

            ResetState();
            QueryTradeOrForgeCard(card);
            m_tradingCardWaitingForHold = true;

            AccessibleInputMgr.ClickLeftMouseButton();
        }

        private void HandleTradeCardWhenHoldingCardInput()
        {
            if (AccessibleKey.TRADE_CARD.IsPressed() && CanTradeCard(m_heldCard))
            {
                QueryTradeOrForgeCard(m_heldCard);
            }
        }

        private void HandlePassCardWhenHoldingCardInput()
        {
            if (AccessibleKey.BATTLEGROUNDS_DUOS_PASS_CARD.IsPressed() && CanPassCard(m_heldCard))
            {
                m_passingCard = true;
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

        private bool CanTradeAnyCardInHand()
        {
            var handCards = GameState.Get().GetFriendlySidePlayer().GetHandZone().GetCards();
            return handCards.Any(card => CanTradeCard(card));
        }

        private bool CanPassCard(Card card)
        {
            if (!GameMgr.Get().IsBattlegroundDuoGame() || card == null)
            {
                return false;
            }

            return card.GetAccessibleZone() == GameState.Get().GetFriendlySidePlayer().GetHandZone()
                && card.GetEntity()?.IsPassable() == true;
        }

        private bool CanPassAnyCardInHand()
        {
            var handCards = GameState.Get().GetFriendlySidePlayer().GetHandZone().GetCards();
            return handCards.Any(card => CanPassCard(card));
        }

        private void QueryRefreshTavern()
		{
            ResetState();
            StopReadingCard();

            var refreshButtonCard = GetRefreshButtonCard();

            if (!m_viewingTeammatesBoard && !refreshButtonCard.IsInputEnabled())
            {
                return;
            }

            m_refreshingTavern = true;
            var refreshCost = refreshButtonCard.GetEntity().GetCost();

            AccessibilityMgr.Output(this, LocalizationUtils.Format(LocalizationKey.BATTLEGROUNDS_GAMEPLAY_QUERY_REFRESH_TAVERN_FOR_N_GOLD, refreshCost));

            var pingedText = AccessibleCardUtils.GetCardPingedText(refreshButtonCard.GetActor());
            if (pingedText != null)
            {
                AccessibilityMgr.Output(this, pingedText);
            }
		}

		private void ForceRefreshTavern()
		{
            ResetState();
            StopReadingCard();

            var refreshButtonCard = GetGameState().GetRefreshButtonCard();
            ClickButtonCard(refreshButtonCard);
		}

		private void QueryFreezeOrUnfreezeTavern()
		{
            ResetState();
            StopReadingCard();

            if (IsTavernFrozen())
			{
                QueryUnfreezeTavern();
			}
            else
			{
                QueryFreezeTavern();
			}
		}

		private bool IsTavernFrozen()
		{
            if (TryGetTavernFrozenState(out var isFrozen))
            {
                m_tavernFrozen = isFrozen;
            }

            return m_tavernFrozen;
		}

        private bool TryGetTavernFrozenState(out bool isFrozen)
        {
            isFrozen = false;

            var shopCards = GameState.Get()?.GetOpposingSidePlayer()?.GetBattlefieldZone()?.GetCards();
            if (shopCards == null || shopCards.Count == 0)
            {
                return false;
            }

            isFrozen = shopCards.Any(card => card?.GetEntity()?.IsFrozen() ?? false);
            return true;
        }

		private void ForceFreezeOrUnfreezeTavern()
		{
            ResetState();
            StopReadingCard();

            var freezeButton = GetGameState().GetFreezeButtonCard();

            ClickButtonCard(freezeButton);
		}

		private void QueryUnfreezeTavern()
		{
            ResetState();
            StopReadingCard();

            var freezeButtonCard = GetFreezeButtonCard();

            if (!m_viewingTeammatesBoard && !freezeButtonCard.IsInputEnabled())
            {
                return;
            }

            m_unfreezingTavern = true;

			AccessibilityMgr.Output(this, LocalizationUtils.Get(LocalizationKey.BATTLEGROUNDS_GAMEPLAY_QUERY_UNFREEZE_TAVERN));

            var pingedText = AccessibleCardUtils.GetCardPingedText(freezeButtonCard.GetActor());
            if (pingedText != null)
            {
                AccessibilityMgr.Output(this, pingedText);
            }
		}

		private void QueryFreezeTavern()
		{
            ResetState();
            StopReadingCard();

            var freezeButtonCard = GetFreezeButtonCard();

            if (!m_viewingTeammatesBoard && !freezeButtonCard.IsInputEnabled())
            {
                return;
            }

            m_freezingTavern = true;
            var freezeCost = freezeButtonCard.GetEntity().GetCost();

            if (freezeCost == 0)
			{
				AccessibilityMgr.Output(this, LocalizationUtils.Get(LocalizationKey.BATTLEGROUNDS_GAMEPLAY_QUERY_FREEZE_TAVERN));
			}
            else
			{
				AccessibilityMgr.Output(this, LocalizationUtils.Format(LocalizationKey.BATTLEGROUNDS_GAMEPLAY_QUERY_FREEZE_TAVERN_FOR_N_GOLD, freezeCost));
			}

            var pingedText = AccessibleCardUtils.GetCardPingedText(freezeButtonCard.GetActor());
            if (pingedText != null)
            {
                AccessibilityMgr.Output(this, pingedText);
            }
		}

		private void QueryUpgradeTavern()
		{
            ResetState();
            StopReadingCard();

            var upgradeButtonCard = GetTavernUpgradeButtonCard();

            if (!m_viewingTeammatesBoard && !upgradeButtonCard.IsInputEnabled())
            {
                return;
            }

            m_upgradingTavern = true;
            var upgradeCost = upgradeButtonCard.GetEntity().GetCost();

            AccessibilityMgr.Output(this, LocalizationUtils.Format(LocalizationKey.BATTLEGROUNDS_GAMEPLAY_QUERY_UPGRADE_TAVERN_FOR_N_GOLD, upgradeCost));

            var pingedText = AccessibleCardUtils.GetCardPingedText(upgradeButtonCard.GetActor());
            if (pingedText != null)
            {
                AccessibilityMgr.Output(this, pingedText);
            }
		}

		private void ForceUpgradeTavern()
		{
            ResetState();
            StopReadingCard();

            var upgradeButtonCard = GetGameState().GetTavernUpgradeButtonCard();
            ClickButtonCard(upgradeButtonCard);
		}

        private Card GetFreezeButtonCard()
        {
            if (m_viewingTeammatesBoard)
            {
                return TeammateBoardViewer.Get().GetViewer<TeammateGameModeButtonViewer>().GetFreezeButtonCard();
            }
            return GetGameState().GetFreezeButtonCard();
        }

        private Card GetRefreshButtonCard()
        {
            if (m_viewingTeammatesBoard)
            {
                return TeammateBoardViewer.Get().GetViewer<TeammateGameModeButtonViewer>().GetRefreshButtonCard();
            }
            return GetGameState().GetRefreshButtonCard();
        }

        private Card GetTavernUpgradeButtonCard()
        {
            if (m_viewingTeammatesBoard)
            {
                return TeammateBoardViewer.Get().GetViewer<TeammateGameModeButtonViewer>().GetTavernUpgradeButtonCard();
            }
            return GetGameState().GetTavernUpgradeButtonCard();
        }

		private TB_BaconShop GetGameState()
		{
            return GameState.Get().GetGameEntity() as TB_BaconShop;
		}

		protected override void Reset()
		{
            base.Reset();

            ResetState();
		}

		protected override bool IsSeeOpponentHeroPressed()
		{
            if (AccessibleGameplayUtils.IsInBattlegroundsCombatPhase())
			{
				return AccessibleKey.SEE_OPPONENT_HERO.IsPressed();
			}
            else
			{
                return AccessibleKey.SEE_TAVERN.IsPressed();
			}
		}

		protected override bool IsSeePlayerHeroPowerPressed()
		{
            return AccessibleKey.BATTLEGROUNDS_SEE_PLAYER_HERO_POWER.IsPressed();
		}

		protected override bool IsSeeOpponentHeroPowerPressed()
		{
            if (AccessibleGameplayUtils.IsInBattlegroundsCombatPhase())
			{
                return AccessibleKey.BATTLEGROUNDS_SEE_OPPONENT_HERO_POWER.IsPressed();
			}

            // No-op in shop phase
            return false;
		}

		internal override void OnFreezeOrUnfreezeEvent()
		{
            m_tavernFrozen = !m_tavernFrozen;

            if (TryGetTavernFrozenState(out var isFrozen))
            {
                m_tavernFrozen = isFrozen;
            }
		}

        protected override void OnCardHovered(Card card)
        {
            var entity = card.GetEntity();
            if (entity.HasTag(GAME_TAG.BACON_PAIR_CANDIDATE))
            {
                HSASoundMgr.Get().Play(HSASound.BATTLEGROUNDS_HOVER_PAIR_CANDIDATE);
            }
            else if (entity.HasTag(GAME_TAG.BACON_TRIPLE_CANDIDATE))
            {
                HSASoundMgr.Get().Play(HSASound.BATTLEGROUNDS_HOVER_TRIPLE_CANDIDATE);
            }
            else if (entity.HasTag(GAME_TAG.BACON_DUO_PAIR_CANDIDATE_TEAMMATE))
            {
                HSASoundMgr.Get().Play(HSASound.BATTLEGROUNDS_HOVER_TEAMMATES_PAIR_CANDIDATE);
            }
            else if (entity.HasTag(GAME_TAG.BACON_DUO_TRIPLE_CANDIDATE_TEAMMATE))
            {
                HSASoundMgr.Get().Play(HSASound.BATTLEGROUNDS_HOVER_TEAMMATES_TRIPLE_CANDIDATE);
            }
        }

        #region Moving Minions

        private int m_movePos;

        private void HandleMovingMinion()
        {
            var playerMinions = GameState.Get().GetFriendlySidePlayer().GetBattlefieldZone();

            if (playerMinions.GetCardCount() <= 1)
            {
                CancelActionWithHeldCard();
                return;
            }

			if (m_prevState != AccessibleGameState.MOVING_MINION)
			{
                m_movePos = 0;
                var startPos = m_heldCard.GetZonePosition();
				QueryMovingPosition(startPos);
			}

			HandleMovingPositionInput();

			if (m_movePos == playerMinions.GetLastPos())
			{
				MoveMouseToRightOfZone(playerMinions);
			}
			else if (m_movePos == 1)
			{
				MoveMouseToLeftOfZone(playerMinions);
			}
			else
			{
				var prevMinion = playerMinions.GetCardAtSlot(m_movePos - 1);
				var nextMinion = playerMinions.GetCardAtSlot(m_movePos);
				var pos = prevMinion.transform.position + (nextMinion.transform.position - prevMinion.transform.position) / 2;
				AccessibleInputMgr.MoveMouseToWorldPosition(pos);
			}

			HandleForcedConfirmOrCancel(ConfirmActionByClicking, CancelActionWithHeldCard, true);
        }

        private void HandleMovingPositionInput()
        {
            if (AccessibleKey.READ_PREV_ITEM.IsPressed())
            {
                QueryMovingPosition(-1);
            }
            else if (AccessibleKey.READ_NEXT_ITEM.IsPressed())
            {
                QueryMovingPosition(1);
            }
            else if (AccessibleKey.READ_FIRST_ITEM.IsPressed())
			{
                m_movePos = 1;
                QueryMovingPosition(0);
			}
            else if (AccessibleKey.READ_LAST_ITEM.IsPressed())
			{
				var playerMinions = GameState.Get().GetFriendlySidePlayer().GetBattlefieldZone();
				var lastPos = playerMinions.GetLastPos();
                m_movePos = lastPos;
                QueryMovingPosition(0);
			}
            else
			{
				int? numKeyPressed = AccessibleInputMgr.TryGetPressedNumKey();

                if (numKeyPressed.HasValue)
				{
					var playerMinions = GameState.Get().GetFriendlySidePlayer().GetBattlefieldZone();
					var lastPos = playerMinions.GetLastPos();

                    m_movePos = Math.Min(lastPos, numKeyPressed.Value);
                    QueryMovingPosition(0);
				}
			}
		}

        private void QueryMovingPosition(int inc)
        {
            var prevMovePos = m_movePos;
            var playerMinions = GameState.Get().GetFriendlySidePlayer().GetBattlefieldZone();
            var lastPos = playerMinions.GetLastPos();

            m_movePos += inc;

            if (m_movePos > lastPos)
            {
                m_movePos = lastPos;
            }
            else if (m_movePos < 1)
            {
                m_movePos = 1;
            }

            if (inc != 0 && prevMovePos == m_movePos)
            {
                return;
            }

            if (m_movePos == lastPos)
            {
                AccessibilityMgr.Output(this, LocalizationUtils.Get(LocalizationKey.BATTLEGROUNDS_GAMEPLAY_QUERY_MOVE_MINION_TO_LAST_POSITION));
            }
            else if (m_movePos == 1)
            {
                AccessibilityMgr.Output(this, LocalizationUtils.Get(LocalizationKey.BATTLEGROUNDS_GAMEPLAY_QUERY_MOVE_MINION_TO_FIRST_POSITION));
            }
            else
            {
                var prevMinion = playerMinions.GetCardAtSlot(m_movePos - 1);
                var nextMinion = playerMinions.GetCardAtSlot(m_movePos);
                var prevMinionName = GetPreferredCardName(prevMinion);
                var nextMinionName = GetPreferredCardName(nextMinion);
                AccessibilityMgr.Output(this, LocalizationUtils.Format(LocalizationKey.BATTLEGROUNDS_GAMEPLAY_QUERY_MOVE_MINION_BETWEEN, prevMinionName, nextMinionName));
            }
        }
        #endregion

        protected override string GetPreferredCardName(Card card)
		{
            return AccessibleCardUtils.GetInGameCardNameWithPremium(card);
		}

        protected override void MoveMouseToCard(Card card)
        {
            if (card.GetAccessibleZone()?.GetType() == typeof(ZoneHand))
            {
				// Note: Unlike in a game of traditional Hearthstone, we can't trust mousedOver as when Bob is speaking or highlighting certain
				// buttons (e.g. in the tutorial) the cards will have their input disabled which messes with mouse over state
				// Bit of a mess but not a whole lot we can do about it
				if (!IsMousedOver(card))
                {
                    // Note: All of this code was added to trace a rare edge case some players have fallen into with cards such as shadowstep.
                    // I haven't been able to reproduce so far but I think the bug has something to do with a NPE somewhere in the actor/manaObject chain
                    // TODO: Clean all of this up once the bug is detected/fixed
                    try
					{
                        var mousedOverCard = GetMousedOverCardRegardlessOfInput();

                        if (mousedOverCard != null && mousedOverCard.GetAccessibleZone() is ZonePlay)
						{
                            // In Battlegrounds, lists of effects can get large enough to the point that it covers cards in hand so we need to hide the tooltip
                            // or we won't be able to hover the card in hand
                            mousedOverCard.HideTooltip();
						}

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

		private bool IsMousedOver(Card card)
		{
            return GetMousedOverCardRegardlessOfInput() == card;
		}

        private Card GetMousedOverCardRegardlessOfInput()
		{
            return InputManager.Get().m_mousedOverCardRegardlessOfInput;
		}

		private int GetTavernTier(Player player)
		{
            return player.GetTag(GAME_TAG.PLAYER_TECH_LEVEL);
		}

		private int GetHeroTavernTier(Entity hero)
		{
            // Opponent sets this in Hero
            return hero.GetTag(GAME_TAG.PLAYER_TECH_LEVEL);
		}

		internal override void OnOpponentHeroChanged()
		{
            if (AccessibleGameplayUtils.IsInBattlegroundsCombatPhase())
			{
				OnBattlegroundsCombatPhaseStart();
			}
		}

		internal override void OnAnyHeroGainedAtk()
		{
            if (AccessibleGameplayUtils.IsInBattlegroundsCombatPhase())
			{
                OnCombatPhaseCombatEnded();
			}
		}

		internal override void OnPlayerAvailableResourcesChanged(int before, int after)
		{
            if (m_isShopPhase)
			{
                if (after > before)
				{
					ReadAvailablePlayerResources();
				}
                else
				{
					ReadRemainingPlayerResources();
				}
			}
		}

		private void ResetStateBetweenPhases()
		{
            m_wasShopPhasePopupShown = false;
            m_isShopPhase = false;
            m_isCombatPhase = false;
            AccessibleBattlegroundsLeaderboardMgr.Get().StopReadingLeaderboard();
		}

		internal override void OnFirstTaskListStart()
		{
            OnBattlegroundsShopPhasePopupShown(); // Not really shown for reconnects, but..
		}

		internal override void OnMainActionStep()
		{
            // We're guaranteed to have gold etc. refreshed when MAIN_ACTION starts
            if (!m_isShopPhase && m_wasShopPhasePopupShown)
			{
                OnBattlegroundsShopPhaseStart();
			}
		}

		internal override void OnMainStepEnd()
		{
            if (m_isCombatPhase)
			{
                if (!m_hasAnyPlayerWonThisPhase)
				{
					AccessibilityMgr.Output(this, LocalizationUtils.Get(LocalizationKey.GAMEPLAY_GAME_OVER_TIED));
				}
			}

            m_isCombatPhase = false;
		}

        #region Mulligan i.e. Choose Hero

        private bool m_confirmingHero;

        private bool m_rerollingHero;

        private bool m_waitingForRerollResponse;

        private Entity m_heroBeingRerolled;

        private string m_originalHeroId;

        private bool m_waitingForOpponentsToChooseHero;

        private string m_prevWaitingText; // Used for reading x players ready whenever a new player becomes ready

        private AccessibleListOfItems<AccessibleCard> m_teammatesMulliganCards;

		private void HandleChooseHeroInput()
        {
            if (m_accessibleMulliganCards == null)
            {
                return; // yield
            }

            if (m_justReconnected)
            {
                m_justReconnected = false;
            }

            var wasViewingTeammatesBoard = m_viewingTeammatesBoard;
            m_viewingTeammatesBoard = IsViewingTeammatesBoard();

            if ((!m_waitingForRerollResponse) && m_viewingTeammatesBoard || m_teammatesMulliganCards != null)
            {
                HandleDuosPortalInput();
            }

            var mulliganCards = GetCurrentMulliganCards();

            if (wasViewingTeammatesBoard != m_viewingTeammatesBoard)
            {
                mulliganCards?.StartReading();
                m_rerollingHero = false;
            }

            HandlePingCardInput();

            if (!m_viewingTeammatesBoard)
            {
                if (m_waitingForRerollResponse)
                {
                    HandleWaitingForRerollResponse();
                    return;
                }

                if (m_rerollingHero && HandleRerollingHero())
                {
                    return;
                }

                if (AccessibleKey.CONFIRM.IsPressed())
                {
                    if (m_waitingForOpponentsToChooseHero)
                    {
                        ReadNumberOfReadyPlayers(true);
                    }
                    else if (m_confirmingHero)
                    {
                        MulliganManager.Get().OnMulliganButtonReleased(null);
                    }
                    else
                    {
                        var focusedCard = m_accessibleMulliganCards.GetItemBeingRead();
                        AccessibleInputMgr.MoveMouseTo(focusedCard.GetCard());
                        AccessibleInputMgr.ClickLeftMouseButton();

                        m_confirmingHero = true;
                        AccessibilityMgr.Output(this, LocalizedText.GLOBAL_PRESS_ENTER_TO_CONFIRM_OR_BACKSPACE_TO_CANCEL);
                    }
                }
                else if (AccessibleKey.BACK.IsPressed() && m_confirmingHero)
                {
                    m_confirmingHero = false;
                    m_accessibleMulliganCards.StartReading();
                }
                else if (AccessibleKey.REFRESH_TAVERN.IsPressed() && !m_confirmingHero && !m_waitingForOpponentsToChooseHero)
                {
                    QueryRerollCurrentHero();
                }
                else if (AccessibleKey.FORCE_REFRESH_TAVERN.IsPressed() && !m_confirmingHero && !m_waitingForOpponentsToChooseHero)
                {
                    TryRerollCurrentHero();
                }
            }

            if (AccessibleKey.BATTLEGROUNDS_READ_RACES_IN_GAME.IsPressed())
			{
                ReadRacesAndAnomaly();
			}
            else if (AccessibleKey.END_TURN.IsPressed())
			{
                MulliganManager.Get().m_mulliganTimer?.OutputSecondsRemaining();
			}
            else if (AccessibleKey.SEE_PLAYER_MANA.IsPressed())
            {
                ReadPlayerRerollTokens(forceRead: true);
            }
            else if (!m_confirmingHero && !m_waitingForOpponentsToChooseHero)
            {
                mulliganCards.HandleAccessibleInput();
                AccessibleInputMgr.MoveMouseTo(mulliganCards.GetItemBeingRead().GetCard());
            }

            // Re-read banner everytime a new player becomes ready while waiting for the game to start
            if (m_waitingForOpponentsToChooseHero)
			{
                ReadNumberOfReadyPlayers();
			}
        }

        private AccessibleListOfItems<AccessibleCard> GetCurrentMulliganCards()
        {
            return m_viewingTeammatesBoard ? m_teammatesMulliganCards : m_accessibleMulliganCards;
        }

		private bool HandleRerollingHero()
        {
            return HandleSmoothConfirmOrCancel(TryRerollCurrentHero, ResetState, AccessibleKey.REFRESH_TAVERN);
        }

        private void QueryRerollCurrentHero()
        {
            if (!HeroRerollEnabled())
            {
                return;
                }

                var currentHero = m_accessibleMulliganCards.GetItemBeingRead().GetCard();
                var cost = currentHero.GetEntity().BaconFreeRerollLeft() > 0 ? 0 : 1;
                AccessibilityMgr.Output(this, LocalizationUtils.Format(LocalizationKey.BATTLEGROUNDS_GAMEPLAY_QUERY_REROLL_HERO, cost));
                m_rerollingHero = true;
        }

        private void HandleWaitingForRerollResponse()
        {
            if (m_heroBeingRerolled.GetCardId() == m_originalHeroId)
            {
                return;
            }
            m_accessibleMulliganCards.StartReading();
            m_waitingForRerollResponse = false;
            m_heroBeingRerolled = null;
            m_originalHeroId = null;
        }

        public void OnBattlegroundsHeroRerollRequested(Entity hero)
        {
            m_waitingForRerollResponse = true;
            m_heroBeingRerolled = hero;
            m_originalHeroId = hero.GetCardId();
        }

        private void TryRerollCurrentHero()
        {
            if (!HeroRerollEnabled())
            {
                return;
            }

            var currentHero = m_accessibleMulliganCards.GetItemBeingRead().GetCard();
            var canReroll = currentHero.GetEntity().ShouldEnableRerollButton();
            if (canReroll <= Entity.RerollButtonEnableResult.UNLOCK)
            {
                currentHero.GetActor().GetComponent<PlayerLeaderboardMainCardActor>()?.GetHeroRerollButton()?.TriggerRelease();
                return;
            }

            string key = null;
            switch (canReroll)
            {
                case Entity.RerollButtonEnableResult.OUT_OF_CURRENCY:
                    key = "GLUE_BACON_TOOLTIP_REROLL_BUTTON_NOT_ENOUGH_CURRENCY_DESC";
                    break;
                case Entity.RerollButtonEnableResult.HERO_REROLL_LIMITATION_REACHED:
                    key = "GLUE_BACON_TOOLTIP_REROLL_BUTTON_LIMIT_REACHED_DESC";
                    break;
                case Entity.RerollButtonEnableResult.INSUFFICIENT_MULLIGAN_TIME_LEFT:
                    key = "GLUE_BACON_TOOLTIP_REROLL_BUTTON_INSUFFICIENT_MULLIGAN_TIME_DESC";
                    break;
                default:
                    break;
            }
            if (!string.IsNullOrEmpty(key))
            {
                AccessibilityMgr.Output(this, GameStrings.Get(key));
            }
        }

        private static bool HeroRerollEnabled()
        {
            return GameState.Get().GetGameEntity().HasTag(GAME_TAG.BACON_MULLIGAN_HERO_REROLL_ACTIVE);
        }

        internal override void OnEnterMultiplayerWaitingArea(List<Card> startingCards, string mulliganBannerText, string mulliganBannerSubtitleText, NormalButton confirmButton)
		{
            ReadRacesAndAnomaly();

            ReadPlayerRerollTokens();

            if (mulliganBannerText != null) AccessibilityMgr.Output(this, mulliganBannerText);
            if (mulliganBannerSubtitleText != null) AccessibilityMgr.Output(this, mulliganBannerSubtitleText);


            try
            {
                m_mulliganConfirmButton = confirmButton;

                var unlockedCards = startingCards.Where((card) => !card.GetEntity().HasTag(GAME_TAG.BACON_LOCKED_MULLIGAN_HERO)).ToList();

                var accessibleCards = new List<AccessibleCard>(unlockedCards.Count);
                unlockedCards.ForEach(c => accessibleCards.Add(new AccessibleBattlegroundsChooseHeroCard(this, c)));

                m_accessibleMulliganCards = new AccessibleListOfItems<AccessibleCard>(this, accessibleCards);
                m_accessibleMulliganCards.StartReading();
                m_prevWaitingText = null;
            }
            catch (Exception e)
            {
                AccessibilityUtils.LogFatalError(e);
            }
		}

		public override void EndMulligan()
		{
            m_waitingForOpponentsToChooseHero = false;
            m_confirmingHero = false;
            m_accessibleMulliganCards = null;
            m_mulliganConfirmButton = null;

            TransitionFromMulliganToGame();
		}

        public override void WaitingForOpponentToFinishMulligan()
        {
            ReadNumberOfReadyPlayers();

            m_confirmingHero = false;
            m_waitingForOpponentsToChooseHero = true;
        }

		private void ReadNumberOfReadyPlayers(bool forceRead=false)
		{
            var tbBaconShop = GetGameState();
            var playersReady = tbBaconShop.CountPlayersFinishedMulligan();
            var playersInGame = tbBaconShop.CountPlayersInGame();
            var waitingText = LocalizationUtils.Format(LocalizationKey.UI_BATTLEGROUNDS_MULLIGAN_PLAYERS_READY, playersReady, playersInGame);

            if (m_prevWaitingText != waitingText || forceRead)
			{
				AccessibilityMgr.Output(this, waitingText);
			}

            m_prevWaitingText = waitingText;
		}

        internal void OnTeammateMulliganEntitiesReceived(List<Card> cards)
        {
            var teammatesMulliganCards = cards.Select((c) => (AccessibleCard)new AccessibleBattlegroundsChooseHeroCard(this, c)).ToList();
            m_teammatesMulliganCards = new AccessibleListOfItems<AccessibleCard>(this, teammatesMulliganCards);
        }

        internal void OnTeammateChoseHero(Card chosenHero)
        {
            AccessibilityMgr.Output(this, LocalizationUtils.Format(LocalizationKey.BATTLEGROUNDS_DUOS_GAMEPLAY_TEAMMATE_CHOSE_HERO, chosenHero.GetEntity().GetName()));

            if (m_viewingTeammatesBoard)
            {
                ClickDuosPortal();
            }
        }

        private void ReadPlayerRerollTokens(bool forceRead = false)
        {
            if (!HeroRerollEnabled())
            {
                return;
            }

            long tokens = NetCache.Get().GetBattlegroundsTokenBalance();
            if (forceRead || tokens > 0)
            {
                AccessibilityMgr.Output(this, LocalizationUtils.Format(LocalizationKey.BATTLEGROUNDS_GAMEPLAY_READ_PLAYER_TOKENS, tokens));
            }
        }

        #endregion

        #region Duos Zones
        protected override IZone GetPlayerHand()
        {
            if (m_viewingTeammatesBoard)
            {
                return TeammateBoardViewer.Get().GetViewer<TeammateHandViewer>().GetVirtualZone();
            }
            return base.GetPlayerHand();
        }

        protected override IZone GetPlayerMinions()
        {
            if (m_viewingTeammatesBoard)
            {
                return TeammateBoardViewer.Get().GetViewer<TeammateMinionViewer>().GetFriendlyVirtualZone();
            }
            return base.GetPlayerMinions();
        }

        protected override IZone GetOpponentMinions()
        {
            if (m_viewingTeammatesBoard)
            {
                return TeammateBoardViewer.Get().GetViewer<TeammateMinionViewer>().GetOpposingVirtualZone();
            }
            return base.GetOpponentMinions();
        }

        protected override IZone GetPlayerSecrets()
        {
            if (m_viewingTeammatesBoard)
            {
                return TeammateBoardViewer.Get().GetViewer<TeammateSecretViewer>().teammateSecretZone;
            }
            return base.GetPlayerSecrets();
        }

        protected override Card GetPlayerHero()
        {
            if (m_viewingTeammatesBoard)
            {
                return TeammateBoardViewer.Get().GetViewer<TeammateHeroViewer>().GetTeammateHero().GetCard();
            }
            return base.GetPlayerHero();
        }

        protected override Card GetOpponentHero()
        {
            if (m_viewingTeammatesBoard)
            {
                return TeammateBoardViewer.Get().GetViewer<TeammateHeroViewer>().GetOpponentHero().GetCard();
            }
            return base.GetOpponentHero();
        }

        protected override IZone GetPlayerHeroPowers()
        {
            if (m_viewingTeammatesBoard)
            {
                return TeammateBoardViewer.Get().GetViewer<TeammateHeroViewer>().GetTeammateHeroPowers();
            }
            return base.GetPlayerHeroPowers();
        }

        private Card GetHeroBuddyCard()
        {
            if (m_viewingTeammatesBoard)
            {
                return TeammateBoardViewer.Get().GetViewer<TeammateHeroViewer>().GetTeammateBuddyButton()?.GetCard();
            }
            return TB_BaconShop.GetHeroBuddyCard(Player.Side.FRIENDLY);
        }
        #endregion

        #region Duos pings
        internal void OnEntityPinged(Actor actor, TEAMMATE_PING_TYPE pingType)
        {
            string fullName = GetPingedActorName(actor);
            var pingName = GetPingName(pingType);

            var msg = LocalizationUtils.Format(LocalizationKey.BATTLEGROUNDS_DUOS_GAMEPLAY_ENTITY_PINGED, fullName, pingName);
            AccessibilityMgr.Output(this, msg);
            AccessibleHistoryMgr.Get().AddEntry(msg);
            PlayPingSound(pingType);
        }

        internal static string GetPingName(TEAMMATE_PING_TYPE pingType)
        {
            LocalizationKey key = null;
            switch (pingType)
            {
                case TEAMMATE_PING_TYPE.EXCLAMATION:
                key = LocalizationKey.BATTLEGROUNDS_DUOS_GAMEPLAY_PING_EXCLAMATION;
                break;
                case TEAMMATE_PING_TYPE.CHECK:
                key = LocalizationKey.BATTLEGROUNDS_DUOS_GAMEPLAY_PING_CHECK;
                break;
                case TEAMMATE_PING_TYPE.CROSS:
                key = LocalizationKey.BATTLEGROUNDS_DUOS_GAMEPLAY_PING_CROSS;
                break;
                case TEAMMATE_PING_TYPE.WARP:
                key = LocalizationKey.BATTLEGROUNDS_DUOS_GAMEPLAY_PING_WARP;
                break;
                case TEAMMATE_PING_TYPE.QUESTION:
                key = LocalizationKey.BATTLEGROUNDS_DUOS_GAMEPLAY_PING_QUESTION;
                break;
            }

            if (key != null)
            {
                return LocalizationUtils.Get(key);
            }
            return "";
        }

        private static void PlayPingSound(TEAMMATE_PING_TYPE pingType)
        {
            HSASound sound = null;
            switch (pingType)
            {
                case TEAMMATE_PING_TYPE.CHECK:
                sound = HSASound.BATTLEGROUNDS_DUOS_PING_CHECK;
                break;
                case TEAMMATE_PING_TYPE.CROSS:
                sound = HSASound.BATTLEGROUNDS_DUOS_PING_CROSS;
                break;
                case TEAMMATE_PING_TYPE.WARP:
                sound = HSASound.BATTLEGROUNDS_DUOS_PING_WARP;
                break;
                case TEAMMATE_PING_TYPE.QUESTION:
                sound = HSASound.BATTLEGROUNDS_DUOS_PING_QUESTION;
                break;
            }

            if (sound != null)
            {
                HSASoundMgr.Get().Play(sound);
            }
        }

        private static string GetPingedActorName(Actor actor)
        {
            var actorName = AccessibleSpeechUtils.GetName(actor.GetEntity());
            var fullName = LocalizationUtils.Format(TeammateBoardViewer.Get().IsActorTeammates(actor) ? LocalizationKey.BATTLEGROUNDS_DUOS_GAMEPLAY_TEAMMATES_ENTITY_FULL_NAME : LocalizationKey.GAMEPLAY_DIFF_PLAYER_ENTITY_FULL_NAME, actorName);
            return fullName;
        }

        private IEnumerator FastApplyPingToActor(Actor actor, TEAMMATE_PING_TYPE pingType)
        {
            if (actor == null)
            {
                AccessibilityMgr.Output(this, LocalizationUtils.Get(LocalizationKey.GLOBAL_CANNOT_DO_THAT));
                yield break;
            }

            if (actor.ArePingsBlocked())
            {
                AccessibilityMgr.Output(this, LocalizationUtils.Get(LocalizationKey.GLOBAL_CANNOT_DO_THAT));
                yield break;
            }

            if (actor.GetActivePingType() == pingType)
            {
                actor.RemovePingAndNotifyTeammate();
                yield break;
            }
            else if (actor.GetActivePingType() != TEAMMATE_PING_TYPE.INVALID)
            {
                actor.RemovePingAndNotifyTeammate();
                yield return new WaitForSeconds(0.25f);
            }

            Network.Get().SendPingTeammateEntity(actor.GetEntity().GetEntityId(), (int)pingType, TeammateBoardViewer.Get().IsActorTeammates(actor));
            actor.ActivatePing(pingType);
        }

        private Actor GetActorToPing()
        {
            if (m_curPhase == AccessibleGamePhase.MULLIGAN)
            {
                var mulliganCards = GetCurrentMulliganCards();
                return mulliganCards?.GetItemBeingRead()?.GetCard()?.GetActor();
            }
            else if (m_freezingTavern)
            {
                return GetFreezeButtonCard()?.GetActor();
            }
            else if (m_refreshingTavern)
            {
                return GetRefreshButtonCard()?.GetActor();
            }
            else if (m_upgradingTavern)
            {
                return GetTavernUpgradeButtonCard()?.GetActor();
            }

            return m_cardBeingRead?.GetCard()?.GetActor();
        }

        private void ReadLastPingedCard()
        {
            var actor = TeammatePingWheelManager.Get()?.GetLastPingedCard()?.GetActor();

            if (actor == null || actor.GetActivePingType() == TEAMMATE_PING_TYPE.INVALID)
            {
                AccessibilityMgr.Output(this, LocalizationUtils.Get(LocalizationKey.BATTLEGROUNDS_DUOS_GAMEPLAY_NO_PING));
            }
            else
            {
                var actorName = GetPingedActorName(actor);
                AccessibilityMgr.Output(this, AccessibleSpeechUtils.CombineWordsWithColon(actorName, GetPingName(actor.GetActivePingType())));
            }
        }

        private IEnumerator JumpToLastPingedCard()
        {
            var card = TeammatePingWheelManager.Get()?.GetLastPingedCard();
            if (card == null)
            {
                AccessibilityMgr.Output(this, LocalizationUtils.Get(LocalizationKey.BATTLEGROUNDS_DUOS_GAMEPLAY_NO_PING));
                yield break;
            }

            var cardActor = card.GetActor();
            if (cardActor == null)
            {
                AccessibilityMgr.Output(this, LocalizationUtils.Get(LocalizationKey.BATTLEGROUNDS_DUOS_GAMEPLAY_NO_PING));
                yield break;
            }
            if (cardActor.GetActivePingType() == TEAMMATE_PING_TYPE.INVALID)
            {
                AccessibilityMgr.Output(this, LocalizationUtils.Get(LocalizationKey.BATTLEGROUNDS_DUOS_GAMEPLAY_NO_PING));
                yield break;
            }

            var teammateBoardViewer = TeammateBoardViewer.Get();
            if (teammateBoardViewer == null)
            {
                AccessibilityMgr.Output(this, LocalizationUtils.Get(LocalizationKey.GLOBAL_CANNOT_DO_THAT));
                yield break;
            }

            if (teammateBoardViewer.IsActorTeammates(cardActor) && !m_viewingTeammatesBoard)
            {
                ClickDuosPortal();
                yield return WaitForViewingTeammateBoard(2f);
                if (TeammateBoardViewer.Get()?.IsViewingTeammate() != true)
                {
                    yield break;
                }
            }

            if (m_curPhase == AccessibleGamePhase.MULLIGAN)
            {
                var mulliganCards = GetCurrentMulliganCards();
                if (mulliganCards == null)
                {
                    AccessibilityMgr.Output(this, LocalizationUtils.Get(LocalizationKey.BATTLEGROUNDS_DUOS_GAMEPLAY_NO_PING));
                    yield break;
                }

                bool foundCard = false;
                for (int i = 0; i < mulliganCards.Count; i++)
                {
                    if (mulliganCards.Items[i].GetCard() == card)
                    {
                        mulliganCards.StartReadingFromIndex(i);
                        foundCard = true;
                        break;
                    }
                }

                if (!foundCard)
                {
                    AccessibilityMgr.Output(this, LocalizationUtils.Get(LocalizationKey.BATTLEGROUNDS_DUOS_GAMEPLAY_NO_PING));
                }
            }
            else
            {
                FocusOnCard(card, true);
            }
        }

        private IEnumerator WaitForViewingTeammateBoard(float timeoutSeconds)
        {
            float timeoutAt = Time.realtimeSinceStartup + timeoutSeconds;
            while (Time.realtimeSinceStartup < timeoutAt)
            {
                if (TeammateBoardViewer.Get()?.IsViewingTeammate() == true)
                {
                    yield break;
                }

                yield return null;
            }

            AccessibilityMgr.Output(this, LocalizationUtils.Get(LocalizationKey.GLOBAL_CANNOT_DO_THAT));
        }

        private void HandlePingCardInput()
        {
            if (!GameMgr.Get().IsBattlegroundDuoGame() || m_heldCard != null)
            {
                return;
            }

            if (AccessibleKey.BATTLEGROUNDS_DUOS_READ_LAST_PINGED_CARD.IsPressed())
            {
                ReadLastPingedCard();
            }
            else if (AccessibleKey.BATTLEGROUNDS_DUOS_JUMP_TO_LAST_PINGED_CARD.IsPressed())
            {
                Gameplay.Get().StartCoroutine(JumpToLastPingedCard());
            }

            if (!InputManager.Get().PermitDecisionMakingInput() || GetActorToPing() == null)
            {
                return;
            }
            if (IsDuosExclamationPingPressed())
            {
                PingCurrentCard(TEAMMATE_PING_TYPE.EXCLAMATION);
            }
            else if (AccessibleKey.BATTLEGROUNDS_DUOS_PING_CHECK.IsPressed())
            {
                PingCurrentCard(TEAMMATE_PING_TYPE.CHECK);
            }
            else if (AccessibleKey.BATTLEGROUNDS_DUOS_PING_CROSS.IsPressed())
            {
                PingCurrentCard(TEAMMATE_PING_TYPE.CROSS);
            }
            else if (AccessibleKey.BATTLEGROUNDS_DUOS_PING_QUESTION.IsPressed())
            {
                PingCurrentCard(TEAMMATE_PING_TYPE.QUESTION);
            }
            else if (AccessibleKey.BATTLEGROUNDS_DUOS_PING_WARP.IsPressed())
            {
                PingCurrentCard(TEAMMATE_PING_TYPE.WARP);
            }
        }

        private static bool IsDuosExclamationPingPressed()
        {
            if (!Input.GetKeyDown(KeyCode.Alpha1))
            {
                return false;
            }

            bool shiftPressed = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
            bool ctrlPressed = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
            bool altPressed = Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt);
            return shiftPressed && !ctrlPressed && !altPressed;
        }

        private void PingCurrentCard(TEAMMATE_PING_TYPE pingType)
        {
            Gameplay.Get().StartCoroutine(FastApplyPingToActor(GetActorToPing(), pingType));
        }
        #endregion

        internal void OnAddTeammateDiscoverEntitiesToViewer(List<Card> cards)
        {
            var accessibleCards = cards.Select((c) => new AccessibleBattlegroundsCard(this, c)).ToList();
            m_teammatesChoiceCards = new AccessibleListOfItems<AccessibleBattlegroundsCard>(this, accessibleCards);
        }

        #region Help

        protected override string GetMulliganHelp()
        {
            var ret = new List<string>();
            ret.Add(m_accessibleMulliganCards?.GetHelp(false));
            if (HeroRerollEnabled())
            {
                ret.Add(LocalizationUtils.Format(LocalizationKey.BATTLEGROUNDS_GAMEPLAY_HERO_REROLL_HELP, AccessibleKey.REFRESH_TAVERN));
            }
            if (GameMgr.Get().IsBattlegroundDuoGame())
            {
                ret.Add($"Press {AccessibleKey.BATTLEGROUNDS_DUOS_SWITCH_TO_TEAMMATES_BOARD} to switch between your board and your teammate's board");
                ret.Add($"Press {AccessibleKey.BATTLEGROUNDS_DUOS_READ_LAST_PINGED_CARD} to read the last pinged card");
                ret.Add($"Press {AccessibleKey.BATTLEGROUNDS_DUOS_JUMP_TO_LAST_PINGED_CARD} to jump to the last pinged card");
                ret.Add($"Use Shift+1, {AccessibleKey.BATTLEGROUNDS_DUOS_PING_CHECK}, {AccessibleKey.BATTLEGROUNDS_DUOS_PING_CROSS}, {AccessibleKey.BATTLEGROUNDS_DUOS_PING_QUESTION}, or {AccessibleKey.BATTLEGROUNDS_DUOS_PING_WARP} to ping the current card");
            }
            return AccessibleSpeechUtils.CombineLines(ret);
        }

        protected override string GetMainOptionModeHelp()
        {
            if (!m_isShopPhase)
			{
                // Don't think this can even happen to be honest
                return "";
			}

            // Keep it simple since most players would rather look at the commands online anyways. Contextual help as in traditional Hearthstone is not worth
            // the complexity

            var lines = new List<string>();

            lines.Add(LocalizationUtils.Format(LocalizationKey.BATTLEGROUNDS_GAMEPLAY_READ_PLAYER_GOLD_HELP, AccessibleKey.SEE_PLAYER_MANA));
            lines.Add(LocalizationUtils.Format(LocalizationKey.BATTLEGROUNDS_GAMEPLAY_READ_TAVERN_INFORMATION_HELP, AccessibleKey.SEE_TAVERN));
            if (GameState.Get().BattlegroundAllowBuddies())
            {
                lines.Add(LocalizationUtils.Format(LocalizationKey.BATTLEGROUNDS_GAMEPLAY_READ_HERO_BUDDY_HELP, AccessibleKey.BATTLEGROUNDS_READ_HERO_BUDDY));
            }
            if (GameState.Get().BattlegroundsAllowTrinkets())
            {
                lines.Add(LocalizationUtils.Format(LocalizationKey.BATTLEGROUNDS_GAMEPLAY_READ_PLAYER_TRINKETS_HELP, AccessibleKey.BATTLEGROUNDS_READ_TRINKETS));
            }
            lines.Add(LocalizationUtils.Format(LocalizationKey.BATTLEGROUNDS_GAMEPLAY_READ_NEXT_OPPONENT_STATS_HELP, AccessibleKey.BATTLEGROUNDS_READ_NEXT_OPPONENT_STATS));
            lines.Add(LocalizationUtils.Format(LocalizationKey.BATTLEGROUNDS_GAMEPLAY_READ_MY_STATS_HELP, AccessibleKey.BATTLEGROUNDS_READ_MY_STATS));
            lines.Add(LocalizationUtils.Format(LocalizationKey.BATTLEGROUNDS_GAMEPLAY_READ_LEADERBOARD_HELP, AccessibleKey.BATTLEGROUNDS_READ_LEADERBOARD));
            lines.Add(LocalizationUtils.Format(LocalizationKey.BATTLEGROUNDS_GAMEPLAY_READ_RACES_IN_GAME_HELP, AccessibleKey.BATTLEGROUNDS_READ_RACES_IN_GAME));
            lines.Add(LocalizationUtils.Format(LocalizationKey.GAMEPLAY_READ_PLAYER_HERO_POWER_HELP, AccessibleKey.BATTLEGROUNDS_SEE_PLAYER_HERO_POWER));
            lines.Add(LocalizationUtils.Format(LocalizationKey.BATTLEGROUNDS_TUTORIAL_READ_TIME_REMAINING, AccessibleKey.END_TURN));
            lines.Add(LocalizationUtils.Format(LocalizationKey.BATTLEGROUNDS_TUTORIAL_MINION_UPGRADE_TAVERN_TUTORIAL_OVERRIDE, AccessibleKey.UPGRADE_TAVERN, AccessibleKey.CONFIRM));
            lines.Add(LocalizationUtils.Format(LocalizationKey.BATTLEGROUNDS_TUTORIAL_REFRESH_TUTORIAL_OVERRIDE, AccessibleKey.FREEZE_TAVERN, AccessibleKey.CONFIRM));
            lines.Add(LocalizationUtils.Format(LocalizationKey.BATTLEGROUNDS_GAMEPLAY_QUERY_FREEZE_TAVERN, AccessibleKey.FREEZE_TAVERN, AccessibleKey.CONFIRM));
            lines.Add(LocalizationUtils.Format(LocalizationKey.BATTLEGROUNDS_TUTORIAL_MINION_MOVE_TUTORIAL_OVERRIDE, AccessibleKey.SEE_PLAYER_MINIONS, AccessibleKey.SPACE));
            lines.Add(LocalizationUtils.Format(LocalizationKey.BATTLEGROUNDS_TUTORIAL_DRAGBUY_TUTORIAL_OVERRIDE, AccessibleKey.SEE_OPPONENT_MINIONS, AccessibleKey.CONFIRM));
            lines.Add(LocalizationUtils.Format(LocalizationKey.BATTLEGROUNDS_TUTORIAL_DRAGSELL_TUTORIAL_OVERRIDE, AccessibleKey.SEE_PLAYER_MINIONS, AccessibleKey.CONFIRM));
            if (CanTradeAnyCardInHand())
            {
                lines.Add(LocalizationUtils.Format(LocalizationKey.GAMEPLAY_TRADE_CARD_HELP, AccessibleKey.TRADE_CARD));
            }
            if (CanPassAnyCardInHand())
            {
                lines.Add($"Press {AccessibleKey.BATTLEGROUNDS_DUOS_PASS_CARD} to pass a card in your hand to your teammate");
            }
            if (GameMgr.Get().IsBattlegroundDuoGame())
            {
                lines.Add($"Press {AccessibleKey.BATTLEGROUNDS_DUOS_SWITCH_TO_TEAMMATES_BOARD} to switch between your board and your teammate's board");
                lines.Add($"Press {AccessibleKey.BATTLEGROUNDS_DUOS_READ_LAST_PINGED_CARD} to read the last pinged card");
                lines.Add($"Press {AccessibleKey.BATTLEGROUNDS_DUOS_JUMP_TO_LAST_PINGED_CARD} to jump to the last pinged card");
                lines.Add($"Use Shift+1, {AccessibleKey.BATTLEGROUNDS_DUOS_PING_CHECK}, {AccessibleKey.BATTLEGROUNDS_DUOS_PING_CROSS}, {AccessibleKey.BATTLEGROUNDS_DUOS_PING_QUESTION}, or {AccessibleKey.BATTLEGROUNDS_DUOS_PING_WARP} to ping the current card");
            }

            return AccessibleSpeechUtils.CombineLines(lines);
        }

        protected override string GetOpponentTurnHelp()
        {
            // No-op
            return "";
        }

        public override string GetHelp()
        {
            var gameEntity = GameState.Get().GetGameEntity();
            if (gameEntity is TB_BaconShop_Tutorial)
			{
                return ((TB_BaconShop_Tutorial)gameEntity).GetHelp();
			}

            return base.GetHelp();
        }

        #endregion

        #region Not used in BGs

        internal override void ReadOpponentResources()
		{
			// No-op
		}

        internal override void ReadOpponentHand()
		{
			// No-op
		}

		internal override void ReadPlayerDeck()
		{
			// No-op
		}

		internal override void ReadOpponentDeck()
		{
			// No-op
		}

		#endregion
	}
}
