using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Video;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Linq;
using StoryGenerator.ChairProperties;
using StoryGenerator.CharInteraction;
using StoryGenerator.DoorProperties;
using StoryGenerator.Helpers;
using StoryGenerator.SceneState;
using StoryGenerator.Utilities;

namespace StoryGenerator.HomeAnnotation
{


    public class HomeAnnotator
    {


        static bool shouldRandomize;



        #region PublicMethods
        public static void ProcessHome(Transform h, bool _shouldRandomize)
        {
#if UNITY_EDITOR
            UnityEditor.AI.NavMeshBuilder.ClearAllNavMeshes();
            UnityEditor.AI.NavMeshBuilder.BuildNavMesh();
#endif
            if (_shouldRandomize)
            {
                UtilsAnnotator.GlobalRandomization(h);
            }
            UtilsAnnotator.Reset();

            var allChildren = h.GetComponentsInChildren<Transform>();
            foreach (Transform child in allChildren)
            {
                ObjectAnnotator.AnnotateObj(child);
            }

            UtilsAnnotator.PostColorEncoding_DisableGameObjects();

        }
        #endregion


    }
}
