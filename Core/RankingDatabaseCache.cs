using System;
using System.Collections.Generic;
using System.Linq;

namespace Glitnir.Ranking
{
    public partial class GlitnirRankingPlugin
    {
        private readonly Dictionary<string, RankingEntry> _entriesByPlayerName = new Dictionary<string, RankingEntry>(StringComparer.OrdinalIgnoreCase);
        private List<RankingEntry> _orderedRankingCache = new List<RankingEntry>();
        private Dictionary<string, int> _rankingSnapshotCache = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        private bool _rankingCacheDirty = true;
        private readonly HashSet<string> _marketplaceQuestCreditKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _fishingCatchCreditKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, List<MarketplaceQuestCreditRecord>> _marketplaceQuestCreditsByPlayer = new Dictionary<string, List<MarketplaceQuestCreditRecord>>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, List<FishingCatchCreditRecord>> _fishingCreditsByPlayer = new Dictionary<string, List<FishingCatchCreditRecord>>(StringComparer.OrdinalIgnoreCase);

        private void RebuildDatabaseIndexes()
        {
            _entriesByPlayerName.Clear();
            _marketplaceQuestCreditKeys.Clear();
            _fishingCatchCreditKeys.Clear();
            _marketplaceQuestCreditsByPlayer.Clear();
            _fishingCreditsByPlayer.Clear();

            if (_database == null || _database.Entries == null)
            {
                _orderedRankingCache = new List<RankingEntry>();
                _rankingSnapshotCache = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                _rankingCacheDirty = false;
                return;
            }

            foreach (RankingEntry entry in _database.Entries)
            {
                if (entry == null || string.IsNullOrWhiteSpace(entry.PlayerName))
                    continue;

                string safeName = SanitizePlayerName(entry.PlayerName);
                entry.PlayerName = safeName;

                if (!_entriesByPlayerName.ContainsKey(safeName))
                    _entriesByPlayerName.Add(safeName, entry);
            }

            if (_database.MarketplaceQuestCredits != null)
            {
                foreach (MarketplaceQuestCreditRecord credit in _database.MarketplaceQuestCredits)
                {
                    if (credit == null)
                        continue;

                    string key = BuildMarketplaceQuestCreditKey(credit.PlayerName, credit.QuestKey);
                    if (!string.IsNullOrWhiteSpace(key))
                        _marketplaceQuestCreditKeys.Add(key);

                    string player = SanitizePlayerName(credit.PlayerName);
                    if (!_marketplaceQuestCreditsByPlayer.TryGetValue(player, out List<MarketplaceQuestCreditRecord> list))
                    {
                        list = new List<MarketplaceQuestCreditRecord>();
                        _marketplaceQuestCreditsByPlayer[player] = list;
                    }
                    list.Add(credit);
                }
            }

            if (_database.FishingCatchCredits != null)
            {
                foreach (FishingCatchCreditRecord credit in _database.FishingCatchCredits)
                {
                    if (credit == null)
                        continue;

                    string key = SafeKey(credit.ZdoKey);
                    if (!string.IsNullOrWhiteSpace(key))
                        _fishingCatchCreditKeys.Add(key);

                    string player = SanitizePlayerName(credit.PlayerName);
                    if (!_fishingCreditsByPlayer.TryGetValue(player, out List<FishingCatchCreditRecord> list))
                    {
                        list = new List<FishingCatchCreditRecord>();
                        _fishingCreditsByPlayer[player] = list;
                    }
                    list.Add(credit);
                }
            }

            _rankingCacheDirty = true;
        }

        private void RegisterRankingEntry(RankingEntry entry)
        {
            if (entry == null || string.IsNullOrWhiteSpace(entry.PlayerName))
                return;

            string safeName = SanitizePlayerName(entry.PlayerName);
            entry.PlayerName = safeName;
            _entriesByPlayerName[safeName] = entry;
            MarkRankingCacheDirty();
        }

        private void MarkRankingCacheDirty()
        {
            _rankingCacheDirty = true;
        }

        private IReadOnlyList<RankingEntry> GetOrderedRankingEntries()
        {
            if (!_rankingCacheDirty)
                return _orderedRankingCache;

            if (_database == null || _database.Entries == null)
            {
                _orderedRankingCache = new List<RankingEntry>();
                _rankingSnapshotCache = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                _rankingCacheDirty = false;
                return _orderedRankingCache;
            }

            _orderedRankingCache = _database.Entries
                .Where(x => x != null && !string.IsNullOrWhiteSpace(x.PlayerName) && !ShouldIgnorePlayerForRanking(x.PlayerName))
                .OrderByDescending(x => x.Points)
                .ThenByDescending(x => x.BossPointsTotal)
                .ThenByDescending(x => x.KillPointsTotal)
                .ThenByDescending(x => x.SkillPointsTotal)
                .ThenBy(x => x.PlayerName)
                .ToList();

            Dictionary<string, int> snapshot = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < _orderedRankingCache.Count; i++)
            {
                RankingEntry entry = _orderedRankingCache[i];
                if (entry != null && !string.IsNullOrWhiteSpace(entry.PlayerName))
                    snapshot[entry.PlayerName] = i + 1;
            }

            _rankingSnapshotCache = snapshot;
            _rankingCacheDirty = false;
            return _orderedRankingCache;
        }


        private bool HasMarketplaceQuestCreditCached(string playerName, string questKey)
        {
            if (_database == null || _database.MarketplaceQuestCredits == null)
                return false;

            string key = BuildMarketplaceQuestCreditKey(playerName, questKey);
            if (_marketplaceQuestCreditKeys.Contains(key))
                return true;

            RebuildDatabaseIndexes();
            return _marketplaceQuestCreditKeys.Contains(key);
        }

        private bool HasFishingCatchCreditCached(string zdoKey)
        {
            if (string.IsNullOrWhiteSpace(zdoKey) || _database == null || _database.FishingCatchCredits == null)
                return false;

            string key = SafeKey(zdoKey);
            if (_fishingCatchCreditKeys.Contains(key))
                return true;

            RebuildDatabaseIndexes();
            return _fishingCatchCreditKeys.Contains(key);
        }

        private List<MarketplaceQuestCreditRecord> GetMarketplaceQuestCreditsForPlayer(string playerName)
        {
            string safeName = SanitizePlayerName(playerName);
            if (_marketplaceQuestCreditsByPlayer.TryGetValue(safeName, out List<MarketplaceQuestCreditRecord> list))
                return list;

            RebuildDatabaseIndexes();
            return _marketplaceQuestCreditsByPlayer.TryGetValue(safeName, out list) ? list : new List<MarketplaceQuestCreditRecord>();
        }

        private List<FishingCatchCreditRecord> GetFishingCreditsForPlayer(string playerName)
        {
            string safeName = SanitizePlayerName(playerName);
            if (_fishingCreditsByPlayer.TryGetValue(safeName, out List<FishingCatchCreditRecord> list))
                return list;

            RebuildDatabaseIndexes();
            return _fishingCreditsByPlayer.TryGetValue(safeName, out list) ? list : new List<FishingCatchCreditRecord>();
        }
        private Dictionary<string, int> GetRankingSnapshotFromCache()
        {
            GetOrderedRankingEntries();
            return new Dictionary<string, int>(_rankingSnapshotCache, StringComparer.OrdinalIgnoreCase);
        }
    }
}



