using UnityEngine;
using UnityEngine.Rendering;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine.PostProcessing;

namespace StoryGenerator.Recording
{
    [RequireComponent (typeof(Camera))]
    public class ImageSynthesis : MonoBehaviour
    {    
        public static readonly string[] PASSNAMES = { "normal", "seg_inst", "seg_class", "depth", "flow", "albedo", "illumination", "surf_normals" };
        public static float OPTICAL_FLOW_SENSITIVITY = 10.0f;
        public const int PASS_NUM_DEPTH = 3;
        public const int PASS_NUM_OPTICAL_FLOW = 4;
        
        [HideInInspector]
        public Camera[] m_captureCameras = new Camera[PASSNAMES.Length];

        static readonly Dictionary<string, string> shader_name_map = new Dictionary<string, string>()
        {
            {"seg_inst", "Hidden/UniformColor"},
            {"seg_class", "Hidden/UniformColor"},
            {"depth", "Hidden/LinearDepth"},
            {"flow", "Hidden/OpticalFlow" },
            {"albedo", "Hidden/Albedo" },
            {"illumination", "Hidden/Illumination" },
            {"surf_normals", "Hidden/SurfaceNormal" },
        };
        public Dictionary<string, Shader> shaders;

        void Start()
        {
            // For more realistic camera
            PostProcessingBehaviour ppb = gameObject.AddComponent<PostProcessingBehaviour> ();
            if (ppb == null)
            {
                ppb = gameObject.AddComponent<PostProcessingBehaviour>();
            }
            PostProcessingProfile ppp = Resources.Load("CamProfiles/defaultProfile") as PostProcessingProfile;
            Debug.Assert(ppp != null, "Can't find Post Processing Profile.");
            ppb.profile = ppp;

            m_captureCameras[0] = GetComponent<Camera> ();
            for (int q = 1; q < m_captureCameras.Length; q++)
            {
                // First display target is for the regular camera
                m_captureCameras[q] = CreateHiddenCamera(PASSNAMES[q], q);
            }

            // HDR should be enabled to fully utilize post processing stack
            m_captureCameras[0].allowHDR = true;
            if (shaders == null)
            {
                shaders = new Dictionary<string, Shader>();
                foreach (string shader_name in shader_name_map.Keys)
                {
                    shaders[shader_name] = Shader.Find(shader_name_map[shader_name]);
                }


            }
            SetupCameraWithReplacementShaderAndBlackBackground(m_captureCameras[1], shaders["seg_inst"], 0);
            SetupCameraWithReplacementShaderAndBlackBackground(m_captureCameras[2], shaders["seg_class"], 1);

            SetupCameraWithPostShader(m_captureCameras[3], shaders["depth"], DepthTextureMode.Depth);
            SetupCameraWithPostShader(m_captureCameras[4], shaders["flow"],
              DepthTextureMode.Depth | DepthTextureMode.MotionVectors, "_Sensitivity", OPTICAL_FLOW_SENSITIVITY);

            SetupCameraWithReplacementShader(m_captureCameras[5], shaders["albedo"]);
            SetupCameraWithReplacementShader(m_captureCameras[6], shaders["illumination"]);
            SetupCameraWithReplacementShader(m_captureCameras[7], shaders["surf_normals"]);


        }
        
        Camera CreateHiddenCamera(string name, int targetDisplay)
        {
            GameObject go = new GameObject (name, typeof (Camera));
            go.hideFlags = HideFlags.HideAndDontSave;;
            go.transform.parent = transform;

            Camera newCamera = go.GetComponent<Camera>();
            newCamera.CopyFrom(GetComponent<Camera>());
            newCamera.targetDisplay = targetDisplay;

            return newCamera;
        }

        static void SetupCameraWithReplacementShader(Camera cam, Shader shader)
        {
            var cb = new CommandBuffer();
            cam.AddCommandBuffer(CameraEvent.AfterEverything, cb);
            cam.SetReplacementShader(shader, "");
            cam.backgroundColor = Color.black;
            cam.clearFlags = CameraClearFlags.SolidColor;
        }

        static void SetupCameraWithReplacementShaderAndBlackBackground(Camera cam, Shader shader, int source)
        {
            var cb = new CommandBuffer();
            cb.SetGlobalFloat("_Source", source); // @TODO: CommandBuffer is missing SetGlobalInt() method
            cam.AddCommandBuffer(CameraEvent.BeforeForwardOpaque, cb);
            cam.AddCommandBuffer(CameraEvent.BeforeFinalPass, cb);
            cam.SetReplacementShader(shader, "");
            cam.backgroundColor = Color.black;
            cam.clearFlags = CameraClearFlags.SolidColor;
        }

        static void SetupCameraWithPostShader(Camera cam, Shader shader, DepthTextureMode depthTextureMode,
            string optionalField = null, float optionalValue = -1)
        {
            var cb = new CommandBuffer();
            Material mat = new Material(shader);

            if (optionalField != null)
            {
                mat.SetFloat(optionalField, optionalValue);
            }

            cb.Blit(null, BuiltinRenderTextureType.CurrentActive, mat);
            cam.AddCommandBuffer(CameraEvent.AfterEverything, cb);
            cam.depthTextureMode = depthTextureMode;
        }

        public void ChangeFieldOfView(float fow)
        {
            foreach (Camera c in m_captureCameras) {
                if (c != null)
                    c.fieldOfView = fow;
            }
        }

    }
}
