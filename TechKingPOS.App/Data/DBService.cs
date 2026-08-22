using System;
using Microsoft.Data.SqlClient; // changed from Microsoft.Data.Sqlite
using TechKingPOS.App.Services;
using System.Collections.Generic;
using System.Data;

namespace TechKingPOS.App.Data
{
    public static class DbService
    {
        // 🔹 Choose DB name based on build configuration
    #if DEBUG
    private const string ConnectionString =
        "Server=.\\SQLEXPRESS;Database=TechKingPOS_Dev;Trusted_Connection=True;TrustServerCertificate=True;";
    #else
    private const string ConnectionString =
        "Server=.\\SQLEXPRESS;Database=TechKingPOS_Prod;Trusted_Connection=True;TrustServerCertificate=True;";
    #endif
    public static void Initialize()
    {
        if (_initialized)
            return;

        try
        {
            using var connection = GetConnection();
            connection.Open();

            CreateTables(connection);
            RunMigrations(connection);
            EnsureDefaultBranch(connection);

            LoggerService.Info(
                "🗄️",
                "DB",
                "Database initialized",
                ConnectionString);

            _initialized = true;
        }
        catch (Exception ex)
        {
            var baseDir = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var logDir = System.IO.Path.Combine(baseDir, "TechKingPOS", "crash");
            System.IO.Directory.CreateDirectory(logDir);

            var logFile = System.IO.Path.Combine(logDir, "DB_INITIALIZE_ERROR.txt");

            System.IO.File.WriteAllText(
                logFile,
                "=== DATABASE INITIALIZATION ERROR ===\n\n" + ex);

            throw;
        }
    }

        private static bool _initialized;

        private static string SchemaSql = "";

        private static void CreateTables(SqlConnection connection)
        {

            using var cmd = connection.CreateCommand();

            // ⚠ Minor SQL adjustments for SQL Server:
            // - AUTOINCREMENT → IDENTITY(1,1)
            // - DECIMAL (18,2)  → FLOAT or DECIMAL
            // - TEXT → NVARCHAR(MAX) or NVARCHAR(255)
            // - Remove PRAGMA statements (not supported in SQL Server)

            SchemaSql = @"
            IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Branch')
                BEGIN
                    CREATE TABLE Branch (
                        Id INT IDENTITY(1,1) PRIMARY KEY,
                        Name NVARCHAR(255) NOT NULL UNIQUE,
                        Code NVARCHAR(50) NOT NULL UNIQUE,
                        IsActive BIT NOT NULL DEFAULT 1,
                        CreatedAt DATETIME2 NOT NULL DEFAULT SYSDATETIME()


                         
                        
                    );
                END

            

            IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Items')
                    BEGIN
                        CREATE TABLE  Items (
                Id INT IDENTITY(1,1) PRIMARY KEY,
                BranchId INT NOT NULL DEFAULT 1,
                Name NVARCHAR(255) NOT NULL,
                Alias NVARCHAR(255),
                MarkedPrice DECIMAL (18,2)   NOT NULL,
                SellingPrice DECIMAL (18,2)   NOT NULL,
                WholesalePrice DECIMAL (18,2) NULL,
                Quantity DECIMAL (18,1)   NOT NULL,
                UnitType NVARCHAR(50) NOT NULL DEFAULT '1',
                UnitValue   DECIMAL (18,2)   NULL,
                CreatedAt DATETIME2 NOT NULL,
                TargetQuantity DECIMAL (18,1) NULL,
                Supplier NVARCHAR(100),
                FOREIGN KEY (BranchId) REFERENCES Branch(Id)

                         
                        

                
            );
            END

            IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Transactions')
            BEGIN
                CREATE TABLE Transactions (
                    Id INT IDENTITY(1,1) PRIMARY KEY,
                    ReceiptNumber NVARCHAR(100) NOT NULL UNIQUE,
                    Cashier NVARCHAR(255) NOT NULL,
                    Total DECIMAL(18,2) NOT NULL,
                    Tax DECIMAL(18,2) NOT NULL,
                    Discount DECIMAL(18,2) NOT NULL,
                    AmountPaid DECIMAL(18,2) NOT NULL,
                    Balance DECIMAL(18,2) NOT NULL DEFAULT 0,
                    PaymentStatus NVARCHAR(50) DEFAULT 'Pending',
                    MpesaCode NVARCHAR(50) NULL ,
                    BranchId INT NOT NULL DEFAULT 1,
                    CreatedAt DATETIME2 NOT NULL,
                    FOREIGN KEY (BranchId) REFERENCES Branch(Id)
                
            );
            END
            
            IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Sales')
                    BEGIN
                        CREATE TABLE  Sales (
                Id INT IDENTITY(1,1) PRIMARY KEY,
                ReceiptNumber NVARCHAR(100) NOT NULL,
                ItemId INT NOT NULL,
                ItemName NVARCHAR(255) NOT NULL,
                UnitType NVARCHAR(50) NOT NULL DEFAULT '1',
                UnitValue DECIMAL (18,2)   NULL,
                Quantity DECIMAL(18,1) NOT NULL,
                Price DECIMAL (18,2)    NOT NULL,
                Total DECIMAL (18,2)    NOT NULL,
                CostPrice DECIMAL (18,2)    NOT NULL DEFAULT 0,
                Profit DECIMAL (18,2)    NOT NULL DEFAULT 0,
                SaleType NVARCHAR(50) NOT NULL,
                BranchId INT NOT NULL DEFAULT 1,
                CreatedAt DATETIME2 NOT NULL DEFAULT SYSDATETIME(),
                FOREIGN KEY (BranchId) REFERENCES Branch(Id)

                         
                
                );
                END
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Returns')
BEGIN
    CREATE TABLE Returns (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        ReceiptNumber NVARCHAR(50) NULL,                       -- Sales or Supplier receipt (can be NULL for manual supplier returns)
        SaleDate DATETIME2 NULL,                  -- Day of original sale
        ReturnDate DATETIME2 NOT NULL DEFAULT SYSDATETIME(),  -- Day of return
        ItemId INT NOT NULL,
        ItemName NVARCHAR(255) NOT NULL,
        Alias NVARCHAR(255) NULL,
        Quantity DECIMAL(18,1) NOT NULL,
        UnitPrice DECIMAL(18,2) NULL,
        Price DECIMAL(18,2) NOT NULL,            -- Total amount for returned qty
        Reason NVARCHAR(MAX) NULL,
        ReturnType NVARCHAR(50) NOT NULL DEFAULT 'Customer Return', -- Customer, Supplier, Damaged
        BranchId INT NOT NULL,
        PerformedBy NVARCHAR(255) NOT NULL,
        RefundMethod NVARCHAR(50) NULL,          -- Cash, M-Pesa
        Restock BIT NOT NULL DEFAULT 1,
        SupplierName NVARCHAR(255) NULL,         -- For supplier returns
        CreatedAt DATETIME2 NOT NULL DEFAULT SYSDATETIME(),
        FOREIGN KEY (ItemId) REFERENCES Items(Id),
        FOREIGN KEY (BranchId) REFERENCES Branch(Id)
    );
END


IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'RepackStock')
BEGIN
    CREATE TABLE RepackStock (
        Id INT PRIMARY KEY IDENTITY,

        -- SOURCE
        ItemId INT NOT NULL,
        ItemName NVARCHAR(150),

        SourceQuantity DECIMAL(18,2),     -- e.g. 1 sack
        SourceUnit NVARCHAR(50),          -- Sack / Bag

        -- DEFINED OUTPUT
        BaseUnit NVARCHAR(50),            -- KG / Litre / PCS / FT
        TotalQuantity DECIMAL(18,2),      -- e.g. 70 KG
        RemainingQuantity DECIMAL(18,2),

        -- COST TRACKING 🔥
        SourceCost DECIMAL(18,2),         -- cost of 1 sack (important)
        CostPerUnit DECIMAL(18,2),        -- 4000 / 70

        -- STATUS / LIFECYCLE
        Status NVARCHAR(20),              -- ACTIVE / CLOSED
        CreatedAt DATETIME,
        ClosedAt DATETIME NULL,

        -- TRACEABILITY (VERY IMPORTANT)
        CreatedBy NVARCHAR(100),
        BranchId INT
    );
END

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Customers')
BEGIN
    CREATE TABLE Customers (
        Id INT IDENTITY(1,1) PRIMARY KEY,

        Name NVARCHAR(255) NOT NULL,
        Phone NVARCHAR(20) NOT NULL,
        BranchId INT NOT NULL,

        CreatedAt DATETIME2 NOT NULL DEFAULT SYSDATETIME(),

        -- ✅ Phone MUST be unique per branch
        CONSTRAINT UQ_Customers_Phone_Branch UNIQUE (Phone, BranchId),

        CONSTRAINT FK_Customers_Branch 
            FOREIGN KEY (BranchId) REFERENCES Branch(Id)
    );
END



                 IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Payments')
                BEGIN
                    CREATE TABLE Payments (
                        Id INT IDENTITY(1,1) PRIMARY KEY,
                        ReceiptNumber NVARCHAR(100) NOT NULL,
                        Method NVARCHAR(100) NOT NULL,
                        Amount DECIMAL (18,2)    NOT NULL,
                        BranchId INT NOT NULL DEFAULT 1,
                        CreatedAt DATETIME2 NOT NULL DEFAULT SYSDATETIME(),
                        FOREIGN KEY (BranchId) REFERENCES Branch(Id)

                         
                
                );
                END

                    -- Credits are CUSTOMER-BASED but still receipt-linked
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Credits')
BEGIN
    CREATE TABLE Credits (
        Id INT IDENTITY(1,1) PRIMARY KEY,

        CustomerId INT NOT NULL, -- ✅ NO UNIQUE HERE

        ReceiptNumber NVARCHAR(100) NOT NULL,
        Total DECIMAL(18,2) NOT NULL,
        Paid DECIMAL(18,2) NOT NULL,
        Balance DECIMAL(18,2) NOT NULL,
        LoanAmount DECIMAL(18,2),
        BalanceBefore DECIMAL(18,2),

        BranchId INT NOT NULL,
        CreatedAt DATETIME2 NOT NULL DEFAULT SYSDATETIME(),

        CONSTRAINT FK_Credits_Customer 
            FOREIGN KEY (CustomerId) REFERENCES Customers(Id),

        CONSTRAINT FK_Credits_Branch 
            FOREIGN KEY (BranchId) REFERENCES Branch(Id)
    );
END


                    IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'CreditPayments')
                    BEGIN
                        CREATE TABLE  CreditPayments (
                            Id INT IDENTITY(1,1) PRIMARY KEY,
                            CreditId INT NOT NULL,
                            ReceiptNumber NVARCHAR(50) NULL,
                            Amount DECIMAL(18,2) NOT NULL,
                            Method NVARCHAR(100) NOT NULL,
                            BalanceBefore DECIMAL(18,2) NOT NULL DEFAULT 0,
                            BalanceAfter DECIMAL(18,2) NOT NULL DEFAULT 0,
                            BranchId INT NOT NULL DEFAULT 1,
                            CreatedAt DATETIME2 NOT NULL DEFAULT SYSDATETIME(),
                            FOREIGN KEY (CreditId) REFERENCES Credits(Id),
                            FOREIGN KEY (BranchId) REFERENCES Branch(Id)

                             
                        
                        );
                        END

                    IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Workers')
                    BEGIN
                        CREATE TABLE  Workers (
                        Id INT IDENTITY(1,1) PRIMARY KEY,
                        Name NVARCHAR(255) NOT NULL,
                        NationalId NVARCHAR(255) NOT NULL,
                        Phone NVARCHAR(255) NOT NULL,
                        Email NVARCHAR(255),
                        PasswordHash NVARCHAR(MAX) NOT NULL,
                        Role INT NOT NULL DEFAULT 2,
                        IsActive BIT NOT NULL DEFAULT 0,
                        MustChangePassword BIT  DEFAULT 1,
                        BranchId INT NOT NULL DEFAULT 1,
                        CreatedAt DATETIME2 NOT NULL DEFAULT SYSDATETIME()


                         
                        
                    );
                    END


                     IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'DamagedItems')
                    BEGIN
                        CREATE TABLE DamagedItems (
                            Id INT IDENTITY(1,1) PRIMARY KEY,
                            ItemId INT NOT NULL,
                            ItemName NVARCHAR(255) NOT NULL,
                            Alias NVARCHAR(255),
                            UnitType NVARCHAR(100),
                            Quantity DECIMAL(18,1) NOT NULL,
                            MarkedPrice DECIMAL(18,2) NOT NULL,
                            SellingPrice DECIMAL(18,2) NOT NULL,
                            Reason NVARCHAR(MAX) NOT NULL,
                            RecordedBy NVARCHAR(255) NOT NULL,
                            BranchId INT NOT NULL DEFAULT 1,
                            DamagedAt DATETIME2 NOT NULL,
                            FOREIGN KEY (ItemId) REFERENCES Items(Id),
                            FOREIGN KEY (BranchId) REFERENCES Branch(Id)

                             
                        
                        );
                        END

                    IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Expenses')
                    BEGIN
                        CREATE TABLE Expenses (
                            Id INT IDENTITY(1,1) PRIMARY KEY,
                            Date DATETIME2 NOT NULL,
                            Category NVARCHAR(255) NOT NULL,
                            Description NVARCHAR(MAX) NOT NULL,
                            Amount DECIMAL(18,2) NOT NULL,
                            PaymentMethod NVARCHAR(100) NOT NULL,
                            BranchId INT NOT NULL DEFAULT 1,
                            CreatedAt DATETIME2 NOT NULL DEFAULT SYSDATETIME(),
                            FOREIGN KEY (BranchId) REFERENCES Branch(Id)

                             
                        
                        );
                        END




                    IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'AppSettings')
                    BEGIN
                    CREATE TABLE  AppSettings
                        (
                            Id INT IDENTITY(1,1) PRIMARY KEY,
                            BusinessName NVARCHAR(255),
                            BranchName NVARCHAR(255),
                            Phone NVARCHAR(255),
                            SecondaryPhone NVARCHAR(255),
                            Email NVARCHAR(255),
                            PhysicalAddress NVARCHAR(MAX),
                            ReceiptFooter NVARCHAR(MAX),

                            AutoPrintReceipt BIT,
                            ShowCashierOnReceipt BIT,
                            ShowLogoOnReceipt BIT,
                            LogoImage VARBINARY(MAX) NULL,
                            PaperSize NVARCHAR(100),
                            PrintLayout NVARCHAR(50) NOT NULL DEFAULT 'Thermal',
                            ReceiptCopies INT,
                            SignReceipt BIT,

                            AllowNegativeStock BIT,
                            AllowPriceEditDuringSale BIT,
                            EnableCreditSales BIT,

                            VatEnabled BIT,
                            VatPercent DECIMAL(18,2) NULL,

                            EnableEtims BIT NOT NULL DEFAULT(0),
                            KraPin NVARCHAR(100) NULL,
                            BhfId NVARCHAR(100) NULL,
                            DeviceSerial NVARCHAR(200) NULL,
                            EtimsUsername NVARCHAR(100) NULL,
                            EtimsPassword NVARCHAR(200) NULL,

                            RequireLogin BIT,
                            AutoLogout BIT,
                            AllowVoidSales BIT,
                            AllowWorkersEditPrices BIT,
                            AllowWorkersGiveDiscounts BIT,

                            LicenseKey NVARCHAR(255) NULL,
                            LicenseExpiry DATETIME2 NULL,
                            LicenseExpiryDate DATETIME2 NULL,
                            LicenseInstallDate DATETIME2 NULL,
                            LicenseActivated BIT NOT NULL DEFAULT 0,
                            LicenseMachineId NVARCHAR(255),

                            EnableDiscounts BIT,

                            AllowFixedDiscount BIT,
                            AllowPercentageDiscount BIT,
                            AllowConditionalDiscount BIT,
                            MaxFixedDiscount DECIMAL(18,2) NULL,
                            MaxPercentageDiscount DECIMAL(18,2) NULL,

                            CondValueFixed BIT,
                            CondValuePercent BIT,
                            CondBasedRanges BIT,

                            ConditionalMinSubtotal DECIMAL(18,2) NULL,
                            ConditionalDiscountAmount DECIMAL(18,2) NULL

                             
                        
                        );
                        END                        


                                IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'DiscountRanges')
                    BEGIN
                        CREATE TABLE  DiscountRanges (
                                    Id INT IDENTITY(1,1) PRIMARY KEY,
                                    FromAmount DECIMAL(18,2) NOT NULL,
                                    ToAmount DECIMAL(18,2) NOT NULL,
                                    DiscountPercent DECIMAL(18,2) NULL,
                                    DiscountAmount DECIMAL(18,2) NULL

                                     
                        
                                );
                                END


                        IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Activity')
                    BEGIN
                        CREATE TABLE  Activity (
                            Id INT IDENTITY(1,1) PRIMARY KEY,
                                                    -- What was affected
                            EntityType NVARCHAR(100) NOT NULL,      -- e.g. Items, Sales, Payments
                            EntityId INT,              -- nullable for global/system actions
                            EntityName NVARCHAR(255),
                                                            -- What happened
                            Action NVARCHAR(100) NOT NULL,          -- INSERT, UPDATE, SALE, DAMAGE, ADD_STOCK
                                                            -- Stock impact (BASE UNIT ONLY)
                            QuantityChange DECIMAL (18,2)    NOT NULL DEFAULT 0,
                            UnitType NVARCHAR(100),                 -- kg / l / pieces (display only)
                            UnitValue DECIMAL(18,2) NULL,
                            Price DECIMAL(18,2) NULL,                     -- conversion context
                                                            -- Optional snapshots (future-safe)
                            BeforeValue NVARCHAR(MAX),              -- JSON (nullable)
                            AfterValue NVARCHAR(MAX),               -- JSON (nullable)
                                                                -- Meta
                            Reason NVARCHAR(MAX),                   -- human explanation
                            PerformedBy NVARCHAR(255) NOT NULL,     -- user / SYSTEM
                            BranchId INT NOT NULL DEFAULT 1,
                            CreatedAt DATETIME2 NOT NULL DEFAULT SYSDATETIME()

                             
                        
                        );
                        END

                        IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'RepackRules')
                    BEGIN
                        CREATE TABLE  RepackRules (
                            Id INT IDENTITY(1,1) PRIMARY KEY,

                            -- Source item (bulk)
                            ItemId INT NOT NULL,
                            ItemName NVARCHAR(255) NOT NULL,

                            -- How it is sold
                            UnitValue DECIMAL (18,2)  NOT NULL,         -- e.g. 0.25
                            UnitType NVARCHAR(100) NOT NULL,         -- kg / g / ml / l / pieces

                            SellingPrice DECIMAL (18,2)  NOT NULL,

                            -- Status
                            IsActive BIT NOT NULL DEFAULT 1,

                            CreatedAt DATETIME2 NOT NULL DEFAULT SYSDATETIME(),

                            FOREIGN KEY (ItemId) REFERENCES Items(Id)

                             
                        
                        );
                        END

                        IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'permissions')
                    BEGIN
                        CREATE TABLE  permissions (
                            id INT IDENTITY(1,1) PRIMARY KEY,
                            UserKey NVARCHAR(255) NOT NULL UNIQUE,
                            description NVARCHAR(MAX)

                             
                        
                        );
                        END

                        IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'roles')
                    BEGIN
                        CREATE TABLE  roles (
                            id INT IDENTITY(1,1) PRIMARY KEY,
                            name NVARCHAR(255) NOT NULL UNIQUE

                             
                        
                        );
                        END

                        IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'role_permissions')
                    BEGIN
                        CREATE TABLE  role_permissions (
                            role_id INT NOT NULL,
                            permission_id INT NOT NULL,
                            PRIMARY KEY (role_id, permission_id)

                             
                        
                        );
                        END


                        IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'user_roles')
                    BEGIN
                        CREATE TABLE  user_roles (
                            user_id INT NOT NULL,
                            role_id INT NOT NULL,
                            PRIMARY KEY (user_id, role_id)

                             
                        
                        );
                        END


                        IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'user_permissions')
                    BEGIN
                        CREATE TABLE  user_permissions (
                            user_id INT NOT NULL,
                            permission_key NVARCHAR(255) NOT NULL,
                            granted INT NOT NULL DEFAULT 1,
                            granted_at DATETIME2 NOT NULL,

                            PRIMARY KEY (user_id, permission_key)

                             
                        
                        );
                        END


                        IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'ItemBarcodes')
                    BEGIN
                        CREATE TABLE  ItemBarcodes (
                            Id INT IDENTITY(1,1) PRIMARY KEY,
                            Barcode NVARCHAR(255) NOT NULL UNIQUE,
                            ItemId INT NOT NULL,
                            UnitsPerScan DECIMAL (18,2)  NOT NULL DEFAULT 1,
                            CreatedAt DATETIME2 NOT NULL DEFAULT SYSDATETIME(),
                            FOREIGN KEY (ItemId) REFERENCES Items(Id) ON DELETE CASCADE

                             
                        
                        );
                        END

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'MpesaMessages')
                        BEGIN
                        CREATE TABLE MpesaMessages
                        (
                            Id INT IDENTITY(1,1) PRIMARY KEY,
                            MessageText NVARCHAR(MAX),
                            MpesaCode NVARCHAR(MAX),

                            SenderName NVARCHAR(200),
                            SenderPhone NVARCHAR(200),

                            ExtractedAmount DECIMAL(20,2),

                            ReceivedAt DATETIME DEFAULT SYSDATETIME(),

                            IsProcessed BIT DEFAULT 0,
                            IsUsed INT DEFAULT 0
                        );
                        END

                        IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Returns')
                    BEGIN
                        CREATE TABLE  Returns (
                            Id INT IDENTITY(1,1) PRIMARY KEY,
                            ItemId INT NOT NULL,
                            ItemName NVARCHAR(255) NOT NULL,
                            Alias NVARCHAR(255),
                            Quantity DECIMAL(18,1) NOT NULL,
                            Price DECIMAL (18,2)  NOT NULL,
                            Reason NVARCHAR(MAX),
                            ReturnType NVARCHAR(255) NOT NULL DEFAULT 'Customer Return',
                            BranchId INT NOT NULL DEFAULT 1,
                            PerformedBy NVARCHAR(255) NOT NULL,
                            CreatedAt DATETIME2 NOT NULL DEFAULT SYSDATETIME(),
                            FOREIGN KEY (ItemId) REFERENCES Items(Id),
                            FOREIGN KEY (BranchId) REFERENCES Branch(Id)

                             
                        
                        );
                        END
                        
-- Locations table
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Locations')
BEGIN
    CREATE TABLE Locations (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        Name NVARCHAR(255) NOT NULL,
        ParentId INT NULL,
        Type NVARCHAR(50) NOT NULL, -- e.g. 'Warehouse', 'Storefront', 'Shelf A1'
        CreatedAt DATETIME2 NOT NULL DEFAULT SYSDATETIME(),
        UpdatedAt DATETIME2 NULL,
        FOREIGN KEY (ParentId) REFERENCES Locations(Id)
                       
    );
END

-- Inventory table
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'StockLevels')
BEGIN
    CREATE TABLE StockLevels (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        ItemId INT NOT NULL,
        LocationId INT NOT NULL,
        Quantity DECIMAL(18,1) NOT NULL DEFAULT 0,
        UpdatedAt DATETIME2 NOT NULL DEFAULT SYSDATETIME(),
        FOREIGN KEY (ItemId) REFERENCES Items(Id),
        FOREIGN KEY (LocationId) REFERENCES Locations(Id)

         
                        
    );
END

-- StockMovements table
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'StockMovements')
BEGIN
    CREATE TABLE StockMovements (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        ItemId INT NOT NULL,
        Quantity DECIMAL(18,1) NOT NULL,
        FromLocationId INT NOT NULL,
        ToLocationId INT NOT NULL,
        Reason NVARCHAR(MAX) NULL,
        PerformedBy NVARCHAR(255) NOT NULL,
        CreatedAt DATETIME2 NOT NULL DEFAULT SYSDATETIME(),
        FOREIGN KEY (ItemId) REFERENCES Items(Id),
        FOREIGN KEY (FromLocationId) REFERENCES Locations(Id),
        FOREIGN KEY (ToLocationId) REFERENCES Locations(Id)

       
                        
    );
END

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'SchemaMigrations')
BEGIN
    CREATE TABLE SchemaMigrations (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        MigrationKey NVARCHAR(255) NOT NULL UNIQUE,
        AppliedAt DATETIME2 NOT NULL DEFAULT SYSDATETIME()
    );
END

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Business')
BEGIN
    CREATE TABLE Business
    (
        Id INT IDENTITY(1,1) PRIMARY KEY,

        BusinessCode NVARCHAR(100) NOT NULL UNIQUE,
        BusinessName NVARCHAR(255) NOT NULL,

        OwnerName NVARCHAR(255),
        Phone NVARCHAR(50),
        Email NVARCHAR(255),

        LicenseKey NVARCHAR(255),

        CloudEnabled BIT NOT NULL DEFAULT 0,

        CreatedAt DATETIME2 NOT NULL DEFAULT SYSDATETIME()
    );
END
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name='SyncQueue')
BEGIN

CREATE TABLE SyncQueue
(
    Id INT IDENTITY PRIMARY KEY,

    ---------------------------------------
    -- Business
    ---------------------------------------
    BusinessCode NVARCHAR(100) NOT NULL,

    BranchId INT NOT NULL,

    DeviceId NVARCHAR(100) NOT NULL,

    ---------------------------------------
    -- Record
    ---------------------------------------

    TableName NVARCHAR(100) NOT NULL,

    RecordId INT NOT NULL,

    Action NVARCHAR(20) NOT NULL,

    JsonData NVARCHAR(MAX) NOT NULL,

    ---------------------------------------
    -- Queue
    ---------------------------------------

    Priority INT NOT NULL DEFAULT 1,

    Status NVARCHAR(20) NOT NULL DEFAULT 'Pending',

    Attempts INT NOT NULL DEFAULT 0,

    LastError NVARCHAR(MAX),

    CreatedAt DATETIME2 NOT NULL DEFAULT SYSDATETIME(),

    SyncedAt DATETIME2,

    NextRetryAt DATETIME2
);

CREATE INDEX IX_SyncQueue_Status
ON SyncQueue(Status);

CREATE INDEX IX_SyncQueue_Table_Record
ON SyncQueue(TableName,RecordId);

CREATE INDEX IX_SyncQueue_Business
ON SyncQueue(BusinessCode);

END

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'CloudSettings')
BEGIN
    CREATE TABLE CloudSettings
    (
        Id INT IDENTITY(1,1) PRIMARY KEY,

        BusinessCode NVARCHAR(100) NOT NULL UNIQUE,
        BusinessId INT NOT NULL,

        BranchId INT NOT NULL,

        DeviceId NVARCHAR(100) NOT NULL,
        DeviceName NVARCHAR(255) NOT NULL,

        ApiUrl NVARCHAR(500) NOT NULL,
        ApiKey NVARCHAR(255) NULL,

        SyncEnabled BIT NOT NULL DEFAULT 1,

        LastUpload DATETIME2 NULL,
        LastDownload DATETIME2 NULL,

        SyncIntervalSeconds INT NOT NULL DEFAULT 60,

        CreatedAt DATETIME2 NOT NULL DEFAULT SYSDATETIME(),
        UpdatedAt DATETIME2 NULL
    );
END

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name='SyncQueue')
BEGIN
    CREATE TABLE SyncQueue
    (
        Id INT IDENTITY(1,1) PRIMARY KEY,

        BusinessCode NVARCHAR(100) NOT NULL,

        TableName NVARCHAR(100) NOT NULL,

        RecordId NVARCHAR(100) NOT NULL,

        ActionType NVARCHAR(20) NOT NULL,

        JsonData NVARCHAR(MAX) NOT NULL,

        Status NVARCHAR(20) NOT NULL DEFAULT 'Pending',

        RetryCount INT NOT NULL DEFAULT 0,

        LastError NVARCHAR(1000),

        CreatedAt DATETIME2 NOT NULL DEFAULT SYSDATETIME(),

        SyncedAt DATETIME2 NULL
    );

    CREATE INDEX IX_SyncQueue_Status
    ON SyncQueue(Status);

    CREATE INDEX IX_SyncQueue_Business
    ON SyncQueue(BusinessCode);
END
            ";

            cmd.CommandText=SchemaSql;
            cmd.ExecuteNonQuery(); 
            
        

        }
private static void AutoSyncSchema(SqlConnection conn)
{
    var tables = ParseTables(SchemaSql);

    foreach (var table in tables)
    {
        SyncTable(conn, table.Key, table.Value);
    }
}
private static Dictionary<string, string> ParseTables(string sql)
{
    var tables = new Dictionary<string, string>();

    var parts = sql.Split("CREATE TABLE", StringSplitOptions.RemoveEmptyEntries);

    foreach (var part in parts)
    {
        var trimmed = part.Trim();

        var nameEnd = trimmed.IndexOf("(");
        if (nameEnd < 0) continue;

        var tableName = trimmed.Substring(0, nameEnd).Trim();

        var bodyStart = trimmed.IndexOf("(") + 1;
        var bodyEnd = trimmed.LastIndexOf(")");

        if (bodyEnd <= bodyStart) continue;

        var body = trimmed.Substring(bodyStart, bodyEnd - bodyStart);

        tables[tableName] = body;
    }

    return tables;
}
private static void SyncTable(SqlConnection conn, string table, string schema)
{
    var existing = GetExistingColumns(conn, table);

    var lines = schema.Split(',');

    foreach (var raw in lines)
    {
        var line = raw.Trim();

        if (line.StartsWith("FOREIGN") ||
            line.StartsWith("PRIMARY") ||
            line.StartsWith("CONSTRAINT"))
            continue;

        var parts = line.Split(' ', 2);
        if (parts.Length < 2) continue;

        var column = parts[0].Trim('[', ']');

        if (existing.Contains(column))
            continue;

        var definition = parts[1];

        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"ALTER TABLE {table} ADD {column} {definition}";
        cmd.ExecuteNonQuery();

        LoggerService.Info("DB", "Migration", $"Added {column} to {table}", "");
    }
}
private static HashSet<string> GetExistingColumns(SqlConnection conn, string table)
{
    var cols = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    using var cmd = conn.CreateCommand();
    cmd.CommandText = @"
        SELECT COLUMN_NAME
        FROM INFORMATION_SCHEMA.COLUMNS
        WHERE TABLE_NAME = @table";

    cmd.Parameters.AddWithValue("@table", table);

    using var reader = cmd.ExecuteReader();

    while (reader.Read())
        cols.Add(reader.GetString(0));

    return cols;
}
        private static void EnsureDefaultBranch(SqlConnection connection)
        {
            using var check = connection.CreateCommand();
            check.CommandText = "SELECT COUNT(*) FROM Branch;";
            int count = (int)check.ExecuteScalar()!;

            if (count > 0)
                return;

            using var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                INSERT INTO Branch (Name, Code, IsActive, CreatedAt)
                VALUES ('Main Branch', 'MAIN', 1, @created);
            ";

            cmd.Parameters.AddWithValue("@created", DateTime.Now);

            cmd.ExecuteNonQuery();
        }

        public static SqlConnection GetConnection()
        {
            return new SqlConnection(ConnectionString);
        }

private static bool HasMigration(SqlConnection conn, string key)
{
    using var cmd = conn.CreateCommand();
    cmd.CommandText = "SELECT COUNT(*) FROM SchemaMigrations WHERE MigrationKey = @key";
    cmd.Parameters.AddWithValue("@key", key);
    return (int)cmd.ExecuteScalar()! > 0;
}

private static void MarkMigration(SqlConnection conn, string key)
{
    using var cmd = conn.CreateCommand();
    cmd.CommandText = "INSERT INTO SchemaMigrations (MigrationKey) VALUES (@key)";
    cmd.Parameters.AddWithValue("@key", key);
    cmd.ExecuteNonQuery();
}
private static void RunMigrations(SqlConnection conn)
{
var migrations = new List<Migration>
{
    new Migration(
        "ADD_CREDITS_BALANCE_BEFORE",
        @"
        IF COL_LENGTH('Credits', 'BalanceBefore') IS NULL
        BEGIN
            ALTER TABLE Credits 
            ADD BalanceBefore DECIMAL(18,2) NOT NULL DEFAULT 0
        END
        "
    ),

    new Migration(
        "ADD_APPSETTINGS_SIGN_RECEIPT",
        @"
        IF COL_LENGTH('AppSettings', 'SignReceipt') IS NULL
        BEGIN
            ALTER TABLE AppSettings 
            ADD SignReceipt BIT NOT NULL DEFAULT 0
        END
        "
    ),
    new Migration(
    "ADD_APPSETTINGS_SECONDARY_PHONE",
    @"
    IF COL_LENGTH('AppSettings', 'SecondaryPhone') IS NULL
    BEGIN
        ALTER TABLE AppSettings 
        ADD SecondaryPhone NVARCHAR(50) NULL
    END
    "
),

new Migration(
    "ADD_APPSETTINGS_LOGO",
    @"
    IF COL_LENGTH('AppSettings', 'LogoImage') IS NULL
    BEGIN
        ALTER TABLE AppSettings 
        ADD LogoImage VARBINARY(MAX) NULL
    END
    "
),
new Migration(
    "UPDATE_MPESA_MESSAGES_DATA",
    @"
    IF COL_LENGTH('MpesaMessages', 'SenderName') IS NULL
    BEGIN
        ALTER TABLE MpesaMessages 
        ADD SenderName NVARCHAR(200) NULL
    END
    "
),
new Migration(
    "UPDATE_MPESA_MESSAGES_PHONE",
    @"
    IF COL_LENGTH('MpesaMessages', 'SenderPhone') IS NULL
    BEGIN
        ALTER TABLE MpesaMessages 
        ADD SenderPhone NVARCHAR(200) NULL
    END
    "
),
new Migration(
    "ADD_REPORTING_INDEXES",
    @"
    -- Transactions
    IF NOT EXISTS (
        SELECT 1 FROM sys.indexes
        WHERE name = 'IX_Transactions_Branch_Date'
        AND object_id = OBJECT_ID('Transactions')
    )
    BEGIN
        CREATE INDEX IX_Transactions_Branch_Date
        ON Transactions(BranchId, CreatedAt);
    END;

    IF NOT EXISTS (
        SELECT 1 FROM sys.indexes
        WHERE name = 'IX_Transactions_Receipt'
        AND object_id = OBJECT_ID('Transactions')
    )
    BEGIN
        CREATE INDEX IX_Transactions_Receipt
        ON Transactions(ReceiptNumber);
    END;

    -- Sales
    IF NOT EXISTS (
        SELECT 1 FROM sys.indexes
        WHERE name = 'IX_Sales_Receipt'
        AND object_id = OBJECT_ID('Sales')
    )
    BEGIN
        CREATE INDEX IX_Sales_Receipt
        ON Sales(ReceiptNumber);
    END;

    IF NOT EXISTS (
        SELECT 1 FROM sys.indexes
        WHERE name = 'IX_Sales_CreatedAt'
        AND object_id = OBJECT_ID('Sales')
    )
    BEGIN
        CREATE INDEX IX_Sales_CreatedAt
        ON Sales(CreatedAt);
    END;

    -- Payments
    IF NOT EXISTS (
        SELECT 1 FROM sys.indexes
        WHERE name = 'IX_Payments_Receipt'
        AND object_id = OBJECT_ID('Payments')
    )
    BEGIN
        CREATE INDEX IX_Payments_Receipt
        ON Payments(ReceiptNumber);
    END;

    -- Credits
    IF NOT EXISTS (
        SELECT 1 FROM sys.indexes
        WHERE name = 'IX_Credits_Customer'
        AND object_id = OBJECT_ID('Credits')
    )
    BEGIN
        CREATE INDEX IX_Credits_Customer
        ON Credits(CustomerId);
    END;

    -- CreditPayments
    IF NOT EXISTS (
        SELECT 1 FROM sys.indexes
        WHERE name = 'IX_CreditPayments_Credit'
        AND object_id = OBJECT_ID('CreditPayments')
    )
    BEGIN
        CREATE INDEX IX_CreditPayments_Credit
        ON CreditPayments(CreditId);
    END;

    -- Returns
    IF NOT EXISTS (
        SELECT 1 FROM sys.indexes
        WHERE name = 'IX_Returns_Date_Branch'
        AND object_id = OBJECT_ID('Returns')
    )
    BEGIN
        CREATE INDEX IX_Returns_Date_Branch
        ON Returns(ReturnDate, BranchId);
    END;
    "
),
new Migration(
    "ADD_ETIMS_SETTINGS",
    @"
    IF COL_LENGTH('AppSettings', 'EnableEtims') IS NULL
    BEGIN
        ALTER TABLE AppSettings
        ADD EnableEtims BIT NOT NULL DEFAULT(0)
    END

    IF COL_LENGTH('AppSettings', 'KraPin') IS NULL
    BEGIN
        ALTER TABLE AppSettings
        ADD KraPin NVARCHAR(100) NULL
    END

    IF COL_LENGTH('AppSettings', 'BhfId') IS NULL
    BEGIN
        ALTER TABLE AppSettings
        ADD BhfId NVARCHAR(100) NULL
    END

    IF COL_LENGTH('AppSettings', 'DeviceSerial') IS NULL
    BEGIN
        ALTER TABLE AppSettings
        ADD DeviceSerial NVARCHAR(200) NULL
    END

    IF COL_LENGTH('AppSettings', 'EtimsUsername') IS NULL
    BEGIN
        ALTER TABLE AppSettings
        ADD EtimsUsername NVARCHAR(100) NULL
    END

    IF COL_LENGTH('AppSettings', 'EtimsPassword') IS NULL
    BEGIN
        ALTER TABLE AppSettings
        ADD EtimsPassword NVARCHAR(200) NULL
    END
    "
),
new Migration(
    "ADD_APPSETTINGS_PRINT_LAYOUT",
    @"
    IF COL_LENGTH('AppSettings', 'PrintLayout') IS NULL
    BEGIN
        ALTER TABLE AppSettings
        ADD PrintLayout NVARCHAR(50) NOT NULL
            CONSTRAINT DF_AppSettings_PrintLayout
            DEFAULT 'Thermal'
    END
    "
),
new Migration(
    "ADD_CREDITPAYMENTS_RECEIPTNUMBER",
    @"
    IF COL_LENGTH('CreditPayments', 'ReceiptNumber') IS NULL
    BEGIN
        ALTER TABLE CreditPayments
        ADD ReceiptNumber NVARCHAR(50) NULL
    END
    "
),
new Migration(
    "ADD_CREDITPAYMENTS_BALANCES",

    @"
    IF COL_LENGTH('CreditPayments', 'BalanceBefore') IS NULL
    BEGIN
        ALTER TABLE CreditPayments
        ADD BalanceBefore DECIMAL(18,2) NOT NULL DEFAULT 0
    END

    IF COL_LENGTH('CreditPayments', 'BalanceAfter') IS NULL
    BEGIN
        ALTER TABLE CreditPayments
        ADD BalanceAfter DECIMAL(18,2) NOT NULL DEFAULT 0
    END
    "
),

};

    foreach (var m in migrations)
    {
        if (!HasMigration(conn, m.Key))
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = m.Sql;
            cmd.ExecuteNonQuery();

            MarkMigration(conn, m.Key);
        }
    }
}

    }
}
