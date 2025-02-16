﻿using System;
using System.Collections.Generic;
using Elements.Spatial.AdaptiveGrid;
using Elements.Geometry;
using System.Linq;
using Elements.Geometry.Solids;

namespace Elements.Spatial.AdaptiveGrid
{
    /// <summary>
    /// Class for routing through an AdaptiveGrid.
    /// </summary>
    public class AdaptiveGraphRouting
    {
        private AdaptiveGrid _grid;
        private RoutingConfiguration _configuration;

        /// <summary>
        /// Structure that holds information about polylines that are used to guide routing.
        /// </summary>
        public struct RoutingHintLine
        {
            /// <summary>
            /// Construct new RoutingHintLine structure.
            /// </summary>
            /// <param name="polyline">Geometry of HintLine.</param>
            /// <param name="factor">Cost multiplier.</param>
            /// <param name="influence">How far it affects.</param>
            /// <param name="userDefined">Is user defined.</param>
            public RoutingHintLine(
                Polyline polyline, double factor, double influence, bool userDefined)
            {
                Polyline = polyline;
                Factor = factor;
                InfluenceDistance = influence;
                UserDefined = userDefined;
            }

            /// <summary>
            /// 2D Polyline geometry representation with an influence that is extended on both sides in Z direction.
            /// </summary>
            public readonly Polyline Polyline;

            /// <summary>
            /// Cost multiplier for edges that lie within the Influence distance to the line.
            /// </summary>
            public readonly double Factor;

            /// <summary>
            /// How far away from the line, edge travel cost is affected.
            /// Both sides of an edge and its middle point should be within influence range.
            /// </summary>
            public readonly double InfluenceDistance;

            /// <summary>
            /// Is line created by the user or from internal parameters?
            /// User defined lines are preferred for input Vertex connection.
            /// </summary>
            public readonly bool UserDefined;
        }

        /// <summary>
        /// Structure that holds additional information about inlet vertex
        /// </summary>
        public struct RoutingVertex
        {
            /// <summary>
            /// Construct new RoutingVertex structure.
            /// </summary>
            /// <param name="id">Id of the vertex in the grid.</param>
            /// <param name="isolationRadius"> Distance, other sections of the route can't travel near this vertex.</param>
            /// <param name="guides">Additional vertices this vertex need to travel through.</param>
            public RoutingVertex(
                ulong id, double isolationRadius, List<ulong> guides = null)
            {
                Id = id;
                IsolationRadius = isolationRadius;
                Guides = guides;
            }

            /// <summary>
            /// Id of the vertex in the grid.
            /// </summary>
            public ulong Id;

            /// <summary>
            /// Additional vertices this vertex need to travel through
            /// </summary>
            public List<ulong> Guides;

            /// <summary>
            /// Distance closer than which, other sections of the route can't travel near this vertex. 
            /// Distance is in base plane of the gird, without elevation.
            /// </summary>
            public double IsolationRadius;
        }

        /// <summary>
        /// Object that holds common parameters that affect routing.
        /// </summary>
        public struct RoutingConfiguration
        {
            /// <summary>
            /// Construct new RoutingConfiguration structure.
            /// </summary>
            /// <param name="turnCost">Travel cost penalty if route changes it's direction.</param>
            /// <param name="mainLayer">Elevation at which route prefers to travel.</param>
            /// <param name="layerPenalty">Penalty if route travels through an elevation different from MainLayer.</param>
            public RoutingConfiguration(double turnCost, double mainLayer,
                double layerPenalty)
            {
                TurnCost = turnCost;
                MainLayer = mainLayer;
                LayerPenalty = layerPenalty;
            }

            /// <summary>
            /// Travel cost penalty if route changes it's direction.
            /// </summary>
            public readonly double TurnCost;

            /// <summary>
            /// Elevation at which route prefers to travel.
            /// </summary>
            public readonly double MainLayer;

            /// <summary>
            /// Travel cost penalty if route travels through an elevation different from MainLayer.
            /// </summary>
            public readonly double LayerPenalty;
        }

        /// <summary>
        /// Enumeration that indicates one of two possible paths in routing.
        /// There are cases when we need to collect more than one path and
        /// only after some time we can decide which one is better.
        /// </summary>
        public enum BranchSide
        {
            /// <summary>
            /// Indicator that first, "left", path is preferred.
            /// </summary>
            Left,

            /// <summary>
            /// Indicator that second, "right" path is preferred.
            /// </summary>
            Right
        }

        /// <summary>
        /// Create AdaptiveGraphRouting objects and store core parameters for further use.
        /// </summary>
        /// <param name="grid">AdaptiveGrid the algorithm travels through.</param>
        /// <param name="configuration">Storage for common parameters that affect routing.</param>
        public AdaptiveGraphRouting(AdaptiveGrid grid, RoutingConfiguration configuration)
        {
            //TO DO. The process of using AdaptiveGrid/Routing pair is hard, see AdaptiveGraphRoutingTests.
            //You need to collect points manually from hint lines, obstacles, then you need to get ids
            //to pass them to routing still maintaining connection between positions and ids.
            //Need to consider extra level of abstraction on top, that will do all the work based on
            //global information line boundaries, points, lines and obstacles.
            _grid = grid;
            _configuration = configuration;
        }

        /// <summary>
        /// Visualize adaptive graph edges.
        /// Material depends on elevation and proximity to different hint lines.
        /// Original split points are not stored in the graph, so they need to be provided.
        /// </summary>
        /// <param name="hintLines">List of hint lines.</param>
        /// <param name="splitPoints">List of split points to visualize</param>
        /// <returns>List of graphics elements</returns>
        public IList<Element> RenderElements(IList<RoutingHintLine> hintLines,
                                             IList<Vector3> splitPoints)
        {
            List<Line> normalEdgesMain = new List<Line>();
            List<Line> hintEdgesMain = new List<Line>();
            List<Line> offsetEdgesMain = new List<Line>();
            List<Line> normalEdgesOther = new List<Line>();
            List<Line> hintEdgesOther = new List<Line>();
            List<Line> offsetEdgesOther = new List<Line>();

            var hintGroups = hintLines.GroupBy(h => h.UserDefined);
            var userHints = hintGroups.SingleOrDefault(hg => hg.Key == true);
            var defaultHints = hintGroups.SingleOrDefault(hg => hg.Key == false);

            foreach (var edge in _grid.GetEdges())
            {
                //There is only one edge for vertex pair, if this is changed -
                //we will need to check edges for uniqueness, 
                var v0 = _grid.GetVertex(edge.StartId);
                var v1 = _grid.GetVertex(edge.EndId);
                Line l = new Line(v0.Point, v1.Point);
                var mainLayer = OnMainLayer(v0, v1);
                if (IsAffectedBy(v0.Point, v1.Point, userHints))
                {
                    if (mainLayer == true)
                    {
                        hintEdgesMain.Add(l);
                    }
                    else
                    {
                        hintEdgesOther.Add(l);
                    }
                }
                else if (IsAffectedBy(v0.Point, v1.Point, defaultHints))
                {
                    if (mainLayer == true)
                    {
                        offsetEdgesMain.Add(l);
                    }
                    else
                    {
                        offsetEdgesOther.Add(l);
                    }
                }
                else
                {
                    if (mainLayer == true)
                    {
                        normalEdgesMain.Add(l);
                    }
                    else
                    {
                        normalEdgesOther.Add(l);
                    }
                }
            }

            List<Element> visualizations = new List<Element>();
            visualizations.Add(VisualizePoints(splitPoints));

            visualizations.Add(new ModelLines(normalEdgesMain, new Material(
                "Normal Edges Main", Colors.Blue)));
            visualizations.Add(new ModelLines(normalEdgesOther, new Material(
                "Normal Edges Other", Colors.Cobalt)));
            visualizations.Add(new ModelLines(offsetEdgesMain, new Material(
                "Offset Edges Main", Colors.Orange)));
            visualizations.Add(new ModelLines(offsetEdgesOther, new Material(
                "Offset Edges Other", Colors.Yellow)));
            visualizations.Add(new ModelLines(hintEdgesMain, new Material(
                "Hint Edges Main", Colors.Green)));
            visualizations.Add(new ModelLines(hintEdgesOther, new Material(
                "Hint Edges Other", Colors.Emerald)));

            return visualizations;
        }

        /// <summary>
        /// Creates tree of routes between set of input Vertices and the exit Vertex.
        /// Routes merge together to form a single trunk. Starting from end, point by point,
        /// vertices are connected to the network using Dijkstra algorithm.
        /// </summary>
        /// <param name="leafVertices">Vertices to connect into the system with extra information attached.</param>
        /// <param name="trunkPathVertices">End vertices, connected in the same order as provided. Exit location goes first.</param>
        /// <param name="hintLines">Collection of lines that routes are attracted to. At least one hint line is required.</param>
        /// <returns>Travel routes from inputVertices to the last of tailVertices.</returns>
        public IDictionary<ulong, ulong?> BuildSpanningTree(
            IList<RoutingVertex> leafVertices,
            IList<ulong> trunkPathVertices, IEnumerable<RoutingHintLine> hintLines)
        {
            if (!hintLines.Any())
            {
                throw new ArgumentException("At least one hint line is required to guide the tree");
            }

            //Excluded vertices includes inlets and vertices in certain distance around these inlets.
            //Sometimes it's not desirable for routing to go through them.
            var excludedVertices = ExcludedVertices(leafVertices);
            var allExcluded = new HashSet<ulong>();
            foreach (var item in excludedVertices)
            {
                item.Value.ForEach(v => allExcluded.Add(v));
            }

            var weights = CalculateWeights(hintLines);

            var vertexTree = new Dictionary<ulong, ulong?>();
            vertexTree[trunkPathVertices.First()] = null;
            foreach (var inlet in leafVertices)
            {
                vertexTree[inlet.Id] = null;
            }

            var hintGroups = hintLines.GroupBy(h => h.UserDefined);
            var userHints = hintGroups.SingleOrDefault(hg => hg.Key == true);
            var defaultHints = hintGroups.SingleOrDefault(hg => hg.Key == false);
            var hintVertices = NearbyVertices(userHints, leafVertices);
            var offsetVertices = NearbyVertices(defaultHints, leafVertices);
            //Hint lines can even go through excluded vertices
            allExcluded.ExceptWith(hintVertices.Select(hv => hv.Id));

            //1. Connect tail points, starting from exit point.
            //In other instances, algorithm goes towards the end, but here it's calculated backwards
            //to take into account end direction of previous section in a cheaper way.
            //Path is still added to tree from start to end.
            //TO DO: investigate if this can be done through automatic hint lines
            //TO DO: this section can be moved into `RouteAndAddTailVertices` function 
            for (int i = 0; i < trunkPathVertices.Count - 1; i++)
            {
                var c = ShortestPathDijkstra(trunkPathVertices[i], weights,
                    out var travelCost, vertexTree[trunkPathVertices[i]], allExcluded);
                var p = GetPathTo(c, trunkPathVertices[i + 1]);
                AddPathToTree(trunkPathVertices[i + 1], p, vertexTree);
            }

            //2. Connect furthest exit point to most efficient point on any of the hint lines.
            var connections = ShortestPathDijkstra(trunkPathVertices.Last(),
                                                   weights,
                                                   out var dropppipeTravelCost,
                                                   vertexTree[trunkPathVertices.Last()],
                                                   allExcluded);
            var droppipeCollectorTerminal = FindConnectionPoint(
                hintVertices, offsetVertices, dropppipeTravelCost);
            var droppipePath = GetPathTo(connections, droppipeCollectorTerminal);
            AddPathToTree(droppipeCollectorTerminal, droppipePath, vertexTree);

            //3. Connect inlets to  most efficient point on hint line.
            var inletToTrunkPaths = new Dictionary<ulong, List<ulong>>();
            var inletTouchPoint = new Dictionary<ulong, ulong>();
            var inletConnections = new Dictionary<ulong, Dictionary<ulong, ulong>>();
            var inletTravelCosts = new Dictionary<ulong, Dictionary<ulong, double>>();
            List<ulong> collectorTerminals = new List<ulong>();
            List<ulong> path = null;
            foreach (var inlet in leafVertices)
            {
                //TO DO: part of this section can be moved into `RouteLeaflVertex` function 
                var combinedPath = new List<ulong>();
                ulong? startDirection = null;
                var otherExcluded = FilteredSet(allExcluded, excludedVertices[inlet.Id]);

                ulong lastInChain = inlet.Id;
                if (inlet.Guides != null && inlet.Guides.Any() &&
                    !IsNearby(_grid.GetVertex(inlet.Id).Point, userHints))
                {
                    for (int i = 0; i < inlet.Guides.Count(); i++)
                    {
                        var c = ShortestPathDijkstra(lastInChain, weights,
                            out var cost, startDirection, otherExcluded);
                        var p = GetPathTo(c, inlet.Guides[i]);
                        CombinePath(combinedPath, p.Take(p.Count - 1).ToList());
                        inletConnections[lastInChain] = c;
                        inletTravelCosts[lastInChain] = cost;
                        lastInChain = inlet.Guides[i];
                        startDirection = p[p.Count - 2];
                    }
                }

                connections = ShortestPathDijkstra(lastInChain, weights,
                    out var travelCost, startDirection, otherExcluded);
                inletConnections[lastInChain] = connections;
                inletTravelCosts[lastInChain] = travelCost;
                var t1 = FindConnectionPoint(hintVertices, offsetVertices, travelCost);
                var t2 = FindConnectionPoint(droppipePath, travelCost);
                if (travelCost[t1] > travelCost[t2])
                {
                    path = GetPathTo(connections, t2);
                }
                else
                {
                    path = GetPathTo(connections, t1);
                    collectorTerminals.Add(t1);

                }
                CombinePath(combinedPath, path);
                inletToTrunkPaths[inlet.Id] = combinedPath;
                inletTouchPoint[path.Last()] = inlet.Id;
            }

            //4. Join all individual pieces together. We start from a single connection
            //path from droppipe and a set of connection points from the previous step.
            //One at a time we choose the connection point that is cheapest to travel to existing
            //network and its path is added to the network until all are added.
            HashSet<ulong> magnetTerminals = new HashSet<ulong>();
            droppipePath.ForEach(p => magnetTerminals.Add(p));

            var terminalInfo = new Dictionary<ulong, (
                Dictionary<ulong, ((ulong, BranchSide), (ulong, BranchSide))> Connections,
                Dictionary<ulong, (double, double)> Costs)>();
            foreach (var terminal in collectorTerminals)
            {
                var inlet = inletTouchPoint[terminal];
                var excluded = allExcluded;
                var localExcluded = excludedVertices[inlet];
                //Allow travel through excluded vertices of inlet only if it not yet left it's zone
                if (localExcluded.Contains(terminal))
                {
                    excluded = FilteredSet(allExcluded, localExcluded);
                }
                var pathBefore = inletToTrunkPaths[inlet];
                var vertexBefore = pathBefore[pathBefore.Count - 2];
                var branchConns = ShortestBranchesDijkstra(terminal, weights,
                    out var travelCost, vertexBefore, excluded);
                terminalInfo[terminal] = (branchConns, travelCost);
            }

            //Distances are precomputed beforehand to avoid square complexity on Dijkstra algorithm.
            //When terminal is connected to the trunk - we can choose one of two routing options,
            //considering also if any of them need extra turn when connected.
            while (collectorTerminals.Any())
            {
                ulong closestTerminal = 0;
                path = null;
                double lowestCost = double.MaxValue;
                foreach (var terminal in collectorTerminals)
                {
                    var info = terminalInfo[terminal];
                    var (localClosest, branchSide) = FindConnectionPoint(
                        magnetTerminals, info.Costs, info.Connections, vertexTree, weights);
                    var costs = info.Costs[localClosest];
                    var bestCost = branchSide == BranchSide.Left ? costs.Item1 : costs.Item2;
                    if (bestCost < lowestCost)
                    {
                        path = GetPathTo(info.Connections, localClosest, branchSide);
                        closestTerminal = terminal;
                        lowestCost = bestCost;
                    }
                }

                path.ForEach(p => magnetTerminals.Add(p));
                AddPathToTree(closestTerminal, path, vertexTree);
                collectorTerminals.Remove(closestTerminal);
            }

            //5. Inlets are added last. Trunk is already built, check for the best
            //join point once again, better one might be found.
            //TO DO: this section can be moved into `RerouteAndAddInletVertices` function 
            foreach (var inlet in leafVertices)
            {
                var key = inlet.Id;
                var oldPath = inletToTrunkPaths[inlet.Id];
                if (inlet.Guides != null && inlet.Guides.Any() &&
                    !IsNearby(_grid.GetVertex(inlet.Id).Point, userHints))
                {
                    var index = oldPath.IndexOf(inlet.Guides.Last());
                    if (index != -1)
                    {
                        key = inlet.Guides.Last();
                        var pathUntilKey = oldPath.Take(index + 1);
                        AddPathToTree(inlet.Id, pathUntilKey.ToList(), vertexTree);
                    }
                }

                //Don't change snapping point is distance is the same as before, as collector
                //joint point is more preferable than perpendicular connection to the trunk
                var oldTerminal = oldPath.Last();
                ulong bestIndex = oldTerminal;
                var costs = inletTravelCosts[key];
                double bestCost = costs[bestIndex] - _grid.Tolerance;
                var t = FindConnectionPoint(magnetTerminals, costs, bestIndex, bestCost);
                path = GetPathTo(inletConnections[key], t);
                AddPathToTree(key, path, vertexTree);
            }

            return vertexTree;
        }

        /// <summary>
        /// Creates tree of routes between multiple sections, each having a set of input Vertices,
        /// hint lines and local end Vertex, and the exit Vertex.
        /// Route is created by using Dijkstra algorithm locally on different segments.
        /// Segments are merged together to form a single trunk. Starting from end, point by point,
        /// segments are connected. Then, Vertices in each segments are connected as well, 
        /// forming a local trunk, connected with the main one.
        /// All parameter except "trunkPathVertices" are provided per section in the same order.
        /// </summary>
        /// <param name="leafVertices">Vertices to connect into the system with extra information attached.</param>
        /// <param name="localTails">Anchor points that will form global tree between sections.</param>
        /// <param name="trunkPathVertices">End vertices, connected in the same order as provided. Exit location goes first.</param>
        /// <param name="hintLines">Collection of lines that routes are attracted to. At least one hint line per group is required.</param>
        /// <returns>Travel routes from inputVertices to the last of tailVertices.</returns>
        public IDictionary<ulong, ulong?> BuildSpanningTree(
            IList<List<RoutingVertex>> leafVertices, IList<ulong> localTails,
            IList<ulong> trunkPathVertices, IList<List<RoutingHintLine>> hintLines)
        {
            if (!hintLines.Any() || hintLines.Any(hl => hl == null || !hl.Any()))
            {
                throw new ArgumentException("At least one hint line in each group is required to guide the tree");
            }

            var allLeafs = leafVertices.SelectMany(l => l).ToList();
            var allHints = hintLines.SelectMany(h => h).ToList();

            //Excluded vertices includes inlets and vertices in certain around inlets
            //Sometimes it's not desirable for routing to go through them.
            var excludedVertices = ExcludedVertices(allLeafs);
            var allExcluded = new HashSet<ulong>();
            foreach (var item in excludedVertices)
            {
                item.Value.ForEach(v => allExcluded.Add(v));
            }

            var weights = CalculateWeights(allHints);
            var allUserHints = allHints.Where(h => h.UserDefined == true).ToList();
            var nearbyHints = NearbyVertices(allUserHints, allLeafs);
            //Hint lines can even go through excluded vertices
            allExcluded.ExceptWith(nearbyHints.Select(nh => nh.Id));

            var vertexTree = new Dictionary<ulong, ulong?>();
            vertexTree[trunkPathVertices.First()] = null;
            foreach (var inlets in leafVertices)
            {
                foreach (var inlet in inlets)
                {
                    vertexTree[inlet.Id] = null;
                }
            }

            //1. Connect global tail points, starting from exit point.
            //In other instances, algorithm goes towards the end, but here it's calculated backwards
            //to take into account end direction of previous section in a cheaper way.
            //Path is still added to tree from start to end.
            //TO DO: investigate if this can be done through automatic hint lines
            //TO DO: this section can be moved into `RouteAndAddTailVertices` function 
            for (int i = 0; i < trunkPathVertices.Count - 1; i++)
            {
                var connections = ShortestPathDijkstra(trunkPathVertices[i], weights,
                    out var travelCost, vertexTree[trunkPathVertices[i]], allExcluded);
                var p = GetPathTo(connections, trunkPathVertices[i + 1]);
                AddPathToTree(trunkPathVertices[i + 1], p, vertexTree);
            }

            //2. Connect ends of each sections together.
            //Starting from the trunk created by global tail points.
            //One at a time we choose the connection point that is cheapest to travel to existing
            //network and its path is added to the network until all are added.
            var tailsInfo = new Dictionary<ulong, (Dictionary<ulong, ulong> Connections,
                                                   Dictionary<ulong, double> Costs)>();
            for (int i = 0; i < localTails.Count; i++)
            {
                //Anchor point can travel through inlets, directly connected to hints.
                var exceptions = leafVertices[i].Where(
                    v => IsNearby(_grid.GetVertex(v.Id).Point, allUserHints));
                var excluded = FilteredSet(
                    allExcluded,
                    exceptions.SelectMany(e => excludedVertices[e.Id]));
                var connections = ShortestPathDijkstra(localTails[i], weights,
                    out var travelCost, null, excluded);
                tailsInfo[localTails[i]] = (connections, travelCost);
            }

            HashSet<ulong> magnetTail = new HashSet<ulong>();
            magnetTail.Add(trunkPathVertices.Last());
            var tailsCopy = new List<ulong>(localTails);
            while (tailsCopy.Any())
            {
                List<ulong> path = null;
                double lowestCost = double.PositiveInfinity;
                ulong closestTerminal = 0u;
                foreach (var terminal in tailsCopy)
                {
                    var info = tailsInfo[terminal];
                    var localClosest = FindConnectionPoint(magnetTail, info.Costs);
                    if (info.Costs[localClosest] < lowestCost)
                    {
                        path = GetPathTo(info.Connections, localClosest);
                        closestTerminal = terminal;
                        lowestCost = info.Costs[localClosest];
                    }
                }

                path.ForEach(p => magnetTail.Add(p));
                tailsCopy.Remove(closestTerminal);
                AddPathToTree(closestTerminal, path, vertexTree);
            }

            //Next steps are repeated independently for each input section
            for (int i = 0; i < leafVertices.Count; i++)
            {
                var hintGroups = hintLines[i].GroupBy(h => h.UserDefined);
                var userHints = hintGroups.SingleOrDefault(hg => hg.Key == true);
                var defaultHints = hintGroups.SingleOrDefault(hg => hg.Key == false);
                var hintVertices = NearbyVertices(userHints, leafVertices[i]);
                var offsetVertices = NearbyVertices(defaultHints, leafVertices[i]);

                var inletToTrunkPaths = new Dictionary<ulong, List<ulong>>();
                var inletTouchPoint = new Dictionary<ulong, ulong>();
                var inletConnections = new Dictionary<ulong, Dictionary<ulong, ulong>>();
                var inletTravelCosts = new Dictionary<ulong, Dictionary<ulong, double>>();
                List<ulong> collectorTerminals = new List<ulong>();
                List<ulong> path = null;

                //3. Connect inlets to  most efficient point on hint line.
                //User defined hint lines have priority, unless they are too far away.
                foreach (var inlet in leafVertices[i])
                {
                    //TO DO: part of this section can be moved into `RouteLeaflVertex` function 
                    var combinedPath = new List<ulong>();
                    ulong? startDirection = null;
                    var otherExcluded = FilteredSet(allExcluded, excludedVertices[inlet.Id]);

                    ulong lastInChain = inlet.Id;
                    if (inlet.Guides != null && inlet.Guides.Any() &&
                        !IsNearby(_grid.GetVertex(inlet.Id).Point, userHints))
                    {
                        for (int j = 0; j < inlet.Guides.Count(); j++)
                        {
                            var innerCons = ShortestPathDijkstra(lastInChain, weights,
                                out var innerCosts, startDirection, otherExcluded);
                            var p = GetPathTo(innerCons, inlet.Guides[j]);
                            CombinePath(combinedPath, p.Take(p.Count - 1).ToList());
                            inletConnections[lastInChain] = innerCons;
                            inletTravelCosts[lastInChain] = innerCosts;
                            lastInChain = inlet.Guides[j];
                            startDirection = p[p.Count - 2];
                        }
                    }

                    var connections = ShortestPathDijkstra(lastInChain, weights,
                        out var travelCost, startDirection, otherExcluded);
                    inletConnections[lastInChain] = connections;
                    inletTravelCosts[lastInChain] = travelCost;
                    var t = FindConnectionPoint(hintVertices, offsetVertices, travelCost);
                    path = GetPathTo(connections, t);
                    collectorTerminals.Add(t);

                    CombinePath(combinedPath, path);
                    inletToTrunkPaths[inlet.Id] = combinedPath;
                    inletTouchPoint[path.Last()] = inlet.Id;
                }

                //4. Join all individual pieces together. We start from a single connection
                //path from droppipe and a set of connection points from the previous step.
                //One at a time we choose the connection point that is cheapest to travel to existing
                //network and its path is added to the network until all are added.
                HashSet<ulong> magnetTerminals = new HashSet<ulong>();
                magnetTerminals.Add(localTails[i]);

                var terminalInfo = new Dictionary<ulong, (
                    Dictionary<ulong, ((ulong, BranchSide), (ulong, BranchSide))> Connections,
                    Dictionary<ulong, (double, double)> Costs)>();
                foreach (var terminal in collectorTerminals)
                {
                    var inlet = inletTouchPoint[terminal];
                    var excluded = allExcluded;
                    var localExcluded = excludedVertices[inlet];
                    //Allow travel through excluded vertices of inlet only if it not yet left it's zone
                    if (localExcluded.Contains(terminal))
                    {
                        excluded = FilteredSet(allExcluded, localExcluded);
                    }
                    var pathBefore = inletToTrunkPaths[inlet];
                    var vertexBefore = pathBefore[pathBefore.Count - 2];
                    var connections = ShortestBranchesDijkstra(terminal, weights,
                        out var travelCost, vertexBefore, excluded);
                    terminalInfo[terminal] = (connections, travelCost);
                }

                //Distances are precomputed beforehand to avoid square complexity on Dijkstra algorithm.
                //When terminal is connected to the trunk - we can choose one of two routing options,
                //considering also if any of them need extra turn when connected.
                while (collectorTerminals.Any())
                {
                    ulong closestTerminal = 0;
                    path = null;
                    double lowestCost = double.MaxValue;
                    foreach (var terminal in collectorTerminals)
                    {
                        var info = terminalInfo[terminal];
                        var (localClosest, branch) = FindConnectionPoint(
                            magnetTerminals, info.Costs, info.Connections, vertexTree, weights);
                        var costs = info.Costs[localClosest];
                        var bestCost = branch == BranchSide.Left ? costs.Item1 : costs.Item2;
                        if (bestCost < lowestCost)
                        {
                            path = GetPathTo(info.Connections, localClosest, branch);
                            closestTerminal = terminal;
                            lowestCost = bestCost;
                        }
                    }

                    path.ForEach(p => magnetTerminals.Add(p));
                    AddPathToTree(closestTerminal, path, vertexTree);
                    collectorTerminals.Remove(closestTerminal);
                }

                foreach (var t in magnetTail)
                {
                    magnetTerminals.Add(t);
                }

                //5. Inlets are added last. Trunk is already built, check for the best
                //join point once again, better one might be found.
                //TO DO: this section can be moved into `RerouteAndAddInletVertices` function 
                foreach (var inlet in leafVertices[i])
                {
                    var key = inlet.Id;
                    var oldPath = inletToTrunkPaths[inlet.Id];
                    if (inlet.Guides != null && inlet.Guides.Any() &&
                        !IsNearby(_grid.GetVertex(inlet.Id).Point, userHints))
                    {
                        var index = oldPath.IndexOf(inlet.Guides.Last());
                        if (index != -1)
                        {
                            key = inlet.Guides.Last();
                            var pathUntilKey = oldPath.Take(index + 1);
                            AddPathToTree(inlet.Id, pathUntilKey.ToList(), vertexTree);
                        }
                    }

                    //Don't change snapping point is distance is the same as before, as collector
                    //joint point is more preferable than perpendicular connection to the trunk
                    var oldTerminal = oldPath.Last();
                    ulong bestIndex = oldTerminal;
                    var costs = inletTravelCosts[key];
                    double bestCost = costs[bestIndex] - _grid.Tolerance;
                    var t = FindConnectionPoint(magnetTerminals, costs, bestIndex, bestCost);
                    path = GetPathTo(inletConnections[key], t);
                    AddPathToTree(key, path, vertexTree);
                }
            }

            return vertexTree;
        }

        /// <summary>
        /// Create network of routes between set of input Vertices and set of exit Vertices.
        /// Each route is most efficient individually, without considering other routes.
        /// Each route is connected to the network using Dijkstra algorithm.
        /// </summary>
        /// <param name="leafVertices">Vertices to connect into the system with extra information attached.</param>
        /// <param name="exits">Possible exit vertices.</param>
        /// <param name="hintLines">Collection of lines that routes are attracted to.</param>
        /// <returns>Travel routes from inputVertices to the last of tailVertices.</returns>
        public IDictionary<ulong, ulong?> BuildSimpleNetwork(
            IList<RoutingVertex> leafVertices,
            IList<ulong> exits,
            IEnumerable<RoutingHintLine> hintLines)
        {
            //Excluded vertices includes inlets and vertices in certain distance around these inlets.
            //Sometimes it's not desirable for routing to go through them.
            var excludedVertices = ExcludedVertices(leafVertices);
            var allExcluded = new HashSet<ulong>();
            foreach (var item in excludedVertices)
            {
                item.Value.ForEach(v => allExcluded.Add(v));
            }

            var weights = CalculateWeights(hintLines);

            var vertexTree = new Dictionary<ulong, ulong?>();
            foreach (var trunk in exits)
            {
                vertexTree[trunk] = null;
            }

            foreach (var inlet in leafVertices)
            {
                vertexTree[inlet.Id] = null;
            }

            if (hintLines != null && hintLines.Any())
            {
                var hintGroups = hintLines.GroupBy(h => h.UserDefined);
                var userHints = hintGroups.SingleOrDefault(hg => hg.Key == true);
                var hintVertices = NearbyVertices(userHints, leafVertices);
                //Hint lines can even go through excluded vertices
                allExcluded.ExceptWith(hintVertices.Select(hv => hv.Id));
            }

            foreach (var inlet in leafVertices)
            {
                var connections = ShortestPathDijkstra(inlet.Id, weights, out var travelCost);
                var exit = FindConnectionPoint(exits, travelCost);
                var path = GetPathTo(connections, exit);
                AddPathToTree(inlet.Id, path, vertexTree);
            }

            return vertexTree;
        }

        /// <summary>
        /// Calculate weight for each edge in the graph. Information is stored as length
        /// of edge and extra factor that encourage or discourage traveling trough it.
        /// Edges that are not on main elevation have bigger factor.
        /// Edges that are near hint lines have smaller factor.
        /// Different factors are combined together.
        /// Also some edges are not allowed at all by setting factor to infinity.
        /// </summary>
        /// <param name="hintLines">Lines that affect travel factor for edges</param>
        /// <returns>For each edge - its length and extra traveling factor</returns>
        private Dictionary<ulong, (double Length, double Factor)> CalculateWeights(
            IEnumerable<RoutingHintLine> hintLines)
        {
            var weights = new Dictionary<ulong, (double Length, double Factor)>();
            foreach (var e in _grid.GetEdges())
            {
                var v0 = _grid.GetVertex(e.StartId);
                var v1 = _grid.GetVertex(e.EndId);

                double hintFactor = 1;
                double offsetFactor = 1;
                double layerFactor = OnMainLayer(v0, v1) ? 1 : _configuration.LayerPenalty;
                if (hintLines != null && hintLines.Any())
                {
                    foreach (var l in hintLines)
                    {
                        if (IsAffectedBy(v0.Point, v1.Point, l))
                        {
                            //If user defined and default hints are overlapped,
                            //we want path to be aligned with default hints.
                            //To achieve this to factors are combined.
                            if (l.UserDefined)
                            {
                                hintFactor = Math.Min(l.Factor, hintFactor);
                            }
                            else
                            {
                                offsetFactor = Math.Min(l.Factor, offsetFactor);
                            }
                        }
                    }
                }

                var w = (_grid.GetVertex(e.StartId).Point - _grid.GetVertex(e.EndId).Point).Length();
                weights[e.Id] = (w, hintFactor * offsetFactor * layerFactor);
            }

            return weights;
        }

        /// <summary>
        /// Collect vertices that are less than configured Manhattan distance
        /// away from given inlets, ignoring Z difference.
        /// </summary>
        /// <returns></returns>
        private Dictionary<ulong, List<ulong>> ExcludedVertices(
            IList<RoutingVertex> inletTerminals)
        {
            var excludedVertices = new Dictionary<ulong, List<ulong>>();
            foreach (var inlet in inletTerminals)
            {
                var adjustedTolerance = inlet.IsolationRadius - Vector3.EPSILON;
                var ip = _grid.GetVertex(inlet.Id).Point;
                var set = new List<ulong>();
                excludedVertices[inlet.Id] = set;

                foreach (var v in _grid.GetVertices())
                {
                    var p = v.Point;
                    var mhDistance2D = Math.Abs(p.X - ip.X) + Math.Abs(p.Y - ip.Y);
                    if (mhDistance2D < adjustedTolerance)
                    {
                        set.Add(v.Id);
                    }
                }
            }
            return excludedVertices;
        }

        /// <summary>
        /// This is a Dijkstra algorithm implementation.
        /// The algorithm travels from start point to all other points, gathering travel cost.
        /// Each time route turns - extra penalty is added to the cost.
        /// Higher level algorithm then decides which one of them to use as an end point.
        /// </summary>
        /// <param name="start">Start Vertex</param>
        /// <param name="edgeWeights">Dictionary of Edge Id to the cost of traveling though it</param>
        /// <param name="travelCost">Output dictionary where traveling cost is stored per Vertex</param>
        /// <param name="startDirection">Previous Vertex, if start Vertex is already part of the Route</param>
        /// <param name="excluded">Vertices that are not allowed to visit</param>
        /// <param name="pathDirections">Next Vertex dictionary for Vertices that are already part of the route</param>
        /// <returns>Dictionary that have travel routes from each Vertex back to start Vertex.</returns>
        public Dictionary<ulong, ulong> ShortestPathDijkstra(
            ulong start, Dictionary<ulong, (double Length, double Factor)> edgeWeights,
            out Dictionary<ulong, double> travelCost,
            ulong? startDirection = null, HashSet<ulong> excluded = null,
            Dictionary<ulong, ulong?> pathDirections = null)
        {
            PriorityQueue<ulong> pq = PreparePriorityQueue(
                start, out Dictionary<ulong, ulong> path, out travelCost);

            while (!pq.Empty())
            {
                //At each step retrieve the vertex with the lowest travel cost and
                //remove it, so it can't be visited again.
                ulong u = pq.PopMin();
                if (excluded != null && excluded.Contains(u))
                {
                    continue;
                }

                var vertex = _grid.GetVertex(u);
                foreach (var e in vertex.Edges)
                {
                    var edgeWeight = edgeWeights[e.Id];
                    if (edgeWeight.Length < 0)
                    {
                        continue;
                    }

                    var id = e.StartId == u ? e.EndId : e.StartId;
                    var v = _grid.GetVertex(id);

                    if ((excluded != null && excluded.Contains(id)) || !pq.Contains(id))
                    {
                        continue;
                    }

                    //All vertices that can be reached from start vertex are visited.
                    //Ignore once only unreachable are left.
                    var cost = travelCost[u];
                    if (cost == double.MaxValue)
                    {
                        break;
                    }

                    //Compute cost of each its neighbors as cost of vertex we came from plus cost of edge.
                    var newWeight = travelCost[u] + edgeWeight.Length * edgeWeight.Factor;

                    //We need as little change of direction as possible. A penalty is added if
                    //a) We have a turn traveling to the next vertex.
                    //b) We just started and going in direction different than how we arrived from the previous segment.
                    //c) We arrived at a vertex that is used in a different path.
                    if (u == start)
                    {
                        if (startDirection.HasValue &&
                            !AreCollinearInplace(_grid.GetVertex(startDirection.Value).Point, vertex.Point, v.Point))
                        {
                            newWeight += CalculateTurnCost(edgeWeight.Factor, vertex, startDirection.Value, edgeWeights);
                        }
                    }
                    else
                    {
                        var vertexBefore = _grid.GetVertex(path[u]);
                        if (!AreCollinearInplace(vertexBefore.Point, vertex.Point, v.Point))
                        {
                            newWeight += CalculateTurnCost(edgeWeight.Factor, vertex, vertexBefore.Id, edgeWeights);
                        }
                        if (pathDirections != null &&
                            pathDirections.TryGetValue(v.Id, out var vertexAfter) && vertexAfter.HasValue &&
                            !AreCollinearInplace(vertex.Point, v.Point, _grid.GetVertex(vertexAfter.Value).Point))
                        {
                            newWeight += CalculateTurnCost(edgeWeight.Factor, v, vertexAfter.Value, edgeWeights);
                        }
                    }

                    // If a lower travel cost to vertex is discovered update the vertex.
                    if (newWeight < travelCost[id])
                    {
                        travelCost[id] = newWeight;
                        path[id] = u;
                        pq.UpdatePriority(id, newWeight);
                    }
                }
            }
            return path;
        }

        /// <summary>
        /// This is a Dijkstra algorithm implementation that stores up to two different paths per vertex.
        /// The algorithm travels from start point to all other points, gathering travel cost.
        /// Each time route turns - extra penalty is added to the cost.
        /// Higher level algorithm then decides which one of them to use as an end point.
        /// Produced dictionary has "Left/Right" label using which two best routes per vertex can be retried.
        /// </summary>
        /// <param name="start">Start Vertex</param>
        /// <param name="edgeWeights">Dictionary of Edge Id to the cost of traveling though it</param>
        /// <param name="travelCost">Output dictionary where traveling costs are stored per Vertex for two possible branches</param>
        /// <param name="startDirection">Previous Vertex, if start Vertex is already part of the Route</param>
        /// <param name="excluded">Vertices that are not allowed to visit</param>
        /// <returns>Dictionary that have two travel routes from each Vertex back to start Vertex.</returns>
        public Dictionary<ulong, ((ulong, BranchSide), (ulong, BranchSide))> ShortestBranchesDijkstra(
            ulong start, Dictionary<ulong, (double Length, double Factor)> edgeWeights,
            out Dictionary<ulong, (double, double)> travelCost,
            ulong? startDirection = null, HashSet<ulong> excluded = null)
        {
            PriorityQueue<ulong> pq = PreparePriorityQueue(
                start, out Dictionary<ulong, ((ulong, BranchSide), (ulong, BranchSide))> path,
                out travelCost);

            while (!pq.Empty())
            {
                //At each step retrieve the vertex with the lowest travel cost and
                //remove it, so it can't be visited again.
                ulong u = pq.PopMin();
                if (excluded != null && excluded.Contains(u))
                {
                    continue;
                }

                var vertex = _grid.GetVertex(u);
                foreach (var e in vertex.Edges)
                {
                    var edgeWeight = edgeWeights[e.Id];
                    if (edgeWeight.Length < 0)
                    {
                        continue;
                    }

                    var id = e.StartId == u ? e.EndId : e.StartId;
                    var v = _grid.GetVertex(id);

                    if ((excluded != null && excluded.Contains(id)) || !pq.Contains(id))
                    {
                        continue;
                    }

                    //All vertices that can be reached from start vertex are visited.
                    //Ignore once only unreachable are left.
                    var cost = travelCost[u];
                    if (cost.Item1 == double.MaxValue)
                    {
                        break;
                    }

                    //Compute cost of each its neighbors as cost of vertex we came from plus cost of edge.
                    var newWeight = edgeWeight.Length * edgeWeight.Factor;
                    BranchSide bestBranch = BranchSide.Left;

                    //We need as little change of direction as possible. A penalty is added if
                    //a) We have a turn traveling to the next vertex.
                    //b) We just started and going in direction different than how we arrived from the previous segment.
                    //c) We arrived at a vertex that is used in a different path.
                    if (u == start)
                    {
                        if (startDirection.HasValue &&
                            !AreCollinearInplace(_grid.GetVertex(startDirection.Value).Point, vertex.Point, v.Point))
                        {
                            newWeight += CalculateTurnCost(edgeWeight.Factor, vertex, startDirection.Value, edgeWeights);
                        }
                    }
                    else
                    {
                        //For each of two stored branches - edges connected to active one.
                        //Add turn cost if the direction is changed.
                        var before = path[u];
                        var leftBefore = _grid.GetVertex(before.Item1.Item1);
                        var leftCollinear = AreCollinearInplace(leftBefore.Point, vertex.Point, v.Point);
                        var leftCost = cost.Item1 + newWeight;
                        if (!leftCollinear)
                        {
                            leftCost += CalculateTurnCost(
                                edgeWeight.Factor, vertex, leftBefore.Id, edgeWeights);
                        }

                        var rigthCost = Double.MaxValue;
                        if (before.Item2.Item1 != 0)
                        {
                            var rigthBefore = _grid.GetVertex(before.Item2.Item1);
                            rigthCost = cost.Item2 + newWeight;
                            if (!AreCollinearInplace(rigthBefore.Point, vertex.Point, v.Point))
                            {
                                rigthCost += CalculateTurnCost(
                                    edgeWeight.Factor, vertex, rigthBefore.Id, edgeWeights);
                            }
                        }

                        //Then choose the path that has lower accumulated value.
                        if (leftCost < rigthCost)
                        {
                            newWeight = leftCost;
                            bestBranch = BranchSide.Left;
                        }
                        else
                        {
                            newWeight = rigthCost;
                            bestBranch = BranchSide.Right;
                        }
                    }


                    var oldCost = travelCost[id];
                    var oldPath = path[id];
                    //Cheaper branch is stored first.
                    //But priority for the Vertex is set by the slower branch.
                    if (newWeight < oldCost.Item1)
                    {
                        travelCost[id] = (newWeight, oldCost.Item1);
                        path[id] = ((u, bestBranch), oldPath.Item1);
                        if (oldCost.Item1 == double.MaxValue)
                        {
                            //When we first meet the vertex we need to slow it down to allow
                            //other slightly slower path but with potentially better turn to reach it.
                            pq.UpdatePriority(id, newWeight + _configuration.TurnCost);
                        }
                        else
                        {
                            var newPriority = Math.Min(oldCost.Item1, newWeight + _configuration.TurnCost);
                            pq.UpdatePriority(id, newPriority);
                        }
                    }
                    else if (newWeight < oldCost.Item2)
                    {
                        travelCost[id] = (oldCost.Item1, newWeight);
                        path[id] = (oldPath.Item1, (u, bestBranch));
                        var newPriority = Math.Min(oldCost.Item1 + _configuration.TurnCost, newWeight);
                        pq.UpdatePriority(id, newPriority);
                    }
                }
            }
            return path;
        }

        /// <summary>
        /// Calculate turn cost between two edges.
        /// Turn cost should take into account cost factor of given edges.
        /// Otherwise hint paths with several turns would be ignored because of extra cost.
        /// The turn factor is multiplied by minimum of two edges factor.
        /// </summary>
        /// <param name="edgeFactor">Edge factor of the first edge</param>
        /// <param name="sharedVertex">Id of the vertex, common for two edges</param>
        /// <param name="thirdVertexId">Third vertex Id</param>
        /// <param name="edgeWeights">Precalculated length and factor for each edge</param>
        /// <returns></returns>
        private double CalculateTurnCost(
            double edgeFactor, Vertex sharedVertex, ulong thirdVertexId,
            IDictionary<ulong, (double Lenght, double Factor)> edgeWeights)
        {
            var otherEdge = sharedVertex.Edges.Where(
                edge => edge.StartId == thirdVertexId || edge.EndId == thirdVertexId).FirstOrDefault();
            var otherWeight = edgeWeights[otherEdge.Id];
            //Minimum factor makes algorithm prefer edges inside of hint lines even if they
            //have several turns but don't give advantage for the tiny edges that are
            //fully inside hint line influence area. 
            return _configuration.TurnCost * Math.Min(edgeFactor, otherWeight.Factor);
        }

        private PriorityQueue<ulong> PreparePriorityQueue(ulong start,
            out Dictionary<ulong, ulong> path, out Dictionary<ulong, double> travelCost)
        {
            path = new Dictionary<ulong, ulong>();
            travelCost = new Dictionary<ulong, double>();

            //Travel cost of all vertices are set to infinity, except for the one we start
            //from for which is set to 0.
            var vertices = _grid.GetVertices();
            List<ulong> indices = new List<ulong>() { start };
            for (int i = 0; i < vertices.Count; i++)
            {
                if (vertices[i].Id != start)
                {
                    indices.Add(vertices[i].Id);
                    travelCost[vertices[i].Id] = double.MaxValue;
                }
                path[vertices[i].Id] = 0;
            }
            travelCost[start] = 0;

            PriorityQueue<ulong> pq = new PriorityQueue<ulong>(indices);
            return pq;
        }

        private PriorityQueue<ulong> PreparePriorityQueue(ulong start,
            out Dictionary<ulong, ((ulong, BranchSide), (ulong, BranchSide))> path,
            out Dictionary<ulong, (double, double)> travelCost)
        {
            path = new Dictionary<ulong, ((ulong, BranchSide), (ulong, BranchSide))>();
            travelCost = new Dictionary<ulong, (double, double)>();

            //Travel cost of all vertices are set to infinity, except for the one we start
            //from for which is set to 0.
            var vertices = _grid.GetVertices();
            List<ulong> indices = new List<ulong>() { start };
            for (int i = 0; i < vertices.Count; i++)
            {
                if (vertices[i].Id != start)
                {
                    indices.Add(vertices[i].Id);
                    travelCost[vertices[i].Id] = (double.MaxValue, double.MaxValue);
                }
                path[vertices[i].Id] = ((0, BranchSide.Left), (0, BranchSide.Left));
            }
            travelCost[start] = (0, 0);

            PriorityQueue<ulong> pq = new PriorityQueue<ulong>(indices);
            return pq;
        }

        private List<ulong> GetPathTo(Dictionary<ulong, ulong> connections, ulong end)
        {
            List<ulong> shortestPath = new List<ulong>();
            shortestPath.Add(end);
            var before = connections[end];
            while (before != 0)
            {
                shortestPath.Add(before);
                before = connections[before];
            }

            return shortestPath.Reverse<ulong>().ToList();
        }

        private List<ulong> GetPathTo(
            Dictionary<ulong, ((ulong, BranchSide), (ulong, BranchSide))> connections,
            ulong end, BranchSide branch)
        {
            List<ulong> shortestPath = new List<ulong>();
            shortestPath.Add(end);
            var before = connections[end];
            var branchBefore = branch == BranchSide.Left ? before.Item1 : before.Item2;
            while (branchBefore.Item1 != 0)
            {
                shortestPath.Add(branchBefore.Item1);
                before = connections[branchBefore.Item1];
                branchBefore = branchBefore.Item2 == BranchSide.Left ? before.Item1 : before.Item2;
            }

            return shortestPath.Reverse<ulong>().ToList();
        }

        private void AddPathToTree(
            ulong start, List<ulong> path, Dictionary<ulong, ulong?> tree)
        {
            if (path.First() != start)
            {
                path.Reverse();
            }

            for (int i = 1; i < path.Count; i++)
            {
                //Path is composed from end to inlets. If tree already has next vertex
                //from this one recorded, we don't want to override it. This way we join
                //the flow that is already created, removing unnecessary loops.
                if (!tree.ContainsKey(path[i - 1]) || tree[path[i - 1]] == null)
                    tree[path[i - 1]] = path[i];
            }
        }

        private List<Vertex> NearbyVertices(
            IEnumerable<RoutingHintLine> hints,
            IEnumerable<RoutingVertex> excluded)
        {
            if (hints == null || !hints.Any())
            {
                return new List<Vertex>();
            }

            return _grid.GetVertices().Where(
                v => !excluded.Any(e => e.Id == v.Id) && IsNearby(v.Point, hints)).ToList();
        }

        private bool IsNearby(Vector3 v, IEnumerable<RoutingHintLine> hints)
        {
            return hints != null &&
                hints.Any(c => new Vector3(v.X, v.Y).DistanceTo(c.Polyline) < c.InfluenceDistance);
        }

        private bool IsAffectedBy(
            Vector3 start, Vector3 end, IEnumerable<RoutingHintLine> hints)
        {
            return hints != null && hints.Any(h => IsAffectedBy(start, end, h));
        }

        private bool IsAffectedBy(Vector3 start, Vector3 end, RoutingHintLine hint)
        {
            Func<Vector3, RoutingHintLine, bool> check = (Vector3 v, RoutingHintLine hl) =>
            {
                return v.DistanceTo(hl.Polyline) < hl.InfluenceDistance;
            };

            var vs_2d = new Vector3(start.X, start.Y);
            var ve_2d = new Vector3(end.X, end.Y);
            //Vertical edges are not affected by hint lines
            if (!vs_2d.IsAlmostEqualTo(ve_2d, _grid.Tolerance) && Math.Abs(start.Z - end.Z) < _grid.Tolerance)
            {
                //Start and End point of edge may be on hint line, but hint line itself
                //can go around the edge. Need to check middle point additionally.
                var vm_2d = new Vector3(vs_2d.X + (ve_2d.X - vs_2d.X) / 2.0,
                                        vs_2d.Y + (ve_2d.Y - vs_2d.Y) / 2.0);
                return check(vs_2d, hint) && check(ve_2d, hint) && check(vm_2d, hint);
            }
            return false;
        }

        private bool OnMainLayer(Vertex v0, Vertex v1)
        {
            return Math.Abs(v0.Point.Z - _configuration.MainLayer) < _grid.Tolerance &&
                   Math.Abs(v1.Point.Z - _configuration.MainLayer) < _grid.Tolerance;
        }

        private void Compare(ulong index, IDictionary<ulong, double> travelCost,
            ref double bestCost, ref ulong bestIndex)
        {
            if (travelCost.TryGetValue(index, out var cost))
            {
                if (cost < bestCost)
                {
                    bestCost = cost;
                    bestIndex = index;
                }
            }
        }

        private ulong FindConnectionPoint(
            IEnumerable<Vertex> hintVertices,
            IEnumerable<Vertex> offsetVertices,
            IDictionary<ulong, double> travelCost)
        {
            ulong bestIndex = 0;
            double bestCost = double.MaxValue;
            foreach (var v in hintVertices)
            {
                Compare(v.Id, travelCost, ref bestCost, ref bestIndex);
            }
            foreach (var v in offsetVertices)
            {
                Compare(v.Id, travelCost, ref bestCost, ref bestIndex);
            }
            return bestIndex;
        }

        private (ulong, BranchSide) FindConnectionPoint(
            IEnumerable<ulong> collection,
            IDictionary<ulong, (double, double)> travelCost,
            IDictionary<ulong, ((ulong, BranchSide), (ulong, BranchSide))> connections,
            IDictionary<ulong, ulong?> tree,
            IDictionary<ulong, (double Length, double Factor)> weights)

        {
            ulong bestIndex = 0;
            double bestCost = double.MaxValue;
            BranchSide bestBranch = BranchSide.Left;
            foreach (var index in collection)
            {
                if (travelCost.TryGetValue(index, out var costs))
                {
                    if (tree.TryGetValue(index, out var next) && next.HasValue)
                    {
                        double cost1 = costs.Item1;
                        double cost2 = costs.Item2;
                        var activeV = _grid.GetVertex(index);
                        var nextV = _grid.GetVertex(next.Value);

                        //TO DO: better investigate why they 0 sometimes
                        var before = connections[index];
                        var before1 = before.Item1.Item1;
                        var before2 = before.Item2.Item1;
                        if (before1 != 0)
                        {
                            var beforeV1 = _grid.GetVertex(before.Item1.Item1);
                            if (!AreCollinearInplace(beforeV1.Point, activeV.Point, nextV.Point))
                            {
                                var edge = activeV.Edges.Where(
                                    e => e.StartId == beforeV1.Id || e.EndId == beforeV1.Id).First();
                                var edgeWeight = weights[edge.Id];
                                cost1 += CalculateTurnCost(edgeWeight.Factor, activeV, nextV.Id, weights);
                            }
                        }

                        if (before2 != 0)
                        {
                            var beforeV2 = _grid.GetVertex(before.Item2.Item1);
                            if (!AreCollinearInplace(beforeV2.Point, activeV.Point, nextV.Point))
                            {
                                var edge = activeV.Edges.Where(
                                    e => e.StartId == beforeV2.Id || e.EndId == beforeV2.Id).First();
                                var edgeWeight = weights[edge.Id];
                                cost2 += CalculateTurnCost(edgeWeight.Factor, activeV, nextV.Id, weights);
                            }

                        }

                        var bestCandidate = cost1 < cost2 ? (cost1, BranchSide.Left) : (cost2, BranchSide.Right);
                        if (bestCandidate.Item1 < bestCost)
                        {
                            bestCost = bestCandidate.Item1;
                            bestIndex = index;
                            bestBranch = bestCandidate.Item2;
                        }
                    }
                    else
                    {
                        if (costs.Item1 < bestCost)
                        {
                            bestCost = costs.Item1;
                            bestIndex = index;
                            bestBranch = BranchSide.Left;
                        }
                    }
                }
            }
            return (bestIndex, bestBranch);
        }

        private ulong FindConnectionPoint(IEnumerable<ulong> collection,
            IDictionary<ulong, double> travelCost,
            ulong bestIndex = 0, double bestCost = double.MaxValue)
        {
            foreach (var v in collection)
            {
                Compare(v, travelCost, ref bestCost, ref bestIndex);
            }
            return bestIndex;
        }

        //This is the same as Vector3.AreCollinear but avoiding all expensive validations.
        private bool AreCollinearInplace(Vector3 a, Vector3 b, Vector3 c)
        {
            var baX = b.X - a.X;
            var baY = b.Y - a.Y;
            var baZ = b.Z - a.Z;
            var baLength = Math.Sqrt(Math.Pow(baX, 2) + Math.Pow(baY, 2) + Math.Pow(baZ, 2));
            if (baLength < Vector3.EPSILON)
            {
                return true;
            }
            baX = baX / baLength;
            baY = baY / baLength;
            baZ = baZ / baLength;

            var cbX = c.X - b.X;
            var cbY = c.Y - b.Y;
            var cbZ = c.Z - b.Z;
            var cbLength = Math.Sqrt(Math.Pow(cbX, 2) + Math.Pow(cbY, 2) + Math.Pow(cbZ, 2));
            if (cbLength < Vector3.EPSILON)
            {
                return true;
            }
            cbX = cbX / cbLength;
            cbY = cbY / cbLength;
            cbZ = cbZ / cbLength;

            return Math.Abs(baX * cbX + baY * cbY + baZ * cbZ) > (1 - Vector3.EPSILON);
        }

        private void CombinePath(List<ulong> mainPath, List<ulong> newPortion)
        {
            for (int i = 0; i < mainPath.Count; i++)
            {
                var index = newPortion.IndexOf(mainPath[i]);
                if (index != -1)
                {
                    mainPath.RemoveRange(i, mainPath.Count - i);
                    mainPath.AddRange(newPortion.Skip(index));
                    return;
                }
            }
            mainPath.AddRange(newPortion);
        }

        private HashSet<ulong> FilteredSet(HashSet<ulong> hashSet, IEnumerable<ulong> exceptions)
        {
            var setCopy = new HashSet<ulong>(hashSet);
            setCopy.ExceptWith(exceptions);
            return setCopy;
        }

        private ModelPoints VisualizePoints(IList<Vector3> points)
        {
            List<SolidOperation> so = new List<SolidOperation>();
            double sideLength = 0.2;
            var baseRectangle = Polygon.Rectangle(sideLength, sideLength);
            foreach (var p in points)
            {
                var rectangle = baseRectangle.TransformedPolygon(
                    new Transform(p - new Vector3(0, 0, sideLength / 2)));
                var extrude = new Extrude(rectangle, sideLength, Vector3.ZAxis, false);
                so.Add(extrude);
            }

            var mp = new ModelPoints(points)
            {
                Representation = new Representation(so),
                Material = new Material("Grid Key Points", new Color(0.6, 0.2, 0.8, 0.5)) //Dark Orchid
            };
            return mp;
        }
    }
}