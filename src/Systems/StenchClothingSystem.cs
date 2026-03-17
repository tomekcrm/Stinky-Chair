using System;
using StenchMod.Config;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Common.Entities;
using Vintagestory.GameContent;

namespace StenchMod.Systems
{
    /// <summary>
    /// Evaluates the player's current clothing/armor and returns the
    /// appropriate stench gain multiplier based on the configured categories.
    /// </summary>
    public static class StenchClothingSystem
    {
        /// <summary>
        /// Examines the player's character inventory slots and returns the stench
        /// gain multiplier that corresponds to the heaviest equipped armor category.
        /// </summary>
        /// <param name="entity">The player entity to inspect.</param>
        /// <param name="config">Active mod configuration.</param>
        /// <returns>
        /// A float multiplier:
        /// <list type="bullet">
        ///   <item>Heavy armor   → <see cref="StenchConfig.ClothingHeavyMultiplier"/></item>
        ///   <item>Medium armor  → <see cref="StenchConfig.ClothingMediumMultiplier"/></item>
        ///   <item>Light clothing→ <see cref="StenchConfig.ClothingLightMultiplier"/></item>
        ///   <item>Nothing worn  → <see cref="StenchConfig.ClothingNoneMultiplier"/></item>
        /// </list>
        /// </returns>
        public static float GetMultiplier(EntityPlayer entity, StenchConfig config)
        {
            if (entity?.Player == null)
                return config.ClothingLightMultiplier;

            IInventory? inv = entity.Player.InventoryManager
                                   .GetOwnInventory(GlobalConstants.characterInvClassName);

            if (inv == null)
                return config.ClothingLightMultiplier;

            bool hasHeavy  = false;
            bool hasMedium = false;
            bool hasLight  = false;

            foreach (ItemSlot slot in inv)
            {
                if (slot.Empty)
                    continue;

                // Determine category from ItemSlotCharacter.Type (armor slots) first,
                // then fall back to item code string matching.
                bool slotIsHeavyArmorSlot = false;

                if (slot is ItemSlotCharacter charSlot)
                {
                    EnumCharacterDressType dressType = charSlot.Type;
                    // ArmorHead = 12, ArmorBody = 13, ArmorLegs = 14
                    slotIsHeavyArmorSlot = dressType == EnumCharacterDressType.ArmorHead
                                       || dressType == EnumCharacterDressType.ArmorBody
                                       || dressType == EnumCharacterDressType.ArmorLegs;
                }

                string itemCode = slot.Itemstack?.Item?.Code?.ToString()
                               ?? slot.Itemstack?.Block?.Code?.ToString()
                               ?? string.Empty;

                // Heavy: slot is an armor slot OR item code matches heavy codes
                if (slotIsHeavyArmorSlot || ContainsAny(itemCode, config.HeavyArmorCodes))
                {
                    hasHeavy = true;
                    break; // Heavy is the maximum — no need to check further
                }

                // Medium: item code matches medium codes
                if (ContainsAny(itemCode, config.MediumArmorCodes))
                {
                    hasMedium = true;
                    continue;
                }

                // Anything else with an item is considered light clothing
                if (!string.IsNullOrEmpty(itemCode))
                    hasLight = true;
            }

            if (hasHeavy)  return config.ClothingHeavyMultiplier;
            if (hasMedium) return config.ClothingMediumMultiplier;
            if (hasLight)  return config.ClothingLightMultiplier;

            // No items in any clothing slot → naked
            return config.ClothingNoneMultiplier;
        }

        // -------------------------------------------------------------------------
        // Helpers
        // -------------------------------------------------------------------------

        private static bool ContainsAny(string source, System.Collections.Generic.List<string> substrings)
        {
            if (string.IsNullOrEmpty(source) || substrings == null)
                return false;

            foreach (string sub in substrings)
            {
                if (!string.IsNullOrEmpty(sub) &&
                    source.Contains(sub, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }
    }
}
