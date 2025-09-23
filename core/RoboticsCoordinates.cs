namespace Hai.PositionSystemToExternalProgram.Core;

public struct RoboticsCoordinates
{
    public float JoystickTargetL0;
    public float JoystickTargetL1;
    public float JoystickTargetL2;
    public float AngleDegR0;
    public float AngleDegR1;
    public float AngleDegR2;
    
    public float JoystickBoundedL0; // This ignores safe limits for use with devices that operate with some amplitude scalar. 
}