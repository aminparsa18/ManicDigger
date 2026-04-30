using OpenTK.Mathematics;

public interface ICameraService
{
    Vector3 Center { get; set; }

    void GetPosition(ref Vector3 ret);
    void Move(CameraMoveArgs camera_move, float p);
    void TurnLeft(float p);
    void TurnRight(float p);
    void TurnUp(float p);
}