//----------------------
// <auto-generated>
//     Generated using the NJsonSchema v10.1.21.0 (Newtonsoft.Json v11.0.0.0) (http://NJsonSchema.org)
// </auto-generated>
//----------------------
using Elements;
using Elements.GeoJSON;
using Elements.Geometry;
using Elements.Geometry.Solids;
using Elements.Validators;
using Elements.Serialization.JSON;
using System;
using System.Collections.Generic;
using System.Linq;
using Line = Elements.Geometry.Line;
using Polygon = Elements.Geometry.Polygon;

namespace Elements.Geometry
{
    #pragma warning disable // Disable all warnings

    /// <summary>A representation containing a collection of points.</summary>
    [System.CodeDom.Compiler.GeneratedCode("NJsonSchema", "10.1.21.0 (Newtonsoft.Json v11.0.0.0)")]
    public partial class PointsRepresentation : Representation
    {
        [Newtonsoft.Json.JsonConstructor]
        public PointsRepresentation(IList<Vector3> @points, Material @material, System.Guid @id, string @name)
            : base(material, id, name)
        {
            var validator = Validator.Instance.GetFirstValidatorForType<PointsRepresentation>();
            if(validator != null)
            {
                validator.PreConstruct(new object[]{ @points, @material, @id, @name});
            }
        
            this.Points = @points;
            
            if(validator != null)
            {
                validator.PostConstruct(this);
            }
        }
    
        /// <summary>A collection of Points.</summary>
        [Newtonsoft.Json.JsonProperty("Points", Required = Newtonsoft.Json.Required.Always)]
        [System.ComponentModel.DataAnnotations.Required]
        public IList<Vector3> Points { get; set; } = new List<Vector3>();
    
    
    }
}