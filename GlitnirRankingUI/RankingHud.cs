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
    public partial class GlitnirRankingPlugin
    {
        private Texture2D GetRuleCategoryIcon(string categoryKey)
        {
            if (string.IsNullOrWhiteSpace(categoryKey))
                return null;

            Texture2D icon;
            return _uiRuleCategoryIcons.TryGetValue(categoryKey, out icon) ? icon : null;
        }

        private void DrawTextureOrLabel(Rect rect, Texture2D texture, string fallbackText, GUIStyle fallbackStyle)
        {
            if (texture != null)
            {
                DrawTextureAspectFit(rect, texture);
                return;
            }

            GUI.Label(rect, fallbackText, fallbackStyle);
        }

        private void DrawTextureAspectFit(Rect rect, Texture2D texture)
        {
            if (texture == null || rect.width <= 0f || rect.height <= 0f || texture.width <= 0 || texture.height <= 0)
                return;

            float textureRatio = (float)texture.width / texture.height;
            float rectRatio = rect.width / rect.height;
            float drawWidth;
            float drawHeight;

            if (rectRatio > textureRatio)
            {
                drawHeight = rect.height;
                drawWidth = drawHeight * textureRatio;
            }
            else
            {
                drawWidth = rect.width;
                drawHeight = drawWidth / textureRatio;
            }

            Rect drawRect = new Rect(
                rect.x + ((rect.width - drawWidth) * 0.5f),
                rect.y + ((rect.height - drawHeight) * 0.5f),
                drawWidth,
                drawHeight);

            GUI.DrawTexture(drawRect, texture, ScaleMode.ScaleToFit, true);
        }

        private void DrawTextureOrLabelCropped(Rect rect, Texture2D texture, string fallbackText, GUIStyle fallbackStyle, float cropTop, float cropBottom)
        {
            if (texture != null)
            {
                DrawTextureCroppedAspectFit(rect, texture, cropTop, cropBottom);
                return;
            }

            GUI.Label(rect, fallbackText, fallbackStyle);
        }

        private void DrawTextureCroppedAspectFit(Rect rect, Texture2D texture, float cropTop, float cropBottom)
        {
            if (texture == null || rect.width <= 0f || rect.height <= 0f || texture.width <= 0 || texture.height <= 0)
                return;

            cropTop = Mathf.Clamp01(cropTop);
            cropBottom = Mathf.Clamp01(cropBottom);

            if (cropBottom <= cropTop + 0.01f)
            {
                DrawTextureAspectFit(rect, texture);
                return;
            }

            float visibleHeight = texture.height * (cropBottom - cropTop);
            float visibleRatio = (float)texture.width / Mathf.Max(1f, visibleHeight);
            float rectRatio = rect.width / rect.height;
            float drawWidth;
            float drawHeight;

            if (rectRatio > visibleRatio)
            {
                drawHeight = rect.height;
                drawWidth = drawHeight * visibleRatio;
            }
            else
            {
                drawWidth = rect.width;
                drawHeight = drawWidth / visibleRatio;
            }

            Rect drawRect = new Rect(
                rect.x + ((rect.width - drawWidth) * 0.5f),
                rect.y + ((rect.height - drawHeight) * 0.5f),
                drawWidth,
                drawHeight);

            Rect uv = new Rect(0f, 1f - cropBottom, 1f, cropBottom - cropTop);
            GUI.DrawTextureWithTexCoords(drawRect, texture, uv, true);
        }

        private float GetTextureHeightForWidth(Texture2D texture, float width, float fallbackHeight, float minHeight, float maxHeight)
        {
            if (texture != null && texture.width > 0 && texture.height > 0)
            {
                float aspectHeight = width * ((float)texture.height / texture.width);
                return Mathf.Clamp(aspectHeight, minHeight, maxHeight);
            }

            return Mathf.Clamp(fallbackHeight, minHeight, maxHeight);
        }

        private void DrawCroppedIcon(Rect rect, Texture2D texture)
        {
            if (rect.width <= 0f || rect.height <= 0f)
                return;

            if (texture == null)
            {
                GUI.Label(rect, "RANK", _headerStyle);
                return;
            }

            float zoom = Mathf.Clamp(_uiIconZoom, 0.75f, 1.2f);
            float drawWidth = rect.width * zoom;
            float drawHeight = rect.height * zoom;
            float drawX = (rect.width - drawWidth) * 0.5f;
            float drawY = (rect.height - drawHeight) * 0.5f;

            GUI.BeginGroup(rect);
            GUI.DrawTexture(new Rect(drawX, drawY, drawWidth, drawHeight), texture, ScaleMode.ScaleToFit, true);
            GUI.EndGroup();
        }

        private void DrawPanel(Rect rect)
        {
            if (_uiWhiteTexture == null)
                return;

            GUI.DrawTexture(rect, _uiWhiteTexture, ScaleMode.StretchToFill, true, 0f, new Color(0.02f, 0.015f, 0.01f, 0.08f), 0f, 0f);

            if (_uiPanelTexture != null)
                GUI.DrawTexture(rect, _uiPanelTexture, ScaleMode.StretchToFill, true);

            Rect innerRect = new Rect(rect.x + 8f, rect.y + 8f, rect.width - 16f, rect.height - 16f);
            if (innerRect.width > 0f && innerRect.height > 0f)
                GUI.DrawTexture(innerRect, _uiWhiteTexture, ScaleMode.StretchToFill, true, 0f, new Color(0.08f, 0.05f, 0.025f, 0.08f), 0f, 0f);

            Color edge = new Color(0.92f, 0.76f, 0.42f, 0.34f);
            Color glow = new Color(0.97f, 0.83f, 0.48f, 0.05f);

            GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width, 2f), _uiWhiteTexture, ScaleMode.StretchToFill, true, 0f, edge, 0f, 0f);
            GUI.DrawTexture(new Rect(rect.x, rect.yMax - 2f, rect.width, 2f), _uiWhiteTexture, ScaleMode.StretchToFill, true, 0f, edge, 0f, 0f);
            GUI.DrawTexture(new Rect(rect.x, rect.y, 2f, rect.height), _uiWhiteTexture, ScaleMode.StretchToFill, true, 0f, edge, 0f, 0f);
            GUI.DrawTexture(new Rect(rect.xMax - 2f, rect.y, 2f, rect.height), _uiWhiteTexture, ScaleMode.StretchToFill, true, 0f, edge, 0f, 0f);

            GUI.DrawTexture(new Rect(rect.x + 2f, rect.y + 2f, rect.width - 4f, 1f), _uiWhiteTexture, ScaleMode.StretchToFill, true, 0f, glow, 0f, 0f);
            GUI.DrawTexture(new Rect(rect.x + 2f, rect.yMax - 3f, rect.width - 4f, 1f), _uiWhiteTexture, ScaleMode.StretchToFill, true, 0f, glow, 0f, 0f);
        }

        private void OnGUI()
        {
            if (Application.isBatchMode || Player.m_localPlayer == null)
                return;

            EnsureUiTexturesLoaded();
            EnsureGuiStyles();
            DrawRankingIcon();

            if (_hudVisible)
            {
                ClampRankingWindowRect();

                float openAnim = Mathf.Clamp01((Time.realtimeSinceStartup - _hudOpenTime) * 4.0f);
                float slideOffset = (1f - openAnim) * 26f;

                Rect animatedRect = _windowRect;
                animatedRect.y += slideOffset;

                Color previousColor = GUI.color;
                GUI.color = new Color(previousColor.r, previousColor.g, previousColor.b, previousColor.a * openAnim);

                Rect returnedRect = GUI.Window(918273, animatedRect, DrawRankingWindow, "", _windowStyle);

                GUI.color = previousColor;


                _windowRect = returnedRect;
                _windowRect.y -= slideOffset;

                ClampRankingWindowRect();

                UpdateRankingInputBlockerRect();

                ConsumeRankingMouseEventIfNeeded();
            }
        }


        private void EnsureRankingInputBlocker()
        {
            if (Application.isBatchMode || _rankingInputBlockerObject != null)
                return;

            _rankingInputBlockerObject = new GameObject("GlitnirRanking_InputBlocker");
            DontDestroyOnLoad(_rankingInputBlockerObject);

            _rankingInputBlockerCanvas = _rankingInputBlockerObject.AddComponent<Canvas>();
            _rankingInputBlockerCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _rankingInputBlockerCanvas.overrideSorting = true;
            _rankingInputBlockerCanvas.sortingOrder = 32760;

            _rankingInputBlockerObject.AddComponent<GraphicRaycaster>();

            GameObject panelObject = new GameObject("Blocker");
            panelObject.transform.SetParent(_rankingInputBlockerObject.transform, false);

            _rankingInputBlockerRect = panelObject.AddComponent<RectTransform>();
            _rankingInputBlockerRect.anchorMin = new Vector2(0f, 0f);
            _rankingInputBlockerRect.anchorMax = new Vector2(0f, 0f);
            _rankingInputBlockerRect.pivot = new Vector2(0f, 0f);

            Image image = panelObject.AddComponent<Image>();
            image.color = new Color(0f, 0f, 0f, 0f);
            image.raycastTarget = true;

            _rankingInputBlockerObject.SetActive(false);
        }

        private void UpdateRankingInputBlockerRect()
        {
            if (Application.isBatchMode || !_hudVisible)
            {
                SetRankingInputBlockerVisible(false);
                return;
            }

            EnsureRankingInputBlocker();

            if (_rankingInputBlockerObject == null || _rankingInputBlockerRect == null)
                return;

            float blockerX = Mathf.Clamp(_windowRect.x, 0f, Screen.width);
            float blockerY = Mathf.Clamp(Screen.height - _windowRect.yMax, 0f, Screen.height);
            float blockerWidth = Mathf.Clamp(_windowRect.width, 0f, Screen.width - blockerX);
            float blockerHeight = Mathf.Clamp(_windowRect.height, 0f, Screen.height - blockerY);

            _rankingInputBlockerRect.anchoredPosition = new Vector2(blockerX, blockerY);
            _rankingInputBlockerRect.sizeDelta = new Vector2(blockerWidth, blockerHeight);

            SetRankingInputBlockerVisible(true);
        }

        private void SetRankingInputBlockerVisible(bool visible)
        {
            if (_rankingInputBlockerObject != null && _rankingInputBlockerObject.activeSelf != visible)
                _rankingInputBlockerObject.SetActive(visible);
        }

        private void DestroyRankingInputBlocker()
        {
            if (_rankingInputBlockerObject == null)
                return;

            try
            {
                Destroy(_rankingInputBlockerObject);
            }
            catch { }

            _rankingInputBlockerObject = null;
            _rankingInputBlockerCanvas = null;
            _rankingInputBlockerRect = null;
        }

        private void ConsumeRankingMouseEventIfNeeded()
        {
            Event currentEvent = Event.current;
            if (currentEvent == null || !ShouldRankingBlockGameInput())
                return;

            EventType type = currentEvent.type;
            if (type == EventType.MouseDown ||
                type == EventType.MouseUp ||
                type == EventType.MouseDrag ||
                type == EventType.ScrollWheel)
            {
                currentEvent.Use();
            }
        }

        public bool IsRankingHudUnderMouse()
        {
            if (Application.isBatchMode)
                return false;

            Vector2 mouse = new Vector2(Input.mousePosition.x, Screen.height - Input.mousePosition.y);

            if (_hudVisible && _windowRect.Contains(mouse))
                return true;

            if (_iconRect.Contains(mouse))
                return true;

            return false;
        }

        public bool ShouldRankingBlockGameInput()
        {
            if (Application.isBatchMode || !_hudVisible)
                return false;

            Vector2 mouse = new Vector2(Input.mousePosition.x, Screen.height - Input.mousePosition.y);
            return _windowRect.Contains(mouse);
        }

        private void ClampRankingWindowRect()
        {
            float minWidth = 500f;
            float minHeight = 760f;

            float maxWidth = Mathf.Max(minWidth, Screen.width - 24f);
            float maxHeight = Mathf.Max(minHeight, Screen.height - 24f);

            _windowRect.width = Mathf.Clamp(_windowRect.width, minWidth, maxWidth);
            _windowRect.height = Mathf.Clamp(_windowRect.height, minHeight, maxHeight);

            _windowRect.x = Mathf.Clamp(_windowRect.x, 4f, Mathf.Max(4f, Screen.width - _windowRect.width - 4f));
            _windowRect.y = Mathf.Clamp(_windowRect.y, 4f, Mathf.Max(4f, Screen.height - _windowRect.height - 4f));
        }


        private void ToggleRankingHud()
        {
            if (_hudVisible)
            {
                CloseRankingHudAndCollapseAll();
                return;
            }

            _hudVisible = true;
            _clientRefreshTimer = ClientRefreshInterval;
            _hudOpenTime = Time.realtimeSinceStartup;
            _hudTabSwitchTime = Time.realtimeSinceStartup;
            RequestSnapshotFromServer();
        }

        private void CloseRankingHudAndCollapseAll()
        {
            _hudVisible = false;
            GUI.FocusControl(null);
            SetRankingInputBlockerVisible(false);
            CollapseAllHudSections();
        }

        private void CollapseAllHudSections()
        {
            _expandedActionPlayers.Clear();
            _expandedRuleCategories.Clear();
            _expandedRuleSubCategories.Clear();
            _expandedGuideSkillRows.Clear();
            _expandedPerformanceCategories.Clear();

            _ruleCategoriesInitialized = false;
            _performanceCategoriesInitialized = false;
        }

        private bool TryParseKeyCode(string value, out KeyCode keyCode)
        {
            keyCode = KeyCode.None;

            if (string.IsNullOrWhiteSpace(value))
                return false;

            value = value.Trim();

            try
            {
                if (Enum.TryParse(value, true, out keyCode))
                    return true;
            }
            catch { }

            return false;
        }

        private void DrawRankingIcon()
        {
            Event e = Event.current;
            if (e != null)
            {
                if (e.type == EventType.MouseDown && e.button == 0 && _iconRect.Contains(e.mousePosition))
                {
                    _iconDragging = true;
                    _iconDragOffset = new Vector2(e.mousePosition.x - _iconRect.x, e.mousePosition.y - _iconRect.y);
                    _iconMouseDownPosition = e.mousePosition;
                    _iconMovedDuringDrag = false;
                    e.Use();
                }
                else if (e.type == EventType.MouseDrag && _iconDragging)
                {
                    Vector2 target = new Vector2(e.mousePosition.x - _iconDragOffset.x, e.mousePosition.y - _iconDragOffset.y);
                    float maxX = Mathf.Max(0f, Screen.width - _iconRect.width);
                    float maxY = Mathf.Max(0f, Screen.height - _iconRect.height);
                    _iconRect.x = Mathf.Clamp(target.x, 0f, maxX);
                    _iconRect.y = Mathf.Clamp(target.y, 0f, maxY);

                    if (!_iconMovedDuringDrag && Vector2.Distance(e.mousePosition, _iconMouseDownPosition) > 6f)
                        _iconMovedDuringDrag = true;

                    e.Use();
                }
                else if (e.type == EventType.MouseUp && e.button == 0 && _iconDragging)
                {
                    bool shouldToggle = !_iconMovedDuringDrag && Vector2.Distance(e.mousePosition, _iconMouseDownPosition) <= 6f;
                    _iconDragging = false;
                    e.Use();

                    if (shouldToggle)
                        ToggleRankingHud();

                    SaveUiSettings();
                }
            }

            Rect iconVisualRect = new Rect(
                _iconRect.x + (_iconRect.width * 0.10f),
                _iconRect.y + (_iconRect.height * 0.10f),
                _iconRect.width * 0.80f,
                _iconRect.height * 0.80f);

            DrawCroppedIcon(iconVisualRect, _uiRankingIconTexture);

            if (_uiWhiteTexture != null)
            {
                Color border = _hudVisible ? new Color(0.98f, 0.82f, 0.34f, 0.42f) : new Color(0.95f, 0.78f, 0.38f, 0.12f);
                GUI.DrawTexture(new Rect(iconVisualRect.x, iconVisualRect.y, iconVisualRect.width, 1f), _uiWhiteTexture, ScaleMode.StretchToFill, true, 0f, border, 0f, 0f);
                GUI.DrawTexture(new Rect(iconVisualRect.x, iconVisualRect.yMax - 1f, iconVisualRect.width, 1f), _uiWhiteTexture, ScaleMode.StretchToFill, true, 0f, border, 0f, 0f);
                GUI.DrawTexture(new Rect(iconVisualRect.x, iconVisualRect.y, 1f, iconVisualRect.height), _uiWhiteTexture, ScaleMode.StretchToFill, true, 0f, border, 0f, 0f);
                GUI.DrawTexture(new Rect(iconVisualRect.xMax - 1f, iconVisualRect.y, 1f, iconVisualRect.height), _uiWhiteTexture, ScaleMode.StretchToFill, true, 0f, border, 0f, 0f);
            }
        }

        private void DrawRankingWindow(int windowId)
        {
            Rect fullRect = new Rect(0f, 0f, _windowRect.width, _windowRect.height);

            if (_uiBackgroundTexture != null)
                GUI.DrawTexture(fullRect, _uiBackgroundTexture, ScaleMode.StretchToFill, true);
            else if (_uiPanelTexture != null)
                GUI.DrawTexture(fullRect, _uiPanelTexture, ScaleMode.StretchToFill, true);

            if (_uiWhiteTexture != null)
                GUI.DrawTexture(fullRect, _uiWhiteTexture, ScaleMode.StretchToFill, true, 0f, new Color(0f, 0f, 0f, 0.03f), 0f, 0f);

            float width = _windowRect.width;
            float height = _windowRect.height;
            float sidePadding = Mathf.Clamp(width * 0.075f, 24f, 38f);
            float contentWidth = width - (sidePadding * 2f);


            float mainTitleHeight = Mathf.Clamp(contentWidth * 0.125f, 58f, 82f);
            float sectionTitleHeight = Mathf.Clamp(contentWidth * 0.105f, 44f, 64f);
            float guideTitleHeight = _uiTitleGuideTexture != null
                ? Mathf.Clamp(contentWidth * 0.26f, 108f, 156f)
                : sectionTitleHeight;

            float actionsTitleHeight = _uiTitleActionsTexture != null
                ? Mathf.Clamp(contentWidth * 0.26f, 108f, 156f)
                : sectionTitleHeight;

            Rect mainTitleRect = new Rect(sidePadding, 16f, contentWidth, mainTitleHeight);
            DrawTextureOrLabel(mainTitleRect, _uiTitleRankingTexture, "RANKING DE GLITNIR", _headerStyle);

            Rect tabsRect = new Rect(sidePadding + 6f, mainTitleRect.yMax + 8f, contentWidth - 12f, 34f);
            DrawHudTabs(tabsRect);

            float footerHeight = 28f;

            if (_hudTabIndex == 1)
            {
                Color tabPreviousColor;
                Matrix4x4 tabPreviousMatrix;
                BeginHudTabTransition(out tabPreviousColor, out tabPreviousMatrix);

                Rect rulesTitleRect = new Rect(sidePadding + 4f, tabsRect.yMax + 8f, contentWidth - 8f, guideTitleHeight);
                DrawTextureOrLabelCropped(rulesTitleRect, _uiTitleGuideTexture, "GUIA DE HONRA", _sectionTitleStyle, 0.22f, 0.82f);

                Rect rulesPanelRect = new Rect(sidePadding + 6f, rulesTitleRect.yMax + 8f, contentWidth - 12f, height - (rulesTitleRect.yMax + 8f) - footerHeight - 18f);
                DrawPanel(rulesPanelRect);

                Rect rulesInnerRect = new Rect(rulesPanelRect.x + 14f, rulesPanelRect.y + 12f, rulesPanelRect.width - 28f, rulesPanelRect.height - 24f);
                float rulesContentHeight = GetRulesContentHeight(rulesInnerRect.width);
                bool rulesNeedsScroll = rulesContentHeight > rulesInnerRect.height + 1f;
                float rulesScrollbarWidth = rulesNeedsScroll ? 18f : 0f;
                Rect rulesViewRect = new Rect(0f, 0f, Mathf.Max(40f, rulesInnerRect.width - rulesScrollbarWidth), Mathf.Max(rulesInnerRect.height, rulesContentHeight));
                _rulesInfoScrollPosition = GUI.BeginScrollView(rulesInnerRect, _rulesInfoScrollPosition, rulesViewRect, false, rulesNeedsScroll);
                DrawRulesContent(new Rect(0f, 0f, rulesViewRect.width, rulesViewRect.height));
                GUI.EndScrollView();

                Rect footerRectRules = new Rect(sidePadding, height - 34f, contentWidth, 20f);
                DrawShadowLabel(footerRectRules, "ESC fecha • Clique nas categorias para expandir as regras", _footerStyle, new Vector2(1f, 1f), new Color(0f, 0f, 0f, 0.92f));

                EndHudTabTransition(tabPreviousColor, tabPreviousMatrix);
                GUI.DragWindow(new Rect(0f, 0f, width, 110f));
                return;
            }

            if (_hudTabIndex == 2)
            {
                Color tabPreviousColor;
                Matrix4x4 tabPreviousMatrix;
                BeginHudTabTransition(out tabPreviousColor, out tabPreviousMatrix);


                Rect actionsTitleRect = new Rect(sidePadding + 4f, tabsRect.yMax + 8f, contentWidth - 8f, actionsTitleHeight);
                DrawTextureOrLabelCropped(actionsTitleRect, _uiTitleActionsTexture, "ORÁCULO DO RANKING", _sectionTitleStyle, 0.22f, 0.82f);

                Rect actionsPanelRect = new Rect(sidePadding + 6f, actionsTitleRect.yMax + 8f, contentWidth - 12f, height - (actionsTitleRect.yMax + 8f) - footerHeight - 18f);
                DrawPanel(actionsPanelRect);

                Rect actionsInnerRect = new Rect(actionsPanelRect.x + 14f, actionsPanelRect.y + 12f, actionsPanelRect.width - 28f, actionsPanelRect.height - 24f);
                float actionsContentHeight = GetActionsContentHeight(actionsInnerRect.width);
                bool actionsNeedsScroll = actionsContentHeight > actionsInnerRect.height + 1f;
                float actionsScrollbarWidth = actionsNeedsScroll ? 18f : 0f;
                Rect actionsViewRect = new Rect(0f, 0f, Mathf.Max(40f, actionsInnerRect.width - actionsScrollbarWidth), Mathf.Max(actionsInnerRect.height, actionsContentHeight));
                _actionsScrollPosition = GUI.BeginScrollView(actionsInnerRect, _actionsScrollPosition, actionsViewRect, false, actionsNeedsScroll);
                DrawActionsContent(new Rect(0f, 0f, actionsViewRect.width, actionsViewRect.height));
                GUI.EndScrollView();

                Rect footerRectActions = new Rect(sidePadding, height - 34f, contentWidth, 20f);
                DrawShadowLabel(footerRectActions, "ESC fecha • Clique em cada jogador para abrir/fechar as ações", _footerStyle, new Vector2(1f, 1f), new Color(0f, 0f, 0f, 0.92f));

                EndHudTabTransition(tabPreviousColor, tabPreviousMatrix);
                GUI.DragWindow(new Rect(0f, 0f, width, 110f));
                return;
            }

            Color rankingTabPreviousColor;
            Matrix4x4 rankingTabPreviousMatrix;
            BeginHudTabTransition(out rankingTabPreviousColor, out rankingTabPreviousMatrix);

            float topRankingTitleHeight = _uiTitleTopTexture != null
                ? Mathf.Clamp(contentWidth * 0.18f, 76f, 118f)
                : sectionTitleHeight;
            Rect topTitleRect = new Rect(sidePadding, tabsRect.yMax + 2f, contentWidth, topRankingTitleHeight);
            DrawTextureOrLabel(topTitleRect, _uiTitleTopTexture, "TOP GLITNIR", _sectionTitleStyle);

            float bottomReserved = sectionTitleHeight + footerHeight + 54f;
            float availableBodyHeight = height - (topTitleRect.yMax + 6f) - bottomReserved;
            float rankingPanelHeight = Mathf.Clamp(availableBodyHeight * 0.48f, 150f, 230f);
            Rect rankingPanelRect = new Rect(sidePadding + 6f, topTitleRect.yMax + 4f, contentWidth - 12f, rankingPanelHeight);
            DrawPanel(rankingPanelRect);

            Rect rankingInnerRect = new Rect(rankingPanelRect.x + 14f, rankingPanelRect.y + 12f, rankingPanelRect.width - 28f, rankingPanelRect.height - 24f);
            float rankingContentHeight = GetTopEntriesContentHeight(rankingInnerRect.width);
            bool rankingNeedsScroll = rankingContentHeight > rankingInnerRect.height + 1f;
            float rankingScrollbarWidth = rankingNeedsScroll ? 18f : 0f;
            Rect rankingViewRect = new Rect(0f, 0f, Mathf.Max(40f, rankingInnerRect.width - rankingScrollbarWidth), Mathf.Max(rankingInnerRect.height, rankingContentHeight));
            _rankingScrollPosition = GUI.BeginScrollView(rankingInnerRect, _rankingScrollPosition, rankingViewRect, false, rankingNeedsScroll);
            DrawTopEntriesContent(new Rect(0f, 0f, rankingViewRect.width, rankingViewRect.height));
            GUI.EndScrollView();

            float playerTitleHeight = _uiTitlePlayerTexture != null
                ? Mathf.Clamp(contentWidth * 0.12f, 54f, 78f)
                : sectionTitleHeight;
            Rect playerTitleRect = new Rect(sidePadding, rankingPanelRect.yMax + 8f, contentWidth, playerTitleHeight);
            DrawTextureOrLabel(playerTitleRect, _uiTitlePlayerTexture, "SEU DESEMPENHO", _sectionTitleStyle);

            float playerPanelHeight = height - (playerTitleRect.yMax + 4f) - footerHeight - 14f;
            playerPanelHeight = Mathf.Max(250f, playerPanelHeight);
            Rect playerPanelRect = new Rect(sidePadding + 6f, playerTitleRect.yMax + 4f, contentWidth - 12f, playerPanelHeight);
            DrawPanel(playerPanelRect);

            Rect playerInnerRect = new Rect(playerPanelRect.x + 14f, playerPanelRect.y + 12f, playerPanelRect.width - 28f, playerPanelRect.height - 24f);
            float playerContentHeight = GetPlayerContentHeight(playerInnerRect.width);
            bool playerNeedsScroll = playerContentHeight > playerInnerRect.height + 1f;
            float playerScrollbarWidth = playerNeedsScroll ? 18f : 0f;
            Rect playerViewRect = new Rect(0f, 0f, Mathf.Max(40f, playerInnerRect.width - playerScrollbarWidth), Mathf.Max(playerInnerRect.height, playerContentHeight));
            _playerInfoScrollPosition = GUI.BeginScrollView(playerInnerRect, _playerInfoScrollPosition, playerViewRect, false, playerNeedsScroll);
            DrawPlayerContent(new Rect(0f, 0f, playerViewRect.width, playerViewRect.height));
            GUI.EndScrollView();

            Rect footerRect = new Rect(sidePadding, height - 34f, contentWidth, 20f);
            DrawShadowLabel(footerRect, "ESC fecha • Clique nos cards do desempenho para expandir", _footerStyle, new Vector2(1f, 1f), new Color(0f, 0f, 0f, 0.92f));

            EndHudTabTransition(rankingTabPreviousColor, rankingTabPreviousMatrix);
            GUI.DragWindow(new Rect(0f, 0f, width, 110f));
        }

        private void BeginHudTabTransition(out Color previousColor, out Matrix4x4 previousMatrix)
        {
            previousColor = GUI.color;
            previousMatrix = GUI.matrix;

            float t = Mathf.Clamp01((Time.realtimeSinceStartup - _hudTabSwitchTime) * 5.5f);
            float alpha = Mathf.SmoothStep(0f, 1f, t);
            float slideX = (1f - alpha) * 18f;

            GUI.color = new Color(previousColor.r, previousColor.g, previousColor.b, previousColor.a * alpha);
            GUI.matrix = Matrix4x4.TRS(new Vector3(slideX, 0f, 0f), Quaternion.identity, Vector3.one) * previousMatrix;
        }

        private void EndHudTabTransition(Color previousColor, Matrix4x4 previousMatrix)
        {
            GUI.matrix = previousMatrix;
            GUI.color = previousColor;
        }

        private void DrawHudTabs(Rect rect)
        {
            float gap = 8f;
            float tabWidth = (rect.width - (gap * 2f)) / 3f;
            DrawHudTabButton(new Rect(rect.x, rect.y, tabWidth, rect.height), "Ranking", 0);
            DrawHudTabButton(new Rect(rect.x + tabWidth + gap, rect.y, tabWidth, rect.height), "Guia", 1);
            DrawHudTabButton(new Rect(rect.x + (tabWidth + gap) * 2f, rect.y, tabWidth, rect.height), "Ações", 2);
        }

        private void DrawHudTabButton(Rect rect, string label, int index)
        {
            bool active = _hudTabIndex == index;
            Color previousBackgroundColor = GUI.backgroundColor;
            GUI.backgroundColor = active ? new Color(0.91f, 0.75f, 0.34f, 0.95f) : new Color(0.34f, 0.26f, 0.13f, 0.90f);

            if (GUI.Button(rect, label))
            {
                if (_hudTabIndex != index)
                {
                    _hudTabIndex = index;
                    _hudTabSwitchTime = Time.realtimeSinceStartup;
                }

                GUI.FocusControl(null);
            }

            GUI.backgroundColor = previousBackgroundColor;

            if (_uiWhiteTexture != null && active)
                GUI.DrawTexture(new Rect(rect.x, rect.yMax - 2f, rect.width, 2f), _uiWhiteTexture, ScaleMode.StretchToFill, true, 0f, new Color(1f, 0.86f, 0.34f, 0.95f), 0f, 0f);
        }

        private float GetTopEntriesContentHeight(float width)
        {
            int count = _cachedTopEntries != null ? _cachedTopEntries.Count : 0;
            if (count <= 0)
                return 120f;

            return 12f + (count * 50f) + ((count - 1) * 6f) + 14f;
        }

        private void DrawTopEntriesContent(Rect rect)
        {
            if (_cachedTopEntries == null || _cachedTopEntries.Count == 0)
            {
                DrawShadowLabel(new Rect(12f, 10f, rect.width - 24f, 72f), "Nenhum herói pontuado ainda. Saia para caçar, evoluir skills e dominar Glitnir.", _emptyStateStyle);
                return;
            }

            float y = 6f;
            for (int i = 0; i < _cachedTopEntries.Count; i++)
            {
                SnapshotTopEntryData entry = _cachedTopEntries[i];
                Rect rowRect = new Rect(4f, y, rect.width - 8f, 50f);
                DrawRankRow(rowRect, entry);
                y += rowRect.height + 6f;
            }
        }

        private void DrawRankRow(Rect rect, SnapshotTopEntryData entry)
        {
            if (entry == null || _uiWhiteTexture == null)
                return;

            Color accent = GetRankAccentColor(entry.Position);
            bool highlightPodium = entry.Position <= 3;


            float podiumPulse = highlightPodium
                ? 0.55f + (Mathf.Sin(Time.time * 3.2f + entry.Position) + 1f) * 0.225f
                : 0f;

            float bgAlpha = highlightPodium ? 0.13f + (podiumPulse * 0.06f) : 0.06f;

            GUI.DrawTexture(
                new Rect(rect.x, rect.y + 2f, rect.width, rect.height - 4f),
                _uiWhiteTexture,
                ScaleMode.StretchToFill,
                true,
                0f,
                new Color(0.03f, 0.02f, 0.01f, bgAlpha),
                0f,
                0f
            );

            if (highlightPodium)
            {

                GUI.DrawTexture(
                    new Rect(rect.x + 4f, rect.y + 3f, rect.width - 8f, rect.height - 6f),
                    _uiWhiteTexture,
                    ScaleMode.StretchToFill,
                    true,
                    0f,
                    new Color(accent.r, accent.g, accent.b, 0.05f + podiumPulse * 0.10f),
                    0f,
                    0f
                );
            }

            GUI.DrawTexture(
                new Rect(rect.x, rect.y, 4f, rect.height),
                _uiWhiteTexture,
                ScaleMode.StretchToFill,
                true,
                0f,
                new Color(accent.r, accent.g, accent.b, highlightPodium ? 0.75f + podiumPulse * 0.20f : accent.a),
                0f,
                0f
            );

            if (highlightPodium)
            {
                GUI.DrawTexture(
                    new Rect(rect.x + 4f, rect.y + 1f, rect.width - 8f, 1.5f),
                    _uiWhiteTexture,
                    ScaleMode.StretchToFill,
                    true,
                    0f,
                    new Color(accent.r, accent.g, accent.b, 0.35f + podiumPulse * 0.35f),
                    0f,
                    0f
                );

                GUI.DrawTexture(
                    new Rect(rect.x + 4f, rect.yMax - 2.5f, rect.width - 8f, 1.5f),
                    _uiWhiteTexture,
                    ScaleMode.StretchToFill,
                    true,
                    0f,
                    new Color(accent.r, accent.g, accent.b, 0.18f + podiumPulse * 0.22f),
                    0f,
                    0f
                );
            }

            Texture2D podiumIcon = GetRankPodiumIcon(entry.Position);
            float textStartX = rect.x + 58f;

            if (podiumIcon != null)
            {

                float iconSize = entry.Position == 1 ? 42f : entry.Position == 2 ? 40f : 38f;
                Rect iconRect = new Rect(rect.x + 8f, rect.y + 5f, iconSize, iconSize);

                DrawTextureAspectFit(iconRect, podiumIcon);
                textStartX = rect.x + 58f;
            }
            else
            {
                Rect numberRect = new Rect(rect.x + 10f, rect.y + 8f, 48f, 20f);
                DrawShadowLabel(numberRect, "#" + Mathf.Max(1, entry.Position), _rankIndexStyle);
            }

            float rightPad = 10f;
            if (entry.Position <= 3)
                rightPad += 18f;

            Rect nameRect = new Rect(textStartX, rect.y + 6f, rect.width - (textStartX - rect.x) - 74f - rightPad, 22f);
            DrawShadowLabel(nameRect, string.IsNullOrWhiteSpace(entry.PlayerName) ? "Jogador" : entry.PlayerName.ToUpperInvariant(), _rankNameStyle);

            Rect pointsRect = new Rect(textStartX, rect.y + 26f, 140f, 16f);
            DrawShadowLabel(pointsRect, ColorizePoints(FormatPoints(entry.Points)), _rankPointsStyle);
        }

        private Texture2D GetRankPodiumIcon(int position)
        {
            switch (position)
            {
                case 1: return _uiRank1IconTexture;
                case 2: return _uiRank2IconTexture;
                case 3: return _uiRank3IconTexture;
                default: return null;
            }
        }

        private string GetRankBadgeLabel(int position)
        {
            switch (position)
            {
                case 1: return "OURO";
                case 2: return "PRATA";
                case 3: return "BRONZE";
                default: return "";
            }
        }

        private const float ActionPlayerCollapsedHeight = 58f;
        private const float ActionPlayerExpandedHeight = 650f;

        private float GetActionsContentHeight(float width)
        {
            int count = _cachedTopEntries != null ? _cachedTopEntries.Count : 0;
            if (count <= 0)
                return 130f;

            float total = 12f;
            for (int i = 0; i < count; i++)
            {
                SnapshotTopEntryData entry = _cachedTopEntries[i];
                total += IsActionPlayerExpanded(entry) ? ActionPlayerExpandedHeight : ActionPlayerCollapsedHeight;
                total += 8f;
            }

            return total + 14f;
        }

        private void DrawActionsContent(Rect rect)
        {
            if (_cachedTopEntries == null || _cachedTopEntries.Count == 0)
            {
                DrawShadowLabel(new Rect(12f, 10f, rect.width - 24f, 72f), "Nenhum jogador pontuado ainda. Quando o ranking receber pontos, as ações aparecerão aqui.", _emptyStateStyle);
                return;
            }

            float y = 8f;
            for (int i = 0; i < _cachedTopEntries.Count; i++)
            {
                SnapshotTopEntryData entry = _cachedTopEntries[i];
                float rowHeight = IsActionPlayerExpanded(entry) ? ActionPlayerExpandedHeight : ActionPlayerCollapsedHeight;
                Rect rowRect = new Rect(4f, y, rect.width - 8f, rowHeight);
                DrawActionPlayerRow(rowRect, entry);
                y += rowRect.height + 8f;
            }
        }

        private string BuildActionPlayerKey(SnapshotTopEntryData entry)
        {
            if (entry == null)
                return "";

            return Mathf.Max(1, entry.Position) + ":" + SafeKey(entry.PlayerName);
        }

        private bool IsActionPlayerExpanded(SnapshotTopEntryData entry)
        {
            string key = BuildActionPlayerKey(entry);
            return !string.IsNullOrWhiteSpace(key) && _expandedActionPlayers.Contains(key);
        }

        private void ToggleActionPlayerExpanded(SnapshotTopEntryData entry)
        {
            string key = BuildActionPlayerKey(entry);
            if (string.IsNullOrWhiteSpace(key))
                return;

            if (_expandedActionPlayers.Contains(key))
                _expandedActionPlayers.Remove(key);
            else
                _expandedActionPlayers.Add(key);
        }


        private string GetNextExplorationGoalText(float currentPercent)
        {
            SnapshotPlayerData data = _cachedPlayerData;
            if (data == null || string.IsNullOrWhiteSpace(data.HudExplorationMapJackpotRules))
                return "Sem marco configurado";

            try
            {
                foreach (string raw in data.HudExplorationMapJackpotRules.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    string[] parts = raw.Split(new[] { ':' }, 2, StringSplitOptions.None);
                    if (parts.Length != 2)
                        continue;

                    string pctText = parts[0].Replace("Mapa", "").Replace("%", "").Trim();
                    int pct;
                    int pts;
                    if (!int.TryParse(pctText, out pct) || !int.TryParse(parts[1].Trim(), out pts))
                        continue;

                    if (currentPercent < pct)
                        return pct + "% (+" + pts + " pts)";
                }
            }
            catch { }

            return "Completo";
        }

        private void DrawExplorationActionCard(Rect rect, Color accent, SnapshotTopEntryData entry)
        {
            float percent = 0f;
            string nextGoal = "Disponível no seu perfil";

            if (entry != null && entry.IsLocalPlayer)
            {
                percent = GetExplorationMapProgressPercent();
                nextGoal = GetNextExplorationGoalText(percent);
            }

            if (_uiWhiteTexture != null)
            {
                GUI.DrawTexture(rect, _uiWhiteTexture, ScaleMode.StretchToFill, true, 0f, new Color(0f, 0f, 0f, 0.26f), 0f, 0f);
                GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width, 1f), _uiWhiteTexture, ScaleMode.StretchToFill, true, 0f, new Color(accent.r, accent.g, accent.b, 0.36f), 0f, 0f);
                GUI.DrawTexture(new Rect(rect.x, rect.y + 26f, rect.width, 1f), _uiWhiteTexture, ScaleMode.StretchToFill, true, 0f, new Color(accent.r, accent.g, accent.b, 0.18f), 0f, 0f);
            }

            DrawShadowLabel(new Rect(rect.x + 10f, rect.y + 5f, rect.width - 20f, 20f), "EXPLORAÇÃO", _cardLabelStyle);

            string percentText = entry != null && entry.IsLocalPlayer
                ? percent.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + "%"
                : "—";

            DrawActionDetailLine(new Rect(rect.x + 12f, rect.y + 32f, rect.width - 24f, 20f), "Mapa revelado", percentText, 0);
            DrawShadowLabel(new Rect(rect.x + 12f, rect.y + 55f, rect.width - 24f, 20f), ColorizeMuted("Próximo marco: ") + ColorizeHighlight(nextGoal), _bodyRowStyle);
        }

        private void DrawActionPlayerRow(Rect rect, SnapshotTopEntryData entry)
        {
            if (entry == null || _uiWhiteTexture == null)
                return;

            bool expanded = IsActionPlayerExpanded(entry);
            Color accent = GetRankAccentColor(entry.Position);
            Color panelColor = expanded ? new Color(0.035f, 0.022f, 0.010f, 0.50f) : new Color(0.025f, 0.018f, 0.010f, 0.34f);

            GUI.DrawTexture(rect, _uiWhiteTexture, ScaleMode.StretchToFill, true, 0f, panelColor, 0f, 0f);
            GUI.DrawTexture(new Rect(rect.x, rect.y, 5f, rect.height), _uiWhiteTexture, ScaleMode.StretchToFill, true, 0f, accent, 0f, 0f);
            GUI.DrawTexture(new Rect(rect.x + 5f, rect.y, rect.width - 10f, 1.5f), _uiWhiteTexture, ScaleMode.StretchToFill, true, 0f, new Color(accent.r, accent.g, accent.b, expanded ? 0.72f : 0.36f), 0f, 0f);
            GUI.DrawTexture(new Rect(rect.x + 5f, rect.yMax - 1.5f, rect.width - 10f, 1.5f), _uiWhiteTexture, ScaleMode.StretchToFill, true, 0f, new Color(accent.r, accent.g, accent.b, expanded ? 0.46f : 0.20f), 0f, 0f);

            Rect headerRect = new Rect(rect.x + 12f, rect.y + 8f, rect.width - 24f, 42f);
            if (GUI.Button(headerRect, GUIContent.none, GUIStyle.none))
                ToggleActionPlayerExpanded(entry);

            string arrow = expanded ? "▼" : "▶";
            string playerName = string.IsNullOrWhiteSpace(entry.PlayerName) ? "JOGADOR" : entry.PlayerName.ToUpperInvariant();
            DrawShadowLabel(new Rect(headerRect.x + 2f, headerRect.y + 8f, 26f, 22f), arrow, _bodyValueStyle);
            DrawShadowLabel(new Rect(headerRect.x + 32f, headerRect.y + 3f, headerRect.width - 184f, 26f), "#" + Mathf.Max(1, entry.Position) + "  " + playerName, _panelHeadingStyle);
            DrawShadowLabel(new Rect(headerRect.xMax - 142f, headerRect.y + 5f, 132f, 22f), ColorizePoints(FormatPoints(entry.Points)), _bodyValueStyle);

            if (!expanded)
            {
                string summary = ColorizeDanger(entry.TotalKillsPontuadas + " kills") + "  •  " +
                                 ColorizeDanger(entry.TotalBossesPontuadas + " bosses") + "  •  " +
                                 ColorizeProgress(entry.TotalSkillLevelUpsPontuados + " skills") + "  •  " +
                                 ColorizeProgress(entry.TotalCraftPontuadas + " crafts") + "  •  " +
                                 ColorizeDanger(entry.TotalPointsExchanges + " câmbios");
                DrawShadowLabel(new Rect(headerRect.x + 32f, headerRect.y + 27f, headerRect.width - 48f, 16f), summary, _mutedBodyStyle);
                return;
            }

            float contentX = rect.x + 20f;
            float contentY = rect.y + 58f;
            float contentW = rect.width - 40f;
            float sectionH = 78f;
            float sectionGap = 10f;


            DrawActionSectionCard(new Rect(contentX, contentY, contentW, sectionH), "COMBATE", accent,
                "Kills", entry.TotalKillsPontuadas + "", entry.KillPointsTotal,
                "Bosses", entry.TotalBossesPontuadas + "", entry.BossPointsTotal);
            contentY += sectionH + sectionGap;

            DrawActionSectionCard(new Rect(contentX, contentY, contentW, sectionH), "PROGRESSÃO", accent,
                "Skills", entry.TotalSkillLevelUpsPontuados + " marcos", entry.SkillPointsTotal,
                "Pesca", entry.TotalFishingPontuadas + " peixes", entry.FishingPointsTotal);
            contentY += sectionH + sectionGap;

            DrawExplorationActionCard(new Rect(contentX, contentY, contentW, sectionH), accent, entry);
            contentY += sectionH + sectionGap;

            DrawActionSectionCard(new Rect(contentX, contentY, contentW, sectionH), "ECONOMIA", accent,
                "Crafts", entry.TotalCraftPontuadas + "", entry.CraftPointsTotal,
                "Cultivo", entry.TotalFarmJackpotsPontuados + " jackpots", entry.FarmJackpotPointsTotal);
            contentY += sectionH + sectionGap;

            DrawActionSectionCard(new Rect(contentX, contentY, contentW, sectionH), "FEITOS", accent,
                "Únicos", entry.TotalUniqueCraftJackpotsPontuados + "", entry.UniqueCraftJackpotPointsTotal,
                "Mortes", entry.TotalDeaths + "", -entry.DeathPenaltyPointsTotal);
            contentY += sectionH + sectionGap;

            DrawActionSectionCard(new Rect(contentX, contentY, contentW, sectionH), "CÂMBIO", accent,
                "Trocas", entry.TotalPointsExchanges + " realizadas", -entry.PointsExchangePenaltyTotal,
                "Moedas", entry.PointsExchangeCoinsTotal + " Coins", 0);

            string last = string.IsNullOrWhiteSpace(entry.LastReason) ? "Sem último registro" : entry.LastReason;
            Rect lastRect = new Rect(contentX, rect.yMax - 32f, contentW, 22f);
            if (_uiWhiteTexture != null)
                GUI.DrawTexture(lastRect, _uiWhiteTexture, ScaleMode.StretchToFill, true, 0f, new Color(0f, 0f, 0f, 0.24f), 0f, 0f);
            GUI.DrawTexture(new Rect(lastRect.x, lastRect.y, 3f, lastRect.height), _uiWhiteTexture, ScaleMode.StretchToFill, true, 0f, new Color(accent.r, accent.g, accent.b, 0.70f), 0f, 0f);
            DrawShadowLabel(new Rect(lastRect.x + 10f, lastRect.y + 3f, lastRect.width - 20f, 18f), ColorizeMuted("Último registro: ") + ColorizeHighlight(last), _configStyle);
        }

        private void DrawActionSectionCard(Rect rect, string title, Color accent, string labelA, string amountA, int pointsA, string labelB, string amountB, int pointsB)
        {
            if (_uiWhiteTexture != null)
            {
                GUI.DrawTexture(rect, _uiWhiteTexture, ScaleMode.StretchToFill, true, 0f, new Color(0f, 0f, 0f, 0.26f), 0f, 0f);
                GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width, 1f), _uiWhiteTexture, ScaleMode.StretchToFill, true, 0f, new Color(accent.r, accent.g, accent.b, 0.36f), 0f, 0f);
                GUI.DrawTexture(new Rect(rect.x, rect.y + 26f, rect.width, 1f), _uiWhiteTexture, ScaleMode.StretchToFill, true, 0f, new Color(accent.r, accent.g, accent.b, 0.18f), 0f, 0f);
            }

            DrawShadowLabel(new Rect(rect.x + 10f, rect.y + 5f, rect.width - 20f, 20f), title, _cardLabelStyle);
            DrawActionDetailLine(new Rect(rect.x + 12f, rect.y + 32f, rect.width - 24f, 20f), labelA, amountA, pointsA);
            DrawActionDetailLine(new Rect(rect.x + 12f, rect.y + 55f, rect.width - 24f, 20f), labelB, amountB, pointsB);
        }

        private void DrawActionDetailLine(Rect rect, string label, string amount, int points)
        {


            GUIStyle leftStyle = new GUIStyle(_bodyRowStyle)
            {
                clipping = TextClipping.Clip,
                wordWrap = false,
                alignment = TextAnchor.MiddleLeft
            };

            GUIStyle pointsStyle = new GUIStyle(_bodyValueStyle)
            {
                clipping = TextClipping.Clip,
                wordWrap = false,
                alignment = TextAnchor.MiddleRight
            };

            float pointsW = Mathf.Clamp(rect.width * 0.42f, 145f, 190f);
            float leftW = Mathf.Max(90f, rect.width - pointsW - 12f);

            Rect leftRect = new Rect(rect.x, rect.y, leftW, rect.height);
            Rect pointsRect = new Rect(rect.xMax - pointsW, rect.y, pointsW, rect.height);

            string amountText = string.IsNullOrWhiteSpace(amount) ? "0" : amount.Trim();
            string leftText = $"<color=#FFFFFF>{label}</color>  {ColorizeActionAmount(label, amountText)}";

            DrawShadowLabel(leftRect, leftText, leftStyle);
            DrawShadowLabel(pointsRect, FormatSignedPointsColored(points), pointsStyle);
        }

        private void DrawActionMetricBox(Rect rect, string label, string amount, int points)
        {
            if (_uiWhiteTexture != null)
                GUI.DrawTexture(rect, _uiWhiteTexture, ScaleMode.StretchToFill, true, 0f, new Color(0f, 0f, 0f, 0.20f), 0f, 0f);

            GUIStyle labelStyle = new GUIStyle(_mutedBodyStyle)
            {
                clipping = TextClipping.Clip,
                wordWrap = false,
                alignment = TextAnchor.MiddleLeft
            };

            GUIStyle amountStyle = new GUIStyle(_bodyRowStyle)
            {
                clipping = TextClipping.Clip,
                wordWrap = false,
                alignment = TextAnchor.MiddleLeft
            };

            GUIStyle pointsStyle = new GUIStyle(_bodyValueStyle)
            {
                clipping = TextClipping.Clip,
                wordWrap = false,
                alignment = TextAnchor.MiddleRight
            };

            float innerX = rect.x + 7f;
            float innerY = rect.y + 2f;
            float innerW = rect.width - 14f;
            float innerH = rect.height - 4f;

            float labelW = innerW * 0.34f;
            float amountW = innerW * 0.25f;
            float pointsW = innerW - labelW - amountW - 10f;

            Rect labelRect = new Rect(innerX, innerY, labelW, innerH);
            Rect amountRect = new Rect(labelRect.xMax + 4f, innerY, amountW, innerH);
            Rect pointsRect = new Rect(amountRect.xMax + 6f, innerY, pointsW, innerH);

            DrawShadowLabel(labelRect, label, labelStyle);
            DrawShadowLabel(amountRect, ColorizeActionAmount(label, amount), amountStyle);
            DrawShadowLabel(pointsRect, FormatSignedPointsColored(points), pointsStyle);
        }

        private string FormatPoints(int points)
        {
            return points.ToString("N0", System.Globalization.CultureInfo.InvariantCulture).Replace(",", ".") + " pts";
        }

        private string FormatSignedPoints(int points)
        {
            if (points > 0)
                return "+" + FormatPoints(points);
            if (points < 0)
                return "-" + FormatPoints(Mathf.Abs(points));
            return "0 pts";
        }

        private const string HudColorPoints = "#00FF7F";
        private const string HudColorDanger = "#FF5555";
        private const string HudColorProgress = "#4FC3F7";
        private const string HudColorHighlight = "#FFD700";
        private const string HudColorMuted = "#D7DCE5";
        private const string HudColorName = "#FFFFFF";
        private const string HudColorSeparator = "#8F6E38";

        private string ColorizeHud(string text, string color)
        {
            return "<color=" + color + ">" + (text ?? "") + "</color>";
        }

        private string ColorizePoints(string text)
        {
            return ColorizeHud(text, HudColorPoints);
        }

        private string ColorizeDanger(string text)
        {
            return ColorizeHud(text, HudColorDanger);
        }

        private string ColorizeProgress(string text)
        {
            return ColorizeHud(text, HudColorProgress);
        }

        private string ColorizeHighlight(string text)
        {
            return ColorizeHud(text, HudColorHighlight);
        }

        private string ColorizeMuted(string text)
        {
            return ColorizeHud(text, HudColorMuted);
        }

        private string FormatSignedPointsColored(int points)
        {
            if (points > 0)
                return ColorizePoints("+" + FormatPoints(points));
            if (points < 0)
                return ColorizeDanger("-" + FormatPoints(Mathf.Abs(points)));
            return ColorizeMuted("0 pts");
        }

        private string ColorizeActionAmount(string label, string amount)
        {
            string key = (label ?? "").ToLowerInvariant();

            if (key.Contains("morte") || key.Contains("câmbio") || key.Contains("cambio") || key.Contains("kill") || key.Contains("boss") || key.Contains("combate"))
                return ColorizeDanger(amount);

            if (key.Contains("skill") || key.Contains("pesca") || key.Contains("craft") || key.Contains("cultivo") || key.Contains("únic") || key.Contains("unico"))
                return ColorizeProgress(amount);

            return ColorizeHighlight(amount);
        }

        private string ColorizePerformanceValue(string label, string value)
        {
            string key = (label ?? "").ToLowerInvariant();

            if (key.Contains("pontos"))
            {
                int parsed;
                if (int.TryParse(value, out parsed))
                    return FormatSignedPointsColored(parsed);

                return ColorizePoints(value);
            }

            if (key.Contains("kill") || key.Contains("boss") || key.Contains("morte") || key.Contains("câmbio") || key.Contains("cambio") || key.Contains("penalidade"))
                return ColorizeDanger(value);

            if (key.Contains("skill") || key.Contains("craft") || key.Contains("pesca") || key.Contains("cultivo") || key.Contains("níve") || key.Contains("nive"))
                return ColorizeProgress(value);

            if (key.Contains("posição") || key.Contains("origem") || key.Contains("item") || key.Contains("recompensa"))
                return ColorizeHighlight(value);

            return ColorizeMuted(value);
        }

        private float GetPlayerContentHeight(float width)
        {
            if (_cachedPlayerData == null || !_cachedPlayerData.HasData)
                return 230f;

            float height = 12f;
            height += 58f + 22f;

            height += EstimatePerformanceBlockHeight("Resumo", 108f);
            height += EstimatePerformanceBlockHeight("Origem", 304f);
            height += EstimatePerformanceBlockHeight("Último registro", 58f);
            height += EstimatePerformanceBlockHeight("Recompensa", 250f);
            height += EstimatePerformanceBlockHeight("Configuração", 44f);

            return Mathf.Max(230f, height + 18f);
        }

        private float EstimatePerformanceBlockHeight(string categoryKey, float expandedContentHeight)
        {
            if (!IsPerformanceCategoryExpanded(categoryKey))
                return 42f;

            return 42f + 30f + expandedContentHeight + 14f;
        }

        private void DrawPlayerContent(Rect rect)
        {
            float x = 8f;
            float width = rect.width - 16f;
            float y = 12f;

            if (_cachedPlayerData == null || !_cachedPlayerData.HasData)
            {
                DrawPerformanceEmptyState(new Rect(x, y, width, 150f));
                y += 166f;
                DrawPerformanceBlock(new Rect(x, y, width, 10f), "Configuração", "Configuração", "Resumo rápido das regras ativas no servidor.", 44f, contentRect =>
                {
                    DrawConfigSection(contentRect, _cachedPlayerData);
                });
                return;
            }

            DrawPlayerSummaryBlock(new Rect(x, y, width, 58f));
            y += 80f;

            y = DrawPerformanceBlock(new Rect(x, y, width, 10f), "Resumo da jornada", "Resumo", "Sua atividade principal registrada no ranking.", 108f, contentRect =>
            {
                DrawSimpleInfoRows(contentRect, new string[]
                {
                    "Kills pontuadas|" + _cachedPlayerData.TotalKillsPontuadas,
                    "Bosses pontuados|" + _cachedPlayerData.TotalBossesPontuadas,
                    "Níveis de skill|" + _cachedPlayerData.TotalSkillLevelUpsPontuados,
                    "Câmbios realizados|" + _cachedPlayerData.TotalPointsExchanges
                });
            });

            y = DrawPerformanceBlock(new Rect(x, y, width, 10f), "Origem dos pontos", "Origem", "Veja quais caminhos estão sustentando sua posição.", 304f, contentRect =>
            {
                DrawSimpleInfoRows(contentRect, new string[]
                {
                    "Pontos por kills|" + _cachedPlayerData.KillPointsTotal,
                    "Pontos por bosses|" + _cachedPlayerData.BossPointsTotal,
                    "Pontos por skills|" + _cachedPlayerData.SkillPointsTotal,
                    "Pontos por pesca|" + _cachedPlayerData.FishingPointsTotal,
                    "Pontos por craft|" + _cachedPlayerData.CraftPointsTotal,
                    "Pontos por cultivo|" + _cachedPlayerData.FarmJackpotPointsTotal,
                    "Pontos por itens únicos|" + _cachedPlayerData.UniqueCraftJackpotPointsTotal,
                    "Pontos por exploração|" + _cachedPlayerData.ExplorationMapJackpotPointsTotal,
                    "Penalidade por mortes|-" + _cachedPlayerData.DeathPenaltyPointsTotal,
                    "Penalidade por câmbio|-" + _cachedPlayerData.PointsExchangePenaltyTotal,
                    "Coins recebidas no câmbio|" + _cachedPlayerData.PointsExchangeCoinsTotal
                });
            });

            y = DrawPerformanceBlock(new Rect(x, y, width, 10f), "Último registro", "Último registro", "Última ação registrada pelo servidor para sua jornada.", 58f, contentRect =>
            {
                DrawSimpleInfoRows(contentRect, new string[]
                {
                    "Origem|" + (string.IsNullOrWhiteSpace(_cachedPlayerData.LastReason) ? "sem registro" : _cachedPlayerData.LastReason),
                    "Atualização|" + FormatHudLastUpdate(_cachedPlayerData.LastUpdateUtc)
                });
            });

            y = DrawPerformanceBlock(new Rect(x, y, width, 10f), "Recompensa do ranking", "Recompensa", "Prêmios disponíveis para os melhores colocados da temporada e câmbio de pontos por moedas.", 300f, contentRect =>
            {
                DrawRewardClaimSection(contentRect);
            });

            y = DrawPerformanceBlock(new Rect(x, y, width, 10f), "Configuração atual", "Configuração", "Estado das regras principais que afetam sua pontuação.", 44f, contentRect =>
            {
                DrawConfigSection(contentRect, _cachedPlayerData);
            });
        }

        private void DrawPerformanceEmptyState(Rect rect)
        {
            if (_uiWhiteTexture != null)
            {
                GUI.DrawTexture(new Rect(rect.x, rect.y + 2f, rect.width, rect.height - 4f), _uiWhiteTexture, ScaleMode.StretchToFill, true, 0f, new Color(0.03f, 0.02f, 0.01f, 0.06f), 0f, 0f);
                GUI.DrawTexture(new Rect(rect.x, rect.y, 4f, rect.height), _uiWhiteTexture, ScaleMode.StretchToFill, true, 0f, new Color(0.91f, 0.75f, 0.34f, 0.82f), 0f, 0f);
            }

            DrawShadowLabel(new Rect(rect.x + 14f, rect.y + 12f, rect.width - 28f, 24f), "Sua jornada ainda não começou", _panelHeadingStyle);
            DrawBodyParagraph(new Rect(rect.x + 14f, rect.y + 42f, rect.width - 28f, 72f), "Ganhe pontos derrotando criaturas, produzindo itens, pescando, completando jackpots e evoluindo seu personagem. Quando o servidor registrar seus primeiros pontos, seu desempenho será exibido em cards expansíveis.");
        }

        private float DrawPerformanceBlock(Rect startRect, string title, string categoryKey, string description, float expandedContentHeight, Action<Rect> drawContent)
        {
            float x = startRect.x;
            float y = startRect.y;
            float width = startRect.width;

            bool expanded = IsPerformanceCategoryExpanded(categoryKey);
            Rect headerRect = new Rect(x, y, width, 34f);

            if (_uiWhiteTexture != null)
            {
                Color headerBg = expanded ? new Color(0.17f, 0.11f, 0.035f, 0.55f) : new Color(0.045f, 0.032f, 0.018f, 0.42f);
                GUI.DrawTexture(headerRect, _uiWhiteTexture, ScaleMode.StretchToFill, true, 0f, headerBg, 0f, 0f);
                GUI.DrawTexture(new Rect(headerRect.x, headerRect.yMax - 1f, headerRect.width, 1f), _uiWhiteTexture, ScaleMode.StretchToFill, true, 0f, new Color(0.95f, 0.76f, 0.33f, expanded ? 0.55f : 0.24f), 0f, 0f);
            }

            Color previousBackground = GUI.backgroundColor;
            GUI.backgroundColor = new Color(0.27f, 0.20f, 0.09f, expanded ? 0.86f : 0.62f);
            if (GUI.Button(headerRect, GUIContent.none))
            {
                TogglePerformanceCategory(categoryKey);
                GUI.FocusControl(null);
            }
            GUI.backgroundColor = previousBackground;

            string arrow = expanded ? "▼" : "▶";
            DrawShadowLabel(new Rect(x + 10f, y + 7f, 20f, 22f), arrow, _panelHeadingStyle);

            Texture2D categoryIcon = GetRuleCategoryIcon(categoryKey);
            float titleX = x + 36f;
            if (categoryIcon != null)
            {
                Rect iconRect = new Rect(x + 31f, y + 5f, RuleCategoryIconSize, RuleCategoryIconSize);
                GUI.DrawTexture(iconRect, categoryIcon, ScaleMode.ScaleToFit, true);
                titleX = iconRect.xMax + 8f;
            }

            DrawShadowLabel(new Rect(titleX, y + 7f, width - (titleX - x) - 12f, 22f), title, _panelHeadingStyle);

            y += 40f;

            if (!expanded)
                return y + 2f;

            DrawBodyParagraph(new Rect(x + 8f, y, width - 16f, 24f), description);
            y += 30f;

            Rect contentRect = new Rect(x + 8f, y, width - 16f, expandedContentHeight);
            if (drawContent != null)
                drawContent(contentRect);

            y += expandedContentHeight + 14f;
            return y;
        }

        private void EnsurePerformanceCategoriesInitialized()
        {
            if (_performanceCategoriesInitialized)
                return;

            _expandedPerformanceCategories.Clear();
            _performanceCategoriesInitialized = true;
        }

        private bool IsPerformanceCategoryExpanded(string categoryKey)
        {
            if (string.IsNullOrWhiteSpace(categoryKey))
                return false;

            EnsurePerformanceCategoriesInitialized();
            return _expandedPerformanceCategories.Contains(categoryKey);
        }

        private void TogglePerformanceCategory(string categoryKey)
        {
            if (string.IsNullOrWhiteSpace(categoryKey))
                return;

            EnsurePerformanceCategoriesInitialized();

            if (_expandedPerformanceCategories.Contains(categoryKey))
                _expandedPerformanceCategories.Remove(categoryKey);
            else
                _expandedPerformanceCategories.Add(categoryKey);
        }

        private float GetRulesContentHeight(float width)
        {
            SnapshotPlayerData data = _cachedPlayerData ?? new SnapshotPlayerData();
            float height = 14f;
            height += 54f;
            height += 34f;

            height += EstimateRulesBlockHeight(width, "Combate", data.HudKillRules);
            height += EstimateRulesBlockHeight(width, "Bosses", data.HudBossRules);
            height += EstimateRulesBlockHeight(width, "Skills", data.HudSkillJackpotRules);
            height += EstimateRulesBlockHeight(width, "Quests", data.HudMarketplaceQuestRules);
            height += EstimateRulesBlockHeight(width, "Pesca", data.HudFishingRules);
            height += EstimateRulesBlockHeight(width, "Exploração", data.HudExplorationMapJackpotRules);
            height += EstimateRulesBlockHeight(width, "Produção", data.HudCraftRules);
            height += EstimateRulesBlockHeight(width, "Cultivo", data.HudFarmJackpotRules);
            height += EstimateRulesBlockHeight(width, "Conquistas", data.HudUniqueCraftJackpotRules);
            height += EstimateRulesBlockHeight(width, "Penalidades", GetDeathPenaltyHudRules(data));

            return Mathf.Max(260f, height + 18f);
        }

        private float EstimateRulesBlockHeight(float width, string title, string rules)
        {
            if (!IsRuleCategoryExpanded(title))
                return 42f;

            if (string.Equals(title, "Skills", StringComparison.OrdinalIgnoreCase))
                return EstimateSkillGuideBlockHeight(rules);

            if (ShouldGroupRulesByItemCategory(title))
                return EstimateCategorizedGuideBlockHeight(title, rules);

            int rows = SplitRulesForHud(rules).Count(); rows = Mathf.Max(1, rows);
            float extraHeight = (string.Equals(title, "Exploração", StringComparison.OrdinalIgnoreCase) ||
                                 string.Equals(title, "Exploracao", StringComparison.OrdinalIgnoreCase) ||
                                 string.Equals(title, "Exploration", StringComparison.OrdinalIgnoreCase)) ? 58f : 0f;
            return 42f + 30f + extraHeight + (rows * 26f) + 14f;
        }

        private void DrawRulesContent(Rect rect)
        {
            SnapshotPlayerData data = _cachedPlayerData ?? new SnapshotPlayerData();
            float x = 8f;
            float width = rect.width - 16f;
            float y = 12f;

            DrawBodyParagraph(new Rect(x, y, width, 42f), "Clique em uma categoria para abrir ou fechar. Use este guia como mapa rápido de pontos: ações comuns aparecem com +pts, jackpots mostram meta e recompensa, e penalidades aparecem como perda.");
            y += 54f;

            y = DrawRulesBlock(new Rect(x, y, width, 10f), "Combate", "Combate", data.EnableKillPoints, data.HudKillRules, "Mate criaturas configuradas pelo servidor.", "Nome:pontos", false);
            y = DrawRulesBlock(new Rect(x, y, width, 10f), "Bosses", "Bosses", data.EnableBossPoints, data.HudBossRules, "Derrote bosses válidos e receba pontos pelo crédito registrado.", "Nome:pontos", false);
            y = DrawRulesBlock(new Rect(x, y, width, 10f), "Skills", "Skills", data.EnableSkillPoints, data.HudSkillJackpotRules, "Evolua uma habilidade até um marco configurado para receber pontos. Cada habilidade pode ser aberta separadamente para ver seus marcos.", "Clique na habilidade para ver níveis e recompensas", false);
            y = DrawRulesBlock(new Rect(x, y, width, 10f), "Quests", "Quests", data.EnableMarketplaceQuestPoints, data.HudMarketplaceQuestRules, "Complete quests configuradas do Marketplace para receber pontos únicos.", "Quest:pontos", false);
            y = DrawRulesBlock(new Rect(x, y, width, 10f), "Pesca", "Pesca", true, data.HudFishingRules, "Pesque os peixes configurados para receber pontos.", "Nome:pontos", false);
            y = DrawRulesBlock(new Rect(x, y, width, 10f), "Exploração", "Exploração", data.RankingEnabled, data.HudExplorationMapJackpotRules, "Revele o mapa para ativar jackpots de exploração.", "Mapa %:pontos", false);
            y = DrawRulesBlock(new Rect(x, y, width, 10f), "Produção", "Produção", true, data.HudCraftRules, "Crie armas, armaduras, comidas ou outros itens configurados.", "Nome:pontos", false);
            y = DrawRulesBlock(new Rect(x, y, width, 10f), "Cultivo", "Cultivo", true, data.HudFarmJackpotRules, "Complete grandes metas de colheita para ativar jackpots.", "Nome:quantidade:pontos", true);
            y = DrawRulesBlock(new Rect(x, y, width, 10f), "Conquistas", "Conquistas", true, data.HudUniqueCraftJackpotRules, "Crie itens especiais uma única vez para ganhar bônus.", "Nome:quantidade:pontos", true);
            string deathRules = GetDeathPenaltyHudRules(data);
            string deathDescription = data.DeathPenaltyUseMultiplier ? "Modo multiplicador ativo: cada morte reduz pontos pelo valor fixo configurado." : "Mortes podem reduzir pontos conforme a configuração por marcos.";
            string deathFormat = data.DeathPenaltyUseMultiplier ? "mortes x pontos por morte" : "mortes:pontos perdidos";
            y = DrawRulesBlock(new Rect(x, y, width, 10f), "Penalidades", "Penalidades", data.EnableDeathPenalty, deathRules, deathDescription, deathFormat, !data.DeathPenaltyUseMultiplier);
        }


        private string GetDeathPenaltyHudRules(SnapshotPlayerData data)
        {
            if (data == null)
                return string.Empty;

            if (!data.DeathPenaltyUseMultiplier)
                return data.HudDeathPenaltyRules ?? string.Empty;

            int penalty = Mathf.Max(0, data.DeathPenaltyPerDeath);
            return "1 morte:-" + penalty + ";15 mortes:-" + (penalty * 15);
        }

        private float GetExplorationMapProgressPercent()
        {
            SnapshotPlayerData data = _cachedPlayerData;
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

        private void DrawExplorationMapProgressBar(Rect rect)
        {
            float percent = GetExplorationMapProgressPercent();
            float clamped = Mathf.Clamp01(percent / 100f);

            Rect labelRect = new Rect(rect.x, rect.y, rect.width, 18f);
            DrawShadowLabel(labelRect, "Mapa revelado: " + percent.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + "%", GetHudTintedStyle(_bodyValueStyle, new Color(0.58f, 0.90f, 1f, 1f), TextAnchor.MiddleLeft));

            Rect barBg = new Rect(rect.x, rect.y + 24f, rect.width, 14f);
            Rect barFill = new Rect(barBg.x + 2f, barBg.y + 2f, Mathf.Max(0f, (barBg.width - 4f) * clamped), barBg.height - 4f);

            if (_uiWhiteTexture != null)
            {
                GUI.DrawTexture(barBg, _uiWhiteTexture, ScaleMode.StretchToFill, true, 0f, new Color(0f, 0f, 0f, 0.48f), 0f, 6f);
                GUI.DrawTexture(barFill, _uiWhiteTexture, ScaleMode.StretchToFill, true, 0f, new Color(0.22f, 0.75f, 1f, 0.86f), 0f, 5f);
                GUI.DrawTexture(new Rect(barBg.x, barBg.y, barBg.width, 1f), _uiWhiteTexture, ScaleMode.StretchToFill, true, 0f, new Color(0.95f, 0.76f, 0.33f, 0.42f), 0f, 0f);
            }

            DrawShadowLabel(new Rect(barBg.x, barBg.y - 1f, barBg.width, barBg.height + 2f), percent.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + "%", GetHudTintedStyle(_mutedBodyStyle, new Color(0.95f, 0.95f, 0.88f, 1f), TextAnchor.MiddleCenter));
        }

        private float DrawRulesBlock(Rect startRect, string title, string categoryKey, bool enabled, string rules, string description, string formatHint, bool jackpotFormat)
        {
            float x = startRect.x;
            float y = startRect.y;
            float width = startRect.width;

            bool expanded = IsRuleCategoryExpanded(categoryKey);
            Rect headerRect = new Rect(x, y, width, 34f);

            if (_uiWhiteTexture != null)
            {
                Color headerBg = expanded ? new Color(0.17f, 0.11f, 0.035f, 0.55f) : new Color(0.045f, 0.032f, 0.018f, 0.42f);
                GUI.DrawTexture(headerRect, _uiWhiteTexture, ScaleMode.StretchToFill, true, 0f, headerBg, 0f, 0f);
                GUI.DrawTexture(new Rect(headerRect.x, headerRect.yMax - 1f, headerRect.width, 1f), _uiWhiteTexture, ScaleMode.StretchToFill, true, 0f, new Color(0.95f, 0.76f, 0.33f, expanded ? 0.55f : 0.24f), 0f, 0f);
            }

            Color previousBackground = GUI.backgroundColor;
            GUI.backgroundColor = new Color(0.27f, 0.20f, 0.09f, expanded ? 0.86f : 0.62f);
            if (GUI.Button(headerRect, GUIContent.none))
            {
                ToggleRuleCategory(categoryKey);
                GUI.FocusControl(null);
            }
            GUI.backgroundColor = previousBackground;

            string arrow = expanded ? "▼" : "▶";
            DrawShadowLabel(new Rect(x + 10f, y + 7f, 20f, 22f), arrow, _panelHeadingStyle);

            Texture2D categoryIcon = GetRuleCategoryIcon(categoryKey);
            float titleX = x + 36f;
            if (categoryIcon != null)
            {
                Rect iconRect = new Rect(x + 31f, y + 5f, RuleCategoryIconSize, RuleCategoryIconSize);
                GUI.DrawTexture(iconRect, categoryIcon, ScaleMode.ScaleToFit, true);
                titleX = iconRect.xMax + 8f;
            }

            DrawShadowLabel(new Rect(titleX, y + 7f, width - (titleX - x) - 12f, 22f), title, _panelHeadingStyle);

            y += 40f;

            if (!expanded)
                return y + 2f;

            float introHeight = string.Equals(categoryKey, "Skills", StringComparison.OrdinalIgnoreCase) ? 52f : 28f;
            DrawBodyParagraph(new Rect(x + 8f, y, width - 16f, introHeight), description + "  •  " + formatHint);
            y += introHeight + 8f;

            if (string.Equals(categoryKey, "Exploração", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(categoryKey, "Exploracao", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(categoryKey, "Exploration", StringComparison.OrdinalIgnoreCase))
            {
                DrawExplorationMapProgressBar(new Rect(x + 8f, y, width - 16f, 46f));
                y += 56f;
            }

            if (string.Equals(categoryKey, "Skills", StringComparison.OrdinalIgnoreCase))
                return DrawSkillGuideRows(x, y, width, rules);

            if (ShouldGroupRulesByItemCategory(categoryKey))
                return DrawCategorizedGuideRows(x, y, width, categoryKey, rules, jackpotFormat);

            List<string> rows = SplitRulesForHud(rules).ToList();
            if (rows.Count == 0)
                rows.Add("Nenhuma regra encontrada.");

            for (int i = 0; i < rows.Count; i++)
            {
                if (IsHudCategoryHeader(rows[i]))
                {
                    DrawGuideCategoryHeader(new Rect(x + 8f, y + 4f, width - 16f, 24f), GetHudCategoryHeaderTitle(rows[i]));
                    y += 30f;
                    continue;
                }

                DrawGuideRuleRow(new Rect(x + 8f, y, width - 16f, 24f), rows[i], categoryKey, jackpotFormat, title.IndexOf("Penalidades", StringComparison.OrdinalIgnoreCase) >= 0);
                y += 26f;
            }

            return y + 14f;
        }

        private int GetRuleProgressCount(string categoryKey, string prefab)
        {
            SnapshotPlayerData data = _cachedPlayerData;
            if (data == null || data.ProgressCounters == null || string.IsNullOrWhiteSpace(prefab))
                return 0;

            string safePrefab = SafeKey(prefab);
            string[] keys;

            if (string.Equals(categoryKey, "Combate", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(categoryKey, "Combat", StringComparison.OrdinalIgnoreCase))
            {
                keys = new string[] { "Kill:" + safePrefab, "Combat:" + safePrefab };
            }
            else if (string.Equals(categoryKey, "Produção", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(categoryKey, "Producao", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(categoryKey, "Production", StringComparison.OrdinalIgnoreCase))
            {
                keys = new string[] { "Craft:" + safePrefab, "Production:" + safePrefab };
            }
            else if (string.Equals(categoryKey, "Conquistas", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(categoryKey, "Achievements", StringComparison.OrdinalIgnoreCase))
            {
                keys = new string[] { "UniqueCraft:" + safePrefab };
            }
            else if (string.Equals(categoryKey, "Bosses", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(categoryKey, "Chefes", StringComparison.OrdinalIgnoreCase))
            {
                keys = new string[] { "Bosses:" + safePrefab };
            }
            else if (string.Equals(categoryKey, "Pesca", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(categoryKey, "Fishing", StringComparison.OrdinalIgnoreCase))
            {
                keys = new string[] { "Fishing:" + safePrefab, "Pesca:" + safePrefab };
            }
            else if (string.Equals(categoryKey, "Cultivo", StringComparison.OrdinalIgnoreCase))
            {
                keys = new string[] { "Farm:" + safePrefab };
            }
            else if (string.Equals(categoryKey, "Exploração", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(categoryKey, "Exploracao", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(categoryKey, "Exploration", StringComparison.OrdinalIgnoreCase))
            {
                keys = new string[] { "ExplorationMap:Percent", "Exploration:Map", "Exploracao:Mapa" };
            }
            else
            {
                keys = new string[] { SafeKey(categoryKey) + ":" + safePrefab };
            }

            for (int i = 0; i < keys.Length; i++)
            {
                int value;
                if (data.ProgressCounters.TryGetValue(keys[i], out value))
                    return Mathf.Clamp(value, 0, int.MaxValue);
            }


            string suffix = ":" + safePrefab;
            int fallback = 0;
            foreach (KeyValuePair<string, int> pair in data.ProgressCounters)
            {
                if (string.IsNullOrWhiteSpace(pair.Key))
                    continue;

                if (pair.Key.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                    fallback = Mathf.Clamp(fallback + Mathf.Max(0, pair.Value), 0, int.MaxValue);
            }

            return fallback;
        }

        private float EstimateCategorizedGuideBlockHeight(string categoryKey, string rules)
        {
            List<RuleGuideGroup> groups = BuildRuleGuideGroups(categoryKey, rules, false);
            if (groups.Count == 0)
                return 42f + 30f + 32f + 14f;

            float height = 42f + 34f;
            for (int i = 0; i < groups.Count; i++)
            {
                RuleGuideGroup group = groups[i];
                height += 32f;
                if (IsRuleSubCategoryExpanded(categoryKey, group.Name))
                    height += Mathf.Max(1, group.Rows.Count) * 26f + 6f;
            }

            return height + 14f;
        }

        private float DrawCategorizedGuideRows(float x, float y, float width, string categoryKey, string rules, bool jackpotFormat)
        {
            List<RuleGuideGroup> groups = BuildRuleGuideGroups(categoryKey, rules, false);
            if (groups.Count == 0)
            {
                DrawGuideRuleRow(new Rect(x + 8f, y, width - 16f, 24f), "Nenhuma regra configurada.", categoryKey, jackpotFormat, false);
                return y + 40f;
            }

            for (int i = 0; i < groups.Count; i++)
            {
                RuleGuideGroup group = groups[i];
                bool expanded = IsRuleSubCategoryExpanded(categoryKey, group.Name);
                Rect headerRect = new Rect(x + 8f, y, width - 16f, 28f);

                if (_uiWhiteTexture != null)
                {
                    Color bg = expanded ? new Color(0.16f, 0.10f, 0.035f, 0.62f) : new Color(0.05f, 0.035f, 0.015f, 0.48f);
                    GUI.DrawTexture(headerRect, _uiWhiteTexture, ScaleMode.StretchToFill, true, 0f, bg, 0f, 8f);
                    GUI.DrawTexture(new Rect(headerRect.x, headerRect.yMax - 1f, headerRect.width, 1f), _uiWhiteTexture, ScaleMode.StretchToFill, true, 0f, new Color(0.95f, 0.76f, 0.33f, expanded ? 0.42f : 0.18f), 0f, 0f);
                }

                if (GUI.Button(headerRect, GUIContent.none, GUIStyle.none))
                {
                    ToggleRuleSubCategory(categoryKey, group.Name);
                    GUI.FocusControl(null);
                }

                string arrow = expanded ? "▼" : "▶";
                int groupProgress = GetRuleGroupProgressCount(categoryKey, group);
                string countText = (group.Rows.Count == 1 ? "1 item" : group.Rows.Count + " itens") + (groupProgress > 0 ? " • " + groupProgress + "x" : "");
                DrawShadowLabel(new Rect(headerRect.x + 10f, headerRect.y + 4f, 22f, 20f), arrow, _panelHeadingStyle);
                DrawShadowLabel(new Rect(headerRect.x + 34f, headerRect.y + 5f, headerRect.width - 160f, 18f), group.Name.ToUpperInvariant(), GetHudTintedStyle(_configStyle, new Color(1f, 0.78f, 0.28f, 1f), TextAnchor.MiddleLeft));
                DrawShadowLabel(new Rect(headerRect.xMax - 118f, headerRect.y + 5f, 110f, 18f), countText, GetRuleProgressStyle(categoryKey, false));

                y += 32f;

                if (!expanded)
                    continue;

                for (int j = 0; j < group.Rows.Count; j++)
                {
                    DrawGuideRuleRow(new Rect(x + 22f, y, width - 30f, 24f), group.Rows[j], categoryKey, jackpotFormat, false);
                    y += 26f;
                }

                y += 6f;
            }

            return y + 14f;
        }

        private bool ShouldGroupRulesByItemCategory(string categoryKey)
        {
            return string.Equals(categoryKey, "Produção", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(categoryKey, "Producao", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(categoryKey, "Production", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(categoryKey, "Conquistas", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(categoryKey, "Achievements", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(categoryKey, "Combate", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(categoryKey, "Combat", StringComparison.OrdinalIgnoreCase);
        }

        private bool IsRuleSubCategoryExpanded(string categoryKey, string subCategory)
        {
            if (string.IsNullOrWhiteSpace(categoryKey) || string.IsNullOrWhiteSpace(subCategory))
                return false;

            return _expandedRuleSubCategories.Contains(BuildRuleSubCategoryKey(categoryKey, subCategory));
        }

        private void ToggleRuleSubCategory(string categoryKey, string subCategory)
        {
            if (string.IsNullOrWhiteSpace(categoryKey) || string.IsNullOrWhiteSpace(subCategory))
                return;

            string key = BuildRuleSubCategoryKey(categoryKey, subCategory);
            if (_expandedRuleSubCategories.Contains(key))
                _expandedRuleSubCategories.Remove(key);
            else
                _expandedRuleSubCategories.Add(key);
        }

        private string BuildRuleSubCategoryKey(string categoryKey, string subCategory)
        {
            return (categoryKey ?? "").Trim() + "::" + (subCategory ?? "").Trim();
        }


        private int GetRuleGroupProgressCount(string categoryKey, RuleGuideGroup group)
        {
            if (group == null || group.Rows == null)
                return 0;

            int total = 0;
            for (int i = 0; i < group.Rows.Count; i++)
            {
                string prefab = ExtractRulePrefabKey(group.Rows[i]);
                total = Mathf.Clamp(total + GetRuleProgressCount(categoryKey, prefab), 0, int.MaxValue);
            }

            return total;
        }

        private sealed class RuleGuideGroup
        {
            public readonly string Name;
            public readonly List<string> Rows = new List<string>();

            public RuleGuideGroup(string name)
            {
                Name = string.IsNullOrWhiteSpace(name) ? "Outros" : name;
            }
        }

        private List<RuleGuideGroup> BuildRuleGuideGroups(string categoryKey, string rules, bool applySearchFilter)
        {
            Dictionary<string, RuleGuideGroup> map = new Dictionary<string, RuleGuideGroup>(StringComparer.OrdinalIgnoreCase);
            List<string> rows = SplitRulesForHud(rules);

            for (int i = 0; i < rows.Count; i++)
            {
                string row = rows[i] ?? "";
                if (IsHudCategoryHeader(row))
                    continue;

                string prefab = ExtractRulePrefabKey(row);
                string groupName = GetRuleGuideGroupName(categoryKey, prefab);

                RuleGuideGroup group;
                if (!map.TryGetValue(groupName, out group))
                {
                    group = new RuleGuideGroup(groupName);
                    map[groupName] = group;
                }

                group.Rows.Add(row);
            }

            string[] order = GetRuleGuideGroupOrder(categoryKey);

            List<RuleGuideGroup> result = map.Values
                .OrderBy(g => Array.IndexOf(order, g.Name) < 0 ? 999 : Array.IndexOf(order, g.Name))
                .ThenBy(g => g.Name)
                .ToList();

            for (int i = 0; i < result.Count; i++)
            {
                result[i].Rows.Sort((a, b) => string.Compare(FriendlyRuleNameForHud(ExtractRulePrefabKey(a)), FriendlyRuleNameForHud(ExtractRulePrefabKey(b)), StringComparison.OrdinalIgnoreCase));
            }

            return result;
        }

        private string ExtractRulePrefabKey(string rawRule)
        {
            if (string.IsNullOrWhiteSpace(rawRule))
                return "";

            string[] parts = rawRule.Split(new char[] { ':' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0)
                return rawRule.Trim();

            return (parts[0] ?? "").Trim();
        }

        private string GetRuleGuideGroupName(string categoryKey, string prefab)
        {
            if (string.Equals(categoryKey, "Combate", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(categoryKey, "Combat", StringComparison.OrdinalIgnoreCase))
                return GetCreatureBiomeCategoryName(prefab);

            return GetRuleItemCategoryName(prefab);
        }

        private string[] GetRuleGuideGroupOrder(string categoryKey)
        {
            if (string.Equals(categoryKey, "Combate", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(categoryKey, "Combat", StringComparison.OrdinalIgnoreCase))
            {
                return new string[]
                {
                    "Prados",
                    "Floresta Negra",
                    "Pântano",
                    "Montanha",
                    "Planícies",
                    "Mistlands",
                    "Ashlands",
                    "Oceano",
                    "Criaturas Especiais",
                    "Outros"
                };
            }

            return new string[]
            {
                "Armaduras",
                "Armas",
                "Escudos",
                "Munições",
                "Comidas",
                "Comidas e Consumíveis",
                "Ferramentas",
                "Materiais",
                "Outros"
            };
        }

        private string GetCreatureBiomeCategoryName(string prefab)
        {
            string key = (prefab ?? "").Trim().ToLowerInvariant();

            if (string.IsNullOrWhiteSpace(key))
                return "Outros";

            key = key.Replace(" ", "").Replace("-", "").Replace("_", "");


            if (key.Contains("boar") || key.Contains("deer") || key.Contains("neck") || key.Contains("greyling"))
                return "Prados";


            if (key.Contains("greydwarf") || key.Contains("troll") || key.Contains("skeleton") || key.Contains("ghost") || key.Contains("rancidremains"))
                return "Floresta Negra";


            if (key.Contains("draugr") || key.Contains("blob") || key.Contains("oozer") || key.Contains("leech") || key.Contains("wraith") || key.Contains("surtling") || key.Contains("abomination"))
                return "Pântano";


            if (key.Contains("wolf") || key.Contains("fenring") || key.Contains("hatchling") || key.Contains("stonegolem") || key.Contains("cultist") || key.Contains("ulv") || key.Contains("bat"))
                return "Montanha";


            if (key.Contains("goblin") || key.Contains("fuling") || key.Contains("lox") || key.Contains("deathsquito") || key.Contains("tarblob") || key.Contains("growth"))
                return "Planícies";


            if (key.Contains("seeker") || key.Contains("gjall") || key.Contains("tick") || key.Contains("dverger") || key.Contains("hare"))
                return "Mistlands";


            if (key.Contains("charred") || key.Contains("morgen") || key.Contains("valkyrie") || key.Contains("volture") || key.Contains("asksvin") || key.Contains("bonemaw") || key.Contains("fader"))
                return "Ashlands";


            if (key.Contains("serpent") || key.Contains("leviathan"))
                return "Oceano";


            if (key.Contains("eikthyr") || key.Contains("gdking") || key.Contains("elder") || key.Contains("bonemass") || key.Contains("dragon") || key.Contains("moder") || key.Contains("goblinking") || key.Contains("yagluth") || key.Contains("queen"))
                return "Criaturas Especiais";

            return "Outros";
        }

        private string GetRuleItemCategoryName(string prefab)
        {
            string key = (prefab ?? "").Trim().ToLowerInvariant();

            if (string.IsNullOrWhiteSpace(key))
                return "Outros";

            if (key.Contains("helmet") || key.Contains("chest") || key.Contains("legs") || key.Contains("armor") || key.Contains("cape") || key.Contains("root") || key.Contains("fenris"))
                return "Armaduras";

            if (key.Contains("shield") || key.Contains("buckler"))
                return "Escudos";

            if (key.Contains("arrow") || key.Contains("bolt"))
                return "Munições";

            if (key.Contains("bow") || key.Contains("crossbow") || key.Contains("sword") || key.Contains("knife") || key.Contains("axe") || key.Contains("mace") || key.Contains("club") || key.Contains("spear") || key.Contains("atgeir") || key.Contains("staff") || key.Contains("bomb"))
                return "Armas";

            if (key.Contains("pickaxe") || key.Contains("hammer") || key.Contains("hoe") || key.Contains("cultivator") || key.Contains("fishingrod") || key.Contains("torch") || key.Contains("tankard"))
                return "Ferramentas";

            if (key.Contains("mead") || key.Contains("stew") || key.Contains("soup") || key.Contains("jerky") || key.Contains("honey") || key.Contains("chicken") || key.Contains("meat") || key.Contains("fish") || key.Contains("pie") || key.Contains("pudding") || key.Contains("porridge") || key.Contains("mushroom") || key.Contains("carrot") || key.Contains("turnip") || key.Contains("onion") || key.Contains("salad") || key.Contains("omelet") || key.Contains("seekeraspic") || key.Contains("bloodpudding") || key.Contains("sausage") || key.Contains("deerstew") || key.Contains("queensjam") || key.Contains("bukeperries"))
                return "Comidas e Consumíveis";

            if (key.Contains("wood") || key.Contains("stone") || key.Contains("ore") || key.Contains("scrap") || key.Contains("bar") || key.Contains("iron") || key.Contains("copper") || key.Contains("tin") || key.Contains("bronze") || key.Contains("silver") || key.Contains("blackmetal") || key.Contains("flametal") || key.Contains("chitin") || key.Contains("thread") || key.Contains("linen") || key.Contains("leather") || key.Contains("hide") || key.Contains("pelt") || key.Contains("trophy") || key.Contains("resin") || key.Contains("coal") || key.Contains("crystal") || key.Contains("yagluth") || key.Contains("eitr") || key.Contains("sap"))
                return "Materiais";

            return "Outros";
        }


        private float EstimateSkillGuideBlockHeight(string rules)
        {
            if (!IsRuleCategoryExpanded("Skills"))
                return 42f;

            List<SkillGuideGroup> groups = BuildSkillGuideGroups(rules);
            if (groups.Count == 0)
                return 42f + 34f + 32f + 14f;

            float height = 42f + 60f;
            for (int i = 0; i < groups.Count; i++)
            {
                SkillGuideGroup group = groups[i];
                height += 58f;
                if (IsGuideSkillRowExpanded(group.Name))
                    height += Mathf.Max(1, group.Milestones.Count) * 28f + 10f;
            }

            return height + 14f;
        }

        private float DrawSkillGuideRows(float x, float y, float width, string rules)
        {
            List<SkillGuideGroup> groups = BuildSkillGuideGroups(rules);
            if (groups.Count == 0)
            {
                DrawGuideRuleRow(new Rect(x + 8f, y, width - 16f, 24f), "Nenhuma regra configurada.", "Skills", false, false);
                return y + 40f;
            }

            for (int i = 0; i < groups.Count; i++)
            {
                SkillGuideGroup group = groups[i];
                bool expanded = IsGuideSkillRowExpanded(group.Name);
                Rect rowRect = new Rect(x + 8f, y, width - 16f, 54f);

                if (_uiWhiteTexture != null)
                {
                    Color bg = expanded ? new Color(0.13f, 0.09f, 0.035f, 0.58f) : new Color(0f, 0f, 0f, 0.24f);
                    GUI.DrawTexture(rowRect, _uiWhiteTexture, ScaleMode.StretchToFill, true, 0f, bg, 0f, 0f);


                    GUI.DrawTexture(new Rect(rowRect.x, rowRect.y, 4f, rowRect.height), _uiWhiteTexture, ScaleMode.StretchToFill, true, 0f, GetSkillGuideAccentColor(group.Name), 0f, 0f);


                    GUI.DrawTexture(new Rect(rowRect.xMax - 116f, rowRect.y + 15f, 84f, 24f), _uiWhiteTexture, ScaleMode.StretchToFill, true, 0f, new Color(0.05f, 0.035f, 0.015f, 0.62f), 0f, 12f);
                }

                if (GUI.Button(rowRect, GUIContent.none, GUIStyle.none))
                {
                    ToggleGuideSkillRow(group.Name);
                    GUI.FocusControl(null);
                }

                string displayName = GetSkillGuideDisplayName(group.Name);
                string description = GetSkillGuideDescription(group.Name);
                string arrow = expanded ? "▼" : "▶";
                string summary = group.Milestones.Count == 1
                    ? "1 marco"
                    : group.Milestones.Count + " marcos";

                float leftX = rowRect.x + 14f;
                float rightBlockWidth = 126f;
                float textWidth = rowRect.width - rightBlockWidth - 24f;

                DrawShadowLabel(new Rect(leftX, rowRect.y + 7f, textWidth, 20f), displayName, _bodyRowStyle);
                DrawShadowLabel(new Rect(leftX, rowRect.y + 30f, textWidth, 18f), description, _configStyle);
                DrawShadowLabel(new Rect(rowRect.xMax - 112f, rowRect.y + 18f, 78f, 18f), summary, _bodyValueStyle);
                DrawShadowLabel(new Rect(rowRect.xMax - 28f, rowRect.y + 17f, 20f, 20f), arrow, _panelHeadingStyle);

                y += 58f;

                if (!expanded)
                    continue;

                for (int j = 0; j < group.Milestones.Count; j++)
                {
                    SkillGuideMilestone milestone = group.Milestones[j];
                    Rect milestoneRect = new Rect(x + 24f, y, width - 40f, 26f);

                    if (_uiWhiteTexture != null)
                    {
                        Color lineBg = j % 2 == 0 ? new Color(0f, 0f, 0f, 0.16f) : new Color(0.08f, 0.055f, 0.025f, 0.18f);
                        GUI.DrawTexture(new Rect(milestoneRect.x, milestoneRect.y + 1f, milestoneRect.width, milestoneRect.height - 2f), _uiWhiteTexture, ScaleMode.StretchToFill, true, 0f, lineBg, 0f, 0f);
                    }

                    DrawShadowLabel(new Rect(milestoneRect.x + 10f, milestoneRect.y + 3f, milestoneRect.width - 128f, 18f), "Alcance o nível " + milestone.Level, _bodyRowStyle);
                    DrawShadowLabel(new Rect(milestoneRect.xMax - 112f, milestoneRect.y + 3f, 104f, 18f), "+" + milestone.Points + " pontos", _bodyValueStyle);

                    y += 28f;
                }

                y += 10f;
            }

            return y + 14f;
        }

        private string GetSkillGuideDisplayName(string skillName)
        {
            string key = (skillName ?? "").Trim();

            switch (key.ToLowerInvariant())
            {
                case "axes": return "Machados";
                case "blocking": return "Bloqueio";
                case "bloodmagic": return "Magia de Sangue";
                case "bows": return "Arcos";
                case "clubs": return "Porretes";
                case "crossbows": return "Bestas";
                case "elementalmagic": return "Magia Elemental";
                case "farming": return "Cultivo";
                case "jump": return "Salto";
                case "knives": return "Facas";
                case "pickaxes": return "Picaretas";
                case "polearms": return "Armas de Haste";
                case "run": return "Corrida";
                case "spears": return "Lanças";
                case "swords": return "Espadas";
                case "swim": return "Natação";
                case "unarmed": return "Combate Desarmado";
                case "woodcutting": return "Lenhador";
                case "fishing": return "Pesca";
                case "sneak": return "Furtividade";
                case "ride": return "Montaria";
                default:
                    return FriendlyRuleNameForHud(key);
            }
        }

        private string GetSkillGuideDescription(string skillName)
        {
            string key = (skillName ?? "").Trim();

            switch (key.ToLowerInvariant())
            {
                case "axes": return "Evolua usando machados.";
                case "blocking": return "Ganhe resistência defendendo ataques.";
                case "bloodmagic": return "Aprimore poderes de sangue.";
                case "bows": return "Treine ataques à distância.";
                case "clubs": return "Esmague inimigos com armas contundentes.";
                case "crossbows": return "Use bestas com precisão.";
                case "elementalmagic": return "Domine fogo, gelo e raios.";
                case "farming": return "Plante, colha e prospere.";
                case "jump": return "Explore o mundo superando obstáculos.";
                case "knives": return "Ataque rápido e de perto.";
                case "pickaxes": return "Extraia minérios e recursos.";
                case "polearms": return "Controle distância com armas longas.";
                case "run": return "Corra, viaje e sobreviva.";
                case "spears": return "Lance ou ataque mantendo distância.";
                case "swords": return "Equilibre ataque e defesa.";
                case "swim": return "Atravesse águas com segurança.";
                case "unarmed": return "Lute sem armas.";
                case "woodcutting": return "Derrube árvores e colete madeira.";
                case "fishing": return "Pesque para receber honra.";
                case "sneak": return "Mova-se sem ser percebido.";
                case "ride": return "Evolua cavalgando.";
                default:
                    return "Abra para ver níveis e recompensas.";
            }
        }

        private Color GetSkillGuideAccentColor(string skillName)
        {
            int hash = Math.Abs((skillName ?? "skill").ToLowerInvariant().GetHashCode());
            float r = 0.32f + ((hash & 0xFF) / 255f) * 0.34f;
            float g = 0.24f + (((hash >> 8) & 0xFF) / 255f) * 0.28f;
            float b = 0.12f + (((hash >> 16) & 0xFF) / 255f) * 0.24f;
            return new Color(r, g, b, 0.78f);
        }

        private bool IsGuideSkillRowExpanded(string skillName)
        {
            if (string.IsNullOrWhiteSpace(skillName))
                return false;

            return _expandedGuideSkillRows.Contains(skillName);
        }

        private void ToggleGuideSkillRow(string skillName)
        {
            if (string.IsNullOrWhiteSpace(skillName))
                return;

            if (_expandedGuideSkillRows.Contains(skillName))
                _expandedGuideSkillRows.Remove(skillName);
            else
                _expandedGuideSkillRows.Add(skillName);
        }

        private List<SkillGuideGroup> BuildSkillGuideGroups(string rules)
        {
            Dictionary<string, SkillGuideGroup> map = new Dictionary<string, SkillGuideGroup>(StringComparer.OrdinalIgnoreCase);
            List<string> rows = SplitRulesForHud(rules);

            for (int i = 0; i < rows.Count; i++)
            {
                string raw = rows[i] ?? "";
                string[] parts = raw.Split(new char[] { ':' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 3)
                    continue;

                string skillName = (parts[0] ?? "").Trim();
                string level = (parts[1] ?? "").Trim();
                string points = (parts[2] ?? "").Trim();

                if (string.IsNullOrWhiteSpace(skillName))
                    skillName = "Habilidade";

                SkillGuideGroup group;
                if (!map.TryGetValue(skillName, out group))
                {
                    group = new SkillGuideGroup(skillName);
                    map[skillName] = group;
                }

                group.Milestones.Add(new SkillGuideMilestone(level, points));
            }

            List<SkillGuideGroup> result = map.Values
                .OrderBy(x => GetSkillGuideDisplayName(x.Name))
                .ToList();

            for (int i = 0; i < result.Count; i++)
            {
                result[i].Milestones = result[i].Milestones
                    .OrderBy(x => ParseIntSafe(x.Level))
                    .ThenBy(x => x.Level)
                    .ToList();
            }

            return result;
        }

        private int ParseIntSafe(string value)
        {
            int parsed;
            if (int.TryParse(value, out parsed))
                return parsed;

            return int.MaxValue;
        }

        private class SkillGuideGroup
        {
            public string Name;
            public List<SkillGuideMilestone> Milestones;

            public SkillGuideGroup(string name)
            {
                Name = string.IsNullOrWhiteSpace(name) ? "Habilidade" : name;
                Milestones = new List<SkillGuideMilestone>();
            }
        }

        private class SkillGuideMilestone
        {
            public string Level;
            public string Points;

            public SkillGuideMilestone(string level, string points)
            {
                Level = string.IsNullOrWhiteSpace(level) ? "?" : level;
                Points = string.IsNullOrWhiteSpace(points) ? "0" : points;
            }
        }

        private void EnsureRuleCategoriesInitialized()
        {
            if (_ruleCategoriesInitialized)
                return;

            _expandedRuleCategories.Clear();
            _ruleCategoriesInitialized = true;
        }

        private bool IsRuleCategoryExpanded(string categoryKey)
        {
            if (string.IsNullOrWhiteSpace(categoryKey))
                return false;

            EnsureRuleCategoriesInitialized();
            return _expandedRuleCategories.Contains(categoryKey);
        }

        private void ToggleRuleCategory(string categoryKey)
        {
            if (string.IsNullOrWhiteSpace(categoryKey))
                return;

            EnsureRuleCategoriesInitialized();

            if (_expandedRuleCategories.Contains(categoryKey))
                _expandedRuleCategories.Remove(categoryKey);
            else
                _expandedRuleCategories.Add(categoryKey);
        }

        private void DrawGuideCategoryHeader(Rect rect, string title)
        {
            if (string.IsNullOrWhiteSpace(title))
                title = "Outros";

            if (_uiWhiteTexture != null)
            {
                GUI.DrawTexture(new Rect(rect.x, rect.y + 10f, rect.width, 1f), _uiWhiteTexture, ScaleMode.StretchToFill, true, 0f, new Color(0.95f, 0.76f, 0.33f, 0.36f), 0f, 0f);
                GUI.DrawTexture(new Rect(rect.x, rect.y, Mathf.Min(210f, rect.width), rect.height), _uiWhiteTexture, ScaleMode.StretchToFill, true, 0f, new Color(0.12f, 0.075f, 0.025f, 0.72f), 0f, 8f);
            }

            DrawShadowLabel(new Rect(rect.x + 10f, rect.y + 3f, rect.width - 20f, 18f), title.ToUpperInvariant(), _configStyle);
        }

        private void DrawGuideRuleRow(Rect rect, string rawRule, string categoryKey, bool jackpotFormat, bool penaltyFormat)
        {
            string left;
            string right;
            FormatGuideRule(rawRule, categoryKey, jackpotFormat, penaltyFormat, out left, out right);

            if (_uiWhiteTexture != null)
                GUI.DrawTexture(new Rect(rect.x, rect.y + 1f, rect.width, rect.height - 2f), _uiWhiteTexture, ScaleMode.StretchToFill, true, 0f, new Color(0f, 0f, 0f, 0.12f), 0f, 0f);

            int progress = GetRuleProgressCount(categoryKey, ExtractRulePrefabKey(rawRule));
            bool explorationProgress = string.Equals(categoryKey, "Exploração", StringComparison.OrdinalIgnoreCase) ||
                                       string.Equals(categoryKey, "Exploracao", StringComparison.OrdinalIgnoreCase) ||
                                       string.Equals(categoryKey, "Exploration", StringComparison.OrdinalIgnoreCase);
            string progressText = explorationProgress
                ? GetExplorationMapProgressPercent().ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + "%"
                : (progress > 0 ? progress + "x" : "0x");

            GUIStyle leftStyle = GetRuleNameStyle(categoryKey, penaltyFormat);
            GUIStyle progressStyle = GetRuleProgressStyle(categoryKey, penaltyFormat);
            GUIStyle valueStyle = GetRuleValueStyle(categoryKey, penaltyFormat);

            DrawShadowLabel(new Rect(rect.x + 8f, rect.y + 3f, rect.width - 190f, 18f), left, leftStyle);
            DrawShadowLabel(new Rect(rect.xMax - 176f, rect.y + 3f, 48f, 18f), progressText, progressStyle);
            DrawShadowLabel(new Rect(rect.xMax - 126f, rect.y + 3f, 118f, 18f), right, valueStyle);
        }

        private GUIStyle GetRuleNameStyle(string categoryKey, bool penaltyFormat)
        {
            Color color = new Color(0.92f, 0.90f, 0.82f, 1f);

            if (penaltyFormat || string.Equals(categoryKey, "Mortes", StringComparison.OrdinalIgnoreCase) || string.Equals(categoryKey, "Deaths", StringComparison.OrdinalIgnoreCase))
            {
                color = new Color(1f, 0.42f, 0.36f, 1f);
            }
            else if (string.Equals(categoryKey, "Combate", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(categoryKey, "Combat", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(categoryKey, "Bosses", StringComparison.OrdinalIgnoreCase))
            {
                color = new Color(1f, 0.70f, 0.58f, 1f);
            }
            else if (string.Equals(categoryKey, "Produção", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(categoryKey, "Producao", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(categoryKey, "Production", StringComparison.OrdinalIgnoreCase))
            {
                color = new Color(0.78f, 0.90f, 1f, 1f);
            }
            else if (string.Equals(categoryKey, "Conquistas", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(categoryKey, "Achievements", StringComparison.OrdinalIgnoreCase))
            {
                color = new Color(1f, 0.86f, 0.38f, 1f);
            }
            else if (string.Equals(categoryKey, "Skills", StringComparison.OrdinalIgnoreCase))
            {
                color = new Color(0.82f, 0.68f, 1f, 1f);
            }
            else if (string.Equals(categoryKey, "Cultivo", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(categoryKey, "Farming", StringComparison.OrdinalIgnoreCase))
            {
                color = new Color(0.64f, 1f, 0.58f, 1f);
            }
            else if (string.Equals(categoryKey, "Exploração", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(categoryKey, "Exploracao", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(categoryKey, "Exploration", StringComparison.OrdinalIgnoreCase))
            {
                color = new Color(0.58f, 0.90f, 1f, 1f);
            }

            return GetHudTintedStyle(_bodyRowStyle, color, TextAnchor.MiddleLeft);
        }

        private GUIStyle GetRuleValueStyle(string categoryKey, bool penaltyFormat)
        {
            Color color = penaltyFormat ? new Color(1f, 0.28f, 0.22f, 1f) : new Color(0.25f, 1f, 0.45f, 1f);
            return GetHudTintedStyle(penaltyFormat ? _configStyle : _bodyValueStyle, color, TextAnchor.MiddleRight);
        }

        private GUIStyle GetRuleProgressStyle(string categoryKey, bool penaltyFormat)
        {
            Color color = new Color(0.45f, 0.78f, 1f, 1f);

            if (penaltyFormat || string.Equals(categoryKey, "Mortes", StringComparison.OrdinalIgnoreCase) || string.Equals(categoryKey, "Deaths", StringComparison.OrdinalIgnoreCase))
            {
                color = new Color(1f, 0.28f, 0.22f, 1f);
            }
            else if (string.Equals(categoryKey, "Combate", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(categoryKey, "Combat", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(categoryKey, "Bosses", StringComparison.OrdinalIgnoreCase))
            {
                color = new Color(1f, 0.28f, 0.22f, 1f);
            }
            else if (string.Equals(categoryKey, "Produção", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(categoryKey, "Producao", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(categoryKey, "Production", StringComparison.OrdinalIgnoreCase))
            {
                color = new Color(0.45f, 0.78f, 1f, 1f);
            }
            else if (string.Equals(categoryKey, "Conquistas", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(categoryKey, "Achievements", StringComparison.OrdinalIgnoreCase))
            {
                color = new Color(1f, 0.82f, 0.28f, 1f);
            }
            else if (string.Equals(categoryKey, "Skills", StringComparison.OrdinalIgnoreCase))
            {
                color = new Color(0.82f, 0.68f, 1f, 1f);
            }
            else if (string.Equals(categoryKey, "Cultivo", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(categoryKey, "Farming", StringComparison.OrdinalIgnoreCase))
            {
                color = new Color(0.42f, 1f, 0.48f, 1f);
            }

            return GetHudTintedStyle(_configStyle, color, TextAnchor.MiddleRight);
        }

        private GUIStyle GetHudTintedStyle(GUIStyle baseStyle, Color color, TextAnchor alignment)
        {
            GUIStyle style = new GUIStyle(baseStyle ?? GUI.skin.label);
            style.alignment = alignment;
            style.normal.textColor = color;
            return style;
        }

        private void FormatGuideRule(string rawRule, string categoryKey, bool jackpotFormat, bool penaltyFormat, out string left, out string right)
        {
            left = string.IsNullOrWhiteSpace(rawRule) ? "Regra" : rawRule.Trim();
            right = "";

            if (IsHudCategoryHeader(left))
            {
                left = GetHudCategoryHeaderTitle(left);
                return;
            }

            string[] parts = left.Split(new char[] { ':' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length <= 1)
                return;

            if (penaltyFormat && parts.Length >= 2)
            {
                left = parts[0].Trim() + " morte(s)";
                right = "-" + parts[1].Trim() + " pts";
                return;
            }

            if (string.Equals(categoryKey, "Skills", StringComparison.OrdinalIgnoreCase) && parts.Length >= 3)
            {
                left = parts[0].Trim() + "  •  ao alcançar nível " + parts[1].Trim();
                right = "+" + parts[2].Trim() + " pts";
                return;
            }


            bool shouldLocalizePrefabName =
                string.Equals(categoryKey, "Combate", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(categoryKey, "Combat", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(categoryKey, "Bosses", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(categoryKey, "Produção", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(categoryKey, "Conquistas", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(categoryKey, "Producao", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(categoryKey, "Production", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(categoryKey, "Achievements", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(categoryKey, "Cultivo", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(categoryKey, "Pesca", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(categoryKey, "Fishing", StringComparison.OrdinalIgnoreCase);

            string displayName = parts[0].Trim();
            if (shouldLocalizePrefabName)
                displayName = FriendlyRuleNameForHud(displayName);

            if (jackpotFormat && parts.Length >= 3)
            {
                left = displayName + "  •  " + parts[1].Trim() + "x";
                right = "+" + parts[2].Trim() + " pts";
                return;
            }

            left = displayName;
            right = "+" + parts[1].Trim() + " pts";
        }

        private List<string> SplitRulesForHud(string rules)
        {
            List<string> result = new List<string>();
            if (string.IsNullOrWhiteSpace(rules))
                return result;

            string[] parts = rules.Split(new char[] { ';', ',' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < parts.Length; i++)
            {
                string item = (parts[i] ?? "").Trim();
                if (!string.IsNullOrWhiteSpace(item))
                    result.Add(item);
            }

            return result;
        }

        private void DrawPlayerSummaryBlock(Rect rect)
        {
            if (_uiWhiteTexture != null)
            {
                GUI.DrawTexture(new Rect(rect.x, rect.y + 2f, rect.width, rect.height - 4f), _uiWhiteTexture, ScaleMode.StretchToFill, true, 0f, new Color(0.03f, 0.02f, 0.01f, 0.05f), 0f, 0f);
            }

            string playerName = string.IsNullOrWhiteSpace(_cachedPlayerData.PlayerName) ? GetLocalPlayerName() : _cachedPlayerData.PlayerName;
            DrawShadowLabel(new Rect(rect.x + 2f, rect.y + 2f, rect.width - 120f, 26f), playerName.ToUpperInvariant(), _panelHeadingStyle);
            DrawShadowLabel(new Rect(rect.x + 2f, rect.y + 30f, rect.width - 140f, 22f), ColorizeHighlight("Posição #" + Mathf.Max(1, _cachedPlayerData.Position)) + "  •  " + ColorizePoints(FormatPoints(_cachedPlayerData.Points)), _bodyValueStyle);

            Rect statusRect = new Rect(rect.xMax - 102f, rect.y + 15f, 96f, 20f);
            DrawPill(statusRect, GetRankTitle(_cachedPlayerData.Position), new Color(0.91f, 0.75f, 0.34f, 0.88f), new Color(0.18f, 0.11f, 0.03f, 0.88f));
        }

        private void DrawSectionHeader(Rect rect, string title)
        {
            DrawShadowLabel(rect, title, _panelHeadingStyle);
        }

        private void DrawBodyParagraph(Rect rect, string text)
        {
            DrawShadowLabel(rect, text, _mutedBodyStyle);
        }

        private void DrawSimpleInfoRows(Rect rect, string[] rows)
        {
            if (rows == null || rows.Length == 0)
                return;

            float rowHeight = Mathf.Max(24f, rect.height / Mathf.Max(1, rows.Length));
            for (int i = 0; i < rows.Length; i++)
            {
                string raw = rows[i] ?? "";
                int split = raw.IndexOf('|');
                string label = split >= 0 ? raw.Substring(0, split) : raw;
                string value = split >= 0 ? raw.Substring(split + 1) : "";

                Rect rowRect = new Rect(rect.x, rect.y + (rowHeight * i), rect.width, rowHeight);
                DrawInfoRow(rowRect, label, value);

            }
        }

        private void DrawRewardClaimSection(Rect rect)
        {
            if (_uiWhiteTexture != null)
            {
                GUI.DrawTexture(new Rect(rect.x, rect.y + 2f, rect.width, rect.height - 4f), _uiWhiteTexture, ScaleMode.StretchToFill, true, 0f, new Color(0.03f, 0.02f, 0.01f, 0.06f), 0f, 0f);
                GUI.DrawTexture(new Rect(rect.x, rect.y, 4f, rect.height), _uiWhiteTexture, ScaleMode.StretchToFill, true, 0f, new Color(0.91f, 0.75f, 0.34f, 0.88f), 0f, 0f);
            }

            SnapshotPlayerData data = _cachedPlayerData ?? new SnapshotPlayerData();
            string title = data.RewardRank >= 1 && data.RewardRank <= 3
                ? "Top " + data.RewardRank + " • " + (string.IsNullOrWhiteSpace(data.RewardLabel) ? "Recompensa" : data.RewardLabel)
                : "Recompensa bloqueada";

            float leftX = rect.x + 12f;
            float rightW = 144f;
            float rightX = rect.xMax - rightW - 12f;
            float textW = Mathf.Max(120f, rightX - leftX - 12f);

            DrawShadowLabel(new Rect(leftX, rect.y + 8f, rect.width - 24f, 20f), title, _bodyValueStyle);

            string itemLine = string.IsNullOrWhiteSpace(data.RewardPrefabName) || data.RewardAmount <= 0
                ? "Item ainda não configurado."
                : "Item: " + data.RewardPrefabName + " x" + data.RewardAmount;

            string requirementLine = data.RewardRank >= 1 && data.RewardRank <= 3
                ? "Exigência: " + data.RewardMinPoints + " pontos"
                : "Somente Top 1, 2 e 3 têm resgate.";

            DrawShadowLabel(new Rect(leftX, rect.y + 34f, textW, 18f), itemLine, _mutedBodyStyle);
            DrawShadowLabel(new Rect(leftX, rect.y + 54f, textW, 18f), requirementLine, _mutedBodyStyle);

            string blockText = string.IsNullOrWhiteSpace(data.RewardBlockReason)
                ? "Recompensa indisponível."
                : data.RewardBlockReason;

            DrawShadowLabel(new Rect(leftX, rect.y + 76f, textW, 34f), blockText, _configStyle);

            Rect buttonRect = new Rect(rightX, rect.y + 56f, rightW, 30f);
            bool allowButton = data.RewardCanClaim && !_rewardClaimRequestPending;

            if (allowButton)
            {
                Color previousBackgroundColor = GUI.backgroundColor;
                GUI.backgroundColor = new Color(0.22f, 0.72f, 0.28f, 1f);

                if (GUI.Button(buttonRect, "Resgatar"))
                    RequestRewardClaimFromServer();

                GUI.backgroundColor = previousBackgroundColor;
            }
            else
            {
                GUI.enabled = false;
                GUI.Button(buttonRect, _rewardClaimRequestPending ? "Processando..." : "Resgatar");
                GUI.enabled = true;
            }

            DrawPointsExchangeSection(new Rect(rect.x, rect.y + 122f, rect.width, 158f), data);
        }

        private string SanitizeNumericInput(string value, int maxDigits = 8)
        {
            if (string.IsNullOrEmpty(value))
                return "";

            StringBuilder builder = new StringBuilder(Mathf.Max(0, value.Length));
            for (int i = 0; i < value.Length && builder.Length < maxDigits; i++)
            {
                char c = value[i];
                if (c >= '0' && c <= '9')
                    builder.Append(c);
            }

            string result = builder.ToString().TrimStart('0');
            return string.IsNullOrEmpty(result) ? "" : result;
        }

        private void DrawPointsExchangeSection(Rect rect, SnapshotPlayerData data)
        {
            bool enabled = _rules != null && _rules.PointsExchangeEnabled;
            int points = data != null ? Mathf.Max(0, data.Points) : 0;
            int coinsPerPoint = _rules != null ? Mathf.Max(1, _rules.PointsExchangeCoinsPerPoint) : 1;
            int maxPoints = _rules != null ? Mathf.Max(0, _rules.PointsExchangeMaxPointsPerRequest) : 0;
            string prefabName = _rules != null && !string.IsNullOrWhiteSpace(_rules.PointsExchangePrefab) ? _rules.PointsExchangePrefab : "Coins";
            int maxSelectablePoints = maxPoints > 0 ? Mathf.Min(points, maxPoints) : points;

            if (string.IsNullOrWhiteSpace(_pointsExchangeAmountInput) && maxSelectablePoints > 0)
                _pointsExchangeAmountInput = maxSelectablePoints.ToString();

            int requestedPoints = 0;
            int.TryParse((_pointsExchangeAmountInput ?? "").Trim(), out requestedPoints);
            requestedPoints = Mathf.Clamp(requestedPoints, 0, Mathf.Max(0, maxSelectablePoints));
            int coins = Mathf.Max(0, requestedPoints * coinsPerPoint);

            float leftX = rect.x + 12f;
            float rightW = 144f;
            float rightX = rect.xMax - rightW - 12f;
            float gap = 8f;
            float inputW = 86f;
            float maxButtonW = 86f;

            DrawShadowLabel(new Rect(leftX, rect.y, rect.width - 24f, 20f), "Câmbio de pontos", _bodyValueStyle);

            string statusLine = enabled
                ? "Disponível: " + points + " pontos"
                : "Câmbio de pontos desativado.";

            DrawShadowLabel(new Rect(leftX, rect.y + 24f, rect.width - 24f, 18f), statusLine, _mutedBodyStyle);

            if (enabled)
            {
                DrawShadowLabel(new Rect(leftX, rect.y + 44f, rect.width - 24f, 18f), "Máximo por troca: " + maxSelectablePoints + " pontos", _mutedBodyStyle);
                DrawShadowLabel(new Rect(leftX, rect.y + 64f, rect.width - 24f, 18f), "Recebe: " + coins + " " + prefabName, _mutedBodyStyle);
            }

            Rect inputLabelRect = new Rect(leftX, rect.y + 92f, 52f, 18f);
            Rect inputRect = new Rect(inputLabelRect.xMax + 6f, rect.y + 86f, inputW, 30f);
            Rect allButtonRect = new Rect(inputRect.xMax + gap, rect.y + 86f, maxButtonW, 30f);


            Rect exchangeButtonRect = new Rect(rightX, rect.y + 124f, rightW, 30f);

            DrawShadowLabel(inputLabelRect, "Pontos:", _mutedBodyStyle);

            bool canEdit = enabled && !_pointsExchangeRequestPending && points > 0 && maxSelectablePoints > 0;
            GUI.enabled = canEdit;
            _pointsExchangeAmountInput = SanitizeNumericInput(GUI.TextField(inputRect, _pointsExchangeAmountInput ?? "", 8), 8);

            if (GUI.Button(allButtonRect, "Máximo"))
                _pointsExchangeAmountInput = maxSelectablePoints.ToString();
            GUI.enabled = true;

            bool allowButton = canEdit && requestedPoints > 0 && requestedPoints <= maxSelectablePoints && coins > 0;

            if (allowButton)
            {
                Color previousBackgroundColor = GUI.backgroundColor;
                GUI.backgroundColor = new Color(0.78f, 0.55f, 0.18f, 1f);

                if (GUI.Button(exchangeButtonRect, "Trocar"))
                    RequestPointsExchangeFromServer(requestedPoints);

                GUI.backgroundColor = previousBackgroundColor;
            }
            else
            {
                GUI.enabled = false;
                GUI.Button(exchangeButtonRect, _pointsExchangeRequestPending ? "Processando..." : "Trocar");
                GUI.enabled = true;
            }
        }

        private void DrawConfigSection(Rect rect, SnapshotPlayerData data)
        {
            string line1 = "Ranking " + (data != null && data.RankingEnabled ? "ON" : "OFF")
                         + "  •  Kills " + (data != null && data.EnableKillPoints ? "ON" : "OFF")
                         + "  •  Bosses " + (data != null && data.EnableBossPoints ? "ON" : "OFF");

            string line2 = "Skills " + (data != null && data.EnableSkillPoints ? "ON" : "OFF")
                         + "  •  Top exibido " + (data != null ? data.TopCount.ToString() : _rules.TopCount.ToString());

            DrawShadowLabel(new Rect(rect.x, rect.y, rect.width, 20f), line1, _configStyle);
            DrawShadowLabel(new Rect(rect.x, rect.y + 20f, rect.width, 20f), line2, _configStyle);
        }

        private void DrawInfoRow(Rect rect, string label, string value)
        {
            float valueWidth = Mathf.Clamp(rect.width * 0.28f, 74f, 130f);
            Rect labelRect = new Rect(rect.x, rect.y + 2f, rect.width - valueWidth - 12f, Mathf.Max(24f, rect.height - 4f));
            Rect valueRect = new Rect(rect.xMax - valueWidth - 4f, rect.y + 2f, valueWidth, Mathf.Max(24f, rect.height - 4f));

            DrawShadowLabel(labelRect, label, _bodyRowStyle);
            DrawShadowLabel(valueRect, ColorizePerformanceValue(label, value), _bodyValueStyle);
        }

        private string GetRankTitle(int position)
        {
            if (position <= 1) return "LENDÁRIO";
            if (position <= 3) return "GLORIOSO";
            if (position <= 10) return "ASCENDENTE";
            return "GUERREIRO";
        }

        private void DrawShadowLabel(Rect rect, string text, GUIStyle style)
        {
            DrawShadowLabel(rect, text, style, new Vector2(1f, 1f), new Color(0f, 0f, 0f, 0.92f));
        }

        private void DrawShadowLabel(Rect rect, string text, GUIStyle style, Vector2 shadowOffset, Color shadowColor)
        {
            if (style == null)
                return;

            GUIStyle shadowStyle = new GUIStyle(style);
            shadowStyle.normal.textColor = shadowColor;
            Rect shadowRect = new Rect(rect.x + shadowOffset.x, rect.y + shadowOffset.y, rect.width, rect.height);
            GUI.Label(shadowRect, text ?? "", shadowStyle);
            GUI.Label(rect, text ?? "", style);
        }

        private void DrawPill(Rect rect, string text, Color borderColor, Color fillColor)
        {
            if (_uiWhiteTexture == null)
                return;

            GUI.DrawTexture(rect, _uiWhiteTexture, ScaleMode.StretchToFill, true, 0f, fillColor, 0f, 0f);
            GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width, 1.5f), _uiWhiteTexture, ScaleMode.StretchToFill, true, 0f, borderColor, 0f, 0f);
            GUI.DrawTexture(new Rect(rect.x, rect.yMax - 1.5f, rect.width, 1.5f), _uiWhiteTexture, ScaleMode.StretchToFill, true, 0f, borderColor, 0f, 0f);
            GUI.DrawTexture(new Rect(rect.x, rect.y, 1.5f, rect.height), _uiWhiteTexture, ScaleMode.StretchToFill, true, 0f, borderColor, 0f, 0f);
            GUI.DrawTexture(new Rect(rect.xMax - 1.5f, rect.y, 1.5f, rect.height), _uiWhiteTexture, ScaleMode.StretchToFill, true, 0f, borderColor, 0f, 0f);

            DrawShadowLabel(rect, text, _youTagStyle);
        }

        private void DrawInnerCard(Rect rect, Color borderColor, float fillAlpha)
        {
            if (_uiWhiteTexture == null)
                return;

            GUI.DrawTexture(rect, _uiWhiteTexture, ScaleMode.StretchToFill, true, 0f, new Color(0.02f, 0.015f, 0.01f, 0.68f + fillAlpha), 0f, 0f);
            GUI.DrawTexture(new Rect(rect.x + 6f, rect.y + 6f, rect.width - 12f, rect.height - 12f), _uiWhiteTexture, ScaleMode.StretchToFill, true, 0f, new Color(0.07f, 0.045f, 0.02f, 0.52f), 0f, 0f);
            GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width, 1.5f), _uiWhiteTexture, ScaleMode.StretchToFill, true, 0f, borderColor, 0f, 0f);
            GUI.DrawTexture(new Rect(rect.x, rect.yMax - 1.5f, rect.width, 1.5f), _uiWhiteTexture, ScaleMode.StretchToFill, true, 0f, borderColor, 0f, 0f);
            GUI.DrawTexture(new Rect(rect.x, rect.y, 1.5f, rect.height), _uiWhiteTexture, ScaleMode.StretchToFill, true, 0f, borderColor, 0f, 0f);
            GUI.DrawTexture(new Rect(rect.xMax - 1.5f, rect.y, 1.5f, rect.height), _uiWhiteTexture, ScaleMode.StretchToFill, true, 0f, borderColor, 0f, 0f);
        }

        private Color GetRankAccentColor(int position)
        {
            switch (position)
            {
                case 1: return new Color(0.95f, 0.77f, 0.26f, 0.95f);
                case 2: return new Color(0.82f, 0.82f, 0.78f, 0.90f);
                case 3: return new Color(0.80f, 0.58f, 0.31f, 0.90f);
                default: return new Color(0.79f, 0.63f, 0.32f, 0.72f);
            }
        }

        private Font TryCreateFont(string preferredNames, int size)
        {
            if (string.IsNullOrWhiteSpace(preferredNames))
                return null;

            try
            {
                string[] names = preferredNames
                    .Split(new char[] { '|', ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(x => x.Trim())
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray();

                if (names.Length == 0)
                    return null;

                return Font.CreateDynamicFontFromOSFont(names, Mathf.Max(10, size));
            }
            catch
            {
                return null;
            }
        }

        private void ApplyFont(GUIStyle style, Font font)
        {
            if (style != null && font != null)
                style.font = font;
        }

        private void EnsureGuiStyles()
        {
            if (_stylesReady)
                return;

            if (_uiTransparentTexture == null)
                _uiTransparentTexture = CreateSolidTexture(new Color(0f, 0f, 0f, 0f));

            _uiTitleFont = _uiUseSystemFonts ? TryCreateFont(_uiTitleFontNames, 26) : null;
            _uiBodyFont = _uiUseSystemFonts ? TryCreateFont(_uiBodyFontNames, 15) : null;
            _uiAccentFont = _uiUseSystemFonts ? TryCreateFont(_uiAccentFontNames, 16) : null;

            _windowStyle = new GUIStyle(GUI.skin.window);
            _windowStyle.normal.background = _uiTransparentTexture;
            _windowStyle.active.background = _uiTransparentTexture;
            _windowStyle.hover.background = _uiTransparentTexture;
            _windowStyle.focused.background = _uiTransparentTexture;
            _windowStyle.border = new RectOffset(0, 0, 0, 0);
            _windowStyle.padding = new RectOffset(0, 0, 0, 0);

            _headerStyle = new GUIStyle(GUI.skin.label);
            _headerStyle.fontSize = 26;
            _headerStyle.fontStyle = FontStyle.Bold;
            _headerStyle.alignment = TextAnchor.MiddleCenter;
            _headerStyle.wordWrap = true;
            _headerStyle.richText = true;
            _headerStyle.normal.textColor = new Color(0.98f, 0.92f, 0.78f, 1f);
            ApplyFont(_headerStyle, _uiTitleFont);

            _sectionTitleStyle = new GUIStyle(_headerStyle);
            _sectionTitleStyle.fontSize = 21;
            ApplyFont(_sectionTitleStyle, _uiTitleFont);

            _rankingTextStyle = new GUIStyle(GUI.skin.label);
            _rankingTextStyle.fontSize = 15;
            _rankingTextStyle.wordWrap = true;
            _rankingTextStyle.richText = true;
            _rankingTextStyle.alignment = TextAnchor.UpperLeft;
            _rankingTextStyle.normal.textColor = new Color(0.97f, 0.92f, 0.82f, 1f);
            _rankingTextStyle.padding = new RectOffset(2, 2, 0, 0);
            ApplyFont(_rankingTextStyle, _uiBodyFont);

            _playerTextStyle = new GUIStyle(_rankingTextStyle);
            _playerTextStyle.fontSize = 14;
            ApplyFont(_playerTextStyle, _uiBodyFont);

            _panelHeadingStyle = new GUIStyle(GUI.skin.label);
            _panelHeadingStyle.fontSize = 15;
            _panelHeadingStyle.fontStyle = FontStyle.Bold;
            _panelHeadingStyle.alignment = TextAnchor.MiddleLeft;
            _panelHeadingStyle.normal.textColor = new Color(0.96f, 0.82f, 0.47f, 1f);
            _panelHeadingStyle.richText = true;
            ApplyFont(_panelHeadingStyle, _uiAccentFont != null ? _uiAccentFont : _uiTitleFont);

            _cardLabelStyle = new GUIStyle(GUI.skin.label);
            _cardLabelStyle.fontSize = 11;
            _cardLabelStyle.alignment = TextAnchor.UpperLeft;
            _cardLabelStyle.fontStyle = FontStyle.Bold;
            _cardLabelStyle.normal.textColor = new Color(0.86f, 0.73f, 0.48f, 0.95f);
            ApplyFont(_cardLabelStyle, _uiAccentFont != null ? _uiAccentFont : _uiTitleFont);

            _cardValueStyle = new GUIStyle(GUI.skin.label);
            _cardValueStyle.fontSize = 24;
            _cardValueStyle.alignment = TextAnchor.UpperLeft;
            _cardValueStyle.fontStyle = FontStyle.Bold;
            _cardValueStyle.normal.textColor = new Color(0.98f, 0.93f, 0.84f, 1f);
            ApplyFont(_cardValueStyle, _uiTitleFont);

            _bodyRowStyle = new GUIStyle(GUI.skin.label);
            _bodyRowStyle.fontSize = 13;
            _bodyRowStyle.alignment = TextAnchor.MiddleLeft;
            _bodyRowStyle.normal.textColor = new Color(0.96f, 0.90f, 0.78f, 1f);
            _bodyRowStyle.clipping = TextClipping.Overflow;
            _bodyRowStyle.wordWrap = false;
            _bodyRowStyle.richText = true;
            ApplyFont(_bodyRowStyle, _uiBodyFont);

            _bodyValueStyle = new GUIStyle(_bodyRowStyle);
            _bodyValueStyle.fontSize = 13;
            _bodyValueStyle.fontStyle = FontStyle.Bold;
            _bodyValueStyle.alignment = TextAnchor.MiddleRight;
            _bodyValueStyle.normal.textColor = new Color(0.98f, 0.92f, 0.80f, 1f);
            _bodyValueStyle.clipping = TextClipping.Overflow;
            _bodyValueStyle.wordWrap = false;
            _bodyValueStyle.richText = true;
            ApplyFont(_bodyValueStyle, _uiAccentFont != null ? _uiAccentFont : _uiBodyFont);

            _mutedBodyStyle = new GUIStyle(_bodyRowStyle);
            _mutedBodyStyle.fontSize = 13;
            _mutedBodyStyle.wordWrap = true;
            _mutedBodyStyle.normal.textColor = new Color(0.86f, 0.79f, 0.67f, 0.95f);
            _mutedBodyStyle.clipping = TextClipping.Overflow;
            _mutedBodyStyle.richText = true;
            ApplyFont(_mutedBodyStyle, _uiBodyFont);

            _rankIndexStyle = new GUIStyle(GUI.skin.label);
            _rankIndexStyle.fontSize = 18;
            _rankIndexStyle.fontStyle = FontStyle.Bold;
            _rankIndexStyle.alignment = TextAnchor.MiddleLeft;
            _rankIndexStyle.normal.textColor = new Color(0.97f, 0.82f, 0.33f, 1f);
            ApplyFont(_rankIndexStyle, _uiTitleFont);

            _rankNameStyle = new GUIStyle(GUI.skin.label);
            _rankNameStyle.fontSize = 16;
            _rankNameStyle.fontStyle = FontStyle.Bold;
            _rankNameStyle.alignment = TextAnchor.MiddleLeft;
            _rankNameStyle.clipping = TextClipping.Clip;
            _rankNameStyle.normal.textColor = new Color(0.95f, 0.89f, 0.77f, 1f);
            _rankNameStyle.richText = true;
            ApplyFont(_rankNameStyle, _uiAccentFont != null ? _uiAccentFont : _uiTitleFont);

            _rankPointsStyle = new GUIStyle(GUI.skin.label);
            _rankPointsStyle.fontSize = 12;
            _rankPointsStyle.alignment = TextAnchor.MiddleLeft;
            _rankPointsStyle.normal.textColor = new Color(0.88f, 0.78f, 0.60f, 0.95f);
            _rankPointsStyle.richText = true;
            ApplyFont(_rankPointsStyle, _uiBodyFont);

            _youTagStyle = new GUIStyle(GUI.skin.label);
            _youTagStyle.fontSize = 10;
            _youTagStyle.alignment = TextAnchor.MiddleCenter;
            _youTagStyle.fontStyle = FontStyle.Bold;
            _youTagStyle.normal.textColor = new Color(0.86f, 0.97f, 0.82f, 1f);
            ApplyFont(_youTagStyle, _uiAccentFont != null ? _uiAccentFont : _uiBodyFont);

            _emptyStateStyle = new GUIStyle(GUI.skin.label);
            _emptyStateStyle.fontSize = 16;
            _emptyStateStyle.wordWrap = true;
            _emptyStateStyle.alignment = TextAnchor.UpperLeft;
            _emptyStateStyle.normal.textColor = new Color(0.95f, 0.89f, 0.78f, 0.95f);
            ApplyFont(_emptyStateStyle, _uiBodyFont);

            _configStyle = new GUIStyle(GUI.skin.label);
            _configStyle.fontSize = 12;
            _configStyle.alignment = TextAnchor.UpperLeft;
            _configStyle.normal.textColor = new Color(0.88f, 0.82f, 0.73f, 0.94f);
            _configStyle.clipping = TextClipping.Overflow;
            _configStyle.richText = true;
            ApplyFont(_configStyle, _uiBodyFont);

            _footerStyle = new GUIStyle(GUI.skin.label);
            _footerStyle.fontSize = 13;
            _footerStyle.alignment = TextAnchor.MiddleCenter;
            _footerStyle.richText = true;
            _footerStyle.normal.textColor = new Color(0.95f, 0.89f, 0.76f, 0.95f);
            ApplyFont(_footerStyle, _uiBodyFont);

            _statusStyle = new GUIStyle(GUI.skin.label);
            _statusStyle.fontSize = 13;
            _statusStyle.fontStyle = FontStyle.Bold;
            _statusStyle.alignment = TextAnchor.MiddleCenter;
            _statusStyle.normal.textColor = new Color(0.95f, 0.89f, 0.76f, 1f);
            ApplyFont(_statusStyle, _uiBodyFont);

            _iconButtonStyle = new GUIStyle(GUI.skin.button);
            _iconButtonStyle.normal.background = _uiTransparentTexture;
            _iconButtonStyle.active.background = _uiTransparentTexture;
            _iconButtonStyle.hover.background = _uiTransparentTexture;
            _iconButtonStyle.focused.background = _uiTransparentTexture;
            _iconButtonStyle.border = new RectOffset(0, 0, 0, 0);
            _iconButtonStyle.padding = new RectOffset(0, 0, 0, 0);
            _iconButtonStyle.margin = new RectOffset(0, 0, 0, 0);

            _stylesReady = true;
        }
    }
}
