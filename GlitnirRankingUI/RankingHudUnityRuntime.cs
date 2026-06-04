using System;
using UnityEngine;
using UnityEngine.UI;

namespace Glitnir.Ranking
{
    public partial class GlitnirRankingPlugin
    {
        private Font _runtimeUnityHudFont;

        private bool CreateRuntimeUnityRankingHud()
        {
            try
            {
                EnsureUnityHudEventSystem();
                _runtimeUnityHudFont = LoadRuntimeUnityHudFont();

                _unityHudCanvas = new GameObject("GlitnirRankingHudCanvas_RuntimeUGUI", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
                DontDestroyOnLoad(_unityHudCanvas);

                Canvas canvas = _unityHudCanvas.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.overrideSorting = true;
                canvas.sortingOrder = 32745;

                CanvasScaler scaler = _unityHudCanvas.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920f, 1080f);
                scaler.matchWidthOrHeight = 0.5f;

                BuildRuntimeUnityHud(_unityHudCanvas.transform);
                CacheUnityHudTransforms(_unityHudCanvas.transform);
                BindUnityHudButtons();
                _unityHudCanvas.SetActive(false);
                _unityHudAvailable = true;
                Logger.LogInfo("[Glitnir Ranking] HUD Unity runtime criado com sucesso.");
                return true;
            }
            catch (Exception ex)
            {
                Logger.LogWarning("[Glitnir Ranking] Falha ao criar HUD Unity runtime: " + ex);
                return false;
            }
        }

        private Font LoadRuntimeUnityHudFont()
        {
            Font font = null;
            try { font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); } catch { }
            if (font != null)
                return font;

            try { font = Font.CreateDynamicFontFromOSFont("Arial", 16); } catch { }
            return font;
        }

        private void BuildRuntimeUnityHud(Transform canvas)
        {
            Panel("ScreenTint", canvas, Stretch(), new Color(0f, 0f, 0f, 0.24f));

            GameObject badge = Panel("CollapsedBadge", canvas, Fixed(new Vector2(42f, 0f), new Vector2(86f, 168f), new Vector2(0f, 0.5f)), new Color(0.035f, 0.038f, 0.036f, 0.96f));
            Outline(badge, new Color(0.82f, 0.58f, 0.22f, 1f), new Vector2(2f, -2f));
            Label("BadgeRankText", badge.transform, Stretch(8f, 42f, 8f, 34f), "R", 52, new Color(0.94f, 0.66f, 0.23f, 1f), TextAnchor.MiddleCenter, true);
            Label("BadgeHintText", badge.transform, Stretch(8f, 12f, 8f, 112f), "RANK", 12, new Color(0.90f, 0.82f, 0.66f, 1f), TextAnchor.MiddleCenter, true);

            GameObject main = Panel("MainPanel", canvas, Fixed(Vector2.zero, new Vector2(1120f, 780f), new Vector2(0.5f, 0.5f)), new Color(0.025f, 0.028f, 0.026f, 0.97f));
            Outline(main, new Color(0.86f, 0.61f, 0.24f, 1f), new Vector2(3f, -3f));
            Panel("InnerFrame", main.transform, Stretch(14f, 14f, 14f, 14f), new Color(0.05f, 0.035f, 0.018f, 0.32f));

            GameObject header = Panel("Header", main.transform, Stretch(24f, 646f, 24f, 22f), new Color(0.055f, 0.060f, 0.058f, 0.96f));
            Outline(header, new Color(0.48f, 0.32f, 0.12f, 1f), new Vector2(1.5f, -1.5f));
            Label("Title", header.transform, Stretch(92f, 48f, 92f, 22f), "GLITNIR RANKING", 36, new Color(0.94f, 0.66f, 0.23f, 1f), TextAnchor.MiddleLeft, true);
            Label("Subtitle", header.transform, Stretch(96f, 22f, 120f, 72f), "HONOR, DEEDS AND LEGEND", 15, new Color(0.74f, 0.67f, 0.52f, 1f), TextAnchor.MiddleLeft, false);
            Diamond("HeaderGem", header.transform, Fixed(new Vector2(52f, 0f), new Vector2(52f, 52f), new Vector2(0f, 0.5f)), new Color(0.65f, 0.07f, 0.10f, 1f));
            ButtonLike("CloseButton", header.transform, Fixed(new Vector2(-42f, 0f), new Vector2(46f, 46f), new Vector2(1f, 0.5f)), "X", 20);

            GameObject tabs = Panel("Tabs", main.transform, Stretch(38f, 584f, 38f, 148f), new Color(0f, 0f, 0f, 0f));
            string[] tabNames = { "TabLeaderboard", "TabPerformance", "TabGuide", "TabOracle" };
            string[] tabLabels = { "TOP GLITNIR", "YOUR DEEDS", "HONOR GUIDE", "ORACLE" };
            for (int i = 0; i < 4; i++)
            {
                float x = 6f + i * 258f;
                ButtonLike(tabNames[i], tabs.transform, Fixed(new Vector2(x, 0f), new Vector2(246f, 48f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f)), tabLabels[i], 16);
            }

            GameObject body = Panel("Body", main.transform, Stretch(38f, 38f, 38f, 210f), new Color(0.040f, 0.047f, 0.047f, 0.86f));
            Outline(body, new Color(0.36f, 0.23f, 0.10f, 1f), new Vector2(1.5f, -1.5f));
            CreateRuntimeLeaderboard(body.transform);
            CreateRuntimePerformance(body.transform);
            CreateRuntimeGuide(body.transform);
            CreateRuntimeOracle(body.transform);
        }

        private void CreateRuntimeLeaderboard(Transform parent)
        {
            GameObject view = Panel("ViewLeaderboard", parent, Stretch(), new Color(0f, 0f, 0f, 0f));
            GameObject podium = Panel("Podium", view.transform, Stretch(24f, 410f, 24f, 24f), new Color(0.075f, 0.078f, 0.070f, 0.90f));
            Outline(podium, new Color(0.48f, 0.32f, 0.12f, 1f), new Vector2(1.5f, -1.5f));
            PodiumSlot(podium.transform, "Rank2", 0.18f, "II");
            PodiumSlot(podium.transform, "Rank1", 0.50f, "I");
            PodiumSlot(podium.transform, "Rank3", 0.82f, "III");

            GameObject list = Panel("LeaderboardListPanel", view.transform, Stretch(24f, 24f, 24f, 184f), new Color(0.055f, 0.060f, 0.060f, 0.92f));
            Outline(list, new Color(0.42f, 0.27f, 0.10f, 1f), new Vector2(1.5f, -1.5f));
            Label("ListHeader", list.transform, Stretch(24f, 466f, 24f, 18f), "RANK     WARRIOR                         POINTS       TITLE", 15, new Color(0.68f, 0.61f, 0.48f, 1f), TextAnchor.MiddleLeft, true);
            GameObject rows = Panel("Rows", list.transform, Stretch(20f, 20f, 20f, 64f), new Color(0f, 0f, 0f, 0f));
            for (int i = 1; i <= 10; i++)
                RankingRow(rows.transform, "Row" + i, i);
        }

        private void CreateRuntimePerformance(Transform parent)
        {
            GameObject view = Panel("ViewPerformance", parent, Stretch(), new Color(0f, 0f, 0f, 0f));
            view.SetActive(false);

            GameObject profile = Panel("PlayerProfile", view.transform, Stretch(24f, 354f, 640f, 24f), new Color(0.065f, 0.070f, 0.068f, 0.92f));
            Outline(profile, new Color(0.42f, 0.27f, 0.10f, 1f), new Vector2(1.5f, -1.5f));
            Label("PlayerName", profile.transform, Stretch(96f, 222f, 24f, 24f), "WARRIOR", 25, new Color(0.94f, 0.66f, 0.23f, 1f), TextAnchor.MiddleLeft, true);
            Label("PlayerRank", profile.transform, Stretch(98f, 188f, 24f, 70f), "RANK #1 | 0 POINTS", 16, new Color(0.88f, 0.80f, 0.64f, 1f), TextAnchor.MiddleLeft, false);
            Diamond("PlayerGem", profile.transform, Fixed(new Vector2(52f, -55f), new Vector2(60f, 60f), new Vector2(0f, 1f)), new Color(0.65f, 0.07f, 0.10f, 1f));
            StatBar(profile.transform, "CombatBar", "COMBAT", 138f);
            StatBar(profile.transform, "CraftBar", "CRAFT", 92f);
            StatBar(profile.transform, "ExploreBar", "EXPLORE", 46f);

            GameObject summary = Panel("ProgressSummary", view.transform, Stretch(480f, 354f, 24f, 24f), new Color(0.065f, 0.070f, 0.068f, 0.92f));
            Outline(summary, new Color(0.42f, 0.27f, 0.10f, 1f), new Vector2(1.5f, -1.5f));
            Label("SummaryTitle", summary.transform, Stretch(28f, 222f, 28f, 24f), "SEASON PROGRESS", 22, new Color(0.94f, 0.66f, 0.23f, 1f), TextAnchor.MiddleLeft, true);
            Metric(summary.transform, "MetricKills", "KILLS", 0.13f);
            Metric(summary.transform, "MetricCrafts", "CRAFTS", 0.37f);
            Metric(summary.transform, "MetricQuests", "QUESTS", 0.61f);
            Metric(summary.transform, "MetricFish", "FISH", 0.85f);

            GameObject feed = Panel("RecentDeedsPanel", view.transform, Stretch(24f, 24f, 24f, 248f), new Color(0.055f, 0.060f, 0.060f, 0.92f));
            Outline(feed, new Color(0.42f, 0.27f, 0.10f, 1f), new Vector2(1.5f, -1.5f));
            Label("RecentDeedsTitle", feed.transform, Stretch(28f, 298f, 28f, 24f), "RECENT DEEDS", 21, new Color(0.94f, 0.66f, 0.23f, 1f), TextAnchor.MiddleLeft, true);
            GameObject rows = Panel("RecentDeedsRows", feed.transform, Stretch(22f, 20f, 22f, 70f), new Color(0f, 0f, 0f, 0f));
            for (int i = 1; i <= 7; i++)
                TextRow(rows.transform, "DeedRow" + i, i, "DEED " + i);
        }

        private void CreateRuntimeGuide(Transform parent)
        {
            GameObject view = Panel("ViewGuide", parent, Stretch(), new Color(0f, 0f, 0f, 0f));
            view.SetActive(false);
            GameObject categories = Panel("GuideCategories", view.transform, Stretch(24f, 24f, 740f, 24f), new Color(0.065f, 0.070f, 0.068f, 0.92f));
            Outline(categories, new Color(0.42f, 0.27f, 0.10f, 1f), new Vector2(1.5f, -1.5f));
            Label("GuideTitle", categories.transform, Stretch(22f, 478f, 22f, 20f), "HONOR PATHS", 22, new Color(0.94f, 0.66f, 0.23f, 1f), TextAnchor.MiddleLeft, true);
            for (int i = 1; i <= 6; i++)
                ButtonLike("GuideCategory" + i, categories.transform, Stretch(22f, 428f - i * 58f, 22f, 92f + i * 58f), "PATH", 14);

            GameObject entries = Panel("GuideEntries", view.transform, Stretch(356f, 24f, 24f, 24f), new Color(0.065f, 0.070f, 0.068f, 0.92f));
            Outline(entries, new Color(0.42f, 0.27f, 0.10f, 1f), new Vector2(1.5f, -1.5f));
            Label("GuideEntriesTitle", entries.transform, Stretch(28f, 478f, 28f, 20f), "POINT RULES", 22, new Color(0.94f, 0.66f, 0.23f, 1f), TextAnchor.MiddleLeft, true);
            GameObject rows = Panel("GuideRows", entries.transform, Stretch(24f, 20f, 24f, 74f), new Color(0f, 0f, 0f, 0f));
            for (int i = 1; i <= 9; i++)
                RuleRow(rows.transform, "RuleRow" + i, i);
        }

        private void CreateRuntimeOracle(Transform parent)
        {
            GameObject view = Panel("ViewOracle", parent, Stretch(), new Color(0f, 0f, 0f, 0f));
            view.SetActive(false);
            GameObject card = Panel("OracleCard", view.transform, Fixed(Vector2.zero, new Vector2(760f, 440f), new Vector2(0.5f, 0.5f)), new Color(0.060f, 0.064f, 0.062f, 0.96f));
            Outline(card, new Color(0.86f, 0.61f, 0.24f, 1f), new Vector2(2f, -2f));
            Diamond("OracleGem", card.transform, Fixed(new Vector2(0f, -36f), new Vector2(76f, 76f), new Vector2(0.5f, 1f)), new Color(0.65f, 0.07f, 0.10f, 1f));
            Label("OracleTitle", card.transform, Stretch(40f, 306f, 40f, 84f), "ORACLE OF GLITNIR", 30, new Color(0.94f, 0.66f, 0.23f, 1f), TextAnchor.MiddleCenter, true);
            Label("OracleText", card.transform, Stretch(72f, 126f, 72f, 168f), "Keep earning honor to awaken the oracle.", 23, new Color(0.88f, 0.80f, 0.64f, 1f), TextAnchor.MiddleCenter, false);
            ButtonLike("OracleActionButton", card.transform, Fixed(new Vector2(0f, 62f), new Vector2(260f, 48f), new Vector2(0.5f, 0f)), "CLAIM REWARD", 16);
        }

        private void PodiumSlot(Transform parent, string name, float x, string rank)
        {
            GameObject slot = Panel(name, parent, Fixed(new Vector2((x - 0.5f) * 900f, 0f), new Vector2(250f, 92f), new Vector2(0.5f, 0.5f)), new Color(0.035f, 0.038f, 0.036f, 0.82f));
            Outline(slot, new Color(0.64f, 0.43f, 0.16f, 1f), new Vector2(1.5f, -1.5f));
            Label("Rank", slot.transform, Stretch(18f, 20f, 178f, 20f), rank, 32, new Color(0.94f, 0.66f, 0.23f, 1f), TextAnchor.MiddleCenter, true);
            Label("Name", slot.transform, Stretch(82f, 42f, 16f, 20f), "WARRIOR", 15, new Color(0.92f, 0.86f, 0.70f, 1f), TextAnchor.MiddleLeft, true);
            Label("Points", slot.transform, Stretch(82f, 18f, 16f, 52f), "0", 14, new Color(0.70f, 0.62f, 0.48f, 1f), TextAnchor.MiddleLeft, false);
        }

        private void RankingRow(Transform parent, string name, int index)
        {
            float y = 396f - (index - 1) * 39f;
            GameObject row = Panel(name, parent, Stretch(0f, y, 0f, 396f - y + 36f), index % 2 == 0 ? new Color(0.075f, 0.078f, 0.070f, 0.72f) : new Color(0.035f, 0.038f, 0.036f, 0.72f));
            Label("Position", row.transform, Stretch(16f, 0f, 930f, 0f), index.ToString("00"), 17, new Color(0.94f, 0.66f, 0.23f, 1f), TextAnchor.MiddleLeft, true);
            Label("Player", row.transform, Stretch(92f, 0f, 470f, 0f), "WARRIOR", 16, new Color(0.92f, 0.86f, 0.70f, 1f), TextAnchor.MiddleLeft, true);
            Label("Points", row.transform, Stretch(620f, 0f, 210f, 0f), "0", 15, new Color(0.88f, 0.80f, 0.64f, 1f), TextAnchor.MiddleRight, true);
            Label("Title", row.transform, Stretch(838f, 0f, 16f, 0f), "HONORED", 13, new Color(0.70f, 0.62f, 0.48f, 1f), TextAnchor.MiddleRight, false);
        }

        private void StatBar(Transform parent, string name, string label, float y)
        {
            GameObject group = Panel(name, parent, Stretch(28f, y, 28f, 258f - y), new Color(0f, 0f, 0f, 0f));
            Label("Label", group.transform, Stretch(0f, 20f, 280f, 0f), label, 13, new Color(0.70f, 0.62f, 0.48f, 1f), TextAnchor.MiddleLeft, true);
            GameObject track = Panel("Track", group.transform, Stretch(88f, 18f, 0f, 6f), new Color(0f, 0f, 0f, 0.52f));
            Panel("Fill", track.transform, Stretch(), new Color(0.94f, 0.66f, 0.23f, 0.84f));
        }

        private void Metric(Transform parent, string name, string label, float x)
        {
            GameObject metric = Panel(name, parent, Fixed(new Vector2((x - 0.5f) * 520f, -36f), new Vector2(112f, 112f), new Vector2(0.5f, 0.5f)), new Color(0.035f, 0.038f, 0.036f, 0.78f));
            Outline(metric, new Color(0.36f, 0.23f, 0.10f, 1f), new Vector2(1f, -1f));
            Label("Label", metric.transform, Stretch(8f, 70f, 8f, 14f), label, 12, new Color(0.70f, 0.62f, 0.48f, 1f), TextAnchor.MiddleCenter, true);
            Label("Value", metric.transform, Stretch(8f, 20f, 8f, 38f), "0", 25, new Color(0.94f, 0.66f, 0.23f, 1f), TextAnchor.MiddleCenter, true);
        }

        private void TextRow(Transform parent, string name, int index, string text)
        {
            float y = 246f - (index - 1) * 36f;
            GameObject row = Panel(name, parent, Stretch(0f, y, 0f, 246f - y + 32f), index % 2 == 0 ? new Color(0.075f, 0.078f, 0.070f, 0.72f) : new Color(0.035f, 0.038f, 0.036f, 0.72f));
            Label("Text", row.transform, Stretch(18f, 0f, 18f, 0f), text, 15, new Color(0.88f, 0.80f, 0.64f, 1f), TextAnchor.MiddleLeft, false);
        }

        private void RuleRow(Transform parent, string name, int index)
        {
            float y = 396f - (index - 1) * 43f;
            GameObject row = Panel(name, parent, Stretch(0f, y, 0f, 396f - y + 38f), index % 2 == 0 ? new Color(0.075f, 0.078f, 0.070f, 0.72f) : new Color(0.035f, 0.038f, 0.036f, 0.72f));
            Label("Action", row.transform, Stretch(18f, 0f, 150f, 0f), "ACTION", 15, new Color(0.88f, 0.80f, 0.64f, 1f), TextAnchor.MiddleLeft, false);
            Label("Points", row.transform, Stretch(410f, 0f, 18f, 0f), "+0", 15, new Color(0.94f, 0.66f, 0.23f, 1f), TextAnchor.MiddleRight, true);
        }

        private GameObject ButtonLike(string name, Transform parent, RectSpec rect, string text, int fontSize)
        {
            GameObject button = Panel(name, parent, rect, new Color(0.070f, 0.076f, 0.073f, 0.94f));
            Outline(button, new Color(0.42f, 0.27f, 0.10f, 1f), new Vector2(1.2f, -1.2f));
            Label("Label", button.transform, Stretch(8f, 0f, 8f, 0f), text, fontSize, new Color(0.92f, 0.84f, 0.66f, 1f), TextAnchor.MiddleCenter, true);
            return button;
        }

        private GameObject Diamond(string name, Transform parent, RectSpec rect, Color color)
        {
            GameObject diamond = Panel(name, parent, rect, color);
            diamond.transform.localRotation = Quaternion.Euler(0f, 0f, 45f);
            Outline(diamond, new Color(0.86f, 0.61f, 0.24f, 1f), new Vector2(1.2f, -1.2f));
            return diamond;
        }

        private GameObject Panel(string name, Transform parent, RectSpec rect, Color color)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            ApplyRect(go.GetComponent<RectTransform>(), rect);
            Image image = go.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = color.a > 0.02f;
            return go;
        }

        private Text Label(string name, Transform parent, RectSpec rect, string value, int fontSize, Color color, TextAnchor anchor, bool bold)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            ApplyRect(go.GetComponent<RectTransform>(), rect);
            Text text = go.GetComponent<Text>();
            text.text = value;
            text.font = _runtimeUnityHudFont;
            text.fontSize = fontSize;
            text.color = color;
            text.alignment = anchor;
            text.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            return text;
        }

        private void Outline(GameObject go, Color color, Vector2 distance)
        {
            Outline outline = go.GetComponent<Outline>();
            if (outline == null)
                outline = go.AddComponent<Outline>();
            outline.effectColor = color;
            outline.effectDistance = distance;
        }

        private RectSpec Stretch()
        {
            return Stretch(0f, 0f, 0f, 0f);
        }

        private RectSpec Stretch(float left, float bottom, float right, float top)
        {
            return new RectSpec
            {
                AnchorMin = Vector2.zero,
                AnchorMax = Vector2.one,
                Pivot = new Vector2(0.5f, 0.5f),
                OffsetMin = new Vector2(left, bottom),
                OffsetMax = new Vector2(-right, -top),
                UseOffsets = true
            };
        }

        private RectSpec Fixed(Vector2 position, Vector2 size, Vector2 anchor)
        {
            return Fixed(position, size, anchor, new Vector2(0.5f, 0.5f));
        }

        private RectSpec Fixed(Vector2 position, Vector2 size, Vector2 anchor, Vector2 pivot)
        {
            return new RectSpec
            {
                AnchorMin = anchor,
                AnchorMax = anchor,
                Pivot = pivot,
                AnchoredPosition = position,
                SizeDelta = size,
                UseOffsets = false
            };
        }

        private void ApplyRect(RectTransform rect, RectSpec spec)
        {
            rect.anchorMin = spec.AnchorMin;
            rect.anchorMax = spec.AnchorMax;
            rect.pivot = spec.Pivot;
            if (spec.UseOffsets)
            {
                rect.offsetMin = spec.OffsetMin;
                rect.offsetMax = spec.OffsetMax;
            }
            else
            {
                rect.anchoredPosition = spec.AnchoredPosition;
                rect.sizeDelta = spec.SizeDelta;
            }
        }

        private struct RectSpec
        {
            public Vector2 AnchorMin;
            public Vector2 AnchorMax;
            public Vector2 Pivot;
            public Vector2 OffsetMin;
            public Vector2 OffsetMax;
            public Vector2 AnchoredPosition;
            public Vector2 SizeDelta;
            public bool UseOffsets;
        }
    }
}
