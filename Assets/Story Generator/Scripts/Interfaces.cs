using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace StoryGenerator.Utilities
{
    public enum InteractionType {
        UNSPECIFIED, SPECIAL,
        CLOSE,
        FIND,
        GOTO,
        GRAB,
        DRINK,
        LOOKAT /* == LOOKAT_MEDIUM */, LOOKAT_SHORT, LOOKAT_MEDIUM, LOOKAT_LONG,
        OPEN,
        POINTAT,
        PUT, PUTBACK,
        PUTOBJBACK,
        PUTIN,
        RUN,
        SIT,
        STANDUP,
        SWITCHON,
        SWITCHOFF,
        TOUCH,
        TURNTO,
        WALK,
        WATCH,
        TALK,
        TEXT,
        WALKTOWARDS,
        WALKFORWARD,
        TURNLEFT,
        TURNRIGHT
    }

    public static class InteractionTypeGroup
    {
        public static HashSet<InteractionType> WATCH = new HashSet<InteractionType> {
                InteractionType.WATCH, InteractionType.LOOKAT, InteractionType.LOOKAT_SHORT,
                InteractionType.LOOKAT_MEDIUM, InteractionType.LOOKAT_LONG
            };

    }


    public interface IGameObjectPropertiesCalculator
    {
        HashSet<InteractionType> GetInteractionTypes(GameObject go);
        IInteractionArea GetInteractionArea(Vector3 goPos, InteractionType type);
        string GetObjectType(GameObject go);
    }

    public class DefaultGameObjectPropertiesCalculator : IGameObjectPropertiesCalculator
    {
        public HashSet<InteractionType> GetInteractionTypes(GameObject go)
        {
            return new HashSet<InteractionType>();
        }

        public IInteractionArea GetInteractionArea(Vector3 goPos, InteractionType type)
        {
            const int angleCount = 20;

            double radius;

            if (InteractionTypeGroup.WATCH.Contains(type))
                radius = 3.0;
            else
                radius = 1.5;

            List<Vector2> points = new List<Vector2>();

            for (int i = 0; i < angleCount; i++) {
                double phi = 2 * Math.PI * i / angleCount;
                points.Add(new Vector2((float)(goPos.x + radius * Math.Cos(phi)), (float)(goPos.z + radius * Math.Sin(phi))));
            }
            points.Add(points[0]);

            var polyArea = new PolygonalArea() { Points = points.ToArray() };

            return new PolygonalInteractionArea() { Area = polyArea };
        }

        public string GetObjectType(GameObject go)
        {
            return "GENERAL";
        }
    }

    public interface IObjectSelector
    {
        bool IsSelectable(GameObject go);
    }

    public struct ScriptObjectName
    {
        public string Name { get; set; }
        public int Instance { get; set; }

        public ScriptObjectName(string name, int instance)
        {
            Name = name;
            Instance = instance;
        }
    }

    public interface IAction
    {
        ScriptObjectName Name { get; }    
        int ScriptLine { get; } 
    }

    public static class GameObjectExtensions
    {
        public static string GetPath(this GameObject go)
        {
            return GetPathAddTransform(go.transform, "");
        }

        public static List<string> GetPathNames(this GameObject go)
        {
            var result = new List<string>();

            GetPathAddName(go.transform, result);
            return result;
        }

        private static string GetPathAddTransform(Transform t, string suffix)
        {
            if (t == null) return suffix;
            else return GetPathAddTransform(t.parent, "/" + t.name + suffix);
        }

        private static void GetPathAddName(Transform t, List<string> path)
        {
            if (t != null) {
                path.Insert(0, t.name);
                GetPathAddName(t.parent, path);
            }
        }

        public static string RoomName(this GameObject go)
        {
            return go.GetPathNames()[1];
        }

        public static bool IsRoom(this GameObject go)
        {
            return go.GetPathNames().Count == 2;
        }

    }

    public static class StringExtensions
    {
        public static bool Contains(this string source, string toCheck, StringComparison comp)
        {
            return source.IndexOf(toCheck, comp) >= 0;
        }
    }

}
