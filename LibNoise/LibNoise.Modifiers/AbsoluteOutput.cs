namespace LibNoise.Modifiers;

public class AbsoluteOutput : IModule
{
	public IModule SourceModule { get; set; }

	public AbsoluteOutput(IModule sourceModule)
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
		return System.Math.Abs(SourceModule.GetValue(x, y, z));
	}
}
