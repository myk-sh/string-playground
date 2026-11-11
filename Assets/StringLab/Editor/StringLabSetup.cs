using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using System.Linq;

namespace StringLab.Editor
{
    public static class StringLabSetup
    {
        public const string ScenePath = "Assets/Scenes/StringLaboratory.unity";
        [MenuItem("String Lab/Open Playground")]
        public static void OpenPlayground()
        {
            if (EditorApplication.isPlaying) return;
            if (System.IO.File.Exists(ScenePath))
            {
                if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) EditorSceneManager.OpenScene(ScenePath);
                return;
            }
            Create();
        }
        public static string Create()
        {
            var existing = SceneManager.GetSceneByPath(ScenePath);
            if (existing.IsValid() && existing.isLoaded) return ScenePath;
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            var camera = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener)); camera.tag = "MainCamera";
            camera.GetComponent<Camera>().backgroundColor = new Color(.035f,.065f,.077f);
            var light = new GameObject("Key light",typeof(Light)).GetComponent<Light>();light.type=LightType.Directional;
            light.intensity=1.5f;light.transform.rotation=Quaternion.Euler(45,-25,0);
            var lab = new GameObject("String Laboratory").AddComponent<StringPlayground>();
            var fonts = AssetDatabase.FindAssets("LiberationSans SDF t:TMP_FontAsset");
            if (fonts.Length == 0) throw new System.InvalidOperationException("TMP font resources are required before setup.");
            lab.font = AssetDatabase.LoadAssetAtPath<TMPro.TMP_FontAsset>(AssetDatabase.GUIDToAssetPath(fonts[0]));
            EditorSceneManager.SaveScene(scene,ScenePath);
            var scenes = EditorBuildSettings.scenes.Where(s=>s.path!=ScenePath).ToList();
            scenes.Insert(0,new EditorBuildSettingsScene(ScenePath,true));EditorBuildSettings.scenes=scenes.ToArray();
            Selection.activeGameObject=lab.gameObject;
            return ScenePath;
        }
    }
}
