namespace Domain.Identity;

/// <summary>
/// The fixed catalog of permission strings the system understands (see
/// docs/PRD.md §13 and docs/security/security-model.md). Authorization
/// policies are evaluated against these, never against role name strings.
/// New permissions are added here as later modules are implemented.
/// </summary>
public static class Permissions
{
    public const string CatalogRead = "catalog.read";
    public const string CatalogManage = "catalog.manage";

    public const string InventoryRead = "inventory.read";
    public const string InventoryAdjust = "inventory.adjust";

    public const string PurchaseCreate = "purchase.create";
    public const string PurchaseApprove = "purchase.approve";

    public const string SalesCreate = "sales.create";
    public const string SalesRefund = "sales.refund";

    public const string ReportsView = "reports.view";
    public const string UsersManage = "users.manage";
    public const string NotificationsView = "notifications.view";

    public static readonly IReadOnlyList<string> All =
    [
        CatalogRead, CatalogManage,
        InventoryRead, InventoryAdjust,
        PurchaseCreate, PurchaseApprove,
        SalesCreate, SalesRefund,
        ReportsView, UsersManage,
        NotificationsView
    ];
}

/// <summary>
/// Starting roles (docs/PRD.md §15) expressed as bundles of the permissions
/// above. Role assignment is tenant-scoped; this is just the default bundle
/// applied when a role is created for a new store.
/// </summary>
public static class DefaultRoles
{
    public const string Admin = "Admin";
    public const string Manager = "Manager";
    public const string InventoryManager = "InventoryManager";
    public const string Cashier = "Cashier";
    public const string Procurement = "Procurement";
    public const string Analyst = "Analyst";

    public static readonly IReadOnlyDictionary<string, IReadOnlyList<string>> PermissionBundles =
        new Dictionary<string, IReadOnlyList<string>>
        {
            [Admin] = Permissions.All,
            [Manager] =
            [
                Permissions.CatalogRead, Permissions.CatalogManage,
                Permissions.InventoryRead, Permissions.InventoryAdjust,
                Permissions.PurchaseCreate, Permissions.PurchaseApprove,
                Permissions.SalesCreate, Permissions.SalesRefund,
                Permissions.ReportsView, Permissions.NotificationsView
            ],
            [InventoryManager] =
            [
                Permissions.CatalogRead, Permissions.CatalogManage,
                Permissions.InventoryRead, Permissions.InventoryAdjust,
                Permissions.PurchaseCreate, Permissions.NotificationsView
            ],
            [Cashier] =
            [
                Permissions.CatalogRead,
                Permissions.InventoryRead,
                Permissions.SalesCreate
            ],
            [Procurement] =
            [
                Permissions.CatalogRead,
                Permissions.PurchaseCreate,
                Permissions.InventoryRead
            ],
            [Analyst] =
            [
                Permissions.CatalogRead,
                Permissions.InventoryRead,
                Permissions.ReportsView
            ]
        };
}
