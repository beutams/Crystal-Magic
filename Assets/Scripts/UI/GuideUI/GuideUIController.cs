using System.Collections.Generic;
using CrystalMagic.Core;
using Unity.Entities;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.UI;

namespace CrystalMagic.UI
{
    public sealed class GuideUIController : UIControllerBase<GuideUI, GuideUIModel>
    {
        private readonly Vector3[] _corners = new Vector3[4];
        public GuideUIController(GuideUI view, GuideUIModel model) : base(view, model) { }
        protected override void OnOpen()
        {
            View.BindModel(Model);
            Bindings.Bind(() => Canvas.preWillRenderCanvases += RefreshTargets,
                () => Canvas.preWillRenderCanvases -= RefreshTargets);
            RefreshTargets();
        }

        private void RefreshTargets()
        {
            if (Model.Session == null || !Model.Session.IsActive) return;
            var node = Model.Node;
            var holes = new List<Rect>(2);
            string text = LocalizationComponent.Resolve(node.ContentKey);
            RectTransform primary = ResolveUI(node.Target, ref text);
            RectTransform secondary = ResolveUI(node.SecondaryTarget, ref text);
            if (primary != null && primary.gameObject.activeInHierarchy)
            {
                Reveal(primary);
                holes.Add(ToRect(primary));
                if (secondary != null && secondary.gameObject.activeInHierarchy)
                {
                    Rect destination = ToRect(secondary);
                    CharacterUI character = UIComponent.Instance.FindOpen<CharacterUI>();
                    if (node.SecondaryTarget == "character.skill.append" && character.GuideSkillPage && character.GuideFirstChain && character.GuideLastSkill != null)
                    {
                        Reveal(character.GuideLastSkill);
                        Rect last = ToRect(character.GuideLastSkill);
                        destination.yMin = Mathf.Max(destination.yMin, last.yMin + 6);
                        destination.yMax = Mathf.Min(destination.yMax, last.center.y - 6);
                    }
                    holes.Add(destination);
                }
                Model.SetPresentation(holes.ToArray(), true, text);
                return;
            }
            string worldTarget = node.Target?.StartsWith("world.") == true ? node.Target.Substring(6) : node.FallbackTarget;
            if (!string.IsNullOrWhiteSpace(node.FallbackContentKey)) text = LocalizationComponent.Resolve(node.FallbackContentKey);
            if (!string.IsNullOrWhiteSpace(worldTarget) && TryWorldRect(worldTarget, out Rect marker)) holes.Add(marker);
            // Missing or closed UI must never create a full-screen input deadlock.
            Model.SetPresentation(holes.ToArray(), false, text);
        }

        private RectTransform ResolveUI(string key, ref string text)
        {
            if (string.IsNullOrWhiteSpace(key)) return null;
            UIComponent ui = UIComponent.Instance;
            if (key.StartsWith("shop.purchase.") && int.TryParse(key.Substring(14), out int itemId))
            {
                ShopBuyUI buy = ui.FindOpen<ShopBuyUI>();
                if (buy != null)
                {
                    text = LocalizationComponent.Resolve(buy.GuideItemId == itemId ? "guide.purchase.confirm" : "guide.purchase.cancel");
                    return buy.GuideItemId == itemId ? buy.GuideConfirm : buy.GuideCancel;
                }
                return ui.FindOpen<ShopUI>()?.GetGuideTarget(itemId);
            }
            if (key == "shop.back") return ui.FindOpen<ShopUI>()?.GuideBack;
            if (key == "interaction.enterDungeon") return ui.FindOpen<InteractionSelectUI>()?.GetGuideOption("npc.option.enter_dungeon");
            if (key.StartsWith("character."))
            {
                CharacterUI character = ui.FindOpen<CharacterUI>();
                if (character == null) return null;
                string target = key.Substring(10);
                bool skill = Model.Node.SecondaryTarget == "character.skill.append";
                if (skill && !character.GuideSkillPage)
                { text = LocalizationComponent.Resolve("guide.page.skill"); return character.GetGuideTarget("page.skill"); }
                if (!skill && target != "page.skill" && !character.GuideEquipPage)
                { text = LocalizationComponent.Resolve("guide.page.equip"); return character.GetGuideTarget("page.equip"); }
                if (skill && !character.GuideFirstChain)
                { text = LocalizationComponent.Resolve("guide.chain.first"); return character.GetGuideTarget("chain.first"); }
                return character.GetGuideTarget(target);
            }
            return null;
        }

        private Rect ToRect(RectTransform target)
        {
            target.GetWorldCorners(_corners);
            Vector2 min = new(float.MaxValue, float.MaxValue), max = new(float.MinValue, float.MinValue);
            for (int i = 0; i < 4; i++)
            {
                Vector2 point = View.Root.InverseTransformPoint(_corners[i]);
                min = Vector2.Min(min, point); max = Vector2.Max(max, point);
            }
            return Rect.MinMaxRect(min.x - 6, min.y - 6, max.x + 6, max.y + 6);
        }

        private bool TryWorldRect(string name, out Rect rect)
        {
            rect = default;
            Entity entity = NPCSequenceUtility.FindEntity(Model.Session, name);
            if (entity == Entity.Null) return false;
            var manager = Model.Session.World.EntityManager;
            Camera camera = CameraComponent.Instance.Current;
            if (camera == null || !manager.HasComponent<LocalToWorld>(entity)) return false;
            Vector3 screen = camera.WorldToScreenPoint((Vector3)manager.GetComponentData<LocalToWorld>(entity).Position + Vector3.up);
            Camera uiCamera = View.Canvas.rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : View.Canvas.rootCanvas.worldCamera;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(View.Root, screen, uiCamera, out Vector2 point);
            Rect bounds = View.Root.rect;
            point.x = Mathf.Clamp(point.x, bounds.xMin + 50, bounds.xMax - 50);
            point.y = Mathf.Clamp(point.y, bounds.yMin + 190, bounds.yMax - 50);
            rect = new Rect(point.x - 38, point.y - 38, 76, 76);
            return true;
        }

        private static void Reveal(RectTransform target)
        {
            ScrollRect scroll = target.GetComponentInParent<ScrollRect>();
            if (scroll == null || scroll.viewport == null || scroll.content == null || !scroll.vertical) return;
            var corners = new Vector3[4];
            target.GetWorldCorners(corners);
            float bottom = scroll.viewport.InverseTransformPoint(corners[0]).y;
            float top = scroll.viewport.InverseTransformPoint(corners[1]).y;
            Rect view = scroll.viewport.rect;
            float delta = top > view.yMax ? view.yMax - top : bottom < view.yMin ? view.yMin - bottom : 0;
            if (Mathf.Abs(delta) < 1) return;
            scroll.StopMovement();
            scroll.content.anchoredPosition += Vector2.up * delta;
        }
    }
}
