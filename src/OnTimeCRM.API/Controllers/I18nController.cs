using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace OnTimeCRM.API.Controllers;

[ApiController]
[Route("api/i18n")]
[AllowAnonymous]
public class I18nController : ControllerBase
{
    private static readonly Dictionary<string, Dictionary<string, string>> _locales = new()
    {
        ["pt-PT"] = PtPT(),
        ["en-US"] = EnUS()
    };

    /// <summary>Returns localised key-value map. Version header allows client-side cache invalidation.</summary>
    [HttpGet]
    public IActionResult Get([FromQuery] string locale = "pt-PT")
    {
        if (!_locales.TryGetValue(locale, out var map))
            map = _locales["pt-PT"];

        // A fixed version string; bump this whenever translations change
        Response.Headers["X-I18n-Version"] = "20260308";

        return Ok(new { v = "20260308", locale, map });
    }

    // ── Portuguese ──────────────────────────────────────────────────────────
    private static Dictionary<string, string> PtPT() => new()
    {
        // ── Navigation ──────────────────────────────────────────────────────
        ["NAV.DASHBOARD"]           = "Dashboard",
        ["NAV.CLIENTS"]             = "Clientes",
        ["NAV.PROPOSALS"]           = "Propostas",
        ["NAV.SALES"]               = "Vendas",
        ["NAV.NOTIFICATIONS"]       = "Notificações",
        ["NAV.STAGES"]              = "Funil",
        ["NAV.VEHICLES"]            = "Veículos",
        ["NAV.TEAM"]                = "Equipa",
        ["NAV.BRANDS"]              = "Marcas",
        ["NAV.SUBSCRIPTION"]        = "Subscrição",
        ["NAV.SETTINGS"]            = "Definições",
        ["NAV.FRIENDS"]             = "Amigos",
        ["NAV.ADMIN"]               = "Administração",
        ["NAV.GOALS"]               = "Objetivos",
        ["NAV.ACCESS_CONTROL"]      = "Controlo de Acesso",

        // ── Labels — Client ─────────────────────────────────────────────────
        ["LABEL.CLIENT.FULL_NAME"]         = "Nome Completo",
        ["LABEL.CLIENT.EMAIL"]             = "E-mail",
        ["LABEL.CLIENT.PHONE"]             = "Telefone",
        ["LABEL.CLIENT.LEAD_SOURCE"]       = "Fonte de Lead",
        ["LABEL.CLIENT.CURRENT_STAGE"]     = "Etapa Atual",
        ["LABEL.CLIENT.TEMPERATURE"]       = "Temperatura",
        ["LABEL.CLIENT.NOTES"]             = "Notas",
        ["LABEL.CLIENT.REGISTERED_AT"]     = "Registado em",
        ["LABEL.CLIENT.LAST_INTERACTION"]  = "Última Interação",

        // ── Labels — Proposal ───────────────────────────────────────────────
        ["LABEL.PROPOSAL.DATE"]            = "Data da Proposta",
        ["LABEL.PROPOSAL.BUSINESS_TYPE"]   = "Tipo de Negócio",
        ["LABEL.PROPOSAL.PAYMENT_TYPE"]    = "Tipo de Pagamento",
        ["LABEL.PROPOSAL.VALUE"]           = "Valor",
        ["LABEL.PROPOSAL.DISCOUNT"]        = "Desconto",
        ["LABEL.PROPOSAL.TRADE_IN"]        = "Retoma",
        ["LABEL.PROPOSAL.STATUS"]          = "Estado",
        ["LABEL.PROPOSAL.VEHICLES"]        = "Veículos",
        ["LABEL.PROPOSAL.LOSS_REASON"]     = "Motivo de Perda",

        // ── Labels — Sale ───────────────────────────────────────────────────
        ["LABEL.SALE.DATE"]                = "Data da Venda",
        ["LABEL.SALE.VALUE"]               = "Valor da Venda",
        ["LABEL.SALE.VEHICLE"]             = "Veículo",
        ["LABEL.SALE.COMMISSION"]          = "Comissão",

        // ── Labels — Vehicle (campos adicionais) ─────────────────────────

        // ── Labels — Friend ─────────────────────────────────────────────────
        ["LABEL.FRIEND.NAME"]              = "Nome",
        ["LABEL.FRIEND.EMAIL"]             = "E-mail",
        ["LABEL.FRIEND.STATUS"]            = "Estado",

        // ── Labels — Notification ───────────────────────────────────────────
        ["LABEL.NOTIFICATION.TITLE"]       = "Título",
        ["LABEL.NOTIFICATION.DUE_AT"]      = "Data Prevista",
        ["LABEL.NOTIFICATION.STATUS"]      = "Estado",
        ["LABEL.NOTIFICATION.SNOOZE_UNTIL"]= "Adiar até",

        // ── Labels — Stage ──────────────────────────────────────────────────
        ["LABEL.STAGE.NAME"]               = "Nome da Etapa",
        ["LABEL.STAGE.COLOR"]              = "Cor",
        ["LABEL.STAGE.ORDER"]              = "Ordem",
        ["LABEL.STAGE.IS_FINAL"]           = "Etapa Final",
        ["LABEL.STAGE.IS_WON"]             = "Etapa Vencida",
        ["LABEL.STAGE.IS_LOST"]            = "Etapa Perdida",

        // ── Labels — Vehicle ────────────────────────────────────────────────
        ["LABEL.VEHICLE.BRAND"]            = "Marca",
        ["LABEL.VEHICLE.MODEL"]            = "Modelo",
        ["LABEL.VEHICLE.YEAR"]             = "Ano",
        ["LABEL.VEHICLE.FUEL_TYPE"]        = "Combustível",
        ["LABEL.VEHICLE.TRANSMISSION"]     = "Caixa",
        ["LABEL.VEHICLE.SEGMENT"]          = "Segmento",
        ["LABEL.VEHICLE.PRICE"]            = "Preço",
        ["LABEL.VEHICLE.DISCOUNT"]         = "Desconto",

        // ── Labels — Trade-In ───────────────────────────────────────────────
        ["LABEL.TRADE_IN.PLATE"]           = "Matrícula",
        ["LABEL.TRADE_IN.BRAND"]           = "Marca",
        ["LABEL.TRADE_IN.MODEL"]           = "Modelo",
        ["LABEL.TRADE_IN.YEAR"]            = "Ano",
        ["LABEL.TRADE_IN.KM"]              = "Quilómetros",
        ["LABEL.TRADE_IN.ESTIMATE"]        = "Estimativa",

        // ── Labels — User ───────────────────────────────────────────────────
        ["LABEL.USER.FULL_NAME"]           = "Nome",
        ["LABEL.USER.EMAIL"]               = "E-mail",
        ["LABEL.USER.PHONE"]               = "Telefone",
        ["LABEL.USER.ROLE"]                = "Função",
        ["LABEL.USER.STATUS"]              = "Estado da Conta",
        ["LABEL.USER.IS_ACTIVE"]           = "Ativo",
        ["LABEL.USER.COUNT"]               = "Utilizadores",

        ["LABEL.COMPANY.NAME"]             = "Nome da Empresa",

        ["LABEL.BRAND.NAME"]               = "Nome da Marca",
        ["LABEL.BRAND.PRIMARY_COLOR"]      = "Cor",
        ["LABEL.BRAND.COUNT"]              = "Marcas",

        ["LABEL.ACTIONS"]                  = "Ações",
        ["LABEL.CREATED_AT"]               = "Criado em",

        // ── Actions ─────────────────────────────────────────────────────────
        ["ACTION.SAVE"]                    = "Guardar",
        ["ACTION.CANCEL"]                  = "Cancelar",
        ["ACTION.DELETE"]                  = "Eliminar",
        ["ACTION.EDIT"]                    = "Editar",
        ["ACTION.ADD"]                     = "Adicionar",
        ["ACTION.CONFIRM"]                 = "Confirmar",
        ["ACTION.CLOSE"]                   = "Fechar",
        ["ACTION.BACK"]                    = "Voltar",
        ["ACTION.SEARCH"]                  = "Pesquisar",
        ["ACTION.FILTER"]                  = "Filtrar",
        ["ACTION.EXPORT"]                  = "Exportar",
        ["ACTION.LOGIN"]                   = "Entrar",
        ["ACTION.LOGOUT"]                  = "Sair",
        ["ACTION.REGISTER"]                = "Registar",
        ["ACTION.CREATE"]                   = "Criar",
        ["ACTION.SEND"]                     = "Enviar",

        ["ACTION.CLIENT.CREATE"]           = "Novo Cliente",
        ["ACTION.CLIENT.CHANGE_STAGE"]     = "Alterar Etapa",
        ["ACTION.CLIENT.DELETE"]           = "Eliminar Cliente",

        ["ACTION.PROPOSAL.CREATE"]         = "Nova Proposta",
        ["ACTION.PROPOSAL.CONVERT"]        = "Converter para Venda",
        ["ACTION.PROPOSAL.MARK_LOST"]      = "Marcar como Perdida",
        ["ACTION.PROPOSAL.EDIT"]           = "Editar Proposta",

        ["ACTION.STAGE.CHANGE"]            = "Alterar Etapa",

        ["ACTION.FRIEND.ADD"]              = "Adicionar Amigo",
        ["ACTION.FRIEND.ACCEPT"]           = "Aceitar",
        ["ACTION.FRIEND.REJECT"]           = "Rejeitar",
        ["ACTION.FRIEND.REMOVE"]           = "Remover Amigo",
        ["ACTION.FRIEND.VIEW_PROFILE"]     = "Ver Perfil",

        ["ACTION.NOTIFICATION.DONE"]       = "Concluída",
        ["ACTION.NOTIFICATION.SNOOZE"]     = "Adiar",
        ["ACTION.NOTIFICATION.IGNORE"]     = "Ignorar",
        ["ACTION.NOTIFICATION.CREATE"]     = "Nova Notificação",

        // ── Enum — Lead Source ──────────────────────────────────────────────
        ["ENUM.LEAD_SOURCE.0"]             = "Stand",
        ["ENUM.LEAD_SOURCE.1"]             = "Telefone",
        ["ENUM.LEAD_SOURCE.2"]             = "OLX",
        ["ENUM.LEAD_SOURCE.3"]             = "Standvirtual",
        ["ENUM.LEAD_SOURCE.4"]             = "Instagram",
        ["ENUM.LEAD_SOURCE.5"]             = "Facebook",
        ["ENUM.LEAD_SOURCE.6"]             = "Referência",
        ["ENUM.LEAD_SOURCE.7"]             = "Outro",

        // ── Enum — Deal Temperature ─────────────────────────────────────────
        ["ENUM.DEAL_TEMPERATURE.0"]        = "Quente",
        ["ENUM.DEAL_TEMPERATURE.1"]        = "Morno",
        ["ENUM.DEAL_TEMPERATURE.2"]        = "Frio",

        // ── Enum — Business Type ────────────────────────────────────────────
        ["ENUM.BUSINESS_TYPE.0"]           = "Compra Direta",
        ["ENUM.BUSINESS_TYPE.1"]           = "Retoma",
        ["ENUM.BUSINESS_TYPE.2"]           = "Retoma c/ Diferença",
        ["ENUM.BUSINESS_TYPE.3"]           = "Leasing",
        ["ENUM.BUSINESS_TYPE.4"]           = "Financiamento",

        // ── Enum — Payment Type ─────────────────────────────────────────────
        ["ENUM.PAYMENT_TYPE.0"]            = "Dinheiro",
        ["ENUM.PAYMENT_TYPE.1"]            = "Financiamento",
        ["ENUM.PAYMENT_TYPE.2"]            = "Leasing",
        ["ENUM.PAYMENT_TYPE.3"]            = "Transferência Bancária",
        ["ENUM.PAYMENT_TYPE.4"]            = "Outro",

        // ── Enum — Proposal Status ──────────────────────────────────────────
        ["ENUM.PROPOSAL_STATUS.0"]         = "Ativa",
        ["ENUM.PROPOSAL_STATUS.1"]         = "Ganha",
        ["ENUM.PROPOSAL_STATUS.2"]         = "Perdida",
        ["ENUM.PROPOSAL_STATUS.3"]         = "Cancelada",

        // ── Enum — Notification Status ──────────────────────────────────────
        ["ENUM.NOTIFICATION_STATUS.0"]     = "Pendente",
        ["ENUM.NOTIFICATION_STATUS.1"]     = "Concluída",
        ["ENUM.NOTIFICATION_STATUS.2"]     = "Ignorada",
        ["ENUM.NOTIFICATION_STATUS.3"]     = "Adiada",

        // ── Enum — Notification Trigger ─────────────────────────────────────
        ["ENUM.NOTIFICATION_TRIGGER.0"]    = "Manual",
        ["ENUM.NOTIFICATION_TRIGGER.1"]    = "Mudança de Etapa",
        ["ENUM.NOTIFICATION_TRIGGER.2"]    = "Fecho de Venda",
        ["ENUM.NOTIFICATION_TRIGGER.3"]    = "Nova Proposta",
        ["ENUM.NOTIFICATION_TRIGGER.4"]    = "Personalizado",

        // ── Enum — User Role ────────────────────────────────────────────────
        ["ENUM.USER_ROLE.0"]               = "Comercial",
        ["ENUM.USER_ROLE.1"]               = "Gestor",
        ["ENUM.USER_ROLE.2"]               = "Administrador",

        // ── Enum — Account Status ───────────────────────────────────────────
        ["ENUM.ACCOUNT_STATUS.0"]          = "Pendente de Ativação",
        ["ENUM.ACCOUNT_STATUS.1"]          = "Ativo",
        ["ENUM.ACCOUNT_STATUS.2"]          = "Expirado",
        ["ENUM.ACCOUNT_STATUS.3"]          = "Inativo",
        ["ENUM.ACCOUNT_STATUS.4"]          = "Suspenso",
        ["ENUM.ACCOUNT_STATUS.5"]          = "Cancelado",

        // ── Enum — Loss Reason ──────────────────────────────────────────────
        ["ENUM.LOSS_REASON.0"]             = "Preço",
        ["ENUM.LOSS_REASON.1"]             = "Concorrência",
        ["ENUM.LOSS_REASON.2"]             = "Indisponibilidade",
        ["ENUM.LOSS_REASON.3"]             = "Sem Resposta",
        ["ENUM.LOSS_REASON.4"]             = "Financiamento Recusado",
        ["ENUM.LOSS_REASON.5"]             = "Mudou de Ideias",
        ["ENUM.LOSS_REASON.6"]             = "Outro",

        // ── Enum — Friendship Status ─────────────────────────────────────────
        ["ENUM.FRIENDSHIP_STATUS.0"]        = "Pendente",
        ["ENUM.FRIENDSHIP_STATUS.1"]        = "Aceite",
        ["ENUM.FRIENDSHIP_STATUS.2"]        = "Rejeitado",
        ["ENUM.FRIENDSHIP_STATUS.3"]        = "Bloqueado",

        // ── Account & Subscription Status (short-key aliases) ───────────────
        ["ACCOUNT.STATUS.0"]               = "Pendente de Ativação",
        ["ACCOUNT.STATUS.1"]               = "Ativo",
        ["ACCOUNT.STATUS.2"]               = "Expirado",
        ["ACCOUNT.STATUS.3"]               = "Inativo",
        ["ACCOUNT.STATUS.4"]               = "Suspenso",
        ["ACCOUNT.STATUS.5"]               = "Cancelado",
        ["SUBSCRIPTION.STATUS.0"]          = "Trial",
        ["SUBSCRIPTION.STATUS.1"]          = "Ativo",
        ["SUBSCRIPTION.STATUS.2"]          = "Em Atraso",
        ["SUBSCRIPTION.STATUS.3"]          = "Cancelado",
        ["SUBSCRIPTION.STATUS.4"]          = "Expirado",

        // ── Enum — Fuel Type ────────────────────────────────────────────────
        ["ENUM.FUEL_TYPE.0"]               = "Gasolina",
        ["ENUM.FUEL_TYPE.1"]               = "Diesel",
        ["ENUM.FUEL_TYPE.2"]               = "Elétrico",
        ["ENUM.FUEL_TYPE.3"]               = "Híbrido",
        ["ENUM.FUEL_TYPE.4"]               = "Híbrido Plug-in",
        ["ENUM.FUEL_TYPE.5"]               = "GPL",

        // ── Enum — Transmission ─────────────────────────────────────────────
        ["ENUM.TRANSMISSION.0"]            = "Manual",
        ["ENUM.TRANSMISSION.1"]            = "Automática",

        // ── Enum — Vehicle Segment ──────────────────────────────────────────
        ["ENUM.VEHICLE_SEGMENT.0"]         = "Citadino",
        ["ENUM.VEHICLE_SEGMENT.1"]         = "Berlina",
        ["ENUM.VEHICLE_SEGMENT.2"]         = "SUV",
        ["ENUM.VEHICLE_SEGMENT.3"]         = "Monovolume",
        ["ENUM.VEHICLE_SEGMENT.4"]         = "Comercial",
        ["ENUM.VEHICLE_SEGMENT.5"]         = "Desportivo",
        ["ENUM.VEHICLE_SEGMENT.6"]         = "Outro",

        // ── Page titles ──────────────────────────────────────────────────────
        ["PAGE.DASHBOARD.TITLE"]           = "Dashboard",
        ["PAGE.CLIENTS.TITLE"]             = "Clientes",
        ["PAGE.PROPOSALS.TITLE"]           = "Propostas",
        ["PAGE.SALES.TITLE"]               = "Vendas",
        ["PAGE.NOTIFICATIONS.TITLE"]       = "Notificações",
        ["PAGE.STAGES.TITLE"]              = "Configurar Pipeline",
        ["PAGE.VEHICLES.TITLE"]            = "Veículos",
        ["PAGE.TEAM.TITLE"]                = "Equipa",
        ["PAGE.BRANDS.TITLE"]              = "Marcas do Stand",
        ["PAGE.SUBSCRIPTION.TITLE"]        = "Subscrição",
        ["PAGE.PROFILE.TITLE"]              = "O Meu Perfil",
        ["PAGE.FRIENDS.TITLE"]              = "Amigos",
        ["PAGE.ADMIN.TITLE"]                = "Painel de Administração",
        ["PAGE.PIPELINE.TITLE"]             = "Configurar Pipeline",
        ["PAGE.GOALS.TITLE"]                = "Os Meus Objetivos",
        ["PAGE.ACCESS_CONTROL.TITLE"]       = "Controlo de Acesso",

        // ── Dashboard KPIs ───────────────────────────────────────────────────
        ["KPI.ACTIVE_CLIENTS"]             = "Clientes Ativos",
        ["KPI.PROPOSALS_MONTH"]            = "Propostas Este Mês",
        ["KPI.SALES_MONTH"]                = "Vendas Este Mês",
        ["KPI.CONVERSION_RATE"]            = "Taxa de Conversão",
        ["KPI.AVG_SALE_VALUE"]             = "Valor Médio de Venda",
        ["KPI.HOT_DEALS"]                  = "Negócios Quentes",
        ["KPI.COMMISSION_MONTH"]           = "Comissão (mês)",

        // ── Messages ────────────────────────────────────────────────────────
        ["MSG.DELETE_CONFIRM"]             = "Tem a certeza que deseja eliminar?",
        ["MSG.DELETE_STAGE_BLOCKED"]       = "Não é possível eliminar uma etapa com clientes ativos.",
        ["MSG.EMPTY_STATE.CLIENTS"]        = "Nenhum cliente encontrado.",
        ["MSG.EMPTY_STATE.PROPOSALS"]      = "Sem propostas.",
        ["MSG.EMPTY_STATE.SALES"]          = "Sem vendas registadas.",
        ["MSG.EMPTY_STATE.NOTIFICATIONS"]  = "Sem notificações.",
        ["MSG.EMPTY_STATE.DEFAULT"]         = "Sem resultados.",
        ["MSG.SAVED"]                      = "Guardado com sucesso.",
        ["MSG.SUCCESS"]                    = "Operação realizada com sucesso.",
        ["MSG.DELETED"]                    = "Eliminado com sucesso.",
        ["MSG.ERROR.GENERIC"]              = "Ocorreu um erro. Por favor tente novamente.",
        ["VALIDATION.REQUIRED"]            = "Campo obrigatório.",
        ["MSG.ERROR.UNAUTHORIZED"]         = "Sessão expirada. Por favor inicie sessão novamente.",
        ["MSG.ERROR.FORBIDDEN"]            = "Não tem permissão para realizar esta ação.",
        ["MSG.ERROR.NOT_FOUND"]            = "Registo não encontrado.",
        ["MSG.SUBSCRIPTION.REQUIRED"]      = "A sua subscrição expirou. Por favor renove para continuar.",
        ["MSG.CLIENT.CREATED"]             = "Cliente criado com sucesso.",
        ["MSG.EMPTY_STATE.FRIENDS"]        = "Ainda não tens amigos. Adiciona alguém pelo e-mail!",
        ["MSG.FRIEND.REQUEST_SENT"]        = "Pedido de amizade enviado.",
        ["MSG.FRIEND.ACCEPTED"]            = "Pedido aceite.",
        ["MSG.FRIEND.REJECTED"]            = "Pedido rejeitado.",
        ["MSG.FRIEND.REMOVED"]             = "Amigo removido.",
        ["MSG.FRIEND.NOT_FOUND"]           = "Utilizador não encontrado com esse e-mail.",
        ["MSG.FRIEND.ALREADY_FRIENDS"]     = "Já são amigos.",

        // ── Hints ───────────────────────────────────────────────────────────
        ["HINT.PROPOSAL.SOLD_AT"]          = "Quando foi concretizada a venda? (pode ser retroativo)",
        ["HINT.PROPOSAL.CONVERT"]         = "Ao converter cria automaticamente uma venda e move o cliente para a etapa Venda.",

        // ── Goals ───────────────────────────────────────────────────────────────────────
        ["LABEL.GOAL.METRIC_TYPE"]          = "Métrica",
        ["LABEL.GOAL.METRIC"]               = "Métrica",
        ["LABEL.GOAL.PERIOD"]               = "Período",
        ["LABEL.GOAL.TARGET_VALUE"]         = "Valor Alvo",
        ["LABEL.GOAL.TARGET"]               = "Valor Alvo",
        ["LABEL.GOAL.START_DATE"]           = "Data de Início",
        ["LABEL.GOAL.END_DATE"]             = "Data de Fim",
        ["LABEL.GOAL.PROGRESS"]             = "Progresso",
        ["ENUM.GOAL_METRIC_TYPE.0"]         = "Novos Clientes",
        ["ENUM.GOAL_METRIC_TYPE.1"]         = "Vendas",
        ["ENUM.GOAL_METRIC_TYPE.2"]         = "Propostas",
        ["ENUM.GOAL_METRIC_TYPE.3"]         = "Taxa de Conversão",
        ["ENUM.GOAL_PERIOD.0"]              = "Diário",
        ["ENUM.GOAL_PERIOD.1"]              = "Semanal",
        ["ENUM.GOAL_PERIOD.2"]              = "Mensal",
        ["ACTION.GOAL.CREATE"]              = "Novo Objetivo",
        ["ACTION.GOAL.EDIT"]                = "Editar Objetivo",
        ["MSG.GOAL.CREATED"]                = "Objetivo criado com sucesso.",
        ["MSG.GOAL.UPDATED"]                = "Objetivo atualizado com sucesso.",
        ["MSG.GOAL.DELETED"]                = "Objetivo eliminado.",
        ["MSG.EMPTY_STATE.GOALS"]           = "Nenhum objetivo definido. Crie o seu primeiro objetivo!",

        // ── Access Control ──────────────────────────────────────────────────────
        ["LABEL.PERMISSION.ROUTE"]          = "Módulo",
        ["LABEL.PERMISSION.CAN_READ"]       = "Leitura",
        ["LABEL.PERMISSION.CAN_VIEW"]       = "Ver",
        ["LABEL.PERMISSION.CAN_CREATE"]     = "Criar",
        ["LABEL.PERMISSION.CAN_EDIT"]       = "Edição",
        ["LABEL.PERMISSION.CAN_DELETE"]     = "Eliminar",
        ["MSG.PERMISSIONS.SAVED"]           = "Permissões guardadas com sucesso.",
        ["ACTION.SAVE_PERMISSIONS"]         = "Guardar Permissões",

        // ── Notification Settings Page ───────────────────────────────────────
        ["NAV.NOTIFICATION_SETTINGS"]       = "Config. Notificações",
        ["PAGE.NOTIFICATION_SETTINGS.TITLE"]= "Configuração de Notificações",
        ["LABEL.NOTIF_SETTINGS.GENERAL"]              = "Configurações Gerais",
        ["LABEL.NOTIF_SETTINGS.DIGEST_FREQUENCY"]    = "Frequência do Resumo (dias)",
        ["LABEL.NOTIF_SETTINGS.SALE_FOLLOWUP"]       = "Follow-up Pós-Venda (dias)",
        ["LABEL.NOTIF_SETTINGS.STAGE_CHANGE_ENABLED"]= "Notificações de Mudança de Etapa",
        ["LABEL.NOTIF_SETTINGS.SALE_NOTIFS_ENABLED"] = "Notificações de Venda",
        ["LABEL.NOTIF_SETTINGS.STAGE"]               = "Etapa",
        ["LABEL.NOTIF_SETTINGS.DAYS_AFTER"]          = "Dias Após",
        ["LABEL.NOTIF_SETTINGS.NEW_CLIENT_DAYS"]     = "Dias após novo cliente",
        ["LABEL.NOTIF_SETTINGS.NEW_CLIENT_TIME"]     = "Hora da notificação",
        ["LABEL.NOTIF_SETTINGS.STAGE_TEMPLATES"]     = "Notificações por Etapa",
        ["LABEL.NOTIF_SETTINGS.OVERRIDE_NEW_CLIENT"] = "Sobrescrever notif. de novo cliente?",
        ["ACTION.TEMPLATE.ADD"]             = "Adicionar Template",
        ["ACTION.TEMPLATE.EDIT"]            = "Editar Template",
        ["ACTION.TEMPLATE.DELETE"]          = "Eliminar Template",
        ["MSG.NOTIF_SETTINGS.SAVED"]        = "Configurações de notificações guardadas.",
    };

    // ── English ─────────────────────────────────────────────────────────────
    private static Dictionary<string, string> EnUS() => new()
    {
        ["NAV.DASHBOARD"]           = "Dashboard",
        ["NAV.CLIENTS"]             = "Clients",
        ["NAV.PROPOSALS"]           = "Proposals",
        ["NAV.SALES"]               = "Sales",
        ["NAV.NOTIFICATIONS"]       = "Notifications",
        ["NAV.STAGES"]              = "Pipeline",
        ["NAV.VEHICLES"]            = "Vehicles",
        ["NAV.TEAM"]                = "Team",
        ["NAV.BRANDS"]              = "Brands",
        ["NAV.SUBSCRIPTION"]        = "Subscription",
        ["NAV.SETTINGS"]            = "Settings",
        ["NAV.FRIENDS"]             = "Friends",
        ["NAV.ADMIN"]               = "Administration",
        ["NAV.GOALS"]               = "Goals",
        ["NAV.ACCESS_CONTROL"]      = "Access Control",

        ["PAGE.GOALS.TITLE"]                = "My Goals",
        ["PAGE.ACCESS_CONTROL.TITLE"]       = "Access Control",

        ["LABEL.GOAL.METRIC_TYPE"]          = "Metric",
        ["LABEL.GOAL.METRIC"]               = "Metric",
        ["LABEL.GOAL.PERIOD"]               = "Period",
        ["LABEL.GOAL.TARGET_VALUE"]         = "Target Value",
        ["LABEL.GOAL.TARGET"]               = "Target Value",
        ["LABEL.GOAL.START_DATE"]           = "Start Date",
        ["LABEL.GOAL.END_DATE"]             = "End Date",
        ["LABEL.GOAL.PROGRESS"]             = "Progress",
        ["ENUM.GOAL_METRIC_TYPE.0"]         = "New Clients",
        ["ENUM.GOAL_METRIC_TYPE.1"]         = "Sales",
        ["ENUM.GOAL_METRIC_TYPE.2"]         = "Proposals",
        ["ENUM.GOAL_METRIC_TYPE.3"]         = "Conversion Rate",
        ["ENUM.GOAL_PERIOD.0"]              = "Daily",
        ["ENUM.GOAL_PERIOD.1"]              = "Weekly",
        ["ENUM.GOAL_PERIOD.2"]              = "Monthly",
        ["ACTION.GOAL.CREATE"]              = "New Goal",
        ["ACTION.GOAL.EDIT"]                = "Edit Goal",
        ["MSG.GOAL.CREATED"]                = "Goal created successfully.",
        ["MSG.GOAL.UPDATED"]                = "Goal updated successfully.",
        ["MSG.GOAL.DELETED"]                = "Goal deleted.",
        ["MSG.EMPTY_STATE.GOALS"]           = "No goals defined. Create your first goal!",
        ["LABEL.PERMISSION.ROUTE"]          = "Module",
        ["LABEL.PERMISSION.CAN_READ"]       = "Read",
        ["LABEL.PERMISSION.CAN_VIEW"]       = "View",
        ["LABEL.PERMISSION.CAN_CREATE"]     = "Create",
        ["LABEL.PERMISSION.CAN_EDIT"]       = "Edit",
        ["LABEL.PERMISSION.CAN_DELETE"]     = "Delete",
        ["MSG.PERMISSIONS.SAVED"]           = "Permissions saved successfully.",
        ["ACTION.SAVE_PERMISSIONS"]         = "Save Permissions",

        ["LABEL.CLIENT.FULL_NAME"]         = "Full Name",
        ["LABEL.CLIENT.EMAIL"]             = "Email",
        ["LABEL.CLIENT.PHONE"]             = "Phone",
        ["LABEL.CLIENT.LEAD_SOURCE"]       = "Lead Source",
        ["LABEL.CLIENT.CURRENT_STAGE"]     = "Current Stage",
        ["LABEL.CLIENT.TEMPERATURE"]       = "Temperature",
        ["LABEL.CLIENT.NOTES"]             = "Notes",
        ["LABEL.CLIENT.REGISTERED_AT"]     = "Registered At",
        ["LABEL.CLIENT.LAST_INTERACTION"]  = "Last Interaction",

        ["LABEL.PROPOSAL.DATE"]            = "Proposal Date",
        ["LABEL.PROPOSAL.BUSINESS_TYPE"]   = "Business Type",
        ["LABEL.PROPOSAL.PAYMENT_TYPE"]    = "Payment Type",
        ["LABEL.PROPOSAL.VALUE"]           = "Value",
        ["LABEL.PROPOSAL.DISCOUNT"]        = "Discount",
        ["LABEL.PROPOSAL.TRADE_IN"]        = "Trade-In",
        ["LABEL.PROPOSAL.STATUS"]          = "Status",
        ["LABEL.PROPOSAL.VEHICLES"]        = "Vehicles",
        ["LABEL.PROPOSAL.LOSS_REASON"]     = "Loss Reason",

        ["LABEL.SALE.DATE"]                = "Sale Date",
        ["LABEL.SALE.VALUE"]               = "Sale Value",
        ["LABEL.SALE.VEHICLE"]             = "Vehicle",
        ["LABEL.SALE.COMMISSION"]          = "Commission",

        ["LABEL.VEHICLE.PRICE"]            = "Price",
        ["LABEL.VEHICLE.DISCOUNT"]         = "Discount",

        ["LABEL.FRIEND.NAME"]              = "Name",
        ["LABEL.FRIEND.EMAIL"]             = "Email",
        ["LABEL.FRIEND.STATUS"]            = "Status",

        ["LABEL.USER.FULL_NAME"]           = "Full Name",
        ["LABEL.USER.EMAIL"]               = "Email",
        ["LABEL.USER.PHONE"]               = "Phone",
        ["LABEL.USER.ROLE"]                = "Role",
        ["LABEL.USER.STATUS"]              = "Account Status",
        ["LABEL.USER.IS_ACTIVE"]           = "Active",
        ["LABEL.USER.COUNT"]               = "Users",

        ["LABEL.COMPANY.NAME"]             = "Company Name",

        ["LABEL.BRAND.NAME"]               = "Brand Name",
        ["LABEL.BRAND.PRIMARY_COLOR"]      = "Color",
        ["LABEL.BRAND.COUNT"]              = "Brands",

        ["LABEL.ACTIONS"]                  = "Actions",
        ["LABEL.CREATED_AT"]               = "Created At",

        ["ACTION.SAVE"]                    = "Save",
        ["ACTION.CANCEL"]                  = "Cancel",
        ["ACTION.DELETE"]                  = "Delete",
        ["ACTION.EDIT"]                    = "Edit",
        ["ACTION.ADD"]                     = "Add",
        ["ACTION.CONFIRM"]                 = "Confirm",
        ["ACTION.CLOSE"]                   = "Close",
        ["ACTION.BACK"]                    = "Back",
        ["ACTION.LOGIN"]                   = "Sign In",
        ["ACTION.LOGOUT"]                  = "Sign Out",
        ["ACTION.CREATE"]                  = "Create",
        ["ACTION.SEND"]                    = "Send",

        ["ACTION.PROPOSAL.CONVERT"]        = "Convert to Sale",
        ["ACTION.PROPOSAL.MARK_LOST"]      = "Mark as Lost",

        ["ACTION.STAGE.CHANGE"]            = "Change Stage",

        ["ACTION.FRIEND.ADD"]              = "Add Friend",
        ["ACTION.FRIEND.ACCEPT"]           = "Accept",
        ["ACTION.FRIEND.REJECT"]           = "Reject",
        ["ACTION.FRIEND.REMOVE"]           = "Remove Friend",
        ["ACTION.FRIEND.VIEW_PROFILE"]     = "View Profile",

        ["ENUM.LEAD_SOURCE.0"]             = "Showroom",
        ["ENUM.LEAD_SOURCE.1"]             = "Phone",
        ["ENUM.LEAD_SOURCE.2"]             = "OLX",
        ["ENUM.LEAD_SOURCE.3"]             = "Standvirtual",
        ["ENUM.LEAD_SOURCE.4"]             = "Instagram",
        ["ENUM.LEAD_SOURCE.5"]             = "Facebook",
        ["ENUM.LEAD_SOURCE.6"]             = "Referral",
        ["ENUM.LEAD_SOURCE.7"]             = "Other",

        ["ENUM.DEAL_TEMPERATURE.0"]        = "Hot",
        ["ENUM.DEAL_TEMPERATURE.1"]        = "Warm",
        ["ENUM.DEAL_TEMPERATURE.2"]        = "Cold",

        ["ENUM.BUSINESS_TYPE.0"]           = "Direct Purchase",
        ["ENUM.BUSINESS_TYPE.1"]           = "Trade-In",
        ["ENUM.BUSINESS_TYPE.2"]           = "Trade-In with Difference",
        ["ENUM.BUSINESS_TYPE.3"]           = "Leasing",
        ["ENUM.BUSINESS_TYPE.4"]           = "Financing",

        ["ENUM.PAYMENT_TYPE.0"]            = "Cash",
        ["ENUM.PAYMENT_TYPE.1"]            = "Financing",
        ["ENUM.PAYMENT_TYPE.2"]            = "Leasing",
        ["ENUM.PAYMENT_TYPE.3"]            = "Bank Transfer",
        ["ENUM.PAYMENT_TYPE.4"]            = "Other",

        ["ENUM.PROPOSAL_STATUS.0"]         = "Active",
        ["ENUM.PROPOSAL_STATUS.1"]         = "Won",
        ["ENUM.PROPOSAL_STATUS.2"]         = "Lost",
        ["ENUM.PROPOSAL_STATUS.3"]         = "Cancelled",

        ["ENUM.NOTIFICATION_STATUS.0"]     = "Pending",
        ["ENUM.NOTIFICATION_STATUS.1"]     = "Done",
        ["ENUM.NOTIFICATION_STATUS.2"]     = "Ignored",
        ["ENUM.NOTIFICATION_STATUS.3"]     = "Snoozed",

        ["ENUM.USER_ROLE.0"]               = "Salesperson",
        ["ENUM.USER_ROLE.1"]               = "Manager",
        ["ENUM.USER_ROLE.2"]               = "Admin",

        ["ENUM.ACCOUNT_STATUS.0"]          = "Pending Activation",
        ["ENUM.ACCOUNT_STATUS.1"]          = "Active",
        ["ENUM.ACCOUNT_STATUS.2"]          = "Expired",
        ["ENUM.ACCOUNT_STATUS.3"]          = "Inactive",
        ["ENUM.ACCOUNT_STATUS.4"]          = "Suspended",
        ["ENUM.ACCOUNT_STATUS.5"]          = "Cancelled",

        ["ENUM.LOSS_REASON.0"]             = "Price",
        ["ENUM.LOSS_REASON.1"]             = "Competition",
        ["ENUM.LOSS_REASON.2"]             = "Unavailability",
        ["ENUM.LOSS_REASON.3"]             = "No Response",
        ["ENUM.LOSS_REASON.4"]             = "Finance Refused",
        ["ENUM.LOSS_REASON.5"]             = "Changed Mind",
        ["ENUM.LOSS_REASON.6"]             = "Other",

        ["ENUM.FRIENDSHIP_STATUS.0"]        = "Pending",
        ["ENUM.FRIENDSHIP_STATUS.1"]        = "Accepted",
        ["ENUM.FRIENDSHIP_STATUS.2"]        = "Rejected",
        ["ENUM.FRIENDSHIP_STATUS.3"]        = "Blocked",

        ["KPI.ACTIVE_CLIENTS"]             = "Active Clients",
        ["KPI.PROPOSALS_MONTH"]            = "Proposals This Month",
        ["KPI.SALES_MONTH"]                = "Sales This Month",
        ["KPI.CONVERSION_RATE"]            = "Conversion Rate",
        ["KPI.AVG_SALE_VALUE"]             = "Average Sale Value",
        ["KPI.HOT_DEALS"]                  = "Hot Deals",
        ["KPI.COMMISSION_MONTH"]           = "Commission (month)",

        ["MSG.DELETE_CONFIRM"]             = "Are you sure you want to delete this?",
        ["MSG.SAVED"]                      = "Saved successfully.",
        ["MSG.SUCCESS"]                    = "Operation completed successfully.",
        ["MSG.EMPTY_STATE.DEFAULT"]        = "No results.",
        ["MSG.DELETED"]                    = "Deleted successfully.",
        ["VALIDATION.REQUIRED"]            = "Required field.",
        ["MSG.CLIENT.CREATED"]             = "Client created successfully.",
        ["MSG.EMPTY_STATE.FRIENDS"]        = "No friends yet. Add someone by email!",
        ["MSG.FRIEND.REQUEST_SENT"]        = "Friend request sent.",
        ["MSG.FRIEND.ACCEPTED"]            = "Request accepted.",
        ["MSG.FRIEND.REJECTED"]            = "Request rejected.",
        ["MSG.FRIEND.REMOVED"]             = "Friend removed.",
        ["MSG.FRIEND.NOT_FOUND"]           = "No user found with that email.",
        ["MSG.FRIEND.ALREADY_FRIENDS"]     = "You are already friends.",

        ["HINT.PROPOSAL.SOLD_AT"]          = "When was the sale closed? (can be backdated)",
        ["HINT.PROPOSAL.CONVERT"]         = "Converting will create a sale and move the client to the Won stage.",

        ["LABEL.NOTIF_SETTINGS.GENERAL"]              = "General Settings",
        ["LABEL.NOTIF_SETTINGS.DIGEST_FREQUENCY"]    = "Digest Frequency (days)",
        ["LABEL.NOTIF_SETTINGS.SALE_FOLLOWUP"]       = "Post-Sale Follow-up (days)",
        ["LABEL.NOTIF_SETTINGS.STAGE_CHANGE_ENABLED"]= "Stage Change Notifications",
        ["LABEL.NOTIF_SETTINGS.SALE_NOTIFS_ENABLED"] = "Sale Notifications",
        ["LABEL.NOTIF_SETTINGS.STAGE"]               = "Stage",
        ["LABEL.NOTIF_SETTINGS.DAYS_AFTER"]          = "Days After",
        ["LABEL.NOTIF_SETTINGS.NEW_CLIENT_DAYS"]     = "Days after new client",
        ["LABEL.NOTIF_SETTINGS.NEW_CLIENT_TIME"]     = "Notification time",
        ["LABEL.NOTIF_SETTINGS.STAGE_TEMPLATES"]     = "Stage Notifications",
        ["LABEL.NOTIF_SETTINGS.OVERRIDE_NEW_CLIENT"] = "Override new client notification?",
        ["ACTION.TEMPLATE.ADD"]             = "Add Template",
        ["ACTION.TEMPLATE.EDIT"]            = "Edit Template",
        ["ACTION.TEMPLATE.DELETE"]          = "Delete Template",
        ["MSG.NOTIF_SETTINGS.SAVED"]        = "Notification settings saved.",
        ["NAV.NOTIFICATION_SETTINGS"]       = "Notif. Settings",
        ["PAGE.NOTIFICATION_SETTINGS.TITLE"]= "Notification Settings",
    };
}
