namespace Aonik.Platform.Services.Tasks;

/// <summary>
/// Answers "is there a registered <c>ITaskActionHandler</c> for this action type?"
/// without constructing the handler. Used by <c>WorkItemService</c> to reject an
/// unknown <c>ActionType</c> at schedule time (Spec 034 §12) so it is never stored.
/// </summary>
public interface ITaskActionHandlerCatalog
{
    bool IsRegistered(string actionType);
}
