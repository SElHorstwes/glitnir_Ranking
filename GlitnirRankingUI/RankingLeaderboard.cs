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

            List<RankingEntry> ordered = _database.Entries
                .Where(x => x != null && !string.IsNullOrWhiteSpace(x.PlayerName) && !ShouldIgnorePlayerForRanking(x.PlayerName))
                .OrderByDescending(x => x.Points)
                .ThenByDescending(x => x.BossPointsTotal)
                .ThenByDescending(x => x.KillPointsTotal)
                .ThenByDescending(x => x.SkillPointsTotal)
                .ThenBy(x => x.PlayerName)
                .ToList();

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

            return _database.Entries.FirstOrDefault(x =>
                x != null &&
                !string.IsNullOrWhiteSpace(x.PlayerName) &&
                string.Equals(x.PlayerName, playerName, StringComparison.OrdinalIgnoreCase));
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
