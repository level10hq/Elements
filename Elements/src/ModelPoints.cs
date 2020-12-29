using System;
using System.Collections.Generic;
using Elements.Geometry;
using Newtonsoft.Json;

namespace Elements
{
    /// <summary>
    /// A collection of points which are visible in 3D.
    /// </summary>
    /// <example>
    /// [!code-csharp[Main](../../Elements/test/ModelPointsTests.cs?name=example)]
    /// </example>
    [UserElement]
    public class ModelPoints : GeometricElement
    {
        /// <summary>
        /// The locations of the points.
        /// </summary>
        [JsonIgnore]
        public IList<Vector3> Locations
        {
            get => FirstRepresentationOfType<PointsRepresentation>().Points;
        }

        /// <summary>
        /// Create a collection of points.
        /// </summary>
        /// <param name="locations">The locations of the points.</param>
        /// <param name="material">The material. Specular and glossiness components will be ignored.</param>
        /// <param name="transform">The model curve's transform.</param>
        /// <param name="isElementDefinition">Is this an element definition?</param>
        /// <param name="id">The id of the model curve.</param>
        /// <param name="name">The name of the model curve.</param>
        [JsonConstructor]
        public ModelPoints(IList<Vector3> locations = null,
                          Material material = null,
                          Transform transform = null,
                          bool isElementDefinition = false,
                          Guid id = default(Guid),
                          string name = null) : base(transform != null ? transform : new Transform(),
                                                     new[] { new PointsRepresentation(locations != null ? locations : new List<Vector3>(), material != null ? material : BuiltInMaterials.Points) },
                                                     isElementDefinition,
                                                     id != default(Guid) ? id : Guid.NewGuid(),
                                                     name)
        {
            var rep = FirstRepresentationOfType<PointsRepresentation>();
            rep.Points = this.Locations;
        }
    }
}