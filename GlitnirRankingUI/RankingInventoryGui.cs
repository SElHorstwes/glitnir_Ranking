using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace Glitnir.Ranking
{
    public partial class GlitnirRankingPlugin
    {
        private GameObject _inventoryRankingButton;
        private GameObject _inventoryRankingPanel;
        private Text _inventoryRankingTitle;
        private Text _inventoryRankingBody;
        private RectTransform _inventoryRankingBodyRect;
        private ScrollRect _inventoryRankingScrollRect;
        private GameObject _inventoryGuideSidebar;
        private GameObject _inventoryGuideSubSidebar;
        private readonly List<GameObject> _inventoryGuideCategoryButtons = new List<GameObject>();
        private readonly List<GameObject> _inventoryGuideSubCategoryButtons = new List<GameObject>();
        private int _inventoryGuideCategoryIndex;
        private string _inventoryGuideSubCategory = "";
        private string[] _inventoryGuideVisibleSubCategories = new string[0];
        private readonly HashSet<string> _inventoryGuideExpandedBlocks = new HashSet<string>();
        private GameObject _inventoryRankingPrimaryAction;
        private Text _inventoryRankingPrimaryActionLabel;
        private GameObject _inventoryRankingSecondaryAction;
        private Text _inventoryRankingSecondaryActionLabel;
        private GameObject _inventoryExchangeMinusAction;
        private Text _inventoryExchangeMinusLabel;
        private GameObject _inventoryExchangePlusAction;
        private Text _inventoryExchangePlusLabel;
        private GameObject _inventoryExchangeMaxAction;
        private Text _inventoryExchangeMaxLabel;
        private Text _inventoryExchangeAmountLabel;
        private bool _inventoryRankingOpen;
        private float _inventoryRankingNextRefresh;
        private int _inventoryRankingTab;
        private int _inventoryExchangePoints;

        private bool TryToggleInventoryRanking()
        {
            return false;
        }

        internal void EnsureInventoryRankingGui(InventoryGui gui)
        {
            DestroyInventoryRankingGui();
            return;
        }

        private void DestroyInventoryRankingGui()
        {
            if (_inventoryRankingButton != null)
            {
                try { Destroy(_inventoryRankingButton); } catch { }
                _inventoryRankingButton = null;
            }

            if (_inventoryRankingPanel != null)
            {
                try { Destroy(_inventoryRankingPanel); } catch { }
                _inventoryRankingPanel = null;
            }

            _inventoryRankingOpen = false;
        }

        private void EnsureInventoryRankingGuiLegacy(InventoryGui gui)
        {
            if (gui == null)
                return;

            if (_inventoryRankingButton != null && _inventoryRankingPanel != null)
                return;

            Transform buttonRoot = gui.m_player != null ? gui.m_player : gui.transform;
            Transform panelRoot = gui.transform;
            _inventoryRankingButton = CreateInventoryRankingButton(gui, buttonRoot);
            _inventoryRankingPanel = CreateInventoryRankingPanel(gui, panelRoot);
            _inventoryRankingPanel.SetActive(false);
            UpdateInventoryRankingContent(true);
        }

        internal void UpdateInventoryRankingGui(InventoryGui gui)
        {
            DestroyInventoryRankingGui();
            return;
        }

        private void UpdateInventoryRankingGuiLegacy(InventoryGui gui)
        {
            if (gui == null)
                return;

            EnsureInventoryRankingGui(gui);

            bool visible = InventoryGui.IsVisible();
            if (_inventoryRankingButton != null && _inventoryRankingButton.activeSelf != visible)
                _inventoryRankingButton.SetActive(visible);

            if (!visible)
            {
                SetInventoryRankingOpen(false);
                return;
            }

            if (_inventoryRankingOpen)
                UpdateInventoryRankingContent(false);
        }

        private void SetInventoryRankingOpen(bool open)
        {
            _inventoryRankingOpen = open;
            if (_inventoryRankingPanel != null)
            {
                if (_inventoryRankingPanel.activeSelf != open)
                    _inventoryRankingPanel.SetActive(open);

                if (open)
                    _inventoryRankingPanel.transform.SetAsLastSibling();
            }

            if (open)
            {
                RequestSnapshotFromServer();
                try
                {
                    UpdateInventoryRankingContent(true);
                    Canvas.ForceUpdateCanvases();
                }
                catch (Exception ex)
                {
                    Logger.LogWarning("[Glitnir Ranking] Falha ao atualizar conteudo do HUD de inventario: " + ex);
                    if (_inventoryRankingBody != null)
                        _inventoryRankingBody.text = "<color=#ffd36a><b>GLITNIR RANKING</b></color>\n<color=#ff7777>Falha ao atualizar conteudo. Use Atualizar ou reabra o HUD.</color>";
                }
            }
        }

        private GameObject CreateInventoryRankingButton(InventoryGui gui, Transform root)
        {
            GameObject buttonObject = CloneNativeButton(gui, root, "GlitnirRanking_TabButton", "RANKING");
            RectTransform rect = buttonObject.GetComponent<RectTransform>();
            rect.SetAsLastSibling();
            rect.anchorMin = new Vector2(1f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(1f, 1f);
            rect.anchoredPosition = new Vector2(-18f, -20f);
            rect.sizeDelta = new Vector2(112f, 30f);

            Button button = buttonObject.GetComponent<Button>();
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(delegate { SetInventoryRankingOpen(!_inventoryRankingOpen); });
            return buttonObject;
        }

        private GameObject CreateInventoryRankingPanel(InventoryGui gui, Transform root)
        {
            Transform backgroundPrefab = gui.m_player != null ? gui.m_player.Find("Bkg") : root.Find("Bkg");
            GameObject panel = backgroundPrefab != null
                ? Instantiate(backgroundPrefab.gameObject)
                : new GameObject("GlitnirRanking_NativePanel", typeof(RectTransform), typeof(Image));

            panel.name = "GlitnirRanking_NativePanel";
            panel.transform.SetParent(root, false);
            RectTransform rect = panel.GetComponent<RectTransform>();
            rect.SetAsLastSibling();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(0f, -24f);
            rect.sizeDelta = new Vector2(920f, 620f);

            Image image = panel.GetComponent<Image>();
            if (image != null)
            {
                image.color = new Color(0.34f, 0.19f, 0.09f, 0.88f);
                image.raycastTarget = true;
            }

            GameObject header = CreateInventoryPanel("Header", panel.transform, InventoryStretch(14f, 562f, 14f, 14f), new Color(0.11f, 0.06f, 0.028f, 0.86f));
            _inventoryRankingTitle = CreateInventoryText("Title", header.transform, InventoryStretch(20f, 8f, 84f, 8f), "GLITNIR RANKING", 28, new Color(1f, 0.76f, 0.28f, 1f), TextAnchor.MiddleLeft, true);

            GameObject close = CreateInventoryRankingCloseButton(gui, header.transform);
            close.transform.SetAsLastSibling();

            GameObject tabs = CreateInventoryPanel("Tabs", panel.transform, InventoryStretch(18f, 502f, 18f, 72f), new Color(0f, 0f, 0f, 0f));
            CreateInventoryRankingTab(gui, tabs.transform, 0, "TOP");
            CreateInventoryRankingTab(gui, tabs.transform, 1, "PERFIL");
            CreateInventoryRankingTab(gui, tabs.transform, 2, "REGRAS");
            CreateInventoryRankingTab(gui, tabs.transform, 3, "RECOMP.");
            CreateInventoryRankingTab(gui, tabs.transform, 4, "TROCAR");
            CreateInventoryRankingTab(gui, tabs.transform, 5, "GUIA");
            CreateInventoryRankingTab(gui, tabs.transform, 6, "JOG.");

            _inventoryGuideSidebar = CreateInventoryPanel("GuideSidebar", panel.transform, InventoryStretch(18f, 78f, 716f, 118f), new Color(0.08f, 0.045f, 0.025f, 0.46f));
            _inventoryGuideSubSidebar = CreateInventoryPanel("GuideSubSidebar", panel.transform, InventoryStretch(708f, 78f, 18f, 118f), new Color(0.08f, 0.045f, 0.025f, 0.46f));
            CreateInventoryGuideCategoryButtonsClean(gui, _inventoryGuideSidebar.transform, _inventoryGuideSubSidebar.transform);

            GameObject viewport = CreateInventoryPanel("Viewport", panel.transform, InventoryStretch(18f, 66f, 18f, 118f), new Color(0.17f, 0.095f, 0.045f, 0.54f));
            Mask mask = viewport.AddComponent<Mask>();
            mask.showMaskGraphic = true;
            _inventoryRankingScrollRect = viewport.AddComponent<ScrollRect>();
            _inventoryRankingScrollRect.horizontal = false;
            _inventoryRankingScrollRect.vertical = true;
            _inventoryRankingScrollRect.movementType = ScrollRect.MovementType.Clamped;
            _inventoryRankingScrollRect.scrollSensitivity = 28f;

            GameObject content = new GameObject("ScrollContent", typeof(RectTransform));
            content.transform.SetParent(viewport.transform, false);
            RectTransform contentRect = content.GetComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0f, 1f);
            contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.pivot = new Vector2(0.5f, 1f);
            contentRect.anchoredPosition = Vector2.zero;
            contentRect.sizeDelta = new Vector2(-46f, 1000f);
            _inventoryRankingScrollRect.content = contentRect;

            _inventoryRankingBody = CreateInventoryText("Body", content.transform, InventoryStretch(26f, 0f, 26f, 0f), "", 18, new Color(0.92f, 0.84f, 0.67f, 1f), TextAnchor.UpperLeft, false);
            _inventoryRankingBodyRect = _inventoryRankingBody.GetComponent<RectTransform>();
            _inventoryRankingBody.supportRichText = true;
            _inventoryRankingBody.lineSpacing = 0.95f;
            _inventoryRankingBody.horizontalOverflow = HorizontalWrapMode.Wrap;
            _inventoryRankingBody.verticalOverflow = VerticalWrapMode.Overflow;

            GameObject scrollBarObject = CreateInventoryPanel("ScrollBar", viewport.transform, InventoryStretch(0f, 18f, 10f, 18f), new Color(0.08f, 0.045f, 0.025f, 0.55f));
            RectTransform scrollBarRect = scrollBarObject.GetComponent<RectTransform>();
            scrollBarRect.anchorMin = new Vector2(1f, 0f);
            scrollBarRect.anchorMax = new Vector2(1f, 1f);
            scrollBarRect.pivot = new Vector2(1f, 0.5f);
            scrollBarRect.sizeDelta = new Vector2(18f, 0f);
            Scrollbar scrollbar = scrollBarObject.AddComponent<Scrollbar>();
            scrollbar.direction = Scrollbar.Direction.BottomToTop;
            GameObject handle = CreateInventoryPanel("Handle", scrollBarObject.transform, InventoryStretch(3f, 3f, 3f, 3f), new Color(1f, 0.55f, 0.08f, 0.72f));
            scrollbar.handleRect = handle.GetComponent<RectTransform>();
            _inventoryRankingScrollRect.verticalScrollbar = scrollbar;
            _inventoryRankingScrollRect.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHideAndExpandViewport;

            _inventoryRankingPrimaryAction = CreateInventoryRankingActionButton(gui, panel.transform, "PrimaryAction", "Atualizar", new Vector2(-92f, 24f));
            _inventoryRankingPrimaryActionLabel = _inventoryRankingPrimaryAction.GetComponentInChildren<Text>(true);
            _inventoryRankingSecondaryAction = CreateInventoryRankingActionButton(gui, panel.transform, "SecondaryAction", "Fechar", new Vector2(92f, 24f));
            _inventoryRankingSecondaryActionLabel = _inventoryRankingSecondaryAction.GetComponentInChildren<Text>(true);

            _inventoryExchangeMinusAction = CreateInventoryRankingActionButton(gui, panel.transform, "ExchangeMinus", "-", new Vector2(-330f, 24f));
            _inventoryExchangeMinusLabel = _inventoryExchangeMinusAction.GetComponentInChildren<Text>(true);
            _inventoryExchangeAmountLabel = CreateInventoryText("ExchangeAmount", panel.transform, InventoryStretch(308f, 18f, 308f, 18f), "0", 16, new Color(1f, 0.82f, 0.42f, 1f), TextAnchor.MiddleCenter, true);
            RectTransform amountRect = _inventoryExchangeAmountLabel.GetComponent<RectTransform>();
            amountRect.anchorMin = new Vector2(0.5f, 0f);
            amountRect.anchorMax = new Vector2(0.5f, 0f);
            amountRect.pivot = new Vector2(0.5f, 0f);
            amountRect.anchoredPosition = new Vector2(-212f, 26f);
            amountRect.sizeDelta = new Vector2(74f, 30f);
            _inventoryExchangePlusAction = CreateInventoryRankingActionButton(gui, panel.transform, "ExchangePlus", "+", new Vector2(-132f, 24f));
            _inventoryExchangePlusLabel = _inventoryExchangePlusAction.GetComponentInChildren<Text>(true);
            _inventoryExchangeMaxAction = CreateInventoryRankingActionButton(gui, panel.transform, "ExchangeMax", "Max.", new Vector2(-18f, 24f));
            _inventoryExchangeMaxLabel = _inventoryExchangeMaxAction.GetComponentInChildren<Text>(true);
            return panel;
        }

        private GameObject CreateInventoryRankingCloseButton(InventoryGui gui, Transform parent)
        {
            GameObject buttonObject = CloneNativeButton(gui, parent, "CloseButton", "X");
            RectTransform rect = buttonObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(1f, 0.5f);
            rect.anchorMax = new Vector2(1f, 0.5f);
            rect.pivot = new Vector2(1f, 0.5f);
            rect.anchoredPosition = new Vector2(-12f, 0f);
            rect.sizeDelta = new Vector2(44f, 40f);
            Button button = buttonObject.GetComponent<Button>();
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(delegate { SetInventoryRankingOpen(false); });
            return buttonObject;
        }

        private void CreateInventoryRankingTab(InventoryGui gui, Transform parent, int index, string label)
        {
            GameObject buttonObject = CloneNativeButton(gui, parent, "RankingTab" + index, label);
            RectTransform rect = buttonObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 0.5f);
            rect.anchorMax = new Vector2(0f, 0.5f);
            rect.pivot = new Vector2(0f, 0.5f);
            rect.anchoredPosition = new Vector2(index * 96f, 0f);
            rect.sizeDelta = new Vector2(90f, 32f);
            Button button = buttonObject.GetComponent<Button>();
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(delegate
            {
                _inventoryRankingTab = index;
                UpdateInventoryRankingContent(true);
            });
        }

        private GameObject CreateInventoryRankingActionButton(InventoryGui gui, Transform parent, string name, string label, Vector2 position)
        {
            GameObject buttonObject = CloneNativeButton(gui, parent, name, label);
            RectTransform rect = buttonObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = position;
            rect.sizeDelta = (label == "-" || label == "+") ? new Vector2(72f, 34f) : new Vector2(154f, 34f);
            return buttonObject;
        }

        private void UpdateInventoryRankingContent(bool force)
        {
            if (_inventoryRankingBody == null)
                return;

            if (!force && Time.realtimeSinceStartup < _inventoryRankingNextRefresh)
                return;

            try
            {
                _inventoryRankingNextRefresh = Time.realtimeSinceStartup + 0.5f;
                SnapshotPlayerData player = _cachedPlayerData ?? new SnapshotPlayerData();
                StringBuilder sb = new StringBuilder();
                _inventoryRankingTab = Mathf.Clamp(_inventoryRankingTab, 0, 6);
                UpdateInventoryRankingActions(player);

                if (_inventoryRankingTab == 0)
                    AppendInventoryRankingTopImmersive(sb);
                else if (_inventoryRankingTab == 1)
                    AppendInventoryRankingProfileImmersive(sb, player);
                else if (_inventoryRankingTab == 2)
                    AppendInventoryRankingRulesImmersive(sb, player);
                else if (_inventoryRankingTab == 3)
                    AppendInventoryRankingRewardImmersive(sb, player);
                else if (_inventoryRankingTab == 4)
                    AppendInventoryRankingExchangeImmersive(sb, player);
                else if (_inventoryRankingTab == 5)
                    AppendInventoryRankingGuideClean(sb, player);
                else
                    AppendInventoryRankingHistoryImmersive(sb, player);

                _inventoryRankingBody.text = sb.ToString();
                RefreshInventoryRankingLayout(force);
            }
            catch (Exception ex)
            {
                Logger.LogWarning("[Glitnir Ranking] Erro ao renderizar aba do HUD de inventario: " + ex);
                _inventoryRankingBody.text = "<color=#ffd36a><b>GLITNIR RANKING</b></color>\n<color=#ff7777>Erro ao renderizar esta aba.</color>";
            }
        }

        private void UpdateInventoryRankingActions(SnapshotPlayerData player)
        {
            bool exchangeTab = _inventoryRankingTab == 4;
            SetExchangeControlsVisible(exchangeTab);
            RefreshInventoryActionButtonLayout(exchangeTab);

            SetInventoryAction(_inventoryRankingPrimaryAction, _inventoryRankingPrimaryActionLabel, true, "Atualizar", delegate
            {
                RequestSnapshotFromServer();
                UpdateInventoryRankingContent(true);
            });

            SetInventoryAction(_inventoryRankingSecondaryAction, _inventoryRankingSecondaryActionLabel, true, "Fechar", delegate
            {
                SetInventoryRankingOpen(false);
            });

            if (_inventoryRankingTab == 3)
            {
                bool canClaim = player != null && player.RewardCanClaim && !_rewardClaimRequestPending;
                SetInventoryAction(_inventoryRankingPrimaryAction, _inventoryRankingPrimaryActionLabel, canClaim, _rewardClaimRequestPending ? "Processando" : "RESGATAR", delegate
                {
                    RequestRewardClaimFromServer();
                    UpdateInventoryRankingContent(true);
                });
            }
            else if (_inventoryRankingTab == 4)
            {
                int exchangePoints = GetInventoryMaxExchangePoints(player);
                EnsureInventoryExchangeAmount(player);
                bool canExchange = _inventoryExchangePoints > 0 && _inventoryExchangePoints <= exchangePoints && !_pointsExchangeRequestPending;

                SetInventoryAction(_inventoryExchangeMinusAction, _inventoryExchangeMinusLabel, exchangePoints > 0 && !_pointsExchangeRequestPending, "-", delegate
                {
                    int step = GetInventoryExchangeStep();
                    _inventoryExchangePoints = Mathf.Max(0, _inventoryExchangePoints - step);
                    EnsureInventoryExchangeAmount(_cachedPlayerData ?? new SnapshotPlayerData());
                    UpdateInventoryRankingContent(true);
                });

                SetInventoryAction(_inventoryExchangePlusAction, _inventoryExchangePlusLabel, exchangePoints > 0 && !_pointsExchangeRequestPending, "+", delegate
                {
                    int step = GetInventoryExchangeStep();
                    _inventoryExchangePoints = Mathf.Min(GetInventoryMaxExchangePoints(_cachedPlayerData ?? new SnapshotPlayerData()), _inventoryExchangePoints + step);
                    EnsureInventoryExchangeAmount(_cachedPlayerData ?? new SnapshotPlayerData());
                    UpdateInventoryRankingContent(true);
                });

                SetInventoryAction(_inventoryExchangeMaxAction, _inventoryExchangeMaxLabel, exchangePoints > 0 && !_pointsExchangeRequestPending, "Max.", delegate
                {
                    _inventoryExchangePoints = GetInventoryMaxExchangePoints(_cachedPlayerData ?? new SnapshotPlayerData());
                    UpdateInventoryRankingContent(true);
                });

                SetInventoryAction(_inventoryRankingPrimaryAction, _inventoryRankingPrimaryActionLabel, canExchange, _pointsExchangeRequestPending ? "Processando" : "RESGATAR", delegate
                {
                    int points = Mathf.Clamp(_inventoryExchangePoints, 0, GetInventoryMaxExchangePoints(_cachedPlayerData ?? new SnapshotPlayerData()));
                    if (points > 0)
                        RequestPointsExchangeFromServer(points);
                    UpdateInventoryRankingContent(true);
                });
            }
        }

        private void RefreshInventoryActionButtonLayout(bool exchangeTab)
        {
            if (exchangeTab)
            {
                SetInventoryButtonRect(_inventoryExchangeMinusAction, -382f, 24f, 58f, 34f);
                SetInventoryAmountRect(_inventoryExchangeAmountLabel, -312f, 24f, 82f, 34f);
                SetInventoryButtonRect(_inventoryExchangePlusAction, -242f, 24f, 58f, 34f);
                SetInventoryButtonRect(_inventoryExchangeMaxAction, -158f, 24f, 92f, 34f);
                SetInventoryButtonRect(_inventoryRankingPrimaryAction, 8f, 24f, 154f, 34f);
                SetInventoryButtonRect(_inventoryRankingSecondaryAction, 184f, 24f, 154f, 34f);
                return;
            }

            SetInventoryButtonRect(_inventoryRankingPrimaryAction, -92f, 24f, 154f, 34f);
            SetInventoryButtonRect(_inventoryRankingSecondaryAction, 92f, 24f, 154f, 34f);
        }

        private void SetInventoryButtonRect(GameObject buttonObject, float x, float y, float width, float height)
        {
            if (buttonObject == null)
                return;

            RectTransform rect = buttonObject.GetComponent<RectTransform>();
            if (rect == null)
                return;

            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = new Vector2(x, y);
            rect.sizeDelta = new Vector2(width, height);
        }

        private void SetInventoryAmountRect(Text label, float x, float y, float width, float height)
        {
            if (label == null)
                return;

            RectTransform rect = label.GetComponent<RectTransform>();
            if (rect == null)
                return;

            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = new Vector2(x, y);
            rect.sizeDelta = new Vector2(width, height);
        }

        private void SetExchangeControlsVisible(bool visible)
        {
            if (_inventoryExchangeMinusAction != null)
                _inventoryExchangeMinusAction.SetActive(visible);
            if (_inventoryExchangePlusAction != null)
                _inventoryExchangePlusAction.SetActive(visible);
            if (_inventoryExchangeMaxAction != null)
                _inventoryExchangeMaxAction.SetActive(visible);
            if (_inventoryExchangeAmountLabel != null)
                _inventoryExchangeAmountLabel.gameObject.SetActive(visible);
        }

        private void EnsureInventoryExchangeAmount(SnapshotPlayerData player)
        {
            int max = GetInventoryMaxExchangePoints(player);
            if (_inventoryExchangePoints <= 0 && max > 0)
                _inventoryExchangePoints = Mathf.Min(max, GetInventoryExchangeStep());

            _inventoryExchangePoints = Mathf.Clamp(_inventoryExchangePoints, 0, max);
            if (_rules != null && _rules.PointsExchangeUsePointsPerCoin)
            {
                int step = GetInventoryExchangeStep();
                _inventoryExchangePoints = (_inventoryExchangePoints / step) * step;
            }

            if (_inventoryExchangeAmountLabel != null)
                _inventoryExchangeAmountLabel.text = _inventoryExchangePoints.ToString();
        }

        private int GetInventoryExchangeStep()
        {
            if (_rules != null && _rules.PointsExchangeUsePointsPerCoin)
                return Mathf.Max(1, _rules.PointsExchangePointsPerCoin);

            return 1;
        }

        private void SetInventoryAction(GameObject buttonObject, Text label, bool enabled, string text, UnityEngine.Events.UnityAction action)
        {
            if (buttonObject == null)
                return;

            buttonObject.SetActive(true);
            if (label != null)
                label.text = text ?? "";

            Button button = buttonObject.GetComponent<Button>();
            if (button == null)
                return;

            button.interactable = enabled;
            button.onClick.RemoveAllListeners();
            if (enabled && action != null)
                button.onClick.AddListener(action);
        }

        private void AppendInventoryPanelTitle(StringBuilder sb, string title, string subtitle)
        {
            sb.AppendLine("<size=22><b><color=#ffd36a>" + title + "</color></b></size>");
            if (!string.IsNullOrWhiteSpace(subtitle))
                sb.AppendLine("<color=#d8c098>" + subtitle + "</color>");
            sb.AppendLine("<color=#b8873d>----------------------------------------</color>");
        }

        private string FormatInventoryPointAmount(int points, bool signed)
        {
            string value = FormatPoints(Mathf.Abs(points));
            if (value.IndexOf("pts", StringComparison.OrdinalIgnoreCase) < 0 &&
                value.IndexOf(" ponto", StringComparison.OrdinalIgnoreCase) < 0)
                value += " pts";

            if (!signed)
                return value;

            return points < 0 ? "-" + value : "+" + value;
        }

        private void AppendInventoryRankingTopImmersive(StringBuilder sb)
        {
            AppendInventoryPanelTitle(sb, "TOP GLITNIR", "Guerreiros com maior honra nesta temporada.");
            int count = _cachedTopEntries != null ? _cachedTopEntries.Count : 0;
            if (count == 0)
            {
                sb.AppendLine();
                sb.AppendLine("<color=#aaaaaa>Aguardando dados do servidor...</color>");
                return;
            }

            int limit = Mathf.Min(20, count);
            for (int i = 0; i < limit; i++)
            {
                SnapshotTopEntryData entry = _cachedTopEntries[i];
                string name = string.IsNullOrWhiteSpace(entry.PlayerName) ? "WARRIOR" : entry.PlayerName;
                string marker = entry.Position == 1 ? "[1]" : entry.Position == 2 ? "[2]" : entry.Position == 3 ? "[3]" : " - ";
                sb.AppendLine(marker + " <b>#" + entry.Position.ToString("00") + "</b>  <color=#ffd36a>" + name + "</color>  <color=#7aff4d>" + FormatInventoryPointAmount(entry.Points, false) + "</color>");
                sb.AppendLine("    <color=#d8c098>" + BuildInventoryPlayerAchievementSummary(entry) + "</color>");
            }
        }

        private void AppendInventoryRankingProfileImmersive(StringBuilder sb, SnapshotPlayerData player)
        {
            AppendInventoryPanelTitle(sb, "MEU PERFIL", "Resumo do seu desempenho no ranking.");
            sb.AppendLine("<color=#ffd36a>Nome:</color> " + (string.IsNullOrWhiteSpace(player.PlayerName) ? GetLocalPlayerName() : player.PlayerName));
            sb.AppendLine("<color=#ffd36a>Rank:</color> #" + Mathf.Max(1, player.Position));
            sb.AppendLine("<color=#ffd36a>Pontos atuais:</color> <color=#7aff4d>" + FormatInventoryPointAmount(player.Points, false) + "</color>");
            sb.AppendLine();
            sb.AppendLine("<color=#d8c098><b>Contribuicao por fonte</b></color>");
            sb.AppendLine("Combate: <color=#7aff4d>" + FormatInventoryPointAmount(player.KillPointsTotal + player.BossPointsTotal, true) + "</color>");
            sb.AppendLine("Habilidades: <color=#7aff4d>" + FormatInventoryPointAmount(player.SkillPointsTotal, true) + "</color>");
            sb.AppendLine("Crafting: <color=#7aff4d>" + FormatInventoryPointAmount(player.CraftPointsTotal, true) + "</color>");
            sb.AppendLine("Quests: <color=#7aff4d>" + FormatInventoryPointAmount(player.MarketplaceQuestPointsTotal, true) + "</color>");
            sb.AppendLine("Pesca: <color=#7aff4d>" + FormatInventoryPointAmount(player.FishingPointsTotal, true) + "</color>");
            sb.AppendLine("Penalidades: <color=#ff7777>" + FormatInventoryPointAmount(-Mathf.Abs(player.DeathPenaltyPointsTotal), true) + "</color>");
            sb.AppendLine();
            sb.AppendLine("<color=#d8c098><b>Eventos registrados</b></color>");
            sb.AppendLine("Kills: " + player.TotalKillsPontuadas + " | Bosses: " + player.TotalBossesPontuadas);
            sb.AppendLine("Crafts: " + player.TotalCraftPontuadas + " | Quests: " + player.TotalMarketplaceQuestsPontuadas + " | Pesca: " + player.TotalFishingPontuadas);
        }

        private void AppendInventoryRankingRulesImmersive(StringBuilder sb, SnapshotPlayerData player)
        {
            AppendInventoryPanelTitle(sb, "REGRAS", "Fontes atuais de honra do personagem.");
            sb.AppendLine("<color=#d8c098><b>Resumo de pontos acumulados</b></color>");
            sb.AppendLine("Combate: <color=#7aff4d>" + FormatInventoryPointAmount(player.KillPointsTotal + player.BossPointsTotal, true) + "</color>");
            sb.AppendLine("Habilidades: <color=#7aff4d>" + FormatInventoryPointAmount(player.SkillPointsTotal, true) + "</color>");
            sb.AppendLine("Marketplace: <color=#7aff4d>" + FormatInventoryPointAmount(player.MarketplaceQuestPointsTotal, true) + "</color>");
            sb.AppendLine("Pesca: <color=#7aff4d>" + FormatInventoryPointAmount(player.FishingPointsTotal, true) + "</color>");
            sb.AppendLine("Crafting: <color=#7aff4d>" + FormatInventoryPointAmount(player.CraftPointsTotal, true) + "</color>");
            sb.AppendLine("Mortes: <color=#ff7777>" + FormatInventoryPointAmount(-Mathf.Abs(player.DeathPenaltyPointsTotal), true) + "</color>");
            sb.AppendLine();
            sb.AppendLine("<color=#ffd36a>Use a aba GUIA para ver as regras detalhadas por categoria.</color>");
        }

        private void AppendInventoryRankingRewardImmersive(StringBuilder sb, SnapshotPlayerData player)
        {
            AppendInventoryPanelTitle(sb, "RECOMPENSA", "Resgate de recompensa da temporada.");
            if (player.RewardCanClaim)
            {
                sb.AppendLine("<color=#7aff4d>Voce possui recompensa disponivel.</color>");
                sb.AppendLine("Posicao: Top " + player.RewardRank);
                sb.AppendLine("Item: <color=#ffd36a>" + player.RewardPrefabName + " x" + player.RewardAmount + "</color>");
            }
            else if (!string.IsNullOrWhiteSpace(player.RewardBlockReason))
                sb.AppendLine(player.RewardBlockReason);
            else
                sb.AppendLine("Continue acumulando honra para liberar a recompensa.");
        }

        private void AppendInventoryRankingExchangeImmersive(StringBuilder sb, SnapshotPlayerData player)
        {
            AppendInventoryPanelTitle(sb, "TROCAR PONTOS", "Escolha a quantidade nos botoes de baixo antes de confirmar.");
            int availablePoints = Mathf.Max(0, player.Points);
            int maxPoints = GetInventoryMaxExchangePoints(player);
            int selectedPoints = Mathf.Clamp(_inventoryExchangePoints, 0, maxPoints);
            sb.AppendLine("Pontos disponiveis: <color=#7aff4d>" + FormatInventoryPointAmount(availablePoints, false) + "</color>");
            sb.AppendLine("Maximo nesta troca: <color=#ffd36a>" + FormatInventoryPointAmount(maxPoints, false) + "</color>");
            sb.AppendLine("Selecionado: <color=#ffd36a>" + FormatInventoryPointAmount(selectedPoints, false) + "</color>");
            sb.AppendLine();

            string prefabName = _rules != null && !string.IsNullOrWhiteSpace(_rules.PointsExchangePrefab) ? _rules.PointsExchangePrefab : "Coins";
            if (_rules == null || !_rules.PointsExchangeEnabled)
            {
                sb.AppendLine("O cambio de pontos esta desativado.");
                return;
            }

            if (_rules.PointsExchangeUsePointsPerCoin)
            {
                int pointsPerCoin = Mathf.Max(1, _rules.PointsExchangePointsPerCoin);
                int coins = Mathf.Max(0, selectedPoints / pointsPerCoin);
                int chargedPoints = coins * pointsPerCoin;
                sb.AppendLine("Taxa: " + pointsPerCoin + " pontos = 1 " + prefabName);
                sb.AppendLine("Vai gastar: " + FormatInventoryPointAmount(chargedPoints, false));
                sb.AppendLine("Vai receber: <color=#7aff4d>" + coins + " " + prefabName + "</color>");
            }
            else if (_rules.PointsExchangeUseCoinsPerPoint)
            {
                int coinsPerPoint = Mathf.Max(1, _rules.PointsExchangeCoinsPerPoint);
                int coins = selectedPoints * coinsPerPoint;
                sb.AppendLine("Taxa: 1 ponto = " + coinsPerPoint + " " + prefabName);
                sb.AppendLine("Vai gastar: " + FormatInventoryPointAmount(selectedPoints, false));
                sb.AppendLine("Vai receber: <color=#7aff4d>" + coins + " " + prefabName + "</color>");
            }
            else
                sb.AppendLine("Nenhum modo de cambio esta ativo.");
        }

        private void AppendInventoryRankingHistoryImmersive(StringBuilder sb, SnapshotPlayerData player)
        {
            AppendInventoryPanelTitle(sb, "JOGADORES", "Pontuacao e conquistas do servidor.");
            int count = _cachedTopEntries != null ? _cachedTopEntries.Count : 0;
            if (count == 0)
            {
                sb.AppendLine();
                sb.AppendLine("<color=#aaaaaa>Aguardando dados do servidor...</color>");
                return;
            }

            int limit = Mathf.Min(40, count);
            for (int i = 0; i < limit; i++)
            {
                SnapshotTopEntryData entry = _cachedTopEntries[i];
                if (entry == null)
                    continue;

                string name = string.IsNullOrWhiteSpace(entry.PlayerName) ? "JOGADOR" : entry.PlayerName;
                string local = entry.IsLocalPlayer ? " <color=#7aff4d>[VOCE]</color>" : "";
                sb.AppendLine("<color=#ffd36a><b>#" + Mathf.Max(1, entry.Position).ToString("00") + " " + name + "</b></color>" + local + "  <color=#7aff4d>" + FormatInventoryPointAmount(entry.Points, false) + "</color>");
                sb.AppendLine("  " + BuildInventoryPlayerAchievementSummary(entry));

                string last = string.IsNullOrWhiteSpace(entry.LastReason) ? "" : entry.LastReason.Trim();
                if (!string.IsNullOrWhiteSpace(last))
                    sb.AppendLine("  <color=#bfa06a>Ultimo registro:</color> " + last);

                sb.AppendLine();
            }
        }

        private string BuildInventoryPlayerAchievementSummary(SnapshotTopEntryData entry)
        {
            if (entry == null)
                return "Sem conquistas registradas.";

            StringBuilder sb = new StringBuilder();
            AppendInventorySummaryPart(sb, entry.TotalKillsPontuadas, "kills");
            AppendInventorySummaryPart(sb, entry.TotalBossesPontuadas, "bosses");
            AppendInventorySummaryPart(sb, entry.TotalSkillLevelUpsPontuados, "skills");
            AppendInventorySummaryPart(sb, entry.TotalMarketplaceQuestsPontuadas, "quests");
            AppendInventorySummaryPart(sb, entry.TotalFishingPontuadas, "pesca");
            AppendInventorySummaryPart(sb, entry.TotalCraftPontuadas, "crafts");
            AppendInventorySummaryPart(sb, entry.TotalFarmJackpotsPontuados, "cultivo");
            AppendInventorySummaryPart(sb, entry.TotalUniqueCraftJackpotsPontuados, "unicos");
            AppendInventorySummaryPart(sb, entry.TotalPointsExchanges, "cambios");

            if (sb.Length == 0)
                return "Sem conquistas registradas.";

            return sb.ToString();
        }

        private void AppendInventorySummaryPart(StringBuilder sb, int amount, string label)
        {
            if (amount <= 0)
                return;

            if (sb.Length > 0)
                sb.Append("  |  ");

            sb.Append(amount);
            sb.Append(" ");
            sb.Append(label);
        }

        private void AppendInventoryRankingTop(StringBuilder sb)
        {
            sb.AppendLine("<b><color=#ffd36a>🏆 TOP GLITNIR</color></b>");
            sb.AppendLine("Ranking geral da temporada.");
            sb.AppendLine("────────────────────────────────────────");
            int count = _cachedTopEntries != null ? _cachedTopEntries.Count : 0;
            if (count == 0)
            {
                sb.AppendLine();
                sb.AppendLine("<color=#aaaaaa>Aguardando dados do servidor...</color>");
                return;
            }

            int limit = Mathf.Min(20, count);
            for (int i = 0; i < limit; i++)
            {
                SnapshotTopEntryData entry = _cachedTopEntries[i];
                string name = string.IsNullOrWhiteSpace(entry.PlayerName) ? "WARRIOR" : entry.PlayerName;
                string medal = entry.Position == 1 ? "👑" : entry.Position == 2 ? "🥈" : entry.Position == 3 ? "🥉" : "•";
                sb.AppendLine(medal + " <b>#" + entry.Position.ToString("00") + "</b>  " + name + "  <color=#7aff4d>" + FormatPoints(entry.Points) + " pts</color>");
            }
        }

        private void AppendInventoryRankingProfile(StringBuilder sb, SnapshotPlayerData player)
        {
            sb.AppendLine("SEU PERFIL");
            sb.AppendLine();
            sb.AppendLine("Nome: " + (string.IsNullOrWhiteSpace(player.PlayerName) ? GetLocalPlayerName() : player.PlayerName));
            sb.AppendLine("Rank: #" + Mathf.Max(1, player.Position));
            sb.AppendLine("Pontos: " + FormatPoints(player.Points));
            sb.AppendLine();
            sb.AppendLine("Kills: " + player.TotalKillsPontuadas);
            sb.AppendLine("Crafts: " + player.TotalCraftPontuadas);
            sb.AppendLine("Quests: " + player.TotalMarketplaceQuestsPontuadas);
            sb.AppendLine("Pesca: " + player.TotalFishingPontuadas);
        }

        private void AppendInventoryRankingRules(StringBuilder sb, SnapshotPlayerData player)
        {
            sb.AppendLine("FONTES DE HONRA");
            sb.AppendLine();
            sb.AppendLine("Combate +" + FormatPoints(player.KillPointsTotal + player.BossPointsTotal));
            sb.AppendLine("Skills +" + FormatPoints(player.SkillPointsTotal));
            sb.AppendLine("Marketplace +" + FormatPoints(player.MarketplaceQuestPointsTotal));
            sb.AppendLine("Pesca +" + FormatPoints(player.FishingPointsTotal));
            sb.AppendLine("Crafting +" + FormatPoints(player.CraftPointsTotal));
            sb.AppendLine("Mortes -" + FormatPoints(player.DeathPenaltyPointsTotal));
        }

        private void AppendInventoryRankingReward(StringBuilder sb, SnapshotPlayerData player)
        {
            sb.AppendLine("RECOMPENSA");
            sb.AppendLine();
            if (player.RewardCanClaim)
            {
                sb.AppendLine("Voce possui recompensa disponivel.");
                sb.AppendLine("Posicao: Top " + player.RewardRank);
                sb.AppendLine("Item: " + player.RewardPrefabName + " x" + player.RewardAmount);
            }
            else if (!string.IsNullOrWhiteSpace(player.RewardBlockReason))
                sb.AppendLine(player.RewardBlockReason);
            else
                sb.AppendLine("Continue acumulando honra para liberar a recompensa.");
        }

        private void AppendInventoryRankingExchange(StringBuilder sb, SnapshotPlayerData player)
        {
            sb.AppendLine("TROCAR PONTOS");
            sb.AppendLine();
            sb.AppendLine("Converta pontos de ranking na recompensa configurada.");
            sb.AppendLine();
            int availablePoints = Mathf.Max(0, player.Points);
            int maxPoints = GetInventoryMaxExchangePoints(player);
            int selectedPoints = Mathf.Clamp(_inventoryExchangePoints, 0, maxPoints);
            sb.AppendLine("Pontos disponiveis: " + FormatPoints(availablePoints));
            sb.AppendLine("Maximo nesta troca: " + FormatPoints(maxPoints));
            sb.AppendLine("Selecionado: " + FormatPoints(selectedPoints));

            string prefabName = _rules != null && !string.IsNullOrWhiteSpace(_rules.PointsExchangePrefab) ? _rules.PointsExchangePrefab : "Coins";
            if (_rules == null || !_rules.PointsExchangeEnabled)
            {
                sb.AppendLine();
                sb.AppendLine("O cambio de pontos esta desativado.");
                return;
            }

            if (_rules.PointsExchangeUsePointsPerCoin)
            {
                int pointsPerCoin = Mathf.Max(1, _rules.PointsExchangePointsPerCoin);
                int coins = Mathf.Max(0, selectedPoints / pointsPerCoin);
                int chargedPoints = coins * pointsPerCoin;
                sb.AppendLine("Taxa: " + pointsPerCoin + " pontos = 1 " + prefabName);
                sb.AppendLine("Vai gastar: " + FormatPoints(chargedPoints) + " pontos");
                sb.AppendLine("Vai receber: " + coins + " " + prefabName);
            }
            else if (_rules.PointsExchangeUseCoinsPerPoint)
            {
                int coinsPerPoint = Mathf.Max(1, _rules.PointsExchangeCoinsPerPoint);
                int coins = selectedPoints * coinsPerPoint;
                sb.AppendLine("Taxa: 1 ponto = " + coinsPerPoint + " " + prefabName);
                sb.AppendLine("Vai gastar: " + FormatPoints(selectedPoints) + " pontos");
                sb.AppendLine("Vai receber: " + coins + " " + prefabName);
            }
            else
                sb.AppendLine("Nenhum modo de cambio esta ativo.");

            sb.AppendLine();
            sb.AppendLine("Use -, + e Max. para escolher a quantidade antes de trocar.");
        }

        private void AppendInventoryRankingGuideClean(StringBuilder sb, SnapshotPlayerData player)
        {
            string[] categoryNames = GetInventoryGuideCategoryNamesClean();
            _inventoryGuideCategoryIndex = Mathf.Clamp(_inventoryGuideCategoryIndex, 0, categoryNames.Length - 1);
            string category = categoryNames[_inventoryGuideCategoryIndex];
            UpdateInventoryGuideSidebarClean(category, player);

            if (category == "Combate")
            {
                AppendInventoryGuideBlockClean(sb, "Combate", player.EnableKillPoints, player.HudKillRules, "Derrote criaturas configuradas para ganhar pontos.", false, 14);
            }
            else if (category == "Chefes")
                AppendInventoryGuideBlockClean(sb, "Chefes", player.EnableBossPoints, player.HudBossRules, "Derrote bosses validos. Normalmente contam uma vez por ciclo.", false, 18);
            else if (category == "Habilidades")
            {
                AppendInventoryGuideBlockClean(sb, "Habilidades", player.EnableSkillPoints, player.HudSkillJackpotRules, "Ganhe pontos ao alcancar marcos de habilidade.", true, 18);
                AppendInventoryGuideBlockClean(sb, "Exploracao", player.RankingEnabled, player.HudExplorationMapJackpotRules, "Revele o mapa para ativar marcos de exploracao.", true, 8);
            }
            else if (category == "Pesca")
                AppendInventoryGuideBlockClean(sb, "Pesca", true, player.HudFishingRules, "Pesque peixes configurados e acumule pontos.", false, 18);
            else if (category == "Cultivo")
                AppendInventoryGuideBlockClean(sb, "Cultivo", true, player.HudFarmJackpotRules, "Complete metas de colheita para receber jackpots.", true, 18);
            else if (category == "Producao")
            {
                AppendInventoryGuideBlockClean(sb, "Producao", true, player.HudCraftRules, "Crie itens configurados para ganhar pontos.", false, 18);
                AppendInventoryGuideBlockClean(sb, "Craft unico", true, player.HudUniqueCraftJackpotRules, "Itens especiais contam como conquistas unicas.", true, 10);
            }
            else if (category == "Quests")
                AppendInventoryGuideBlockClean(sb, "Quests", player.EnableMarketplaceQuestPoints, player.HudMarketplaceQuestRules, "Complete quests configuradas do Marketplace.", false, 18);
            else if (category == "Penalidades")
                AppendInventoryGuideBlockClean(sb, "Penalidades", player.EnableDeathPenalty, GetInventoryDeathPenaltyRules(player), "Mortes e punicoes podem remover pontos.", true, 18);
            else
            {
                sb.AppendLine("<b><color=#ffd36a>DICAS RAPIDAS</color></b>");
                sb.AppendLine("- Foque em bosses, habilidades e quests para ganhar muitos pontos.");
                sb.AppendLine("- Producao, cultivo e pesca ajudam a manter pontuacao constante.");
                sb.AppendLine("- Evite mortes: penalidades podem derrubar sua posicao.");
                sb.AppendLine("- Use Atualizar para puxar dados novos do servidor.");
                sb.AppendLine();
                sb.AppendLine("<color=#ffd36a>Ultima atualizacao:</color> " + FormatHudLastUpdate(player.LastUpdateUtc));
            }
        }

        private void AppendInventoryGuideBlockClean(StringBuilder sb, string title, bool enabled, string rules, string description, bool jackpotFormat, int visibleLimit)
        {
            string key = NormalizeInventoryGuideKeyClean(title);
            bool expanded = _inventoryGuideExpandedBlocks.Contains(key);
            List<string> rows = FilterInventoryGuideRowsBySubCategory(rules);
            int total = rows != null ? rows.Count : 0;
            bool filtered = !string.IsNullOrWhiteSpace(_inventoryGuideSubCategory);
            int limit = filtered ? total : (expanded ? total : Mathf.Min(total, visibleLimit));

            string displayTitle = filtered ? _inventoryGuideSubCategory : title;
            sb.AppendLine("<size=22><b><color=#ffd36a>" + displayTitle.ToUpperInvariant() + "</color></b></size> " + (enabled ? "<color=#7aff4d>[ATIVO]</color>" : "<color=#ff6666>[OFF]</color>"));
            sb.AppendLine("<color=#d8c098>" + description + "</color>");
            sb.AppendLine("<color=#b8873d>----------------------------------------</color>");

            if (total == 0)
            {
                sb.AppendLine("  <color=#aaaaaa>Nenhuma regra configurada.</color>");
                sb.AppendLine();
                return;
            }

            sb.AppendLine("<color=#ffd36a><b>Regras configuradas</b></color>");
            for (int i = 0; i < limit; i++)
                sb.AppendLine(FormatInventoryGuideRuleClean(rows[i], jackpotFormat));

            if (!filtered && !expanded && total > limit)
                sb.AppendLine("<color=#ffd36a>+" + (total - limit) + " regras. Escolha uma subcategoria na esquerda para filtrar.</color>");

            sb.AppendLine();
        }

        private string FormatInventoryGuideRuleClean(string rule, bool jackpotFormat)
        {
            if (string.IsNullOrWhiteSpace(rule))
                return string.Empty;

            string[] parts = rule.Split(new char[] { ':' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < parts.Length; i++)
                parts[i] = (parts[i] ?? string.Empty).Trim();

            string name = FriendlyInventoryRuleName(parts.Length > 0 ? parts[0] : rule);
            string points = string.Empty;
            if (parts.Length >= 3 && jackpotFormat)
            {
                name = name + " x" + parts[1];
                points = NormalizeInventoryPointsText(parts[2]);
            }
            else if (parts.Length >= 2)
            {
                points = NormalizeInventoryPointsText(parts[1]);
            }
            else
            {
                name = rule.Trim();
            }

            if (name.Length > 38)
                name = name.Substring(0, 35) + "...";

            if (string.IsNullOrWhiteSpace(points))
                return "  - <color=#e8d2a0>" + name + "</color>";

            return "  - <color=#e8d2a0>" + name + "</color>  <color=#7aff4d>" + points + "</color>";
        }

        private string[] GetInventoryGuideCategoryNamesClean()
        {
            return new string[] { "Combate", "Chefes", "Habilidades", "Pesca", "Cultivo", "Producao", "Quests", "Penalidades", "Dicas" };
        }

        private string[] GetInventoryGuideCategoryIconsClean()
        {
            return new string[] { "X", "*", "+", "~", "#", "@", "!", "-", "?" };
        }

        private void CreateInventoryGuideCategoryButtonsClean(InventoryGui gui, Transform mainParent, Transform subParent)
        {
            string[] names = GetInventoryGuideCategoryNamesClean();
            string[] icons = GetInventoryGuideCategoryIconsClean();
            for (int i = 0; i < names.Length; i++)
            {
                int index = i;
                string icon = icons[Mathf.Clamp(i, 0, icons.Length - 1)];
                GameObject buttonObject = CloneNativeButton(gui, mainParent, "GuideCategory" + i, icon + "  " + names[i].ToUpperInvariant());
                RectTransform rect = buttonObject.GetComponent<RectTransform>();
                rect.anchorMin = new Vector2(0f, 1f);
                rect.anchorMax = new Vector2(1f, 1f);
                rect.pivot = new Vector2(0.5f, 1f);
                rect.anchoredPosition = new Vector2(0f, -12f - i * 38f);
                rect.sizeDelta = new Vector2(-14f, 32f);

                Button button = buttonObject.GetComponent<Button>();
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(delegate
                {
                    _inventoryGuideSubCategory = "";
                    _inventoryGuideCategoryIndex = index;
                    UpdateInventoryRankingContent(true);
                });

                _inventoryGuideCategoryButtons.Add(buttonObject);
            }

            for (int i = 0; i < 10; i++)
            {
                int index = i;
                GameObject buttonObject = CloneNativeButton(gui, subParent, "GuideSubCategory" + i, "");
                RectTransform rect = buttonObject.GetComponent<RectTransform>();
                rect.anchorMin = new Vector2(0f, 1f);
                rect.anchorMax = new Vector2(1f, 1f);
                rect.pivot = new Vector2(0.5f, 1f);
                rect.sizeDelta = new Vector2(-14f, 32f);

                Button button = buttonObject.GetComponent<Button>();
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(delegate
                {
                    string[] subs = GetVisibleInventoryGuideSubCategories();
                    if (index >= 0 && index < subs.Length)
                        _inventoryGuideSubCategory = subs[index];
                    UpdateInventoryRankingContent(true);
                });

                buttonObject.SetActive(false);
                _inventoryGuideSubCategoryButtons.Add(buttonObject);
            }
        }

        private void UpdateInventoryGuideSidebarClean(string category, SnapshotPlayerData player)
        {
            string[] mainCategories = GetInventoryGuideCategoryNamesClean();
            List<string> subCategories = GetInventoryGuideSubCategoriesFor(category, player);
            _inventoryGuideVisibleSubCategories = subCategories.ToArray();

            bool selectedSubStillExists = string.IsNullOrWhiteSpace(_inventoryGuideSubCategory);
            for (int i = 0; i < _inventoryGuideVisibleSubCategories.Length; i++)
            {
                if (string.Equals(_inventoryGuideVisibleSubCategories[i], _inventoryGuideSubCategory, StringComparison.OrdinalIgnoreCase))
                {
                    selectedSubStillExists = true;
                    break;
                }
            }
            if (!selectedSubStillExists)
                _inventoryGuideSubCategory = "";
            if (string.IsNullOrWhiteSpace(_inventoryGuideSubCategory) && _inventoryGuideVisibleSubCategories.Length > 0)
                _inventoryGuideSubCategory = _inventoryGuideVisibleSubCategories[0];

            string[] icons = GetInventoryGuideCategoryIconsClean();
            float y = -12f;
            for (int i = 0; i < _inventoryGuideCategoryButtons.Count; i++)
            {
                GameObject buttonObject = _inventoryGuideCategoryButtons[i];
                if (buttonObject == null)
                    continue;

                bool selected = i == _inventoryGuideCategoryIndex;
                string icon = icons[Mathf.Clamp(i, 0, icons.Length - 1)];
                string label = (selected ? "> " : "  ") + icon + "  " + mainCategories[Mathf.Clamp(i, 0, mainCategories.Length - 1)].ToUpperInvariant();
                SetNativeButtonText(buttonObject.transform, label);

                RectTransform rect = buttonObject.GetComponent<RectTransform>();
                if (rect != null)
                {
                    rect.anchoredPosition = new Vector2(0f, y);
                    rect.sizeDelta = new Vector2(-14f, 32f);
                }
                y -= 38f;
            }

            int subLimit = Mathf.Min(_inventoryGuideSubCategoryButtons.Count, _inventoryGuideVisibleSubCategories.Length);
            float subY = -12f;
            for (int j = 0; j < subLimit; j++)
            {
                GameObject subButton = _inventoryGuideSubCategoryButtons[j];
                if (subButton == null)
                    continue;

                string sub = _inventoryGuideVisibleSubCategories[j];
                bool subSelected = string.Equals(sub, _inventoryGuideSubCategory, StringComparison.OrdinalIgnoreCase);
                subButton.SetActive(true);
                SetNativeButtonText(subButton.transform, (subSelected ? "> " : "  ") + sub.ToUpperInvariant());

                RectTransform subRect = subButton.GetComponent<RectTransform>();
                if (subRect != null)
                {
                    subRect.anchoredPosition = new Vector2(0f, subY);
                    subRect.sizeDelta = new Vector2(-14f, 32f);
                }
                subY -= 38f;
            }

            for (int i = 0; i < _inventoryGuideSubCategoryButtons.Count; i++)
            {
                if (i >= subLimit && _inventoryGuideSubCategoryButtons[i] != null)
                    _inventoryGuideSubCategoryButtons[i].SetActive(false);
            }
        }

        private string[] GetVisibleInventoryGuideSubCategories()
        {
            return _inventoryGuideVisibleSubCategories ?? new string[0];
        }

        private List<string> GetInventoryGuideSubCategoriesFor(string category, SnapshotPlayerData player)
        {
            List<InventoryGuideRuleGroup> groups = BuildInventoryGuideRuleGroups(GetInventoryGuideRulesForCategory(category, player));
            List<string> result = new List<string>();
            for (int i = 0; i < groups.Count; i++)
            {
                if (!string.Equals(groups[i].Name, "Todos", StringComparison.OrdinalIgnoreCase))
                    result.Add(groups[i].Name);
            }
            return result;
        }

        private string GetInventoryGuideRulesForCategory(string category, SnapshotPlayerData player)
        {
            if (player == null)
                return string.Empty;

            if (category == "Combate")
                return player.HudKillRules;
            if (category == "Chefes")
                return player.HudBossRules;
            if (category == "Habilidades")
                return player.HudSkillJackpotRules;
            if (category == "Pesca")
                return player.HudFishingRules;
            if (category == "Cultivo")
                return player.HudFarmJackpotRules;
            if (category == "Producao")
                return player.HudCraftRules;
            if (category == "Quests")
                return player.HudMarketplaceQuestRules;
            if (category == "Penalidades")
                return GetInventoryDeathPenaltyRules(player);

            return string.Empty;
        }

        private List<string> FilterInventoryGuideRowsBySubCategory(string rules)
        {
            List<InventoryGuideRuleGroup> groups = BuildInventoryGuideRuleGroups(rules);
            List<string> result = new List<string>();
            for (int i = 0; i < groups.Count; i++)
            {
                InventoryGuideRuleGroup group = groups[i];
                if (!string.IsNullOrWhiteSpace(_inventoryGuideSubCategory) &&
                    !string.Equals(group.Name, _inventoryGuideSubCategory, StringComparison.OrdinalIgnoreCase))
                    continue;

                for (int j = 0; j < group.Rows.Count; j++)
                    result.Add(group.Rows[j]);
            }

            return result;
        }

        private List<InventoryGuideRuleGroup> BuildInventoryGuideRuleGroups(string rules)
        {
            List<InventoryGuideRuleGroup> groups = new List<InventoryGuideRuleGroup>();
            InventoryGuideRuleGroup current = new InventoryGuideRuleGroup("Todos");
            groups.Add(current);

            List<string> rows = SplitRulesForHud(rules);
            for (int i = 0; i < rows.Count; i++)
            {
                string row = rows[i];
                string categoryName;
                if (TryGetInventoryGuideRuleCategory(row, out categoryName))
                {
                    current = FindOrCreateInventoryGuideRuleGroup(groups, categoryName);
                    continue;
                }

                string normalizedLegacyRow;
                if (TryGetLegacyInventoryGuideCategoryRule(row, out categoryName, out normalizedLegacyRow))
                {
                    current = FindOrCreateInventoryGuideRuleGroup(groups, categoryName);
                    if (!string.IsNullOrWhiteSpace(normalizedLegacyRow))
                        current.Rows.Add(normalizedLegacyRow);
                    continue;
                }

                current.Rows.Add(row);
            }

            for (int i = groups.Count - 1; i >= 0; i--)
            {
                if (groups[i].Rows.Count == 0)
                    groups.RemoveAt(i);
            }

            return groups;
        }

        private InventoryGuideRuleGroup FindOrCreateInventoryGuideRuleGroup(List<InventoryGuideRuleGroup> groups, string name)
        {
            for (int i = 0; i < groups.Count; i++)
            {
                if (string.Equals(groups[i].Name, name, StringComparison.OrdinalIgnoreCase))
                    return groups[i];
            }

            InventoryGuideRuleGroup group = new InventoryGuideRuleGroup(name);
            groups.Add(group);
            return group;
        }

        private bool TryGetInventoryGuideRuleCategory(string row, out string categoryName)
        {
            categoryName = string.Empty;
            if (string.IsNullOrWhiteSpace(row))
                return false;

            string value = row.Trim();
            while (value.Length > 0 && (value[0] == '-' || value[0] == '>' || value[0] == '*' || value[0] == '_' || value[0] == '•' || char.IsWhiteSpace(value[0])))
                value = value.Substring(1).TrimStart();

            if (!value.StartsWith("GLITNIR_CAT", StringComparison.OrdinalIgnoreCase))
                return false;

            value = value.Replace("__GLITNIR_CAT__|", string.Empty)
                         .Replace("GLITNIR_CAT__|", string.Empty)
                         .Replace("GLITNIR_CAT__", string.Empty)
                         .Replace("GLITNIR_CAT_", string.Empty)
                         .Replace("GLITNIR CAT ", string.Empty)
                         .Trim('_', '-', '|', ' ', ':');

            categoryName = FriendlyInventoryRuleName(value);
            return !string.IsNullOrWhiteSpace(categoryName);
        }

        private bool TryGetLegacyInventoryGuideCategoryRule(string row, out string categoryName, out string normalizedRule)
        {
            categoryName = string.Empty;
            normalizedRule = string.Empty;
            if (string.IsNullOrWhiteSpace(row))
                return false;

            string[] parts = row.Split(new char[] { ':' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 3)
                return false;

            string first = (parts[0] ?? string.Empty).Trim();
            string second = (parts[1] ?? string.Empty).Trim();
            string third = (parts[2] ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(first) || string.IsNullOrWhiteSpace(second) || string.IsNullOrWhiteSpace(third))
                return false;

            int numericValue;
            if (int.TryParse(second.Replace("+", string.Empty), out numericValue))
                return false;

            if (!int.TryParse(third.Replace("+", string.Empty), out numericValue))
                return false;

            categoryName = FriendlyInventoryRuleName(first);
            normalizedRule = second + ":" + third;
            return !string.IsNullOrWhiteSpace(categoryName);
        }

        private sealed class InventoryGuideRuleGroup
        {
            public readonly string Name;
            public readonly List<string> Rows = new List<string>();

            public InventoryGuideRuleGroup(string name)
            {
                Name = string.IsNullOrWhiteSpace(name) ? "Todos" : name;
            }
        }

        private string NormalizeInventoryGuideKeyClean(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;

            return value.Trim().ToUpperInvariant()
                .Replace("PRODUCAO", "PRODUCAO")
                .Replace("PRODUÇÃO", "PRODUCAO")
                .Replace("EXPLORAÇÃO", "EXPLORACAO")
                .Replace("EXPLORACAO", "EXPLORACAO");
        }

        private void AppendInventoryRankingGuide(StringBuilder sb, SnapshotPlayerData player)
        {
            string[] categoryNames = GetInventoryGuideCategoryNames();
            _inventoryGuideCategoryIndex = Mathf.Clamp(_inventoryGuideCategoryIndex, 0, categoryNames.Length - 1);
            string category = categoryNames[_inventoryGuideCategoryIndex];

            sb.AppendLine("<b><color=#ffd36a>GUIA DE HONRA</color></b>");
            sb.AppendLine("Categoria selecionada: <color=#7aff4d><b>" + category.ToUpperInvariant() + "</b></color>");
            sb.AppendLine("────────────────────────────────────────");
            sb.AppendLine();

            if (category == "Combate")
            {
                AppendInventoryGuideBlock(sb, "⚔ COMBATE", player.EnableKillPoints, player.HudKillRules, "Derrote criaturas configuradas para ganhar pontos.", false, 12);
                AppendInventoryGuideBlock(sb, "☠ CHEFES", player.EnableBossPoints, player.HudBossRules, "Chefes entregam grandes recompensas de honra.", false, 8);
            }
            else if (category == "Chefes")
                AppendInventoryGuideBlock(sb, "☠ CHEFES", player.EnableBossPoints, player.HudBossRules, "Derrote bosses válidos. Normalmente contam uma vez por ciclo.", false, 18);
            else if (category == "Habilidades")
            {
                AppendInventoryGuideBlock(sb, "🪓 HABILIDADES", player.EnableSkillPoints, player.HudSkillJackpotRules, "Ganhe pontos ao alcançar marcos de habilidade.", true, 18);
                AppendInventoryGuideBlock(sb, "🗺 EXPLORAÇÃO", player.RankingEnabled, player.HudExplorationMapJackpotRules, "Revele o mapa para ativar marcos de exploração.", true, 8);
            }
            else if (category == "Pesca")
                AppendInventoryGuideBlock(sb, "🎣 PESCA", true, player.HudFishingRules, "Pesque peixes configurados e acumule pontos.", false, 18);
            else if (category == "Cultivo")
                AppendInventoryGuideBlock(sb, "🌾 CULTIVO", true, player.HudFarmJackpotRules, "Complete metas de colheita para receber jackpots.", true, 18);
            else if (category == "Produção")
            {
                AppendInventoryGuideBlock(sb, "🔨 PRODUÇÃO", true, player.HudCraftRules, "Crie itens configurados para ganhar pontos.", false, 18);
                AppendInventoryGuideBlock(sb, "⭐ CRAFT ÚNICO", true, player.HudUniqueCraftJackpotRules, "Itens especiais contam como conquistas únicas.", true, 10);
            }
            else if (category == "Quests")
                AppendInventoryGuideBlock(sb, "📜 QUESTS", player.EnableMarketplaceQuestPoints, player.HudMarketplaceQuestRules, "Complete quests configuradas do Marketplace.", false, 18);
            else if (category == "Penalidades")
                AppendInventoryGuideBlock(sb, "💀 PENALIDADES", player.EnableDeathPenalty, GetInventoryDeathPenaltyRules(player), "Mortes e punições podem remover pontos.", true, 18);
            else
            {
                sb.AppendLine("<b><color=#ffd36a>⭐ DICAS RÁPIDAS</color></b>");
                sb.AppendLine("• Foque em bosses, habilidades e quests para ganhar muitos pontos.");
                sb.AppendLine("• Produção, cultivo e pesca ajudam a manter pontuação constante.");
                sb.AppendLine("• Evite mortes: penalidades podem derrubar sua posição.");
                sb.AppendLine("• Use o botão Atualizar quando quiser puxar dados novos do servidor.");
                sb.AppendLine();
                sb.AppendLine("<color=#ffd36a>Última atualização:</color> " + FormatHudLastUpdate(player.LastUpdateUtc));
            }
        }

        private void AppendInventoryGuideBlock(StringBuilder sb, string title, bool enabled, string rules, string description, bool jackpotFormat, int visibleLimit)
        {
            string key = title;
            bool expanded = _inventoryGuideExpandedBlocks.Contains(key);
            List<string> rows = SplitRulesForHud(rules);
            int total = rows != null ? rows.Count : 0;
            int limit = expanded ? total : Mathf.Min(total, visibleLimit);

            sb.AppendLine("<b><color=#ffd36a>" + (expanded ? "▼ " : "▶ ") + title + " " + (enabled ? "<color=#7aff4d>[ATIVO]</color>" : "<color=#ff6666>[OFF]</color>") + "</color></b>");
            sb.AppendLine("<color=#d8c098>" + description + "</color>");
            sb.AppendLine("<color=#b8873d>────────────────────────────────────────</color>");

            if (total == 0)
            {
                sb.AppendLine("  <color=#aaaaaa>Nenhuma regra configurada.</color>");
                sb.AppendLine();
                return;
            }

            sb.AppendLine("  <color=#ffd36a><b>Meta / Item</b></color>                          <color=#ffd36a><b>Pontos</b></color>");
            sb.AppendLine("  <color=#805225>───────────────────────────────</color>");
            for (int i = 0; i < limit; i++)
                sb.AppendLine(FormatInventoryGuideRuleColumn(rows[i], jackpotFormat));

            if (!expanded && total > limit)
                sb.AppendLine("  <color=#ffd36a>+" + (total - limit) + " regras ocultas. Clique novamente na categoria da esquerda para expandir.</color>");

            sb.AppendLine();
        }

        private string FormatInventoryGuideRuleColumn(string rule, bool jackpotFormat)
        {
            if (string.IsNullOrWhiteSpace(rule))
                return string.Empty;

            string[] parts = rule.Split(new char[] { ':' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < parts.Length; i++)
                parts[i] = (parts[i] ?? string.Empty).Trim();

            string name = rule.Trim();
            string points = string.Empty;
            if (parts.Length >= 3 && jackpotFormat)
            {
                name = FriendlyInventoryRuleName(parts[0]) + " x" + parts[1];
                points = NormalizeInventoryPointsText(parts[2]);
            }
            else if (parts.Length >= 2)
            {
                name = FriendlyInventoryRuleName(parts[0]);
                points = NormalizeInventoryPointsText(parts[1]);
            }

            if (name.Length > 34)
                name = name.Substring(0, 31) + "...";

            string padded = name.PadRight(38);
            return "  • <color=#e8d2a0>" + padded + "</color><color=#7aff4d>" + points + "</color>";
        }

        private string[] GetInventoryGuideCategoryNames()
        {
            return new string[] { "Combate", "Chefes", "Habilidades", "Pesca", "Cultivo", "Produção", "Quests", "Penalidades", "Dicas" };
        }

        private void CreateInventoryGuideCategoryButtons(InventoryGui gui, Transform parent)
        {
            string[] names = GetInventoryGuideCategoryNames();
            string[] icons = new string[] { "⚔", "☠", "🪓", "🎣", "🌾", "🔨", "📜", "💀", "⭐" };
            for (int i = 0; i < names.Length; i++)
            {
                int index = i;
                GameObject buttonObject = CloneNativeButton(gui, parent, "GuideCategory" + i, icons[i] + "  " + names[i].ToUpperInvariant());
                RectTransform rect = buttonObject.GetComponent<RectTransform>();
                rect.anchorMin = new Vector2(0f, 1f);
                rect.anchorMax = new Vector2(1f, 1f);
                rect.pivot = new Vector2(0.5f, 1f);
                rect.anchoredPosition = new Vector2(0f, -12f - i * 38f);
                rect.sizeDelta = new Vector2(-18f, 32f);
                Button button = buttonObject.GetComponent<Button>();
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(delegate
                {
                    if (_inventoryGuideCategoryIndex == index)
                    {
                        string[] cats = GetInventoryGuideCategoryNames();
                        string key = cats[Mathf.Clamp(index, 0, cats.Length - 1)];
                        if (_inventoryGuideExpandedBlocks.Contains(key))
                            _inventoryGuideExpandedBlocks.Remove(key);
                        else
                            _inventoryGuideExpandedBlocks.Add(key);
                    }
                    _inventoryGuideCategoryIndex = index;
                    UpdateInventoryRankingContent(true);
                });
                _inventoryGuideCategoryButtons.Add(buttonObject);
            }
        }

        private void RefreshInventoryRankingLayout(bool resetScroll)
        {
            bool guide = _inventoryRankingTab == 5;
            if (_inventoryGuideSidebar != null)
                _inventoryGuideSidebar.SetActive(guide);
            if (_inventoryGuideSubSidebar != null)
                _inventoryGuideSubSidebar.SetActive(guide);

            if (_inventoryRankingScrollRect != null)
            {
                RectTransform viewportRect = _inventoryRankingScrollRect.GetComponent<RectTransform>();
                if (viewportRect != null)
                    ApplyInventoryRect(viewportRect, guide ? InventoryStretch(222f, 102f, 222f, 118f) : InventoryStretch(18f, 102f, 18f, 118f));

                Canvas.ForceUpdateCanvases();
                RectTransform content = _inventoryRankingScrollRect.content;
                if (content != null && _inventoryRankingBody != null)
                {
                    float height = Mathf.Max(1000f, _inventoryRankingBody.preferredHeight + 60f);
                    content.sizeDelta = new Vector2(content.sizeDelta.x, height);
                }
                if (resetScroll)
                    _inventoryRankingScrollRect.verticalNormalizedPosition = 1f;
            }
        }

        private void AppendInventoryRankingHistory(StringBuilder sb, SnapshotPlayerData player)
        {
            sb.AppendLine("HISTORICO");
            sb.AppendLine();
            sb.AppendLine("Ultima acao: " + (string.IsNullOrWhiteSpace(player.LastReason) ? "nenhuma" : player.LastReason));
            sb.AppendLine("Atualizacao: " + FormatHudLastUpdate(player.LastUpdateUtc));
            sb.AppendLine();
            sb.AppendLine("Kills pontuadas: " + player.TotalKillsPontuadas);
            sb.AppendLine("Bosses pontuados: " + player.TotalBossesPontuadas);
            sb.AppendLine("Crafts pontuados: " + player.TotalCraftPontuadas);
            sb.AppendLine("Quests pontuadas: " + player.TotalMarketplaceQuestsPontuadas);
            sb.AppendLine("Pescas pontuadas: " + player.TotalFishingPontuadas);
            sb.AppendLine("Mortes: " + player.TotalDeaths);
            sb.AppendLine("Cambios realizados: " + player.TotalPointsExchanges);
        }

        private int GetInventoryMaxExchangePoints(SnapshotPlayerData player)
        {
            if (_rules == null || !_rules.PointsExchangeEnabled || player == null)
                return 0;

            int points = Mathf.Max(0, player.Points);
            int maxPoints = _rules.PointsExchangeMaxPointsPerRequest > 0
                ? Mathf.Min(points, _rules.PointsExchangeMaxPointsPerRequest)
                : points;

            if (_rules.PointsExchangeUsePointsPerCoin)
            {
                int pointsPerCoin = Mathf.Max(1, _rules.PointsExchangePointsPerCoin);
                maxPoints = (maxPoints / pointsPerCoin) * pointsPerCoin;
            }

            return Mathf.Max(0, maxPoints);
        }

        private GameObject CloneNativeButton(InventoryGui gui, Transform parent, string name, string label)
        {
            Transform prefab = gui.m_takeAllButton != null ? gui.m_takeAllButton.transform : null;
            if (prefab == null && gui.m_tabCraft != null)
                prefab = gui.m_tabCraft.transform;

            GameObject buttonObject = prefab != null
                ? Instantiate(prefab.gameObject, parent, false)
                : new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));

            buttonObject.name = name;
            Button button = buttonObject.GetComponent<Button>();
            if (button == null)
                button = buttonObject.AddComponent<Button>();

            Image image = buttonObject.GetComponent<Image>();
            if (image != null)
                image.raycastTarget = true;

            SetNativeButtonText(buttonObject.transform, label);
            return buttonObject;
        }

        private void SetNativeButtonText(Transform root, string value)
        {
            DisableNativeLocalization(root);

            Text[] texts = root.GetComponentsInChildren<Text>(true);
            for (int i = 0; i < texts.Length; i++)
                texts[i].text = value;

            Component[] components = root.GetComponentsInChildren<Component>(true);
            for (int i = 0; i < components.Length; i++)
            {
                Component component = components[i];
                if (component == null)
                    continue;

                Type type = component.GetType();
                PropertyInfo property = type.GetProperty("text");
                if (property != null && property.CanWrite)
                {
                    try
                    {
                        if (property.PropertyType == typeof(string) &&
                            (type.FullName.IndexOf("TMPro", StringComparison.OrdinalIgnoreCase) >= 0 ||
                             type.Name.IndexOf("Text", StringComparison.OrdinalIgnoreCase) >= 0))
                            property.SetValue(component, value, null);
                    }
                    catch { }
                }
            }
        }

        private void DisableNativeLocalization(Transform root)
        {
            if (root == null)
                return;

            Component[] components = root.GetComponentsInChildren<Component>(true);
            for (int i = 0; i < components.Length; i++)
            {
                Component component = components[i];
                if (component == null)
                    continue;

                Type type = component.GetType();
                string typeName = type.FullName ?? type.Name;
                if (typeName.IndexOf("Localize", StringComparison.OrdinalIgnoreCase) < 0 &&
                    typeName.IndexOf("Localization", StringComparison.OrdinalIgnoreCase) < 0)
                    continue;

                Behaviour behaviour = component as Behaviour;
                if (behaviour != null)
                    behaviour.enabled = false;
            }
        }

        private GameObject CreateInventoryPanel(string name, Transform parent, RectOffsetSpec rectSpec, Color color)
        {
            GameObject panel = new GameObject(name, typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(parent, false);
            ApplyInventoryRect(panel.GetComponent<RectTransform>(), rectSpec);
            Image image = panel.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = color.a > 0.02f;
            return panel;
        }

        private Text CreateInventoryText(string name, Transform parent, RectOffsetSpec rectSpec, string value, int size, Color color, TextAnchor anchor, bool bold)
        {
            GameObject textObject = new GameObject(name, typeof(RectTransform), typeof(Text));
            textObject.transform.SetParent(parent, false);
            ApplyInventoryRect(textObject.GetComponent<RectTransform>(), rectSpec);
            Text text = textObject.GetComponent<Text>();
            text.text = value;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = size;
            text.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;
            text.color = color;
            text.alignment = anchor;
            text.raycastTarget = false;
            return text;
        }


        private string GetInventoryDeathPenaltyRules(SnapshotPlayerData player)
        {
            if (player == null)
                return string.Empty;

            if (!player.DeathPenaltyUseMultiplier)
                return player.HudDeathPenaltyRules ?? string.Empty;

            int penalty = Mathf.Max(0, player.DeathPenaltyPerDeath);
            if (penalty <= 0)
                return "1 morte:0;15 mortes:0";

            return "1 morte:-" + penalty + ";15 mortes:-" + (penalty * 15);
        }

        private string FormatInventoryGuideRule(string rule, bool jackpotFormat)
        {
            if (string.IsNullOrWhiteSpace(rule))
                return string.Empty;

            string[] parts = rule.Split(new char[] { ':' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < parts.Length; i++)
                parts[i] = (parts[i] ?? string.Empty).Trim();

            if (parts.Length >= 3 && jackpotFormat)
                return "<color=#ffd36a>" + FriendlyInventoryRuleName(parts[0]) + "</color> x" + parts[1] + " → <color=#7aff4d>" + NormalizeInventoryPointsText(parts[2]) + "</color>";

            if (parts.Length >= 2)
                return "<color=#ffd36a>" + FriendlyInventoryRuleName(parts[0]) + "</color> → <color=#7aff4d>" + NormalizeInventoryPointsText(parts[1]) + "</color>";

            return "<color=#dddddd>" + rule.Trim() + "</color>";
        }

        private string NormalizeInventoryPointsText(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return "0 pts";

            value = value.Trim();
            int points;
            if (int.TryParse(value.Replace("+", string.Empty), out points))
            {
                if (points < 0)
                    return points + " pts";
                return "+" + points + " pts";
            }

            if (value.IndexOf("pt", StringComparison.OrdinalIgnoreCase) >= 0)
                return value;

            return value + " pts";
        }

        private string FriendlyInventoryRuleName(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return "Regra";

            string name = value.Trim().Replace('_', ' ');
            if (name.StartsWith("GLITNIR CAT ", StringComparison.OrdinalIgnoreCase))
                name = name.Substring("GLITNIR CAT ".Length).Trim();
            if (name.StartsWith("GLITNIR_CAT_", StringComparison.OrdinalIgnoreCase))
                name = name.Substring("GLITNIR_CAT_".Length).Trim();

            return name;
        }

        private RectOffsetSpec InventoryStretch()
        {
            return InventoryStretch(0f, 0f, 0f, 0f);
        }

        private RectOffsetSpec InventoryStretch(float left, float bottom, float right, float top)
        {
            return new RectOffsetSpec { Left = left, Bottom = bottom, Right = right, Top = top };
        }

        private void ApplyInventoryRect(RectTransform rect, RectOffsetSpec spec)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = new Vector2(spec.Left, spec.Bottom);
            rect.offsetMax = new Vector2(-spec.Right, -spec.Top);
        }

        private struct RectOffsetSpec
        {
            public float Left;
            public float Bottom;
            public float Right;
            public float Top;
        }
    }

    [HarmonyPatch(typeof(InventoryGui))]
    public static class GlitnirRankingInventoryGuiPatch
    {
        [HarmonyPostfix]
        [HarmonyPatch("Awake")]
        private static void AwakePostfix(InventoryGui __instance)
        {
            GlitnirRankingPlugin.Instance?.EnsureInventoryRankingGui(__instance);
        }

        [HarmonyPostfix]
        [HarmonyPatch("Update")]
        private static void UpdatePostfix(InventoryGui __instance)
        {
            GlitnirRankingPlugin.Instance?.UpdateInventoryRankingGui(__instance);
        }
    }
}
