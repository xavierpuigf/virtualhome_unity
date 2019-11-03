using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif
using System.Collections.Generic;
using System.IO;

using StoryGenerator.Recording;
using StoryGenerator.Helpers;

namespace StoryGenerator.Rendering
{
    [System.Serializable]
    public class RenderPref
    {
        public const string DEFAULT_CONFIG_DIR = "Config";
        public const string DEFAULT_RENDER_PREF_FILENAME = "UserPref.json";
        public const string CUR_RENDER_PREF_FILENAME = "RenderPreference.json";
        public const string EXT_RENDER_PREF = "json";
        public const string PATH_CHARS = "Chars/";
        public const string PATH_SCENES = "Assets/Story Generator/TestScene/";

        public BaseSettings baseSettings = new BaseSettings ();
        public DebugSettings debugSettings = new DebugSettings();
        public CharSelection charSelection = new CharSelection();
        public SceneSelection sceneSelection = new SceneSelection();
        public DirSelection dirSelection = new DirSelection();

        [System.Serializable]
        public class BaseSettings
        {
            public bool processScriptOnly = false;
            public string ffmpegPath = "";
            public string outPath = "";
            public bool randomExec = false;
            public int  randomSeed = -1;
            public int frameRate = 5;
            public FrameGeneration fg = FrameGeneration.None;
            public bool savePoseData = false;
            public bool saveSceneStates = false;
            public bool autoDoorOpening = false;
            public int processingTimeLimit = 10;

            public bool ErrorCheck(ref string ioMsg)
            {
                if (ffmpegPath == null || ffmpegPath == "")
                {
                    ioMsg = "ERROR\nFFmpeg path is not specified.";
                    return true;
                }

                if (outPath == null || outPath == "")
                {
                    ioMsg = "ERROR\nOutput path is not specified.";
                    return true;
                }

                if (frameRate < 1)
                {
                    ioMsg = "ERROR\nFrame rate must be greater or equal to 1. 15 is the recommended value.";
                    return true;
                }

                if (processingTimeLimit <= 0)
                {
                    ioMsg = "ERROR\nProcessing time limit must be greater than 0 seconds.";
                    return true;
                }
                return false;
            }
        };

        [System.Serializable]
        public class DebugSettings
        {
            public bool pauseAfterEachScript = false;
            public bool keepRawFrames = false;
            public bool dontMoveDoneScripts = false;
        }

        // We don't actually need separate class but Unity JSON serialization doensn't work
        // we just use ItemSelectionWeighted<T>
        [System.Serializable]
        public class CharSelection : ItemSelectionWeighted<CharSettings> {}
        [System.Serializable]
        public class SceneSelection : ItemSelectionWeighted<SceneSettings> {}


        [System.Serializable]
        public class ItemSelectionWeighted<T> : ItemSelection<T> where T : UseWeight
        {
            // Need to save what we returned so we can get some statistics later on.
            public string curItem {get; protected set;}

            [System.NonSerialized]
            List<float> weights_items_inUse = new List<float> ();

            public override void Prepare()
            {
                base.Prepare();

                for (int i = 0; i < items.Count; i++)
                {
                    if (items[i].use)
                    {
                        weights_items_inUse.Add(items[i].weight);
                    }
                }
            }

            public string GetItem()
            {
                int rand = Helper.GetRandomWeightedIndex(weights_items_inUse);
                int idx_curItem = mapping_idx_to_items_inUse[rand];

                curItem = items[idx_curItem].name;
                return curItem;
            }
        }

        [System.Serializable]
        public class DirSelection : ItemSelection<ScriptDirectory>
        {
            [System.NonSerialized]
            int curFolderIdx = 0;

            public override void Prepare()
            {
                base.Prepare();
                foreach (ScriptDirectory sd in items)
                {
                    if (sd.use)
                    {
                        sd.Prepare();
                    }
                }
            }

            public bool HasNextScript()
            {
                if (curFolderIdx < mapping_idx_to_items_inUse.Count - 1)
                {
                    if (! items[ mapping_idx_to_items_inUse[curFolderIdx] ].HasNextScript() )
                    {
                        curFolderIdx++;
                        RenderProgress.Reset();
                        return items[ mapping_idx_to_items_inUse[curFolderIdx] ].HasNextScript();
                    }
                }
                // If we are currently at last folder to render
                else
                {
                    if (! items[ mapping_idx_to_items_inUse[curFolderIdx] ].HasNextScript() )
                    {
                        return false;
                    }
                }

                return true;
            }

            public ScriptDirectory GetDirectory()
            {
                return items[ mapping_idx_to_items_inUse[curFolderIdx] ];
            }
        }

        [System.Serializable]
        public class ItemSelection<T> where T : Use
        {
            public List<T> items =  new List<T> ();

            [System.NonSerialized]
            protected List<int> mapping_idx_to_items_inUse =  new List<int> ();

            public virtual void Prepare()
            {
                for (int i = 0; i < items.Count; i++)
                {
                    if (items[i].use)
                    {
                        mapping_idx_to_items_inUse.Add(i);
                    }
                }
            }

            public bool ErrorCheck(ref string ioMsg)
            {
                if (items.Count < 1)
                {
                    System.Type myType = typeof(T);
                    ioMsg = "ERROR\nAt least one " + myType.ToString() + " must be used.";
                    return true;
                }
                foreach(T t in items)
                {
                    if ( t.ErrorCheck(ref ioMsg) )
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        // Classes for character settings
        [System.Serializable]
        public class CharSettings : UseWeight
        {
            public CharSettings() : base("character prefabs") {}
        }

        // Classes for scene settings
        [System.Serializable]
        public class SceneSettings : UseWeight
        {
            public SceneSettings() : base("the scenes") {}
        }

        [System.Serializable]
        public class UseWeight : Use
        {
            public string name = null;
            public float weight = 1;

            [System.NonSerialized]
            public Object unityObject = null;

            [System.NonSerialized]            
            readonly string error_prefix;

            public UseWeight(string _error_prefix)
            {
                error_prefix = _error_prefix;
            }

            public override bool ErrorCheck(ref string ioMsg)
            {
                if (use && weight <= 0)
                {
                    ioMsg = "ERROR\nWeight must be a positive number";
                    return true;
                }
                if (unityObject == null)
                {
                    ioMsg = "ERROR\nCannot find one of " + error_prefix;
                    return true;
                }
                return false;
            }
        }

        // Classes for folder location that has scripts to be rendered
        [System.Serializable]
        public class ScriptDirectory : Use
        {
            public string path = "";
            public string pattern = "";
            public int iteration = 1;

            public string curScript {get; private set;}
            public int curIteration {get; private set;} = 1;

            [System.NonSerialized]
            DirectoryInfo di;
            [System.NonSerialized]
            FileInfo[] fi_scripts;
            [System.NonSerialized]
            int curScriptIdx = 0;

            public void Prepare()
            {
                di = new DirectoryInfo(path);
                fi_scripts = di.GetFiles(pattern);
            }

            public bool HasNextScript()
            {
                if (curScriptIdx == fi_scripts.Length)
                {
                    if (curIteration < iteration)
                    {
                        curScriptIdx = 0;
                        curIteration++;

                        // Update the scripts list because completed files have been moved
                        fi_scripts =  di.GetFiles(pattern);
                        return true;
                    }
                    return false;
                }

                return true;
            }

            public string NextScript()
            {
                string retVal = fi_scripts[curScriptIdx].FullName;
                curScript = fi_scripts[curScriptIdx].Name;
                curScriptIdx++;
                return retVal;
            }

            public override bool ErrorCheck(ref string ioMsg)
            {
                if (path == null)
                {
                    ioMsg = "ERROR\nOne of the folder path is empty.";
                    return true;
                }
                if (iteration < 1)
                {
                    ioMsg = "ERROR\nIteration must be greater or equal to 1.";
                    return true;
                }
                return false;
            }
        }

        public abstract class Use
        {
            public bool use = true;

            public abstract bool ErrorCheck(ref string ioMsg);
        }

        // Which type frames are generated?
        public enum FrameGeneration
        {
            RGB = 0,
            GT,
            None
        }

        public string Save(string path)
        {
            string ioMsg = null;
            if ( ErrorCheck(ref ioMsg) )
            {
                return ioMsg;
            }
            try
            {
                using ( StreamWriter sw = new StreamWriter(path) )
                {
                    sw.WriteLine( JsonUtility.ToJson(this, true) );
                }
                ioMsg = "Successfully saved current setting";
            }
            catch (System.Exception e)
            {
                ioMsg = e.Message;
            }

            return ioMsg;
        }

        public static RenderPref Load(string path, ref string ioMsg)
        {
            RenderPref rp = new RenderPref();
            if (File.Exists(path))
            {
                try
                {
                    using ( StreamReader sr = new StreamReader(path) )
                    {
                        string str_file = sr.ReadToEnd();
                        rp = JsonUtility.FromJson<RenderPref> (str_file);
                    }
                    ioMsg = "Successfully loaded setting from file";

                    foreach(RenderPref.CharSettings cs in rp.charSelection.items)
                    {
                        if (cs.name != null)
                        {
                            cs.unityObject = Resources.Load(PATH_CHARS + cs.name);
                        }
                    }

                    foreach(RenderPref.SceneSettings ss in rp.sceneSelection.items)
                    {
                        if (ss.name != null)
                        {
#if UNITY_EDITOR
                            ss.unityObject = AssetDatabase.LoadAssetAtPath(PATH_SCENES + ss.unityObject + ".unity", typeof(SceneAsset));
#endif
                        }
                    }
                }
                catch (System.Exception e)
                {
                    ioMsg = e.Message;
                }
            }
            else
            {
                return null;
            }

            return rp;
        }

        public void Prepare()
        {
            charSelection.Prepare();
            sceneSelection.Prepare();
            dirSelection.Prepare();
        }

        public string GetRandomChar()
        {
            return PATH_CHARS + charSelection.GetItem();
        }

        public string GetRandomScene()
        {
            return sceneSelection.GetItem();
        }

        public bool HasNextScript()
        {
            return dirSelection.HasNextScript();
        }

        public string NextScript()
        {
            ScriptDirectory sd = dirSelection.GetDirectory();
            string retVal = sd.NextScript();
            RenderProgress.UpdateFileInfo(sd);
            return retVal;
        }

        public string GetCurrentCharacter()
        {
            RenderProgress.UpdateCharDist(charSelection.curItem);
            return PATH_CHARS + charSelection.curItem;
        }

        public string GetCurrentScene()
        {
            RenderProgress.UpdateSceneDist(sceneSelection.curItem);
            return sceneSelection.curItem;
        }

        public string GetCurrentFolder()
        {
            return dirSelection.GetDirectory().path;
        }

        bool ErrorCheck(ref string ioMsg)
        {
            if ( baseSettings.ErrorCheck(ref ioMsg) )
            {
                return true;
            }
            if ( charSelection.ErrorCheck(ref ioMsg) )
            {
                return true;
            }
            if ( sceneSelection.ErrorCheck(ref ioMsg) )
            {
                return true;
            }
            if ( dirSelection.ErrorCheck(ref ioMsg) )
            {
                return true;
            }

            return false;
        }
    }

    // Variables to store progress
    public static class RenderProgress
    {
        public static int num_rendered = 0;
        public static int num_processed = 0;

        // The folder that contains currently rendering script
        public static string curFolder = null;
        public static int curIteration = 1;
        public static int iteration = 1;
        public static string curScript = null;

        public static Dictionary<string, int> charDistribution = new Dictionary<string, int> ();
        public static Dictionary<string, int> sceneDistribution = new Dictionary<string, int> ();

        public static void UpdateFileInfo(RenderPref.ScriptDirectory sd)
        {
            curFolder = sd.path;
            curIteration = sd.curIteration;
            iteration = sd.iteration;
            curScript = sd.curScript;
        }

        public static void UpdateCharDist(string curChar)
        {
            if ( !charDistribution.ContainsKey(curChar) )
            {
                charDistribution.Add(curChar, 0);
            }
            charDistribution[curChar]++;
        }

        public static void UpdateSceneDist(string curScene)
        {
            if ( !sceneDistribution.ContainsKey(curScene) )
            {
                sceneDistribution.Add(curScene, 0);
            }
            sceneDistribution[curScene]++;
        }

        public static void Reset()
        {
            num_rendered = 0;
            num_processed = 0;
            curScript = null;
            curFolder = null;
            curIteration = 1;
            charDistribution.Clear();
            sceneDistribution.Clear();
        }
    };

}
