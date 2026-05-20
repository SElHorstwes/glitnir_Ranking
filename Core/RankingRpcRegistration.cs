namespace Glitnir.Ranking
{
    public partial class GlitnirRankingPlugin
    {
        private void RegisterRpcsIfNeeded()
        {
            if (_rpcsRegistered || ZRoutedRpc.instance == null)
                return;

            ZRoutedRpc.instance.Register<ZPackage>(RpcRequestSnapshot, RPC_RequestSnapshot);
            ZRoutedRpc.instance.Register<ZPackage>(RpcReceiveSnapshot, RPC_ReceiveSnapshot);
            ZRoutedRpc.instance.Register<ZPackage>(RpcReportHit, RPC_ReportHit);
            ZRoutedRpc.instance.Register<ZPackage>(RpcReportKill, RPC_ReportKill);
            ZRoutedRpc.instance.Register<ZPackage>(RpcReportSkillGain, RPC_ReportSkillGain);
            ZRoutedRpc.instance.Register<ZPackage>(RpcRequestRewardClaim, RPC_RequestRewardClaim);
            ZRoutedRpc.instance.Register<ZPackage>(RpcGrantRewardItem, RPC_GrantRewardItem);
            ZRoutedRpc.instance.Register<ZPackage>(RpcFinalizeRewardClaim, RPC_FinalizeRewardClaim);
            ZRoutedRpc.instance.Register<ZPackage>(RpcRewardClaimFeedback, RPC_RewardClaimFeedback);
            ZRoutedRpc.instance.Register<ZPackage>(RpcRequestPointsExchange, RPC_RequestPointsExchange);
            ZRoutedRpc.instance.Register<ZPackage>(RpcGrantExchangeCoins, RPC_GrantExchangeCoins);
            ZRoutedRpc.instance.Register<ZPackage>(RpcFinalizePointsExchange, RPC_FinalizePointsExchange);
            ZRoutedRpc.instance.Register<ZPackage>(RpcPointsExchangeFeedback, RPC_PointsExchangeFeedback);
            ZRoutedRpc.instance.Register<ZPackage>(RpcReportMarketplaceQuestComplete, RPC_ReportMarketplaceQuestComplete);
            ZRoutedRpc.instance.Register<ZPackage>(RpcReportFishCaught, RPC_ReportFishCaught);
            ZRoutedRpc.instance.Register<ZPackage>(RpcReportCraftedItem, RPC_ReportCraftedItem);
            ZRoutedRpc.instance.Register<ZPackage>(RpcReportFarmHarvest, RPC_ReportFarmHarvest);
            ZRoutedRpc.instance.Register<ZPackage>(RpcReportPlayerDeath, RPC_ReportPlayerDeath);
            ZRoutedRpc.instance.Register<ZPackage>(RpcReportExplorationMap, RPC_ReportExplorationMap);

            _rpcsRegistered = true;
        }
    }
}
