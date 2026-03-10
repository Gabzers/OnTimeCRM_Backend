using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

namespace OnTimeCRM.Tests.Infrastructure;

/// <summary>
/// Configures WireMock stubs to simulate Stripe and Ifthenpay external API calls.
/// Called once during TestWebAppFactory.InitializeAsync().
/// </summary>
public static class ExternalApiMocks
{
    public static void SetupStripeMocks(WireMockServer server)
    {
        // POST /v1/customers → create Stripe customer
        server.Given(Request.Create()
                .WithPath("/v1/customers")
                .UsingPost())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody("{\"id\":\"cus_test_fixture\",\"object\":\"customer\"}"));

        // POST /v1/payment_intents → create payment intent
        server.Given(Request.Create()
                .WithPath("/v1/payment_intents")
                .UsingPost())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody(@"{
                    ""id"":""pi_test_fixture"",
                    ""object"":""payment_intent"",
                    ""client_secret"":""pi_test_fixture_secret_test"",
                    ""status"":""requires_payment_method"",
                    ""amount"":1500
                }"));

        // GET /v1/payment_intents/{id} → query payment intent status
        server.Given(Request.Create()
                .WithPath("/v1/payment_intents/*")
                .UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody(@"{
                    ""id"":""pi_test_fixture"",
                    ""object"":""payment_intent"",
                    ""status"":""succeeded"",
                    ""amount"":1500
                }"));

        // POST /v1/payment_intents/{id}/confirm
        server.Given(Request.Create()
                .WithPath("/v1/payment_intents/*/confirm")
                .UsingPost())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody(@"{""id"":""pi_test_fixture"",""status"":""succeeded""}"));
    }

    public static void SetupIfthenpayMocks(WireMockServer server)
    {
        // MBWay — initiate payment
        // GET /mbway/set?... → initiate MBWay payment
        server.Given(Request.Create()
                .WithPath("/mbway/set")
                .UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody(@"{
                    ""RequestId"":""mbw_test_fixture_123"",
                    ""Status"":""000"",
                    ""Message"":""Pending""
                }"));

        // MBWay — query status
        server.Given(Request.Create()
                .WithPath("/mbway/getautoMBWayStatus")
                .UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody(@"{""Status"":""000"",""Message"":""Success""}"));

        // Multibanco — generate reference
        server.Given(Request.Create()
                .WithPath("/multibanco/setreference")
                .UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody($@"{{
                    ""Entity"":""11111"",
                    ""Reference"":""123 456 789"",
                    ""ExpiryDate"":""{DateTime.UtcNow.AddHours(72):yyyy-MM-dd HH:mm:ss}""
                }}"));

        // Generic fallback for any unmatched request — return 404 JSON
        // (This helps identify unexpected calls during tests)
    }
}
