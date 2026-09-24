using System;
using System.Collections;
using System.IO;
using System.Reflection;
using _3_Script.UI.SaveUI;
using _3_Script.UI.TitleScreenMenuUI;
using BepInEx;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace GlassSol.Patches
{
    internal static class ModeMenuPatch
    {
        internal const string GlassSolName = "Стеклянный Сол";
        internal const string GlassSolNameEn = "Glass Sol";
        internal const string TrueGlassSolName = "Истинный Стеклянный Сол";
        internal const string TrueGlassSolNameEn = "True Glass Sol";
        const string GlassSolDescription = "Повышенная сложность Nine Sols.\nПрямой урон вас мгновенно убивает. Ваше сохранение при этом остается целым.";
        const string GlassSolDescriptionEn = "An increased difficulty for Nine Sols.\nDirect damage kills you instantly. Your save remains intact.";
        const string TrueGlassSolDescription = "Наивысшая сложность Nine Sols.\nЛюбой полученный вами урон фатальный, второго шанса не дается.";
        const string TrueGlassSolDescriptionEn = "The highest difficulty for Nine Sols.\nAny damage you take is fatal. There is no second chance.";
        static Image glassArt;
        static Image trueArt;
        static int shownMode = 1;
        static UIControlButton soundButton;

        [HarmonyPostfix]
        [HarmonyPatch(typeof(StartMenuLogic), "Start")]
        static void AddDifficultyButtons(StartMenuLogic __instance)
        {
            __instance.StartCoroutine(AddWhenReady(__instance));
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(UIControlButton), "OnSelect")]
        static void ShowPlaceholder(UIControlButton __instance)
        {
            Transform options = OptionsOf(__instance.transform);
            if (options == null)
                return;

            ReleaseOthers(options, __instance);
            int mode = ModeOf(__instance.transform, options);
            if (glassArt != null && mode >= 0)
            {
                shownMode = mode;
                ApplyArt();
            }
            Transform glassButton = options.Find("GlassSolMode");
            if (glassButton != null)
                glassButton.Find("ActionText").GetComponent<TMP_Text>().text = Difficulty.Phrase(GlassSolName, GlassSolNameEn);
            Transform trueButton = options.Find("TrueGlassSolMode");
            if (trueButton != null)
                trueButton.Find("ActionText").GetComponent<TMP_Text>().text = Difficulty.Phrase(TrueGlassSolName, TrueGlassSolNameEn);

            if (!IsCustom(__instance.transform))
                return;

            PlaySelectSound();
            Transform description = options.Find("Selected Description");
            DestroyLocalization(description.gameObject);
            description.GetComponentInChildren<TMP_Text>(true).text = IsTrue(__instance.transform)
                ? Difficulty.Phrase(TrueGlassSolDescription, TrueGlassSolDescriptionEn)
                : Difficulty.Phrase(GlassSolDescription, GlassSolDescriptionEn);
        }

        static int ModeOf(Transform transform, Transform options)
        {
            if (IsTrue(transform))
                return 3;
            if (IsCustom(transform) && !IsTrue(transform))
                return 2;

            Transform row = transform;
            while (row.parent != null && row.parent != options)
                row = row.parent;

            int index = 0;
            for (int i = 0; i < options.childCount; i++)
            {
                if (!options.GetChild(i).name.Contains("Start Game"))
                    continue;
                if (options.GetChild(i) == row)
                    return index;
                index++;
            }

            return -1;
        }

        internal static void ApplyArt()
        {
            if (glassArt == null)
                return;

            glassArt.gameObject.SetActive(shownMode == 2);
            trueArt.gameObject.SetActive(shownMode == 3);
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(UIControlButton), "OnDeselect")]
        static void Release(UIControlButton __instance)
        {
            if (!IsCustom(__instance.transform))
                return;

            __instance.StartCoroutine(ClearWhenLeft(__instance));
        }

        static IEnumerator ClearWhenLeft(UIControlButton button)
        {
            yield return null;
            button.RefreshState();
        }

        static IEnumerator AddWhenReady(StartMenuLogic menu)
        {
            for (int i = 0; i < 40; i++)
            {
                if (TryAdd(menu))
                {
                    yield return null;
                    Rebind(FindOptionsRoot());
                    yield break;
                }

                yield return new WaitForSecondsRealtime(0.25f);
            }
        }

        static bool TryAdd(StartMenuLogic menu)
        {
            Transform root = FindOptionsRoot();
            if (root == null)
                return false;
            if (root.Find("TrueGlassSolMode") != null)
                return true;

            Transform template = FindChild(root, "Start Game");
            soundButton = template.GetComponent<UIControlButton>() ?? template.GetComponentInChildren<UIControlButton>(true);
            CreateArts(root);
            Transform padding = root.GetChild(template.GetSiblingIndex() + 1);
            TryAddMode(menu, root, template, padding, Difficulty.GlassSol, "GlassSolMode", Difficulty.Phrase(GlassSolName, GlassSolNameEn));
            TryAddMode(menu, root, template, padding, Difficulty.TrueGlassSol, "TrueGlassSolMode", Difficulty.Phrase(TrueGlassSolName, TrueGlassSolNameEn));

            Rebind(root);
            return root.Find("TrueGlassSolMode") != null;
        }

        static void Rebind(Transform root)
        {
            SelectableNavigationProvider navigation = root.GetComponent<SelectableNavigationProvider>();
            if (navigation == null)
                navigation = root.GetComponentInParent<SelectableNavigationProvider>();
            if (navigation != null)
                navigation.AutoNavigateBindForAll();
        }

        static void TryAddMode(StartMenuLogic menu, Transform root, Transform template, Transform padding, int mode, string objectName, string title)
        {
            try
            {
                AddMode(menu, root, template, padding, mode, objectName, title);
            }
            catch (Exception exception)
            {
                Plugin.Log.LogError(objectName + ": " + exception);
            }
        }

        static void AddMode(StartMenuLogic menu, Transform root, Transform template, Transform padding, int mode, string objectName, string title)
        {
            if (root.Find(objectName) != null)
                return;

            int index = InsertIndex(root);

            bool wasActive = template.gameObject.activeSelf;
            template.gameObject.SetActive(false);
            Transform button = UnityEngine.Object.Instantiate(template.gameObject, root).transform;
            template.gameObject.SetActive(wasActive);
            button.SetParent(root, false);
            button.name = objectName;
            button.SetSiblingIndex(index);
            DetachStandardMode(button.gameObject);
            PointArt(button, mode == Difficulty.TrueGlassSol ? trueArt : glassArt);

            Transform gap = UnityEngine.Object.Instantiate(padding.gameObject, root).transform;
            gap.SetParent(root, false);
            gap.SetSiblingIndex(index + 1);

            button.gameObject.SetActive(true);
            DestroyLocalization(button.gameObject);
            TMP_Text label = button.Find("ActionText").GetComponent<TMP_Text>();
            label.text = title;

            UIControlButton control = button.GetComponent<UIControlButton>();
            if (control == null)
                control = button.GetComponentInChildren<UIControlButton>(true);

            RetargetLocal(control, button);
            control.RefreshState();
            BindDirectStart(menu, control, mode);
        }

        static void PointArt(Transform button, Image art)
        {
            FieldInfo field = null;
            foreach (MonoBehaviour behaviour in button.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (behaviour == null || behaviour.GetType().Name != "AnimatorColorControlNode")
                    continue;

                if (field == null)
                    field = behaviour.GetType().GetField("imageTarget", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                field.SetValue(behaviour, art);
            }
        }

        static void DetachStandardMode(GameObject button)
        {
            foreach (MonoBehaviour behaviour in button.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (behaviour != null && behaviour.GetType().Name == "CreateGameAction")
                    UnityEngine.Object.DestroyImmediate(behaviour);
            }
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

        static int InsertIndex(Transform root)
        {
            int lastButton = 0;
            for (int i = 0; i < root.childCount; i++)
            {
                string name = root.GetChild(i).name;
                if (name.Contains("Start Game") || name == "GlassSolMode" || name == "TrueGlassSolMode")
                    lastButton = i;
            }

            int index = lastButton + 1;
            if (index < root.childCount && root.GetChild(index).name.StartsWith("Pedding"))
                index++;

            return index;
        }

        static Transform FindOptionsRoot()
        {
            foreach (Transform transform in Resources.FindObjectsOfTypeAll<Transform>())
            {
                if (transform.name == "UIOptions" && transform.gameObject.scene.IsValid() && FindChild(transform, "Start Game") != null)
                    return transform;
            }

            return null;
        }

        static Transform FindChild(Transform root, string namePart)
        {
            for (int i = 0; i < root.childCount; i++)
            {
                Transform child = root.GetChild(i);
                if (child.name.Contains(namePart))
                    return child;
            }

            return null;
        }

        static bool IsCustom(Transform transform)
        {
            while (transform != null && transform.name != "UIOptions")
            {
                if (transform.name == "GlassSolMode" || transform.name == "TrueGlassSolMode")
                    return true;
                transform = transform.parent;
            }

            return false;
        }

        static bool IsTrue(Transform transform)
        {
            while (transform != null && transform.name != "UIOptions")
            {
                if (transform.name == "TrueGlassSolMode")
                    return true;
                transform = transform.parent;
            }

            return false;
        }

        static void CreateArts(Transform options)
        {
            if (glassArt != null)
                return;

            Transform panel = options;
            while (panel != null && panel.Find("BG") == null)
                panel = panel.parent;

            Transform background = panel.Find("BG");
            Image second = null;
            int count = 0;
            for (int i = 0; i < background.childCount; i++)
            {
                if (background.GetChild(i).name != "BG Image")
                    continue;
                count++;
                if (count == 2)
                {
                    second = background.GetChild(i).GetComponent<Image>();
                    break;
                }
            }

            glassArt = CopyArt(second, "GlassSolArt", "GlassSol.png");
            trueArt = CopyArt(second, "TrueGlassSolArt", "TrueGlassSol.png");
            if (background.GetComponent<ModeArtKeep>() == null)
                background.gameObject.AddComponent<ModeArtKeep>();
        }

        static Image CopyArt(Image source, string name, string fileName)
        {
            bool wasActive = source.gameObject.activeSelf;
            source.gameObject.SetActive(false);
            Image copy = UnityEngine.Object.Instantiate(source.gameObject, source.transform.parent).GetComponent<Image>();
            source.gameObject.SetActive(wasActive);
            copy.name = name;
            copy.sprite = LoadArt(fileName);
            Color color = copy.color;
            color.a = 1f;
            copy.color = color;
            Animator animator = copy.GetComponent<Animator>();
            if (animator != null)
                animator.enabled = false;
            copy.transform.SetSiblingIndex(source.transform.GetSiblingIndex() + 1);
            copy.gameObject.SetActive(false);
            return copy;
        }

        static Sprite LoadArt(string fileName)
        {
            string path = Path.Combine(Paths.PluginPath, "GlassSol", fileName);
            Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            ImageConversion.LoadImage(texture, File.ReadAllBytes(path));
            return Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f));
        }

        static void PlaySelectSound()
        {
            object group = AccessTools.Field(typeof(UIControlButton), "_buttonSoundGroup").GetValue(soundButton);
            AccessTools.Method(group.GetType(), "PlaySelectSound").Invoke(group, null);
        }

        static Transform OptionsOf(Transform transform)
        {
            while (transform != null && transform.name != "UIOptions")
                transform = transform.parent;
            return transform;
        }

        static void ReleaseOthers(Transform options, UIControlButton current)
        {
            ReleaseIfOther(options.Find("GlassSolMode"), current);
            ReleaseIfOther(options.Find("TrueGlassSolMode"), current);
        }

        static void ReleaseIfOther(Transform row, UIControlButton current)
        {
            if (row == null || current.transform.IsChildOf(row) || current.transform == row)
                return;

            UIControlButton button = row.GetComponent<UIControlButton>() ?? row.GetComponentInChildren<UIControlButton>(true);
            button.RefreshState();
        }

        static void BindDirectStart(StartMenuLogic menu, UIControlButton button, int mode)
        {
            FieldInfo actionsField = AccessTools.Field(typeof(UIControlButton), "_onSubmitActions");
            Type actionType = actionsField.FieldType.GetElementType();
            actionsField.SetValue(button, Array.CreateInstance(actionType, 0));
            AccessTools.Field(typeof(UIControlButton), "clickToShowGroup").SetValue(button, null);
            if (button.onSubmit == null)
                button.onSubmit = new UnityEvent();
            button.onSubmit.AddListener(() => menu.CreateNewGame(mode));
        }

        internal static void DestroyLocalization(GameObject root)
        {
            foreach (MonoBehaviour behaviour in root.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (behaviour != null && behaviour.GetType().FullName == "I2.Loc.Localize")
                    UnityEngine.Object.DestroyImmediate(behaviour);
            }
        }
    }

    internal static class SaveModeLabelPatch
    {
        [HarmonyPostfix]
        [HarmonyPatch(typeof(RCGAbstractUILabel<int>), "UpdateText")]
        static void WriteCustomMode(RCGAbstractUILabel<int> __instance)
        {
            if (AccessTools.Field(__instance.GetType(), "propertyName").GetValue(__instance) as string != "gameMode")
                return;

            object value = AccessTools.Method(__instance.GetType(), "GetValueOfData").Invoke(__instance, null);
            if (!(value is int mode))
                return;
            if (mode != Difficulty.GlassSol && mode != Difficulty.TrueGlassSol)
                return;

            ModeMenuPatch.DestroyLocalization(__instance.gameObject);
            TMP_Text text = AccessTools.Field(typeof(RCGAbstractUILabel<int>), "text").GetValue(__instance) as TMP_Text;
            text.text = mode == Difficulty.TrueGlassSol
                ? Difficulty.Phrase(ModeMenuPatch.TrueGlassSolName, ModeMenuPatch.TrueGlassSolNameEn)
                : Difficulty.Phrase(ModeMenuPatch.GlassSolName, ModeMenuPatch.GlassSolNameEn);
        }
    }

    internal class ModeArtKeep : MonoBehaviour
    {
        void LateUpdate()
        {
            ModeMenuPatch.ApplyArt();
        }
    }

    internal static class NewGameModeSavePatch
    {
        static int pendingMode = -1;
        static StartMenuLogic menu;

        [HarmonyPostfix]
        [HarmonyPatch(typeof(StartMenuLogic), nameof(StartMenuLogic.CreateNewGame))]
        static void RememberMode(StartMenuLogic __instance, int mode)
        {
            menu = __instance;
            pendingMode = mode;
            Apply(mode);
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(StartMenuLogic), "CreateOrLoadSaveSlotAndPlay")]
        static void ClearOnContinue()
        {
            pendingMode = -1;
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(A0_S6_IntroVideo), "Start")]
        static bool SkipIntro(A0_S6_IntroVideo __instance)
        {
            if (pendingMode != Difficulty.TrueGlassSol)
                return true;

            AccessTools.Method(typeof(A0_S6_IntroVideo), "ChangeToStartScene").Invoke(__instance, null);
            return false;
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(ApplicationCore), nameof(ApplicationCore.ChangeSceneClean))]
        static void SaveMode(string sceneName)
        {
            if (sceneName != "A0_S6_Intro_Video")
                return;

            Apply(pendingMode);
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(PlayerGamePlayData), nameof(PlayerGamePlayData.SaveMetaData))]
        static void StampSave(SaveSlotMetaData __result)
        {
            if (__result == null)
                return;
            if (pendingMode != Difficulty.GlassSol && pendingMode != Difficulty.TrueGlassSol)
                return;

            __result.gameMode = pendingMode;
        }

        static void Apply(int mode)
        {
            if (mode != Difficulty.GlassSol && mode != Difficulty.TrueGlassSol)
                return;

            if (menu != null)
                menu.gameModeFlag.CurrentValue = mode;

            PlayerGamePlayData data = PlayerGamePlayData.Instance;
            if (data != null && data.gameMode != null)
                data.gameMode.CurrentValue = mode;
        }
    }
}
