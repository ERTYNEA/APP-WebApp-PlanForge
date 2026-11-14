namespace PlanForge.Web.Components.Planning;

using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

/// <summary>Formats state-entry instants only in the browser's timezone, never the server's.</summary>
public partial class StatusDate
{
	private DateTimeOffset? formattedValue;
	private string? localDate;

	[Parameter] public DateTimeOffset? Value { get; set; }

	protected override async Task OnAfterRenderAsync(bool firstRender)
	{
		if (Value is not DateTimeOffset date || formattedValue == date)
			return;
		formattedValue = date;
		try
		{
			string result = await JavaScript.InvokeAsync<string>("planForge.formatStatusDate", date.ToUniversalTime().ToString("O"));
			if (Value == date)
			{
				localDate = result;
				StateHasChanged();
			}
		}
		catch (JSDisconnectedException)
		{
			// A lost circuit cannot render; a restored/new component retries the same instant.
			formattedValue = null;
		}
	}

	protected override void OnParametersSet()
	{
		if (Value != formattedValue)
		{
			formattedValue = null;
			localDate = null;
		}
	}
}
