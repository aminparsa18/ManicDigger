namespace LibNoise.Models;

public class Sphere : Math
{
	public IModule SourceModule { get; set; }

	public Sphere(IModule sourceModule)
	{
        ArgumentNullException.ThrowIfNull(sourceModule);
        SourceModule = sourceModule;
	}

	public double GetValue(double latitude, double longitude)
	{
		if (SourceModule == null)
		{
			throw new NullReferenceException("A source module must be provided.");
		}
		double x = 0.0;
		double y = 0.0;
		double z = 0.0;
		LatLonToXYZ(latitude, longitude, ref x, ref y, ref z);
		return SourceModule.GetValue(x, y, z);
	}
}
