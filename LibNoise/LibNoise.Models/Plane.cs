namespace LibNoise.Models;

public class Plane
{
	public IModule SourceModule { get; set; }

	public Plane(IModule sourceModule)
	{
        ArgumentNullException.ThrowIfNull(sourceModule);
        SourceModule = sourceModule;
	}

	public double GetValue(double x, double z)
	{
		if (SourceModule == null)
		{
			throw new NullReferenceException("A source module must be provided.");
		}
		return SourceModule.GetValue(x, 0.0, z);
	}
}
