using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace Glitnir.Ranking.Patches
{

    [HarmonyPatch(typeof(InventoryGui), "Update")]
    public static class Glitnir_AAA_CraftMaterialFix_Update
    {
        private static void Postfix()
        {
            Glitnir_AAA_CraftMaterialFix_Core.ClampInventoryGui(false);
        }
    }

    [HarmonyPatch(typeof(InventoryGui), "DoCrafting")]
    public static class Glitnir_AAA_CraftMaterialFix_DoCrafting
    {
        private static void Prefix()
        {
            Glitnir_AAA_CraftMaterialFix_Core.ClampInventoryGui(true);
        }
    }


    [HarmonyPatch]
    public static class Glitnir_AAA_CraftMaterialFix_RefreshHooks
    {
        private static IEnumerable<MethodBase> TargetMethods()
        {
            string[] methodNames =
            {
                "UpdateRecipe",
                "SetRecipe",
                "SelectRecipe",
                "UpdateCraftingPanel",
                "UpdateRecipeList"
            };

            foreach (string methodName in methodNames)
            {
                MethodInfo method = AccessTools.Method(typeof(InventoryGui), methodName);
                if (method != null)
                    yield return method;
            }
        }

        private static void Postfix()
        {
            Glitnir_AAA_CraftMaterialFix_Core.MarkDirty();
            Glitnir_AAA_CraftMaterialFix_Core.ClampInventoryGui(true);
        }
    }

    public static class Glitnir_AAA_CraftMaterialFix_Core
    {
        private const int ABSOLUTE_MAX_CRAFT_AMOUNT = 9999;
        private static float _nextSoftCheck;
        private static int _lastRecipeHash;
        private static int _lastStationHash;
        private static bool _dirty = true;

        public static void MarkDirty()
        {
            _dirty = true;
            _nextSoftCheck = 0f;
        }

        public static void ClampInventoryGui(bool force)
        {
            try
            {
                if (InventoryGui.instance == null) return;
                if (!InventoryGui.IsVisible()) return;

                object recipe = GetSelectedRecipe(InventoryGui.instance);
                object station = GetCurrentCraftingStation(InventoryGui.instance);

                int recipeHash = recipe != null ? recipe.GetHashCode() : 0;
                int stationHash = station != null ? station.GetHashCode() : 0;

                if (recipeHash != _lastRecipeHash || stationHash != _lastStationHash)
                {
                    _lastRecipeHash = recipeHash;
                    _lastStationHash = stationHash;
                    _dirty = true;
                    force = true;
                }

                if (!force && !_dirty && Time.time < _nextSoftCheck)
                    return;

                _nextSoftCheck = Time.time + 0.05f;
                _dirty = false;

                if (recipe == null)
                    recipe = GetRecipeFromVisibleCraftButtonOrPanel(InventoryGui.instance.gameObject);

                int maxCraftAmount = Mathf.Clamp(GetMaxCraftAmountSafe(recipe), 1, ABSOLUTE_MAX_CRAFT_AMOUNT);

                GameObject root = InventoryGui.instance.gameObject;
                RaiseKnownMaxCraftAmountFields(InventoryGui.instance, maxCraftAmount);
                UpdateVisibleMaxCraftLabels(root, maxCraftAmount);
                ClampCraftAmountInputs(root, maxCraftAmount);
                ClampKnownCraftAmountFields(InventoryGui.instance, maxCraftAmount);
            }
            catch
            {
                // Nao quebrar a UI caso algum mod altere o InventoryGui.
            }
        }

        private static void ClampCraftAmountInputs(GameObject root, int maxCraftAmount)
        {
            int max = Mathf.Max(1, maxCraftAmount);

            foreach (Component component in root.GetComponentsInChildren<Component>(true))
            {
                if (component == null) continue;
                if (!IsInputField(component)) continue;
                if (!LooksLikeCraftAmountInput(component)) continue;

                string rawText = GetText(component);

                if (!int.TryParse(rawText, out int currentAmount))
                    currentAmount = 1;

                int clamped = Mathf.Clamp(currentAmount, 1, max);

                if (rawText != clamped.ToString())
                {
                    SetText(component, clamped.ToString());
                    InvokeNoArg(component, "ForceLabelUpdate");
                    InvokeNoArg(component, "UpdateLabel");
                }
            }
        }

        private static void RaiseKnownMaxCraftAmountFields(object inventoryGui, int maxCraftAmount)
        {
            int max = Mathf.Clamp(maxCraftAmount, 1, ABSOLUTE_MAX_CRAFT_AMOUNT);

            string[] possibleMaxFieldNames =
            {
                "m_maxCraftAmount",
                "m_maxCraftingAmount",
                "m_multiCraftMaxAmount",
                "m_maxMultiCraftAmount",
                "m_craftMaxAmount",
                "m_maxAmount"
            };

            foreach (string fieldName in possibleMaxFieldNames)
            {
                object value = GetFieldOrProperty(inventoryGui, fieldName);
                if (value == null) continue;

                try
                {
                    if (value is int)
                    {
                        SetFieldOrProperty(inventoryGui, fieldName, max);
                    }
                    else if (value is float)
                    {
                        SetFieldOrProperty(inventoryGui, fieldName, (float)max);
                    }
                    else if (value is double)
                    {
                        SetFieldOrProperty(inventoryGui, fieldName, (double)max);
                    }
                }
                catch
                {
                }
            }
        }

        private static void UpdateVisibleMaxCraftLabels(GameObject root, int maxCraftAmount)
        {
            string expected = "Max: " + Mathf.Clamp(maxCraftAmount, 1, ABSOLUTE_MAX_CRAFT_AMOUNT);

            foreach (Component component in root.GetComponentsInChildren<Component>(true))
            {
                if (component == null) continue;

                string text = GetText(component);
                if (string.IsNullOrEmpty(text)) continue;

                string trimmed = text.Trim();

                if (!trimmed.StartsWith("Max:", StringComparison.OrdinalIgnoreCase))
                    continue;

                SetText(component, expected);
                InvokeNoArg(component, "ForceLabelUpdate");
                InvokeNoArg(component, "UpdateLabel");
            }
        }

        private static void ClampKnownCraftAmountFields(object inventoryGui, int maxCraftAmount)
        {
            int max = Mathf.Max(1, maxCraftAmount);

            string[] possibleFieldNames =
            {
                "m_craftAmount",
                "m_multiCraftAmount",
                "m_multiCraftingAmount",
                "m_selectedAmount",
                "m_amount"
            };

            foreach (string fieldName in possibleFieldNames)
            {
                object value = GetFieldOrProperty(inventoryGui, fieldName);
                if (value == null) continue;

                if (!int.TryParse(value.ToString(), out int current))
                    continue;

                int clamped = Mathf.Clamp(current, 1, max);

                if (clamped != current)
                    SetFieldOrProperty(inventoryGui, fieldName, clamped);
            }
        }

        private static bool LooksLikeCraftAmountInput(Component component)
        {
            string text = GetText(component);

            if (!int.TryParse(text, out _))
                return false;

            Transform parent = component.transform.parent;

            if (parent == null)
                return false;

            bool hasMinus = false;
            bool hasPlus = false;

            foreach (Button button in parent.GetComponentsInChildren<Button>(true))
            {
                if (button == null) continue;

                if (IsMinusButton(button.gameObject))
                    hasMinus = true;

                if (IsPlusButton(button.gameObject))
                    hasPlus = true;
            }

            return hasMinus && hasPlus;
        }

        private static int GetMaxCraftAmountSafe(object recipe)
        {
            try
            {
                if (recipe == null)
                    return 1;

                object requirements = GetFieldOrProperty(recipe, "m_resources");

                if (requirements == null)
                    return 1;

                int maxAmount = int.MaxValue;
                bool foundMaterial = false;

                foreach (object requirement in (IEnumerable)requirements)
                {
                    if (requirement == null) continue;

                    object itemDrop = GetFieldOrProperty(requirement, "m_resItem");

                    if (itemDrop == null)
                        continue;

                    int amountPerCraft = GetIntFieldOrProperty(requirement, "m_amount", 1);

                    if (amountPerCraft <= 0)
                        continue;

                    string itemName = GetItemSharedName(itemDrop);

                    if (string.IsNullOrEmpty(itemName))
                        continue;

                    int available = CountPlayerItem(itemName);
                    int possible = available / amountPerCraft;

                    if (possible < maxAmount)
                        maxAmount = possible;

                    foundMaterial = true;
                }

                if (!foundMaterial || maxAmount == int.MaxValue)
                    return 1;

                return Mathf.Clamp(maxAmount, 0, ABSOLUTE_MAX_CRAFT_AMOUNT);
            }
            catch
            {
                return 1;
            }
        }

        private static object GetSelectedRecipe(InventoryGui inventoryGui)
        {
            string[] possibleNames =
            {
                "m_selectedRecipe",
                "m_craftRecipe",
                "m_currentRecipe",
                "m_recipe"
            };

            foreach (string name in possibleNames)
            {
                object value = GetFieldOrProperty(inventoryGui, name);
                object recipe = ExtractRecipe(value);

                if (recipe != null)
                    return recipe;
            }

            object recipeFromFields = FindRecipeInsideObject(inventoryGui, 0);
            if (recipeFromFields != null)
                return recipeFromFields;

            return null;
        }

        private static object GetRecipeFromVisibleCraftButtonOrPanel(GameObject root)
        {

            foreach (Component component in root.GetComponentsInChildren<Component>(true))
            {
                if (component == null) continue;

                object recipe = FindRecipeInsideObject(component, 0);
                if (recipe != null)
                    return recipe;
            }

            return null;
        }

        private static object ExtractRecipe(object value)
        {
            if (value == null)
                return null;

            if (value.GetType().Name == "Recipe")
                return value;

            return FindRecipeInsideObject(value, 0);
        }

        private static object FindRecipeInsideObject(object obj, int depth)
        {
            if (obj == null || depth > 2)
                return null;

            Type type = obj.GetType();

            if (type.Name == "Recipe")
                return obj;

            foreach (FieldInfo field in type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            {
                object value = null;

                try { value = field.GetValue(obj); }
                catch { }

                if (value == null)
                    continue;

                if (value.GetType().Name == "Recipe")
                    return value;

                // KeyValuePair<Recipe, ItemData> ou wrappers similares.
                if (field.Name == "Key" || field.Name == "Value" || field.Name.ToLowerInvariant().Contains("recipe"))
                {
                    object nested = FindRecipeInsideObject(value, depth + 1);
                    if (nested != null)
                        return nested;
                }
            }

            foreach (PropertyInfo prop in type.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            {
                if (!prop.CanRead)
                    continue;

                object value = null;

                try { value = prop.GetValue(obj, null); }
                catch { }

                if (value == null)
                    continue;

                if (value.GetType().Name == "Recipe")
                    return value;

                if (prop.Name == "Key" || prop.Name == "Value" || prop.Name.ToLowerInvariant().Contains("recipe"))
                {
                    object nested = FindRecipeInsideObject(value, depth + 1);
                    if (nested != null)
                        return nested;
                }
            }

            return null;
        }

        private static object GetCurrentCraftingStation(InventoryGui inventoryGui)
        {
            string[] possibleNames =
            {
                "m_currentCraftingStation",
                "m_craftingStation",
                "m_station",
                "m_currentStation"
            };

            foreach (string name in possibleNames)
            {
                object value = GetFieldOrProperty(inventoryGui, name);
                if (value != null)
                    return value;
            }

            return null;
        }

        private static int CountPlayerItem(string sharedName)
        {
            try
            {
                if (Player.m_localPlayer == null)
                    return 0;

                Inventory inventory = Player.m_localPlayer.GetInventory();

                if (inventory == null)
                    return 0;

                return inventory.CountItems(sharedName);
            }
            catch
            {
                return 0;
            }
        }

        private static string GetItemSharedName(object itemDrop)
        {
            object itemData = GetFieldOrProperty(itemDrop, "m_itemData");
            object shared = GetFieldOrProperty(itemData, "m_shared");
            object name = GetFieldOrProperty(shared, "m_name");

            return name?.ToString();
        }

        private static bool IsInputField(Component component)
        {
            string typeName = component.GetType().Name;
            return typeName == "InputField" || typeName == "TMP_InputField";
        }

        private static bool IsPlusButton(GameObject obj)
        {
            string name = obj.name.ToLowerInvariant();

            if (name.Contains("search"))
                return false;

            return name.Contains("plus") || HasText(obj, "+");
        }

        private static bool IsMinusButton(GameObject obj)
        {
            string name = obj.name.ToLowerInvariant();

            if (name.Contains("search"))
                return false;

            return name.Contains("minus") || HasText(obj, "-");
        }

        private static bool HasText(GameObject obj, string expected)
        {
            foreach (Component component in obj.GetComponentsInChildren<Component>(true))
            {
                if (component == null) continue;

                string value = GetText(component);

                if (value != null && value.Trim() == expected)
                    return true;
            }

            return false;
        }

        private static string GetText(object obj)
        {
            try
            {
                object value = GetFieldOrProperty(obj, "text");
                return value?.ToString();
            }
            catch
            {
                return null;
            }
        }

        private static void SetText(object obj, string value)
        {
            try
            {
                PropertyInfo prop = obj.GetType().GetProperty("text");

                if (prop != null && prop.CanWrite)
                    prop.SetValue(obj, value, null);
            }
            catch
            {
            }
        }

        private static void InvokeNoArg(object obj, string methodName)
        {
            try
            {
                MethodInfo method = obj.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (method != null && method.GetParameters().Length == 0)
                    method.Invoke(obj, null);
            }
            catch
            {
            }
        }

        private static int GetIntFieldOrProperty(object obj, string name, int fallback)
        {
            object value = GetFieldOrProperty(obj, name);

            if (value == null)
                return fallback;

            try
            {
                return Convert.ToInt32(value);
            }
            catch
            {
                return fallback;
            }
        }

        private static object GetFieldOrProperty(object obj, string name)
        {
            if (obj == null)
                return null;

            Type type = obj.GetType();

            FieldInfo field = type.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            if (field != null)
                return field.GetValue(obj);

            PropertyInfo prop = type.GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            if (prop != null && prop.CanRead)
                return prop.GetValue(obj, null);

            return null;
        }

        private static void SetFieldOrProperty(object obj, string name, object value)
        {
            if (obj == null)
                return;

            Type type = obj.GetType();

            FieldInfo field = type.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            if (field != null)
            {
                field.SetValue(obj, value);
                return;
            }

            PropertyInfo prop = type.GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            if (prop != null && prop.CanWrite)
                prop.SetValue(obj, value, null);
        }
    }
}
