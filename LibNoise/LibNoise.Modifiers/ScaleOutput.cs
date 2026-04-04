namespace LibNoise.Modifiers;

public class ScaleOutput : IModule
{
	public IModule SourceModule { get; set; }

	public double Scale { get; set; }

	public ScaleOutput(IModule sourceModule, double scale)
	{
        ArgumentNullException.ThrowIfNull(sourceModule);
        SourceModule = sourceModule;
		Scale = scale;
	}

	public double GetValue(double x, double y, double z)
	{
		if (SourceModule == null)
		{
			throw new NullReferenceException("A source module must be provided.");
		}
		return SourceModule.GetValue(x, y, z) * Scale;
	}
}
