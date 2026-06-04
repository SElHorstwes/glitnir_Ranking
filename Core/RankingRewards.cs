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
        private RankRewardInfo GetRankRewardInfo(int rank)
        {
            switch (rank)
            {
                case 1:
                    return new RankRewardInfo
                    {
                        Rank = 1,
                        MinPoints = Mathf.Max(0, _rules.RewardTop1MinPoints),
                        Label = SafeLimit(_rules.RewardTop1Label, 96),
                        PrefabName = SafeLimit(_rules.RewardTop1Prefab, 96),
                        Amount = Mathf.Max(0, _rules.RewardTop1Amount)
                    };

                case 2:
                    return new RankRewardInfo
                    {
                        Rank = 2,
                        MinPoints = Mathf.Max(0, _rules.RewardTop2MinPoints),
                        Label = SafeLimit(_rules.RewardTop2Label, 96),
                        PrefabName = SafeLimit(_rules.RewardTop2Prefab, 96),
                        Amount = Mathf.Max(0, _rules.RewardTop2Amount)
                    };

                case 3:
                    return new RankRewardInfo
                    {
                        Rank = 3,
                        MinPoints = Mathf.Max(0, _rules.RewardTop3MinPoints),
                        Label = SafeLimit(_rules.RewardTop3Label, 96),
                        PrefabName = SafeLimit(_rules.RewardTop3Prefab, 96),
                        Amount = Mathf.Max(0, _rules.RewardTop3Amount)
                    };

                default:
                    return null;
            }
        }

        private string BuildRewardClaimKey(string cycleId, string playerName, int rank)
        {
            return SafeLimit(cycleId, 64) + "|" + SanitizePlayerName(playerName) + "|" + Mathf.Clamp(rank, 0, int.MaxValue);
        }

        private string BuildPendingRewardKey(long sender, string cycleId, int rank)
        {
            return sender + "|" + SafeLimit(cycleId, 64) + "|" + Mathf.Clamp(rank, 0, int.MaxValue);
        }

        private bool HasClaimedReward(string cycleId, string playerName, int rank)
        {
            if (_database == null || _database.Claims == null)
                return false;

            string key = BuildRewardClaimKey(cycleId, playerName, rank);
            return _database.Claims.Any(x =>
                x != null &&
                string.Equals(BuildRewardClaimKey(x.CycleId, x.PlayerName, x.Rank), key, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(SafeLimit(x.Status, 16), "claimed", StringComparison.OrdinalIgnoreCase));
        }

        private bool HasPendingRewardClaim(string cycleId, string playerName, int rank)
        {
            if (_database == null || _database.Claims == null)
                return false;

            string key = BuildRewardClaimKey(cycleId, playerName, rank);
            return _database.Claims.Any(x =>
                x != null &&
                string.Equals(BuildRewardClaimKey(x.CycleId, x.PlayerName, x.Rank), key, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(SafeLimit(x.Status, 16), "pending", StringComparison.OrdinalIgnoreCase));
        }

        private void ReserveRewardClaim(string cycleId, string playerName, int rank)
        {
            if (_database == null)
                _database = new RankingDatabase();

            if (_database.Claims == null)
                _database.Claims = new List<RewardClaimRecord>();

            string safeCycleId = SafeLimit(cycleId, 64);
            string safePlayerName = SanitizePlayerName(playerName);
            int safeRank = Mathf.Clamp(rank, 1, int.MaxValue);
            string key = BuildRewardClaimKey(safeCycleId, safePlayerName, safeRank);

            RewardClaimRecord existing = _database.Claims.FirstOrDefault(x =>
                x != null &&
                string.Equals(BuildRewardClaimKey(x.CycleId, x.PlayerName, x.Rank), key, StringComparison.OrdinalIgnoreCase));

            if (existing != null)
            {
                existing.Status = "pending";
                existing.ClaimedAtUtc = "";
                return;
            }

            _database.Claims.Add(new RewardClaimRecord
            {
                CycleId = safeCycleId,
                PlayerName = safePlayerName,
                Rank = safeRank,
                ClaimedAtUtc = "",
                Status = "pending"
            });
        }

        private void ClearPendingRewardClaim(string cycleId, string playerName, int rank)
        {
            if (_database == null || _database.Claims == null)
                return;

            string key = BuildRewardClaimKey(cycleId, playerName, rank);
            _database.Claims.RemoveAll(x =>
                x != null &&
                string.Equals(BuildRewardClaimKey(x.CycleId, x.PlayerName, x.Rank), key, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(SafeLimit(x.Status, 16), "pending", StringComparison.OrdinalIgnoreCase));
        }

        private void MarkRewardClaimed(string cycleId, string playerName, int rank)
        {
            if (_database == null)
                _database = new RankingDatabase();

            if (_database.Claims == null)
                _database.Claims = new List<RewardClaimRecord>();

            string safeCycleId = SafeLimit(cycleId, 64);
            string safePlayerName = SanitizePlayerName(playerName);
            int safeRank = Mathf.Clamp(rank, 1, int.MaxValue);
            string key = BuildRewardClaimKey(safeCycleId, safePlayerName, safeRank);

            RewardClaimRecord existing = _database.Claims.FirstOrDefault(x =>
                x != null &&
                string.Equals(BuildRewardClaimKey(x.CycleId, x.PlayerName, x.Rank), key, StringComparison.OrdinalIgnoreCase));

            if (existing != null)
            {
                existing.Status = "claimed";
                existing.ClaimedAtUtc = DateTime.UtcNow.ToString("O");
                return;
            }

            _database.Claims.Add(new RewardClaimRecord
            {
                CycleId = safeCycleId,
                PlayerName = safePlayerName,
                Rank = safeRank,
                ClaimedAtUtc = DateTime.UtcNow.ToString("O"),
                Status = "claimed"
            });
        }

        private void FillRewardSnapshotData(SnapshotPlayerData playerData)
        {
            if (playerData == null)
                return;

            playerData.RewardClaimsEnabled = _rules != null && _rules.RewardClaimsEnabled;
            playerData.RewardCanClaim = false;
            playerData.RewardAlreadyClaimed = false;
            playerData.RewardRank = Mathf.Max(0, playerData.Position);
            playerData.RewardMinPoints = 0;
            playerData.RewardLabel = "";
            playerData.RewardPrefabName = "";
            playerData.RewardAmount = 0;
            playerData.RewardBlockReason = "";
            playerData.RewardClaimCycleId = _rules != null ? SafeLimit(_rules.RewardClaimCycleId, 64) : "";

            if (_rules == null || !_rules.RewardClaimsEnabled)
            {
                playerData.RewardBlockReason = "Resgate de recompensa desativado.";
                return;
            }

            if (!playerData.HasData)
            {
                playerData.RewardBlockReason = "Você ainda não possui pontuação no ranking.";
                return;
            }

            RankRewardInfo rewardInfo = GetRankRewardInfo(playerData.Position);
            if (rewardInfo == null)
            {
                playerData.RewardBlockReason = "Somente Top 1, 2 e 3 podem resgatar.";
                return;
            }

            playerData.RewardRank = rewardInfo.Rank;
            playerData.RewardMinPoints = rewardInfo.MinPoints;
            playerData.RewardLabel = rewardInfo.Label ?? "";
            playerData.RewardPrefabName = rewardInfo.PrefabName ?? "";
            playerData.RewardAmount = Mathf.Max(0, rewardInfo.Amount);

            if (string.IsNullOrWhiteSpace(playerData.RewardClaimCycleId))
            {
                playerData.RewardBlockReason = "Ciclo de recompensa não configurado.";
                return;
            }

            if (string.IsNullOrWhiteSpace(playerData.RewardPrefabName) || playerData.RewardAmount <= 0)
            {
                playerData.RewardBlockReason = "Recompensa não configurada para esta posição.";
                return;
            }

            if (playerData.Points < rewardInfo.MinPoints)
            {
                playerData.RewardBlockReason = "Requer " + rewardInfo.MinPoints + " pontos para resgatar.";
                return;
            }

            if (HasClaimedReward(playerData.RewardClaimCycleId, playerData.PlayerName, rewardInfo.Rank))
            {
                playerData.RewardAlreadyClaimed = true;
                playerData.RewardBlockReason = "Recompensa já resgatada.";
                return;
            }

            playerData.RewardCanClaim = true;
            playerData.RewardBlockReason = "Recompensa disponível para resgate.";
        }

        private bool TryAddRewardItemToLocalInventory(string prefabName, int amount, out string message)
        {
            message = "";

            try
            {
                if (Player.m_localPlayer == null)
                {
                    message = "Jogador local não encontrado.";
                    return false;
                }

                if (string.IsNullOrWhiteSpace(prefabName))
                {
                    message = "Prefab da recompensa não configurado.";
                    return false;
                }

                if (amount <= 0)
                {
                    message = "Quantidade de recompensa inválida.";
                    return false;
                }

                GameObject itemPrefab = null;
                if (ObjectDB.instance != null)
                    itemPrefab = ObjectDB.instance.GetItemPrefab(prefabName);

                if (itemPrefab == null && ZNetScene.instance != null)
                    itemPrefab = ZNetScene.instance.GetPrefab(prefabName);

                if (itemPrefab == null)
                {
                    message = "Prefab '" + prefabName + "' não encontrado.";
                    return false;
                }

                ItemDrop itemDrop = itemPrefab.GetComponent<ItemDrop>();
                if (itemDrop == null)
                {
                    message = "Prefab '" + prefabName + "' não é um item válido.";
                    return false;
                }

                Inventory inventory = Player.m_localPlayer.GetInventory();
                if (inventory == null)
                {
                    message = "Inventário não encontrado.";
                    return false;
                }

                int remaining = Mathf.Max(0, amount);
                int addedToInventory = 0;
                int droppedOnGround = 0;

                int quality = 1;
                int variant = 0;
                int maxStackSize = 1;
                string itemName = prefabName;

                if (itemDrop.m_itemData != null)
                {
                    quality = Mathf.Max(1, itemDrop.m_itemData.m_quality);
                    variant = Mathf.Max(0, itemDrop.m_itemData.m_variant);

                    if (itemDrop.m_itemData.m_shared != null)
                    {
                        maxStackSize = Mathf.Max(1, itemDrop.m_itemData.m_shared.m_maxStackSize);

                        if (!string.IsNullOrWhiteSpace(itemDrop.m_itemData.m_shared.m_name))
                            itemName = itemDrop.m_itemData.m_shared.m_name;
                    }
                }

                while (remaining > 0)
                {
                    int stackAmount = Mathf.Min(maxStackSize, remaining);

                    ItemDrop.ItemData addedItem = inventory.AddItem(
                        prefabName,
                        stackAmount,
                        quality,
                        variant,
                        0L,
                        "");

                    if (addedItem == null)
                        break;

                    addedToInventory += stackAmount;
                    remaining -= stackAmount;
                }

                if (remaining > 0)
                {
                    droppedOnGround = DropExchangeItemOverflowNearPlayer(itemPrefab, itemDrop, remaining, maxStackSize, quality, variant);
                    remaining -= droppedOnGround;
                }

                if (remaining > 0)
                {
                    message = "Não foi possível entregar toda a recompensa.";
                    return false;
                }

                try
                {
                    if (droppedOnGround > 0 && addedToInventory > 0)
                        Player.m_localPlayer.Message(MessageHud.MessageType.Center, "Recompensa recebida: " + itemName + " x" + addedToInventory + " no inventário, x" + droppedOnGround + " no chão.");
                    else if (droppedOnGround > 0)
                        Player.m_localPlayer.Message(MessageHud.MessageType.Center, "Inventário cheio. Recompensa caiu no chão: " + itemName + " x" + droppedOnGround + ".");
                    else
                        Player.m_localPlayer.Message(MessageHud.MessageType.Center, "Recompensa recebida: " + itemName + " x" + addedToInventory + ".");
                }
                catch { }

                if (droppedOnGround > 0 && addedToInventory > 0)
                    message = "Parte da recompensa foi para o inventário e o restante caiu no chão.";
                else if (droppedOnGround > 0)
                    message = "Inventário cheio: a recompensa caiu no chão.";
                else
                    message = "Recompensa adicionada ao inventário.";

                return true;
            }
            catch (Exception ex)
            {
                Logger.LogError("Erro ao entregar recompensa: " + ex);
                message = "Erro ao entregar recompensa.";
                return false;
            }
        }

        private bool TryAddExchangeItemToLocalInventoryOrDropOverflow(string prefabName, int amount, out string message)
        {
            message = "";

            try
            {
                if (Player.m_localPlayer == null)
                {
                    message = "Jogador local não encontrado.";
                    return false;
                }

                if (string.IsNullOrWhiteSpace(prefabName))
                {
                    message = "Prefab do câmbio não configurado.";
                    return false;
                }

                if (amount <= 0)
                {
                    message = "Quantidade de câmbio inválida.";
                    return false;
                }

                GameObject itemPrefab = null;
                if (ObjectDB.instance != null)
                    itemPrefab = ObjectDB.instance.GetItemPrefab(prefabName);

                if (itemPrefab == null && ZNetScene.instance != null)
                    itemPrefab = ZNetScene.instance.GetPrefab(prefabName);

                if (itemPrefab == null)
                {
                    message = "Prefab '" + prefabName + "' não encontrado.";
                    return false;
                }

                ItemDrop itemDrop = itemPrefab.GetComponent<ItemDrop>();
                if (itemDrop == null)
                {
                    message = "Prefab '" + prefabName + "' não é um item válido.";
                    return false;
                }

                Inventory inventory = Player.m_localPlayer.GetInventory();
                if (inventory == null)
                {
                    message = "Inventário não encontrado.";
                    return false;
                }

                int remaining = Mathf.Max(0, amount);
                int addedToInventory = 0;
                int droppedOnGround = 0;

                int quality = 1;
                int variant = 0;
                int maxStackSize = 1;
                string itemName = prefabName;

                if (itemDrop.m_itemData != null)
                {
                    quality = Mathf.Max(1, itemDrop.m_itemData.m_quality);
                    variant = Mathf.Max(0, itemDrop.m_itemData.m_variant);

                    if (itemDrop.m_itemData.m_shared != null)
                    {
                        maxStackSize = Mathf.Max(1, itemDrop.m_itemData.m_shared.m_maxStackSize);
                        if (!string.IsNullOrWhiteSpace(itemDrop.m_itemData.m_shared.m_name))
                            itemName = itemDrop.m_itemData.m_shared.m_name;
                    }
                }


                while (remaining > 0)
                {
                    int stackAmount = Mathf.Min(maxStackSize, remaining);

                    ItemDrop.ItemData addedItem = inventory.AddItem(
                        prefabName,
                        stackAmount,
                        quality,
                        variant,
                        0L,
                        "");

                    if (addedItem == null)
                        break;

                    addedToInventory += stackAmount;
                    remaining -= stackAmount;
                }

                if (remaining > 0)
                {
                    droppedOnGround = DropExchangeItemOverflowNearPlayer(itemPrefab, itemDrop, remaining, maxStackSize, quality, variant);
                    remaining -= droppedOnGround;
                }

                if (remaining > 0)
                {
                    message = "Não foi possível entregar todo o câmbio.";
                    return false;
                }

                try
                {
                    if (droppedOnGround > 0 && addedToInventory > 0)
                        Player.m_localPlayer.Message(MessageHud.MessageType.Center, "Câmbio recebido: " + itemName + " x" + addedToInventory + " no inventário, x" + droppedOnGround + " no chão.");
                    else if (droppedOnGround > 0)
                        Player.m_localPlayer.Message(MessageHud.MessageType.Center, "Inventário cheio. Câmbio caiu no chão: " + itemName + " x" + droppedOnGround + ".");
                    else
                        Player.m_localPlayer.Message(MessageHud.MessageType.Center, "Câmbio recebido: " + itemName + " x" + addedToInventory + ".");
                }
                catch { }

                if (droppedOnGround > 0 && addedToInventory > 0)
                    message = "Parte foi para o inventário e o restante caiu no chão.";
                else if (droppedOnGround > 0)
                    message = "Inventário cheio: as moedas caíram no chão.";
                else
                    message = "Moedas adicionadas ao inventário.";

                return true;
            }
            catch (Exception ex)
            {
                Logger.LogError("Erro ao entregar moedas do câmbio: " + ex);
                message = "Erro ao entregar moedas do câmbio.";
                return false;
            }
        }

        private int DropExchangeItemOverflowNearPlayer(GameObject itemPrefab, ItemDrop sourceDrop, int amount, int maxStackSize, int quality, int variant)
        {
            if (Player.m_localPlayer == null || itemPrefab == null || amount <= 0)
                return 0;

            int remaining = amount;
            int dropped = 0;
            Transform playerTransform = Player.m_localPlayer.transform;
            Vector3 basePos = playerTransform.position + playerTransform.forward * 1.25f + Vector3.up * 0.65f;

            while (remaining > 0)
            {
                int stackAmount = Mathf.Min(Mathf.Max(1, maxStackSize), remaining);
                Vector3 randomOffset = new Vector3(UnityEngine.Random.Range(-0.35f, 0.35f), 0f, UnityEngine.Random.Range(-0.35f, 0.35f));
                GameObject droppedObject = UnityEngine.Object.Instantiate(itemPrefab, basePos + randomOffset, Quaternion.identity);

                ItemDrop droppedItem = droppedObject != null ? droppedObject.GetComponent<ItemDrop>() : null;
                if (droppedItem != null && droppedItem.m_itemData != null)
                {
                    droppedItem.m_itemData.m_stack = stackAmount;
                    droppedItem.m_itemData.m_quality = Mathf.Max(1, quality);
                    droppedItem.m_itemData.m_variant = Mathf.Max(0, variant);
                }

                Rigidbody body = droppedObject != null ? droppedObject.GetComponent<Rigidbody>() : null;
                if (body != null)
                    body.linearVelocity = playerTransform.forward * 1.5f + Vector3.up * 1.25f;

                dropped += stackAmount;
                remaining -= stackAmount;
            }

            return dropped;
        }
    }
}
