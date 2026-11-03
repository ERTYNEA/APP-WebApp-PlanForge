namespace PlanForge.Application.Planning;

using PlanForge.Application.Common;
using PlanForge.Contracts.Planning;
using PlanForge.Domain.Planning;

public static class PlanSnapshotValidator
{
	/// <summary>Validates the entire normalized snapshot before any database mutation.</summary>
	public static void Validate(ProjectSnapshot snapshot)
	{
		if (string.IsNullOrWhiteSpace(snapshot.Name) || snapshot.Name.Length > 160 || snapshot.Description is null || snapshot.Description.Length > 8000 || snapshot.Items is null || snapshot.Items.Count > 5000)
			throw Invalid("Provide a project name and a plan of at most 5000 records.");
		ParseEnum<DevelopmentWorkflow>(snapshot.Workflow);
		ParseEnum<WorkStatus>(snapshot.Status);
		if (snapshot.Items.Any(item => item is null || item.Id == Guid.Empty) || snapshot.Items.Select(item => item.Id).Distinct().Count() != snapshot.Items.Count)
			throw Invalid("Plan record IDs must be nonempty and unique.");
		Dictionary<Guid, PlanItem> items = snapshot.Items.ToDictionary(item => item.Id);
		foreach (PlanItem item in snapshot.Items)
		{
			WorkItemKind kind = ParseEnum<WorkItemKind>(item.Kind);
			ParseEnum<WorkStatus>(item.Status);
			AssignmentMode assignment = ParseEnum<AssignmentMode>(item.AssignmentMode);
			if (string.IsNullOrWhiteSpace(item.Title) || item.Title.Length > 240 || item.Description is null || item.Description.Length > 16000 || item.Resolution is null || item.Resolution.Length > 8000 || item.SortOrder < 0)
				throw Invalid("Plan titles, descriptions, resolutions and order must be valid and within limits.");
			if ((assignment == AssignmentMode.Override) != item.AssignedAgentId.HasValue)
				throw Invalid("Only an explicit agent override may contain an agent ID.");
			if (kind == WorkItemKind.Decision && (item.Status is not ("Pending" or "Completed") || (item.Status == "Completed" && string.IsNullOrWhiteSpace(item.Resolution))))
				throw Invalid("A decision is Pending or Decided (Completed), and a decided record requires a resolution.");
			if (item.Status == "Completed" && kind is WorkItemKind.Phase or WorkItemKind.Subphase)
			{
				List<PlanItem> children = snapshot.Items.Where(child => child.ParentId == item.Id).ToList();
				if (!children.Any(child => child.Kind != "Decision") || children.Any(child => child.Status != "Completed"))
					throw Invalid("A Phase or Subphase cannot complete before its children and decisions.");
			}
			if (kind == WorkItemKind.Phase)
			{
				if (item.ParentId is not null || !snapshot.Items.Any(child => child.Kind == "Subphase" && child.ParentId == item.Id))
					throw Invalid("Every Phase must have at least one Subphase and no parent.");
			}
			else
			{
				if (item.ParentId is null || !items.TryGetValue(item.ParentId.Value, out PlanItem? parent) || (kind == WorkItemKind.Subphase && parent.Kind != "Phase") || (kind == WorkItemKind.Step && parent.Kind != "Subphase") || (kind == WorkItemKind.Decision && parent.Kind is not ("Phase" or "Subphase" or "Step")))
					throw Invalid("Each plan record must belong to the required parent in this project.");
			}
		}
		if (snapshot.Items.GroupBy(item => (item.Kind, item.ParentId, item.SortOrder)).Any(group => group.Count() > 1))
			throw Invalid("Sibling order values must be unique within each record type.");
	}

	public static T ParseEnum<T>(string value) where T : struct, Enum
	{
		if (!Enum.TryParse(value, out T parsed) || !Enum.IsDefined(parsed) || parsed.ToString() != value)
			throw Invalid("A plan value is not supported by this schema.");
		return parsed;
	}

	private static PlanForgeException Invalid(string message) => new(400, message);
}
