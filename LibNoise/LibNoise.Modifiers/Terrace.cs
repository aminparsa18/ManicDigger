namespace LibNoise.Modifiers;

public class Terrace : Math, IModule
{
	public List<double> ControlPoints = new List<double>();

	public IModule SourceModule { get; set; }

	public bool InvertTerraces { get; set; }

	public Terrace(IModule sourceModule)
	{
        ArgumentNullException.ThrowIfNull(sourceModule);
        SourceModule = sourceModule;
		InvertTerraces = false;
	}

	public double GetValue(double x, double y, double z)
	{
		if (SourceModule == null)
		{
			throw new NullReferenceException("A source module must be provided.");
		}
		if (ControlPoints.Count < 2)
		{
			throw new Exception("Two or more control points must be specified.");
		}
		double value = SourceModule.GetValue(x, y, z);
		int count = ControlPoints.Count;
		int i;
		for (i = 0; i < count && !(value < ControlPoints[i]); i++)
		{
		}
		int num = ClampValue(i - 1, 0, count - 1);
		int num2 = ClampValue(i, 0, count - 1);
		if (num == num2)
		{
			return ControlPoints[num2];
		}
		double a = ControlPoints[num];
		double b = ControlPoints[num2];
		double num3 = (value - a) / (b - a);
		if (InvertTerraces)
		{
			num3 = 1.0 - num3;
            SwapValues(ref a, ref b);
		}
		num3 *= num3;
		return LinearInterpolate(a, b, num3);
	}
}
