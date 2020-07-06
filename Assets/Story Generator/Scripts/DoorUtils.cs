using StoryGenerator.DoorProperties;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;

namespace StoryGenerator.Utilities
{
    public class DoorControl
    {
        public IList<Properties_door> doors = new List<Properties_door>();

        public void Update(IList<GameObject> objectList)
        {
            doors.Clear();
            foreach (GameObject go in objectList) {
                var pd = go.GetComponent<Properties_door>();

                if (pd != null) 
                    doors.Add(pd);
            }
        }

        public void MarkClosed(Properties_door pd)
        {
            pd.offMeshLink.activated = true;
        }

        public void MarkOpen(Properties_door pd)
        {
            pd.offMeshLink.activated = false;
        }

        public int ClosedDoorsCount()
        {
            return doors.Count(pd => pd.offMeshLink.activated);
        }

        public IList<DoorAction> SelectDoorsOnPath(Vector3[] path, bool closedOnly)
        {
            IList<DoorAction> result = new List<DoorAction>();
            for (int i = 1; i < path.Length; i++)
            {
                Vector2 p = ProjectXZ(path[i - 1]);
                Vector2 q = ProjectXZ(path[i]);
                foreach (Properties_door pd in doors)
                {
                    if (closedOnly && !pd.offMeshLink.activated)
                        continue;

                    if (GeoUtils.SegmentsIntersectNC(p, q, pd.doorwayA, pd.doorwayB))
                    {


                        Vector2 pull = ProjectXZ(pd.transformPull.position);
                        Vector2 push = ProjectXZ(pd.transformPush.position);
                        DoorAction da = new DoorAction();
                        Vector2 ppdir = (pull - push).normalized;
                        float dp = Vector2.Dot((q - p).normalized, pull - push);

                        if (dp > 0)
                        {
                            // push
                            da.pull = false;
                            da.posOne = pd.posItrnPush;
                            da.posTwo = pd.lookAtPush;
                        }
                        else
                        {
                            // pull
                            da.pull = true;
                            da.posOne = pd.posItrnPull;
                            da.posTwo = pd.lookAtPull;
                        }
                        da.properties = pd;
                        result.Add(da);
                        break;
                    }
                }
            }

            return result;
        }

        public static Vector2 ProjectXZ(Vector3 v3)
        {
            return new Vector2(v3.x, v3.z);
        }

        public static Vector3 ToXZ(Vector2 v2)
        {
            return new Vector3(v2.x, 0, v2.y);
        }
    }

    public class DoorAction
    {
        public Properties_door properties;
        public Vector3 posOne;
        public bool pull;
        public Vector3 posTwo;
    }

}
