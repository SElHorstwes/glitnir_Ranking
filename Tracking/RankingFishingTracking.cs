using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace Glitnir.Ranking
{
    public partial class GlitnirRankingPlugin
    {
        public void NotifyLocalFishCaught(object fishInstance, Player player)
        {
            try
            {
                if (Player.m_localPlayer == null || player == null || player != Player.m_localPlayer)
                    return;

                Component component = fishInstance as Component;
                if (component == null)
                    return;

                string prefabName = GetPrefabName(component.gameObject);
                if (!IsFishPrefab(prefabName))
                    return;

                string zdoKey = GetZdoKey(component.gameObject);


                if (string.IsNullOrWhiteSpace(zdoKey))
                {
                    DebugLog(DebugCategory.Points, "Pesca ignorada: peixe sem ZDO real. prefab=" + prefabName);
                    return;
                }

                long serverPeerUid = GetServerPeerUid();
                if (serverPeerUid == 0L || !_rpcsRegistered || ZRoutedRpc.instance == null)
                    return;

                ZPackage pkg = new ZPackage();
                pkg.Write(SafeKey(zdoKey));
                pkg.Write(SafeKey(prefabName));
                pkg.Write(SafeLimit(Player.m_localPlayer.GetPlayerName(), MaxPlayerNameLength));

                ZRoutedRpc.instance.InvokeRoutedRPC(serverPeerUid, RpcReportFishCaught, pkg);

                DebugLog(DebugCategory.Points, "Cliente reportando pesca: player=" + GetLocalPlayerName() + " fish=" + prefabName + " zdo=" + zdoKey);
            }
            catch (Exception ex)
            {
                Logger.LogError("Erro ao reportar pesca local: " + ex);
            }
        }

        public void NotifyLocalFishItemAdded(string fishPrefab, int amount)
        {


            try
            {
                fishPrefab = SafeKey(fishPrefab);
                if (IsFishPrefab(fishPrefab))
                    DebugLog(DebugCategory.Points, "Peixe em inventÃ¡rio ignorado para evitar exploit: fish=" + fishPrefab + " amount=" + amount);
            }
            catch { }
        }

        private bool IsValidFishCatchCreditKey(string zdoKey)
        {
            zdoKey = SafeKey(zdoKey);
            if (string.IsNullOrWhiteSpace(zdoKey))
                return false;


            if (zdoKey.StartsWith("inventory-fish:", StringComparison.OrdinalIgnoreCase))
                return false;


            if (zdoKey.StartsWith("Fish", StringComparison.OrdinalIgnoreCase) && zdoKey.Split(':').Length >= 3)
                return false;

            return true;
        }

        private void RPC_ReportFishCaught(long sender, ZPackage pkg)
        {
            try
            {
                if (!IsServerInstance() || pkg == null)
                    return;

                string zdoKey = SafeKey(pkg.ReadString());
                string fishPrefab = SafeKey(pkg.ReadString());
                string reporterName = SanitizePlayerName(pkg.ReadString());
                string playerName = ResolvePlayerNameFromSender(sender);

                if (string.IsNullOrWhiteSpace(playerName))
                    playerName = reporterName;

                if (ShouldIgnoreSenderForRanking(sender, playerName))
                {
                    DebugLog(DebugCategory.Points, "Pesca ignorada para admin: " + playerName);
                    return;
                }


                if (!IsValidFishCatchCreditKey(zdoKey))
                {
                    DebugLog(DebugCategory.Points, "Pesca rejeitada por chave invÃ¡lida/forjada: player=" + playerName + " fish=" + fishPrefab + " zdo=" + zdoKey);
                    return;
                }

                ProcessFishingCatchReport(playerName, fishPrefab, zdoKey, "rpc-fishing");
            }
            catch (Exception ex)
            {
                Logger.LogError("Erro no RPC_ReportFishCaught: " + ex);
            }
        }

        private void ProcessFishingCatchReport(string playerName, string fishPrefab, string zdoKey, string source)
        {
            try
            {
                if (!IsServerInstance() || _rules == null || !_rules.RankingEnabled || !_rules.EnableFishingPoints)
                    return;

                playerName = SanitizePlayerName(playerName);
                fishPrefab = NormalizeFishPrefabName(fishPrefab);
                zdoKey = SafeKey(zdoKey);

                if (string.IsNullOrWhiteSpace(playerName) || !IsFishPrefab(fishPrefab))
                    return;

                if (ShouldIgnorePlayerForRanking(playerName))
                {
                    DebugLog(DebugCategory.Points, "Pesca ignorada para admin: " + playerName);
                    return;
                }

                int points;
                if (_rules.FishingPoints == null || !_rules.FishingPoints.TryGetValue(fishPrefab, out points))
                    return;

                points = Mathf.Max(0, points);
                if (points <= 0)
                    return;

                if (HasFishingCatchCredit(zdoKey))
                {
                    DebugLog(DebugCategory.Points, "Pesca jÃ¡ creditada: zdo=" + zdoKey + " fish=" + fishPrefab + " source=" + source);
                    return;
                }

                RankingEntry entry = GetOrCreateEntry(playerName);
                if (entry == null)
                    return;

                ApplyPointsToEntry(entry, points, "Pesca: " + fishPrefab);
                entry.TotalFishingPontuadas++;
                entry.FishingPointsTotal = Mathf.Clamp(entry.FishingPointsTotal + points, 0, int.MaxValue);


                IncrementProgressCounter(entry, "Fishing", fishPrefab, 1);
                IncrementProgressCounter(entry, "Pesca", fishPrefab, 1);

                MarkFishingCatchCredit(playerName, fishPrefab, zdoKey);


            }
            catch (Exception ex)
            {
                Logger.LogError("Erro ao processar pontuaÃ§Ã£o de pesca: " + ex);
            }
        }

        private bool HasFishingCatchCredit(string zdoKey)
        {
            return HasFishingCatchCreditCached(zdoKey);
        }

        private void MarkFishingCatchCredit(string playerName, string fishPrefab, string zdoKey)
        {
            if (_database == null)
                _database = new RankingDatabase();

            if (_database.FishingCatchCredits == null)
                _database.FishingCatchCredits = new List<FishingCatchCreditRecord>();

            if (HasFishingCatchCredit(zdoKey))
                return;

            _database.FishingCatchCredits.Add(new FishingCatchCreditRecord
            {
                PlayerName = SanitizePlayerName(playerName),
                FishPrefab = SafeKey(fishPrefab),
                ZdoKey = SafeKey(zdoKey),
                GrantedAtUtc = DateTime.UtcNow.ToString("O")
            });

            if (IsServerInstance())
                SaveDatabase();
        }
    }

    [HarmonyPatch]
    internal static class FishingRankingPatches
    {
        [HarmonyPrepare]
        private static bool Prepare()
        {
            try
            {
                Type floatType = AccessTools.TypeByName("FishingFloat");
                if (floatType == null)
                {
                    GlitnirRankingPlugin.Log.LogWarning("[Ranking] Patch de pesca nÃ£o aplicado: classe FishingFloat nÃ£o encontrada.");
                    return false;
                }

                MethodInfo setCatch = AccessTools.Method(floatType, "SetCatch");

                if (setCatch == null)
                {
                    GlitnirRankingPlugin.Log.LogWarning("[Ranking] Patch de pesca nÃ£o aplicado: FishingFloat.SetCatch nÃ£o encontrado.");
                    return false;
                }

                return true;
            }
            catch
            {
                return false;
            }
        }

        [HarmonyTargetMethods]
        private static IEnumerable<MethodBase> TargetMethods()
        {
            Type floatType = AccessTools.TypeByName("FishingFloat");
            if (floatType == null)
                yield break;

            MethodInfo setCatch = AccessTools.Method(floatType, "SetCatch");
            if (setCatch != null)
                yield return setCatch;
        }

        [HarmonyPostfix]
        private static void Postfix(MethodBase __originalMethod, object __instance, object[] __args)
        {
            try
            {
                if (GlitnirRankingPlugin.Instance == null)
                    return;

                string methodName = __originalMethod != null ? (__originalMethod.Name ?? "") : "";
                GameObject fishObject = TryExtractFishGameObject(__instance, __args);

                if (fishObject == null)
                {


                    return;
                }

                GlitnirRankingPlugin.Instance.NotifyLocalFishCaught(fishObject.transform, Player.m_localPlayer);
            }
            catch (Exception ex)
            {
                GlitnirRankingPlugin.Log.LogError("Erro no patch FishingFloat de pesca: " + ex);
            }
        }

        private static GameObject TryExtractFishGameObject(object instance, object[] args)
        {
            try
            {
                GameObject fromArgs = FindGameObjectRecursive(args, 0);
                if (fromArgs != null)
                    return fromArgs;

                Component component = instance as Component;
                if (component == null)
                    return null;

                GameObject fromFields = FindGameObjectInFields(component, 0);
                if (fromFields != null)
                    return fromFields;

                Transform transform = component.transform;
                if (transform != null)
                {
                    for (int i = 0; i < transform.childCount; i++)
                    {
                        Transform child = transform.GetChild(i);
                        if (child == null)
                            continue;

                        if (IsFishObject(child.gameObject))
                            return child.gameObject;
                    }
                }
            }
            catch { }

            return null;
        }

        private static GameObject FindGameObjectRecursive(object value, int depth)
        {
            if (value == null || depth > 2)
                return null;

            object[] array = value as object[];
            if (array != null)
            {
                for (int i = 0; i < array.Length; i++)
                {
                    GameObject found = FindGameObjectRecursive(array[i], depth + 1);
                    if (found != null)
                        return found;
                }

                return null;
            }

            GameObject go = value as GameObject;
            if (go != null && IsFishObject(go))
                return go;

            Component component = value as Component;
            if (component != null && component.gameObject != null && IsFishObject(component.gameObject))
                return component.gameObject;

            return null;
        }

        private static GameObject FindGameObjectInFields(Component component, int depth)
        {
            if (component == null || depth > 2)
                return null;

            Type type = component.GetType();
            FieldInfo[] fields = type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            for (int i = 0; i < fields.Length; i++)
            {
                FieldInfo field = fields[i];
                if (field == null)
                    continue;

                string fieldName = field.Name ?? "";
                if (fieldName.IndexOf("fish", StringComparison.OrdinalIgnoreCase) < 0 &&
                    fieldName.IndexOf("catch", StringComparison.OrdinalIgnoreCase) < 0)
                    continue;

                object value = null;
                try { value = field.GetValue(component); } catch { }

                GameObject found = FindGameObjectRecursive(value, depth + 1);
                if (found != null)
                    return found;
            }

            return null;
        }

        private static bool IsFishObject(GameObject go)
        {
            if (go == null)
                return false;

            string name = go.name ?? "";
            if (name.StartsWith("Fish", StringComparison.OrdinalIgnoreCase))
                return true;

            try
            {
                ZNetView znv = go.GetComponent<ZNetView>();
                if (znv != null && znv.GetZDO() != null)
                {
                    string prefab = GlitnirRankingPlugin.Instance != null
                        ? GlitnirRankingPlugin.Instance.GetPrefabName(go)
                        : "";
                    return !string.IsNullOrWhiteSpace(prefab) && prefab.StartsWith("Fish", StringComparison.OrdinalIgnoreCase);
                }
            }
            catch { }

            return false;
        }
    }
    [HarmonyPatch]
    internal static class FishingDropBlockPatches
    {
        [HarmonyPrepare]
        private static bool Prepare()
        {
            try
            {
                MethodInfo dropItem = AccessTools.Method(typeof(Player), "DropItem");
                if (dropItem == null)
                {
                    GlitnirRankingPlugin.Log.LogWarning("[Ranking] Bloqueio de drop de peixe nÃ£o aplicado: Player.DropItem nÃ£o encontrado.");
                    return false;
                }

                return true;
            }
            catch
            {
                return false;
            }
        }

        [HarmonyTargetMethods]
        private static IEnumerable<MethodBase> TargetMethods()
        {
            MethodInfo dropItem = AccessTools.Method(typeof(Player), "DropItem");
            if (dropItem != null)
                yield return dropItem;
        }

        [HarmonyPrefix]
        private static bool Prefix(object[] __args)
        {
            try
            {
                ItemDrop.ItemData item = TryExtractItemData(__args);
                if (!IsFishItem(item))
                    return true;

                ShowBlockedDropMessage();
                GlitnirRankingPlugin.Log.LogInfo("[Ranking] Drop de peixe bloqueado para evitar farm infinito de pontos.");
                return false;
            }
            catch (Exception ex)
            {
                GlitnirRankingPlugin.Log.LogWarning("[Ranking] Erro ao validar bloqueio de drop de peixe: " + ex.Message);
                return true;
            }
        }

        private static ItemDrop.ItemData TryExtractItemData(object[] args)
        {
            if (args == null)
                return null;

            for (int i = 0; i < args.Length; i++)
            {
                ItemDrop.ItemData item = args[i] as ItemDrop.ItemData;
                if (item != null)
                    return item;
            }

            return null;
        }

        private static bool IsFishItem(ItemDrop.ItemData item)
        {
            if (item == null)
                return false;

            try
            {
                GameObject dropPrefab = item.m_dropPrefab;
                if (dropPrefab != null)
                {
                    string prefabName = GetCleanName(dropPrefab.name);
                    if (prefabName.StartsWith("Fish", StringComparison.OrdinalIgnoreCase))
                        return true;
                }
            }
            catch { }

            try
            {
                if (item.m_shared != null)
                {
                    string sharedName = item.m_shared.m_name ?? "";
                    string sharedNameLower = sharedName.ToLowerInvariant();

                    if (sharedNameLower.Contains("fish"))
                        return true;


                    if (sharedNameLower.Contains("$item_fish"))
                        return true;
                }
            }
            catch { }


            return false;
        }

        private static string GetCleanName(string rawName)
        {
            if (string.IsNullOrWhiteSpace(rawName))
                return "";

            string name = rawName;
            int idx = name.IndexOf('(');
            if (idx > 0)
                name = name.Substring(0, idx);

            return name.Trim();
        }

        private static void ShowBlockedDropMessage()
        {
            try
            {
                if (MessageHud.instance != null)
                    MessageHud.instance.ShowMessage(MessageHud.MessageType.Center, "Necromancia aquÃ¡tica detectada. Sjar estÃ¡ decepcionado.");
            }
            catch { }
        }
    }


}
