using System.Linq;
using System.Reflection;
using CrystalMagic.Core;
using CrystalMagic.UI;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public sealed class InteractionSelectUILayoutTests
{
    private const string PrefabPath = "Assets/Res/UI/InteractionSelectUI.prefab";
    private const string ArtPath = "Assets/Res/Sprites/UISprites/BookV1/Content/";

    private static InteractionSelectUIData Bind(GameObject root)
    {
        var data = new InteractionSelectUIData();
        data.Bind(root.transform);
        foreach (FieldInfo field in typeof(InteractionSelectUIData).GetFields())
            Assert.That(field.GetValue(data), Is.Not.Null, field.Name);
        return data;
    }

    [Test]
    public void BookFrameAndHighlightUseExistingSpritesWithoutChangingSharedSlice()
    {
        var data = Bind(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath));
        Image frame = data.BG.Image;
        Assert.That(frame.sprite, Is.EqualTo(AssetDatabase.LoadAssetAtPath<Sprite>(
            ArtPath + "Titles and Underlying/13.png")));
        Assert.That(frame.sprite, Is.Not.Null);
        Assert.That(frame.type, Is.EqualTo(Image.Type.Sliced));
        Assert.That(frame.fillCenter, Is.False);
        Assert.That(frame.sprite.border, Is.EqualTo(new Vector4(6, 6, 6, 6)));
        Assert.That(frame.color.a, Is.EqualTo(0.65f).Within(0.001f));
        Assert.That(data.BG_Fill.Image.color.a, Is.EqualTo(0.84f).Within(0.001f));
        var sprites = AssetDatabase.LoadAllAssetsAtPath(ArtPath + "Titles and Underlying/2_16x16.png")
            .OfType<Sprite>().ToArray();
        Sprite highlight = sprites.Single(s => s.name == "InteractionSelect_Highlight");
        Assert.That(highlight.border, Is.EqualTo(new Vector4(4, 4, 4, 4)));
        Assert.That(sprites.Single(s => s.name == "2_16x16_0").border,
            Is.EqualTo(Vector4.one), "Do not alter CharacterUI's shared slice.");
        Assert.That(data.Content_Button_Enter.Image.sprite, Is.EqualTo(highlight));
        Assert.That(data.Content_Button_Click.Image.sprite, Is.EqualTo(highlight));
        foreach (UINode arrow in new[] { data.Content_Button_Enter_Arrow, data.Content_Button_Click_Arrow })
        {
            Assert.That(arrow.Image.sprite, Is.Not.Null);
            Assert.That(AssetDatabase.GetAssetPath(arrow.Image.sprite),
                Is.EqualTo(ArtPath + "Inscriptions/Light/16x16.png"));
            Assert.That(arrow.RectTransform.localEulerAngles.z, Is.EqualTo(90).Within(0.001f));
        }
    }

    [Test]
    public void OnlyStableOptionRootReceivesRaycastsAndStatesAreSeparate()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        var data = Bind(prefab);
        Graphic[] targets = prefab.GetComponentsInChildren<Graphic>(true).Where(g => g.raycastTarget).ToArray();
        Assert.That(targets, Has.Length.EqualTo(1));
        Assert.That(targets[0], Is.EqualTo(data.Content_Button.Image));
        var button = new SerializedObject(data.Content_Button.ButtonPlus);
        Assert.That(button.FindProperty("defaultTransforms").objectReferenceValue,
            Is.EqualTo(data.Content_Button_Default.RectTransform));
        Assert.That(button.FindProperty("enterTransforms").objectReferenceValue,
            Is.EqualTo(data.Content_Button_Enter.RectTransform));
        Assert.That(button.FindProperty("clickTransforms").objectReferenceValue,
            Is.EqualTo(data.Content_Button_Click.RectTransform));
        Assert.That(button.FindProperty("_hoverScaleMultiplier").floatValue, Is.EqualTo(1));
        foreach (LocalizedTextMeshProUGUI text in prefab.GetComponentsInChildren<LocalizedTextMeshProUGUI>(true))
        {
            Assert.That(text.font, Is.Not.Null);
            Assert.That(text.LocalizationKey, Is.Empty, "Runtime model supplies the dialog and options.");
            Assert.That((Color32)text.color, Is.EqualTo(new Color32(231, 213, 179, 255)));
        }
    }

    [Test]
    public void PanelResizesForOptionCountAndMultilineDialog()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            var data = Bind(root);
            var view = root.GetComponent<InteractionSelectUI>();
            view.EnsureInitialized();
            MethodInfo refresh = typeof(InteractionSelectUI).GetMethod("RefreshLayout",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(refresh, Is.Not.Null);
            data.Dialog.TextMeshProUGUI.text = string.Empty;
            float priorHeight = 0;
            foreach (int count in new[] { 0, 1, 2, 4, 6 })
            {
                refresh.Invoke(view, new object[] { count });
                Assert.That(data.Dialog.GameObject.activeSelf, Is.False);
                Assert.That(data.Content.GameObject.activeSelf, Is.EqualTo(count > 0));
                Assert.That(data.Content.RectTransform.sizeDelta.y,
                    Is.EqualTo(count * 108 + Mathf.Max(0, count - 1) * 10));
                float height = data.BG.RectTransform.sizeDelta.y;
                Assert.That(height, Is.GreaterThan(priorHeight));
                Assert.That(height, Is.LessThan(1440 * 0.9f));
                priorHeight = height;
            }
            data.Dialog.TextMeshProUGUI.text = "是否继续探索？";
            refresh.Invoke(view, new object[] { 2 });
            float shortHeight = data.BG.RectTransform.sizeDelta.y;
            data.Dialog.TextMeshProUGUI.text = "是否继续探索？\n撤离后将返回营地。";
            refresh.Invoke(view, new object[] { 2 });
            Assert.That(data.Dialog.GameObject.activeSelf, Is.True);
            Assert.That(data.BG.RectTransform.sizeDelta.y, Is.GreaterThan(shortHeight));
            float dialogBottom = data.Dialog.RectTransform.anchoredPosition.y - data.Dialog.RectTransform.sizeDelta.y;
            float optionsTop = data.Content.RectTransform.anchoredPosition.y + data.Content.RectTransform.sizeDelta.y;
            Assert.That(dialogBottom - optionsTop, Is.EqualTo(44).Within(0.1f));
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    [Test]
    public void AllStatesRenderCurrentOptionAndRebindingDoesNotDuplicateClick()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            var data = Bind(root);
            var view = data.Content_Button.GameObject.GetComponent<InteractionSelectUI_OptionView>();
            var button = data.Content_Button.ButtonPlus;
            var current = new InteractionSelectOptionDisplayData { DisplayName = "撤离地牢" };
            view.Render(new InteractionSelectOptionDisplayData { DisplayName = "继续探索" });
            view.Render(current);
            Assert.That(view.UI.Default_TextTMP.TextMeshProUGUI.text, Is.EqualTo(current.DisplayName));
            Assert.That(view.UI.Enter_TextTMP.TextMeshProUGUI.text, Is.EqualTo(current.DisplayName));
            Assert.That(view.UI.Click_TextTMP.TextMeshProUGUI.text, Is.EqualTo(current.DisplayName));
            int clicks = 0;
            view.Clicked += option => { Assert.That(option, Is.SameAs(current)); clicks++; };
            button.onClick.Invoke();
            Assert.That(clicks, Is.EqualTo(1));
            button.OnPointerEnter(null);
            Assert.That(view.UI.Enter.GameObject.activeSelf, Is.True);
            Assert.That(view.UI.Default.GameObject.activeSelf, Is.False);
            Assert.That(view.UI.Click.GameObject.activeSelf, Is.False);
            button.OnPointerDown(null);
            Assert.That(view.UI.Click.GameObject.activeSelf, Is.True);
            Assert.That(view.UI.Enter.GameObject.activeSelf, Is.False);
            button.OnPointerExit(null);
            Assert.That(view.UI.Default.GameObject.activeSelf, Is.True);
            view.Render(null);
            button.onClick.Invoke();
            Assert.That(clicks, Is.EqualTo(1));
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }
}
