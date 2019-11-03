using StoryGenerator.Utilities;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

using UnityEngine;

namespace LightBoxScene
{

public class Lightbox : MonoBehaviour
{
	public GameObject dummy;
	public GameObject lights;
	Camera cam;
	public string gif_dir = null;

	[System.Serializable]
	class Prefab
	{
		public string path = null;
		public float[] center = null;
		public Vector3 CenterV3
		{
			get {return new Vector3(center[0], center[1], center[2]); }
		}

		public float[] size = null;
		public Vector3 SizeV3
		{
			get {return new Vector3(size[0], size[1], size[2]); }
		}

		public bool ErrorCheck()
		{
			if (path == null || path == "")
			{
				LogErrorAndBreak("path is not provided.");
				return true;
			}
			if (center != null && center.Length != 3)
			{
				LogErrorAndBreak(path + ": center must have exactly 3 values.");
				return true;
			}
			if (size != null && size.Length != 3)
			{
				LogErrorAndBreak(path + ": dimensions must have exactly 3 values.");
				return true;
			}
			return false;
		}
	}

	[System.Serializable]
	class GifCreator
	{
		public string ffmpegPath = null;
		public float framerate = 1.0f;

		string prefab_name;
		string prefab_dir;
		string saveLocaion;
		string common_filters;
		
		static readonly string PALETTE_NAME = "palette.png";

		public void Init(string _prefab_dir, Settings s)
		{
			prefab_name = Path.GetFileName(_prefab_dir);
			prefab_dir = _prefab_dir;
			saveLocaion = s.saveLocation;
			common_filters = $"scale={Screen.width}:-1:flags=lanczos";
		}

		public bool CreateGif()
		{
			// Creating gif from ffmpeg with one pass really degrades the quality; it appears
			// it appears too grainy.
			// Solution: two pass method.
			// Source: http://blog.pkh.me/p/21-high-quality-gif-with-ffmpeg.html
			if ( Pass1() && Pass2() )
			{
				return true;
			}
			return false;
		}

		bool Pass1()
		{
			string ffmpeg_args = $"-v error -i {prefab_name}_%03d.png -vf \"fps={framerate},{common_filters},palettegen\" -y {PALETTE_NAME}";
			return RunFFmpeg(ffmpeg_args, "pass1: Palette generation failed");
		}

		bool Pass2()
		{
			string path_gif = $"{Path.Combine(saveLocaion, prefab_name)}.gif";
			// I noticed that if 'fps' filter is used, it will skip some frames. Use framerate option instead.
			string ffmpeg_args = $"-v error -framerate {framerate} -i {prefab_name}_%03d.png -i {PALETTE_NAME} -lavfi \"{common_filters} [x]; [x][1:v] paletteuse\" -y {path_gif}";
			return RunFFmpeg(ffmpeg_args, "pass2: gif generation failed");
		}

		bool RunFFmpeg(string args, string err_prefix)
		{
			Process p = new Process();
			p.StartInfo.FileName = ffmpegPath;
			p.StartInfo.Arguments = args;

			// Pipe the output to itself - we will catch this later
			p.StartInfo.RedirectStandardError = true;
			p.StartInfo.CreateNoWindow = true;

			p.StartInfo.WorkingDirectory = prefab_dir;
			p.StartInfo.UseShellExecute = false;

			p.Start();
			string err = p.StandardError.ReadToEnd();

			bool ret_val = true;
			if (err != "")
			{
				ret_val = false;
				UnityEngine.Debug.LogError($"GifCreator: {err_prefix}.\n{err}");
			}

			p.WaitForExit();
			p.Close();

			return ret_val;
		}
	}

	[System.Serializable]
	class Settings
	{
		public float fieldOfView = 30.0f;
		public byte[] backgroundRGB = null;
		public float minProportion = 0.6f;
		public float maxProportion = 0.8f;
		public int camAngleDelta = 45;
		public float brightness = 1.2f;
		public string saveLocation = null;
		public GifCreator gifOption = null;

		public Prefab[] prefabs = null;

		public bool ErrorCheck()
		{
			if (fieldOfView < 15.0f || fieldOfView > 120.0f)
			{
				LogErrorAndBreak("fieldOfView is out of range (15 - 120)");
				return true;
			}
			if (backgroundRGB == null)
			{
				LogErrorAndBreak("backgroundRGB is not provided.");
				return true;
			}
			if (backgroundRGB.Length != 3)
			{
				LogErrorAndBreak("backgroundRGB must have 3 values.");
				return true;
			}
			if (minProportion < 0.1)
			{
				LogErrorAndBreak("minProportion is too small (< 0.1).");
				return true;
			}
			if (maxProportion < 0.1)
			{
				LogErrorAndBreak("maxProportion is too small (< 0.1).");
				return true;
			}
			if (minProportion > maxProportion)
			{
				LogErrorAndBreak("minProportion is greater than maxProportion.");
				return true;
			}
			if (90 % camAngleDelta != 0 || 360 % camAngleDelta != 0)
			{
				LogErrorAndBreak("90 and 360 must be divisable by camAngleDelta.");
				return true;
			}
			if (brightness < 0.1f || brightness > 4.0f)
			{
				LogErrorAndBreak("brightness is out of range (0.1 - 4.0).");
				return true;
			}
			if (saveLocation == null || saveLocation == "")
			{
				LogErrorAndBreak("saveLocaion is not provided.");
				return true;
			}
			if (prefabs == null || prefabs.Length == 0)
			{
				LogErrorAndBreak("prefabs are empty");
				return true;
			}
			foreach (Prefab p in prefabs)
			{
				if ( p.ErrorCheck() )
				{
					return true;
				}
			}
			return false;
		}
	}

	class Edge
	{
		Vector3 u;
		Vector3 v;

		public Edge(Vector3 _u, Vector3 _v)
		{
			u = _u;
			v = _v;
		}

		// Returns proportion of the length against screen size.
		public float screenLength(Camera c)
		{
			Vector2 screen_u = GetPixelPos(c, u);
			Vector2 screen_v = GetPixelPos(c, v);

			return (screen_u - screen_v).magnitude / (float) Screen.width;
		}

		Vector2 GetPixelPos(Camera c, Vector3 p_world)
		{
			Vector3 screen_vec3 = c.WorldToScreenPoint(p_world);
			return new Vector2(screen_vec3.x, Screen.height - screen_vec3.y);
		}
	}

	class EdgeChecker
	{
		List<Edge> edges = new List<Edge> (28);

		public EdgeChecker(Bounds bnd)
		{
			List<Vector3> vertices = new List<Vector3> (8);

			Vector3 diag = new Vector3(bnd.extents.x, 0.0f, bnd.extents.z);
			// Start with the top
			Vector3 top_center = bnd.center + new Vector3(0.0f, bnd.extents.y, 0.0f);

			// top_tl
			vertices.Add(top_center - diag);
			// top_tr
			vertices.Add( vertices[0] + new Vector3(0.0f, 0.0f, bnd.size.z) );
			// top_bl
			vertices.Add( vertices[0] + new Vector3(bnd.size.x, 0.0f, 0.0f) );
			// top_br
			vertices.Add(top_center + diag);

			Vector3 height = new Vector3(0.0f, bnd.size.y, 0.0f);
			vertices.Add(vertices[0] - height);
			vertices.Add(vertices[1] - height);
			vertices.Add(vertices[2] - height);
			vertices.Add(vertices[3] - height);

			// Create edges - all combination of the vertices
			for (int i = 0; i < vertices.Count; i++)
			{
				for (int j = i + 1; j < vertices.Count; j++)
				{
					edges.Add( new Edge(vertices[i], vertices[j]) );
				}
			}
		}

		public float maxEdgeLen(Camera c)
		{
			float max = float.NegativeInfinity;

			foreach (Edge e in edges)
			{
				float l = e.screenLength(c);
				if (l > max)
				{
					max = l;
				}
			}

			return max;
		}
	}



	void Start ()
	{
		const string SETTINGS_FILE = "Config/lightbox.json";

		if (Screen.width != Screen.height)
		{
			UnityEngine.Debug.LogError("This script assumes width and heights are equal to simplify calculations. Please change the resolution to make them equal.");
			UnityEngine.Debug.Break();
		}

		if (dummy != null)
		{
			dummy.SetActive(false);
		}

		Settings s;
		using ( StreamReader sr = new StreamReader(SETTINGS_FILE) )
		{
			string str_file = sr.ReadToEnd();
		 	s = JsonUtility.FromJson<Settings> (str_file);
		}
		if ( s.ErrorCheck() )
		{
			return;
		}

		cam = GetComponentInChildren<Camera> ();
		cam.fieldOfView = s.fieldOfView;
		cam.backgroundColor = new Color32(s.backgroundRGB[0], s.backgroundRGB[1], s.backgroundRGB[2], 255);

		Light[] ls = lights.GetComponentsInChildren<Light> ();
		foreach (Light l in ls)
		{
			l.intensity = s.brightness;
		}

		Directory.CreateDirectory(s.saveLocation);

		// If ffmpeg path is given, GIFs will be stored under the gif directory.
		if (s.gifOption != null)
		{
			gif_dir = Path.Combine(s.saveLocation, "gif");
		}

		StartCoroutine( ProcessPrefabs(s) );
	}

	IEnumerator ProcessPrefabs(Settings s)
	{
		int I_MAX = 90 / s.camAngleDelta + 1;
		int J_MAX = 360 / s.camAngleDelta;
		Vector3 CAM_ORIG_LOCAL_POS = cam.transform.localPosition;

		foreach (Prefab p in s.prefabs)
		{
			GameObject go_prefab = Resources.Load(p.path) as GameObject;
			GameObject go = Instantiate(go_prefab);

			Bounds bnd;
			// This object is not annotated so use the provided information.
			if (p.center != null && p.size != null)
			{
				bnd = new Bounds(p.CenterV3, p.SizeV3);
			}
			else
			{
				bnd = GameObjectUtils.GetBounds(go);
				if (bnd.size == Vector3.zero)
				{
					UnityEngine.Debug.LogError($"{p.path} is not annotated. Skipping for now");
					continue;
				}
			}

			// Must lift the camera pivot point to prevent objects from not visible to camera.
			// float cam_offset_from_ground = (bnd.max.y - bnd.min.y) * 0.35f;
			// transform.position = Vector3.up * cam_offset_from_ground;
			transform.position = Vector3.up * bnd.center.y;

			string prefab_name = Path.GetFileName(p.path);
			string prefab_dir = Path.Combine(s.saveLocation, prefab_name);
			Directory.CreateDirectory(prefab_dir);

			print($"Rendering {prefab_name}");

			EdgeChecker ec = new EdgeChecker(bnd);

			// Rotate camera in x axis along the pivot
			for (int i = 0; i < I_MAX; i++)
			// for (int i = 0; i < 1; i++)
			{
				float rot_x = s.camAngleDelta * i;
				// Rotate camera in y axis along the pivot
				for (int j = 0; j < J_MAX; j++)
				{
					transform.rotation = Quaternion.Euler(rot_x, s.camAngleDelta * j, 0.0f);

					const float STEP = 5e-6f;
					const int MAX_ITERATION = 50;
					Vector3 STEP_VECTOR = Vector3.forward * STEP;

					float edge_len_max = ec.maxEdgeLen(cam);
					float compare_val = 0;

					if (edge_len_max < s.minProportion || edge_len_max > s.maxProportion)
					{
						const float THRESHOLD = 0.01f;

						// Object should appear larger -> move camera closer
						if (edge_len_max < s.minProportion)
						{
							compare_val = s.minProportion;
						}
						else if (edge_len_max > s.maxProportion)
						{
							compare_val = s.maxProportion;
						}

						const float CAM_MIN_Z = -20.0f;
						const float CAM_MAX_Z = -0.15f;
						float dynamic_random_center = (CAM_MIN_Z - CAM_MAX_Z) / 2.0F;
						// Use newton's method to find the camera distance
						for (int k = 0; k < MAX_ITERATION && Mathf.Abs(edge_len_max - compare_val) > THRESHOLD; k++)
						{
							float cur_z = cam.transform.localPosition.z;

							// Calculate the derivative
							cam.transform.Translate(STEP_VECTOR);
							float edge_len_max_step = ec.maxEdgeLen(cam);
							float tangent = (edge_len_max_step - edge_len_max) / STEP;
							float new_z = cur_z + (compare_val - edge_len_max) / tangent - STEP;
							// print($"{new_z} = {cur_z} + ({compare_val} - {edge_len_max}) / {tangent}");

							// Need to clamp the value to prevent bad estimation to screw up future calculatino
							new_z = Mathf.Clamp(new_z, CAM_MIN_Z, CAM_MAX_Z);

							// Since Newton's method can stuck, randomize z value if it appears to be stuck
							if ( cur_z == new_z && (new_z == CAM_MIN_Z || new_z == CAM_MAX_Z) )
							{
								new_z = Mathf.Clamp( dynamic_random_center + Random.Range(-0.5f, 0.5f), CAM_MIN_Z, CAM_MAX_Z);
								dynamic_random_center++;
							}
							cam.transform.localPosition = new Vector3(0.0f, 0.0f, new_z);

							// Update the values based on new camera position;
							edge_len_max = ec.maxEdgeLen(cam);
						}
					}

					string file_name = string.Format("{0}_{1:D3}.png", prefab_name, i * J_MAX + j);
					string file_path = Path.Combine(prefab_dir, file_name);
					yield return new WaitForEndOfFrame();
					// print("saving" + (i * J_MAX + j));
					ScreenCapture.CaptureScreenshot(file_path);

					
				}
			}

			Destroy(go);
			// Wait till the object successfully destroyed (it doesn't happen immediately)
			while (go != null)
			{
				yield return null;
			}
			
			GifCreator gc = s.gifOption;
			if (gc.ffmpegPath != null && gc.ffmpegPath != "")
			{
				gc.Init(prefab_dir, s);
				if ( gc.CreateGif() )
				{
					// Clear the directory if gif is successfully created
					Directory.Delete(prefab_dir, true);
				}
			}

			// Revert translation and rotation. If translation is not reset, the newton's method might
			// stuck due to bad initial guess.
			cam.transform.localPosition = CAM_ORIG_LOCAL_POS;
			transform.rotation = Quaternion.identity;
		}

		print("Finished");
		UnityEngine.Debug.Break();
	}
	
	static void LogErrorAndBreak(string err_str)
	{
		UnityEngine.Debug.LogError(err_str);
		UnityEngine.Debug.Break();
	}
}

} // namespace LightBoxScene
