namespace PlanForge.Web;

using PlanForge.Contracts.Planning;

public static class PlanLabels
{
	public static readonly string[] Workflows = ["PlanningOnly", "LocalChangesManualApproval", "BranchPullRequest", "DirectToMain"];
	public static string Workflow(string value) => UiText.Get(value switch { "PlanningOnly" => "Planning Only", "LocalChangesManualApproval" => "Local Changes + Manual Approval", "BranchPullRequest" => "Branch + Pull Request", "DirectToMain" => "Direct to Main", _ => value });
	public static string Status(PlanItem item, string workflow) => UiText.Get(item.EffectiveStatus == "InProgress" && item.Evidence == "WorkDone" ? workflow switch { "LocalChangesManualApproval" => "Awaiting approval", "BranchPullRequest" => "Awaiting merge", "DirectToMain" => "Awaiting push confirmation", _ => "In progress" } : item.EffectiveStatus == "InProgress" ? "In progress" : item.EffectiveStatus);
	public static string StatusClass(string status) => status switch { "Completed" or "Decided" => "success", "Blocked" => "error", "InProgress" => "active", _ => "warning" };
	public static string RouteKind(string kind) => kind switch { "Phase" => "phases", "Subphase" => "subphases", "Step" => "steps", "Decision" => "decisions", _ => throw new ArgumentOutOfRangeException(nameof(kind)) };
	public static PlanItem? CurrentWork(ProjectSnapshot snapshot) => snapshot.Items.FirstOrDefault(item => item.Kind == "Step" && item.EffectiveStatus is "InProgress" or "Blocked")
		?? snapshot.Items.FirstOrDefault(item => item.Kind == "Subphase" && item.EffectiveStatus is "InProgress" or "Blocked")
		?? snapshot.Items.FirstOrDefault(item => item.Kind == "Phase" && item.EffectiveStatus is "InProgress" or "Blocked");
	public static string Bytes(long bytes) => bytes >= 1024 * 1024 ? $"{bytes / (1024d * 1024):0.#} MiB" : bytes >= 1024 ? $"{bytes / 1024d:0.#} KiB" : $"{bytes} B";
}
