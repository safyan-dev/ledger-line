using Xunit;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using PaymentReconciliation.Tests.Infrastructure;

namespace PaymentReconciliation.Tests;

[Collection(LedgerlineCollection.Name)]
public sealed class PlansTests(LedgerlineAppFactory factory)
{
    [Fact]
    public async Task Plans_are_public_and_carry_features_and_limits()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/plans");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var plans = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(3, plans.GetArrayLength());

        var free = plans[0];
        Assert.Equal("free", free.GetProperty("code").GetString());
        Assert.Equal("Growth", plans[1].GetProperty("name").GetString());
        Assert.True(plans[1].GetProperty("recommended").GetBoolean());

        var members = free.GetProperty("limits")[0];
        Assert.Equal("team_members", members.GetProperty("key").GetString());
        Assert.Equal(2, members.GetProperty("value").GetInt32());

        var advanced = free.GetProperty("features")[1];
        Assert.Equal("reconciliation.advanced", advanced.GetProperty("key").GetString());
        Assert.False(advanced.GetProperty("included").GetBoolean());
    }
}
