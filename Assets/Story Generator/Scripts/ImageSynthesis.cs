using UnityEngine;
using UnityEngine.Rendering;
using System.Collections;
using System.IO;
using UnityEngine.PostProcessing;

namespace StoryGenerator.Recording
{
    [RequireComponent (typeof(Camera))]
    public class ImageSynthesis : MonoBehaviour
    {    
        public static readonly string[] PASSNAMES = {"normal", "seg_inst", "seg_class", "depth", "flow"};
        public static float OPTICAL_FLOW_SENSITIVITY = 10.0f;
        public const int PASS_NUM_DEPTH = 3;
        public const int PASS_NUM_OPTICAL_FLOW = 4;
        
        [HideInInspector]
        public Camera[] m_captureCameras = new Camera[PASSNAMES.Length];

        static Shader m_shader_colorPass;
        static Shader m_shader_depthPass;
        static Shader m_shader_opticalFlowPass;

        void Start()
        {
            // For more realistic camera
            PostProcessingBehaviour ppb = gameObject.AddComponent<PostProcessingBehaviour> ();
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

            if (m_shader_colorPass == null)
            {
                m_shader_colorPass = Shader.Find("Hidden/UniformColor");
            }
            if (m_shader_depthPass == null)
            {
                m_shader_depthPass = Shader.Find("Hidden/LinearDepth");
            }
            if (m_shader_opticalFlowPass == null)
            {
                m_shader_opticalFlowPass = Shader.Find("Hidden/OpticalFlow");
            }

            SetupCameraWithReplacementShaderAndBlackBackground(m_captureCameras[1], m_shader_colorPass, 0);
            SetupCameraWithReplacementShaderAndBlackBackground(m_captureCameras[2], m_shader_colorPass, 1);

            SetupCameraWithPostShader(m_captureCameras[3], m_shader_depthPass, DepthTextureMode.Depth);
            SetupCameraWithPostShader(m_captureCameras[4], m_shader_opticalFlowPass,
              DepthTextureMode.Depth | DepthTextureMode.MotionVectors, "_Sensitivity", OPTICAL_FLOW_SENSITIVITY);
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
