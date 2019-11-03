using UnityEngine;
using UnityEngine.SceneManagement;

namespace StoryGenerator.Rendering
{

    public class MasterRenderer : MonoBehaviour
    {
        public static RenderPref renderPreference = null;
        
        void Start()
        {
            string dontCare = null;
            renderPreference = RenderPref.Load(System.IO.Path.Combine(RenderPref.DEFAULT_CONFIG_DIR,
                RenderPref.CUR_RENDER_PREF_FILENAME), ref dontCare);
            Debug.Assert(renderPreference != null, string.Format("{0} is not found! Please click Apply on Master Settings window!",
              RenderPref.CUR_RENDER_PREF_FILENAME));
            renderPreference.Prepare();
            SceneManager.LoadScene(renderPreference.GetRandomScene());
        }
    }

}