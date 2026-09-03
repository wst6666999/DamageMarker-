public class SpeedSettingMessage
{
    public float LimitedSpeed { get; }
    public float ThroughWeldSpeed { get; }

    public SpeedSettingMessage(float limitedSpeed, float throughWeldSpeed)
    {
        LimitedSpeed = limitedSpeed;
        ThroughWeldSpeed = throughWeldSpeed;
    }
}
