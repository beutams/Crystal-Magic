using System;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

namespace CrystalMagic.Editor
{
    public static class EditorFocusUtility
    {
        public static void ClearTextFocus()
        {
            GUI.FocusControl(null);
            EditorGUI.FocusTextInControl(string.Empty);
            GUIUtility.keyboardControl = 0;
            EditorGUIUtility.editingTextField = false;
        }
    }

    public class TopBottomPortNode : Node
    {
        private const float PortStripHeight = 20f;

        private readonly VisualElement _topPortContainer;
        private readonly VisualElement _bottomPortContainer;

        public TopBottomPortNode()
        {
            inputContainer.RemoveFromHierarchy();
            outputContainer.RemoveFromHierarchy();

            _topPortContainer = CreatePortContainer();
            _bottomPortContainer = CreatePortContainer();
            mainContainer.Insert(0, _topPortContainer);
            mainContainer.Add(_bottomPortContainer);
        }

        public Port CreateTopInput(string name, Port.Capacity capacity, Type portType)
        {
            return CreatePort(_topPortContainer, name, Direction.Input, capacity, portType);
        }

        public Port CreateBottomOutput(string name, Port.Capacity capacity, Type portType)
        {
            return CreatePort(_bottomPortContainer, name, Direction.Output, capacity, portType);
        }

        private Port CreatePort(VisualElement container, string name, Direction direction, Port.Capacity capacity, Type portType)
        {
            Port port = InstantiatePort(Orientation.Vertical, direction, capacity, portType ?? typeof(bool));
            port.portName = name;
            container.Add(port);
            RefreshPorts();
            RefreshExpandedState();
            return port;
        }

        private static VisualElement CreatePortContainer()
        {
            return new VisualElement
            {
                style =
                {
                    minHeight = PortStripHeight,
                    flexDirection = FlexDirection.Row,
                    justifyContent = Justify.Center,
                },
            };
        }
    }
}
