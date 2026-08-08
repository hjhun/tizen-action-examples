namespace Reminder.UseCases;

public enum ResultCode { Success, Invalid, NotFound, Conflict, Unavailable, Internal }

public sealed record CommandResult(ResultCode Code, string Reason)
{
    public bool Success => Code == ResultCode.Success;
    public static CommandResult Ok(string reason = "") => new(ResultCode.Success, reason);
    public static CommandResult Fail(ResultCode code, string reason) => new(code, reason);
}
