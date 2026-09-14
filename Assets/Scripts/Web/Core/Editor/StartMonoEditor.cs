using System;
using System.Collections;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Server
{
    [CustomEditor(typeof(StartMono))]
    public class StartMonoEditor : Editor
    {
        private static readonly BindingFlags InstanceNonPublic =
            BindingFlags.Instance | BindingFlags.NonPublic;

        public override void OnInspectorGUI()
        {
            DrawRuntimeStatus((StartMono)target);
        }

        public override bool RequiresConstantRepaint()
        {
            return Application.isPlaying;
        }

        private static void DrawRuntimeStatus(StartMono startMono)
        {
            EditorGUILayout.LabelField("Runtime Monitor", EditorStyles.boldLabel);

            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox("Enter Play Mode to view live connection status.", MessageType.Info);
                return;
            }

            Service service = startMono.service;
            if (service == null)
            {
                EditorGUILayout.HelpBox("Service has not been created yet.", MessageType.Warning);
                return;
            }

            EditorGUILayout.LabelField("Service", service.GetType().Name);
            DrawServiceState(service);
            DrawConnects(service);
        }

        private static void DrawServiceState(Service service)
        {
            if (service is ServerService)
            {
                object state = GetFieldValue(service, "state");
                EditorGUILayout.LabelField("Server State", state == null ? "Unknown" : state.ToString());
                return;
            }

            if (service is ClientService)
            {
                IDictionary connectingTasks = GetFieldValue(service, "connectingTask") as IDictionary;
                EditorGUILayout.LabelField("Client State", GetClientState(service, connectingTasks));
            }
        }

        private static string GetClientState(Service service, IDictionary connectingTasks)
        {
            if (connectingTasks != null && connectingTasks.Count > 0)
            {
                return "Connecting";
            }

            IEnumerable connects = GetFieldValue(service, "connects") as IEnumerable;
            if (connects == null)
            {
                return "Unknown";
            }

            bool hasConnect = false;
            foreach (object item in connects)
            {
                TCPPair pair = GetPropertyValue(item, "Value") as TCPPair;
                if (pair == null || pair.connect == null)
                {
                    continue;
                }

                hasConnect = true;
                if (pair.connect.State == ConnectState.Connected)
                {
                    return "Connected";
                }
            }

            return hasConnect ? "Disconnected" : "No Connection";
        }

        private static void DrawConnects(Service service)
        {
            IEnumerable connects = GetFieldValue(service, "connects") as IEnumerable;
            if (connects == null)
            {
                EditorGUILayout.HelpBox("Unable to read connections.", MessageType.Warning);
                return;
            }

            int count = 0;
            foreach (object item in connects)
            {
                count++;
            }

            EditorGUILayout.LabelField("Connect Count", count.ToString());
            if (count == 0)
            {
                return;
            }

            EditorGUILayout.Space();
            foreach (object item in connects)
            {
                TCPPair pair = GetPropertyValue(item, "Value") as TCPPair;
                string id = GetPropertyValue(item, "Key").ToString();
                if (pair == null || pair.connect == null)
                {
                    EditorGUILayout.HelpBox("Invalid Connect: " + id, MessageType.Warning);
                    continue;
                }

                Connect connect = pair.connect;
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.LabelField("Connect " + id.Substring(0, 8), EditorStyles.boldLabel);
                EditorGUILayout.LabelField("State", connect.State.ToString());
                EditorGUILayout.LabelField("Remote", pair.IPEndPoint == null ? "Unknown" : pair.IPEndPoint.ToString());
                EditorGUILayout.LabelField("Last Receive", FormatElapsed(connect.LastReceiveTime));
                EditorGUILayout.LabelField("Receive Buffer", GetStreamLength(connect.readSteam) + " bytes");
                EditorGUILayout.LabelField("Send Buffer", GetStreamLength(connect.sendSteam) + " bytes");
                EditorGUILayout.EndVertical();
            }
        }

        private static object GetFieldValue(object instance, string fieldName)
        {
            Type type = instance.GetType();
            while (type != null)
            {
                FieldInfo field = type.GetField(fieldName, InstanceNonPublic);
                if (field != null)
                {
                    return field.GetValue(instance);
                }
                type = type.BaseType;
            }
            return null;
        }

        private static object GetPropertyValue(object instance, string propertyName)
        {
            PropertyInfo property = instance.GetType().GetProperty(propertyName);
            return property == null ? null : property.GetValue(instance, null);
        }

        private static string FormatElapsed(long lastReceiveTime)
        {
            if (lastReceiveTime <= 0)
            {
                return "No message received";
            }

            long elapsed = NetworkTimer.Instance.TimeNow - lastReceiveTime;
            return elapsed < 1000 ? elapsed + " ms ago" : (elapsed / 1000f).ToString("F1") + " s ago";
        }

        private static long GetStreamLength(System.IO.MemoryStream stream)
        {
            return stream == null ? 0 : stream.Length;
        }
    }
}
