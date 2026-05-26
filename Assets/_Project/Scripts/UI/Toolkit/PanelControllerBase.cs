using UnityEngine;
using UnityEngine.UIElements;

namespace Guideon.UI.Toolkit
{
    [RequireComponent(typeof(UIDocument))]
    public abstract class PanelControllerBase : MonoBehaviour
    {
        [SerializeField] protected UIDocument _document;

        protected VisualElement Root => _document != null ? _document.rootVisualElement : null;

        public string PanelId { get; protected set; }

        protected virtual void Awake()
        {
            if (_document == null)
                _document = GetComponent<UIDocument>();
        }

        protected virtual void OnEnable()
        {
            if (Root == null) return;
            Root.style.display = DisplayStyle.Flex;
            OnBindUI();
            SubscribeEvents();
        }

        protected virtual void OnDisable()
        {
            UnsubscribeEvents();
            if (Root != null)
                Root.style.display = DisplayStyle.None;
        }

        protected abstract void OnBindUI();

        protected virtual void SubscribeEvents() { }
        protected virtual void UnsubscribeEvents() { }

        protected T Q<T>(string name) where T : VisualElement =>
            Root?.Q<T>(name);

        protected VisualElement Q(string name) =>
            Root?.Q(name);
    }
}
