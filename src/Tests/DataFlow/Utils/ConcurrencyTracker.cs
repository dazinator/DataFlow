namespace Tests.DataFlow.Utils;

public class ConcurrencyTracker : IConcurrencyTracker
{
    private readonly Action _onEnter;
    private readonly Action _onExit;

    public ConcurrencyTracker(Action onEnter, Action onExit)
    {
        _onEnter = onEnter;
        _onExit = onExit;
    }

    public void Enter() => _onEnter();
    public void Exit() => _onExit();
}
