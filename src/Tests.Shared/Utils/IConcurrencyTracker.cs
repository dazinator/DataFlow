namespace Tests.DataFlow.Utils;

public interface IConcurrencyTracker
{
    void Enter();
    void Exit();
}
