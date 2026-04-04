namespace LibNoise.Modifiers;

public class InvertInput : IModule
{
	public IModule SourceModule { get; set; }

	public InvertInput(IModule sourceModule)
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
		return SourceModule.GetValue(0.0 - x, 0.0 - y, 0.0 - z);
	}
}
