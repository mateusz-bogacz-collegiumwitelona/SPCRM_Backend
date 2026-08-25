namespace Api.Request.Contract
{
    public interface IProductDimensionsContract<TDiameter, TDimension>
    {
        string? Category { get; }
        TDiameter Diameter { get; }
        TDimension Width { get; }
        TDimension Thickness { get; }
    }
}
