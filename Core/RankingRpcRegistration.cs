namespace Glitnir.Ranking
{
    public partial class GlitnirRankingPlugin
    {
        private void RegisterRpcsIfNeeded()
        {
            ZRoutedRpc routedRpc = ZRoutedRpc.instance;
            if (routedRpc == null)
                return;






            if (_rpcsRegistered && object.ReferenceEquals(_registeredRoutedRpcInstance, routedRpc))
                return;

            routedRpc.Register<ZPackage>(RpcRequestSnapshot, RPC_RequestSnapshot);
            routedRpc.Register<ZPackage>(RpcReceiveSnapshot, RPC_ReceiveSnapshot);
            routedRpc.Register<ZPackage>(RpcReportKill, RPC_ReportKill);
            routedRpc.Register<ZPackage>(RpcReportSkillGain, RPC_ReportSkillGain);
            routedRpc.Register<ZPackage>(RpcRequestRewardClaim, RPC_RequestRewardClaim);
            routedRpc.Register<ZPackage>(RpcGrantRewardItem, RPC_GrantRewardItem);
            routedRpc.Register<ZPackage>(RpcFinalizeRewardClaim, RPC_FinalizeRewardClaim);
            routedRpc.Register<ZPackage>(RpcRewardClaimFeedback, RPC_RewardClaimFeedback);
            routedRpc.Register<ZPackage>(RpcRequestPointsExchange, RPC_RequestPointsExchange);
            routedRpc.Register<ZPackage>(RpcGrantExchangeCoins, RPC_GrantExchangeCoins);
            routedRpc.Register<ZPackage>(RpcFinalizePointsExchange, RPC_FinalizePointsExchange);
            routedRpc.Register<ZPackage>(RpcPointsExchangeFeedback, RPC_PointsExchangeFeedback);
            routedRpc.Register<ZPackage>(RpcReportMarketplaceQuestComplete, RPC_ReportMarketplaceQuestComplete);
            routedRpc.Register<ZPackage>(RpcReportFishCaught, RPC_ReportFishCaught);
            routedRpc.Register<ZPackage>(RpcReportCraftedItem, RPC_ReportCraftedItem);
            routedRpc.Register<ZPackage>(RpcReportFarmHarvest, RPC_ReportFarmHarvest);
            routedRpc.Register<ZPackage>(RpcReportPlayerDeath, RPC_ReportPlayerDeath);
            routedRpc.Register<ZPackage>(RpcReportExplorationMap, RPC_ReportExplorationMap);

            _registeredRoutedRpcInstance = routedRpc;
            _rpcsRegistered = true;
        }
    }
}
