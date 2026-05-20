using BepInEx.Configuration;
using ServerSync;

namespace Glitnir.Ranking
{
    internal static class GlitnirRankingServerSync
    {
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

            if (synced)
            {
                ConfigSync.AddConfigEntry(entry);
            }

            return entry;
        }
    }
}
