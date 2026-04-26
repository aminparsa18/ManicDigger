namespace LibNoise.Modifiers;

public class Constant : IModule
{
	public double Value { get; set; }

	public double GetValue(double x, double y, double z)
	{
		return Value;
	}
}
