using System.Numerics;
using Hai.PositionSystemToExternalProgram.Configuration;
using Hai.PositionSystemToExternalProgram.Core;
using ImGuiNET;
using Veldrid;
using Veldrid.Sdl2;

namespace Hai.PositionSystemToExternalProgram.ImGuiProgram;

public class UiMainApplication
{
    private const ImGuiWindowFlags WindowFlags = ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoDocking | ImGuiWindowFlags.NoResize;
    private const ImGuiWindowFlags WindowFlagsNoCollapse = WindowFlags | ImGuiWindowFlags.NoCollapse;

    private readonly IUiActions _uiActions;
    private readonly SavedData _config;
    private readonly UiScrollManager _scrollManager = new UiScrollManager();
    
    private string[] _portNames;
    private int _selectedPortIndex;
    private string _selectedPortName = "";
    
    private int _lastExtractedDataIteration;
    private Texture _cachedTexture;
    private int _lastWidth;
    private int _lastHeight;
    private IntPtr _textureId;
    private readonly string[] _extractorNames;

    private readonly UiRoboticsTab _roboticsTab;
    private Dictionary<string, string> _portDetail;

    public UiMainApplication(IUiActions uiActions, SavedData config)
    {
        _uiActions = uiActions;
        _config = config;
        _extractorNames = Enum.GetNames<ExtractorConfig>();

        _roboticsTab = new UiRoboticsTab(_uiActions, config);
    }

    public void Initialize()
    {
        UpdatePortNames();
        _selectedPortName = _portNames.Length > 0 ? _portNames[0] : "";
    }

    public void SubmitUi(CustomImGuiController controller, Sdl2Window window)
    {
        ImGui.SetNextWindowPos(new Vector2(0, 0), ImGuiCond.Always);
        ImGui.SetNextWindowSize(new Vector2(window.Width, window.Height), ImGuiCond.Always);
        ImGui.Begin("###main", WindowFlagsNoCollapse | ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoBackground | ImGuiWindowFlags.NoScrollbar);

        DrawMain(controller, window);
        
        ImGui.End();
        
        _scrollManager.StoreIfAnyItemHovered();
    }

    private void DrawMain(CustomImGuiController controller, Sdl2Window window)
    {
        var rawData = _uiActions.ExposeRawData();
        var isOpenVrRunning = _uiActions.IsOpenVrRunning();
        var data = _uiActions.Data();
        var anyChanged = false;

        #region serial
        var isSerialOpen = _uiActions.IsSerialOpen();
        var isIntifaceOpen = _uiActions.IsIntifaceOpen();
        var isAnyOpen = isSerialOpen || isIntifaceOpen;
        {
            ImGui.BeginDisabled(isSerialOpen);
            if (ImGui.BeginCombo("##PortCombo", _portDetail.TryGetValue(_selectedPortName, out var value) ? value : _selectedPortName))
            {
                for (int i = 0; i < _portNames.Length; i++)
                {
                    bool isSelected = (_selectedPortIndex == i);
                    if (ImGui.Selectable(_portDetail[_portNames[i]], isSelected))
                    {
                        _selectedPortIndex = i;
                        _selectedPortName = _portNames[i];
                    }
                    
                    if (isSelected)
                    {
                        ImGui.SetItemDefaultFocus();
                    }
                }
                ImGui.EndCombo();
            }
            ImGui.SameLine();
            if (ImGui.Button(LocalizationPhrase.MainLocalizationPhrase.RefreshLabel))
            {
                UpdatePortNames();
            }
            ImGui.EndDisabled();

            if (isSerialOpen)
            {
                ImGui.SameLine();
                if (ImGui.Button(LocalizationPhrase.MainLocalizationPhrase.CloseSerialLabel))
                {
                    _uiActions.DisconnectSerial();
                }
            }
            else
            {
                ImGui.BeginDisabled(_selectedPortName == "");
                var port = (_selectedPortName == "" ? "UNKNOWN" : _selectedPortName);
                if (isAnyOpen) ImGui.SameLine();
                if (ImGui.Button(string.Format(LocalizationPhrase.MainLocalizationPhrase.MsgConnectToDeviceOnSerialPort, port), isAnyOpen ? Vector2.Zero : new Vector2(ImGui.GetContentRegionAvail().X, 60)))
                {
                    _uiActions.ConnectSerial(_selectedPortName);
                }
                ImGui.EndDisabled();
            }
        }
        #endregion
        #region intiface
        {
            ImGui.BeginDisabled(isIntifaceOpen);
            anyChanged |= ImGui.InputInt($"{LocalizationPhrase.MainLocalizationPhrase.IntifacePortLabel}##IntifacePort", ref _config.transmitterIntifacePort, 0);
            ImGui.SameLine();
            if (ImGui.Button(LocalizationPhrase.MainLocalizationPhrase.ResetIntifacePortLabel))
            {
                _config.transmitterIntifacePort = 12345;
                anyChanged = true;
            }
            ImGui.EndDisabled();
            if (isIntifaceOpen)
            {
                ImGui.SameLine();
                if (ImGui.Button(LocalizationPhrase.MainLocalizationPhrase.DisconnectIntifaceLabel))
                {
                    _uiActions.DisconnectIntiface();
                }
            }
            else
            {
                ImGui.BeginDisabled(_config.transmitterIntifacePort < 0 || _config.transmitterIntifacePort > ushort.MaxValue);
                if (isAnyOpen) ImGui.SameLine();
                if (ImGui.Button(string.Format(LocalizationPhrase.MainLocalizationPhrase.MsgConnectIntiface, _config.transmitterIntifacePort), isAnyOpen ? Vector2.Zero : new Vector2(ImGui.GetContentRegionAvail().X, 60)))
                {
                    _uiActions.ConnectIntiface((ushort)_config.transmitterIntifacePort);
                }
                ImGui.EndDisabled();
            }
        }
        #endregion
        
        ShowDataWarningIfApplicable(data);
        if (data.validity == DataValidity.Ok)
        {
            var interpreted = _uiActions.InterpretedTarget();
            if (interpreted.hasTarget)
            {
                ImGui.SameLine();
                ImGui.Text("- Target found");
            }
            else
            {
                ImGui.SameLine();
                ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.5f, 0.5f, 0.5f, 1));
                ImGui.Text("- Target not found");
                ImGui.PopStyleColor();
            }
        }
        
        ImGui.BeginTabBar("##tabs");
        _scrollManager.MakeTab(LocalizationPhrase.MainLocalizationPhrase.RoboticsLabel, () => { anyChanged |= _roboticsTab.RoboticsTab(); });
        _scrollManager.MakeTab(LocalizationPhrase.MainLocalizationPhrase.RoboticsAdvancedLabel, () =>
        {
            var anyRoboticsConfigChanged = _roboticsTab.RoboticsAdvancedTab();
            anyChanged |= anyRoboticsConfigChanged;
            
            if (anyRoboticsConfigChanged)
            {
                _uiActions.ConfigRoboticsUpdated();
            }
        });
        _scrollManager.MakeTab(LocalizationPhrase.MainLocalizationPhrase.WirelessLabel, () =>
        {
            anyChanged |= ImGui.Checkbox(LocalizationPhrase.MainLocalizationPhrase.LimitMessageRateLabel, ref _config.wirelessLimitMessageRate);
            if (_config.wirelessLimitMessageRate)
            {
                anyChanged |= ImGui.SliderInt(LocalizationPhrase.MainLocalizationPhrase.MessagesPerSecondLabel, ref _config.messagesPerSecond, 5, 100);
            }
            if (ImGui.Button($"{LocalizationPhrase.RoboticsLocalizationPhrase.ResetLabel}##reset_wireless"))
            {
                _config.wirelessLimitMessageRate = false;
                _config.messagesPerSecond = 20;
                anyChanged = true;
            }
            
            ImGui.NewLine();
            ImGui.Separator();
            ImGui.SliderInt("L0", ref rawData.L0, 0, 9999);
            ImGui.SliderInt("L1", ref rawData.L1, 0, 9999);
            ImGui.SliderInt("L2", ref rawData.L2, 0, 9999);
            ImGui.SliderInt("R0", ref rawData.R0, 0, 9999);
            ImGui.SliderInt("R1", ref rawData.R1, 0, 9999);
            ImGui.SliderInt("R2", ref rawData.R2, 0, 9999);
        });
        _scrollManager.MakeTab(LocalizationPhrase.MainLocalizationPhrase.DataCalibrationLabel, () =>
        {
            ImGui.SeparatorText(LocalizationPhrase.MainLocalizationPhrase.ExtractorPreferenceLabel);
            var currentExtractor = (int)_config.extractorPreference;
            if (ImGui.Combo(LocalizationPhrase.MainLocalizationPhrase.ModeLabel, ref currentExtractor, _extractorNames, _extractorNames.Length))
            {
                _config.extractorPreference = (ExtractorConfig)currentExtractor;
                anyChanged = true;
            }

            if (_config.extractorPreference is ExtractorConfig.PrioritizeSpout or ExtractorConfig.UseSpoutIfVRRunning)
            {
                ImGui.SeparatorText(LocalizationPhrase.MainLocalizationPhrase.SpoutLabel);
                ImGui.Text(LocalizationPhrase.MainLocalizationPhrase.MsgSpoutUnavailable);
            }
            
            var anyCoordinateChanged = false;
            if (!_uiActions.IsUsingVrExtractor())
            {
                if (_config.extractorPreference == ExtractorConfig.PrioritizeVR && !isOpenVrRunning)
                {
                    ImGui.SeparatorText(LocalizationPhrase.MainLocalizationPhrase.OpenVrLabel);
                    ImGui.Text(LocalizationPhrase.MainLocalizationPhrase.MsgOpenVrUnavailable);
                }
                ImGui.SeparatorText(LocalizationPhrase.MainLocalizationPhrase.WindowLabel);
                anyCoordinateChanged |= SmallAdjustmentSlider($"{LocalizationPhrase.MainLocalizationPhrase.WindowOffsetLabel} X", ref _config.windowCoordinates.x);
                anyCoordinateChanged |= SmallAdjustmentSlider($"{LocalizationPhrase.MainLocalizationPhrase.WindowOffsetLabel} Y", ref _config.windowCoordinates.y);
                anyCoordinateChanged |= ImGui.SliderFloat($"{LocalizationPhrase.MainLocalizationPhrase.WindowAnchorLabel} X", ref _config.windowCoordinates.anchorX, 0f, 1f);
                anyCoordinateChanged |= ImGui.SliderFloat($"{LocalizationPhrase.MainLocalizationPhrase.WindowAnchorLabel} Y", ref _config.windowCoordinates.anchorY, 0f, 1f);
                anyCoordinateChanged |= ImGui.InputText(LocalizationPhrase.MainLocalizationPhrase.WindowNameLabel, ref _config.windowName, 500);
                if (ImGui.Button(LocalizationPhrase.MainLocalizationPhrase.ResetToDefaultsExceptWindowNameLabel))
                {
                    anyCoordinateChanged = true;
                    _config.SetWindowCoordinatesToDefault();
                }
            }
            else
            {
                ImGui.SeparatorText(LocalizationPhrase.MainLocalizationPhrase.OpenVrLabel);
                anyCoordinateChanged |= SmallAdjustmentSlider($"{LocalizationPhrase.MainLocalizationPhrase.VrOffsetLabel} X", ref _config.vrCoordinates.x);
                anyCoordinateChanged |= SmallAdjustmentSlider($"{LocalizationPhrase.MainLocalizationPhrase.VrOffsetLabel} Y", ref _config.vrCoordinates.y);
                anyCoordinateChanged |= ImGui.SliderFloat($"{LocalizationPhrase.MainLocalizationPhrase.VrAnchorLabel} X", ref _config.vrCoordinates.anchorX, 0f, 1f);
                anyCoordinateChanged |= ImGui.SliderFloat($"{LocalizationPhrase.MainLocalizationPhrase.VrAnchorLabel} Y", ref _config.vrCoordinates.anchorY, 0f, 1f);
                anyCoordinateChanged |= ImGui.Checkbox(LocalizationPhrase.MainLocalizationPhrase.UseRightEyeLabel, ref _config.vrUseRightEye);
                if (ImGui.Button(LocalizationPhrase.MainLocalizationPhrase.ResetToDefaultsLabel))
                {
                    anyCoordinateChanged = true;
                    _config.SetVrCoordinatesToDefault();
                }
            }

            if (anyCoordinateChanged)
            {
                _uiActions.ConfigCoordinatesUpdated();
            }

            anyChanged |= anyCoordinateChanged;
            
            var extractedData = _uiActions.ExtractedData();
            if (extractedData.IsValid())
            {
                var interpreted = _uiActions.InterpretedTarget();
                ImGui.SeparatorText(LocalizationPhrase.MainLocalizationPhrase.DebugLabel);
                ImGui.Columns(2);
                
                ImGui.SeparatorText(LocalizationPhrase.MainLocalizationPhrase.InterpretedDataLabel);
                TargetDebug(interpreted);
                
                ImGui.SeparatorText(LocalizationPhrase.MainLocalizationPhrase.DataLabel);
                DrawSieve(16, 101);
                ImGui.Text("");
                DrawSieve(16);
                ImGui.NextColumn();

                var textureId = TurnDataIntoTexture(controller, extractedData);

                ImGui.Text($"{extractedData.Iteration}");
                ImGui.Image(textureId, new Vector2(_lastWidth, _lastHeight));
                var coordinates = _uiActions.IsUsingVrExtractor() ? _uiActions.VrCoordinates() : _uiActions.WindowCoordinates();
                ImGui.Text($"{coordinates.requestedWidth} x {coordinates.requestedHeight}");
            }
            
            ImGui.Columns(1);
            
            ImGui.SeparatorText(LocalizationPhrase.MainLocalizationPhrase.WebsocketsSupportLabel);
            var websocketChanged = ImGui.Checkbox(string.Format(LocalizationPhrase.MainLocalizationPhrase.ExposeWebsocketsOnPortLabel, IWebsocketActions.WebsocketDefaultPort), ref _config.useWebsockets);
            if (websocketChanged)
            {
                _uiActions.ConfigWebsocketsUpdated();
            }
            anyChanged |= websocketChanged;
        });
        _scrollManager.MakeTab(LocalizationPhrase.MainLocalizationPhrase.DebugLabel, () =>
        {
            ImGui.BeginTabBar("##tabs_debug");
            _scrollManager.MakeTab(LocalizationPhrase.MainLocalizationPhrase.LightsLabel, () => DrawLightsTab(data));
            _scrollManager.MakeTab(LocalizationPhrase.MainLocalizationPhrase.EntitiesLabel, () => DrawEntitiesTab(data));
            _scrollManager.MakeTab(LocalizationPhrase.MainLocalizationPhrase.TargetLabel, () => DrawTargetTab());
            _scrollManager.MakeTab(LocalizationPhrase.MainLocalizationPhrase.CameraLabel, () => DrawCameraTab(data));
            _scrollManager.MakeTab(LocalizationPhrase.MainLocalizationPhrase.DataLabel, () => DrawDataTab());
            ImGui.EndTabBar();
        });

        _scrollManager.MakeTab(VERSION.miniVersion, () =>
        {
            ImGui.Text($"{LocalizationPhrase.MainLocalizationPhrase.SoftwareVersionLabel}: {VERSION.version}");
        });

        ImGui.EndTabBar();
        
        if (anyChanged)
        {
            _config.SaveConfig();
        }
    }

    private bool SmallAdjustmentSlider(string label, ref int coord)
    {
        var anyChanged = false;
        if (ImGui.Button($"-##minus__{label}"))
        {
            coord--;
            anyChanged = true;
        }
        ImGui.SameLine();
        anyChanged |= ImGui.SliderInt($"##slider__{label}", ref coord, -100, 100);
        ImGui.SameLine();
        if (ImGui.Button($"+##plus__{label}"))
        {
            coord++;
            anyChanged = true;
        }
        ImGui.SameLine();
        if (ImGui.Button($"0##plus__{label}"))
        {
            coord = 0;
            anyChanged = true;
        }
        ImGui.SameLine();
        ImGui.Text(label);

        return anyChanged;
    }

    private void DrawLightsTab(DecodedData data)
    {
        if (ShaderProtocols.IsProtocol2(data.Version))
        {
            ImGui.Text(LocalizationPhrase.MainLocalizationPhrase.MsgProtocol2NoLightData);
            return;
        }

        var valid = data.validity == DataValidity.Ok;
        if (!valid) ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(1, 0, 0, 1));
        for (var index = 0; index < data.Lights.Length; index++)
        {
            var decodedLight = data.Lights[index];
            ImGui.SeparatorText($"Light #{index + 1}");
            ImGui.BeginDisabled(!decodedLight.enabled);
            ImGui.Text($"Enabled: {BoolToString(decodedLight.enabled)}");
            ImGui.Text($"Position available: {BoolToString(decodedLight.positionAvailable)}");
            ImGui.Text($"Position: {decodedLight.position.X} {decodedLight.position.Y} {decodedLight.position.Z}");
            ImGui.Text($"Color available: {BoolToString(decodedLight.colorAvailable)}");
            ImGui.Text($"Color: {decodedLight.color.X} {decodedLight.color.Y} {decodedLight.color.Z}");
            ImGui.Text($"Intensity: {decodedLight.intensity}");
            ImGui.Text($"Range available: {BoolToString(decodedLight.rangeAvailable)}");
            ImGui.Text($"Range: {decodedLight.range}");
            ImGui.EndDisabled();
        }
        if (!valid) ImGui.PopStyleColor();
    }

    private void DrawEntitiesTab(DecodedData data)
    {
        if (data.validity != DataValidity.Ok)
        {
            ShowDataWarningIfApplicable(data);
            return;
        }
        if (!ShaderProtocols.IsProtocol2(data.Version))
        {
            ImGui.Text(LocalizationPhrase.MainLocalizationPhrase.MsgProtocol1NoEntityData);
            return;
        }

        var interpreted = _uiActions.InterpretedTarget();
        ImGui.Text($"Presence mask: 0x{data.PresenceMask:X8}");
        if (!interpreted.hasTarget)
        {
            ImGui.Text($"{LocalizationPhrase.MainLocalizationPhrase.SelectedTargetLabel}: {LocalizationPhrase.MainLocalizationPhrase.NoneLabel}");
        }
        for (var slot = 0; slot < data.Entities.Length; slot++)
        {
            EntityDebug($"Entity {slot}", data.Entities[slot]);
            var selected = interpreted.hasSourceEntitySlot && interpreted.sourceEntitySlot == slot;
            ImGui.Text($"{LocalizationPhrase.MainLocalizationPhrase.SelectedAsTargetLabel}: {BoolToString(selected)}");
        }
    }

    private void DrawTargetTab()
    {
        ImGui.SeparatorText(LocalizationPhrase.MainLocalizationPhrase.TargetLabel);
        TargetDebug(_uiActions.InterpretedTarget());
    }

    private void DrawCameraTab(DecodedData data)
    {
        if (data.validity != DataValidity.Ok)
        {
            ShowDataWarningIfApplicable(data);
            return;
        }
        if (!ShaderProtocols.SupportsCameraPosition(data.Version))
        {
            ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(1, 0, 1, 1));
            ImGui.Text(string.Format(LocalizationPhrase.MainLocalizationPhrase.MsgShaderDoesNotSupportCameraPosition, data.AsSemverString(), "1.1.0"));
            ImGui.PopStyleColor();
            ImGui.NewLine();
        }

        ImGui.Text($"{LocalizationPhrase.MainLocalizationPhrase.CameraPositionLabel} X: {data.CameraPosition.X}");
        ImGui.Text($"{LocalizationPhrase.MainLocalizationPhrase.CameraPositionLabel} Y: {data.CameraPosition.Y}");
        ImGui.Text($"{LocalizationPhrase.MainLocalizationPhrase.CameraPositionLabel} Z: {data.CameraPosition.Z}");
        ImGui.Text($"Camera position available: {BoolToString(data.CameraPositionAvailable)}");
        ImGui.NewLine();
        ImGui.Text($"{LocalizationPhrase.MainLocalizationPhrase.CameraRotationLabel} X: {data.CameraRotation.X}");
        ImGui.Text($"{LocalizationPhrase.MainLocalizationPhrase.CameraRotationLabel} Y: {data.CameraRotation.Y}");
        ImGui.Text($"{LocalizationPhrase.MainLocalizationPhrase.CameraRotationLabel} Z: {data.CameraRotation.Z}");
        ImGui.Text($"Camera Euler available: {BoolToString(data.CameraEulerAvailable)}");
        ImGui.NewLine();
        ImGui.SeparatorText(LocalizationPhrase.MainLocalizationPhrase.SteamVrPlayspaceLabel);
        ImGui.Text($"{LocalizationPhrase.MainLocalizationPhrase.EstimatedScaleLabel}: {_uiActions.VirtualScale()}");
    }

    private void DrawDataTab()
    {
        var extractedData = _uiActions.ExtractedData();
        if (extractedData.IsValid())
        {
            var decodedData = _uiActions.Data();
            ImGui.Text($"{LocalizationPhrase.MainLocalizationPhrase.ShaderVersionLabel}: {decodedData.AsSemverString()}");
            DrawSieve(32);
            ImGui.NewLine();
        }
    }

    private static void TargetDebug(InterpretedTargetData target)
    {
        ImGui.Text($"HasTarget: {BoolToString(target.hasTarget)}");
        ImGui.Text($"HasNormal: {BoolToString(target.hasNormal)}");
        var interpretedType = target.isHole ? "hole" : target.isRing ? "ring" : "undefined";
        ImGui.Text($"Type: {interpretedType}");
        ImGui.Text($"Position: {target.position.X} {target.position.Y} {target.position.Z}");
        ImGui.Text($"Normal: {target.normal.X} {target.normal.Y} {target.normal.Z}");
        ImGui.Text($"HasTangent: {BoolToString(target.hasTangent)}");
        ImGui.Text($"Tangent: {target.tangent.X} {target.tangent.Y} {target.tangent.Z}");
        ImGui.Text($"Source kind: {target.sourceKind}");
        ImGui.Text($"Owner ID: {(target.hasOwnerIdentity ? $"0x{target.ownerIdentity:X8}" : "N/A")}");
        ImGui.Text($"Entity ID: {(target.hasEntityIdentity ? $"0x{target.entityIdentity:X8}" : "N/A")}");
        ImGui.Text($"HasSocketWorldScale: {BoolToString(target.hasSocketWorldScale)}");
        ImGui.Text($"SocketWorldScale: {target.socketWorldScale}");
        if (target.hasSourceEntitySlot)
        {
            ImGui.Text($"Source entity slot: {target.sourceEntitySlot}");
        }
    }

    private static void EntityDebug(string label, DecodedEntity entity)
    {
        ImGui.SeparatorText(label);
        ImGui.Text($"Present: {BoolToString(entity.Present)}");
        ImGui.Text($"Descriptor: 0x{entity.RawDescriptor:X8} ({entity.SourceKind}, {entity.EntityKind})");
        ImGui.Text($"Descriptor known: {BoolToString(entity.DescriptorKnown)}");
        ImGui.Text($"Owner ID: {(entity.OwnerIdentityAvailable ? $"0x{entity.OwnerIdentity:X8}" : "N/A")}");
        ImGui.Text($"Entity ID: {(entity.EntityIdentityAvailable ? $"0x{entity.EntityIdentity:X8}" : "N/A")}");
        ImGui.Text($"Position: ({entity.Position.X}, {entity.Position.Y}, {entity.Position.Z})");
        ImGui.Text($"Forward available: {BoolToString(entity.ForwardAvailable)}");
        ImGui.Text($"Forward: ({entity.Forward.X}, {entity.Forward.Y}, {entity.Forward.Z})");
        ImGui.Text($"Up available: {BoolToString(entity.UpAvailable)}");
        ImGui.Text($"Up: ({entity.Up.X}, {entity.Up.Y}, {entity.Up.Z})");
        ImGui.Text($"Scale: {entity.Scale} ({(entity.ScaleAvailable ? "source" : "default")})");
        ImGui.Text($"Reserved words zero: {BoolToString(entity.ReservedWordsZero)}");
    }

    private static string BoolToString(bool b)
    {
        return b ? "true" : "false";
    }

    private void DrawSieve(int numberOfColumns, int startAtRow = 0)
    {
        var bits = _uiActions.Bits();
        var data = _uiActions.Data();
        var valid = data.validity == DataValidity.Ok;
        
        ShowDataWarningIfApplicable(data);

        if (!valid) ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.5f, 0.5f, 0.5f, 1));
        
        var numberOfLines = PositionSystemDataLayout.CalculateNumberOfLines(numberOfColumns);
        var digitCount = (int)Math.Floor(Math.Log10(numberOfLines)) + 1;
        var format = new string('0', digitCount);
        
        for (var row = startAtRow; row < numberOfLines; row++)
        {
            ImGui.Text($"#{row.ToString(format)}  ");
            ImGui.SameLine();
            for (var index = 0; index < numberOfColumns; index++)
            {
                var inx = row * numberOfColumns + index;
                var b = inx < bits.Length ? bits[inx] : false;
                var xx = b;
                ImGui.Text(xx ? "X" : ".");
                if (index != numberOfColumns - 1)
                {
                    ImGui.SameLine();
                }
            }
            if (numberOfLines == (int)ShaderV1_1_0.NumberOfLines)
            {
                ImGui.SameLine();
                var wordName = ShaderProtocols.IsProtocol2(data.Version)
                    ? Protocol2WordName(row, data)
                    : Enum.GetName(typeof(ShaderV1_1_0), row);
                ImGui.Text("  ->   " + wordName);
            }
        }
        if (!valid) ImGui.PopStyleColor();
    }

    private static string Protocol2WordName(int word, DecodedData data)
    {
        if (word == 0) return "Checksum";
        if (word == 1) return "Time";
        if (word == 2) return "Identifier";
        if (word == 3) return "Version";
        if (word == 4) return "PresenceMask";
        if (word is >= 5 and <= 7) return $"CameraPosition{(char)('X' + word - 5)}";
        if (word is >= 8 and <= 10) return $"CameraEuler{(char)('X' + word - 8)}";
        if (word is >= ShaderV2_0_0.Entity0 and < ShaderV2_0_0.Entity1)
            return EntityWordLabel(0, word - ShaderV2_0_0.Entity0, data.Entity0);
        if (word is >= ShaderV2_0_0.Entity1 and < ShaderV2_0_0.ReservedStart)
            return EntityWordLabel(1, word - ShaderV2_0_0.Entity1, data.Entity1);
        if (word is >= ShaderV2_0_0.ReservedStart and <= ShaderV2_0_0.ReservedEnd) return $"Reserve{word}";
        return word == ShaderV2_0_0.CanaryWord ? "Canary" : string.Empty;
    }

    private static string EntityWordLabel(int slot, int offset, DecodedEntity entity)
    {
        var name = $"Entity{slot}";
        if (offset == 0) return $"{name}.Descriptor";
        if (offset == 1) return $"{name}.OwnerIdentity";
        if (offset == 2) return $"{name}.EntityIdentity";
        if (offset is >= 3 and <= 5) return $"{name}.Position.{(char)('X' + offset - 3)}";
        if (offset is >= 6 and <= 8) return entity.ForwardAvailable && !entity.UpAvailable
            ? $"{name}.Forward.{(char)('X' + offset - 6)}"
            : $"{name}.Quaternion.{(char)('X' + offset - 6)}";
        if (offset == 9) return entity.ForwardAvailable && !entity.UpAvailable
            ? $"{name}.Quaternion.Unused"
            : $"{name}.Quaternion.W";
        if (offset == 10) return $"{name}.Scale";
        return $"{name}.Reserve{offset - ShaderV2_0_0.EntityReservedOffset}";
    }

    private static void ShowDataWarningIfApplicable(DecodedData data)
    {
        var valid = data.validity == DataValidity.Ok;
        ImGui.PushStyleColor(ImGuiCol.Text, !valid ? new Vector4(1, 0, 0, 1) : new Vector4(0, 1, 1, 1));
        switch (data.validity)
        {
            case DataValidity.NotInitialized:
                ImGui.Text(LocalizationPhrase.MainLocalizationPhrase.MsgDataNotInitialized);
                break;
            case DataValidity.Ok:
                ImGui.Text(LocalizationPhrase.MainLocalizationPhrase.MsgChecksumOk);
                break;
            case DataValidity.InvalidChecksum:
                ImGui.Text(LocalizationPhrase.MainLocalizationPhrase.MsgChecksumInvalid);
                break;
            case DataValidity.UnexpectedVendor:
                ImGui.Text(LocalizationPhrase.MainLocalizationPhrase.MsgChecksumUnexpectedVendor);
                break;
            case DataValidity.UnexpectedVersion:
                ImGui.Text(LocalizationPhrase.MainLocalizationPhrase.MsgChecksumUnexpectedMajorVersion);
                break;
            case DataValidity.InvalidPayload:
                ImGui.Text("Shader data payload is malformed.");
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }
        ImGui.PopStyleColor();
    }

    private IntPtr TurnDataIntoTexture(CustomImGuiController controller, ExtractionResult extractedData)
    {
        if (extractedData.Iteration == _lastExtractedDataIteration) return _textureId;
        
        _lastExtractedDataIteration = extractedData.Iteration;
        if (_cachedTexture == null || _lastWidth != extractedData.Width || _lastHeight != extractedData.Height)
        {
            _cachedTexture?.Dispose();
            _cachedTexture = controller.Graphics.ResourceFactory.CreateTexture(new TextureDescription(
                (uint)extractedData.Width,
                (uint)extractedData.Height,
                1, // depth
                1, // mipLevels
                1, // arrayLayers
                PixelFormat.R8_G8_B8_A8_UNorm,
                TextureUsage.Sampled,
                TextureType.Texture2D
            ));
            _lastWidth = extractedData.Width;
            _lastHeight = extractedData.Height;
        }
            
        controller.Graphics.UpdateTexture(
            _cachedTexture,
            extractedData.ColorData,
            0, 0, 0, // x, y, z offsets
            (uint)extractedData.Width,
            (uint)extractedData.Height,
            1, // depth
            0, // mipLevel
            0  // arrayLayer
        );
            
        _textureId = controller.GetOrCreateImGuiBinding(controller.Graphics.ResourceFactory, _cachedTexture);

        return _textureId;
    }

    private void UpdatePortNames()
    {
        var fetchPortNames = _uiActions.FetchPortNames();
        _portNames = fetchPortNames.Keys.ToArray();
        _portDetail = fetchPortNames;
    }
}
