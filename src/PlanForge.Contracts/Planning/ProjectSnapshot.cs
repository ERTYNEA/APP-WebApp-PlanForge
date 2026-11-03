namespace PlanForge.Contracts.Planning;

public sealed record ProjectSnapshot(Guid Id, long Revision, string Name, string Description, string Status, string Workflow, DateTimeOffset UpdatedAt, IReadOnlyList<PlanItem> Items)
{
	public int Progress
	{
		get
		{
			PlanItem[] steps = Items.Where(item => item.Kind == "Step").ToArray();
			int progress = steps.Length == 0 ? 0 : steps.Count(item => item.Status == "Completed") * 100 / steps.Length;
			return progress == 100 && Items.Any(item => item.Kind == "Decision" && item.Status == "Pending") ? 99 : progress;
		}
	}

	public Guid? GetEffectiveAgentId(Guid itemId)
	{
		Guid? currentId = itemId;
		for (int depth = 0; depth < Items.Count && currentId.HasValue; depth++)
		{
			PlanItem? current = Items.SingleOrDefault(item => item.Id == currentId);
			if (current is null || current.AssignmentMode == "Unassigned")
				return null;
			if (current.AssignmentMode == "Override")
				return current.AssignedAgentId;
			currentId = current.ParentId;
		}
		return null;
	}
}
