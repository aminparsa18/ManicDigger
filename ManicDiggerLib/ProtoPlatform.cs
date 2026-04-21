using System.Text;

public class ProtoPlatform
{
    public static byte[] StringToBytes(string s)
    {
        return Encoding.UTF8.GetBytes(s);
    }

    public static string BytesToString(byte[] bytes, int length)
    {
        string s;
        return Encoding.UTF8.GetString(bytes);
    }

    public static int ArrayLength(byte[] a)
    {
        int len;
#if CITO
#if CS
        native
        {
            len = a.Length;
        }
#elif JAVA
        native
        {
            len = a.length;
        }
#elif JS
        native
        {
            len = a.length;
        }
#else
        len = 0;
#endif
#else
        len = a.Length;
#endif
        return len;
    }

    public static byte IntToByte(int a)
    {
#if CITO
        return a.LowByte;
#else
        return (byte)a;
#endif
    }

    //http://stackoverflow.com/a/8248336
    public static int logical_right_shift(int x, int n)
    {
        int mask = ~(-1 << n) << (32 - n);
        return ~mask & ((x >> n) | mask);
    }
}