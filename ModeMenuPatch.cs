using System.Collections.Generic;
using System.Reflection;
using _3_Script.UI.TitleScreenMenuUI;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GlassSol.Patches
{
    internal static class ModeMenuPatch
    {
        const string Placeholder = "Описание.";

        [HarmonyPostfix]
        [HarmonyPatch(typeof(StartMenuLogic), "Start")]
        static void AddDifficultyButtons(StartMenuLogic __instance)
        {
            UIControlGroup panel = __instance.ChooseModePanel;
            if (panel == null || panel.transform.Find("GlassSolMode") != null)
                return;

            CreateGameAction[] actions = panel.GetComponentsInChildren<CreateGameAction>(true);
            CreateGameAction template = null;
            foreach (CreateGameAction action in actions)
            {
                if ((int)actionGameMode(action) == 0)
                    template = action;
            }

            if (template == null && actions.Length > 0)
                template = actions[0];
            if (template == null)
                return;

            UIControlButton templateButton = ButtonOf(template);
            if (templateButton == null)
                return;

            UIControlButton glass = CloneButton(templateButton, "GlassSolMode", Difficulty.GlassSol, "Стеклянный Сол");
            UIControlButton trueGlass = CloneButton(templateButton, "TrueGlassSolMode", Difficulty.TrueGlassSol, "Истинный Стеклянный Сол");

            List<UIControlButton> buttons = new List<UIControlButton>();
            foreach (CreateGameAction action in panel.GetComponentsInChildren<CreateGameAction>(true))
            {
                UIControlButton button = ButtonOf(action);
                if (button != null && !buttons.Contains(button))
                    buttons.Add(button);
            }

            WireNavigation(buttons);
            AppendSelectables(panel, glass, trueGlass);
        }

        static UIControlButton CloneButton(UIControlButton source, string name, int mode, string title)
        {
            GameObject clone = Object.Instantiate(source.gameObject, source.transform.parent);
            clone.name = name;
            clone.transform.SetSiblingIndex(source.transform.GetSiblingIndex() + (mode - 1));

            RectTransform sourceRect = source.GetComponent<RectTransform>();
            RectTransform cloneRect = clone.GetComponent<RectTransform>();
            if (source.transform.parent.GetComponent<VerticalLayoutGroup>() == null && sourceRect != null && cloneRect != null)
            {
                float height = sourceRect.rect.height;
                if (height < 1f)
                    height = 80f;
                cloneRect.anchoredPosition = sourceRect.anchoredPosition + new Vector2(0f, -height * (mode - 1));
            }

            foreach (CreateGameAction action in clone.GetComponentsInChildren<CreateGameAction>(true))
                actionGameMode(action, (GameMode)mode);

            DisableLocalization(clone);
            ApplyTexts(clone, title, Placeholder);
            return clone.GetComponent<UIControlButton>();
        }

        static void DisableLocalization(GameObject root)
        {
            foreach (MonoBehaviour behaviour in root.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (behaviour == null || behaviour.GetType().FullName != "I2.Loc.Localize")
                    continue;

                FieldInfo term = behaviour.GetType().GetField("mTerm", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (term != null)
                    term.SetValue(behaviour, string.Empty);
                behaviour.enabled = false;
            }
        }

        static void ApplyTexts(GameObject root, string title, string description)
        {
            TMP_Text[] texts = root.GetComponentsInChildren<TMP_Text>(true);
            if (texts.Length == 0)
                return;

            TMP_Text titleText = texts[0];
            TMP_Text descriptionText = texts[0];
            foreach (TMP_Text text in texts)
            {
                int length = text.text == null ? 0 : text.text.Length;
                int titleLength = titleText.text == null ? 0 : titleText.text.Length;
                int descriptionLength = descriptionText.text == null ? 0 : descriptionText.text.Length;
                if (length < titleLength)
                    titleText = text;
                if (length >= descriptionLength)
                    descriptionText = text;
            }

            titleText.text = title;
            if (descriptionText != titleText)
                descriptionText.text = description;
        }

        static void WireNavigation(List<UIControlButton> buttons)
        {
            for (int i = 0; i < buttons.Count; i++)
            {
                Selectable selectable = SelectableOf(buttons[i]);
                if (selectable == null)
                    continue;

                Navigation navigation = selectable.navigation;
                navigation.mode = Navigation.Mode.Explicit;
                navigation.selectOnUp = i > 0 ? SelectableOf(buttons[i - 1]) : null;
                navigation.selectOnDown = i < buttons.Count - 1 ? SelectableOf(buttons[i + 1]) : null;
                selectable.navigation = navigation;
            }
        }

        static void AppendSelectables(UIControlGroup panel, params UIControlButton[] extra)
        {
            FieldInfo field = AccessTools.Field(typeof(UIControlGroup), "_allSelectables");
            Selectable[] current = field.GetValue(panel) as Selectable[] ?? new Selectable[0];
            List<Selectable> selectables = new List<Selectable>(current);
            foreach (UIControlButton button in extra)
            {
                Selectable selectable = SelectableOf(button);
                if (selectable != null && !selectables.Contains(selectable))
                    selectables.Add(selectable);
            }

            field.SetValue(panel, selectables.ToArray());
        }

        static UIControlButton ButtonOf(CreateGameAction action)
        {
            UIControlButton button = action.GetComponent<UIControlButton>();
            return button != null ? button : action.GetComponentInParent<UIControlButton>();
        }

        static Selectable SelectableOf(UIControlButton button)
        {
            if (button == null)
                return null;

            Selectable selectable = AccessTools.Field(typeof(UIControlButton), "_button").GetValue(button) as Selectable;
            return selectable != null ? selectable : button.GetComponent<Selectable>();
        }

        static FieldInfo GameModeField()
        {
            return AccessTools.Field(typeof(CreateGameAction), "gameMode");
        }

        static GameMode actionGameMode(CreateGameAction action)
        {
            return (GameMode)GameModeField().GetValue(action);
        }

        static void actionGameMode(CreateGameAction action, GameMode mode)
        {
            GameModeField().SetValue(action, mode);
        }
    }

    [HarmonyPatch]
    internal static class NewGameModeSavePatch
    {
        [HarmonyTargetMethod]
        static MethodBase MoveNext()
        {
            return AccessTools.Method(
                typeof(StartMenuLogic).GetNestedType("<NewGameChangeScene>d__52", BindingFlags.NonPublic),
                "MoveNext");
        }

        [HarmonyPostfix]
        static void SaveCustomMode(object __instance)
        {
            FieldInfo stateField = __instance.GetType().GetField("<>1__state", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (stateField == null || (int)stateField.GetValue(__instance) == 0)
                return;

            FieldInfo menuField = __instance.GetType().GetField("<>4__this", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            StartMenuLogic menu = menuField == null ? null : menuField.GetValue(__instance) as StartMenuLogic;
            if (menu == null || menu.gameModeFlag == null)
                return;

            int mode = (int)AccessTools.Field(typeof(StartMenuLogic), "_newGameMode").GetValue(menu);
            if (mode == Difficulty.GlassSol || mode == Difficulty.TrueGlassSol)
                menu.gameModeFlag.CurrentValue = mode;
        }
    }
}
