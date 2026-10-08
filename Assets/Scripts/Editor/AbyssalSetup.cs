#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using System;
using System.Linq;

namespace AbyssalProtocol.Editor
{
    public static class AbyssalSetup
    {
        [InitializeOnLoadMethod]
        static void EnsureRuntimeMaterial()
        {
            EditorApplication.delayCall += () =>
            {
                const string folder="Assets/Resources";
                if(!AssetDatabase.IsValidFolder(folder))AssetDatabase.CreateFolder("Assets","Resources");
                const string path=folder+"/AbyssalRuntime.mat";
                if(AssetDatabase.LoadAssetAtPath<Material>(path)!=null)return;
                var shader=Shader.Find("Universal Render Pipeline/Lit");
                if(shader==null)shader=Shader.Find("Standard");
                if(shader==null)shader=Shader.Find("Unlit/Color");
                if(shader==null){Debug.LogError("Abyssal Protocol: no compatible shader is available in the Editor.");return;}
                AssetDatabase.CreateAsset(new Material(shader),path);
                AssetDatabase.SaveAssets();
            };
        }

        [MenuItem("Abyssal/Create Playable Scene")]
        public static void CreatePlayableScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var bootstrap = new GameObject("Campaign Bootstrap");
            var componentType = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(a => { try { return a.GetTypes(); } catch { return Type.EmptyTypes; } })
                .FirstOrDefault(t => typeof(MonoBehaviour).IsAssignableFrom(t) &&
                    (t.Name == "AbyssalManager" || t.Name == "GameManager"));
            if (componentType != null) bootstrap.AddComponent(componentType);
            else Debug.LogWarning("No se encontró el componente gestor. Revisa que AbyssalGame.cs esté dentro de Assets y compile sin errores.");
            if (!AssetDatabase.IsValidFolder("Assets/Scenes"))
                AssetDatabase.CreateFolder("Assets", "Scenes");
            EditorSceneManager.SaveScene(scene, "Assets/Scenes/Campaign.unity");
            Debug.Log("Playable campaign created at Assets/Scenes/Campaign.unity");
        }
    }
}
#endif
