using BepInEx;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEngine;

namespace Glitnir.Ranking
{
    public partial class GlitnirRankingPlugin
    {
        private string GetAttackerKeyFromHit(HitData hit)
        {
            if (hit == null)
                return "";

            try
            {
                ZDOID zdo = hit.m_attacker;
                if (!zdo.IsNone())
                    return zdo.UserID + ":" + zdo.ID;
            }
            catch { }

            try
            {
                Character attacker = hit.GetAttacker();
                if (attacker != null)
                    return GetZdoKey(attacker);
            }
            catch { }

            return "";
        }

        private void CleanupOldLocalRecentHits()
        {
            List<string> toRemove = new List<string>();
            foreach (KeyValuePair<string, float> kvp in _localRecentHits)
            {
                if (Time.time - kvp.Value > LocalRecentHitLifetimeSeconds)
                    toRemove.Add(kvp.Key);
            }

            foreach (string key in toRemove)
                _localRecentHits.Remove(key);
        }

        private bool HasRecentLocalHit(string zdoKey)
        {
            if (string.IsNullOrWhiteSpace(zdoKey))
                return false;

            float hitTime;
            if (!_localRecentHits.TryGetValue(zdoKey, out hitTime))
                return false;

            if (Time.time - hitTime > LocalRecentHitLifetimeSeconds)
            {
                _localRecentHits.Remove(zdoKey);
                return false;
            }

            return true;
        }

        private void SendKillReportToServer(string zdoKey, string prefabName, string attackerKey, string sourceTag, bool victimTamed)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(zdoKey) || string.IsNullOrWhiteSpace(prefabName))
                    return;

                if (string.IsNullOrWhiteSpace(attackerKey))
                    attackerKey = GetLocalPlayerKey();

                if (string.IsNullOrWhiteSpace(attackerKey))
                    return;

                long serverPeerUid = GetServerPeerUid();
                if (serverPeerUid == 0L || !_rpcsRegistered || ZRoutedRpc.instance == null)
                    return;

                ZPackage killPkg = new ZPackage();
                killPkg.Write(zdoKey);
                killPkg.Write(prefabName);
                killPkg.Write(attackerKey);
                killPkg.Write(victimTamed);
                ZRoutedRpc.instance.InvokeRoutedRPC(serverPeerUid, RpcReportKill, killPkg);

                DebugLog(DebugCategory.Kill, "Cliente reportando KILL(" + sourceTag + "): player=" + GetLocalPlayerName() + " attackerKey=" + attackerKey + " zdo=" + zdoKey + " prefab=" + prefabName + " tamed=" + victimTamed);
            }
            catch (Exception ex)
            {
                Logger.LogError("Erro ao enviar KILL local do ranking: " + ex);
            }
        }

        public void NotifyLocalDamage(Character victim, HitData hit, bool postDamage)
        {
            try
            {
                if (victim == null || hit == null)
                    return;

                if (victim is Player)
                    return;

                if (_rules.IgnoreTamedKills && IsTamedCharacter(victim))
                    return;

                if (Player.m_localPlayer == null)
                    return;

                if (!IsLocalPlayerAttackAuthor(hit))
                    return;

                string zdoKey = GetZdoKey(victim);
                if (string.IsNullOrWhiteSpace(zdoKey))
                    return;

                string prefabName = GetPrefabName(victim.gameObject);
                if (string.IsNullOrWhiteSpace(prefabName))
                    return;

                string attackerKey = GetAttackerKeyFromHit(hit);
                if (string.IsNullOrWhiteSpace(attackerKey))
                    attackerKey = GetLocalPlayerKey();
                if (string.IsNullOrWhiteSpace(attackerKey))
                    return;

                _localRecentHits[zdoKey] = Time.time;

                if (!postDamage)
                    return;

                bool isDead = false;
                float hp = 1f;
                try { hp = victim.GetHealth(); } catch { }
                try { isDead = victim.IsDead(); } catch { }

                if (!isDead && hp > 0f)
                    return;

                if (!HasRecentLocalHit(zdoKey))
                    return;

                SendKillReportToServer(zdoKey, prefabName, attackerKey, "Damage", IsTamedCharacter(victim));
                _localRecentHits.Remove(zdoKey);
            }
            catch (Exception ex)
            {
                Logger.LogError("Erro ao reportar dano local do ranking: " + ex);
            }
        }

        public void NotifyLocalCharacterDeath(Character victim)
        {
            try
            {
                if (victim == null || victim is Player)
                    return;

                if (_rules.IgnoreTamedKills && IsTamedCharacter(victim))
                    return;

                if (Player.m_localPlayer == null)
                    return;

                string zdoKey = GetZdoKey(victim);
                if (string.IsNullOrWhiteSpace(zdoKey))
                    return;

                if (!HasRecentLocalHit(zdoKey))
                    return;

                string prefabName = GetPrefabName(victim.gameObject);
                if (string.IsNullOrWhiteSpace(prefabName))
                    return;

                string attackerKey = GetLocalPlayerKey();
                if (string.IsNullOrWhiteSpace(attackerKey))
                    return;

                SendKillReportToServer(zdoKey, prefabName, attackerKey, "OnDeath", IsTamedCharacter(victim));
                _localRecentHits.Remove(zdoKey);
            }
            catch (Exception ex)
            {
                Logger.LogError("Erro ao reportar morte local do ranking: " + ex);
            }
        }

        private bool DoesSenderMatchAttackerKey(long sender, string attackerKey)
        {
            if (sender == 0L || string.IsNullOrWhiteSpace(attackerKey))
                return false;

            int sep = attackerKey.IndexOf(':');
            string rawUserId = sep >= 0 ? attackerKey.Substring(0, sep) : attackerKey;

            long attackerUserId;
            if (!long.TryParse(rawUserId, out attackerUserId))
                return false;

            return attackerUserId == sender;
        }

        private void RPC_ReportKill(long sender, ZPackage pkg)
        {
            try
            {
                if (!IsServerInstance() || pkg == null)
                    return;

                string zdoKey = SafeKey(pkg.ReadString());
                string prefabName = SafeKey(pkg.ReadString());
                string attackerKey = SafeKey(pkg.ReadString());
                bool clientSaysTamed = false;
                try { clientSaysTamed = pkg.ReadBool(); } catch { }
                string reporterName = ResolvePlayerNameFromSender(sender);

                DebugLog(DebugCategory.Kill, "Servidor recebeu KILL: sender=" + sender + " reporter=" + reporterName + " attackerKey=" + attackerKey + " zdo=" + zdoKey + " prefab=" + prefabName);

                if (string.IsNullOrWhiteSpace(zdoKey) || string.IsNullOrWhiteSpace(prefabName) || string.IsNullOrWhiteSpace(reporterName))
                    return;

                if (ShouldIgnoreSenderForRanking(sender, reporterName))
                {
                    DebugLog(DebugCategory.Kill, "KILL ignorado para admin: sender=" + sender + " reporter=" + reporterName);
                    return;
                }

                if (!DoesSenderMatchAttackerKey(sender, attackerKey))
                {
                    DebugLog(DebugCategory.Kill, "KILL rejeitado por mismatch: sender=" + sender + " reporter=" + reporterName + " attackerKey=" + attackerKey);
                    return;
                }

                if (_rules.IgnoreTamedKills && clientSaysTamed)
                {
                    MarkTamedKillIgnored(zdoKey, "rpc-kill-client-flag");
                    return;
                }

                if (ShouldIgnoreTamedKill(zdoKey, "rpc-kill"))
                    return;

                List<string> creditedPlayers = GetCreditedPlayerNames(zdoKey);
                if (creditedPlayers.Count == 0)
                    creditedPlayers.Add(reporterName);

                ProcessKillReportMulti(creditedPlayers, zdoKey, prefabName, "rpc-kill");
            }
            catch (Exception ex)
            {
                Logger.LogError("Erro no RPC_ReportKill: " + ex);
            }
        }

        private string ResolvePlayerNameFromSender(long sender)
        {
            try
            {
                if (ZNet.instance != null)
                {
                    ZNetPeer peer = ZNet.instance.GetPeer(sender);
                    if (peer != null && !string.IsNullOrWhiteSpace(peer.m_playerName))
                        return SanitizePlayerName(peer.m_playerName);

                    List<ZNet.PlayerInfo> playerList = ZNet.instance.GetPlayerList();
                    if (playerList != null)
                    {
                        foreach (ZNet.PlayerInfo info in playerList)
                        {
                            try
                            {
                                long userId = Convert.ToInt64(info.m_characterID.UserID);
                                if (userId == sender && !string.IsNullOrWhiteSpace(info.m_name))
                                    return SanitizePlayerName(info.m_name);
                            }
                            catch { }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogWarning("[Ranking] Falha ao resolver player por sender=" + sender + ": " + ex.Message);
            }

            return "";
        }

        private bool ShouldIgnoreSenderForRanking(long sender, string playerName)
        {
            try
            {
                if (_cfgIgnoreAdminsInRanking == null || !_cfgIgnoreAdminsInRanking.Value)
                    return false;

                if (ShouldIgnorePlayerForRanking(playerName))
                    return true;

                if (sender != 0 && IsSenderDetectedInAdminList(sender))
                    return true;
            }
            catch { }

            return false;
        }

        private bool ShouldIgnorePlayerForRanking(string playerName)
        {
            if (_cfgIgnoreAdminsInRanking == null || !_cfgIgnoreAdminsInRanking.Value)
                return false;

            string safeName = SanitizePlayerName(playerName);
            if (string.IsNullOrWhiteSpace(safeName))
                return false;

            if (IsNameOrIdInIgnoredAdminConfig(safeName))
                return true;

            HashSet<string> admins = GetAllKnownAdminIdentifiers();
            if (admins.Contains(safeName))
                return true;

            return false;
        }

        private bool IsNameOrIdInIgnoredAdminConfig(string value)
        {
            try
            {
                if (_cfgIgnoredAdminNames == null || string.IsNullOrWhiteSpace(_cfgIgnoredAdminNames.Value))
                    return false;

                string safeValue = SanitizePlayerName(value);
                string[] parts = _cfgIgnoredAdminNames.Value.Split(new char[] { ',', ';', '\n', '\r', '|' }, StringSplitOptions.RemoveEmptyEntries);

                for (int i = 0; i < parts.Length; i++)
                {
                    string item = SanitizePlayerName(parts[i]);
                    if (!string.IsNullOrWhiteSpace(item) && string.Equals(item, safeValue, StringComparison.OrdinalIgnoreCase))
                        return true;
                }
            }
            catch { }

            return false;
        }

        private bool IsSenderDetectedInAdminList(long sender)
        {
            try
            {
                HashSet<string> admins = GetAllKnownAdminIdentifiers();
                if (admins == null || admins.Count == 0)
                    return false;

                string senderText = sender.ToString();
                if (admins.Contains(senderText))
                    return true;

                if (ZNet.instance != null)
                {
                    ZNetPeer peer = ZNet.instance.GetPeer(sender);
                    HashSet<string> peerIds = GetPossiblePeerIdentifiers(peer);
                    foreach (string id in peerIds)
                    {
                        if (!string.IsNullOrWhiteSpace(id) && admins.Contains(id.Trim()))
                            return true;
                    }
                }
            }
            catch { }

            return false;
        }

        private HashSet<string> GetAllKnownAdminIdentifiers()
        {
            if (_adminIdentifiersCacheReady && Time.realtimeSinceStartup < _nextAdminIdentifierRefreshAt)
                return new HashSet<string>(_cachedAdminIdentifiers, StringComparer.OrdinalIgnoreCase);

            HashSet<string> result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            try
            {
                if (ZNet.instance != null)
                {
                    HashSet<string> znetAdmins = GetAdminIdentifiersFromZNet(ZNet.instance);
                    foreach (string id in znetAdmins)
                    {
                        if (!string.IsNullOrWhiteSpace(id))
                            result.Add(id.Trim());
                    }
                }
            }
            catch { }

            try
            {
                foreach (string id in ReadAdminIdentifiersFromKnownFiles())
                {
                    if (!string.IsNullOrWhiteSpace(id))
                        result.Add(id.Trim());
                }
            }
            catch { }

            _cachedAdminIdentifiers = new HashSet<string>(result, StringComparer.OrdinalIgnoreCase);
            _nextAdminIdentifierRefreshAt = Time.realtimeSinceStartup + AdminIdentifierRefreshIntervalSeconds;
            _adminIdentifiersCacheReady = true;

            return result;
        }

        private HashSet<string> ReadAdminIdentifiersFromKnownFiles()
        {
            HashSet<string> result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            string[] candidates = new string[]
            {
                Path.Combine(Paths.ConfigPath, "adminlist.txt"),
                Path.Combine(Paths.ConfigPath, "adminlist"),
                Path.Combine(Application.persistentDataPath, "adminlist.txt"),
                Path.Combine(Application.persistentDataPath, "adminlist"),
                Path.Combine(Directory.GetCurrentDirectory(), "adminlist.txt"),
                Path.Combine(Directory.GetCurrentDirectory(), "adminlist")
            };

            for (int i = 0; i < candidates.Length; i++)
            {
                string path = candidates[i];
                try
                {
                    if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                        continue;

                    foreach (string raw in File.ReadAllLines(path, Encoding.UTF8))
                    {
                        string line = raw != null ? raw.Trim() : "";
                        if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#") || line.StartsWith("//"))
                            continue;

                        int comment = line.IndexOf('#');
                        if (comment >= 0)
                            line = line.Substring(0, comment).Trim();

                        if (!string.IsNullOrWhiteSpace(line))
                            result.Add(line);
                    }
                }
                catch { }
            }

            return result;
        }

        private HashSet<string> GetAdminIdentifiersFromZNet(object znet)
        {
            HashSet<string> result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            try
            {
                if (znet == null)
                    return result;

                Type type = znet.GetType();
                BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

                FieldInfo[] fields = type.GetFields(flags);
                for (int i = 0; i < fields.Length; i++)
                {
                    FieldInfo field = fields[i];
                    if (field == null || string.IsNullOrWhiteSpace(field.Name))
                        continue;

                    if (field.Name.IndexOf("admin", StringComparison.OrdinalIgnoreCase) < 0)
                        continue;

                    object value = field.GetValue(znet);
                    AddStringsFromObject(value, result);
                }

                PropertyInfo[] props = type.GetProperties(flags);
                for (int i = 0; i < props.Length; i++)
                {
                    PropertyInfo prop = props[i];
                    if (prop == null || string.IsNullOrWhiteSpace(prop.Name) || prop.GetIndexParameters().Length > 0)
                        continue;

                    if (prop.Name.IndexOf("admin", StringComparison.OrdinalIgnoreCase) < 0)
                        continue;

                    try
                    {
                        object value = prop.GetValue(znet, null);
                        AddStringsFromObject(value, result);
                    }
                    catch { }
                }
            }
            catch { }

            return result;
        }

        private void AddStringsFromObject(object value, HashSet<string> output)
        {
            if (value == null || output == null)
                return;

            try
            {
                string direct = value as string;
                if (!string.IsNullOrWhiteSpace(direct))
                {
                    output.Add(direct.Trim());
                    return;
                }

                System.Collections.IEnumerable enumerable = value as System.Collections.IEnumerable;
                if (enumerable != null)
                {
                    foreach (object item in enumerable)
                    {
                        if (item == null)
                            continue;

                        string itemText = item as string;
                        if (!string.IsNullOrWhiteSpace(itemText))
                        {
                            output.Add(itemText.Trim());
                            continue;
                        }

                        string text = item.ToString();
                        if (!string.IsNullOrWhiteSpace(text))
                            output.Add(text.Trim());
                    }
                }
            }
            catch { }
        }

        private HashSet<string> GetPossiblePeerIdentifiers(object peer)
        {
            HashSet<string> result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            try
            {
                if (peer == null)
                    return result;

                Type type = peer.GetType();
                BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

                string[] preferredNames = new string[]
                {
                    "m_uid", "uid", "UID", "m_userId", "userId", "UserId",
                    "m_userID", "UserID", "m_steamID", "steamID", "SteamID",
                    "m_hostName", "hostName", "HostName", "m_socketHost", "socketHost"
                };

                for (int i = 0; i < preferredNames.Length; i++)
                {
                    string value = GetStringMemberByNames(peer, preferredNames[i]);
                    if (!string.IsNullOrWhiteSpace(value))
                        result.Add(value.Trim());
                }

                FieldInfo[] fields = type.GetFields(flags);
                for (int i = 0; i < fields.Length; i++)
                {
                    FieldInfo field = fields[i];
                    if (field == null || string.IsNullOrWhiteSpace(field.Name))
                        continue;

                    string n = field.Name;
                    if (n.IndexOf("uid", StringComparison.OrdinalIgnoreCase) < 0 &&
                        n.IndexOf("id", StringComparison.OrdinalIgnoreCase) < 0 &&
                        n.IndexOf("host", StringComparison.OrdinalIgnoreCase) < 0 &&
                        n.IndexOf("steam", StringComparison.OrdinalIgnoreCase) < 0 &&
                        n.IndexOf("playfab", StringComparison.OrdinalIgnoreCase) < 0)
                        continue;

                    object value = field.GetValue(peer);
                    if (value != null && !string.IsNullOrWhiteSpace(value.ToString()))
                        result.Add(value.ToString().Trim());
                }
            }
            catch { }

            return result;
        }

        private string GetStringMemberByNames(object obj, params string[] names)
        {
            if (obj == null || names == null)
                return null;

            try
            {
                Type type = obj.GetType();
                BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

                for (int i = 0; i < names.Length; i++)
                {
                    FieldInfo field = type.GetField(names[i], flags);
                    if (field != null)
                    {
                        object value = field.GetValue(obj);
                        if (value != null)
                            return value.ToString();
                    }

                    PropertyInfo prop = type.GetProperty(names[i], flags);
                    if (prop != null && prop.GetIndexParameters().Length == 0)
                    {
                        object value = prop.GetValue(obj, null);
                        if (value != null)
                            return value.ToString();
                    }
                }
            }
            catch { }

            return null;
        }

        private bool IsTamedCharacter(Character character)
        {
            try
            {
                if (character == null)
                    return false;

                try
                {
                    if (character.IsTamed())
                        return true;
                }
                catch { }

                try
                {
                    ZNetView znv = character.GetComponent<ZNetView>();
                    if (znv != null && znv.GetZDO() != null && znv.GetZDO().GetBool("tamed", false))
                        return true;
                }
                catch { }

                try
                {
                    Tameable tameable = character.GetComponent<Tameable>();
                    if (tameable != null)
                    {
                        ZNetView znv = tameable.GetComponent<ZNetView>();
                        if (znv != null && znv.GetZDO() != null && znv.GetZDO().GetBool("tamed", false))
                            return true;
                    }
                }
                catch { }

                return false;
            }
            catch
            {
                return false;
            }
        }

        private void MarkTamedKillIgnored(string zdoKey, string source)
        {
            zdoKey = SafeKey(zdoKey);
            if (string.IsNullOrWhiteSpace(zdoKey))
                return;

            _ignoredTamedKills[zdoKey] = Time.time;
            _processedKills[zdoKey] = Time.time;
            RemoveDamageCredit(zdoKey);
            DebugLog(DebugCategory.Kill, "Kill ignorado por criatura domada: source=" + source + " zdo=" + zdoKey);
        }

        private bool ShouldIgnoreTamedKill(string zdoKey, string source)
        {
            if (_rules == null || !_rules.IgnoreTamedKills)
                return false;

            zdoKey = SafeKey(zdoKey);
            if (string.IsNullOrWhiteSpace(zdoKey))
                return false;

            if (_ignoredTamedKills.ContainsKey(zdoKey))
                return true;

            return false;
        }

        private bool TryNormalizeBossPrefabName(string rawName, out string prefabName)
        {
            prefabName = "";
            string key = SafeKey(rawName);
            if (string.IsNullOrWhiteSpace(key))
                return false;

            if (BossPortugueseToPrefab.TryGetValue(key, out prefabName))
                return true;

            if (BossDisplayNamesPtBr.ContainsKey(key))
            {
                prefabName = key;
                return true;
            }

            return false;
        }

        private string NormalizeBossPrefabName(string rawName)
        {
            string prefabName;
            return TryNormalizeBossPrefabName(rawName, out prefabName) ? prefabName : SafeKey(rawName);
        }

        private bool TryNormalizeFishPrefabName(string rawName, out string prefabName)
        {
            prefabName = "";
            string key = SafeKey(rawName);
            if (string.IsNullOrWhiteSpace(key))
                return false;

            if (FishPortugueseToPrefab.TryGetValue(key, out prefabName))
                return true;

            if (FishDisplayNamesPtBr.ContainsKey(key))
            {
                prefabName = key;
                return true;
            }

            if (key.StartsWith("Fish", StringComparison.OrdinalIgnoreCase))
            {
                prefabName = key;
                return true;
            }

            return false;
        }

        private string NormalizeFishPrefabName(string rawName)
        {
            string prefabName;
            return TryNormalizeFishPrefabName(rawName, out prefabName) ? prefabName : SafeKey(rawName);
        }

        private bool IsFishPrefab(string prefabName)
        {
            string normalized;
            return TryNormalizeFishPrefabName(prefabName, out normalized);
        }

        public void NotifyServerCharacterDeath(Character victim)
        {
            try
            {
                if (!IsServerInstance() || victim == null || victim is Player)
                    return;

                string zdoKey = GetZdoKey(victim);
                string prefabName = GetPrefabName(victim.gameObject);
                List<string> creditedPlayers = GetCreditedPlayerNames(zdoKey);

                DebugLog(DebugCategory.Kill, "OnDeath servidor: zdo=" + zdoKey + " prefab=" + prefabName + " credited=" + string.Join(",", creditedPlayers.ToArray()));

                if (string.IsNullOrWhiteSpace(zdoKey) || string.IsNullOrWhiteSpace(prefabName) || creditedPlayers.Count == 0)
                    return;

                if (IsFishPrefab(prefabName))
                {
                    DebugLog(DebugCategory.Kill, "Kill de peixe ignorado: source=ondeath zdo=" + zdoKey + " prefab=" + prefabName);
                    return;
                }

                if (_rules.IgnoreTamedKills && IsTamedCharacter(victim))
                {
                    MarkTamedKillIgnored(zdoKey, "ondeath");
                    return;
                }

                ProcessKillReportMulti(creditedPlayers, zdoKey, prefabName, "ondeath");
            }
            catch (Exception ex)
            {
                Logger.LogError("Erro ao processar OnDeath do ranking: " + ex);
            }
        }

        private bool IsConfiguredCombatPointPrefab(string prefabName)
        {
            prefabName = SafeKey(prefabName);
            if (string.IsNullOrWhiteSpace(prefabName) || _rules == null)
                return false;

            string bossPrefabName = NormalizeBossPrefabName(prefabName);
            if (_rules.EnableBossPoints && _rules.BossPoints != null && _rules.BossPoints.ContainsKey(bossPrefabName))
                return true;

            if (_rules.EnableKillPoints && _rules.KillPoints != null && _rules.KillPoints.ContainsKey(prefabName))
                return true;

            return false;
        }

        private void ProcessKillReportMulti(List<string> playerNames, string zdoKey, string prefabName, string source)
        {
            try
            {
                if (!IsServerInstance() || !_rules.RankingEnabled)
                    return;

                zdoKey = SafeKey(zdoKey);
                prefabName = SafeKey(prefabName);

                if (string.IsNullOrWhiteSpace(zdoKey) || string.IsNullOrWhiteSpace(prefabName))
                    return;
                if (playerNames == null || playerNames.Count == 0)
                    return;

                if (IsFishPrefab(prefabName))
                {
                    DebugLog(DebugCategory.Kill, "Kill de peixe ignorado: source=" + source + " zdo=" + zdoKey + " prefab=" + prefabName);
                    RemoveDamageCredit(zdoKey);
                    return;
                }

                if (!IsConfiguredCombatPointPrefab(prefabName))
                {
                    DebugLog(DebugCategory.Kill, "Kill ignorado porque o prefab nao esta configurado em KillPoints/BossPoints: source=" + source + " zdo=" + zdoKey + " prefab=" + prefabName);
                    RemoveDamageCredit(zdoKey);
                    return;
                }

                CleanupOldProcessedKills();

                if (ShouldIgnoreTamedKill(zdoKey, source))
                    return;

                if (_processedKills.ContainsKey(zdoKey))
                {
                    DebugLog(DebugCategory.Kill, "Kill duplicado ignorado: source=" + source + " zdo=" + zdoKey + " prefab=" + prefabName);
                    return;
                }

                _processedKills[zdoKey] = Time.time;

                foreach (string rawName in playerNames.Distinct(StringComparer.OrdinalIgnoreCase))
                {
                    string playerName = SanitizePlayerName(rawName);
                    if (string.IsNullOrWhiteSpace(playerName))
                        continue;

                    RankingEntry entry = GetOrCreateEntry(playerName);
                    int points;
                    string reason;

                    string bossPrefabName = NormalizeBossPrefabName(prefabName);
                    if (_rules.EnableBossPoints && _rules.BossPoints.TryGetValue(bossPrefabName, out points))
                    {
                        bool alreadyCreditedBoss = entry.BossCredits != null && entry.BossCredits.Contains(bossPrefabName);


                        entry.TotalBossesPontuadas += 1;
                        IncrementProgressCounter(entry, "Bosses", bossPrefabName, 1);

                        if (!_rules.AllowRepeatedBossPoints && alreadyCreditedBoss)
                        {
                            DebugLog(DebugCategory.Kill, "Boss repetido sem pontos, progresso contado: player=" + playerName + " boss=" + prefabName);
                            SaveRankingEntry(entry);
                            continue;
                        }

                        entry.BossPointsTotal = Mathf.Clamp(entry.BossPointsTotal + points, -int.MaxValue, int.MaxValue);

                        if (entry.BossCredits == null)
                            entry.BossCredits = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                        entry.BossCredits.Add(bossPrefabName);

                        reason = "Boss: " + prefabName;
                        ApplyPointsToEntry(entry, points, reason);
                        continue;
                    }

                    if (_rules.EnableKillPoints && _rules.KillPoints.TryGetValue(prefabName, out points))
                    {
                        entry.TotalKillsPontuadas += 1;
                        entry.KillPointsTotal = Mathf.Clamp(entry.KillPointsTotal + points, -int.MaxValue, int.MaxValue);


                        IncrementProgressCounter(entry, "Kill", prefabName, 1);
                        IncrementProgressCounterAlias(entry, "Combat", prefabName, 1);
                        IncrementProgressCounterAlias(entry, "Combate", prefabName, 1);

                        reason = "Kill: " + prefabName;
                        ApplyPointsToEntry(entry, points, reason);
                        continue;
                    }

                    DebugLog(DebugCategory.Kill, "Kill sem regra de pontos: player=" + playerName + " prefab=" + prefabName);
                }

                RemoveDamageCredit(zdoKey);
            }
            catch (Exception ex)
            {
                Logger.LogError("Erro ao processar kill múltiplo reportado: " + ex);
            }
        }

        private void RecordDamageCredit(string zdoKey, string playerName)
        {
            if (!IsServerInstance())
                return;

            zdoKey = SafeKey(zdoKey);
            playerName = SanitizePlayerName(playerName);

            if (string.IsNullOrWhiteSpace(zdoKey) || string.IsNullOrWhiteSpace(playerName))
                return;

            if (ShouldIgnorePlayerForRanking(playerName))
            {
                DebugLog(DebugCategory.Hit, "Crédito de dano ignorado para admin: " + playerName);
                return;
            }

            Dictionary<string, float> credits;
            if (!_damageCredits.TryGetValue(zdoKey, out credits) || credits == null)
            {
                credits = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
                _damageCredits[zdoKey] = credits;
            }

            credits[playerName] = Time.time;
        }

        private List<string> GetCreditedPlayerNames(string zdoKey)
        {
            List<string> result = new List<string>();

            if (string.IsNullOrWhiteSpace(zdoKey))
                return result;

            Dictionary<string, float> credits;
            if (!_damageCredits.TryGetValue(zdoKey, out credits) || credits == null)
                return result;

            foreach (KeyValuePair<string, float> kvp in credits)
            {
                if (Time.time - kvp.Value <= DamageCreditLifetimeSeconds)
                {
                    string creditedName = SanitizePlayerName(kvp.Key);
                    if (!ShouldIgnorePlayerForRanking(creditedName))
                        result.Add(creditedName);
                }
            }

            return result.Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private void CleanupOldDamageCredits()
        {
            List<string> emptyTargets = new List<string>();

            foreach (KeyValuePair<string, Dictionary<string, float>> target in _damageCredits)
            {
                Dictionary<string, float> credits = target.Value;
                if (credits == null)
                {
                    emptyTargets.Add(target.Key);
                    continue;
                }

                List<string> removePlayers = new List<string>();

                foreach (KeyValuePair<string, float> playerCredit in credits)
                {
                    if (Time.time - playerCredit.Value > DamageCreditLifetimeSeconds)
                        removePlayers.Add(playerCredit.Key);
                }

                foreach (string player in removePlayers)
                    credits.Remove(player);

                if (credits.Count == 0)
                    emptyTargets.Add(target.Key);
            }

            foreach (string key in emptyTargets)
                _damageCredits.Remove(key);
        }

        private void CleanupOldProcessedKills()
        {
            if (Time.time < _nextProcessedKillCleanupAt)
                return;

            _nextProcessedKillCleanupAt = Time.time + ProcessedKillCleanupIntervalSeconds;

            List<string> toRemove = new List<string>();
            foreach (KeyValuePair<string, float> kvp in _processedKills)
            {
                if (Time.time - kvp.Value > ProcessedKillLifetimeSeconds)
                    toRemove.Add(kvp.Key);
            }

            foreach (string key in toRemove)
                _processedKills.Remove(key);

            toRemove.Clear();
            foreach (KeyValuePair<string, float> kvp in _ignoredTamedKills)
            {
                if (Time.time - kvp.Value > ProcessedKillLifetimeSeconds)
                    toRemove.Add(kvp.Key);
            }

            foreach (string key in toRemove)
                _ignoredTamedKills.Remove(key);
        }

        private void RemoveDamageCredit(string zdoKey)
        {
            if (string.IsNullOrWhiteSpace(zdoKey))
                return;

            _damageCredits.Remove(zdoKey);
        }
    }
}
