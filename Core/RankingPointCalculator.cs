using BepInEx;
using BepInEx.Logging;
using BepInEx.Configuration;
using BepPaths = BepInEx.Paths;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Text;
using System.Reflection;
using LiteDB;
using Application = UnityEngine.Application;

namespace Glitnir.Ranking
{
    public partial class GlitnirRankingPlugin
    {
        public void AddPoints(string playerName, int amount, string reason)
        {
            if (!IsServerInstance() || !_rules.RankingEnabled || string.IsNullOrWhiteSpace(playerName))
                return;

            playerName = SanitizePlayerName(playerName);
            if (ShouldIgnorePlayerForRanking(playerName))
            {
                DebugLog(DebugCategory.Points, "PontuaÃ§Ã£o ignorada para admin: " + playerName + " motivo=" + reason);
                return;
            }

            RankingEntry entry = GetOrCreateEntry(playerName);
            ApplyPointsToEntry(entry, amount, reason);
        }

        private void ApplyPointsToEntry(RankingEntry entry, int amount, string reason)
        {
            if (entry == null)
                return;

            if (ShouldIgnorePlayerForRanking(entry.PlayerName))
            {
                DebugLog(DebugCategory.Points, "PontuaÃ§Ã£o ignorada para admin: " + entry.PlayerName + " motivo=" + reason);
                return;
            }

            bool shouldCheckTop3Webhook = IsDiscordTop3WebhookEnabled();
            Dictionary<string, int> oldRanks = IsServerInstance() && shouldCheckTop3Webhook
                ? GetRankingSnapshot()
                : new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            entry.Points = Mathf.Clamp(entry.Points + amount, -int.MaxValue, int.MaxValue);
            entry.LastReason = SafeReason(reason);
            entry.LastUpdateUtc = DateTime.UtcNow.ToString("O");
            RegisterRankingEntry(entry);

            Dictionary<string, int> newRanks = IsServerInstance() && shouldCheckTop3Webhook
                ? GetRankingSnapshot()
                : new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            DebugLog(DebugCategory.Points, "PontuaÃ§Ã£o alterada: player=" + entry.PlayerName + " amount=" + amount + " total=" + entry.Points + " motivo=" + entry.LastReason);


            if (IsServerInstance())
            {
                if (shouldCheckTop3Webhook)
                    TrySendTop3DiscordWebhooks(oldRanks, newRanks, entry.LastReason);

                SaveRankingEntry(entry);
            }
        }

        private bool IsDiscordTop3WebhookEnabled()
        {
            return IsServerInstance()
                && _cfgDiscordTop3WebhookEnabled != null
                && _cfgDiscordTop3WebhookEnabled.Value
                && _cfgDiscordTop3WebhookUrl != null
                && !string.IsNullOrWhiteSpace(_cfgDiscordTop3WebhookUrl.Value);
        }

        private void TrySendTop3DiscordWebhooks(Dictionary<string, int> oldRanks, Dictionary<string, int> newRanks, string reason)
        {
            try
            {
                if (!IsServerInstance())
                    return;

                if (_cfgDiscordTop3WebhookEnabled == null || !_cfgDiscordTop3WebhookEnabled.Value)
                    return;

                if (_cfgDiscordTop3WebhookUrl == null || string.IsNullOrWhiteSpace(_cfgDiscordTop3WebhookUrl.Value))
                    return;

                if (oldRanks == null)
                    oldRanks = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

                if (newRanks == null)
                    newRanks = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

                HashSet<string> players = new HashSet<string>(oldRanks.Keys, StringComparer.OrdinalIgnoreCase);
                foreach (string name in newRanks.Keys)
                    players.Add(name);

                foreach (string playerName in players)
                {
                    if (string.IsNullOrWhiteSpace(playerName))
                        continue;

                    int oldRank = oldRanks.TryGetValue(playerName, out int foundOldRank) ? foundOldRank : int.MaxValue;
                    int newRank = newRanks.TryGetValue(playerName, out int foundNewRank) ? foundNewRank : int.MaxValue;

                    bool enteredTop3 = oldRank > 3 && newRank >= 1 && newRank <= 3;
                    bool leftTop3 = oldRank >= 1 && oldRank <= 3 && newRank > 3;
                    bool droppedInsideTop3 = oldRank >= 1 && oldRank <= 3 && newRank >= 1 && newRank <= 3 && newRank > oldRank;

                    if (!enteredTop3 && !leftTop3 && !droppedInsideTop3)
                        continue;

                    RankingEntry rankedEntry = FindRankingEntryByName(playerName);
                    int points = rankedEntry != null ? rankedEntry.Points : 0;

                    if (enteredTop3)
                        SendDiscordTop3EnteredWebhook(playerName, newRank, points, reason);
                    else if (leftTop3)
                        SendDiscordTop3LostWebhook(playerName, oldRank, newRank, points, reason);
                    else if (droppedInsideTop3)
                        SendDiscordTop3DroppedWebhook(playerName, oldRank, newRank, points, reason);
                }
            }
            catch (Exception ex)
            {
                Logger.LogWarning("Erro ao preparar webhook Top 3 do Discord: " + ex.Message);
            }
        }
    }
}
