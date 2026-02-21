using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Accessibility
{
    class AccessibleBattlegroundsCard : AccessibleCard
    {
        internal AccessibleBattlegroundsCard(AccessibleComponent parent, Card card) : base(parent, card)
        {
        }

        internal override List<string> GetLines()
        {
            if (m_card.GetEntity().IsHero())
            {
                return GetLinesForHero();
            }
            else if (m_card.GetEntity().IsHeroPower())
            {
                return GetLinesForHeroPower();
            }
            else if (m_card.GetEntity().IsQuest())
            {
                return GetLinesForQuest();
            }
            else if (m_card.GetEntity().IsSecret())
            {
                if (m_card.GetEntity().IsControlledByFriendlySidePlayer())
                {
                    return GetLinesForSpell();
                }
                else
                {
                    return GetLinesForOpponentSecret();
                }
            }
            else if (m_card.GetEntity().IsSpell() || m_card.GetEntity().GetCardType() == TAG_CARDTYPE.BATTLEGROUND_SPELL)
            {
                return GetLinesForSpell();
            }
            else if (m_card.GetEntity().IsBattlegroundQuestReward())
            {
                return GetLinesForBattlegroundsQuestReward();
            }
            else if (m_card.GetEntity().IsBattlegroundTrinket())
            {
                return GetLinesForBattlegroundsTrinket();
            }
            else
            {
                return GetLinesForNormalCard();
            }
        }

        private new List<string> GetLinesForHeroPower()
        {
            var lines = base.GetLinesForHeroPower();

            AccessibleCardUtils.AddLineIfExists(GetHeroPowerProgressLine(), lines);

            return lines;
        }

        private List<string> GetLinesForBuddyMeter()
        {
            var ret = new List<string>();

            var heroCard = GameState.Get().GetFriendlySidePlayer().GetHero();

            var progress = heroCard.GetTag(GAME_TAG.BACON_HERO_BUDDY_PROGRESS);
            ret.Add(LocalizationUtils.Format(LocalizationKey.BATTLEGROUNDS_GAMEPLAY_READ_PLAYER_BUDDY_METER, progress));

            ret.AddRange(AccessibleCardUtils.GetHeroBuddyCardLinesForHeroCard(heroCard));

            return ret;
        }

        private string GetHeroPowerProgressLine()
        {
            var controller = m_card.GetController();

            if (controller == null)
            {
                return null;
            }

            var progressBarTotal = controller.GetTag(GAME_TAG.PROGRESSBAR_TOTAL);
            var progressBarCardDbId = controller.GetTag(GAME_TAG.PROGRESSBAR_CARDID);

            if (progressBarTotal > 0 && IsProgressBarForCurrentHeroPower(progressBarCardDbId))
            {
                var progressBarProgress = controller.GetTag(GAME_TAG.PROGRESSBAR_PROGRESS);
                return LocalizationUtils.Format(LocalizationKey.TOAST_QUEST_PROGRESS_TOAST_PROGRESS, progressBarProgress, progressBarTotal);
            }

            var questProgressTotal = m_card.GetEntity().GetTag(GAME_TAG.QUEST_PROGRESS_TOTAL);

            if (questProgressTotal > 0)
            {
                var questProgress = m_card.GetEntity().GetTag(GAME_TAG.QUEST_PROGRESS);
                return LocalizationUtils.Format(LocalizationKey.TOAST_QUEST_PROGRESS_TOAST_PROGRESS, questProgress, questProgressTotal);
            }

            return null;
        }

        private bool IsProgressBarForCurrentHeroPower(int progressBarCardDbId)
        {
            if (progressBarCardDbId <= 0)
            {
                return false;
            }

            var currentHeroPowerCardId = m_card.GetEntity().GetCardId();

            if (string.IsNullOrEmpty(currentHeroPowerCardId))
            {
                return false;
            }

            if (progressBarCardDbId == GameUtils.TranslateCardIdToDbId(currentHeroPowerCardId))
            {
                return true;
            }

            var progressBarCardId = GameUtils.TranslateDbIdToCardId(progressBarCardDbId);

            if (string.IsNullOrEmpty(progressBarCardId))
            {
                return false;
            }

            return GameUtils.GetHeroPowerCardIdFromHero(progressBarCardId) == currentHeroPowerCardId;
        }

        private List<string> GetLinesForSpell()
        {
            // Recruitment Map -> Spell -> 3 gold -> description
            // Note: Handles player secrets as well

            var lines = new List<string>();
            lines.AddRange(GetHeader());

            if (!AccessibleCardUtils.IsCostHidden(m_card))
            {
                var cost = AccessibleCardUtils.GetCost(m_card);
                var effects = GetEffects();
                if (effects.Length > 0)
                {
                    cost += $" {effects}";
                }
                lines.Add(cost);
            }

            AccessibleCardUtils.AddLineIfExists(GetDescription(), lines);

            AccessibleCardUtils.AddLineIfExists(GetCardType(), lines);
            AccessibleCardUtils.AddLineIfExists(GetRace(), lines); // Handles spell school as well
            lines.Add(GetTier());

            return lines;
        }

        private List<string> GetLinesForNormalCard()
        {
            // (Golden) Tarecgosa -> x atk x health -> description -> Dragon -> Tier 3 -> Minion (handles buddy as well if necessary)

            var zone = m_card.GetEntity().GetZone();
            var isInPlayZone = zone == TAG_ZONE.PLAY;

            var lines = new List<string>();
            lines.AddRange(GetHeaderWithRarity());

            var resources = GetResources();
            if (resources.Length > 0)
            {
                if (isInPlayZone)
                {
                    var effects = GetEffects();
                    if (effects.Length > 0)
                    {
                        resources = $"{resources} {effects}";
                    }
                }

                lines.Add(resources);
            }
            else if (isInPlayZone)
            {
                AccessibleCardUtils.AddLineIfExists(GetEffects(), lines);
            }

            AccessibleCardUtils.AddLineIfExists(GetDescription(), lines);

            AccessibleCardUtils.AddLineIfExists(GetRace(), lines); // Handles spell school as well

            lines.Add(GetTier());

            AccessibleCardUtils.AddLineIfExists(GetCardType(), lines);

            return lines;
        }

        private List<string> GetLinesForHero()
        {
            if (IsControlledByTavernBob())
            {
                return GetLinesForTavernBob();
            }

            return GetLinesForHeroInPlayZone();
        }

        private List<string> GetLinesForChooseHero()
        {
            var lines = new List<string>();

            var entity = m_card.GetEntity();

            lines.Add(entity.GetName());

            var armor = entity.GetArmor();

            if (armor > 0)
            {
                lines.Add(LocalizationUtils.Format(LocalizationKey.READ_HERO_CARD_ARMOR, armor));
            }

            try
            {
                var cardId = entity.GetCardId();
                lines.AddRange(AccessibleCardUtils.GetHeroPowerCardLinesForHeroCard(cardId));
                if (GameState.Get().BattlegroundAllowBuddies())
                {
                    lines.AddRange(AccessibleCardUtils.GetHeroBuddyCardLinesForHeroCard(entity));
                }
            }
            catch (Exception e)
            {
                AccessibilityUtils.LogFatalError(e);
            }

            return lines;
        }

        private List<string> GetLinesForHeroInPlayZone()
        {
            if (m_card.GetEntity().IsControlledByFriendlySidePlayer())
            {
                return GetLinesForPlayerHero();
            }
            else
            {
                return GetLinesForOpponentHero();
            }
        }

        private List<string> GetLinesForOpponentHero()
        {
            // Opponent's Hero -> AFK -> Tier 3 -> x health -> <player name>
            var lines = new List<string>();
            lines.AddRange(GetHeader());

            lines.Add(m_card.GetEntity().GetEntityDef().GetName());

            lines.Add(FormatTier(m_card.GetEntity().GetTag(GAME_TAG.PLAYER_TECH_LEVEL)));

            var resources = GetResources();
            if (resources.Length > 0)
            {
                var effects = GetEffects();
                if (effects.Length > 0)
                {
                    resources = $"{resources} {effects}";
                }

                lines.Add(resources);
            }
            else
            {
                AccessibleCardUtils.AddLineIfExists(GetEffects(), lines);
            }

            if (AccessibilityUtils.IsInPvPGame())
            {
                lines.Add(m_card.GetController().GetName());
            }

            return lines;
        }

        private List<string> GetLinesForPlayerHero()
        {
            // Your Hero -> Tier 3 -> x health -> AFK -> <player name>
            var lines = new List<string>();
            lines.AddRange(GetHeader());

            lines.Add(GetControllerTier());

            var resources = GetResources();
            if (resources.Length > 0)
            {
                var effects = GetEffects();
                if (effects.Length > 0)
                {
                    resources = $"{resources} {effects}";
                }

                lines.Add(resources);
            }
            else
            {
                AccessibleCardUtils.AddLineIfExists(GetEffects(), lines);
            }

            lines.Add(m_card.GetEntity().GetEntityDef().GetName());

            if (AccessibilityUtils.IsInPvPGame())
            {
                lines.Add(m_card.GetController().GetName());
            }

            return lines;
        }

        private List<string> GetLinesForTavernBob()
        {
            var lines = new List<string>();

            lines.Add(GetFriendlySidePlayerTier());

            lines.Add(m_card.GetEntity().GetEntityDef().GetName());

            return lines;
        }

        private string GetControllerTier()
        {
            return GetPlayerTier(m_card.GetController());
        }

        private string GetFriendlySidePlayerTier()
        {
            if (TeammateBoardViewer.Get()?.IsViewingTeammate() ?? false)
            {
                return FormatTier(TeammateGameModeButtonViewer.GetTeammateTechLevelInt());
            }
            return GetPlayerTier(GameState.Get().GetFriendlySidePlayer());
        }

        private string GetPlayerTier(Player player)
        {
            return FormatTier(player.GetTag(GAME_TAG.PLAYER_TECH_LEVEL));
        }

        private string GetTier()
        {
            return FormatTier(m_card.GetEntity().GetTag(GAME_TAG.TECH_LEVEL));
        }

        private string FormatTier(int tier)
        {
            return LocalizationUtils.Format(LocalizationKey.BATTLEGROUNDS_GAMEPLAY_READ_CARD_TIER, tier);
        }

        private bool IsControlledByTavernBob()
        {
            return AccessibleGameplayUtils.IsInBattlegroundsShopPhase() && !m_card.GetEntity().IsControlledByFriendlySidePlayer();
        }

        private List<string> GetHeaderWithRarity()
        {
            if (!m_card.GetEntity().IsMinion())
            {
                return GetHeader();
            }

            var ret = new List<string>();

            ret.Add(AccessibleCardUtils.GetInGameCardNameWithPremium(m_card));

            AddTrailingHeader(ret);

            return ret;
        }

        // Used for quickly describing the opponent's board when combat phase starts
        public string GetMinionSummary()
        {
            var name = AccessibleCardUtils.GetInGameCardNameWithPremium(m_card);

            var effects = GetEffectsNotInEntityDef();
            var resources = GetResources();

            return $"{name} {resources} {effects}";
        }

        protected List<string> GetLinesForBattlegroundsQuestReward()
        {
            var lines = new List<string>();
            lines.Add(m_card.GetEntity().GetEntityDef().GetName());

            AccessibleCardUtils.AddLineIfExists(GetDescription(), lines);

            return lines;
        }

        private List<string> GetLinesForBattlegroundsTrinket()
        {
            var ret = new List<string>();
            ret.Add(m_card.GetEntity().GetName());
            if (m_card.GetEntity().GetZone() != TAG_ZONE.PLAY)
            {
                AccessibleCardUtils.AddLineIfExists(AccessibleCardUtils.GetCost(m_card), ret);
            }
            ret.Add(GetDescription());
            AccessibleCardUtils.AddLineIfExists(GetRace(), ret); // Handles spell school as well
            return ret;
        }
    }
}
