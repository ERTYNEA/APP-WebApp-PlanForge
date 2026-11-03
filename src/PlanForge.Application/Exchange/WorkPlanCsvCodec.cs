namespace PlanForge.Application.Exchange;

using PlanForge.Application.Common;
using PlanForge.Application.Planning;
using PlanForge.Contracts.Planning;
using System.Globalization;
using System.Text;

/// <summary>Pure, lossless schema-version-1 codec for work-plan.csv; never authorizes or persists changes.</summary>
/// <remarks>
/// UTF-8 without an emitted BOM; one leading BOM is accepted. Semicolon-delimited RFC4180 quoting
/// doubles embedded quotes and quotes fields containing semicolons, quotes, CR or LF. Record separators
/// are CRLF on output and LF or CRLF on input; embedded newlines and whitespace are preserved verbatim.
/// The final record separator is optional on input. The exact, case-sensitive 19-column header is:
/// SchemaVersion;ProjectId;BaseRevision;Kind;Id;ParentId;SortOrder;Title;Description;Status;Workflow;UpdatedAt;AssignmentMode;AssignedAgentId;Resolution;Evidence;EvidenceIsNull;CompletedBy;CompletedAt.
/// Columns, numbered from one:
/// 1 SchemaVersion: literal 1 on every data row.
/// 2 ProjectId: the snapshot ID, repeated on every row as a nonempty D-format GUID.
/// 3 BaseRevision: the snapshot Revision, repeated as a nonnegative invariant Int64.
/// 4 Kind: Project on the first and only metadata row, then Phase, Subphase, Step or Decision.
/// 5 Id: the project ID on Project; otherwise the stable, unique plan-item GUID.
/// 6 ParentId: optional D-format item parent GUID; empty on Project.
/// 7 SortOrder: nonnegative invariant Int32 on items; empty on Project.
/// 8 Title: snapshot Name on Project, otherwise item Title.
/// 9 Description and 10 Status: the corresponding project or item fields; Status is a WorkStatus name, or Pending/Decided for Decision rows.
/// 11 Workflow: the DevelopmentWorkflow name on Project only.
/// 12 UpdatedAt: the snapshot timestamp on Project only, in invariant round-trip O format with its offset.
/// 13 AssignmentMode: the exact AssignmentMode name on items only.
/// 14 AssignedAgentId: optional D-format GUID on items only.
/// 15 Resolution: item resolution, including the required resolution of a Decided decision.
/// 16 Evidence: item closure evidence, or empty when null.
/// 17 EvidenceIsNull: 1 for null item Evidence, 0 for non-null Evidence (including an empty string).
/// 18 CompletedBy: optional D-format closure-actor GUID on items only.
/// 19 CompletedAt: optional item closure timestamp in invariant round-trip O format with its offset.
/// Columns 13–19 are empty on Project; columns 11–12 are empty on items. Other inapplicable fields
/// must also be empty, never silently discarded. Optional GUIDs and timestamps use empty fields for null.
/// Enum names are case-sensitive, not numeric. Row order is preserved; no sorting, ID generation,
/// deduplication or closure-evidence rewriting occurs. Existing snapshot semantics are validated.
/// Limits are 1 MiB of encoded payload (including an input BOM) and 5000 plan items plus one Project metadata record,
/// excluding the header. Malformed data throws PlanForgeException with status 400; exceeded limits use 413.
/// Authorization, target-project matching, current-revision checks and explicit replace confirmation
/// belong to the caller; BaseRevision is parsed and preserved, not compared to external state.
/// </remarks>
public static class WorkPlanCsvCodec
{
	private const string header = "SchemaVersion;ProjectId;BaseRevision;Kind;Id;ParentId;SortOrder;Title;Description;Status;Workflow;UpdatedAt;AssignmentMode;AssignedAgentId;Resolution;Evidence;EvidenceIsNull;CompletedBy;CompletedAt";
	private const int maximumBytes = 1024 * 1024;
	private const int maximumRecords = 5001;
	private static readonly UTF8Encoding utf8 = new(false, true);

	/// <summary>Parses and validates one complete snapshot without modifying any external state.</summary>
	/// <param name="payload">The complete UTF-8 CSV payload.</param>
	/// <returns>The project metadata and its plan items in input order.</returns>
	public static ProjectSnapshot Parse(byte[] payload)
	{
		if (payload is null)
			throw Invalid();
		if (payload.Length > maximumBytes)
			throw TooLarge();
		List<string[]> rows = ReadRows(Decode(payload));
		if (rows.Count < 2 || !rows[0].SequenceEqual(header.Split(';')) || rows.Any(row => row.Length != 19))
			throw Invalid();
		string[] project = rows[1];
		Guid projectId = ParseId(project[1]);
		long revision = ParseRevision(project[2]);
		if (project[0] != "1" || project[3] != "Project" || ParseId(project[4]) != projectId
			|| new[] { 5, 6, 12, 13, 14, 15, 16, 17, 18 }.Any(column => project[column] != ""))
			throw Invalid();
		List<PlanItem> items = [];
		foreach (string[] row in rows.Skip(2))
		{
			if (row[0] != "1" || ParseId(row[1]) != projectId || ParseRevision(row[2]) != revision
				|| row[3] == "Project" || row[10] != "" || row[11] != ""
				|| row[16] is not ("0" or "1") || (row[16] == "1" && row[15] != ""))
				throw Invalid();
			items.Add(new PlanItem(
				ParseId(row[4]), row[3], ParseOptionalId(row[5]), ParseSortOrder(row[6]), row[7], row[8], row[9],
				row[12], ParseOptionalId(row[13]), row[14], row[16] == "1" ? null : row[15],
				ParseOptionalId(row[17]), row[18] == "" ? null : ParseTimestamp(row[18])));
		}
		var snapshot = new ProjectSnapshot(projectId, revision, project[7], project[8], project[9], project[10], ParseTimestamp(project[11]), items);
		Validate(snapshot);
		return snapshot;
	}

	/// <summary>Serializes project metadata and each plan item exactly once, without changing the snapshot.</summary>
	/// <param name="snapshot">The complete project snapshot, including an empty plan if applicable.</param>
	/// <returns>BOM-free UTF-8 CSV with a header and CRLF-terminated records.</returns>
	public static byte[] Serialize(ProjectSnapshot snapshot)
	{
		if (snapshot is null || snapshot.Items is null)
			throw Invalid();
		if (snapshot.Items.Count >= maximumRecords)
			throw TooLarge();
		Validate(snapshot);
		string projectId = snapshot.Id.ToString("D");
		string revision = snapshot.Revision.ToString(CultureInfo.InvariantCulture);
		string[] project = [
			"1", projectId, revision, "Project", projectId, "", "", snapshot.Name, snapshot.Description,
			snapshot.Status, snapshot.Workflow, snapshot.UpdatedAt.ToString("O", CultureInfo.InvariantCulture),
			"", "", "", "", "", "", ""
		];
		var result = new StringBuilder(header).Append("\r\n").AppendJoin(';', project.Select(Escape)).Append("\r\n");
		foreach (PlanItem item in snapshot.Items)
		{
			string[] row = [
				"1", projectId, revision, item.Kind, item.Id.ToString("D"), item.ParentId?.ToString("D") ?? "",
				item.SortOrder.ToString(CultureInfo.InvariantCulture), item.Title, item.Description, item.Status, "", "",
				item.AssignmentMode, item.AssignedAgentId?.ToString("D") ?? "", item.Resolution, item.Evidence ?? "",
				item.Evidence is null ? "1" : "0", item.CompletedBy?.ToString("D") ?? "",
				item.CompletedAt?.ToString("O", CultureInfo.InvariantCulture) ?? ""
			];
			result.AppendJoin(';', row.Select(Escape)).Append("\r\n");
			if (result.Length > maximumBytes)
				throw TooLarge();
		}
		try
		{
			string text = result.ToString();
			if (utf8.GetByteCount(text) > maximumBytes)
				throw TooLarge();
			return utf8.GetBytes(text);
		}
		catch (EncoderFallbackException)
		{
			throw Invalid();
		}
	}

	private static string Decode(byte[] payload)
	{
		try
		{
			string text = utf8.GetString(payload);
			return text.StartsWith('\uFEFF') ? text[1..] : text;
		}
		catch (DecoderFallbackException)
		{
			throw Invalid();
		}
	}

	private static string Escape(string value)
	{
		if (value.Length > maximumBytes)
			throw TooLarge();
		return value.IndexOfAny([';', '"', '\r', '\n']) < 0 ? value : "\"" + value.Replace("\"", "\"\"") + "\"";
	}

	private static PlanForgeException Invalid() => new(400, "The work plan CSV is malformed or unsupported.");

	private static Guid ParseId(string value) => Guid.TryParseExact(value, "D", out Guid id) && value.Length == 36 && id != Guid.Empty ? id : throw Invalid();

	private static Guid? ParseOptionalId(string value) => value == "" ? null : ParseId(value);

	private static long ParseRevision(string value) => long.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out long revision) ? revision : throw Invalid();

	private static int ParseSortOrder(string value) => int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out int order) ? order : throw Invalid();

	private static DateTimeOffset ParseTimestamp(string value) => DateTimeOffset.TryParseExact(value, "O", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTimeOffset timestamp) ? timestamp : throw Invalid();

	private static List<string[]> ReadRows(string text)
	{
		List<string[]> rows = [];
		List<string> row = [];
		var field = new StringBuilder();
		bool quoted = false;
		bool closed = false;
		for (int index = 0; index < text.Length; index++)
		{
			char character = text[index];
			if (quoted)
			{
				if (character != '"')
					field.Append(character);
				else if (index + 1 < text.Length && text[index + 1] == '"')
				{
					field.Append('"');
					index++;
				}
				else
				{
					quoted = false;
					closed = true;
				}
				continue;
			}
			if (character is ';' or '\r' or '\n')
			{
				row.Add(field.ToString());
				if (row.Count > 19)
					throw Invalid();
				field.Clear();
				closed = false;
				if (character == ';')
					continue;
				if (character == '\r')
				{
					if (index + 1 >= text.Length || text[++index] != '\n')
						throw Invalid();
				}
				rows.Add(row.ToArray());
				if (rows.Count > maximumRecords + 1)
					throw TooLarge();
				row.Clear();
			}
			else if (closed)
				throw Invalid();
			else if (character == '"')
			{
				if (field.Length != 0)
					throw Invalid();
				quoted = true;
			}
			else
				field.Append(character);
		}
		if (quoted)
			throw Invalid();
		if (closed || field.Length != 0 || row.Count != 0)
		{
			row.Add(field.ToString());
			rows.Add(row.ToArray());
			if (rows.Count > maximumRecords + 1)
				throw TooLarge();
		}
		return rows;
	}

	private static PlanForgeException TooLarge() => new(413, "The work plan CSV exceeds 1 MiB or 5000 plan items.");

	private static void Validate(ProjectSnapshot? snapshot)
	{
		if (snapshot is null || snapshot.Id == Guid.Empty || snapshot.Revision < 0 || snapshot.Items is null
			|| snapshot.Items.Any(item => item is null || item.Id == snapshot.Id || item.ParentId == Guid.Empty
				|| item.AssignedAgentId == Guid.Empty || item.CompletedBy == Guid.Empty))
			throw Invalid();
		PlanSnapshotValidator.Validate(snapshot);
	}
}
