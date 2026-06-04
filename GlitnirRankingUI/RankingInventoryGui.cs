using System;
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

            Transform root = gui.transform;
            _inventoryRankingButton = CreateInventoryRankingButton(root);
            _inventoryRankingPanel = CreateInventoryRankingPanel(root);
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

        private GameObject CreateInventoryRankingButton(Transform root)
        {
            GameObject buttonObject = new GameObject("GlitnirRanking_TabButton", typeof(RectTransform), typeof(Image), typeof(Button));
            buttonObject.transform.SetParent(root, false);
            RectTransform rect = buttonObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(1f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(1f, 1f);
            rect.anchoredPosition = new Vector2(-34f, -38f);
            rect.sizeDelta = new Vector2(118f, 42f);

            Image image = buttonObject.GetComponent<Image>();
            image.color = new Color(0.18f, 0.10f, 0.035f, 0.94f);
            image.raycastTarget = true;
            Outline outline = buttonObject.AddComponent<Outline>();
            outline.effectColor = new Color(0.88f, 0.58f, 0.18f, 0.96f);
            outline.effectDistance = new Vector2(1.5f, -1.5f);

            Button button = buttonObject.GetComponent<Button>();
            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1f, 0.86f, 0.42f, 1f);
            colors.pressedColor = new Color(0.72f, 0.18f, 0.12f, 1f);
            colors.selectedColor = colors.highlightedColor;
            colors.colorMultiplier = 1f;
            button.colors = colors;
            button.onClick.AddListener(delegate { SetInventoryRankingOpen(!_inventoryRankingOpen); });

            CreateInventoryText("Label", buttonObject.transform, InventoryStretch(), "RANKING", 15, new Color(1f, 0.76f, 0.28f, 1f), TextAnchor.MiddleCenter, true);
            return buttonObject;
        }

        private GameObject CreateInventoryRankingPanel(Transform root)
        {
            GameObject panel = new GameObject("GlitnirRanking_NativePanel", typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(root, false);
            RectTransform rect = panel.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(245f, -20f);
            rect.sizeDelta = new Vector2(690f, 610f);

            Image image = panel.GetComponent<Image>();
            image.color = new Color(0.13f, 0.073f, 0.035f, 0.96f);
            image.raycastTarget = true;
            Outline outline = panel.AddComponent<Outline>();
            outline.effectColor = new Color(0.72f, 0.45f, 0.15f, 1f);
            outline.effectDistance = new Vector2(2f, -2f);

            GameObject header = CreateInventoryPanel("Header", panel.transform, InventoryStretch(14f, 548f, 14f, 14f), new Color(0.09f, 0.05f, 0.025f, 0.98f));
            _inventoryRankingTitle = CreateInventoryText("Title", header.transform, InventoryStretch(20f, 8f, 84f, 8f), "GLITNIR RANKING", 24, new Color(1f, 0.76f, 0.28f, 1f), TextAnchor.MiddleLeft, true);

            GameObject close = CreateInventoryRankingCloseButton(header.transform);
            close.transform.SetAsLastSibling();

            GameObject viewport = CreateInventoryPanel("Viewport", panel.transform, InventoryStretch(18f, 18f, 18f, 80f), new Color(0.055f, 0.034f, 0.022f, 0.74f));
            _inventoryRankingBody = CreateInventoryText("Body", viewport.transform, InventoryStretch(18f, 18f, 18f, 18f), "", 15, new Color(0.92f, 0.84f, 0.67f, 1f), TextAnchor.UpperLeft, false);
            _inventoryRankingBody.horizontalOverflow = HorizontalWrapMode.Wrap;
            _inventoryRankingBody.verticalOverflow = VerticalWrapMode.Overflow;
            return panel;
        }

        private GameObject CreateInventoryRankingCloseButton(Transform parent)
        {
            GameObject buttonObject = new GameObject("CloseButton", typeof(RectTransform), typeof(Image), typeof(Button));
            buttonObject.transform.SetParent(parent, false);
            RectTransform rect = buttonObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(1f, 0.5f);
            rect.anchorMax = new Vector2(1f, 0.5f);
            rect.pivot = new Vector2(1f, 0.5f);
            rect.anchoredPosition = new Vector2(-12f, 0f);
            rect.sizeDelta = new Vector2(44f, 40f);
            buttonObject.GetComponent<Image>().color = new Color(0.20f, 0.08f, 0.035f, 0.98f);
            buttonObject.GetComponent<Button>().onClick.AddListener(delegate { SetInventoryRankingOpen(false); });
            CreateInventoryText("Label", buttonObject.transform, InventoryStretch(), "X", 18, new Color(1f, 0.76f, 0.28f, 1f), TextAnchor.MiddleCenter, true);
            return buttonObject;
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
            sb.AppendLine("YOUR HONOR");
            sb.AppendLine("Name: " + (string.IsNullOrWhiteSpace(player.PlayerName) ? GetLocalPlayerName() : player.PlayerName));
            sb.AppendLine("Rank: #" + Mathf.Max(1, player.Position) + "     Points: " + FormatPoints(player.Points));
            sb.AppendLine("Kills: " + player.TotalKillsPontuadas + "     Crafts: " + player.TotalCraftPontuadas + "     Quests: " + player.TotalMarketplaceQuestsPontuadas);
            sb.AppendLine();
            sb.AppendLine("TOP GLITNIR");

            int count = _cachedTopEntries != null ? _cachedTopEntries.Count : 0;
            if (count == 0)
            {
                sb.AppendLine("Waiting for server ranking data...");
            }
            else
            {
                int limit = Mathf.Min(10, count);
                for (int i = 0; i < limit; i++)
                {
                    SnapshotTopEntryData entry = _cachedTopEntries[i];
                    string name = string.IsNullOrWhiteSpace(entry.PlayerName) ? "WARRIOR" : entry.PlayerName;
                    sb.AppendLine(entry.Position.ToString("00") + "  " + name + "  -  " + FormatPoints(entry.Points) + " pts");
                }
            }

            sb.AppendLine();
            sb.AppendLine("HONOR SOURCES");
            sb.AppendLine("Combat +" + FormatPoints(player.KillPointsTotal + player.BossPointsTotal));
            sb.AppendLine("Skills +" + FormatPoints(player.SkillPointsTotal));
            sb.AppendLine("Marketplace +" + FormatPoints(player.MarketplaceQuestPointsTotal));
            sb.AppendLine("Fishing +" + FormatPoints(player.FishingPointsTotal));
            sb.AppendLine("Crafting +" + FormatPoints(player.CraftPointsTotal));
            sb.AppendLine("Deaths -" + FormatPoints(player.DeathPenaltyPointsTotal));

            _inventoryRankingBody.text = sb.ToString();
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
