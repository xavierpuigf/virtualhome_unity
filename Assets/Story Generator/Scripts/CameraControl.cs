using StoryGenerator.Scripts;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using StoryGenerator.Recording;
using System;

namespace StoryGenerator.Utilities
{

    public delegate void CameraChange(Camera newCamera);

    public interface ICameraControl
    {
        Camera CurrentCamera { get; }
        void SetFocusArea(Bounds bounds);
        void ClearFocusArea();
        void SetVisibleArea(Bounds bounds);
        void ClearVisibleArea();
        void SetFocusObject(GameObject go, Bounds? bounds = null);
        void ClearFocusObject();
        void Update();
        void Activate(bool activate);
    }

    public class FrontCameraControl : ICameraControl
    {
        private Camera camera;

        public FrontCameraControl(Camera camera)
        {
            this.camera = camera;
            CameraUtils.InitCameras(new[] { camera });
            camera.gameObject.SetActive(false);
            camera.enabled = false;
        }

        public Camera CurrentCamera
        {
            get { return camera; }
        }

        public void ClearFocusArea()
        {
        }

        public void ClearFocusObject()
        {
        }

        public void ClearVisibleArea()
        {
        }

        public void SetFocusArea(Bounds bounds)
        {
        }

        public void SetFocusObject(GameObject go, Bounds? bounds = null)
        {
        }

        public void SetVisibleArea(Bounds bounds)
        {
        }

        public void Update()
        {
            CameraUtils.AdjustFrontCharacterCamera(camera);
        }
        public void Activate(bool activate)
        {
            camera.gameObject.SetActive(activate);
            camera.enabled = activate;
        }

    }

    public class FixedCameraControl : ICameraControl
    {
        private Camera camera;
        private Quaternion initialRotation;

        public bool DoFocusObject { get; set; }

        public FixedCameraControl(Camera camera)
        {
            this.camera = camera;
            CameraUtils.InitCameras(new[] { camera });
            camera.gameObject.SetActive(false);
            camera.enabled = false;
            initialRotation = camera.transform.localRotation;
        }

        public Camera CurrentCamera
        {
            get { return camera; }
        }

        public void ClearFocusArea()
        {
        }

        public void ClearFocusObject()
        {
            if (DoFocusObject) {
                //Debug.Log($"Clear focus before {camera.transform.localPosition}, {camera.transform.localRotation}");
                camera.transform.localRotation = initialRotation;
                //Debug.Log($"Clear focus after {camera.transform.localPosition}, {camera.transform.localRotation}");

            }
        }

        public void ClearVisibleArea()
        {
        }

        public void SetFocusArea(Bounds bounds)
        {
        }

        public void SetFocusObject(GameObject go, Bounds? bounds = null)
        {
            if (DoFocusObject) {
                //Debug.Log($"Set focus before {camera.transform.localPosition}, {camera.transform.localRotation}");
                camera.transform.LookAt(go.transform);
                //Debug.Log($"Set focus after {camera.transform.localPosition}, {camera.transform.localRotation}");
            }
        }

        public void SetVisibleArea(Bounds bounds)
        {
        }

        public void Update()
        {
        }
        public void Activate(bool activate)
        {
            camera.gameObject.SetActive(activate);
            camera.enabled = activate;
        }

    }

    public class AutoCameraControl : ICameraControl
    {
        private class FocusData
        {
            public bool refocus;                // Force re-focus camera (e.g., when focus object is set)
            public GameObject focusObject;      // Object camera should focus to (e.g., when grabbing, opening, ...)
            public Bounds? focusArea;
        }


        private const long MIN_FIXED_CAMERA_TIME =  500 * TimeSpan.TicksPerMillisecond; // Camera should be unchanged at least 0.5 seconds
        private const float MIN_CHARACTER_CAMERA_DISTANCE = 1.0f;    // Minimal distance of the camera to the character
        private const float MAX_CHARACTER_CAMERA_DISTANCE = 5.0f;    // Maximal distance of he camera to the character

        private Camera currentCamera;
        private Quaternion prevRotation;        // Previous rotation of current camera
        private float prevFieldOfView;          // Previous fow of current camera

        private List<Camera> cameras;
        private Transform characterTransform;   // Transform of character object
        private Vector3 characterCenterDelta;   // transform.position + characterCenterDelta should be approximately character center
        private FocusData focusData = new FocusData();         
        private Bounds? visibleArea;            // Area which should be visible on camera
        private bool randomizeCameras;
        private long lastCameraChange = 0;      // Last time camera was switched (in "ticks")

        public event CameraChange CameraChangeEvent;    // Called whenever camera changes


        public AutoCameraControl(List<Camera> cameras, Transform characterTransform, Vector3 characterCenterDelta)
        {
            this.characterTransform = characterTransform;
            this.characterCenterDelta = characterCenterDelta;
            this.cameras = cameras ?? new List<Camera>();
            CameraUtils.InitCameras(this.cameras);
        }

        public bool RandomizeCameras
        {
            get { return randomizeCameras; }
            set {
                randomizeCameras = value;
                if (randomizeCameras) {
                    foreach (Camera c in cameras) {
                        CameraUtils.RandomizeCameraProperties(c);
                    }
                }
            }
        }

        public Camera CurrentCamera
        {
            get { return currentCamera; }
        }

        // Focus to fo game object, optionally focus to area fb
        public void SetFocusObject(GameObject fo, Bounds? fb = null)
        {
            focusData = new FocusData() { focusObject = fo, focusArea = fb, refocus = true };
        }

        public void SetFocusArea(Bounds ib)
        {
            focusData = new FocusData() { focusArea = ib, refocus = true };
        }

        public void SetVisibleArea(Bounds va)
        {
            visibleArea = va;
        }

        public void ClearFocusObject()
        {
            SetFocusObject(null);
            //ChangeCamera(currentCamera, false);
        }

        public void ClearFocusArea()
        {
            focusData = new FocusData();
        }

        public void ClearVisibleArea()
        {
            visibleArea = null;
        }

        public void Update()
        {
            if (cameras.Count == 0)
                return;

            if (DateTime.Now.Ticks - lastCameraChange < MIN_FIXED_CAMERA_TIME)
                return;

            List<Tuple<float, Camera>> visible = new List<Tuple<float, Camera>>();    // list of (distance to camera, camera) pairs for character
            bool doRefocus = false;

            // Filter cameras catching focus object
            if (focusData.focusObject != null && focusData.refocus) {
                foreach (Camera c in cameras) {
                    if (focusData.focusArea != null) {
                        if (CheckViewport(c, focusData.focusArea.Value.min) && CheckViewport(c, focusData.focusArea.Value.max)) {
                            if (ScriptExecutor.IsVisible(focusData.focusObject, c.transform.position))
                                visible.Add(Tuple.Create(Vector3.Distance(focusData.focusObject.transform.position, c.transform.position), c));
                        }
                    } else {
                        if (CheckViewport(c, focusData.focusObject.transform.position)) {
                            float vf = ScriptExecutor.VisiblityFactor(focusData.focusObject, c.transform.position);

                        if (vf > 0.15f) {
                                visible.Add(Tuple.Create(Vector3.Distance(focusData.focusObject.transform.position, c.transform.position), c));
                        }
                    }
                }
                }
                doRefocus = true;
            }
            
            // Find cameras catching character 
            if (focusData.focusObject == null) {
                foreach (Camera c in cameras) {
                    bool viewPortOk;

                    if (visibleArea == null) viewPortOk = CheckViewport(c, characterTransform.position + characterCenterDelta);
                    else viewPortOk = CheckViewport(c, visibleArea.Value.min) && CheckViewport(c, visibleArea.Value.max);

                    if (viewPortOk) {
                        float vf = ScriptExecutor.AxisVisiblityFactor(characterTransform.gameObject, c.transform.position);
                        if (vf > 0.15f) {
                            visible.Add(Tuple.Create(Vector3.Distance(characterTransform.position + characterCenterDelta, c.transform.position), c));
                        }
                    }
                }

                // "Middle range" cameras
                var optimalCameras = visible.FindAll(pair => pair.Item1 > MIN_CHARACTER_CAMERA_DISTANCE && pair.Item1 < MAX_CHARACTER_CAMERA_DISTANCE);

                // If they exist, they are preferred, otherwise we are satisfied with any selected above
                if (optimalCameras.Count > 0) {
                    visible = optimalCameras;
                }
                doRefocus = focusData.refocus;
            }

            visible.Sort((p, q) => p.Item1.CompareTo(q.Item1));

            if (currentCamera == null) {
                ChangeCamera(visible.Count > 0 ? visible[0].Item2 : cameras[0], false);
            } else {
                if (visible.Count > 0) {
                    var currentPair = visible.Find(pair => pair.Item2 == currentCamera);

                    if (currentPair == null || currentPair.Item1 > 6.0f || doRefocus) {
                        Tuple<float, Camera> newPair;

                        if (RandomizeCameras) {
                            int maxRnd = 0;
                            float limit = visible[0].Item1 * 1.1f;

                            while (maxRnd < visible.Count && visible[maxRnd].Item1 < limit)
                                maxRnd++;
                            newPair = visible[UnityEngine.Random.Range(0, maxRnd)];
                            // Debug.Log($"Dist & camera name: {newPair.Item1}, {newPair.Item2.name}");
                        } else {
                            newPair = visible[0];
                        }
                        if (currentPair == null || newPair.Item1 < currentPair.Item1 - 0.5f || doRefocus) {
                            ChangeCamera(newPair.Item2, doRefocus);
                        }
                        if (doRefocus)
                            focusData.refocus = false;
                    }
                }
            }
        }

        private void ChangeCamera(Camera newCamera, bool doRefocus)
        {
            if (newCamera == null)
                return;

            //bool changed = false;

            //if (newCamera == currentCamera && !doRefocus) {
            //    if (currentCamera.transform.rotation != prevRotation) {
            //        currentCamera.transform.rotation = prevRotation;
            //        changed = true;
            //    }
            //    if (currentCamera.fieldOfView != prevFieldOfView) {
            //        currentCamera.fieldOfView = prevFieldOfView;
            //        changed = true;
            //    }
            //} else {
            if (currentCamera != null) {
            currentCamera.gameObject.SetActive(false);
                currentCamera.transform.rotation = prevRotation;
                currentCamera.fieldOfView = prevFieldOfView;
            }
            currentCamera = newCamera;
            prevRotation = currentCamera.transform.rotation;
            prevFieldOfView = currentCamera.fieldOfView;
            if (doRefocus) {
                if (focusData.focusObject != null)
                    ZoomToObject(currentCamera, focusData.focusObject);
                else if (focusData.focusArea != null)
                    ZoomToBounds(currentCamera, focusData.focusArea.Value);
            }
            currentCamera.gameObject.SetActive(true);
            //changed = true;

            //if (changed) {
                CameraChangeEvent?.Invoke(currentCamera);
            lastCameraChange = DateTime.Now.Ticks;
            //}
        }

        private void ZoomToObject(Camera camera, GameObject focusObject)
        {
            if (focusObject == null)
                return;

            Bounds bounds = GameObjectUtils.GetBounds(focusObject);

            if (bounds.extents == Vector3.zero) {
                bounds = new Bounds(focusObject.transform.position, Vector3.zero);
            }
            ZoomToBounds(camera, bounds);
        }
        
        private void ZoomToBounds(Camera camera, Bounds bounds)
        {
            if (bounds.extents == Vector3.zero) {
                camera.transform.LookAt(bounds.center);
            } else {
                Vector3 center = bounds.center;
                Vector3 toCenter = center - camera.transform.position;
                Vector3 extents = bounds.extents;
                float maxViewAngle = 0.0f;

                for (int xs = -1; xs <= 1; xs += 2) {
                    for (int ys = -1; ys <= 1; ys += 2) {
                        for (int zs = -1; zs <= 1; zs += 2) {
                            Vector3 toAngle = center + new Vector3(extents.x * xs, extents.y * ys, extents.z * zs) - camera.transform.position;
                            float angle = Vector3.Angle(toCenter, toAngle);

                            if (angle > maxViewAngle)
                                maxViewAngle = angle;
                        }
                    }
                }
                camera.transform.LookAt(center);
                ChangeFieldOfView(camera, maxViewAngle + 15.0f);
            }
        }

        private void ChangeFieldOfView(Camera camera, float fow)
        {
            camera.fieldOfView = fow;

            ImageSynthesis isc = camera.GetComponent<ImageSynthesis>();

            if (isc != null)
                isc.ChangeFieldOfView(fow);
        }


        private static bool CheckViewport(Camera c, Vector3 pos)
        {
            Vector3 vpPos = c.WorldToViewportPoint(pos);

            return vpPos.z > 0 && vpPos.x >= 0.1f && vpPos.x <= 0.9f && vpPos.y >= 0.1f && vpPos.y <= 0.9f;
        }

        public void Activate(bool activate)
        {

            return;
        }
    }

    public static class CameraUtils
    {
        const float CAMERA_FAR_PLANE = 100.0f;
        public static void InitCamera(Camera c)
        {
            c.farClipPlane = CAMERA_FAR_PLANE;
            GameObject go_cam = c.gameObject;
            if (go_cam.GetComponent<ImageSynthesis>() == null) {
                go_cam.AddComponent<ImageSynthesis>();
            }
            go_cam.SetActive(false);
        }

        public static void InitCameras(ICollection<Camera> cameras)
        {
            foreach (Camera c in cameras) {
                InitCamera(c);
            }
        }

        public static void RandomizeCameraProperties(Camera camera)
        {
            float delta = UnityEngine.Random.Range(-12, 12); // +/- 12 degrees
            float newFoV = Mathf.Clamp(camera.fieldOfView + delta, 10.0f, 45.0f); // Clamp camera FoV to 45 degree
            camera.fieldOfView = newFoV;

            Vector3 center = camera.transform.position;
            float f = (float)0.013;
            float x = f * UnityEngine.Random.Range(-10, 10);
            float y = f * UnityEngine.Random.Range(-10, 10);
            float z = f * UnityEngine.Random.Range(-10, 10);
            center.x += x;
            center.y += y;
            center.z += z;
            camera.transform.position = center;
        }

        public static byte[] RenderImage(Camera camera, int imageWidth, int imageHeight, int camPassNo)
        {
            const int RT_DEPTH = 24;

            Camera cam = camera.GetComponent<ImageSynthesis>().m_captureCameras[camPassNo];

            RenderTexture renderRT;
            // Use different render texture for depth values.
            if (camPassNo == ImageSynthesis.PASS_NUM_DEPTH) {
                // Half precision is good enough
                renderRT = RenderTexture.GetTemporary(imageWidth, imageHeight, RT_DEPTH, RenderTextureFormat.ARGBHalf);
            } else {
                renderRT = RenderTexture.GetTemporary(imageWidth, imageHeight, RT_DEPTH);
            }
            RenderTexture prevCameraRT = cam.targetTexture;

            // Render to offscreen texture (readonly from CPU side)
            cam.targetTexture = renderRT;
            cam.Render();
            cam.targetTexture = prevCameraRT;

            Texture2D tex;
            if (camPassNo == ImageSynthesis.PASS_NUM_DEPTH) {
                tex = new Texture2D(imageWidth, imageHeight, TextureFormat.RGBAHalf, false);
            } else {
                tex = new Texture2D(imageWidth, imageHeight, TextureFormat.RGB24, false);
            }

            RenderTexture prevActiveRT = RenderTexture.active;
            RenderTexture.active = renderRT;
            // read offsreen texture contents into the CPU readable texture
            tex.ReadPixels(new Rect(0, 0, imageWidth, imageHeight), 0, 0);
            RenderTexture.active = prevActiveRT;
            RenderTexture.ReleaseTemporary(renderRT);

            byte[] bytes;
            // encode texture
            if (camPassNo == ImageSynthesis.PASS_NUM_DEPTH) {
                bytes = tex.EncodeToEXR(Texture2D.EXRFlags.CompressZIP);
            } else {
                bytes = tex.EncodeToPNG();
            }

            return bytes;
        }

        public static void DeactivateCameras(IList<Camera> cameras)
        {
            foreach (Camera c in cameras) {
                c.gameObject.SetActive(false);
            }
        }

        public static IList<CameraInfo> CreateCameraData(List<Camera> cameras, IList<int> indexes)
        {
            List<CameraInfo> result = new List<CameraInfo>();

            foreach (int i in indexes) {
                Camera camera = cameras[i];
                Matrix4x4 projMatrix = camera.projectionMatrix;
                Matrix4x4 cameraMatrix = camera.worldToCameraMatrix;
                float[] projArray = new float[16];
                float[] cameraArray = new float[16];

                for (int j = 0; j < 16; j++) {
                    projArray[j] = projMatrix[j];
                    cameraArray[j] = cameraMatrix[j];
                }

                CameraInfo data = new CameraInfo() {
                    index = i,
                    name = camera.name,
                    aspect = camera.aspect,
                    projection_matrix = projArray,
                    world_to_camera_matrix = cameraArray
                };
                result.Add(data);
            }
            return result;
        }

        public static void AdjustFrontCharacterCamera(Camera camera)
        {
            // priority list of distances and angles from the character
            float[] distances = { 2.5f, 2.0f, 1.75f, 1.5f };
            float[] hAngles = { 0, 30, -30, 60, -60, 90, -90 };
            float[] vAngles = { 0, 30, 60 };
            Vector3 vertDelta = 1.5f * Vector3.up;
            Vector3 posDelta = 0.75f * Vector3.up;

            Transform charTransform = camera.transform.parent;

            foreach (float vAngle in vAngles) {
                foreach (float hAngle in hAngles) {
                    Quaternion r = Quaternion.Euler(vAngle, hAngle, 0);

                    foreach (float d in distances) {
                        Vector3 v = r * (d * Vector3.forward);
                        Vector3 vg = charTransform.TransformPoint(v) + vertDelta;

                        if (ScriptExecutor.IsVisible(charTransform.gameObject, vg)) {
                            camera.transform.position = vg;
                            camera.transform.LookAt(charTransform.position + posDelta);
                            return;
                        }
                    }
                }
            }
        }

    }

    // Json object
    public class CameraInfo
    {
        public int index;
        public string name;
        public float aspect;
        public float[] projection_matrix;
        public float[] world_to_camera_matrix;
    }
}
