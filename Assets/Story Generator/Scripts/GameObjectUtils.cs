using StoryGenerator.Scripts;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace StoryGenerator.Utilities
{
    public static class GameObjectUtils
    {
        public static Transform GetRoomTransform(Transform t)
        {
            while (t != null) {
                if (t.CompareTag(Tags.TYPE_ROOM))
                    return t;
                t = t.parent;
            }
            return null;
        }

        // Checks if GameObject go is in path starting from Transform src
        public static bool IsInPath(GameObject go, Transform src)
        {
            Transform t = src;

            while (t != null) {
                if (t.gameObject == go)
                    return true;
                t = t.parent;
            }
            return false;
        }

        public static Collider GetCollider(GameObject go)
        {
            Collider co = go.GetComponent<Collider>();

            if (co == null)
                co = go.GetComponentInChildren<Collider>();
            return co;
        }

        public static Bounds GetBounds(GameObject go)
        {
            Bounds bnd = new Bounds();

            // Checked by the order of their accuracy.
            if (CheckBounds_nmo(go, ref bnd)) return bnd;
            if (CheckBounds_collider(go, ref bnd)) return bnd;
            if (CheckBounds_renderer(go, ref bnd)) return bnd;
            if (!go.name.Contains("Male") && !go.name.Contains("male"))
            {
                if (CheckBounds_childCollider(go, ref bnd)) return bnd;
            }
            if (CheckBounds_childRenderer(go, ref bnd)) return bnd;

            // If it reaches here, it's impossible to get the size of the bounds.
            // The best we can do for now is to return bounds that is centered at
            // the GameObject's position. Note that the position info is useless if 
            // the GameObject is a prefab that hasn't been instantiated
            return new Bounds(go.transform.position, Vector3.zero);
        }

        private static bool CheckBounds_nmo(GameObject go, ref Bounds bnd)
        {
            NavMeshObstacle nmo = go.GetComponent<NavMeshObstacle>();
            if (nmo != null) {
                bnd = CreateBounds(go, nmo.center, nmo.size);
                return true;
            }
            return false;
        }

        private static bool CheckBounds_collider(GameObject go, ref Bounds bnd)
        {
            BoxCollider bc = go.GetComponent<BoxCollider>();
            if (bc != null) {
                if (bc.bounds.extents != Vector3.zero) { // bound property returns zero extents on prefabs
                    bnd = bc.bounds;
                    return true;
                } else { // If this is a prefab
                    bnd = CreateBounds(go, bc.center, bc.size);
                    return true;
                }
            }

            return false;
        }

        private static bool CheckBounds_childCollider(GameObject go, ref Bounds bnd)
        {
            BoxCollider[] bcs = go.GetComponentsInChildren<BoxCollider>();
            if (bcs != null && bcs.Length != 0)
            {
                bnd = new Bounds();
                foreach (BoxCollider bc in bcs)
                {
                    Bounds curBounds = CreateBounds(bc.gameObject, bc.center, bc.size);
                    if (bnd.size == Vector3.zero)
                        bnd = curBounds;
                    else
                        bnd.Encapsulate(curBounds);

                    //if (Helpers.Helper.IsFirstV3GtrThanSecondV3(curBounds.size, maxBounds.size)) {
                    //    maxBounds = curBounds;
                    //}
                }


                if (bnd.size != Vector3.zero)
                {
                    //bnd = maxBounds;
                    return true;
                }
            }

            return false;

        }

        private static bool CheckBounds_renderer(GameObject go, ref Bounds bnd)
        {
            Renderer rdr = go.GetComponent<Renderer>();
            if (rdr != null && rdr.bounds.extents != Vector3.zero) {
                bnd = rdr.bounds;
                return true;
            }

            return false;
        }

        private static bool CheckBounds_childRenderer(GameObject go, ref Bounds bnd)
        {
            Renderer[] rdrs = go.GetComponentsInChildren<Renderer>();
            bnd = new Bounds();
            if (rdrs != null && rdrs.Length != 0)
            {
                foreach (Renderer r in rdrs)
                {
                    if (bnd.size == Vector3.zero)
                        bnd = r.bounds;
                    else
                        bnd.Encapsulate(r.bounds);
                }
                if (bnd.extents != Vector3.zero) return true;
            }
            return false;
        }

        private static Bounds CreateBounds(GameObject go, Vector3 pos, Vector3 size)
        {
            // Convert into world space.
            Vector3 center_world = go.transform.TransformPoint(pos);
            return new Bounds(center_world, CalcSize(go, size));
        }

        private static Vector3 CalcSize(GameObject go, Vector3 size)
        {
            Vector3 size_world = go.transform.TransformVector(size);
            // Sometimes, size vector has negative value.
            size_world.x = Mathf.Abs(size_world.x);
            size_world.y = Mathf.Abs(size_world.y);
            size_world.z = Mathf.Abs(size_world.z);

            return size_world;
        }

        public static List<Vector3> GetWalkLocation(Vector3[] points, out Vector3 current_point, out Vector3? lookAt, float walk_distance = 1.0f)
        {
            float WALK_DISTANCE = walk_distance;
            float cum_distance = 0.0f;
            current_point = points[0];
            Vector3 last_point;
            int index = 1;
            while (cum_distance < WALK_DISTANCE && index < points.Length)
            {
                float distance_points = Vector3.Distance(current_point, points[index]);
                cum_distance += distance_points;
                last_point = current_point;
                current_point = points[index];
                if (cum_distance > WALK_DISTANCE)
                {
                    // Debug.Log($"walked over");
                    float overwalked = cum_distance - WALK_DISTANCE;
                    float alpha = overwalked / distance_points;
                    current_point = Vector3.Lerp(last_point, points[index], 1 - alpha);
                    // Debug.Log($"walk pos calculated with walkover {current_point}");
                    // Debug.Log($"current corner: {points[index]}");
                    break;
                }
                index += 1;
            }
            if (index < points.Length)
            {
                lookAt = points[index];
            }
            else
            {
                lookAt = null;
            }
            List<Vector3> path_list = new List<Vector3>();
            path_list.AddRange(points);
            path_list[index - 1] = current_point;
            path_list.RemoveRange(0, index - 1);
            return path_list;

        }

        public static List<Vector3> CalculatePutPositions(Vector3 intPos, GameObject go, GameObject goDest, bool putInside,
            bool ignoreObstacles, Vector2? destPos = null, float yPos = -1)
        {

            return CalculatePutPositions(intPos, GetBounds(go), go.transform.position, goDest, putInside, ignoreObstacles, destPos, yPos);
        }

        // Put object go to goDest, character is at interaction position
        public static List<Vector3> CalculatePutPositions(Vector3 intPos, Bounds srcBounds, Vector3 srcPos, GameObject goDest, 
            bool putInside, bool ignoreObstacles, Vector2? destPos = null, float yPos = -1)
        {

            // "Optimal" distance from character to search for space (0.5 meters)
            Bounds destBounds = GetBounds(goDest);
            Vector3 dir = destBounds.center - intPos;
            bool is_expanding_scene = dir.magnitude == 0;
            float min_center_distance = 0.0f;
            float putCenterDistance = 0.5f;

            if (!putInside)
            {

                float smaller_surface_radius = Math.Min(destBounds.extents.x, destBounds.extents.z);
                float max_object_radius = Math.Max(srcBounds.extents.x, srcBounds.extents.z);

                if (is_expanding_scene)
                {
                    min_center_distance = Math.Max(smaller_surface_radius - 1.0f - max_object_radius, 0.0f);
                    putCenterDistance = 1.5f;
                }
                else
                {
                    // this is center ditance form character
                    min_center_distance = 0.0f;
                    putCenterDistance = 0.7f;
                }

            }

            // Factor to put object into goDest
            const float putInDirFactor = 0.9f;

            List<Vector3> result = new List<Vector3>();
            
            if (srcBounds.extents == Vector3.zero || destBounds.extents == Vector3.zero)
                return result;

            Vector3 destMin = destBounds.min;
            Vector3 destMax = destBounds.max;
            Vector3 srcCenter = srcBounds.center;

            dir.y = 0;
            if (putInside) {
                dir *= putInDirFactor;
            } else {
                dir.Normalize();
                dir *= putCenterDistance;
            }

            Vector3 center = new Vector3(intPos.x + dir.x, 0, intPos.z + dir.z);
            // If position specified
            if (destPos == null)
                for (float r = min_center_distance; r <= putCenterDistance; r += 0.1f) {  // advance radii by 10 cm
                    for (int i = 0; i < 20; i++) {                      // angle quantization is 360/20 = 18 degrees
                        float phi = 2 * Mathf.PI * i / 20;
                        float x = center.x + r * Mathf.Cos(phi);
                        float z = center.z + r * Mathf.Sin(phi);
                        Vector3 srcDelta;

                        if (putInside)
                            srcDelta = new Vector3(x - srcCenter.x, destMax.y - srcBounds.size.y - 0.03f - srcBounds.min.y, z - srcCenter.z);
                        else
                            srcDelta = new Vector3(x - srcCenter.x, destMax.y - srcBounds.min.y, z - srcCenter.z);

                        // Check if placement is sufficiently far from boundary
                        // bool ok = x > destMin.x + 0.1f && z > destMin.z + 0.1f && x < destMax.x - 0.1f && z < destMax.z - 0.1f;

                        //if (!ok)
                        //    continue;

                        float hity;

                        if (HitFlatSurface(srcBounds, srcDelta, goDest, out hity)) {

                            float yDelta = hity - (srcBounds.min.y + srcDelta.y);
                            Vector3 delta = srcDelta + new Vector3(0, yDelta, 0);

                            if (putInside || ignoreObstacles || !CheckBox(srcBounds, delta, 0.01f, goDest)) {
                                result.Add(srcPos + delta);
                            }
                        }
                    }
                }
            else
            {
                // obtain x and z values from the destination
                float x = destPos.Value.x;
                float z = destPos.Value.y;

                Vector3 srcDelta;

                if (yPos != -1) {
                    srcDelta = new Vector3(x - srcCenter.x, yPos, z - srcCenter.z);
                }
                else if (putInside)
                    srcDelta = new Vector3(x - srcCenter.x, destMax.y - srcBounds.size.y - 0.03f - srcBounds.min.y, z - srcCenter.z);
                else
                    srcDelta = new Vector3(x - srcCenter.x, destMax.y - srcBounds.min.y, z - srcCenter.z);

                float hity;

                if (HitFlatSurface(srcBounds, srcDelta, goDest, out hity))
                {

                    float yDelta = hity - (srcBounds.min.y + srcDelta.y);
                    Vector3 delta = srcDelta + new Vector3(0, yDelta, 0);

                    if (putInside || ignoreObstacles || !CheckBox(srcBounds, delta, 0.01f, goDest))
                    {
                        result.Add(srcPos + delta);
                    }
                }
            }
            return result;
        }

        // Checks if plane where bottom four vertices of goBounds hit a collider is horizontal (moving down)
        // hitHeight is y coordinate of hit points
        public static bool HitFlatSurface(Bounds goBounds, Vector3 goDelta, GameObject destGo, out float hitHeight)
        {
            

            Bounds b = new Bounds(goBounds.center + goDelta + 0.02f * Vector3.up, goBounds.extents);
            
            Vector3 bMin = b.min;
            Vector3 bMax = b.max;

            Transform hitTransform = null;
            hitHeight = 0;

            if (!CheckHitDown(bMin, ref hitTransform, ref hitHeight)) return false;

            if (!GameObjectUtils.IsInPath(destGo, hitTransform)) return false;
            if (!CheckHitDown(new Vector3(bMin.x, bMin.y, bMax.z), ref hitTransform, ref hitHeight)) return false;
            if (!CheckHitDown(new Vector3(bMax.x, bMin.y, bMin.z), ref hitTransform, ref hitHeight)) return false;
            if (!CheckHitDown(new Vector3(bMax.x, bMin.y, bMax.z), ref hitTransform, ref hitHeight)) return false;
            return true;
        }

        private static bool CheckHitDown(Vector3 p, ref Transform hitTransform, ref float yHit)
        {
            const float yTolerance = 0.01f;

            RaycastHit hit;

            if (!Physics.Raycast(p, Vector3.down, out hit)) return false;

            if (hitTransform == null) {
                hitTransform = hit.transform;
                yHit = hit.point.y;
            } else {
                if (hitTransform != hit.transform) return false;
            }

            if (Mathf.Abs(hit.point.y - yHit) > yTolerance) return false;

            return true;
        }

        // Returns true if box defined by bounds translated by delta and up*upDelta collides with some object
        public static bool CheckBox(Bounds bounds, Vector3 delta, float upDelta, GameObject exception)
        {
            Collider[] colliders = Physics.OverlapBox(bounds.center + delta + upDelta * Vector3.up, bounds.extents);

            foreach (Collider co in colliders) {
                if (!GameObjectUtils.IsInPath(exception, co.transform))
                    return true;
            }
            return false;
        }

        public static Bounds GetRoomBounds(GameObject room)
        {
            Bounds bounds = new Bounds();
            
            foreach (GameObject go in ScriptUtils.FindAllObjects(room.transform)) {
                Bounds goBounds = GetBounds(go);

                if (goBounds.extents == Vector3.zero)
                    continue;

                if (bounds.extents == Vector3.zero) bounds = goBounds;
                else bounds.Encapsulate(goBounds);
            }
            return bounds;
        }

        public static List<Vector3> CalculateDestinationPositions(Vector3 center, GameObject go, Bounds roomBounds)
        {
            const float ObstructionHeight = 0.1f;   // Allow for some obstuction around target object (e.g., carpet)
            const float PutCenterDistance = 2.0f;

            NavMeshAgent nma = go.GetComponent<NavMeshAgent>();
            List<Vector3> result = new List<Vector3>();

            for (float r = 0.0f; r <= PutCenterDistance; r += 0.3f) {  // advance radii by 10 cm
                for (int i = 0; i < 20; i++) {                      // angle quantization is 360/20 = 18 degrees
                    float phi = 2 * Mathf.PI * i / 20;
                    float x = center.x + r * Mathf.Cos(phi);
                    float z = center.z + r * Mathf.Sin(phi);

                    Vector3 cStart = new Vector3(x, nma.radius + ObstructionHeight, z);
                    Vector3 cEnd = new Vector3(x, nma.height - nma.radius + ObstructionHeight, z);

                    // Check for space
                    if (!Physics.CheckCapsule(cStart, cEnd, nma.radius)) {
                        result.Add(new Vector3(x, 0, z));
                    }
                }
            }
            return result;
        }
    }


}
