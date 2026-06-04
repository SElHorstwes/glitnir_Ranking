using System;
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
        private bool _inventoryRankingOpen;
        private float _inventoryRankingNextRefresh;
        private int _inventoryRankingTab;

        private bool TryToggleInventoryRanking()
        {
            if (Application.isBatchMode || InventoryGui.instance == null)
                return false;

            InventoryGui gui = InventoryGui.instance;
            if (!InventoryGui.IsVisible())
                gui.Show(null, 0);

            EnsureInventoryRankingGui(gui);
            SetInventoryRankingOpen(!_inventoryRankingOpen);
            return true;
        }

        internal void EnsureInventoryRankingGui(InventoryGui gui)
        {
            if (gui == null)
                return;

            if (_inventoryRankingButton != null && _inventoryRankingPanel != null)
                return;

            Transform root = gui.m_player != null ? gui.m_player : gui.transform;
            _inventoryRankingButton = CreateInventoryRankingButton(gui, root);
            _inventoryRankingPanel = CreateInventoryRankingPanel(gui, root);
            _inventoryRankingPanel.SetActive(false);
            UpdateInventoryRankingContent(true);
        }

        internal void UpdateInventoryRankingGui(InventoryGui gui)
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
            if (_inventoryRankingPanel != null && _inventoryRankingPanel.activeSelf != open)
                _inventoryRankingPanel.SetActive(open);

            if (open)
            {
                RequestSnapshotFromServer();
                UpdateInventoryRankingContent(true);
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
            rect.anchoredPosition = new Vector2(-18f, -18f);
            rect.sizeDelta = new Vector2(116f, 32f);

            Button button = buttonObject.GetComponent<Button>();
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(delegate { SetInventoryRankingOpen(!_inventoryRankingOpen); });
            return buttonObject;
        }

        private GameObject CreateInventoryRankingPanel(InventoryGui gui, Transform root)
        {
            Transform backgroundPrefab = root.Find("Bkg");
            GameObject panel = backgroundPrefab != null
                ? Instantiate(backgroundPrefab.gameObject)
                : new GameObject("GlitnirRanking_NativePanel", typeof(RectTransform), typeof(Image));

            panel.name = "GlitnirRanking_NativePanel";
            panel.transform.SetParent(root, false);
            RectTransform rect = panel.GetComponent<RectTransform>();
            rect.SetAsLastSibling();
            rect.anchorMin = new Vector2(1f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(330f, -255f);
            rect.sizeDelta = new Vector2(650f, 520f);

            Image image = panel.GetComponent<Image>();
            if (image != null)
            {
                image.color = new Color(0.13f, 0.073f, 0.035f, 0.98f);
                image.raycastTarget = true;
            }

            GameObject header = CreateInventoryPanel("Header", panel.transform, InventoryStretch(14f, 460f, 14f, 14f), new Color(0.09f, 0.05f, 0.025f, 0.98f));
            _inventoryRankingTitle = CreateInventoryText("Title", header.transform, InventoryStretch(20f, 8f, 84f, 8f), "GLITNIR RANKING", 24, new Color(1f, 0.76f, 0.28f, 1f), TextAnchor.MiddleLeft, true);

            GameObject close = CreateInventoryRankingCloseButton(gui, header.transform);
            close.transform.SetAsLastSibling();

            GameObject tabs = CreateInventoryPanel("Tabs", panel.transform, InventoryStretch(18f, 398f, 18f, 72f), new Color(0f, 0f, 0f, 0f));
            CreateInventoryRankingTab(gui, tabs.transform, 0, "TOP");
            CreateInventoryRankingTab(gui, tabs.transform, 1, "PERFIL");
            CreateInventoryRankingTab(gui, tabs.transform, 2, "REGRAS");
            CreateInventoryRankingTab(gui, tabs.transform, 3, "RECOMP.");

            GameObject viewport = CreateInventoryPanel("Viewport", panel.transform, InventoryStretch(18f, 18f, 18f, 128f), new Color(0.055f, 0.034f, 0.022f, 0.74f));
            _inventoryRankingBody = CreateInventoryText("Body", viewport.transform, InventoryStretch(18f, 18f, 18f, 18f), "", 15, new Color(0.92f, 0.84f, 0.67f, 1f), TextAnchor.UpperLeft, false);
            _inventoryRankingBody.horizontalOverflow = HorizontalWrapMode.Wrap;
            _inventoryRankingBody.verticalOverflow = VerticalWrapMode.Overflow;
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
            rect.anchoredPosition = new Vector2(index * 154f, 0f);
            rect.sizeDelta = new Vector2(146f, 34f);
            Button button = buttonObject.GetComponent<Button>();
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(delegate
            {
                _inventoryRankingTab = index;
                UpdateInventoryRankingContent(true);
            });
        }

        private void UpdateInventoryRankingContent(bool force)
        {
            if (_inventoryRankingBody == null)
                return;

            if (!force && Time.realtimeSinceStartup < _inventoryRankingNextRefresh)
                return;

            _inventoryRankingNextRefresh = Time.realtimeSinceStartup + 0.5f;
            SnapshotPlayerData player = _cachedPlayerData ?? new SnapshotPlayerData();
            StringBuilder sb = new StringBuilder();
            if (_inventoryRankingTab == 0)
                AppendInventoryRankingTop(sb);
            else if (_inventoryRankingTab == 1)
                AppendInventoryRankingProfile(sb, player);
            else if (_inventoryRankingTab == 2)
                AppendInventoryRankingRules(sb, player);
            else
                AppendInventoryRankingReward(sb, player);

            _inventoryRankingBody.text = sb.ToString();
        }

        private void AppendInventoryRankingTop(StringBuilder sb)
        {
            sb.AppendLine("TOP GLITNIR");
            sb.AppendLine();
            int count = _cachedTopEntries != null ? _cachedTopEntries.Count : 0;
            if (count == 0)
            {
                sb.AppendLine("Aguardando dados do servidor...");
                return;
            }

            int limit = Mathf.Min(10, count);
            for (int i = 0; i < limit; i++)
            {
                SnapshotTopEntryData entry = _cachedTopEntries[i];
                string name = string.IsNullOrWhiteSpace(entry.PlayerName) ? "WARRIOR" : entry.PlayerName;
                sb.AppendLine(entry.Position.ToString("00") + "  " + name + "  -  " + FormatPoints(entry.Points) + " pts");
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
                sb.AppendLine("Voce possui recompensa disponivel.");
            else if (!string.IsNullOrWhiteSpace(player.RewardBlockReason))
                sb.AppendLine(player.RewardBlockReason);
            else
                sb.AppendLine("Continue acumulando honra para liberar a recompensa.");
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
            Text[] texts = root.GetComponentsInChildren<Text>(true);
            for (int i = 0; i < texts.Length; i++)
                texts[i].text = value;

            Component[] components = root.GetComponentsInChildren<Component>(true);
            for (int i = 0; i < components.Length; i++)
            {
                Component component = components[i];
                if (component == null || component.GetType().FullName != "TMPro.TMP_Text")
                    continue;

                PropertyInfo property = component.GetType().GetProperty("text");
                if (property != null && property.CanWrite)
                    property.SetValue(component, value, null);
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
