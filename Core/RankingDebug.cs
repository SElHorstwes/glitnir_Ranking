namespace Glitnir.Ranking
{
    public partial class GlitnirRankingPlugin
    {
        private enum DebugCategory
        {
            General,
            Hit,
            Kill,
            Skill,
            PendingKill,
            Snapshot,
            Points
        }

        private void DebugLog(DebugCategory category, string message)
        {
            if (_rules == null || !_rules.DebugLogging)
            {
                return;
            }

            bool allowed = false;

            switch (category)
            {
                case DebugCategory.Hit:
                    allowed = _rules.LogHitReports;
                    break;

                case DebugCategory.Kill:
                    allowed = _rules.LogKillReports;
                    break;

                case DebugCategory.Skill:
                    allowed = _rules.LogSkillReports;
                    break;

                case DebugCategory.PendingKill:
                    allowed = _rules.LogPendingKillReports;
                    break;

                case DebugCategory.Snapshot:
                    allowed = _rules.LogSnapshotRequests;
                    break;

                case DebugCategory.Points:
                    allowed = _rules.LogPointsChanges;
                    break;

                default:
                    allowed = true;
                    break;
            }

            if (!allowed)
            {
                return;
            }

            Log?.LogInfo($"[{category}] {message}");
        }
    }
}