namespace Hai.PositionSystemToExternalProgram.Core;

public interface IUiActions
{
    void ConnectSerial(string portName);
    void DisconnectSerial();
    void ConnectIntiface(ushort portNumber);
    void DisconnectIntiface();
    bool IsSerialOpen();
    bool IsIntifaceOpen();
    TcodeData ExposeRawData();
    bool IsOpenVrRunning();
    bool IsUsingVrExtractor();
    ExtractionResult ExtractedData();
    bool[] Bits();
    ExtractionCoordinates VrCoordinates();
    ExtractionCoordinates WindowCoordinates();
    DecodedData Data();
    InterpretedLightData InterpretedData();
    float VirtualScale();
    Dictionary<string, string> FetchPortNames();
    void ConfigCoordinatesUpdated();
    void ConfigRoboticsUpdated();
    void ConfigWebsocketsUpdated();
}