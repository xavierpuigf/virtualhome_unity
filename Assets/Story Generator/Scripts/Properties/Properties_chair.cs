using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.AI;

namespace StoryGenerator.ChairProperties
{
    public class Properties_chair : MonoBehaviour
    {
        public bool isBed;
        public ViewOptions viewOptions;
        public SittableArea[] SittableAreas;
    
        List<SittableUnit> m_list_su;

        // Used for detecting change in object position
        // or rotation so that we can recalculate if 
        // some SittableUnits become unaccessible.
        Vector3 m_prevPos;
        Quaternion m_prevQtrn;

        [System.Serializable]
        public struct ViewOptions
        {
            public bool edgeCheckBox;
            public bool sittableUnit;
            public bool sittingUnitCheckBox;
            public bool depObjDetection;
        }

        [System.Serializable]
        public class SittableArea
        {
            public Vector3 position;
            public Vector3 area;

            Transform m_tsfm_parent;

            const int NUMOFSIDECHECK = 4;

            private class EdgeChecker
            {
                public Vector3 boxCenter_local;
                public Vector3 boxHalfDimensions;
                public Quaternion boxQtrn;
                public bool isCollision = false;
                // Unit vector (in local space) that goes from the center
                // of the sitting area to the origin of this box. Notice
                // that Y component is ignored.
                public Vector3 unitVectToBox;
                public const float BOXWIDTH = 0.07f;

                const float BOXHEIGHT = 0.25f;
                // So that box check does not collide with surface itself
                const float BOX_CENTER_Y_OFFSET = 0.05f;

                public bool CheckAtEdge(Transform tsfmParent, int edgeNum,
                    Vector3 sittingAreaLocalPos, float sittingAreaX,
                    float sittingAreaZ, bool viewEdgeCheckBox)
                {
                    // Decrease box length so that things in a corner aren't counted more than once.
        			const int LAYER_MASK_DEFAULT = 1;
                    float boxLength_x = sittingAreaX / 3 * 2;
                    float boxLength_z = sittingAreaZ / 3 * 2;
                    boxCenter_local = sittingAreaLocalPos +
                        new Vector3(0, BOXHEIGHT / 2 + BOX_CENTER_Y_OFFSET, 0);

                    if (edgeNum % 2 == 0)
                    {
                        boxHalfDimensions = new Vector3(BOXWIDTH, BOXHEIGHT, boxLength_z) / 2;
                        float xVal = (sittingAreaX + BOXWIDTH) / 2;
                        if (edgeNum < 2)
                        {
                            boxCenter_local.x += xVal;
                            unitVectToBox = new Vector3(1, 0, 0);
                        }
                        else
                        {
                            boxCenter_local.x -= xVal;
                            unitVectToBox = new Vector3(-1, 0, 0);
                        }
                    }
                    else
                    {
                        boxHalfDimensions = new Vector3(BOXWIDTH, BOXHEIGHT, boxLength_x) / 2;
                        float zVal = (sittingAreaZ + BOXWIDTH) / 2;
                        if (edgeNum < 2)
                        {
                            boxCenter_local.z -= zVal;
                            unitVectToBox = new Vector3(0, 0, -1);
                        }
                        else
                        {
                            boxCenter_local.z += zVal;
                            unitVectToBox = new Vector3(0, 0, 1);
                        }
                    }

                    boxQtrn = Quaternion.Euler(0, edgeNum * 90, 0);
                    Collider[] collided = Physics.OverlapBox( tsfmParent.TransformPoint(boxCenter_local),
                        boxHalfDimensions, tsfmParent.rotation * boxQtrn, LAYER_MASK_DEFAULT);

                    // For all colliders, check if any of them are from this chair
                    // because sitting units are generated based on chair shape, not
                    // other colliding objects.
                    foreach (Collider c in collided)
                    {
                        if ( c.transform.IsChildOf(tsfmParent) )
                        {
                            isCollision = true;
                            break;
                        }
                    }

                    if (viewEdgeCheckBox)
                    {
                        Transform box = new GameObject("CheckBox_" + edgeNum).transform;
                        // box.SetParent(tsfmParent);
                        box.SetParent(tsfmParent, false);
                        box.localPosition = boxCenter_local;
                        box.localRotation = boxQtrn;
                        BoxCollider bc = box.gameObject.AddComponent<BoxCollider> ();
                        bc.enabled = false; // so that the edges of the box are not vivid
                        bc.size = boxHalfDimensions * 2;
                    }

                    return isCollision;
                }

                public float GetSittableAreaLength()
                {
                    return boxHalfDimensions.z * 3;
                }
            }

            // Returns number of generated SittableUnit
            public void GenerateSittableUnit(Transform tsfm, bool isBed,
                List<SittableUnit> list_su, bool viewEdgeCheckBox)
            {
                m_tsfm_parent = tsfm;

                // Try to determine where character should be, according to the shape of the chair.
                // It checks four edges of the sittable area of the chair to see if there is collision.
                int cnt = 0; // used for chairs only (not bed)
                EdgeChecker[] ecs = new EdgeChecker[NUMOFSIDECHECK];

                for (int i = 0; i < NUMOFSIDECHECK; i++)
                {
                    ecs[i] = new EdgeChecker();
                    if ( ecs[i].CheckAtEdge(m_tsfm_parent, i, position,
                        area.x, area.z, viewEdgeCheckBox) )
                    {
                        cnt++;
                    }
                }

                if (isBed || cnt == 0) // chair with zero collision shares the logic as bed
                {
                    Handle_bedSetup(ecs, list_su);
                }
                else
                {
                    Handle_chairSetup(cnt, ecs, list_su);
                }
            }

            void Handle_bedSetup(EdgeChecker[] ecs, List<SittableUnit> list_su)
            {
                for (int i = 0; i < NUMOFSIDECHECK; i++)
                {
                    // Only spawn SittableUnit on sides without collision
                    if (! ecs[i].isCollision)
                    {
                        GenerateSUonEdge(ecs[i],
                            ecs[FindIdxOfNeighborEdge(i)].unitVectToBox, list_su);
                    }
                }
            }

            void Handle_chairSetup(int cnt, EdgeChecker[] ecs, List<SittableUnit> list_su)
            {
                int idx;
                if (cnt == 1 || cnt == 3)
                {
                    // If cnt == 1, there is only back part of a chair.
                    // We will find this box and find other box located
                    // across.
                    bool collisionState = true;

                    // This chair has arm rest as well. Find a box which did not collide
                    if (cnt == 3)
                    {
                        collisionState = false;
                    }

                    for (idx = 0; idx < NUMOFSIDECHECK; idx++)
                    {
                        if (ecs[idx].isCollision == collisionState)
                        {
                            break;
                        }
                    }
                }
                // When cnt == 2, find a box with collision that has longer
                // length. That is usually the back part of a chair.
                else
                {
                    // Find two boxes that had collision
                    int idx1 = -1;
                    int idx2 = -1;

                    for (int i = 0; i < NUMOFSIDECHECK; i++)
                    {
                        if (ecs[i].isCollision == true)
                        {
                            if (idx1 == -1)
                            {
                                idx1 = i;
                            }
                            else
                            {
                                idx2 = i;
                            }
                        }
                    }

                    idx = ecs[idx1].boxHalfDimensions.z > ecs[idx2].boxHalfDimensions.z ? 
                        idx1 : idx2;
                }

                // This chair only has back. Look for the back position and find the
                // matching box on the opposite side
                if (cnt == 1 || cnt == 2)
                {
                    idx = FindIdxOfBoxAcross(idx);
                }

                GenerateSUonEdge(ecs[idx],
                    ecs[FindIdxOfNeighborEdge(idx)].unitVectToBox, list_su);
            }
                
            void GenerateSUonEdge(EdgeChecker ec, Vector3 unitSUMoveDir, List<SittableUnit> list_su)
            {
                Vector3 adjustedBoxPos = new Vector3(ec.boxCenter_local.x,
                    position.y, ec.boxCenter_local.z);
                Vector3 centerOfSUs = adjustedBoxPos -
                    (EdgeChecker.BOXWIDTH + SittableUnit.UNIT_LENGTH_X) / 2 * ec.unitVectToBox;

                float totalLength = ec.GetSittableAreaLength();
                int numOfSUs = (int) (totalLength / SittableUnit.UNIT_LENGTH_Z);
                float gap = (totalLength - numOfSUs * SittableUnit.UNIT_LENGTH_Z) / (numOfSUs + 1);
                float intervalBtwSUs = gap + SittableUnit.UNIT_LENGTH_Z;

                Vector3 suLocalPos = centerOfSUs -
                    ( (totalLength - SittableUnit.UNIT_LENGTH_Z) / 2 - gap) * unitSUMoveDir;
                for (int i = 0; i < numOfSUs; i++)
                {
                    list_su.Add( new SittableUnit( m_tsfm_parent, list_su.Count,
                        suLocalPos, ec.boxQtrn ) );
                    suLocalPos += intervalBtwSUs * unitSUMoveDir;
                }
            }

            int FindIdxOfBoxAcross(int idx)
            {
                return (idx + 2) % NUMOFSIDECHECK;
            }

            int FindIdxOfNeighborEdge(int idx)
            {
                // We can either + or - but it doesn't really matter
                return (idx + 1) % NUMOFSIDECHECK;
            }
        }

        public class SittableUnit
        {
            public Transform tsfm_group;
            public bool isSittable = true;
            // Can a character walk to the interaction position?
            public bool isAccessible = true;
            public const float UNIT_LENGTH_X = 0.355f;
            public const float UNIT_LENGTH_Z = 0.4f;
            public readonly static Vector3 CHECKBOX_HALF_DIMENSION = new Vector3(
                DEFAULT_CHAR_DIMENSION_BOX_SHAPE_X,
                DEFAULT_CHAR_DIMENSION_BOX_SHAPE_Y,
                DEFAULT_CHAR_DIMENSION_BOX_SHAPE_Z) / 2;
            public readonly static Vector3 CHECKBOX_HALF_DIMENSION_SHORT = new Vector3(
                DEFAULT_CHAR_DIMENSION_BOX_SHAPE_X,
                DEFAULT_CHAR_DIMENSION_BOX_SHAPE_Y / 2,
                DEFAULT_CHAR_DIMENSION_BOX_SHAPE_Z) / 2;

            // Local coordinates of the position where we check box
            // to determine if this SittableUnit are 1. Accessible 2. Sittable
            public Vector3 CHECKBOX_LOCAL_POS_ITRN;
            public Vector3 CHECKBOX_LOCAL_POS_SIT_AREA;

            const string NAME_PREFIX_SU = "SittableUnit_";
            const string NAME_POS_INTRCN = "Position_interaction";
            const string NAME_LOOK_AT = "LookAt_body";
            // We no longer uses IK target on SittableUnit for sitting.
            // It's left intact due to compatibility with other codes.
            const float INTERACTION_POS_OFFSET_FROM_CHAIR = 0.15f;
            const int CHILD_IDX_POS_ITRN = 0;
            const int CHILD_IDX_LOOKAT_BODY = 1;
            const float DEFAULT_CHAR_DIMENSION_BOX_SHAPE_X = 0.3f;
            const float DEFAULT_CHAR_DIMENSION_BOX_SHAPE_Y = 1.8f;
            const float DEFAULT_CHAR_DIMENSION_BOX_SHAPE_Z = 0.3f;
            const float CHECKBOX_POS_Y_OFFSET = 0.1f;

            public SittableUnit(Transform tsfm_parent, int num, Vector3 localPos,
                Quaternion localQtrn)
            {
                tsfm_group = new GameObject(NAME_PREFIX_SU + num).transform;
                Transform charPos = new GameObject(NAME_POS_INTRCN).transform;
                Transform lookAt_body = new GameObject(NAME_LOOK_AT).transform;

                tsfm_group.SetParent(tsfm_parent, false);
                charPos.SetParent(tsfm_group, false);
                lookAt_body.SetParent(tsfm_group, false);

                tsfm_group.localPosition = localPos;
                tsfm_group.localRotation = localQtrn;
                charPos.localPosition = new Vector3(UNIT_LENGTH_X / 2 + INTERACTION_POS_OFFSET_FROM_CHAIR,
                  -tsfm_group.position.y);
                lookAt_body.localPosition = charPos.localPosition + 
                    new Vector3(4 * INTERACTION_POS_OFFSET_FROM_CHAIR, 0, 0);

                CHECKBOX_LOCAL_POS_ITRN = charPos.localPosition +
                    new Vector3(INTERACTION_POS_OFFSET_FROM_CHAIR,
                    DEFAULT_CHAR_DIMENSION_BOX_SHAPE_Y / 2 + CHECKBOX_POS_Y_OFFSET, 0);
                CHECKBOX_LOCAL_POS_SIT_AREA = new Vector3(
                    0, DEFAULT_CHAR_DIMENSION_BOX_SHAPE_Y / 4 + CHECKBOX_POS_Y_OFFSET, 0);
                UpdateConditions();
            }

            public void UpdateConditions()
            {
                isAccessible = ! Physics.CheckBox(
                    tsfm_group.TransformPoint(CHECKBOX_LOCAL_POS_ITRN),
                    CHECKBOX_HALF_DIMENSION,
                    tsfm_group.rotation);
                isSittable = ! Physics.CheckBox(
                    tsfm_group.TransformPoint(CHECKBOX_LOCAL_POS_SIT_AREA),
                    CHECKBOX_HALF_DIMENSION_SHORT,
                    tsfm_group.rotation);
            }

            public Transform GetTsfm_positionInteraction()
            {
                return tsfm_group.GetChild(CHILD_IDX_POS_ITRN);
            }
                
            public Transform GetTsfm_lookAtBody()
            {
                return tsfm_group.GetChild(CHILD_IDX_LOOKAT_BODY);
            }
        }

        #region UnityEventFunctions
        void Awake()
        {
            if (SittableAreas != null)
            {
                Initialize();
            }
        }

        void Update()
        {
            if (SittableAreas != null && m_list_su != null)
            {
                if (transform.position != m_prevPos || transform.rotation != m_prevQtrn)
                {
                    foreach(SittableUnit su in m_list_su)
                    {
                        su.UpdateConditions();
                    }

                    m_prevPos = transform.position;
                    m_prevQtrn = transform.rotation;
                }
            }
        }

        void OnDrawGizmosSelected()
        {
            if (this.enabled)
            {
                Gizmos.color = new Color(0.0f, 1.0f, 1.0f, 0.5f);
                if (SittableAreas != null)
                {
                    Gizmos.matrix = transform.localToWorldMatrix;
                    foreach (SittableArea sa in SittableAreas)
                    {
                        Gizmos.DrawCube(sa.position, sa.area);
                    }

                    if (viewOptions.sittableUnit && m_list_su != null)
                    {
                        foreach (SittableUnit su in m_list_su)
                        {
                            if (su.isSittable)
                            {
                                if (su.isAccessible)
                                {
                                    Gizmos.color = new Color(0.0f, 1.0f, 0.0f, 0.6f);
                                }
                                else
                                {
                                    Gizmos.color = new Color(0.7f, 0.0f, 1.0f, 0.6f);
                                }
                            }
                            else
                            {
                                Gizmos.color = new Color(1.0f, 0.0f, 0.0f, 0.6f);
                            }
                            Vector3 rectSize = new Vector3(SittableUnit.UNIT_LENGTH_X, 0, SittableUnit.UNIT_LENGTH_Z);
                            Gizmos.matrix = su.tsfm_group.localToWorldMatrix;
                            Gizmos.DrawCube(Vector3.zero, rectSize);

                            if (viewOptions.sittingUnitCheckBox)
                            {
                                Gizmos.color = new Color(0.1f, 0.0f, 0.5f, 0.3f);
                                Gizmos.DrawWireCube(su.CHECKBOX_LOCAL_POS_ITRN,
                                    SittableUnit.CHECKBOX_HALF_DIMENSION * 2);
                                Gizmos.DrawWireCube(su.CHECKBOX_LOCAL_POS_SIT_AREA,
                                    SittableUnit.CHECKBOX_HALF_DIMENSION_SHORT * 2);
                            }
                        }
                    }
                }
            }
        }
        #endregion


        #region PublicMethods
        public void Initialize()
        {
            m_list_su = new List<SittableUnit> ();
            foreach(SittableArea sa in SittableAreas)
            {
                sa.GenerateSittableUnit(transform, isBed, m_list_su, viewOptions.edgeCheckBox);
            }

            // Adjust the size of the attached NavMeshObstacle to allow
            // closer sitting. This avoids reducing Navigation agent size
            // for baking the area.
            NavMeshObstacle nmo = GetComponentInChildren<NavMeshObstacle> ();
            if (nmo != null)
            {
                nmo.size = nmo.size * 0.85f;
            }

            m_prevPos = transform.position;
            m_prevQtrn = transform.rotation;
        }

        public List<SittableUnit> GetSittableUnits()
        {
            List<SittableUnit> list_su_useable = new List<SittableUnit>();
            foreach (SittableUnit su in m_list_su) {
                if (su.isSittable && su.isAccessible) {
                    list_su_useable.Add(su);
                }
            }
            return list_su_useable;
        }
        #endregion
    }
    
}
