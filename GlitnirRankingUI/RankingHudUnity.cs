using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Glitnir.Ranking
{
    public partial class GlitnirRankingPlugin
    {
        private const string UnityHudBundleResourceSuffix = "glitnirrankinghud";
        private AssetBundle _unityHudBundle;
        private GameObject _unityHudCanvas;
        private readonly Dictionary<string, Transform> _unityHudTransforms = new Dictionary<string, Transform>(StringComparer.OrdinalIgnoreCase);
        private bool _unityHudLoadAttempted;
        private bool _unityHudAvailable;
        private bool _unityHudButtonsBound;
        private float _unityHudNextRefresh;
        private int _unityHudTabIndex;
        private int _unityHudGuideCategoryIndex;
        private int _unityHudExchangePoints;
        private int _unityHudLeaderboardScrollIndex;
        private int _unityHudGuideScrollIndex;
        private int _unityHudPlayersScrollIndex;
        private int _unityHudGuideSubCategoryIndex;
        private InputField _unityHudExchangeInputField;
        private float _unityHudExchangePendingSince;

        private void TickUnityRankingHud()
        {
            if (Application.isBatchMode)
                return;

            if (!_hudVisible)
            {
                HideUnityRankingHud();
                return;
            }

            if (Player.m_localPlayer == null)
            {
                _hudVisible = false;
                HideUnityRankingHud();
                SetRankingInputBlockerVisible(false);
                return;
            }

            if (!EnsureUnityRankingHudLoaded())
                return;

            if (_unityHudCanvas != null && !_unityHudCanvas.activeSelf)
                _unityHudCanvas.SetActive(true);

            SetUnityViewActive("MainPanel", true);
            SetUnityViewActive("CollapsedBadge", false);

            Cursor.visible = true;
            if (Cursor.lockState != CursorLockMode.None)
                Cursor.lockState = CursorLockMode.None;
            HandleUnityHudKeyboard();
            UpdateUnityRankingHud();
        }

        private bool TryShowUnityRankingHud()
        {
            if (Application.isBatchMode)
                return false;

            if (!_hudVisible)
            {
                HideUnityRankingHud();
                return false;
            }

            if (Player.m_localPlayer == null)
            {
                _hudVisible = false;
                HideUnityRankingHud();
                SetRankingInputBlockerVisible(false);
                return false;
            }

            if (!EnsureUnityRankingHudLoaded())
                return false;

            if (_unityHudCanvas != null && !_unityHudCanvas.activeSelf)
                _unityHudCanvas.SetActive(true);

            UpdateUnityRankingHud();
            return true;
        }

        private void HideUnityRankingHud()
        {
            if (_unityHudCanvas != null && _unityHudCanvas.activeSelf)
                _unityHudCanvas.SetActive(false);
        }

        private bool IsUnityRankingHudVisible()
        {
            return !Application.isBatchMode &&
                   _hudVisible &&
                   _unityHudCanvas != null &&
                   _unityHudCanvas.activeSelf;
        }

        private bool EnsureUnityRankingHudLoaded()
        {
            if (_unityHudAvailable && _unityHudCanvas != null)
                return true;

            if (_unityHudLoadAttempted)
                return false;

            _unityHudLoadAttempted = true;

            try
            {
                byte[] bytes = LoadUnityHudBundleBytes();
                if (bytes == null || bytes.Length == 0)
                {
                    Logger.LogWarning("[Glitnir Ranking] HUD Unity não encontrado nos recursos embutidos da DLL.");
                    return CreateRuntimeUnityRankingHud();
                }

                _unityHudBundle = AssetBundle.LoadFromMemory(bytes);
                if (_unityHudBundle == null)
                {
                    Logger.LogWarning("[Glitnir Ranking] AssetBundle do HUD Unity não pôde ser carregado.");
                    return CreateRuntimeUnityRankingHud();
                }

                string assetName = _unityHudBundle.GetAllAssetNames()
                    .FirstOrDefault(name => name.EndsWith("glitnirrankinghud.prefab", StringComparison.OrdinalIgnoreCase));
                if (string.IsNullOrWhiteSpace(assetName))
                    assetName = _unityHudBundle.GetAllAssetNames().FirstOrDefault();
                if (string.IsNullOrWhiteSpace(assetName))
                {
                    Logger.LogWarning("[Glitnir Ranking] AssetBundle do HUD Unity não contém prefab.");
                    return CreateRuntimeUnityRankingHud();
                }

                GameObject prefab = _unityHudBundle.LoadAsset<GameObject>(assetName);
                if (prefab == null)
                {
                    Logger.LogWarning("[Glitnir Ranking] Prefab do HUD Unity não pôde ser carregado: " + assetName);
                    return CreateRuntimeUnityRankingHud();
                }

                _unityHudCanvas = Instantiate(prefab);
                _unityHudCanvas.name = "GlitnirRankingHudCanvas_Runtime";
                DontDestroyOnLoad(_unityHudCanvas);
                Canvas canvas = _unityHudCanvas.GetComponent<Canvas>();
                if (canvas != null)
                {
                    canvas.overrideSorting = true;
                    canvas.sortingOrder = 32740;
                }
                RemoveNestedEventSystems(_unityHudCanvas.transform);
                CacheUnityHudTransforms(_unityHudCanvas.transform);
                BindUnityHudButtons();
                _unityHudCanvas.SetActive(false);
                _unityHudAvailable = true;
                Logger.LogInfo("[Glitnir Ranking] HUD Unity carregado com sucesso: " + assetName);
                return true;
            }
            catch (Exception ex)
            {
                Logger.LogWarning("[Glitnir Ranking] Falha ao carregar HUD Unity: " + ex.Message);
                return CreateRuntimeUnityRankingHud();
            }
        }

        private byte[] LoadUnityHudBundleBytes()
        {
            Assembly assembly = Assembly.GetExecutingAssembly();
            string[] resourceNames = assembly.GetManifestResourceNames();
            string resourceName = assembly.GetManifestResourceNames()
                .FirstOrDefault(name => name.EndsWith(UnityHudBundleResourceSuffix, StringComparison.OrdinalIgnoreCase));
            if (string.IsNullOrWhiteSpace(resourceName))
            {
                Logger.LogWarning("[Glitnir Ranking] Recursos embutidos disponíveis: " + string.Join(", ", resourceNames));
                return null;
            }

            using (Stream stream = assembly.GetManifestResourceStream(resourceName))
            {
                if (stream == null)
                    return null;

                using (MemoryStream memory = new MemoryStream())
                {
                    stream.CopyTo(memory);
                    return memory.ToArray();
                }
            }
        }

        private void RemoveNestedEventSystems(Transform root)
        {
            EventSystem[] eventSystems = root.GetComponentsInChildren<EventSystem>(true);
            for (int i = 0; i < eventSystems.Length; i++)
            {
                if (eventSystems[i] != null)
                    Destroy(eventSystems[i].gameObject);
            }
        }

        private void EnsureUnityHudEventSystem()
        {
            if (EventSystem.current != null)
                return;

            GameObject eventSystem = new GameObject("GlitnirRankingHud_EventSystem");
            DontDestroyOnLoad(eventSystem);
            eventSystem.AddComponent<EventSystem>();
            eventSystem.AddComponent<StandaloneInputModule>();
        }

        private void BindUnityHudButtons()
        {
            if (_unityHudButtonsBound)
                return;

            EnsureUnityHudEventSystem();
            BindUnityButton("CollapsedBadge", ToggleRankingHud);
            BindUnityButton("CloseButton", CloseRankingHudAndCollapseAll);
            BindUnityButton("TabLeaderboard", delegate { SetUnityHudTab(0); });
            BindUnityButton("TabPerformance", delegate { SetUnityHudTab(1); });
            BindUnityButton("TabGuide", delegate { SetUnityHudTab(2); });
            BindUnityButton("TabOracle", delegate { SetUnityHudTab(3); });
            BindUnityButton("TabExchange", delegate { SetUnityHudTab(4); });
            BindUnityButton("TabPlayers", delegate { SetUnityHudTab(5); });
            BindUnityButton("OracleActionButton", RequestRewardClaimFromServer);
            BindUnityButton("ExchangeMinusButton", delegate { AdjustUnityExchangePoints(-GetUnityExchangeStep()); });
            BindUnityButton("ExchangePlusButton", delegate { AdjustUnityExchangePoints(GetUnityExchangeStep()); });
            BindUnityButton("ExchangeMaxButton", delegate { SetUnityExchangePoints(GetUnityMaxExchangePoints(_cachedPlayerData)); });
            BindUnityButton("ExchangeClaimButton", delegate { RequestPointsExchangeFromServer(_unityHudExchangePoints); });
            BindUnityButton("LeaderboardScrollbar", delegate { SetScrollFromMouse("LeaderboardListPanel/LeaderboardScrollbar", ref _unityHudLeaderboardScrollIndex, _cachedTopEntries != null ? _cachedTopEntries.Count : 0, 6); });
            BindUnityButton("GuideScrollbar", delegate { SetScrollFromMouse("GuideEntries/GuideScrollbar", ref _unityHudGuideScrollIndex, GetCurrentUnityGuideRulesCount(), GetCurrentUnityGuideVisibleRows()); });
            BindUnityButton("PlayersScrollbar", delegate { SetScrollFromMouse("PlayersListPanel/PlayersScrollbar", ref _unityHudPlayersScrollIndex, _cachedTopEntries != null ? _cachedTopEntries.Count : 0, 7); });
            for (int i = 0; i < UnityGuideCategories.Length; i++)
            {
                int categoryIndex = i;
                BindUnityButton("GuideCategory" + (i + 1), delegate { SetUnityHudGuideCategory(categoryIndex); });
            }
            for (int i = 0; i < 9; i++)
            {
                int subCategoryIndex = i;
                BindUnityButton("GuideSubCategory" + (i + 1), delegate { SetUnityHudGuideSubCategory(subCategoryIndex); });
            }
            BindUnityHudExchangeInput();
            _unityHudButtonsBound = true;
        }

        private void BindUnityButton(string pathOrName, UnityEngine.Events.UnityAction action)
        {
            Transform transform = ResolveUnityHudPath(pathOrName);
            if (transform == null || action == null)
                return;

            Image image = transform.GetComponent<Image>();
            if (image == null)
                image = transform.gameObject.AddComponent<Image>();

            image.raycastTarget = true;

            Button button = transform.GetComponent<Button>();
            if (button == null)
                button = transform.gameObject.AddComponent<Button>();

            if (button.transition == Selectable.Transition.None)
                button.transition = Selectable.Transition.SpriteSwap;

            GlitnirButtonFx fx = transform.GetComponent<GlitnirButtonFx>();
            if (fx == null)
                fx = transform.gameObject.AddComponent<GlitnirButtonFx>();
            fx.SetSelected(false);

            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(action);
        }

        private void SetUnityHudTab(int index)
        {
            _unityHudTabIndex = Mathf.Clamp(index, 0, 5);
            _unityHudNextRefresh = 0f;
            UpdateUnityRankingHud();
        }

        private void SetUnityHudGuideCategory(int index)
        {
            _unityHudGuideCategoryIndex = Mathf.Clamp(index, 0, UnityGuideCategories.Length - 1);
            _unityHudGuideScrollIndex = 0;
            _unityHudGuideSubCategoryIndex = 0;
            _unityHudNextRefresh = 0f;
            UpdateUnityRankingHud();
        }

        private void SetUnityHudGuideSubCategory(int index)
        {
            _unityHudGuideSubCategoryIndex = Mathf.Clamp(index, 0, 32);
            _unityHudGuideScrollIndex = 0;
            _unityHudNextRefresh = 0f;
            UpdateUnityRankingHud();
        }

        private void CacheUnityHudTransforms(Transform root)
        {
            _unityHudTransforms.Clear();
            Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < transforms.Length; i++)
            {
                Transform current = transforms[i];
                if (current == null || string.IsNullOrWhiteSpace(current.name))
                    continue;

                if (!_unityHudTransforms.ContainsKey(current.name))
                    _unityHudTransforms[current.name] = current;
            }
        }

        private Transform UnityHudFind(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return null;

            Transform transform;
            return _unityHudTransforms.TryGetValue(name, out transform) ? transform : null;
        }

        private Text UnityHudText(string pathOrName)
        {
            Transform transform = ResolveUnityHudPath(pathOrName);
            return transform != null ? transform.GetComponent<Text>() : null;
        }

        private Transform ResolveUnityHudPath(string pathOrName)
        {
            if (_unityHudCanvas == null || string.IsNullOrWhiteSpace(pathOrName))
                return null;

            if (!pathOrName.Contains("/"))
                return UnityHudFind(pathOrName);

            string[] parts = pathOrName.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0)
                return null;

            Transform current = FindDirectChild(_unityHudCanvas.transform, parts[0]) ?? UnityHudFind(parts[0]);
            if (current == null)
                return null;

            for (int i = 1; i < parts.Length; i++)
            {
                current = FindDirectChild(current, parts[i]);
                if (current == null)
                    return null;
            }

            return current;
        }

        private Transform FindDirectChild(Transform parent, string name)
        {
            if (parent == null)
                return null;

            for (int i = 0; i < parent.childCount; i++)
            {
                Transform child = parent.GetChild(i);
                if (child != null && child.name.Equals(name, StringComparison.OrdinalIgnoreCase))
                    return child;
            }

            return null;
        }

        private void UpdateUnityRankingHud()
        {
            if (_unityHudCanvas == null)
                return;

            if (Time.realtimeSinceStartup < _unityHudNextRefresh)
                return;

            _unityHudNextRefresh = Time.realtimeSinceStartup + 0.25f;
            UpdateUnityHudTabs();
            UpdateUnityHudLeaderboard();
            UpdateUnityHudPerformance();
            UpdateUnityHudGuide();
            UpdateUnityHudOracle();
            UpdateUnityHudExchange();
            UpdateUnityHudPlayers();
        }

        private void UpdateUnityHudTabs()
        {
            SetUnityViewActive("ViewLeaderboard", _unityHudTabIndex == 0);
            SetUnityViewActive("ViewPerformance", _unityHudTabIndex == 1);
            SetUnityViewActive("ViewGuide", _unityHudTabIndex == 2);
            SetUnityViewActive("ViewOracle", _unityHudTabIndex == 3);
            SetUnityViewActive("ViewExchange", _unityHudTabIndex == 4);
            SetUnityViewActive("ViewPlayers", _unityHudTabIndex == 5);
            SetUnityTabVisual("TabLeaderboard", _unityHudTabIndex == 0);
            SetUnityTabVisual("TabPerformance", _unityHudTabIndex == 1);
            SetUnityTabVisual("TabGuide", _unityHudTabIndex == 2);
            SetUnityTabVisual("TabOracle", _unityHudTabIndex == 3);
            SetUnityTabVisual("TabExchange", _unityHudTabIndex == 4);
            SetUnityTabVisual("TabPlayers", _unityHudTabIndex == 5);
        }

        private void SetUnityTabVisual(string pathOrName, bool active)
        {
            Transform transform = ResolveUnityHudPath(pathOrName);
            if (transform == null)
                return;

            Image image = transform.GetComponent<Image>();
            if (image != null)
                image.color = active ? new Color(1f, 0.78f, 0.34f, 1f) : Color.white;

            GlitnirButtonFx fx = transform.GetComponent<GlitnirButtonFx>();
            if (fx != null)
                fx.SetSelected(active);
        }

        private void SetUnityViewActive(string name, bool active)
        {
            Transform transform = UnityHudFind(name);
            if (transform != null && transform.gameObject.activeSelf != active)
                transform.gameObject.SetActive(active);
        }

        private void UpdateUnityHudLeaderboard()
        {
            int count = _cachedTopEntries != null ? _cachedTopEntries.Count : 0;
            const int visibleRows = 6;
            _unityHudLeaderboardScrollIndex = Mathf.Clamp(_unityHudLeaderboardScrollIndex, 0, Mathf.Max(0, count - visibleRows));
            for (int i = 0; i < visibleRows; i++)
            {
                int dataIndex = _unityHudLeaderboardScrollIndex + i;
                SnapshotTopEntryData entry = dataIndex < count ? _cachedTopEntries[dataIndex] : null;
                string row = "LeaderboardListPanel/Rows/Row" + (i + 1);
                Transform rowTransform = ResolveUnityHudPath(row);
                bool showFallback = count == 0 && i == 0;
                if (rowTransform != null && rowTransform.gameObject.activeSelf != (entry != null || showFallback))
                    rowTransform.gameObject.SetActive(entry != null || showFallback);

                if (entry == null && !showFallback)
                    continue;

                if (showFallback)
                {
                    SetUnityText(row + "/Position", "--");
                    SetUnityText(row + "/Player", "Aguardando dados do servidor");
                    SetUnityText(row + "/Points", "");
                    SetUnityText(row + "/Title", "");
                }
                else
                {
                    SetUnityText(row + "/Position", entry.Position.ToString("00"));
                    SetUnityText(row + "/Player", string.IsNullOrWhiteSpace(entry.PlayerName) ? "WARRIOR" : entry.PlayerName);
                    SetUnityText(row + "/Points", FormatPoints(entry.Points));
                    SetUnityText(row + "/Title", GetRankTitle(entry.Position).ToUpperInvariant());
                }
            }

            UpdateUnityHudPodium("Podium/Rank1", count > 0 ? _cachedTopEntries[0] : null);
            UpdateUnityHudPodium("Podium/Rank2", count > 1 ? _cachedTopEntries[1] : null);
            UpdateUnityHudPodium("Podium/Rank3", count > 2 ? _cachedTopEntries[2] : null);
            SetUnityScrollbar("LeaderboardListPanel/LeaderboardScrollbar", count, visibleRows, _unityHudLeaderboardScrollIndex);
        }

        private void UpdateUnityHudPodium(string path, SnapshotTopEntryData entry)
        {
            Transform slot = ResolveUnityHudPath(path);
            if (slot == null)
                return;

            slot.gameObject.SetActive(entry != null);
            if (entry == null)
                return;

            SetUnityText(path + "/Name", string.IsNullOrWhiteSpace(entry.PlayerName) ? "WARRIOR" : entry.PlayerName.ToUpperInvariant());
            SetUnityText(path + "/Points", FormatPoints(entry.Points));
        }

        private void UpdateUnityHudPerformance()
        {
            SnapshotPlayerData data = _cachedPlayerData ?? new SnapshotPlayerData();
            string playerName = !string.IsNullOrWhiteSpace(data.PlayerName) ? data.PlayerName : GetLocalPlayerName();

            SetUnityText("PlayerProfile/PlayerName", string.IsNullOrWhiteSpace(playerName) ? "WARRIOR" : playerName.ToUpperInvariant());
            SetUnityText("PlayerProfile/PlayerRank", "RANK #" + Mathf.Max(1, data.Position) + "  |  " + FormatPoints(data.Points));
            SetUnityText("ProgressSummary/MetricKills/Value", data.TotalKillsPontuadas.ToString());
            SetUnityText("ProgressSummary/MetricCrafts/Value", data.TotalCraftPontuadas.ToString());
            SetUnityText("ProgressSummary/MetricQuests/Value", data.TotalMarketplaceQuestsPontuadas.ToString());
            SetUnityText("ProgressSummary/MetricFish/Value", data.TotalFishingPontuadas.ToString());

            int total = Mathf.Max(1, data.Points + data.DeathPenaltyPointsTotal + data.PointsExchangePenaltyTotal);
            SetUnityFill("PlayerProfile/CombatBar/Track/Fill", (data.KillPointsTotal + data.BossPointsTotal) / (float)total);
            SetUnityFill("PlayerProfile/CraftBar/Track/Fill", data.CraftPointsTotal / (float)total);
            float explorationPercent = GetUnityExplorationMapProgressPercent(data);
            SetUnityFill("PlayerProfile/ExploreBar/Track/Fill", explorationPercent / 100f);
            SetUnityText("PlayerProfile/CombatBar/Value", FormatPoints(data.KillPointsTotal + data.BossPointsTotal));
            SetUnityText("PlayerProfile/CraftBar/Value", FormatPoints(data.CraftPointsTotal));
            SetUnityText("PlayerProfile/ExploreBar/Value", explorationPercent.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + "%");

            string[] deeds =
            {
                "COMBATE  " + data.TotalKillsPontuadas + " kills  |  +" + FormatPoints(data.KillPointsTotal + data.BossPointsTotal),
                "CHEFES  " + data.TotalBossesPontuadas + " vitorias  |  +" + FormatPoints(data.BossPointsTotal),
                "HABILIDADES  " + data.TotalSkillLevelUpsPontuados + " marcos  |  +" + FormatPoints(data.SkillPointsTotal),
                "PESCA  " + data.TotalFishingPontuadas + " capturas  |  +" + FormatPoints(data.FishingPointsTotal),
                "PRODUCAO  " + data.TotalCraftPontuadas + " crafts  |  +" + FormatPoints(data.CraftPointsTotal),
                "QUESTS  " + data.TotalMarketplaceQuestsPontuadas + " concluidas  |  +" + FormatPoints(data.MarketplaceQuestPointsTotal),
                "EXPLORACAO  " + explorationPercent.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + "% mapa  |  +" + FormatPoints(data.ExplorationMapJackpotPointsTotal),
                "PENALIDADES  " + data.TotalDeaths + " mortes  |  -" + FormatPoints(data.DeathPenaltyPointsTotal),
                "CAMBIO  " + data.TotalPointsExchanges + " trocas  |  -" + FormatPoints(data.PointsExchangePenaltyTotal),
                "ULTIMO REGISTRO  " + (string.IsNullOrWhiteSpace(data.LastReason) ? "Nenhum registro recente" : data.LastReason)
            };

            SetUnityObjectActive("RecentDeedsPanel", true);
            SetUnityObjectActive("RecentDeedsPanel/RecentDeedsRows", true);
            for (int i = 0; i < 6; i++)
            {
                string row = "RecentDeedsPanel/RecentDeedsRows/DeedRow" + (i + 1);
                string value = i < deeds.Length ? deeds[i] : "";
                SetUnityObjectActive(row, true);
                SetUnityText(row + "/Text", value);
                SetUnityChildText(row, "Text", value);
            }
        }

        private void UpdateUnityHudGuide()
        {
            for (int i = 0; i < UnityGuideCategories.Length; i++)
            {
                SetUnityText("GuideCategories/GuideCategory" + (i + 1) + "/Label", UnityGuideCategories[i].Label);
                SetUnityCategoryVisual("GuideCategories/GuideCategory" + (i + 1), i == _unityHudGuideCategoryIndex);
            }

            SnapshotPlayerData data = _cachedPlayerData ?? new SnapshotPlayerData();
            UnityGuideCategory category = UnityGuideCategories[Mathf.Clamp(_unityHudGuideCategoryIndex, 0, UnityGuideCategories.Length - 1)];
            SetUnityText("GuideEntries/GuideTitle", category.Title);
            SetUnityText("GuideEntries/GuideText", GetUnityGuideDescription(category, data));

            List<UnityGuideRuleLine> allRules = BuildUnityGuideRules(category, data);
            List<string> subCategories = GetUnityGuideSubCategories(allRules);
            bool showSubCategories = ShouldShowUnityGuideSubCategories(subCategories);
            SetUnityViewActive("GuideSubCategories", showSubCategories);
            SetUnityGuideListBounds(showSubCategories);
            _unityHudGuideSubCategoryIndex = Mathf.Clamp(_unityHudGuideSubCategoryIndex, 0, Mathf.Max(0, subCategories.Count - 1));
            for (int i = 0; i < 9; i++)
            {
                string subPath = "GuideSubCategories/GuideSubCategory" + (i + 1);
                Transform sub = ResolveUnityHudPath(subPath);
                bool visible = showSubCategories && i < subCategories.Count;
                if (sub != null && sub.gameObject.activeSelf != visible)
                    sub.gameObject.SetActive(visible);
                if (!visible)
                    continue;
                SetUnityText(subPath + "/Label", subCategories[i].ToUpperInvariant());
                SetUnityCategoryVisual(subPath, i == _unityHudGuideSubCategoryIndex);
            }

            string selectedSubCategory = showSubCategories && subCategories.Count > 0 ? subCategories[_unityHudGuideSubCategoryIndex] : "";
            List<UnityGuideRuleLine> rules = string.IsNullOrWhiteSpace(selectedSubCategory)
                ? allRules
                : allRules.Where(x => string.Equals(x.Category, selectedSubCategory, StringComparison.OrdinalIgnoreCase) ||
                                       (string.Equals(selectedSubCategory, "GERAL", StringComparison.OrdinalIgnoreCase) && string.IsNullOrWhiteSpace(x.Category))).ToList();
            int visibleRows = showSubCategories ? 9 : 12;
            _unityHudGuideScrollIndex = Mathf.Clamp(_unityHudGuideScrollIndex, 0, Mathf.Max(0, rules.Count - visibleRows));
            for (int i = 0; i < 12; i++)
            {
                string row = "GuideEntries/GuideRows/RuleRow" + (i + 1);
                Transform rowTransform = ResolveUnityHudPath(row);
                int dataIndex = _unityHudGuideScrollIndex + i;
                bool hasRule = i < visibleRows && dataIndex < rules.Count;
                if (rowTransform != null && rowTransform.gameObject.activeSelf != hasRule)
                    rowTransform.gameObject.SetActive(hasRule);

                if (!hasRule)
                    continue;

                string action = rules[dataIndex].Action;
                string progress = GetUnityGuideRuleProgressText(category, rules[dataIndex], data);
                if (!string.IsNullOrWhiteSpace(progress))
                    action = action + "  " + progress;
                SetUnityText(row + "/Action", action);
                SetUnityText(row + "/Points", rules[dataIndex].Points);
            }
            SetUnityScrollbar("GuideEntries/GuideScrollbar", rules.Count, visibleRows, _unityHudGuideScrollIndex);
        }

        private void SetUnityCategoryVisual(string pathOrName, bool active)
        {
            Transform transform = ResolveUnityHudPath(pathOrName);
            if (transform == null)
                return;

            Image image = transform.GetComponent<Image>();
            if (image != null)
                image.color = active ? new Color(1f, 0.74f, 0.28f, 1f) : Color.white;

            GlitnirButtonFx fx = transform.GetComponent<GlitnirButtonFx>();
            if (fx != null)
                fx.SetSelected(active);
        }

        private List<UnityGuideRuleLine> BuildUnityGuideRules(UnityGuideCategory category, SnapshotPlayerData data)
        {
            string raw = GetUnityGuideRawRules(category.Key, data);
            List<UnityGuideRuleLine> lines = new List<UnityGuideRuleLine>();
            string currentCategory = "";

            if (!string.IsNullOrWhiteSpace(raw))
            {
                string[] split = raw.Replace("\r", "").Split(new[] { '\n', ',' }, StringSplitOptions.RemoveEmptyEntries);
                for (int i = 0; i < split.Length; i++)
                {
                    UnityGuideRuleLine line = ParseUnityGuideRuleLine(split[i], ref currentCategory, category.Key);
                    if (!string.IsNullOrWhiteSpace(line.Action))
                        lines.Add(line);
                }
            }

            if (lines.Count == 0)
                lines.Add(new UnityGuideRuleLine("Sem regras configuradas", ""));

            return lines;
        }

        private string GetUnityGuideRawRules(string key, SnapshotPlayerData data)
        {
            if (data == null)
                data = new SnapshotPlayerData();

            switch ((key ?? "").ToUpperInvariant())
            {
                case "COMBAT": return data.HudKillRules;
                case "BOSSES": return data.HudBossRules;
                case "SKILLS": return data.HudSkillJackpotRules;
                case "FISHING": return data.HudFishingRules;
                case "FARMING": return data.HudFarmJackpotRules;
                case "CRAFTING": return JoinUnityGuideRules(data.HudCraftRules, data.HudUniqueCraftJackpotRules);
                case "QUESTS": return data.HudMarketplaceQuestRules;
                case "EXPLORATION": return data.HudExplorationMapJackpotRules;
                case "PENALTIES": return GetDeathPenaltyHudRules(data);
                default: return data.HudKillRules;
            }
        }

        private string JoinUnityGuideRules(params string[] values)
        {
            List<string> parts = new List<string>();
            for (int i = 0; i < values.Length; i++)
            {
                if (!string.IsNullOrWhiteSpace(values[i]))
                    parts.Add(values[i]);
            }

            return string.Join("\n", parts.ToArray());
        }

        private UnityGuideRuleLine ParseUnityGuideRuleLine(string raw, ref string currentCategory, string categoryKey)
        {
            string line = (raw ?? "").Trim();
            if (line.Length == 0)
                return new UnityGuideRuleLine("", "");

            while (line.StartsWith("-", StringComparison.Ordinal) || line.StartsWith(">", StringComparison.Ordinal) || line.StartsWith("*", StringComparison.Ordinal))
                line = line.Substring(1).Trim();

            if (line.StartsWith("__GLITNIR_CAT__|", StringComparison.OrdinalIgnoreCase))
            {
                currentCategory = FriendlyUnityRuleName(line.Substring("__GLITNIR_CAT__|".Length));
                return new UnityGuideRuleLine("", "");
            }

            if (line.StartsWith("GLITNIR_CAT", StringComparison.OrdinalIgnoreCase))
                return new UnityGuideRuleLine("", "");

            string[] colonParts = line.Split(':');
            if (colonParts.Length >= 2)
            {
                string rawKey = colonParts[0].Trim();
                string action = FriendlyRuleNameForHud(rawKey);
                int points;
                if (int.TryParse(colonParts[colonParts.Length - 1].Trim(), out points))
                {
                    bool penalty = string.Equals(categoryKey, "PENALTIES", StringComparison.OrdinalIgnoreCase);
                    bool skills = string.Equals(categoryKey, "SKILLS", StringComparison.OrdinalIgnoreCase);
                    int signedPoints = penalty ? -Mathf.Abs(points) : points;
                    string category = currentCategory;
                    string middle = "";
                    if (colonParts.Length >= 3)
                    {
                        middle = skills ? " nivel " + colonParts[1].Trim() : " x" + colonParts[1].Trim();
                        if (skills)
                            category = action;
                    }
                    else if (penalty)
                    {
                        middle = " morte(s)";
                    }
                    return new UnityGuideRuleLine(action + middle, FormatSigned(signedPoints), category, rawKey);
                }
            }

            int plusIndex = line.LastIndexOf('+');
            int minusIndex = line.LastIndexOf('-');
            int pointIndex = Mathf.Max(plusIndex, minusIndex);
            if (pointIndex > 0 && line.IndexOf("pt", pointIndex, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                string action = line.Substring(0, pointIndex).Trim();
                string points = line.Substring(pointIndex).Trim();
                return new UnityGuideRuleLine(action, points, currentCategory, action);
            }

            return new UnityGuideRuleLine(FriendlyUnityRuleName(line), "", currentCategory, line);
        }

        private bool ShouldShowUnityGuideSubCategories(List<string> subCategories)
        {
            if (subCategories == null || subCategories.Count == 0)
                return false;
            if (subCategories.Count <= 1)
                return false;
            return true;
        }

        private void SetUnityGuideListBounds(bool showSubCategories)
        {
            float top = showSubCategories ? 230f : 132f;
            SetUnityRectStretch("GuideEntries/GuideRows", 28f, 28f, 42f, top);
            SetUnityVerticalScrollbarBounds("GuideEntries/GuideScrollbar", 12f, 28f, 12f, top);
        }

        private List<string> GetUnityGuideSubCategories(List<UnityGuideRuleLine> rules)
        {
            List<string> categories = new List<string>();
            if (rules == null)
                return categories;

            for (int i = 0; i < rules.Count; i++)
            {
                string category = rules[i].Category;
                if (string.IsNullOrWhiteSpace(category))
                    category = "GERAL";
                if (!categories.Any(x => string.Equals(x, category, StringComparison.OrdinalIgnoreCase)))
                    categories.Add(category);
            }

            return categories;
        }

        private int GetCurrentUnityGuideRulesCount()
        {
            SnapshotPlayerData data = _cachedPlayerData ?? new SnapshotPlayerData();
            UnityGuideCategory category = UnityGuideCategories[Mathf.Clamp(_unityHudGuideCategoryIndex, 0, UnityGuideCategories.Length - 1)];
            List<UnityGuideRuleLine> allRules = BuildUnityGuideRules(category, data);
            List<string> subCategories = GetUnityGuideSubCategories(allRules);
            if (subCategories.Count == 0)
                return allRules.Count;
            if (!ShouldShowUnityGuideSubCategories(subCategories))
                return allRules.Count;
            int subIndex = Mathf.Clamp(_unityHudGuideSubCategoryIndex, 0, subCategories.Count - 1);
            string selected = subCategories[subIndex];
            return allRules.Count(x => string.Equals(x.Category, selected, StringComparison.OrdinalIgnoreCase) ||
                                       (string.Equals(selected, "GERAL", StringComparison.OrdinalIgnoreCase) && string.IsNullOrWhiteSpace(x.Category)));
        }

        private int GetCurrentUnityGuideVisibleRows()
        {
            SnapshotPlayerData data = _cachedPlayerData ?? new SnapshotPlayerData();
            UnityGuideCategory category = UnityGuideCategories[Mathf.Clamp(_unityHudGuideCategoryIndex, 0, UnityGuideCategories.Length - 1)];
            return ShouldShowUnityGuideSubCategories(GetUnityGuideSubCategories(BuildUnityGuideRules(category, data))) ? 9 : 12;
        }

        private string GetUnityGuideRuleProgressText(UnityGuideCategory category, UnityGuideRuleLine rule, SnapshotPlayerData data)
        {
            if (data == null)
                return "";

            if (string.Equals(category.Key, "EXPLORATION", StringComparison.OrdinalIgnoreCase))
                return "";

            int count = GetUnityRuleProgressCount(category.Key, rule.RawKey, data);
            return ColorUnityGuideProgressText(count);
        }

        private string ColorUnityGuideProgressText(int count)
        {
            count = Mathf.Clamp(count, 0, int.MaxValue);
            string color = count > 0 ? "#73d7ff" : "#c9bba5";
            return "<color=" + color + ">" + count + "x</color>";
        }

        private string GetUnityGuideDescription(UnityGuideCategory category, SnapshotPlayerData data)
        {
            if (string.Equals(category.Key, "EXPLORATION", StringComparison.OrdinalIgnoreCase))
            {
                float percent = GetUnityExplorationMapProgressPercent(data);
                return "Mapa revelado: " + percent.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) +
                       "%  |  Proximo marco: " + GetNextUnityExplorationGoalText(data, percent);
            }

            return category.Description;
        }

        private string GetNextUnityExplorationGoalText(SnapshotPlayerData data, float currentPercent)
        {
            if (data == null || string.IsNullOrWhiteSpace(data.HudExplorationMapJackpotRules))
                return "sem marco configurado";

            string[] rows = data.HudExplorationMapJackpotRules.Split(new[] { ';', ',' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < rows.Length; i++)
            {
                string[] parts = (rows[i] ?? "").Split(':');
                if (parts.Length < 2)
                    continue;

                string percentText = parts[0].Replace("Mapa", "").Replace("%", "").Trim();
                int percentGoal;
                int points;
                if (!int.TryParse(percentText, out percentGoal) || !int.TryParse(parts[parts.Length - 1].Trim(), out points))
                    continue;

                if (currentPercent < percentGoal)
                    return percentGoal + "% (+" + points + " pts)";
            }

            return "completo";
        }

        private int GetUnityRuleProgressCount(string categoryKey, string prefab, SnapshotPlayerData data)
        {
            if (data == null || data.ProgressCounters == null || string.IsNullOrWhiteSpace(prefab))
                return 0;

            string safePrefab = SafeKey(prefab);
            string[] keys;
            switch ((categoryKey ?? "").ToUpperInvariant())
            {
                case "COMBAT":
                    keys = new[] { "Kill:" + safePrefab, "Combat:" + safePrefab };
                    break;
                case "BOSSES":
                    string boss = NormalizeBossPrefabName(safePrefab);
                    keys = boss.Equals(safePrefab, StringComparison.OrdinalIgnoreCase)
                        ? new[] { "Bosses:" + safePrefab }
                        : new[] { "Bosses:" + safePrefab, "Bosses:" + boss };
                    break;
                case "FISHING":
                    keys = new[] { "Fishing:" + safePrefab, "Pesca:" + safePrefab };
                    break;
                case "FARMING":
                    keys = new[] { "Farm:" + safePrefab, "Cultivo:" + safePrefab };
                    break;
                case "CRAFTING":
                    keys = new[] { "Craft:" + safePrefab, "Production:" + safePrefab, "UniqueCraft:" + safePrefab };
                    break;
                case "QUESTS":
                    keys = new[] { "MarketplaceQuest:" + safePrefab, "Marketplace:" + safePrefab, "Quest:" + safePrefab, "Quests:" + safePrefab };
                    break;
                case "PENALTIES":
                    keys = new[] { "DeathPenalty:Deaths", "Deaths:" + safePrefab };
                    break;
                default:
                    keys = new[] { SafeKey(categoryKey) + ":" + safePrefab };
                    break;
            }

            for (int i = 0; i < keys.Length; i++)
            {
                int value;
                if (data.ProgressCounters.TryGetValue(keys[i], out value))
                    return Mathf.Clamp(value, 0, int.MaxValue);
            }

            int fallback = 0;
            foreach (KeyValuePair<string, int> pair in data.ProgressCounters)
            {
                if (!string.IsNullOrWhiteSpace(pair.Key) && pair.Key.EndsWith(":" + safePrefab, StringComparison.OrdinalIgnoreCase))
                    fallback = Mathf.Clamp(fallback + Mathf.Max(0, pair.Value), 0, int.MaxValue);
            }
            return fallback;
        }

        private float GetUnityExplorationMapProgressPercent(SnapshotPlayerData data)
        {
            if (data == null)
                return 0f;

            float floatValue;
            if (data.FloatProgressCounters != null)
            {
                if (data.FloatProgressCounters.TryGetValue("ExplorationMap:Percent", out floatValue))
                    return Mathf.Clamp(floatValue, 0f, 100f);
                if (data.FloatProgressCounters.TryGetValue("Exploration:Map", out floatValue))
                    return Mathf.Clamp(floatValue, 0f, 100f);
                if (data.FloatProgressCounters.TryGetValue("Exploracao:Mapa", out floatValue))
                    return Mathf.Clamp(floatValue, 0f, 100f);
            }

            if (data.ProgressCounters == null)
                return 0f;

            int value;
            if (data.ProgressCounters.TryGetValue("ExplorationMap:Percent", out value))
                return Mathf.Clamp(value, 0, 100);
            if (data.ProgressCounters.TryGetValue("Exploration:Map", out value))
                return Mathf.Clamp(value, 0, 100);
            if (data.ProgressCounters.TryGetValue("Exploracao:Mapa", out value))
                return Mathf.Clamp(value, 0, 100);

            return 0f;
        }

        private string FriendlyUnityRuleName(string value)
        {
            string text = (value ?? "").Trim();
            if (text.Length == 0)
                return "";

            text = text.Replace("_", " ").Replace("-", " ");
            while (text.Contains("  "))
                text = text.Replace("  ", " ");

            return text.Trim();
        }

        private static readonly UnityGuideCategory[] UnityGuideCategories =
        {
            new UnityGuideCategory("COMBAT", "COMBATE", "COMBATE [ATIVO]", "Derrote criaturas configuradas para ganhar pontos."),
            new UnityGuideCategory("BOSSES", "CHEFES", "CHEFES [ATIVO]", "Vitórias contra chefes e encontros especiais."),
            new UnityGuideCategory("SKILLS", "HABILID.", "HABILIDADES [ATIVO]", "Marcos de progressao de habilidade."),
            new UnityGuideCategory("FISHING", "PESCA", "PESCA [ATIVO]", "Peixes e capturas que geram honra."),
            new UnityGuideCategory("FARMING", "CULTIVO", "CULTIVO [ATIVO]", "Colheitas e jackpots de producao."),
            new UnityGuideCategory("CRAFTING", "PRODUCAO", "PRODUCAO [ATIVO]", "Itens criados, raridades e bonus unicos."),
            new UnityGuideCategory("QUESTS", "QUESTS", "QUESTS [ATIVO]", "Missoes do Marketplace que contam para o ranking."),
            new UnityGuideCategory("EXPLORATION", "EXPLOR.", "EXPLORACAO [ATIVO]", "Revele o mapa para ativar marcos de exploracao."),
            new UnityGuideCategory("PENALTIES", "PENAL.", "PENALIDADES", "Mortes e ajustes que removem pontos.")
        };

        private struct UnityGuideCategory
        {
            public readonly string Key;
            public readonly string Label;
            public readonly string Title;
            public readonly string Description;

            public UnityGuideCategory(string key, string label, string title, string description)
            {
                Key = key;
                Label = label;
                Title = title;
                Description = description;
            }
        }

        private struct UnityGuideRuleLine
        {
            public readonly string Action;
            public readonly string Points;
            public readonly string Category;
            public readonly string RawKey;

            public UnityGuideRuleLine(string action, string points, string category = "", string rawKey = "")
            {
                Action = action ?? "";
                Points = points ?? "";
                Category = category ?? "";
                RawKey = rawKey ?? action ?? "";
            }
        }

        private void UpdateUnityHudOracle()
        {
            SnapshotPlayerData data = _cachedPlayerData ?? new SnapshotPlayerData();
            string rewardName = !string.IsNullOrWhiteSpace(data.RewardLabel) ? data.RewardLabel : data.RewardPrefabName;
            if (string.IsNullOrWhiteSpace(rewardName))
                rewardName = "-";

            SetUnityText("OracleCard/RewardRankLine/RewardRankValue", data.RewardRank > 0 ? "#" + data.RewardRank : "-");
            SetUnityText("OracleCard/RewardItemLine/RewardItemValue", rewardName + " x" + Mathf.Max(0, data.RewardAmount));
            SetUnityText("OracleCard/RewardNeedLine/RewardNeedValue", FormatPoints(data.RewardMinPoints));
            SetUnityText("OracleCard/OracleActionButton/Label", data.RewardCanClaim ? "RESGATAR" : data.RewardAlreadyClaimed ? "RESGATADO" : "BLOQUEADO");
            UpdateUnityHudRewardSlots(data);
        }

        private void UpdateUnityHudExchange()
        {
            SnapshotPlayerData data = _cachedPlayerData ?? new SnapshotPlayerData();
            if (_pointsExchangeRequestPending && _unityHudExchangePendingSince > 0f && Time.realtimeSinceStartup - _unityHudExchangePendingSince > 12f)
            {
                _pointsExchangeRequestPending = false;
                _unityHudExchangePendingSince = 0f;
                RequestSnapshotFromServer();
                SetStatus("Câmbio liberado novamente. Tente outra quantidade.", 3f);
            }

            int max = GetUnityMaxExchangePoints(data);
            if (_unityHudExchangePoints <= 0 && max > 0)
                _unityHudExchangePoints = Mathf.Min(max, GetUnityExchangeStep());
            _unityHudExchangePoints = Mathf.Clamp(_unityHudExchangePoints, 0, max);

            int receive = GetUnityExchangeCoins(_unityHudExchangePoints);
            string prefab = _rules != null && !string.IsNullOrWhiteSpace(_rules.PointsExchangePrefab) ? _rules.PointsExchangePrefab : "Coins";
            SetUnityText("ExchangeCard/AvailableLine/AvailableValue", FormatPoints(data.Points));
            SetUnityText("ExchangeCard/SelectedLine/SelectedValue", FormatPoints(_unityHudExchangePoints));
            SetUnityText("ExchangeCard/ReceiveLine/ReceiveValue", receive + " " + prefab);
            SetUnityText("ExchangeCard/RateLine/RateValue", GetUnityExchangeRateText());
            SetUnityText("ExchangePreviewCost", "-" + FormatPoints(_unityHudExchangePoints));
            SetUnityText("ExchangePreviewReceive", "+" + receive + " " + prefab);
            SetUnityText("ExchangeCard/ExchangeClaimButton/Label", _pointsExchangeRequestPending ? "PROCESSANDO" : "RESGATAR");
            if (_unityHudExchangeInputField != null && !IsUnityInputFocused(_unityHudExchangeInputField))
                _unityHudExchangeInputField.text = _unityHudExchangePoints > 0 ? _unityHudExchangePoints.ToString() : "";
        }

        private void UpdateUnityHudRewardSlots(SnapshotPlayerData data)
        {
            for (int rank = 1; rank <= 3; rank++)
            {
                RankRewardInfo reward = _rules != null ? GetRankRewardInfo(rank) : null;
                string path = "RewardSlot" + rank;
                if (ResolveUnityHudPath(path) == null)
                    continue;

                bool configured = reward != null && !string.IsNullOrWhiteSpace(reward.PrefabName) && reward.Amount > 0;
                SetUnityText(path + "/Label", configured && !string.IsNullOrWhiteSpace(reward.Label) ? reward.Label : "Recompensa nao configurada");
                SetUnityText(path + "/Prefab", "Prefab: " + (configured ? reward.PrefabName : "-"));
                SetUnityText(path + "/Amount", "Qtd: " + (configured ? Mathf.Max(0, reward.Amount).ToString() : "0"));
                SetUnityText(path + "/Min", "Minimo: " + FormatPoints(reward != null ? reward.MinPoints : 0) + (data != null && data.RewardRank == rank ? "  SUA POSICAO" : ""));
            }
        }

        private string BuildUnityRewardInfoText(SnapshotPlayerData data, string rewardName)
        {
            if (data == null)
                data = new SnapshotPlayerData();

            List<string> lines = new List<string>();
            lines.Add(data.RewardCanClaim
                ? "Recompensa liberada. Ao resgatar, o premio entra no inventario."
                : data.RewardAlreadyClaimed
                    ? "Recompensa ja resgatada nesta temporada."
                    : string.IsNullOrWhiteSpace(data.RewardBlockReason)
                        ? "Continue ganhando honra para liberar a recompensa."
                        : data.RewardBlockReason);
            lines.Add("Posicao: " + (data.RewardRank > 0 ? "#" + data.RewardRank : "-") + "   Seus pontos: " + FormatPoints(data.Points));
            lines.Add("Premio: " + rewardName + " x" + Mathf.Max(0, data.RewardAmount));
            lines.Add("Minimo para resgatar: " + FormatPoints(data.RewardMinPoints));
            lines.Add("Temporada: " + (string.IsNullOrWhiteSpace(data.RewardClaimCycleId) ? "atual" : data.RewardClaimCycleId));
            return string.Join("\n", lines.ToArray());
        }

        private string BuildUnityExchangeInfoText(SnapshotPlayerData data, int max, int receive, string prefab)
        {
            if (data == null)
                data = new SnapshotPlayerData();

            List<string> lines = new List<string>();
            if (_rules == null || !_rules.PointsExchangeEnabled)
                lines.Add("Cambio desativado pelo servidor.");
            else if (_pointsExchangeRequestPending)
                lines.Add("Processando cambio. Aguarde a confirmacao do servidor.");
            else
                lines.Add("Escolha quantos pontos deseja converter e confirme em RESGATAR.");

            lines.Add("Disponivel: " + FormatPoints(data.Points) + "   Limite desta troca: " + FormatPoints(max));
            lines.Add("Selecionado: " + FormatPoints(_unityHudExchangePoints) + "   Voce recebe: " + receive + " " + prefab);
            lines.Add("Taxa: " + GetUnityExchangeRateText());

            int min = GetUnityExchangeStep();
            if (max <= 0)
                lines.Add("Sem pontos suficientes para cambio agora.");
            else if (_unityHudExchangePoints > 0 && _unityHudExchangePoints < min)
                lines.Add("Minimo recomendado: " + FormatPoints(min) + ".");

            return string.Join("\n", lines.ToArray());
        }

        private void UpdateUnityHudPlayers()
        {
            int count = _cachedTopEntries != null ? _cachedTopEntries.Count : 0;
            const int visibleRows = 7;
            _unityHudPlayersScrollIndex = Mathf.Clamp(_unityHudPlayersScrollIndex, 0, Mathf.Max(0, count - visibleRows));
            for (int i = 0; i < visibleRows; i++)
            {
                int dataIndex = _unityHudPlayersScrollIndex + i;
                SnapshotTopEntryData entry = dataIndex < count ? _cachedTopEntries[dataIndex] : null;
                string row = "PlayersListPanel/PlayersRows/PlayerRow" + (i + 1);
                Transform rowTransform = ResolveUnityHudPath(row);
                bool showFallback = count == 0 && i == 0;
                if (rowTransform != null && rowTransform.gameObject.activeSelf != (entry != null || showFallback))
                    rowTransform.gameObject.SetActive(entry != null || showFallback);
                if (entry == null && !showFallback)
                    continue;

                if (showFallback)
                {
                    SetUnityText(row + "/Position", "--");
                    SetUnityText(row + "/Player", "Aguardando dados do servidor");
                    SetUnityText(row + "/Points", "");
                    SetUnityText(row + "/Summary", "Atualize o ranking ou aguarde a sincronizacao.");
                }
                else
                {
                    SetUnityText(row + "/Position", entry.Position.ToString("00"));
                    SetUnityText(row + "/Player", string.IsNullOrWhiteSpace(entry.PlayerName) ? "WARRIOR" : entry.PlayerName);
                    SetUnityText(row + "/Points", FormatPoints(entry.Points));
                    SetUnityText(row + "/Summary", BuildUnityPlayerSummary(entry));
                }
            }
            UpdateUnityHudPlayersPodium(count);
            SetUnityScrollbar("PlayersListPanel/PlayersScrollbar", count, visibleRows, _unityHudPlayersScrollIndex);
        }

        private void UpdateUnityHudPlayersPodium(int count)
        {
            UpdateUnityHudPodium("PlayersPodium/PlayersRank1", count > 0 ? _cachedTopEntries[0] : null);
            UpdateUnityHudPodium("PlayersPodium/PlayersRank2", count > 1 ? _cachedTopEntries[1] : null);
            UpdateUnityHudPodium("PlayersPodium/PlayersRank3", count > 2 ? _cachedTopEntries[2] : null);
        }

        private string BuildUnityPlayerSummary(SnapshotTopEntryData entry)
        {
            if (entry == null)
                return "";

            return ColorUnitySummaryPart("Kills " + entry.TotalKillsPontuadas, "#ffb13b") +
                   "  " + ColorUnitySummaryPart("Bosses " + entry.TotalBossesPontuadas, "#d69cff") +
                   "  " + ColorUnitySummaryPart("Skills " + entry.TotalSkillLevelUpsPontuados, "#73d7ff") +
                   "  " + ColorUnitySummaryPart("Craft " + entry.TotalCraftPontuadas, "#ffd66b") +
                   "  " + ColorUnitySummaryPart("Mortes " + entry.TotalDeaths + " (-" + FormatPoints(entry.DeathPenaltyPointsTotal) + ")", "#ff5555") +
                   "  " + ColorUnitySummaryPart("Cambios " + entry.TotalPointsExchanges, "#5dff6a");
        }

        private string ColorUnitySummaryPart(string text, string color)
        {
            if (string.IsNullOrWhiteSpace(text))
                return "";

            return "<color=" + color + ">" + text + "</color>";
        }

        private void AdjustUnityExchangePoints(int delta)
        {
            SetUnityExchangePoints(_unityHudExchangePoints + delta);
        }

        private void SetUnityExchangePoints(int value)
        {
            _unityHudExchangePoints = Mathf.Clamp(value, 0, GetUnityMaxExchangePoints(_cachedPlayerData));
            if (_rules != null && _rules.PointsExchangeUsePointsPerCoin)
            {
                int step = Mathf.Max(1, _rules.PointsExchangePointsPerCoin);
                _unityHudExchangePoints = (_unityHudExchangePoints / step) * step;
            }
            _unityHudNextRefresh = 0f;
            UpdateUnityRankingHud();
        }

        private void BindUnityHudExchangeInput()
        {
            Transform transform = ResolveUnityHudPath("ExchangeAmountInput");
            if (transform == null)
                return;

            _unityHudExchangeInputField = transform.GetComponent<InputField>();
            if (_unityHudExchangeInputField == null)
                _unityHudExchangeInputField = transform.gameObject.AddComponent<InputField>();

            Text text = UnityHudText("ExchangeAmountInput/Text");
            Text placeholder = UnityHudText("ExchangeAmountInput/Placeholder");
            _unityHudExchangeInputField.textComponent = text;
            _unityHudExchangeInputField.placeholder = placeholder;
            _unityHudExchangeInputField.contentType = InputField.ContentType.Standard;
            _unityHudExchangeInputField.characterValidation = InputField.CharacterValidation.None;
            _unityHudExchangeInputField.characterLimit = 9;
            _unityHudExchangeInputField.onValidateInput = ValidateUnityExchangeDigit;
            _unityHudExchangeInputField.onValueChanged.RemoveAllListeners();
            _unityHudExchangeInputField.onValueChanged.AddListener(delegate(string value)
            {
                int amount;
                if (int.TryParse((value ?? "").Trim(), out amount))
                    _unityHudExchangePoints = Mathf.Clamp(amount, 0, GetUnityMaxExchangePoints(_cachedPlayerData));
                _unityHudNextRefresh = 0f;
            });
            _unityHudExchangeInputField.onEndEdit.RemoveAllListeners();
            _unityHudExchangeInputField.onEndEdit.AddListener(delegate(string value)
            {
                int amount;
                if (int.TryParse((value ?? "").Trim(), out amount))
                    SetUnityExchangePoints(amount);
                else
                    SetUnityExchangePoints(0);
            });
        }

        private bool IsUnityInputFocused(InputField input)
        {
            return input != null &&
                   EventSystem.current != null &&
                   EventSystem.current.currentSelectedGameObject == input.gameObject;
        }

        private char ValidateUnityExchangeDigit(string text, int charIndex, char addedChar)
        {
            return addedChar >= '0' && addedChar <= '9' ? addedChar : '\0';
        }

        private int GetUnityExchangeStep()
        {
            if (_rules != null && _rules.PointsExchangeUsePointsPerCoin)
                return Mathf.Max(1, _rules.PointsExchangePointsPerCoin);
            return 1;
        }

        private int GetUnityMaxExchangePoints(SnapshotPlayerData player)
        {
            if (_rules == null || !_rules.PointsExchangeEnabled || player == null)
                return 0;

            int points = Mathf.Max(0, player.Points);
            int max = _rules.PointsExchangeMaxPointsPerRequest > 0 ? Mathf.Min(points, _rules.PointsExchangeMaxPointsPerRequest) : points;
            if (_rules.PointsExchangeUsePointsPerCoin)
            {
                int step = Mathf.Max(1, _rules.PointsExchangePointsPerCoin);
                max = (max / step) * step;
            }
            return Mathf.Max(0, max);
        }

        private int GetUnityExchangeCoins(int points)
        {
            if (_rules == null || points <= 0)
                return 0;
            if (_rules.PointsExchangeUsePointsPerCoin)
                return points / Mathf.Max(1, _rules.PointsExchangePointsPerCoin);
            if (_rules.PointsExchangeUseCoinsPerPoint)
                return points * Mathf.Max(1, _rules.PointsExchangeCoinsPerPoint);
            return points;
        }

        private string GetUnityExchangeRateText()
        {
            if (_rules == null)
                return "Cambio indisponivel";
            if (_rules.PointsExchangeUsePointsPerCoin)
                return Mathf.Max(1, _rules.PointsExchangePointsPerCoin) + " pontos = 1 moeda";
            if (_rules.PointsExchangeUseCoinsPerPoint)
                return "1 ponto = " + Mathf.Max(1, _rules.PointsExchangeCoinsPerPoint) + " moedas";
            return "1 ponto = 1 moeda";
        }

        private string FormatSigned(int value)
        {
            return value >= 0 ? "+" + FormatPoints(value) : "-" + FormatPoints(Mathf.Abs(value));
        }

        private void SetUnityText(string pathOrName, string value)
        {
            Text text = UnityHudText(pathOrName);
            if (text != null)
            {
                text.supportRichText = true;
                text.text = value ?? "";
            }
        }

        private void SetUnityChildText(string parentPathOrName, string childName, string value)
        {
            Transform parent = ResolveUnityHudPath(parentPathOrName);
            if (parent == null || string.IsNullOrWhiteSpace(childName))
                return;

            Transform child = FindDirectChild(parent, childName);
            Text text = child != null ? child.GetComponent<Text>() : null;
            if (text == null)
            {
                Text[] texts = parent.GetComponentsInChildren<Text>(true);
                for (int i = 0; i < texts.Length; i++)
                {
                    if (texts[i] != null && texts[i].name.Equals(childName, StringComparison.OrdinalIgnoreCase))
                    {
                        text = texts[i];
                        break;
                    }
                }
            }

            if (text != null)
                text.text = value ?? "";
        }

        private void SetUnityObjectActive(string pathOrName, bool active)
        {
            Transform transform = ResolveUnityHudPath(pathOrName);
            if (transform != null && transform.gameObject.activeSelf != active)
                transform.gameObject.SetActive(active);
        }

        private void SetUnityFill(string pathOrName, float value)
        {
            Transform transform = ResolveUnityHudPath(pathOrName);
            if (transform == null)
                return;

            RectTransform rect = transform as RectTransform;
            if (rect == null)
                return;

            Vector2 anchorMax = rect.anchorMax;
            anchorMax.x = Mathf.Clamp01(value);
            rect.anchorMax = anchorMax;
        }

        private void SetUnityRectStretch(string pathOrName, float left, float bottom, float right, float top)
        {
            Transform transform = ResolveUnityHudPath(pathOrName);
            RectTransform rect = transform as RectTransform;
            if (rect == null)
                return;

            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(left, bottom);
            rect.offsetMax = new Vector2(-right, -top);
        }

        private void SetUnityVerticalScrollbarBounds(string pathOrName, float width, float bottom, float right, float top)
        {
            Transform transform = ResolveUnityHudPath(pathOrName);
            RectTransform rect = transform as RectTransform;
            if (rect == null)
                return;

            rect.anchorMin = new Vector2(1f, 0f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(1f, 0.5f);
            rect.sizeDelta = new Vector2(width, -(bottom + top));
            rect.anchoredPosition = new Vector2(-right, 0f);
        }

        private void SetUnityScrollbar(string path, int total, int visible, int offset)
        {
            Transform bar = ResolveUnityHudPath(path);
            Transform thumb = ResolveUnityHudPath(path + "/Thumb");
            if (bar == null || thumb == null)
                return;

            bool active = total > visible;
            if (bar.gameObject.activeSelf != active)
                bar.gameObject.SetActive(active);
            if (!active)
                return;

            RectTransform barRect = bar as RectTransform;
            RectTransform thumbRect = thumb as RectTransform;
            if (barRect == null || thumbRect == null)
                return;

            float height = Mathf.Max(1f, barRect.rect.height);
            float thumbHeight = Mathf.Clamp(height * (visible / (float)Mathf.Max(visible, total)), 36f, height);
            int maxOffset = Mathf.Max(1, total - visible);
            float y = (height - thumbHeight) * Mathf.Clamp01(offset / (float)maxOffset);

            thumbRect.anchorMin = new Vector2(0f, 1f);
            thumbRect.anchorMax = new Vector2(1f, 1f);
            thumbRect.pivot = new Vector2(0.5f, 1f);
            thumbRect.offsetMin = new Vector2(0f, -thumbHeight - y);
            thumbRect.offsetMax = new Vector2(0f, -y);
        }

        private void SetScrollFromMouse(string path, ref int offset, int total, int visible)
        {
            Transform bar = ResolveUnityHudPath(path);
            RectTransform barRect = bar as RectTransform;
            if (barRect == null || total <= visible)
                return;

            Vector2 local;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(barRect, Input.mousePosition, null, out local);
            float normalized = Mathf.Clamp01(1f - ((local.y - barRect.rect.yMin) / Mathf.Max(1f, barRect.rect.height)));
            offset = Mathf.RoundToInt(normalized * Mathf.Max(0, total - visible));
            _unityHudNextRefresh = 0f;
            UpdateUnityRankingHud();
        }

        private sealed class GlitnirButtonFx : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
        {
            private RectTransform _rect;
            private GameObject _hoverGlow;
            private GameObject _selectedGlow;
            private Vector3 _baseScale = Vector3.one;
            private Vector3 _targetScale = Vector3.one;
            private bool _hovered;
            private bool _pressed;
            private bool _selected;

            private void Awake()
            {
                _rect = transform as RectTransform;
                _baseScale = transform.localScale;
                _targetScale = _baseScale;
                Transform hover = transform.Find("HoverGlow");
                Transform selected = transform.Find("SelectedGlow");
                _hoverGlow = hover != null ? hover.gameObject : null;
                _selectedGlow = selected != null ? selected.gameObject : null;
                RefreshGlow();
            }

            private void OnEnable()
            {
                _pressed = false;
                _hovered = false;
                _targetScale = _baseScale;
                RefreshGlow();
            }

            private void Update()
            {
                if (_rect == null)
                    return;

                _rect.localScale = Vector3.Lerp(_rect.localScale, _targetScale, Time.unscaledDeltaTime * 14f);
            }

            public void SetSelected(bool selected)
            {
                _selected = selected;
                RefreshGlow();
            }

            public void OnPointerEnter(PointerEventData eventData)
            {
                _hovered = true;
                _targetScale = _baseScale * 1.035f;
                RefreshGlow();
            }

            public void OnPointerExit(PointerEventData eventData)
            {
                _hovered = false;
                _pressed = false;
                _targetScale = _baseScale;
                RefreshGlow();
            }

            public void OnPointerDown(PointerEventData eventData)
            {
                _pressed = true;
                _targetScale = _baseScale * 0.975f;
                RefreshGlow();
            }

            public void OnPointerUp(PointerEventData eventData)
            {
                _pressed = false;
                _targetScale = _hovered ? _baseScale * 1.035f : _baseScale;
                RefreshGlow();
            }

            private void RefreshGlow()
            {
                if (_hoverGlow != null)
                    _hoverGlow.SetActive(_hovered || _pressed);
                if (_selectedGlow != null)
                    _selectedGlow.SetActive(_selected);
            }
        }

        private void HandleUnityHudKeyboard()
        {
            if (!_hudVisible || _unityHudCanvas == null || !_unityHudCanvas.activeSelf)
                return;

            if (IsUnityInputFocused(_unityHudExchangeInputField))
                return;

            if (Input.GetKeyDown(KeyCode.Alpha1))
                SetUnityHudTab(0);
            else if (Input.GetKeyDown(KeyCode.Alpha2))
                SetUnityHudTab(1);
            else if (Input.GetKeyDown(KeyCode.Alpha3))
                SetUnityHudTab(2);
            else if (Input.GetKeyDown(KeyCode.Alpha4))
                SetUnityHudTab(3);
            else if (Input.GetKeyDown(KeyCode.Alpha5))
                SetUnityHudTab(4);
            else if (Input.GetKeyDown(KeyCode.Alpha6))
                SetUnityHudTab(5);

            float wheel = Input.mouseScrollDelta.y;
            if (Mathf.Abs(wheel) > 0.01f)
            {
                int delta = wheel < 0f ? 3 : -3;
                if (_unityHudTabIndex == 0)
                    _unityHudLeaderboardScrollIndex += delta;
                else if (_unityHudTabIndex == 2)
                    _unityHudGuideScrollIndex += delta;
                else if (_unityHudTabIndex == 5)
                    _unityHudPlayersScrollIndex += delta;

                _unityHudNextRefresh = 0f;
                UpdateUnityRankingHud();
            }
        }
    }
}
