using BepInEx.Configuration;
using ServerSync;
using System.Collections.Generic;

namespace Glitnir.Ranking
{
    internal static class GlitnirRankingServerSync
    {
        private static readonly HashSet<string> RegisteredConfigEntries = new HashSet<string>();
        private static bool _lockingConfigRegistered;

        internal static readonly ConfigSync ConfigSync = new ConfigSync(GlitnirRankingPlugin.ModGuid)
        {
            DisplayName = GlitnirRankingPlugin.ModName,
            CurrentVersion = GlitnirRankingPlugin.ModVersion,
            MinimumRequiredVersion = GlitnirRankingPlugin.ModVersion
        };

        internal static ConfigEntry<T> BindConfig<T>(
            this ConfigFile config,
            string group,
            string name,
            T value,
            string description,
            bool synced = true)
        {
            ConfigEntry<T> entry = config.Bind(group, name, value, description);

            if (synced && RegisteredConfigEntries.Add(group + "\u001f" + name))
            {
                ConfigSync.AddConfigEntry(entry);
            }

            return entry;
        }

        internal static ConfigEntry<bool> BindLockingConfig(this ConfigFile config)
        {
            ConfigEntry<bool> entry = config.Bind(
                "General",
                "LockConfiguration",
                true,
                "Se ativo, o servidor sincroniza e força as configurações do Glitnir Ranking nos clientes.");

            if (!_lockingConfigRegistered)
            {
                ConfigSync.AddLockingConfigEntry(entry);
                _lockingConfigRegistered = true;
            }

            return entry;
        }
    }
}
