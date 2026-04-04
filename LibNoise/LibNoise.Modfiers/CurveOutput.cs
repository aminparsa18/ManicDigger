namespace LibNoise.Modfiers;

public class CurveOutput : Math, IModule
{
	public List<CurveControlPoint> ControlPoints = new List<CurveControlPoint>();

	public IModule SourceModule { get; set; }

	public CurveOutput(IModule sourceModule)
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
		if (ControlPoints.Count < 4)
		{
			throw new Exception("Four or more control points must be specified.");
		}
		double value = SourceModule.GetValue(x, y, z);
		int count = ControlPoints.Count;
		int i;
		for (i = 0; i < count && !(value < ControlPoints[i].Input); i++)
		{
		}
		int index = ClampValue(i - 2, 0, count - 1);
		int num = ClampValue(i - 1, 0, count - 1);
		int num2 = ClampValue(i, 0, count - 1);
		int index2 = ClampValue(i + 1, 0, count - 1);
		if (num == num2)
		{
			return ControlPoints[num].Output;
		}
		double input = ControlPoints[num].Input;
		double input2 = ControlPoints[num2].Input;
		double a = (value - input) / (input2 - input);
		return CubicInterpolate(ControlPoints[index].Output, ControlPoints[num].Output, ControlPoints[num2].Output, ControlPoints[index2].Output, a);
	}
}
