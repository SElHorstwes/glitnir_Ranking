using BepInEx;
using BepInEx.Logging;
using BepInEx.Configuration;
using BepPaths = BepInEx.Paths;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Text;
using System.Reflection;
using LiteDB;
using Application = UnityEngine.Application;

namespace Glitnir.Ranking
{
    [BepInPlugin(ModGuid, ModName, ModVersion)]
    [BepInDependency("MarketplaceAndServerNPCs", BepInDependency.DependencyFlags.SoftDependency)]
    public partial class GlitnirRankingPlugin : BaseUnityPlugin
    {
        public const string ModGuid = "com.glitnir.ranking";
        public const string ModName = "Glitnir Ranking";
        public const string ModVersion = "0.7.66";

        internal static GlitnirRankingPlugin Instance;
        internal static ManualLogSource Log;

        private const string RpcRequestSnapshot = "glitnir.ranking.requestsnapshot";
        private const string RpcReceiveSnapshot = "glitnir.ranking.receivesnapshot";
        private const string RpcReportHit = "glitnir.ranking.reporthit";
        private const string RpcReportKill = "glitnir.ranking.reportkill";
        private const string RpcReportSkillGain = "glitnir.ranking.reportskillgain";
        private const string RpcRequestRewardClaim = "glitnir.ranking.requestrewardclaim";
        private const string RpcGrantRewardItem = "glitnir.ranking.grantrewarditem";
        private const string RpcFinalizeRewardClaim = "glitnir.ranking.finalizerewardclaim";
        private const string RpcRewardClaimFeedback = "glitnir.ranking.rewardclaimfeedback";
        private const string RpcRequestPointsExchange = "glitnir.ranking.requestpointsexchange";
        private const string RpcGrantExchangeCoins = "glitnir.ranking.grantexchangecoins";
        private const string RpcFinalizePointsExchange = "glitnir.ranking.finalizepointsexchange";
        private const string RpcPointsExchangeFeedback = "glitnir.ranking.pointsexchangefeedback";
        private const string RpcReportMarketplaceQuestComplete = "glitnir.ranking.marketquestcomplete";
        private const string RpcReportFishCaught = "glitnir.ranking.reportfishcaught";
        private const string RpcReportCraftedItem = "glitnir.ranking.reportcrafteditem";
        private const string RpcReportFarmHarvest = "glitnir.ranking.reportfarmharvest";
        private const string RpcReportPlayerDeath = "glitnir.ranking.reportplayerdeath";
        private const string RpcReportExplorationMap = "glitnir.ranking.reportexplorationmap";

        private const float DamageCreditLifetimeSeconds = 180f;
        private const float ProcessedKillLifetimeSeconds = 180f;
        private const float LocalRecentHitLifetimeSeconds = 15f;
        private const float ServerPendingKillDelaySeconds = 0.25f;
        private const float ServerPendingKillTimeoutSeconds = 12f;
        private const float ServerPendingKillCheckInterval = 0.25f;
        private const int MaxHudChars = 3000;
        private const int MaxPlayerNameLength = 32;
        private const int MaxReasonLength = 64;
        private const float ClientRefreshInterval = 1f;


        private readonly Dictionary<string, string> _hudPrefabDisplayNameCache =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);


        private readonly Dictionary<string, GameObject> _hudPrefabObjectCache =
            new Dictionary<string, GameObject>(StringComparer.OrdinalIgnoreCase);
        private int _hudObjectDbItemCountCached = -1;
        private int _hudZNetScenePrefabCountCached = -1;

        private Type _hudLocalizationType;
        private FieldInfo _hudLocalizationInstanceField;
        private PropertyInfo _hudLocalizationInstanceProperty;
        private MethodInfo _hudLocalizationLocalizeMethod;
        private bool _hudLocalizationReflectionReady;

        private Harmony _harmony;
        private bool _rpcsRegistered = false;
        private ZRoutedRpc _registeredRoutedRpcInstance;
        private readonly object _databaseSaveLock = new object();

        private string _rulesFilePath;
        private string _uiFolderPath;
        private string _uiFilePath;
        private string _databaseFilePath;
        private string _legacyDatabaseFilePath;

        private ConfigFile _rulesConfig;
        private FileSystemWatcher _rulesConfigWatcher;
        private bool _rulesConfigReloadQueued;
        private float _rulesConfigReloadAt;
        private float _rulesConfigApplyAt;
        private const float RulesConfigApplyInterval = 1f;

        private ConfigEntry<bool> _cfgRankingEnabled;
        private ConfigEntry<bool> _cfgEnableKillPoints;
        private ConfigEntry<bool> _cfgEnableBossPoints;
        private ConfigEntry<int> _cfgDefaultKillPoints;
        private ConfigEntry<int> _cfgTopCount;
        private ConfigEntry<bool> _cfgAllowRepeatedBossPoints;
        private ConfigEntry<bool> _cfgDebugLogging;
        private ConfigEntry<bool> _cfgLogHitReports;
        private ConfigEntry<bool> _cfgLogKillReports;
        private ConfigEntry<bool> _cfgLogSkillReports;
        private ConfigEntry<bool> _cfgLogPendingKillReports;
        private ConfigEntry<bool> _cfgLogSnapshotRequests;
        private ConfigEntry<bool> _cfgLogPointsChanges;
        private ConfigEntry<bool> _cfgEnableSkillPoints;
        private ConfigEntry<bool> _cfgIgnoreTamedKills;
        private ConfigEntry<bool> _cfgEnableMarketplaceQuestPoints;
        private ConfigEntry<bool> _cfgEnableFishingPoints;
        private ConfigEntry<bool> _cfgEnableDeathPenalty;
        private ConfigEntry<bool> _cfgEnableExplorationJackpots;
        private ConfigEntry<string> _cfgExplorationMapJackpotRules;
        private ConfigEntry<string> _cfgDeathPenaltyRules;
        private ConfigEntry<bool> _cfgDeathPenaltyUseMultiplier;
        private ConfigEntry<int> _cfgDeathPenaltyPerDeath;
        private readonly Dictionary<string, ConfigEntry<string>> _cfgCombatBiomeRuleEntries = new Dictionary<string, ConfigEntry<string>>(StringComparer.OrdinalIgnoreCase);
        private ConfigEntry<string> _cfgProductionCategoryRules;
        private ConfigEntry<string> _cfgMarketplaceQuestPointMap;
        private ConfigEntry<string> _cfgFarmJackpotRules;
        private ConfigEntry<bool> _cfgRewardClaimsEnabled;
        private ConfigEntry<string> _cfgRewardClaimCycleId;
        private ConfigEntry<int> _cfgRewardTop1MinPoints;
        private ConfigEntry<string> _cfgRewardTop1Label;
        private ConfigEntry<string> _cfgRewardTop1Prefab;
        private ConfigEntry<int> _cfgRewardTop1Amount;
        private ConfigEntry<int> _cfgRewardTop2MinPoints;
        private ConfigEntry<string> _cfgRewardTop2Label;
        private ConfigEntry<string> _cfgRewardTop2Prefab;
        private ConfigEntry<int> _cfgRewardTop2Amount;
        private ConfigEntry<int> _cfgRewardTop3MinPoints;
        private ConfigEntry<string> _cfgRewardTop3Label;
        private ConfigEntry<string> _cfgRewardTop3Prefab;
        private ConfigEntry<int> _cfgRewardTop3Amount;
        private ConfigEntry<bool> _cfgPointsExchangeEnabled;
        private ConfigEntry<string> _cfgPointsExchangePrefab;
        private ConfigEntry<bool> _cfgPointsExchangeUseCoinsPerPoint;
        private ConfigEntry<int> _cfgPointsExchangeCoinsPerPoint;
        private ConfigEntry<bool> _cfgPointsExchangeUsePointsPerCoin;
        private ConfigEntry<int> _cfgPointsExchangePointsPerCoin;
        private ConfigEntry<int> _cfgPointsExchangeMinPoints;
        private ConfigEntry<int> _cfgPointsExchangeMaxPointsPerRequest;

        private ConfigEntry<bool> _cfgDiscordTop3WebhookEnabled;
        private ConfigEntry<string> _cfgDiscordTop3WebhookUrl;
        private ConfigEntry<bool> _cfgIgnoreAdminsInRanking;
        private ConfigEntry<string> _cfgIgnoredAdminNames;

        private readonly Dictionary<string, ConfigEntry<int>> _cfgKillPointEntries =
            new Dictionary<string, ConfigEntry<int>>(StringComparer.OrdinalIgnoreCase);

        private readonly Dictionary<string, ConfigEntry<int>> _cfgBossPointEntries =
            new Dictionary<string, ConfigEntry<int>>(StringComparer.OrdinalIgnoreCase);

        private readonly Dictionary<string, ConfigEntry<int>> _cfgFishingPointEntries =
            new Dictionary<string, ConfigEntry<int>>(StringComparer.OrdinalIgnoreCase);

        private readonly Dictionary<string, ConfigEntry<int>> _cfgCraftPointEntries =
            new Dictionary<string, ConfigEntry<int>>(StringComparer.OrdinalIgnoreCase);

        private readonly Dictionary<string, ConfigEntry<string>> _cfgFarmJackpotEntries =
            new Dictionary<string, ConfigEntry<string>>(StringComparer.OrdinalIgnoreCase);

        private readonly Dictionary<string, ConfigEntry<string>> _cfgUniqueCraftJackpotEntries =
            new Dictionary<string, ConfigEntry<string>>(StringComparer.OrdinalIgnoreCase);


        private readonly Dictionary<string, ConfigEntry<string>> _cfgSkillMilestoneJackpotPointEntries =
            new Dictionary<string, ConfigEntry<string>>(StringComparer.OrdinalIgnoreCase);

        private RankingDatabase _database = new RankingDatabase();
        private RankingRules _rules = new RankingRules();
        private readonly Dictionary<int, MarketplaceQuestPointRule> _marketplaceQuestPointsByUid = new Dictionary<int, MarketplaceQuestPointRule>();

        private readonly Dictionary<string, Dictionary<string, float>> _damageCredits =
            new Dictionary<string, Dictionary<string, float>>(StringComparer.Ordinal);

        private readonly Dictionary<string, float> _processedKills =
            new Dictionary<string, float>(StringComparer.Ordinal);

        private readonly Dictionary<string, float> _ignoredTamedKills =
            new Dictionary<string, float>(StringComparer.Ordinal);

        private readonly Dictionary<string, float> _localRecentHits =
            new Dictionary<string, float>(StringComparer.Ordinal);

        private readonly Dictionary<string, PendingKillCheck> _pendingKillChecks =
            new Dictionary<string, PendingKillCheck>(StringComparer.Ordinal);

        private float _serverPendingKillCheckAt;

        private bool _hudVisible;
        private float _hudOpenTime;
        private Rect _windowRect = new Rect(660f, 56f, 548f, 804f);
        private Rect _iconRect = new Rect(24f, 180f, 56f, 56f);


        private GameObject _rankingInputBlockerObject;
        private Canvas _rankingInputBlockerCanvas;
        private RectTransform _rankingInputBlockerRect;
        private Vector2 _rankingScrollPosition = Vector2.zero;
        private Vector2 _playerInfoScrollPosition = Vector2.zero;
        private Vector2 _rulesInfoScrollPosition = Vector2.zero;
        private Vector2 _actionsScrollPosition = Vector2.zero;
        private readonly HashSet<string> _expandedActionPlayers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private int _hudTabIndex = 0;
        private float _hudTabSwitchTime = 0f;
        private readonly HashSet<string> _expandedRuleCategories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _expandedGuideSkillRows = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _expandedRuleSubCategories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private bool _ruleCategoriesInitialized;
        private readonly HashSet<string> _expandedPerformanceCategories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private bool _performanceCategoriesInitialized;
        private float _clientRefreshTimer;
        private float _explorationReportTimer;
        private float _lastReportedMapExplorePercent = -1f;
        private long _lastKnownServerPeerUid;
        private bool _requestedInitialServerSnapshot;
        private string _lastSnapshotSyncPlayerName = "";
        private bool _lastSnapshotHadLocalPlayer;
        private string _cachedTopText = "Carregando ranking...";
        private string _cachedPlayerText = "Aguardando dados do servidor...";
        private string _statusText = "Sincronizando...";
        private float _statusUntil = 0f;
        private bool _rewardClaimRequestPending;
        private string _rewardClaimPendingCycleId = "";
        private int _rewardClaimPendingRank = 0;
        private readonly HashSet<string> _pendingRewardClaims = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _pendingPointsExchanges = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private bool _pointsExchangeRequestPending;
        private string _pointsExchangeAmountInput = "";
        private bool _iconDragging;
        private Vector2 _iconDragOffset = Vector2.zero;
        private Vector2 _iconMouseDownPosition = Vector2.zero;
        private bool _iconMovedDuringDrag;

        private readonly List<SnapshotTopEntryData> _cachedTopEntries = new List<SnapshotTopEntryData>();
        private SnapshotPlayerData _cachedPlayerData = new SnapshotPlayerData();

        private GUIStyle _windowStyle;
        private GUIStyle _headerStyle;
        private GUIStyle _sectionTitleStyle;
        private GUIStyle _rankingTextStyle;
        private GUIStyle _playerTextStyle;
        private GUIStyle _footerStyle;
        private GUIStyle _statusStyle;
        private GUIStyle _iconButtonStyle;
        private GUIStyle _panelHeadingStyle;
        private GUIStyle _cardLabelStyle;
        private GUIStyle _cardValueStyle;
        private GUIStyle _bodyRowStyle;
        private GUIStyle _bodyValueStyle;
        private GUIStyle _mutedBodyStyle;
        private GUIStyle _rankIndexStyle;
        private GUIStyle _rankNameStyle;
        private GUIStyle _rankPointsStyle;
        private GUIStyle _youTagStyle;
        private GUIStyle _emptyStateStyle;
        private GUIStyle _configStyle;
        private bool _stylesReady;

        private string _uiTitleFontNames = "Cinzel Decorative|Cinzel|Trajan Pro|Palatino Linotype|Georgia|Times New Roman";
        private string _uiBodyFontNames = "Cormorant Garamond|Cormorant SC|Palatino Linotype|Georgia|Garamond|Arial";
        private string _uiAccentFontNames = "Cinzel|Palatino Linotype|Georgia|Arial";
        private bool _uiUseSystemFonts = false;
        private KeyCode _uiToggleKey = KeyCode.Y;
        private float _uiIconZoom = 0.96f;
        private Font _uiTitleFont;
        private Font _uiBodyFont;
        private Font _uiAccentFont;

        private Texture2D _uiTransparentTexture;
        private Texture2D _uiWhiteTexture;
        private Texture2D _uiPanelTexture;
        private Texture2D _uiBackgroundTexture;
        private Texture2D _uiRankingIconTexture;
        private Texture2D _uiTitleRankingTexture;
        private Texture2D _uiTitleTopTexture;
        private Texture2D _uiTitleGuideTexture;
        private Texture2D _uiTitlePlayerTexture;
        private Texture2D _uiTitleActionsTexture;


        private readonly Dictionary<string, Texture2D> _uiRuleCategoryIcons = new Dictionary<string, Texture2D>(StringComparer.OrdinalIgnoreCase);
        private const float RuleCategoryIconSize = 24f;


        private Texture2D _uiRank1IconTexture;
        private Texture2D _uiRank2IconTexture;
        private Texture2D _uiRank3IconTexture;

        private bool _uiTexturesLoaded;
        private readonly HashSet<string> _missingUiTextureWarnings = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        private sealed class SnapshotTopEntryData
        {
            public int Position;
            public string PlayerName = "";
            public int Points;
            public bool IsLocalPlayer;
            public int TotalKillsPontuadas;
            public int TotalBossesPontuadas;
            public int TotalSkillLevelUpsPontuados;
            public int TotalMarketplaceQuestsPontuadas;
            public int MarketplaceQuestPointsTotal;
            public int TotalFishingPontuadas;
            public int TotalCraftPontuadas;
            public int TotalFarmJackpotsPontuados;
            public int TotalUniqueCraftJackpotsPontuados;
            public int TotalDeaths;
            public int KillPointsTotal;
            public int BossPointsTotal;
            public int SkillPointsTotal;
            public int FishingPointsTotal;
            public int CraftPointsTotal;
            public int FarmJackpotPointsTotal;
            public int UniqueCraftJackpotPointsTotal;
            public int DeathPenaltyPointsTotal;
            public int TotalPointsExchanges;
            public int PointsExchangePenaltyTotal;
            public int PointsExchangeCoinsTotal;
            public int ExplorationMapJackpotPointsTotal;
            public string LastReason = "";
            public string LastUpdateUtc = "";
        }

        private sealed class SnapshotPlayerData
        {
            public bool HasData;
            public int Position;
            public string PlayerName = "";
            public int Points;
            public int TotalKillsPontuadas;
            public int TotalBossesPontuadas;
            public int TotalSkillLevelUpsPontuados;
            public int TotalMarketplaceQuestsPontuadas;
            public int KillPointsTotal;
            public int BossPointsTotal;
            public int SkillPointsTotal;
            public int MarketplaceQuestPointsTotal;
            public int TotalFishingPontuadas;
            public int TotalCraftPontuadas;
            public int TotalFarmJackpotsPontuados;
            public int TotalUniqueCraftJackpotsPontuados;
            public int TotalDeaths;
            public int FishingPointsTotal;
            public int CraftPointsTotal;
            public int FarmJackpotPointsTotal;
            public int UniqueCraftJackpotPointsTotal;
            public int DeathPenaltyPointsTotal;
            public int TotalPointsExchanges;
            public int PointsExchangePenaltyTotal;
            public int PointsExchangeCoinsTotal;
            public int ExplorationMapJackpotPointsTotal;
            public string LastReason = "";
            public string LastUpdateUtc = "";
            public bool RankingEnabled;
            public bool EnableKillPoints;
            public bool EnableBossPoints;
            public bool EnableSkillPoints;
            public bool EnableMarketplaceQuestPoints;
            public int TopCount;
            public bool RewardClaimsEnabled;
            public bool RewardCanClaim;
            public bool RewardAlreadyClaimed;
            public int RewardRank;
            public int RewardMinPoints;
            public string RewardLabel = "";
            public string RewardPrefabName = "";
            public int RewardAmount;
            public string RewardBlockReason = "";
            public string RewardClaimCycleId = "";
            public string HudKillRules = "";
            public string HudBossRules = "";
            public string HudSkillJackpotRules = "";
            public string HudMarketplaceQuestRules = "";
            public string HudFishingRules = "";
            public string HudCraftRules = "";
            public string HudFarmJackpotRules = "";
            public string HudUniqueCraftJackpotRules = "";
            public string HudDeathPenaltyRules = "";
            public bool EnableDeathPenalty;
            public bool DeathPenaltyUseMultiplier;
            public int DeathPenaltyPerDeath;
            public string HudExplorationMapJackpotRules = "";
            public Dictionary<string, int> ProgressCounters = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            public Dictionary<string, float> FloatProgressCounters = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
        }


        private sealed class PendingKillCheck
        {
            public string PrefabName = "";
            public float FirstSeenTime;
            public float LastHitTime;
        }

        private sealed class RankRewardInfo
        {
            public int Rank;
            public int MinPoints;
            public string Label = "";
            public string PrefabName = "";
            public int Amount;
        }

        private void Awake()
        {
            Instance = this;
            Log = Logger;


            _rulesFilePath = Config.ConfigFilePath;
            _uiFolderPath = string.Empty;
            _uiFilePath = Config.ConfigFilePath;
            _databaseFilePath = Path.Combine(BepPaths.ConfigPath, "GlitnirRanking", "SavedData", "DB.db");
            _legacyDatabaseFilePath = Path.Combine(BepPaths.ConfigPath, "glitnir.ranking.database.legacy.db");

            if (IsDedicatedServerInstance())
                EnsureDatabaseDirectoryExists();

            LoadUiSettings();

            _cfgDiscordTop3WebhookEnabled = Config.Bind(
                "Discord",
                "EnableTop3Webhook",
                true,
                "Ativa o webhook do Discord quando um jogador entra no Top 3 do ranking.");

            _cfgDiscordTop3WebhookUrl = Config.Bind(
                "Discord",
                "Top3WebhookUrl",
                "",
                "URL do webhook do Discord para anunciar ascensão ao Top 3.");

            _cfgIgnoreAdminsInRanking = Config.Bind(
                "Ranking",
                "IgnoreAdminsInRanking",
                true,
                "Se ativo, jogadores presentes na adminlist do servidor nao pontuam nem aparecem no ranking.");

            _cfgIgnoredAdminNames = Config.Bind(
                "Ranking",
                "IgnoredAdminNames",
                "",
                "Fallback opcional: nomes ou IDs de admins para ignorar. Separe por virgula, ponto e virgula ou quebra de linha.");

            InitializeSyncedRulesConfig();
            SetupRulesConfigWatcher();

            if (IsDedicatedServerInstance())
                LoadDatabase();
            else
                _database = new RankingDatabase();

            _harmony = new Harmony(ModGuid);
            _harmony.PatchAll();
            try
            {
                Glitnir.Ranking.Patches.GlitnirAAACraftMaxClamp.Apply(_harmony);
            }
            catch (Exception e)
            {
                Logger.LogWarning("[Glitnir Ranking] Falha ao aplicar AAA Craft Max Clamp: " + e.Message);
            }

        }

        private void OnDestroy()
        {
            try
            {
                if (IsDedicatedServerInstance())
                    SaveDatabase();

                try
                {
                    if (_rulesConfigWatcher != null)
                    {
                        _rulesConfigWatcher.EnableRaisingEvents = false;
                        _rulesConfigWatcher.Dispose();
                        _rulesConfigWatcher = null;
                    }
                }
                catch { }

                DestroyRankingInputBlocker();
                SaveUiSettings();
                _harmony?.UnpatchSelf();
            }
            catch { }
        }

        private void Update()
        {
            RegisterRpcsIfNeeded();
            ProcessRulesConfigReloadIfNeeded();
            RefreshRulesFromSyncedConfigIfNeeded();

            if (!Application.isBatchMode)
            {
                CleanupOldLocalRecentHits();
                CheckInitialServerSnapshotRequest();
            }

            if (Application.isBatchMode)
            {
                CleanupOldDamageCredits();
                CleanupOldProcessedKills();
                ProcessPendingKillChecks();
                return;
            }

            if (_uiToggleKey != KeyCode.None && Input.GetKeyDown(_uiToggleKey))
                ToggleRankingHud();

            if (_hudVisible)
            {
                if (Input.GetKeyDown(KeyCode.Escape))
                {
                    CloseRankingHudAndCollapseAll();
                    return;
                }

                _clientRefreshTimer += Time.unscaledDeltaTime;
                if (_clientRefreshTimer >= ClientRefreshInterval)
                {
                    _clientRefreshTimer = 0f;
                    RequestSnapshotFromServer();
                }

                ProcessLocalExplorationMapReport();
            }
            else
            {
                SetRankingInputBlockerVisible(false);
            }

            if (IsServerInstance())
            {
                CleanupOldDamageCredits();
                CleanupOldProcessedKills();
                ProcessPendingKillChecks();
            }
        }
        private bool IsServerInstance()
        {
            if (Application.isBatchMode)
                return true;

            try
            {
                if (ZNet.instance != null)
                    return ZNet.instance.IsServer();
            }
            catch { }

            return false;
        }

        private bool IsDedicatedServerInstance()
        {
            return Application.isBatchMode;
        }

        private bool IsConnectedToDedicatedServer()
        {
            return !Application.isBatchMode && GetServerPeerUid() != 0L;
        }

        private const string DefaultKillPointRules = "Boar:1";

        private const string DefaultCombatCategoryRules =
            "Prados:Boar:1,Deer:1,Neck:1;"
            + "Floresta Negra:Greydwarf:1,Greydwarf_Elite:2,Greydwarf_Shaman:2,Troll:5;"
            + "Pântano:Draugr:2,Draugr_Elite:3,Blob:2,Abomination:8;"
            + "Montanha:Wolf:2,Hatchling:2,StoneGolem:8;"
            + "Planícies:Goblin:3,GoblinBrute:5,Lox:6,Deathsquito:3;"
            + "Oceano:Serpent:6;"
            + "Mistlands:Seeker:4,SeekerBrute:8,Gjall:10;"
            + "Ashlands:Charred_Melee:4,Charred_Archer:4,Volture:4;"
            + "Especiais:Haldor:0";

        private const string DefaultProductionCategoryRules =
            "Armas:SwordIron=800;"
            + "Armaduras:HelmetBronze=800;"
            + "Comidas:DeerStew=25";


        public void ReportLocalMarketplaceQuestCompletion(int questUid)
        {
            try
            {
                if (_rules == null || !_rules.RankingEnabled || !_rules.EnableMarketplaceQuestPoints)
                    return;

                if (!TryGetMarketplaceQuestRule(questUid, out MarketplaceQuestPointRule rule))
                    return;

                if (Player.m_localPlayer == null || ZRoutedRpc.instance == null)
                    return;

                ZPackage pkg = new ZPackage();
                pkg.Write(questUid);
                pkg.Write(SafeMarketplaceQuestKey(rule.QuestKey));
                pkg.Write(rule.Points);
                pkg.Write(SafeLimit(Player.m_localPlayer.GetPlayerName(), MaxPlayerNameLength));

                ZRoutedRpc.instance.InvokeRoutedRPC(GetServerPeerUid(), RpcReportMarketplaceQuestComplete, pkg);

                DebugLog(DebugCategory.Points, "Quest Marketplace reportada: uid=" + questUid + " key=" + rule.QuestKey + " points=" + rule.Points);
            }
            catch (Exception ex)
            {
                Logger.LogError("Erro ao reportar conclusão de quest do Marketplace: " + ex);
            }
        }

        private void RPC_ReportMarketplaceQuestComplete(long sender, ZPackage pkg)
        {
            try
            {
                if (!IsServerInstance() || pkg == null)
                    return;

                int questUid = pkg.ReadInt();
                string questKey = SafeMarketplaceQuestKey(pkg.ReadString());
                int requestedPoints = Mathf.Clamp(pkg.ReadInt(), -int.MaxValue, int.MaxValue);
                string reporterName = SanitizePlayerName(pkg.ReadString());
                string playerName = ResolvePlayerNameFromSender(sender);

                if (string.IsNullOrWhiteSpace(playerName))
                    playerName = reporterName;

                if (string.IsNullOrWhiteSpace(playerName))
                    return;

                if (ShouldIgnoreSenderForRanking(sender, playerName))
                {
                    DebugLog(DebugCategory.Points, "Quest Marketplace ignorada para admin: " + playerName);
                    return;
                }

                ProcessMarketplaceQuestReport(playerName, questUid, questKey, requestedPoints, "rpc-marketplace-quest");
            }
            catch (Exception ex)
            {
                Logger.LogError("Erro no RPC_ReportMarketplaceQuestComplete: " + ex);
            }
        }

        private void ProcessMarketplaceQuestReport(string playerName, int questUid, string questKey, int requestedPoints, string source)
        {
            try
            {
                if (!IsServerInstance() || _rules == null || !_rules.RankingEnabled || !_rules.EnableMarketplaceQuestPoints)
                    return;

                if (!TryGetMarketplaceQuestRule(questUid, out MarketplaceQuestPointRule rule))
                {
                    DebugLog(DebugCategory.Points, "Quest do Marketplace ignorada sem regra: uid=" + questUid + " key=" + questKey + " source=" + source);
                    return;
                }

                if (!string.IsNullOrWhiteSpace(questKey) &&
                    !string.Equals(rule.QuestKey, questKey, StringComparison.OrdinalIgnoreCase))
                {
                    DebugLog(DebugCategory.Points, "Quest do Marketplace rejeitada por chave divergente: uid=" + questUid + " key=" + questKey + " esperado=" + rule.QuestKey);
                    return;
                }

                playerName = SanitizePlayerName(playerName);
                if (ShouldIgnorePlayerForRanking(playerName))
                {
                    DebugLog(DebugCategory.Points, "Quest Marketplace ignorada para admin: " + playerName);
                    return;
                }

                if (HasMarketplaceQuestCredit(playerName, rule.QuestKey))
                {
                    DebugLog(DebugCategory.Points, "Quest Marketplace já creditada: player=" + playerName + " key=" + rule.QuestKey);
                    return;
                }

                RankingEntry entry = GetOrCreateEntry(playerName);
                if (entry == null)
                    return;

                int pointsToGrant = rule.Points;
                if (requestedPoints != pointsToGrant)
                {
                    DebugLog(DebugCategory.Points, "Quest Marketplace com pontos normalizados: player=" + playerName + " key=" + rule.QuestKey + " solicitado=" + requestedPoints + " configurado=" + pointsToGrant);
                }

                ApplyPointsToEntry(entry, pointsToGrant, "Quest Marketplace: " + rule.QuestKey);
                MarkMarketplaceQuestCredit(playerName, rule.QuestKey);


            }
            catch (Exception ex)
            {
                Logger.LogError("Erro ao processar conclusão de quest do Marketplace: " + ex);
            }
        }

        private string BuildMarketplaceQuestCreditKey(string playerName, string questKey)
        {
            return SanitizePlayerName(playerName).ToLowerInvariant() + "|" + SafeMarketplaceQuestKey(questKey);
        }

        private bool HasMarketplaceQuestCredit(string playerName, string questKey)
        {
            return HasMarketplaceQuestCreditCached(playerName, questKey);
        }

        private void MarkMarketplaceQuestCredit(string playerName, string questKey)
        {
            if (_database == null)
                _database = new RankingDatabase();

            if (_database.MarketplaceQuestCredits == null)
                _database.MarketplaceQuestCredits = new List<MarketplaceQuestCreditRecord>();

            if (HasMarketplaceQuestCredit(playerName, questKey))
                return;

            _database.MarketplaceQuestCredits.Add(new MarketplaceQuestCreditRecord
            {
                PlayerName = SanitizePlayerName(playerName),
                QuestKey = SafeMarketplaceQuestKey(questKey),
                GrantedAtUtc = DateTime.UtcNow.ToString("O")
            });

            if (IsServerInstance())
                SaveDatabase();
        }


        private void ProcessLocalExplorationMapReport()
        {
            try
            {
                if (_rules == null || !_rules.RankingEnabled || !_rules.EnableExplorationJackpots)
                    return;

                if (Player.m_localPlayer == null || Minimap.instance == null)
                    return;

                _explorationReportTimer += Time.unscaledDeltaTime;
                if (_explorationReportTimer < 15f)
                    return;

                _explorationReportTimer = 0f;

                float percent = GetLocalMapExplorationPercent();
                if (percent < 0f)
                    return;

                if (percent <= _lastReportedMapExplorePercent + 0.05f)
                    return;

                _lastReportedMapExplorePercent = percent;

                long serverPeerUid = GetServerPeerUid();
                if (serverPeerUid == 0L || !_rpcsRegistered || ZRoutedRpc.instance == null)
                    return;

                ZPackage pkg = new ZPackage();
                pkg.Write(percent);
                pkg.Write(SafeLimit(Player.m_localPlayer.GetPlayerName(), MaxPlayerNameLength));
                ZRoutedRpc.instance.InvokeRoutedRPC(serverPeerUid, RpcReportExplorationMap, pkg);

                DebugLog(DebugCategory.Points, "Exploração de mapa reportada: " + percent + "%");
            }
            catch (Exception ex)
            {
                Logger.LogWarning("[Ranking] Falha ao reportar exploração de mapa: " + ex.Message);
            }
        }

        private float GetLocalMapExplorationPercent()
        {
            try
            {
                Minimap minimap = Minimap.instance;
                if (minimap == null)
                    return -1;

                FieldInfo exploredField = AccessTools.Field(typeof(Minimap), "m_explored");
                if (exploredField == null)
                    return -1f;

                bool[] explored = exploredField.GetValue(minimap) as bool[];
                if (explored == null || explored.Length == 0)
                    return -1f;

                int exploredCount = 0;
                for (int i = 0; i < explored.Length; i++)
                {
                    if (explored[i])
                        exploredCount++;
                }

                float pct = (exploredCount * 100f) / Mathf.Max(1, explored.Length);
                return Mathf.Clamp(pct, 0f, 100f);
            }
            catch
            {
                return -1;
            }
        }

        private void RPC_ReportExplorationMap(long sender, ZPackage pkg)
        {
            try
            {
                if (!IsServerInstance() || pkg == null)
                    return;

                float percent = Mathf.Clamp(pkg.ReadSingle(), 0f, 100f);
                string reporterName = SanitizePlayerName(pkg.ReadString());
                string playerName = ResolvePlayerNameFromSender(sender);

                if (string.IsNullOrWhiteSpace(playerName))
                    playerName = reporterName;

                ProcessExplorationMapReport(playerName, percent);
            }
            catch (Exception ex)
            {
                Logger.LogError("Erro no RPC_ReportExplorationMap: " + ex);
            }
        }

        private void ProcessExplorationMapReport(string playerName, float percent)
        {
            try
            {
                if (!IsServerInstance() || _rules == null || !_rules.RankingEnabled || !_rules.EnableExplorationJackpots)
                    return;

                playerName = SanitizePlayerName(playerName);
                if (string.IsNullOrWhiteSpace(playerName) || ShouldIgnorePlayerForRanking(playerName))
                    return;

                if (_rules.ExplorationMapJackpots == null || _rules.ExplorationMapJackpots.Count == 0)
                    return;

                RankingEntry entry = GetOrCreateEntry(playerName);
                if (entry == null)
                    return;

                if (entry.ProgressCounters == null)
                    entry.ProgressCounters = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                if (entry.GenericJackpotCredits == null)
                    entry.GenericJackpotCredits = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                if (entry.FloatProgressCounters == null)
                    entry.FloatProgressCounters = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);

                float oldPercent = 0f;
                float oldFloatPercent;
                int oldIntPercent = 0;

                if (entry.FloatProgressCounters.TryGetValue("ExplorationMap:Percent", out oldFloatPercent))
                    oldPercent = oldFloatPercent;
                else if (entry.ProgressCounters.TryGetValue("ExplorationMap:Percent", out oldIntPercent))
                    oldPercent = oldIntPercent;

                float savedPercent = Mathf.Clamp(Mathf.Max(oldPercent, percent), 0f, 100f);
                int savedPercentInt = Mathf.Clamp(Mathf.FloorToInt(savedPercent), 0, 100);


                entry.FloatProgressCounters["ExplorationMap:Percent"] = savedPercent;
                entry.FloatProgressCounters["Exploration:Map"] = savedPercent;
                entry.FloatProgressCounters["Exploracao:Mapa"] = savedPercent;


                entry.ProgressCounters["ExplorationMap:Percent"] = savedPercentInt;
                entry.ProgressCounters["Exploration:Map"] = savedPercentInt;
                entry.ProgressCounters["Exploracao:Mapa"] = savedPercentInt;

                string cycleId = SafeLimit(_rules.RewardClaimCycleId, 64);

                foreach (KeyValuePair<int, int> rule in _rules.ExplorationMapJackpots.OrderBy(p => p.Key))
                {
                    int requiredPercent = Mathf.Clamp(rule.Key, 1, 100);
                    int points = Mathf.Max(0, rule.Value);

                    if (savedPercent < requiredPercent || points <= 0)
                        continue;

                    string creditKey = cycleId + ":ExplorationMap:" + requiredPercent;
                    if (entry.GenericJackpotCredits.Contains(creditKey))
                        continue;

                    entry.GenericJackpotCredits.Add(creditKey);
                    ApplyPointsToEntry(entry, points, "Exploração do mapa: " + requiredPercent + "%");
                    entry.ExplorationMapJackpotPointsTotal = Mathf.Clamp(entry.ExplorationMapJackpotPointsTotal + points, 0, int.MaxValue);
                }

                SaveDatabase();
            }
            catch (Exception ex)
            {
                Logger.LogError("Erro ao processar exploração de mapa: " + ex);
            }
        }


        private void EnsureUiDirectoryExists()
        {

        }

        private void MigrateLegacyUiSettingsIfNeeded()
        {
            try
            {
                string legacyUiFilePath = Path.Combine(BepPaths.ConfigPath, "glitnir.ranking.ui.cfg");
                if (string.IsNullOrWhiteSpace(_uiFilePath) || string.Equals(legacyUiFilePath, _uiFilePath, StringComparison.OrdinalIgnoreCase))
                    return;

                if (File.Exists(_uiFilePath))
                    return;

                if (!File.Exists(legacyUiFilePath))
                    return;

                File.Copy(legacyUiFilePath, _uiFilePath, true);
            }
            catch (Exception ex)
            {
                Logger.LogError("Erro ao migrar configuração antiga da UI do ranking: " + ex);
            }
        }

        private void LoadUiSettings()
        {
            try
            {
                _uiToggleKey = Config.Bind(
                    "UI",
                    "HudShortcutKey",
                    _uiToggleKey,
                    "Tecla para abrir/fechar o HUD. Use nomes do Unity KeyCode, ex.: F, G, Alpha1, None.").Value;

                _uiUseSystemFonts = Config.Bind(
                    "UI",
                    "UseSystemFonts",
                    _uiUseSystemFonts,
                    "Ative apenas se o cliente renderizar bem fontes instaladas no sistema.").Value;

                _uiTitleFontNames = Config.Bind(
                    "UI",
                    "TitleFontNames",
                    _uiTitleFontNames,
                    "Fontes opcionais para titulos, separadas por |.").Value;

                _uiBodyFontNames = Config.Bind(
                    "UI",
                    "BodyFontNames",
                    _uiBodyFontNames,
                    "Fontes opcionais para texto, separadas por |.").Value;

                _uiAccentFontNames = Config.Bind(
                    "UI",
                    "AccentFontNames",
                    _uiAccentFontNames,
                    "Fontes opcionais para detalhes/acento, separadas por |.").Value;

                _iconRect.x = Config.Bind("UI", "IconX", _iconRect.x, "Posição X do botão do ranking.").Value;
                _iconRect.y = Config.Bind("UI", "IconY", _iconRect.y, "Posição Y do botão do ranking.").Value;

                float iconSize = Config.Bind("UI", "IconSize", _iconRect.width, "Tamanho do botão do ranking.").Value;
                _iconRect.width = Mathf.Clamp(iconSize, 36f, 120f);
                _iconRect.height = _iconRect.width;

                _uiIconZoom = Mathf.Clamp(Config.Bind("UI", "IconZoom", _uiIconZoom, "Zoom interno do ícone do botão.").Value, 0.75f, 1.3f);

                _windowRect.x = Config.Bind("UI", "WindowX", _windowRect.x, "Posição X da janela do ranking.").Value;
                _windowRect.y = Config.Bind("UI", "WindowY", _windowRect.y, "Posição Y da janela do ranking.").Value;
                _windowRect.width = Mathf.Max(420f, Config.Bind("UI", "WindowW", _windowRect.width, "Largura da janela do ranking.").Value);
                _windowRect.height = Mathf.Max(760f, Config.Bind("UI", "WindowH", _windowRect.height, "Altura da janela do ranking.").Value);
            }
            catch (Exception ex)
            {
                Logger.LogError("Erro ao carregar UI do ranking: " + ex);
            }
        }

        private void SaveUiSettings()
        {
            try
            {
                Config.Save();
            }
            catch (Exception ex)
            {
                Logger.LogError("Erro ao salvar configuração do ranking: " + ex);
            }
        }

        private Texture2D LoadTextureFromUiFolder(params string[] fileNames)
        {
            if (fileNames == null || fileNames.Length == 0)
                return null;

            for (int i = 0; i < fileNames.Length; i++)
            {
                string fileName = fileNames[i];
                if (string.IsNullOrWhiteSpace(fileName))
                    continue;

                Texture2D texture = LoadTextureFromUiFolder(fileName);
                if (texture != null)
                    return texture;
            }

            return null;
        }

        private Texture2D LoadTextureFromUiFolder(string fileName)
        {
            Texture2D embeddedTexture = LoadEmbeddedTexture(fileName);
            if (embeddedTexture != null)
                return embeddedTexture;

            try
            {
                if (string.IsNullOrWhiteSpace(_uiFolderPath))
                    return null;

                string fullPath = Path.Combine(_uiFolderPath, fileName);
                if (!File.Exists(fullPath))
                    return null;

                byte[] bytes = File.ReadAllBytes(fullPath);
                if (bytes == null || bytes.Length == 0)
                {
                    if (_missingUiTextureWarnings.Add(fileName + ":empty"))
                        Logger.LogWarning("[Ranking UI] Arquivo vazio ou inválido: " + fullPath);
                    return null;
                }

                Texture2D texture = CreateTextureFromBytes(bytes, fileName);
                if (texture != null)
                    return texture;

                if (_missingUiTextureWarnings.Add(fileName + ":decoder"))
                    Logger.LogWarning("[Ranking UI] Não foi possível decodificar a textura: " + fullPath + ". Verifique se o PNG/JPG é válido.");
            }
            catch (Exception ex)
            {
                if (_missingUiTextureWarnings.Add(fileName + ":exception"))
                    Logger.LogWarning("[Ranking UI] Falha ao carregar textura " + fileName + ": " + ex.Message);
            }

            return null;
        }

        private Texture2D LoadEmbeddedTexture(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
                return null;

            try
            {
                Assembly assembly = Assembly.GetExecutingAssembly();
                if (assembly == null)
                    return null;

                string[] resourceNames = assembly.GetManifestResourceNames();
                if (resourceNames == null || resourceNames.Length == 0)
                    return null;

                string normalizedFileName = fileName.Replace('/', '.').Replace('\\', '.');
                StringComparison comparison = StringComparison.OrdinalIgnoreCase;

                string matchedResourceName = resourceNames.FirstOrDefault(name =>
                    !string.IsNullOrWhiteSpace(name) &&
                    (name.Equals(normalizedFileName, comparison) ||
                     name.EndsWith("." + normalizedFileName, comparison) ||
                     name.EndsWith(fileName, comparison) ||
                     name.IndexOf(normalizedFileName, comparison) >= 0));

                if (string.IsNullOrWhiteSpace(matchedResourceName))
                    return null;

                using (Stream stream = assembly.GetManifestResourceStream(matchedResourceName))
                {
                    if (stream == null)
                        return null;

                    byte[] bytes = ReadAllBytes(stream);
                    if (bytes == null || bytes.Length == 0)
                        return null;

                    Texture2D texture = CreateTextureFromBytes(bytes, matchedResourceName);
                    if (texture != null)
                    {
                        return texture;
                    }
                }
            }
            catch (Exception ex)
            {
                if (_missingUiTextureWarnings.Add(fileName + ":embeddedexception"))
                    Logger.LogWarning("[Ranking UI] Falha ao carregar textura embutida " + fileName + ": " + ex.Message);
            }

            return null;
        }

        private Texture2D CreateTextureFromBytes(byte[] bytes, string textureName)
        {
            if (bytes == null || bytes.Length == 0)
                return null;

            Texture2D texture = null;

            try
            {
                texture = new Texture2D(2, 2, TextureFormat.ARGB32, false);
                texture.wrapMode = TextureWrapMode.Clamp;
                texture.filterMode = FilterMode.Bilinear;
                texture.name = textureName;

                if (TryLoadTextureBytes(texture, bytes))
                    return texture;
            }
            catch (Exception ex)
            {
                if (_missingUiTextureWarnings.Add((textureName ?? "texture") + ":createtextureexception"))
                    Logger.LogWarning("[Ranking UI] Falha ao criar textura " + textureName + ": " + ex.Message);
            }

            if (texture != null)
                UnityEngine.Object.Destroy(texture);

            return null;
        }

        private byte[] ReadAllBytes(Stream stream)
        {
            if (stream == null)
                return null;

            if (stream is MemoryStream memoryStream)
                return memoryStream.ToArray();

            using (MemoryStream ms = new MemoryStream())
            {
                stream.CopyTo(ms);
                return ms.ToArray();
            }
        }

        private bool TryLoadTextureBytes(Texture2D texture, byte[] bytes)
        {
            if (texture == null || bytes == null || bytes.Length == 0)
                return false;

            try
            {
                Type imageConversionType = FindTypeInLoadedAssemblies("UnityEngine.ImageConversion");
                if (imageConversionType != null)
                {
                    MethodInfo staticLoadImage = imageConversionType.GetMethod(
                        "LoadImage",
                        BindingFlags.Public | BindingFlags.Static,
                        null,
                        new Type[] { typeof(Texture2D), typeof(byte[]) },
                        null);

                    if (staticLoadImage != null)
                    {
                        object result = staticLoadImage.Invoke(null, new object[] { texture, bytes });
                        if (result is bool)
                            return (bool)result;
                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogWarning("[Ranking UI] Falha no carregamento refletido da textura: " + ex.Message);
            }

            return false;
        }

        private Type FindTypeInLoadedAssemblies(string fullTypeName)
        {
            if (string.IsNullOrWhiteSpace(fullTypeName))
                return null;

            try
            {
                Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
                for (int i = 0; i < assemblies.Length; i++)
                {
                    Assembly assembly = assemblies[i];
                    if (assembly == null)
                        continue;

                    Type foundType = assembly.GetType(fullTypeName, false);
                    if (foundType != null)
                        return foundType;
                }
            }
            catch { }

            return null;
        }

        private Texture2D CreateSolidTexture(Color color)
        {
            Texture2D texture = new Texture2D(1, 1, TextureFormat.ARGB32, false);
            texture.SetPixel(0, 0, color);
            texture.Apply(false, true);
            texture.wrapMode = TextureWrapMode.Clamp;
            return texture;
        }

        private void EnsureUiTexturesLoaded()
        {
            if (_uiTexturesLoaded)
                return;

            _uiTransparentTexture = CreateSolidTexture(new Color(0f, 0f, 0f, 0f));
            _uiWhiteTexture = CreateSolidTexture(new Color(1f, 1f, 1f, 1f));
            _uiPanelTexture = CreateSolidTexture(new Color(0.05f, 0.04f, 0.03f, 0.72f));

            _uiBackgroundTexture = LoadTextureFromUiFolder("background_epic_forest_transparent.png");
            _uiRankingIconTexture = LoadTextureFromUiFolder("ranking_icon.png");
            _uiTitleRankingTexture = LoadTextureFromUiFolder("title_ranking_glitnir_transparent.png");
            _uiTitleTopTexture = LoadTextureFromUiFolder("topRanking.png", "title_top_glitnir_transparent.png", "title_top_glitnir.png", "title_top_ranking.png");
            _uiTitleGuideTexture = LoadTextureFromUiFolder("guiaHonra.png", "guideHonor.png", "title_guia_honra_transparent.png", "title_guia_de_honra_transparent.png", "title_guide_honor.png");
            _uiTitlePlayerTexture = LoadTextureFromUiFolder("title_seu_desempenho_transparent.png");
            _uiTitleActionsTexture = LoadTextureFromUiFolder("title_oraculo_ranking_transparent.png", "title_oraculo_do_ranking_transparent.png", "oraculo_ranking.png", "oraculo_do_ranking.png");


            _uiRank1IconTexture = LoadTextureFromUiFolder("icon/rank_1.png", "icons/rank_1.png", "rank_1.png");
            _uiRank2IconTexture = LoadTextureFromUiFolder("icon/rank_2.png", "icons/rank_2.png", "rank_2.png");
            _uiRank3IconTexture = LoadTextureFromUiFolder("icon/rank_3.png", "icons/rank_3.png", "rank_3.png");

            LoadRuleCategoryIcons();

            _uiTexturesLoaded = true;
        }

        private void LoadRuleCategoryIcons()
        {
            _uiRuleCategoryIcons.Clear();


            RegisterRuleCategoryIcon("Combate", "0 (5)");
            RegisterRuleCategoryIcon("Bosses", "0 (14)");
            RegisterRuleCategoryIcon("Skills", "0 (1)");
            RegisterRuleCategoryIcon("Quests", "0 (31)");
            RegisterRuleCategoryIcon("Pesca", "0 (32)");
            RegisterRuleCategoryIcon("Exploração", "0 (27)");
            RegisterRuleCategoryIcon("Exploracao", "0 (27)");
            RegisterRuleCategoryIcon("Exploration", "0 (27)");
            RegisterRuleCategoryIcon("Exploração", "icon_exploration.png");
            RegisterRuleCategoryIcon("Exploracao", "icon_exploration.png");
            RegisterRuleCategoryIcon("Exploration", "icon_exploration.png");
            RegisterRuleCategoryIcon("Produção", "0 (26)");
            RegisterRuleCategoryIcon("Cultivo", "0 (11)");
            RegisterRuleCategoryIcon("Conquistas", "0 (29)");
            RegisterRuleCategoryIcon("Penalidades", "0 (23)");


            RegisterRuleCategoryIcon("Resumo", "0 (29)");
            RegisterRuleCategoryIcon("Origem", "0 (12)");
            RegisterRuleCategoryIcon("Último registro", "0 (7)");
            RegisterRuleCategoryIcon("Recompensa", "0 (29)");
            RegisterRuleCategoryIcon("Configuração", "0 (27)");
        }

        private void RegisterRuleCategoryIcon(string categoryKey, string iconFileNameWithoutExtension)
        {
            if (string.IsNullOrWhiteSpace(categoryKey) || string.IsNullOrWhiteSpace(iconFileNameWithoutExtension))
                return;


            string fileName = iconFileNameWithoutExtension.EndsWith(".png", StringComparison.OrdinalIgnoreCase)
                ? iconFileNameWithoutExtension
                : iconFileNameWithoutExtension + ".png";

            Texture2D icon = LoadTextureFromUiFolder(
                "icon/" + fileName,
                "icons/" + fileName,
                fileName
            );

            if (icon != null)
                _uiRuleCategoryIcons[categoryKey] = icon;


        }


        private bool IsLocalPlayerAttackAuthor(HitData hit)
        {
            if (hit == null || Player.m_localPlayer == null)
                return false;

            try
            {
                ZDOID attackerZdo = hit.m_attacker;
                ZDOID localZdo = Player.m_localPlayer.GetZDOID();

                if (!attackerZdo.IsNone() && !localZdo.IsNone() &&
                    attackerZdo.UserID == localZdo.UserID &&
                    attackerZdo.ID == localZdo.ID)
                    return true;
            }
            catch { }

            try
            {
                Character attacker = hit.GetAttacker();
                if (attacker == Player.m_localPlayer)
                    return true;
            }
            catch { }

            return false;
        }

        private string GetLocalPlayerKey()
        {
            try
            {
                if (Player.m_localPlayer != null)
                    return GetZdoKey(Player.m_localPlayer);
            }
            catch { }

            return "";
        }


        internal void ReportLocalPlayerDeathToServer()
        {
            try
            {
                if (Player.m_localPlayer == null || ZRoutedRpc.instance == null)
                    return;

                long serverPeerUid = GetServerPeerUid();

                ZPackage pkg = new ZPackage();
                pkg.Write(SafeLimit(Player.m_localPlayer.GetPlayerName(), MaxPlayerNameLength));

                ZRoutedRpc.instance.InvokeRoutedRPC(serverPeerUid, RpcReportPlayerDeath, pkg);

                DebugLog(DebugCategory.Points, "Cliente reportando morte: player=" + GetLocalPlayerName());
            }
            catch (Exception ex)
            {
                Logger.LogError("Erro ao enviar morte local: " + ex);
            }
        }

        private void RPC_ReportPlayerDeath(long sender, ZPackage pkg)
        {
            try
            {
                if (!IsServerInstance() || pkg == null)
                    return;

                string reporterName = SanitizePlayerName(pkg.ReadString());
                string playerName = ResolvePlayerNameFromSender(sender);

                if (string.IsNullOrWhiteSpace(playerName))
                    playerName = reporterName;

                if (ShouldIgnoreSenderForRanking(sender, playerName))
                {
                    DebugLog(DebugCategory.Points, "Morte ignorada para admin: " + playerName);
                    return;
                }

                ProcessPlayerDeathReport(playerName, "rpc-death");
            }
            catch (Exception ex)
            {
                Logger.LogError("Erro no RPC_ReportPlayerDeath: " + ex);
            }
        }

        private void ProcessPlayerDeathReport(string playerName, string source)
        {
            try
            {
                if (!IsServerInstance() || _rules == null || !_rules.RankingEnabled || !_rules.EnableDeathPenalty)
                    return;

                playerName = SanitizePlayerName(playerName);
                if (string.IsNullOrWhiteSpace(playerName))
                    return;

                if (ShouldIgnorePlayerForRanking(playerName))
                {
                    DebugLog(DebugCategory.Points, "Morte ignorada para admin: " + playerName);
                    return;
                }

                RankingEntry entry = GetOrCreateEntry(playerName);
                if (entry == null)
                    return;

                if (entry.ProgressCounters == null)
                    entry.ProgressCounters = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                if (entry.GenericJackpotCredits == null)
                    entry.GenericJackpotCredits = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                string progressKey = "DeathPenalty:Deaths";
                int deaths = 0;
                entry.ProgressCounters.TryGetValue(progressKey, out deaths);
                deaths = Mathf.Clamp(deaths + 1, 0, int.MaxValue);
                entry.ProgressCounters[progressKey] = deaths;
                entry.TotalDeaths = deaths;

                DebugLog(DebugCategory.Points, "Morte registrada: player=" + playerName + " deaths=" + deaths + " source=" + source);


                if (_rules.DeathPenaltyUseMultiplier)
                {
                    int pointsLost = Mathf.Max(0, _rules.DeathPenaltyPerDeath);

                    if (pointsLost > 0)
                    {
                        ApplyPointsToEntry(entry, -pointsLost, "Penalidade por morte: " + deaths + "x -" + pointsLost);
                        entry.DeathPenaltyPointsTotal = Mathf.Clamp(entry.DeathPenaltyPointsTotal + pointsLost, 0, int.MaxValue);
                    }

                    SaveDatabase();
                    return;
                }


                if (_rules.DeathPenaltyRules != null && _rules.DeathPenaltyRules.Count > 0)
                {
                    string cycleId = SafeLimit(_rules.RewardClaimCycleId, 64);

                    foreach (KeyValuePair<int, int> rule in _rules.DeathPenaltyRules.OrderBy(p => p.Key))
                    {
                        int requiredDeaths = Mathf.Max(1, rule.Key);
                        int pointsLost = Mathf.Max(0, rule.Value);

                        if (deaths < requiredDeaths)
                            continue;

                        string creditKey = cycleId + ":DeathPenalty:" + requiredDeaths;
                        if (entry.GenericJackpotCredits.Contains(creditKey))
                            continue;

                        entry.GenericJackpotCredits.Add(creditKey);

                        if (pointsLost > 0)
                        {
                            ApplyPointsToEntry(entry, -pointsLost, "Penalidade por morte: " + deaths + " mortes");
                            entry.DeathPenaltyPointsTotal = Mathf.Clamp(entry.DeathPenaltyPointsTotal + pointsLost, 0, int.MaxValue);
                        }
                    }
                }

                SaveDatabase();
            }
            catch (Exception ex)
            {
                Logger.LogError("Erro ao processar penalidade por morte: " + ex);
            }
        }


        private void IncrementProgressCounter(RankingEntry entry, string category, string key, int amount)
        {
            if (entry == null || string.IsNullOrWhiteSpace(category) || string.IsNullOrWhiteSpace(key) || amount <= 0)
                return;

            if (entry.ProgressCounters == null)
                entry.ProgressCounters = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            string progressKey = SafeKey(category) + ":" + SafeKey(key);
            int current = 0;
            entry.ProgressCounters.TryGetValue(progressKey, out current);
            entry.ProgressCounters[progressKey] = Mathf.Clamp(current + amount, 0, int.MaxValue);
        }

        private void IncrementProgressCounterAlias(RankingEntry entry, string category, string key, int amount)
        {


            IncrementProgressCounter(entry, category, key, amount);
        }

        private void AddProgressAndCheckJackpot(RankingEntry entry, string category, string key, int amount, JackpotRule rule, string reason, bool unique)
        {
            if (entry == null || rule == null || rule.RequiredAmount <= 0 || rule.Points <= 0)
                return;

            if (entry.ProgressCounters == null)
                entry.ProgressCounters = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            if (entry.GenericJackpotCredits == null)
                entry.GenericJackpotCredits = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            string progressKey = SafeKey(category) + ":" + SafeKey(key);
            string creditKey = SafeLimit(_rules.RewardClaimCycleId, 64) + ":" + progressKey + ":" + rule.RequiredAmount;

            if (entry.GenericJackpotCredits.Contains(creditKey))
                return;

            int current = 0;
            entry.ProgressCounters.TryGetValue(progressKey, out current);
            current = unique ? Mathf.Max(current, amount) : Mathf.Clamp(current + amount, 0, int.MaxValue);
            entry.ProgressCounters[progressKey] = current;

            if (current < rule.RequiredAmount)
            {
                if (IsDedicatedServerInstance())
                    SaveDatabase();
                return;
            }

            entry.GenericJackpotCredits.Add(creditKey);
            ApplyPointsToEntry(entry, rule.Points, reason + " (" + current + "/" + rule.RequiredAmount + ")");

            if (category.Equals("Farm", StringComparison.OrdinalIgnoreCase))
            {
                entry.TotalFarmJackpotsPontuados++;
                entry.FarmJackpotPointsTotal = Mathf.Clamp(entry.FarmJackpotPointsTotal + rule.Points, -int.MaxValue, int.MaxValue);
            }
            else if (category.Equals("UniqueCraft", StringComparison.OrdinalIgnoreCase))
            {
                entry.TotalUniqueCraftJackpotsPontuados++;
                entry.UniqueCraftJackpotPointsTotal = Mathf.Clamp(entry.UniqueCraftJackpotPointsTotal + rule.Points, -int.MaxValue, int.MaxValue);
            }

            if (IsServerInstance())
                SaveDatabase();
        }


        private void SendDiscordTop3EnteredWebhook(string playerName, int rank, int points, string reason)
        {
            if (_cfgDiscordTop3WebhookUrl == null || string.IsNullOrWhiteSpace(_cfgDiscordTop3WebhookUrl.Value))
                return;

            string medal = rank == 1 ? "🥇" : rank == 2 ? "🥈" : "🥉";
            string safePlayer = string.IsNullOrWhiteSpace(playerName) ? "Jogador" : playerName.Trim();
            string safeReason = string.IsNullOrWhiteSpace(reason) ? "Ascensão no ranking" : reason.Trim();

            string json = BuildDiscordTop3EmbedJson(
                "🏛️ ORÁCULO DO RANKING",
                "✨ Um novo nome ascendeu ao pódio de Glitnir.",
                0xF1C40F,
                "👤 Jogador",
                safePlayer,
                "🏆 Posição",
                medal + " #" + rank,
                "⭐ Pontos",
                points.ToString(),
                "📜 Registro",
                safeReason,
                "🌟 Seu nome agora ecoa nos salões de Glitnir."
            );

            PostDiscordWebhook(json);
        }

        private void SendDiscordTop3LostWebhook(string playerName, int oldRank, int newRank, int points, string reason)
        {
            if (_cfgDiscordTop3WebhookUrl == null || string.IsNullOrWhiteSpace(_cfgDiscordTop3WebhookUrl.Value))
                return;

            string safePlayer = string.IsNullOrWhiteSpace(playerName) ? "Jogador" : playerName.Trim();
            string safeReason = string.IsNullOrWhiteSpace(reason) ? "Movimento no ranking" : reason.Trim();
            string newRankText = newRank == int.MaxValue ? "Fora do ranking" : "#" + newRank;

            string json = BuildDiscordTop3EmbedJson(
                "🏛️ ORÁCULO DO RANKING",
                "⚠️ Um campeão perdeu seu lugar entre os três maiores.",
                0xE74C3C,
                "👤 Jogador",
                safePlayer,
                "📉 Mudança",
                "#" + oldRank + " → " + newRankText,
                "⭐ Pontos",
                points.ToString(),
                "📜 Registro",
                safeReason,
                "⏳ Os salões de Glitnir aguardam sua retomada."
            );

            PostDiscordWebhook(json);
        }

        private void SendDiscordTop3DroppedWebhook(string playerName, int oldRank, int newRank, int points, string reason)
        {
            if (_cfgDiscordTop3WebhookUrl == null || string.IsNullOrWhiteSpace(_cfgDiscordTop3WebhookUrl.Value))
                return;

            string safePlayer = string.IsNullOrWhiteSpace(playerName) ? "Jogador" : playerName.Trim();
            string safeReason = string.IsNullOrWhiteSpace(reason) ? "Movimento no ranking" : reason.Trim();

            string json = BuildDiscordTop3EmbedJson(
                "🏛️ ORÁCULO DO RANKING",
                "⚖️ A balança da honra se moveu dentro do Top 3.",
                0x3498DB,
                "👤 Jogador",
                safePlayer,
                "📉 Posição",
                "#" + oldRank + " → #" + newRank,
                "⭐ Pontos",
                points.ToString(),
                "📜 Registro",
                safeReason,
                "🔥 A disputa pelo topo continua."
            );

            PostDiscordWebhook(json);
        }

        private string BuildDiscordTop3EmbedJson(
            string title,
            string description,
            int color,
            string field1Name,
            string field1Value,
            string field2Name,
            string field2Value,
            string field3Name,
            string field3Value,
            string field4Name,
            string field4Value,
            string footerText)
        {
            string timestamp = DateTime.UtcNow.ToString("o");
            string podium = BuildDiscordTop3PodiumValue();

            return
                "{" +
                "\"username\":\"Glitnir Ranking\"," +
                "\"embeds\":[{" +
                    "\"title\":\"" + EscapeJson(title) + "\"," +
                    "\"description\":\"" + EscapeJson(description) + "\"," +
                    "\"color\":" + color + "," +
                    "\"fields\":[" +
                        "{\"name\":\"🏆 Pódio de Glitnir\",\"value\":\"" + EscapeJson(podium) + "\",\"inline\":false}," +
                        "{\"name\":\"" + EscapeJson(field1Name) + "\",\"value\":\"" + EscapeJson(field1Value) + "\",\"inline\":true}," +
                        "{\"name\":\"" + EscapeJson(field2Name) + "\",\"value\":\"" + EscapeJson(field2Value) + "\",\"inline\":true}," +
                        "{\"name\":\"" + EscapeJson(field3Name) + "\",\"value\":\"" + EscapeJson(field3Value) + "\",\"inline\":true}," +
                        "{\"name\":\"" + EscapeJson(field4Name) + "\",\"value\":\"" + EscapeJson(field4Value) + "\",\"inline\":false}" +
                    "]," +
                    "\"footer\":{\"text\":\"" + EscapeJson(footerText) + "\"}," +
                    "\"timestamp\":\"" + EscapeJson(timestamp) + "\"" +
                "}]" +
                "}";
        }

        private string BuildDiscordTop3PodiumValue()
        {
            try
            {
                if (_database == null || _database.Entries == null)
                    return "Nenhum nome registrado no pódio ainda.";

                List<RankingEntry> top3 = _database.Entries
                    .Where(x => x != null && !string.IsNullOrWhiteSpace(x.PlayerName) && !ShouldIgnorePlayerForRanking(x.PlayerName))
                    .OrderByDescending(x => x.Points)
                    .ThenByDescending(x => x.BossPointsTotal)
                    .ThenByDescending(x => x.KillPointsTotal)
                    .ThenByDescending(x => x.SkillPointsTotal)
                    .ThenBy(x => x.PlayerName)
                    .Take(3)
                    .ToList();

                if (top3.Count == 0)
                    return "Nenhum nome registrado no pódio ainda.";

                StringBuilder sb = new StringBuilder();

                for (int i = 0; i < top3.Count; i++)
                {
                    RankingEntry entry = top3[i];
                    string medal = i == 0 ? "🥇" : i == 1 ? "🥈" : "🥉";
                    string name = string.IsNullOrWhiteSpace(entry.PlayerName) ? "Jogador" : entry.PlayerName.Trim();

                    sb.Append(medal)
                      .Append(" **#")
                      .Append(i + 1)
                      .Append("** — ")
                      .Append(name)
                      .Append("\n")
                      .Append("⭐ **")
                      .Append(entry.Points)
                      .Append(" pts**");

                    if (i < top3.Count - 1)
                        sb.Append("\n\n");
                }

                return sb.ToString();
            }
            catch (Exception ex)
            {
                Log.LogWarning("Erro ao montar pódio do webhook: " + ex.Message);
                return "Pódio indisponível no momento.";
            }
        }

        private void PostDiscordWebhook(string json)
        {
            string url = _cfgDiscordTop3WebhookUrl != null ? _cfgDiscordTop3WebhookUrl.Value : "";
            if (string.IsNullOrWhiteSpace(url))
                return;

            ThreadPool.QueueUserWorkItem(_ =>
            {
                try
                {
                    byte[] bodyRaw = Encoding.UTF8.GetBytes(json);

                    HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
                    request.Method = "POST";
                    request.ContentType = "application/json";
                    request.ContentLength = bodyRaw.Length;
                    request.Timeout = 10000;

                    using (Stream stream = request.GetRequestStream())
                    {
                        stream.Write(bodyRaw, 0, bodyRaw.Length);
                    }

                    using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
                    {
                        int statusCode = (int)response.StatusCode;
                        if (statusCode < 200 || statusCode >= 300)
                            Log.LogWarning("Webhook Top 3 Discord retornou status: " + statusCode);
                    }
                }
                catch (Exception ex)
                {
                    Log.LogWarning("Webhook Top 3 Discord falhou: " + ex.Message);
                }
            });
        }

        private string EscapeJson(string value)
        {
            if (string.IsNullOrEmpty(value))
                return "";

            return value
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"")
                .Replace("\r", "\\r")
                .Replace("\n", "\\n");
        }

        private string SafeLimit(string value, int maxLength)
        {
            if (string.IsNullOrEmpty(value))
                return "";

            value = value.Replace("\r", " ").Replace("\n", " ").Replace("\t", " ").Trim();
            if (value.Length > maxLength)
                value = value.Substring(0, maxLength);

            return value;
        }

        private string SafeKey(string value)
        {
            value = SafeLimit(value, 128);
            value = value.Replace("|", "/");
            return value;
        }

        private string SafeReason(string reason)
        {
            reason = SafeLimit(reason, MaxReasonLength);
            reason = reason.Replace("|", "/");
            return reason;
        }

        private string SanitizePlayerName(string playerName)
        {
            playerName = SafeLimit(playerName, MaxPlayerNameLength);
            playerName = playerName.Replace("|", "/");
            if (string.IsNullOrWhiteSpace(playerName))
                return "Jogador";
            return playerName;
        }

        private RankingEntry GetOrCreateEntry(string playerName)
        {
            string safeName = SanitizePlayerName(playerName);
            RankingEntry entry = FindRankingEntryByName(safeName);
            if (entry != null)
                return entry;

            entry = new RankingEntry
            {
                PlayerName = safeName,
                Points = 0,
                LastReason = "",
                LastUpdateUtc = DateTime.UtcNow.ToString("O"),
                TotalSkillLevelUpsPontuados = 0,
                SkillPointsTotal = 0,
                BossCredits = new HashSet<string>(StringComparer.OrdinalIgnoreCase),
                TotalDeaths = 0,
                DeathPenaltyPointsTotal = 0
            };

            _database.Entries.Add(entry);
            RegisterRankingEntry(entry);
            return entry;
        }

        private string GetLocalPlayerName()
        {
            try
            {
                if (Player.m_localPlayer != null)
                    return SanitizePlayerName(Player.m_localPlayer.GetPlayerName());
            }
            catch { }

            return "Jogador";
        }


        private long GetServerPeerUid()
        {
            try
            {
                if (ZNet.instance == null)
                    return 0L;

                if (ZNet.instance.IsServer())
                    return ZDOMan.GetSessionID();

                ZNetPeer serverPeer = ZNet.instance.GetServerPeer();
                if (serverPeer != null)
                    return serverPeer.m_uid;
            }
            catch (Exception ex)
            {
                Logger.LogWarning("[Ranking] Falha ao obter peer do servidor: " + ex.Message);
            }

            return 0L;
        }

        public string GetPrefabName(GameObject go)
        {
            if (go == null)
                return "";

            try
            {
                string name = go.name;
                if (string.IsNullOrWhiteSpace(name))
                    return "";
                int idx = name.IndexOf('(');
                if (idx > 0)
                    name = name.Substring(0, idx);
                return SafeKey(name.Trim());
            }
            catch { }

            return "";
        }


        private string GetZdoKey(GameObject go)
        {
            if (go == null)
                return "";

            try
            {
                ZNetView nview = go.GetComponent<ZNetView>();
                if (nview != null && nview.GetZDO() != null)
                {
                    ZDO zdo = nview.GetZDO();
                    return zdo.m_uid.UserID + ":" + zdo.m_uid.ID;
                }
            }
            catch { }

            return "";
        }

        private string GetZdoKey(Character character)
        {
            if (character == null)
                return "";

            try
            {
                ZDOID zdo = character.GetZDOID();
                if (zdo.IsNone())
                    return "";

                return zdo.UserID + ":" + zdo.ID;
            }
            catch { }

            try
            {
                ZNetView nview = character.GetComponent<ZNetView>();
                if (nview != null && nview.GetZDO() != null)
                {
                    ZDO zdo = nview.GetZDO();
                    return zdo.m_uid.UserID + ":" + zdo.m_uid.ID;
                }
            }
            catch { }

            return "";
        }


        private bool IsLocalPlayerSkillsInstance(Skills skills)
        {
            if (skills == null || Player.m_localPlayer == null)
                return false;

            try
            {
                Player owner = Traverse.Create(skills).Field("m_player").GetValue<Player>();
                if (owner != null && owner == Player.m_localPlayer)
                    return true;
            }
            catch { }

            return false;
        }

        private float GetSafeSkillLevel(Skills skills, Skills.SkillType skillType)
        {
            try
            {
                if (skills != null)
                    return Mathf.Clamp(skills.GetSkillLevel(skillType), 0f, 100f);
            }
            catch { }

            return 0f;
        }

        private int GetEffectiveSkillLevel(float rawLevel)
        {
            return Mathf.Clamp(Mathf.FloorToInt(rawLevel + 0.0001f), 0, 100);
        }

        private bool IsTrackableSkill(Skills.SkillType skillType)
        {
            string skillKey = GetSkillKey(skillType);
            if (string.IsNullOrWhiteSpace(skillKey))
                return false;

            if (string.Equals(skillKey, "None", StringComparison.OrdinalIgnoreCase))
                return false;

            if (string.Equals(skillKey, "All", StringComparison.OrdinalIgnoreCase))
                return false;

            return true;
        }

        private string GetSkillKey(Skills.SkillType skillType)
        {
            return SafeKey(skillType.ToString());
        }

        private string GetSkillDisplayName(string skillKey)
        {
            skillKey = SafeKey(skillKey);

            switch (skillKey)
            {
                case "WoodCutting": return "Wood Cutting";
                case "BloodMagic": return "Blood Magic";
                case "ElementalMagic": return "Elemental Magic";
                default:
                    StringBuilder sb = new StringBuilder(skillKey.Length + 8);
                    for (int i = 0; i < skillKey.Length; i++)
                    {
                        char ch = skillKey[i];
                        if (i > 0 && char.IsUpper(ch) && char.IsLetter(skillKey[i - 1]))
                            sb.Append(' ');
                        sb.Append(ch);
                    }
                    return sb.ToString().Trim();
            }
        }

        public float CaptureLocalSkillLevelBeforeRaise(Skills skills, Skills.SkillType skillType)
        {
            if (skills == null || !IsTrackableSkill(skillType))
                return 0f;

            if (!IsLocalPlayerSkillsInstance(skills))
                return 0f;

            return GetSafeSkillLevel(skills, skillType);
        }

        public void NotifyLocalSkillGain(Skills skills, Skills.SkillType skillType, float oldRawLevel)
        {
            try
            {
                if (skills == null)
                    return;

                if (!IsTrackableSkill(skillType))
                    return;

                if (Player.m_localPlayer == null)
                    return;

                if (!IsLocalPlayerSkillsInstance(skills))
                    return;

                float newRawLevel = GetSafeSkillLevel(skills, skillType);
                int oldLevel = GetEffectiveSkillLevel(oldRawLevel);
                int newLevel = GetEffectiveSkillLevel(newRawLevel);
                int gainedLevels = Mathf.Clamp(newLevel - oldLevel, 0, 100);

                if (gainedLevels <= 0)
                    return;

                string skillKey = GetSkillKey(skillType);
                if (string.IsNullOrWhiteSpace(skillKey))
                    return;

                if (IsServerInstance())
                {
                    ProcessSkillGainReport(GetLocalPlayerName(), skillKey, gainedLevels, oldLevel, newLevel, "local-host");
                    return;
                }

                long serverPeerUid = GetServerPeerUid();
                if (serverPeerUid == 0L || !_rpcsRegistered || ZRoutedRpc.instance == null)
                    return;

                ZPackage pkg = new ZPackage();
                pkg.Write(skillKey);
                pkg.Write(gainedLevels);
                pkg.Write(oldLevel);
                pkg.Write(newLevel);
                ZRoutedRpc.instance.InvokeRoutedRPC(serverPeerUid, RpcReportSkillGain, pkg);

                DebugLog(DebugCategory.Skill, "Cliente reportando SKILL: player=" + GetLocalPlayerName() +
                    " skill=" + skillKey +
                    " gainedLevels=" + gainedLevels +
                    " oldLevel=" + oldLevel +
                    " newLevel=" + newLevel);
            }
            catch (Exception ex)
            {
                Logger.LogError("Erro ao reportar ganho de skill local do ranking: " + ex);
            }
        }

        private void RPC_ReportSkillGain(long sender, ZPackage pkg)
        {
            try
            {
                if (!IsServerInstance() || pkg == null)
                    return;

                string skillKey = SafeKey(pkg.ReadString());
                int gainedLevels = Mathf.Clamp(pkg.ReadInt(), 0, 100);
                int oldLevel = Mathf.Clamp(pkg.ReadInt(), 0, 100);
                int newLevel = Mathf.Clamp(pkg.ReadInt(), 0, 100);
                string playerName = ResolvePlayerNameFromSender(sender);

                DebugLog(DebugCategory.Skill, "Servidor recebeu SKILL: sender=" + sender +
                    " player=" + playerName +
                    " skill=" + skillKey +
                    " gainedLevels=" + gainedLevels +
                    " oldLevel=" + oldLevel +
                    " newLevel=" + newLevel);

                if (string.IsNullOrWhiteSpace(playerName) || string.IsNullOrWhiteSpace(skillKey) || gainedLevels <= 0)
                    return;

                if (ShouldIgnoreSenderForRanking(sender, playerName))
                {
                    DebugLog(DebugCategory.Skill, "Skill ignorada para admin: " + playerName);
                    return;
                }

                ProcessSkillGainReport(playerName, skillKey, gainedLevels, oldLevel, newLevel, "rpc-skill");
            }
            catch (Exception ex)
            {
                Logger.LogError("Erro no RPC_ReportSkillGain: " + ex);
            }
        }

        private void ProcessSkillGainReport(string playerName, string skillKey, int gainedLevels, int oldLevel, int newLevel, string source)
        {
            try
            {
                if (!IsServerInstance() || !_rules.RankingEnabled || !_rules.EnableSkillPoints)
                    return;

                playerName = SanitizePlayerName(playerName);
                skillKey = SafeKey(skillKey);
                gainedLevels = Mathf.Clamp(gainedLevels, 0, 100);

                if (string.IsNullOrWhiteSpace(playerName) || string.IsNullOrWhiteSpace(skillKey) || gainedLevels <= 0)
                    return;

                if (ShouldIgnorePlayerForRanking(playerName))
                {
                    DebugLog(DebugCategory.Skill, "Skill ignorada para admin: " + playerName);
                    return;
                }

                RankingEntry entry = GetOrCreateEntry(playerName);

                int milestoneTotalPoints = 0;
                int milestoneCredits = 0;
                List<int> creditedMilestones = new List<int>();

                Dictionary<int, int> milestoneMap = null;
                if (_rules.SkillMilestoneJackpotPoints != null)
                    _rules.SkillMilestoneJackpotPoints.TryGetValue(skillKey, out milestoneMap);

                if (milestoneMap != null && milestoneMap.Count > 0)
                {
                    foreach (KeyValuePair<int, int> milestone in milestoneMap.OrderBy(p => p.Key))
                    {
                        int milestoneLevel = Mathf.Clamp(milestone.Key, 1, 100);
                        int milestonePoints = Mathf.Max(0, milestone.Value);

                        if (oldLevel < milestoneLevel && newLevel >= milestoneLevel)
                        {
                            string milestoneKey = skillKey + ":" + milestoneLevel;
                            if (!entry.SkillLevel100JackpotCredits.Contains(milestoneKey))
                            {
                                entry.SkillLevel100JackpotCredits.Add(milestoneKey);
                                milestoneCredits++;
                                creditedMilestones.Add(milestoneLevel);
                                milestoneTotalPoints = Mathf.Clamp(milestoneTotalPoints + milestonePoints, -int.MaxValue, int.MaxValue);
                            }
                        }
                    }

                    if (milestoneTotalPoints > 0)
                    {
                        entry.TotalSkillLevelUpsPontuados = Mathf.Clamp(entry.TotalSkillLevelUpsPontuados + milestoneCredits, 0, int.MaxValue);
                        entry.SkillPointsTotal = Mathf.Clamp(entry.SkillPointsTotal + milestoneTotalPoints, -int.MaxValue, int.MaxValue);

                        string jackpotReason = "Jackpot Skill: " + GetSkillDisplayName(skillKey) + " " + string.Join("/", creditedMilestones.Select(l => l.ToString()).ToArray());
                        ApplyPointsToEntry(entry, milestoneTotalPoints, jackpotReason);
                        return;
                    }
                }

                DebugLog(DebugCategory.Skill, "Skill ignorada fora dos jackpots de marco: player=" + playerName +
                    " skill=" + skillKey +
                    " oldLevel=" + oldLevel +
                    " newLevel=" + newLevel +
                    " source=" + source);
                return;
            }
            catch (Exception ex)
            {
                Logger.LogError("Erro ao processar skill reportado: " + ex);
            }
        }


        private string FormatHudLastUpdate(string rawUtc)
        {
            if (string.IsNullOrWhiteSpace(rawUtc))
                return "sem registro";

            try
            {
                DateTime dt = DateTime.Parse(rawUtc, null, System.Globalization.DateTimeStyles.RoundtripKind);
                return dt.ToLocalTime().ToString("dd/MM/yyyy HH:mm");
            }
            catch
            {
                return rawUtc;
            }
        }
        private string FormatIntRulesForHud(Dictionary<string, int> rules)
        {
            if (rules == null || rules.Count == 0)
                return "";

            return string.Join(",", rules
                .OrderBy(x => x.Key)
                .Select(x => SafeKey(x.Key) + ":" + x.Value)
                .ToArray());
        }

        private string FormatCategorizedIntRulesForHud(Dictionary<string, int> rules, Dictionary<string, string> manualCategories = null, bool combatMode = false)
        {
            if (rules == null || rules.Count == 0)
                return "";

            List<string> parts = new List<string>();
            foreach (IGrouping<string, KeyValuePair<string, int>> group in rules
                .OrderBy(x => GetHudRuleCategoryOrder(x.Key, manualCategories, combatMode))
                .ThenBy(x => combatMode ? SafeKey(x.Key) : FriendlyRuleNameForHud(x.Key))
                .GroupBy(x => GetHudRuleCategoryName(x.Key, manualCategories, combatMode)))
            {
                parts.Add(BuildHudCategoryHeader(group.Key));

                foreach (KeyValuePair<string, int> rule in group)
                {
                    string label = combatMode ? SafeKey(rule.Key) : rule.Key;
                    parts.Add(label + ":" + rule.Value);
                }
            }

            return string.Join(",", parts.ToArray());
        }

        private string FormatCategorizedJackpotRulesForHud(Dictionary<string, JackpotRule> rules, Dictionary<string, string> manualCategories = null, bool combatMode = false)
        {
            if (rules == null || rules.Count == 0)
                return "";

            List<string> parts = new List<string>();
            foreach (IGrouping<string, KeyValuePair<string, JackpotRule>> group in rules
                .OrderBy(x => GetHudRuleCategoryOrder(x.Key, manualCategories, combatMode))
                .ThenBy(x => FriendlyRuleNameForHud(x.Key))
                .GroupBy(x => GetHudRuleCategoryName(x.Key, manualCategories, combatMode)))
            {
                parts.Add(BuildHudCategoryHeader(group.Key));

                foreach (KeyValuePair<string, JackpotRule> entry in group)
                {
                    JackpotRule rule = entry.Value ?? new JackpotRule();
                    parts.Add(entry.Key + ":" + Mathf.Max(1, rule.RequiredAmount) + ":" + rule.Points);
                }
            }

            return string.Join(",", parts.ToArray());
        }

        private string BuildHudCategoryHeader(string categoryName)
        {
            return "__GLITNIR_CAT__|" + (string.IsNullOrWhiteSpace(categoryName) ? "Outros" : categoryName.Trim());
        }

        private bool IsHudCategoryHeader(string row)
        {
            return !string.IsNullOrWhiteSpace(row) && row.StartsWith("__GLITNIR_CAT__|", StringComparison.Ordinal);
        }

        private string GetHudCategoryHeaderTitle(string row)
        {
            if (!IsHudCategoryHeader(row))
                return "";

            return row.Substring("__GLITNIR_CAT__|".Length).Trim();
        }


        private string GetHudRuleCategoryName(string prefabName, Dictionary<string, string> manualCategories, bool combatMode)
        {
            string manual;
            if (manualCategories != null && manualCategories.TryGetValue(SafeKey(prefabName), out manual) && !string.IsNullOrWhiteSpace(manual))
                return NormalizeHudCategoryName(manual.Trim(), prefabName, combatMode);

            return combatMode ? GetHudCombatCategoryName(prefabName) : GetHudPrefabCategoryName(prefabName);
        }

        private string NormalizeHudCategoryName(string category, string prefabName, bool combatMode)
        {
            if (combatMode || string.IsNullOrWhiteSpace(category))
                return category;

            string c = category.Trim();

            if (c.Equals("Arcos e Munições", StringComparison.OrdinalIgnoreCase) ||
                c.Equals("Arcos e munições", StringComparison.OrdinalIgnoreCase) ||
                c.Equals("Armas e escudos", StringComparison.OrdinalIgnoreCase))
            {
                return GetHudPrefabCategoryName(prefabName);
            }

            return c;
        }

        private int GetHudRuleCategoryOrder(string prefabName, Dictionary<string, string> manualCategories, bool combatMode)
        {
            string category = GetHudRuleCategoryName(prefabName, manualCategories, combatMode);
            return combatMode ? GetHudCombatCategoryOrder(category) : GetHudProductionCategoryOrder(category);
        }

        private int GetHudProductionCategoryOrder(string category)
        {
            if (category == "Armas, armaduras e munições") return 10;
            if (category == "Armaduras") return 10;
            if (category == "Armas") return 10;
            if (category == "Escudos") return 10;
            if (category == "Munições") return 10;
            if (category == "Comidas") return 40;
            if (category == "Comidas e bebidas") return 40;
            if (category == "Ferramentas") return 50;
            if (category == "Materiais") return 60;
            if (category == "Troféus") return 70;
            if (category == "Cultivo") return 80;
            return 999;
        }

        private int GetHudCombatCategoryOrder(string category)
        {
            if (category == "Prados") return 10;
            if (category == "Floresta Negra") return 20;
            if (category == "Pântano") return 30;
            if (category == "Montanha") return 40;
            if (category == "Planícies") return 50;
            if (category == "Oceano") return 60;
            if (category == "Mistlands") return 70;
            if (category == "Ashlands") return 80;
            if (category == "Especiais" || category == "Criaturas Especiais") return 90;
            return 999;
        }

        private string GetHudCombatCategoryName(string prefabName)
        {
            string key = SafeKey(prefabName).ToLowerInvariant().Replace("_", "").Replace("-", "").Replace(" ", "");

            if (key.Contains("boar") || key.Contains("deer") || key.Contains("greyling") || key.Contains("neck") || key.Contains("crow") || key.Contains("hen") || key.Contains("chicken"))
                return "Prados";
            if (key.Contains("greydwarf") || key.Contains("skeleton") || key.Contains("ghost") || key.Contains("troll"))
                return "Floresta Negra";
            if (key.Contains("blob") || key.Contains("abomination") || key.Contains("draugr") || key.Contains("leech") || key.Contains("surtling") || key.Contains("wraith"))
                return "Pântano";
            if (key.Contains("hatchling") || key.Contains("fenring") || key.Contains("stonegolem") || key.Contains("ulv") || key.Contains("wolf") || key.Contains("bat"))
                return "Montanha";
            if (key.Contains("deathsquito") || key.Contains("goblin") || key.Contains("fuling") || key.Contains("lox"))
                return "Planícies";
            if (key.Contains("serpent") || key.Contains("bonemaw") || key.Contains("leviathan") || key.Contains("seagal"))
                return "Oceano";
            if (key.Contains("seeker") || key.Contains("gjall") || key.Contains("tick") || key.Contains("hare") || key.Contains("dverger"))
                return "Mistlands";
            if (key.Contains("asksvin") || key.Contains("charred") || key.Contains("morgen") || key.Contains("valkyrie") || key.Contains("volture") || key.Contains("tentaroot") || key.Contains("bloblava"))
                return "Ashlands";
            if (key.Contains("haldor") || key.Contains("hildir") || key.Contains("odin") || key.Contains("wisp") || key.Contains("trainingdummy"))
                return "Especiais";

            return "Outros";
        }

        private int GetHudPrefabCategoryOrder(string prefabName)
        {
            string category = GetHudPrefabCategoryName(prefabName);
            if (category == "Armaduras") return 10;
            if (category == "Armas") return 20;
            if (category == "Escudos") return 25;
            if (category == "Munições") return 30;
            if (category == "Comidas") return 40;
            if (category == "Comidas e bebidas") return 40;
            if (category == "Ferramentas") return 50;
            if (category == "Materiais") return 60;
            if (category == "Troféus") return 70;
            if (category == "Cultivo") return 80;
            return 999;
        }

        private string GetHudPrefabCategoryName(string prefabName)
        {
            string key = SafeKey(prefabName).ToLowerInvariant().Replace("_", "").Replace("-", "").Replace(" ", "");

            if (key.Contains("helmet") || key.Contains("armor") || key.Contains("cape") || key.Contains("chest") || key.Contains("legs") || key.Contains("greaves"))
                return "Armaduras";

            if (key.Contains("shield"))
                return "Escudos";

            if (key.Contains("arrow") || key.Contains("bolt") || key.Contains("bomb") || key.Contains("oozebomb") || key.Contains("missile"))
                return "Munições";

            if (key.Contains("sword") || key.Contains("knife") || key.Contains("axe") || key.Contains("mace") || key.Contains("spear") || key.Contains("atgeir") ||
                key.Contains("bow") || key.Contains("crossbow") || key.Contains("staff") || key.Contains("club") || key.Contains("pickaxe"))
                return "Armas";

            if (key.Contains("stew") || key.Contains("soup") || key.Contains("mead") || key.Contains("wine") || key.Contains("cooked") || key.Contains("jerky") ||
                key.Contains("honey") || key.Contains("chicken") || key.Contains("fish") || key.Contains("meat") || key.Contains("mushroom") || key.Contains("bread") ||
                key.Contains("pie") || key.Contains("pudding") || key.Contains("sausages") || key.Contains("eyescream") || key.Contains("salad") || key.Contains("omelette") ||
                key.Contains("seekeraspic") || key.Contains("wolfskewer") || key.Contains("mincemeatsauce") || key.Contains("bloodpudding"))
                return "Comidas e bebidas";

            if (key.Contains("hoe") || key.Contains("hammer") || key.Contains("cultivator") || key.Contains("fishingrod") || key.Contains("torch") || key.Contains("wishbone"))
                return "Ferramentas";

            if (key.Contains("trophy"))
                return "Troféus";

            if (key.Contains("seed") || key.Contains("sapling") || key.Contains("barley") || key.Contains("flax") || key.Contains("carrot") || key.Contains("turnip") ||
                key.Contains("onion") || key.Contains("jotunpuffs") || key.Contains("magecap") || key.Contains("vineberry"))
                return "Cultivo";

            if (key.Contains("wood") || key.Contains("stone") || key.Contains("ore") || key.Contains("metal") || key.Contains("bar") || key.Contains("ingot") ||
                key.Contains("hide") || key.Contains("leather") || key.Contains("scale") || key.Contains("chitin") || key.Contains("thread") || key.Contains("linen") ||
                key.Contains("resin") || key.Contains("coal") || key.Contains("core") || key.Contains("crystal") || key.Contains("bone") || key.Contains("fang") ||
                key.Contains("feather") || key.Contains("needle") || key.Contains("claw") || key.Contains("carapace") || key.Contains("mandible") || key.Contains("bloodbag"))
                return "Materiais";

            return "Outros";
        }

        private string FormatJackpotRulesForHud(Dictionary<string, JackpotRule> rules)
        {
            if (rules == null || rules.Count == 0)
                return "";

            return string.Join(",", rules
                .OrderBy(x => x.Key)
                .Select(x =>
                {
                    JackpotRule rule = x.Value ?? new JackpotRule();
                    return SafeKey(x.Key) + ":" + Mathf.Max(1, rule.RequiredAmount) + ":" + rule.Points;
                })
                .ToArray());
        }

        private string FriendlyRuleNameForHud(string rawKey)
        {
            string key = SafeKey(rawKey);
            if (string.IsNullOrWhiteSpace(key))
                return "Regra";






            string prefabDisplayName = GetPrefabDisplayNameForHud(key);
            if (!string.IsNullOrWhiteSpace(prefabDisplayName) &&
                !string.Equals(prefabDisplayName, key, StringComparison.OrdinalIgnoreCase))
                return prefabDisplayName;

            string bossDisplayName;
            if (BossDisplayNamesPtBr.TryGetValue(key, out bossDisplayName) && !string.IsNullOrWhiteSpace(bossDisplayName))
                return bossDisplayName;

            string normalizedBossName;
            if (BossPortugueseToPrefab.TryGetValue(key, out normalizedBossName) &&
                BossDisplayNamesPtBr.TryGetValue(normalizedBossName, out bossDisplayName) &&
                !string.IsNullOrWhiteSpace(bossDisplayName))
                return bossDisplayName;

            string fishDisplayName;
            if (FishDisplayNamesPtBr.TryGetValue(key, out fishDisplayName) && !string.IsNullOrWhiteSpace(fishDisplayName))
                return fishDisplayName;

            string normalizedFishName;
            if (FishPortugueseToPrefab.TryGetValue(key, out normalizedFishName) &&
                FishDisplayNamesPtBr.TryGetValue(normalizedFishName, out fishDisplayName) &&
                !string.IsNullOrWhiteSpace(fishDisplayName))
                return fishDisplayName;

            string friendly;
            if (HudFriendlyNames.TryGetValue(key, out friendly) && !string.IsNullOrWhiteSpace(friendly))
                return friendly;

            string manualName = TryManualPortugueseNameForHud(key);
            if (!string.IsNullOrWhiteSpace(manualName))
                return manualName;

            if (!string.IsNullOrWhiteSpace(prefabDisplayName))
                return prefabDisplayName;

            return HumanizePrefabNamePt(key);
        }

        private string GetPrefabDisplayNameForHud(string prefabName)
        {
            if (string.IsNullOrWhiteSpace(prefabName))
                return "";

            string cleanName = CleanPrefabNameForHud(prefabName);
            if (string.IsNullOrWhiteSpace(cleanName))
                return "";

            string cachedName;
            if (_hudPrefabDisplayNameCache.TryGetValue(cleanName, out cachedName) && !string.IsNullOrWhiteSpace(cachedName))
                return cachedName;

            string resolvedName = "";
            bool safeToCache = false;

            try
            {
                GameObject prefab = ResolvePrefabForHud(cleanName);
                if (prefab != null)
                {
                    resolvedName = TryGetLocalizedPrefabNameForHud(prefab, cleanName);
                    safeToCache = !string.IsNullOrWhiteSpace(resolvedName);
                }
            }
            catch (Exception ex)
            {
                if (_rules != null && _rules.DebugLogging)
                    Log.LogWarning("[Glitnir Ranking] Falha ao localizar nome do prefab '" + prefabName + "': " + ex.Message);
            }



            if (string.IsNullOrWhiteSpace(resolvedName))
            {
                string bossDisplayName;
                if (BossDisplayNamesPtBr.TryGetValue(cleanName, out bossDisplayName) && !string.IsNullOrWhiteSpace(bossDisplayName))
                {
                    resolvedName = bossDisplayName;
                    safeToCache = true;
                }
            }

            if (string.IsNullOrWhiteSpace(resolvedName))
            {
                string normalizedBossName;
                string bossDisplayName;
                if (BossPortugueseToPrefab.TryGetValue(cleanName, out normalizedBossName) &&
                    BossDisplayNamesPtBr.TryGetValue(normalizedBossName, out bossDisplayName) &&
                    !string.IsNullOrWhiteSpace(bossDisplayName))
                {
                    resolvedName = bossDisplayName;
                    safeToCache = true;
                }
            }

            if (string.IsNullOrWhiteSpace(resolvedName))
            {
                string fishDisplayName;
                if (FishDisplayNamesPtBr.TryGetValue(cleanName, out fishDisplayName) && !string.IsNullOrWhiteSpace(fishDisplayName))
                {
                    resolvedName = fishDisplayName;
                    safeToCache = true;
                }
            }

            if (string.IsNullOrWhiteSpace(resolvedName))
            {
                string normalizedFishName;
                string fishDisplayName;
                if (FishPortugueseToPrefab.TryGetValue(cleanName, out normalizedFishName) &&
                    FishDisplayNamesPtBr.TryGetValue(normalizedFishName, out fishDisplayName) &&
                    !string.IsNullOrWhiteSpace(fishDisplayName))
                {
                    resolvedName = fishDisplayName;
                    safeToCache = true;
                }
            }

            if (string.IsNullOrWhiteSpace(resolvedName))
            {
                string friendly;
                if (HudFriendlyNames.TryGetValue(cleanName, out friendly) && !string.IsNullOrWhiteSpace(friendly))
                {
                    resolvedName = friendly;
                    safeToCache = true;
                }
            }

            if (string.IsNullOrWhiteSpace(resolvedName))
            {
                string manualName = TryManualPortugueseNameForHud(cleanName);
                if (!string.IsNullOrWhiteSpace(manualName))
                {
                    resolvedName = manualName;
                    safeToCache = true;
                }
            }

            if (string.IsNullOrWhiteSpace(resolvedName))
            {



                return HumanizePrefabNamePt(cleanName);
            }

            if (safeToCache)
                _hudPrefabDisplayNameCache[cleanName] = resolvedName;

            return resolvedName;
        }


        private string TryGetLocalizedPrefabNameForHud(GameObject prefab, string fallbackPrefabName)
        {
            if (prefab == null)
                return "";

            string rawName = "";

            try
            {
                ItemDrop itemDrop = prefab.GetComponent<ItemDrop>();
                if (itemDrop != null &&
                    itemDrop.m_itemData != null &&
                    itemDrop.m_itemData.m_shared != null &&
                    !string.IsNullOrWhiteSpace(itemDrop.m_itemData.m_shared.m_name))
                {
                    rawName = itemDrop.m_itemData.m_shared.m_name;
                }

                if (string.IsNullOrWhiteSpace(rawName))
                {
                    Character character = prefab.GetComponent<Character>();
                    if (character != null && !string.IsNullOrWhiteSpace(character.m_name))
                        rawName = character.m_name;
                }

                if (string.IsNullOrWhiteSpace(rawName))
                {
                    Piece piece = prefab.GetComponent<Piece>();
                    if (piece != null && !string.IsNullOrWhiteSpace(piece.m_name))
                        rawName = piece.m_name;
                }

                if (string.IsNullOrWhiteSpace(rawName))
                {
                    Pickable pickable = prefab.GetComponent<Pickable>();
                    if (pickable != null && pickable.m_itemPrefab != null)
                    {
                        ItemDrop pickableItem = pickable.m_itemPrefab.GetComponent<ItemDrop>();
                        if (pickableItem != null &&
                            pickableItem.m_itemData != null &&
                            pickableItem.m_itemData.m_shared != null &&
                            !string.IsNullOrWhiteSpace(pickableItem.m_itemData.m_shared.m_name))
                        {
                            rawName = pickableItem.m_itemData.m_shared.m_name;
                        }
                    }
                }

                if (string.IsNullOrWhiteSpace(rawName))
                    return "";

                string localized = TryLocalizeTokenForHud(rawName);

                if (string.IsNullOrWhiteSpace(localized))
                    return "";

                localized = localized.Trim();



                if (localized.StartsWith("$", StringComparison.Ordinal))
                    localized = HumanizeTokenNameForHud(localized);

                if (string.IsNullOrWhiteSpace(localized) || localized == fallbackPrefabName)
                    return localized;

                return localized;
            }
            catch
            {
                return "";
            }
        }

        private GameObject ResolvePrefabForHud(string cleanName)
        {
            if (string.IsNullOrWhiteSpace(cleanName))
                return null;

            GameObject cachedPrefab;
            if (_hudPrefabObjectCache.TryGetValue(cleanName, out cachedPrefab))
                return cachedPrefab;

            RebuildHudPrefabObjectCacheIfNeeded();

            if (_hudPrefabObjectCache.TryGetValue(cleanName, out cachedPrefab))
                return cachedPrefab;

            GameObject prefab = null;


            if (ObjectDB.instance != null)
                prefab = ObjectDB.instance.GetItemPrefab(cleanName);

            if (prefab == null && ZNetScene.instance != null)
                prefab = ZNetScene.instance.GetPrefab(cleanName);

            if (prefab != null)
                _hudPrefabObjectCache[cleanName] = prefab;

            return prefab;
        }

        private void RebuildHudPrefabObjectCacheIfNeeded()
        {
            int objectDbCount = ObjectDB.instance != null && ObjectDB.instance.m_items != null ? ObjectDB.instance.m_items.Count : -1;
            int znetCount = ZNetScene.instance != null && ZNetScene.instance.m_prefabs != null ? ZNetScene.instance.m_prefabs.Count : -1;

            if (_hudObjectDbItemCountCached == objectDbCount && _hudZNetScenePrefabCountCached == znetCount && _hudPrefabObjectCache.Count > 0)
                return;

            _hudPrefabObjectCache.Clear();

            if (ObjectDB.instance != null && ObjectDB.instance.m_items != null)
            {
                for (int i = 0; i < ObjectDB.instance.m_items.Count; i++)
                {
                    GameObject item = ObjectDB.instance.m_items[i];
                    if (item == null || string.IsNullOrWhiteSpace(item.name))
                        continue;

                    if (!_hudPrefabObjectCache.ContainsKey(item.name))
                        _hudPrefabObjectCache[item.name] = item;
                }
            }

            if (ZNetScene.instance != null && ZNetScene.instance.m_prefabs != null)
            {
                for (int i = 0; i < ZNetScene.instance.m_prefabs.Count; i++)
                {
                    GameObject prefab = ZNetScene.instance.m_prefabs[i];
                    if (prefab == null || string.IsNullOrWhiteSpace(prefab.name))
                        continue;

                    if (!_hudPrefabObjectCache.ContainsKey(prefab.name))
                        _hudPrefabObjectCache[prefab.name] = prefab;
                }
            }

            _hudObjectDbItemCountCached = objectDbCount;
            _hudZNetScenePrefabCountCached = znetCount;
        }

        private string HumanizeTokenNameForHud(string tokenOrName)
        {
            if (string.IsNullOrWhiteSpace(tokenOrName))
                return "";

            string name = tokenOrName.Trim();

            if (name.StartsWith("$item_", StringComparison.OrdinalIgnoreCase))
                name = name.Substring(6);
            else if (name.StartsWith("$", StringComparison.Ordinal))
                name = name.Substring(1);

            return HumanizePrefabName(name);
        }

        private string CleanPrefabNameForHud(string prefabName)
        {
            if (string.IsNullOrWhiteSpace(prefabName))
                return "";

            string cleanName = prefabName.Trim();

            int cloneIndex = cleanName.IndexOf("(Clone)", StringComparison.OrdinalIgnoreCase);
            if (cloneIndex >= 0)
                cleanName = cleanName.Substring(0, cloneIndex).Trim();

            return cleanName;
        }

        private string TryLocalizeTokenForHud(string tokenOrName)
        {
            if (string.IsNullOrWhiteSpace(tokenOrName))
                return "";

            try
            {
                if (!_hudLocalizationReflectionReady)
                {
                    _hudLocalizationReflectionReady = true;


                    _hudLocalizationType = AccessTools.TypeByName("Localization");

                    if (_hudLocalizationType == null)
                    {
                        Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
                        for (int i = 0; i < assemblies.Length; i++)
                        {
                            Type[] types;
                            try
                            {
                                types = assemblies[i].GetTypes();
                            }
                            catch
                            {
                                continue;
                            }

                            for (int t = 0; t < types.Length; t++)
                            {
                                Type type = types[t];
                                if (type != null && type.Name == "Localization")
                                {
                                    _hudLocalizationType = type;
                                    break;
                                }
                            }

                            if (_hudLocalizationType != null)
                                break;
                        }
                    }

                    if (_hudLocalizationType != null)
                    {
                        _hudLocalizationInstanceField = AccessTools.Field(_hudLocalizationType, "instance");
                        _hudLocalizationInstanceProperty = AccessTools.Property(_hudLocalizationType, "instance");
                        _hudLocalizationLocalizeMethod = AccessTools.Method(_hudLocalizationType, "Localize", new Type[] { typeof(string) });

                        if (_hudLocalizationLocalizeMethod == null)
                        {
                            MethodInfo[] methods = _hudLocalizationType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
                            for (int i = 0; i < methods.Length; i++)
                            {
                                MethodInfo method = methods[i];
                                if (method == null || method.Name != "Localize")
                                    continue;

                                ParameterInfo[] parameters = method.GetParameters();
                                if (parameters.Length >= 1 && parameters[0].ParameterType == typeof(string))
                                {
                                    _hudLocalizationLocalizeMethod = method;
                                    break;
                                }
                            }
                        }
                    }
                }

                if (_hudLocalizationType == null || (_hudLocalizationInstanceField == null && _hudLocalizationInstanceProperty == null) || _hudLocalizationLocalizeMethod == null)
                    return tokenOrName;

                object instance = null;
                if (_hudLocalizationInstanceField != null)
                    instance = _hudLocalizationInstanceField.GetValue(null);
                if (instance == null && _hudLocalizationInstanceProperty != null)
                    instance = _hudLocalizationInstanceProperty.GetValue(null, null);
                if (instance == null)
                    return tokenOrName;

                ParameterInfo[] localizeParams = _hudLocalizationLocalizeMethod.GetParameters();
                object[] args = new object[localizeParams.Length];
                args[0] = tokenOrName;

                for (int i = 1; i < args.Length; i++)
                {
                    if (localizeParams[i].ParameterType == typeof(bool))
                        args[i] = true;
                    else
                        args[i] = localizeParams[i].DefaultValue;
                }

                object result = _hudLocalizationLocalizeMethod.Invoke(instance, args);
                return result as string ?? tokenOrName;
            }
            catch
            {
                return tokenOrName;
            }
        }

        private string TryManualPortugueseNameForHud(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
                return "";

            string clean = key.Trim();

            if (clean.StartsWith("$item_", StringComparison.OrdinalIgnoreCase))
                clean = clean.Substring(6);
            else if (clean.StartsWith("$", StringComparison.Ordinal))
                clean = clean.Substring(1);

            clean = clean.Replace("_", "").Replace("-", "").Replace(" ", "");

            string value;
            if (ManualPortuguesePrefabNames.TryGetValue(clean, out value))
                return value;

            return "";
        }

        private string HumanizePrefabNamePt(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
                return "Regra";

            string manualName = TryManualPortugueseNameForHud(key);
            if (!string.IsNullOrWhiteSpace(manualName))
                return manualName;

            string name = key.Trim();

            int cloneIndex = name.IndexOf("(Clone)", StringComparison.OrdinalIgnoreCase);
            if (cloneIndex >= 0)
                name = name.Substring(0, cloneIndex);


            name = System.Text.RegularExpressions.Regex.Replace(name, "([a-z])([A-Z])", "$1 $2");
            name = name.Replace("_", " ").Replace("-", " ").Trim();

            string lower = name.ToLowerInvariant();


            lower = lower.Replace("helmet", "elmo");
            lower = lower.Replace("armor", "armadura");
            lower = lower.Replace("chest", "peitoral");
            lower = lower.Replace("legs", "perneiras");
            lower = lower.Replace("greaves", "grevas");
            lower = lower.Replace("knife", "faca");
            lower = lower.Replace("sword", "espada");
            lower = lower.Replace("axe", "machado");
            lower = lower.Replace("mace", "maça");
            lower = lower.Replace("bow", "arco");
            lower = lower.Replace("shield", "escudo");
            lower = lower.Replace("spear", "lança");
            lower = lower.Replace("atgier", "atgeir");
            lower = lower.Replace("crossbow", "besta");


            lower = lower.Replace("bronze", "bronze");
            lower = lower.Replace("iron", "ferro");
            lower = lower.Replace("leather", "couro");
            lower = lower.Replace("rag", "trapos");
            lower = lower.Replace("padded", "acolchoado");
            lower = lower.Replace("root", "raiz");
            lower = lower.Replace("trollleather", "couro de troll");
            lower = lower.Replace("troll leather", "couro de troll");
            lower = lower.Replace("blackmetal", "metal negro");
            lower = lower.Replace("black metal", "metal negro");
            lower = lower.Replace("silver", "prata");
            lower = lower.Replace("copper", "cobre");
            lower = lower.Replace("flint", "sílex");
            lower = lower.Replace("carapace", "carapaça");
            lower = lower.Replace("flametal", "flametal");
            lower = lower.Replace("mage", "mago");
            lower = lower.Replace("fenris", "fenris");
            lower = lower.Replace("dverger", "dvergr");
            lower = lower.Replace("drake", "draco");

            while (lower.IndexOf("  ", StringComparison.Ordinal) >= 0)
                lower = lower.Replace("  ", " ");

            lower = lower.Trim();
            if (lower.Length == 0)
                return key;

            return char.ToUpperInvariant(lower[0]) + lower.Substring(1);
        }

        private static readonly Dictionary<string, string> ManualPortuguesePrefabNames =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "deerstew", "Ensopado de cervo" },
                { "honeysglazedchicken", "Frango glaceado com mel" },
                { "mushroomjotun", "Cogumelo Jotun" },

                { "helmetbronze", "Elmo de bronze" },
                { "helmetcarapace", "Elmo de carapaça" },
                { "helmetdrake", "Elmo de draco" },
                { "helmetdverger", "Tiara Dvergr" },
                { "helmetfenris", "Capuz de Fenris" },
                { "helmetiron", "Elmo de ferro" },
                { "helmetleather", "Elmo de couro" },
                { "helmetmage", "Capuz de mago" },
                { "helmetpadded", "Elmo acolchoado" },
                { "helmetroot", "Máscara de raiz" },
                { "helmettrollleather", "Capuz de couro de troll" },
                { "helmetflametal", "Elmo de flametal" },

                { "armorbronzechest", "Peitoral de bronze" },
                { "armorbronzelegs", "Perneiras de bronze" },
                { "armorcarapacechest", "Peitoral de carapaça" },
                { "armorcarapacelegs", "Perneiras de carapaça" },
                { "armorfenringchest", "Peitoral de Fenris" },
                { "armorfenringlegs", "Perneiras de Fenris" },
                { "armorironchest", "Peitoral de ferro" },
                { "armorironlegs", "Perneiras de ferro" },
                { "armorleatherchest", "Túnica de couro" },
                { "armorleatherlegs", "Calças de couro" },
                { "armormagechest", "Manto de mago" },
                { "armormagelegs", "Calças de mago" },
                { "armorpaddedcuirass", "Couraça acolchoada" },
                { "armorpaddedgreaves", "Grevas acolchoadas" },
                { "armorragschest", "Túnica de trapos" },
                { "armorragslegs", "Calças de trapos" },
                { "armorrootchest", "Armadura de raiz" },
                { "armorrootlegs", "Perneiras de raiz" },
                { "armortrollleatherchest", "Túnica de couro de troll" },
                { "armortrollleatherlegs", "Calças de couro de troll" },

                { "knifeblackmetal", "Faca de metal negro" },
                { "knifecopper", "Faca de cobre" },
                { "knifeflint", "Faca de sílex" },
                { "knifesilver", "Faca de prata" },


                { "swordiron", "Espada de ferro" },
                { "swordsilver", "Espada de prata" },
                { "swordblackmetal", "Espada de metal negro" },
                { "swordbronze", "Espada de bronze" },
                { "swordmistwalker", "Mistwalker" },
                { "swordflametal", "Espada de flametal" },
                { "axeiron", "Machado de ferro" },
                { "axebronze", "Machado de bronze" },
                { "axeblackmetal", "Machado de metal negro" },
                { "maceiron", "Maça de ferro" },
                { "macebronze", "Maça de bronze" },
                { "macesilver", "Frostner" },
                { "sledgeiron", "Marreta de ferro" },
                { "sledge_demolisher", "Demolidor" },
                { "bow", "Arco bruto" },
                { "bowfinewood", "Arco de madeira fina" },
                { "bowhuntsman", "Arco do caçador" },
                { "bowdraugrfang", "Presa de Draugr" },
                { "bowspine", "Estilhaçador de espinha" },
                { "crossbowarbalest", "Arbalest" },
                { "crossbowripper", "Ripper" },
                { "shieldwood", "Escudo de madeira" },
                { "shieldbronze buckler", "Broquel de bronze" },
                { "shieldironbuckler", "Broquel de ferro" },
                { "shieldblackmetal", "Escudo de metal negro" },
                { "shieldcarapace", "Escudo de carapaça" },
                { "shieldflametal", "Escudo de flametal" },
                { "atgierbronze", "Atgeir de bronze" },
                { "atgieriron", "Atgeir de ferro" },
                { "atgierblackmetal", "Atgeir de metal negro" },
                { "spearflint", "Lança de sílex" },
                { "spearbronze", "Lança de bronze" },
                { "spearwolffang", "Lança de presas" },
                { "carrot", "Cenoura" },
                { "turnip", "Nabo" },
                { "onion", "Cebola" },
                { "barley", "Cevada" },
                { "flax", "Linho" },
                { "sap", "Seiva" },
                { "jotunpuffs", "Cogumelo Jotun" },
                { "magecap", "Chapéu de Mago" },
                { "vineberry", "Baga de Videira" },
            };

        private string StripRichText(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return "";

            try
            {
                return System.Text.RegularExpressions.Regex.Replace(value, "<.*?>", "");
            }
            catch
            {
                return value;
            }
        }

        private string HumanizePrefabName(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
                return "Regra";

            string name = key.Trim();

            if (name.StartsWith("$", StringComparison.Ordinal))
                name = name.Substring(1);

            int cloneIndex = name.IndexOf("(Clone)", StringComparison.OrdinalIgnoreCase);
            if (cloneIndex >= 0)
                name = name.Substring(0, cloneIndex);

            name = name.Replace("_", " ").Replace("-", " ");
            while (name.IndexOf("  ", StringComparison.Ordinal) >= 0)
                name = name.Replace("  ", " ");

            name = name.Trim();

            if (name.Length == 0)
                return key;

            string[] words = name.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < words.Length; i++)
            {
                string word = words[i];
                if (word.Length <= 1)
                    words[i] = word.ToUpperInvariant();
                else
                    words[i] = char.ToUpperInvariant(word[0]) + word.Substring(1).ToLowerInvariant();
            }

            return string.Join(" ", words);
        }

        private static readonly Dictionary<string, string> BossDisplayNamesPtBr =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "Eikthyr", "Eikthyr" },
                { "gd_king", "O Ancião" },
                { "Bonemass", "Massa Óssea" },
                { "Dragon", "Moder" },
                { "GoblinKing", "Yagluth" },
                { "SeekerQueen", "Rainha Seeker" },
                { "Fader", "Fader" },
                { "Charred_Melee_Dyrnwyn", "Guerreiro Carbonizado Dyrnwyn" },
                { "Fenring_Cultist_Hildir", "Cultista Fenring da Hildir" },
                { "GoblinBruteBros", "Irmãos Berserker Fuling" },
                { "GoblinBrute_Hildir", "Berserker Fuling da Hildir" },
                { "GoblinShaman_Hildir", "Xamã Fuling da Hildir" },
                { "Skeleton_Hildir", "Esqueleto da Hildir" }
            };

        private static readonly Dictionary<string, string> BossPortugueseToPrefab =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "Eikthyr", "Eikthyr" },
                { "O Ancião", "gd_king" },
                { "Ancião", "gd_king" },
                { "O Anciao", "gd_king" },
                { "Anciao", "gd_king" },
                { "Massa Óssea", "Bonemass" },
                { "Massa Ossea", "Bonemass" },
                { "Bonemass", "Bonemass" },
                { "Moder", "Dragon" },
                { "Yagluth", "GoblinKing" },
                { "Rainha Seeker", "SeekerQueen" },
                { "Rainha dos Seeker", "SeekerQueen" },
                { "Seeker Queen", "SeekerQueen" },
                { "Fader", "Fader" },
                { "Guerreiro Carbonizado Dyrnwyn", "Charred_Melee_Dyrnwyn" },
                { "Dyrnwyn", "Charred_Melee_Dyrnwyn" },
                { "Cultista Fenring da Hildir", "Fenring_Cultist_Hildir" },
                { "Irmãos Berserker Fuling", "GoblinBruteBros" },
                { "Irmaos Berserker Fuling", "GoblinBruteBros" },
                { "Berserker Fuling da Hildir", "GoblinBrute_Hildir" },
                { "Xamã Fuling da Hildir", "GoblinShaman_Hildir" },
                { "Xama Fuling da Hildir", "GoblinShaman_Hildir" },
                { "Esqueleto da Hildir", "Skeleton_Hildir" }
            };

        private static readonly Dictionary<string, string> FishDisplayNamesPtBr =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "Fish1", "Perca" },
                { "Fish2", "Lúcio" },
                { "Fish3", "Atum" },
                { "Fish4_cave", "Tetra" },
                { "Fish5", "Peixe Troll" },
                { "Fish6", "Arenque gigante" },
                { "Fish7", "Garoupa" },
                { "Fish8", "Garoupa estrelada" },
                { "Fish9", "Peixe-pescador" },
                { "Fish10", "Salmão do norte" },
                { "Fish11", "Peixe magma" },
                { "Fish12", "Baiacu" }
            };

        private static readonly Dictionary<string, string> FishPortugueseToPrefab =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "Perca", "Fish1" },
                { "Lúcio", "Fish2" },
                { "Lucio", "Fish2" },
                { "Fish3", "Fish3" },
                { "Atum", "Fish3" },
                { "Tetra", "Fish4_cave" },
                { "Peixe Troll", "Fish5" },
                { "Arenque gigante", "Fish6" },
                { "Garoupa", "Fish7" },
                { "Garoupa estrelada", "Fish8" },
                { "Peixe-pescador", "Fish9" },
                { "Peixe pescador", "Fish9" },
                { "Salmão do norte", "Fish10" },
                { "Salmao do norte", "Fish10" },
                { "Peixe magma", "Fish11" },
                { "Baiacu", "Fish12" }
            };

        private static readonly Dictionary<string, string> HudFriendlyNames =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "Boar", "Javali" },
                { "Neck", "Nixe" },
                { "Greyling", "Greydwarf Jovem" },
                { "Greydwarf", "Greydwarf" },
                { "Greydwarf_Elite", "Greydwarf Elite" },
                { "Greydwarf_Shaman", "Xamã Greydwarf" },
                { "Troll", "Troll" },
                { "Skeleton", "Esqueleto" },
                { "Skeleton_Poison", "Esqueleto Venenoso" },
                { "Draugr", "Draugr" },
                { "Draugr_Elite", "Draugr Elite" },
                { "Blob", "Gosma" },
                { "BlobElite", "Gosma Elite" },
                { "Leech", "Sanguessuga" },
                { "Surtling", "Surtling" },
                { "Abomination", "Abominação" },
                { "Wolf", "Lobo" },
                { "Hatchling", "Draco" },
                { "Fenring", "Fenring" },
                { "StoneGolem", "Golem de Pedra" },
                { "Goblin", "Fuling" },
                { "GoblinArcher", "Fuling Arqueiro" },
                { "GoblinBrute", "Fuling Berserker" },
                { "GoblinShaman", "Xamã Fuling" },
                { "Lox", "Lox" },
                { "Deathsquito", "Mosquito da Morte" },
                { "Serpent", "Serpente Marinha" },
                { "Bat", "Morcego" },
                { "Ulv", "Ulv" },
                { "Hare", "Lebre" },
                { "Tick", "Carrapato" },
                { "Seeker", "Seeker" },
                { "SeekerBrute", "Seeker Brutamontes" },
                { "Gjall", "Gjall" },
                { "Dverger", "Dvergr" },
                { "Charred_Melee", "Carbonizado Guerreiro" },
                { "Charred_Archer", "Carbonizado Arqueiro" },
                { "Charred_Mage", "Carbonizado Mago" },
                { "Morgen", "Morgen" },
                { "Asksvin", "Asksvin" },
                { "Volture", "Abutre das Cinzas" },
                { "Eikthyr", "Eikthyr" },
                { "gd_king", "O Ancião" },
                { "Bonemass", "Massa Óssea" },
                { "Dragon", "Moder" },
                { "GoblinKing", "Yagluth" },
                { "SeekerQueen", "Rainha Seeker" },
                { "Fader", "Fader" },

                { "Fish1", "Perca" },
                { "Fish2", "Lúcio" },
                { "Fish3", "Atum" },
                { "Fish4_cave", "Tetra" },
                { "Fish5", "Peixe Troll" },
                { "Fish6", "Arenque gigante" },
                { "Fish7", "Garoupa" },
                { "Fish8", "Garoupa estrelada" },
                { "Fish9", "Peixe-pescador" },
                { "Fish10", "Salmão do norte" },
                { "Fish11", "Peixe magma" },
                { "Fish12", "Baiacu" },

                { "Carrot", "Cenoura" },
                { "Turnip", "Nabo" },
                { "Onion", "Cebola" },
                { "Barley", "Cevada" },
                { "Flax", "Linho" },
                { "Sap", "Seiva" },
                { "JotunPuffs", "Cogumelo Jotun" },
                { "Magecap", "Chapéu de Mago" },
                { "Vineberry", "Baga de Videira" },
                { "Fiddleheadfern", "Samambaia" },

                { "Run", "Corrida" },
                { "Jump", "Salto" },
                { "Swords", "Espadas" },
                { "Knives", "Facas" },
                { "Clubs", "Porretes" },
                { "Polearms", "Armas de Haste" },
                { "Spears", "Lanças" },
                { "Blocking", "Bloqueio" },
                { "Axes", "Machados" },
                { "Bows", "Arcos" },
                { "Crossbows", "Bestas" },
                { "ElementalMagic", "Magia Elemental" },
                { "BloodMagic", "Magia de Sangue" },
                { "Fishing", "Pesca" },
                { "Cooking", "Culinária" },


                { "Deer", "Cervo" },
                { "EvilHeart_Forest", "Coração Maligno da Floresta" },
                { "Ghost", "Fantasma" },
                { "Wraith", "Aparição" },
                { "Fenring_Cultist", "Cultista Fenring" },
                { "SeekerBrood", "Cria de Seeker" },
                { "DvergerMage", "Mago Dvergr" },
                { "DvergerMageFire", "Mago Dvergr de Fogo" },
                { "DvergerMageIce", "Mago Dvergr de Gelo" },
                { "DvergerMageSupport", "Mago Dvergr de Suporte" },
                { "DvergerRogue", "Ladino Dvergr" },
                { "Charred_Twitcher", "Carbonizado Retorcido" },
                { "Charred_Melee_Dyrnwyn", "Guerreiro Carbonizado Dyrnwyn" },
                { "GoblinBruteBros", "Irmãos Berserker Fuling" },
                { "GoblinBrute_Hildir", "Berserker Fuling da Hildir" },
                { "GoblinShaman_Hildir", "Xamã Fuling da Hildir" },
                { "Fenring_Cultist_Hildir", "Cultista Fenring da Hildir" },
                { "Skeleton_Hildir", "Esqueleto da Hildir" },
                { "ExploreBlackForest", "Explorador da Floresta Negra" },
                { "ExploreSwamp", "Explorador do Pântano" },
                { "ExploreMountain", "Explorador das Montanhas" },
                { "ExplorePlains", "Explorador das Planícies" },
                { "ExploreMistlands", "Explorador das Terras Nebulosas" },
                { "ExploreAshlands", "Explorador das Terras das Cinzas" },
                { "Kill100Enemies", "Exterminador" },
                { "CraftMaster", "Mestre Artesão" },
                { "Mapa 5%", "Mapa 5%" },
            };


        private string FormatExplorationMapJackpotRulesForHud(Dictionary<int, int> rules)
        {
            if (rules == null || rules.Count == 0)
                return "";

            return string.Join(";", rules
                .Where(p => p.Key > 0 && p.Value > 0)
                .OrderBy(p => p.Key)
                .Select(p => "Mapa " + Mathf.Clamp(p.Key, 1, 100) + "%:" + p.Value)
                .ToArray());
        }

        private string FormatDeathPenaltyRulesForHud(Dictionary<int, int> rules)
        {
            if (rules == null || rules.Count == 0)
                return "";

            return string.Join(",", rules
                .OrderBy(x => x.Key)
                .Select(x => Mathf.Max(1, x.Key) + ":" + Mathf.Max(0, x.Value))
                .ToArray());
        }

        private string FormatSkillJackpotRulesForHud(Dictionary<string, Dictionary<int, int>> rules)
        {
            if (rules == null || rules.Count == 0)
                return "";

            List<string> parts = new List<string>();
            foreach (KeyValuePair<string, Dictionary<int, int>> skill in rules.OrderBy(x => x.Key))
            {
                if (skill.Value == null)
                    continue;

                foreach (KeyValuePair<int, int> milestone in skill.Value.OrderBy(x => x.Key))
                    parts.Add(SafeKey(skill.Key) + ":" + Mathf.Max(1, milestone.Key) + ":" + milestone.Value);
            }

            return string.Join(",", parts.ToArray());
        }
    }

    [HarmonyPatch]
    internal static class RankingPatches
    {
        [HarmonyPostfix]
        [HarmonyPatch(typeof(Character), "ApplyDamage")]
        private static void Character_ApplyDamage_Postfix(Character __instance, HitData hit)
        {
            if (GlitnirRankingPlugin.Instance == null)
                return;

            GlitnirRankingPlugin.Instance.NotifyLocalDamage(__instance, hit, false);
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(Character), "Damage")]
        private static void Character_Damage_Postfix(Character __instance, HitData hit)
        {
            if (GlitnirRankingPlugin.Instance == null)
                return;

            GlitnirRankingPlugin.Instance.NotifyLocalDamage(__instance, hit, true);
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(Character), "OnDeath")]
        private static void Character_OnDeath_Postfix(Character __instance)
        {
            if (GlitnirRankingPlugin.Instance == null)
                return;

            GlitnirRankingPlugin.Instance.NotifyLocalCharacterDeath(__instance);
            GlitnirRankingPlugin.Instance.NotifyServerCharacterDeath(__instance);
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(Skills), "RaiseSkill")]
        private static void Skills_RaiseSkill_Prefix(Skills __instance, Skills.SkillType skillType, ref float __state)
        {
            if (GlitnirRankingPlugin.Instance == null)
                return;

            __state = GlitnirRankingPlugin.Instance.CaptureLocalSkillLevelBeforeRaise(__instance, skillType);
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(Skills), "RaiseSkill")]
        private static void Skills_RaiseSkill_Postfix(Skills __instance, Skills.SkillType skillType, float __state)
        {
            if (GlitnirRankingPlugin.Instance == null)
                return;

            GlitnirRankingPlugin.Instance.NotifyLocalSkillGain(__instance, skillType, __state);
        }
    }


    [HarmonyPatch]
    internal static class CraftRankingPatches
    {
        [HarmonyTargetMethods]
        private static IEnumerable<MethodBase> TargetMethods()
        {
            Type inventoryGuiType = AccessTools.TypeByName("InventoryGui");
            if (inventoryGuiType == null)
                yield break;

            foreach (MethodInfo method in inventoryGuiType.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            {
                if (string.Equals(method.Name, "DoCrafting", StringComparison.OrdinalIgnoreCase))
                    yield return method;
            }
        }

        [HarmonyPostfix]
        private static void Postfix(object __instance, object[] __args)
        {
            try
            {
                if (GlitnirRankingPlugin.Instance == null)
                    return;

                Recipe recipe = ExtractRecipe(__args);
                if (recipe == null && __instance != null)
                    recipe = ExtractRecipeFromInventoryGui(__instance);

                if (recipe == null)
                    return;

                GlitnirRankingPlugin.Instance.NotifyLocalCraftingStarted(recipe);
            }
            catch (Exception ex)
            {
                GlitnirRankingPlugin.Log.LogError("Erro no patch de craft: " + ex);
            }
        }

        private static Recipe ExtractRecipe(object[] args)
        {
            if (args == null)
                return null;

            for (int i = 0; i < args.Length; i++)
            {
                Recipe recipe = args[i] as Recipe;
                if (recipe != null)
                    return recipe;
            }

            return null;
        }

        private static Recipe ExtractRecipeFromInventoryGui(object inventoryGui)
        {
            try
            {
                FieldInfo recipeField = inventoryGui.GetType().GetField("m_craftRecipe", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (recipeField != null)
                    return recipeField.GetValue(inventoryGui) as Recipe;
            }
            catch
            {
            }

            return null;
        }
    }


    [HarmonyPatch]
    internal static class MarketplaceQuestRankingPatches
    {
        [HarmonyTargetMethod]
        private static MethodBase TargetMethod()
        {
            Type questType = AccessTools.TypeByName("Marketplace.Modules.Quests.Quests_DataTypes+Quest");
            if (questType == null)
                return null;

            return AccessTools.Method(questType, "RemoveQuestComplete", new[] { typeof(int) });
        }

        [HarmonyPostfix]
        private static void Postfix(int UID)
        {
            if (GlitnirRankingPlugin.Instance == null)
                return;

            GlitnirRankingPlugin.Instance.ReportLocalMarketplaceQuestCompletion(UID);
        }
    }

    [Serializable]
    [HarmonyPatch(typeof(Player), "OnDeath")]
    public static class GlitnirRankingPlayerDeathPatch
    {
        private static float _lastLocalDeathReportAt;

        private static void Postfix(Player __instance)
        {
            try
            {
                if (__instance == null || Player.m_localPlayer == null)
                    return;

                if (__instance != Player.m_localPlayer)
                    return;

                if (Time.realtimeSinceStartup - _lastLocalDeathReportAt < 2f)
                    return;

                _lastLocalDeathReportAt = Time.realtimeSinceStartup;
                GlitnirRankingPlugin.Instance?.ReportLocalPlayerDeathToServer();
            }
            catch (Exception ex)
            {
                GlitnirRankingPlugin.Log?.LogWarning("[Ranking] Falha ao reportar morte do jogador: " + ex.Message);
            }
        }
    }

    public class MarketplaceQuestPointRule
    {
        public string QuestKey = "";
        public int Points = 0;
    }

    [Serializable]
    public class MarketplaceQuestCreditRecord
    {
        public string PlayerName = "";
        public string QuestKey = "";
        public string GrantedAtUtc = "";
    }

    [Serializable]

    public class RewardClaimRecord
    {
        public string CycleId = "";
        public string PlayerName = "";
        public int Rank = 0;
        public string ClaimedAtUtc = "";
        public string Status = "claimed";
    }


    public class RankingEntryDocument
    {
        [BsonId]
        public string PlayerName { get; set; }
        public int Points { get; set; }
        public string LastReason { get; set; }
        public string LastUpdateUtc { get; set; }
        public int TotalKillsPontuadas { get; set; }
        public int TotalBossesPontuadas { get; set; }
        public int KillPointsTotal { get; set; }
        public int BossPointsTotal { get; set; }
        public int TotalSkillLevelUpsPontuados { get; set; }
        public int SkillPointsTotal { get; set; }
        public int TotalFishingPontuadas { get; set; }
        public int FishingPointsTotal { get; set; }
        public int TotalCraftPontuadas { get; set; }
        public int CraftPointsTotal { get; set; }
        public int TotalFarmJackpotsPontuados { get; set; }
        public int FarmJackpotPointsTotal { get; set; }
        public int TotalUniqueCraftJackpotsPontuados { get; set; }
        public int UniqueCraftJackpotPointsTotal { get; set; }
        public int TotalDeaths { get; set; }
        public int DeathPenaltyPointsTotal { get; set; }
        public int TotalPointsExchanges { get; set; }
        public int PointsExchangePenaltyTotal { get; set; }
        public int PointsExchangeCoinsTotal { get; set; }
        public int ExplorationMapJackpotPointsTotal { get; set; }
        public string ProgressCounters { get; set; }
        public string FloatProgressCounters { get; set; }
        public string GenericJackpotCredits { get; set; }
        public string BossCredits { get; set; }
        public string SkillLevel100JackpotCredits { get; set; }
    }

    public class RewardClaimDocument
    {
        [BsonId]
        public string Key { get; set; }
        public string CycleId { get; set; }
        public string PlayerName { get; set; }
        public int Rank { get; set; }
        public string ClaimedAtUtc { get; set; }
        public string Status { get; set; }
    }

    public class MarketplaceQuestCreditDocument
    {
        [BsonId]
        public string Key { get; set; }
        public string PlayerName { get; set; }
        public string QuestKey { get; set; }
        public string GrantedAtUtc { get; set; }
    }

    public class FishingCatchCreditDocument
    {
        public string PlayerName { get; set; }
        public string FishPrefab { get; set; }

        [BsonId]
        public string ZdoKey { get; set; }
        public string GrantedAtUtc { get; set; }
    }


    [Serializable]
    public class RankingDatabase
    {
        public List<RankingEntry> Entries = new List<RankingEntry>();
        public List<RewardClaimRecord> Claims = new List<RewardClaimRecord>();
        public List<MarketplaceQuestCreditRecord> MarketplaceQuestCredits = new List<MarketplaceQuestCreditRecord>();
        public List<FishingCatchCreditRecord> FishingCatchCredits = new List<FishingCatchCreditRecord>();
    }

    [Serializable]
    public class RankingEntry
    {
        public string PlayerName = "";
        public int Points = 0;
        public string LastReason = "";
        public string LastUpdateUtc = "";
        public int TotalKillsPontuadas = 0;
        public int TotalBossesPontuadas = 0;
        public int KillPointsTotal = 0;
        public int BossPointsTotal = 0;
        public int TotalSkillLevelUpsPontuados = 0;
        public int SkillPointsTotal = 0;
        public int TotalFishingPontuadas = 0;
        public int FishingPointsTotal = 0;
        public int TotalCraftPontuadas = 0;
        public int CraftPointsTotal = 0;
        public int TotalFarmJackpotsPontuados = 0;
        public int FarmJackpotPointsTotal = 0;
        public int TotalUniqueCraftJackpotsPontuados = 0;
        public int UniqueCraftJackpotPointsTotal = 0;
        public int TotalDeaths = 0;
        public int DeathPenaltyPointsTotal = 0;
        public int TotalPointsExchanges = 0;
        public int PointsExchangePenaltyTotal = 0;
        public int PointsExchangeCoinsTotal = 0;
        public int ExplorationMapJackpotPointsTotal = 0;
        public Dictionary<string, int> ProgressCounters = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, float> FloatProgressCounters = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
        public HashSet<string> GenericJackpotCredits = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        public HashSet<string> BossCredits = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        public HashSet<string> SkillLevel100JackpotCredits = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    }

    [Serializable]
    public class FishingCatchCreditRecord
    {
        public string PlayerName = "";
        public string FishPrefab = "";
        public string ZdoKey = "";
        public string GrantedAtUtc = "";
    }

    [Serializable]
    public class JackpotRule
    {
        public int RequiredAmount = 1;
        public int Points = 0;
    }

    [Serializable]
    public class RankingRules
    {
        public bool RankingEnabled = true;
        public bool EnableKillPoints = true;
        public bool EnableBossPoints = true;
        public int DefaultKillPoints = 1;
        public int TopCount = 10;
        public bool AllowRepeatedBossPoints = false;
        public bool DebugLogging = false;
        public bool LogHitReports = false;
        public bool LogKillReports = false;
        public bool LogSkillReports = false;
        public bool LogPendingKillReports = false;
        public bool LogSnapshotRequests = false;
        public bool LogPointsChanges = false;
        public bool EnableSkillPoints = true;
        public bool IgnoreTamedKills = true;
        public bool EnableMarketplaceQuestPoints = true;
        public bool EnableFishingPoints = true;
        public bool EnableDeathPenalty = true;
        public bool DeathPenaltyUseMultiplier = false;
        public int DeathPenaltyPerDeath = 100;
        public bool EnableExplorationJackpots = true;
        public bool RewardClaimsEnabled = true;
        public string RewardClaimCycleId = "temporada_001";
        public int RewardTop1MinPoints = 300;
        public string RewardTop1Label = "Recompensa do 1 lugar";
        public string RewardTop1Prefab = "Coins";
        public int RewardTop1Amount = 500;
        public int RewardTop2MinPoints = 200;
        public string RewardTop2Label = "Recompensa do 2 lugar";
        public string RewardTop2Prefab = "AmberPearl";
        public int RewardTop2Amount = 10;
        public int RewardTop3MinPoints = 100;
        public string RewardTop3Label = "Recompensa do 3 lugar";
        public string RewardTop3Prefab = "Ruby";
        public int RewardTop3Amount = 5;
        public bool PointsExchangeEnabled = true;
        public string PointsExchangePrefab = "Coins";
        public bool PointsExchangeUseCoinsPerPoint = false;
        public int PointsExchangeCoinsPerPoint = 1;
        public bool PointsExchangeUsePointsPerCoin = true;
        public int PointsExchangePointsPerCoin = 100;
        public int PointsExchangeMinPoints = 1;
        public int PointsExchangeMaxPointsPerRequest = 0;
        public Dictionary<string, int> KillPoints = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, string> CombatHudCategories = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, string> ProductionHudCategories = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, int> BossPoints = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, Dictionary<int, int>> SkillMilestoneJackpotPoints = new Dictionary<string, Dictionary<int, int>>(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, int> MarketplaceQuestPoints = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, int> FishingPoints = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, int> CraftPoints = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, JackpotRule> FarmJackpots = new Dictionary<string, JackpotRule>(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, JackpotRule> UniqueCraftJackpots = new Dictionary<string, JackpotRule>(StringComparer.OrdinalIgnoreCase);
        public Dictionary<int, int> DeathPenaltyRules = new Dictionary<int, int>();
        public Dictionary<int, int> ExplorationMapJackpots = new Dictionary<int, int>();
    }
}
