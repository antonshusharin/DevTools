using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Accessibility
{
    public abstract class AccessibleCard : AccessibleItem
    {
        protected readonly Card m_card;

        internal AccessibleCard(AccessibleComponent parent, Card card) : base(parent)
        {
            m_card = card;
        }

        internal Card GetCard()
        {
            return m_card;
        }

        public static AccessibleCard CreateCard(AccessibleComponent parent, Card card)
		{
            if (AccessibleGameplayUtils.IsPlayingBattlegrounds())
			{
				return new AccessibleBattlegroundsCard(parent, card);
			}
            else
			{
                // Use traditional for everything else so players can play other unsupported game modes such as Mercenaries with OCR
                return new AccessibleTraditionalCard(parent, card);
			}
		}

        #region Common stuff

        protected List<string> GetHeader()
		{
            var ret = new List<string>();

            var name = GetName();

            ret.Add(name);
            AddTrailingHeader(ret);

            return ret;
		}

		protected void AddTrailingHeader(List<string> ret)
        {
            if (AccessibleCardUtils.IsCursed(m_card.GetEntity()))
            {
                ret.Add(LocalizationUtils.Get(LocalizationKey.GLOBAL_CURSED));
            }
            if (m_card.GetEntity().HasTag(GAME_TAG.VALEERASHADOW))
            {
                ret.Add(LocalizationUtils.Get(LocalizationKey.GLOBAL_HAUNTED));
            }
            if (AccessibleCardUtils.IsReady(m_card))
            {
                ret.Add(LocalizationUtils.Get(LocalizationKey.GLOBAL_READY));
            }

            if (m_card.GetEntity().GetZone() == TAG_ZONE.SECRET && m_card.GetEntity().IsObjective())
            {
                ret.Add(m_card.GetEntity().GetCustomObjectiveBannerText());
            }

            AccessibleCardUtils.AddLineIfExists(AccessibleCardUtils.GetCardPingedText(m_card.GetActor()), ret);
        }

        protected string GetName()
        {
            if (m_card.GetEntity().IsHero() && m_card.GetEntity().GetZone() == TAG_ZONE.PLAY)
            {
                if (m_card.GetEntity().IsControlledByFriendlySidePlayer())
                {
                    return LocalizationUtils.Get(LocalizationKey.GAMEPLAY_ZONE_PLAYER_HERO);
                }
                else
                {
                    return LocalizationUtils.Get(LocalizationKey.GAMEPLAY_ZONE_OPPONENT_HERO);
                }
            }
            else if (m_card.GetEntity().IsSecret() && m_card.GetControllerSide() != Player.Side.FRIENDLY)
            {
                var secretClass = GameStrings.GetClassName(m_card.GetEntity().GetClass());
                return LocalizationUtils.Format(LocalizationKey.GLOBAL_SECRET, secretClass);
            }
            else
            {
                return m_card.GetEntity().GetName();
            }
        }

        protected string GetEffects()
        {
            // Note: Remember to update GetEffectsNotInEntityDef() as well if this needs updating
            // Could simply have a flag defaulting instead but that would add significant complexity and I'd rather split the fault domains for now

            List<string> effects = new List<string>();

            var entity = m_card.GetEntity();

            if (AccessibleCardUtils.IsSilencedMinion(entity)) effects.Add(LocalizedText.GLOBAL_SILENCE);
                        if (entity.HasTag(GAME_TAG.DARKMOON_TICKET)) effects.Add(LocalizationUtils.Get(LocalizationKey.BATTLEGROUNDS_GAMEPLAY_DARKMOON_TICKET));
            if (entity.HasDivineShield()) effects.Add(LocalizedText.GLOBAL_DIVINE_SHIELD);
            if (entity.IsFrozen()) effects.Add(LocalizedText.GLOBAL_FROZEN);
            if (entity.HasLifesteal()) effects.Add(LocalizedText.GLOBAL_LIFESTEAL);


            if (entity.HasDeathrattle()) effects.Add(LocalizedText.GLOBAL_DEATHRATTLE);
            if (entity.IsPoisonous()) effects.Add(LocalizedText.GLOBAL_POISONOUS);
            if (entity.IsStealthed()) effects.Add(LocalizedText.GLOBAL_STEALTH);
            if (entity.HasTaunt()) effects.Add(LocalizedText.GLOBAL_TAUNT);
            if (entity.HasTag(GAME_TAG.ELUSIVE)) effects.Add(LocalizedText.GLOBAL_ELUSIVE);
            if (entity.IsImmune()) effects.Add(LocalizedText.GLOBAL_IMMUNE);
            if (entity.IsDormant()) effects.Add(LocalizedText.GLOBAL_DORMANT);
            if (entity.HasReborn()) effects.Add(LocalizedText.GLOBAL_REBORN);
            if (entity.HasWindfury()) effects.Add(LocalizedText.GLOBAL_WINDFURY);
            if (entity.IsVenomous()) effects.Add(LocalizedText.GLOBAL_VENOMOUS);
            if (entity.HasTag(GAME_TAG.HAS_DARK_GIFT)) effects.Add(LocalizedText.GLOBAL_DARK_GIFT);

            if (effects.Count == 0)
            {
                return "";
            }

            return AccessibleSpeechUtils.HumanizeList(effects);
        }

        protected string GetEffectsNotInEntityDef()
        {
            // Note: This is only used for Battlegrounds at the moment to reduce the chattiness when narrating the opponent's board at the start of the combat phase
            List<string> effects = new List<string>();

            var entity = m_card.GetEntity();
            var entityDef = entity.GetEntityDef();

            if (AccessibleCardUtils.IsSilencedMinion(entity)) effects.Add(LocalizedText.GLOBAL_SILENCE);

            if (entity.HasDivineShield() && !entityDef.HasDivineShield()) effects.Add(LocalizedText.GLOBAL_DIVINE_SHIELD);
            if (entity.IsFrozen() && !entityDef.IsFrozen()) effects.Add(LocalizedText.GLOBAL_FROZEN);
            if (entity.HasLifesteal() && !entityDef.HasLifesteal()) effects.Add(LocalizedText.GLOBAL_LIFESTEAL);
            if (entity.HasDeathrattle() && !entityDef.HasDeathrattle()) effects.Add(LocalizedText.GLOBAL_DEATHRATTLE);
            if (entity.IsPoisonous() && !entityDef.IsPoisonous()) effects.Add(LocalizedText.GLOBAL_POISONOUS);
            if (entity.IsStealthed() && !entityDef.IsStealthed()) effects.Add(LocalizedText.GLOBAL_STEALTH);
            if (entity.HasTaunt() && !entityDef.HasTaunt()) effects.Add(LocalizedText.GLOBAL_TAUNT);
            if (entity.IsImmune() && !entityDef.IsImmune()) effects.Add(LocalizedText.GLOBAL_IMMUNE);
            if (entity.IsDormant() && !entityDef.IsDormant()) effects.Add(LocalizedText.GLOBAL_DORMANT);
            if (entity.HasReborn() && !entityDef.HasReborn()) effects.Add(LocalizedText.GLOBAL_REBORN);
            if (entity.HasWindfury() && !entityDef.HasWindfury()) effects.Add(LocalizedText.GLOBAL_WINDFURY);
            if (entity.IsVenomous() && !entityDef.IsVenomous()) effects.Add(LocalizedText.GLOBAL_VENOMOUS);

            if (effects.Count == 0)
            {
                return "";
            }

            return AccessibleSpeechUtils.HumanizeList(effects);
        }

        protected List<string> GetLinesForHeroPower()
        {
            var lines = new List<string>();
            lines.AddRange(GetHeader());

            if (!AccessibleCardUtils.IsCostHidden(m_card))
            {
                lines.Add(AccessibleCardUtils.GetCost(m_card));
            }

            AccessibleCardUtils.AddLineIfExists(GetDescription(), lines);

            return lines;
        }

        protected string GetDescription()
        {
            Entity entity = m_card.GetEntity();

            if (AccessibleCardUtils.IsSilencedMinion(entity))
            {
                return LocalizedText.GLOBAL_SILENCE;
            }

            return entity.GetCardTextBuilder().BuildCardTextInHand(entity);
        }

        protected List<string> GetLinesForOpponentSecret()
        {
            var lines = new List<string>();
            lines.AddRange(GetHeader());
            return lines;
        }

        protected string GetResources()
        {
            var entity = m_card.GetEntity();

            if (!entity.IsLaunchpad() && (entity.IsDormant() || entity.HasTag(GAME_TAG.DORMANT_VISUAL)))
            {
                var dormantBannerText = GetDormantBannerText(entity);
                if (dormantBannerText.Length > 0)
                {
                    return dormantBannerText;
                }

                return "";
            }
            else if (entity.GetTag(GAME_TAG.HIDE_STATS) == 1 && !entity.IsStarship())
            {
                // e.g. Divine Bell in BoH Garrosh 7
                return "";
            }
            else if (entity.IsMinion()) {
                var atk = entity.GetATK();
                var hp = entity.GetCurrentHealth();
                return LocalizationUtils.Format(LocalizationKey.READ_CARD_ATK_HEALTH, atk, hp);
            }
            else if (entity.IsWeapon()) {
                var atk = entity.GetATK();
                var durability = entity.GetCurrentHealth();
                return LocalizationUtils.Format(LocalizationKey.READ_CARD_ATK_DURABILITY, atk, durability);
            }
            else if (entity.IsLocation()) {
                // This is internally tracked as health, but most game literature refers to it as durability so we do too
                return LocalizationUtils.Format(LocalizationKey.READ_CARD_DURABILITY, entity.GetCurrentHealth());
            }
            else if (entity.IsHero())
            {
                var atk = entity.GetATK();
                var hp = entity.GetCurrentHealth();
                var armor = entity.GetArmor();

                var atkHidden = AccessibleCardUtils.IsAttackHidden(m_card);
                var hpHidden = AccessibleCardUtils.IsHealthHidden(m_card);
                var armorHidden = AccessibleCardUtils.IsArmorHidden(m_card);

                var showAtk = !atkHidden && atk > 0;
                var showHp = !hpHidden;
                var showArmor = !armorHidden && armor > 0;

                var stats = new List<string>();

                if (showAtk)
                {
                    stats.Add(LocalizationUtils.Format(LocalizationKey.READ_HERO_CARD_ATK, atk));
                }
                if (showArmor)
                {
                    stats.Add(LocalizationUtils.Format(LocalizationKey.READ_HERO_CARD_ARMOR, armor));
                }
                if (showHp)
                {
                    stats.Add(LocalizationUtils.Format(LocalizationKey.READ_HERO_CARD_HEALTH, hp));
                }

                return AccessibleSpeechUtils.HumanizeList(stats);
            }
            else
            {
                return "";
            }
        }

        private static string GetDormantBannerText(Entity entity)
        {
            if (entity == null || !entity.HasTag(GAME_TAG.ENCHANTMENT_BANNER_TEXT))
            {
                return "";
            }

            var bannerText = entity.GetCustomEnchantmentBannerText();
            if (bannerText == null || bannerText.Length == 0)
            {
                return "";
            }

            return bannerText;
        }

        protected string GetCardType()
        {
            return AccessibleCardUtils.GetType(m_card.GetEntity().GetCardType());
        }

        protected string GetRace()
        {
            // Note: This handles spell school as well
            return m_card.GetEntity().GetEntityDef().GetRaceText();
        }

        protected string GetFaction()
        {
            return AccessibleCardUtils.GetFaction(m_card.GetEntity().GetEntityDef());
        }

        protected List<string> GetLinesForQuest()
        {
            var lines = new List<string>();
            lines.AddRange(GetHeader());

            AccessibleCardUtils.AddLineIfExists(GetDescription(), lines);
            AccessibleCardUtils.AddLineIfExists(GetQuestProgress(), lines);
            lines.AddRange(GetQuestReward());

            return lines;
        }

        protected string GetQuestProgress()
        {
            return AccessibleCardUtils.GetQuestProgressLine(m_card.GetEntity());
        }

        protected List<string> GetQuestReward()
        {
            var rewardCardId = AccessibleCardUtils.GetRewardCardIDFromQuestCardID(m_card.GetEntity());

            if (rewardCardId == null)
            {
                return new List<string>();
            }

            return AccessibleCardUtils.GetQuestRewardCardLines(m_card.GetEntity(), rewardCardId);
        }

		#endregion
	}
}
