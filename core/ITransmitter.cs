namespace Hai.PositionSystemToExternalProgram.Core;

public interface ITransmitter
{
    void ProvideNewTarget(RoboticsCoordinates roboticsCoordinates);
    Task Update(float deltaTimeMs);
    
    bool IsOpen();
    Task Open();
    Task Close();
}