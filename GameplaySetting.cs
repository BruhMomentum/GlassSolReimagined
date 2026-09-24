using System.Collections;
using System.Reflection;
using HarmonyLib;
using TMPro;
using UnityEngine;

namespace GlassSol.Patches
{
    internal static class InternalKillSetting
    {
        public static bool InMemory()
        {
            PlayerGamePlayData data = PlayerGamePlayData.Instance;
            return data != null && data.memoryMode != null && data.memoryMode.CurrentValue;
        }

        public static bool Shown()
        {
            return Difficulty.IsOneShot() || InMemory();
        }

        public static bool Kills()
        {
            int fallback = !InMemory() && Difficulty.Current() == Difficulty.TrueGlassSol ? 1 : 0;
            return PlayerPrefs.GetInt(Key(), fallback) == 1;
        }

        public static void Set(bool kills)
        {
            PlayerPrefs.SetInt(Key(), kills ? 1 : 0);
        }

        static string Key()
        {
            if (InMemory())
                return "GlassSol.InternalKills.memory";

            return "GlassSol.InternalKills." + Difficulty.Current();
        }
    }

    internal static class NohitSetting
    {
        public static bool Enabled()
        {
            return PlayerPrefs.GetInt("GlassSol.Nohit", 0) == 1;
        }

        public static void Set(bool enabled)
        {
            PlayerPrefs.SetInt("GlassSol.Nohit", enabled ? 1 : 0);
        }

        public static bool Active()
        {
            return Enabled() && InternalKillSetting.InMemory();
        }
    }

    internal static class GameplaySettingPatch
    {
        const string OptionName = "InternalKillOption";
        const string NohitOptionName = "NohitOption";
        internal const string TitleRu = "Внутренний урон убивает";
        internal const string TitleEn = "Internal damage kills";
        internal const string NohitTitleRu = "Включить ноухит";
        internal const string NohitTitleEn = "Enable Nohit";

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
                    if (row.parent == null || row.parent.name != "Layout")
                        continue;
                    Transform internalRow = row.parent.Find(OptionName);
                    if (internalRow == null)
                        AddRow(row, false);
                    else
                        internalRow.gameObject.SetActive(InternalKillSetting.Shown());

                    Transform nohit = row.parent.Find(NohitOptionName);
                    if (nohit == null)
                        AddRow(row, true);
                    else
                        nohit.gameObject.SetActive(InternalKillSetting.InMemory());
                }

                yield return new WaitForSecondsRealtime(0.5f);
            }
        }

        static void AddRow(Transform row, bool nohit)
        {
            bool wasActive = row.gameObject.activeSelf;
            row.gameObject.SetActive(false);
            Transform copy = Object.Instantiate(row.gameObject, row.parent).transform;
            row.gameObject.SetActive(wasActive);

            copy.name = nohit ? NohitOptionName : OptionName;
            copy.SetSiblingIndex(row.GetSiblingIndex() + (nohit ? 2 : 1));

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
                title.text = nohit ? Difficulty.Phrase(NohitTitleRu, NohitTitleEn) : Difficulty.Phrase(TitleRu, TitleEn);
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

            if (nohit)
            {
                NohitOption option = copy.gameObject.AddComponent<NohitOption>();
                option.Setup(selector, entry, yesIndex, title);
            }
            else
            {
                InternalKillOption option = copy.gameObject.AddComponent<InternalKillOption>();
                option.Setup(selector, entry, yesIndex, title);
            }

            SelectableNavigationProvider navigation = copy.GetComponentInParent<SelectableNavigationProvider>();
            if (navigation != null)
                navigation.AutoNavigateBindForAll();

            Plugin.Log.LogInfo(nohit ? "Added nohit setting" : "Added internal damage setting");
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
            if (!InternalKillSetting.Shown())
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
            if (!InternalKillSetting.Shown())
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

    internal class NohitOption : MonoBehaviour
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
            if (!InternalKillSetting.InMemory())
            {
                gameObject.SetActive(false);
                return;
            }

            ApplyTitle();
            Value(NohitSetting.Enabled() ? yesIndex : (yesIndex == 0 ? 1 : 0));
            selector.GetType().GetMethod("UpdateView").Invoke(selector, null);
            ApplyTitle();
        }

        void LateUpdate()
        {
            bool enabled = Value() == yesIndex;
            if (enabled != NohitSetting.Enabled())
                NohitSetting.Set(enabled);
            ApplyTitle();
        }

        void ApplyTitle()
        {
            if (title == null)
                return;

            GameplaySettingPatch.DestroyLocalization(title.gameObject);
            title.text = Difficulty.Phrase(GameplaySettingPatch.NohitTitleRu, GameplaySettingPatch.NohitTitleEn);
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
