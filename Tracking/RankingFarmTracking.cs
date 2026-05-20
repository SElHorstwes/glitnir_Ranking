using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace Glitnir.Ranking
{
    public partial class GlitnirRankingPlugin
    {
        public void NotifyLocalFarmHarvest(string prefabName, int amount)
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

                ZRoutedRpc.instance.InvokeRoutedRPC(serverPeerUid, RpcReportFarmHarvest, pkg);

                DebugLog(DebugCategory.Points, "Cliente reportando colheita: player=" + GetLocalPlayerName() + " item=" + prefabName + " amount=" + amount);
            }
            catch (Exception ex)
            {
                Logger.LogError("Erro ao enviar colheita local: " + ex);
            }
        }

        private void RPC_ReportFarmHarvest(long sender, ZPackage pkg)
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
                    DebugLog(DebugCategory.Points, "Cultivo ignorado para admin: " + playerName);
                    return;
                }

                ProcessFarmHarvestReport(playerName, prefabName, amount, "rpc-farm");
            }
            catch (Exception ex)
            {
                Logger.LogError("Erro no RPC_ReportFarmHarvest: " + ex);
            }
        }

        private void ProcessFarmHarvestReport(string playerName, string prefabName, int amount, string source)
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
                    DebugLog(DebugCategory.Points, "Cultivo ignorado para admin: " + playerName);
                    return;
                }

                JackpotRule rule;
                if (_rules.FarmJackpots == null || !_rules.FarmJackpots.TryGetValue(prefabName, out rule) || rule == null || rule.Points <= 0)
                {
                    DebugLog(DebugCategory.Points, "Colheita sem jackpot configurado: prefab=" + prefabName + " amount=" + amount + " player=" + playerName);
                    return;
                }

                RankingEntry entry = GetOrCreateEntry(playerName);
                if (entry == null)
                    return;

                AddProgressAndCheckJackpot(entry, "Farm", prefabName, amount, rule, "Jackpot colheita: " + prefabName, false);
            }
            catch (Exception ex)
            {
                Logger.LogError("Erro ao processar colheita: " + ex);
            }
        }
    }

    [HarmonyPatch]
    internal static class FarmHarvestRankingPatches
    {
        [HarmonyTargetMethods]
        private static IEnumerable<MethodBase> TargetMethods()
        {
            Type pickableType = AccessTools.TypeByName("Pickable");
            if (pickableType == null)
                yield break;

            foreach (MethodInfo method in pickableType.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            {
                if (string.Equals(method.Name, "Interact", StringComparison.OrdinalIgnoreCase))
                    yield return method;
            }
        }

        [HarmonyPostfix]
        private static void Postfix(object __instance, object[] __args, object __result)
        {
            try
            {
                if (GlitnirRankingPlugin.Instance == null || Player.m_localPlayer == null || __instance == null)
                    return;

                if (__result is bool && !(bool)__result)
                    return;

                Player player = ExtractPlayer(__args);
                if (player != null && player != Player.m_localPlayer)
                    return;

                string prefabName;
                int amount;
                if (!TryExtractPickableDrop(__instance, out prefabName, out amount))
                    return;

                GlitnirRankingPlugin.Instance.NotifyLocalFarmHarvest(prefabName, amount);
            }
            catch (Exception ex)
            {
                GlitnirRankingPlugin.Log.LogError("Erro no patch de colheita: " + ex);
            }
        }

        private static Player ExtractPlayer(object[] args)
        {
            if (args == null)
                return null;

            for (int i = 0; i < args.Length; i++)
            {
                Player player = args[i] as Player;
                if (player != null)
                    return player;

                Humanoid humanoid = args[i] as Humanoid;
                if (humanoid is Player)
                    return humanoid as Player;
            }

            return null;
        }

        private static bool TryExtractPickableDrop(object pickable, out string prefabName, out int amount)
        {
            prefabName = "";
            amount = 1;

            try
            {
                Type type = pickable.GetType();

                FieldInfo itemPrefabField = type.GetField("m_itemPrefab", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                GameObject itemPrefab = itemPrefabField != null ? itemPrefabField.GetValue(pickable) as GameObject : null;
                if (itemPrefab == null)
                    return false;

                prefabName = itemPrefab.name.Replace("(Clone)", "").Trim();

                FieldInfo amountField = type.GetField("m_amount", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (amountField != null && amountField.GetValue(pickable) is int)
                    amount = Mathf.Max(1, (int)amountField.GetValue(pickable));

                return !string.IsNullOrWhiteSpace(prefabName);
            }
            catch
            {
                return false;
            }
        }
    }
}
