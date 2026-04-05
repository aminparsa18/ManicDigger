//glMatrix license:
//Copyright (c) 2013, Brandon Jones, Colin MacKenzie IV. All rights reserved.

//Redistribution and use in source and binary forms, with or without modification,
//are permitted provided that the following conditions are met:

//  * Redistributions of source code must retain the above copyright notice, this
//    list of conditions and the following disclaimer.
//  * Redistributions in binary form must reproduce the above copyright notice,
//    this list of conditions and the following disclaimer in the documentation 
//    and/or other materials provided with the distribution.

//THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND
//ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
//WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE 
//DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT HOLDER OR CONTRIBUTORS BE LIABLE FOR
//ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
//(INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
//LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON
//ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
//(INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
//SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.

public class Platform
{
    public static float Sqrt(float a)
    {
#if CS
        native
        {
            return (float)System.Math.Sqrt(a);
        }
        return 0;
#elif JS
        native
        {
            return Math.sqrt(a);
        }
        return 0;
#elif C
        native
        {
            return sqrt(a);
        }
        return 0;
#elif C99
        native
        {
            return sqrt(a);
        }
        return 0;
#elif PHP
        native
        {
            return sqrt("$a");
        }
        return 0;
#elif JAVA
        float ret = 0;
        native
        {
            ret = (float)Math.sqrt(a);
        }
        return ret;
#elif D
        float ret = 0;
        native
        {
            ret = std.math.sqrt(a);
        }
        return ret;
#elif AS
        float ret = 0;
        native
        {
            ret = Math.sqrt(a);
        }
        return ret;
#else
#if CITO
        return 0;
#else
        return (float)Math.Sqrt(a);
#endif
#endif
    }

    public static float Cos(float a)
    {
#if CS
        native
        {
            return (float)System.Math.Cos(a);
        }
        return 0;
#elif JS
        native
        {
            return Math.cos(a);
        }
        return 0;
#elif C
        native
        {
            return cos(a);
        }
        return 0;
#elif C99
        native
        {
            return cos(a);
        }
        return 0;
#elif PHP
        native
        {
            return cos("$a");
        }
        return 0;
#elif JAVA
        float ret = 0;
        native
        {
            ret = (float)Math.cos(a);
        }
        return ret;
#elif D
        float ret = 0;
        native
        {
            ret = std.math.cos(a);
        }
        return ret;
#elif AS
        float ret = 0;
        native
        {
            ret = Math.cos(a);
        }
        return ret;
#else
#if CITO
        return 0;
#else
        return (float)Math.Cos(a);
#endif
#endif
    }

    public static float Sin(float a)
    {
#if CS
        native
        {
            return (float)System.Math.Sin(a);
        }
        return 0;
#elif JS
        native
        {
            return Math.sin(a);
        }
        return 0;
#elif C
        native
        {
            return sin(a);
        }
        return 0;
#elif C99
        native
        {
            return sin(a);
        }
        return 0;
#elif PHP
        native
        {
            return sin("$a");
        }
        return 0;
#elif JAVA
        float ret = 0;
        native
        {
            ret = (float)Math.sin(a);
        }
        return ret;
#elif D
        float ret = 0;
        native
        {
            ret = std.math.sin(a);
        }
        return ret;
#elif AS
        float ret = 0;
        native
        {
            ret = Math.sin(a);
        }
        return ret;
#else
#if CITO
        return 0;
#else
        return (float)Math.Sin(a);
#endif
#endif
    }

    //public static float Random()
    //{
    //    return 0;
    //}

    public static float Tan(float a)
    {
#if CS
        native
        {
            return (float)System.Math.Tan(a);
        }
        return 0;
#elif JS
        native
        {
            return Math.tan(a);
        }
        return 0;
#elif C
        native
        {
            return tan(a);
        }
        return 0;
#elif C99
        native
        {
            return tan(a);
        }
        return 0;
#elif PHP
        native
        {
            return tan("$a");
        }
        return 0;
#elif JAVA
        float ret = 0;
        native
        {
            ret = (float)Math.tan(a);
        }
        return ret;
#elif D
        float ret = 0;
        native
        {
            ret = std.math.tan(a);
        }
        return ret;
#elif AS
        float ret = 0;
        native
        {
            ret = Math.tan(a);
        }
        return ret;
#else
#if CITO
        return 0;
#else
        return (float)Math.Tan(a);
#endif
#endif
    }

    public static float Acos(float a)
    {
#if CS
        native
        {
            return (float)System.Math.Acos(a);
        }
        return 0;
#elif JS
        native
        {
            return Math.acos(a);
        }
        return 0;
#elif C
        native
        {
            return acos(a);
        }
        return 0;
#elif C99
        native
        {
            return acos(a);
        }
        return 0;
#elif PHP
        native
        {
            return acos("$a");
        }
        return 0;
#elif JAVA
        float ret = 0;
        native
        {
            ret = (float)Math.acos(a);
        }
        return ret;
#elif D
        float ret = 0;
        native
        {
            ret = std.math.acos(a);
        }
        return ret;
#elif AS
        float ret = 0;
        native
        {
            ret = Math.acos(a);
        }
        return ret;
#else
#if CITO
        return 0;
#else
        return (float)Math.Acos(a);
#endif
#endif
    }

    public static void WriteString(string a)
    {
#if CS
        native
        {
            System.Console.Write(a);
        }
#elif JS
        native
        {
            console.log(a);
        }
#elif C
        native
        {
            printf("%s", a);
        }
#elif C99
        native
        {
            printf("%s", a);
        }
#elif PHP
        native
        {
            echo("$a");
        }
#elif JAVA
        native
        {
            System.out.println(a);
        }
#elif D
        native
        {
            std.stdio.write(a);
        }
#elif AS
        native
        {
            trace(a);
        }
#else
#if CITO
#else
        Console.Write(a);
#endif
#endif
    }

    public static void WriteInt(int a)
    {
#if CS
        native
        {
            System.Console.Write(a);
        }
#elif JS
        native
        {
            console.log(a);
        }
#elif C
        native
        {
            printf("%i", a);
        }
#elif C99
        native
        {
            printf("%i", a);
        }
#elif PHP
        native
        {
            echo("$a");
        }
#elif JAVA
        native
        {
            System.out.println(a);
        }
#elif D
        native
        {
            std.stdio.write(a);
        }
#elif AS
        native
        {
            trace(a);
        }
#else
#if CITO
#else
        Console.Write(a);
#endif
#endif
    }
}

public class GlMatrixMath
{
    public static float min(float a, float b)
    {
        if (a < b)
        {
            return a;
        }
        else
        {
            return b;
        }
    }

    public static float max(float a, float b)
    {
        if (a > b)
        {
            return a;
        }
        else
        {
            return b;
        }
    }

    public static float PI()
    {
        float a = 3141592;
        return a / 1000000;
    }

    public static float Abs(float len)
    {
        if (len < 0)
        {
            return -len;
        }
        else
        {
            return len;
        }
    }

    public static float GLMAT_EPSILON()
    {
        float one = 1;
        return one / 1000000;
    }
}

#if TESTS

public class Tests
{
    public static void RunAll()
    {
        TestVec3 testvec3 = new TestVec3();
        testvec3.Test();
        TestMat4 testmat4 = new TestMat4();
        testmat4.Test();
    }
}

public class TestVec3
{
    public void Test()
    {
        citoassert = new CitoAssert();
        ResetTests();
        TransformMat4(); ResetTests();
        Create(); ResetTests();
        CloneIt(); ResetTests();
        FromValues(); ResetTests();
        Copy(); ResetTests();
        Set(); ResetTests();
        Add(); ResetTests();
        Subtract(); ResetTests();
        Multiply(); ResetTests();
        Divide(); ResetTests();
        Min(); ResetTests();
        Max(); ResetTests();
        Scale(); ResetTests();
        ScaleAndAdd(); ResetTests();
        Distance(); ResetTests();
        SquaredDistance(); ResetTests();
        Length_(); ResetTests();
        SquaredLength(); ResetTests();
        Negate(); ResetTests();
        Normalize(); ResetTests();
        Dot(); ResetTests();
        Cross(); ResetTests();
        Lerp(); ResetTests();
        //Random(); ResetTests();
        ForEachDo(); ResetTests();
        Str(); ResetTests();
    }

    void ResetTests()
    {
        vecA = Arr3(1, 2, 3);
        vecB = Arr3(4, 5, 6);
        output = Arr3(0, 0, 0);
    }

    float[] vecA;
    float[] vecB;
    float[] output;

    void TransformMat4()
    {
        TransformMat4WithAnIdentity();
        TransformMat4WithALookAt();
        TransformMat3WithAnIdentity();
        TransformMat3With90DegAboutX();
        TransformMat3With90DegAboutY();
        TransformMat3With90DegAboutZ();
        TransformMat3WithALookAtNormalMatrix();
    }

    void TransformMat4WithAnIdentity()
    {
        float[] matr = Arr16(1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1);
        float[] result = Vec3.TransformMat4(output, vecA, matr);
        AssertArrayEqual(output, Arr3(1, 2, 3), 3, "TransformMat4WithAnIdentity should produce the input");
        AssertArrayEqual(result, output, 3, "TransformMat4WithAnIdentity should return output");
    }

    void TransformMat4WithALookAt()
    {
        float[] matr = Mat4.LookAt(Mat4.Create(), Arr3(5, 6, 7), Arr3(2, 6, 7), Arr3(0, 1, 0));
        float[] result = Vec3.TransformMat4(output, vecA, matr);
        AssertArrayEqual(output, Arr3(4, -4, -4), 3, "TransformMat4WithALookAt should rotate and translate the input");
        AssertArrayEqual(result, output, 3, "TransformMat4WithALookAt should return out");
    }

    void TransformMat3WithAnIdentity()
    {
        float[] matr = Arr9(1, 0, 0, 0, 1, 0, 0, 0, 1);
        float[] result = Vec3.TransformMat3(output, vecA, matr);
        AssertArrayEqual(output, Arr3(1, 2, 3), 3, "TransformMat3WithAnIdentity should produce the input");
        AssertArrayEqual(result, output, 3, "TransformMat3WithAnIdentity should return output");
    }

    void TransformMat3With90DegAboutX()
    {
        float[] result = Vec3.TransformMat3(output, Arr3(0, 1, 0), Arr9(1, 0, 0, 0, 0, 1, 0, -1, 0));
        AssertArrayEqual(output, Arr3(0, 0, 1), 3, "TransformMat3With90DegAboutX should produce correct output");
    }

    void TransformMat3With90DegAboutY()
    {
        float[] result = Vec3.TransformMat3(output, Arr3(1, 0, 0), Arr9(0, 0, -1, 0, 1, 0, 1, 0, 0));
        AssertArrayEqual(output, Arr3(0, 0, -1), 3, "TransformMat3With90DegAboutU should produce correct output");
    }

    void TransformMat3With90DegAboutZ()
    {
        float[] result = Vec3.TransformMat3(output, Arr3(1, 0, 0), Arr9(0, 1, 0, -1, 0, 0, 0, 0, 1));
        AssertArrayEqual(output, Arr3(0, 1, 0), 3, "TransformMat3With90DegAboutZ should produce correct output");
    }

    void TransformMat3WithALookAtNormalMatrix()
    {
        float[] matr = Mat4.LookAt(Mat4.Create(), Arr3(5, 6, 7), Arr3(2, 6, 7), Arr3(0, 1, 0));
        float[] n = Mat3.Create();
        matr = Mat3.Transpose(n, Mat3.Invert(n, Mat3.FromMat4(n, matr)));
        float[] result = Vec3.TransformMat3(output, Arr3(1, 0, 0), matr);

        AssertArrayEqual(output, Arr3(0, 0, 1), 3, "TransformMat3WithALookAtNormalMatrix should rotate the input");
        AssertArrayEqual(result, output, 3, "TransformMat3WithALookAtNormalMatrix should return output");
    }

    void Create()
    {
        float[] result = Vec3.Create();
        AssertArrayEqual(result, Arr3(0, 0, 0), 3, "Create should return a 3 element array initialized to 0s");
    }

    void CloneIt()
    {
        float[] result = Vec3.CloneIt(vecA);
        AssertArrayEqual(result, vecA, 3, "Clone should return a 3 element array initialized to the values in vecA");
    }

    void FromValues()
    {
        float[] result = Vec3.FromValues(1, 2, 3);
        AssertArrayEqual(result, Arr3(1, 2, 3), 3, "FromValues should return a 3 element array initialized to the values passed");
    }

    void Copy()
    {
        float[] result = Vec3.Copy(output, vecA);
        AssertArrayEqual(output, Arr3(1, 2, 3), 3, "Copy should place values into out");
        AssertArrayEqual(result, output, 3, "Copy should return output");
    }

    void Set()
    {
        float[] result = Vec3.Set(output, 1, 2, 3);
        AssertArrayEqual(output, Arr3(1, 2, 3), 3, "Set should place values into output");
        AssertArrayEqual(result, output, 3, "Set should return output");
    }

    void Add()
    {
        AddWithASeparateOutputVector();
        AddWhenVecAIsTheOutputVector();
        AddWhenVecBIsTheOutputVector();
    }

    void AddWithASeparateOutputVector()
    {
        float[] result = Vec3.Add(output, vecA, vecB);
        AssertArrayEqual(output, Arr3(5, 7, 9), 3, "Add should place values into out");
        AssertArrayEqual(result, output, 3, "Add should return out");
        AssertArrayEqual(vecA, Arr3(1, 2, 3), 3, "Add should not modify vecA");
        AssertArrayEqual(vecB, Arr3(4, 5, 6), 3, "Add should not modify vecB");
    }

    void AddWhenVecAIsTheOutputVector()
    {
    }

    void AddWhenVecBIsTheOutputVector()
    {
    }

    void Subtract()
    {
        SubtractShouldHaveAnAliasCalledSub();
        SubtractWithASeparateOutputVector();
        SubtractWhenVecAIsTheOutputVector();
        SubtractWhenVecBIsTheOutputVector();
    }

    void SubtractShouldHaveAnAliasCalledSub()
    {
    }

    void SubtractWithASeparateOutputVector()
    {
    }

    void SubtractWhenVecAIsTheOutputVector()
    {
    }

    void SubtractWhenVecBIsTheOutputVector()
    {
    }

    void Multiply()
    {
        MultiplyWithASeparateOutputVector();
        MultiplyWhenVecAIsTheOutputVector();
        MultiplyWhenVecBIsTheOutputVector();
    }

    void MultiplyWithASeparateOutputVector()
    {
    }

    void MultiplyWhenVecAIsTheOutputVector()
    {
    }

    void MultiplyWhenVecBIsTheOutputVector()
    {
    }

    void Divide()
    {
        DivideWithASeparateOutputVector();
        DivideWhenVecAIsTheOutputVector();
        DivideWhenVecBIsTheOutputVector();
    }

    void DivideWithASeparateOutputVector()
    {
    }

    void DivideWhenVecAIsTheOutputVector()
    {
    }

    void DivideWhenVecBIsTheOutputVector()
    {
    }

    void Min()
    {
        MinWithASeparateOutputVector();
        MinWhenVecAIsTheOutputVector();
        MinWhenVecBIsTheOutputVector();
    }

    void MinWithASeparateOutputVector()
    {
    }

    void MinWhenVecAIsTheOutputVector()
    {
    }

    void MinWhenVecBIsTheOutputVector()
    {
    }

    void Max()
    {
        MaxWithASeparateOutputVector();
        MaxWhenVecAIsTheOutputVector();
        MaxWhenVecBIsTheOutputVector();
    }

    void MaxWithASeparateOutputVector()
    {
    }

    void MaxWhenVecAIsTheOutputVector()
    {
    }

    void MaxWhenVecBIsTheOutputVector()
    {
    }

    void Scale()
    {
        ScaleWithASeparateOutputVector();
        ScaleWhenVecAIsTheOutputVector();
    }

    void ScaleWithASeparateOutputVector()
    {
    }

    void ScaleWhenVecAIsTheOutputVector()
    {
    }

    void ScaleAndAdd()
    {
        ScaleAndAddWithASeparateOutputVector();
        ScaleAndAddWhenVecAIsTheOutputVector();
        ScaleAndAddWhenVecBIsTheOutputVector();
    }

    void ScaleAndAddWithASeparateOutputVector()
    {
    }

    void ScaleAndAddWhenVecAIsTheOutputVector()
    {
    }

    void ScaleAndAddWhenVecBIsTheOutputVector()
    {
    }

    void Distance()
    {
        float result = Vec3.Distance(vecA, vecB);
        float r = 5196152;
        r /= 1000 * 1000; // 5.196152
        AssertCloseTo(result, r, "Distance should return the distance");
    }

    void SquaredDistance()
    {
        float result = Vec3.SquaredDistance(vecA, vecB);
        AssertEqual(result, 27, "SquaredDistance should return the squared distance");
    }

    void Length_()
    {
        float result = Vec3.Length_(vecA);
        float r = 3741657;
        r /= 1000 * 1000;// 3.741657
        AssertCloseTo(result, r, "Length should return the length");
    }

    void SquaredLength()
    {
        float result = Vec3.SquaredLength(vecA);
        AssertEqual(result, 14, "SquaredLength should return the squared length");
    }

    void Negate()
    {
        NegateWithASeparateOutputVector();
        NegateWhenVecAIsTheOutputVector();
    }

    void NegateWithASeparateOutputVector()
    {
        float[] result = Vec3.Negate(output, vecA);
        AssertArrayEqual(output, Arr3(-1, -2, -3), 3, "NegateWithASeparateOutputVector should place values into out");
        AssertArrayEqual(result, output, 3, "NegateWithASeparateOutputVector should should return out");
        AssertArrayEqual(vecA, Arr3(1, 2, 3), 3, "NegateWithASeparateOutputVector should not modify vecA");
    }

    void NegateWhenVecAIsTheOutputVector()
    {
        float[] result = Vec3.Negate(vecA, vecA);
        AssertArrayEqual(vecA, Arr3(-1, -2, -3), 3, "NegateWhenVecAIsTheOutputVector should place values into vecA");
        AssertArrayEqual(result, vecA, 3, "NegateWhenVecAIsTheOutputVector should return vecA");
    }

    void Normalize()
    {
        NormalizeWithASeparateOutputVector();
        NormalizeWhenVecAIsTheOutputVector();
    }

    void NormalizeWithASeparateOutputVector()
    {
        vecA = Arr3(5, 0, 0);
        float[] result = Vec3.Normalize(output, vecA);
        AssertArrayEqual(output, Arr3(1, 0, 0), 3, "NormalizeWithASeparateOutputVector should place values into out");
        AssertArrayEqual(result, output, 3, "NormalizeWithASeparateOutputVector should return out");
        AssertArrayEqual(vecA, Arr3(5, 0, 0), 3, "NormalizeWithASeparateOutputVector should not modify vecA");
    }

    void NormalizeWhenVecAIsTheOutputVector()
    {
        float[] vecA1 = Arr3(5, 0, 0);
        float[] result = Vec3.Normalize(vecA, vecA);
        AssertArrayEqual(vecA, Arr3(1, 0, 0), 3, "NormalizeWhenVecAIsTheOutputVector should place values into vecA");
        AssertArrayEqual(result, vecA, 3, "NormalizeWhenVecAIsTheOutputVector should return vecA");
    }

    void Dot()
    {
        float result = Vec3.Dot(vecA, vecB);
        AssertEqual(result, 32, "Dot should return the dot product");
        AssertArrayEqual(vecA, Arr3(1, 2, 3), 3, "Dot should not modify vecA");
        AssertArrayEqual(vecB, Arr3(4, 5, 6), 3, "Dot should not modify vecB");
    }

    void Cross()
    {
        CrossWithASeparateOutputVector();
        CrossWhenVecAIsTheOutputVector();
        CrossWhenVecBIsTheOutputVector();
    }

    void CrossWithASeparateOutputVector()
    {
    }

    void CrossWhenVecAIsTheOutputVector()
    {
    }

    void CrossWhenVecBIsTheOutputVector()
    {
    }

    void Lerp()
    {
        LerpWithASeparateOutputVector();
        LerpWhenVecAIsTheOutputVector();
        LerpWhenVecBIsTheOutputVector();
    }

    void LerpWithASeparateOutputVector()
    {
    }

    void LerpWhenVecAIsTheOutputVector()
    {
    }

    void LerpWhenVecBIsTheOutputVector()
    {
    }

    //void Random()
    //{
    //}

    void ForEachDo()
    {
    }

    void Str()
    {
    }

    void AssertEqual(float actual, float expected, string msg)
    {
        citoassert.AssertEqual(actual, expected, msg);
    }

    void AssertCloseTo(float actual, float expected, string msg)
    {
        citoassert.AssertCloseTo(actual, expected, msg);
    }

    void AssertArrayEqual(float[] actual, float[] expected, int length, string msg)
    {
        citoassert.AssertArrayEqual(actual, expected, length, msg);
    }

    float[] Arr3(float p, float p_2, float p_3)
    {
        return citoassert.Arr3(p, p_2, p_3);
    }

    float[] Arr9(int p, int p_2, int p_3, int p_4, int p_5, int p_6, int p_7, int p_8, int p_9)
    {
        return citoassert.Arr9(p, p_2, p_3, p_4, p_5, p_6, p_7, p_8, p_9);
    }

    float[] Arr16(int p, int p_2, int p_3, int p_4, int p_5, int p_6, int p_7, int p_8, int p_9, int p_10, int p_11, int p_12, int p_13, int p_14, int p_15, int p_16)
    {
        return citoassert.Arr16(p, p_2, p_3, p_4, p_5, p_6, p_7, p_8, p_9, p_10, p_11, p_12, p_13, p_14, p_15, p_16);
    }

    CitoAssert citoassert;
}

public class TestMat4
{
    public void Test()
    {
        citoassert = new CitoAssert();
        ResetTests();
        Create(); ResetTests();
        CloneIt(); ResetTests();
        Copy(); ResetTests();
        Identity_(); ResetTests();
        Transpose(); ResetTests();
        Invert(); ResetTests();
        Adjoint(); ResetTests();
        Determinant(); ResetTests();
        Multiply(); ResetTests();
        Translate(); ResetTests();
        Scale(); ResetTests();
        Rotate(); ResetTests();
        RotateX(); ResetTests();
        RotateY(); ResetTests();
        RotateZ(); ResetTests();
        Frustum(); ResetTests();
        Perspective(); ResetTests();
        Ortho(); ResetTests();
        LookAt(); ResetTests();
        Str(); ResetTests();
    }

    CitoAssert citoassert;
    float[] matA;
    float[] matB;
    float[] output;
    float[] identity;

    void ResetTests()
    {
        // Attempting to portray a semi-realistic transform matrix
        matA = Arr16(1, 0, 0, 0,
                0, 1, 0, 0,
                0, 0, 1, 0,
                1, 2, 3, 1);
        matB = Arr16(1, 0, 0, 0,
                0, 1, 0, 0,
                0, 0, 1, 0,
                4, 5, 6, 1);

        output = Arr16(0, 0, 0, 0,
                0, 0, 0, 0,
                0, 0, 0, 0,
                0, 0, 0, 0);

        identity = Arr16(1, 0, 0, 0,
                    0, 1, 0, 0,
                    0, 0, 1, 0,
                    0, 0, 0, 1);
    }

    void Create()
    {
        float[] result = Mat4.Create();
        AssertArrayEqual(result, identity, 16, "Create should return a 16 element array initialized to a 4x4 identity matrix");
    }

    void CloneIt()
    {
        float[] result = Mat4.CloneIt(matA);
        AssertArrayEqual(result, matA, 16, "Clone should return a 16 element array initialized to the values in matA");
    }

    void Copy()
    {
        float[] result = Mat4.Copy(output, matA);
        AssertArrayEqual(output, matA, 16, "Copy should place values into out");
        AssertArrayEqual(result, output, 16, "Copy should return out");
    }

    void Identity_()
    {
        float[] result = Mat4.Identity_(output);
        AssertArrayEqual(output, identity, 16, "Copy should place values into out");
        AssertArrayEqual(result, output, 16, "Copy should return out");
    }

    void Transpose()
    {
        TransposeWithASeparateOutputMatrix();
        TransposeWhenMatAIsTheOutputMatrix();
    }

    void TransposeWithASeparateOutputMatrix()
    {
    }

    void TransposeWhenMatAIsTheOutputMatrix()
    {
    }

    void Invert()
    {
        InvertWithASeparateOutputMatrix();
        InvertWhenMatAIsTheOutputMatrix();
    }

    void InvertWithASeparateOutputMatrix()
    {

    }

    void InvertWhenMatAIsTheOutputMatrix()
    {

    }

    void Adjoint()
    {
        AdjointWithASeparateOutputMatrix();
        AdjointWhenMatAIsTheOutputMatrix();
    }

    void AdjointWithASeparateOutputMatrix()
    {

    }

    void AdjointWhenMatAIsTheOutputMatrix()
    {

    }

    void Determinant()
    {

    }

    void Multiply()
    {
        MultiplyWithASeparateOutputMatrix();
        MultiplyWhenMatAIsTheOutputMatrix();
        MultiplyWhenMatBIsTheOutputMatrix();
    }

    void MultiplyWithASeparateOutputMatrix()
    {

    }

    void MultiplyWhenMatAIsTheOutputMatrix()
    {

    }

    void MultiplyWhenMatBIsTheOutputMatrix()
    {

    }

    void Translate()
    {
        TranslateWithASeparateOutputMatrix();
        TranslateWhenMatAIsTheOutputMatrix();
    }

    void TranslateWithASeparateOutputMatrix()
    {

    }

    void TranslateWhenMatAIsTheOutputMatrix()
    {

    }

    void Scale()
    {
        ScaleWithASeparateOutputMatrix();
        ScaleWhenMatAIsTheOutputMatrix();
    }

    void ScaleWithASeparateOutputMatrix()
    {

    }

    void ScaleWhenMatAIsTheOutputMatrix()
    {

    }

    void Rotate()
    {
        RotateWithASeparateOutputMatrix();
        RotateWhenMatAIsTheOutputMatrix();
    }

    void RotateWithASeparateOutputMatrix()
    {

    }

    void RotateWhenMatAIsTheOutputMatrix()
    {

    }

    void RotateX()
    {
        RotateXWithASeparateOutputMatrix();
        RotateXWhenMatAIsTheOutputMatrix();
    }

    void RotateXWithASeparateOutputMatrix()
    {

    }

    void RotateXWhenMatAIsTheOutputMatrix()
    {

    }

    void RotateY()
    {
        RotateYWithASeparateOutputMatrix();
        RotateYWhenMatAIsTheOutputMatrix();
    }

    void RotateYWithASeparateOutputMatrix()
    {

    }

    void RotateYWhenMatAIsTheOutputMatrix()
    {

    }

    void RotateZ()
    {
        RotateZWithASeparateOutputMatrix();
        RotateZWhenMatAIsTheOutputMatrix();
    }

    void RotateZWithASeparateOutputMatrix()
    {

    }

    void RotateZWhenMatAIsTheOutputMatrix()
    {

    }

    void Frustum()
    {
        float[] result = Mat4.Frustum(output, -1, 1, -1, 1, -1, 1);
        AssertArrayEqual(result, Arr16(-1, 0, 0, 0,
                0, -1, 0, 0,
                0, 0, 0, -1,
                0, 0, 1, 0), 16, "Frustum should place values into out");
        AssertArrayEqual(result, output, 16, "Frustum should return out");
    }

    void Perspective()
    {
        Perspective1();
        PerspectiveWithNonzeroNear45degFovyAndRealisticAspectRatio();
    }

    void Perspective1()
    {

    }

    void PerspectiveWithNonzeroNear45degFovyAndRealisticAspectRatio()
    {

    }

    void Ortho()
    {
        float[] result = Mat4.Ortho(output, -1, 1, -1, 1, -1, 1);
        AssertArrayEqual(result, Arr16(1, 0, 0, 0,
                0, 1, 0, 0,
                0, 0, -1, 0,
                0, 0, 0, 1), 16, "Ortho should place values into out");
        AssertArrayEqual(result, output, 16, "Ortho should return out");
    }

    void LookAt()
    {
        eye = Arr3(0, 0, 1);
        center = Arr3(0, 0, -1);
        up = Arr3(0, 1, 0);

        LookAtLookingDown();
        LookAt74();
        LookAt3();
    }

    float[] eye;
    float[] center;
    float[] up;
    float[] view;
    float[] right;

    void LookAtLookingDown()
    {
        view = Arr3(0, -1, 0);
        up = Arr3(0, 0, -1);
        right = Arr3(1, 0, 0);

        float[] result = Mat4.LookAt(output, Arr3(0, 0, 0), view, up);

        result = Vec3.TransformMat4(Vec3.Create(), view, output);
        AssertArrayEqual(result, Arr3(0, 0, -1), 3, "LookAtLookingDown should transform view into local -Z");

        result = Vec3.TransformMat4(Vec3.Create(), up, output);
        AssertArrayEqual(result, Arr3(0, 1, 0), 3, "LookAtLookingDownshould transform up into local +Y");

        result = Vec3.TransformMat4(Vec3.Create(), right, output);
        AssertArrayEqual(result, Arr3(1, 0, 0), 3, "LookAtLookingDownshould transform right into local +X");

        AssertArrayEqual(result, output, 3, "LookAtLookingDown should return out");
    }

    void LookAt74()
    {
        float six = 6;
        Mat4.LookAt(output, Arr3(0, 2, 0), Arr3(0, six / 10, 0), Arr3(0, 0, -1));

        float[] result = Vec3.TransformMat4(Vec3.Create(), Arr3(0, 2, -1), output);
        AssertArrayEqual(result, Arr3(0, 1, 0), 3, "LookAt74 should transform a point 'above' into local +Y");

        result = Vec3.TransformMat4(Vec3.Create(), Arr3(1, 2, 0), output);
        AssertArrayEqual(result, Arr3(1, 0, 0), 3, "LookAt74 should transform a point 'right of' into local +X");

        result = Vec3.TransformMat4(Vec3.Create(), Arr3(0, 1, 0), output);
        AssertArrayEqual(result, Arr3(0, 0, -1), 3, "LookAt74 should transform a point 'in front of' into local -Z");
    }

    void LookAt3()
    {

    }

    void Str()
    {

    }


    void AssertEqual(float actual, float expected, string msg)
    {
        citoassert.AssertEqual(actual, expected, msg);
    }

    void AssertCloseTo(float actual, float expected, string msg)
    {
        citoassert.AssertCloseTo(actual, expected, msg);
    }

    void AssertArrayEqual(float[] actual, float[] expected, int length, string msg)
    {
        citoassert.AssertArrayEqual(actual, expected, length, msg);
    }

    float[] Arr3(float p, float p_2, float p_3)
    {
        float[] arr = citoassert.Arr3(p, p_2, p_3);
        arr[0] = arr[0]; // fix for a problem with Cito D generator
        return arr;
    }

    float[] Arr9(int p, int p_2, int p_3, int p_4, int p_5, int p_6, int p_7, int p_8, int p_9)
    {
        return citoassert.Arr9(p, p_2, p_3, p_4, p_5, p_6, p_7, p_8, p_9);
    }

    float[] Arr16(int p, int p_2, int p_3, int p_4, int p_5, int p_6, int p_7, int p_8, int p_9, int p_10, int p_11, int p_12, int p_13, int p_14, int p_15, int p_16)
    {
        float[] arr = citoassert.Arr16(p, p_2, p_3, p_4, p_5, p_6, p_7, p_8, p_9, p_10, p_11, p_12, p_13, p_14, p_15, p_16);
        arr[0] = arr[0]; // fix for a problem with Cito D generator
        return arr;
    }
}

public class CitoAssert
{
    public CitoAssert()
    {
        errors = new string[1024];
        errorsCount = 0;
        testI = 0;
    }

    string[] errors;
    int errorsCount;

    int testI;

    public void AssertEqual(float actual, float expected, string msg)
    {
        Platform.WriteString("Test ");
        Platform.WriteInt(testI);
        if (actual != expected)
        {
            errors[errorsCount++] = msg;
            Platform.WriteString(" error: ");
            Platform.WriteString(msg);
        }
        else
        {
            Platform.WriteString(" ok");
        }
        Platform.WriteString("\n");
        testI++;
    }

    public void AssertCloseTo(float actual, float expected, string msg)
    {
        Platform.WriteString("Test ");
        Platform.WriteInt(testI);
        if (GlMatrixMath.Abs(actual - expected) > GlMatrixMath.GLMAT_EPSILON())
        {
            errors[errorsCount++] = msg;
            Platform.WriteString(" error: ");
            Platform.WriteString(msg);
        }
        else
        {
            Platform.WriteString(" ok");
        }
        Platform.WriteString("\n");
        testI++;
    }

    public void AssertArrayEqual(float[] actual, float[] expected, int length, string msg)
    {
        Platform.WriteString("Test ");
        Platform.WriteInt(testI);
        bool isequal = true;
        for (int i = 0; i < length; i++)
        {
            if (actual[i] != expected[i])
            {
                isequal = false;
            }
        }
        if (!isequal)
        {
            errors[errorsCount++] = msg;
            Platform.WriteString(" error: ");
            Platform.WriteString(msg);
        }
        else
        {
            Platform.WriteString(" ok");
        }
        Platform.WriteString("\n");
        testI++;
    }

    public float[] Arr3(float p, float p_2, float p_3)
    {
        float[] arr = new float[3];
        arr[0] = p;
        arr[1] = p_2;
        arr[2] = p_3;
        return arr;
    }

    public float[] Arr9(int p, int p_2, int p_3, int p_4, int p_5, int p_6, int p_7, int p_8, int p_9)
    {
        float[] arr = new float[16];
        arr[0] = p;
        arr[1] = p_2;
        arr[2] = p_3;
        arr[3] = p_4;
        arr[4] = p_5;
        arr[5] = p_6;
        arr[6] = p_7;
        arr[7] = p_8;
        arr[8] = p_9;
        return arr;
    }

    public float[] Arr16(int p, int p_2, int p_3, int p_4, int p_5, int p_6, int p_7, int p_8, int p_9, int p_10, int p_11, int p_12, int p_13, int p_14, int p_15, int p_16)
    {
        float[] arr = new float[16];
        arr[0] = p;
        arr[1] = p_2;
        arr[2] = p_3;
        arr[3] = p_4;
        arr[4] = p_5;
        arr[5] = p_6;
        arr[6] = p_7;
        arr[7] = p_8;
        arr[8] = p_9;
        arr[9] = p_10;
        arr[10] = p_11;
        arr[11] = p_12;
        arr[12] = p_13;
        arr[13] = p_14;
        arr[14] = p_15;
        arr[15] = p_16;
        return arr;
    }
}
#endif
