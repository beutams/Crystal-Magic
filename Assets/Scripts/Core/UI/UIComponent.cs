using System.Collections.Generic;
using UnityEngine;

namespace CrystalMagic.Core {
    /// <summary>
    /// UI 管理组件
    /// 全局单例，负责分组创建、注册、路由和每帧更新
    /// </summary>
    public class UIComponent : GameComponent<UIComponent>
    {
        private const string DefaultGroupName = "Default";
        private const string ConfigPath = "Assets/Config/ui_config.json";

        private Dictionary<string, UIGroup> _groups = new();
        private Dictionary<string, string> _uiNameToGroupName = new();
        private UIGroupConfig _config;

        public override int Priority => 15;

        public override void Initialize()
        {
            base.Initialize();

            // 从固定路径加载配置
            LoadConfigFromPath();

            // 确保存在默认分组
            EnsureDefaultGroupExists();

            // 通过 CameraComponent 获取相机，它比 UIComponent(15) 优先级更高(13)，确保已初始化
            Camera uiCamera = CameraComponent.Instance.Current;
            if (uiCamera != null)
            {
                ApplyCameraToGroups(uiCamera);
            }
        }

        private void LoadConfigFromPath()
        {
            #if UNITY_EDITOR
            TextAsset configAsset = UnityEditor.AssetDatabase.LoadAssetAtPath<TextAsset>(ConfigPath);
            if (configAsset != null)
            {
                _config = UIConfigLoader.LoadFromJson(configAsset.text);
                if (_config != null)
                {
                    CreateGroupsFromConfig();
                }
            }
            #endif
        }

        private void ApplyCameraToGroups(Camera camera)
        {
            foreach (var group in _groups.Values)
            {
                group.GetComponent<Canvas>().worldCamera = camera;
            }
        }

        private void Update()
        {
            // 每帧更新所有分组
            foreach (var group in _groups.Values)
            {
                group.Tick();
            }
        }

        /// <summary>
        /// 从配置创建分组
        /// </summary>
        private void CreateGroupsFromConfig()
        {
            foreach (var entry in _config.groups)
            {
                CreateGroup(entry);
            }
        }

        /// <summary>
        /// 创建单个分组
        /// </summary>
        private void CreateGroup(UIGroupEntry entry)
        {
            GameObject groupObj = new GameObject(entry.groupName);
            groupObj.transform.SetParent(transform);

            UIGroup group = null;
            switch (entry.groupType)
            {
                case UIGroupType.Stack:
                    group = groupObj.AddComponent<StackUIGroup>();
                    break;
                case UIGroupType.Queue:
                    group = groupObj.AddComponent<QueueUIGroup>();
                    break;
                case UIGroupType.List:
                    group = groupObj.AddComponent<ListUIGroup>();
                    break;
            }

            if (group != null)
            {
                RegisterGroup(entry.groupName, group, entry.uiNames);
            }
        }

        /// <summary>
        /// 确保存在默认分组
        /// </summary>
        private void EnsureDefaultGroupExists()
        {
            if (_groups.ContainsKey(DefaultGroupName))
                return;

            GameObject groupObj = new GameObject(DefaultGroupName);
            groupObj.transform.SetParent(transform);

            StackUIGroup group = groupObj.AddComponent<StackUIGroup>();
            RegisterGroup(DefaultGroupName, group);
        }

        /// <summary>
        /// 注册分组
        /// </summary>
        public void RegisterGroup(string groupName, UIGroup group, List<string> uiNames = null)
        {
            if (string.IsNullOrEmpty(groupName))
                return;

            _groups[groupName] = group;

            if (uiNames != null)
            {
                foreach (var uiName in uiNames)
                {
                    _uiNameToGroupName[uiName] = groupName;
                }
            }
        }

        /// <summary>
        /// 显示 UI
        /// </summary>
        public void ShowUI(UIBase panel)
        {
            string groupName = GetGroupNameByUIName(panel.name);
            if (string.IsNullOrEmpty(groupName))
                groupName = DefaultGroupName;

            ShowUI(groupName, panel);
        }

        /// <summary>
        /// 显示 UI（按组名）
        /// </summary>
        public void ShowUI(string groupName, UIBase panel)
        {
            if (_groups.TryGetValue(groupName, out UIGroup group))
            {
                group.ShowUI(panel);
            }
        }

        /// <summary>
        /// 关闭 UI
        /// </summary>
        public void CloseUI(UIBase panel)
        {
            string groupName = GetGroupNameByUIName(panel.name);
            if (string.IsNullOrEmpty(groupName))
                groupName = DefaultGroupName;

            CloseUI(groupName, panel);
        }

        /// <summary>
        /// 关闭 UI（按组名）
        /// </summary>
        public void CloseUI(string groupName, UIBase panel)
        {
            if (_groups.TryGetValue(groupName, out UIGroup group))
            {
                group.CloseUI(panel);
            }
        }

        /// <summary>
        /// 获取 UI 所属的组名
        /// </summary>
        public string GetGroupNameByUIName(string uiName)
        {
            _uiNameToGroupName.TryGetValue(uiName, out string groupName);
            return groupName;
        }

        /// <summary>
        /// 获取分组
        /// </summary>
        public T GetGroup<T>(string groupName) where T : UIGroup
        {
            if (_groups.TryGetValue(groupName, out UIGroup group))
            {
                return group as T;
            }
            return null;
        }

        public override void Cleanup()
        {
            _groups.Clear();
            _uiNameToGroupName.Clear();
            base.Cleanup();
        }
    }
}
