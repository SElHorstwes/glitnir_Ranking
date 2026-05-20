using System;
using UnityEngine;

namespace Glitnir.Ranking
{
    public partial class GlitnirRankingPlugin
    {


        public void NotifyLocalCraftedRecipe(Recipe recipe)
        {
            try
            {
                if (Player.m_localPlayer == null || recipe == null || recipe.m_item == null)
                    return;

                string prefabName = GetPrefabName(recipe.m_item.gameObject);
                int amount = Mathf.Max(1, recipe.m_amount);
                NotifyLocalCraftedItem(prefabName, amount);
            }
            catch (Exception ex)
            {
                Logger.LogError("Erro ao reportar craft local: " + ex);
            }
        }


        public void NotifyLocalCraftedItem(string prefabName, int amount)
        {
            try
            {
                if (Player.m_localPlayer == null)
                    return;

                prefabName = SafeKey(prefabName);
                amount = Mathf.Clamp(amount, 1, 100);

                if (string.IsNullOrWhiteSpace(prefabName))
                    return;


                long serverPeerUid = GetServerPeerUid();
                if (serverPeerUid == 0L || !_rpcsRegistered || ZRoutedRpc.instance == null)
                    return;

                ZPackage pkg = new ZPackage();
                pkg.Write(prefabName);
                pkg.Write(amount);
                pkg.Write(SafeLimit(Player.m_localPlayer.GetPlayerName(), MaxPlayerNameLength));

                ZRoutedRpc.instance.InvokeRoutedRPC(serverPeerUid, RpcReportCraftedItem, pkg);

                DebugLog(DebugCategory.Points, "Cliente reportando craft: player=" + GetLocalPlayerName() + " item=" + prefabName + " amount=" + amount);
            }
            catch (Exception ex)
            {
                Logger.LogError("Erro ao enviar craft local: " + ex);
            }
        }


        private void RPC_ReportCraftedItem(long sender, ZPackage pkg)
        {
            try
            {
                if (!IsServerInstance() || pkg == null)
                    return;

                string prefabName = SafeKey(pkg.ReadString());
                int amount = Mathf.Clamp(pkg.ReadInt(), 1, 100);
                string reporterName = SanitizePlayerName(pkg.ReadString());
                string playerName = ResolvePlayerNameFromSender(sender);

                if (string.IsNullOrWhiteSpace(playerName))
                    playerName = reporterName;

                if (ShouldIgnoreSenderForRanking(sender, playerName))
                {
                    DebugLog(DebugCategory.Points, "Craft ignorado para admin: " + playerName);
                    return;
                }

                ProcessCraftReport(playerName, prefabName, amount, "rpc-craft");
            }
            catch (Exception ex)
            {
                Logger.LogError("Erro no RPC_ReportCraftedItem: " + ex);
            }
        }


        private void ProcessCraftReport(string playerName, string prefabName, int amount, string source)
        {
            try
            {
                if (!IsServerInstance() || _rules == null || !_rules.RankingEnabled)
                    return;

                playerName = SanitizePlayerName(playerName);
                prefabName = SafeKey(prefabName);
                amount = Mathf.Clamp(amount, 1, 100);

                if (string.IsNullOrWhiteSpace(playerName) || string.IsNullOrWhiteSpace(prefabName))
                    return;

                if (ShouldIgnorePlayerForRanking(playerName))
                {
                    DebugLog(DebugCategory.Points, "Ação de ranking ignorada para admin: " + playerName);
                    return;
                }

                RankingEntry entry = GetOrCreateEntry(playerName);
                if (entry == null)
                    return;

                int craftPoints = 0;
                if (_rules.CraftPoints != null && _rules.CraftPoints.TryGetValue(prefabName, out craftPoints) && craftPoints > 0)
                {
                    int pointsToGrant = Mathf.Clamp(craftPoints * amount, 0, int.MaxValue);
                    ApplyPointsToEntry(entry, pointsToGrant, "Craft: " + prefabName + " x" + amount);
                    entry.TotalCraftPontuadas = Mathf.Clamp(entry.TotalCraftPontuadas + amount, 0, int.MaxValue);
                    entry.CraftPointsTotal = Mathf.Clamp(entry.CraftPointsTotal + pointsToGrant, -int.MaxValue, int.MaxValue);
                    IncrementProgressCounter(entry, "Craft", prefabName, amount);
                    SaveDatabase();
                }

                JackpotRule uniqueRule = null;
                bool matchedUnique = _rules.UniqueCraftJackpots != null && _rules.UniqueCraftJackpots.TryGetValue(prefabName, out uniqueRule) && uniqueRule != null && uniqueRule.Points > 0;
                if (matchedUnique)
                    AddProgressAndCheckJackpot(entry, "UniqueCraft", prefabName, amount, uniqueRule, "Jackpot craft único: " + prefabName, true);

                if ((craftPoints <= 0) && !matchedUnique)
                    DebugLog(DebugCategory.Points, "Craft sem regra configurada: prefab=" + prefabName + " amount=" + amount + " player=" + playerName);
            }
            catch (Exception ex)
            {
                Logger.LogError("Erro ao processar craft: " + ex);
            }
        }
    }
}
