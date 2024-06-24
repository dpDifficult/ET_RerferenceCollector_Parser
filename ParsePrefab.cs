using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace ET
{
    public class ParsePrefab : EditorWindow
    {
#if UNITY_EDITOR
        [MenuItem("CodeGenerationTools/ParsePrefab")] // 创建一个菜单项，在Unity菜单栏中显示
        public static void ShowWindow()
        {
            EditorWindow.GetWindow(typeof(ParsePrefab)); // 创建并显示自定义Editor窗口
        }

        void OnGUI()
        {
            GUILayout.Label("从Project中拖入___Window.prefab", EditorStyles.boldLabel);

            Event evt = Event.current;
            Rect dropArea = GUILayoutUtility.GetRect(0.0f, 50.0f, GUILayout.ExpandWidth(true));
            GUI.Box(dropArea, "Drop files here");

            switch (evt.type)
            {
                case EventType.DragUpdated:
                case EventType.DragPerform:
                    if (!dropArea.Contains(evt.mousePosition))
                        break;

                    DragAndDrop.visualMode = DragAndDropVisualMode.Copy;

                    if (evt.type == EventType.DragPerform)
                    {
                        DragAndDrop.AcceptDrag();

                        foreach (var draggedObject in DragAndDrop.objectReferences)
                        {
                            this.path = AssetDatabase.GetAssetPath(draggedObject);
                            if (!string.IsNullOrEmpty(path) && File.Exists(path))
                            {
                                break;
                            }
                        }
                    }

                    Event.current.Use();
                    break;
            }

            GUILayout.Label("File Path: " + this.path, EditorStyles.wordWrappedLabel);

            if (GUILayout.Button("Load Window Prefab and Extract Data"))
            {
                GetReferenceCollector();
            }

            GUILayout.Space(20);
            GUILayout.BeginHorizontal();
            GUILayout.Space(20);

            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, GUILayout.Height(500)); // 开始滚动视图
            input = EditorGUILayout.TextArea(input, GUILayout.ExpandHeight(true)); // 文本区域，允许高度扩展
            EditorGUILayout.EndScrollView(); // 结束滚动视图
            GUILayout.EndHorizontal();
        }


        private string path;
        public List<string> Data = new List<string>(64);
        public string input;


        private Vector2 scrollPosition = Vector2.zero;

        private void GetReferenceCollector()
        {
            input = "";

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(this.path);
            if (prefab == null)
            {
                Debug.LogError("prefab is null , check the file path");
                return;
            }

            var rc = prefab.GetComponent<ReferenceCollector>();
            if (rc == null)
            {
                Debug.LogError("the prefab without RerferenceCollector Component!");
                return;
            }

            ReferenceCollectorParser(prefab.name, rc, 0);
        }

        private void ReferenceCollectorParser(string titleName, ReferenceCollector rc, int depth)
        {
            SerializedObject serializedObject = new SerializedObject(rc);
            SerializedProperty dataProperty = serializedObject.FindProperty("data");

            for (int i = 0; i < dataProperty.arraySize; ++i)
            {
                SerializedProperty element = dataProperty.GetArrayElementAtIndex(i);
                var obj = element.FindPropertyRelative("gameObject").objectReferenceValue;

                if (obj == null)
                {
                    Debug.LogWarning(titleName + " reference collector组件右侧有空对象!" );
                }
                else
                {
                    string nameStr = obj.name;
                    string typeStr = obj.GetType().ToString();
                    int lastIndex = typeStr.LastIndexOf('.');

                    if (lastIndex != -1 && lastIndex < typeStr.Length - 1)
                    {
                        typeStr = typeStr.Substring(lastIndex + 1); // 提取点号后的子字符串
                    }

                    Data.Add(typeStr);
                    Data.Add(nameStr);
                }
            }

            if (titleName.Contains("Window"))
            {
                WindowDataListOutput(titleName);
            }
            else
            {
                FormDataListOutput(titleName);
            }

            Data.Clear();

            for (int i = 0; i < dataProperty.arraySize; ++i)
            {
                SerializedProperty element = dataProperty.GetArrayElementAtIndex(i);
                var obj = element.FindPropertyRelative("gameObject").objectReferenceValue;

                if (obj is GameObject || obj is Component) //先判断是不是gameObject和组件
                {
                    var go = obj as GameObject;
                    if (!go)
                    {
                        go = ((Component)obj).gameObject;
                    }
                    List<Component> componentList = new List<Component>(4);
                    go.GetComponents(componentList); //将gameObject身上所有的组件存放到list里
                    foreach (var component in componentList)
                    {
                        if (component is ReferenceCollector && go.name.Contains("Form"))
                        {
                            if (depth > 0)
                            {
                                continue;
                            }

                            ReferenceCollectorParser(go.name, component as ReferenceCollector, 1);
                        }
                    }
                }
            }
        }

        private void FormDataListOutput(string titleName)
        {
            StringBuilder stringBuilder = new StringBuilder();
            stringBuilder .Append("******" + titleName + "******" + "\n");
            
            for (int i = 0; i < Data.Count; i += 2)
            {
                stringBuilder.Append("public " + Data[i] + " " + Data[i + 1] + ";\n");
            }

            stringBuilder.Append("\n");

            stringBuilder.Append("************" + titleName + "System :" + "************" + "\n");

            for (int i = 0; i < Data.Count; i += 2)
            {
                stringBuilder.Append("self." + Data[i + 1] + " = " + "self.Collector.Get<" + Data[i] + ">(" + "\"" +
                                     Data[i + 1] + "\");");
                stringBuilder.Append("\n");
            }

            input += stringBuilder.ToString();
            input += "\n\n";
        }
        
        private void WindowDataListOutput(string titleName)
        {
            StringBuilder stringBuilder = new StringBuilder();
            // stringBuilder.Append("******" + titleName + "******" + "\n");
            //
            // for (int i = 0; i < Data.Count; i += 2)
            // {
            //     stringBuilder.Append("public " + Data[i] + " " + Data[i + 1] + ";\n");
            // }

            //stringBuilder.Append("\n");
            
            //self.AddForm<LuckyTurntableMainForm>(self.Collector.Get<GameObject>("MainForm"));

            string frontName = titleName.Substring(0, titleName.IndexOf("Window")); //去除Window后缀
            
            stringBuilder.Append("************" + titleName + "System :" + "************" + "\n");

            for (int i = 0; i < Data.Count; i += 2)
            {
                stringBuilder.Append("self.AddForm<" + frontName + Data[i + 1] + ">(self.Collector.Get<" + Data[i] + ">(" + "\"" +
                                     Data[i + 1] + "\");");
                stringBuilder.Append("\n");
            }

            input += stringBuilder.ToString();
            input += "\n\n";
        }


#endif
    }
}
