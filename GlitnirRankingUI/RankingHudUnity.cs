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
        private bool _unityHudTickLogged;
        private float _unityHudNextRefresh;
        private int _unityHudTabIndex;

        private void TickUnityRankingHud()
        {
            if (Application.isBatchMode)
                return;

            if (!_unityHudTickLogged)
            {
                _unityHudTickLogged = true;
                Logger.LogWarning("[Glitnir Ranking] HUD Unity tick ativo no cliente.");
            }

            if (!EnsureUnityRankingHudLoaded())
                return;

            if (_unityHudCanvas != null && !_unityHudCanvas.activeSelf)
                _unityHudCanvas.SetActive(true);

            SetUnityViewActive("MainPanel", _hudVisible);
            SetUnityViewActive("CollapsedBadge", !_hudVisible);

            if (_hudVisible)
            {
                Cursor.visible = true;
                if (Cursor.lockState != CursorLockMode.None)
                    Cursor.lockState = CursorLockMode.None;
                HandleUnityHudKeyboard();
                UpdateUnityRankingHud();
            }
        }

        private bool TryShowUnityRankingHud()
        {
            if (Application.isBatchMode)
                return false;

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
                    return false;
                }

                _unityHudBundle = AssetBundle.LoadFromMemory(bytes);
                if (_unityHudBundle == null)
                {
                    Logger.LogWarning("[Glitnir Ranking] AssetBundle do HUD Unity não pôde ser carregado.");
                    return false;
                }

                string assetName = _unityHudBundle.GetAllAssetNames()
                    .FirstOrDefault(name => name.EndsWith("glitnirrankinghud.prefab", StringComparison.OrdinalIgnoreCase));
                if (string.IsNullOrWhiteSpace(assetName))
                    assetName = _unityHudBundle.GetAllAssetNames().FirstOrDefault();
                if (string.IsNullOrWhiteSpace(assetName))
                {
                    Logger.LogWarning("[Glitnir Ranking] AssetBundle do HUD Unity não contém prefab.");
                    return false;
                }

                GameObject prefab = _unityHudBundle.LoadAsset<GameObject>(assetName);
                if (prefab == null)
                {
                    Logger.LogWarning("[Glitnir Ranking] Prefab do HUD Unity não pôde ser carregado: " + assetName);
                    return false;
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
                return false;
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

            button.transition = Selectable.Transition.ColorTint;
            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1f, 0.86f, 0.42f, 1f);
            colors.pressedColor = new Color(0.72f, 0.18f, 0.12f, 1f);
            colors.selectedColor = colors.highlightedColor;
            colors.colorMultiplier = 1f;
            button.colors = colors;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(action);
        }

        private void SetUnityHudTab(int index)
        {
            _unityHudTabIndex = Mathf.Clamp(index, 0, 3);
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
            Transform current = _unityHudCanvas.transform;
            for (int i = 0; i < parts.Length; i++)
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
        }

        private void UpdateUnityHudTabs()
        {
            SetUnityViewActive("ViewLeaderboard", _unityHudTabIndex == 0);
            SetUnityViewActive("ViewPerformance", _unityHudTabIndex == 1);
            SetUnityViewActive("ViewGuide", _unityHudTabIndex == 2);
            SetUnityViewActive("ViewOracle", _unityHudTabIndex == 3);
            SetUnityTabVisual("TabLeaderboard", _unityHudTabIndex == 0);
            SetUnityTabVisual("TabPerformance", _unityHudTabIndex == 1);
            SetUnityTabVisual("TabGuide", _unityHudTabIndex == 2);
            SetUnityTabVisual("TabOracle", _unityHudTabIndex == 3);
        }

        private void SetUnityTabVisual(string pathOrName, bool active)
        {
            Transform transform = ResolveUnityHudPath(pathOrName);
            if (transform == null)
                return;

            Image image = transform.GetComponent<Image>();
            if (image != null)
                image.color = active ? new Color(0.24f, 0.17f, 0.08f, 0.96f) : new Color(0.09f, 0.10f, 0.10f, 0.88f);
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
            for (int i = 0; i < 10; i++)
            {
                SnapshotTopEntryData entry = i < count ? _cachedTopEntries[i] : null;
                string row = "LeaderboardListPanel/Rows/Row" + (i + 1);
                Transform rowTransform = ResolveUnityHudPath(row);
                if (rowTransform != null && rowTransform.gameObject.activeSelf != (entry != null))
                    rowTransform.gameObject.SetActive(entry != null);

                if (entry == null)
                    continue;

                SetUnityText(row + "/Position", entry.Position.ToString("00"));
                SetUnityText(row + "/Player", string.IsNullOrWhiteSpace(entry.PlayerName) ? "WARRIOR" : entry.PlayerName);
                SetUnityText(row + "/Points", FormatPoints(entry.Points));
                SetUnityText(row + "/Title", GetRankTitle(entry.Position).ToUpperInvariant());
            }

            UpdateUnityHudPodium("Podium/Rank1", count > 0 ? _cachedTopEntries[0] : null);
            UpdateUnityHudPodium("Podium/Rank2", count > 1 ? _cachedTopEntries[1] : null);
            UpdateUnityHudPodium("Podium/Rank3", count > 2 ? _cachedTopEntries[2] : null);
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
            SetUnityText("PlayerProfile/PlayerRank", "RANK #" + Mathf.Max(1, data.Position) + "  |  " + FormatPoints(data.Points) + " POINTS");
            SetUnityText("ProgressSummary/MetricKills/Value", data.TotalKillsPontuadas.ToString());
            SetUnityText("ProgressSummary/MetricCrafts/Value", data.TotalCraftPontuadas.ToString());
            SetUnityText("ProgressSummary/MetricQuests/Value", data.TotalMarketplaceQuestsPontuadas.ToString());
            SetUnityText("ProgressSummary/MetricFish/Value", data.TotalFishingPontuadas.ToString());

            int total = Mathf.Max(1, data.Points + data.DeathPenaltyPointsTotal + data.PointsExchangePenaltyTotal);
            SetUnityFill("PlayerProfile/CombatBar/Track/Fill", (data.KillPointsTotal + data.BossPointsTotal) / (float)total);
            SetUnityFill("PlayerProfile/CraftBar/Track/Fill", data.CraftPointsTotal / (float)total);
            SetUnityFill("PlayerProfile/ExploreBar/Track/Fill", data.ExplorationMapJackpotPointsTotal / (float)total);

            string[] deeds =
            {
                "KILLS SCORED  +" + FormatPoints(data.KillPointsTotal),
                "BOSSES SCORED  +" + FormatPoints(data.BossPointsTotal),
                "SKILLS ADVANCED  +" + FormatPoints(data.SkillPointsTotal),
                "MARKET QUESTS  +" + FormatPoints(data.MarketplaceQuestPointsTotal),
                "FISHING  +" + FormatPoints(data.FishingPointsTotal),
                "CRAFTING  +" + FormatPoints(data.CraftPointsTotal),
                "LAST DEED  " + (string.IsNullOrWhiteSpace(data.LastReason) ? "NONE" : data.LastReason)
            };

            for (int i = 0; i < deeds.Length; i++)
                SetUnityText("RecentDeedsPanel/RecentDeedsRows/DeedRow" + (i + 1) + "/Text", deeds[i]);
        }

        private void UpdateUnityHudGuide()
        {
            string[] categories = { "COMBAT", "CRAFTING", "EXPLORATION", "MARKETPLACE", "FISHING", "FARMING" };
            for (int i = 0; i < categories.Length; i++)
                SetUnityText("GuideCategories/GuideCategory" + (i + 1) + "/Label", categories[i]);

            SnapshotPlayerData data = _cachedPlayerData ?? new SnapshotPlayerData();
            string[] rules =
            {
                "KILLS|" + FormatSigned(data.KillPointsTotal),
                "BOSSES|" + FormatSigned(data.BossPointsTotal),
                "SKILLS|" + FormatSigned(data.SkillPointsTotal),
                "QUESTS|" + FormatSigned(data.MarketplaceQuestPointsTotal),
                "FISHING|" + FormatSigned(data.FishingPointsTotal),
                "CRAFTING|" + FormatSigned(data.CraftPointsTotal),
                "FARMING|" + FormatSigned(data.FarmJackpotPointsTotal),
                "UNIQUE ITEMS|" + FormatSigned(data.UniqueCraftJackpotPointsTotal),
                "DEATHS|-" + FormatPoints(data.DeathPenaltyPointsTotal)
            };

            for (int i = 0; i < rules.Length; i++)
            {
                string[] pair = rules[i].Split('|');
                SetUnityText("GuideEntries/GuideRows/RuleRow" + (i + 1) + "/Action", pair[0]);
                SetUnityText("GuideEntries/GuideRows/RuleRow" + (i + 1) + "/Points", pair.Length > 1 ? pair[1] : "");
            }
        }

        private void UpdateUnityHudOracle()
        {
            SnapshotPlayerData data = _cachedPlayerData ?? new SnapshotPlayerData();
            SetUnityText("OracleCard/OracleText", data.RewardCanClaim
                ? "A reward waits for your name in Glitnir's hall."
                : string.IsNullOrWhiteSpace(data.RewardBlockReason) ? "Keep earning honor to awaken the oracle." : data.RewardBlockReason);
            SetUnityText("OracleCard/OracleActionButton/Label", data.RewardCanClaim ? "CLAIM REWARD" : "LOCKED");
        }

        private string FormatSigned(int value)
        {
            return value >= 0 ? "+" + FormatPoints(value) : "-" + FormatPoints(Mathf.Abs(value));
        }

        private void SetUnityText(string pathOrName, string value)
        {
            Text text = UnityHudText(pathOrName);
            if (text != null)
                text.text = value ?? "";
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

        private void HandleUnityHudKeyboard()
        {
            if (!_hudVisible || _unityHudCanvas == null || !_unityHudCanvas.activeSelf)
                return;

            if (Input.GetKeyDown(KeyCode.Alpha1))
                SetUnityHudTab(0);
            else if (Input.GetKeyDown(KeyCode.Alpha2))
                SetUnityHudTab(1);
            else if (Input.GetKeyDown(KeyCode.Alpha3))
                SetUnityHudTab(2);
            else if (Input.GetKeyDown(KeyCode.Alpha4))
                SetUnityHudTab(3);
        }
    }
}
