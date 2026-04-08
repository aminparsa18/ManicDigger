using OpenTK.Mathematics;

public partial class Game
{
    public void GLMatrixModeModelView()
    {
        currentMatrixModeProjection = false;
    }

    public void GLMatrixModeProjection()
    {
        currentMatrixModeProjection = true;
    }

    public void SetMatrixUniforms()
    {
        var p = pMatrix.Peek();
        var mv = mvMatrix.Peek();
        platform.SetMatrixUniformProjection(ref p);
        platform.SetMatrixUniformModelView(ref mv);
    }

    public void SetMatrixUniformProjection()
    {
        var p = pMatrix.Peek();
        platform.SetMatrixUniformProjection(ref p);
    }

    public void SetMatrixUniformModelView()
    {
        var mv = mvMatrix.Peek();
        platform.SetMatrixUniformModelView(ref mv);
    }

    public void GLLoadMatrix(Matrix4 m)
    {
        if (currentMatrixModeProjection)
        {
            if (pMatrix.Count > 0)
            {
                pMatrix.Pop();
            }
            pMatrix.Push(m);
        }
        else
        {
            if (mvMatrix.Count > 0)
            {
                mvMatrix.Pop();
            }
            mvMatrix.Push(m);
        }
    }

    public void GLPopMatrix()
    {
        if (currentMatrixModeProjection)
        {
            if (pMatrix.Count > 1)
            {
                pMatrix.Pop();
            }
        }
        else
        {
            if (mvMatrix.Count() > 1)
            {
                mvMatrix.Pop();
            }
        }
    }

    public void GLScale(float x, float y, float z)
    {
        Matrix4 m;
        if (currentMatrixModeProjection)
        {
            m = pMatrix.Peek();
            Matrix4.CreateScale(x, y, z, out Matrix4 scale);
            m = scale * m;
            pMatrix.Pop();
            pMatrix.Push(m);
        }
        else
        {
            m = mvMatrix.Peek();
            Matrix4.CreateScale(x, y, z, out Matrix4 scale);
            m = scale * m;
            mvMatrix.Pop();
            mvMatrix.Push(m);
        }
    }

    public void GLRotate(float angle, float x, float y, float z)
    {
        angle /= 360;
        angle *= 2 * MathF.PI;
        Matrix4.CreateFromAxisAngle(new Vector3(x, y, z), angle, out Matrix4 rotation);
        if (currentMatrixModeProjection)
        {
            var m = pMatrix.Peek();
            m = rotation * m;
            pMatrix.Pop();
            pMatrix.Push(m);
        }
        else
        {
            var m = mvMatrix.Peek();
            m = rotation * m;
            mvMatrix.Pop();
            mvMatrix.Push(m);
        }
    }

    public void GLTranslate(float x, float y, float z)
    {
        Matrix4.CreateTranslation(x, y, z, out Matrix4 translation);
        if (currentMatrixModeProjection)
        {
            var m = pMatrix.Peek();
            m = translation * m;
            pMatrix.Pop();
            pMatrix.Push(m);
        }
        else
        {
            var m = mvMatrix.Peek();
            m = translation * m;
            mvMatrix.Pop();
            mvMatrix.Push(m);
        }
    }

    public void GLPushMatrix()
    {
        if (currentMatrixModeProjection)
        {
            pMatrix.Push(pMatrix.Peek());
        }
        else
        {
            mvMatrix.Push(mvMatrix.Peek());
        }
    }

    public void GLLoadIdentity()
    {
        if (currentMatrixModeProjection)
        {
            if (pMatrix.Count() > 0)
            {
                pMatrix.Pop();
            }
            pMatrix.Push(identityMatrix);
        }
        else
        {
            if (mvMatrix.Count() > 0)
            {
                mvMatrix.Pop();
            }
            mvMatrix.Push(identityMatrix);
        }
    }

    public void GLOrtho(float left, float right, float bottom, float top, float zNear, float zFar)
    {
        if (currentMatrixModeProjection)
        {
            Matrix4.CreateOrthographicOffCenter(left, right, bottom, top, zNear, zFar, out Matrix4 ortho);
            pMatrix.Pop();
            pMatrix.Push(ortho);
        }
        else
        {
            platform.ThrowException("GLOrtho");
        }
    }

    public void OrthoMode(int width, int height)
    {
        //GL.Disable(EnableCap.DepthTest);
        GLMatrixModeProjection();
        GLPushMatrix();
        GLLoadIdentity();
        GLOrtho(0, width, height, 0, 0, 1);
        SetMatrixUniformProjection();

        GLMatrixModeModelView();
        GLPushMatrix();
        GLLoadIdentity();
        SetMatrixUniformModelView();
    }

    public void PerspectiveMode()
    {
        // Enter into our projection matrix mode
        GLMatrixModeProjection();
        // Pop off the last matrix pushed on when in projection mode (Get rid of ortho mode)
        GLPopMatrix();
        SetMatrixUniformProjection();

        // Go back to our model view matrix like normal
        GLMatrixModeModelView();
        GLPopMatrix();
        SetMatrixUniformModelView();
        //GL.LoadIdentity();
        //GL.Enable(EnableCap.DepthTest);
    }
}