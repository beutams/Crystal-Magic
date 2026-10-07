using CrystalMagic.Core;
using Unity.Entities;
using Unity.Transforms;
using UnityEngine;

namespace CrystalMagic.UI
{
    public sealed class DialogueUIController : UIControllerBase<DialogueUI, DialogueUIModel>
    {
        private const string OpenedEvent = "DialogueUI.Opened";
        private bool _closeRequested;

        public DialogueUIController(DialogueUI view, DialogueUIModel model) : base(view, model) { }

        protected override void OnOpen()
        {
            View.BindModel(Model);
            _closeRequested = false;
            BindEvent(new CommonGameEvent(OpenedEvent), OnAnotherDialogueOpened);
            Bindings.Bind(() => Canvas.preWillRenderCanvases += RefreshAnchor,
                () => Canvas.preWillRenderCanvases -= RefreshAnchor);
            EventComponent.Instance.Publish(new CommonGameEvent(OpenedEvent, Model));
            RefreshAnchor();
        }

        protected override void OnUpdate()
        {
            if (Model.Playback.IsCancelled || !AnchorExists())
            {
                _closeRequested = true;
                Model.Playback.Cancel();
                return;
            }
            // Typing and the reading wait use one clock shared with the interaction runner.
            // Both freeze while gameplay is paused.
            Model.Advance(Time.deltaTime);
            if (Model.Playback.IsCompleted)
                _closeRequested = true;
        }

        protected override void OnClose()
        {
            Model.ClosePresentation();
        }

        private void OnAnotherDialogueOpened(CommonGameEvent message)
        {
            DialogueUIModel other = message.GetData<DialogueUIModel>();
            if (!ReferenceEquals(other, Model) && other.World == Model.World && other.Anchor == Model.Anchor)
                _closeRequested = true;
        }

        private bool AnchorExists()
        {
            if (Model.World == null || !Model.World.IsCreated || Model.Anchor == Entity.Null)
                return false;
            EntityManager manager = Model.World.EntityManager;
            return manager.Exists(Model.Anchor) && manager.HasComponent<LocalToWorld>(Model.Anchor) &&
                !(manager.HasComponent<DestroyEntityFlag>(Model.Anchor) && manager.IsComponentEnabled<DestroyEntityFlag>(Model.Anchor));
        }

        private void RefreshAnchor()
        {
            // UIGroup.Tick enumerates its panels. Defer removal until the render callback.
            if (_closeRequested || !AnchorExists())
            {
                UIComponent.Instance.ReleaseUI(View);
                return;
            }
            Camera camera = CameraComponent.Instance.Current;
            if (!AnchorExists() || camera == null)
            {
                Model.SetAnchor(Model.Position, false);
                return;
            }
            EntityManager manager = Model.World.EntityManager;
            LocalToWorld transform = manager.GetComponentData<LocalToWorld>(Model.Anchor);
            SpriteRenderer renderer = manager.HasComponent<SpriteRenderer>(Model.Anchor)
                ? manager.GetComponentObject<SpriteRenderer>(Model.Anchor) : null;
            Vector3 anchor = InteractionPromptManager.ResolveAnchor(renderer, transform, Model.WorldYOffset);
            Vector3 screen = camera.WorldToScreenPoint(anchor);
            RectTransform root = (RectTransform)View.transform;
            Canvas canvas = View.Canvas.rootCanvas;
            Camera uiCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            bool visible = screen.z > 0f && screen.x >= 0f && screen.x <= Screen.width && screen.y >= 0f && screen.y <= Screen.height;
            if (visible && RectTransformUtility.ScreenPointToLocalPointInRectangle(root, screen, uiCamera, out Vector2 point))
                Model.SetAnchor(point, true);
            else
                Model.SetAnchor(Model.Position, false);
        }
    }
}
