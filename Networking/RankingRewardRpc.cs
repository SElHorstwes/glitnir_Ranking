using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Glitnir.Ranking
{
    public partial class GlitnirRankingPlugin
    {
        private void RequestRewardClaimFromServer()
        {
            try
            {
                if (_cachedPlayerData == null)
                    return;

                if (_rewardClaimRequestPending)
                    return;

                if (!_cachedPlayerData.RewardCanClaim)
                {
                    SetStatus(string.IsNullOrWhiteSpace(_cachedPlayerData.RewardBlockReason)
                        ? "Recompensa indisponível."
                        : _cachedPlayerData.RewardBlockReason, 3f);
                    return;
                }

                if (!_rpcsRegistered || ZRoutedRpc.instance == null || ZNet.instance == null)
                    return;

                long serverUid = GetServerPeerUid();
                if (serverUid == 0L)
                    return;

                ZPackage pkg = new ZPackage();
                pkg.Write(_cachedPlayerData.RewardClaimCycleId ?? "");
                pkg.Write(Mathf.Clamp(_cachedPlayerData.RewardRank, 0, int.MaxValue));
                ZRoutedRpc.instance.InvokeRoutedRPC(serverUid, RpcRequestRewardClaim, pkg);

                _rewardClaimRequestPending = true;
                _rewardClaimPendingCycleId = _cachedPlayerData.RewardClaimCycleId ?? "";
                _rewardClaimPendingRank = Mathf.Clamp(_cachedPlayerData.RewardRank, 0, int.MaxValue);
                SetStatus("Solicitando resgate da recompensa...", 2f);
            }
            catch (Exception ex)
            {
                Logger.LogError("Erro ao solicitar resgate da recompensa: " + ex);
            }
        }

        private void RequestPointsExchangeFromServer(int requestedPoints)
        {
            try
            {
                if (_pointsExchangeRequestPending)
                    return;

                if (_cachedPlayerData == null || _cachedPlayerData.Points <= 0)
                {
                    SetStatus("Você não possui pontos para trocar.", 3f);
                    return;
                }

                if (_rules == null || !_rules.PointsExchangeEnabled)
                {
                    SetStatus("Câmbio de pontos desativado.", 3f);
                    return;
                }

                int availablePoints = Mathf.Max(0, _cachedPlayerData.Points);
                int maxPoints = Mathf.Max(0, _rules.PointsExchangeMaxPointsPerRequest);
                int maxSelectablePoints = maxPoints > 0 ? Mathf.Min(availablePoints, maxPoints) : availablePoints;

                if (requestedPoints <= 0)
                {
                    SetStatus("Informe uma quantidade válida para trocar.", 3f);
                    return;
                }

                if (requestedPoints > maxSelectablePoints)
                {
                    SetStatus("Quantidade acima do limite disponível para câmbio.", 3f);
                    return;
                }

                if (!_rpcsRegistered || ZRoutedRpc.instance == null || ZNet.instance == null)
                    return;

                long serverUid = GetServerPeerUid();
                if (serverUid == 0L)
                    return;

                ZPackage pkg = new ZPackage();
                pkg.Write(SafeLimit(_rules.RewardClaimCycleId, 64));
                pkg.Write(Mathf.Clamp(requestedPoints, 1, int.MaxValue));
                ZRoutedRpc.instance.InvokeRoutedRPC(serverUid, RpcRequestPointsExchange, pkg);

                _pointsExchangeRequestPending = true;
                _unityHudExchangePendingSince = Time.realtimeSinceStartup;
                SetStatus("Solicitando câmbio de " + requestedPoints + " pontos por moedas...", 2f);
            }
            catch (Exception ex)
            {
                Logger.LogError("Erro ao solicitar câmbio de pontos: " + ex);
            }
        }

        private void RPC_RequestRewardClaim(long sender, ZPackage pkg)
        {
            try
            {
                if (!IsServerInstance() || pkg == null)
                    return;

                string cycleId = SafeLimit(pkg.ReadString(), 64);
                int requestedRank = Mathf.Clamp(pkg.ReadInt(), 0, int.MaxValue);
                string playerName = ResolvePlayerNameFromSender(sender);

                if (string.IsNullOrWhiteSpace(playerName))
                {
                    SendRewardClaimFeedback(sender, false, "Não foi possível identificar o jogador.");
                    return;
                }

                if (_rules == null || !_rules.RewardClaimsEnabled)
                {
                    SendRewardClaimFeedback(sender, false, "Sistema de recompensas desativado.");
                    return;
                }

                if (!string.Equals(SafeLimit(_rules.RewardClaimCycleId, 64), cycleId, StringComparison.OrdinalIgnoreCase))
                {
                    SendRewardClaimFeedback(sender, false, "Ciclo de recompensa inválido. Abra o ranking novamente.");
                    return;
                }

                List<RankingEntry> ordered = _database.Entries
                    .Where(x => x != null && !string.IsNullOrWhiteSpace(x.PlayerName) && !ShouldIgnorePlayerForRanking(x.PlayerName))
                    .OrderByDescending(x => x.Points)
                    .ThenByDescending(x => x.BossPointsTotal)
                    .ThenByDescending(x => x.KillPointsTotal)
                    .ThenByDescending(x => x.SkillPointsTotal)
                    .ThenBy(x => x.PlayerName)
                    .ToList();

                RankingEntry entry = null;
                int actualRank = -1;

                for (int i = 0; i < ordered.Count; i++)
                {
                    if (ordered[i] == null)
                        continue;

                    if (!string.Equals(ordered[i].PlayerName, playerName, StringComparison.OrdinalIgnoreCase))
                        continue;

                    entry = ordered[i];
                    actualRank = i + 1;
                    break;
                }

                if (entry == null || actualRank <= 0)
                {
                    SendRewardClaimFeedback(sender, false, "Você não está no ranking.");
                    return;
                }

                if (actualRank != requestedRank)
                {
                    SendRewardClaimFeedback(sender, false, "Sua posição mudou. Abra o ranking novamente.");
                    return;
                }

                RankRewardInfo rewardInfo = GetRankRewardInfo(actualRank);
                if (rewardInfo == null)
                {
                    SendRewardClaimFeedback(sender, false, "Somente Top 1, 2 e 3 podem resgatar.");
                    return;
                }

                if (entry.Points < rewardInfo.MinPoints)
                {
                    SendRewardClaimFeedback(sender, false, "Você precisa de " + rewardInfo.MinPoints + " pontos para resgatar.");
                    return;
                }

                if (string.IsNullOrWhiteSpace(rewardInfo.PrefabName) || rewardInfo.Amount <= 0)
                {
                    SendRewardClaimFeedback(sender, false, "Recompensa não configurada.");
                    return;
                }

                if (HasClaimedReward(cycleId, playerName, actualRank))
                {
                    SendRewardClaimFeedback(sender, false, "Essa recompensa já foi resgatada.");
                    return;
                }

                if (HasPendingRewardClaim(cycleId, playerName, actualRank))
                {
                    SendRewardClaimFeedback(sender, false, "Essa recompensa já está em processamento.");
                    return;
                }

                string pendingKey = BuildPendingRewardKey(sender, cycleId, actualRank);
                if (_pendingRewardClaims.Contains(pendingKey))
                {
                    SendRewardClaimFeedback(sender, false, "Já existe um resgate em andamento.");
                    return;
                }

                ReserveRewardClaim(cycleId, playerName, actualRank);
                SaveRewardClaim(cycleId, playerName, actualRank);
                _pendingRewardClaims.Add(pendingKey);

                ZPackage response = new ZPackage();
                response.Write(cycleId);
                response.Write(actualRank);
                response.Write(SafeLimit(playerName, MaxPlayerNameLength));
                response.Write(rewardInfo.PrefabName ?? "");
                response.Write(Mathf.Max(1, rewardInfo.Amount));
                response.Write(rewardInfo.Label ?? "");
                ZRoutedRpc.instance.InvokeRoutedRPC(sender, RpcGrantRewardItem, response);
            }
            catch (Exception ex)
            {
                Logger.LogError("Erro no RPC_RequestRewardClaim: " + ex);
                SendRewardClaimFeedback(sender, false, "Erro interno ao validar recompensa.");
            }
        }

        private void RPC_GrantRewardItem(long sender, ZPackage pkg)
        {
            try
            {
                if (pkg == null)
                    return;

                string cycleId = SafeLimit(pkg.ReadString(), 64);
                int rank = Mathf.Clamp(pkg.ReadInt(), 0, int.MaxValue);
                string playerName = SanitizePlayerName(pkg.ReadString());
                string prefabName = SafeLimit(pkg.ReadString(), 96);
                int amount = Mathf.Clamp(pkg.ReadInt(), 0, int.MaxValue);
                string label = SafeLimit(pkg.ReadString(), 96);

                bool success = TryAddRewardItemToLocalInventory(prefabName, amount, out string message);

                ZPackage response = new ZPackage();
                response.Write(cycleId);
                response.Write(rank);
                response.Write(playerName);
                response.Write(success);
                response.Write(string.IsNullOrWhiteSpace(message) ? label : message);
                ZRoutedRpc.instance.InvokeRoutedRPC(GetServerPeerUid(), RpcFinalizeRewardClaim, response);
            }
            catch (Exception ex)
            {
                Logger.LogError("Erro no RPC_GrantRewardItem: " + ex);
            }
        }


        private void RPC_FinalizeRewardClaim(long sender, ZPackage pkg)
        {
            try
            {
                if (!IsServerInstance() || pkg == null)
                    return;

                string cycleId = SafeLimit(pkg.ReadString(), 64);
                int rank = Mathf.Clamp(pkg.ReadInt(), 0, int.MaxValue);
                string playerName = SanitizePlayerName(pkg.ReadString());
                bool success = pkg.ReadBool();
                string message = SafeLimit(pkg.ReadString(), 128);

                string pendingKey = BuildPendingRewardKey(sender, cycleId, rank);
                if (_pendingRewardClaims.Contains(pendingKey))
                    _pendingRewardClaims.Remove(pendingKey);

                string resolvedPlayerName = ResolvePlayerNameFromSender(sender);
                if (!string.IsNullOrWhiteSpace(resolvedPlayerName))
                    playerName = resolvedPlayerName;

                if (string.IsNullOrWhiteSpace(playerName))
                {
                    ClearPendingRewardClaim(cycleId, playerName, rank);
                    DeleteRewardClaim(cycleId, playerName, rank);
                    SendRewardClaimFeedback(sender, false, "Não foi possível confirmar o jogador.");
                    return;
                }

                if (!success)
                {
                    ClearPendingRewardClaim(cycleId, playerName, rank);
                    DeleteRewardClaim(cycleId, playerName, rank);
                    SendRewardClaimFeedback(sender, false, string.IsNullOrWhiteSpace(message) ? "Falha ao adicionar item no inventário." : message);
                    return;
                }

                if (HasClaimedReward(cycleId, playerName, rank))
                {
                    SendRewardClaimFeedback(sender, false, "Essa recompensa já foi resgatada.");
                    return;
                }

                MarkRewardClaimed(cycleId, playerName, rank);
                SaveRewardClaim(cycleId, playerName, rank);
                SendRewardClaimFeedback(sender, true, string.IsNullOrWhiteSpace(message) ? "Recompensa adicionada ao inventário." : message);
            }
            catch (Exception ex)
            {
                Logger.LogError("Erro no RPC_FinalizeRewardClaim: " + ex);
                SendRewardClaimFeedback(sender, false, "Erro ao finalizar o resgate.");
            }
        }


        private void RPC_RequestPointsExchange(long sender, ZPackage pkg)
        {
            try
            {
                if (!IsServerInstance() || pkg == null)
                    return;

                string cycleId = SafeLimit(pkg.ReadString(), 64);
                int requestedPoints = Mathf.Clamp(pkg.ReadInt(), 0, int.MaxValue);
                string playerName = ResolvePlayerNameFromSender(sender);

                if (string.IsNullOrWhiteSpace(playerName))
                {
                    SendPointsExchangeFeedback(sender, false, "Não foi possível identificar o jogador.");
                    return;
                }

                if (_rules == null || !_rules.PointsExchangeEnabled)
                {
                    SendPointsExchangeFeedback(sender, false, "Câmbio de pontos desativado.");
                    return;
                }

                if (!string.Equals(SafeLimit(_rules.RewardClaimCycleId, 64), cycleId, StringComparison.OrdinalIgnoreCase))
                {
                    SendPointsExchangeFeedback(sender, false, "Ciclo inválido. Abra o ranking novamente.");
                    return;
                }

                if (string.IsNullOrWhiteSpace(_rules.PointsExchangePrefab))
                {
                    SendPointsExchangeFeedback(sender, false, "Prefab do câmbio não configurado.");
                    return;
                }

                RankingEntry entry = _database.Entries
                    .FirstOrDefault(x => x != null && string.Equals(x.PlayerName, playerName, StringComparison.OrdinalIgnoreCase));

                if (entry == null)
                {
                    SendPointsExchangeFeedback(sender, false, "Você não está no ranking.");
                    return;
                }

                int availablePoints = Mathf.Max(0, entry.Points);

                if (availablePoints <= 0)
                {
                    SendPointsExchangeFeedback(sender, false, "Você não possui pontos para trocar.");
                    return;
                }

                string pendingKey = BuildPendingPointsExchangeKey(sender, cycleId);
                if (_pendingPointsExchanges.Contains(pendingKey))
                {
                    SendPointsExchangeFeedback(sender, false, "Já existe um câmbio em andamento.");
                    return;
                }

                int maxPointsPerRequest = Mathf.Max(0, _rules.PointsExchangeMaxPointsPerRequest);
                int maxSelectablePoints = maxPointsPerRequest > 0 ? Mathf.Min(availablePoints, maxPointsPerRequest) : availablePoints;

                if (requestedPoints <= 0)
                {
                    SendPointsExchangeFeedback(sender, false, "Informe uma quantidade válida para trocar.");
                    return;
                }

                if (requestedPoints > availablePoints)
                {
                    SendPointsExchangeFeedback(sender, false, "Você possui apenas " + availablePoints + " pontos.");
                    return;
                }

                if (requestedPoints > maxSelectablePoints)
                {
                    SendPointsExchangeFeedback(sender, false, "O limite por câmbio é " + maxSelectablePoints + " pontos.");
                    return;
                }

                int pointsToExchange = requestedPoints;
                int amount = 0;

                if (_rules.PointsExchangeUsePointsPerCoin)
                {
                    int pointsPerCoin = Mathf.Max(1, _rules.PointsExchangePointsPerCoin);
                    amount = Mathf.Max(0, requestedPoints / pointsPerCoin);
                    pointsToExchange = amount * pointsPerCoin;

                    if (amount <= 0 || pointsToExchange <= 0)
                    {
                        SendPointsExchangeFeedback(sender, false, "Você precisa de pelo menos " + pointsPerCoin + " pontos para receber 1 moeda.");
                        return;
                    }
                }
                else if (_rules.PointsExchangeUseCoinsPerPoint)
                {
                    int coinsPerPoint = Mathf.Max(1, _rules.PointsExchangeCoinsPerPoint);
                    amount = Mathf.Max(1, pointsToExchange * coinsPerPoint);
                }
                else
                {
                    SendPointsExchangeFeedback(sender, false, "Nenhum modo de câmbio está ativo. Ative UsePointsPerCoin ou UseCoinsPerPoint.");
                    return;
                }

                _pendingPointsExchanges.Add(pendingKey);

                ZPackage response = new ZPackage();
                response.Write(cycleId);
                response.Write(SafeLimit(playerName, MaxPlayerNameLength));
                response.Write(pointsToExchange);
                response.Write(_rules.PointsExchangePrefab ?? "Coins");
                response.Write(amount);
                ZRoutedRpc.instance.InvokeRoutedRPC(sender, RpcGrantExchangeCoins, response);
            }
            catch (Exception ex)
            {
                Logger.LogError("Erro no RPC_RequestPointsExchange: " + ex);
                SendPointsExchangeFeedback(sender, false, "Erro ao processar o câmbio.");
            }
        }

        private void RPC_GrantExchangeCoins(long sender, ZPackage pkg)
        {
            try
            {
                if (pkg == null)
                    return;

                string cycleId = SafeLimit(pkg.ReadString(), 64);
                string playerName = SanitizePlayerName(pkg.ReadString());
                int pointsToExchange = Mathf.Clamp(pkg.ReadInt(), 0, int.MaxValue);
                string prefabName = SafeLimit(pkg.ReadString(), 96);
                int amount = Mathf.Clamp(pkg.ReadInt(), 0, int.MaxValue);

                bool success = TryAddExchangeItemToLocalInventoryOrDropOverflow(prefabName, amount, out string message);

                ZPackage response = new ZPackage();
                response.Write(cycleId);
                response.Write(playerName);
                response.Write(pointsToExchange);
                response.Write(success);
                response.Write(success ? ("Câmbio concluído: -" + pointsToExchange + " pontos, +" + amount + " " + prefabName + ". " + message) : message);
                ZRoutedRpc.instance.InvokeRoutedRPC(GetServerPeerUid(), RpcFinalizePointsExchange, response);
            }
            catch (Exception ex)
            {
                Logger.LogError("Erro no RPC_GrantExchangeCoins: " + ex);
            }
        }

        private void RPC_FinalizePointsExchange(long sender, ZPackage pkg)
        {
            try
            {
                if (!IsServerInstance() || pkg == null)
                    return;

                string cycleId = SafeLimit(pkg.ReadString(), 64);
                string playerName = SanitizePlayerName(pkg.ReadString());
                int pointsToExchange = Mathf.Clamp(pkg.ReadInt(), 0, int.MaxValue);
                bool success = pkg.ReadBool();
                string message = SafeLimit(pkg.ReadString(), 128);

                string pendingKey = BuildPendingPointsExchangeKey(sender, cycleId);
                if (_pendingPointsExchanges.Contains(pendingKey))
                    _pendingPointsExchanges.Remove(pendingKey);

                string resolvedPlayerName = ResolvePlayerNameFromSender(sender);
                if (!string.IsNullOrWhiteSpace(resolvedPlayerName))
                    playerName = resolvedPlayerName;

                RankingEntry entry = _database.Entries
                    .FirstOrDefault(x => x != null && string.Equals(x.PlayerName, playerName, StringComparison.OrdinalIgnoreCase));

                if (entry == null)
                {
                    SendPointsExchangeFeedback(sender, false, "Jogador não encontrado no ranking.");
                    return;
                }

                if (!success)
                {
                    SendPointsExchangeFeedback(sender, false, string.IsNullOrWhiteSpace(message) ? "Falha ao adicionar moedas no inventário." : message);
                    return;
                }

                if (pointsToExchange <= 0 || entry.Points < pointsToExchange)
                {
                    SendPointsExchangeFeedback(sender, false, "Pontos insuficientes para finalizar o câmbio.");
                    return;
                }

                int coinsReceived = 0;
                if (_rules != null && _rules.PointsExchangeUsePointsPerCoin)
                    coinsReceived = pointsToExchange / Mathf.Max(1, _rules.PointsExchangePointsPerCoin);
                else
                    coinsReceived = Mathf.Max(1, pointsToExchange * Mathf.Max(1, _rules != null ? _rules.PointsExchangeCoinsPerPoint : 1));

                entry.Points = Mathf.Max(0, entry.Points - pointsToExchange);
                entry.TotalPointsExchanges = Mathf.Clamp(entry.TotalPointsExchanges + 1, 0, int.MaxValue);
                entry.PointsExchangePenaltyTotal = Mathf.Clamp(entry.PointsExchangePenaltyTotal + pointsToExchange, 0, int.MaxValue);
                entry.PointsExchangeCoinsTotal = Mathf.Clamp(entry.PointsExchangeCoinsTotal + coinsReceived, 0, int.MaxValue);
                entry.LastReason = "Câmbio de pontos: -" + pointsToExchange + " pts, +" + coinsReceived + " moedas";
                entry.LastUpdateUtc = DateTime.UtcNow.ToString("o");
                SaveRankingEntry(entry);

                SendPointsExchangeFeedback(sender, true, string.IsNullOrWhiteSpace(message) ? "Câmbio concluído." : message);
            }
            catch (Exception ex)
            {
                Logger.LogError("Erro no RPC_FinalizePointsExchange: " + ex);
                SendPointsExchangeFeedback(sender, false, "Erro ao finalizar o câmbio.");
            }
        }

        private void RPC_PointsExchangeFeedback(long sender, ZPackage pkg)
        {
            try
            {
                if (pkg == null)
                    return;

                bool success = pkg.ReadBool();
                string message = SafeLimit(pkg.ReadString(), 128);

                _pointsExchangeRequestPending = false;
                _unityHudExchangePendingSince = 0f;
                _unityHudExchangePoints = 0;
                _unityHudNextRefresh = 0f;

                SetStatus(string.IsNullOrWhiteSpace(message)
                    ? (success ? "Câmbio concluído." : "Câmbio não concluído.")
                    : message, success ? 4f : 3.5f);

                if (success)
                    SpawnGlitnirRewardClaimEffect();

                RequestSnapshotFromServer();
            }
            catch (Exception ex)
            {
                Logger.LogError("Erro no RPC_PointsExchangeFeedback: " + ex);
            }
        }

        private void SendPointsExchangeFeedback(long sender, bool success, string message)
        {
            if (ZRoutedRpc.instance == null)
                return;

            ZPackage pkg = new ZPackage();
            pkg.Write(success);
            pkg.Write(SafeLimit(message, 128));
            ZRoutedRpc.instance.InvokeRoutedRPC(sender, RpcPointsExchangeFeedback, pkg);
        }

        private string BuildPendingPointsExchangeKey(long sender, string cycleId)
        {
            return sender + "|" + SafeLimit(cycleId, 64);
        }

        private void RPC_RewardClaimFeedback(long sender, ZPackage pkg)
        {
            try
            {
                if (pkg == null)
                    return;

                bool success = pkg.ReadBool();
                string message = SafeLimit(pkg.ReadString(), 128);

                _rewardClaimRequestPending = false;
                _rewardClaimPendingCycleId = "";
                _rewardClaimPendingRank = 0;

                SetStatus(string.IsNullOrWhiteSpace(message)
                    ? (success ? "Recompensa resgatada." : "Resgate não concluído.")
                    : message, success ? 4f : 3.5f);

                if (success)
                    SpawnGlitnirRewardClaimEffect();

                RequestSnapshotFromServer();
            }
            catch (Exception ex)
            {
                Logger.LogError("Erro no RPC_RewardClaimFeedback: " + ex);
            }
        }

        private void SendRewardClaimFeedback(long sender, bool success, string message)
        {
            if (ZRoutedRpc.instance == null)
                return;

            ZPackage pkg = new ZPackage();
            pkg.Write(success);
            pkg.Write(SafeLimit(message, 128));
            ZRoutedRpc.instance.InvokeRoutedRPC(sender, RpcRewardClaimFeedback, pkg);
        }

        private void SpawnGlitnirRewardClaimEffect()
        {
            try
            {
                Player player = Player.m_localPlayer;
                if (player == null || ZNetScene.instance == null)
                    return;


                string[] effectPrefabs =
                {
                            "vfx_Potion_stamina_medium",
                            "vfx_Potion_health_medium",
                            "vfx_Potion_eitr_minor",
                            "vfx_coin_pile_destroyed"
                        };

                GameObject prefab = null;
                foreach (string prefabName in effectPrefabs)
                {
                    prefab = ZNetScene.instance.GetPrefab(prefabName);
                    if (prefab != null)
                        break;
                }

                if (prefab == null)
                {
                    Logger.LogWarning("[GlitnirRanking] Nenhum prefab de efeito encontrado para o claim.");
                    return;
                }

                Vector3 center = player.transform.position;
                const int count = 10;
                const float radius = 2.1f;

                for (int i = 0; i < count; i++)
                {
                    float angle = i * Mathf.PI * 2f / count;
                    Vector3 pos = center + new Vector3(
                        Mathf.Cos(angle) * radius,
                        0.9f,
                        Mathf.Sin(angle) * radius
                    );

                    UnityEngine.Object.Instantiate(prefab, pos, Quaternion.identity);
                }


                UnityEngine.Object.Instantiate(prefab, center + Vector3.up * 1.6f, Quaternion.identity);
            }
            catch (Exception ex)
            {
                Logger.LogWarning("[GlitnirRanking] Falha ao criar efeito visual do claim: " + ex.Message);
            }
        }

    }
}
