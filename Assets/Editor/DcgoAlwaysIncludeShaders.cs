#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace DcgoWebBuild
{
    // No build WebGL, shaders trocados em runtime via Shader.Find (UIEffect, Glow,
    // fallback do TextMeshPro, etc.) sao removidos pelo stripping porque nenhum
    // material/cena os referencia diretamente -> renderizam magenta (SwiftShader)
    // ou branco (algumas GPUs). Este hook adiciona os shaders do projeto a
    // "Always Included Shaders" antes do build, garantindo que entrem no player.
    public class DcgoAlwaysIncludeShaders : IPreprocessBuildWithReport
    {
        public int callbackOrder => 0;

        public void OnPreprocessBuild(BuildReport report)
        {
            var gsObj = AssetDatabase.LoadAssetAtPath<Object>("ProjectSettings/GraphicsSettings.asset");
            if (gsObj == null)
            {
                Debug.LogWarning("[DCGO] GraphicsSettings.asset nao encontrado; pulando inclusao de shaders.");
                return;
            }

            var so = new SerializedObject(gsObj);
            var arr = so.FindProperty("m_AlwaysIncludedShaders");
            if (arr == null)
            {
                Debug.LogWarning("[DCGO] m_AlwaysIncludedShaders nao encontrado.");
                return;
            }

            var included = new HashSet<Object>();
            for (int i = 0; i < arr.arraySize; i++)
                included.Add(arr.GetArrayElementAtIndex(i).objectReferenceValue);

            var toAdd = new List<Shader>();

            // 1) Por nome: cobre os shaders buscados via Shader.Find (e fallbacks do TMP).
            string[] byName =
            {
                "TextMeshPro/Mobile/Distance Field",
                "TextMeshPro/Mobile/Distance Field SSD",
                "TextMeshPro/Distance Field",
                "TextMeshPro/Distance Field SSD",
                "TextMeshPro/Sprite",
                "UI/Unlit/Glow",
                "UI/ColorAdditive",
            };
            foreach (var n in byName)
            {
                var s = Shader.Find(n);
                if (s != null && included.Add(s)) toAdd.Add(s);
            }

            // 2) Todos os shaders sob Assets/ (AddOns, UIEffect e seus "Hidden/... (UIEffect)",
            //    efeitos de UI, particulas, shaders proprios do projeto).
            foreach (var guid in AssetDatabase.FindAssets("t:Shader"))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (string.IsNullOrEmpty(path) || !path.StartsWith("Assets/")) continue;
                var s = AssetDatabase.LoadAssetAtPath<Shader>(path);
                if (s != null && included.Add(s)) toAdd.Add(s);
            }

            foreach (var s in toAdd)
            {
                arr.arraySize++;
                arr.GetArrayElementAtIndex(arr.arraySize - 1).objectReferenceValue = s;
            }
            so.ApplyModifiedProperties();

            Debug.Log($"[DCGO] Always Included Shaders += {toAdd.Count} (total agora {arr.arraySize}).");
        }
    }
}
#endif
