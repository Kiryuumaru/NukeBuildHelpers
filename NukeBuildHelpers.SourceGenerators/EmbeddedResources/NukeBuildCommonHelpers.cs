/// <summary>
/// Contains all methods for performing proper <see cref="global::System.IDisposable"/> operations.
/// </summary>
public class NukeBuildCommonHelpers
{
    private const int DisposalNotStarted = 0;
    private const int DisposalStarted = 1;
    private const int DisposalComplete = 2;
}
