using UnityEngine;

namespace Glitnir.Ranking
{
    public partial class GlitnirRankingPlugin
    {
        private void SetStatus(string text, float seconds = 2.5f)
        {
            _statusText = string.IsNullOrWhiteSpace(text) ? "" : text;
            _statusUntil = Time.unscaledTime + Mathf.Max(0.1f, seconds);
        }
    }
}
