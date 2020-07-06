using System.IO;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using StoryGenerator.Recording;
using StoryGenerator.Helpers;


public class PrintClassColor : MonoBehaviour
{
  // Use this for initialization
  void Start ()
  {
    List<string> allClasses = Helper.GetAllClasses();
    string FILE_PATH = Helper.DATA_RESOURCES_FOLDER + "class2rgb.txt";

    using (StreamWriter sw = new StreamWriter(FILE_PATH))
    {
        for (int i = 0; i < allClasses.Count; ++i)
        {
        Color32 classColor = PrefabClassEncoding.EncodeColor32(i);
        string line = string.Format("{0}: ({1},{2},{3})", allClasses[i],
            classColor.r, classColor.g, classColor.b);
        sw.WriteLine(line);
        }
    }

    Debug.Log("Successfully printed class colors on " + FILE_PATH);
    Debug.Break();
  }

}
