namespace OnTimeCRM.Application.Common;

/// <summary>
/// Centralized catalog of all API errors. Always throw ApiException(ApiErrorCatalog.X), never return error DTOs manually.
/// </summary>
public static class ApiErrorCatalog
{
    // ── Auth ──────────────────────────────────────────────────────────────────
    public static readonly ApiError USER_NOT_FOUND               = new("USER_NOT_FOUND",               "User not found.",                        "NotFound",    404);
    public static readonly ApiError USER_EMAIL_TAKEN             = new("USER_EMAIL_TAKEN",             "Email is already in use.",               "Conflict",    409);
    public static readonly ApiError USER_INVALID_CREDENTIALS     = new("USER_INVALID_CREDENTIALS",     "Invalid email or password.",             "Unauthorized", 401);
    public static readonly ApiError USER_INACTIVE                = new("USER_INACTIVE",                "User account is inactive.",              "Unauthorized", 401);
    public static readonly ApiError AUTH_UNAUTHORIZED            = new("AUTH_UNAUTHORIZED",            "Authentication required.",               "Unauthorized", 401);
    public static readonly ApiError AUTH_FORBIDDEN               = new("AUTH_FORBIDDEN",               "You do not have permission.",            "Forbidden",   403);

    // ── Client ────────────────────────────────────────────────────────────────
    public static readonly ApiError CLIENT_NOT_FOUND             = new("CLIENT_NOT_FOUND",             "Client not found.",                      "NotFound",    404);
    public static readonly ApiError CLIENT_WRONG_USER            = new("CLIENT_WRONG_USER",            "Client does not belong to you.",         "Forbidden",   403);

    // ── Proposal ──────────────────────────────────────────────────────────────
    public static readonly ApiError PROPOSAL_NOT_FOUND           = new("PROPOSAL_NOT_FOUND",           "Proposal not found.",                    "NotFound",    404);
    public static readonly ApiError PROPOSAL_NOT_ACTIVE          = new("PROPOSAL_NOT_ACTIVE",          "Proposal is not active.",                "UnprocessableEntity", 422);
    public static readonly ApiError PROPOSAL_ALREADY_CLOSED      = new("PROPOSAL_ALREADY_CLOSED",      "Proposal is already closed.",            "UnprocessableEntity", 422);

    // ── Stage ─────────────────────────────────────────────────────────────────
    public static readonly ApiError STAGE_NOT_FOUND              = new("STAGE_NOT_FOUND",              "Stage not found.",                       "NotFound",    404);
    public static readonly ApiError STAGE_HAS_CLIENTS            = new("STAGE_HAS_CLIENTS",            "Stage has active clients. Move them before deleting.", "UnprocessableEntity", 422);
    public static readonly ApiError STAGE_WRONG_USER             = new("STAGE_WRONG_USER",             "Stage does not belong to you.",          "Forbidden",   403);

    // ── Vehicle ───────────────────────────────────────────────────────────────
    public static readonly ApiError VEHICLE_BRAND_NOT_FOUND      = new("VEHICLE_BRAND_NOT_FOUND",      "Vehicle brand not found.",               "NotFound",    404);
    public static readonly ApiError VEHICLE_MODEL_NOT_FOUND      = new("VEHICLE_MODEL_NOT_FOUND",      "Vehicle model not found.",               "NotFound",    404);
    public static readonly ApiError VEHICLE_BRAND_EXISTS         = new("VEHICLE_BRAND_EXISTS",         "Vehicle brand already exists.",          "Conflict",    409);

    // ── Notification ──────────────────────────────────────────────────────────
    public static readonly ApiError NOTIFICATION_NOT_FOUND       = new("NOTIFICATION_NOT_FOUND",       "Notification not found.",                "NotFound",    404);
    public static readonly ApiError NOTIFICATION_WRONG_USER      = new("NOTIFICATION_WRONG_USER",      "Notification does not belong to you.",   "Forbidden",   403);

    // ── Sale ──────────────────────────────────────────────────────────────────
    public static readonly ApiError SALE_NOT_FOUND               = new("SALE_NOT_FOUND",               "Sale not found.",                        "NotFound",    404);    public static readonly ApiError SALE_MISSING_VEHICLE         = new("SALE_MISSING_VEHICLE",         "A vehicle is required to convert to sale.", "UnprocessableEntity", 422);
    // ── Subscription ──────────────────────────────────────────────────────────
    public static readonly ApiError SUBSCRIPTION_REQUIRED        = new("SUBSCRIPTION_REQUIRED",        "Active subscription required.",          "PaymentRequired", 402);
    public static readonly ApiError SUBSCRIPTION_EXPIRED         = new("SUBSCRIPTION_EXPIRED",         "Subscription has expired.",              "PaymentRequired", 402);
    public static readonly ApiError SUBSCRIPTION_SUSPENDED       = new("SUBSCRIPTION_SUSPENDED",       "Subscription is suspended.",             "PaymentRequired", 402);
    public static readonly ApiError PAYMENT_PENDING              = new("PAYMENT_PENDING",              "Payment is pending.",                    "UnprocessableEntity", 422);
    public static readonly ApiError PAYMENT_NOT_FOUND            = new("PAYMENT_NOT_FOUND",            "Payment not found.",                     "NotFound",    404);
    public static readonly ApiError STRIPE_WEBHOOK_INVALID       = new("STRIPE_WEBHOOK_INVALID",       "Invalid Stripe webhook signature.",      "Unauthorized", 401);
    public static readonly ApiError IFTHENPAY_CALLBACK_INVALID   = new("IFTHENPAY_CALLBACK_INVALID",   "Invalid Ifthenpay callback.",            "Unauthorized", 401);
    public static readonly ApiError MBWAY_PHONE_INVALID          = new("MBWAY_PHONE_INVALID",          "Invalid MBWay phone number.",            "BadRequest",  400);

    // ── Company / Brand ───────────────────────────────────────────────────────
    public static readonly ApiError COMPANY_NOT_FOUND            = new("COMPANY_NOT_FOUND",            "Company not found.",                     "NotFound",    404);
    public static readonly ApiError COMPANY_INACTIVE             = new("COMPANY_INACTIVE",             "Company is inactive.",                   "UnprocessableEntity", 422);
    public static readonly ApiError BRAND_NOT_FOUND              = new("BRAND_NOT_FOUND",              "Brand not found.",                       "NotFound",    404);
    public static readonly ApiError BRAND_INACTIVE               = new("BRAND_INACTIVE",               "Brand is inactive.",                     "UnprocessableEntity", 422);
    public static readonly ApiError BRAND_WRONG_COMPANY          = new("BRAND_WRONG_COMPANY",          "Brand does not belong to this company.", "Forbidden",   403);
    public static readonly ApiError USER_WRONG_BRAND             = new("USER_WRONG_BRAND",             "User does not belong to this brand.",    "Forbidden",   403);
}
