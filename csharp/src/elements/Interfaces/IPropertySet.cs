using Hypar.Elements;
using System.Collections.Generic;

namespace Hypar.Interfaces
{
    public interface IProperty
    {
        /// <summary>
        /// The description of the property.
        /// </summary>
        string Description{get;}
    }

    public interface IPropertySingleValue<TValue> : IProperty
    {
        TValue Value {get;}
        UnitType UnitType{get;}
    }

    public interface IPropertySet
    {
        Dictionary<string, IProperty> Properties{get;}
    }
}