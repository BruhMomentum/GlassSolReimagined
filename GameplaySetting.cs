using System.Collections;
using System.Reflection;
using HarmonyLib;
using TMPro;
using UnityEngine;

namespace GlassSol.Patches
{
    internal static class InternalKillSetting
    {
        public static bool Kills()
        {
            int mode = Difficulty.Current();
            int fallback = mode == Difficulty.TrueGlassSol ? 1 : 0;
            return PlayerPrefs.GetInt("GlassSol.InternalKills." + mode, fallback) == 1;
        }

        public static void Set(bool kills)
        {
            PlayerPrefs.SetInt("GlassSol.InternalKills." + Difficulty.Current(), kills ? 1 : 0);
        }
    }

    internal static class GameplaySettingPatch
    {
        const string OptionName = "InternalKillOption";
        internal const string TitleRu = "Внутренний урон убивает";
        internal const string TitleEn = "Internal damage kills";

        [HarmonyPostfix]
        [HarmonyPatch(typeof(GamePreferenceManager), "Start")]
        static void AddOption(GamePreferenceManager __instance)
        {
            __instance.StartCoroutine(Watch());
        }

        static IEnumerator Watch()
        {
            while (true)
            {
                foreach (Transform row in Resources.FindObjectsOfTypeAll<Transform>())
                {
                    if (row == null || row.name != "Show HUD" || !row.gameObject.scene.IsValid())
                        continue;
                    if (row.parent == null || row.parent.name != "Layout" || row.parent.Find(OptionName) != null)
                        continue;

                    AddRow(row);
                }

                yield return new WaitForSecondsRealtime(0.5f);
            }
        }

        static void AddRow(Transform row)
        {
            bool wasActive = row.gameObject.activeSelf;
            row.gameObject.SetActive(false);
            Transform copy = Object.Instantiate(row.gameObject, row.parent).transform;
            row.gameObject.SetActive(wasActive);

            copy.name = OptionName;
            copy.SetSiblingIndex(row.GetSiblingIndex() + 1);

            Component selector = null;
            foreach (MonoBehaviour behaviour in copy.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (behaviour == null)
                    continue;

                for (System.Type type = behaviour.GetType(); type != null; type = type.BaseType)
                {
                    if (type.Name == "MultipleOptionSelector" || type.Name.StartsWith("ListRotateSelector"))
                    {
                        selector = behaviour;
                        break;
                    }
                }

                if (selector != null)
                    break;
            }

            if (selector == null)
            {
                Plugin.Log.LogError("Show HUD has no selector");
                return;
            }

            object entry = null;
            for (System.Type type = selector.GetType(); type != null; type = type.BaseType)
            {
                FieldInfo field = type.GetField("entry", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                if (field == null)
                    continue;

                entry = field.GetValue(selector);
                break;
            }

            entry.GetType().GetField("flagBase").SetValue(entry, null);

            copy.gameObject.SetActive(true);
            TMP_Text title = null;
            foreach (Transform transform in copy.GetComponentsInChildren<Transform>(true))
            {
                if (transform.name != "Label")
                    continue;

                title = transform.GetComponent<TMP_Text>() ?? transform.GetComponentInChildren<TMP_Text>(true);
                break;
            }

            if (title != null)
            {
                DestroyLocalization(title.gameObject);
                title.text = Difficulty.Phrase(TitleRu, TitleEn);
            }

            UIControlButton control = copy.GetComponent<UIControlButton>() ?? copy.GetComponentInChildren<UIControlButton>(true);
            RetargetLocal(control, copy);
            control.RefreshState();

            object list = selector.GetType().GetField("list", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).GetValue(selector);
            PropertyInfo count = list.GetType().GetProperty("Count");
            PropertyInfo item = list.GetType().GetProperty("Item");
            int yesIndex = 1;
            int total = (int)count.GetValue(list, null);
            for (int i = 0; i < total; i++)
            {
                object value = item.GetValue(list, new object[] { i });
                string choice = value != null ? value.ToString() : null;
                if (choice == "Да" || choice == "Yes")
                {
                    yesIndex = i;
                    break;
                }
            }

            InternalKillOption option = copy.gameObject.AddComponent<InternalKillOption>();
            option.Setup(selector, entry, yesIndex, title);

            SelectableNavigationProvider navigation = copy.GetComponentInParent<SelectableNavigationProvider>();
            if (navigation != null)
                navigation.AutoNavigateBindForAll();

            Plugin.Log.LogInfo("Added internal damage setting");
        }

        static void RetargetLocal(UIControlButton control, Transform root)
        {
            foreach (FieldInfo field in typeof(UIControlButton).GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            {
                if (field.FieldType == typeof(RectTransform) || !typeof(Component).IsAssignableFrom(field.FieldType))
                    continue;
                if (field.Name == "belongGroup" || field.Name == "provider" || field.Name == "instructionPanelProvider")
                    continue;

                Component current = field.GetValue(control) as Component;
                if (current != null && (current.transform == root || current.transform.IsChildOf(root)))
                    continue;

                Component local = control.GetComponent(field.FieldType) ?? control.GetComponentInChildren(field.FieldType, true);
                if (local != null)
                    field.SetValue(control, local);
            }
        }

        internal static void DestroyLocalization(GameObject root)
        {
            foreach (MonoBehaviour behaviour in root.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (behaviour != null && behaviour.GetType().Name == "Localize")
                    Object.DestroyImmediate(behaviour);
            }
        }
    }

    internal class InternalKillOption : MonoBehaviour
    {
        Component selector;
        object entry;
        int yesIndex;
        TMP_Text title;

        public void Setup(Component selector, object entry, int yesIndex, TMP_Text title)
        {
            this.selector = selector;
            this.entry = entry;
            this.yesIndex = yesIndex;
            this.title = title;
        }

        void OnEnable()
        {
            if (!Difficulty.IsOneShot())
            {
                gameObject.SetActive(false);
                return;
            }

            ApplyTitle();
            Value(InternalKillSetting.Kills() ? yesIndex : (yesIndex == 0 ? 1 : 0));
            selector.GetType().GetMethod("UpdateView").Invoke(selector, null);
            ApplyTitle();
        }

        void LateUpdate()
        {
            if (!Difficulty.IsOneShot())
                return;

            bool kills = Value() == yesIndex;
            if (kills != InternalKillSetting.Kills())
                InternalKillSetting.Set(kills);
            ApplyTitle();
        }

        void ApplyTitle()
        {
            if (title == null)
                return;

            GameplaySettingPatch.DestroyLocalization(title.gameObject);
            title.text = Difficulty.Phrase(GameplaySettingPatch.TitleRu, GameplaySettingPatch.TitleEn);
        }

        int Value(int? next = null)
        {
            object field = entry.GetType().GetMethod("get_field").Invoke(entry, null);
            if (next == null)
                return (int)field.GetType().GetMethod("get_CurrentValue").Invoke(field, null);

            field.GetType().GetMethod("set_CurrentValue").Invoke(field, new object[] { next.Value });
            return next.Value;
        }
    }
}
