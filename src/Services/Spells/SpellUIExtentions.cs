﻿using EQTool.Models;
using System.Collections.Generic;
using System.Linq;

namespace EQTool.Services.Spells
{
    public static class SpellUIExtentions
    {
        public static bool HideSpell(List<PlayerClasses> showSpellsForClasses, Dictionary<PlayerClasses, int> spellclasses)
        {
            if (!spellclasses.Any())
            {
                return false;
            }

            foreach (var showspellclass in showSpellsForClasses)
            {
                if (spellclasses.ContainsKey(showspellclass))
                {
                    return false;
                }
            }

            return true;
        }
    }
}
