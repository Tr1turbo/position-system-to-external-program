
using System.Numerics;
using Hai.PositionSystemToExternalProgram.Core;

namespace Hai.PositionSystemToExternalProgram.Robotics;

public class RoboticsDriver
{
    // FIXME: Temporarily disabled PidRoot because the PID controller is unstable.
    private const bool TEMP_CanUsePidRoot = false;

    private const float MinimumTwistAngleDegrees = -135f;
    private const float MaximumTwistAngleDegrees = 135f;
    private const float MinimumUsableDirectionLengthSquared = 0.000001f;
    
    private float _configVirtualScale = 1f;
    
    private bool _configUsePidRoot = false;
    private bool _configUsePidTarget = false;
    
    private float _configSafetyDistanceBeyondWhichInputsAreIgnored = 3f;
    
    private bool _configSafetyUsePolarMode = true;
    private float _configSafetyPolarModeUppermostRadius = 1f;
    private float _configSafetyPolarModeBottommostRadius = 0.4f;
    
    private float _configTopmostHardLimit_NeverZeroToOne = 1f;
    private float _configBottommostHardLimit_ZeroToNeverOne = 0f;
    private bool _configCompensateVirtualSpaceHardLimit = true;
    private float _configRotateSystemAngleDegPitch = 0f;
    
    private float _configTwistFromRoll;
    private float _configTwistFromLateral;
    
    //
    
    private Quaternion _precalculatedPitcher = Quaternion.Identity;
    private float _precalculatedEffectiveVirtualScale = 1f;
    
    //

    private float _unsafeJoystickTargetL0;
    private float _unsafeJoystickTargetL1;
    private float _unsafeJoystickTargetL2;
    private float _unsafeAngleDegR0 = 0; 
    private float _unsafeAngleDegR1 = 0; 
    private float _unsafeAngleDegR2 = 0;
    private float _unsafeVerticality;

    private bool _boundedTwistInitialized;
    private Vector3 _previousTwistNormal;
    private Vector3 _previousTwistFrameUp;
    private float _boundedTwistCommandDegrees;
    
    private Vector3 _transitionalCoordinate;
    private readonly PidControllerVector3 _postTransitionalPid;
    private Vector3 _pidCoordinateCurrent;
    
    private readonly PidControllerVector3 _rootPositionPid;
    private Vector3 _pidRootCurrent;
    private Vector3 _pidRootTarget;

    private float _offsetJoystickTargetL0;
    private float _offsetJoystickTargetL1;
    private float _offsetJoystickTargetL2;
    private float _offsetAngleDegR0; 
    private float _offsetAngleDegR1; 
    private float _offsetAngleDegR2; 
    
    private float _safeJoystickTargetL0;
    private float _safeJoystickTargetL1;
    private float _safeJoystickTargetL2;
    private float _safeAngleDegTargetR0 = 0; 
    private float _safeAngleDegTargetR1 = 0; 
    private float _safeAngleDegTargetR2 = 0;
    
    private float _safeBoundedL0 = 0;

    public RoboticsDriver()
    {
        // TODO: We need to those PID controllers.
        _rootPositionPid = new PidControllerVector3
        {
            proportionalGain = 0.003f,
            integralGain = 0.003f,
            derivativeGain = 0.01f,
            integralMaximumMagnitude = 0.1f
        };
        _postTransitionalPid = new PidControllerVector3
        {
            proportionalGain = 0.05f,
            integralGain = 1f,
            derivativeGain = 0f,
            integralMaximumMagnitude = 0.1f
        };
    }

    private bool IsUsingPidRoot()
    {
        return TEMP_CanUsePidRoot && _configUsePidRoot;
    }

    public void ProvideTargets(InterpretedLightData interpretedData)
    {
        if (!interpretedData.hasTarget)
        {
            // TODO: If there is no target, we need to remember that, so that when a target appears,
            // we don't immediately slam the robotic arm because the data has changed too much.
            ResetBoundedTwistObservation();
            return;
        }

        // ## Acquire Inputs
        {
            // Confine the input light position to a centered box and make it match the robotics coordinate system.
            var reorientedPosition = new Vector3(
                interpretedData.position.Y,
                -interpretedData.position.Z,
                interpretedData.position.X
            );
            var reorientedNormal = new Vector3(
                interpretedData.normal.Y,
                -interpretedData.normal.Z,
                interpretedData.normal.X
            );
            var reorientedTangent = new Vector3(
                interpretedData.tangent.Y,
                -interpretedData.tangent.Z,
                interpretedData.tangent.X
            );
            
            // Rotate the entire system
            if (_configRotateSystemAngleDegPitch != 0)
            {
                reorientedPosition = Vector3.Transform(reorientedPosition, _precalculatedPitcher);
                reorientedNormal = Vector3.Transform(reorientedNormal, _precalculatedPitcher);
                reorientedTangent = Vector3.Transform(reorientedTangent, _precalculatedPitcher);
            }

            reorientedPosition /= _precalculatedEffectiveVirtualScale;
            if (_configCompensateVirtualSpaceHardLimit && _configBottommostHardLimit_ZeroToNeverOne > 0f)
            {
                reorientedPosition.X += _configBottommostHardLimit_ZeroToNeverOne;
            }
            
            var unclampedVectorUntouched = new Vector3(
                Remap(reorientedPosition.X, 0f, 1f, -1f, 1f),
                Remap(reorientedPosition.Y, -0.5f, 0.5f, -1f, 1f),
                Remap(reorientedPosition.Z, -0.5f, 0.5f, -1f, 1f)
            );
            
            // Optionally, use a PID controller to stabilize the root.
            Vector3 unclampedVector;
            if (IsUsingPidRoot())
            {
                _pidRootTarget = unclampedVectorUntouched;
                unclampedVector = unclampedVectorUntouched - _pidRootCurrent;
            }
            else
            {
                unclampedVector = unclampedVectorUntouched;
            }
            
            // If we use the root PID controller, the length does not matter because it will readjust anyway.
            if (IsUsingPidRoot() || unclampedVector.Length() <= _configSafetyDistanceBeyondWhichInputsAreIgnored)
            {
                _unsafeJoystickTargetL0 = Clamp(unclampedVector.X, -1f, 1f);
                _unsafeJoystickTargetL1 = Clamp(unclampedVector.Y, -1f, 1f);
                _unsafeJoystickTargetL2 = Clamp(unclampedVector.Z, -1f, 1f);
                // The verticality must not depend on the hard limit, because we want the uppermost circle safety limit to be independent of the hard limit:
                // If the topmost hard limit is set to a low value, for example 0.25, then the circle limit must be already small.
                // It is explicitly NOT the available range within the hard limits.
                _unsafeVerticality = (_unsafeJoystickTargetL0 + 1) / 2f;

                if (interpretedData.hasNormal)
                {
                    // Perform a normal to degree conversion. This limits the range from -90 to +90.
                    if (interpretedData.hasTangent)
                    {
                        _unsafeAngleDegR0 = UpdateBoundedTwist(reorientedNormal, reorientedTangent);
                    }
                    else
                    {
                        // Normals have no twist, so this needs to be simulated.
                        if (_configTwistFromLateral != 0 || _configTwistFromRoll != 0)
                        {
                            var L2_Lateral = Clamp(unclampedVector.Z, -1f, 1f);
                            var R1_Roll = NormalToDegrees(-reorientedNormal.Z);
                            _unsafeAngleDegR0 = Clamp(
                                _configTwistFromLateral * L2_Lateral * MaximumTwistAngleDegrees + _configTwistFromRoll * R1_Roll,
                                MinimumTwistAngleDegrees,
                                MaximumTwistAngleDegrees
                            );
                        }
                        else
                        {
                            _unsafeAngleDegR0 = 0;
                        }
                        ResetBoundedTwistObservation(_unsafeAngleDegR0);
                    }
                    _unsafeAngleDegR1 = NormalToDegrees(-reorientedNormal.Z);
                    _unsafeAngleDegR2 = NormalToDegrees(reorientedNormal.Y);
                }
                else
                {
                    ResetBoundedTwistObservation();
                }
            }
            else
            {
                // Do not compare a future valid frame against an observation from before
                // the target left the permitted workspace.
                ResetBoundedTwistObservation();
            }
        }

        // ## From there on, we use the robotic arm coordinate space, where X is up (!!!)

        if (_configSafetyUsePolarMode)
        {
            // When the Safety Polar mode is enabled, we clamp the Y and Z axis to be within a disc.
            // If the length on (Y, Z) is greater than the allowed radius, we clamp it to that radius. 
            
            var allowedRadius = RemapAndClamp(_unsafeVerticality, 0f, 1f, _configSafetyPolarModeBottommostRadius, _configSafetyPolarModeUppermostRadius);
            var radial = new Vector3(0, _unsafeJoystickTargetL1, _unsafeJoystickTargetL2);
            if (radial.Length() > allowedRadius) radial = Vector3.Normalize(radial) * allowedRadius;
            
            _transitionalCoordinate = new Vector3(_unsafeJoystickTargetL0, radial.Y, radial.Z);
        }
        else
        {
            // Otherwise, we use unclamped coordinates. This can be dangerous.
            
            _transitionalCoordinate = new Vector3(_unsafeJoystickTargetL0, _unsafeJoystickTargetL1, _unsafeJoystickTargetL2);
        }

        if (!_configUsePidTarget)
        {
            CalculateOutputs(_transitionalCoordinate);
        }
    }

    private float UpdateBoundedTwist(Vector3 normalUntrusted, Vector3 frameUpUntrusted)
    {
        if (!TryCreateTwistFrame(normalUntrusted, frameUpUntrusted, out var normal, out var frameUp))
        {
            ResetBoundedTwistObservation();
            return _boundedTwistCommandDegrees;
        }

        if (!_boundedTwistInitialized)
        {
            _previousTwistNormal = normal;
            _previousTwistFrameUp = frameUp;
            _boundedTwistInitialized = true;
            return _boundedTwistCommandDegrees;
        }

        if (!TryTransportFrameUp(_previousTwistNormal, _previousTwistFrameUp, normal, out var transportedPreviousFrameUp))
        {
            _previousTwistNormal = normal;
            _previousTwistFrameUp = frameUp;
            return _boundedTwistCommandDegrees;
        }

        var sine = Vector3.Dot(Vector3.Cross(transportedPreviousFrameUp, frameUp), normal);
        var cosine = Clamp(Vector3.Dot(transportedPreviousFrameUp, frameUp), -1f, 1f);
        var deltaDegrees = MathF.Atan2(sine, cosine) * 180f / MathF.PI;

        // Always advance the observation, even at a mechanical limit. Any outward
        // movement that the machine cannot perform is discarded instead of stored as windup.
        _previousTwistNormal = normal;
        _previousTwistFrameUp = frameUp;
        _boundedTwistCommandDegrees = Clamp(
            _boundedTwistCommandDegrees + deltaDegrees,
            MinimumTwistAngleDegrees,
            MaximumTwistAngleDegrees
        );
        return _boundedTwistCommandDegrees;
    }

    private static bool TryCreateTwistFrame(
        Vector3 normalUntrusted,
        Vector3 frameUpUntrusted,
        out Vector3 normal,
        out Vector3 frameUp)
    {
        normal = Vector3.Zero;
        frameUp = Vector3.Zero;
        if (!IsUsableDirection(normalUntrusted) || !IsUsableDirection(frameUpUntrusted))
        {
            return false;
        }

        normal = Vector3.Normalize(normalUntrusted);
        var frameUpProjected = frameUpUntrusted - normal * Vector3.Dot(frameUpUntrusted, normal);
        if (!IsUsableDirection(frameUpProjected))
        {
            return false;
        }

        frameUp = Vector3.Normalize(frameUpProjected);
        return true;
    }

    private static bool TryTransportFrameUp(
        Vector3 previousNormal,
        Vector3 previousFrameUp,
        Vector3 currentNormal,
        out Vector3 transportedFrameUp)
    {
        transportedFrameUp = Vector3.Zero;
        var normalDot = Clamp(Vector3.Dot(previousNormal, currentNormal), -1f, 1f);
        if (normalDot < -0.9999f)
        {
            // Parallel transport is ambiguous for an exact 180-degree normal change.
            // Re-baseline this sample rather than issuing an arbitrary half-turn.
            return false;
        }

        Quaternion transport;
        if (normalDot > 0.9999f)
        {
            transport = Quaternion.Identity;
        }
        else
        {
            var cross = Vector3.Cross(previousNormal, currentNormal);
            transport = Quaternion.Normalize(new Quaternion(cross, 1f + normalDot));
        }

        var transported = Vector3.Transform(previousFrameUp, transport);
        transported -= currentNormal * Vector3.Dot(transported, currentNormal);
        if (!IsUsableDirection(transported))
        {
            return false;
        }

        transportedFrameUp = Vector3.Normalize(transported);
        return true;
    }

    private static bool IsUsableDirection(Vector3 direction)
    {
        return float.IsFinite(direction.X)
            && float.IsFinite(direction.Y)
            && float.IsFinite(direction.Z)
            && direction.LengthSquared() > MinimumUsableDirectionLengthSquared;
    }

    private void ResetBoundedTwistObservation(float? commandDegrees = null)
    {
        _boundedTwistInitialized = false;
        if (commandDegrees.HasValue)
        {
            _boundedTwistCommandDegrees = Clamp(
                commandDegrees.Value,
                MinimumTwistAngleDegrees,
                MaximumTwistAngleDegrees
            );
        }
    }

    private void CalculateOutputs(Vector3 whichVector)
    {
        // Apply offsets to the physical device. Note that doing this will reduce the motion range of the device
        // because the input was already clamped.
        // Using offsets instead of reducing the motion space has the advantage that the motion in virtual space
        // is still consistent in scale in comparison to the other axis.
        var workX = whichVector.X + _offsetJoystickTargetL0;
        if (_configTopmostHardLimit_NeverZeroToOne < 1f)
        {
            var uppermostLimit = _configTopmostHardLimit_NeverZeroToOne * 2 - 1;
            if (workX > uppermostLimit)
            {
                workX = uppermostLimit;
            }
        }
        if (_configBottommostHardLimit_ZeroToNeverOne > 0f)
        {
            var bottommostLimit = _configBottommostHardLimit_ZeroToNeverOne * 2 - 1;
            if (workX < bottommostLimit)
            {
                workX = bottommostLimit;
            }
        }
        _safeJoystickTargetL0 = Clamp(workX, -1f, 1f);
        _safeJoystickTargetL1 = Clamp(whichVector.Y + _offsetJoystickTargetL1, -1f, 1f);
        _safeJoystickTargetL2 = Clamp(whichVector.Z + _offsetJoystickTargetL2, -1f, 1f);

        // Apply offsets to the physical device and clamp it. Since the input was not clamped,
        // this will not reduce the motion range of the device.
        _safeAngleDegTargetR0 = Clamp(_unsafeAngleDegR0 + _offsetAngleDegR0, -360f, 360f);
        _safeAngleDegTargetR1 = Clamp(_unsafeAngleDegR1 + _offsetAngleDegR1, -65f, 65f);
        _safeAngleDegTargetR2 = Clamp(_unsafeAngleDegR2 + _offsetAngleDegR2, -65f, 65f);

        _safeBoundedL0 = Clamp(whichVector.X, -1, 1);
    }

    public void MarkDataFailure()
    {
        // Placeholder; then there may be a procedure to ensure that when data is recovered,
        // we don't immediately slam the robotic arm because the data has changed too much.
        ResetBoundedTwistObservation();
    }

    public RoboticsCoordinates UpdateAndGetCoordinates(long deltaTimeMs)
    {
        var deltaTime = deltaTimeMs / 1000f;
        if (IsUsingPidRoot())
        {
            _pidRootCurrent += _rootPositionPid.Update(deltaTime, _pidRootCurrent, _pidRootTarget);
        }
        
        if (_configUsePidTarget)
        {
            _pidCoordinateCurrent += _postTransitionalPid.Update(deltaTime, _pidCoordinateCurrent, _transitionalCoordinate);
            
            CalculateOutputs(_pidCoordinateCurrent);
        }
        
        return new RoboticsCoordinates
        {
            JoystickTargetL0 = _safeJoystickTargetL0,
            JoystickTargetL1 = _safeJoystickTargetL1,
            JoystickTargetL2 = _safeJoystickTargetL2,
            AngleDegR0 = _safeAngleDegTargetR0,
            AngleDegR1 = _safeAngleDegTargetR1,
            AngleDegR2 = _safeAngleDegTargetR2,
            
            JoystickBoundedL0 = _safeBoundedL0
        };
    }

    private static float Clamp(float value, float toMin, float toMax)
    {
        if (value < toMin) return toMin;
        if (value > toMax) return toMax;
        return value;
    }

    private static float RemapAndClamp(float value, float fromMin, float fromMax, float toMin, float toMax)
    {
        var result = Remap(value, fromMin, fromMax, toMin, toMax);
        if (result < toMin) return toMin;
        if (result > toMax) return toMax;
        return result;
    }

    private static float Remap(float value, float fromMin, float fromMax, float toMin, float toMax)
    {
        var normalizedValue = (value - fromMin) / (fromMax - fromMin);
        var result = toMin + normalizedValue * (toMax - toMin);
        return result;
    }

    private static float NormalToDegrees(float normal)
    {
        var angleRad = (float)Math.Asin(normal);
        var angleDegrees = angleRad * 180f / (float)Math.PI;
        return angleDegrees;
    }

    public void UpdateConfiguration(RoboticsConfiguration config)
    {
        _configVirtualScale = config.RoboticsVirtualScale;
        _configSafetyUsePolarMode = config.RoboticsSafetyUsePolarMode;
        _configUsePidRoot = config.RoboticsUsePidRoot;
        _configUsePidTarget = config.RoboticsUsePidTarget;
        var configBottommostHardLimit = config.BottommostHardLimit;
        var configTopmostHardLimit = config.TopmostHardLimit;
        if (configBottommostHardLimit >= configTopmostHardLimit)
        {
            configBottommostHardLimit = configTopmostHardLimit - 0.001f;
        }
        
        _configTopmostHardLimit_NeverZeroToOne = configTopmostHardLimit;
        if (configTopmostHardLimit <= 0f)
        {
            // We can't let this be 0 or less
            _configTopmostHardLimit_NeverZeroToOne = 0.0001f;
        }
        else if (configTopmostHardLimit > 1f)
        {
            _configTopmostHardLimit_NeverZeroToOne = 1f;
        }
        _configBottommostHardLimit_ZeroToNeverOne = configBottommostHardLimit;
        if (configBottommostHardLimit >= 1f)
        {
            _configBottommostHardLimit_ZeroToNeverOne = 0.9999f;
        }
        else if (configBottommostHardLimit < 0f)
        {
            _configBottommostHardLimit_ZeroToNeverOne = 0f;
        }
        
        _configCompensateVirtualSpaceHardLimit = config.CompensateVirtualScaleHardLimit;
        _offsetAngleDegR2 = config.OffsetAngleDegR2;
        _configRotateSystemAngleDegPitch = config.RotateSystemAngleDegPitch;
        
        //
        
        _precalculatedPitcher = Quaternion.CreateFromAxisAngle(new Vector3(0, 0, 1), -_configRotateSystemAngleDegPitch * (float)Math.PI / 180f);
        if (_configCompensateVirtualSpaceHardLimit)
        {
            var totalAvailableDistance = _configTopmostHardLimit_NeverZeroToOne - _configBottommostHardLimit_ZeroToNeverOne;
            _precalculatedEffectiveVirtualScale = _configVirtualScale / totalAvailableDistance;
        }
        else
        {
            _precalculatedEffectiveVirtualScale = _configVirtualScale;
        }

        _configTwistFromRoll = config.UseSimulatedTwistFromRoll ? config.SimulatedTwistFromRoll : 0f;
        _configTwistFromLateral = config.UseSimulatedTwistFromLateral ? config.SimulatedTwistFromLateral : 0f;
        ResetBoundedTwistObservation();
    }
    
    public struct RoboticsConfiguration
    {
        public float RoboticsVirtualScale { get; init; }
        public bool RoboticsSafetyUsePolarMode { get; init; }
        public bool RoboticsUsePidRoot { get; init; }
        public bool RoboticsUsePidTarget { get; init; }
        public float TopmostHardLimit { get; init; }
        public float BottommostHardLimit { get; init; }
        public float OffsetAngleDegR2 { get; init; }
        public float RotateSystemAngleDegPitch { get; init; }
        public bool CompensateVirtualScaleHardLimit { get; init; }
        public bool UseSimulatedTwistFromRoll { get; init; }
        public float SimulatedTwistFromRoll { get; init; }
        public bool UseSimulatedTwistFromLateral { get; init; }
        public float SimulatedTwistFromLateral { get; init; }
    }
}
