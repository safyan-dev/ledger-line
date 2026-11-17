using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;
using PaymentReconciliation.Tests.Integration.Infrastructure;

namespace PaymentReconciliation.Tests.Integration;

[Collection(LedgerlineCollection.Name)]
public sealed class SupportTicketTests(LedgerlineAppFactory factory)
{
    [Fact]
    public async Task All_roles_can_create_view_and_reply()
    {
        var client = factory.CreateClient();
        var owner = await client.RegisterTenantAsync(NewSlug("supportroles"));
        var finance = await AddMemberAsync(client, owner, "Finance");

        var created = await CreateTicketAsync(client, owner, "Missing payout", "Payout did not arrive.");
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var createdBody = await created.Content.ReadFromJsonAsync<JsonElement>();
        var ticketId = createdBody.GetProperty("id").GetGuid();
        Assert.Equal("Open", createdBody.GetProperty("status").GetString());

        var list = await client.SendAsync(client.AuthorizedAs(
            HttpMethod.Get,
            "/api/support/tickets",
            finance.AccessToken,
            owner.TenantSlug));
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        var listBody = await list.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Contains(
            listBody.GetProperty("items").EnumerateArray(),
            item => item.GetProperty("id").GetGuid() == ticketId);

        var detail = await client.SendAsync(client.AuthorizedAs(
            HttpMethod.Get,
            $"/api/support/tickets/{ticketId}",
            finance.AccessToken,
            owner.TenantSlug));
        Assert.Equal(HttpStatusCode.OK, detail.StatusCode);

        var reply = await client.SendAsync(client.AuthorizedAs(
            HttpMethod.Post,
            $"/api/support/tickets/{ticketId}/messages",
            finance.AccessToken,
            owner.TenantSlug,
            JsonContent.Create(new { body = "Finance is investigating." })));
        Assert.Equal(HttpStatusCode.OK, reply.StatusCode);

        var updatedDetail = await client.SendAsync(client.Authorized(
            HttpMethod.Get,
            $"/api/support/tickets/{ticketId}",
            owner));
        var updatedBody = await updatedDetail.Content.ReadFromJsonAsync<JsonElement>();
        var messages = updatedBody.GetProperty("messages").EnumerateArray().ToList();
        Assert.Single(messages);
        Assert.Equal("Finance", messages[0].GetProperty("authorRole").GetString());
        Assert.Equal("Finance is investigating.", messages[0].GetProperty("body").GetString());
    }

    [Fact]
    public async Task Only_owner_can_change_ticket_status()
    {
        var client = factory.CreateClient();
        var owner = await client.RegisterTenantAsync(NewSlug("supportstatus"), "growth");
        var admin = await AddMemberAsync(client, owner, "TenantAdmin");
        var finance = await AddMemberAsync(client, owner, "Finance");

        var created = await CreateTicketAsync(client, admin, "Reconciliation mismatch", "Two payments do not match.");
        var ticketId = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        var adminStatus = await client.SendAsync(client.AuthorizedAs(
            HttpMethod.Patch,
            $"/api/support/tickets/{ticketId}/status",
            admin.AccessToken,
            owner.TenantSlug,
            JsonContent.Create(new { status = "InProgress" })));
        Assert.Equal(HttpStatusCode.Forbidden, adminStatus.StatusCode);

        var financeStatus = await client.SendAsync(client.AuthorizedAs(
            HttpMethod.Patch,
            $"/api/support/tickets/{ticketId}/status",
            finance.AccessToken,
            owner.TenantSlug,
            JsonContent.Create(new { status = "InProgress" })));
        Assert.Equal(HttpStatusCode.Forbidden, financeStatus.StatusCode);

        var inProgress = await ChangeStatusAsync(client, owner, ticketId, "InProgress");
        Assert.Equal(HttpStatusCode.OK, inProgress.StatusCode);

        var resolved = await ChangeStatusAsync(client, owner, ticketId, "Resolved");
        Assert.Equal(HttpStatusCode.OK, resolved.StatusCode);

        var closed = await ChangeStatusAsync(client, owner, ticketId, "Closed");
        Assert.Equal(HttpStatusCode.OK, closed.StatusCode);
        var closedBody = await closed.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Closed", closedBody.GetProperty("status").GetString());

        var replyOnClosed = await client.SendAsync(client.Authorized(
            HttpMethod.Post,
            $"/api/support/tickets/{ticketId}/messages",
            owner,
            JsonContent.Create(new { body = "Too late" })));
        Assert.Equal(HttpStatusCode.Conflict, replyOnClosed.StatusCode);
    }

    [Fact]
    public async Task Invalid_status_transition_is_rejected()
    {
        var client = factory.CreateClient();
        var owner = await client.RegisterTenantAsync(NewSlug("supporttransition"));
        var created = await CreateTicketAsync(client, owner, "Open ticket", "Cannot close directly.");
        var ticketId = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        var closed = await ChangeStatusAsync(client, owner, ticketId, "Closed");
        Assert.Equal(HttpStatusCode.Conflict, closed.StatusCode);
    }

    [Fact]
    public async Task Tickets_are_tenant_scoped()
    {
        var client = factory.CreateClient();
        var first = await client.RegisterTenantAsync(NewSlug("supportscopeA"));
        var second = await client.RegisterTenantAsync(NewSlug("supportscopeB"));

        var created = await CreateTicketAsync(client, first, "Private ticket", "Only first tenant.");
        var ticketId = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        var listed = await client.SendAsync(client.Authorized(
            HttpMethod.Get,
            "/api/support/tickets",
            second));
        var listedBody = await listed.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Empty(listedBody.GetProperty("items").EnumerateArray());

        var detail = await client.SendAsync(client.Authorized(
            HttpMethod.Get,
            $"/api/support/tickets/{ticketId}",
            second));
        Assert.Equal(HttpStatusCode.NotFound, detail.StatusCode);

        var reply = await client.SendAsync(client.Authorized(
            HttpMethod.Post,
            $"/api/support/tickets/{ticketId}/messages",
            second,
            JsonContent.Create(new { body = "Cross tenant" })));
        Assert.Equal(HttpStatusCode.NotFound, reply.StatusCode);

        var status = await ChangeStatusAsync(client, second, ticketId, "Resolved");
        Assert.Equal(HttpStatusCode.NotFound, status.StatusCode);
    }

    [Fact]
    public async Task Tickets_support_filtering_and_pagination()
    {
        var client = factory.CreateClient();
        var owner = await client.RegisterTenantAsync(NewSlug("supportfilter"));

        await CreateTicketAsync(client, owner, "Technical", "Technical issue.", category: "TechnicalIssue", priority: "Low");
        await CreateTicketAsync(client, owner, "Payment", "Payment issue.", category: "PaymentIssue", priority: "High");
        await CreateTicketAsync(client, owner, "Payment again", "Another payment issue.", category: "PaymentIssue", priority: "High");

        var filtered = await client.SendAsync(client.Authorized(
            HttpMethod.Get,
            "/api/support/tickets?category=PaymentIssue&priority=High&page=1&pageSize=1",
            owner));

        Assert.Equal(HttpStatusCode.OK, filtered.StatusCode);
        var body = await filtered.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(1, body.GetProperty("items").GetArrayLength());
        Assert.Equal(2, body.GetProperty("totalCount").GetInt32());
    }

    [Fact]
    public async Task Created_by_is_recorded()
    {
        var client = factory.CreateClient();
        var owner = await client.RegisterTenantAsync(NewSlug("supportcreator"));

        var created = await CreateTicketAsync(client, owner, "Creator test", "Creator fields.");
        var createdBody = await created.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(owner.UserId, createdBody.GetProperty("createdByUserId").GetGuid());
        Assert.False(string.IsNullOrWhiteSpace(createdBody.GetProperty("createdBy").GetString()));
    }

    [Fact]
    public async Task Invalid_ticket_input_is_rejected()
    {
        var client = factory.CreateClient();
        var owner = await client.RegisterTenantAsync(NewSlug("supportinvalid"));

        var badCategory = await CreateTicketAsync(
            client,
            owner,
            "Bad category",
            "Bad category.",
            category: "Nope",
            priority: "High");
        Assert.Equal(HttpStatusCode.BadRequest, badCategory.StatusCode);

        var badPriority = await CreateTicketAsync(
            client,
            owner,
            "Bad priority",
            "Bad priority.",
            category: "Other",
            priority: "Urgent");
        Assert.Equal(HttpStatusCode.BadRequest, badPriority.StatusCode);

        var emptySubject = await CreateTicketAsync(client, owner, "", "Empty subject.");
        Assert.Equal(HttpStatusCode.BadRequest, emptySubject.StatusCode);
    }

    private static string NewSlug(string prefix)
        => $"{prefix}-{Guid.NewGuid().ToString("N")[..10]}";

    private static string Email(TestSession session)
        => $"owner@{session.TenantSlug}.test";

    private static Task<HttpResponseMessage> CreateTicketAsync(
        HttpClient client,
        TestSession session,
        string subject,
        string description,
        string category = "Other",
        string priority = "Normal")
        => client.SendAsync(client.Authorized(
            HttpMethod.Post,
            "/api/support/tickets",
            session,
            JsonContent.Create(new { subject, description, category, priority })));

    private static Task<HttpResponseMessage> ChangeStatusAsync(
        HttpClient client,
        TestSession session,
        Guid ticketId,
        string status)
        => client.SendAsync(client.Authorized(
            HttpMethod.Patch,
            $"/api/support/tickets/{ticketId}/status",
            session,
            JsonContent.Create(new { status })));

    private async Task<TestSession> AddMemberAsync(HttpClient client, TestSession owner, string role)
    {
        var member = await client.RegisterTenantAsync(NewSlug("supportmember"));
        var add = await client.SendAsync(client.Authorized(
            HttpMethod.Post,
            "/api/tenants/current/members",
            owner,
            JsonContent.Create(new { email = Email(member), role })));
        add.EnsureSuccessStatusCode();

        return member with { TenantId = owner.TenantId, TenantSlug = owner.TenantSlug };
    }
}
