using System;
using System.Collections.Generic;
using System.Linq;

namespace Elements.Geometry
{
    /// <summary>
    /// A utility class for calculating Convex Hulls from inputs
    /// </summary>
    public static class ConvexHull
    {
        /// <summary>
        /// Calculate a polygon from the 2d convex hull of a collection of points.
        /// Adapted from https://rosettacode.org/wiki/Convex_hull#C.23
        /// </summary>
        /// <param name="points">a collection of points</param>
        /// <returns>A polygon representing the convex hull of the provided points.</returns>
        public static Polygon FromPoints(IEnumerable<Vector3> points)
        {
            if (points.Count() == 0)
            {
                return null;
            }
            var pointsSorted = points.OrderBy(p => p.X).ThenBy(p => p.Y).ToArray();
            List<Vector3> hullPoints = new List<Vector3>();

            Func<Vector3, Vector3, Vector3, bool> Ccw = (Vector3 a, Vector3 b, Vector3 c) => ((b.X - a.X) * (c.Y - a.Y)) > ((b.Y - a.Y) * (c.X - a.X));

            // lower hull
            foreach (var pt in pointsSorted)
            {
                while (hullPoints.Count >= 2 && !Ccw(hullPoints[hullPoints.Count - 2], hullPoints[hullPoints.Count - 1], pt))
                {
                    hullPoints.RemoveAt(hullPoints.Count - 1);
                }
                hullPoints.Add(pt);
            }

            // upper hull
            int t = hullPoints.Count + 1;
            for (int i = pointsSorted.Length - 1; i >= 0; i--)
            {
                Vector3 pt = pointsSorted[i];
                while (hullPoints.Count >= t && !Ccw(hullPoints[hullPoints.Count - 2], hullPoints[hullPoints.Count - 1], pt))
                {
                    hullPoints.RemoveAt(hullPoints.Count - 1);
                }
                hullPoints.Add(pt);
            }

            hullPoints.RemoveAt(hullPoints.Count - 1);
            return new Polygon(hullPoints);
        }

        /// <summary>
        /// Get the 2D polygon convex hull of the points, allowing the points
        /// to be 3D and finding the polygon that frames those points when
        /// looking in the direction of the provided normal vector.
        /// </summary>
        /// <param name="points">A collection of points</param>
        /// <param name="normalVectorOfFrame">The direction of the frames perspective.</param>
        /// <returns>The polygonal frame that will encompass the points, roughly centered on the points themselves.</returns>
        public static Polygon Frame3DPoints(IEnumerable<Vector3> points, Vector3 normalVectorOfFrame)
        {
            if (normalVectorOfFrame.Length().ApproximatelyEquals(0))
            {
                throw new ArgumentException("The current normal vector cannot be of length 0");
            }
            if (normalVectorOfFrame.Unitized() != Vector3.ZAxis
                && normalVectorOfFrame.Unitized().Negate() != Vector3.ZAxis)
            {

                var transform = new Transform();
                transform.Rotate(normalVectorOfFrame.Cross(Vector3.ZAxis).Unitized(), normalVectorOfFrame.AngleTo(Vector3.ZAxis));
                var tPoints = points.Select(p => transform.OfPoint(p)).Select(p => new Vector3(p.X, p.Y));

                var twoDHull = FromPoints(tPoints);

                var threeDHull = twoDHull.TransformedPolygon(transform.Inverted());
                var center = points.Average();
                var planPoint = center.Project(threeDHull.Plane());
                var movement = center - planPoint;

                return threeDHull.TransformedPolygon(new Transform().Moved(movement));
            }
            else if (
                 normalVectorOfFrame.Unitized() == Vector3.ZAxis
                 || normalVectorOfFrame.Unitized().Negate() == Vector3.ZAxis)
            {
                var tPoints = points.Select(p => new Vector3(p.X, p.Y));
                return FromPoints(tPoints);
            }
            else
            {
                return FromPoints(points);
            }

        }

        /// <summary>
        /// Calculate a polygon from the 2d convex hull of a polyline or polygon's vertices.
        /// </summary>
        /// <param name="p">A polygon</param>
        /// <returns>A polygon representing the convex hull of the provided shape.</returns>
        public static Polygon FromPolyline(Polyline p)
        {
            return FromPoints(p.Vertices);
        }

        /// <summary>
        /// Calculate a polygon from the 2d convex hull of the vertices of a collection of polylines or polygons.
        /// </summary>
        /// <param name="polylines">A collection of polygons</param>
        /// <returns>A polygon representing the convex hull of the provided shapes.</returns>
        public static Polygon FromPolylines(IEnumerable<Polyline> polylines)
        {
            return FromPoints(polylines.SelectMany(p => p.Vertices));
        }

        /// <summary>
        /// Calculate a polygon from the 2d convex hull of a profile.
        /// </summary>
        /// <param name="p">A profile</param>
        /// <returns>A polygon representing the convex hull of the provided shape.</returns>
        public static Polygon FromProfile(Profile p)
        {
            // it's safe to consider only the perimeter because the voids must be within it
            return FromPolyline(p.Perimeter);
        }

    }
}