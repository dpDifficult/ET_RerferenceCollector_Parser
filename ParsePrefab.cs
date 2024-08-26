using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace ET
{
    public class ParsePrefab : EditorWindow
    {
#if UNITY_EDITOR
        [MenuItem("CodeGenerationTools/获取预制体中的ReferenceCollector的初始化代码")] // 创建一个菜单项，在Unity菜单栏中显示
        public static void ShowWindow()
        {
            EditorWindow.GetWindow(typeof(ParsePrefab)); // 创建并显示自定义Editor窗口
        }

        void OnGUI()
        {
            GUILayout.Label("从Project中拖入___Window.prefab", EditorStyles.boldLabel);

            Event evt = Event.current;
            Rect dropArea = GUILayoutUtility.GetRect(0.0f, 50.0f, GUILayout.ExpandWidth(true));
            GUI.Box(dropArea, "将prefab拖入此处");

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
                            ParsePrefab.path = AssetDatabase.GetAssetPath(draggedObject);
                            if (!string.IsNullOrEmpty(path) && File.Exists(path))
                            {
                                break;
                            }
                        }
                    }

                    Event.current.Use();
                    break;
            }

            GUILayout.Label("File Path: " + ParsePrefab.path, EditorStyles.wordWrappedLabel);

            if (GUILayout.Button("解析"))
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


        private static string path;
        public static List<string> Data = new List<string>(64);
        public static List<string> FormName = new List<string>(32);
        public static List<string> CellName = new List<string>();
        public static HashSet<string> Fitter = new HashSet<string>();
        public static Dictionary<string, string> Code = new Dictionary<string, string>();
        public string input;

        private Vector2 scrollPosition = Vector2.zero;

        private void GetReferenceCollector()
        {
            Data.Clear();
            FormName.Clear();
            input = "";

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
            {
                Debug.LogError("prefab is null , check the file path");
                return;
            }

            var rc = prefab.GetComponent<ReferenceCollector>();
            if (rc == null)
            {
                Debug.LogError("the prefab without ReferenceCollector Component!");
                return;
            }

            ReferenceCollectorParser(prefab.name, rc, 0);
        }

        void ReferenceCollectorParser(string titleName, ReferenceCollector rc, int depth)
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

            string frontName = titleName.Substring(0, titleName.IndexOf("Window")); //去除Window后缀
            
            stringBuilder.Append("************" + titleName + "System :" + "************" + "\n");

            for (int i = 0; i < Data.Count; i += 2)
            {
                stringBuilder.Append("self.AddForm<" + frontName + Data[i + 1] + ">(self.Collector.Get<" + Data[i] + ">(" + "\"" +
                                     Data[i + 1] + "\"));");
                stringBuilder.Append("\n");
            }

            input += stringBuilder.ToString();
            input += "\n\n";
        }


        public static void Clear()
        {
            Data.Clear();
            FormName.Clear();
            CellName.Clear();
            Code.Clear();
            Fitter.Clear();
        }

        public static int GetLayer(GameObject prefab)
        {
            var canvas = prefab.GetComponent<Canvas>();
            if (canvas == null)
            {
                throw new System.Exception("Prefab Without Canvas!");
            }
            return canvas.sortingOrder;
        }
        
        public static void ReferenceCollectorParser_Static(string titleName, ReferenceCollector rc)
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
                GenerateWindowCode(titleName);
            }
            else
            {
                GenerateFormCode(titleName);
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
                        if (component is ReferenceCollector && !Fitter.Contains(go.name))
                        {
                            if(go.name.Contains("Form"))
                            {
                                FormName.Add(go.name);
                            }

                            Fitter.Add(go.name);

                            ReferenceCollectorParser_Static(go.name, component as ReferenceCollector);
                        }
                        else if (component is MyScrController)
                        {
                            var cellRc = (component as MyScrController).cell.GetComponent<ReferenceCollector>();
                            string cellTitleName = FindParentForm(cellRc.gameObject).name.Replace("Form","") + cellRc.name;
                            CellName.Add(cellTitleName);
                            ReferenceCollectorParser_Static(cellTitleName ,cellRc);
                        }
                    }
                }
            }
        }

        static GameObject FindParentForm(GameObject child)
        {
            if (child.name.Contains("Form"))
            {
                return child;
            }

            return FindParentForm(child.transform.parent.gameObject);
        }

        public static void GenerateFormCode(string titleName)
        {
            StringBuilder stringBuilder = new StringBuilder();
            
            for (int i = 0; i < Data.Count; i += 2)
            {
                stringBuilder.Append("public " + Data[i] + " " + Data[i + 1] + ";\n");
            }

            stringBuilder.Append("\n");

            Code.TryAdd(titleName, stringBuilder.ToString());

            stringBuilder.Clear();

            for (int i = 0; i < Data.Count; i += 2)
            {
                stringBuilder.Append("self." + Data[i + 1] + " = " + "self.Collector.Get<" + Data[i] + ">(" + "\"" +
                                     Data[i + 1] + "\");");
                stringBuilder.Append("\n");
            }

            Code.TryAdd(titleName + "System", stringBuilder.ToString());
        }
        
        public static void GenerateWindowCode(string titleName)
        {
            StringBuilder stringBuilder = new StringBuilder();
            
            stringBuilder.Append("\n");
            
            Code.Add(titleName,stringBuilder.ToString());

            stringBuilder.Clear();

            for (int i = 0; i < Data.Count; i += 2)
            {
                stringBuilder.Append("self.AddForm<" + FrameCode.NameWithoutSuffix + Data[i + 1] + ">(self.Collector.Get<" + Data[i] + ">(" + "\"" +
                                     Data[i + 1] + "\"));");
                stringBuilder.Append("\n");
            }

            Code.Add(titleName + "System", stringBuilder.ToString());
        }
#endif
    }
}
