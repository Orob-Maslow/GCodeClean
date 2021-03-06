// Copyright (c) 2020 - Lee HUMPHRIES (lee@md8n.com) and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE.txt file in the project root for details.

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace GCodeClean.Processing
{
    public static class Utility
    {
        public static char[] Commands = { 'G', 'M' };

        public static char[] Arguments = { 'A', 'B', 'C', 'D', 'F', 'H', 'I', 'J', 'K', 'L', 'N', 'P', 'R', 'S', 'T', 'X', 'Y', 'Z' };

        public static string[] MovementCommands = { "G0", "G1", "G2", "G3", "G00", "G01", "G02", "G03" };

        public static string[] ArcCommands = { "G2", "G3", "G02", "G03" };


        /// <summary>
        /// Roughly equivalent to `IsNullOrWhiteSpace` this returns true if there are:
        /// * no tokens,
        /// * only a file terminator,
        /// * only one or more comments
        /// </summary>
        public static Boolean IsNotCommandOrArguments(this List<string> tokens)
        {
            return tokens.Count == 0 || tokens.All(t => t[0] == '%') || tokens.All(t => t[0] == '(');
        }

        /// <summary>
        /// This returns true if there are one or more Arguments but no Commands, comments are ignored for this test
        /// </summary>
        public static Boolean IsArgumentsOnly(this List<string> tokens)
        {
            if (tokens.IsNotCommandOrArguments())
            {
                return false;
            }

            return tokens.Any(t => Arguments.Any(a => a == t[0])) && !tokens.Any(t => Commands.Any(a => a == t[0]));
        }

        /// <summary>
        /// This returns true if there are one or more Arguments but no Commands, comments are ignored for this test
        /// </summary>
        public static Boolean HasMovementCommand(this List<string> tokens)
        {
            if (tokens.IsArgumentsOnly())
            {
                return false;
            }

            return tokens.Any(t => MovementCommands.Contains(t));
        }

        /// <summary>
        /// Compares two sets of tokens to ensure they are completely the same
        /// </summary>
        public static Boolean AreTokensEqual(this List<string> tokensA, List<string> tokensB)
        {
            if (tokensA.Count != tokensB.Count)
            {
                return false;
            }
            var isDuplicate = true;
            for (var ix = 0; ix < tokensB.Count; ix++)
            {
                if (tokensA[ix] != tokensB[ix])
                {
                    isDuplicate = false;
                    break;
                }
            }
            return isDuplicate;
        }

        /// <sumary>
        /// Compares two sets of tokens to ensure they are `compatible`
        /// </summary>
        public static Boolean AreTokensCompatible(this List<string> tokensA, List<string> tokensB)
        {
            if (tokensA.Count != tokensB.Count)
            {
                return false;
            }
            var isCompatible = true;
            for (var ix = 0; ix < tokensB.Count; ix++)
            {
                if (tokensA[ix][0] != tokensB[ix][0])
                {
                    isCompatible = false;
                    break;
                }
                if (tokensA[ix][0] == 'G' || tokensA[ix][0] == 'M')
                {
                    // For 'Commands' the whole thing must be the same
                    if (tokensA[ix] != tokensB[ix])
                    {
                        isCompatible = false;
                        break;
                    }
                }
            }
            return isCompatible;
        }

        public static Coord ExtractCoords(this List<string> tokens)
        {
            var coords = new Coord();
            decimal? value = null;
            foreach (var token in tokens)
            {
                value = token.ExtractCoord();
                if (value.HasValue)
                {
                    if (token[0] == 'X')
                    {
                        coords.X = value.Value;
                        coords.Set |= CoordSet.X;
                    }
                    if (token[0] == 'Y')
                    {
                        coords.Y = value.Value;
                        coords.Set |= CoordSet.Y;
                    }
                    if (token[0] == 'Z')
                    {
                        coords.Z = value.Value;
                        coords.Set |= CoordSet.Z;
                    }
                }
            }

            return coords;
        }

        public static decimal? ExtractCoord(this string token)
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                return null;
            }

            decimal value;
            if (decimal.TryParse((string)token.Substring(1), out value))
            {
                return value;
            }

            return null;
        }

        /// <summary>
        /// Is B between A and C, inclusive
        /// </summary>
        public static Boolean WithinRange(this decimal B, decimal A, decimal C)
        {
            return (A >= B && B >= C) || (A <= B && B <= C);
        }

        public static Double Angle(this Double da, Double db)
        {
            var theta = Math.Atan2((Double)da, (Double)db); // range (-PI, PI]
            theta *= 180 / Math.PI; // rads to degs, range (-180, 180]

            return theta;
        }

        public static decimal Sqr(this decimal value)
        {
            return value * value;
        }

        public static decimal Distance(this (Coord A, Coord B) c)
        {
            return (decimal)Math.Sqrt((double)((c.B.X - c.A.X).Sqr() + (c.B.Y - c.A.Y).Sqr() + (c.B.Z - c.A.Z).Sqr()));
        }

        public static decimal Angle(this (decimal A, decimal B) d)
        {
            var theta = Math.Atan2((Double)d.A, (Double)d.B); // range (-PI, PI]
            theta *= 180 / Math.PI; // rads to degs, range (-180, 180]

            return (decimal)theta;
        }

        // Function to find the circle on 
        // which the given three points lie 
        public static (Coord center, decimal radius, bool isClockwise) FindCircle(Coord a, Coord b, Coord c)
        {
            var center = new Coord();
            var radius = 0M;
            var isClockwise = false;

            // We only calculate a circle through one orthogonal plane,
            // therefore at least one of the dimensions must be the same for all 3 coords
            var ortho = Coord.Ortho(new List<Coord>() { a, b, c });
            if (ortho == CoordSet.None)
            {
                return (center, radius, isClockwise);
            }

            // Convert to points in 2 dimensions
            var dropCoord = CoordSet.Z;
            if ((ortho & CoordSet.X) == CoordSet.X)
            {
                dropCoord = CoordSet.X;
            }
            else if ((ortho & CoordSet.Y) == CoordSet.Y)
            {
                dropCoord = CoordSet.Y;
            }
            var pA = a.ToPointF(dropCoord);
            var pB = b.ToPointF(dropCoord);
            var pC = c.ToPointF(dropCoord);

            var xAB = pA.X - pB.X;
            var xAC = pA.X - pC.X;

            var yAB = pA.Y - pB.Y;
            var yAC = pA.Y - pC.Y;

            var yCA = pC.Y - pA.Y;
            var yBA = pB.Y - pA.Y;

            var xCA = pC.X - pA.X;
            var xBA = pB.X - pA.X;

            // pA.X^2 - pC.X^2 
            var sxAC = Math.Pow(pA.X, 2) - Math.Pow(pC.X, 2);

            // pA.Y^2 - pC.Y^2 
            var syAC = Math.Pow(pA.Y, 2) - Math.Pow(pC.Y, 2);

            var sxBA = Math.Pow(pB.X, 2) - Math.Pow(pA.X, 2);

            var syBA = Math.Pow(pB.Y, 2) - Math.Pow(pA.Y, 2);

            var f = ((sxAC) * (xAB)
                    + (syAC) * (xAB)
                    + (sxBA) * (xAC)
                    + (syBA) * (xAC))
                    / (2 * ((yCA) * (xAB) - (yBA) * (xAC)));
            var g = ((sxAC) * (yAB)
                    + (syAC) * (yAB)
                    + (sxBA) * (yAC)
                    + (syBA) * (yAC))
                    / (2 * ((xCA) * (yAB) - (xBA) * (yAC)));

            if (double.IsInfinity(f) || double.IsInfinity(g))
            {
                // lines are parallel / colinear
                return (center, radius, isClockwise);
            }

            var circ = -Math.Pow(pA.X, 2) - Math.Pow(pA.Y, 2) -
                                        2 * g * pA.X - 2 * f * pA.Y;

            // eqn of circle be x^2 + y^2 + 2*g*x + 2*f*y + c = 0 
            // where centre is (h = -g, k = -f) and radius r 
            // as r^2 = h^2 + k^2 - c 
            var h = -g;
            var k = -f;
            var sqr_of_r = h * h + k * k - circ;

            radius = (decimal)Math.Round(Math.Sqrt(sqr_of_r), 5);
            center = new Coord((decimal)h, (decimal)k, dropCoord);

            isClockwise = DirectionOfPoint(pA, pB, center.ToPointF()) < 0;

            return (center, radius, isClockwise);
        }

        public static int DirectionOfPoint(PointF pA, PointF pB, PointF pC)
        {
            // subtracting co-ordinates of point A  
            // from B and P, to make A as origin
            pB.X -= pA.X;
            pB.Y -= pA.Y;
            pC.X -= pA.X;
            pC.Y -= pA.Y;

            // Determining cross product 
            var cross_product = (pB.X * pC.Y) - (pB.Y * pC.X);

            // return the sign of the cross product 
            if (cross_product > 0)
            {
                return 1;
            }
            if (cross_product < 0)
            {
                return -1;
            }
            return 0;
        }

        public static List<Coord> FindIntersections(Coord cA, Coord cB, decimal radius)
        {
            var intersections = new List<Coord>();

            // We only calculate a circle through one orthogonal plane,
            // therefore at least one of the dimensions must be the same for both coords
            var ortho = Coord.Ortho(new List<Coord>() { cA, cB });
            if (ortho == CoordSet.None)
            {
                return intersections;
            }

            // Convert to points in 2 dimensions
            var dropCoord = CoordSet.Z;
            if ((ortho & CoordSet.X) == CoordSet.X)
            {
                dropCoord = CoordSet.X;
            }
            else if ((ortho & CoordSet.Y) == CoordSet.Y)
            {
                dropCoord = CoordSet.Y;
            }
            var pA = cA.ToPointF(dropCoord);
            var pB = cB.ToPointF(dropCoord);

            // Find the distance between the centers.
            var dx = pA.X - pB.X;
            var dy = pA.Y - pB.Y;
            double dist = Math.Sqrt(dx * dx + dy * dy);

            // See how many solutions there are.
            if (dist > (double)(radius * 2) || dist == 0)
            {
                // No solutions, the circles are too far apart or coincide, must be malformed
                return intersections;
            }

            // Find a and h.
            var a = (dist * dist) / (2 * dist);
            var h = Math.Sqrt((double)radius.Sqr() - a * a);

            // Find pC.
            var pC = new PointF((float)(pA.X + a * (pB.X - pA.X) / dist), (float)(pA.Y + a * (pB.Y - pA.Y) / dist));

            // Get the points P3.
            intersections.Add(new Coord(new PointF(
                (float)(pC.X + h * (pB.Y - pA.Y) / dist),
                (float)(pC.Y - h * (pB.X - pA.X) / dist)), dropCoord));

            // Do we have 1 or 2 solutions.
            if (dist < (double)(radius * 2))
            {
                intersections.Add(new Coord(new PointF(
                (float)(pC.X - h * (pB.Y - pA.Y) / dist),
                (float)(pC.Y + h * (pB.X - pA.X) / dist)), dropCoord));
            }

            return intersections;
        }
    }
}
