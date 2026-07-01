using OnTime.Domain.Enums;

namespace OnTime.Application.Common;

public record BusinessSummaryCounts(int NewClients, int SalesCount, decimal Commission);

public record BusinessSummaryStageCount(string StageName, int Count);

public record BusinessSummaryGoalLine(
    GoalMetricType MetricType, GoalPeriod Period,
    decimal CurrentValue, decimal TargetValue, decimal ProgressPct,
    // Set when this goal's own period cycle closed inside the reported window — e.g. a Weekly
    // goal being reported in a monthly summary. Shows the final result of that finished cycle
    // separately from the ongoing live progress above.
    decimal? CompletedValue, decimal? CompletedTargetValue, decimal? CompletedPct);

/// <summary>
/// Small bilingual (pt-PT / en-US) HTML templates for transactional emails. Anything other than
/// "en-US" is treated as pt-PT — mirrors the frontend's two-locale i18n setup, no third language.
/// </summary>
public static class EmailTemplates
{
    private const string BrandColor = "#1677FF";
    private const string MutedColor = "#64748B";
    private const string BorderColor = "#E2E8F0";

    private static bool IsEnglish(string? locale) => locale == "en-US";

    public static string FriendRequestSubject(string? locale) =>
        IsEnglish(locale) ? "New friend request — OnTime" : "Novo pedido de amizade — OnTime";

    public static string FriendRequestBody(string? locale, string receiverName, string senderName) =>
        IsEnglish(locale)
            ? $"<p>Hi {receiverName},</p>" +
              $"<p><strong>{senderName}</strong> sent you a friend request on OnTime.</p>" +
              "<p>Open the app, under \"Friends\", to accept or decline.</p>"
            : $"<p>Olá {receiverName},</p>" +
              $"<p><strong>{senderName}</strong> enviou-te um pedido de amizade no OnTime.</p>" +
              "<p>Entra na aplicação, em \"Amigos\", para aceitar ou rejeitar.</p>";

    public static string DigestSubject(string? locale) =>
        IsEnglish(locale) ? "Your pending notifications — OnTime" : "As tuas notificações pendentes — OnTime";

    public static string DigestBody(string? locale, string userName, IReadOnlyList<string> titles)
    {
        var items = string.Join("", titles.Select(title => $"<li>{title}</li>"));
        return IsEnglish(locale)
            ? $"<p>Hi {userName},</p>" +
              $"<p>You have {titles.Count} pending notification(s):</p>" +
              $"<ul>{items}</ul>" +
              "<p>Open the app to see the details.</p>"
            : $"<p>Olá {userName},</p>" +
              $"<p>Tens {titles.Count} notificação(ões) pendente(s):</p>" +
              $"<ul>{items}</ul>" +
              "<p>Entra na aplicação para ver os detalhes.</p>";
    }

    // ── Business summary (weekly/monthly) ────────────────────────────────────

    public static string BusinessSummarySubject(string? locale, SummaryFrequency frequency)
    {
        var isWeekly = frequency == SummaryFrequency.Weekly;
        if (IsEnglish(locale)) return isWeekly ? "Your weekly summary — OnTime" : "Your monthly summary — OnTime";
        return isWeekly ? "O teu resumo semanal — OnTime" : "O teu resumo mensal — OnTime";
    }

    public static string BusinessSummaryBody(
        string? locale, string userName, DateTimeOffset periodStart, DateTimeOffset periodEnd,
        BusinessSummaryCounts? counts, IReadOnlyList<BusinessSummaryStageCount>? stageBreakdown,
        IReadOnlyList<BusinessSummaryGoalLine>? goals)
    {
        var en = IsEnglish(locale);
        var periodLabel = FormatPeriodRange(periodStart, periodEnd, en);

        var sections = new List<string>();
        if (counts is not null) sections.Add(CountsSection(counts, en));
        if (stageBreakdown is { Count: > 0 }) sections.Add(StageSection(stageBreakdown, en));
        if (goals is { Count: > 0 }) sections.Add(GoalsSection(goals, en));

        var greeting = en ? $"Hi {userName}," : $"Olá {userName},";
        var intro = en
            ? $"Here's your summary for <strong>{periodLabel}</strong>:"
            : $"Aqui está o teu resumo de <strong>{periodLabel}</strong>:";
        var footer = en ? "Open the app to see everything in detail." : "Entra na aplicação para ver tudo em detalhe.";

        // Sections separated by a hairline rule instead of just margin — much clearer visual
        // grouping in clients that under-render plain spacing (Gmail's condensed reading pane).
        var sectionsHtml = string.Join(
            $"""<hr style="border: none; border-top: 1px solid {BorderColor}; margin: 28px 0;" />""",
            sections);

        return $"""
            <div style="font-family: -apple-system, Segoe UI, Roboto, Arial, sans-serif; color: #1E293B; max-width: 560px; line-height: 1.5;">
              <p style="font-size: 15px; margin: 0 0 12px;">{greeting}</p>
              <p style="font-size: 15px; margin: 0 0 28px;">{intro}</p>
              {sectionsHtml}
              <p style="font-size: 13px; color: {MutedColor}; margin: 28px 0 0;">{footer}</p>
            </div>
            """;
    }

    private static string CountsSection(BusinessSummaryCounts c, bool en)
    {
        var stats = new (string label, string value)[]
        {
            (en ? "New clients" : "Novos clientes", c.NewClients.ToString()),
            (en ? "Sales" : "Vendas", c.SalesCount.ToString()),
            (en ? "Commission" : "Comissão", $"{c.Commission:N0} €"),
        };

        var cells = string.Join("", stats.Select(s => $"""
            <td style="padding: 18px 12px; text-align: center; border: 1px solid {BorderColor}; border-radius: 8px;">
              <div style="font-size: 22px; font-weight: 700; color: {BrandColor}; line-height: 1.2;">{s.value}</div>
              <div style="font-size: 12px; color: {MutedColor}; margin-top: 6px;">{s.label}</div>
            </td>
            """));

        return $"""
            <table role="presentation" style="width: 100%; border-collapse: separate; border-spacing: 8px 0;">
              <tr>{cells}</tr>
            </table>
            """;
    }

    private static string StageSection(IReadOnlyList<BusinessSummaryStageCount> stages, bool en)
    {
        var title = en ? "Client status" : "Estado dos clientes";
        var maxCount = Math.Max(1, stages.Max(s => s.Count));
        var rows = string.Join("", stages.Select(s =>
        {
            var widthPct = Math.Round((decimal)s.Count / maxCount * 100m, 0);
            return $"""
                <tr>
                  <td style="padding: 7px 10px 7px 0; font-size: 13px; white-space: nowrap;">{s.StageName}</td>
                  <td style="padding: 7px 0; width: 100%;">
                    <div style="background: #F1F5F9; border-radius: 4px; height: 10px; width: 100%;">
                      <div style="background: {BrandColor}; border-radius: 4px; height: 10px; width: {widthPct}%;"></div>
                    </div>
                  </td>
                  <td style="padding: 7px 0 7px 10px; font-size: 13px; text-align: right; color: {MutedColor};">{s.Count}</td>
                </tr>
                """;
        }));

        return $"""
            <div>
              <div style="font-size: 14px; font-weight: 600; margin-bottom: 12px;">{title}</div>
              <table role="presentation" style="width: 100%; border-collapse: collapse;">{rows}</table>
            </div>
            """;
    }

    private static string GoalsSection(IReadOnlyList<BusinessSummaryGoalLine> goals, bool en)
    {
        var title = en ? "Goals" : "Objetivos";
        var rows = string.Join($"""<div style="height: 18px;"></div>""", goals.Select(g =>
        {
            var metricLabel = GoalMetricLabel(g.MetricType, en);
            var periodLabel = GoalPeriodLabel(g.Period, en);
            var pct = Math.Min(g.ProgressPct, 100m);
            var completedLine = g.CompletedValue is not null
                ? $"""
                  <div style="font-size: 12px; color: {MutedColor}; margin-top: 6px;">
                    {(en ? "Last cycle finished at" : "Ciclo anterior terminou em")} {g.CompletedValue:N0}/{g.CompletedTargetValue:N0} ({g.CompletedPct:N0}%)
                  </div>
                  """
                : "";

            return $"""
                <div>
                  <table role="presentation" style="width: 100%; border-collapse: collapse; margin-bottom: 6px;">
                    <tr>
                      <td style="font-size: 13px; padding: 0;"><strong>{metricLabel}</strong> &middot; {periodLabel}</td>
                      <td style="font-size: 13px; color: {MutedColor}; text-align: right; padding: 0; white-space: nowrap;">{g.CurrentValue:N0}/{g.TargetValue:N0} ({pct:N0}%)</td>
                    </tr>
                  </table>
                  <div style="background: #F1F5F9; border-radius: 4px; height: 8px; width: 100%;">
                    <div style="background: {BrandColor}; border-radius: 4px; height: 8px; width: {pct}%;"></div>
                  </div>
                  {completedLine}
                </div>
                """;
        }));

        return $"""
            <div>
              <div style="font-size: 14px; font-weight: 600; margin-bottom: 14px;">{title}</div>
              {rows}
            </div>
            """;
    }

    private static string GoalMetricLabel(GoalMetricType type, bool en) => type switch
    {
        GoalMetricType.NewClients     => en ? "New clients" : "Novos clientes",
        GoalMetricType.Sales          => en ? "Sales" : "Vendas",
        GoalMetricType.Proposals      => en ? "Proposals" : "Propostas",
        GoalMetricType.ConversionRate => en ? "Conversion rate" : "Taxa de conversão",
        _                             => type.ToString(),
    };

    private static string GoalPeriodLabel(GoalPeriod period, bool en) => period switch
    {
        GoalPeriod.Daily   => en ? "Daily" : "Diário",
        GoalPeriod.Weekly  => en ? "Weekly" : "Semanal",
        GoalPeriod.Annual  => en ? "Annual" : "Anual",
        _                  => en ? "Monthly" : "Mensal",
    };

    private static string FormatPeriodRange(DateTimeOffset start, DateTimeOffset end, bool en)
    {
        var culture = System.Globalization.CultureInfo.GetCultureInfo(en ? "en-US" : "pt-PT");
        var lastDay = end.AddDays(-1);

        // Monthly window: start is day 1 and end is the 1st of the following month.
        if (start.Day == 1 && lastDay.Month != start.Month)
            return start.ToString("MMMM yyyy", culture);

        return en
            ? $"{start.ToString("MMM d", culture)} – {lastDay.ToString("MMM d, yyyy", culture)}"
            : $"{start.ToString("d MMM", culture)} – {lastDay.ToString("d MMM yyyy", culture)}";
    }
}
