//----------------------
// <auto-generated>
//     Generated using the NJsonSchema v10.0.27.0 (Newtonsoft.Json v12.0.0.0) (http://NJsonSchema.org)
// </auto-generated>
//----------------------
using Elements;
using Elements.GeoJSON;
using Elements.Geometry;
using Elements.Geometry.Solids;
using Elements.Properties;
using Elements.Validators;
using System;
using System.Collections.Generic;
using System.Linq;
using Line = Elements.Geometry.Line;
using Polygon = Elements.Geometry.Polygon;

namespace Elements
{
    #pragma warning disable // Disable all warnings

    /// <summary>A material.</summary>
    [System.CodeDom.Compiler.GeneratedCode("NJsonSchema", "10.0.27.0 (Newtonsoft.Json v12.0.0.0)")]
    public partial class Material : Element
    {
        [Newtonsoft.Json.JsonConstructor]
        public Material(Color @color, double @specularFactor, double @glossinessFactor, System.Guid @id, string @name)
            : base(id, name)
        {
            var validator = Validator.Instance.GetFirstValidatorForType<Material>();
            if(validator != null)
            {
                validator.Validate(new object[]{ @color, @specularFactor, @glossinessFactor, @id, @name});
            }
        
            this.Color = @color;
            this.SpecularFactor = @specularFactor;
            this.GlossinessFactor = @glossinessFactor;
        }
    
        /// <summary>The material's color.</summary>
        [Newtonsoft.Json.JsonProperty("Color", Required = Newtonsoft.Json.Required.Always)]
        [System.ComponentModel.DataAnnotations.Required]
        public Color Color { get; set; } = new Color();
    
        /// <summary>The specular factor between 0.0 and 1.0.</summary>
        [Newtonsoft.Json.JsonProperty("SpecularFactor", Required = Newtonsoft.Json.Required.Always)]
        [System.ComponentModel.DataAnnotations.Range(0, 1)]
        public double SpecularFactor { get; set; } = 0.1D;
    
        /// <summary>The glossiness factor between 0.0 and 1.0.</summary>
        [Newtonsoft.Json.JsonProperty("GlossinessFactor", Required = Newtonsoft.Json.Required.Always)]
        [System.ComponentModel.DataAnnotations.Range(0, 1)]
        public double GlossinessFactor { get; set; } = 0.1D;
    
    
    }
}