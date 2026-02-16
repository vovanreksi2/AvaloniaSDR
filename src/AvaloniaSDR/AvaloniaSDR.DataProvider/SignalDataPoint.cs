namespace AvaloniaSDR.DataProvider;

public record struct SignalDataPoint 
{
    public double Frequency { get; set; }
    public double SignalPower { get; set; }

    public SignalDataPoint(double frequency, double signalPower)
    {
        Frequency = frequency;
        SignalPower = signalPower;
    }
};