using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;

using StoryGenerator.Rendering;

public class MasterSettings : EditorWindow
{    
    string[] fgDescription = new string[] {"RGB Only", "RGB + GT", "None"};

    // Boolean to show/hide details
    bool showSettings_base = true;
    bool showSettings_debug = true;
    bool showSettings_char = true;
    bool showSettings_scene = true;
    bool showSettings_ftr = true;
    bool showProgress = true;

    Vector2 scrollPos;
    RenderPref rp;
    string ioMsg;


    [MenuItem ("Window/Master Settings")]
    static void Init()
    {
        // Get existing open window or if none, make a new one:
        MasterSettings rp = (MasterSettings)EditorWindow.GetWindow(typeof(MasterSettings));
        rp.minSize = new Vector2(270, 400);
        rp.Show();
    }

    void Awake()
    {
        // Load preference file if exists
        RenderPref rp_tmp = RenderPref.Load(Path.Combine(RenderPref.DEFAULT_CONFIG_DIR, RenderPref.CUR_RENDER_PREF_FILENAME),
            ref ioMsg);
        if (rp_tmp != null)
        {
            rp = rp_tmp;
        }
        else
        {
            rp = new RenderPref();
        }
    }

    void OnGUI()
    {
        const float MAX_WIDTH_NAV_BUTTON = 25.0f;
        const float MAX_WIDTH_USE = 15.0f;
        const float MIN_WIDTH_OBJ_FIELD = 185.0f;

        EditorStyles.foldout.fontStyle = FontStyle.Bold;
        EditorStyles.label.wordWrap = true;

        scrollPos =  EditorGUILayout.BeginScrollView(scrollPos, false, false);

        showSettings_base = EditorGUILayout.Foldout(showSettings_base, "Base Settings");
        if (showSettings_base)
        {
            rp.baseSettings.processScriptOnly = EditorGUILayout.Toggle("Process Script Only", rp.baseSettings.processScriptOnly);
            rp.baseSettings.ffmpegPath = EditorGUILayout.TextField ("FFmpeg Path", rp.baseSettings.ffmpegPath);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.TextField ("Output Path", rp.baseSettings.outPath);
            if ( GUILayout.Button("...", GUILayout.MaxWidth(MAX_WIDTH_NAV_BUTTON)) )
            {
                string path = EditorUtility.OpenFolderPanel("Choose folder for rendered data", rp.baseSettings.outPath, "");
                if (path.Length != 0)
                {
                    rp.baseSettings.outPath = path;
                }
                EditorGUIUtility.ExitGUI();
            }
            EditorGUILayout.EndHorizontal();

            rp.baseSettings.randomExec = EditorGUILayout.Toggle("Random Execution", rp.baseSettings.randomExec);
            rp.baseSettings.randomSeed = EditorGUILayout.DelayedIntField("Random Seed", rp.baseSettings.randomSeed);
            rp.baseSettings.frameRate = EditorGUILayout.DelayedIntField("Frame Rate (FPS)", rp.baseSettings.frameRate);
            rp.baseSettings.fg = (RenderPref.FrameGeneration) System.Enum.ToObject(
                typeof(RenderPref.FrameGeneration),
                EditorGUILayout.Popup("Frame Generation", (int) rp.baseSettings.fg, fgDescription));
            rp.baseSettings.savePoseData = EditorGUILayout.Toggle("Save Pose Data", rp.baseSettings.savePoseData);
            rp.baseSettings.saveSceneStates = EditorGUILayout.Toggle("Save Scene States", rp.baseSettings.saveSceneStates);
            rp.baseSettings.autoDoorOpening = EditorGUILayout.Toggle("Auto Door Opening", rp.baseSettings.autoDoorOpening);
            rp.baseSettings.processingTimeLimit = EditorGUILayout.DelayedIntField("Processing Time Limit", rp.baseSettings.processingTimeLimit);
        }

        EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
        showSettings_debug = EditorGUILayout.Foldout(showSettings_debug, "Debug Options");
        if (showSettings_debug)
        {
            rp.debugSettings.pauseAfterEachScript = EditorGUILayout.Toggle("Pause After Each Script", rp.debugSettings.pauseAfterEachScript);
            rp.debugSettings.keepRawFrames = EditorGUILayout.Toggle("Keep Raw Frames", rp.debugSettings.keepRawFrames);
            rp.debugSettings.dontMoveDoneScripts = EditorGUILayout.Toggle("Do Not Move Done Scripts", rp.debugSettings.dontMoveDoneScripts);
        }
        
        EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
        showSettings_char = EditorGUILayout.Foldout(showSettings_char, "Characters");
        if (showSettings_char)
        {
            int newNum = EditorGUILayout.DelayedIntField("Number of characters", rp.charSelection.items.Count);
            int diff = rp.charSelection.items.Count - newNum;
            // Append new entries
            if (diff < 0)
            {
                // Trick to avoid doing diff = |diff|
                for (int i = 0; i > diff; i--)
                {
                    rp.charSelection.items.Add( new RenderPref.CharSettings() );
                }
            }
            // Delete from the end of the list
            else if (diff > 0)
            {
                rp.charSelection.items.RemoveRange(rp.charSelection.items.Count - diff, diff);
            }

            foreach (RenderPref.CharSettings cs in rp.charSelection.items)
            {
                EditorGUILayout.BeginHorizontal();
                cs.use = EditorGUILayout.ToggleLeft("", cs.use, GUILayout.MaxWidth(MAX_WIDTH_USE));
                cs.unityObject = EditorGUILayout.ObjectField(cs.unityObject, typeof(Object), false,
                    GUILayout.MinWidth(MIN_WIDTH_OBJ_FIELD));
                if (cs.unityObject == null)
                {
                    if (cs.name != null)
                    {
                        cs.unityObject = Resources.Load(RenderPref.PATH_CHARS + cs.name);
                    }
                }
                else
                {
                    cs.name = cs.unityObject.name;
                }
                cs.weight = EditorGUILayout.DelayedFloatField(cs.weight);
                EditorGUILayout.EndHorizontal();
            }

            if (rp.charSelection.items.Count != 0)
            {
                if (GUILayout.Button("Reset Proportion"))
                {
                    foreach (RenderPref.CharSettings cs in rp.charSelection.items)
                    {
                        if (cs.use)
                        {
                            cs.weight = 1.0f;
                        }
                    }
                }
            }
        }

        EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
        showSettings_scene = EditorGUILayout.Foldout(showSettings_scene, "Scenes");
        if (showSettings_scene)
        {
            int newNum = EditorGUILayout.DelayedIntField("Number of Scenes", rp.sceneSelection.items.Count);
            int diff = rp.sceneSelection.items.Count - newNum;
            // Append new entries
            if (diff < 0)
            {
                // Trick to avoid doing diff = |diff|
                for (int i = 0; i > diff; i--)
                {
                    rp.sceneSelection.items.Add( new RenderPref.SceneSettings() );
                }
            }
            // Delete from the end of the list
            else if (diff > 0)
            {
                rp.sceneSelection.items.RemoveRange(rp.sceneSelection.items.Count - diff, diff);
            }

            foreach(RenderPref.SceneSettings ss in rp.sceneSelection.items)
            {
                EditorGUILayout.BeginHorizontal();
                ss.use = EditorGUILayout.ToggleLeft("", ss.use, GUILayout.MaxWidth(MAX_WIDTH_USE));
                ss.unityObject = EditorGUILayout.ObjectField(ss.unityObject, typeof(SceneAsset), false,
                    GUILayout.MinWidth(MIN_WIDTH_OBJ_FIELD));
                if (ss.unityObject == null)
                {
                    if (ss.name != null)
                    {
                        ss.unityObject = AssetDatabase.LoadAssetAtPath(RenderPref.PATH_SCENES +
                            ss.name + ".unity", typeof(SceneAsset));
                    }
                }
                else
                {
                    ss.name = ss.unityObject.name;
                }
                ss.weight = EditorGUILayout.DelayedFloatField(ss.weight);
                EditorGUILayout.EndHorizontal();
            }

            if (rp.sceneSelection.items.Count != 0)
            {
                if ( GUILayout.Button("Reset Proportion") )
                {
                    foreach(RenderPref.SceneSettings ss in rp.sceneSelection.items)
                    {
                        if (ss.use)
                        {
                            ss.weight = 1.0f;
                        }
                    }
                }
            }
        }

        EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
        showSettings_ftr = EditorGUILayout.Foldout(showSettings_ftr, "Folders to Render");
        if (showSettings_ftr)
        {
            int newNum = EditorGUILayout.DelayedIntField("Number of Folders", rp.dirSelection.items.Count);
            int diff = rp.dirSelection.items.Count - newNum;
            // Append new entries
            if (diff < 0)
            {
                // Trick to avoid doing diff = |diff|
                for (int i = 0; i > diff; i--)
                {
                    rp.dirSelection.items.Add( new RenderPref.ScriptDirectory() );
                }
            }
            // Delete from the end of the list
            else if (diff > 0)
            {
                rp.dirSelection.items.RemoveRange(rp.dirSelection.items.Count - diff, diff);
            }

            for (int i = 0; i < newNum; i++)
            {
                EditorGUILayout.BeginHorizontal();
                rp.dirSelection.items[i].use = EditorGUILayout.ToggleLeft("", rp.dirSelection.items[i].use, GUILayout.MaxWidth(MAX_WIDTH_USE));
                EditorGUILayout.TextField ("", rp.dirSelection.items[i].path);
                if ( GUILayout.Button("...", GUILayout.MaxWidth(MAX_WIDTH_NAV_BUTTON)) )
                {
                    string path = EditorUtility.OpenFolderPanel("Choose folder where scripts are located",
                      rp.dirSelection.items[i].path, "");
                    if (path.Length != 0)
                    {
                        rp.dirSelection.items[i].path = path;
                    }
                    EditorGUIUtility.ExitGUI();
                }
                EditorGUILayout.EndHorizontal();
                rp.dirSelection.items[i].pattern = EditorGUILayout.DelayedTextField("File Pattern", rp.dirSelection.items[i].pattern);
                rp.dirSelection.items[i].iteration = EditorGUILayout.DelayedIntField("Iteration", rp.dirSelection.items[i].iteration);
                if (i < newNum - 1)
                {
                    EditorGUILayout.Space();
                }
            }
        }

        EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
        showProgress = EditorGUILayout.Foldout(showProgress, "Rendering Progress");

        if (RenderProgress.num_processed != 0 && showProgress)
        {
            EditorGUILayout.LabelField("Current Folder: ", RenderProgress.curFolder);
            EditorGUI.indentLevel++;
            string iterationFormat = string.Format( "Iteration: {0} / {1}", RenderProgress.curIteration.ToString(), RenderProgress.iteration.ToString() );
            EditorGUILayout.LabelField("Iteration: ", iterationFormat);
            float percentage = (float) RenderProgress.num_rendered * 100.0f / (float) RenderProgress.num_processed;
            string sRateFormat = string.Format("{0} / {1} ({2:0.##}) %", RenderProgress.num_rendered, RenderProgress.num_processed, percentage);
            EditorGUILayout.LabelField("Success Rate: ", sRateFormat);
            EditorGUILayout.LabelField("Current Script: ", RenderProgress.curScript);

            EditorGUILayout.LabelField("Rendered Character Distribution: ");
            EditorGUI.indentLevel++;
            foreach(KeyValuePair<string, int> c in RenderProgress.charDistribution)
            {
                percentage = (float) c.Value * 100.0f / (float) RenderProgress.num_rendered;
                string dist = string.Format("{0} ({1:0.##} %)", c.Value, percentage);
                EditorGUILayout.LabelField( c.Key.ToString() + ": ", dist);
            }
            EditorGUI.indentLevel--;
            EditorGUILayout.LabelField("Rendered Scene Distribution:");
            EditorGUI.indentLevel++;
            foreach(KeyValuePair<string, int> s in RenderProgress.sceneDistribution)
            {
                percentage = (float) s.Value * 100.0f / (float) RenderProgress.num_rendered;
                string dist = string.Format("{0} ({1:0.##} %)", s.Value, percentage);
                EditorGUILayout.LabelField(s.Key.ToString(), dist);
            };
            EditorGUI.indentLevel -= 2;
        }

        EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
        EditorGUILayout.BeginHorizontal();
        if ( GUILayout.Button("Save") )
        {
            string path = EditorUtility.SaveFilePanel("Save Current Render Preference as",
                RenderPref.DEFAULT_CONFIG_DIR, RenderPref.DEFAULT_RENDER_PREF_FILENAME, RenderPref.EXT_RENDER_PREF);
            if (path.Length != 0)
            {
                ioMsg = rp.Save(path);
                ShowNotification( new GUIContent(ioMsg) );
            }
            EditorGUIUtility.ExitGUI();
        }
        if ( GUILayout.Button("Load") )
        {
            string path = EditorUtility.OpenFilePanelWithFilters("Open saved render preference file",
                RenderPref.DEFAULT_CONFIG_DIR, new string[] {"RenderPref", RenderPref.EXT_RENDER_PREF});
            if (path.Length != 0)
            {
                RenderPref rp_tmp = RenderPref.Load(path, ref ioMsg);
                if (rp_tmp != null)
                {
                    rp = rp_tmp;
                }
                else
                {
                    rp = new RenderPref();
                }
                ShowNotification(new GUIContent(ioMsg) );
            }
            EditorGUIUtility.ExitGUI();
        }
            
        GUI.backgroundColor = Color.green;
        if ( GUILayout.Button("Apply") )
        {
            ioMsg = rp.Save(Path.Combine(RenderPref.DEFAULT_CONFIG_DIR, RenderPref.CUR_RENDER_PREF_FILENAME));
            ShowNotification( new GUIContent(ioMsg) );
        }
            
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.EndScrollView();
    }
}
