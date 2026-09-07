namespace Manimal.QuestBriefingAPI.Briefings;

public sealed class BriefingFocusGate
{
    private double _visibleSince = double.NaN;

    public bool Update(bool foreground, double now)
    {
        if (!foreground)
        {
            Reset(); 
            return false;
        }
        
        if (double.IsNaN(_visibleSince))
        {
            _visibleSince = now;
        }
        
        return now - _visibleSince >= 0.15;
    }

    public void Reset() => _visibleSince = double.NaN;
}