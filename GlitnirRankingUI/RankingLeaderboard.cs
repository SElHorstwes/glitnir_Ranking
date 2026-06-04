using System;
using System.Collections.Generic;
using System.Linq;

namespace Glitnir.Ranking
{
    public partial class GlitnirRankingPlugin
    {
        private int GetRankingPosition(RankingEntry target)
        {
            if (target == null || _database == null || _database.Entries == null)
                return int.MaxValue;

            IReadOnlyList<RankingEntry> ordered = GetOrderedRankingEntries();

            for (int i = 0; i < ordered.Count; i++)
            {
                RankingEntry entry = ordered[i];
                if (object.ReferenceEquals(entry, target))
                    return i + 1;

                if (entry != null && string.Equals(entry.PlayerName, target.PlayerName, StringComparison.OrdinalIgnoreCase))
                    return i + 1;
            }

            return int.MaxValue;
        }

        private RankingEntry FindRankingEntryByName(string playerName)
        {
            if (string.IsNullOrWhiteSpace(playerName) || _database == null || _database.Entries == null)
                return null;

            string safeName = SanitizePlayerName(playerName);
            RankingEntry entry;
            if (_entriesByPlayerName.TryGetValue(safeName, out entry))
                return entry;

            RebuildDatabaseIndexes();
            return _entriesByPlayerName.TryGetValue(safeName, out entry) ? entry : null;
        }

        private string GetHudTopAccent(int position)
        {
            if (position == 1)
                return "#FFD700";
            if (position == 2)
                return "#D7DCE5";
            if (position == 3)
                return "#D79A63";
            return "#E8C988";
        }

        private string BuildHudTopLine(int position, RankingEntry entry, string localPlayerName)
        {
            string accent = GetHudTopAccent(position);
            string safeName = entry != null ? entry.PlayerName : "Jogador";
            int points = entry != null ? entry.Points : 0;
            return "<size=22><color=" + accent + "><b>#" + position + "</b></color>  <b>" + safeName + "</b></size>\n"
                 + "<size=15><color=" + HudColorPoints + ">" + points + " pts</color></size>";
        }
    }
}
