namespace LibNoise.Modfiers;

public class ExponentialOutput : IModule
{
	public IModule SourceModule { get; set; }

	public double Exponent { get; set; }

	public ExponentialOutput(IModule sourceModule, double exponent)
	{
        ArgumentNullException.ThrowIfNull(sourceModule);
        SourceModule = sourceModule;
		Exponent = exponent;
	}

	public double GetValue(double x, double y, double z)
	{
		if (SourceModule == null)
		{
			throw new NullReferenceException("A source module must be provided.");
		}
		return System.Math.Pow(System.Math.Abs((SourceModule.GetValue(x, y, z) + 1.0) / 2.0), Exponent) * 2.0 - 1.0;
	}
}
