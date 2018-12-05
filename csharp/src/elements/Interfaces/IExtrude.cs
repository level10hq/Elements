namespace Hypar.Interfaces
{
    public interface IExtrude: IGeometry3D, IProfileProvider
    {
        double Thickness{get;}
    }
}