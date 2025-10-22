## database
/* ============================================================
   SmartCanteen – Full Database Schema & Seed (SQL Server)
   Tạo DB + Tables + FKs + Indexes + Seed
   ============================================================ */
SET NOCOUNT ON;

IF DB_ID('SmartCanteen') IS NULL
BEGIN
    CREATE DATABASE SmartCanteen;
END
GO

USE SmartCanteen;
GO

/* ===========================
   DROP TABLES (nếu đã tồn tại)
   =========================== */
IF OBJECT_ID('dbo.PaymentTransactions','U') IS NOT NULL DROP TABLE dbo.PaymentTransactions;
IF OBJECT_ID('dbo.OrderItems','U')            IS NOT NULL DROP TABLE dbo.OrderItems;
IF OBJECT_ID('dbo.PointsLedger','U')          IS NOT NULL DROP TABLE dbo.PointsLedger;
IF OBJECT_ID('dbo.UserNotifications','U')     IS NOT NULL DROP TABLE dbo.UserNotifications;
IF OBJECT_ID('dbo.Orders','U')                IS NOT NULL DROP TABLE dbo.Orders;
IF OBJECT_ID('dbo.Items','U')                 IS NOT NULL DROP TABLE dbo.Items;
IF OBJECT_ID('dbo.Categories','U')            IS NOT NULL DROP TABLE dbo.Categories;
IF OBJECT_ID('dbo.Voucher','U')               IS NOT NULL DROP TABLE dbo.Voucher;
IF OBJECT_ID('dbo.AuditLogs','U')             IS NOT NULL DROP TABLE dbo.AuditLogs;
IF OBJECT_ID('dbo.Users','U')                 IS NOT NULL DROP TABLE dbo.Users;
GO

/* ===============
   TABLE: Users
   =============== */
CREATE TABLE dbo.Users
(
    id             BIGINT IDENTITY(1,1)    NOT NULL,
    name           NVARCHAR(120)           NOT NULL,
    username       NVARCHAR(100)           NOT NULL,
    email          NVARCHAR(150)           NULL,        -- dùng cho OTP email / đăng nhập
    password_hash  NVARCHAR(255)           NULL,
    mssv           NVARCHAR(30)            NULL,
    class          NVARCHAR(30)            NULL,
    phone          NVARCHAR(20)            NULL,
    role           NVARCHAR(20)            NOT NULL,    -- USER | ADMIN
    allergies      NVARCHAR(255)           NULL,
    preferences    NVARCHAR(255)           NULL,
    is_active      BIT                     NOT NULL CONSTRAINT DF_Users_is_active DEFAULT(1),
    created_at     DATETIME2               NOT NULL CONSTRAINT DF_Users_created_at DEFAULT (SYSUTCDATETIME()),

    CONSTRAINT PK_Users PRIMARY KEY CLUSTERED (id)
);
-- unique username
CREATE UNIQUE INDEX UQ_Users_Username ON dbo.Users(username);
-- unique email nếu có (bỏ qua NULL)
CREATE UNIQUE INDEX UQ_Users_Email ON dbo.Users(email) WHERE email IS NOT NULL;
GO

/* =================
   TABLE: AuditLogs
   ================= */
CREATE TABLE dbo.AuditLogs
(
    id         BIGINT IDENTITY(1,1) NOT NULL,
    actor_id   BIGINT               NULL,        -- FK Users
    action     NVARCHAR(50)         NOT NULL,    -- CREATE / UPDATE / DELETE / LOGIN / ...
    entity     NVARCHAR(50)         NULL,        -- Users / Orders / Items / ...
    entity_id  BIGINT               NULL,
    detail     NVARCHAR(1000)       NULL,        -- JSON hoặc text mô tả
    created_at DATETIME2            NOT NULL CONSTRAINT DF_AuditLogs_created_at DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT PK_AuditLogs PRIMARY KEY CLUSTERED (id)
);
ALTER TABLE dbo.AuditLogs
  ADD CONSTRAINT FK_AuditLogs_Users
  FOREIGN KEY (actor_id) REFERENCES dbo.Users(id);
GO

/* =================
   TABLE: Categories
   ================= */
CREATE TABLE dbo.Categories
(
    id          BIGINT IDENTITY(1,1) NOT NULL,
    name        NVARCHAR(100)        NOT NULL,
    sort_order  INT                  NULL,
    is_active   BIT                  NOT NULL CONSTRAINT DF_Categories_is_active DEFAULT(1),
    created_at  DATETIME2            NOT NULL CONSTRAINT DF_Categories_created_at DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT PK_Categories PRIMARY KEY CLUSTERED (id)
);
-- unique name
CREATE UNIQUE INDEX UQ_Categories_Name ON dbo.Categories(name);
GO

/* =============
   TABLE: Items
   ============= */
CREATE TABLE dbo.Items
(
    id                 BIGINT IDENTITY(1,1) NOT NULL,
    name               NVARCHAR(120)        NOT NULL,
    price              DECIMAL(12,2)        NOT NULL,
    image_url          NVARCHAR(255)        NULL,
    category_id        BIGINT               NOT NULL,    -- FK Categories
    category           NVARCHAR(50)         NULL,        -- dự phòng text hiển thị (nếu dùng)
    is_available_today BIT                  NOT NULL CONSTRAINT DF_Items_is_available_today DEFAULT(1),
    created_at         DATETIME2            NOT NULL CONSTRAINT DF_Items_created_at DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT PK_Items PRIMARY KEY CLUSTERED (id),
    CONSTRAINT CK_Items_Price_Positive CHECK (price >= 0)
);
CREATE INDEX IX_Items_CategoryId ON dbo.Items(category_id);
ALTER TABLE dbo.Items
  ADD CONSTRAINT FK_Items_Categories
  FOREIGN KEY (category_id) REFERENCES dbo.Categories(id);
GO

/* =============
   TABLE: Orders
   ============= */
CREATE TABLE dbo.Orders
(
    id              BIGINT IDENTITY(1,1) NOT NULL,
    user_id         BIGINT               NOT NULL,        -- FK Users
    status          NVARCHAR(20)         NOT NULL,        -- PENDING | IN_PROGRESS | READY | PICKED_UP | CANCELLED
    payment_status  NVARCHAR(20)         NOT NULL CONSTRAINT DF_Orders_payment_status DEFAULT (N'UNPAID'), -- UNPAID | PAID | REFUNDED
    payment_method  NVARCHAR(20)         NULL,            -- CASH | INTERNAL_QR
    payment_ref     NVARCHAR(100)        NULL,            -- mã giao dịch/qr ref
    total           DECIMAL(12,2)        NOT NULL,
    eta_minutes     INT                  NULL,            -- thời gian dự kiến
    note            NVARCHAR(255)        NULL,
    created_at      DATETIME2            NOT NULL CONSTRAINT DF_Orders_created_at DEFAULT (SYSUTCDATETIME()),
    paid_at         DATETIME2            NULL,
    CONSTRAINT PK_Orders PRIMARY KEY CLUSTERED (id),
    CONSTRAINT CK_Orders_Total_Positive CHECK (total >= 0)
);
CREATE INDEX IX_Orders_UserId ON dbo.Orders(user_id);
ALTER TABLE dbo.Orders
  ADD CONSTRAINT FK_Orders_Users
  FOREIGN KEY (user_id) REFERENCES dbo.Users(id);
GO

/* ==================
   TABLE: OrderItems
   ================== */
CREATE TABLE dbo.OrderItems
(
    id        BIGINT IDENTITY(1,1) NOT NULL,
    order_id  BIGINT               NOT NULL,   -- FK Orders
    item_id   BIGINT               NOT NULL,   -- FK Items
    qty       INT                  NOT NULL,
    note      NVARCHAR(255)        NULL,
    CONSTRAINT PK_OrderItems PRIMARY KEY CLUSTERED (id),
    CONSTRAINT CK_OrderItems_Qty_Positive CHECK (qty > 0)
);
CREATE INDEX IX_OrderItems_OrderId ON dbo.OrderItems(order_id);
CREATE INDEX IX_OrderItems_ItemId  ON dbo.OrderItems(item_id);
ALTER TABLE dbo.OrderItems
  ADD CONSTRAINT FK_OrderItems_Orders
  FOREIGN KEY (order_id) REFERENCES dbo.Orders(id);
ALTER TABLE dbo.OrderItems
  ADD CONSTRAINT FK_OrderItems_Items
  FOREIGN KEY (item_id) REFERENCES dbo.Items(id);
GO

/* =========================
   TABLE: PaymentTransactions
   ========================= */
CREATE TABLE dbo.PaymentTransactions
(
    id         BIGINT IDENTITY(1,1) NOT NULL,
    order_id   BIGINT               NOT NULL,        -- FK Orders
    actor_id   BIGINT               NULL,            -- FK Users (ai thực hiện)
    method     NVARCHAR(20)         NOT NULL,        -- CASH | INTERNAL_QR
    action     NVARCHAR(20)         NOT NULL,        -- CHARGE | REFUND
    status     NVARCHAR(20)         NOT NULL,        -- PENDING | SUCCESS | FAIL
    amount     DECIMAL(12,2)        NOT NULL,
    ref_code   NVARCHAR(100)        NULL,
    created_at DATETIME2            NOT NULL CONSTRAINT DF_PayTrans_created_at DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT PK_PaymentTransactions PRIMARY KEY CLUSTERED (id)
);
CREATE INDEX IX_PayTrans_OrderId ON dbo.PaymentTransactions(order_id);
ALTER TABLE dbo.PaymentTransactions
  ADD CONSTRAINT FK_PaymentTransactions_Orders
  FOREIGN KEY (order_id) REFERENCES dbo.Orders(id);
ALTER TABLE dbo.PaymentTransactions
  ADD CONSTRAINT FK_PaymentTransactions_Users
  FOREIGN KEY (actor_id) REFERENCES dbo.Users(id);
GO

/* =================
   TABLE: PointsLedger
   ================= */
CREATE TABLE dbo.PointsLedger
(
    id         BIGINT IDENTITY(1,1) NOT NULL,
    user_id    BIGINT               NOT NULL,  -- FK Users
    order_id   BIGINT               NULL,      -- FK Orders
    delta      INT                  NOT NULL,  -- +/- điểm phát sinh
    points     INT                  NULL,      -- tổng điểm sau phát sinh (snapshot) - tuỳ chọn
    reason     NVARCHAR(255)        NULL,
    created_at DATETIME2            NOT NULL CONSTRAINT DF_PointsLedger_created_at DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT PK_PointsLedger PRIMARY KEY CLUSTERED (id)
);
ALTER TABLE dbo.PointsLedger
  ADD CONSTRAINT FK_PointsLedger_Users
  FOREIGN KEY (user_id) REFERENCES dbo.Users(id);
ALTER TABLE dbo.PointsLedger
  ADD CONSTRAINT FK_PointsLedger_Orders
  FOREIGN KEY (order_id) REFERENCES dbo.Orders(id);
GO

/* ======================
   TABLE: UserNotifications
   ====================== */
CREATE TABLE dbo.UserNotifications
(
    id            BIGINT IDENTITY(1,1) NOT NULL,
    user_id       BIGINT               NOT NULL, -- FK Users
    type          NVARCHAR(50)         NULL,     -- ORDER_STATUS | PROMO | SYSTEM...
    title         NVARCHAR(200)        NOT NULL,
    body          NVARCHAR(500)        NOT NULL,
    reference_id  BIGINT               NULL,     -- link đến order/voucher...
    is_read       BIT                  NOT NULL CONSTRAINT DF_UserNoti_is_read DEFAULT(0),
    created_at    DATETIME2            NOT NULL CONSTRAINT DF_UserNoti_created_at DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT PK_UserNotifications PRIMARY KEY CLUSTERED (id)
);
CREATE INDEX IX_UserNoti_UserId ON dbo.UserNotifications(user_id);
ALTER TABLE dbo.UserNotifications
  ADD CONSTRAINT FK_UserNotifications_Users
  FOREIGN KEY (user_id) REFERENCES dbo.Users(id);
GO

/* ===============
   TABLE: Voucher
   =============== */
CREATE TABLE dbo.Voucher
(
    id         BIGINT IDENTITY(1,1) NOT NULL,
    code       NVARCHAR(50)         NOT NULL,
    type       NVARCHAR(20)         NOT NULL,     -- PERCENT | FIXED
    value      DECIMAL(12,2)        NOT NULL,     -- % hoặc số tiền
    start_at   DATETIME2            NULL,
    end_at     DATETIME2            NULL,
    quota      INT                  NULL,         -- số lượt tối đa
    used       INT                  NOT NULL CONSTRAINT DF_Voucher_used DEFAULT(0),
    created_at DATETIME2            NOT NULL CONSTRAINT DF_Voucher_created_at DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT PK_Voucher PRIMARY KEY CLUSTERED (id)
);
CREATE UNIQUE INDEX UQ_Voucher_Code ON dbo.Voucher(code);
GO

/* ========================
   SEED DATA MẪU (an toàn)
   ======================== */

-- Users (mật khẩu đã băm SHA-256)
-- admin: admin123
-- user:  12345678
INSERT INTO dbo.Users (name, username, email, password_hash, phone, role, is_active)
VALUES
(N'Administrator', N'admin', N'admin@smartcanteen.local', N'240be518fabd2724ddb6f04eeb1da5967448d7e831c08c8fa822809f74c720a9', N'0900000001', N'ADMIN', 1),
(N'Nguyen Van A', N'sv001', N'sv001@example.com',     N'ef797c8118f02dfb649607dd5d3f8c7623048c9c063d532cc95c5ed7a898a64f', N'0900000002', N'USER',  1);

-- Categories
INSERT INTO dbo.Categories (name, sort_order) VALUES
(N'Cơm', 1),
(N'Bún/Phở', 2),
(N'Mì/Nùi', 3),
(N'Nước uống', 4),
(N'Tráng miệng', 5);

-- Items (giá VND)
INSERT INTO dbo.Items (name, price, image_url, category_id, category)
VALUES
(N'Cơm gà xối mỡ',   45000, NULL, 1, N'Cơm'),
(N'Cơm sườn bì chả', 42000, NULL, 1, N'Cơm'),
(N'Bún bò Huế',      38000, NULL, 2, N'Bún/Phở'),
(N'Phở bò tái',      40000, NULL, 2, N'Bún/Phở'),
(N'Mì xào bò',       42000, NULL, 3, N'Mì/Nùi'),
(N'Nùi xào hải sản',45000, NULL, 3, N'Mì/Nùi'),
(N'Trà tắc',         12000, NULL, 4, N'Nước uống'),
(N'Cà phê sữa',      18000, NULL, 4, N'Nước uống'),
(N'Sting dâu',       15000, NULL, 4, N'Nước uống'),
(N'Chè đậu xanh',    15000, NULL, 5, N'Tráng miệng'),
(N'Chè bưởi',        18000, NULL, 5, N'Tráng miệng'),
(N'Rau câu dừa',     15000, NULL, 5, N'Tráng miệng');

-- Vouchers
-- NEW10: giảm 10% | FIX5K: giảm 5.000đ
INSERT INTO dbo.Voucher (code, type, value, start_at, end_at, quota)
VALUES
('NEW10', 'PERCENT', 10.00, DATEADD(DAY,-1,SYSUTCDATETIME()), DATEADD(MONTH,1,SYSUTCDATETIME()), 100),
('FIX5K', 'FIXED',   5000.00, DATEADD(DAY,-1,SYSUTCDATETIME()), DATEADD(MONTH,1,SYSUTCDATETIME()), 200);

-- Audit log mẫu
INSERT INTO dbo.AuditLogs (actor_id, action, entity, entity_id, detail)
VALUES
(1, N'CREATE', N'Items', 1, N'Seed item'),
(1, N'CREATE', N'Voucher', 1, N'Seed voucher NEW10');

-- (Tuỳ chọn) seed điểm thưởng ban đầu cho user sv001
INSERT INTO dbo.PointsLedger (user_id, order_id, delta, points, reason)
VALUES (2, NULL, 20, 20, N'Đăng ký tài khoản tặng điểm');

-- Thông báo mẫu
INSERT INTO dbo.UserNotifications (user_id, type, title, body, reference_id)
VALUES
(2, N'SYSTEM', N'Chào mừng đến Smart Canteen', N'Bạn đã nhận 20 điểm thưởng.', NULL);
GO

PRINT 'SmartCanteen schema & seed completed.';











##pacakage crtl+` chạy
dotnet add package Microsoft.EntityFrameworkCore.SqlServer --version 8.0.10
dotnet add package Microsoft.EntityFrameworkCore.Tools --version 8.0.10
dotnet add package Microsoft.EntityFrameworkCore.Design --version 8.0.10
dotnet add package Microsoft.AspNetCore.Authentication.JwtBearer --version 8.0.10
dotnet add package Swashbuckle.AspNetCore --version 6.6.2
dotnet add package QRCoder --version 1.5.0
dotnet add package QuestPDF --version 2024.5.2
dotnet add package MailKit









