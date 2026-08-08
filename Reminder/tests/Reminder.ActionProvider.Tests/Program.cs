static void Assert(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
var root = Directory.GetCurrentDirectory();
var service = File.ReadAllText(Path.Combine(root, "src/Reminder.ScheduleActionProvider/ReminderScheduleService.cs"));
var generated = File.ReadAllText(Path.Combine(root, "src/Reminder.ScheduleActionProvider/Generated/ReminderScheduleActionProvider.cs"));
var viewGenerated = File.ReadAllText(Path.Combine(root, "src/Reminder.ViewActionProvider/Generated/ReminderViewActionProvider.cs"));
var viewService = File.ReadAllText(Path.Combine(root, "src/Reminder.ViewActionProvider/ReminderViewService.cs"));
var manifest = File.ReadAllText(Path.Combine(root, "src/Reminder.App/tizen-manifest.xml"));
string[] actions = ["AddRecording", "AddViewing", "CancelRecording", "CancelViewing", "CompleteReminder", "CreateReminder", "DeleteReminder", "GetReservations", "SearchReminder", "UpdateReminder"];
for (var index = 0; index < actions.Length; index++)
{
    var action = actions[index];
    Assert(generated.Contains($"{action} = {index + 2},", StringComparison.Ordinal), $"Generated MethodId for {action} is wrong.");
    Assert(service.Contains($"override TizenEntityStatus {action}", StringComparison.Ordinal), $"Provider does not implement {action}.");
    Assert(manifest.Contains($"Tv_Tizen.Action.Schedule_{action}", StringComparison.Ordinal), $"Manifest does not advertise {action}.");
}
Assert(generated.Contains("#if TIZEN_RPCPORT_HAS_PRIVILEGE_LOCAL", StringComparison.Ordinal) && generated.Contains("has = false;", StringComparison.Ordinal), "Tizen.NET 13 deny guard is missing.");
Assert(viewGenerated.Contains("public ScreenBounds ScreenBounds;", StringComparison.Ordinal), "Current View ScreenBounds contract is missing.");
Assert(viewGenerated.Contains("public WindowBounds WindowBounds;", StringComparison.Ordinal), "Current View WindowBounds contract is missing.");
Assert(viewGenerated.Contains("public string EntityInfo;", StringComparison.Ordinal), "Current Annotation.EntityInfo contract is missing.");
Assert(viewService.Contains(".ToJson()", StringComparison.Ordinal), "ViewAnnotation must use generated Entity ToJson().");
Assert(manifest.Contains("package=\"org.tizen.actionexamples.reminder\"", StringComparison.Ordinal) && manifest.Contains("api-version=\"13\"", StringComparison.Ordinal), "App identity or Tizen.NET 13 target is wrong.");
Console.WriteLine("Reminder.ActionProvider.Tests: PASS (10 MethodIds/implementations/metadata + current View contract)");
