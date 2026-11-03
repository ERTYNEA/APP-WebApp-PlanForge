namespace PlanForge.Infrastructure.Tests;

using PlanForge.Application.Common;
using PlanForge.Application.Exchange;
using PlanForge.Contracts.Planning;
using System.Text;

public sealed class WorkPlanCsvCodecTests
{
	private const string header = "SchemaVersion;ProjectId;BaseRevision;Kind;Id;ParentId;SortOrder;Title;Description;Status;Workflow;UpdatedAt;AssignmentMode;AssignedAgentId;Resolution;Evidence;EvidenceIsNull;CompletedBy;CompletedAt";

	[Fact]
	public void RoundTrip_EmptyPlan_PreservesProjectMetadata()
	{
		ProjectSnapshot expected = EmptyProject();

		byte[] payload = WorkPlanCsvCodec.Serialize(expected);
		ProjectSnapshot actual = WorkPlanCsvCodec.Parse(payload);

		Assert.Equal(expected, actual with { Items = expected.Items });
		Assert.Empty(actual.Items);
		Assert.Equal($"{header}\r\n1;{expected.Id:D};42;Project;{expected.Id:D};;;Project;Description;InProgress;BranchPullRequest;2026-11-03T12:34:56.1234567+02:00;;;;;;;\r\n", Encoding.UTF8.GetString(payload));
	}

	[Fact]
	public void RoundTrip_NormalizedPlan_PreservesEveryFieldAndClosureEvidenceWithoutDuplicateRows()
	{
		ProjectSnapshot expected = PopulatedProject();

		byte[] payload = WorkPlanCsvCodec.Serialize(expected);
		ProjectSnapshot actual = WorkPlanCsvCodec.Parse(payload);

		Assert.Equal(expected, actual with { Items = expected.Items });
		Assert.Equal(expected.Items, actual.Items);
		Assert.Equal(expected.Items.Count + 2, Encoding.UTF8.GetString(payload).Split("\r\n", StringSplitOptions.RemoveEmptyEntries).Length);
		Assert.Equal(payload, WorkPlanCsvCodec.Serialize(actual));
		Assert.Equal(expected.UpdatedAt.ToString("O"), actual.UpdatedAt.ToString("O"));
		Assert.Equal(expected.Items.Select(item => item.CompletedAt?.ToString("O")), actual.Items.Select(item => item.CompletedAt?.ToString("O")));
	}

	[Fact]
	public void RoundTrip_QuotedUnicodeFields_PreservesWhitespaceQuotesAndMixedNewlines()
	{
		const string text = "  Rubén;\"設計\" 🛠️\r\nsecond\nthird\rfourth  ";
		ProjectSnapshot expected = PopulatedProject() with { Name = text, Description = text };
		expected = expected with { Items = expected.Items.Select(item => item with { Title = text, Description = text, Resolution = text }).ToArray() };

		byte[] payload = WorkPlanCsvCodec.Serialize(expected);
		ProjectSnapshot actual = WorkPlanCsvCodec.Parse(payload);

		Assert.Equal(expected, actual with { Items = expected.Items });
		Assert.Equal(expected.Items, actual.Items);
		Assert.Contains("\"  Rubén;\"\"設計\"\" 🛠️\r\nsecond\nthird\rfourth  \"", Encoding.UTF8.GetString(payload));
		Assert.Equal(payload, WorkPlanCsvCodec.Serialize(actual));
	}

	[Theory]
	[InlineData("\"unterminated")]
	[InlineData("unquoted\"quote")]
	[InlineData("\"closed\"trailing")]
	[InlineData("\"closed\" ")]
	[InlineData("bare\rcarriage")]
	public void Parse_MalformedQuoting_ThrowsBadRequest(string row)
	{
		string[] fields = SimpleRows()[1].Split(';');
		fields[7] = row;
		AssertBadRequest(Encoding.UTF8.GetBytes(header + "\r\n" + string.Join(';', fields)));
	}

	[Theory]
	[InlineData("\n", true)]
	[InlineData("\n", false)]
	[InlineData("\r\n", true)]
	[InlineData("\r\n", false)]
	public void Parse_BomAndRecordTerminators_AcceptsOptionalBomAndFinalNewline(string newline, bool finalNewline)
	{
		ProjectSnapshot expected = PopulatedProject();
		string text = Encoding.UTF8.GetString(WorkPlanCsvCodec.Serialize(expected)).Replace("\r\n", newline);
		if (!finalNewline)
			text = text[..^newline.Length];
		byte[] payload = [0xEF, 0xBB, 0xBF, .. Encoding.UTF8.GetBytes(text)];

		ProjectSnapshot actual = WorkPlanCsvCodec.Parse(payload);

		Assert.Equal(expected, actual with { Items = expected.Items });
		Assert.Equal(expected.Items, actual.Items);
		Assert.Equal((byte)'S', WorkPlanCsvCodec.Serialize(actual)[0]);
	}

	[Theory]
	[InlineData(new byte[] { 0xC3, 0x28 })]
	[InlineData(new byte[] { 0xED, 0xA0, 0x80 })]
	[InlineData(new byte[] { 0xF0, 0x9F, 0x92 })]
	public void Parse_InvalidUtf8_ThrowsBadRequest(byte[] invalid)
	{
		string[] parts = Encoding.UTF8.GetString(WorkPlanCsvCodec.Serialize(EmptyProject())).Replace(";Project;Description;", ";MARKER;Description;").Split("MARKER");
		AssertBadRequest([.. Encoding.UTF8.GetBytes(parts[0]), .. invalid, .. Encoding.UTF8.GetBytes(parts[1])]);
	}

	[Fact]
	public void Serialize_InvalidUtf16_ThrowsBadRequest()
	{
		ProjectSnapshot snapshot = EmptyProject() with { Name = "unpaired\uD800" };
		Assert.Equal(400, Assert.Throws<PlanForgeException>(() => WorkPlanCsvCodec.Serialize(snapshot)).StatusCode);
	}

	[Theory]
	[InlineData(0, 0, "Version")]
	[InlineData(1, 0, "2")]
	[InlineData(2, 0, "2")]
	[InlineData(1, 1, "invalid")]
	[InlineData(1, 1, "00000000-0000-0000-0000-000000000000")]
	[InlineData(1, 4, "22222222-2222-2222-2222-222222222222")]
	[InlineData(2, 1, "99999999-9999-9999-9999-999999999999")]
	[InlineData(2, 2, "43")]
	[InlineData(1, 2, "-1")]
	[InlineData(1, 2, "9223372036854775808")]
	[InlineData(1, 2, " 42")]
	[InlineData(2, 4, "invalid")]
	[InlineData(2, 4, "66666666666666666666666666666666")]
	[InlineData(2, 4, "00000000-0000-0000-0000-000000000000")]
	[InlineData(2, 5, "invalid")]
	[InlineData(2, 13, "invalid")]
	[InlineData(2, 17, "invalid")]
	[InlineData(1, 11, "not-a-timestamp")]
	[InlineData(2, 18, "2026-11-03")]
	[InlineData(2, 6, "2147483648")]
	[InlineData(2, 6, " 0")]
	[InlineData(1, 5, "22222222-2222-2222-2222-222222222222")]
	[InlineData(1, 6, "0")]
	[InlineData(1, 12, "Inherit")]
	[InlineData(1, 15, "hidden data")]
	[InlineData(2, 10, "PlanningOnly")]
	[InlineData(2, 11, "2026-11-03T12:34:56.1234567+02:00")]
	[InlineData(2, 16, "true")]
	[InlineData(2, 16, "2")]
	public void Parse_InvalidSchemaOrTypedField_ThrowsBadRequest(int row, int column, string value)
	{
		string[] rows = SimpleRows();
		string[] fields = rows[row].Split(';');
		fields[column] = value;
		rows[row] = string.Join(';', fields);

		AssertBadRequest(Encoding.UTF8.GetBytes(string.Join("\r\n", rows)));
	}

	[Theory]
	[InlineData(0, false)]
	[InlineData(0, true)]
	[InlineData(1, false)]
	[InlineData(1, true)]
	[InlineData(2, false)]
	[InlineData(2, true)]
	public void Parse_IncorrectRowWidth_ThrowsBadRequest(int row, bool extraColumn)
	{
		string[] rows = SimpleRows();
		rows[row] = extraColumn ? rows[row] + ";extra" : rows[row][..rows[row].LastIndexOf(';')];
		AssertBadRequest(Encoding.UTF8.GetBytes(string.Join("\r\n", rows)));
	}

	[Theory]
	[InlineData("empty")]
	[InlineData("header-only")]
	[InlineData("missing-header")]
	[InlineData("missing-project")]
	[InlineData("duplicate-project")]
	[InlineData("blank-row")]
	public void Parse_MissingOrDuplicateMetadata_ThrowsBadRequest(string scenario)
	{
		string[] rows = SimpleRows();
		string text = scenario switch
		{
			"empty" => "",
			"header-only" => header,
			"missing-header" => string.Join("\r\n", rows.Skip(1)),
			"missing-project" => string.Join("\r\n", rows.Where((_, index) => index != 1)),
			"duplicate-project" => string.Join("\r\n", rows) + "\r\n" + rows[1],
			_ => string.Join("\r\n", rows) + "\r\n\r\n"
		};
		AssertBadRequest(Encoding.UTF8.GetBytes(text));
	}

	[Theory]
	[InlineData(1, 10, "Unknown")]
	[InlineData(1, 10, "0")]
	[InlineData(1, 9, "completed")]
	[InlineData(1, 9, "42")]
	[InlineData(2, 3, "Task")]
	[InlineData(2, 3, "0")]
	[InlineData(2, 9, "Completed")]
	[InlineData(2, 9, "InProgress")]
	[InlineData(2, 12, "Unknown")]
	[InlineData(2, 12, "0")]
	[InlineData(2, 14, "")]
	[InlineData(5, 15, "Invented")]
	[InlineData(3, 13, "")]
	[InlineData(4, 9, "Pending")]
	[InlineData(4, 5, "44444444-4444-4444-4444-444444444444")]
	[InlineData(5, 5, "99999999-9999-9999-9999-999999999999")]
	[InlineData(5, 4, "11111111-1111-1111-1111-111111111111")]
	public void Parse_UnsupportedEnumOrInvalidPlanSemantics_ThrowsBadRequest(int row, int column, string value)
	{
		string[] rows = SimpleRows();
		string[] fields = rows[row].Split(';');
		fields[column] = value;
		rows[row] = string.Join(';', fields);
		AssertBadRequest(Encoding.UTF8.GetBytes(string.Join("\r\n", rows)));
	}

	[Fact]
	public void Parse_DuplicatePlanRecord_ThrowsBadRequest()
	{
		string[] rows = SimpleRows();
		AssertBadRequest(Encoding.UTF8.GetBytes(string.Join("\r\n", rows) + "\r\n" + rows[2]));
	}

	[Theory]
	[InlineData("null")]
	[InlineData("empty-id")]
	[InlineData("negative-revision")]
	[InlineData("duplicate-id")]
	[InlineData("project-id-collision")]
	[InlineData("null-items")]
	[InlineData("null-item")]
	[InlineData("null-name")]
	[InlineData("null-description")]
	[InlineData("null-title")]
	[InlineData("null-resolution")]
	[InlineData("empty-agent-id")]
	[InlineData("empty-completedby-id")]
	[InlineData("empty-parent-id")]
	[InlineData("invalid-kind")]
	public void Serialize_MalformedSnapshot_ThrowsBadRequest(string scenario)
	{
		ProjectSnapshot snapshot = PopulatedProject();
		PlanItem first = snapshot.Items[0];
		snapshot = scenario switch
		{
			"null" => null!,
			"empty-id" => snapshot with { Id = Guid.Empty },
			"negative-revision" => snapshot with { Revision = -1 },
			"duplicate-id" => snapshot with { Items = [.. snapshot.Items, first] },
			"project-id-collision" => snapshot with { Id = first.Id },
			"null-items" => snapshot with { Items = null! },
			"null-item" => snapshot with { Items = [null!] },
			"null-name" => snapshot with { Name = null! },
			"null-description" => snapshot with { Description = null! },
			_ => snapshot with
			{
				Items = [first with
				{
					Title = scenario == "null-title" ? null! : first.Title,
					Resolution = scenario == "null-resolution" ? null! : first.Resolution,
					AssignedAgentId = scenario == "empty-agent-id" ? Guid.Empty : first.AssignedAgentId,
					CompletedBy = scenario == "empty-completedby-id" ? Guid.Empty : first.CompletedBy,
					ParentId = scenario == "empty-parent-id" ? Guid.Empty : first.ParentId,
					Kind = scenario == "invalid-kind" ? "Task" : first.Kind
				}, .. snapshot.Items.Skip(1)]
			}
		};
		Assert.Equal(400, Assert.Throws<PlanForgeException>(() => WorkPlanCsvCodec.Serialize(snapshot)).StatusCode);
	}

	[Fact]
	public void Parse_NullPayload_ThrowsBadRequest() => AssertBadRequest(null!);

	[Fact]
	public void Parse_PayloadAboveOneMebibyte_ThrowsPayloadTooLarge()
	{
		Assert.Equal(413, Assert.Throws<PlanForgeException>(() => WorkPlanCsvCodec.Parse(new byte[1024 * 1024 + 1])).StatusCode);
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public void RoundTrip_ExactlyOneMebibyte_AcceptsUtf8ByteBoundary(bool multibyte)
	{
		ProjectSnapshot snapshot = ProjectWithRecordCount(100);
		int padding = 1024 * 1024 - WorkPlanCsvCodec.Serialize(snapshot).Length;
		List<PlanItem> padded = [];
		foreach (PlanItem item in snapshot.Items)
		{
			int count = Math.Min(padding, 16000);
			string description = multibyte ? new string('é', count / 2) + new string('x', count % 2) : new string('x', count);
			padded.Add(item with { Description = description });
			padding -= count;
		}
		Assert.Equal(0, padding);
		snapshot = snapshot with { Items = padded };

		byte[] payload = WorkPlanCsvCodec.Serialize(snapshot);

		Assert.Equal(1024 * 1024, payload.Length);
		Assert.Equal(snapshot.Items, WorkPlanCsvCodec.Parse(payload).Items);
		Assert.Equal(413, Assert.Throws<PlanForgeException>(() => WorkPlanCsvCodec.Parse([0xEF, 0xBB, 0xBF, .. payload])).StatusCode);
		ProjectSnapshot oversized = snapshot with { Items = [.. snapshot.Items.SkipLast(1), snapshot.Items.Last() with { Description = "x" }] };
		Assert.Equal(413, Assert.Throws<PlanForgeException>(() => WorkPlanCsvCodec.Serialize(oversized)).StatusCode);
	}

	[Fact]
	public void RoundTrip_ExactlyFiveThousandDataRecords_PreservesEveryRecord()
	{
		ProjectSnapshot expected = ProjectWithRecordCount(5000);

		ProjectSnapshot actual = WorkPlanCsvCodec.Parse(WorkPlanCsvCodec.Serialize(expected));

		Assert.Equal(5000, actual.Items.Count);
		Assert.Equal(expected.Items, actual.Items);
	}

	[Fact]
	public void Parse_AboveFiveThousandDataRecords_ThrowsPayloadTooLargeBeforePlanValidation()
	{
		byte[] valid = WorkPlanCsvCodec.Serialize(ProjectWithRecordCount(5000));
		string extraRow = Encoding.UTF8.GetString(valid).Split("\r\n")[2] + "\r\n";
		byte[] payload = [.. valid, .. Encoding.UTF8.GetBytes(extraRow)];
		Assert.True(payload.Length < 1024 * 1024);

		Assert.Equal(413, Assert.Throws<PlanForgeException>(() => WorkPlanCsvCodec.Parse(payload)).StatusCode);
	}

	[Fact]
	public void Serialize_AboveFiveThousandDataRecords_ThrowsPayloadTooLarge()
	{
		Assert.Equal(413, Assert.Throws<PlanForgeException>(() => WorkPlanCsvCodec.Serialize(ProjectWithRecordCount(5001))).StatusCode);
	}

	private static ProjectSnapshot ProjectWithRecordCount(int records)
	{
		Guid phaseId = new(100, 0, 0, new byte[8]);
		Guid subphaseId = new(101, 0, 0, new byte[8]);
		List<PlanItem> items = [new(phaseId, "Phase", null, 0, "p"), new(subphaseId, "Subphase", phaseId, 0, "s")];
		for (int index = 0; index < records - 2; index++)
			items.Add(new PlanItem(new Guid(102 + index, 0, 0, new byte[8]), "Step", subphaseId, index, "s"));
		return EmptyProject() with { Items = items };
	}

	private static string[] SimpleRows() => Encoding.UTF8.GetString(WorkPlanCsvCodec.Serialize(PopulatedProject())).Split("\r\n", StringSplitOptions.RemoveEmptyEntries);

	private static void AssertBadRequest(byte[] payload) => Assert.Equal(400, Assert.Throws<PlanForgeException>(() => WorkPlanCsvCodec.Parse(payload)).StatusCode);

	private static ProjectSnapshot PopulatedProject()
	{
		Guid phaseId = Guid.Parse("22222222-2222-2222-2222-222222222222");
		Guid subphaseId = Guid.Parse("33333333-3333-3333-3333-333333333333");
		Guid stepId = Guid.Parse("44444444-4444-4444-4444-444444444444");
		Guid actorId = Guid.Parse("55555555-5555-5555-5555-555555555555");
		DateTimeOffset completedAt = DateTimeOffset.Parse("2026-11-02T23:59:58.7654321-07:00");
		return EmptyProject() with
		{
			Status = "Completed",
			Items = [
				new(Guid.Parse("66666666-6666-6666-6666-666666666666"), "Decision", stepId, 0, "Decision", "Reason", "Decided", "Unassigned", null, "Resolved", null, actorId, completedAt),
				new(phaseId, "Phase", null, 3, "Phase", "Phase description", "Completed", "Override", actorId, "", null, actorId, completedAt),
				new(subphaseId, "Subphase", phaseId, 5, "Subphase", "Subphase description", "Completed", "Inherit", null, "", null, actorId, completedAt),
				new(stepId, "Step", subphaseId, 7, "Step", "Step description", "Completed", "Unassigned", null, "", "MergeConfirmed", actorId, completedAt)
			]
		};
	}

	private static ProjectSnapshot EmptyProject() => new(
		Guid.Parse("11111111-1111-1111-1111-111111111111"), 42, "Project", "Description", "InProgress", "BranchPullRequest",
		DateTimeOffset.Parse("2026-11-03T12:34:56.1234567+02:00"), Array.Empty<PlanItem>());
}
