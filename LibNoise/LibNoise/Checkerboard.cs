namespace LibNoise;

public class Checkerboard : IModule
{
	public double GetValue(double x, double y, double z)
	{
		int num = ((x > 0.0) ? ((int)x) : ((int)x - 1));
		int num2 = ((y > 0.0) ? ((int)y) : ((int)y - 1));
		int num3 = ((z > 0.0) ? ((int)z) : ((int)z - 1));
		int num4 = (num & 1) ^ (num2 & 1) ^ (num3 & 1);
		if (num4 > 0)
		{
			return -1.0;
		}
		return 1.0;
	}
}
