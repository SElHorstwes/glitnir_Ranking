using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using LiteDB;
using UnityEngine;

namespace Glitnir.Ranking
{
    public partial class GlitnirRankingPlugin
    {
        private void LoadDatabase()
        {
            _database = new RankingDatabase();

            if (!IsDedicatedServerInstance())
            {
                DebugLog(DebugCategory.General, "LoadDatabase ignorado fora do servidor dedicado. Banco LiteDB fica somente no servidor.");
                return;
            }

            try
            {
                if (string.IsNullOrWhiteSpace(_databaseFilePath))
                {
                    Logger.LogWarning("[Ranking] Caminho do banco LiteDB nao definido.");
                    return;
                }

                EnsureDatabaseDirectoryExists();

                using (LiteDatabase db = new LiteDatabase(_databaseFilePath))
                {
                    ILiteCollection<RankingEntryDocument> entries = db.GetCollection<RankingEntryDocument>("ranking_entries");
                    ILiteCollection<RewardClaimDocument> claims = db.GetCollection<RewardClaimDocument>("reward_claims");
                    ILiteCollection<MarketplaceQuestCreditDocument> marketplaceCredits = db.GetCollection<MarketplaceQuestCreditDocument>("marketplace_quest_credits");
                    ILiteCollection<FishingCatchCreditDocument> fishingCredits = db.GetCollection<FishingCatchCreditDocument>("fishing_catch_credits");

                    entries.EnsureIndex(x => x.PlayerName, true);
                    claims.EnsureIndex(x => x.Key, true);
                    marketplaceCredits.EnsureIndex(x => x.Key, true);
                    fishingCredits.EnsureIndex(x => x.ZdoKey, true);

                    RankingDatabase loaded = new RankingDatabase();

                    foreach (RankingEntryDocument doc in entries.FindAll())
                    {
                        RankingEntry entry = ConvertFromDocument(doc);
                        if (entry != null && !string.IsNullOrWhiteSpace(entry.PlayerName))
                            loaded.Entries.Add(entry);
                    }

                    foreach (RewardClaimDocument doc in claims.FindAll())
                    {
                        RewardClaimRecord claim = ConvertFromDocument(doc);
                        if (claim != null && !string.IsNullOrWhiteSpace(claim.PlayerName))
                            loaded.Claims.Add(claim);
                    }

                    foreach (MarketplaceQuestCreditDocument doc in marketplaceCredits.FindAll())
                    {
                        MarketplaceQuestCreditRecord credit = ConvertFromDocument(doc);
                        if (credit != null && !string.IsNullOrWhiteSpace(credit.PlayerName) && !string.IsNullOrWhiteSpace(credit.QuestKey))
                            loaded.MarketplaceQuestCredits.Add(credit);
                    }

                    foreach (FishingCatchCreditDocument doc in fishingCredits.FindAll())
                    {
                        FishingCatchCreditRecord credit = ConvertFromDocument(doc);
                        if (credit != null && !string.IsNullOrWhiteSpace(credit.ZdoKey))
                            loaded.FishingCatchCredits.Add(credit);
                    }

                    _database = NormalizeDatabase(loaded);

                }
            }
            catch (Exception ex)
            {
                _database = new RankingDatabase();
                Logger.LogError("Erro ao carregar banco LiteDB do ranking: " + ex);
            }
        }

        private void EnsureDatabaseDirectoryExists()
        {
            if (!IsDedicatedServerInstance())
                return;

            try
            {
                if (string.IsNullOrWhiteSpace(_databaseFilePath))
                    return;

                string directory = Path.GetDirectoryName(_databaseFilePath);
                if (!string.IsNullOrWhiteSpace(directory) && !Directory.Exists(directory))
                    Directory.CreateDirectory(directory);
            }
            catch (Exception ex)
            {
                Logger.LogWarning("[Ranking] Nao foi possivel preparar pasta do banco LiteDB: " + ex.Message);
            }
        }


        private RankingDatabase LoadLegacyDatabaseFile(string filePath)
        {
            RankingDatabase result = new RankingDatabase();

            try
            {
                if (string.IsNullOrWhiteSpace(filePath))
                    return result;

                if (!File.Exists(filePath))
                    return result;

                RankingDatabase loaded = new RankingDatabase();
                string[] lines = File.ReadAllLines(filePath, Encoding.UTF8);

                foreach (string rawLine in lines)
                {
                    string line = rawLine == null ? "" : rawLine.Trim();
                    if (string.IsNullOrWhiteSpace(line))
                        continue;
                    if (line.StartsWith("#"))
                        continue;
                    if (line.StartsWith("Claim|", StringComparison.Ordinal))
                    {
                        string[] claimParts = line.Split('|');
                        if (claimParts.Length >= 5)
                        {
                            RewardClaimRecord claim = new RewardClaimRecord();
                            claim.CycleId = SafeLimit(DecodeDatabaseField(claimParts[1]), 64);
                            claim.PlayerName = SanitizePlayerName(DecodeDatabaseField(claimParts[2]));
                            claim.Rank = Mathf.Clamp(ParseInt(claimParts[3], 0), 0, int.MaxValue);
                            claim.ClaimedAtUtc = SafeLimit(DecodeDatabaseField(claimParts[4]), 64);
                            claim.Status = claimParts.Length >= 6
                                ? SafeLimit(DecodeDatabaseField(claimParts[5]), 16)
                                : "claimed";

                            if (!string.IsNullOrWhiteSpace(claim.CycleId) &&
                                !string.IsNullOrWhiteSpace(claim.PlayerName) &&
                                claim.Rank > 0)
                            {
                                string claimKey = BuildRewardClaimKey(claim.CycleId, claim.PlayerName, claim.Rank);
                                if (!loaded.Claims.Any(x => string.Equals(BuildRewardClaimKey(x.CycleId, x.PlayerName, x.Rank), claimKey, StringComparison.OrdinalIgnoreCase)))
                                    loaded.Claims.Add(claim);
                            }
                        }

                        continue;
                    }

                    if (line.StartsWith("QuestCredit|", StringComparison.Ordinal))
                    {
                        string[] questCreditParts = line.Split('|');
                        if (questCreditParts.Length >= 4)
                        {
                            MarketplaceQuestCreditRecord questCredit = new MarketplaceQuestCreditRecord();
                            questCredit.PlayerName = SanitizePlayerName(DecodeDatabaseField(questCreditParts[1]));
                            questCredit.QuestKey = SafeKey(DecodeDatabaseField(questCreditParts[2]));
                            questCredit.GrantedAtUtc = questCreditParts.Length >= 4
                                ? SafeLimit(DecodeDatabaseField(questCreditParts[3]), 64)
                                : "";

                            if (!string.IsNullOrWhiteSpace(questCredit.PlayerName) &&
                                !string.IsNullOrWhiteSpace(questCredit.QuestKey))
                            {
                                string creditKey = BuildMarketplaceQuestCreditKey(questCredit.PlayerName, questCredit.QuestKey);
                                if (!loaded.MarketplaceQuestCredits.Any(x =>
                                    string.Equals(BuildMarketplaceQuestCreditKey(x.PlayerName, x.QuestKey), creditKey, StringComparison.OrdinalIgnoreCase)))
                                {
                                    loaded.MarketplaceQuestCredits.Add(questCredit);
                                }
                            }
                        }

                        continue;
                    }

                    if (!line.StartsWith("Entry|", StringComparison.Ordinal))
                        continue;

                    string[] parts = line.Split('|');
                    if (parts.Length < 10)
                        continue;

                    RankingEntry entry = new RankingEntry();
                    entry.PlayerName = SanitizePlayerName(DecodeDatabaseField(parts[1]));
                    entry.Points = Mathf.Clamp(ParseInt(parts[2], 0), -int.MaxValue, int.MaxValue);
                    entry.LastReason = SafeReason(DecodeDatabaseField(parts[3]));
                    entry.LastUpdateUtc = SafeLimit(DecodeDatabaseField(parts[4]), 64);
                    entry.TotalKillsPontuadas = Mathf.Clamp(ParseInt(parts[5], 0), 0, int.MaxValue);
                    entry.TotalBossesPontuadas = Mathf.Clamp(ParseInt(parts[6], 0), 0, int.MaxValue);
                    entry.KillPointsTotal = Mathf.Clamp(ParseInt(parts[7], 0), -int.MaxValue, int.MaxValue);
                    entry.BossPointsTotal = Mathf.Clamp(ParseInt(parts[8], 0), -int.MaxValue, int.MaxValue);
                    entry.BossCredits = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                    string bossCreditsRaw = DecodeDatabaseField(parts[9]);
                    if (!string.IsNullOrWhiteSpace(bossCreditsRaw))
                    {
                        string[] bossCredits = bossCreditsRaw.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
                        foreach (string bossCredit in bossCredits)
                        {
                            string safeBossCredit = SafeKey(bossCredit);
                            if (!string.IsNullOrWhiteSpace(safeBossCredit))
                                entry.BossCredits.Add(safeBossCredit);
                        }
                    }

                    if (parts.Length >= 11)
                        entry.TotalSkillLevelUpsPontuados = Mathf.Clamp(ParseInt(parts[10], 0), 0, int.MaxValue);

                    if (parts.Length >= 12)
                        entry.SkillPointsTotal = Mathf.Clamp(ParseInt(parts[11], 0), -int.MaxValue, int.MaxValue);

                    if (parts.Length >= 13)
                        entry.TotalCraftPontuadas = Mathf.Clamp(ParseInt(parts[12], 0), 0, int.MaxValue);
                    if (parts.Length >= 14)
                        entry.CraftPointsTotal = Mathf.Clamp(ParseInt(parts[13], 0), -int.MaxValue, int.MaxValue);
                    if (parts.Length >= 15)
                        entry.TotalFarmJackpotsPontuados = Mathf.Clamp(ParseInt(parts[14], 0), 0, int.MaxValue);
                    if (parts.Length >= 16)
                        entry.FarmJackpotPointsTotal = Mathf.Clamp(ParseInt(parts[15], 0), -int.MaxValue, int.MaxValue);
                    if (parts.Length >= 17)
                        entry.TotalUniqueCraftJackpotsPontuados = Mathf.Clamp(ParseInt(parts[16], 0), 0, int.MaxValue);
                    if (parts.Length >= 18)
                        entry.UniqueCraftJackpotPointsTotal = Mathf.Clamp(ParseInt(parts[17], 0), -int.MaxValue, int.MaxValue);
                    if (parts.Length >= 19)
                        entry.ProgressCounters = DeserializeStringIntDictionary(DecodeDatabaseField(parts[18]));
                    if (parts.Length >= 20)
                        entry.GenericJackpotCredits = DeserializeStringSet(DecodeDatabaseField(parts[19]));
                    if (parts.Length >= 21)
                        entry.TotalDeaths = Mathf.Clamp(ParseInt(parts[20], 0), 0, int.MaxValue);
                    if (parts.Length >= 22)
                        entry.DeathPenaltyPointsTotal = Mathf.Clamp(ParseInt(parts[21], 0), 0, int.MaxValue);
                    if (parts.Length >= 23)
                        entry.TotalPointsExchanges = Mathf.Clamp(ParseInt(parts[22], 0), 0, int.MaxValue);
                    if (parts.Length >= 24)
                        entry.PointsExchangePenaltyTotal = Mathf.Clamp(ParseInt(parts[23], 0), 0, int.MaxValue);
                    if (parts.Length >= 25)
                        entry.PointsExchangeCoinsTotal = Mathf.Clamp(ParseInt(parts[24], 0), 0, int.MaxValue);
                    if (parts.Length >= 26)
                        entry.ExplorationMapJackpotPointsTotal = Mathf.Clamp(ParseInt(parts[25], 0), 0, int.MaxValue);

                    if (string.IsNullOrWhiteSpace(entry.PlayerName))
                        continue;

                    RankingEntry existing = loaded.Entries.FirstOrDefault(x =>
                        string.Equals(x.PlayerName, entry.PlayerName, StringComparison.OrdinalIgnoreCase));

                    if (existing != null)
                    {
                        existing.Points = entry.Points;
                        existing.LastReason = entry.LastReason;
                        existing.LastUpdateUtc = entry.LastUpdateUtc;
                        existing.TotalKillsPontuadas = entry.TotalKillsPontuadas;
                        existing.TotalBossesPontuadas = entry.TotalBossesPontuadas;
                        existing.KillPointsTotal = entry.KillPointsTotal;
                        existing.BossPointsTotal = entry.BossPointsTotal;
                        existing.BossCredits = entry.BossCredits;
                        existing.TotalSkillLevelUpsPontuados = entry.TotalSkillLevelUpsPontuados;
                        existing.SkillPointsTotal = entry.SkillPointsTotal;
                        existing.TotalCraftPontuadas = entry.TotalCraftPontuadas;
                        existing.CraftPointsTotal = entry.CraftPointsTotal;
                        existing.TotalFarmJackpotsPontuados = entry.TotalFarmJackpotsPontuados;
                        existing.FarmJackpotPointsTotal = entry.FarmJackpotPointsTotal;
                        existing.TotalUniqueCraftJackpotsPontuados = entry.TotalUniqueCraftJackpotsPontuados;
                        existing.UniqueCraftJackpotPointsTotal = entry.UniqueCraftJackpotPointsTotal;
                        existing.ProgressCounters = entry.ProgressCounters ?? new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                        existing.GenericJackpotCredits = entry.GenericJackpotCredits ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                        existing.TotalDeaths = entry.TotalDeaths;
                        existing.DeathPenaltyPointsTotal = entry.DeathPenaltyPointsTotal;
                        existing.TotalPointsExchanges = entry.TotalPointsExchanges;
                        existing.PointsExchangePenaltyTotal = entry.PointsExchangePenaltyTotal;
                        existing.PointsExchangeCoinsTotal = entry.PointsExchangeCoinsTotal;
                        existing.ExplorationMapJackpotPointsTotal = entry.ExplorationMapJackpotPointsTotal;
                    }
                    else
                    {
                        loaded.Entries.Add(entry);
                    }
                }

                return NormalizeDatabase(loaded);
            }
            catch (Exception ex)
            {
                Logger.LogError("Erro ao importar banco legado do ranking: " + ex);
                return result;
            }
        }


        private RankingDatabase NormalizeDatabase(RankingDatabase database)
        {
            if (database == null)
                database = new RankingDatabase();

            if (database.Entries == null)
                database.Entries = new List<RankingEntry>();

            if (database.Claims == null)
                database.Claims = new List<RewardClaimRecord>();

            if (database.MarketplaceQuestCredits == null)
                database.MarketplaceQuestCredits = new List<MarketplaceQuestCreditRecord>();

            if (database.FishingCatchCredits == null)
                database.FishingCatchCredits = new List<FishingCatchCreditRecord>();

            foreach (RankingEntry entry in database.Entries)
            {
                if (entry == null)
                    continue;

                entry.PlayerName = SanitizePlayerName(entry.PlayerName);
                entry.LastReason = SafeReason(entry.LastReason);
                entry.LastUpdateUtc = SafeLimit(entry.LastUpdateUtc, 64);

                entry.Points = Mathf.Clamp(entry.Points, -int.MaxValue, int.MaxValue);
                entry.TotalKillsPontuadas = Mathf.Clamp(entry.TotalKillsPontuadas, 0, int.MaxValue);
                entry.TotalBossesPontuadas = Mathf.Clamp(entry.TotalBossesPontuadas, 0, int.MaxValue);
                entry.KillPointsTotal = Mathf.Clamp(entry.KillPointsTotal, -int.MaxValue, int.MaxValue);
                entry.BossPointsTotal = Mathf.Clamp(entry.BossPointsTotal, -int.MaxValue, int.MaxValue);
                entry.TotalSkillLevelUpsPontuados = Mathf.Clamp(entry.TotalSkillLevelUpsPontuados, 0, int.MaxValue);
                entry.SkillPointsTotal = Mathf.Clamp(entry.SkillPointsTotal, -int.MaxValue, int.MaxValue);
                entry.TotalFishingPontuadas = Mathf.Clamp(entry.TotalFishingPontuadas, 0, int.MaxValue);
                entry.FishingPointsTotal = Mathf.Clamp(entry.FishingPointsTotal, -int.MaxValue, int.MaxValue);
                entry.TotalCraftPontuadas = Mathf.Clamp(entry.TotalCraftPontuadas, 0, int.MaxValue);
                entry.CraftPointsTotal = Mathf.Clamp(entry.CraftPointsTotal, -int.MaxValue, int.MaxValue);
                entry.TotalFarmJackpotsPontuados = Mathf.Clamp(entry.TotalFarmJackpotsPontuados, 0, int.MaxValue);
                entry.FarmJackpotPointsTotal = Mathf.Clamp(entry.FarmJackpotPointsTotal, -int.MaxValue, int.MaxValue);
                entry.TotalUniqueCraftJackpotsPontuados = Mathf.Clamp(entry.TotalUniqueCraftJackpotsPontuados, 0, int.MaxValue);
                entry.UniqueCraftJackpotPointsTotal = Mathf.Clamp(entry.UniqueCraftJackpotPointsTotal, -int.MaxValue, int.MaxValue);
                entry.TotalDeaths = Mathf.Clamp(entry.TotalDeaths, 0, int.MaxValue);
                entry.DeathPenaltyPointsTotal = Mathf.Clamp(entry.DeathPenaltyPointsTotal, 0, int.MaxValue);
                entry.TotalPointsExchanges = Mathf.Clamp(entry.TotalPointsExchanges, 0, int.MaxValue);
                entry.PointsExchangePenaltyTotal = Mathf.Clamp(entry.PointsExchangePenaltyTotal, 0, int.MaxValue);
                entry.PointsExchangeCoinsTotal = Mathf.Clamp(entry.PointsExchangeCoinsTotal, 0, int.MaxValue);
                entry.ExplorationMapJackpotPointsTotal = Mathf.Clamp(entry.ExplorationMapJackpotPointsTotal, 0, int.MaxValue);

                entry.BossCredits = entry.BossCredits == null
                    ? new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                    : new HashSet<string>(entry.BossCredits.Where(x => !string.IsNullOrWhiteSpace(x)).Select(SafeKey), StringComparer.OrdinalIgnoreCase);

                entry.GenericJackpotCredits = entry.GenericJackpotCredits == null
                    ? new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                    : new HashSet<string>(entry.GenericJackpotCredits.Where(x => !string.IsNullOrWhiteSpace(x)).Select(SafeKey), StringComparer.OrdinalIgnoreCase);

                entry.SkillLevel100JackpotCredits = entry.SkillLevel100JackpotCredits == null
                    ? new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                    : new HashSet<string>(entry.SkillLevel100JackpotCredits.Where(x => !string.IsNullOrWhiteSpace(x)).Select(SafeKey), StringComparer.OrdinalIgnoreCase);

                entry.ProgressCounters = entry.ProgressCounters == null
                    ? new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
                    : entry.ProgressCounters
                        .Where(x => !string.IsNullOrWhiteSpace(x.Key))
                        .GroupBy(x => SafeKey(x.Key), StringComparer.OrdinalIgnoreCase)
                        .ToDictionary(x => x.Key, x => Mathf.Clamp(x.Last().Value, 0, int.MaxValue), StringComparer.OrdinalIgnoreCase);
            }

            database.Entries = database.Entries
                .Where(x => x != null && !string.IsNullOrWhiteSpace(x.PlayerName))
                .GroupBy(x => x.PlayerName, StringComparer.OrdinalIgnoreCase)
                .Select(x => x.First())
                .OrderBy(x => x.PlayerName, StringComparer.OrdinalIgnoreCase)
                .ToList();

            database.Claims = database.Claims
                .Where(x => x != null && !string.IsNullOrWhiteSpace(x.CycleId) && !string.IsNullOrWhiteSpace(x.PlayerName) && x.Rank > 0)
                .Select(x =>
                {
                    x.CycleId = SafeLimit(x.CycleId, 64);
                    x.PlayerName = SanitizePlayerName(x.PlayerName);
                    x.Rank = Mathf.Clamp(x.Rank, 1, int.MaxValue);
                    x.ClaimedAtUtc = SafeLimit(x.ClaimedAtUtc, 64);
                    x.Status = SafeLimit(string.IsNullOrWhiteSpace(x.Status) ? "claimed" : x.Status, 16);
                    return x;
                })
                .GroupBy(x => BuildRewardClaimKey(x.CycleId, x.PlayerName, x.Rank), StringComparer.OrdinalIgnoreCase)
                .Select(x => x.First())
                .ToList();

            database.MarketplaceQuestCredits = database.MarketplaceQuestCredits
                .Where(x => x != null && !string.IsNullOrWhiteSpace(x.PlayerName) && !string.IsNullOrWhiteSpace(x.QuestKey))
                .Select(x =>
                {
                    x.PlayerName = SanitizePlayerName(x.PlayerName);
                    x.QuestKey = SafeKey(x.QuestKey);
                    x.GrantedAtUtc = SafeLimit(x.GrantedAtUtc, 64);
                    return x;
                })
                .GroupBy(x => BuildMarketplaceQuestCreditKey(x.PlayerName, x.QuestKey), StringComparer.OrdinalIgnoreCase)
                .Select(x => x.First())
                .ToList();

            database.FishingCatchCredits = database.FishingCatchCredits
                .Where(x => x != null && !string.IsNullOrWhiteSpace(x.ZdoKey))
                .Select(x =>
                {
                    x.PlayerName = SanitizePlayerName(x.PlayerName);
                    x.FishPrefab = SafeKey(x.FishPrefab);
                    x.ZdoKey = SafeKey(x.ZdoKey);
                    x.GrantedAtUtc = SafeLimit(x.GrantedAtUtc, 64);
                    return x;
                })
                .GroupBy(x => x.ZdoKey, StringComparer.OrdinalIgnoreCase)
                .Select(x => x.First())
                .ToList();

            return database;
        }

        private void SaveDatabase()
        {
            try
            {
                if (!IsDedicatedServerInstance())
                    return;

                if (string.IsNullOrWhiteSpace(_databaseFilePath) || _database == null)
                    return;

                EnsureDatabaseDirectoryExists();

                RankingDatabase normalized = NormalizeDatabase(_database);

                using (LiteDatabase db = new LiteDatabase(_databaseFilePath))
                {
                    ILiteCollection<RankingEntryDocument> entries = db.GetCollection<RankingEntryDocument>("ranking_entries");
                    ILiteCollection<RewardClaimDocument> claims = db.GetCollection<RewardClaimDocument>("reward_claims");
                    ILiteCollection<MarketplaceQuestCreditDocument> marketplaceCredits = db.GetCollection<MarketplaceQuestCreditDocument>("marketplace_quest_credits");
                    ILiteCollection<FishingCatchCreditDocument> fishingCredits = db.GetCollection<FishingCatchCreditDocument>("fishing_catch_credits");

                    entries.EnsureIndex(x => x.PlayerName, true);
                    claims.EnsureIndex(x => x.Key, true);
                    marketplaceCredits.EnsureIndex(x => x.Key, true);
                    fishingCredits.EnsureIndex(x => x.ZdoKey, true);

                    entries.DeleteAll();
                    claims.DeleteAll();
                    marketplaceCredits.DeleteAll();
                    fishingCredits.DeleteAll();

                    if (normalized.Entries != null)
                    {
                        foreach (RankingEntry entry in normalized.Entries)
                        {
                            RankingEntryDocument doc = ConvertToDocument(entry);
                            if (doc != null && !string.IsNullOrWhiteSpace(doc.PlayerName))
                                entries.Upsert(doc);
                        }
                    }

                    if (normalized.Claims != null)
                    {
                        foreach (RewardClaimRecord claim in normalized.Claims)
                        {
                            RewardClaimDocument doc = ConvertToDocument(claim);
                            if (doc != null && !string.IsNullOrWhiteSpace(doc.Key))
                                claims.Upsert(doc);
                        }
                    }

                    if (normalized.MarketplaceQuestCredits != null)
                    {
                        foreach (MarketplaceQuestCreditRecord credit in normalized.MarketplaceQuestCredits)
                        {
                            MarketplaceQuestCreditDocument doc = ConvertToDocument(credit);
                            if (doc != null && !string.IsNullOrWhiteSpace(doc.Key))
                                marketplaceCredits.Upsert(doc);
                        }
                    }

                    if (normalized.FishingCatchCredits != null)
                    {
                        foreach (FishingCatchCreditRecord credit in normalized.FishingCatchCredits)
                        {
                            FishingCatchCreditDocument doc = ConvertToDocument(credit);
                            if (doc != null && !string.IsNullOrWhiteSpace(doc.ZdoKey))
                                fishingCredits.Upsert(doc);
                        }
                    }
                }

                _database = normalized;
            }
            catch (Exception ex)
            {
                Logger.LogError("Erro ao salvar banco LiteDB do ranking: " + ex);
            }
        }

        private RankingEntryDocument ConvertToDocument(RankingEntry entry)
        {
            if (entry == null)
                return null;

            RankingEntryDocument doc = new RankingEntryDocument();
            doc.PlayerName = SanitizePlayerName(entry.PlayerName);
            doc.Points = entry.Points;
            doc.LastReason = SafeReason(entry.LastReason);
            doc.LastUpdateUtc = SafeLimit(entry.LastUpdateUtc, 64);
            doc.TotalKillsPontuadas = entry.TotalKillsPontuadas;
            doc.TotalBossesPontuadas = entry.TotalBossesPontuadas;
            doc.KillPointsTotal = entry.KillPointsTotal;
            doc.BossPointsTotal = entry.BossPointsTotal;
            doc.TotalSkillLevelUpsPontuados = entry.TotalSkillLevelUpsPontuados;
            doc.SkillPointsTotal = entry.SkillPointsTotal;
            doc.TotalFishingPontuadas = entry.TotalFishingPontuadas;
            doc.FishingPointsTotal = entry.FishingPointsTotal;
            doc.TotalCraftPontuadas = entry.TotalCraftPontuadas;
            doc.CraftPointsTotal = entry.CraftPointsTotal;
            doc.TotalFarmJackpotsPontuados = entry.TotalFarmJackpotsPontuados;
            doc.FarmJackpotPointsTotal = entry.FarmJackpotPointsTotal;
            doc.TotalUniqueCraftJackpotsPontuados = entry.TotalUniqueCraftJackpotsPontuados;
            doc.UniqueCraftJackpotPointsTotal = entry.UniqueCraftJackpotPointsTotal;
            doc.TotalDeaths = entry.TotalDeaths;
            doc.DeathPenaltyPointsTotal = entry.DeathPenaltyPointsTotal;
            doc.TotalPointsExchanges = entry.TotalPointsExchanges;
            doc.PointsExchangePenaltyTotal = entry.PointsExchangePenaltyTotal;
            doc.PointsExchangeCoinsTotal = entry.PointsExchangeCoinsTotal;
            doc.ExplorationMapJackpotPointsTotal = entry.ExplorationMapJackpotPointsTotal;
            doc.ProgressCounters = SerializeStringIntDictionary(entry.ProgressCounters);
            doc.FloatProgressCounters = SerializeStringFloatDictionary(entry.FloatProgressCounters);
            doc.GenericJackpotCredits = SerializeStringSet(entry.GenericJackpotCredits);
            doc.BossCredits = SerializeStringSet(entry.BossCredits);
            doc.SkillLevel100JackpotCredits = SerializeStringSet(entry.SkillLevel100JackpotCredits);
            return doc;
        }

        private RankingEntry ConvertFromDocument(RankingEntryDocument doc)
        {
            if (doc == null)
                return null;

            RankingEntry entry = new RankingEntry();
            entry.PlayerName = SanitizePlayerName(doc.PlayerName);
            entry.Points = doc.Points;
            entry.LastReason = SafeReason(doc.LastReason);
            entry.LastUpdateUtc = SafeLimit(doc.LastUpdateUtc, 64);
            entry.TotalKillsPontuadas = doc.TotalKillsPontuadas;
            entry.TotalBossesPontuadas = doc.TotalBossesPontuadas;
            entry.KillPointsTotal = doc.KillPointsTotal;
            entry.BossPointsTotal = doc.BossPointsTotal;
            entry.TotalSkillLevelUpsPontuados = doc.TotalSkillLevelUpsPontuados;
            entry.SkillPointsTotal = doc.SkillPointsTotal;
            entry.TotalFishingPontuadas = doc.TotalFishingPontuadas;
            entry.FishingPointsTotal = doc.FishingPointsTotal;
            entry.TotalCraftPontuadas = doc.TotalCraftPontuadas;
            entry.CraftPointsTotal = doc.CraftPointsTotal;
            entry.TotalFarmJackpotsPontuados = doc.TotalFarmJackpotsPontuados;
            entry.FarmJackpotPointsTotal = doc.FarmJackpotPointsTotal;
            entry.TotalUniqueCraftJackpotsPontuados = doc.TotalUniqueCraftJackpotsPontuados;
            entry.UniqueCraftJackpotPointsTotal = doc.UniqueCraftJackpotPointsTotal;
            entry.TotalDeaths = doc.TotalDeaths;
            entry.DeathPenaltyPointsTotal = doc.DeathPenaltyPointsTotal;
            entry.TotalPointsExchanges = doc.TotalPointsExchanges;
            entry.PointsExchangePenaltyTotal = doc.PointsExchangePenaltyTotal;
            entry.PointsExchangeCoinsTotal = doc.PointsExchangeCoinsTotal;
            entry.ExplorationMapJackpotPointsTotal = doc.ExplorationMapJackpotPointsTotal;
            entry.ProgressCounters = DeserializeStringIntDictionary(doc.ProgressCounters);
            entry.FloatProgressCounters = DeserializeStringFloatDictionary(doc.FloatProgressCounters);
            entry.GenericJackpotCredits = DeserializeStringSet(doc.GenericJackpotCredits);
            entry.BossCredits = DeserializeStringSet(doc.BossCredits);
            entry.SkillLevel100JackpotCredits = DeserializeStringSet(doc.SkillLevel100JackpotCredits);
            return entry;
        }

        private RewardClaimDocument ConvertToDocument(RewardClaimRecord claim)
        {
            if (claim == null)
                return null;

            RewardClaimDocument doc = new RewardClaimDocument();
            doc.CycleId = SafeKey(claim.CycleId);
            doc.PlayerName = SanitizePlayerName(claim.PlayerName);
            doc.Rank = claim.Rank;
            doc.ClaimedAtUtc = SafeLimit(claim.ClaimedAtUtc, 64);
            doc.Status = SafeLimit(claim.Status, 32);
            doc.Key = BuildRewardClaimKey(doc.CycleId, doc.PlayerName, doc.Rank);
            return doc;
        }

        private RewardClaimRecord ConvertFromDocument(RewardClaimDocument doc)
        {
            if (doc == null)
                return null;

            RewardClaimRecord claim = new RewardClaimRecord();
            claim.CycleId = SafeKey(doc.CycleId);
            claim.PlayerName = SanitizePlayerName(doc.PlayerName);
            claim.Rank = doc.Rank;
            claim.ClaimedAtUtc = SafeLimit(doc.ClaimedAtUtc, 64);
            claim.Status = SafeLimit(doc.Status, 32);
            return claim;
        }

        private MarketplaceQuestCreditDocument ConvertToDocument(MarketplaceQuestCreditRecord credit)
        {
            if (credit == null)
                return null;

            MarketplaceQuestCreditDocument doc = new MarketplaceQuestCreditDocument();
            doc.PlayerName = SanitizePlayerName(credit.PlayerName);
            doc.QuestKey = SafeKey(credit.QuestKey);
            doc.GrantedAtUtc = SafeLimit(credit.GrantedAtUtc, 64);
            doc.Key = BuildMarketplaceQuestCreditKey(doc.PlayerName, doc.QuestKey);
            return doc;
        }

        private MarketplaceQuestCreditRecord ConvertFromDocument(MarketplaceQuestCreditDocument doc)
        {
            if (doc == null)
                return null;

            MarketplaceQuestCreditRecord credit = new MarketplaceQuestCreditRecord();
            credit.PlayerName = SanitizePlayerName(doc.PlayerName);
            credit.QuestKey = SafeKey(doc.QuestKey);
            credit.GrantedAtUtc = SafeLimit(doc.GrantedAtUtc, 64);
            return credit;
        }

        private FishingCatchCreditDocument ConvertToDocument(FishingCatchCreditRecord credit)
        {
            if (credit == null)
                return null;

            FishingCatchCreditDocument doc = new FishingCatchCreditDocument();
            doc.PlayerName = SanitizePlayerName(credit.PlayerName);
            doc.FishPrefab = SafeKey(credit.FishPrefab);
            doc.ZdoKey = SafeKey(credit.ZdoKey);
            doc.GrantedAtUtc = SafeLimit(credit.GrantedAtUtc, 64);
            return doc;
        }

        private FishingCatchCreditRecord ConvertFromDocument(FishingCatchCreditDocument doc)
        {
            if (doc == null)
                return null;

            FishingCatchCreditRecord credit = new FishingCatchCreditRecord();
            credit.PlayerName = SanitizePlayerName(doc.PlayerName);
            credit.FishPrefab = SafeKey(doc.FishPrefab);
            credit.ZdoKey = SafeKey(doc.ZdoKey);
            credit.GrantedAtUtc = SafeLimit(doc.GrantedAtUtc, 64);
            return credit;
        }


        private string SerializeStringSet(HashSet<string> values)
        {
            if (values == null || values.Count == 0)
                return "";

            return string.Join("\n", values
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(SafeKey)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                .ToArray());
        }

        private HashSet<string> DeserializeStringSet(string raw)
        {
            HashSet<string> result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (string.IsNullOrWhiteSpace(raw))
                return result;

            foreach (string part in raw.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string key = SafeKey(part);
                if (!string.IsNullOrWhiteSpace(key))
                    result.Add(key);
            }

            return result;
        }

        private string SerializeStringIntDictionary(Dictionary<string, int> values)
        {
            if (values == null || values.Count == 0)
                return "";


            return string.Join("\n", values
                .Where(p => !string.IsNullOrWhiteSpace(p.Key))
                .Select(p => EncodeDatabaseField(SafeKey(p.Key)) + ":" + Mathf.Clamp(p.Value, 0, int.MaxValue))
                .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                .ToArray());
        }

        private Dictionary<string, int> DeserializeStringIntDictionary(string raw)
        {
            Dictionary<string, int> result = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            if (string.IsNullOrWhiteSpace(raw))
                return result;

            foreach (string part in raw.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string line = part == null ? "" : part.Trim();
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                int splitIndex = line.LastIndexOf(':');
                if (splitIndex <= 0 || splitIndex >= line.Length - 1)
                    continue;

                string rawKey = line.Substring(0, splitIndex);
                string rawValue = line.Substring(splitIndex + 1);

                int value;
                if (!int.TryParse(rawValue, out value))
                    continue;

                string key = DecodeDatabaseField(rawKey);


                if (string.IsNullOrWhiteSpace(key))
                    key = rawKey;

                key = SafeKey(key);
                if (string.IsNullOrWhiteSpace(key))
                    continue;

                result[key] = Mathf.Clamp(value, 0, int.MaxValue);
            }

            return result;
        }


        private string SerializeStringFloatDictionary(Dictionary<string, float> values)
        {
            if (values == null || values.Count == 0)
                return "";

            return string.Join("\n", values
                .Where(p => !string.IsNullOrWhiteSpace(p.Key))
                .Select(p => EncodeDatabaseField(SafeKey(p.Key)) + ":" + Mathf.Clamp(p.Value, 0f, 1000000f).ToString("R", System.Globalization.CultureInfo.InvariantCulture))
                .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                .ToArray());
        }

        private Dictionary<string, float> DeserializeStringFloatDictionary(string raw)
        {
            Dictionary<string, float> result = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);

            if (string.IsNullOrWhiteSpace(raw))
                return result;

            foreach (string part in raw.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string line = part == null ? "" : part.Trim();
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                int splitIndex = line.LastIndexOf(':');
                if (splitIndex <= 0 || splitIndex >= line.Length - 1)
                    continue;

                string rawKey = line.Substring(0, splitIndex);
                string rawValue = line.Substring(splitIndex + 1);

                float value;
                if (!float.TryParse(rawValue, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out value))
                    continue;

                string key = DecodeDatabaseField(rawKey);
                if (string.IsNullOrWhiteSpace(key))
                    key = rawKey;

                key = SafeKey(key);
                if (string.IsNullOrWhiteSpace(key))
                    continue;

                result[key] = Mathf.Clamp(value, 0f, 1000000f);
            }

            return result;
        }

        private string EncodeDatabaseField(string value)
        {
            try
            {
                byte[] bytes = Encoding.UTF8.GetBytes(value ?? "");
                return Convert.ToBase64String(bytes);
            }
            catch
            {
                return "";
            }
        }

        private string DecodeDatabaseField(string value)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(value))
                    return "";

                byte[] bytes = Convert.FromBase64String(value);
                return Encoding.UTF8.GetString(bytes);
            }
            catch
            {
                return "";
            }
        }
    }
}
