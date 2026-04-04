namespace LibNoise.Models;

public class Cylinder
{
	public IModule SourceModule { get; set; }

	public Cylinder(IModule sourceModule)
	{
        ArgumentNullException.ThrowIfNull(sourceModule);
        SourceModule = sourceModule;
	}

	public double GetValue(double angle, double height)
	{
		if (SourceModule == null)
		{
			throw new NullReferenceException("A source module must be provided.");
		}
		double x = System.Math.Cos(angle);
		double z = System.Math.Sin(angle);
		return SourceModule.GetValue(x, height, z);
	}
}
