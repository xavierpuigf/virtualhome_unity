using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace StoryGenerator.Utilities
{
    public class PolygonalArea
    {
        public Vector2[] Points { get; set; }

        public bool ContainsPoint(Vector2 v)
        {
            return WindingNumber(v, Points) != 0;
        }

        // Taken from: http://geomalgorithms.com/a03-_inclusion.html
        // 
        // Copyright 2000 softSurfer, 2012 Dan Sunday
        // This code may be freely used and modified for any purpose
        // providing that this copyright notice is included with it.
        // SoftSurfer makes no warranty for this code, and cannot be held
        // liable for any real or imagined damage resulting from its use.
        // Users of this code must verify correctness for their application.
        //
        // Note: v[0] must be equal to v[v.Length - 1]
        public static int WindingNumber(Vector2 p, Vector2[] v)
        {
            int wn = 0;    // the  winding number counter
            int n = v.Length - 1;

            // loop through all edges of the polygon
            for (int i = 0; i < n; i++) {       // edge from V[i] to  V[i+1]
                if (v[i].y <= p.y) {            // start y <= P.y
                    if (v[i + 1].y > p.y)       // an upward crossing
                        if (IsLeft(v[i], v[i + 1], p) > 0)  // P left of  edge
                            ++wn;               // have  a valid up intersect
                } else {                        // start y > P.y (no test needed)
                    if (v[i + 1].y <= p.y)      // a downward crossing
                        if (IsLeft(v[i], v[i + 1], p) < 0)  // P right of  edge
                            --wn;               // have  a valid down intersect
                }
            }
            return wn;
        }

        private static float IsLeft(Vector2 p0, Vector2 p1, Vector2 p2)
        {
            return ((p1.x - p0.x) * (p2.y - p0.y) - (p2.x - p0.x) * (p1.y - p0.y));
        }
    }

    public interface IInteractionArea
    {
        bool ContainsPoint(Vector2 v);
    }

    public class PolygonalInteractionArea : IInteractionArea
    {
        public PolygonalArea Area { get; set; }

        public bool ContainsPoint(Vector2 v)
        {
            return Area.ContainsPoint(v);
        }
    }

    public class MultiPolygonalInteractionArea : IInteractionArea
    {
        public PolygonalArea[] Areas { get; set; }

        public bool ContainsPoint(Vector2 v)
        {
            foreach (var pa in Areas) {
                if (pa.ContainsPoint(v)) return true;
            }
            return false;
        }
    }

    public static class GeoUtils
    {
        public static Vector2 ProjectY(Vector3 v)
        {
            return new Vector2(v.x, v.z);
        }

        public static float Cross(Vector2 p1, Vector2 p2)
        {
            return p1.x * p2.y - p2.x * p1.y;
        }

        // Return true if p1, p2, p3 appear in counter-clockwise order
        private static bool CCW(Vector2 p1, Vector2 p2, Vector2 p3)
        {
            return Cross(p2 - p1, p3 - p1) > 0;
        }

        // Return true if line segments ab and cd intersect.
        // Note that it works correctly if no three points are collinear
        public static bool SegmentsIntersectNC(Vector2 a, Vector2 b, Vector2 c, Vector2 d)
        {
            return CCW(a, c, d) != CCW(b, c, d) && CCW(a, b, c) != CCW(a, b, d);
        }

    }

    public static class RectUtils
    {
        public static float IntersectionArea(Rect r1, Rect r2)
        {
            Interval<float>? xIntersection = new Interval<float>(r1.xMin, r1.xMax).Intersection(r2.xMin, r2.xMax);
            Interval<float>? yIntersection = new Interval<float>(r1.yMin, r1.yMax).Intersection(r2.yMin, r2.yMax);
            return (xIntersection == null || yIntersection == null) ? 0 : xIntersection.Value.Area() * yIntersection.Value.Area();
        }

        public static float Area(this Rect r)
        {
            return r.width * r.height;
        }
    }

    public static class BoundsUtils
    {
        public static Rect XZRect(Bounds bounds)
        {
            return Rect.MinMaxRect(bounds.min.x, bounds.min.z, bounds.max.x, bounds.max.z);
        }

        public static Vector3[] CornersAndCenter(Bounds bounds, float factor)
        {
            Vector3 ext = bounds.extents * factor;
            Vector3[] result = new Vector3[9];

            result[0] = bounds.center;
            result[1] = bounds.center + new Vector3(ext.x, ext.y, ext.z);
            result[2] = bounds.center + new Vector3(ext.x, ext.y, -ext.z);
            result[3] = bounds.center + new Vector3(ext.x, -ext.y, ext.z);
            result[4] = bounds.center + new Vector3(ext.x, -ext.y, -ext.z);
            result[5] = bounds.center + new Vector3(-ext.x, ext.y, ext.z);
            result[6] = bounds.center + new Vector3(-ext.x, ext.y, -ext.z);
            result[7] = bounds.center + new Vector3(-ext.x, -ext.y, ext.z);
            result[8] = bounds.center + new Vector3(-ext.x, -ext.y, ext.z);
            return result;
        }

    }

    /// <summary>
    /// Closed interval [Start, End]
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public struct Interval<T> : IEquatable<Interval<T>> where T : IComparable<T>
    {
        private T start;
        private T end;


        public T Start { get { return start; } }
        public T End { get { return end; } }


        public Interval(Interval<T> other)
        {
            start = other.start;
            end = other.end;
        }

        public Interval(T start, T end)
        {
            if (start.CompareTo(end) < 0) {
                this.start = start; this.end = end;
            } else {
                this.start = end; this.end = start;
            }
        }

        public bool Contains(T element)
        {
            return element.CompareTo(start) >= 0 && element.CompareTo(end) <= 0;
        }

        public bool Contains(Interval<T> other)
        {
            return other.start.CompareTo(start) >= 0 &&
                    other.end.CompareTo(end) <= 0;
        }

        public Interval<T>? Intersection(Interval<T> other)
        {
            T s = Max(start, other.start);
            T e = Min(end, other.end);
            if (s.CompareTo(e) <= 0) return new Interval<T>(s, e);
            else return null;
        }

        public Interval<T>? Intersection(T a, T b)
        {
            T s = Max(start, a);
            T e = Min(end, b);
            if (s.CompareTo(e) <= 0) return new Interval<T>(s, e);
            else return null;
        }

        private static T Max(T a, T b)
        {
            if (a.CompareTo(b) > 0) return a; else return b;
        }

        private static T Min(T a, T b)
        {
            if (a.CompareTo(b) < 0) return a; else return b;
        }

        public bool Intersects(T a, T b)
        {
            T s = Max(start, a);
            T e = Min(end, b);
            return s.CompareTo(e) <= 0;
        }

        public bool Intersects(Interval<T> other)
        {
            T s = Max(start, other.start);
            T e = Min(end, other.end);
            return s.CompareTo(e) <= 0;
        }

        public T ClosestValue(T value)
        {
            if (value.CompareTo(start) < 0) return start;
            else if (value.CompareTo(end) > 0) return end;
            else return value;
        }

        public int CompareTo(Interval<T> other)
        {
            if (start.CompareTo(other.start) < 0) return -1;
            else if (start.CompareTo(other.start) > 0) return 1;
            else if (end.CompareTo(other.end) < 0) return -1;
            else if (end.CompareTo(other.end) > 0) return 1;
            else return 0;
        }

        public override String ToString()
        {
            return "[" + start + ", " + end + "]";
        }

        public bool Equals(Interval<T> other)
        {
            return start.Equals(other.start) && end.Equals(other.end);
        }
    }

    public static class IntervalExtensions
    {
        public static float Area(this Interval<float> interval)
        {
            return interval.End - interval.Start;
        }
    }

}
