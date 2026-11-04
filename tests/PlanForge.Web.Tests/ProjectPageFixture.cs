namespace PlanForge.Web.Tests;

using PlanForge.Contracts.Agents;
using PlanForge.Contracts.Library;
using PlanForge.Contracts.Planning;
using System.Net.Http.Json;

public sealed class ProjectPageFixture : IDisposable
{
	public ProjectPageFixture()
	{
		Guid phase=Guid.NewGuid(),subphase=Guid.NewGuid();
		Project=new(Guid.NewGuid(),5,"Fixture project","Purpose","InProgress","PlanningOnly",DateTimeOffset.UtcNow,[new(phase,"Phase",null,0,"Active phase",Status:"InProgress"),new(subphase,"Subphase",phase,0,"Active subphase",Status:"InProgress"),new(Guid.NewGuid(),"Step",subphase,0,"Active step",Status:"InProgress")]);
		Context=new ComponentTestContext(RespondAsync);
	}
	public ComponentTestContext Context { get; }
	public ProjectSnapshot Project { get; private set; }
	public List<(string Method,string Path,string? Body)> Commands { get; }=[];
	public bool Conflict { get; set; }
	public string? Action { get; set; }
	public int ReadsAfterProjectDelete { get; private set; }
	private bool projectDeleted;
	public void Dispose() => Context.Dispose();
	private async Task<HttpResponseMessage> RespondAsync(HttpRequestMessage request)
	{
		string path=request.RequestUri!.AbsolutePath;
		if(projectDeleted && request.Method==HttpMethod.Get) ReadsAfterProjectDelete++;
		if(request.Method==HttpMethod.Delete && path==$"/api/v1/projects/{Project.Id}") projectDeleted=true;
		if(request.Method!=HttpMethod.Get)
		{
			Commands.Add((request.Method.Method,path,request.Content is null ? null : await request.Content.ReadAsStringAsync()));
			if(Conflict) return ComponentTestContext.Json(new {detail="The project changed."},System.Net.HttpStatusCode.Conflict);
			Project=Project with {Revision=Project.Revision+1};
		}
		return ComponentTestContext.Json(path.EndsWith("/usage",StringComparison.Ordinal) ? (object)new ResourceUsage(0,1024,512,false) : path.EndsWith("/agents",StringComparison.Ordinal) ? (object)Array.Empty<AgentProfile>() : path.EndsWith("/resources",StringComparison.Ordinal) ? Array.Empty<ResourceProfile>() : path.EndsWith("/changes",StringComparison.Ordinal) ? new ProjectChangePage(Project.Revision,Action is null ? 1 : 2,false,Action is null ? [] : [new(2,Project.Revision,Guid.NewGuid(),"Agent","NEA",Action,DateTimeOffset.UtcNow)]) : Project);
	}
}
