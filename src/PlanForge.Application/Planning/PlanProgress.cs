namespace PlanForge.Application.Planning;

using PlanForge.Contracts.Planning;

public static class PlanProgress
{
	/// <summary>Calculates upward decision blocking without changing a Step's underlying work state.</summary>
	public static ProjectSnapshot Recalculate(ProjectSnapshot snapshot)
	{
		List<PlanItem> items = snapshot.Items.ToList();
		Dictionary<Guid, PlanItem> indexById = items.ToDictionary(item => item.Id);
		HashSet<Guid> pendingDecisionAncestors = [];
		foreach (PlanItem decision in items.Where(item => item.Kind == "Decision" && item.Status == "Pending"))
		{
			Guid? parentId = decision.ParentId;
			if (parentId == snapshot.Id)
				pendingDecisionAncestors.Add(snapshot.Id);
			for (int depth = 0; depth < items.Count && parentId is Guid id && indexById.TryGetValue(id, out PlanItem? parent); depth++)
			{
				pendingDecisionAncestors.Add(id);
				parentId = parent.ParentId;
			}
		}
		Dictionary<Guid, List<int>> childIndexes = items.Select((item, index) => (item, index))
			.Where(pair => pair.item.ParentId.HasValue && pair.item.Kind != "Decision")
			.GroupBy(pair => pair.item.ParentId!.Value).ToDictionary(group => group.Key, group => group.Select(pair => pair.index).ToList());
		foreach (string kind in new[] { "Step", "Subphase", "Phase", "Decision" })
			for (int index = 0; index < items.Count; index++)
			{
				PlanItem item = items[index];
				if (item.Kind != kind)
					continue;
				string effective = item.Kind == "Step" ? pendingDecisionAncestors.Contains(item.Id) ? "Blocked" : item.Status
					: item.Kind == "Decision" ? item.Status
					: Derive(childIndexes.TryGetValue(item.Id, out List<int>? children) ? children.Select(child => items[child]).ToList() : [], pendingDecisionAncestors.Contains(item.Id));
				items[index] = item with { Status = item.Kind is "Step" or "Decision" ? item.Status : effective, EffectiveStatus = effective };
			}
		return snapshot with { Items = items, Status = Derive(items.Where(item => item.Kind == "Phase").ToList(), pendingDecisionAncestors.Count > 0) };
	}

	private static string Derive(List<PlanItem> children, bool hasPendingDecision)
	{
		if (hasPendingDecision || children.Any(item => item.EffectiveStatus == "Blocked"))
			return "Blocked";
		if (children.Count > 0 && children.All(item => item.EffectiveStatus == "Completed"))
			return "Completed";
		return children.Any(item => item.EffectiveStatus is "Completed" or "InProgress") ? "InProgress" : "Pending";
	}
}
