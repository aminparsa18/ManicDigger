namespace LibNoise.Modifiers;

public class TranslateInput : IModule
{
	public double X { get; set; }

	public double Y { get; set; }

	public double Z { get; set; }

	public IModule SourceModule { get; set; }

	public TranslateInput(IModule sourceModule, double x, double y, double z)
	{
        ArgumentNullException.ThrowIfNull(sourceModule);
        SourceModule = sourceModule;
		X = x;
		Y = y;
		Z = z;
	}

	public double GetValue(double x, double y, double z)
	{
		if (SourceModule == null)
		{
			throw new NullReferenceException("A source module must be provided.");
		}
		return SourceModule.GetValue(x + X, y + Y, z + Z);
	}
}
