using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using StoryGenerator.Utilities;
using StoryGenerator.SceneState;
using System;
using System.Text;

namespace StoryGenerator.Recording
{

    public class ExecutionError
    {
        public string Message { get; internal set; }

        public ExecutionError(string message)
        {
            Message = message;
        }
    }

    public class Recorder : MonoBehaviour
    {
        public List <string> imageSynthesis = new List<string>();
        public bool savePoseData = false;
        public bool saveSceneStates = false;

        public string OutputDirectory { get; set; }
        public int MaxFrameNumber { get; set; }
        public string FileName { get; set; }
        public Animator Animator { get; internal set; }
        public ExecutionError Error { get; set; }
        public ICameraControl CamCtrl { get; set; }
        public SceneStateSequence sceneStateSequence { get; set; } = new SceneStateSequence();

        List<ActionRange> actionRanges = new List<ActionRange>();
        List<CameraData> cameraData = new List<CameraData>();
        List<PoseData> poseData = new List<PoseData>();
        int frameRate = 20;
        int frameNum = 0;
        bool recording = false;
        // Used to skip optic flow frame generation upon camera transition
        // Initial value is true since the first frame has bad optical flow
        bool isCameraChanged = true;

        const int INITIAL_FRAME_SKIP = 2;
        public int ImageWidth = 640; // 375;
        public int ImageHeight = 480; //250;

        // ======================================================================================== //
        // ================================== Class Declarations ================================== //
        // ======================================================================================== //

        private class ActionRange
        {
            string action; // For sub actions, action is printed
            int scriptLine;
            int frameStart;
            int frameEnd;

            public ActionRange(int scriptLine, string actionStr, int frameNum)
            {
                this.scriptLine = scriptLine;
                action = actionStr;
                frameStart = frameNum;
            }

            public void MarkActionEnd(int frameNum)
            {
                frameEnd = frameNum;
            }

            public string GetString()
            {
                return string.Format("{0} {1} {2} {3}", scriptLine, action, frameStart, frameEnd);
            }
        }

        private class CameraData
        {
            public Matrix4x4 ProjectionMatrix { get; set; }
            public int FrameStart { get; set; }
            public int FrameEnd { get; set; }

            override public string ToString()
            {
                return string.Format("{0}{1} {2}",
                    ProjectionMatrix.ToString().Replace('\t', ' ').Replace('\n', ' '),
                    FrameStart, FrameEnd);
            }
        }

        private class PoseData
        {
            private static HumanBodyBones[] bones = (HumanBodyBones[])Enum.GetValues(typeof(HumanBodyBones));

            int frameNumber;
            private Vector3[] boneVectors = new Vector3[bones.Length];

            public PoseData(int frameNumber, Animator animator)
            {
                this.frameNumber = frameNumber;
                for (int i = 0; i < bones.Length; i++) {
                    Transform bt = animator.GetBoneTransform(bones[i]);

                    if (bt != null)
                        boneVectors[i] = bt.position;
                }
            }

            public static string BoneNamesToString()
            {
                return string.Join(" ", bones.Select(hbb => hbb.ToString()).ToArray());
            }

            override public string ToString()
            {
                StringBuilder sb = new StringBuilder();

                sb.Append(frameNumber);
                foreach (Vector3 v in boneVectors) {
                    sb.Append(' '); sb.Append(v.x);
                    sb.Append(' '); sb.Append(v.y);
                    sb.Append(' '); sb.Append(v.z);
                }
                return sb.ToString();
            }
        }

        // ======================================================================================== //
        // ====================================== Properties ====================================== //
        // ======================================================================================== //

        public bool Recording
        {
            get { return recording; }
            set {
                if (value == false) {
                    MarkActionEnd();
                    MarkCameraEnd();
                }
                recording = value;
            }
        }

        public int FrameRate
        {
            get { return frameRate; }
            set {
                // optical flow sentivity should be proportional to the framerate
                ImageSynthesis.OPTICAL_FLOW_SENSITIVITY = value;
                frameRate = value;
            }
        }

        // ======================================================================================== //
        // =============================== Monobehaviour Executions =============================== //
        // ======================================================================================== //

        public void Initialize()
        {
            Time.captureFramerate = frameRate;

            if (CamCtrl != null) {
                CamCtrl.Update();
            }

            if (OutputDirectory != null) {
                const string FILE_NAME_PREFIX = "Action_";
                StartCoroutine(OnEndOfFrame(Path.Combine(OutputDirectory, FILE_NAME_PREFIX)));
            }
        }

        // ======================================================================================== //
        // ================================== Methods - Actions =================================== //
        // ======================================================================================== //

        public void MarkActionStart(InteractionType a, int scriptLine)
        {
            // For current implementation, MarkActionEnd and MarkActionStart happens on the same frame
            // Include offset to prevent this.
            MarkActionEnd();
            actionRanges.Add(new ActionRange(scriptLine, a.ToString(), frameNum > 0 ? frameNum + 1 : 0));
        }

        // This marks the end of execution so that intentional delay after executing all actions
        // doesn't corrupt ground truth data.
        public void MarkTermination()
        {
            const string NULL_ACTION = "NULL";
            MarkActionEnd();
            actionRanges.Add(new ActionRange(-1, NULL_ACTION, frameNum + 1));
        }

        private void MarkActionEnd()
        {
            // Always update the last element in the list
            if (actionRanges.Count > 0)
                actionRanges[actionRanges.Count - 1].MarkActionEnd(frameNum);
        }

        // ======================================================================================== //
        // ================================== Methods - Cameras =================================== //
        // ======================================================================================== //

        public void UpdateCameraData(Camera newCamera)
        {
            isCameraChanged = true;
            MarkCameraEnd();
            cameraData.Add(new CameraData() { ProjectionMatrix = newCamera.projectionMatrix, FrameStart = frameNum > 0 ? frameNum + 1 : 0 });
        }

        void MarkCameraEnd()
        {
            if (cameraData.Count > 0)
                cameraData[cameraData.Count - 1].FrameEnd = frameNum;
        }

        // ======================================================================================== //
        // ============================== Methods - Saving/Rendering ============================== //
        // ======================================================================================== //

        // Single point where we save/render/record things. WaitForEndOfFrame does not always align
        // Update() or LateUpdate(). One might get called few times before the other or vice versa.
        // This shows similar issue:
        // https://forum.unity.com/threads/yield-return-waitendofframe-will-wait-until-end-of-the-same-frame-or-the-next-frame.282213/
        System.Collections.IEnumerator OnEndOfFrame(string pathPrefix)
        {
            for (int i = 0; i < INITIAL_FRAME_SKIP; i++) {
                yield return new WaitForEndOfFrame();
                CamCtrl.Update();
            }

            // Need to check since recording can be disabled due to error such as stuck error.
            while (recording && frameNum <= MaxFrameNumber) {
                yield return new WaitForEndOfFrame();

                if (recording) {
                    for (int i = 0; i < ImageSynthesis.PASSNAMES.Length; i++) {
                        if (imageSynthesis.Contains(ImageSynthesis.PASSNAMES[i]))
                        {
                            // Special case for optical flow camera - flow is really high whenver camera changes so it
                            // should just save black image
                            bool isOpticalFlow = (i == ImageSynthesis.PASS_NUM_OPTICAL_FLOW);
                            SaveRenderedFromCam(pathPrefix, i, isOpticalFlow);
                        }

                    }
                    if (savePoseData) {
                        UpdatePoseData(frameNum);
                    }
                    if (saveSceneStates) {
                        sceneStateSequence.SetFrameNum(frameNum);
                    }
                }

                CamCtrl.Update();
                frameNum++;
            }
            // If code reaches here, it means either recording is set to false or
            // frameNum exceeded max frame number. If recording is still true,
            // it means max frame number is reached.
            if (recording) {
                Error = new ExecutionError("Max frame number exceeded");
            }
        }

        void SaveRenderedFromCam(string pathPrefix, int camPassNo, bool isOpticalFlow)
        {
            const int RT_DEPTH = 24;

            Camera cam = CamCtrl.CurrentCamera.GetComponent<ImageSynthesis>().m_captureCameras[camPassNo];
            RenderTexture renderRT;
            // Use different render texture for depth values.
            if (camPassNo == ImageSynthesis.PASS_NUM_DEPTH) {
                // Half precision is good enough
                renderRT = RenderTexture.GetTemporary(ImageWidth, ImageHeight, RT_DEPTH, RenderTextureFormat.ARGBHalf);
            } else {
                renderRT = RenderTexture.GetTemporary(ImageWidth, ImageHeight, RT_DEPTH);
            }
            RenderTexture prevCameraRT = cam.targetTexture;

            // Render to offscreen texture (readonly from CPU side)
            cam.targetTexture = renderRT;
            cam.Render();
            cam.targetTexture = prevCameraRT;

            Texture2D tex;
            if (camPassNo == ImageSynthesis.PASS_NUM_DEPTH) {
                tex = new Texture2D(ImageWidth, ImageHeight, TextureFormat.RGBAHalf, false);
            } else {
                tex = new Texture2D(ImageWidth, ImageHeight, TextureFormat.RGB24, false);
            }

            // Corner case for optical flow - just render black texture
            if (isOpticalFlow && isCameraChanged) {
                // Set texture black
                for (int y = 0; y < ImageHeight; y++) {
                    for (int x = 0; x < ImageWidth; x++) {
                        tex.SetPixel(x, y, Color.black);
                    }
                }
                tex.Apply();
            } else {
                RenderTexture prevActiveRT = RenderTexture.active;
                RenderTexture.active = renderRT;
                // read offsreen texture contents into the CPU readable texture
                tex.ReadPixels(new Rect(0, 0, ImageWidth, ImageHeight), 0, 0);
                RenderTexture.active = prevActiveRT;
            }

            RenderTexture.ReleaseTemporary(renderRT);

            string filePath = string.Format("{0}{1:D4}_{2}", pathPrefix, frameNum,
                        ImageSynthesis.PASSNAMES[camPassNo]);

            byte[] bytes;
            // encode texture
            if (camPassNo == ImageSynthesis.PASS_NUM_DEPTH) {
                filePath += ".exr";
                bytes = tex.EncodeToEXR(Texture2D.EXRFlags.CompressZIP);
            } else {
                filePath += ".png";
                bytes = tex.EncodeToPNG();
            }
            File.WriteAllBytes(filePath, bytes);

            // Reset the value - check if current check is on optical flow
            // since it's is the last GT we are rendering.
            if (isCameraChanged && isOpticalFlow) {
                isCameraChanged = false;
            }
        }

        public void CreateTextualGTs()
        {
            const string PREFIX_ACTION = "ftaa_";
            const string PREFIX_CAMERA = "cd_";
            const string PREFIX_POSE = "pd_";
            const string PREFIX_SCENE_STATE = "ss_";
            const string FILE_EXT_TXT = ".txt";
            const string FILE_EXT_JSON = ".json";

            string currentFileName = Path.Combine(OutputDirectory, PREFIX_ACTION) + FileName + FILE_EXT_TXT;

            if (actionRanges.Count == 0) {
                File.Delete(currentFileName);
            } else {
                using (StreamWriter sw = new StreamWriter(currentFileName)) {
                    foreach (ActionRange ar in actionRanges) {
                        sw.WriteLine(ar.GetString());
                    }
                }
            }

            currentFileName = Path.Combine(OutputDirectory, PREFIX_CAMERA) + FileName + FILE_EXT_TXT;

            if (cameraData.Count == 0) {
                File.Delete(currentFileName);
            } else {
                using (StreamWriter sw = new StreamWriter(currentFileName)) {
                    foreach (CameraData cd in cameraData) {
                        sw.WriteLine(cd.ToString());
                    }
                }
            }

            currentFileName = Path.Combine(OutputDirectory, PREFIX_POSE) + FileName + FILE_EXT_TXT;

            if (poseData.Count == 0) {
                File.Delete(currentFileName);
            } else {
                using (StreamWriter sw = new StreamWriter(currentFileName)) {
                    sw.WriteLine(PoseData.BoneNamesToString());
                    foreach (PoseData pd in poseData) {
                        sw.WriteLine(pd.ToString());
                    }
                }
            }

            currentFileName = Path.Combine(OutputDirectory, PREFIX_SCENE_STATE) + FileName + FILE_EXT_JSON;

            if (sceneStateSequence.states.Count == 0) {
                File.Delete(currentFileName);
            } else {
                using (StreamWriter sw = new StreamWriter(currentFileName)) {
                    sw.WriteLine(JsonUtility.ToJson(sceneStateSequence, true));
                }
            }
        }

        // ======================================================================================== //
        // =================================== Methods - Misc. ==================================== //
        // ======================================================================================== //        

        public bool BreakExecution()
        {
            return recording && (Error != null);
        }

        void UpdatePoseData(int actualFrameNum)
        {
            if (Animator != null)
                poseData.Add(new PoseData(actualFrameNum, Animator));
        }

    }
}
