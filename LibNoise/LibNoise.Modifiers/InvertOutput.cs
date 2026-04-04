namespace LibNoise.Modifiers;

public class InvertOutput : IModule
{
	public IModule SourceModule { get; set; }

	public InvertOutput(IModule sourceModule)
	{
        ArgumentNullException.ThrowIfNull(sourceModule);
        SourceModule = sourceModule;
	}

	public double GetValue(double x, double y, double z)
	{
		if (SourceModule == null)
		{
			throw new NullReferenceException("A source module must be provided.");
		}
		return 0.0 - SourceModule.GetValue(x, y, z);
	}
}
