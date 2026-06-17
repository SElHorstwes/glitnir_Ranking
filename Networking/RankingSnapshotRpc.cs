using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace Glitnir.Ranking
{
    public partial class GlitnirRankingPlugin
    {
        private void CheckInitialServerSnapshotRequest()
        {
            try
            {
                if (!_rpcsRegistered || ZRoutedRpc.instance == null || ZNet.instance == null)
                    return;

                long serverUid = GetServerPeerUid();

                if (serverUid == 0L)
                {
                    _lastKnownServerPeerUid = 0L;
                    _requestedInitialServerSnapshot = false;
                    _lastSnapshotSyncPlayerName = "";
                    _lastSnapshotHadLocalPlayer = false;
                    return;
                }

                bool hasLocalPlayer = Player.m_localPlayer != null;
                string localPlayerName = hasLocalPlayer ? GetLocalPlayerName() : "";

                if (!hasLocalPlayer)
                {



                    _requestedInitialServerSnapshot = false;
                    _lastSnapshotSyncPlayerName = "";
                    _lastSnapshotHadLocalPlayer = false;
                    return;
                }

                bool serverChanged = serverUid != _lastKnownServerPeerUid;
                bool playerChanged = !string.Equals(localPlayerName, _lastSnapshotSyncPlayerName, StringComparison.OrdinalIgnoreCase);
                bool playerReappeared = !_lastSnapshotHadLocalPlayer && hasLocalPlayer;

                if (serverChanged || playerChanged || playerReappeared)
                {
                    _lastKnownServerPeerUid = serverUid;
                    _lastSnapshotSyncPlayerName = localPlayerName;
                    _lastSnapshotHadLocalPlayer = true;
                    _requestedInitialServerSnapshot = false;
                    _cachedTopText = "Sincronizando ranking...";
                    _cachedPlayerText = "Aguardando dados do servidor...";
                }

                if (!_requestedInitialServerSnapshot)
                {
                    _requestedInitialServerSnapshot = true;
                    _clientRefreshTimer = 0f;
                    RequestSnapshotFromServer(true);
                }
            }
            catch (Exception ex)
            {
                Logger.LogWarning("[Ranking] Falha ao solicitar snapshot inicial: " + ex.Message);
            }
        }

        private Dictionary<string, int> GetRankingSnapshot()
        {
            return GetRankingSnapshotFromCache();
        }

        private void RequestSnapshotFromServer(bool force = false)
        {
            try
            {
                if (!force && Time.unscaledTime - _lastSnapshotRequestTime < ClientRefreshInterval)
                    return;

                if (IsServerInstance())
                {
                    long serverPeerForClient = GetServerPeerUid();

                    if (serverPeerForClient == 0L)
                    {
                        string localPlayerName = GetLocalPlayerName();
                        string topText;
                        string playerText;
                        List<SnapshotTopEntryData> topEntries;
                        SnapshotPlayerData playerData;

                        BuildSnapshotTexts(localPlayerName, out topText, out playerText);
                        BuildSnapshotData(localPlayerName, out topEntries, out playerData);

                        _cachedTopText = topText;
                        _cachedPlayerText = playerText;
                        CacheSnapshotData(topEntries, playerData);
                        SetStatus("Snapshot local atualizado.", 2f);
                        _lastSnapshotRequestTime = Time.unscaledTime;
                        return;
                    }
                }

                if (!_rpcsRegistered || ZRoutedRpc.instance == null || ZNet.instance == null)
                    return;

                long serverUid = GetServerPeerUid();
                if (serverUid == 0L)
                    return;

                ZPackage pkg = new ZPackage();
                pkg.Write(GetLocalPlayerName());
                ZRoutedRpc.instance.InvokeRoutedRPC(serverUid, RpcRequestSnapshot, pkg);
                _lastSnapshotRequestTime = Time.unscaledTime;
            }
            catch (Exception ex)
            {
                Logger.LogError("Erro ao solicitar snapshot do ranking: " + ex);
            }
        }

        private void RPC_RequestSnapshot(long sender, ZPackage pkg)
        {
            try
            {
                if (!IsServerInstance())
                    return;

                string playerName = "";
                if (pkg != null)
                    playerName = SanitizePlayerName(pkg.ReadString());

                DebugLog(DebugCategory.Snapshot, "Snapshot solicitado por sender=" + sender + " player=" + playerName + " entries=" + _database.Entries.Count);

                string topText;
                string playerText;
                List<SnapshotTopEntryData> topEntries;
                SnapshotPlayerData playerData;

                BuildSnapshotTexts(playerName, out topText, out playerText);
                BuildSnapshotData(playerName, out topEntries, out playerData);

                ZPackage response = new ZPackage();
                response.Write(topText);
                response.Write(playerText);
                WriteSnapshotPayload(response, topEntries, playerData);
                ZRoutedRpc.instance.InvokeRoutedRPC(sender, RpcReceiveSnapshot, response);
            }
            catch (Exception ex)
            {
                Logger.LogError("Erro no RPC_RequestSnapshot: " + ex);
            }
        }

        private void RPC_ReceiveSnapshot(long sender, ZPackage pkg)
        {
            try
            {
                if (pkg == null)
                    return;

                _cachedTopText = pkg.ReadString();
                _cachedPlayerText = pkg.ReadString();
                ReadSnapshotPayload(pkg);
                SetStatus("Ranking sincronizado.", 1f);
                _unityHudNextRefresh = 0f;
                if (IsUnityRankingHudVisible())
                    UpdateUnityRankingHud();
            }
            catch (Exception ex)
            {
                Logger.LogError("Erro no RPC_ReceiveSnapshot: " + ex);
            }
        }

        private void CacheSnapshotData(List<SnapshotTopEntryData> topEntries, SnapshotPlayerData playerData)
        {
            _cachedTopEntries.Clear();
            if (topEntries != null)
                _cachedTopEntries.AddRange(topEntries);

            _cachedPlayerData = playerData ?? new SnapshotPlayerData();
        }

        private void BuildSnapshotData(string playerName, out List<SnapshotTopEntryData> topEntries, out SnapshotPlayerData playerData)
        {
            topEntries = new List<SnapshotTopEntryData>();
            playerData = new SnapshotPlayerData
            {
                RankingEnabled = _rules.RankingEnabled,
                EnableKillPoints = _rules.EnableKillPoints,
                EnableBossPoints = _rules.EnableBossPoints,
                EnableSkillPoints = _rules.EnableSkillPoints,
                EnableMarketplaceQuestPoints = _rules.EnableMarketplaceQuestPoints,
                EnableDeathPenalty = _rules.EnableDeathPenalty,
                DeathPenaltyUseMultiplier = _rules.DeathPenaltyUseMultiplier,
                DeathPenaltyPerDeath = Mathf.Max(0, _rules.DeathPenaltyPerDeath),
                TopCount = _rules.TopCount,
                HudKillRules = FormatCategorizedIntRulesForHud(_rules.KillPoints, _rules.CombatHudCategories, true),
                HudBossRules = FormatIntRulesForHud(_rules.BossPoints),
                HudSkillJackpotRules = FormatSkillJackpotRulesForHud(_rules.SkillMilestoneJackpotPoints),
                HudMarketplaceQuestRules = FormatIntRulesForHud(_rules.MarketplaceQuestPoints),
                HudFishingRules = FormatIntRulesForHud(_rules.FishingPoints),
                HudCraftRules = FormatCategorizedIntRulesForHud(_rules.CraftPoints, _rules.ProductionHudCategories, false),
                HudFarmJackpotRules = FormatCategorizedJackpotRulesForHud(_rules.FarmJackpots, _rules.ProductionHudCategories, false),
                HudUniqueCraftJackpotRules = FormatCategorizedJackpotRulesForHud(_rules.UniqueCraftJackpots, _rules.ProductionHudCategories, false),
                HudDeathPenaltyRules = FormatDeathPenaltyRulesForHud(_rules.DeathPenaltyRules),
                HudExplorationMapJackpotRules = FormatExplorationMapJackpotRulesForHud(_rules.ExplorationMapJackpots)
            };

            if (!_rules.RankingEnabled)
            {
                FillRewardSnapshotData(playerData);
                return;
            }

            IReadOnlyList<RankingEntry> ordered = GetOrderedRankingEntries();

            playerName = SanitizePlayerName(playerName);
            int limit = Mathf.Min(Mathf.Clamp(_rules.TopCount, 1, 50), ordered.Count);

            for (int i = 0; i < limit; i++)
            {
                RankingEntry entry = ordered[i];
                topEntries.Add(new SnapshotTopEntryData
                {
                    Position = i + 1,
                    PlayerName = entry != null ? entry.PlayerName : "Jogador",
                    Points = entry != null ? entry.Points : 0,
                    IsLocalPlayer = entry != null && string.Equals(entry.PlayerName, playerName, StringComparison.OrdinalIgnoreCase),
                    TotalKillsPontuadas = entry != null ? entry.TotalKillsPontuadas : 0,
                    TotalBossesPontuadas = entry != null ? entry.TotalBossesPontuadas : 0,
                    TotalSkillLevelUpsPontuados = entry != null ? entry.TotalSkillLevelUpsPontuados : 0,
                    TotalMarketplaceQuestsPontuadas = entry != null ? CountMarketplaceQuestCreditsForPlayer(entry.PlayerName) : 0,
                    MarketplaceQuestPointsTotal = entry != null ? CalculateMarketplaceQuestPointsForPlayer(entry.PlayerName) : 0,
                    TotalFishingPontuadas = entry != null ? entry.TotalFishingPontuadas : 0,
                    TotalCraftPontuadas = entry != null ? entry.TotalCraftPontuadas : 0,
                    TotalFarmJackpotsPontuados = entry != null ? entry.TotalFarmJackpotsPontuados : 0,
                    TotalUniqueCraftJackpotsPontuados = entry != null ? entry.TotalUniqueCraftJackpotsPontuados : 0,
                    TotalDeaths = entry != null ? entry.TotalDeaths : 0,
                    KillPointsTotal = entry != null ? entry.KillPointsTotal : 0,
                    BossPointsTotal = entry != null ? entry.BossPointsTotal : 0,
                    SkillPointsTotal = entry != null ? entry.SkillPointsTotal : 0,
                    FishingPointsTotal = entry != null ? entry.FishingPointsTotal : 0,
                    CraftPointsTotal = entry != null ? entry.CraftPointsTotal : 0,
                    FarmJackpotPointsTotal = entry != null ? entry.FarmJackpotPointsTotal : 0,
                    UniqueCraftJackpotPointsTotal = entry != null ? entry.UniqueCraftJackpotPointsTotal : 0,
                    DeathPenaltyPointsTotal = entry != null ? entry.DeathPenaltyPointsTotal : 0,
                    TotalPointsExchanges = entry != null ? entry.TotalPointsExchanges : 0,
                    PointsExchangePenaltyTotal = entry != null ? entry.PointsExchangePenaltyTotal : 0,
                    PointsExchangeCoinsTotal = entry != null ? entry.PointsExchangeCoinsTotal : 0,
                    ExplorationMapJackpotPointsTotal = entry != null ? entry.ExplorationMapJackpotPointsTotal : 0,
                    LastReason = entry != null ? (entry.LastReason ?? "") : "",
                    LastUpdateUtc = entry != null ? (entry.LastUpdateUtc ?? "") : ""
                });
            }

            for (int i = 0; i < ordered.Count; i++)
            {
                RankingEntry entry = ordered[i];
                if (entry == null)
                    continue;

                if (!string.Equals(entry.PlayerName, playerName, StringComparison.OrdinalIgnoreCase))
                    continue;

                playerData.HasData = true;
                playerData.Position = i + 1;
                playerData.PlayerName = entry.PlayerName;
                playerData.Points = entry.Points;
                playerData.TotalKillsPontuadas = entry.TotalKillsPontuadas;
                playerData.TotalBossesPontuadas = entry.TotalBossesPontuadas;
                playerData.TotalSkillLevelUpsPontuados = entry.TotalSkillLevelUpsPontuados;
                playerData.TotalMarketplaceQuestsPontuadas = CountMarketplaceQuestCreditsForPlayer(entry.PlayerName);
                playerData.MarketplaceQuestPointsTotal = CalculateMarketplaceQuestPointsForPlayer(entry.PlayerName);
                playerData.KillPointsTotal = entry.KillPointsTotal;
                playerData.BossPointsTotal = entry.BossPointsTotal;
                playerData.SkillPointsTotal = entry.SkillPointsTotal;
                playerData.TotalFishingPontuadas = entry.TotalFishingPontuadas;
                playerData.TotalCraftPontuadas = entry.TotalCraftPontuadas;
                playerData.TotalFarmJackpotsPontuados = entry.TotalFarmJackpotsPontuados;
                playerData.TotalUniqueCraftJackpotsPontuados = entry.TotalUniqueCraftJackpotsPontuados;
                playerData.TotalDeaths = entry.TotalDeaths;
                playerData.FishingPointsTotal = entry.FishingPointsTotal;
                playerData.CraftPointsTotal = entry.CraftPointsTotal;
                playerData.FarmJackpotPointsTotal = entry.FarmJackpotPointsTotal;
                playerData.UniqueCraftJackpotPointsTotal = entry.UniqueCraftJackpotPointsTotal;
                playerData.DeathPenaltyPointsTotal = entry.DeathPenaltyPointsTotal;
                playerData.TotalPointsExchanges = entry.TotalPointsExchanges;
                playerData.PointsExchangePenaltyTotal = entry.PointsExchangePenaltyTotal;
                playerData.PointsExchangeCoinsTotal = entry.PointsExchangeCoinsTotal;
                playerData.ExplorationMapJackpotPointsTotal = entry.ExplorationMapJackpotPointsTotal;
                playerData.LastReason = entry.LastReason ?? "";
                playerData.LastUpdateUtc = entry.LastUpdateUtc ?? "";
                playerData.ProgressCounters = entry.ProgressCounters != null
                    ? new Dictionary<string, int>(entry.ProgressCounters, StringComparer.OrdinalIgnoreCase)
                    : new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                EnsureFishingProgressCountersFromCredits(playerData.PlayerName, playerData.ProgressCounters);
                EnsureMarketplaceQuestProgressCountersFromCredits(playerData.PlayerName, playerData.ProgressCounters);
                playerData.FloatProgressCounters = entry.FloatProgressCounters != null
                    ? new Dictionary<string, float>(entry.FloatProgressCounters, StringComparer.OrdinalIgnoreCase)
                    : new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
                FillRewardSnapshotData(playerData);
                return;
            }

            FillRewardSnapshotData(playerData);
        }

        private int CountMarketplaceQuestCreditsForPlayer(string playerName)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(playerName) || _database == null || _database.MarketplaceQuestCredits == null)
                    return 0;

                return GetMarketplaceQuestCreditsForPlayer(playerName).Count;
            }
            catch
            {
                return 0;
            }
        }

        private int CalculateMarketplaceQuestPointsForPlayer(string playerName)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(playerName) || _database == null || _database.MarketplaceQuestCredits == null || _rules == null || _rules.MarketplaceQuestPoints == null)
                    return 0;

                int total = 0;
                List<MarketplaceQuestCreditRecord> credits = GetMarketplaceQuestCreditsForPlayer(playerName);

                for (int i = 0; i < credits.Count; i++)
                {
                    MarketplaceQuestCreditRecord credit = credits[i];
                    if (credit == null)
                        continue;

                    int points;
                    if (_rules.MarketplaceQuestPoints.TryGetValue(SafeMarketplaceQuestKey(credit.QuestKey), out points))
                        total = Mathf.Clamp(total + Mathf.Max(0, points), 0, int.MaxValue);
                }

                return total;
            }
            catch
            {
                return 0;
            }
        }

        private void EnsureMarketplaceQuestProgressCountersFromCredits(string playerName, Dictionary<string, int> counters)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(playerName) || counters == null || _database == null || _database.MarketplaceQuestCredits == null)
                    return;

                Dictionary<string, int> countsByQuest = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                List<MarketplaceQuestCreditRecord> credits = GetMarketplaceQuestCreditsForPlayer(playerName);

                for (int i = 0; i < credits.Count; i++)
                {
                    MarketplaceQuestCreditRecord credit = credits[i];
                    if (credit == null)
                        continue;

                    string questKey = SafeMarketplaceQuestKey(credit.QuestKey);
                    if (string.IsNullOrWhiteSpace(questKey))
                        continue;

                    int current = 0;
                    countsByQuest.TryGetValue(questKey, out current);
                    countsByQuest[questKey] = Mathf.Clamp(current + 1, 0, int.MaxValue);
                }

                foreach (KeyValuePair<string, int> pair in countsByQuest)
                {
                    SetProgressCounterMinimum(counters, "MarketplaceQuest", pair.Key, pair.Value);
                    SetProgressCounterMinimum(counters, "Marketplace", pair.Key, pair.Value);
                    SetProgressCounterMinimum(counters, "Quest", pair.Key, pair.Value);
                    SetProgressCounterMinimum(counters, "Quests", pair.Key, pair.Value);
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Erro ao reconstruir contagem de quests Marketplace no snapshot: " + ex);
            }
        }

        private void EnsureFishingProgressCountersFromCredits(string playerName, Dictionary<string, int> counters)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(playerName) || counters == null || _database == null || _database.FishingCatchCredits == null)
                    return;

                Dictionary<string, int> countsByFish = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

                for (int i = 0; i < _database.FishingCatchCredits.Count; i++)
                {
                    FishingCatchCreditRecord credit = _database.FishingCatchCredits[i];
                    if (credit == null)
                        continue;

                    if (!string.Equals(SanitizePlayerName(credit.PlayerName), SanitizePlayerName(playerName), StringComparison.OrdinalIgnoreCase))
                        continue;

                    string fishPrefab = NormalizeFishPrefabName(credit.FishPrefab);
                    if (string.IsNullOrWhiteSpace(fishPrefab) || !IsFishPrefab(fishPrefab))
                        continue;

                    int current = 0;
                    countsByFish.TryGetValue(fishPrefab, out current);
                    countsByFish[fishPrefab] = Mathf.Clamp(current + 1, 0, int.MaxValue);
                }

                foreach (KeyValuePair<string, int> pair in countsByFish)
                {
                    SetProgressCounterMinimum(counters, "Fishing", pair.Key, pair.Value);
                    SetProgressCounterMinimum(counters, "Pesca", pair.Key, pair.Value);
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Erro ao reconstruir contagem de pesca no snapshot: " + ex);
            }
        }

        private void SetProgressCounterMinimum(Dictionary<string, int> counters, string category, string key, int value)
        {
            if (counters == null || string.IsNullOrWhiteSpace(category) || string.IsNullOrWhiteSpace(key) || value <= 0)
                return;

            string progressKey = SafeKey(category) + ":" + SafeKey(key);
            int current = 0;
            counters.TryGetValue(progressKey, out current);
            counters[progressKey] = Mathf.Clamp(Mathf.Max(current, value), 0, int.MaxValue);
        }

        private void WriteSnapshotPayload(ZPackage response, List<SnapshotTopEntryData> topEntries, SnapshotPlayerData playerData)
        {
            if (response == null)
                return;

            int count = topEntries != null ? topEntries.Count : 0;
            response.Write(count);

            for (int i = 0; i < count; i++)
            {
                SnapshotTopEntryData entry = topEntries[i] ?? new SnapshotTopEntryData();
                response.Write(entry.Position);
                response.Write(entry.PlayerName ?? "");
                response.Write(entry.Points);
                response.Write(entry.IsLocalPlayer);
                response.Write(entry.TotalKillsPontuadas);
                response.Write(entry.TotalBossesPontuadas);
                response.Write(entry.TotalSkillLevelUpsPontuados);
                response.Write(entry.TotalFishingPontuadas);
                response.Write(entry.TotalCraftPontuadas);
                response.Write(entry.TotalFarmJackpotsPontuados);
                response.Write(entry.TotalUniqueCraftJackpotsPontuados);
                response.Write(entry.TotalDeaths);
                response.Write(entry.KillPointsTotal);
                response.Write(entry.BossPointsTotal);
                response.Write(entry.SkillPointsTotal);
                response.Write(entry.FishingPointsTotal);
                response.Write(entry.CraftPointsTotal);
                response.Write(entry.FarmJackpotPointsTotal);
                response.Write(entry.UniqueCraftJackpotPointsTotal);
                response.Write(entry.DeathPenaltyPointsTotal);
                response.Write(entry.TotalPointsExchanges);
                response.Write(entry.PointsExchangePenaltyTotal);
                response.Write(entry.PointsExchangeCoinsTotal);
                response.Write(entry.ExplorationMapJackpotPointsTotal);
                response.Write(entry.LastReason ?? "");
                response.Write(entry.LastUpdateUtc ?? "");
                response.Write(entry.TotalMarketplaceQuestsPontuadas);
                response.Write(entry.MarketplaceQuestPointsTotal);
            }

            SnapshotPlayerData data = playerData ?? new SnapshotPlayerData();
            response.Write(data.HasData);
            response.Write(data.Position);
            response.Write(data.PlayerName ?? "");
            response.Write(data.Points);
            response.Write(data.TotalKillsPontuadas);
            response.Write(data.TotalBossesPontuadas);
            response.Write(data.TotalSkillLevelUpsPontuados);
            response.Write(data.KillPointsTotal);
            response.Write(data.BossPointsTotal);
            response.Write(data.SkillPointsTotal);
            response.Write(data.TotalFishingPontuadas);
            response.Write(data.TotalCraftPontuadas);
            response.Write(data.TotalFarmJackpotsPontuados);
            response.Write(data.TotalUniqueCraftJackpotsPontuados);
            response.Write(data.TotalDeaths);
            response.Write(data.FishingPointsTotal);
            response.Write(data.CraftPointsTotal);
            response.Write(data.FarmJackpotPointsTotal);
            response.Write(data.UniqueCraftJackpotPointsTotal);
            response.Write(data.DeathPenaltyPointsTotal);
            response.Write(data.TotalPointsExchanges);
            response.Write(data.PointsExchangePenaltyTotal);
            response.Write(data.PointsExchangeCoinsTotal);
            response.Write(data.ExplorationMapJackpotPointsTotal);
            response.Write(data.LastReason ?? "");
            response.Write(data.LastUpdateUtc ?? "");
            response.Write(data.RankingEnabled);
            response.Write(data.EnableKillPoints);
            response.Write(data.EnableBossPoints);
            response.Write(data.EnableSkillPoints);
            response.Write(data.EnableMarketplaceQuestPoints);
            response.Write(data.TopCount);
            response.Write(data.RewardClaimsEnabled);
            response.Write(data.RewardCanClaim);
            response.Write(data.RewardAlreadyClaimed);
            response.Write(data.RewardRank);
            response.Write(data.RewardMinPoints);
            response.Write(data.RewardLabel ?? "");
            response.Write(data.RewardPrefabName ?? "");
            response.Write(data.RewardAmount);
            response.Write(data.RewardBlockReason ?? "");
            response.Write(data.RewardClaimCycleId ?? "");
            response.Write(data.HudKillRules ?? "");
            response.Write(data.HudBossRules ?? "");
            response.Write(data.HudSkillJackpotRules ?? "");
            response.Write(data.HudMarketplaceQuestRules ?? "");
            response.Write(data.HudFishingRules ?? "");
            response.Write(data.HudCraftRules ?? "");
            response.Write(data.HudFarmJackpotRules ?? "");
            response.Write(data.HudUniqueCraftJackpotRules ?? "");
            response.Write(data.HudDeathPenaltyRules ?? "");
            response.Write(data.HudExplorationMapJackpotRules ?? "");
            response.Write(data.EnableDeathPenalty);
            response.Write(data.DeathPenaltyUseMultiplier);
            response.Write(Mathf.Max(0, data.DeathPenaltyPerDeath));
            response.Write(SerializeStringIntDictionary(data.ProgressCounters));
            response.Write(SerializeStringFloatDictionary(data.FloatProgressCounters));
            response.Write(data.TotalMarketplaceQuestsPontuadas);
            response.Write(data.MarketplaceQuestPointsTotal);
        }

        private void ReadSnapshotPayload(ZPackage pkg)
        {
            _cachedTopEntries.Clear();
            _cachedPlayerData = new SnapshotPlayerData();

            if (pkg == null)
                return;

            int count = 0;
            try { count = Mathf.Clamp(pkg.ReadInt(), 0, 100); } catch { count = 0; }

            for (int i = 0; i < count; i++)
            {
                SnapshotTopEntryData entry = new SnapshotTopEntryData();
                try { entry.Position = pkg.ReadInt(); } catch { }
                try { entry.PlayerName = pkg.ReadString(); } catch { entry.PlayerName = ""; }
                try { entry.Points = pkg.ReadInt(); } catch { }
                try { entry.IsLocalPlayer = pkg.ReadBool(); } catch { }
                try { entry.TotalKillsPontuadas = pkg.ReadInt(); } catch { }
                try { entry.TotalBossesPontuadas = pkg.ReadInt(); } catch { }
                try { entry.TotalSkillLevelUpsPontuados = pkg.ReadInt(); } catch { }
                try { entry.TotalFishingPontuadas = pkg.ReadInt(); } catch { }
                try { entry.TotalCraftPontuadas = pkg.ReadInt(); } catch { }
                try { entry.TotalFarmJackpotsPontuados = pkg.ReadInt(); } catch { }
                try { entry.TotalUniqueCraftJackpotsPontuados = pkg.ReadInt(); } catch { }
                try { entry.TotalDeaths = pkg.ReadInt(); } catch { }
                try { entry.KillPointsTotal = pkg.ReadInt(); } catch { }
                try { entry.BossPointsTotal = pkg.ReadInt(); } catch { }
                try { entry.SkillPointsTotal = pkg.ReadInt(); } catch { }
                try { entry.FishingPointsTotal = pkg.ReadInt(); } catch { }
                try { entry.CraftPointsTotal = pkg.ReadInt(); } catch { }
                try { entry.FarmJackpotPointsTotal = pkg.ReadInt(); } catch { }
                try { entry.UniqueCraftJackpotPointsTotal = pkg.ReadInt(); } catch { }
                try { entry.DeathPenaltyPointsTotal = pkg.ReadInt(); } catch { }
                try { entry.TotalPointsExchanges = pkg.ReadInt(); } catch { }
                try { entry.PointsExchangePenaltyTotal = pkg.ReadInt(); } catch { }
                try { entry.PointsExchangeCoinsTotal = pkg.ReadInt(); } catch { }
                try { entry.ExplorationMapJackpotPointsTotal = pkg.ReadInt(); } catch { }
                try { entry.LastReason = pkg.ReadString(); } catch { }
                try { entry.LastUpdateUtc = pkg.ReadString(); } catch { }
                try { entry.TotalMarketplaceQuestsPontuadas = pkg.ReadInt(); } catch { }
                try { entry.MarketplaceQuestPointsTotal = pkg.ReadInt(); } catch { }
                _cachedTopEntries.Add(entry);
            }

            SnapshotPlayerData data = new SnapshotPlayerData();
            try { data.HasData = pkg.ReadBool(); } catch { }
            try { data.Position = pkg.ReadInt(); } catch { }
            try { data.PlayerName = pkg.ReadString(); } catch { }
            try { data.Points = pkg.ReadInt(); } catch { }
            try { data.TotalKillsPontuadas = pkg.ReadInt(); } catch { }
            try { data.TotalBossesPontuadas = pkg.ReadInt(); } catch { }
            try { data.TotalSkillLevelUpsPontuados = pkg.ReadInt(); } catch { }
            try { data.KillPointsTotal = pkg.ReadInt(); } catch { }
            try { data.BossPointsTotal = pkg.ReadInt(); } catch { }
            try { data.SkillPointsTotal = pkg.ReadInt(); } catch { }
            try { data.TotalFishingPontuadas = pkg.ReadInt(); } catch { }
            try { data.TotalCraftPontuadas = pkg.ReadInt(); } catch { }
            try { data.TotalFarmJackpotsPontuados = pkg.ReadInt(); } catch { }
            try { data.TotalUniqueCraftJackpotsPontuados = pkg.ReadInt(); } catch { }
            try { data.TotalDeaths = pkg.ReadInt(); } catch { }
            try { data.FishingPointsTotal = pkg.ReadInt(); } catch { }
            try { data.CraftPointsTotal = pkg.ReadInt(); } catch { }
            try { data.FarmJackpotPointsTotal = pkg.ReadInt(); } catch { }
            try { data.UniqueCraftJackpotPointsTotal = pkg.ReadInt(); } catch { }
            try { data.DeathPenaltyPointsTotal = pkg.ReadInt(); } catch { }
            try { data.TotalPointsExchanges = pkg.ReadInt(); } catch { }
            try { data.PointsExchangePenaltyTotal = pkg.ReadInt(); } catch { }
            try { data.PointsExchangeCoinsTotal = pkg.ReadInt(); } catch { }
            try { data.ExplorationMapJackpotPointsTotal = pkg.ReadInt(); } catch { }
            try { data.LastReason = pkg.ReadString(); } catch { }
            try { data.LastUpdateUtc = pkg.ReadString(); } catch { }
            try { data.RankingEnabled = pkg.ReadBool(); } catch { data.RankingEnabled = _rules.RankingEnabled; }
            try { data.EnableKillPoints = pkg.ReadBool(); } catch { data.EnableKillPoints = _rules.EnableKillPoints; }
            try { data.EnableBossPoints = pkg.ReadBool(); } catch { data.EnableBossPoints = _rules.EnableBossPoints; }
            try { data.EnableSkillPoints = pkg.ReadBool(); } catch { data.EnableSkillPoints = _rules.EnableSkillPoints; }
            try { data.EnableMarketplaceQuestPoints = pkg.ReadBool(); } catch { data.EnableMarketplaceQuestPoints = _rules.EnableMarketplaceQuestPoints; }
            try { data.TopCount = pkg.ReadInt(); } catch { data.TopCount = _rules.TopCount; }
            try { data.RewardClaimsEnabled = pkg.ReadBool(); } catch { data.RewardClaimsEnabled = _rules.RewardClaimsEnabled; }
            try { data.RewardCanClaim = pkg.ReadBool(); } catch { }
            try { data.RewardAlreadyClaimed = pkg.ReadBool(); } catch { }
            try { data.RewardRank = pkg.ReadInt(); } catch { }
            try { data.RewardMinPoints = pkg.ReadInt(); } catch { }
            try { data.RewardLabel = pkg.ReadString(); } catch { }
            try { data.RewardPrefabName = pkg.ReadString(); } catch { }
            try { data.RewardAmount = pkg.ReadInt(); } catch { }
            try { data.RewardBlockReason = pkg.ReadString(); } catch { }
            try { data.RewardClaimCycleId = pkg.ReadString(); } catch { }
            try { data.HudKillRules = pkg.ReadString(); } catch { data.HudKillRules = FormatCategorizedIntRulesForHud(_rules.KillPoints, _rules.CombatHudCategories, true); }
            try { data.HudBossRules = pkg.ReadString(); } catch { data.HudBossRules = FormatIntRulesForHud(_rules.BossPoints); }
            try { data.HudSkillJackpotRules = pkg.ReadString(); } catch { data.HudSkillJackpotRules = FormatSkillJackpotRulesForHud(_rules.SkillMilestoneJackpotPoints); }
            try { data.HudMarketplaceQuestRules = pkg.ReadString(); } catch { data.HudMarketplaceQuestRules = FormatIntRulesForHud(_rules.MarketplaceQuestPoints); }
            try { data.HudFishingRules = pkg.ReadString(); } catch { data.HudFishingRules = FormatIntRulesForHud(_rules.FishingPoints); }
            try { data.HudCraftRules = pkg.ReadString(); } catch { data.HudCraftRules = FormatCategorizedIntRulesForHud(_rules.CraftPoints, _rules.ProductionHudCategories, false); }
            try { data.HudFarmJackpotRules = pkg.ReadString(); } catch { data.HudFarmJackpotRules = FormatCategorizedJackpotRulesForHud(_rules.FarmJackpots, _rules.ProductionHudCategories, false); }
            try { data.HudUniqueCraftJackpotRules = pkg.ReadString(); } catch { data.HudUniqueCraftJackpotRules = FormatCategorizedJackpotRulesForHud(_rules.UniqueCraftJackpots, _rules.ProductionHudCategories, false); }
            try { data.HudDeathPenaltyRules = pkg.ReadString(); } catch { data.HudDeathPenaltyRules = FormatDeathPenaltyRulesForHud(_rules.DeathPenaltyRules); }
            try { data.HudExplorationMapJackpotRules = pkg.ReadString(); } catch { data.HudExplorationMapJackpotRules = FormatExplorationMapJackpotRulesForHud(_rules.ExplorationMapJackpots); }
            try { data.EnableDeathPenalty = pkg.ReadBool(); } catch { data.EnableDeathPenalty = _rules.EnableDeathPenalty; }
            try { data.DeathPenaltyUseMultiplier = pkg.ReadBool(); } catch { data.DeathPenaltyUseMultiplier = _rules.DeathPenaltyUseMultiplier; }
            try { data.DeathPenaltyPerDeath = Mathf.Max(0, pkg.ReadInt()); } catch { data.DeathPenaltyPerDeath = Mathf.Max(0, _rules.DeathPenaltyPerDeath); }
            try { data.ProgressCounters = DeserializeStringIntDictionary(pkg.ReadString()); } catch { data.ProgressCounters = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase); }
            try { data.FloatProgressCounters = DeserializeStringFloatDictionary(pkg.ReadString()); } catch { data.FloatProgressCounters = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase); }
            try { data.TotalMarketplaceQuestsPontuadas = pkg.ReadInt(); } catch { }
            try { data.MarketplaceQuestPointsTotal = pkg.ReadInt(); } catch { }

            _cachedPlayerData = data;
        }

        private void BuildSnapshotTexts(string playerName, out string topText, out string playerText)
        {
            if (!_rules.RankingEnabled)
            {
                topText = "<size=20><b>Ranking desativado.</b></size>";
                playerText = "<size=18>Status: desativado</size>";
                return;
            }

            IReadOnlyList<RankingEntry> ordered = GetOrderedRankingEntries();

            int topCount = Mathf.Clamp(_rules.TopCount, 1, 50);
            playerName = SanitizePlayerName(playerName);

            int myPosition = -1;
            RankingEntry myEntry = null;
            for (int i = 0; i < ordered.Count; i++)
            {
                if (string.Equals(ordered[i].PlayerName, playerName, StringComparison.OrdinalIgnoreCase))
                {
                    myPosition = i + 1;
                    myEntry = ordered[i];
                    break;
                }
            }

            StringBuilder topSb = new StringBuilder(1400);
            if (ordered.Count == 0)
            {
                topSb.Append("<size=20><b>Nenhum jogador pontuado ainda.</b></size>\n");
                topSb.Append("<size=15><color=" + HudColorMuted + ">Saia para caçar, evoluir skills e dominar Glitnir.</color></size>");
            }
            else
            {
                int limit = Mathf.Min(topCount, ordered.Count);
                for (int i = 0; i < limit; i++)
                {
                    RankingEntry entry = ordered[i];
                    string block = BuildHudTopLine(i + 1, entry, playerName);

                    if (topSb.Length + block.Length + 8 > MaxHudChars)
                        break;

                    if (i > 0)
                        topSb.Append("\n\n<color=" + HudColorSeparator + ">────────────────────────</color>\n\n");

                    topSb.Append(block);
                }
            }
            topText = topSb.ToString().TrimEnd();

            StringBuilder playerSb = new StringBuilder(1800);
            if (myEntry != null)
            {
                playerSb.Append("<size=24><color=" + HudColorHighlight + "><b>POSIÇÃO #" + myPosition + "</b></color></size>\n");
                playerSb.Append("<size=16><color=" + HudColorMuted + ">Pontuação total</color></size>\n");
                playerSb.Append("<size=28><b>" + myEntry.Points + " pts</b></size>\n\n");

                playerSb.Append("<size=18><color=" + HudColorHighlight + "><b>Resumo da jornada</b></color></size>\n");
                playerSb.Append("<size=15>• Kills pontuadas: <b>" + myEntry.TotalKillsPontuadas + "</b></size>\n");
                playerSb.Append("<size=15>• Bosses pontuados: <b>" + myEntry.TotalBossesPontuadas + "</b></size>\n");
                playerSb.Append("<size=15>• Níveis de skills pontuados: <b>" + myEntry.TotalSkillLevelUpsPontuados + "</b></size>\n\n");

                playerSb.Append("<size=18><color=" + HudColorHighlight + "><b>Origem dos pontos</b></color></size>\n");
                playerSb.Append("<size=15>• Kills: <b>" + myEntry.KillPointsTotal + "</b></size>\n");
                playerSb.Append("<size=15>• Bosses: <b>" + myEntry.BossPointsTotal + "</b></size>\n");
                playerSb.Append("<size=15>• Skills: <b>" + myEntry.SkillPointsTotal + "</b></size>\n\n");

                playerSb.Append("<size=18><color=" + HudColorHighlight + "><b>Último registro</b></color></size>\n");
                playerSb.Append("<size=15>• Origem: <b>" + (string.IsNullOrWhiteSpace(myEntry.LastReason) ? "sem registro" : myEntry.LastReason) + "</b></size>\n");
                playerSb.Append("<size=15>• Atualização: <b>" + FormatHudLastUpdate(myEntry.LastUpdateUtc) + "</b></size>");
            }
            else
            {
                playerSb.Append("<size=22><b>Você ainda não tem pontos.</b></size>\n");
                playerSb.Append("<size=15><color=" + HudColorMuted + ">Participe de combates, derrote bosses e evolua skills para subir no ranking.</color></size>");
            }

            playerSb.Append("\n\n<color=" + HudColorSeparator + ">────────────────────────</color>\n\n");
            playerSb.Append("<size=18><color=" + HudColorHighlight + "><b>Configuração atual</b></color></size>\n");
            playerSb.Append("<size=15>• Ranking: <b>" + (_rules.RankingEnabled ? "ON" : "OFF") + "</b></size>\n");
            playerSb.Append("<size=15>• Kills: <b>" + (_rules.EnableKillPoints ? "ON" : "OFF") + "</b></size>\n");
            playerSb.Append("<size=15>• Bosses: <b>" + (_rules.EnableBossPoints ? "ON" : "OFF") + "</b></size>\n");
            playerSb.Append("<size=15>• Skills: <b>" + (_rules.EnableSkillPoints ? "ON" : "OFF") + "</b></size>\n");
            playerSb.Append("<size=15>• Top exibido: <b>" + _rules.TopCount + "</b></size>");

            playerText = playerSb.ToString().TrimEnd();
        }
    }
}
