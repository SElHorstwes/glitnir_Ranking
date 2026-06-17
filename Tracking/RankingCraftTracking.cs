using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace Glitnir.Ranking
{
    public partial class GlitnirRankingPlugin
    {
        private const float CraftRankingVerifyIntervalSeconds = 0.25f;
        private const float CraftRankingVerifyTimeoutSeconds = 12f;









        public void NotifyLocalCraftingStarted(Recipe recipe)
        {
            try
            {
                if (Player.m_localPlayer == null || recipe == null || recipe.m_item == null)
                    return;

                string prefabName = GetPrefabName(recipe.m_item.gameObject);
                prefabName = SafeKey(prefabName);

                if (string.IsNullOrWhiteSpace(prefabName))
                    return;

                string sharedName = GetRecipeItemSharedName(recipe);
                int beforeCount = CountLocalInventoryItemsByPrefabOrSharedName(prefabName, sharedName);

                StartCoroutine(VerifyCraftCompletedAndReport(recipe, prefabName, sharedName, beforeCount));
            }
            catch (Exception ex)
            {
                Logger.LogError("Erro ao iniciar verificação de craft local: " + ex);
            }
        }





        public void NotifyLocalCraftedRecipe(Recipe recipe)
        {
            NotifyLocalCraftingStarted(recipe);
        }

        private IEnumerator VerifyCraftCompletedAndReport(Recipe recipe, string prefabName, string sharedName, int beforeCount)
        {
            float start = Time.time;
            int lastCount = beforeCount;

            while (Time.time - start <= CraftRankingVerifyTimeoutSeconds)
            {
                yield return new WaitForSeconds(CraftRankingVerifyIntervalSeconds);

                if (Player.m_localPlayer == null)
                    yield break;

                int currentCount = CountLocalInventoryItemsByPrefabOrSharedName(prefabName, sharedName);
                lastCount = currentCount;

                int gained = currentCount - beforeCount;
                if (gained <= 0)
                    continue;


                int amountToReport = Mathf.Clamp(gained, 1, 100);
                NotifyLocalCraftedItem(prefabName, amountToReport);

                DebugLog(DebugCategory.Points, "Craft confirmado por inventário: item=" + prefabName + " antes=" + beforeCount + " depois=" + currentCount + " ganho=" + amountToReport);
                yield break;
            }

            DebugLog(DebugCategory.Points, "Craft não pontuado: item não entrou no inventário após início do craft. item=" + prefabName + " antes=" + beforeCount + " ultimo=" + lastCount);
        }

        private string GetRecipeItemSharedName(Recipe recipe)
        {
            try
            {
                if (recipe != null && recipe.m_item != null && recipe.m_item.m_itemData != null && recipe.m_item.m_itemData.m_shared != null)
                    return SafeKey(recipe.m_item.m_itemData.m_shared.m_name);
            }
            catch
            {
            }

            return "";
        }

        private int CountLocalInventoryItemsByPrefabOrSharedName(string prefabName, string sharedName)
        {
            try
            {
                if (Player.m_localPlayer == null)
                    return 0;

                Inventory inventory = Player.m_localPlayer.GetInventory();
                if (inventory == null)
                    return 0;

                prefabName = SafeKey(prefabName);
                sharedName = SafeKey(sharedName);
                if (string.IsNullOrWhiteSpace(prefabName) && string.IsNullOrWhiteSpace(sharedName))
                    return 0;

                List<ItemDrop.ItemData> items = inventory.GetAllItems();
                if (items == null || items.Count == 0)
                    return 0;

                int total = 0;

                for (int i = 0; i < items.Count; i++)
                {
                    ItemDrop.ItemData item = items[i];
                    if (item == null)
                        continue;

                    string itemPrefab = SafeKey(GetPrefabNameFromItemData(item));
                    string itemSharedName = SafeKey(GetSharedNameFromItemData(item));

                    bool prefabMatches = !string.IsNullOrWhiteSpace(prefabName) && string.Equals(itemPrefab, prefabName, StringComparison.OrdinalIgnoreCase);
                    bool sharedNameMatches = !string.IsNullOrWhiteSpace(sharedName) && string.Equals(itemSharedName, sharedName, StringComparison.OrdinalIgnoreCase);

                    if (!prefabMatches && !sharedNameMatches)
                        continue;

                    total += Mathf.Max(1, item.m_stack);
                }

                return Mathf.Clamp(total, 0, int.MaxValue);
            }
            catch (Exception ex)
            {
                Logger.LogWarning("Falha ao contar item craftado no inventário: " + ex.Message);
                return 0;
            }
        }


        private string GetSharedNameFromItemData(ItemDrop.ItemData item)
        {
            try
            {
                if (item != null && item.m_shared != null && !string.IsNullOrWhiteSpace(item.m_shared.m_name))
                    return item.m_shared.m_name;
            }
            catch
            {
            }

            return "";
        }

        private string GetPrefabNameFromItemData(ItemDrop.ItemData item)
        {
            if (item == null)
                return "";

            try
            {
                FieldInfo dropPrefabField = item.GetType().GetField("m_dropPrefab", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (dropPrefabField != null)
                {
                    GameObject dropPrefab = dropPrefabField.GetValue(item) as GameObject;
                    if (dropPrefab != null)
                        return GetPrefabName(dropPrefab);
                }
            }
            catch
            {
            }

            try
            {
                if (item.m_shared != null && !string.IsNullOrWhiteSpace(item.m_shared.m_name))
                    return SafeKey(item.m_shared.m_name);
            }
            catch
            {
            }

            return "";
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

                DebugLog(DebugCategory.Points, "Cliente reportando craft confirmado: player=" + GetLocalPlayerName() + " item=" + prefabName + " amount=" + amount);
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

                ProcessCraftReport(playerName, prefabName, amount, "rpc-craft-confirmed");
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
                    SaveRankingEntry(entry);
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
