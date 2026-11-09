namespace PlanForge.Contracts.Planning;

public sealed record ProjectSnapshot(Guid Id, long Revision, string Name, string Description, string Status, string Workflow, DateTimeOffset UpdatedAt, IReadOnlyList<PlanItem> Items)
{
	public int Progress
	{
		get
		{
			PlanItem[] steps = Items.Where(item => item.Kind == "Step").ToArray();
			int progress = steps.Length == 0 ? 0 : steps.Count(item => item.EffectiveStatus == "Completed") * 100 / steps.Length;
			return progress == 100 && Items.Any(item => item.Kind == "Decision" && item.Status == "Pending") ? 99 : progress;
		}
	}

	/// <summary>Orders eligible work by immutable age, then the real hierarchy, never GUID before position.</summary>
	public IReadOnlyList<PlanItem> GetEligibleSteps(bool prioritizeInProgress = true)
	{
		Dictionary<Guid, PlanItem> index = Items.ToDictionary(item => item.Id);
		int ParentOrder(PlanItem item, bool phase)
		{
			if (item.ParentId is not Guid parentId || !index.TryGetValue(parentId, out PlanItem? parent))
				return int.MaxValue;
			return !phase ? parent.SortOrder : parent.ParentId is Guid phaseId && index.TryGetValue(phaseId, out PlanItem? ancestor) ? ancestor.SortOrder : int.MaxValue;
		}
		return Items.Where(item => item.Kind == "Step" && item.EffectiveStatus is "Pending" or "InProgress")
			.OrderBy(item => prioritizeInProgress && item.EffectiveStatus == "InProgress" ? 0 : 1).ThenBy(item => item.CreationOrder)
			.ThenBy(item => ParentOrder(item, true)).ThenBy(item => ParentOrder(item, false)).ThenBy(item => item.SortOrder)
			.ThenBy(item => item.Id.ToString("D"), StringComparer.Ordinal).ToList();
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
