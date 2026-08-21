namespace Hai.PositionSystemToExternalProgram.ImGuiProgram;

public class LocalizationPhrase
{
    public static string LocalizeOrElse(Type localizationGroup, string localizationKey, string englishPhrase)
    {
        return Localization.LocalizeOrElse(localizationGroup, localizationKey, englishPhrase);
    }
    
    public class MainLocalizationPhrase
    {
        private static string LocalizeOrElse(string localizationKey, string englishPhrase) => LocalizationPhrase.LocalizeOrElse(typeof(MainLocalizationPhrase), localizationKey, englishPhrase);

        public static string Separator => "-----------------------------";
        public static string CameraLabel => LocalizeOrElse(nameof(CameraLabel), "Camera");
        public static string CameraPositionLabel => LocalizeOrElse(nameof(CameraPositionLabel), "Camera Position");
        public static string CameraRotationLabel => LocalizeOrElse(nameof(CameraRotationLabel), "Camera Rotation");
        public static string CloseSerialLabel => LocalizeOrElse(nameof(CloseSerialLabel), "Close serial");
        public static string DataCalibrationLabel => LocalizeOrElse(nameof(DataCalibrationLabel), "Data calibration");
        public static string DataLabel => LocalizeOrElse(nameof(DataLabel), "Data");
        public static string DebugLabel => LocalizeOrElse(nameof(DebugLabel), "Debug");
        public static string EntitiesLabel => LocalizeOrElse(nameof(EntitiesLabel), "Entities");
        public static string EstimatedScaleLabel => LocalizeOrElse(nameof(EstimatedScaleLabel), "Estimated scale");
        public static string ExposeWebsocketsOnPortLabel => LocalizeOrElse(nameof(ExposeWebsocketsOnPortLabel), "Expose WebSockets on port {0}");
        public static string ExtractorPreferenceLabel => LocalizeOrElse(nameof(ExtractorPreferenceLabel), "Extractor Preference");
        public static string InterpretedDataLabel => LocalizeOrElse(nameof(InterpretedDataLabel), "Interpreted data");
        public static string LightsLabel => LocalizeOrElse(nameof(LightsLabel), "Lights");
        public static string ModeLabel => LocalizeOrElse(nameof(ModeLabel), "Mode");
        public static string NoneLabel => LocalizeOrElse(nameof(NoneLabel), "None");
        public static string OpenVrLabel => LocalizeOrElse(nameof(OpenVrLabel), "OpenVR");
        public static string RefreshLabel => LocalizeOrElse(nameof(RefreshLabel), "Refresh");
        public static string ResetToDefaultsExceptWindowNameLabel => LocalizeOrElse(nameof(ResetToDefaultsExceptWindowNameLabel), "Reset to defaults (except Window name)");
        public static string ResetToDefaultsLabel => LocalizeOrElse(nameof(ResetToDefaultsLabel), "Reset to defaults");
        public static string RoboticsAdvancedLabel => LocalizeOrElse(nameof(RoboticsAdvancedLabel), "Robotics (Advanced)");
        public static string RoboticsLabel => LocalizeOrElse(nameof(RoboticsLabel), "Robotics");
        public static string SelectedAsTargetLabel => LocalizeOrElse(nameof(SelectedAsTargetLabel), "Selected as target");
        public static string SelectedTargetLabel => LocalizeOrElse(nameof(SelectedTargetLabel), "Selected target");
        public static string ShaderVersionLabel => LocalizeOrElse(nameof(ShaderVersionLabel), "Shader version");
        public static string SoftwareVersionLabel => LocalizeOrElse(nameof(SoftwareVersionLabel), "Software version");
        public static string SpoutLabel => LocalizeOrElse(nameof(SpoutLabel), "Spout");
        public static string SteamVrPlayspaceLabel => LocalizeOrElse(nameof(SteamVrPlayspaceLabel), "SteamVR playspace");
        public static string TargetLabel => LocalizeOrElse(nameof(TargetLabel), "Target");
        public static string UseRightEyeLabel => LocalizeOrElse(nameof(UseRightEyeLabel), "Use right eye");
        public static string VrAnchorLabel => LocalizeOrElse(nameof(VrAnchorLabel), "VR Anchor");
        public static string VrOffsetLabel => LocalizeOrElse(nameof(VrOffsetLabel), "VR Offset");
        public static string WebsocketsSupportLabel => LocalizeOrElse(nameof(WebsocketsSupportLabel), "WebSockets support");
        public static string WindowAnchorLabel => LocalizeOrElse(nameof(WindowAnchorLabel), "Window Anchor");
        public static string WindowLabel => LocalizeOrElse(nameof(WindowLabel), "Window");
        public static string WindowNameLabel => LocalizeOrElse(nameof(WindowNameLabel), "Window name");
        public static string WindowOffsetLabel => LocalizeOrElse(nameof(WindowOffsetLabel), "Window Offset");
        
        public static string MsgChecksumInvalid => LocalizeOrElse(nameof(MsgChecksumInvalid), "Checksum is failing");
        public static string MsgChecksumOk => LocalizeOrElse(nameof(MsgChecksumOk), "Data is OK");
        public static string MsgChecksumUnexpectedMajorVersion => LocalizeOrElse(nameof(MsgChecksumUnexpectedMajorVersion), "Unexpected protocol version");
        public static string MsgChecksumUnexpectedVendor => LocalizeOrElse(nameof(MsgChecksumUnexpectedVendor), "Unexpected vendor");
        public static string MsgConnectToDeviceOnSerialPort => LocalizeOrElse(nameof(MsgConnectToDeviceOnSerialPort), "Connect to device on serial port {0}");
        public static string MsgDataNotInitialized => LocalizeOrElse(nameof(MsgDataNotInitialized), "Data not initialized");
        public static string MsgOpenVrUnavailable => LocalizeOrElse(nameof(MsgOpenVrUnavailable), "OpenVR is not running.");
        public static string MsgProtocol1NoEntityData => LocalizeOrElse(nameof(MsgProtocol1NoEntityData), "Protocol 1 contains decoded Unity light data and does not contain entity records. See the Lights tab.");
        public static string MsgProtocol2NoLightData => LocalizeOrElse(nameof(MsgProtocol2NoLightData), "Protocol 2 contains decoded entity data and does not contain Unity light data. See the Entities tab.");
        public static string MsgShaderDoesNotSupportCameraPosition => LocalizeOrElse(nameof(MsgShaderDoesNotSupportCameraPosition), "Detected shader version is {0}, which does not support camera position (minimum required: {1})");
        public static string MsgSpoutUnavailable => LocalizeOrElse(nameof(MsgSpoutUnavailable), "Spout is not yet available in this version of the software.");
        
        // 1.2.0
        public static string WirelessLabel => LocalizeOrElse(nameof(WirelessLabel), "Wireless");
        public static string LimitMessageRateLabel => LocalizeOrElse(nameof(LimitMessageRateLabel), "Limit message rate");
        public static string MessagesPerSecondLabel => LocalizeOrElse(nameof(MessagesPerSecondLabel), "Messages per second");
        
        // 1.3.0
        public static string MsgConnectIntiface => LocalizeOrElse(nameof(MsgConnectIntiface), "Connect to Intiface on port {0}");
        public static string DisconnectIntifaceLabel => LocalizeOrElse(nameof(DisconnectIntifaceLabel), "Disconnect Intiface");
        public static string IntifacePortLabel => LocalizeOrElse(nameof(IntifacePortLabel), "Intiface port");
        public static string ResetIntifacePortLabel => LocalizeOrElse(nameof(ResetIntifacePortLabel), "Reset port");
    }

    public class RoboticsLocalizationPhrase
    {
        private static string LocalizeOrElse(string localizationKey, string englishPhrase) => LocalizationPhrase.LocalizeOrElse(typeof(RoboticsLocalizationPhrase), localizationKey, englishPhrase);
        
        public static string Separator => "-----------------------------";
        public static string AutoAdjustRootLabel => LocalizeOrElse(nameof(AutoAdjustRootLabel), "Auto-adjust root (Root PID controller)");
        public static string AutoUpdateLabel => LocalizeOrElse(nameof(AutoUpdateLabel), "Auto-update");
        public static string CommandLabel => LocalizeOrElse(nameof(CommandLabel), "Command");
        public static string CompensateVirtualScaleLabel => LocalizeOrElse(nameof(CompensateVirtualScaleLabel), "Compensate virtual scale");
        public static string DampenTargetLabel => LocalizeOrElse(nameof(DampenTargetLabel), "Dampen target (Target PID controller)");
        public static string HardLimits => LocalizeOrElse(nameof(HardLimits), "Hard limits");
        public static string LimitLateralMovementAtTheBottom => LocalizeOrElse(nameof(LimitLateralMovementAtTheBottom), "Limit movement at the bottom");
        public static string LimitMaximumHeightLabel => LocalizeOrElse(nameof(LimitMaximumHeightLabel), "Limit maximum height");
        public static string OffsetPitchAngleLabel => LocalizeOrElse(nameof(OffsetPitchAngleLabel), "Offset pitch angle");
        public static string OffsetsLabel => LocalizeOrElse(nameof(OffsetsLabel), "Offsets");
        public static string ResetLabel => LocalizeOrElse(nameof(ResetLabel), "Reset");
        public static string ResetVirtualScaleLabel => LocalizeOrElse(nameof(ResetVirtualScaleLabel), "Reset virtual scale");
        public static string RoboticsConfigurationLabel => LocalizeOrElse(nameof(RoboticsConfigurationLabel), "Robotics configuration");
        public static string RotateMachineLabel => LocalizeOrElse(nameof(RotateMachineLabel), "Rotate machine");
        public static string RotationPitchLabel => LocalizeOrElse(nameof(RotationPitchLabel), "Rotation pitch");
        public static string SafetySettingsLabel => LocalizeOrElse(nameof(SafetySettingsLabel), "Safety settings");
        public static string SubmitLabel => LocalizeOrElse(nameof(SubmitLabel), "Submit");
        public static string VirtualScaleLabel => LocalizeOrElse(nameof(VirtualScaleLabel), "Virtual scale");
        
        public static string MsgHardLimitsHelper => LocalizeOrElse(nameof(MsgHardLimitsHelper), "Hard limits are applied after PID controllers. PID controllers will remain unaware that a limit has been applied.");
        public static string MsgNotDefaultWarning => LocalizeOrElse(nameof(MsgNotDefaultWarning), "This value is not the default. If you think something is strange with the machine behaviour, press the Reset button.");
        public static string MsgNotLimitedWarning => LocalizeOrElse(nameof(MsgNotLimitedWarning), "The movement of the machine is not limited. If you are using a machine that is capable of moving laterally to the main axis, this can pose a risk.");
        public static string MsgRotateMachineHelper => LocalizeOrElse(nameof(MsgRotateMachineHelper), "This will rotate the entire machine, so that the movement in the virtual space in one direction results in a different direction in the physical space.");
        public static string MsgVirtualScaleHelper => LocalizeOrElse(nameof(MsgVirtualScaleHelper), "A value greater than 1 means it takes more travel in the virtual space to move the same distance in the physical space.");
        
        // 1.1.0
        public static string LimitMinimumHeightLabel => LocalizeOrElse(nameof(LimitMinimumHeightLabel), "Limit minimum height");
        
        // 1.2.0
        public static string TwistLabel => LocalizeOrElse(nameof(TwistLabel), "Twist");
        public static string UseSimulatedTwistFromRollLabel => LocalizeOrElse(nameof(UseSimulatedTwistFromRollLabel), "Use simulated twist from Roll");
        public static string UseSimulatedTwistFromLateralLabel => LocalizeOrElse(nameof(UseSimulatedTwistFromLateralLabel), "Use simulated twist from Lateral");
        public static string SimulatedTwistFromRollLabel => LocalizeOrElse(nameof(SimulatedTwistFromRollLabel), "Simulated twist from Roll");
        public static string SimulatedTwistFromLateralLabel => LocalizeOrElse(nameof(SimulatedTwistFromLateralLabel), "Simulated twist from Lateral");
        public static string TwistMappingPolicyLabel => LocalizeOrElse(nameof(TwistMappingPolicyLabel), "Twist Mapping");
        public static string TwistMappingLinearDiscardLabel => LocalizeOrElse(nameof(TwistMappingLinearDiscardLabel), "Linear Mapping (Discard Excess Twist)");
        public static string TwistMappingLinearStoreLabel => LocalizeOrElse(nameof(TwistMappingLinearStoreLabel), "Linear Mapping (Retain Excess Twist)");
        public static string TwistMappingCenterSeekingRelativeLabel => LocalizeOrElse(nameof(TwistMappingCenterSeekingRelativeLabel), "Center-Seeking Relative Mapping");
        public static string TwistScaleLabel => LocalizeOrElse(nameof(TwistScaleLabel), "Twist Scale");
        public static string TwistMappingPolicyHelper => LocalizeOrElse(nameof(TwistMappingPolicyHelper), "Twist Scale controls R0 degrees per virtual degree.");
        public static string ResetTwistOnSocketTransitionLabel => LocalizeOrElse(nameof(ResetTwistOnSocketTransitionLabel), "Reset Twist on Socket Loss or Identity Change");
        public static string TwistSocketPolicyHelper => LocalizeOrElse(nameof(TwistSocketPolicyHelper), "When enabled, the R0 command resets to 0 immediately; otherwise, the current twist command is preserved.");
    }
}
