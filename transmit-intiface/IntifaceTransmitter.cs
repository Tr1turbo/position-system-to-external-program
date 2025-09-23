using Buttplug.Client;
using Hai.PositionSystemToExternalProgram.Core;

namespace Hai.PositionSystemToExternalProgram.Intiface;

public class IntifaceTransmitter : ITransmitter
{
    public string Hostname { get; set; } = "localhost";
    public ushort Port { get; set; } = 12345;
    
    private readonly HashSet<ButtplugClientDevice> _consideredDevices = new();

    private ButtplugClient _client;
    private ButtplugWebsocketConnector _connector;
    private CancellationTokenSource _cancellationTokenSource;

    private RoboticsCoordinates _target;
    private bool _isConnecting;

    public void ProvideNewTarget(RoboticsCoordinates roboticsCoordinates)
    {
        _target = roboticsCoordinates;
    }

    public async Task Update(float deltaTimeMs)
    {
        if (!_client.Connected) return;
        
        foreach (var device in _consideredDevices)
        {
            if (device.LinearAttributes.Count > 0)
            {
                var mechanical01 = Clamp01(1f - (_target.JoystickTargetL0 + 1f) / 2f);
                await device.LinearAsync((uint)deltaTimeMs, mechanical01);
            }
            // The else-if means we don't execute this if the device had linear attributes to begin with.
            else if (device.VibrateAttributes.Count > 0)
            {
                var amplitude01 = Clamp01(1f - (_target.JoystickBoundedL0 + 1f) / 2f);
                await device.VibrateAsync(amplitude01);
            }
        }
    }

    private float Clamp01(float value)
    {
        if (value < 0) return 0;
        if (value > 1) return 1;
        return value;
    }

    public bool IsOpen()
    {
        return _isConnecting || _client != null && _client.Connected;
    }

    public async Task Open()
    {
        var uri = new Uri($"ws://{Hostname}:{Port}/buttplug");
        
        _client = new ButtplugClient("Position System");
        _connector = new ButtplugWebsocketConnector(uri);

        _cancellationTokenSource = new CancellationTokenSource();

        try
        {
            _client.DeviceAdded += WhenDeviceAdded;
            _client.DeviceRemoved += WhenDeviceRemoved;

            _isConnecting = true;
            await _client.ConnectAsync(_connector, _cancellationTokenSource.Token);
            await _client.StartScanningAsync();
        }
        catch (Exception e)
        {
            Console.Error.WriteLine(e);

            _client = null;
            _connector = null;

            _cancellationTokenSource.Cancel();
            _cancellationTokenSource.Dispose();
            _cancellationTokenSource = null;
        }
        finally
        {
            _isConnecting = false;
        }
    }

    private void WhenDeviceAdded(object sender, DeviceAddedEventArgs e)
    {
        Console.WriteLine($"Device added: {e.Device.Name}");
        
        var thatDevice = e.Device;
        if (thatDevice.LinearAttributes.Count > 0 || thatDevice.VibrateAttributes.Count > 0)
        {
            _consideredDevices.Add(thatDevice);
        }
    }

    private void WhenDeviceRemoved(object sender, DeviceRemovedEventArgs e)
    {
        Console.WriteLine($"Device removed: {e.Device.Name}");
        
        _consideredDevices.Remove(e.Device);
    }

    public async Task Close()
    {
        if (_cancellationTokenSource == null) return;

        await _client.DisconnectAsync();
        _client = null;
        _connector = null;
        
        _cancellationTokenSource.Cancel();
        _cancellationTokenSource.Dispose();
        _cancellationTokenSource = null;
        
        _consideredDevices.Clear();
    }
}