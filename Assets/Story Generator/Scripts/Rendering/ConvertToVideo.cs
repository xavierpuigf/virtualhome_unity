using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Diagnostics;

namespace StoryGenerator.Rendering
{

    public static class ConvertToVideo
    {
        public static void ConvertAndClean(string scriptName, string dir_out,
          bool keepRawFrames, bool dontMoveDoneScripts)
        {
            const string PYTHON_SCRIPT = "frames2mp4.py";
            // Obtained from:
            // http://onedayitwillmake.com/blog/2014/10/running-a-pythonshell-script-from-the-unityeditor/
            // Modified a bit
            string dPath = Application.dataPath;
            string pythonFilePath = dPath.Substring(0, dPath.LastIndexOf('/') + 1) + "Utilities";

            Process p = new Process();
            RenderPref rp = MasterRenderer.renderPreference;
            p.StartInfo.FileName = "python";
            string scriptArgs = string.Format("{0} \"{1}\" \"{2}\" \"{3}\" \"{4}\" {5}",
                PYTHON_SCRIPT, scriptName, dir_out, rp.GetCurrentFolder(), dir_out, rp.baseSettings.frameRate);
            if (rp.debugSettings.keepRawFrames)
            {
                scriptArgs += " --keep_frames";
            }
            if (rp.debugSettings.dontMoveDoneScripts)
            {
                scriptArgs += " --keep_scripts";
            }
            if (rp.baseSettings.fg == RenderPref.FrameGeneration.RGB)
            {
                scriptArgs += " --rgb_only";
            }
            
			p.StartInfo.Arguments = scriptArgs;
            p.StartInfo.EnvironmentVariables["PATH"] = string.Format("{0}:{1}", p.StartInfo.EnvironmentVariables["PATH"],
                rp.baseSettings.ffmpegPath);

            // Pipe the output to itself - we will catch this later
            p.StartInfo.RedirectStandardError = true;
            p.StartInfo.RedirectStandardOutput = true;
            p.StartInfo.CreateNoWindow = true;

            // Where the script lives
            p.StartInfo.WorkingDirectory = pythonFilePath; 
            p.StartInfo.UseShellExecute = false;

            p.Start();
            string err = p.StandardError.ReadToEnd();
            UnityEngine.Debug.Assert(err == null || err == "",
              "Python script to convert into video failed. Error message:\n" + err);
            p.WaitForExit();
            p.Close();
        }
    }

}